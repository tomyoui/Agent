using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyCorruptedHussarBoss2D : MonoBehaviour
{
    private enum BossState
    {
        Idle,
        Chase,
        Telegraph,
        Attack,
        Recovery,
        Dead
    }

    private enum BossPattern
    {
        LanceCharge,
        WingSweep,
        JumpSlam
    }

    [Header("Target")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.2f;
    [SerializeField, Min(0f)] private float chaseRange = 8f;
    [SerializeField, Min(0f)] private float attackCooldown = 1f;

    [Header("Lance Charge")]
    [SerializeField, Min(0f)] private float chargeTelegraphTime = 0.85f;
    [SerializeField, Min(0f)] private float chargeSpeed = 14f;
    [SerializeField, Min(0f)] private float chargeDuration = 0.45f;
    [SerializeField, Min(0)] private int chargeDamage = 8;
    [SerializeField, Min(0f)] private float chargeRecoveryTime = 1.05f;
    [SerializeField, Min(0f)] private float chargeHitRadius = 1.35f;
    [SerializeField, Min(0f)] private float chargeHitForwardOffset = 0.75f;
    [SerializeField, Min(0f)] private float chargeCloseHitRadius = 1.25f;

    [Header("Wing Sweep")]
    [SerializeField, Min(0f)] private float sweepTelegraphTime = 0.75f;
    [SerializeField, Min(0f)] private float sweepRadius = 3.2f;
    [SerializeField, Min(0)] private int sweepDamage = 5;
    [SerializeField, Min(0f)] private float sweepRecoveryTime = 1f;
    [SerializeField, Min(0f)] private float sweepForwardOffset = 1.2f;
    [SerializeField, Min(0f)] private float sweepCloseRadius = 1.6f;

    [Header("Jump Slam")]
    [SerializeField, Min(0f)] private float slamTelegraphTime = 0.8f;
    [SerializeField, Min(0f)] private float slamRadius = 3f;
    [SerializeField, Min(0)] private int slamDamage = 10;
    [SerializeField, Min(0f)] private float slamRecoveryTime = 1f;

    [Header("Telegraph Color")]
    [SerializeField] private Color chargeTelegraphColor = new Color(1f, 0.85f, 0.15f);
    [SerializeField] private Color sweepTelegraphColor = new Color(1f, 0.35f, 0.15f);
    [SerializeField] private Color slamTelegraphColor = new Color(0.75f, 0.15f, 1f);
    [SerializeField] private Color attackColor = new Color(1f, 0.1f, 0.1f);
    [SerializeField] private Color recoveryColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Fake Animation")]
    [SerializeField, Min(1f)] private float telegraphScaleMultiplier = 1.08f;
    [SerializeField, Min(0.01f)] private float telegraphBlinkInterval = 0.1f;
    [SerializeField, Min(1f)] private float chargeStretchMultiplier = 1.22f;
    [SerializeField, Range(0f, 30f)] private float chargeLeanAngle = 10f;
    [SerializeField, Min(0f)] private float wingShakeDistance = 0.12f;
    [SerializeField, Min(0.01f)] private float wingShakeDuration = 0.18f;
    [SerializeField, Range(0.5f, 1f)] private float slamCrouchScaleMultiplier = 0.92f;
    [SerializeField, Min(1f)] private float slamImpactScaleMultiplier = 1.15f;
    [SerializeField, Min(0.01f)] private float slamImpactHoldTime = 0.12f;

    private const float SlamPredictLeadTime = 0.35f;
    private const float SlamLateRetargetLeadTime = 0.2f;

    private Rigidbody2D _rb;
    private Health _health;
    private SpriteRenderer _spriteRenderer;
    private Transform _target;
    private BossState _state = BossState.Idle;
    private Coroutine _patternRoutine;
    private Color _originalColor = Color.white;
    private Vector3 _originalScale = Vector3.one;
    private Quaternion _originalRotation = Quaternion.identity;
    private Vector2 _facingDirection = Vector2.down;
    private float _nextAttackReadyTime;
    private Vector2 _lastTargetPoint;
    private float _lastTargetSampleTime;
    private Vector2 _estimatedTargetVelocity;
    private Vector2 _lastHitCenter;
    private float _lastHitRadius;
    private Vector2 _lastCloseHitCenter;
    private float _lastCloseHitRadius;
    private bool _hasLastHit;
    private bool _hasLastCloseHit;
    private readonly HashSet<IDamageable> _damagedThisAttack = new HashSet<IDamageable>();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalScale = transform.localScale;
        _originalRotation = transform.localRotation;

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        EnsurePlayerLayerConfigured();
    }

    private void FixedUpdate()
    {
        if (IsBossDead())
        {
            EnterDeadState();
            return;
        }

        if (_state == BossState.Telegraph || _state == BossState.Attack || _state == BossState.Recovery)
        {
            StopMovement();
            return;
        }

        ResolveTarget();
        if (!IsValidTarget(_target))
        {
            _target = null;
            SetState(BossState.Idle);
            StopMovement();
            return;
        }

        Vector2 toTarget = GetTargetPoint(_target) - (Vector2)transform.position;
        float distanceToTarget = toTarget.magnitude;
        UpdateTargetMotion(_target);

        if (distanceToTarget > chaseRange)
        {
            SetState(BossState.Idle);
            StopMovement();
            return;
        }

        UpdateFacingDirection(toTarget);

        if (CanStartAttack(distanceToTarget))
        {
            StopMovement();
            StartPattern(SelectPattern(distanceToTarget));
            return;
        }

        SetState(BossState.Chase);
        _rb.linearVelocity = _facingDirection * moveSpeed;
    }

    private void StartPattern(BossPattern pattern)
    {
        if (_patternRoutine != null)
        {
            return;
        }

        _patternRoutine = StartCoroutine(RunPattern(pattern));
    }

    private IEnumerator RunPattern(BossPattern pattern)
    {
        ResetVisuals();
        _damagedThisAttack.Clear();

        switch (pattern)
        {
            case BossPattern.LanceCharge:
                yield return RunLanceCharge();
                break;
            case BossPattern.WingSweep:
                yield return RunWingSweep();
                break;
            case BossPattern.JumpSlam:
                yield return RunJumpSlam();
                break;
        }

        _damagedThisAttack.Clear();
        ResetVisuals();
        _nextAttackReadyTime = Time.time + attackCooldown;
        _patternRoutine = null;
    }

    private IEnumerator RunLanceCharge()
    {
        SetState(BossState.Telegraph);
        FaceCurrentTarget();
        Vector2 chargeDirection = _facingDirection;
        StopMovement();
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 랜스 차지 전조 연출 시작 | direction={_facingDirection}, time={chargeTelegraphTime}", this);

        yield return PlayTelegraphVisual(chargeTelegraphTime, chargeTelegraphColor, telegraphScaleMultiplier);
        if (IsBossDead()) yield break;

        _facingDirection = chargeDirection;
        SetState(BossState.Attack);
        SetSpriteColor(attackColor);
        ApplyChargeVisual();
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 랜스 차지 돌진 연출 시작 | speed={chargeSpeed}, duration={chargeDuration}", this);

        float endTime = Time.time + chargeDuration;
        while (Time.time < endTime && !IsBossDead())
        {
            Vector2 nextPosition = _rb.position + chargeDirection * (chargeSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(nextPosition);
            Vector2 chargeCenter = GetForwardHitCenter(_rb.position, chargeDirection, chargeHitForwardOffset);
            TryDealDamageInRange(chargeCenter, chargeHitRadius, chargeDamage, "랜스 차지");
            TryDealCloseDamageInRange(_rb.position, chargeCloseHitRadius, chargeDamage, "랜스 차지 근접 보정");
            yield return new WaitForFixedUpdate();
        }

        ResetTransformVisuals();
        yield return RunRecovery(chargeRecoveryTime, "랜스 차지");
    }

    private IEnumerator RunWingSweep()
    {
        SetState(BossState.Telegraph);
        FaceCurrentTarget();
        StopMovement();
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 윙 스윕 전조 연출 시작 | direction={_facingDirection}, radius={sweepRadius}, time={sweepTelegraphTime}", this);

        yield return PlayTelegraphVisual(sweepTelegraphTime, sweepTelegraphColor, telegraphScaleMultiplier);
        if (IsBossDead()) yield break;

        SetState(BossState.Attack);
        SetSpriteColor(attackColor);
        Vector2 sweepCenter = GetForwardHitCenter(transform.position, _facingDirection, sweepForwardOffset);
        DrawSweepDebug(sweepCenter);
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 윙 스윕 공격 판정 | center={sweepCenter}, radius={sweepRadius}, closeRadius={sweepCloseRadius}, damage={sweepDamage}", this);
        yield return PlayWingSweepShake();
        TryDealDamageInRange(sweepCenter, sweepRadius, sweepDamage, "윙 스윕");
        TryDealCloseDamageInRange(transform.position, sweepCloseRadius, sweepDamage, "윙 스윕 근접 보정");

        ResetTransformVisuals();
        yield return RunRecovery(sweepRecoveryTime, "윙 스윕");
    }

    private IEnumerator RunJumpSlam()
    {
        SetState(BossState.Telegraph);
        FaceCurrentTarget();
        Vector2 slamPoint = GetPredictedSlamPoint(SlamPredictLeadTime);
        StopMovement();
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 점프 슬램 예측 전조 시작 | targetPoint={slamPoint}, velocity={_estimatedTargetVelocity}, radius={slamRadius}, time={slamTelegraphTime}", this);

        yield return PlayTelegraphVisual(slamTelegraphTime, slamTelegraphColor, slamCrouchScaleMultiplier);
        if (IsBossDead()) yield break;

        SetState(BossState.Attack);
        SetSpriteColor(attackColor);
        transform.localScale = GetUniformScale(slamImpactScaleMultiplier);
        slamPoint = GetPredictedSlamPoint(SlamLateRetargetLeadTime);
        FacePoint(slamPoint);
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 점프 슬램 착지 직전 재조준 | landingPoint={slamPoint}, velocity={_estimatedTargetVelocity}", this);

        Vector2 startPoint = _rb.position;
        float travelTime = Mathf.Max(0.12f, Vector2.Distance(startPoint, slamPoint) / Mathf.Max(0.01f, chargeSpeed));
        float elapsed = 0f;

        while (elapsed < travelTime && !IsBossDead())
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            _rb.MovePosition(Vector2.Lerp(startPoint, slamPoint, eased));
            yield return new WaitForFixedUpdate();
        }

        if (IsBossDead()) yield break;

        _rb.MovePosition(slamPoint);
        transform.localScale = GetUniformScale(slamImpactScaleMultiplier);
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 점프 슬램 착지 연출 및 공격 | center={slamPoint}, radius={slamRadius}, damage={slamDamage}", this);
        TryDealDamageInRange(slamPoint, slamRadius, slamDamage, "점프 슬램");
        yield return WaitWhileAlive(slamImpactHoldTime);

        ResetTransformVisuals();
        yield return RunRecovery(slamRecoveryTime, "점프 슬램");
    }

    private IEnumerator RunRecovery(float recoveryTime, string patternName)
    {
        if (IsBossDead()) yield break;

        SetState(BossState.Recovery);
        ResetTransformVisuals();
        SetSpriteColor(recoveryColor);
        StopMovement();
        Debug.Log($"[EnemyCorruptedHussarBoss2D] {patternName} 후딜 시작 | recovery={recoveryTime}", this);

        yield return WaitWhileAlive(recoveryTime);
        if (IsBossDead()) yield break;

        ResetVisuals();
        SetState(BossState.Idle);
        Debug.Log($"[EnemyCorruptedHussarBoss2D] {patternName} 후딜 종료 | 원본 연출값 복구", this);
    }

    private IEnumerator PlayTelegraphVisual(float seconds, Color telegraphColor, float scaleMultiplier)
    {
        float endTime = Time.time + Mathf.Max(0f, seconds);
        float nextBlinkTime = 0f;
        bool showTelegraphColor = true;
        transform.localScale = GetUniformScale(scaleMultiplier);

        while (Time.time < endTime && !IsBossDead())
        {
            if (Time.time >= nextBlinkTime)
            {
                showTelegraphColor = !showTelegraphColor;
                SetSpriteColor(showTelegraphColor ? telegraphColor : _originalColor);
                nextBlinkTime = Time.time + telegraphBlinkInterval;
            }

            transform.localScale = GetUniformScale(scaleMultiplier);
            StopMovement();
            yield return null;
        }

        SetSpriteColor(telegraphColor);
    }

    private IEnumerator PlayWingSweepShake()
    {
        float endTime = Time.time + wingShakeDuration;
        int shakeStep = 0;

        while (Time.time < endTime && !IsBossDead())
        {
            float sign = shakeStep % 2 == 0 ? 1f : -1f;
            float shakeAngle = Mathf.Lerp(4f, 10f, Mathf.Clamp01(wingShakeDistance / 0.2f));
            transform.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, shakeAngle * sign);
            shakeStep++;
            yield return new WaitForSeconds(0.03f);
        }

        transform.localRotation = _originalRotation;
    }

    private IEnumerator WaitWhileAlive(float seconds)
    {
        float endTime = Time.time + Mathf.Max(0f, seconds);
        while (Time.time < endTime && !IsBossDead())
        {
            StopMovement();
            yield return null;
        }
    }

    private bool CanStartAttack(float distanceToTarget)
    {
        if (Time.time < _nextAttackReadyTime || _patternRoutine != null)
        {
            return false;
        }

        float attackStartRange = Mathf.Max(sweepRadius, slamRadius, chaseRange);
        return distanceToTarget <= attackStartRange;
    }

    private BossPattern SelectPattern(float distanceToTarget)
    {
        if (distanceToTarget <= sweepRadius)
        {
            Debug.Log($"[EnemyCorruptedHussarBoss2D] 패턴 선택 | 가까운 거리={distanceToTarget:0.00}, Wing Sweep 우선", this);
            return BossPattern.WingSweep;
        }

        Debug.Log($"[EnemyCorruptedHussarBoss2D] 패턴 선택 | 거리={distanceToTarget:0.00}, Lance Charge 우선", this);
        return BossPattern.LanceCharge;
    }

    private int TryDealDamageInRange(Vector2 center, float radius, int damage, string debugLabel)
    {
        _lastHitCenter = center;
        _lastHitRadius = Mathf.Max(0f, radius);
        _hasLastHit = true;

        ContactFilter2D filter = CreatePlayerFilter();
        Collider2D[] hits = new Collider2D[32];
        int hitCount = Physics2D.OverlapCircle(center, _lastHitRadius, filter, hits);
        return ApplyDamageToHits(hits, hitCount, damage, debugLabel);
    }

    private int TryDealCloseDamageInRange(Vector2 center, float radius, int damage, string debugLabel)
    {
        _lastCloseHitCenter = center;
        _lastCloseHitRadius = Mathf.Max(0f, radius);
        _hasLastCloseHit = true;

        ContactFilter2D filter = CreatePlayerFilter();
        Collider2D[] hits = new Collider2D[32];
        int hitCount = Physics2D.OverlapCircle(center, _lastCloseHitRadius, filter, hits);
        return ApplyDamageToHits(hits, hitCount, damage, debugLabel);
    }

    private int ApplyDamageToHits(Collider2D[] hits, int hitCount, int damage, string debugLabel)
    {
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null && hit.transform.root != null)
            {
                damageable = hit.transform.root.GetComponentInChildren<IDamageable>();
            }

            if (damageable == null || !_damagedThisAttack.Add(damageable))
            {
                continue;
            }

            damageable.TakeDamage(damage, CombatAttribute.Justice);
            damagedCount++;
            Debug.Log($"[EnemyCorruptedHussarBoss2D] {debugLabel} 적중 | target={hit.name}, damage={damage}", this);
        }

        Debug.Log($"[EnemyCorruptedHussarBoss2D] {debugLabel} 판정 종료 | hitCount={hitCount}, damagedCount={damagedCount}", this);
        return damagedCount;
    }

    private ContactFilter2D CreatePlayerFilter()
    {
        EnsurePlayerLayerConfigured();

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = playerLayerMask;
        filter.useTriggers = true;
        return filter;
    }

    private void ResolveTarget()
    {
        if (PartyManager2D.Instance != null)
        {
            GameObject current = PartyManager2D.Instance.CurrentMember;
            _target = IsTargetCandidate(current) ? NormalizeTarget(current.transform) : null;
            return;
        }

        if (IsValidTarget(_target))
        {
            return;
        }

        Collider2D[] players = Physics2D.OverlapCircleAll(transform.position, chaseRange, playerLayerMask);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && IsTargetCandidate(players[i].gameObject))
            {
                _target = NormalizeTarget(players[i].transform);
                return;
            }
        }

        _target = null;
    }

    private bool IsTargetCandidate(GameObject candidate)
    {
        return candidate != null
            && IsInPlayerLayer(candidate)
            && IsValidTarget(candidate.transform);
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        Health targetHealth = FindTargetHealth(candidate);
        return targetHealth != null && !targetHealth.IsDead;
    }

    private Health FindTargetHealth(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Health health = candidate.GetComponent<Health>();
        if (health != null)
        {
            return health;
        }

        health = candidate.GetComponentInParent<Health>(true);
        return health != null ? health : candidate.GetComponentInChildren<Health>(true);
    }

    private Transform NormalizeTarget(Transform candidate)
    {
        Health health = FindTargetHealth(candidate);
        return health != null ? health.transform.root : candidate;
    }

    private Vector2 GetTargetPoint(Transform candidate)
    {
        if (candidate == null)
        {
            return transform.position;
        }

        Collider2D targetCollider = candidate.GetComponentInChildren<Collider2D>();
        return targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)candidate.position;
    }

    private void FaceCurrentTarget()
    {
        if (!IsValidTarget(_target))
        {
            return;
        }

        UpdateFacingDirection(GetTargetPoint(_target) - (Vector2)transform.position);
    }

    private void FacePoint(Vector2 point)
    {
        UpdateFacingDirection(point - (Vector2)transform.position);
    }

    private void UpdateFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _facingDirection = direction.normalized;
    }

    private void UpdateTargetMotion(Transform candidate)
    {
        if (!IsValidTarget(candidate))
        {
            _estimatedTargetVelocity = Vector2.zero;
            _lastTargetSampleTime = 0f;
            return;
        }

        Vector2 currentPoint = GetTargetPoint(candidate);
        if (_lastTargetSampleTime <= 0f)
        {
            _lastTargetPoint = currentPoint;
            _lastTargetSampleTime = Time.time;
            _estimatedTargetVelocity = Vector2.zero;
            return;
        }

        float deltaTime = Mathf.Max(0.0001f, Time.time - _lastTargetSampleTime);
        Vector2 rawVelocity = (currentPoint - _lastTargetPoint) / deltaTime;
        _estimatedTargetVelocity = Vector2.Lerp(_estimatedTargetVelocity, rawVelocity, 0.35f);
        _lastTargetPoint = currentPoint;
        _lastTargetSampleTime = Time.time;
    }

    private Vector2 GetPredictedSlamPoint(float leadTime)
    {
        if (!IsValidTarget(_target))
        {
            return transform.position;
        }

        UpdateTargetMotion(_target);
        Vector2 targetPoint = GetTargetPoint(_target);
        Vector2 predictedPoint = targetPoint + _estimatedTargetVelocity * Mathf.Max(0f, leadTime);
        Debug.DrawLine(targetPoint, predictedPoint, Color.magenta, slamTelegraphTime + 0.25f);
        return predictedPoint;
    }

    private void ApplyChargeVisual()
    {
        Vector3 scale = _originalScale;
        if (Mathf.Abs(_facingDirection.x) >= Mathf.Abs(_facingDirection.y))
        {
            scale.x *= chargeStretchMultiplier;
            scale.y *= 0.92f;
        }
        else
        {
            scale.x *= 0.92f;
            scale.y *= chargeStretchMultiplier;
        }

        transform.localScale = scale;
        float sign = _facingDirection.x >= 0f ? -1f : 1f;
        transform.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, chargeLeanAngle * sign);
    }

    private Vector2 GetForwardHitCenter(Vector2 origin, Vector2 direction, float forwardOffset)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : _facingDirection;

        if (safeDirection.sqrMagnitude <= 0.0001f)
        {
            safeDirection = Vector2.down;
        }

        return origin + safeDirection.normalized * Mathf.Max(0f, forwardOffset);
    }

    private void DrawSweepDebug(Vector2 sweepCenter)
    {
        Vector2 origin = transform.position;
        Vector2 side = new Vector2(-_facingDirection.y, _facingDirection.x);
        Vector2 leftEdge = (_facingDirection + side * 0.75f).normalized * sweepRadius;
        Vector2 rightEdge = (_facingDirection - side * 0.75f).normalized * sweepRadius;

        Debug.DrawLine(origin, sweepCenter, Color.yellow, 0.35f);
        Debug.DrawLine(sweepCenter, sweepCenter + leftEdge, Color.red, 0.35f);
        Debug.DrawLine(sweepCenter, sweepCenter + rightEdge, Color.red, 0.35f);
        Debug.DrawLine(sweepCenter + leftEdge, sweepCenter + rightEdge, Color.yellow, 0.35f);
    }

    private Vector3 GetUniformScale(float multiplier)
    {
        return _originalScale * Mathf.Max(0.01f, multiplier);
    }

    private void ResetVisuals()
    {
        SetSpriteColor(_originalColor);
        ResetTransformVisuals();
    }

    private void ResetTransformVisuals()
    {
        transform.localScale = _originalScale;
        transform.localRotation = _originalRotation;
    }

    private void SetState(BossState nextState)
    {
        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        Debug.Log($"[EnemyCorruptedHussarBoss2D] 상태 변경 | state={_state}", this);
    }

    private bool IsBossDead()
    {
        return _health != null && _health.IsDead;
    }

    private void EnterDeadState()
    {
        if (_state == BossState.Dead)
        {
            return;
        }

        if (_patternRoutine != null)
        {
            StopCoroutine(_patternRoutine);
            _patternRoutine = null;
        }

        StopMovement();
        ResetVisuals();
        SetState(BossState.Dead);
        Debug.Log("[EnemyCorruptedHussarBoss2D] 보스 사망 확인 | 모든 동작과 가짜 애니메이션 중지", this);
    }

    private void StopMovement()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private void SetSpriteColor(Color color)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = color;
        }
    }

    private bool IsInPlayerLayer(GameObject candidate)
    {
        EnsurePlayerLayerConfigured();
        return candidate != null && (playerLayerMask.value & (1 << candidate.layer)) != 0;
    }

    private void EnsurePlayerLayerConfigured()
    {
        if (playerLayerMask.value != 0)
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            playerLayerMask = 1 << playerLayer;
        }
    }

    private void OnValidate()
    {
        EnsurePlayerLayerConfigured();
    }

    private void OnDisable()
    {
        StopMovement();
        ResetVisuals();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Vector2 sweepCenter = GetForwardHitCenter(transform.position, _facingDirection, sweepForwardOffset);
        Gizmos.DrawWireSphere(sweepCenter, sweepRadius);

        Gizmos.color = new Color(1f, 0.45f, 0f);
        Gizmos.DrawWireSphere(transform.position, sweepCloseRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, slamRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + _facingDirection * Mathf.Max(1f, sweepRadius));

        Vector2 chargeCenter = GetForwardHitCenter(transform.position, _facingDirection, chargeHitForwardOffset);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(chargeCenter, chargeHitRadius);

        Gizmos.color = new Color(0.25f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, chargeCloseHitRadius);

        if (_hasLastHit)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_lastHitCenter, _lastHitRadius);
        }

        if (_hasLastCloseHit)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(_lastCloseHitCenter, _lastCloseHitRadius);
        }
    }
}
