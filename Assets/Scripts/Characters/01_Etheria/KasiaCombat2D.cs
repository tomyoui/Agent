using UnityEngine;

public class KasiaCombat2D : BasePlayableCombat2D
{
    [Header("Kasia Basic Attack")]
    [SerializeField] private AttackDefinition basicAttack = new AttackDefinition { coefficient = 2.4f };
    [SerializeField] private float fallbackAttackRange = 1.6f;
    [SerializeField] private float fallbackAttackAngle = 115f;
    [SerializeField] private float fallbackAttackCooldown = 0.55f;
    [SerializeField] private float fallbackHitStop = 0.08f;
    [SerializeField] private float fallbackKnockbackPower = 7f;

    protected override int GetMaxComboStep()
    {
        return 1;
    }

    protected override float GetAttackCooldown()
    {
        return combatData != null ? combatData.AttackCooldown : fallbackAttackCooldown;
    }

    protected override AttackDefinition GetComboAttackDef(int comboStep)
    {
        return basicAttack;
    }

    protected override void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        range = combatData != null ? combatData.AttackRange : fallbackAttackRange;
        angle = fallbackAttackAngle;
    }

    protected override float GetHitStopDuration(int comboStep)
    {
        return combatData != null ? combatData.HitStopDuration : fallbackHitStop;
    }

    protected override float GetKnockbackForce(int comboStep)
    {
        return combatData != null ? combatData.KnockbackPower : fallbackKnockbackPower;
    }
}
