using UnityEngine;

/// <summary>
/// StatBlock을 읽어 최종 스탯을 계산·제공하는 컴포넌트.
/// 플레이어·적이 공통으로 사용.
///
/// 계산 공식:
///   FinalAttack (float) = base × (1 + percent) + flat   ← 정수 변환 없음
///   FinalHP / FinalDefense (int) = RoundToInt(base × (1 + percent)) + flat
///
/// [확장 포인트]
///   버프/장비 합산: AddModifier(StatBlock mod) / RemoveModifier(StatBlock mod) 추가 후
///   FinalAttack 등 프로퍼티에서 modifiers 리스트를 순회해 합산.
///   현재 MVP에서는 단일 baseStats만 사용.
/// </summary>
[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    [Header("기본 스탯 블록")]
    [SerializeField] private StatBlock baseStats = new StatBlock();

    // ── 최종 스탯 프로퍼티 ────────────────────────────────────────────────

    public int FinalHP =>
        Mathf.Max(1, Mathf.RoundToInt(baseStats.baseHP * (1f + baseStats.hpPercent)) + baseStats.flatHP);

    // 외부 버프(궁극기 등)에서 일시적으로 곱하는 공격력 배율. 기본값 1(배율 없음).
    private float _attackBuffMultiplier = 1f;
    public float AttackBuffMultiplier
    {
        get => _attackBuffMultiplier;
        set => _attackBuffMultiplier = Mathf.Max(0f, value);
    }

    // float 유지 — 계수·보너스 계산 중 반올림 손실 방지.
    // 최종 int 변환은 DamageFormula.Calculate() 마지막에서만 수행.
    public float FinalAttack =>
        Mathf.Max(0f, (baseStats.baseAttack * (1f + baseStats.attackPercent) + baseStats.flatAttack) * _attackBuffMultiplier);

    public int FinalDefense =>
        Mathf.Max(0, Mathf.RoundToInt(baseStats.baseDefense * (1f + baseStats.defensePercent)) + baseStats.flatDefense);

    public float FinalCritRate    => Mathf.Clamp01(baseStats.critRate);
    public float FinalCritDamage  => Mathf.Max(0f, baseStats.critDamage);

    // 피해 보너스 — DamageFormula에서 읽는다
    public float DamageBonus          => baseStats.damageBonus;
    public float MeleeDamageBonus     => baseStats.meleeDamageBonus;
    public float RangedDamageBonus    => baseStats.rangedDamageBonus;
    public float SkillDamageBonus     => baseStats.skillDamageBonus;
    public float UltimateDamageBonus  => baseStats.ultimateDamageBonus;
    public float JusticeDamageBonus   => baseStats.justiceDamageBonus;
    public float DoomDamageBonus      => baseStats.doomDamageBonus;

    // 날 블록 직접 접근 (에디터/디버그/세이브용)
    public StatBlock BaseStats => baseStats;
}
