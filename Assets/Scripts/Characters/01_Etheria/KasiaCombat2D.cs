using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KasiaCombat2D : BasePlayableCombat2D
{
    [Header("Kasia Basic Attack")]
    [SerializeField] private AttackDefinition basicAttack = new AttackDefinition { coefficient = 2.4f };
    [SerializeField] private AttackDefinition combo3Attack = new AttackDefinition { coefficient = 3.2f };
    [SerializeField] private float fallbackAttackRange = 1.55f;
    [SerializeField] private float combo3AttackRange = 2.15f;
    [SerializeField] private float fallbackAttackAngle = 120f;
    [SerializeField] private float combo3AttackAngle = 155f;
    [SerializeField] private float fallbackAttackCooldown = 0.34f;
    [SerializeField] private float fallbackHitStop = 0.055f;
    [SerializeField] private float fallbackKnockbackPower = 8.5f;
    [SerializeField, Min(1f)] private float combo3DamageMultiplier = 1.55f;

    [Header("Kasia E Skill")]
    [SerializeField] private float dashDistance = 3.2f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField, Min(0)] private int fallbackSkillDamage = 95;
    [SerializeField, Min(0f)] private float fallbackSkillCooldown = 4f;
    [SerializeField, Min(0f)] private float fallbackSkillRange = 2.7f;
    [SerializeField, Min(0f)] private float skillBoxWidth = 1.75f;
    [SerializeField, Min(0f)] private float skillForwardOffset = 0.25f;
    [SerializeField, Min(0f)] private float skillKnockbackForce = 13f;
    [SerializeField, Min(0f)] private float skillKnockbackDuration = 0.18f;
    [SerializeField] private Color skillDamageNumberColor = new Color(1f, 0.25f, 0.1f);

    [Header("Kasia Q Ultimate")]
    [SerializeField, Min(0)] private int fallbackUltimateDamage = 180;
    [SerializeField, Min(0f)] private float ultimateHitInterval = 0.16f;
    [SerializeField, Min(0f)] private float ultimateFirstHitLength = 2.6f;
    [SerializeField, Min(0f)] private float ultimateFirstHitWidth = 2.4f;
    [SerializeField, Min(0f)] private float ultimateSecondDashDistance = 1.4f;
    [SerializeField, Min(0f)] private float ultimateSecondDashDuration = 0.08f;
    [SerializeField, Min(0f)] private float ultimateSecondHitLength = 2.8f;
    [SerializeField, Min(0f)] private float ultimateSecondHitWidth = 1.7f;
    [SerializeField, Min(0f)] private float ultimateThirdHitLength = 3.5f;
    [SerializeField, Min(0f)] private float ultimateThirdHitWidth = 2.8f;
    [SerializeField, Min(0f)] private float ultimateKnockbackForce = 16f;
    [SerializeField, Min(0f)] private float ultimateKnockbackDuration = 0.22f;
    [SerializeField] private Color ultimateDamageNumberColor = new Color(1f, 0.9f, 0.25f);

    private Rigidbody2D _rb;
    private Coroutine _skillRoutine;
    private Coroutine _ultimateRoutine;
    private float _nextSkillTime;
    private Vector2 _lastSkillDebugCenter;
    private float _lastSkillDebugRadius;
    private float _lastSkillDebugUntil;
    private Vector2 _lastUltimateDebugCenter;
    private Vector2 _lastUltimateDebugSize;
    private float _lastUltimateDebugAngle;
    private float _lastUltimateDebugUntil;
    private bool _isUltimateSuperArmorActive;

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

        if (_ultimateRoutine != null)
        {
            StopCoroutine(_ultimateRoutine);
            _ultimateRoutine = null;
        }

        _isUltimateSuperArmorActive = false;

        if (_controller != null)
        {
            _controller.IsVelocityLocked = false;
        }
    }

    public override void RequestSkill()
    {
        if (!CanAcceptInput(CombatInputKind.Skill))
        {
            Debug.Log($"[카샤 E] 스킬 입력 차단: 현재 상태={CurrentCombatState}", this);
            return;
        }

        if (_skillRoutine != null || Time.time < _nextSkillTime)
        {
            Debug.Log($"[카샤 E] 스킬 입력 무시: 쿨타임={SkillCooldownRemaining:0.##}초", this);
            return;
        }

        _nextSkillTime = Time.time + Mathf.Max(0f, GetSkillCooldown());
        _skillRoutine = StartCoroutine(JudgementGreatswordRoutine());
        LogCombatStabilitySnapshot("카샤 E 스킬 직후");
    }

    public override void RequestUltimate()
    {
        if (!CanAcceptInput(CombatInputKind.Ultimate))
        {
            Debug.Log($"[카샤 Q] 궁극기 입력 차단: 현재 상태={CurrentCombatState}", this);
            return;
        }

        if (_ultimateRoutine != null)
        {
            Debug.Log("[카샤 Q] 궁극기 입력 무시: 이미 발동 중입니다.", this);
            return;
        }

        if (!TryConsumeUltimateGauge())
        {
            Debug.Log($"[카샤 Q] 궁극기 게이지 부족: {GetUltimateGauge():0.##}/{GetUltimateMax():0.##}", this);
            return;
        }

        _ultimateRoutine = StartCoroutine(BattleRaptureRoutine());
        LogCombatStabilitySnapshot("카샤 Q 궁극기 직후");
    }

    protected override int GetMaxComboStep()
    {
        return 3;
    }

    protected override float GetAttackCooldown()
    {
        return combatData != null ? combatData.AttackCooldown : fallbackAttackCooldown;
    }

    protected override AttackDefinition GetComboAttackDef(int comboStep)
    {
        return comboStep >= 3 ? combo3Attack : basicAttack;
    }

    protected override void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        if (comboStep >= 3)
        {
            range = combo3AttackRange;
            angle = combo3AttackAngle;
            return;
        }

        range = combatData != null ? combatData.AttackRange : fallbackAttackRange;
        angle = fallbackAttackAngle;
    }

    protected override int GetBasicAttackDamage(int comboStep, AttackDefinition attackDef)
    {
        int baseDamage = combatData != null
            ? Mathf.Max(1, combatData.BasicAttackDamage)
            : CalculateSkillDamage(attackDef, fallbackFlatAttack: 12);

        return comboStep >= 3 ? Mathf.RoundToInt(baseDamage * combo3DamageMultiplier) : baseDamage;
    }

    protected override float GetHitStopDuration(int comboStep)
    {
        return combatData != null ? combatData.HitStopDuration : fallbackHitStop;
    }

    protected override float GetKnockbackForce(int comboStep)
    {
        return combatData != null ? combatData.KnockbackPower : fallbackKnockbackPower;
    }

    private IEnumerator JudgementGreatswordRoutine()
    {
        BeginSkillActive();

        Vector2 skillDirection = ResolveSkillDirection();
        Debug.Log($"[카샤 E] 심판의 대검 시작: skillDirection={skillDirection}, cooldown={GetSkillCooldown():0.##}", this);
        yield return DashRoutine(skillDirection);

        int damagedCount = ExecuteJudgementGreatswordHit(skillDirection);
        if (damagedCount > 0)
        {
            // 테스트용 수치: SCN_Bootstrap의 skillHitGain을 크게 올려 3~5회 적중으로 궁극기를 확인할 수 있게 둔다.
            AddUltimateGauge(skillHitGain);
            PlayHitSfx(isMelee: true);
            TriggerHitStop(GetHitStopDuration(1));
            Debug.Log($"[카샤 E] 심판의 대검 적중: 대상 수={damagedCount}, 궁극기 게이지 획득={skillHitGain:0.##}", this);
        }
        else
        {
            Debug.Log("[카샤 E] 심판의 대검 종료: 적중 대상 없음", this);
        }

        EndSkillActive();
        _skillRoutine = null;
        LogCombatStabilitySnapshot("카샤 E 스킬 종료 직후");
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        float duration = Mathf.Max(0.01f, dashDuration);
        float distance = Mathf.Max(0f, dashDistance);
        float elapsed = 0f;
        Vector2 startPosition = transform.position;
        Vector2 dashEndPosition = startPosition + direction * distance;
        Debug.DrawLine(startPosition, dashEndPosition, Color.green, duration + 0.35f);
        Debug.Log($"[카샤 E] 돌진 방향 확정: start={startPosition}, end={dashEndPosition}, direction={direction}", this);

        if (_controller != null)
        {
            _controller.IsVelocityLocked = true;
            LogCombatStabilitySnapshot("카샤 E 돌진 잠금 직후");
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
            LogCombatStabilitySnapshot("카샤 E 돌진 잠금 해제 직후");
        }
    }

    private int ExecuteJudgementGreatswordHit(Vector2 direction)
    {
        float length = GetSkillRange();
        Vector2 center = (Vector2)transform.position + direction * (skillForwardOffset + length * 0.5f);
        Vector2 size = new Vector2(length, skillBoxWidth);

        return DealDamageInForwardBox(
            center,
            size,
            direction,
            GetSkillDamage(),
            "KasiaCombat2D/심판의 대검",
            skillDamageNumberColor,
            skillKnockbackForce,
            skillKnockbackDuration,
            Color.red,
            true
        );
    }

    private IEnumerator BattleRaptureRoutine()
    {
        SetCombatState(CombatState.UltimateActive);
        bool hadVelocityLock = _controller != null && _controller.IsVelocityLocked;

        if (_controller != null)
        {
            _controller.IsVelocityLocked = true;
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }

        _isUltimateSuperArmorActive = true;
        Vector2 direction = ResolveSkillDirection();
        Debug.Log($"[카샤 Q] 전투의 희열 시작: 전방 3연격, 슈퍼아머={_isUltimateSuperArmorActive}, direction={direction}", this);

        int totalDamage = GetUltimateDamage();
        int firstDamage = Mathf.RoundToInt(totalDamage * 0.25f);
        int secondDamage = Mathf.RoundToInt(totalDamage * 0.30f);
        int thirdDamage = Mathf.Max(0, totalDamage - firstDamage - secondDamage);

        ExecuteBattleRaptureHit(1, "넓은 횡베기", direction, ultimateFirstHitLength, ultimateFirstHitWidth, firstDamage, 0.75f);
        yield return new WaitForSeconds(ultimateHitInterval);

        yield return DashRoutine(direction, ultimateSecondDashDistance, ultimateSecondDashDuration, "카샤 Q 2타 전진");
        ExecuteBattleRaptureHit(2, "전진 베기", direction, ultimateSecondHitLength, ultimateSecondHitWidth, secondDamage, 0.85f);
        yield return new WaitForSeconds(ultimateHitInterval);

        ExecuteBattleRaptureHit(3, "내려찍기 폭발", direction, ultimateThirdHitLength, ultimateThirdHitWidth, thirdDamage, 1f);

        if (_controller != null)
        {
            _controller.IsVelocityLocked = hadVelocityLock;
        }

        if (CurrentCombatState == CombatState.UltimateActive)
        {
            SetCombatState(CombatState.Ready);
        }

        _ultimateRoutine = null;
        _isUltimateSuperArmorActive = false;
        Debug.Log($"[카샤 Q] 전투의 희열 종료: 슈퍼아머={_isUltimateSuperArmorActive}, 이동/공격 잠금 해제", this);
        LogCombatStabilitySnapshot("카샤 Q 궁극기 종료 직후");
    }

    private int ExecuteBattleRaptureHit(int hitIndex, string hitName, Vector2 direction, float length, float width, int damage, float knockbackMultiplier)
    {
        Vector2 center = (Vector2)transform.position + direction * (length * 0.5f);
        int damagedCount = DealDamageInForwardBox(
            center,
            new Vector2(length, width),
            direction,
            damage,
            $"KasiaCombat2D/전투의 희열 {hitIndex}타 {hitName}",
            ultimateDamageNumberColor,
            ultimateKnockbackForce * knockbackMultiplier,
            ultimateKnockbackDuration,
            Color.yellow,
            false
        );

        if (damagedCount > 0)
        {
            PlayHitSfx(isMelee: true);
            TriggerHitStop(GetHitStopDuration(1));
        }

        Debug.Log($"[카샤 Q] 전투의 희열 {hitIndex}타({hitName}): damage={damage}, 대상 수={damagedCount}", this);
        return damagedCount;
    }

    private IEnumerator DashRoutine(Vector2 direction, float distance, float duration, string context)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeDistance = Mathf.Max(0f, distance);
        float elapsed = 0f;
        Vector2 startPosition = transform.position;
        Vector2 dashEndPosition = startPosition + direction * safeDistance;
        Debug.DrawLine(startPosition, dashEndPosition, Color.green, safeDuration + 0.35f);
        Debug.Log($"[카샤 이동] {context}: start={startPosition}, end={dashEndPosition}, direction={direction}", this);

        while (elapsed < safeDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            Vector2 nextPosition = startPosition + direction * (safeDistance * t);

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
    }

    private int DealDamageInForwardBox(
        Vector2 center,
        Vector2 size,
        Vector2 direction,
        int damage,
        string debugLabel,
        Color damageNumberColor,
        float knockbackForce,
        float knockbackDuration,
        Color debugColor,
        bool recordSkillDebug)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;

        if (recordSkillDebug)
        {
            RecordSkillDebug(center, size, angle, debugColor);
        }
        else
        {
            RecordUltimateDebug(center, size, angle, debugColor);
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayer;
        filter.useTriggers = true;

        Collider2D[] hits = new Collider2D[32];
        int hitCount = Physics2D.OverlapBox(center, size, angle, filter, hits);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        int damagedCount = 0;

        Debug.Log($"[{debugLabel}] 전방 박스 판정: center={center}, size={size}, angle={angle:0.##}, hitCount={hitCount}", this);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null && hit.transform.root != null)
            {
                damageable = hit.transform.root.GetComponentInChildren<IDamageable>();
            }

            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            damageable.TakeDamage(damage, CombatAttribute.Justice, damageNumberColor);
            ApplyKnockback(hit, safeDirection, knockbackForce, knockbackDuration);
            damagedCount++;
        }

        Debug.Log($"[{debugLabel}] 전방 박스 피해 적용 완료: 대상 수={damagedCount}", this);
        return damagedCount;
    }

    private void ApplyKnockback(Collider2D hit, Vector2 direction, float force, float duration)
    {
        if (force <= 0f || duration <= 0f || hit == null)
        {
            return;
        }

        KnockbackReceiver2D knockbackReceiver = hit.GetComponentInParent<KnockbackReceiver2D>();
        if (knockbackReceiver == null && hit.transform.root != null)
        {
            knockbackReceiver = hit.transform.root.GetComponentInChildren<KnockbackReceiver2D>();
        }

        if (knockbackReceiver == null)
        {
            Debug.Log($"[카샤 넉백] {hit.name}에 KnockbackReceiver2D가 없어 넉백을 건너뜁니다.", hit);
            return;
        }

        knockbackReceiver.ApplyKnockback(direction, force, duration);
        knockbackReceiver.ApplyStagger(duration);
    }

    private Vector2 ResolveSkillDirection()
    {
        Vector2 moveDirection;
        bool hasMoveDirection = TryGetMoveDirection(out moveDirection);
        Vector2 aimDirection = GetCurrentAimDirection();

        if (hasMoveDirection)
        {
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                float dot = Vector2.Dot(moveDirection, aimDirection.normalized);
                if (dot < -0.65f)
                {
                    Debug.Log($"[카샤 E] 이동 방향과 바라보는 방향이 반대로 잡혀 바라보는 방향을 사용합니다: move={moveDirection}, aim={aimDirection.normalized}", this);
                    return aimDirection.normalized;
                }
            }

            return moveDirection;
        }

        if (aimDirection.sqrMagnitude > 0.0001f)
        {
            return aimDirection.normalized;
        }

        float facingX = transform.localScale.x;
        return facingX < -0.0001f ? Vector2.left : Vector2.right;
    }

    private bool TryGetMoveDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (_controller == null)
        {
            return false;
        }

        Vector2 currentMoveInput = _controller.CurrentMoveInput;
        if (currentMoveInput.sqrMagnitude > 0.0001f)
        {
            direction = currentMoveInput.normalized;
            return true;
        }

        if (_controller.TryGetLastMoveDirection(out Vector2 lastMoveDirection) &&
            lastMoveDirection.sqrMagnitude > 0.0001f)
        {
            direction = lastMoveDirection.normalized;
            return true;
        }

        return false;
    }

    private int GetSkillDamage()
    {
        return combatData != null && combatData.SkillDamage > 0 ? combatData.SkillDamage : fallbackSkillDamage;
    }

    private float GetSkillCooldown()
    {
        return combatData != null ? combatData.SkillCooldown : fallbackSkillCooldown;
    }

    private float GetSkillRange()
    {
        return combatData != null ? combatData.SkillRange : fallbackSkillRange;
    }

    private int GetUltimateDamage()
    {
        return combatData != null && combatData.UltimateDamage > 0 ? combatData.UltimateDamage : fallbackUltimateDamage;
    }

    private void RecordSkillDebug(Vector2 center, Vector2 size, float angle, Color color)
    {
        _lastSkillDebugCenter = center;
        _lastSkillDebugRadius = Mathf.Max(size.x, size.y) * 0.5f;
        _lastSkillDebugUntil = Time.time + 0.5f;
        DrawBoxDebug(center, size, angle, color);
    }

    private void RecordUltimateDebug(Vector2 center, Vector2 size, float angle, Color color)
    {
        _lastUltimateDebugCenter = center;
        _lastUltimateDebugSize = size;
        _lastUltimateDebugAngle = angle;
        _lastUltimateDebugUntil = Time.time + 0.5f;
        DrawBoxDebug(center, size, angle, color);
    }

    private void DrawBoxDebug(Vector2 center, Vector2 size, float angle, Color color)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 half = size * 0.5f;
        Vector2 a = center + (Vector2)(rotation * new Vector2(-half.x, -half.y));
        Vector2 b = center + (Vector2)(rotation * new Vector2(half.x, -half.y));
        Vector2 c = center + (Vector2)(rotation * new Vector2(half.x, half.y));
        Vector2 d = center + (Vector2)(rotation * new Vector2(-half.x, half.y));

        Debug.DrawLine(a, b, color, 0.35f);
        Debug.DrawLine(b, c, color, 0.35f);
        Debug.DrawLine(c, d, color, 0.35f);
        Debug.DrawLine(d, a, color, 0.35f);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.time <= _lastSkillDebugUntil)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_lastSkillDebugCenter, _lastSkillDebugRadius);
        }

        if (Time.time <= _lastUltimateDebugUntil)
        {
            Gizmos.color = Color.yellow;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(_lastUltimateDebugCenter, Quaternion.Euler(0f, 0f, _lastUltimateDebugAngle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _lastUltimateDebugSize);
            Gizmos.matrix = previousMatrix;
        }
    }
}
