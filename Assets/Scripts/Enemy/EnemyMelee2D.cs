using UnityEngine;

// 플레이어를 추적하고 범위 내 진입 시 근접 공격하는 적 AI
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMelee2D : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("추적할 대상 Transform (미할당 시 playerTag로 자동 탐색)")]
    [SerializeField] private Transform target;
    [Tooltip("플레이어를 자동 탐색할 때 사용하는 태그")]
    [SerializeField] private string playerTag = "Player";

    [Header("Chase")]
    [Tooltip("플레이어 추적 이동 속도")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Attack")]
    [Tooltip("공격이 시작되는 거리")]
    [SerializeField] private float attackRange = 1.0f;
    [Tooltip("공격 전 선딜 시간 (초)")]
    [SerializeField] private float attackWindup = 0.2f;
    [Tooltip("공격 후 쿨다운 시간 (초)")]
    [SerializeField] private float attackCooldown = 1.0f;
    [Tooltip("1회 공격 데미지")]
    [SerializeField] private int attackDamage = 5;

    private Rigidbody2D _rb;
    private bool _isPreparingAttack;
    private float _attackWindupEndTime;
    private float _recoveryEndTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        ResolveTarget();
    }

    private void FixedUpdate()
    {
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
                ExecuteAttack();
                _isPreparingAttack = false;
                _recoveryEndTime = Time.time + Mathf.Max(0.01f, attackCooldown);
            }

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
            Vector2 moveDir = toTarget.normalized;
            _rb.linearVelocity = moveDir * moveSpeed;
            return;
        }

        _rb.linearVelocity = Vector2.zero;
        StartAttackWindup();
    }

    private void StartAttackWindup()
    {
        _isPreparingAttack = true;
        _attackWindupEndTime = Time.time + Mathf.Max(0.01f, attackWindup);
    }

    private void ExecuteAttack()
    {
        if (target == null)
        {
            return;
        }

        LayerMask targetMask = 1 << target.gameObject.layer;

        MeleeHitResolver2D.DealDamageInRange(
            transform.position,
            attackRange,
            attackDamage,
            targetMask,
            this,
            "EnemyMelee2D"
        );
    }

    private bool ResolveTarget()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target == null)
        {
            return false;
        }

        return true;
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
    }
}
