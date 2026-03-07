using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMelee2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";

    [Header("Chase")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private int attackDamage = 5;

    private Rigidbody2D _rb;
    private Health _targetHealth;
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
        if (_targetHealth == null)
        {
            _targetHealth = target.GetComponentInParent<Health>();
        }

        if (_targetHealth == null)
        {
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        if (distanceToTarget <= attackRange)
        {
            _targetHealth.TakeDamage(attackDamage);
        }
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
            _targetHealth = null;
            return false;
        }

        if (_targetHealth == null)
        {
            _targetHealth = target.GetComponentInParent<Health>();
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
