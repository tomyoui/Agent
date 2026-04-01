using UnityEngine;

/// <summary>
/// 최종 피해 계산 담당 정적 유틸리티.
///
/// [계산 순서]
///   1. 기본 피해   = FinalAttack × coefficient
///   2. 보너스 합   = DamageBonus + CategoryBonus + AttributeBonus
///   3. 보너스 적용 = 기본 피해 × (1 + 보너스 합)
///   4. 치명타 판정 → 치명타 배율 적용
///   5. 방어 감소   = 100 / (100 + defenderDefense)
///   6. 최종 피해   = Floor(결과), 최소 1
///
/// [방어 공식 참고]
///   방어 0   → 100% 피해 (감소 없음)
///   방어 100 → 50%  피해
///   방어 300 → 25%  피해
///   원신 방식보다 단순화. 추후 방어 무시/내성 추가 시 이 함수에만 파라미터 추가.
///
/// [확장 포인트]
///   - 속성 내성: defenderResistance(float) 파라미터 추가
///   - 방어 무시: defPenetrationPercent 파라미터 추가
///   - 받는 피해 감소: defenderDamageReduction 파라미터 추가
/// </summary>
public static class DamageFormula
{
    /// <summary>
    /// 공격자 스탯 + 공격 정의로 최종 피해 계산.
    /// defender가 null이면 방어 감소 없이 계산 (보스 무시, 테스트 등).
    /// </summary>
    public static int Calculate(CharacterStats attacker, AttackDefinition attack, CharacterStats defender = null)
    {
        if (attacker == null || attack == null)
        {
            Debug.LogWarning("[DamageFormula] attacker 또는 attack이 null입니다. 0 반환.");
            return 0;
        }

        // ── 1. 기본 피해 ─────────────────────────────────────────────────
        float baseDamage = attacker.FinalAttack * attack.coefficient;

        // ── 2. 보너스 합산 ───────────────────────────────────────────────
        float bonus = attacker.DamageBonus;
        bonus += GetCategoryBonus(attacker, attack.category);
        bonus += GetAttributeBonus(attacker, attack.attribute);

        float afterBonus = baseDamage * (1f + bonus);

        // ── 3. 치명타 판정 ───────────────────────────────────────────────
        bool isCrit = Random.value < attacker.FinalCritRate;
        float afterCrit = isCrit ? afterBonus * (1f + attacker.FinalCritDamage) : afterBonus;

        // 치명타 디버그 로그 — 너무 시끄러우면 이 블록 주석 처리
        if (isCrit)
        {
            Debug.Log($"[DamageFormula] CRIT! attacker={attacker.name}" +
                      $" | coeff={attack.coefficient:F2}" +
                      $" | before={afterBonus:F1} → after={afterCrit:F1}" +
                      $" | critRate={attacker.FinalCritRate:P0}, critDmg=+{attacker.FinalCritDamage:P0}");
        }

        // ── 4. 방어 감소 ─────────────────────────────────────────────────
        float defReduction = 1f;
        if (defender != null)
        {
            int def = Mathf.Max(0, defender.FinalDefense);
            defReduction = 100f / (100f + def);
        }

        float finalDamage = afterCrit * defReduction;

        // ── 5. 정수 변환, 최소 1 보장 ────────────────────────────────────
        return Mathf.Max(1, Mathf.FloorToInt(finalDamage));
    }

    /// <summary>
    /// CharacterStats 없이 flatAttack 수치만으로 계산하는 브리지 오버로드.
    /// 적이 아직 CharacterStats를 붙이기 전 임시로 사용하거나, 단순 테스트용.
    /// </summary>
    public static int CalculateFlat(int flatAttack, AttackDefinition attack)
    {
        if (attack == null) return Mathf.Max(1, flatAttack);
        return Mathf.Max(1, Mathf.FloorToInt(flatAttack * attack.coefficient));
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────

    private static float GetCategoryBonus(CharacterStats stats, AttackCategory category)
    {
        switch (category)
        {
            case AttackCategory.Melee:    return stats.MeleeDamageBonus;
            case AttackCategory.Ranged:   return stats.RangedDamageBonus;
            case AttackCategory.Skill:    return stats.SkillDamageBonus;
            case AttackCategory.Ultimate: return stats.UltimateDamageBonus;
            default:                      return 0f;
        }
    }

    private static float GetAttributeBonus(CharacterStats stats, CombatAttribute attribute)
    {
        switch (attribute)
        {
            case CombatAttribute.Justice: return stats.JusticeDamageBonus;
            case CombatAttribute.Doom:    return stats.DoomDamageBonus;
            default:                      return 0f;
        }
    }
}
