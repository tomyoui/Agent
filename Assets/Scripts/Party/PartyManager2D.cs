using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3인 파티 전환 매니저 — 활성 캐릭터 추종 슬롯 버전.
///
/// [배치 원칙]
///   - 활성 캐릭터: 플레이어가 직접 조작, 위치 건드리지 않음
///   - 대기 캐릭터 2명: 매 LateUpdate마다 활성 캐릭터 위치 + standbyOffset으로 절대 위치 설정
///   - anchor는 고정값이 아닌 "현재 활성 캐릭터의 transform.position"이 기준
///
/// [전환 원칙]
///   - SwitchTo()는 _currentIndex만 변경
///   - 위치 이동은 LateUpdate가 담당 (전환 직후에도 자동 처리됨)
///
/// [외부 연동 API]
///   PartyManager2D.Instance.CurrentMember      → 현재 활성 캐릭터 GameObject
///   PartyManager2D.Instance.CurrentIndex       → 현재 인덱스 (0~2)
///   PartyManager2D.Instance.GetCurrentCombat() → 전투 컴포넌트 (게이지 UI 연동)
///   PartyManager2D.Instance.GetCurrentStats()  → CharacterStats (HP바 연동)
/// </summary>
public class PartyManager2D : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 싱글턴
    // ─────────────────────────────────────────────────────────────

    public static PartyManager2D Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    // Inspector — 파티 멤버
    // ─────────────────────────────────────────────────────────────

    [Header("파티 멤버 (인덱스 0 = 1번 키, 1 = 2번 키, 2 = 3번 키)")]
    [SerializeField] private GameObject[] partyMembers;

    // ─────────────────────────────────────────────────────────────
    // Inspector — 슬롯 오프셋 (활성 캐릭터 위치 기준 상대 좌표)
    // ─────────────────────────────────────────────────────────────

    [Header("대기 슬롯 오프셋 (활성 캐릭터 위치 기준)")]
    [Tooltip("대기 슬롯 A (뒤 왼쪽). 예: (-0.6, -0.4, 0)")]
    [SerializeField] private Vector3 standbyOffsetA = new Vector3(-0.6f, -0.4f, 0f);

    [Tooltip("대기 슬롯 B (뒤 오른쪽). 예: (0.6, -0.4, 0)")]
    [SerializeField] private Vector3 standbyOffsetB = new Vector3( 0.6f, -0.4f, 0f);

    // ─────────────────────────────────────────────────────────────
    // 내부 캐시 구조체
    // ─────────────────────────────────────────────────────────────

    private struct MemberCache
    {
        public GameObject             go;
        public PlayerController2D     controller;
        public PlayerInput            playerInput;
        public BasePlayableCombat2D[] combatComponents;
        public Rigidbody2D            rb;
        public Collider2D[]           colliders;
        public SpriteRenderer[]       renderers;     // 비주얼 on/off 및 연출용
        public Vector3                originalScale;  // Awake 시점 localScale — 연출 후 복구 기준
        public PlayerRangedAttack2D   rangedAttack;  // E키 스킬 라우팅용 (없으면 null)
    }

    private MemberCache[] _cache;
    private int _currentIndex = 0;

    // ─────────────────────────────────────────────────────────────
    // Inspector — 스위칭 연출
    // ─────────────────────────────────────────────────────────────

    [Header("스위칭 연출")]
    [Tooltip("스위칭 쿨타임 (초). 이 시간 동안 다음 스위칭 입력 무시.")]
    [SerializeField] private float switchCooldown = 0.5f;

    [Tooltip("등장/퇴장 애니메이션 지속 시간 (초). 0.15~0.2 권장.")]
    [SerializeField] private float switchAnimDuration = 0.15f;

    private float _nextSwitchTime; // 다음 스위칭 가능 시각

    // 파티 전환 입력 액션 (1/2/3 키)
    private InputAction _switch1;
    private InputAction _switch2;
    private InputAction _switch3;

    // E키 스킬 액션 — active 캐릭터의 rangedAttack으로 라우팅
    private InputAction _eSkill;

    // ─────────────────────────────────────────────────────────────
    // 외부 접근 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>현재 활성 캐릭터 인덱스 (0~2)</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>현재 활성 캐릭터 GameObject</summary>
    public GameObject CurrentMember => _cache[_currentIndex].go;

    /// <summary>현재 캐릭터의 첫 번째 전투 컴포넌트. 궁극기 게이지 UI 연동용.</summary>
    public BasePlayableCombat2D GetCurrentCombat()
    {
        var comps = _cache[_currentIndex].combatComponents;
        return comps.Length > 0 ? comps[0] : null;
    }

    /// <summary>현재 캐릭터의 CharacterStats. HP바 연동용.</summary>
    public CharacterStats GetCurrentStats()
    {
        return _cache[_currentIndex].go.GetComponent<CharacterStats>();
    }

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (partyMembers == null || partyMembers.Length == 0)
        {
            Debug.LogError("[PartyManager] partyMembers 배열이 비어있습니다.", this);
            return;
        }

        _cache = new MemberCache[partyMembers.Length];
        for (int i = 0; i < partyMembers.Length; i++)
        {
            if (partyMembers[i] == null)
            {
                Debug.LogError($"[PartyManager] partyMembers[{i}]가 null입니다.", this);
                continue;
            }

            _cache[i] = new MemberCache
            {
                go               = partyMembers[i],
                controller       = partyMembers[i].GetComponent<PlayerController2D>(),
                playerInput      = partyMembers[i].GetComponent<PlayerInput>(),
                combatComponents = partyMembers[i].GetComponentsInChildren<BasePlayableCombat2D>(includeInactive: true),
                rb               = partyMembers[i].GetComponent<Rigidbody2D>(),
                colliders        = partyMembers[i].GetComponentsInChildren<Collider2D>(includeInactive: true),
                // includeInactive=true: 자식 오브젝트가 비활성 상태여도 찾음
                renderers        = partyMembers[i].GetComponentsInChildren<SpriteRenderer>(includeInactive: true),
                // Awake 시점의 localScale을 캐싱 — 연출 후 Vector3.one이 아닌 원래 값으로 복구
                originalScale    = partyMembers[i].transform.localScale,
                // E키 스킬 라우팅용. 검총캐처럼 PlayerRangedAttack2D가 없는 캐릭터는 null
                rangedAttack = partyMembers[i].GetComponentInChildren<PlayerRangedAttack2D>(includeInactive: true)
            };

            if (_cache[i].controller == null)
                Debug.LogWarning($"[PartyManager] {partyMembers[i].name}에 PlayerController2D가 없습니다.", this);

            // combat 컴포넌트가 없으면 SetMemberAsStandby/Active에서 enabled 토글이 무시됨
            // → PlayerCombat2D·PlayerRangedAttack2D가 루트 오브젝트에 있는지 확인 필요
            if (_cache[i].combatComponents.Length == 0)
                Debug.LogWarning($"[PartyManager] {partyMembers[i].name}에 BasePlayableCombat2D 파생 컴포넌트가 없습니다. " +
                                 "PlayerCombat2D/PlayerRangedAttack2D가 루트 GameObject에 붙어 있는지 확인하세요.", this);
        }

        _switch1 = new InputAction("PartySwitch1", InputActionType.Button, "<Keyboard>/1");
        _switch2 = new InputAction("PartySwitch2", InputActionType.Button, "<Keyboard>/2");
        _switch3 = new InputAction("PartySwitch3", InputActionType.Button, "<Keyboard>/3");
        _eSkill  = new InputAction("PartyESkill",  InputActionType.Button, "<Keyboard>/e");
    }

    private void Start()
    {
        if (_cache == null) return;

        // 활성/대기 입력 상태 초기 설정
        ApplyControlState();

        // LateUpdate를 기다리지 않고 즉시 standby 위치 배치
        // → 첫 프레임부터 세 캐릭터가 겹치지 않음
        PlaceStandbyMembers();

        // 초기 비주얼: active만 보이고 standby는 숨김
        for (int i = 0; i < _cache.Length; i++)
            SetSpritesVisible(i, i == _currentIndex);

        Debug.Log($"[PartyManager] 초기화 완료. 활성 캐릭터: {_cache[_currentIndex].go.name}", this);

        // 초기화 직후 실제 상태 덤프 — 로그에서 enabled 상태 확인
        DumpState("Start");
    }

    private void OnEnable()
    {
        _switch1.performed += OnSwitch1;
        _switch2.performed += OnSwitch2;
        _switch3.performed += OnSwitch3;
        _switch1.Enable();
        _switch2.Enable();
        _switch3.Enable();

        _eSkill.performed += OnESkillPerformed;
        _eSkill.Enable();
    }

    private void OnDisable()
    {
        _switch1.performed -= OnSwitch1;
        _switch2.performed -= OnSwitch2;
        _switch3.performed -= OnSwitch3;
        _switch1.Disable();
        _switch2.Disable();
        _switch3.Disable();

        _eSkill.performed -= OnESkillPerformed;
        _eSkill.Disable();
    }

    /// <summary>
    /// 매 프레임 대기 캐릭터를 활성 캐릭터 위치 기준으로 재배치.
    /// LateUpdate: 활성 캐릭터의 FixedUpdate(물리 이동) 완료 후 읽어야
    /// 대기 캐릭터가 한 프레임 뒤처지지 않음.
    /// </summary>
    private void LateUpdate()
    {
        if (_cache == null) return;
        PlaceStandbyMembers();
    }

    /// <summary>
    /// 대기 캐릭터를 활성 캐릭터 위치 기준 오프셋으로 즉시 배치.
    /// Start()와 LateUpdate() 양쪽에서 호출 — 첫 프레임 겹침 방지.
    /// </summary>
    private void PlaceStandbyMembers()
    {
        Vector3 activePos = _cache[_currentIndex].go.transform.position;

        Vector3[] offsets = { standbyOffsetA, standbyOffsetB };
        int slot = 0;

        for (int i = 0; i < _cache.Length; i++)
        {
            if (i == _currentIndex) continue;
            if (_cache[i].go == null) continue;
            if (slot >= offsets.Length) break; // standby 슬롯 초과 방어

            Vector3 targetPos = activePos + offsets[slot++];

            // transform.position 직접 설정 → 즉시 시각 반영 (physics sync 대기 없음)
            _cache[i].go.transform.position = targetPos;

            // Kinematic rb도 동기화 — physics 위치와 visual 위치 불일치 방지
            if (_cache[i].rb != null)
                _cache[i].rb.position = targetPos;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 파티 전환
    // ─────────────────────────────────────────────────────────────

    private void OnSwitch1(InputAction.CallbackContext _) => SwitchTo(0);
    private void OnSwitch2(InputAction.CallbackContext _) => SwitchTo(1);
    private void OnSwitch3(InputAction.CallbackContext _) => SwitchTo(2);

    /// <summary>
    /// Health.cs가 사망 처리 시 호출. 다음 살아있는 캐릭터로 자동 전환.
    /// </summary>
    public void OnMemberDied(GameObject deadMember)
    {
        if (_cache == null) return;

        // 죽은 멤버의 인덱스 탐색
        int deadIndex = -1;
        for (int i = 0; i < _cache.Length; i++)
        {
            if (_cache[i].go == deadMember) { deadIndex = i; break; }
        }
        if (deadIndex < 0) return;

        // 다음 살아있는 캐릭터 탐색 (0→1→2 순서)
        int nextIndex = -1;
        for (int i = 0; i < _cache.Length; i++)
        {
            if (i == deadIndex) continue;
            if (_cache[i].go != null && _cache[i].go.activeSelf)
            {
                nextIndex = i;
                break;
            }
        }

        if (nextIndex < 0)
        {
            Debug.Log("[PartyManager] 전원 사망 — 미구현");
            return;
        }

        // 쿨타임 무시하고 즉시 전환
        _nextSwitchTime = 0f;
        SwitchTo(nextIndex);
    }

    /// <summary>
    /// E키 스킬 — active 캐릭터의 PlayerRangedAttack2D.TryFire()로 라우팅.
    ///
    /// 현재 분기:
    ///   index 0 (검총캐): rangedAttack.TryFire() 호출
    ///   index 1, 2     : 아무 일도 하지 않음
    ///
    /// 나중에 다른 캐릭터 E스킬 추가 시 switch 케이스만 늘리면 됨.
    /// </summary>
    private void OnESkillPerformed(InputAction.CallbackContext _)
    {
        if (_cache == null) return;

        var ranged = _cache[_currentIndex].rangedAttack;
        if (ranged == null || !ranged.enabled) return; // 없거나 standby면 무시

        ranged.TryFire();
    }

    /// <summary>
    /// 인덱스만 변경 후 입력/제어 상태를 재적용.
    /// 위치 재배치는 LateUpdate가 자동 처리.
    /// </summary>
    private void SwitchTo(int index)
    {
        if (_cache == null) return;
        if (index == _currentIndex) return;
        if (index < 0 || index >= _cache.Length) return;
        if (_cache[index].go == null) return;

        // 쿨타임 체크 — 0.5초 내 재입력 무시
        if (Time.time < _nextSwitchTime)
        {
            Debug.Log($"[PartyManager] 스위칭 쿨타임 중 ({_nextSwitchTime - Time.time:F2}s 남음)", this);
            return;
        }
        _nextSwitchTime = Time.time + switchCooldown;

        int prevIndex = _currentIndex;
        _currentIndex = index;

        // ── 버그 2 수정: 새 active를 이전 active 위치로 순간이동 ──────────
        // standby 캐릭터는 offset 위치에 배치되어 있음.
        // active가 됐을 때 그 offset 위치를 기준으로 PlaceStandbyMembers가 계산하면
        // 다음 standby들이 offset 누적으로 점점 밀려남.
        // → active 전환 시 새 캐릭터를 이전 active의 위치로 강제 복구.
        if (_cache[prevIndex].go != null && _cache[_currentIndex].go != null)
        {
            Vector3 prevActivePos = _cache[prevIndex].go.transform.position;
            _cache[_currentIndex].go.transform.position = prevActivePos;
            if (_cache[_currentIndex].rb != null)
                _cache[_currentIndex].rb.position = prevActivePos;
        }

        // 입력/물리 상태 즉시 전환 (연출과 독립)
        ApplyControlState();

        // 연출: 이전 캐릭터 아웃 + 새 캐릭터 인
        StartCoroutine(SwitchVisualRoutine(prevIndex, _currentIndex));

        Debug.Log($"[PartyManager] → [{_currentIndex + 1}번] {_cache[_currentIndex].go.name} 전환", this);
        DumpState($"SwitchTo({index})");
    }

    // ─────────────────────────────────────────────────────────────
    // 활성/대기 제어 상태 적용
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 _currentIndex 기준으로 3명 전원의 활성/대기 상태를 갱신.
    /// 위치는 여기서 건드리지 않음 — LateUpdate 전담.
    /// </summary>
    private void ApplyControlState()
    {
        for (int i = 0; i < _cache.Length; i++)
        {
            if (_cache[i].go == null) continue;

            if (i == _currentIndex)
                SetMemberAsActive(i);
            else
                SetMemberAsStandby(i);
        }
    }

    /// <summary>
    /// 캐릭터를 "활성" 상태로 전환.
    ///
    /// - Rigidbody2D.bodyType = Dynamic
    /// - Collider2D on
    /// - 전투 컴포넌트 on
    /// - PlayerController2D on
    /// - PlayerInput: 건드리지 않음 — 공유 InputActionAsset 비활성화 방지
    /// - SpriteRenderer: SwitchVisualRoutine(InRoutine)이 담당
    /// </summary>
    private void SetMemberAsActive(int index)
    {
        var m = _cache[index];

        if (m.rb != null)
        {
            m.rb.bodyType        = RigidbodyType2D.Dynamic;
            m.rb.linearVelocity  = Vector2.zero;
            m.rb.angularVelocity = 0f;
        }

        foreach (var col in m.colliders)
            if (col != null) col.isTrigger = false;

        foreach (var combat in m.combatComponents)
            if (combat != null) combat.enabled = true;

        if (m.controller != null)
        {
            m.controller.IsVelocityLocked = false; // 백스텝 등으로 잠긴 경우 강제 해제
            m.controller.enabled = true;
        }

        // ── 디버그 로그 ──────────────────────────────
        var controllerState = m.controller != null ? m.controller.enabled.ToString() : "null";
        var combatStates    = m.combatComponents.Length > 0
            ? string.Join(", ", System.Array.ConvertAll(m.combatComponents,
                c => c != null ? $"{c.GetType().Name}={c.enabled}" : "null"))
            : "없음";
        Debug.Log($"[PartyManager] [ACTIVE  ← {m.go.name}]  controller={controllerState}  combat=[{combatStates}]", this);
    }

    /// <summary>
    /// 캐릭터를 "대기" 상태로 전환.
    ///
    /// - PlayerController2D off  → OnDisable에서 이동 입력 구독 자동 해제
    /// - 전투 컴포넌트 off       → OnDisable에서 전투 입력 구독 자동 해제
    /// - PlayerInput: 건드리지 않음 — 세 캐릭터가 같은 InputActionAsset을 공유하므로
    ///                playerInput.enabled = false 하면 공유 ActionMap 전체가 꺼져
    ///                활성 캐릭터(0번)의 입력까지 죽는 버그가 생김.
    ///                입력 필터링은 PlayerController2D / 전투 컴포넌트의 OnDisable에서
    ///                구독 해제로 이미 처리됨.
    /// - Rigidbody2D.bodyType = Kinematic → 물리 밀림 방지 (LateUpdate가 위치 제어)
    /// - Collider2D off          → 대기 캐릭터 간 물리 충돌 방지
    /// - SpriteRenderer: SwitchVisualRoutine(OutRoutine)이 담당
    /// </summary>
    private void SetMemberAsStandby(int index)
    {
        Debug.LogWarning($"[PartyManager] SetMemberAsStandby({index}) — currentIndex={_currentIndex}\n{System.Environment.StackTrace}", this);
        var m = _cache[index];

        if (m.controller != null)
        {
            m.controller.IsVelocityLocked = false;
            m.controller.enabled          = false;
        }

        foreach (var combat in m.combatComponents)
            if (combat != null) combat.enabled = false;

        if (m.rb != null)
        {
            m.rb.linearVelocity  = Vector2.zero;
            m.rb.angularVelocity = 0f;
            m.rb.bodyType        = RigidbodyType2D.Kinematic;
        }

        foreach (var col in m.colliders)
            if (col != null) col.isTrigger = true;

        // ── 디버그 로그 ──────────────────────────────
        var controllerState = m.controller != null ? m.controller.enabled.ToString() : "null";
        var combatStates    = m.combatComponents.Length > 0
            ? string.Join(", ", System.Array.ConvertAll(m.combatComponents,
                c => c != null ? $"{c.GetType().Name}={c.enabled}" : "null"))
            : "없음";
        Debug.Log($"[PartyManager] [STANDBY ← {m.go.name}]  controller={controllerState}  combat=[{combatStates}]", this);
    }

    // ─────────────────────────────────────────────────────────────
    // 스위칭 연출
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 스위칭 연출 진입점.
    /// 이전 캐릭터 아웃 + 새 캐릭터 인을 동시에 시작한다.
    /// </summary>
    private IEnumerator SwitchVisualRoutine(int prevIndex, int newIndex)
    {
        // 두 코루틴을 동시에 실행 (yield return 없이 StartCoroutine)
        StartCoroutine(OutRoutine(prevIndex));
        StartCoroutine(InRoutine(newIndex));
        yield break;
    }

    /// <summary>
    /// 퇴장 연출: originalScale → 0.8배 후 스프라이트 숨김.
    /// 연출 완료 후 originalScale로 복구 (Vector3.one 고정 금지).
    /// </summary>
    private IEnumerator OutRoutine(int index)
    {
        if (_cache[index].go == null) yield break;

        Transform t    = _cache[index].go.transform;
        Vector3 origin = _cache[index].originalScale;
        float elapsed  = 0f;

        while (elapsed < switchAnimDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / switchAnimDuration);
            t.localScale = Vector3.Lerp(origin, origin * 0.8f, ratio);
            yield return null;
        }

        SetSpritesVisible(index, false);
        t.localScale = origin; // 다음 등장을 위해 원래 스케일로 복구
    }

    /// <summary>
    /// 등장 연출: 스프라이트 즉시 표시 후 originalScale × 1.2 → originalScale.
    /// </summary>
    private IEnumerator InRoutine(int index)
    {
        if (_cache[index].go == null) yield break;

        SetSpritesVisible(index, true);

        Transform t    = _cache[index].go.transform;
        Vector3 origin = _cache[index].originalScale;
        float elapsed  = 0f;

        while (elapsed < switchAnimDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / switchAnimDuration);
            t.localScale = Vector3.Lerp(origin * 1.2f, origin, ratio);
            yield return null;
        }

        t.localScale = origin; // 정확히 원래 스케일로 마무리
    }

    /// <summary>
    /// 해당 멤버의 모든 SpriteRenderer 표시 여부를 설정.
    /// GetComponentsInChildren으로 수집했으므로 자식 스프라이트도 포함.
    /// </summary>
    private void SetSpritesVisible(int index, bool visible)
    {
        if (_cache[index].go == null) return;
        foreach (var sr in _cache[index].renderers)
            if (sr != null) sr.enabled = visible;
    }

    // ─────────────────────────────────────────────────────────────
    // 상태 덤프 (디버그용)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 전원의 controller·combat enabled 상태를 Console에 출력.
    /// 스위칭 버그 추적용 — 원인 파악 후 제거 가능.
    /// </summary>
    private void DumpState(string context)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[PartyManager] ── 상태 덤프 ({context}, currentIndex={_currentIndex}) ──");
        for (int i = 0; i < _cache.Length; i++)
        {
            var m = _cache[i];
            if (m.go == null) { sb.AppendLine($"  [{i}] go=null"); continue; }

            string role = i == _currentIndex ? "ACTIVE " : "STANDBY";
            string ctrl = m.controller != null ? $"ctrl={m.controller.enabled}" : "ctrl=null";

            string combatStr;
            if (m.combatComponents.Length == 0)
            {
                combatStr = "combat=[] ← GetComponents<BasePlayableCombat2D> 결과 없음!";
            }
            else
            {
                combatStr = "combat=[" + string.Join(", ", System.Array.ConvertAll(
                    m.combatComponents,
                    c => c != null ? $"{c.GetType().Name}={c.enabled}" : "null")) + "]";
            }

            sb.AppendLine($"  [{i}] {role}  {m.go.name}  {ctrl}  {combatStr}");
        }
        Debug.Log(sb.ToString(), this);
    }

    // ─────────────────────────────────────────────────────────────
    // 에디터 시각화
    // ─────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (partyMembers == null || partyMembers.Length == 0) return;

        // 활성 캐릭터 위치를 기준으로 슬롯 미리보기
        Vector3 activePos = Vector3.zero;
        if (Application.isPlaying && _cache != null)
            activePos = _cache[_currentIndex].go.transform.position;
        else if (partyMembers[0] != null)
            activePos = partyMembers[0].transform.position;

        // 활성 슬롯 — 노란색
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);
        Gizmos.DrawWireCube(activePos, Vector3.one * 0.45f);

        // 대기 슬롯 A — 하늘색
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(activePos + standbyOffsetA, Vector3.one * 0.35f);

        // 대기 슬롯 B — 하늘색
        Gizmos.DrawWireCube(activePos + standbyOffsetB, Vector3.one * 0.35f);

        // 슬롯 연결선
        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        Gizmos.DrawLine(activePos, activePos + standbyOffsetA);
        Gizmos.DrawLine(activePos, activePos + standbyOffsetB);
    }
}
