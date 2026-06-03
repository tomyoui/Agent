using System.Collections;
using UnityEngine;

public class ArrivalCombat2D : BasePlayableCombat2D
{
    [Header("Arrival 전투 감각")]
    [SerializeField, Min(0.01f)] private float duelAttackCooldown = 0.23f;
    [SerializeField, Min(0f)] private float duelCombo1Range = 1.05f;
    [SerializeField, Min(0f)] private float duelCombo2Range = 1.12f;
    [SerializeField, Min(0f)] private float duelCombo3Range = 1.22f;
    [SerializeField, Min(0f)] private float duelCombo1Angle = 80f;
    [SerializeField, Min(0f)] private float duelCombo2Angle = 95f;
    [SerializeField, Min(0f)] private float duelCombo3Angle = 110f;
    [SerializeField, Min(0f)] private float duelHeavyRange = 1.65f;
    [SerializeField, Min(1f)] private float duelHeavyDamageMultiplier = 1.25f;
    [SerializeField, Min(0f)] private float duelHitStop = 0.035f;
    [SerializeField, Min(0f)] private float duelHeavyHitStop = 0.055f;
    [SerializeField, Min(0f)] private float duelKnockback = 4.5f;
    [SerializeField, Min(0f)] private float duelHeavyKnockback = 6.5f;

    [Header("Ultimate Buff")]
    [SerializeField] private float ultimateBuffDuration = 8f;
    [SerializeField] private float ultimateAttackMultiplier = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float ultimateCooldownMultiplier = 0.5f;

    private Coroutine _ultimateBuffRoutine;
    private bool _isUltimateBuffActive;

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetUltimateBuff();
    }

    public override void ExecuteUltimate()
    {
        if (_ultimateBuffRoutine != null)
        {
            StopCoroutine(_ultimateBuffRoutine);
            ResetUltimateBuff();
        }

        _ultimateBuffRoutine = StartCoroutine(UltimateBuffRoutine());
    }

    protected override float GetAttackCooldown()
    {
        return Mathf.Max(0.01f, duelAttackCooldown);
    }

    protected override void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        switch (comboStep)
        {
            case 1:
                range = duelCombo1Range;
                angle = duelCombo1Angle;
                break;
            case 2:
                range = duelCombo2Range;
                angle = duelCombo2Angle;
                break;
            default:
                range = duelCombo3Range;
                angle = duelCombo3Angle;
                break;
        }
    }

    protected override int GetHeavyAttackDamage(AttackDefinition attackDef)
    {
        int baseDamage = base.GetHeavyAttackDamage(attackDef);
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * duelHeavyDamageMultiplier));
    }

    protected override float GetHeavyAttackRange()
    {
        return duelHeavyRange;
    }

    protected override float GetHitStopDuration(int comboStep)
    {
        return comboStep == 0 ? duelHeavyHitStop : duelHitStop;
    }

    protected override float GetKnockbackForce(int comboStep)
    {
        return comboStep == 0 ? duelHeavyKnockback : duelKnockback;
    }

    private IEnumerator UltimateBuffRoutine()
    {
        ApplyUltimateBuff();

        Debug.Log(
            $"[ArrivalCombat2D] Ultimate buff start. attack x{ultimateAttackMultiplier:0.##}, cooldown x{ultimateCooldownMultiplier:0.##}, duration {ultimateBuffDuration:0.##}s",
            this);

        yield return new WaitForSeconds(ultimateBuffDuration);

        ResetUltimateBuff();
    }

    private void ApplyUltimateBuff()
    {
        _isUltimateBuffActive = true;

        if (_stats != null)
        {
            _stats.AttackBuffMultiplier = ultimateAttackMultiplier;
        }

        attackCooldown = Mathf.Max(0.01f, _baseAttackCooldown * ultimateCooldownMultiplier);
    }

    private void ResetUltimateBuff()
    {
        if (!_isUltimateBuffActive)
        {
            return;
        }

        if (_stats != null)
        {
            _stats.AttackBuffMultiplier = 1f;
        }

        attackCooldown = _baseAttackCooldown > 0f ? _baseAttackCooldown : attackCooldown;
        _isUltimateBuffActive = false;
        _ultimateBuffRoutine = null;
        Debug.Log("[ArrivalCombat2D] Ultimate buff end", this);
    }
}
