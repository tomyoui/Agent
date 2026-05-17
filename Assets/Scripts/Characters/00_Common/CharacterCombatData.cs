using UnityEngine;

[CreateAssetMenu(menuName = "Agent/Character Combat Data", fileName = "CharacterCombatData")]
public class CharacterCombatData : ScriptableObject
{
    [Header("Damage")]
    [SerializeField, Min(1)] private int basicAttackDamage = 9;
    [SerializeField, Min(1)] private int heavyAttackDamage = 18;
    [SerializeField, Min(0)] private int skillDamage;
    [SerializeField, Min(0)] private int ultimateDamage;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float attackCooldown = 0.25f;
    [SerializeField, Min(0f)] private float skillCooldown = 5f;
    [SerializeField, Min(0f)] private float heavyAttackCooldown;
    [SerializeField, Min(0f)] private float heavyAttackStartupDelay;
    [SerializeField, Min(0f)] private float heavyAttackRecoveryDelay;

    [Header("Range")]
    [SerializeField, Min(0f)] private float attackRange = 0.9f;
    [SerializeField, Min(0f)] private float skillRange = 1.5f;
    [SerializeField, Min(0f)] private float heavyAttackRange = 1.4f;

    [Header("Hit Feedback")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.03f;
    [SerializeField, Min(0f)] private float knockbackPower = 4f;

    public int BasicAttackDamage => basicAttackDamage;
    public int HeavyAttackDamage => heavyAttackDamage;
    public int SkillDamage => skillDamage;
    public int UltimateDamage => ultimateDamage;
    public float AttackCooldown => attackCooldown;
    public float SkillCooldown => skillCooldown;
    public float HeavyAttackCooldown => heavyAttackCooldown;
    public float HeavyAttackStartupDelay => heavyAttackStartupDelay;
    public float HeavyAttackRecoveryDelay => heavyAttackRecoveryDelay;
    public float AttackRange => attackRange;
    public float SkillRange => skillRange;
    public float HeavyAttackRange => heavyAttackRange;
    public float HitStopDuration => hitStopDuration;
    public float KnockbackPower => knockbackPower;
}
