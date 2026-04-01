using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat2D : BasePlayableCombat2D
{
    [Serializable]
    public class ComboEvent : UnityEvent<int> { }

    [Header("Hit Detection")]
    [Tooltip("공격 판정 기준점 Transform")]
    [SerializeField] private Transform attackPoint;
    [Tooltip("강공격 판정 반경")]
    [SerializeField] private float heavyAttackRange = 1.4f;
    [Tooltip("공격 대상 레이어 마스크")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Attack Definitions — 공격 계수")]
    [Tooltip("콤보 1타 계수·분류·속성. coefficient × FinalAttack = 기본 피해.")]
    [SerializeField] private AttackDefinition combo1Attack = new AttackDefinition { coefficient = 0.90f };
    [Tooltip("콤보 2타 계수·분류·속성")]
    [SerializeField] private AttackDefinition combo2Attack = new AttackDefinition { coefficient = 1.05f };
    [Tooltip("콤보 3타 계수·분류·속성")]
    [SerializeField] private AttackDefinition combo3Attack = new AttackDefinition { coefficient = 1.50f };
    [Tooltip("강공격 계수·분류·속성")]
    [SerializeField] private AttackDefinition heavyAttackDef = new AttackDefinition { coefficient = 1.80f };

    [Header("Ultimate — 궁극기")]
    [Tooltip("궁극기 계수·분류·속성.\n" +
             "category = Ultimate → UltimateDamageBonus 자동 적용.\n" +
             "coefficient를 높게 설정해 강력한 한 방으로 연출.")]
    [SerializeField] private AttackDefinition ultimateAttackDef = new AttackDefinition
    {
        coefficient = 4.0f,
        category    = AttackCategory.Ultimate,
        attribute   = CombatAttribute.Justice
    };

    [Tooltip("궁극기 판정 반경 (전방향 원형).")]
    [SerializeField] private float ultimateRange = 2.5f;

    [Tooltip("궁극기 판정 콘 각도. 360이면 전방향 원형.")]
    [SerializeField] private float ultimateAngle = 360f;

    [Tooltip("궁극기 입력 키. 기본: Q키.")]
    [SerializeField] private InputAction ultimateAction = new InputAction(
        name: "Ultimate",
        type: InputActionType.Button,
        binding: "<Keyboard>/q"
    );

    [Header("Combo Step 1")]
    [Tooltip("콤보 1타 판정 반경")]
    [SerializeField] private float combo1Range = 0.9f;
    [Tooltip("콤보 1타 콘 각도")]
    [SerializeField] private float combo1Angle = 70f;

    [Header("Combo Step 2")]
    [Tooltip("콤보 2타 판정 반경")]
    [SerializeField] private float combo2Range = 1.1f;
    [Tooltip("콤보 2타 콘 각도")]
    [SerializeField] private float combo2Angle = 95f;

    [Header("Combo Step 3")]
    [Tooltip("콤보 3타 판정 반경")]
    [SerializeField] private float combo3Range = 1.3f;
    [Tooltip("콤보 3타 콘 각도")]
    [SerializeField] private float combo3Angle = 120f;

    [Header("Combo Timing")]
    [Tooltip("최대 콤보 단계 수")]
    [SerializeField] private int maxComboStep = 3;
    [Tooltip("다음 콤보 입력을 받아들이는 시간 창 (초)")]
    [SerializeField] private float comboInputWindow = 0.35f;
    [Tooltip("콤보가 초기화되기까지의 대기 시간 (초)")]
    [SerializeField] private float comboResetDelay = 0.8f;
    [Tooltip("공격 간 최소 쿨다운 (초)")]
    [SerializeField] private float attackCooldown = 0.25f;

    [Header("Heavy Attack")]
    [Tooltip("강공격으로 판정되는 최소 버튼 홀드 시간 (초)")]
    [SerializeField] private float heavyHoldThreshold = 0.4f;
    [Tooltip("강공격 콘 각도")]
    [SerializeField] private float heavyAttackAngle = 110f;

    [Header("Aim")]
    [Tooltip("플레이어 중심에서 공격 포인트까지의 거리")]
    [SerializeField] private float attackPointDistance = 0.9f;

    [Header("Audio")]
    [Tooltip("피격·공격 사운드를 재생할 AudioSource")]
    [SerializeField] private AudioSource hitAudioSource;
    [Tooltip("일반 공격 피격 시 재생할 클립 목록")]
    [SerializeField] private AudioClip[] hitClips;
    [Tooltip("강공격 스윙 시 재생할 클립 목록")]
    [SerializeField] private AudioClip[] heavySwingClips;

    [Header("Hit Audio")]
    [Tooltip("피격음 재생용 AudioSource")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("총 피격 시 재생할 클립")]
    [SerializeField] private AudioClip gunHitSfx;
    [Tooltip("근접 피격 시 재생할 클립")]
    [SerializeField] private AudioClip meleeHitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.9f;

    [Header("Hit Stop")]
    [Tooltip("기본 히트 스탑 지속 시간 (초). 콤보별 값이 없거나 외부 호출 시 사용되는 폴백.")]
    [SerializeField] private float hitStopDuration = 0.05f;

    // [추가] 콤보 단계별 히트스탑 차등.
    // 3타는 마무리타 느낌으로 가장 길게 설정.
    [Header("Hit Stop — 콤보 차등")]
    [Tooltip("콤보 1타 히트스탑 (초). 가볍고 짧게.")]
    [SerializeField] private float combo1HitStop = 0.03f;
    [Tooltip("콤보 2타 히트스탑 (초).")]
    [SerializeField] private float combo2HitStop = 0.05f;
    [Tooltip("콤보 3타 히트스탑 (초). 마무리타 — 가장 길게.")]
    [SerializeField] private float combo3HitStop = 0.10f;
    [Tooltip("강공격 히트스탑 (초).")]
    [SerializeField] private float heavyHitStop = 0.12f;

    [Header("Knockback")]
    [Tooltip("넉백 힘 (폴백). 콤보별 값이 설정되어 있으면 해당 값 우선 적용.")]
    [SerializeField] private float knockbackForce = 5f;
    [Tooltip("넉백 지속 시간 (초). 모든 콤보에 공통 적용.\n" +
             "너무 짧으면 날아가는 구간이 눈에 안 보임. 0.18~0.25 권장.")]
    [SerializeField] private float knockbackDuration = 0.20f;

    // [추가] 콤보 단계별 넉백 힘 차등.
    // 3타와 강공격은 확실히 날아가는 느낌이 나도록 더 강하게 설정.
    // 적 Rigidbody2D mass=1 기준: Impulse 후 초기속도 = force/mass (units/s)
    // walkSpeed=2.5 대비 확실히 빠르게 밀려야 읽힘 → 최소 4 이상 권장.
    [Header("Knockback — 콤보 차등")]
    [Tooltip("콤보 1타 넉백 힘. 가볍게 밀리는 느낌.")]
    [SerializeField] private float combo1KnockbackForce = 4f;
    [Tooltip("콤보 2타 넉백 힘.")]
    [SerializeField] private float combo2KnockbackForce = 6f;
    [Tooltip("콤보 3타 넉백 힘. 마무리타 — 확실히 날아가는 느낌.")]
    [SerializeField] private float combo3KnockbackForce = 9f;
    [Tooltip("강공격 넉백 힘. 가장 강하게.")]
    [SerializeField] private float heavyKnockbackForce = 12f;

    // [추가] 콤보 단계별 카메라 셰이크 강도.
    // 너무 과한 흔들림 방지를 위해 기본값을 작게 설정.
    [Header("Camera Shake")]
    [Tooltip("콤보 1타 카메라 셰이크 강도.")]
    [SerializeField] private float combo1ShakeIntensity = 0.04f;
    [Tooltip("콤보 2타 카메라 셰이크 강도.")]
    [SerializeField] private float combo2ShakeIntensity = 0.07f;
    [Tooltip("콤보 3타 카메라 셰이크 강도. 마무리타 — 가장 강하게.")]
    [SerializeField] private float combo3ShakeIntensity = 0.12f;
    [Tooltip("강공격 카메라 셰이크 강도.")]
    [SerializeField] private float heavyShakeIntensity = 0.15f;

    // [추가] 적 피격 경직.
    // 넉백(물리 이동)과 별개로 적 AI를 짧게 멈추게 하는 행동 경직.
    // 넉백 종료 직후에도 이 시간만큼 추가로 AI가 멈춰 "맞았다" 반응이 읽힘.
    // 콤보가 빠를 때 샌드백이 되지 않도록 Mathf.Max로 중첩을 막아둠.
    [Header("Hit Stagger — 피격 경직")]
    [Tooltip("콤보 1타 피격 경직 시간 (초). 짧은 멈칫.")]
    [SerializeField] private float combo1StaggerDuration = 0.12f;
    [Tooltip("콤보 2타 피격 경직 시간 (초).")]
    [SerializeField] private float combo2StaggerDuration = 0.18f;
    [Tooltip("콤보 3타 피격 경직 시간 (초). 마무리타 — 가장 길게.")]
    [SerializeField] private float combo3StaggerDuration = 0.26f;
    [Tooltip("강공격 피격 경직 시간 (초). 적이 확실히 멈추는 느낌.")]
    [SerializeField] private float heavyStaggerDuration = 0.35f;

    // [추가] 공격 중 이동 감속.
    // 공격 직후 짧은 시간 동안 이동 속도에 배율을 적용해 타격 무게감을 부여.
    // 1 = 감속 없음, 0 = 완전 정지. 0.1~0.4 범위가 타격감과 조작성 사이 균형점.
    [Header("Attack Slow — 이동 감속")]
    [Tooltip("콤보 1타 이동 속도 배율 (0~1). 짧고 가볍게.")]
    [SerializeField, Range(0f, 1f)] private float combo1SlowMultiplier = 0.35f;
    [Tooltip("콤보 2타 이동 속도 배율 (0~1).")]
    [SerializeField, Range(0f, 1f)] private float combo2SlowMultiplier = 0.28f;
    [Tooltip("콤보 3타 이동 속도 배율 (0~1). 마무리타 — 가장 무겁게.")]
    [SerializeField, Range(0f, 1f)] private float combo3SlowMultiplier = 0.18f;
    [Tooltip("강공격 이동 속도 배율 (0~1). 거의 멈추는 느낌.")]
    [SerializeField, Range(0f, 1f)] private float heavySlowMultiplier = 0.08f;

    [Tooltip("콤보 1타 감속 지속 시간 (초).")]
    [SerializeField] private float combo1SlowDuration = 0.10f;
    [Tooltip("콤보 2타 감속 지속 시간 (초).")]
    [SerializeField] private float combo2SlowDuration = 0.13f;
    [Tooltip("콤보 3타 감속 지속 시간 (초).")]
    [SerializeField] private float combo3SlowDuration = 0.17f;
    [Tooltip("강공격 감속 지속 시간 (초).")]
    [SerializeField] private float heavySlowDuration = 0.24f;

    [Header("Events")]
    [Tooltip("콤보 공격 시 발생하는 이벤트 (콤보 단계 전달)")]
    [SerializeField] private ComboEvent onComboAttack;
    [Tooltip("강공격 시 발생하는 이벤트")]
    [SerializeField] private UnityEvent onHeavyAttack;

    private PlayerInput _playerInput;
    private InputAction _attackAction;

    private int _currentComboStep;
    private float _lastComboTime;
    private float _attackPressStartTime;
    private float _nextComboAttackTime;
    private Vector2 _lastAimDirection = Vector2.right;
    private Camera _mainCamera;
    private bool _hasLoggedMissingAttackPoint;
    private Coroutine _hitStopRoutine;
    private Coroutine _attackSlowRoutine;

    // 공격 적중 시 카메라 셰이크 호출용 (null-safe: 없어도 동작)
    private CameraFollow2D _cameraShake;

    // 공격 중 이동 감속 호출용 (null-safe: 없어도 동작)
    private PlayerController2D _controller;

    protected override void Awake()
    {
        base.Awake(); // CharacterStats 탐색 (_stats 초기화)
        _playerInput = GetComponent<PlayerInput>();
        _attackAction = _playerInput.actions.FindAction("Attack", true);
        _mainCamera = Camera.main;
        hitAudioSource = hitAudioSource != null ? hitAudioSource : GetComponent<AudioSource>();
        ResolveAttackPoint();

        // 카메라 셰이크 컴포넌트 탐색. 없으면 경고만 출력하고 셰이크 없이 동작.
        _cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (_cameraShake == null)
            Debug.LogWarning("[PlayerCombat2D] CameraFollow2D를 찾을 수 없습니다. 카메라 셰이크가 동작하지 않습니다.", this);

        // 이동 감속용 PlayerController2D 탐색. null-safe: 없으면 감속 없이 동작.
        _controller = GetComponent<PlayerController2D>();

        if (targetLayer.value == 0)
        {
            Debug.LogWarning("[PlayerCombat2D] targetLayer가 비어 있습니다. 레이어를 지정하기 전까지 적에게 데미지가 들어가지 않습니다.", this);
        }
    }

    private void OnValidate()
    {
        ResolveAttackPoint();
    }

    private void OnEnable()
    {
        Debug.Log($"[PlayerCombat2D] OnEnable ← {gameObject.name}", this);

        // PlayerInput이 ActionMap을 끈 경우에도 동작하도록 개별 Enable
        _attackAction.Enable();
        _attackAction.started  += OnAttackStarted;
        _attackAction.canceled += OnAttackCanceled;

        ultimateAction.performed += OnUltimatePerformed;
        ultimateAction.Enable();
    }

    private void OnDisable()
    {
        Debug.Log($"[PlayerCombat2D] OnDisable ← {gameObject.name}", this);
        _attackAction.started  -= OnAttackStarted;
        _attackAction.canceled -= OnAttackCanceled;
        _attackAction.Disable(); // standby 캐릭터 입력 차단

        ultimateAction.performed -= OnUltimatePerformed;
        ultimateAction.Disable();

        // 감속 도중 컴포넌트가 꺼지면 배율이 낮은 채로 남음 — 즉시 복구
        if (_controller != null)
            _controller.AttackSpeedMultiplier = 1f;
    }

    private void OnUltimatePerformed(UnityEngine.InputSystem.InputAction.CallbackContext _)
    {
        TryTriggerUltimate();
    }

    private void Update()
    {
        ResolveAttackPoint();
        UpdateAttackPointFromMouse();

        if (_currentComboStep > 0 && Time.time - _lastComboTime > comboResetDelay)
        {
            _currentComboStep = 0;
        }
    }

    private void UpdateAttackPointFromMouse()
    {
        if (attackPoint == null)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        float depthFromCamera = Mathf.Abs(transform.position.z - _mainCamera.transform.position.z);

        Vector3 mouseWorldPosition = _mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, depthFromCamera)
        );

        Vector2 rawAimDirection = (Vector2)(mouseWorldPosition - transform.position);
        if (rawAimDirection.sqrMagnitude > 0.0001f)
        {
            _lastAimDirection = rawAimDirection.normalized;
        }

        attackPoint.position = (Vector2)transform.position + (_lastAimDirection * attackPointDistance);

        // [추가] AttackPoint 회전을 조준 방향으로 업데이트.
        // MeleeSlashVFX 등 AttackPoint를 기준으로 스폰되는 VFX가
        // transform.rotation으로 조준 방향을 읽을 수 있도록.
        float aimAngle = Mathf.Atan2(_lastAimDirection.y, _lastAimDirection.x) * Mathf.Rad2Deg;
        attackPoint.rotation = Quaternion.Euler(0f, 0f, aimAngle);
    }

    private void OnAttackStarted(InputAction.CallbackContext context)
    {
        _attackPressStartTime = Time.time;
        // [수정] 콤보 공격은 버튼 누르는 순간(pressed) 즉시 발동.
        // 이전에는 canceled(릴리즈)에서 발동하여 입력 반응이 늦게 느껴졌음.
        TriggerComboAttack();
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        float heldTime = Time.time - _attackPressStartTime;

        // 강공격만 릴리즈 시점에 홀드 시간 기준으로 발동.
        // 콤보는 이미 OnAttackStarted에서 처리됨.
        if (heldTime >= heavyHoldThreshold)
        {
            TriggerHeavyAttack();
        }
    }

    private void TriggerComboAttack()
    {
        if (Time.time < _nextComboAttackTime)
        {
            return;
        }

        if (Time.time - _lastComboTime > comboInputWindow)
        {
            _currentComboStep = 0;
        }

        _currentComboStep = (_currentComboStep % maxComboStep) + 1;
        _lastComboTime = Time.time;
        _nextComboAttackTime = Time.time + Mathf.Max(0.01f, attackCooldown);

        AttackDefinition attackDef = GetComboAttackDef(_currentComboStep);
        int damage = CalculateSkillDamage(attackDef, fallbackFlatAttack: 10);
        GetComboStepGeometry(_currentComboStep, out float range, out float angle);
        ApplyAttackSlow(_currentComboStep);
        PerformAttack(range, damage, angle, attackDef.attribute, $"Combo {_currentComboStep}", comboStep: _currentComboStep);
        onComboAttack?.Invoke(_currentComboStep);
    }

    private void TriggerHeavyAttack()
    {
        _currentComboStep = 0;
        _lastComboTime = 0f;

        PlayRandomHeavySwingSound();
        int heavyDmg = CalculateSkillDamage(heavyAttackDef, fallbackFlatAttack: 14); // 14 × 1.8 ≈ 25
        ApplyAttackSlow(comboStep: 0);
        PerformAttack(heavyAttackRange, heavyDmg, heavyAttackAngle, heavyAttackDef.attribute, "Heavy", comboStep: 0);
        onHeavyAttack?.Invoke();
    }

    // comboStep: 0=강공격, 1~3=콤보 단계. 히트스탑/넉백/셰이크 강도 분기에 사용.
    // attribute: AttackDefinition에서 전달 — 하드코딩 제거
    private void PerformAttack(float range, int damage, float attackAngle,
        CombatAttribute attribute, string attackType, int comboStep = 1)
    {
        if (!ResolveAttackPoint())
        {
            return;
        }

        Vector2 origin = attackPoint.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        int damagedCount = MeleeHitResolver2D.DealDamageInCone(
            origin,
            aimDirection,
            attackPointDistance,
            range,
            attackAngle,
            damage,
            targetLayer,
            this,
            $"PlayerCombat2D/{attackType}",
            attribute
        );

        if (damagedCount > 0)
        {
            // [수정] 콤보 단계별로 넉백 힘, 히트스탑, 카메라 셰이크 강도를 달리 적용
            // [추가] 피격 경직(staggerDuration)을 함께 전달 — 넉백 루프 내에서 ApplyStagger 호출됨
            ApplyKnockbackToHitTargets(origin, aimDirection, range, attackAngle,
                GetKnockbackForce(comboStep), GetStaggerDuration(comboStep));
            PlayHitSfx(isMelee: true);
            TriggerHitStop(GetHitStopDuration(comboStep));
            _cameraShake?.Shake(GetShakeIntensity(comboStep));

            // 평타 적중 시 궁극기 게이지 획득
            // 궁극기(comboStep < 0) 타격은 게이지를 올리지 않음
            if (comboStep >= 0)
                AddUltimateGauge(basicAttackHitGain);
        }
    }

    /// <summary>
    /// [궁극기] 근접 전방향 AoE 타격.
    /// TryTriggerUltimate() → ExecuteUltimate() 순으로 호출됨.
    ///
    /// 캐릭터 설정으로 달라지는 것:
    ///   ultimateAttackDef (coefficient, attribute)
    ///   ultimateRange, ultimateAngle
    /// 계산 공식은 CalculateSkillDamage()로 공통 처리.
    /// </summary>
    protected override void ExecuteUltimate()
    {
        if (!ResolveAttackPoint()) return;

        int dmg = CalculateSkillDamage(ultimateAttackDef, fallbackFlatAttack: 30);
        Vector2 origin = attackPoint.position;
        // 전방향(360°)이면 aimDirection은 관계없음 — 기준으로 right 사용
        Vector2 aimDir = ultimateAngle >= 360f ? Vector2.right : GetCurrentAimDirection();

        int damagedCount = MeleeHitResolver2D.DealDamageInCone(
            origin,
            aimDir,
            attackPointDistance,
            ultimateRange,
            ultimateAngle,
            dmg,
            targetLayer,
            this,
            "PlayerCombat2D/Ultimate",
            ultimateAttackDef.attribute
        );

        if (damagedCount > 0)
        {
            PlayHitSfx(isMelee: true);
            TriggerHitStop(0.15f);
            _cameraShake?.Shake(0.20f);
        }

        Debug.Log($"[PlayerCombat2D] 궁극기 — 피격 수: {damagedCount} / 데미지: {dmg}", this);
    }

    private bool ResolveAttackPoint()
    {
        if (attackPoint != null)
        {
            _hasLoggedMissingAttackPoint = false;
            return true;
        }

        Transform found = transform.Find("AttackPoint");
        if (found != null)
        {
            attackPoint = found;
            _hasLoggedMissingAttackPoint = false;
            return true;
        }

        if (!_hasLoggedMissingAttackPoint)
        {
            Debug.LogError("[PlayerCombat2D] attackPoint가 없습니다. AttackPoint를 지정하거나 'AttackPoint'라는 이름의 자식 오브젝트를 추가하세요.", this);
            _hasLoggedMissingAttackPoint = true;
        }

        return false;
    }

    private Vector2 GetCurrentAimDirection()
    {
        Vector2 aimDirection = _lastAimDirection;
        if (attackPoint != null)
        {
            Vector2 fromPlayerToAttackPoint = (Vector2)attackPoint.position - (Vector2)transform.position;
            if (fromPlayerToAttackPoint.sqrMagnitude > 0.0001f)
            {
                aimDirection = fromPlayerToAttackPoint.normalized;
            }
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            aimDirection = Vector2.right;
        }

        return aimDirection;
    }

    // 콤보 단계 → AttackDefinition 반환 (계수·속성 포함)
    private AttackDefinition GetComboAttackDef(int comboStep)
    {
        switch (comboStep)
        {
            case 1:  return combo1Attack;
            case 2:  return combo2Attack;
            default: return combo3Attack;
        }
    }

    // 콤보 단계 → 판정 반경·각도만 반환 (피격 지오메트리 전용)
    private void GetComboStepGeometry(int comboStep, out float range, out float angle)
    {
        switch (comboStep)
        {
            case 1:  range = combo1Range; angle = combo1Angle; break;
            case 2:  range = combo2Range; angle = combo2Angle; break;
            default: range = combo3Range; angle = combo3Angle; break;
        }
    }

    // 근접(isMelee=true) 또는 총(isMelee=false) 피격음 재생
    public void PlayHitSfx(bool isMelee)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("[PlayerCombat2D] AudioSource가 설정되지 않았습니다.", this);
            return;
        }

        AudioClip clip = isMelee ? meleeHitSfx : gunHitSfx;

        if (clip == null)
        {
            Debug.LogWarning("[PlayerCombat2D] 피격 사운드 클립이 설정되지 않았습니다.", this);
            return;
        }

        audioSource.PlayOneShot(clip, hitSfxVolume);
    }

    private void PlayRandomHitSound()
    {
        if (hitAudioSource == null)
        {
            Debug.LogWarning("[PlayerCombat2D] 피격음 재생 생략: hitAudioSource가 null입니다.", this);
            return;
        }

        if (hitClips == null || hitClips.Length == 0)
        {
            Debug.LogWarning("[PlayerCombat2D] 피격음 재생 생략: hitClips가 비어 있습니다.", this);
            return;
        }

        int clipIndex = UnityEngine.Random.Range(0, hitClips.Length);
        AudioClip clip = hitClips[clipIndex];
        if (clip == null)
        {
            Debug.LogWarning($"[PlayerCombat2D] 피격음 재생 생략: 인덱스 {clipIndex}의 클립이 null입니다.", this);
            return;
        }

        Debug.Log($"[PlayerCombat2D] 피격음 재생: '{clip.name}' (인덱스 {clipIndex})", this);
        hitAudioSource.PlayOneShot(clip);
    }

    private void PlayRandomHeavySwingSound()
    {
        if (hitAudioSource == null)
        {
            Debug.LogWarning("[PlayerCombat2D] 강공격 스윙음 재생 생략: hitAudioSource가 null입니다.", this);
            return;
        }

        if (heavySwingClips == null || heavySwingClips.Length == 0)
        {
            Debug.LogWarning("[PlayerCombat2D] 강공격 스윙음 재생 생략: heavySwingClips가 비어 있습니다.", this);
            return;
        }

        int clipIndex = UnityEngine.Random.Range(0, heavySwingClips.Length);
        AudioClip clip = heavySwingClips[clipIndex];
        if (clip == null)
        {
            Debug.LogWarning($"[PlayerCombat2D] 강공격 스윙음 재생 생략: 인덱스 {clipIndex}의 클립이 null입니다.", this);
            return;
        }

        Debug.Log($"[PlayerCombat2D] 강공격 스윙음 재생: '{clip.name}' (인덱스 {clipIndex})", this);
        hitAudioSource.PlayOneShot(clip);
    }

    // forceOverride < 0이면 Inspector의 knockbackForce 폴백 사용
    // staggerDuration > 0이면 넉백과 별개로 피격 경직도 함께 적용
    private void ApplyKnockbackToHitTargets(Vector2 origin, Vector2 aimDirection, float range, float attackAngle, float forceOverride = -1f, float staggerDuration = 0f)
    {
        float force = forceOverride >= 0f ? forceOverride : knockbackForce;
        if (force <= 0f || knockbackDuration <= 0f)
        {
            return;
        }

        float coneRange = MeleeHitResolver2D.GetConeRange(attackPointDistance, range);
        float halfAngle = Mathf.Clamp(attackAngle * 0.5f, 0f, 180f);
        float minDot = Mathf.Cos(Mathf.Deg2Rad * halfAngle);
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, coneRange, targetLayer);
        HashSet<KnockbackReceiver2D> knockedTargets = new HashSet<KnockbackReceiver2D>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            Vector2 targetPoint = hit.ClosestPoint(origin);
            Vector2 toTarget = targetPoint - origin;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = (Vector2)hit.transform.position - origin;
            }

            if (toTarget.sqrMagnitude > coneRange * coneRange)
            {
                continue;
            }

            float dot = Vector2.Dot(aimDirection, toTarget.normalized);
            if (dot < minDot)
            {
                continue;
            }

            KnockbackReceiver2D knockbackReceiver = hit.GetComponentInParent<KnockbackReceiver2D>();
            if (knockbackReceiver == null)
            {
                // 넉백이 적용되지 않음 — 이 경고가 보이면 해당 오브젝트에
                // KnockbackReceiver2D 컴포넌트를 추가하세요.
                Debug.LogWarning($"[PlayerCombat2D] {hit.gameObject.name} 에 KnockbackReceiver2D가 없어 넉백이 적용되지 않습니다.", hit.gameObject);
                continue;
            }
            if (!knockedTargets.Add(knockbackReceiver))
            {
                continue;
            }

            knockbackReceiver.ApplyKnockback(toTarget.normalized, force, knockbackDuration);

            // [추가] 넉백과 별개로 피격 경직 적용.
            // 경직은 행동(추적/공격 AI) 차단이고, 넉백은 물리 이동 — 역할이 다름.
            if (staggerDuration > 0f)
                knockbackReceiver.ApplyStagger(staggerDuration);
        }
    }

    // [수정] public으로 변경 — PlayerRangedAttack2D 등 외부에서도 히트스탑을 요청할 수 있도록.
    // durationOverride >= 0이면 해당 값 사용, 아니면 Inspector의 hitStopDuration 폴백 사용.
    public void TriggerHitStop(float durationOverride = -1f)
    {
        float d = durationOverride >= 0f ? durationOverride : hitStopDuration;
        if (d <= 0f)
        {
            return;
        }

        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            Time.timeScale = 1f;
        }

        _hitStopRoutine = StartCoroutine(HitStopRoutine(d));
    }

    // [수정] duration을 파라미터로 받아 콤보별 차등 히트스탑 지원
    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        _hitStopRoutine = null;
    }

    // ─────────────────────────────────────────────
    // 콤보 단계별 피드백 수치 분기 헬퍼
    // comboStep: 0=강공격, 1=1타, 2=2타, 3이상=3타(마무리)
    // ─────────────────────────────────────────────
    private float GetHitStopDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyHitStop;
            case 1: return combo1HitStop;
            case 2: return combo2HitStop;
            default: return combo3HitStop;
        }
    }

    private float GetKnockbackForce(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyKnockbackForce;
            case 1: return combo1KnockbackForce;
            case 2: return combo2KnockbackForce;
            default: return combo3KnockbackForce;
        }
    }

    private float GetShakeIntensity(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyShakeIntensity;
            case 1: return combo1ShakeIntensity;
            case 2: return combo2ShakeIntensity;
            default: return combo3ShakeIntensity;
        }
    }

    private float GetStaggerDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavyStaggerDuration;
            case 1: return combo1StaggerDuration;
            case 2: return combo2StaggerDuration;
            default: return combo3StaggerDuration;
        }
    }

    // ─────────────────────────────────────────────
    // 공격 중 이동 감속
    // ─────────────────────────────────────────────

    /// <summary>
    /// 공격 스윙 시 즉시 호출. PlayerController2D의 AttackSpeedMultiplier를
    /// 지정 배율로 낮춘 뒤 지속 시간 후 1로 복구.
    /// 연속 공격 시 이전 코루틴을 취소하고 새 배율로 덮어씀.
    /// </summary>
    private void ApplyAttackSlow(int comboStep)
    {
        if (_controller == null) return;

        float multiplier = GetSlowMultiplier(comboStep);
        float duration   = GetSlowDuration(comboStep);

        if (_attackSlowRoutine != null)
        {
            StopCoroutine(_attackSlowRoutine);
            // 이전 감속 코루틴이 복구 전에 중단됐으므로 직접 새 값 적용
        }

        _attackSlowRoutine = StartCoroutine(AttackSlowRoutine(multiplier, duration));
    }

    private IEnumerator AttackSlowRoutine(float multiplier, float duration)
    {
        _controller.AttackSpeedMultiplier = multiplier;
        // WaitForSecondsRealtime: 히트스탑(timeScale=0) 중에도 감속 타이머 진행
        yield return new WaitForSecondsRealtime(duration);
        _controller.AttackSpeedMultiplier = 1f;
        _attackSlowRoutine = null;
    }

    private float GetSlowMultiplier(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavySlowMultiplier;
            case 1: return combo1SlowMultiplier;
            case 2: return combo2SlowMultiplier;
            default: return combo3SlowMultiplier;
        }
    }

    private float GetSlowDuration(int comboStep)
    {
        switch (comboStep)
        {
            case 0: return heavySlowDuration;
            case 1: return combo1SlowDuration;
            case 2: return combo2SlowDuration;
            default: return combo3SlowDuration;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Vector2 aimDirection = GetCurrentAimDirection();
        float coneRange = MeleeHitResolver2D.GetConeRange(attackPointDistance, combo3Range);
        float halfAngle = combo3Angle * 0.5f;

        Vector2 leftEdge = Quaternion.Euler(0f, 0f, -halfAngle) * aimDirection;
        Vector2 rightEdge = Quaternion.Euler(0f, 0f, halfAngle) * aimDirection;

        // 플레이어 원점 마커
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);

        // 현재 공격 방향
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + (Vector3)(aimDirection * coneRange));

        // 콘 경계선
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + (Vector3)(leftEdge * coneRange));
        Gizmos.DrawLine(origin, origin + (Vector3)(rightEdge * coneRange));

        // 콘 호
        const int arcSegments = 24;
        Vector3 previousPoint = origin + (Vector3)(leftEdge * coneRange);
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 arcDirection = Quaternion.Euler(0f, 0f, angle) * aimDirection;
            Vector3 currentPoint = origin + (Vector3)(arcDirection * coneRange);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
