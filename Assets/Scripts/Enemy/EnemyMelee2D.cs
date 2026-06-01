using UnityEngine;

// 플레이어를 추적하고 전조-돌진-회복 구조로 공격하는 기본 침식체 AI
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMelee2D : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;

    [Header("Target")]
    [Tooltip("추적 대상 Transform. 비어 있으면 playerTag로 자동 탐색")]
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
    [Tooltip("공격을 준비하기 시작하는 거리")]
    [SerializeField] private float attackRange = 1.0f;
    [Tooltip("돌진 전 멈춰서 전조를 보여주는 시간(초)")]
    [SerializeField] private float attackWindup = 0.5f;
    [Tooltip("회복이 끝난 뒤 다음 공격까지 기다리는 시간(초)")]
    [SerializeField] private float attackCooldown = 0.25f;
    [Tooltip("1회 공격 데미지")]
    [SerializeField] private int attackDamage = 12;
    [Tooltip("돌진 중 전방 피해 판정 반경")]
    [SerializeField] private float hitRadius = 0.6f;
    [Tooltip("돌진 방향 앞쪽으로 피해 판정 중심을 미는 거리")]
    [SerializeField] private float hitForwardOffset = 0.75f;
    [Tooltip("전조 때 고정한 방향으로 짧게 돌진하는 거리")]
    [SerializeField] private float lungeDistance = 1.8f;
    [Tooltip("돌진에 걸리는 시간(초)")]
    [SerializeField] private float lungeDuration = 0.22f;
    [Tooltip("돌진 뒤 멈춰서 반격 빈틈을 주는 시간(초)")]
    [SerializeField] private float attackRecoveryTime = 0.5f;

    private Rigidbody2D _rb;
    private bool _isPreparingAttack;
    private bool _isLunging;
    private float _attackWindupEndTime;
    private float _lungeStartTime;
    private float _lungeEndTime;
    private float _recoveryEndTime;
    private float _nextAttackReadyTime;
    private Vector2 _cachedAttackDirection = Vector2.right;
    private Vector2 _lungeStartPosition;
    private Vector2 _lungeEndPosition;
    private Vector2 _lastHitCenter;
    private float _lastHitRadius;
    private bool _hasLastHit;
    private bool _hasDamagedThisLunge;

    // 넉백 중 AI 이동 차단에 사용
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
        attackWindup = enemyData.AttackWindup;
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

        if (_knockbackReceiver != null && _knockbackReceiver.IsKnockedBack)
        {
            return;
        }

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

        if (Time.time >= _nextAttackReadyTime)
        {
            StartAttackWindup();
        }
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

        Debug.Log(
            $"[EnemyMelee2D] 침식체 공격 전조 시작 | target={target.name}, direction={_cachedAttackDirection}, windup={attackWindup}",
            this
        );
        Debug.DrawRay(origin, _cachedAttackDirection * attackRange, Color.yellow, attackWindup);
    }

    private void StartAttackLunge()
    {
        _rb.linearVelocity = Vector2.zero;

        float safeLungeDuration = Mathf.Max(0.01f, lungeDuration);
        _lungeStartTime = Time.time;
        _lungeEndTime = Time.time + safeLungeDuration;
        _lungeStartPosition = _rb.position;
        _lungeEndPosition = _lungeStartPosition + _cachedAttackDirection.normalized * Mathf.Max(0f, lungeDistance);
        _hasDamagedThisLunge = false;
        _isLunging = true;

        Debug.Log(
            $"[EnemyMelee2D] 침식체 돌진 시작 | direction={_cachedAttackDirection}, distance={lungeDistance}, duration={safeLungeDuration}",
            this
        );
        Debug.DrawLine(_lungeStartPosition, _lungeEndPosition, Color.red, safeLungeDuration);
    }

    private void UpdateAttackLunge()
    {
        _rb.linearVelocity = Vector2.zero;

        float duration = Mathf.Max(0.01f, _lungeEndTime - _lungeStartTime);
        float progress = Mathf.Clamp01((Time.time - _lungeStartTime) / duration);
        _rb.MovePosition(Vector2.Lerp(_lungeStartPosition, _lungeEndPosition, progress));
        TryDealLungeDamage();

        if (progress < 1f)
        {
            return;
        }

        _isLunging = false;
        _recoveryEndTime = Time.time + Mathf.Max(0.01f, attackRecoveryTime);
        _nextAttackReadyTime = _recoveryEndTime + Mathf.Max(0f, attackCooldown);

        Debug.Log(
            $"[EnemyMelee2D] 침식체 회복 시작 | recovery={attackRecoveryTime}, nextCooldown={attackCooldown}, hit={_hasDamagedThisLunge}",
            this
        );
    }

    private void TryDealLungeDamage()
    {
        if (_hasDamagedThisLunge)
        {
            return;
        }

        if (!IsValidTarget(target))
        {
            target = null;
            return;
        }

        target = NormalizeTarget(target);

        EnsurePlayerLayerConfigured();

        Vector2 origin = transform.position;
        Vector2 attackDirection = _cachedAttackDirection.sqrMagnitude > 0.0001f
            ? _cachedAttackDirection.normalized
            : Vector2.right;

        float safeHitRadius = Mathf.Max(0f, hitRadius);
        Vector2 hitCenter = origin + attackDirection * Mathf.Max(0f, hitForwardOffset);
        _lastHitCenter = hitCenter;
        _lastHitRadius = safeHitRadius;
        _hasLastHit = true;

        Debug.DrawLine(origin, hitCenter, Color.red, 0.1f);

        int damagedCount = MeleeHitResolver2D.DealDamageInRange(
            hitCenter,
            safeHitRadius,
            attackDamage,
            playerLayer,
            this,
            "EnemyMelee2D",
            CombatAttribute.Justice
        );

        if (damagedCount <= 0)
        {
            return;
        }

        _hasDamagedThisLunge = true;
        Debug.Log(
            $"[EnemyMelee2D] 침식체 돌진 적중 | target={target.name}, damage={attackDamage}, hitCenter={hitCenter}, hitRadius={safeHitRadius}",
            this
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

        if (_isPreparingAttack)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + _cachedAttackDirection * attackRange);
        }

        if (!_hasLastHit)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_lastHitCenter, _lastHitRadius);
    }
}
