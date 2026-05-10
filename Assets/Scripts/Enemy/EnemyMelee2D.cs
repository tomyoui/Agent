using UnityEngine;

// 플레이어를 추적하고 범위 내 진입 시 근접 공격하는 적 AI
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMelee2D : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;

    [Header("Target")]
    [Tooltip("추적할 대상 Transform (미할당 시 playerTag로 자동 탐색)")]
    private Transform target;
    [Tooltip("플레이어를 자동 탐색할 때 사용하는 태그")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Enemy attack overlap uses this mask to find player colliders.")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Chase")]
    [Tooltip("플레이어 추적 이동 속도")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float separationRadius = 0.6f;
    [SerializeField] private float separationWeight = 0.75f;

    [Header("Attack")]
    [Tooltip("공격이 시작되는 거리")]
    [SerializeField] private float attackRange = 1.0f;
    [Tooltip("공격 전 선딜 시간 (초)")]
    [SerializeField] private float attackWindup = 0.4f;
    [Tooltip("공격 후 쿨다운 시간 (초)")]
    [SerializeField] private float attackCooldown = 1.0f;
    [Tooltip("1회 공격 데미지")]
    [SerializeField] private int attackDamage = 5;
    [Tooltip("실제 타격 원 반경")]
    [SerializeField] private float hitRadius = 0.6f;
    [Tooltip("공격 방향으로 판정 중심을 미는 거리")]
    [SerializeField] private float hitForwardOffset = 0.9f;
    [Tooltip("공격 준비 후 고정 방향으로 짧게 돌진하는 거리")]
    [SerializeField] private float lungeDistance = 0.9f;
    [Tooltip("돌진에 걸리는 시간 (초)")]
    [SerializeField] private float lungeDuration = 0.18f;
    [Tooltip("돌진 공격 후 추가 회복 시간 (초)")]
    [SerializeField] private float attackRecoveryTime = 0.28f;

    private Rigidbody2D _rb;
    private bool _isPreparingAttack;
    private bool _isLunging;
    private float _attackWindupEndTime;
    private float _lungeStartTime;
    private float _lungeEndTime;
    private float _recoveryEndTime;
    private Vector2 _cachedAttackDirection = Vector2.right;
    private Vector2 _lungeStartPosition;
    private Vector2 _lungeEndPosition;
    private Vector2 _lastHitCenter;
    private float _lastHitRadius;
    private bool _hasLastHit;

    // 넉백 중 AI 이동 차단을 위해 참조 보관 (null-safe: 없어도 동작)
    private KnockbackReceiver2D _knockbackReceiver;

    private void Awake()
    {
        ApplyEnemyData();

        _rb = GetComponent<Rigidbody2D>();
        _knockbackReceiver = GetComponent<KnockbackReceiver2D>();
        EnsurePlayerLayerConfigured();
        target = null;
        ResolveTarget();
    }

    private void ApplyEnemyData()
    {
        if (enemyData == null)
        {
            return;
        }

        attackDamage = enemyData.AttackDamage;
        moveSpeed = enemyData.MoveSpeed;
        attackRange = enemyData.AttackRange;
        attackCooldown = enemyData.AttackCooldown;
    }

    private void FixedUpdate()
    {
        if (!IsValidTarget(target))
        {
            target = null;
        }
        else
        {
            target = NormalizeTarget(target);
        }

        // 넉백 중: velocity를 절대 덮어쓰지 않고 그냥 return.
        // AddForce(Impulse)로 부여된 velocity를 물리 엔진이 직접 소멸시키도록 둬야 함.
        // 이 블록에 linearVelocity = zero를 넣으면 매 FixedUpdate마다 넉백이 즉시 취소됨.
        if (_knockbackReceiver != null && _knockbackReceiver.IsKnockedBack)
            return;

        // 경직 중(넉백은 끝남): 제자리에 멈춰 있어야 하므로 velocity = zero.
        if (_knockbackReceiver != null && _knockbackReceiver.IsStaggered)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!ResolveTarget())
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_isPreparingAttack)
        {
            _rb.linearVelocity = Vector2.zero;

            if (Time.time >= _attackWindupEndTime)
            {
                _isPreparingAttack = false;
                StartAttackLunge();
            }

            return;
        }

        if (_isLunging)
        {
            UpdateAttackLunge();
            return;
        }

        if (Time.time < _recoveryEndTime)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = target.position - transform.position;
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget > attackRange)
        {
            Vector2 desiredDirection = toTarget.normalized;
            Vector2 separation = CalculateSeparation();
            Vector2 moveDir = (desiredDirection + separation * separationWeight).normalized;
            _rb.linearVelocity = moveDir * moveSpeed;
            return;
        }

        _rb.linearVelocity = Vector2.zero;
        StartAttackWindup();
    }

    private Vector2 CalculateSeparation()
    {
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, separationRadius, enemyLayer);
        Vector2 separation = Vector2.zero;

        for (int i = 0; i < nearbyEnemies.Length; i++)
        {
            Collider2D enemy = nearbyEnemies[i];
            if (enemy == null || enemy.transform == transform || enemy.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector2 awayFromEnemy = (Vector2)transform.position - (Vector2)enemy.transform.position;
            float distance = awayFromEnemy.magnitude;
            if (distance <= 0.0001f)
            {
                continue;
            }

            float closeness = Mathf.Clamp01((separationRadius - distance) / separationRadius);
            separation += awayFromEnemy.normalized * closeness;
        }

        return separation;
    }

    private void StartAttackWindup()
    {
        Vector2 origin = transform.position;
        Vector2 toTarget = GetTargetPoint(target) - origin;
        _cachedAttackDirection = toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized
            : Vector2.right;

        _isPreparingAttack = true;
        _attackWindupEndTime = Time.time + Mathf.Max(0.01f, attackWindup);
    }

    private void StartAttackLunge()
    {
        _rb.linearVelocity = Vector2.zero;

        float safeLungeDuration = Mathf.Max(0.01f, lungeDuration);
        _lungeStartTime = Time.time;
        _lungeEndTime = Time.time + safeLungeDuration;
        _lungeStartPosition = _rb.position;
        _lungeEndPosition = _lungeStartPosition + _cachedAttackDirection.normalized * Mathf.Max(0f, lungeDistance);
        _isLunging = true;
    }

    private void UpdateAttackLunge()
    {
        _rb.linearVelocity = Vector2.zero;

        float duration = Mathf.Max(0.01f, _lungeEndTime - _lungeStartTime);
        float progress = Mathf.Clamp01((Time.time - _lungeStartTime) / duration);
        _rb.MovePosition(Vector2.Lerp(_lungeStartPosition, _lungeEndPosition, progress));

        if (progress < 1f)
        {
            return;
        }

        _isLunging = false;
        ExecuteAttack();
        _recoveryEndTime = Time.time + Mathf.Max(0.01f, attackCooldown + attackRecoveryTime);
    }

    private void ExecuteAttack()
    {
        if (!IsValidTarget(target))
        {
            target = null;
            return;
        }

        target = NormalizeTarget(target);

        EnsurePlayerLayerConfigured();

        Vector2 origin = transform.position;
        Vector2 targetPoint = GetTargetPoint(target);
        Vector2 attackDirection = _cachedAttackDirection.sqrMagnitude > 0.0001f
            ? _cachedAttackDirection.normalized
            : Vector2.right;

        float safeHitRadius = Mathf.Max(0f, hitRadius);
        Vector2 hitCenter = origin + attackDirection * Mathf.Max(0f, hitForwardOffset);
        _lastHitCenter = hitCenter;
        _lastHitRadius = safeHitRadius;
        _hasLastHit = true;

        Debug.Log(
            $"[EnemyMelee2D] 공격 실행 | origin={origin}, target={target.name}, targetPoint={targetPoint}, " +
            $"attackDirection={attackDirection}, hitCenter={hitCenter}, attackRange={attackRange}, hitRadius={safeHitRadius}",
            this
        );
        Debug.DrawLine(origin, hitCenter, Color.red, 0.5f);

        MeleeHitResolver2D.DealDamageInRange(
            hitCenter,
            safeHitRadius,
            attackDamage,
            playerLayer,
            this,
            "EnemyMelee2D",
            CombatAttribute.Justice   // 현재 적 근접 공격 = 정의 속성 (임시). 적별 속성 추가 시 Inspector 필드로 분리
        );
    }

    private Vector2 GetTargetPoint(Transform candidate)
    {
        if (candidate == null)
        {
            return transform.position;
        }

        Collider2D targetCollider = candidate.GetComponentInChildren<Collider2D>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return candidate.position;
    }

    private bool ResolveTarget()
    {
        if (PartyManager2D.Instance != null)
        {
            GameObject current = PartyManager2D.Instance.CurrentMember;
            target = IsTargetCandidate(current) ? NormalizeTarget(current.transform) : null;
        }
        else if (target == null)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
            for (int i = 0; i < players.Length; i++)
            {
                if (IsTargetCandidate(players[i]))
                {
                    target = NormalizeTarget(players[i].transform);
                    break;
                }
            }
        }

        return target != null;
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        Health health = FindTargetHealth(candidate);
        return health != null && !health.IsDead;
    }

    private bool IsTargetCandidate(GameObject candidate)
    {
        return candidate != null
            && IsValidTarget(candidate.transform)
            && IsInPlayerLayer(candidate);
    }

    private Health FindTargetHealth(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return null;
        }

        Health health = candidate.GetComponent<Health>();
        if (health != null)
        {
            return health;
        }

        health = candidate.GetComponentInParent<Health>(true);
        if (health != null)
        {
            return health;
        }

        return candidate.GetComponentInChildren<Health>(true);
    }

    private Transform NormalizeTarget(Transform candidate)
    {
        Health health = FindTargetHealth(candidate);
        return health != null ? health.transform.root : candidate;
    }

    private bool IsInPlayerLayer(GameObject candidate)
    {
        EnsurePlayerLayerConfigured();
        return candidate != null && (playerLayer.value & (1 << candidate.layer)) != 0;
    }

    private void EnsurePlayerLayerConfigured()
    {
        if (playerLayer.value != 0)
        {
            return;
        }

        int layer = LayerMask.NameToLayer("Player");
        if (layer >= 0)
        {
            playerLayer = 1 << layer;
        }
    }

    private void OnValidate()
    {
        EnsurePlayerLayerConfigured();
    }

    private void OnDisable()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (!_hasLastHit)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_lastHitCenter, _lastHitRadius);
    }
}
