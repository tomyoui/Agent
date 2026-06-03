using UnityEngine;

public class AgnieszkaCombat2D : BasePlayableCombat2D
{
    [Header("Agnieszka 전투 감각")]
    [SerializeField, Min(0.01f)] private float controlAttackCooldown = 0.2f;
    [SerializeField, Min(0f)] private float controlCombo1Range = 1.75f;
    [SerializeField, Min(0f)] private float controlCombo2Range = 1.95f;
    [SerializeField, Min(0f)] private float controlCombo3Range = 2.15f;
    [SerializeField, Min(0f)] private float controlCombo1Angle = 38f;
    [SerializeField, Min(0f)] private float controlCombo2Angle = 44f;
    [SerializeField, Min(0f)] private float controlCombo3Angle = 52f;
    [SerializeField, Min(0f)] private float controlHeavyRange = 2.85f;
    [SerializeField, Min(1f)] private float controlHeavyDamageMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float controlHitStop = 0.025f;
    [SerializeField, Min(0f)] private float controlHeavyHitStop = 0.04f;
    [SerializeField, Min(0f)] private float controlKnockback = 2.25f;
    [SerializeField, Min(0f)] private float controlHeavyKnockback = 3f;

    protected override float GetAttackCooldown()
    {
        return Mathf.Max(0.01f, controlAttackCooldown);
    }

    protected override void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        switch (comboStep)
        {
            case 1:
                range = controlCombo1Range;
                angle = controlCombo1Angle;
                break;
            case 2:
                range = controlCombo2Range;
                angle = controlCombo2Angle;
                break;
            default:
                range = controlCombo3Range;
                angle = controlCombo3Angle;
                break;
        }
    }

    protected override int GetHeavyAttackDamage(AttackDefinition attackDef)
    {
        int baseDamage = base.GetHeavyAttackDamage(attackDef);
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * controlHeavyDamageMultiplier));
    }

    protected override float GetHeavyAttackRange()
    {
        return controlHeavyRange;
    }

    protected override float GetHitStopDuration(int comboStep)
    {
        return comboStep == 0 ? controlHeavyHitStop : controlHitStop;
    }

    protected override float GetKnockbackForce(int comboStep)
    {
        return comboStep == 0 ? controlHeavyKnockback : controlKnockback;
    }
}
