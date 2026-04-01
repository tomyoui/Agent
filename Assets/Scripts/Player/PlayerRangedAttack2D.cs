using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────
// 발사 모드 — Inspector에서 캐릭터/무기별로 설정
// ─────────────────────────────────────────────────────────
/// <summary>
/// Single : 클릭당 1발 즉발
/// Burst  : 클릭당 burstCount발 연속 발사 (기본 4연발)
/// Auto   : 버튼 홀드 중 반복 발사
///
/// 차후 추가 가능 자리:
///   Shotgun  — 1클릭에 여러 방향 Raycast (부채꼴)
///   Charge   — 홀드 시간에 비례해 강화 발사
/// </summary>
public enum FireMode { Single, Burst, Auto }


/// <summary>
/// [파멸(Doom) 속성] 원거리 히트스캔 공격 + 백스텝 유틸 이동.
///
/// [E 스킬 동작]
///   E 키 → Burst 연사 + 동시에 사격 반대 방향으로 백스텝
///   → 공격하면서 뒤로 빠지는 "공격+회피" 복합 유틸기
///
/// [백스텝 설계 방침]
///   단순 시각 반동(Recoil)이 아닌 실제 위치 이동(rb.MovePosition).
///   PlayerController2D.IsVelocityLocked를 통해 일반 이동과 충돌 없이 분리.
///   벽 충돌은 CircleCast로 사전 감지 → 벽 속으로 관통하지 않음.
///
/// [이펙트 연결 지점]
///   OnMuzzleFlash()     — 총구 이펙트
///   OnDoomHitEffect()   — 파멸 속성 피격 이펙트
///   OnBackstepStart()   — 백스텝 시작 이펙트 (SFX, VFX 등)
///   OnBackstepEnd()     — 백스텝 종료 이펙트
/// </summary>
public class PlayerRangedAttack2D : BasePlayableCombat2D
{
    // ─────────────────────────────────────────────
    // Inspector — E 스킬 공격 정의
    // ─────────────────────────────────────────────
    [Header("Attack Definition — E 스킬 계수")]
    [Tooltip("E 스킬 공격 계수·분류·속성.\n" +
             "coefficient × FinalAttack = 기본 피해.\n" +
             "category = Skill → SkillDamageBonus 자동 적용.\n" +
             "attribute = Doom → DoomDamageBonus 자동 적용.")]
    [SerializeField] private AttackDefinition eSkillAttackDef = new AttackDefinition
    {
        coefficient = 1.1f,
        category    = AttackCategory.Skill,   // 스킬 보너스 적용
        attribute   = CombatAttribute.Doom    // 파멸 속성 보너스 적용
    };

    // ─────────────────────────────────────────────
    // Inspector — 입력
    // ─────────────────────────────────────────────
    [Header("Input")]
    [Tooltip("발사 액션. 기본: E키. 인스펙터 '+' 버튼으로 자유롭게 변경 가능.")]
    [SerializeField] private InputAction fireAction = new InputAction(
        name: "DoomGunFire",
        type: InputActionType.Button,
        binding: "<Keyboard>/e"
    );

    // ─────────────────────────────────────────────
    // Inspector — 발사 모드
    // ─────────────────────────────────────────────
    [Header("Fire Mode")]
    [Tooltip("Single=단발, Burst=점사, Auto=연사")]
    [SerializeField] private FireMode fireMode = FireMode.Burst;

    [Tooltip("Burst 모드: 한 번 클릭에 발사할 총알 수 (기본 4발)")]
    [SerializeField] private int burstCount = 4;

    [Tooltip("Burst / Auto 모드: 탄 사이 간격 (초). 작을수록 빠름.")]
    [SerializeField] private float burstInterval = 0.08f;

    // ─────────────────────────────────────────────
    // Inspector — 히트스캔
    // ─────────────────────────────────────────────
    [Header("Hitscan")]
    [Tooltip("히트스캔 최대 사거리 (유닛)")]
    [SerializeField] private float range = 20f;

    [Tooltip("데미지를 줄 대상 레이어 (Enemy 등)")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("총알을 막는 벽 레이어. 0이면 관통.")]
    [SerializeField] private LayerMask wallLayer;

    // ─────────────────────────────────────────────
    // Inspector — 발사 기점 / 쿨타임
    // ─────────────────────────────────────────────
    [Header("Origin & Cooldown")]
    [Tooltip("발사 기점. 비워두면 플레이어 중심 기준.")]
    [SerializeField] private Transform firePoint;

    [Tooltip("버스트/단발 완료 후 다음 발사까지 대기 시간 (초)")]
    [SerializeField] private float cooldown = 0.6f;

    // ─────────────────────────────────────────────
    // Inspector — 오디오
    // ─────────────────────────────────────────────
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSfx;
    [SerializeField, Range(0f, 1f)] private float fireSfxVolume = 0.9f;

    // ─────────────────────────────────────────────
    // Inspector — 적중 피드백
    // ─────────────────────────────────────────────
    [Header("Hit Feedback")]
    [Tooltip("총 적중 시 히트스탑 지속 시간 (초). 근접보다 짧게 유지 권장.")]
    [SerializeField] private float gunHitStopDuration = 0.02f;

    // ─────────────────────────────────────────────
    // Inspector — VFX
    // ─────────────────────────────────────────────
    [Header("VFX")]
    [Tooltip("총구 플래시 프리팹. 할당 시 발사마다 Instantiate 후 자동 제거됨.")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [Tooltip("적 피격 이펙트 프리팹. 할당 시 피격 위치에 Instantiate 후 자동 제거됨.")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("탄환 궤적 LineRenderer. 총구~피격점 사이를 짧게 표시.\n" +
             "미할당 시 트레이서 없이 동작.")]
    [SerializeField] private LineRenderer tracerRenderer;
    [Tooltip("트레이서 표시 지속 시간 (초).")]
    [SerializeField] private float tracerDuration = 0.05f;

    // ─────────────────────────────────────────────
    // Inspector — 백스텝 (유틸 이동)
    // ─────────────────────────────────────────────
    [Header("Backstep — Utility Move")]
    [Tooltip("발사 시 뒤로 빠지는 백스텝 활성화 여부")]
    [SerializeField] private bool enableBackstep = true;

    [Tooltip("백스텝 이동 거리 (유닛). 권장: 2~3.5")]
    [SerializeField] private float backstepDistance = 2.5f;

    [Tooltip("백스텝 완료까지 걸리는 시간 (초). 권장: 0.15~0.25\n" +
             "짧을수록 스냅하는 느낌, 길수록 미끄러지는 느낌.")]
    [SerializeField] private float backstepDuration = 0.2f;

    [Tooltip("벽 충돌 감지용 CircleCast 반경.\n" +
             "플레이어 콜라이더 반경과 맞춰주세요 (기본 0.3).")]
    [SerializeField] private float backstepColliderRadius = 0.3f;

    [Tooltip("백스텝을 막을 벽 레이어. wallLayer와 동일하게 설정 권장.\n" +
             "0이면 벽 충돌 체크 없이 이동 (관통).")]
    [SerializeField] private LayerMask backstepWallLayer;

    // ─────────────────────────────────────────────
    // 런타임 상태
    // ─────────────────────────────────────────────
    private float _nextFireTime;
    private bool _isBurstActive;
    private Camera _mainCamera;

    // 백스텝이 PlayerController2D.FixedUpdate와 충돌하지 않도록 참조 보관
    private Rigidbody2D _rb;
    private PlayerController2D _controller;  // null-safe: 없어도 동작
    private PlayerCombat2D _playerCombat;    // 총 피격음/히트스탑 호출용, null-safe
    private Coroutine _hideTracerRoutine;

    // ─────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake(); // CharacterStats 탐색 (_stats 초기화)
        _mainCamera = Camera.main;
        _rb = GetComponent<Rigidbody2D>();
        _controller = GetComponent<PlayerController2D>(); // 없으면 null (경고 없이 처리)
        _playerCombat = GetComponent<PlayerCombat2D>();   // 없으면 null (경고 없이 처리)

        // [수정] 기존에는 _playerCombat이 null이어도 조용히 실패했음.
        // 인스펙터 구성 실수를 빠르게 발견할 수 있도록 Awake에서 경고 출력.
        if (_playerCombat == null)
            Debug.LogWarning("[DoomGun] PlayerCombat2D가 같은 GameObject에 없습니다. 총 피격음과 히트스탑이 동작하지 않습니다.", this);

        if (_rb == null)
            Debug.LogWarning("[DoomGun] Rigidbody2D가 없습니다. 백스텝이 동작하지 않습니다.", this);

        if (targetLayer.value == 0)
            Debug.LogWarning("[DoomGun] targetLayer가 비어있습니다. 인스펙터에서 Enemy 레이어를 설정하세요.", this);

        if (enableBackstep && backstepWallLayer.value == 0)
            Debug.LogWarning("[DoomGun] backstepWallLayer가 비어있습니다. 백스텝 시 벽 관통이 발생할 수 있습니다.", this);
    }

    private void OnEnable()
    {
        Debug.Log($"[PlayerRangedAttack2D] OnEnable ← {gameObject.name}", this);
        // fireAction은 PartyManager2D가 E키를 라우팅해 TryFire()를 직접 호출함.
        // 자체 Enable/구독 없이 외부 호출 전담 구조로 변경.
    }

    private void OnDisable()
    {
        Debug.Log($"[PlayerRangedAttack2D] OnDisable ← {gameObject.name}", this);
        _isBurstActive = false;

        // 비활성화 시 잠금 해제 — 안전장치
        if (_controller != null)
            _controller.IsVelocityLocked = false;
    }

    /// <summary>
    /// PartyManager2D에서 E키 입력 시 직접 호출하는 발사 진입점.
    /// Auto 모드는 홀드 여부를 외부에서 판단할 수 없으므로 Single 발사로 처리.
    /// </summary>
    public void TryFire()
    {
        switch (fireMode)
        {
            case FireMode.Single: TryFireSingle();  break;
            case FireMode.Burst:  TryStartBurst();  break;
            case FireMode.Auto:   TryFireSingle();  break;
        }
    }

    private void Update()
    {
        // Auto 모드: 버튼 홀드 중 반복 발사
        if (fireMode == FireMode.Auto && fireAction.IsPressed() && !_isBurstActive)
        {
            TryFireSingle();
        }
    }

    // ─────────────────────────────────────────────
    // 입력 콜백 (Single / Burst)
    // ─────────────────────────────────────────────
    private void OnFirePerformed(InputAction.CallbackContext _)
    {
        // Auto는 Update에서 처리
        if (fireMode == FireMode.Auto) return;

        switch (fireMode)
        {
            case FireMode.Single:
                TryFireSingle();
                break;
            case FireMode.Burst:
                TryStartBurst();
                break;
        }
    }

    // ─────────────────────────────────────────────
    // 단발 발사
    // ─────────────────────────────────────────────
    private void TryFireSingle()
    {
        if (Time.time < _nextFireTime) return;

        _nextFireTime = Time.time + Mathf.Max(0.01f, cooldown);

        Vector2 origin = GetFireOrigin();
        Vector2 aimDir = GetAimDirection(origin);

        FireHitscan(origin, aimDir, shotIndex: 0);

        // 백스텝: 단발에서도 활성화 가능
        if (enableBackstep && _rb != null)
            StartCoroutine(BackstepRoutine(aimDir));
    }

    // ─────────────────────────────────────────────
    // 버스트 발사 + 백스텝 동시 실행
    // ─────────────────────────────────────────────
    private void TryStartBurst()
    {
        if (Time.time < _nextFireTime || _isBurstActive) return;

        StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        _isBurstActive = true;

        Vector2 origin = GetFireOrigin();
        // ★ 버스트 시작 시 방향 고정 — 첫 발 기준 방향으로 전탄 발사
        Vector2 aimDir = GetAimDirection(origin);

        // 백스텝을 버스트 첫 발과 동시에 시작
        if (enableBackstep && _rb != null)
            StartCoroutine(BackstepRoutine(aimDir));

        for (int i = 0; i < burstCount; i++)
        {
            FireHitscan(origin, aimDir, shotIndex: i);

            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        _nextFireTime = Time.time + Mathf.Max(0.01f, cooldown);
        _isBurstActive = false;
    }

    // ─────────────────────────────────────────────
    // 백스텝 유틸 이동
    //
    // 동작 흐름:
    //   1. 사격 방향 반대로 CircleCast → 안전 이동 거리 계산
    //   2. IsVelocityLocked = true → PlayerController2D 일반 이동 중단
    //   3. SmoothStep 보간으로 rb.MovePosition → 물리 기반 실제 위치 이동
    //   4. 완료 후 IsVelocityLocked = false → 일반 이동 복구
    // ─────────────────────────────────────────────
    private IEnumerator BackstepRoutine(Vector2 aimDir)
    {
        // 총구 반대 방향 = 백스텝 방향
        Vector2 backstepDir = -aimDir;
        Vector2 startPos = _rb.position;

        // ── 벽 충돌 사전 계산 ──────────────────────
        float safeDistance = backstepDistance;
        if (backstepWallLayer.value != 0)
        {
            // CircleCast: 플레이어 크기를 고려한 벽 감지
            RaycastHit2D hit = Physics2D.CircleCast(
                startPos, backstepColliderRadius,
                backstepDir, backstepDistance,
                backstepWallLayer
            );

            if (hit)
            {
                // hit.distance는 원 중심 기준 이동 거리
                // 작은 여유(0.05f)를 두어 벽에 딱 붙지 않게
                safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
                Debug.Log($"[DoomGun] 백스텝 — 벽 감지: {hit.collider.name} / 안전 거리: {safeDistance:F2}", this);
            }
        }

        Vector2 endPos = startPos + backstepDir * safeDistance;

        // ── 이동 제어권 획득 ──────────────────────
        if (_controller != null)
            _controller.IsVelocityLocked = true;

        // [이펙트 연결 지점] 백스텝 시작 시 이펙트
        OnBackstepStart(startPos, backstepDir);

        // ── SmoothStep 보간 이동 ──────────────────
        // SmoothStep: 초반 빠르게 → 끝에서 감속 → 총기 손맛과 어울림
        float elapsed = 0f;
        while (elapsed < backstepDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / backstepDuration));
            _rb.MovePosition(Vector2.Lerp(startPos, endPos, t));
            yield return new WaitForFixedUpdate();
        }

        // 최종 위치 보정 (루프 타이밍 오차 제거)
        _rb.MovePosition(endPos);
        _rb.linearVelocity = Vector2.zero; // 관성 제거

        // ── 이동 제어권 반환 ──────────────────────
        if (_controller != null)
            _controller.IsVelocityLocked = false;

        // [이펙트 연결 지점] 백스텝 종료 이펙트
        OnBackstepEnd(endPos);

        Debug.Log($"[DoomGun] 백스텝 완료 — 이동: {safeDistance:F2}u / 방향: {backstepDir}", this);
    }

    // ─────────────────────────────────────────────
    // 히트스캔 핵심 판정 — 파멸(Doom) 속성
    // ─────────────────────────────────────────────
    private void FireHitscan(Vector2 origin, Vector2 aimDir, int shotIndex)
    {
        // [이펙트 연결 지점] 총구 이펙트
        OnMuzzleFlash(origin, aimDir);
        PlayFireSfx();

        LayerMask combinedMask = targetLayer | wallLayer;
        RaycastHit2D hit = Physics2D.Raycast(origin, aimDir, range, combinedMask);

        // Scene 뷰 디버그 라인 (마젠타 = 파멸 속성)
        Debug.DrawRay(origin, aimDir * range, Color.magenta, 0.15f);

        // [추가] 트레이서: 히트 여부와 관계없이 Game 뷰에서 궤적 표시
        // 적 명중 시 hit.point까지, 빗나감 시 사거리 끝까지 표시
        Vector2 tracerEnd = hit ? hit.point : (origin + aimDir * range);
        ShowTracer(origin, tracerEnd);

        if (!hit)
        {
            Debug.Log($"[DoomGun] [{shotIndex + 1}/{burstCount}] 빗나감", this);
            return;
        }

        // 벽에 막힘
        if (wallLayer.value != 0 && (wallLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
        {
            Debug.Log($"[DoomGun] [{shotIndex + 1}] 벽에 막힘: {hit.collider.gameObject.name}", this);
            return;
        }

        // IDamageable 인터페이스 — PlayerCombat2D(정의 검)와 동일한 인터페이스 공용
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            Debug.Log($"[DoomGun] [{shotIndex + 1}] IDamageable 없음: {hit.collider.gameObject.name}", this);
            return;
        }

        // 발사 시점에 데미지 계산 — 계산은 발사자, 적용은 TakeDamage
        // CharacterStats 있으면 정식 계산, 없으면 flatAttack=20 폴백
        int dmg = CalculateSkillDamage(eSkillAttackDef, fallbackFlatAttack: 20);
        damageable.TakeDamage(dmg, eSkillAttackDef.attribute);

        // E 스킬 적중 시 궁극기 게이지 획득
        AddUltimateGauge(skillHitGain);

        if (_playerCombat != null) _playerCombat.PlayHitSfx(false); // 총 피격음

        // [추가] 총 적중 피드백: 짧은 히트스탑
        // _playerCombat을 통해 히트스탑을 요청하여 근접/원거리 히트스탑이 충돌하지 않도록 단일 창구 유지
        if (_playerCombat != null) _playerCombat.TriggerHitStop(gunHitStopDuration);

        Debug.Log($"[DoomGun] [{eSkillAttackDef.attribute}] [{shotIndex + 1}] 피격: {hit.collider.gameObject.name} / 데미지: {dmg}", this);

        // [이펙트 연결 지점] 파멸 속성 피격 이펙트
        OnDoomHitEffect(hit.point, hit.normal);
    }

    // ─────────────────────────────────────────────
    // 조준 방향 계산 — firePoint 기준
    // ─────────────────────────────────────────────
    private Vector2 GetAimDirection(Vector2 fromPosition)
    {
        _mainCamera ??= Camera.main;

        if (_mainCamera == null || Mouse.current == null)
            return Vector2.right;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - _mainCamera.transform.position.z);
        Vector3 mouseWorld = _mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, depth)
        );

        Vector2 toMouse = (Vector2)mouseWorld - fromPosition;
        return toMouse.sqrMagnitude > 0.0001f ? toMouse.normalized : Vector2.right;
    }

    private Vector2 GetFireOrigin()
    {
        return firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
    }

    // ─────────────────────────────────────────────
    // 발사 사운드
    // ─────────────────────────────────────────────
    private void PlayFireSfx()
    {
        if (audioSource == null || fireSfx == null)
        {
            Debug.LogWarning("[DoomGun] audioSource 또는 fireSfx가 비어있습니다. 인스펙터에서 설정하세요.", this);
            return;
        }
        audioSource.PlayOneShot(fireSfx, fireSfxVolume);
    }

    // ─────────────────────────────────────────────
    // 트레이서 — Game 뷰에서 보이는 탄환 궤적
    // ─────────────────────────────────────────────
    private void ShowTracer(Vector2 start, Vector2 end)
    {
        if (tracerRenderer == null) return;

        tracerRenderer.positionCount = 2;
        tracerRenderer.SetPosition(0, start);
        tracerRenderer.SetPosition(1, end);
        tracerRenderer.enabled = true;

        // 이전 은닉 코루틴 취소 후 재시작 — 버스트 연사 시 충돌 방지
        if (_hideTracerRoutine != null) StopCoroutine(_hideTracerRoutine);
        _hideTracerRoutine = StartCoroutine(HideTracerAfterDelay());
    }

    private IEnumerator HideTracerAfterDelay()
    {
        // WaitForSecondsRealtime: 히트스탑(timeScale=0) 중에도 트레이서가 제때 사라짐
        yield return new WaitForSecondsRealtime(tracerDuration);
        if (tracerRenderer != null) tracerRenderer.enabled = false;
        _hideTracerRoutine = null;
    }

    // ─────────────────────────────────────────────
    // 이펙트 연결 지점
    // VFX 프리팹 할당 시 자동 동작. 미할당 시 스킵.
    // ─────────────────────────────────────────────
#pragma warning disable IDE0060

    /// <summary>[파멸 총] 발사 시 총구 이펙트. muzzleFlashPrefab 할당 시 Instantiate.</summary>
    private void OnMuzzleFlash(Vector2 origin, Vector2 direction)
    {
        if (muzzleFlashPrefab == null) return;

        // 발사 방향으로 프리팹 회전 정렬
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject fx = Instantiate(muzzleFlashPrefab, origin, Quaternion.Euler(0f, 0f, angle));
        Destroy(fx, 0.12f);
    }

    /// <summary>[파멸 총] 적 피격 시 히트 이펙트. hitEffectPrefab 할당 시 Instantiate.</summary>
    private void OnDoomHitEffect(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (hitEffectPrefab == null) return;

        // 피격 법선 방향으로 프리팹 회전 정렬
        float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
        GameObject fx = Instantiate(hitEffectPrefab, hitPoint, Quaternion.Euler(0f, 0f, angle));
        Destroy(fx, 0.3f);
    }

    /// <summary>[백스텝] 이동 시작 시 이펙트. 예: 잔상, 부스터 VFX</summary>
    private void OnBackstepStart(Vector2 startPos, Vector2 direction)
    {
        // TODO: afterImageVFX.Play() / SFX 재생
    }

    /// <summary>[백스텝] 이동 완료 시 이펙트. 예: 착지 먼지</summary>
    private void OnBackstepEnd(Vector2 endPos)
    {
        // TODO: landingDustVFX.Play()
    }

#pragma warning restore IDE0060

    // ─────────────────────────────────────────────
    // 에디터 시각화
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;

        // 발사 기점 마커 (보라색 = 파멸)
        Gizmos.color = new Color(0.6f, 0f, 1f);
        Gizmos.DrawWireSphere(origin, 0.08f);

        // 사거리 표시선
        Gizmos.DrawLine(origin, origin + Vector3.right * range);

        // 백스텝 범위 표시 (노란색)
        if (enableBackstep)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireSphere(origin, backstepDistance);
        }
    }
}
