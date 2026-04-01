using System;
using UnityEngine;

/// <summary>
/// 캐릭터의 날 스탯 데이터 컨테이너.
/// CharacterStats가 이 블록을 읽어 최종값을 계산한다.
///
/// [확장 방향]
///   장비/버프/패시브 → 추가 StatBlock을 만들어 CharacterStats.AddModifier()로 합산.
///   레벨 성장 → baseAttack/baseHP를 레벨 테이블에서 설정.
/// </summary>
[Serializable]
public class StatBlock
{
    [Header("기본 스탯")]
    [Tooltip("기초 최대 HP")]
    [SerializeField] public int baseHP = 100;
    [Tooltip("기초 공격력")]
    [SerializeField] public int baseAttack = 10;
    [Tooltip("기초 방어력")]
    [SerializeField] public int baseDefense = 5;

    [Header("퍼센트 보정  (0.0 = 0%,  0.5 = +50%)")]
    [Tooltip("최대 HP 퍼센트 증가")]
    [SerializeField, Range(-1f, 5f)] public float hpPercent = 0f;
    [Tooltip("공격력 퍼센트 증가")]
    [SerializeField, Range(-1f, 5f)] public float attackPercent = 0f;
    [Tooltip("방어력 퍼센트 증가")]
    [SerializeField, Range(-1f, 5f)] public float defensePercent = 0f;

    [Header("고정 보정 (Flat)")]
    [Tooltip("최대 HP 고정 증가")]
    [SerializeField] public int flatHP = 0;
    [Tooltip("공격력 고정 증가")]
    [SerializeField] public int flatAttack = 0;
    [Tooltip("방어력 고정 증가")]
    [SerializeField] public int flatDefense = 0;

    [Header("치명타")]
    [Tooltip("치명타 확률 (0~1). 0.05 = 5%")]
    [SerializeField, Range(0f, 1f)] public float critRate = 0.05f;
    [Tooltip("치명타 추가 피해 배율. 0.5 = +50%, 1.0 = +100%")]
    [SerializeField, Range(0f, 5f)] public float critDamage = 0.5f;

    [Header("전체 피해 보너스")]
    [Tooltip("모든 공격에 적용되는 피해 보너스. 0.1 = +10%")]
    [SerializeField, Range(0f, 5f)] public float damageBonus = 0f;

    [Header("공격 카테고리 보너스")]
    [Tooltip("근접(Melee) 공격 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float meleeDamageBonus = 0f;
    [Tooltip("원거리(Ranged) 공격 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float rangedDamageBonus = 0f;
    [Tooltip("스킬(Skill) 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float skillDamageBonus = 0f;
    [Tooltip("궁극기(Ultimate) 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float ultimateDamageBonus = 0f;

    [Header("속성 보너스")]
    [Tooltip("정의(Justice) 속성 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float justiceDamageBonus = 0f;
    [Tooltip("파멸(Doom) 속성 피해 보너스")]
    [SerializeField, Range(0f, 5f)] public float doomDamageBonus = 0f;
}
