using System;
using UnityEngine;

/// <summary>
/// 하나의 공격 동작(스킬·콤보·탄환 등)의 계수 데이터.
/// 캐릭터 스탯과 완전히 분리되어 "이 공격은 공격력의 몇 배를 때리는가"를 정의.
///
/// [사용 예 — Inspector 기본값 기준]
///   combo1Attack : coefficient = 0.90, Melee, Justice
///   combo2Attack : coefficient = 1.05, Melee, Justice
///   combo3Attack : coefficient = 1.50, Melee, Justice
///   heavyAttack  : coefficient = 1.80, Melee, Justice
///   doomGun      : coefficient = 1.10, Ranged, Doom
///
/// [주의] 이 클래스 자체는 데이터만 담는다. 계산은 DamageFormula에서 수행.
/// </summary>
[Serializable]
public class AttackDefinition
{
    [Tooltip("공격력 계수.\n" +
             "최종 피해 기초값 = FinalAttack × coefficient\n" +
             "예: 0.9 = 공격력의 90%,  1.8 = 공격력의 180%")]
    [SerializeField, Range(0.01f, 10f)] public float coefficient = 1.0f;

    [Tooltip("공격 분류. 해당 카테고리 피해 보너스가 추가 적용됨.")]
    [SerializeField] public AttackCategory category = AttackCategory.Melee;

    [Tooltip("공격 속성. 해당 속성 피해 보너스 + Health의 속성별 반응이 적용됨.")]
    [SerializeField] public CombatAttribute attribute = CombatAttribute.Justice;
}
