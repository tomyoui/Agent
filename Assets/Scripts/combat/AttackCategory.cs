/// <summary>
/// 공격 분류 — 해당 카테고리 피해 보너스를 결정.
/// DamageFormula에서 CharacterStats의 보너스 필드와 매핑됨.
/// </summary>
public enum AttackCategory
{
    Melee,     // 근접 — MeleeDamageBonus 적용
    Ranged,    // 원거리 — RangedDamageBonus 적용
    Skill,     // 스킬 — SkillDamageBonus 적용
    Ultimate,  // 궁극기 — UltimateDamageBonus 적용
}
