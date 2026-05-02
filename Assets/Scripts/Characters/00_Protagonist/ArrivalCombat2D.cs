using System.Collections;
using UnityEngine;

public class ArrivalCombat2D : BasePlayableCombat2D
{
    [Header("Ultimate Buff")]
    [SerializeField] private float ultimateBuffDuration = 8f;
    [SerializeField] private float ultimateAttackMultiplier = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float ultimateCooldownMultiplier = 0.5f;

    private Coroutine _ultimateBuffRoutine;
    private bool _isUltimateBuffActive;

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetUltimateBuff();
    }

    public override void ExecuteUltimate()
    {
        if (_ultimateBuffRoutine != null)
        {
            StopCoroutine(_ultimateBuffRoutine);
            ResetUltimateBuff();
        }

        _ultimateBuffRoutine = StartCoroutine(UltimateBuffRoutine());
    }

    private IEnumerator UltimateBuffRoutine()
    {
        ApplyUltimateBuff();

        Debug.Log(
            $"[ArrivalCombat2D] Ultimate buff start. attack x{ultimateAttackMultiplier:0.##}, cooldown x{ultimateCooldownMultiplier:0.##}, duration {ultimateBuffDuration:0.##}s",
            this);

        yield return new WaitForSeconds(ultimateBuffDuration);

        ResetUltimateBuff();
    }

    private void ApplyUltimateBuff()
    {
        _isUltimateBuffActive = true;

        if (_stats != null)
        {
            _stats.AttackBuffMultiplier = ultimateAttackMultiplier;
        }

        attackCooldown = Mathf.Max(0.01f, _baseAttackCooldown * ultimateCooldownMultiplier);
    }

    private void ResetUltimateBuff()
    {
        if (!_isUltimateBuffActive)
        {
            return;
        }

        if (_stats != null)
        {
            _stats.AttackBuffMultiplier = 1f;
        }

        attackCooldown = _baseAttackCooldown > 0f ? _baseAttackCooldown : attackCooldown;
        _isUltimateBuffActive = false;
        _ultimateBuffRoutine = null;
        Debug.Log("[ArrivalCombat2D] Ultimate buff end", this);
    }
}
