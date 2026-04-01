using UnityEngine;

/// <summary>
/// 모든 플레이어블 캐릭터 전투 컴포넌트의 공통 베이스.
///
/// [역할]
///   - CharacterStats 참조 보관
///   - CalculateSkillDamage() 헬퍼 → 평타·강공·스킬·궁극기 전부 공통 계산
///   - 궁극기 게이지 상태 + 헬퍼 → 모든 캐릭터 공통 게이지 시스템
///
/// [캐릭터마다 달라지는 것]
///   coefficient, attribute, 범위, 투사체 수, 애니/VFX/SFX
///   → AttackDefinition 설정값과 ExecuteUltimate() 오버라이드로 처리
///
/// [공통인 것 — 이 베이스가 담당]
///   공격력 계산, 치명타, 카테고리/속성 보너스, 궁극기 게이지 증감
///
/// [확장 방법]
///   새 캐릭터 전투 컴포넌트는 MonoBehaviour 대신 BasePlayableCombat2D 상속.
///   Awake 오버라이드 시 base.Awake() 를 반드시 먼저 호출.
///   궁극기 실제 동작은 ExecuteUltimate() 를 override.
/// </summary>
public abstract class BasePlayableCombat2D : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // Inspector — 궁극기 게이지 설정
    // ─────────────────────────────────────────────────────────

    [Header("궁극기 게이지")]
    [Tooltip("궁극기 사용에 필요한 최대 게이지.\n" +
             "이 값에 도달하면 CanUseUltimate = true.")]
    [SerializeField, Min(1)] protected int maxUltimateGauge = 20;

    [Tooltip("평타 적중 1회당 게이지 획득량.")]
    [SerializeField, Min(0)] protected int basicAttackHitGain = 1;

    [Tooltip("스킬(E 등) 적중 1회당 게이지 획득량.")]
    [SerializeField, Min(0)] protected int skillHitGain = 4;

    // ─────────────────────────────────────────────────────────
    // 런타임 상태
    // ─────────────────────────────────────────────────────────

    // 서브클래스에서 직접 접근 가능하도록 protected
    protected CharacterStats _stats;
    protected int _currentUltimateGauge;

    // ─────────────────────────────────────────────────────────
    // 궁극기 게이지 프로퍼티
    // ─────────────────────────────────────────────────────────

    /// <summary>궁극기 사용 가능 여부. UI 표시, 입력 차단 등에 활용.</summary>
    public bool CanUseUltimate => _currentUltimateGauge >= maxUltimateGauge;

    /// <summary>현재 게이지 비율 (0~1). UI 슬라이더·이펙트 연동용.</summary>
    public float UltimateGaugeRatio => (float)_currentUltimateGauge / maxUltimateGauge;

    /// <summary>현재 게이지 절대값. 디버그·세이브용.</summary>
    public int CurrentUltimateGauge => _currentUltimateGauge;

    // ─────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        if (_stats == null)
            Debug.LogWarning($"[{GetType().Name}] CharacterStats가 없습니다. flatAttack 폴백으로 동작합니다.", this);
    }

    // ─────────────────────────────────────────────────────────
    // 공통 데미지 계산 헬퍼
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 평타·강공·E 스킬·궁극기 등 모든 공격의 데미지 계산 단일 진입점.
    ///
    /// CharacterStats가 있으면 정식 계산:
    ///   FinalAttack × coefficient × (1 + 보너스) × 치명타
    /// 없으면 fallbackFlatAttack으로 단순 계산.
    ///
    /// 사용 예:
    ///   int dmg = CalculateSkillDamage(ultimateAttackDef, fallbackFlatAttack: 30);
    /// </summary>
    protected int CalculateSkillDamage(AttackDefinition attackDef, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    /// <summary>
    /// 방어자 스탯이 있을 때 방어 감소까지 적용하는 오버로드.
    /// defender == null 이면 방어 감소 없이 계산 (DamageFormula 내부 처리).
    /// </summary>
    protected int CalculateSkillDamage(AttackDefinition attackDef, CharacterStats defender, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef, defender)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    // ─────────────────────────────────────────────────────────
    // 궁극기 게이지 헬퍼
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 게이지를 amount만큼 증가. maxUltimateGauge를 초과하지 않음.
    ///
    /// 호출 위치:
    ///   - 평타 적중 후 → AddUltimateGauge(basicAttackHitGain)
    ///   - 스킬 적중 후 → AddUltimateGauge(skillHitGain)
    /// </summary>
    protected void AddUltimateGauge(int amount)
    {
        _currentUltimateGauge = Mathf.Clamp(_currentUltimateGauge + amount, 0, maxUltimateGauge);
    }

    /// <summary>
    /// 게이지를 소모. 게이지가 부족하면 false 반환 후 아무 것도 하지 않음.
    /// </summary>
    protected bool TryConsumeUltimateGauge()
    {
        if (!CanUseUltimate) return false;
        _currentUltimateGauge = 0;
        return true;
    }

    /// <summary>
    /// 궁극기 발동 시도 — 외부(입력 콜백 등)에서 호출하는 단일 진입점.
    ///
    /// 흐름:
    ///   1. CanUseUltimate 체크
    ///   2. 게이지 소모 (TryConsumeUltimateGauge)
    ///   3. ExecuteUltimate() 호출 → 각 캐릭터가 실제 동작 정의
    ///
    /// 반환값: 성공 시 true, 게이지 부족 시 false
    /// </summary>
    protected bool TryTriggerUltimate()
    {
        if (!TryConsumeUltimateGauge())
        {
            Debug.Log($"[{GetType().Name}] 궁극기 게이지 부족 ({_currentUltimateGauge}/{maxUltimateGauge})", this);
            return false;
        }

        Debug.Log($"[{GetType().Name}] 궁극기 발동!", this);
        ExecuteUltimate();
        return true;
    }

    /// <summary>
    /// 실제 궁극기 동작 정의. 각 캐릭터 서브클래스에서 override.
    ///
    /// [구현 예]
    ///   - 근접 캐릭터: 원형 범위 AoE MeleeHitResolver
    ///   - 원거리 캐릭터: 전방 다수 투사체 발사
    ///   - 버프형: 일정 시간 버프 코루틴 시작
    ///
    /// 데미지 계산은 반드시 CalculateSkillDamage(ultimateAttackDef, ...)를 사용할 것.
    /// </summary>
    protected virtual void ExecuteUltimate() { }
}
