using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat2D : MonoBehaviour
{
    [Serializable]
    public class ComboEvent : UnityEvent<int> { }

    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float heavyAttackRange = 1.4f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private int heavyDamage = 25;

    [Header("Combo Step 1")]
    [SerializeField] private int combo1Damage = 8;
    [SerializeField] private float combo1Range = 0.9f;
    [SerializeField] private float combo1Angle = 70f;

    [Header("Combo Step 2")]
    [SerializeField] private int combo2Damage = 12;
    [SerializeField] private float combo2Range = 1.1f;
    [SerializeField] private float combo2Angle = 95f;

    [Header("Combo Step 3")]
    [SerializeField] private int combo3Damage = 18;
    [SerializeField] private float combo3Range = 1.3f;
    [SerializeField] private float combo3Angle = 120f;

    [Header("Combo Timing")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float comboInputWindow = 0.35f;
    [SerializeField] private float comboResetDelay = 0.8f;
    [SerializeField] private float attackCooldown = 0.25f;

    [Header("Heavy Attack")]
    [SerializeField] private float heavyHoldThreshold = 0.4f;
    [SerializeField] private float heavyAttackAngle = 110f;

    [Header("Aim")]
    [SerializeField] private float attackPointDistance = 0.9f;

    [Header("Events")]
    [SerializeField] private ComboEvent onComboAttack;
    [SerializeField] private UnityEvent onHeavyAttack;

    private PlayerInput _playerInput;
    private InputAction _attackAction;

    private int _currentComboStep;
    private float _lastComboTime;
    private float _attackPressStartTime;
    private float _nextComboAttackTime;
    private Vector2 _lastAimDirection = Vector2.right;
    private Camera _mainCamera;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _attackAction = _playerInput.actions.FindAction("Attack", true);
        _mainCamera = Camera.main;

        if (targetLayer.value == 0)
        {
            Debug.LogWarning("[PlayerCombat2D] targetLayer is empty. No enemies will be hit until a layer is assigned.", this);
        }
    }

    private void OnEnable()
    {
        _attackAction.started += OnAttackStarted;
        _attackAction.canceled += OnAttackCanceled;
    }

    private void OnDisable()
    {
        _attackAction.started -= OnAttackStarted;
        _attackAction.canceled -= OnAttackCanceled;
    }

    private void Update()
    {
        UpdateAttackPointFromMouse();

        if (_currentComboStep > 0 && Time.time - _lastComboTime > comboResetDelay)
        {
            _currentComboStep = 0;
        }
    }

    private void UpdateAttackPointFromMouse()
    {
        if (attackPoint == null)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        float depthFromCamera = Mathf.Abs(transform.position.z - _mainCamera.transform.position.z);

        Vector3 mouseWorldPosition = _mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, depthFromCamera)
        );

        Vector2 rawAimDirection = (Vector2)(mouseWorldPosition - transform.position);
        if (rawAimDirection.sqrMagnitude > 0.0001f)
        {
            _lastAimDirection = rawAimDirection.normalized;
        }

        attackPoint.position = (Vector2)transform.position + (_lastAimDirection * attackPointDistance);
    }

    private void OnAttackStarted(InputAction.CallbackContext context)
    {
        _attackPressStartTime = Time.time;
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        float heldTime = Time.time - _attackPressStartTime;

        if (heldTime >= heavyHoldThreshold)
        {
            TriggerHeavyAttack();
            return;
        }

        TriggerComboAttack();
    }

    private void TriggerComboAttack()
    {
        if (Time.time < _nextComboAttackTime)
        {
            return;
        }

        if (Time.time - _lastComboTime > comboInputWindow)
        {
            _currentComboStep = 0;
        }

        _currentComboStep = (_currentComboStep % maxComboStep) + 1;
        _lastComboTime = Time.time;
        _nextComboAttackTime = Time.time + Mathf.Max(0.01f, attackCooldown);

        GetComboStepStats(_currentComboStep, out int damage, out float range, out float angle);
        PerformAttack(range, damage, angle, $"Combo {_currentComboStep}");
        onComboAttack?.Invoke(_currentComboStep);
    }

    private void TriggerHeavyAttack()
    {
        _currentComboStep = 0;
        _lastComboTime = 0f;

        PerformAttack(heavyAttackRange, heavyDamage, heavyAttackAngle, "Heavy");
        onHeavyAttack?.Invoke();
    }

    private void PerformAttack(float range, int damage, float attackAngle, string attackType)
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("[PlayerCombat2D] attackPoint is not assigned.", this);
            return;
        }

        Vector2 origin = transform.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        float coneRange = GetConeRange(range);
        float halfAngle = attackAngle * 0.5f;
        float minDot = Mathf.Cos(Mathf.Deg2Rad * halfAngle);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, coneRange, targetLayer);
        Debug.Log($"[PlayerCombat2D] {attackType} attack found {hits.Length} collider(s).", this);

        HashSet<Health> damagedTargets = new HashSet<Health>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            Health health = hit.GetComponentInParent<Health>();

            if (health == null)
            {
                Debug.Log($"[PlayerCombat2D] Collider '{hit.name}' has no Health component.", hit);
                continue;
            }

            if (!damagedTargets.Add(health))
            {
                continue;
            }

            Vector2 targetPoint = hit.ClosestPoint(origin);
            Vector2 toTarget = targetPoint - origin;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = (Vector2)health.transform.position - origin;
            }

            if (toTarget.sqrMagnitude > coneRange * coneRange)
            {
                continue;
            }

            Vector2 toTargetDirection = toTarget.normalized;
            float dot = Vector2.Dot(aimDirection, toTargetDirection);
            if (dot < minDot)
            {
                continue;
            }

            Debug.Log($"[PlayerCombat2D] {attackType} hit target: {health.gameObject.name}", health);
            health.TakeDamage(damage);
        }
    }

    private Vector2 GetCurrentAimDirection()
    {
        Vector2 aimDirection = _lastAimDirection;
        if (attackPoint != null)
        {
            Vector2 fromPlayerToAttackPoint = (Vector2)attackPoint.position - (Vector2)transform.position;
            if (fromPlayerToAttackPoint.sqrMagnitude > 0.0001f)
            {
                aimDirection = fromPlayerToAttackPoint.normalized;
            }
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            aimDirection = Vector2.right;
        }

        return aimDirection;
    }

    private float GetConeRange(float attackRange)
    {
        return Mathf.Max(0f, attackPointDistance + attackRange);
    }

    private void GetComboStepStats(int comboStep, out int damage, out float range, out float angle)
    {
        switch (comboStep)
        {
            case 1:
                damage = combo1Damage;
                range = combo1Range;
                angle = combo1Angle;
                break;
            case 2:
                damage = combo2Damage;
                range = combo2Range;
                angle = combo2Angle;
                break;
            case 3:
                damage = combo3Damage;
                range = combo3Range;
                angle = combo3Angle;
                break;
            default:
                damage = combo1Damage;
                range = combo1Range;
                angle = combo1Angle;
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        float coneRange = GetConeRange(combo3Range);
        float halfAngle = combo3Angle * 0.5f;

        Vector2 leftEdge = Quaternion.Euler(0f, 0f, -halfAngle) * aimDirection;
        Vector2 rightEdge = Quaternion.Euler(0f, 0f, halfAngle) * aimDirection;

        // Player origin marker.
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);

        // Current attack direction.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + (Vector3)(aimDirection * coneRange));

        // Cone edge lines.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + (Vector3)(leftEdge * coneRange));
        Gizmos.DrawLine(origin, origin + (Vector3)(rightEdge * coneRange));

        // Cone arc.
        const int arcSegments = 24;
        Vector3 previousPoint = origin + (Vector3)(leftEdge * coneRange);
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 arcDirection = Quaternion.Euler(0f, 0f, angle) * aimDirection;
            Vector3 currentPoint = origin + (Vector3)(arcDirection * coneRange);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
