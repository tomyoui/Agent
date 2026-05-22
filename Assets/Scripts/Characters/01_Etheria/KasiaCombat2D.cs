using System.Collections;
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

    [Header("Kasia E Skill")]
    [SerializeField] private float dashDistance = 2.5f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField, Min(0)] private int fallbackSkillDamage = 40;
    [SerializeField, Min(0f)] private float fallbackSkillCooldown = 5f;
    [SerializeField, Min(0f)] private float fallbackSkillRange = 1.5f;
    [SerializeField, Range(1f, 360f)] private float skillAttackAngle = 115f;
    [SerializeField, Min(0f)] private float skillHitInterval = 0.08f;
    [SerializeField] private Color skillDamageNumberColor = new Color(1f, 0.25f, 0.1f);

    private Rigidbody2D _rb;
    private Coroutine _skillRoutine;
    private float _nextSkillTime;

    public override float SkillCooldownRemaining => Mathf.Max(0f, _nextSkillTime - Time.time);
    public override float SkillCooldownDuration => GetSkillCooldown();

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_skillRoutine != null)
        {
            StopCoroutine(_skillRoutine);
            _skillRoutine = null;
        }

        if (_controller != null)
        {
            _controller.IsVelocityLocked = false;
        }
    }

    public override void RequestSkill()
    {
        if (!CanAcceptInput(CombatInputKind.Skill))
        {
            return;
        }

        if (_skillRoutine != null || Time.time < _nextSkillTime)
        {
            return;
        }

        _nextSkillTime = Time.time + Mathf.Max(0f, GetSkillCooldown());
        _skillRoutine = StartCoroutine(ChargeSlashRoutine());
        LogCombatStabilitySnapshot("스킬 직후");
    }

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

    private IEnumerator ChargeSlashRoutine()
    {
        BeginSkillActive();

        Vector2 direction = GetCurrentAimDirection();
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        yield return DashRoutine(direction);
        yield return ChargeSlashHitsRoutine(direction);
        _skillRoutine = null;
        EndSkillActive();
        LogCombatStabilitySnapshot("카샤 스킬 종료 직후");
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        float duration = Mathf.Max(0.01f, dashDuration);
        float distance = Mathf.Max(0f, dashDistance);
        float elapsed = 0f;
        Vector2 startPosition = transform.position;

        if (_controller != null)
        {
            _controller.IsVelocityLocked = true;
            LogCombatStabilitySnapshot("카샤 스킬 대시 잠금 직후");
        }

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 nextPosition = startPosition + direction * (distance * t);

            if (_rb != null)
            {
                _rb.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            yield return new WaitForFixedUpdate();
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }

        if (_controller != null)
        {
            _controller.IsVelocityLocked = false;
            LogCombatStabilitySnapshot("카샤 스킬 대시 해제 직후");
        }
    }

    private IEnumerator ChargeSlashHitsRoutine(Vector2 direction)
    {
        int totalDamage = GetSkillDamage();
        int firstHitDamage = Mathf.Max(0, Mathf.RoundToInt(totalDamage * (2f / 7f)));
        int secondHitDamage = Mathf.Max(0, Mathf.RoundToInt(totalDamage * (2f / 7f)));
        int thirdHitDamage = Mathf.Max(0, totalDamage - firstHitDamage - secondHitDamage);
        bool hasHit = false;

        hasHit |= ExecuteChargeSlashHit(direction, firstHitDamage, 1) > 0;
        yield return new WaitForSeconds(skillHitInterval);

        hasHit |= ExecuteChargeSlashHit(direction, secondHitDamage, 2) > 0;
        yield return new WaitForSeconds(skillHitInterval);

        hasHit |= ExecuteChargeSlashHit(direction, thirdHitDamage, 3) > 0;

        if (hasHit)
        {
            AddUltimateGauge(skillHitGain);
        }
    }

    private int ExecuteChargeSlashHit(Vector2 direction, int damage, int hitIndex)
    {
        return MeleeHitResolver2D.DealDamageInCone(
            transform.position,
            direction,
            0f,
            GetSkillRange(),
            skillAttackAngle,
            damage,
            targetLayer,
            this,
            $"KasiaCombat2D/격정의 돌진베기 {hitIndex}타",
            CombatAttribute.Justice,
            skillDamageNumberColor
        );
    }

    private int GetSkillDamage()
    {
        return combatData != null ? combatData.SkillDamage : fallbackSkillDamage;
    }

    private float GetSkillCooldown()
    {
        return combatData != null ? combatData.SkillCooldown : fallbackSkillCooldown;
    }

    private float GetSkillRange()
    {
        return combatData != null ? combatData.SkillRange : fallbackSkillRange;
    }
}
