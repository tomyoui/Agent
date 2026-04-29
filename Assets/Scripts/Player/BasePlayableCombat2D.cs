using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Common combat base for playable characters.
/// - Holds CharacterStats for shared damage calculation.
/// - Owns the shared ultimate gauge state.
/// - Leaves actual ultimate behavior to character-specific overrides.
/// </summary>
public abstract class BasePlayableCombat2D : MonoBehaviour
{
    [Header("Ultimate Gauge")]
    [FormerlySerializedAs("maxUltimateGauge")]
    [SerializeField, Min(1f)] protected float ultimateMax = 100f;

    [SerializeField, Min(0f)] protected float basicAttackHitGain = 5f;
    [SerializeField, Min(0f)] protected float skillHitGain = 15f;

    protected CharacterStats _stats;
    protected float ultimateGauge;

    public float UltimateGauge => ultimateGauge;
    public float UltimateMax => ultimateMax;
    public float UltimateGaugeRatio => ultimateMax > 0f ? ultimateGauge / ultimateMax : 0f;
    public float CurrentUltimateGauge => ultimateGauge;
    public bool IsUltimateReady => CanUseUltimate();

    protected virtual void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        if (_stats == null)
        {
            Debug.LogWarning($"[{GetType().Name}] CharacterStats is missing. Damage falls back to flat attack values.", this);
        }
    }

    protected int CalculateSkillDamage(AttackDefinition attackDef, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    protected int CalculateSkillDamage(AttackDefinition attackDef, CharacterStats defender, int fallbackFlatAttack = 10)
    {
        return _stats != null
            ? DamageFormula.Calculate(_stats, attackDef, defender)
            : DamageFormula.CalculateFlat(fallbackFlatAttack, attackDef);
    }

    public bool CanUseUltimate()
    {
        return ultimateGauge >= ultimateMax;
    }

    public void AddUltimateGauge(float amount)
    {
        if (amount <= 0f || ultimateMax <= 0f)
        {
            return;
        }

        float before = ultimateGauge;
        ultimateGauge = Mathf.Clamp(ultimateGauge + amount, 0f, ultimateMax);

        if (!Mathf.Approximately(before, ultimateGauge))
        {
            Debug.Log($"[{GetType().Name}] Ultimate gauge +{amount:0.##}: {before:0.##}/{ultimateMax:0.##} -> {ultimateGauge:0.##}/{ultimateMax:0.##}", this);
        }
    }

    public float GetUltimateGauge()
    {
        return ultimateGauge;
    }

    public float GetUltimateMax()
    {
        return ultimateMax;
    }

    public float GetUltimateGaugeRatio()
    {
        return UltimateGaugeRatio;
    }

    protected bool TryConsumeUltimateGauge()
    {
        if (!CanUseUltimate())
        {
            return false;
        }

        ultimateGauge = 0f;
        Debug.Log($"[{GetType().Name}] Ultimate gauge consumed.", this);
        return true;
    }

    public bool TryTriggerUltimate()
    {
        if (!TryConsumeUltimateGauge())
        {
            Debug.Log($"[{GetType().Name}] Ultimate gauge is not ready ({ultimateGauge:0.##}/{ultimateMax:0.##}).", this);
            return false;
        }

        Debug.Log($"[{GetType().Name}] Ultimate used.", this);
        ExecuteUltimate();
        return true;
    }

    public virtual void ExecuteUltimate() { }
}
