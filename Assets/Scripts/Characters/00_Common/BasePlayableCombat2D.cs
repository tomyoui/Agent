using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Common combat base for playable characters.
/// - Holds CharacterStats for shared damage calculation.
/// - Owns the shared ultimate gauge state.
/// - Leaves actual ultimate behavior to character-specific overrides.
/// </summary>
public abstract class BasePlayableCombat2D : MonoBehaviour
{
    [Serializable]
    public class ComboEvent : UnityEvent<int> { }

    [Header("Ultimate Gauge")]
    [FormerlySerializedAs("maxUltimateGauge")]
    [SerializeField, Min(1f)] protected float ultimateMax = 100f;

    [SerializeField, Min(0f)] protected float basicAttackHitGain = 5f;
    [SerializeField, Min(0f)] protected float skillHitGain = 15f;

    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float heavyAttackRange = 1.4f;
    [SerializeField] protected LayerMask targetLayer;

    [Header("Attack Definitions")]
    [SerializeField] private AttackDefinition combo1Attack = new AttackDefinition { coefficient = 0.90f };
    [SerializeField] private AttackDefinition combo2Attack = new AttackDefinition { coefficient = 1.05f };
    [SerializeField] private AttackDefinition combo3Attack = new AttackDefinition { coefficient = 1.50f };
    [SerializeField] private AttackDefinition heavyAttackDef = new AttackDefinition { coefficient = 1.80f };

    [Header("Combo Step 1")]
    [SerializeField] private float combo1Range = 0.9f;
    [SerializeField] private float combo1Angle = 70f;

    [Header("Combo Step 2")]
    [SerializeField] private float combo2Range = 1.1f;
    [SerializeField] private float combo2Angle = 95f;

    [Header("Combo Step 3")]
    [SerializeField] private float combo3Range = 1.3f;
    [SerializeField] private float combo3Angle = 120f;

    [Header("Combo Timing")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float comboInputWindow = 0.35f;
    [SerializeField] private float comboResetDelay = 0.8f;
    [SerializeField] protected float attackCooldown = 0.25f;

    [Header("Heavy Attack")]
    [SerializeField] private float heavyHoldThreshold = 0.4f;
    [SerializeField] private float heavyAttackAngle = 110f;

    [Header("Aim")]
    [SerializeField] private float attackPointDistance = 0.9f;

    [Header("Audio")]
    [SerializeField] private AudioSource hitAudioSource;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private AudioClip[] heavySwingClips;

    [Header("Hit Audio")]
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] private AudioClip gunHitSfx;
    [SerializeField] private AudioClip meleeHitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.9f;

    [Header("Hit Stop")]
    [SerializeField] private float hitStopDuration = 0.05f;

    [Header("Hit Stop Combo Variants")]
    [SerializeField] private float combo1HitStop = 0.03f;
    [SerializeField] private float combo2HitStop = 0.05f;
    [SerializeField] private float combo3HitStop = 0.10f;
    [SerializeField] private float heavyHitStop = 0.12f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackDuration = 0.20f;

    [Header("Knockback Combo Variants")]
    [SerializeField] private float combo1KnockbackForce = 4f;
    [SerializeField] private float combo2KnockbackForce = 6f;
    [SerializeField] private float combo3KnockbackForce = 9f;
    [SerializeField] private float heavyKnockbackForce = 12f;

    [Header("Camera Shake")]
    [SerializeField] private float combo1ShakeIntensity = 0.04f;
    [SerializeField] private float combo2ShakeIntensity = 0.07f;
    [SerializeField] private float combo3ShakeIntensity = 0.12f;
    [SerializeField] private float heavyShakeIntensity = 0.15f;

    [Header("Hit Stagger")]
    [SerializeField] private float combo1StaggerDuration = 0.12f;
    [SerializeField] private float combo2StaggerDuration = 0.18f;
    [SerializeField] private float combo3StaggerDuration = 0.26f;
    [SerializeField] private float heavyStaggerDuration = 0.35f;

    [Header("Attack Slow")]
    [SerializeField, Range(0f, 1f)] private float combo1SlowMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)] private float combo2SlowMultiplier = 0.28f;
    [SerializeField, Range(0f, 1f)] private float combo3SlowMultiplier = 0.18f;
    [SerializeField, Range(0f, 1f)] private float heavySlowMultiplier = 0.08f;
    [SerializeField] private float combo1SlowDuration = 0.10f;
    [SerializeField] private float combo2SlowDuration = 0.13f;
    [SerializeField] private float combo3SlowDuration = 0.17f;
    [SerializeField] private float heavySlowDuration = 0.24f;

    [Header("Events")]
    [SerializeField] private ComboEvent onComboAttack;
    [SerializeField] private UnityEvent onHeavyAttack;

    protected CharacterStats _stats;
    protected float ultimateGauge;
    protected float _baseAttackCooldown;

    private int _currentComboStep;
    private float _lastComboTime;
    private float _attackPressStartTime;
    private float _nextComboAttackTime;
    private Vector2 _lastAimDirection = Vector2.right;
    protected Camera _mainCamera;
    private bool _hasLoggedMissingAttackPoint;
    private Coroutine _hitStopRoutine;
    private Coroutine _attackSlowRoutine;

    private CameraFollow2D _cameraShake;
    protected PlayerController2D _controller;

    public float UltimateGauge => ultimateGauge;
    public float UltimateMax => ultimateMax;
    public float UltimateGaugeRatio => ultimateMax > 0f ? ultimateGauge / ultimateMax : 0f;
    public float CurrentUltimateGauge => ultimateGauge;
    public bool IsUltimateReady => CanUseUltimate();
    public virtual bool IsPrimaryCombat => true;

    protected virtual void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        if (_stats == null)
        {
            Debug.LogWarning($"[{GetType().Name}] CharacterStats is missing. Damage falls back to flat attack values.", this);
        }

        if (!IsPrimaryCombat)
        {
            return;
        }

        _baseAttackCooldown = attackCooldown;
        _mainCamera = Camera.main;
        hitAudioSource = hitAudioSource != null ? hitAudioSource : GetComponent<AudioSource>();
        audioSource = audioSource != null ? audioSource : GetComponent<AudioSource>();
        ResolveAttackPoint();

        _cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (_cameraShake == null)
        {
            Debug.LogWarning("[ArrivalCombat2D] CameraFollow2D not found. Camera shake is disabled.", this);
        }

        _controller = GetComponent<PlayerController2D>();

        if (targetLayer.value == 0)
        {
            Debug.LogWarning("[ArrivalCombat2D] targetLayer is empty.", this);
        }
    }

    protected virtual void OnValidate()
    {
        if (!IsPrimaryCombat)
        {
            return;
        }

        ResolveAttackPoint();
    }

    protected virtual void OnDisable()
    {
        if (!IsPrimaryCombat)
        {
            return;
        }

        if (_controller != null)
        {
            _controller.AttackSpeedMultiplier = 1f;
        }
    }

    protected virtual void Update()
    {
        if (!IsPrimaryCombat)
        {
            return;
        }

        ResolveAttackPoint();
        UpdateAttackPointFromMouse();

        if (_currentComboStep > 0 && Time.time - _lastComboTime > comboResetDelay)
        {
            _currentComboStep = 0;
        }
    }

    protected int CalculateSkillDamage(AttackDefinition attackDef, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    protected int CalculateSkillDamage(AttackDefinition attackDef, CharacterStats defender, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef, defender)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    public bool CanUseUltimate()
    {
        return ultimateGauge >= ultimateMax;
    }

    public virtual void RequestAttack()
    {
        TriggerComboAttack();
    }

    public virtual void RequestSkill()
    {
        PlayerRangedAttack2D rangedAttack = GetComponent<PlayerRangedAttack2D>();
        if (rangedAttack != null)
        {
            rangedAttack.TryFire();
        }
    }

    public virtual void RequestUltimate()
    {
        TryTriggerUltimate();
    }

    public virtual void RequestHeavyAttackStart()
    {
        _attackPressStartTime = Time.time;
    }

    public virtual void RequestHeavyAttackRelease()
    {
        float heldTime = Time.time - _attackPressStartTime;
        if (heldTime >= heavyHoldThreshold)
        {
            TriggerHeavyAttack();
        }
    }

    public void AddUltimateGauge(float amount)
    {
        if (amount <= 0f || ultimateMax <= 0f)
        {
            return;
        }

        float before = ultimateGauge;
        ultimateGauge = Mathf.Clamp(ultimateGauge + amount, 0f, ultimateMax);

        if (!Mathf.Approximately(before, ultimateGauge))
        {
            Debug.Log($"[{GetType().Name}] Ultimate gauge +{amount:0.##}: {before:0.##}/{ultimateMax:0.##} -> {ultimateGauge:0.##}/{ultimateMax:0.##}", this);
        }
    }

    public float GetUltimateGauge()
    {
        return ultimateGauge;
    }

    public float GetUltimateMax()
    {
        return ultimateMax;
    }

    public float GetUltimateGaugeRatio()
    {
        return UltimateGaugeRatio;
    }

    protected bool TryConsumeUltimateGauge()
    {
        if (!CanUseUltimate())
        {
            return false;
        }

        ultimateGauge = 0f;
        Debug.Log($"[{GetType().Name}] Ultimate gauge consumed.", this);
        return true;
    }

    public bool TryTriggerUltimate()
    {
        if (!TryConsumeUltimateGauge())
        {
            Debug.Log($"[{GetType().Name}] Ultimate gauge is not ready ({ultimateGauge:0.##}/{ultimateMax:0.##}).", this);
            return false;
        }

        Debug.Log($"[{GetType().Name}] Ultimate used.", this);
        ExecuteUltimate();
        return true;
    }

    public virtual void ExecuteUltimate() { }

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

        float aimAngle = Mathf.Atan2(_lastAimDirection.y, _lastAimDirection.x) * Mathf.Rad2Deg;
        attackPoint.rotation = Quaternion.Euler(0f, 0f, aimAngle);
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

        AttackDefinition attackDef = GetComboAttackDef(_currentComboStep);
        int damage = CalculateSkillDamage(attackDef, fallbackFlatAttack: 10);
        GetComboStepGeometry(_currentComboStep, out float range, out float angle);
        ApplyAttackSlow(_currentComboStep);
        PerformAttack(range, damage, angle, attackDef.attribute, $"Combo {_currentComboStep}", _currentComboStep);
        onComboAttack?.Invoke(_currentComboStep);
    }

    private void TriggerHeavyAttack()
    {
        _currentComboStep = 0;
        _lastComboTime = 0f;

        PlayRandomHeavySwingSound();
        int heavyDmg = CalculateSkillDamage(heavyAttackDef, fallbackFlatAttack: 14);
        ApplyAttackSlow(comboStep: 0);
        PerformAttack(heavyAttackRange, heavyDmg, heavyAttackAngle, heavyAttackDef.attribute, "Heavy", 0);
        onHeavyAttack?.Invoke();
    }

    private void PerformAttack(float range, int damage, float attackAngle, CombatAttribute attribute, string attackType, int comboStep = 1)
    {
        if (!ResolveAttackPoint())
        {
            return;
        }

        Vector2 origin = attackPoint.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        int damagedCount = MeleeHitResolver2D.DealDamageInCone(
            origin,
            aimDirection,
            attackPointDistance,
            range,
            attackAngle,
            damage,
            targetLayer,
            this,
            $"ArrivalCombat2D/{attackType}",
            attribute
        );

        if (damagedCount > 0)
        {
            ApplyKnockbackToHitTargets(origin, aimDirection, range, attackAngle, GetKnockbackForce(comboStep), GetStaggerDuration(comboStep));
            PlayHitSfx(isMelee: true);
            TriggerHitStop(GetHitStopDuration(comboStep));
            _cameraShake?.Shake(GetShakeIntensity(comboStep));

            if (comboStep >= 0)
            {
                AddUltimateGauge(basicAttackHitGain);
            }
        }
    }

    private bool ResolveAttackPoint()
    {
        if (attackPoint != null)
        {
            _hasLoggedMissingAttackPoint = false;
            return true;
        }

        Transform found = transform.Find("AttackPoint");
        if (found != null)
        {
            attackPoint = found;
            _hasLoggedMissingAttackPoint = false;
            return true;
        }

        if (!_hasLoggedMissingAttackPoint)
        {
            Debug.LogError("[ArrivalCombat2D] Missing AttackPoint transform.", this);
            _hasLoggedMissingAttackPoint = true;
        }

        return false;
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

    private AttackDefinition GetComboAttackDef(int comboStep)
    {
        switch (comboStep)
        {
            case 1: return combo1Attack;
            case 2: return combo2Attack;
            default: return combo3Attack;
        }
    }

    private void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        switch (comboStep)
        {
            case 1:
                range = combo1Range;
                angle = combo1Angle;
                break;
            case 2:
                range = combo2Range;
                angle = combo2Angle;
                break;
            default:
                range = combo3Range;
                angle = combo3Angle;
                break;
        }
    }

    public virtual void PlayHitSfx(bool isMelee)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("[ArrivalCombat2D] AudioSource is not assigned.", this);
            return;
        }

        AudioClip clip = isMelee ? meleeHitSfx : gunHitSfx;
        if (clip == null)
        {
            Debug.LogWarning("[ArrivalCombat2D] Hit SFX clip is not assigned.", this);
            return;
        }

        audioSource.PlayOneShot(clip, hitSfxVolume);
    }

    private void PlayRandomHeavySwingSound()
    {
        if (hitAudioSource == null)
        {
            Debug.LogWarning("[ArrivalCombat2D] Heavy swing skipped: missing AudioSource.", this);
            return;
        }

        if (heavySwingClips == null || heavySwingClips.Length == 0)
        {
            Debug.LogWarning("[ArrivalCombat2D] Heavy swing skipped: no clips configured.", this);
            return;
        }

        int clipIndex = UnityEngine.Random.Range(0, heavySwingClips.Length);
        AudioClip clip = heavySwingClips[clipIndex];
        if (clip == null)
        {
            Debug.LogWarning($"[ArrivalCombat2D] Heavy swing skipped: clip {clipIndex} is null.", this);
            return;
        }

        hitAudioSource.PlayOneShot(clip);
    }

    private void ApplyKnockbackToHitTargets(Vector2 origin, Vector2 aimDirection, float range, float attackAngle, float forceOverride = -1f, float staggerDuration = 0f)
    {
        float force = forceOverride >= 0f ? forceOverride : knockbackForce;
        if (force <= 0f || knockbackDuration <= 0f)
        {
            return;
        }

        float coneRange = MeleeHitResolver2D.GetConeRange(attackPointDistance, range);
        float halfAngle = Mathf.Clamp(attackAngle * 0.5f, 0f, 180f);
        float minDot = Mathf.Cos(Mathf.Deg2Rad * halfAngle);
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, coneRange, targetLayer);
        HashSet<KnockbackReceiver2D> knockedTargets = new HashSet<KnockbackReceiver2D>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null && hit.transform.root != null)
            {
                damageable = hit.transform.root.GetComponentInChildren<IDamageable>();
            }

            if (damageable == null)
            {
                continue;
            }

            Vector2 targetPoint = hit.ClosestPoint(origin);
            Vector2 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = (Vector2)hit.transform.position - origin;
            }

            if (toTarget.sqrMagnitude > coneRange * coneRange)
            {
                continue;
            }

            float dot = Vector2.Dot(aimDirection, toTarget.normalized);
            if (dot < minDot)
            {
                continue;
            }

            KnockbackReceiver2D knockbackReceiver = hit.GetComponentInParent<KnockbackReceiver2D>();
            if (knockbackReceiver == null && hit.transform.root != null)
            {
                knockbackReceiver = hit.transform.root.GetComponentInChildren<KnockbackReceiver2D>();
            }

            if (knockbackReceiver == null)
            {
                Debug.LogWarning($"[ArrivalCombat2D] {hit.gameObject.name} has no KnockbackReceiver2D.", hit.gameObject);
                continue;
            }

            if (!knockedTargets.Add(knockbackReceiver))
            {
                continue;
            }

            knockbackReceiver.ApplyKnockback(toTarget.normalized, force, knockbackDuration);

            if (staggerDuration > 0f)
            {
                knockbackReceiver.ApplyStagger(staggerDuration);
            }
        }
    }

    public virtual void TriggerHitStop(float durationOverride = -1f)
    {
        float duration = durationOverride >= 0f ? durationOverride : hitStopDuration;
        if (duration <= 0f)
        {
            return;
        }

        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            Time.timeScale = 1f;
        }

        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        _hitStopRoutine = null;
    }

    private float GetHitStopDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyHitStop;
            case 1: return combo1HitStop;
            case 2: return combo2HitStop;
            default: return combo3HitStop;
        }
    }

    private float GetKnockbackForce(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyKnockbackForce;
            case 1: return combo1KnockbackForce;
            case 2: return combo2KnockbackForce;
            default: return combo3KnockbackForce;
        }
    }

    private float GetShakeIntensity(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyShakeIntensity;
            case 1: return combo1ShakeIntensity;
            case 2: return combo2ShakeIntensity;
            default: return combo3ShakeIntensity;
        }
    }

    private float GetStaggerDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyStaggerDuration;
            case 1: return combo1StaggerDuration;
            case 2: return combo2StaggerDuration;
            default: return combo3StaggerDuration;
        }
    }

    private void ApplyAttackSlow(int comboStep)
    {
        if (_controller == null)
        {
            return;
        }

        float multiplier = GetSlowMultiplier(comboStep);
        float duration = GetSlowDuration(comboStep);

        if (_attackSlowRoutine != null)
        {
            StopCoroutine(_attackSlowRoutine);
        }

        _attackSlowRoutine = StartCoroutine(AttackSlowRoutine(multiplier, duration));
    }

    private IEnumerator AttackSlowRoutine(float multiplier, float duration)
    {
        _controller.AttackSpeedMultiplier = multiplier;
        yield return new WaitForSecondsRealtime(duration);
        _controller.AttackSpeedMultiplier = 1f;
        _attackSlowRoutine = null;
    }

    private float GetSlowMultiplier(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavySlowMultiplier;
            case 1: return combo1SlowMultiplier;
            case 2: return combo2SlowMultiplier;
            default: return combo3SlowMultiplier;
        }
    }

    private float GetSlowDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavySlowDuration;
            case 1: return combo1SlowDuration;
            case 2: return combo2SlowDuration;
            default: return combo3SlowDuration;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!IsPrimaryCombat)
        {
            return;
        }

        Vector3 origin = transform.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        float coneRange = MeleeHitResolver2D.GetConeRange(attackPointDistance, combo3Range);
        float halfAngle = combo3Angle * 0.5f;

        Vector2 leftEdge = Quaternion.Euler(0f, 0f, -halfAngle) * aimDirection;
        Vector2 rightEdge = Quaternion.Euler(0f, 0f, halfAngle) * aimDirection;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + (Vector3)(aimDirection * coneRange));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + (Vector3)(leftEdge * coneRange));
        Gizmos.DrawLine(origin, origin + (Vector3)(rightEdge * coneRange));

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
