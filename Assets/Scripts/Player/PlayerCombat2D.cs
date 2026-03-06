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
    [SerializeField] private float comboAttackRange = 1.0f;
    [SerializeField] private float heavyAttackRange = 1.4f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private int comboDamage = 10;
    [SerializeField] private int heavyDamage = 25;

    [Header("Combo")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float comboInputWindow = 0.35f;
    [SerializeField] private float comboResetDelay = 0.8f;

    [Header("Heavy Attack")]
    [SerializeField] private float heavyHoldThreshold = 0.4f;

    [Header("Events")]
    [SerializeField] private ComboEvent onComboAttack;
    [SerializeField] private UnityEvent onHeavyAttack;

    private PlayerInput _playerInput;
    private InputAction _attackAction;

    private int _currentComboStep;
    private float _lastComboTime;
    private float _attackPressStartTime;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _attackAction = _playerInput.actions.FindAction("Attack", true);

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
        if (_currentComboStep > 0 && Time.time - _lastComboTime > comboResetDelay)
        {
            _currentComboStep = 0;
        }
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
        if (Time.time - _lastComboTime > comboInputWindow)
        {
            _currentComboStep = 0;
        }

        _currentComboStep = (_currentComboStep % maxComboStep) + 1;
        _lastComboTime = Time.time;

        PerformAttack(comboAttackRange, comboDamage, "Combo");
        onComboAttack?.Invoke(_currentComboStep);
    }

    private void TriggerHeavyAttack()
    {
        _currentComboStep = 0;
        _lastComboTime = 0f;

        PerformAttack(heavyAttackRange, heavyDamage, "Heavy");
        onHeavyAttack?.Invoke();
    }

    private void PerformAttack(float range, int damage, string attackType)
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("[PlayerCombat2D] attackPoint is not assigned.", this);
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, range, targetLayer);
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

            Debug.Log($"[PlayerCombat2D] {attackType} hit target: {health.gameObject.name}", health);
            health.TakeDamage(damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPoint.position, comboAttackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, heavyAttackRange);
    }
}