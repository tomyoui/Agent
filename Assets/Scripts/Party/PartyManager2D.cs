using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PartyManager2D : MonoBehaviour
{
    public static PartyManager2D Instance { get; private set; }

    [Header("Party Members")]
    [Tooltip("Index 0/1/2 maps to keyboard 1/2/3 and D-Pad Up/Right/Down.")]
    [SerializeField] private GameObject[] partyMembers = new GameObject[3];

    [Header("Switch Settings")]
    [SerializeField, Min(0f)] private float switchCooldown = 0.5f;
    [SerializeField] private int startingMemberIndex;

    [Header("Attack Input")]
    [SerializeField, Min(0f)] private float heavyAttackInputThreshold = 0.45f;

    [Header("Debug")]
    [SerializeField] private bool enableInputDebugLog;
    [SerializeField, Min(0.1f)] private float debugLogInterval = 0.5f;

    private int _currentIndex = -1;
    private float _nextSwitchTime;
    private bool[] _deadMembers;
    private float _nextDebugLogTime;
    private bool _hasTriggeredGameOver;
    private bool _loggedPartySceneState;
    private InputRouter2D _inputRouter;

    public event Action PotionRequested;
    public event Action QuickMenuHoldStarted;
    public event Action QuickMenuHoldEnded;
    public event Action InteractionRequested;
    public event Action MapRequested;
    public event Action PauseRequested;
    public event Action<bool> AimModeChanged;

    public int CurrentIndex => _currentIndex;

    public GameObject CurrentMember
    {
        get
        {
            if (!IsValidIndex(_currentIndex))
            {
                return null;
            }

            return partyMembers[_currentIndex];
        }
    }

    public BasePlayableCombat2D GetCurrentCombat()
    {
        GameObject current = CurrentMember;
        if (current == null)
        {
            return null;
        }

        BasePlayableCombat2D[] combatComponents = current.GetComponents<BasePlayableCombat2D>();
        for (int i = 0; i < combatComponents.Length; i++)
        {
            if (combatComponents[i] != null && combatComponents[i].IsPrimaryCombat)
            {
                return combatComponents[i];
            }
        }

        return combatComponents.Length > 0 ? combatComponents[0] : null;
    }

    public CharacterStats GetCurrentStats()
    {
        GameObject current = CurrentMember;
        return current != null ? current.GetComponent<CharacterStats>() : null;
    }

    public CharacterStats[] GetAllCharacterStats()
    {
        if (partyMembers == null)
        {
            return Array.Empty<CharacterStats>();
        }

        CharacterStats[] result = new CharacterStats[partyMembers.Length];
        for (int i = 0; i < partyMembers.Length; i++)
        {
            result[i] = partyMembers[i] != null
                ? partyMembers[i].GetComponent<CharacterStats>()
                : null;
        }

        return result;
    }

    public int GetPartyMemberCount()
    {
        return partyMembers != null ? partyMembers.Length : 0;
    }

    public GameObject GetPartyMember(int index)
    {
        return IsValidIndex(index) ? partyMembers[index] : null;
    }

    public Health GetPartyMemberHealth(int index)
    {
        return GetMemberHealth(index);
    }

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
            Debug.LogError("[PartyManager2D] partyMembers가 비어 있습니다.", this);
            return;
        }

        _deadMembers = new bool[partyMembers.Length];
        startingMemberIndex = Mathf.Clamp(startingMemberIndex, 0, partyMembers.Length - 1);
        SetupInputRouter();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (partyMembers == null || partyMembers.Length == 0)
        {
            return;
        }

        int initialIndex = ResolveInitialIndex();
        if (initialIndex < 0)
        {
            Debug.LogError("[PartyManager2D] 활성화할 수 있는 파티 멤버가 없습니다.", this);
            return;
        }

        DisableUnmanagedCharacterBaseObjects();
        ForceActivateSingleMember(initialIndex);
        LogPartyVisibilityState("초기 활성화");
        LogPartyAssignmentState();
    }

    private void Update()
    {
        if (_hasTriggeredGameOver || _currentIndex < 0)
        {
            return;
        }

        SyncDeadMemberFlagsFromHealth();
        if (IsMemberDead(_currentIndex))
        {
            OnMemberDied(CurrentMember);
            return;
        }

        _inputRouter?.Tick();
        HandleSwitchInput();
        RouteActiveMemberInput();
    }

    public void OnMemberDied(GameObject deadMember)
    {
        Debug.Log($"[PartyManager2D] 사망 처리 수신: member={(deadMember != null ? deadMember.name : "null")}", this);
        int deadIndex = FindMemberIndex(deadMember);
        if (deadIndex < 0)
        {
            Debug.LogError($"[PartyManager2D] 사망한 멤버를 partyMembers에서 찾을 수 없습니다. incoming={(deadMember != null ? deadMember.name : "null")}", this);
            LogPartyAssignmentState();
            EvaluateGameOverState("dead member not found");
            return;
        }

        MarkMemberDead(deadIndex);
        int aliveCount = LogAliveState("After death mark");

        if (deadIndex != _currentIndex)
        {
            Debug.Log($"[PartyManager2D] 비활성 슬롯 {deadIndex + 1} 사망 처리. currentIndex={_currentIndex + 1}", this);
            if (aliveCount == 0)
            {
                TriggerGameOverIfNeeded("non-active member death resulted in zero alive members");
            }
            return;
        }

        int nextIndex = FindFirstAliveMemberIndex(exceptIndex: deadIndex);
        if (nextIndex >= 0)
        {
            Debug.Log($"[PartyManager2D] 다음 생존 멤버로 교체합니다. slot={nextIndex + 1}", this);
            ForceSwitchTo(nextIndex, ignoreCooldown: true);
            return;
        }

        _currentIndex = -1;
        Debug.Log("[PartyManager2D] 생존 파티원이 없습니다. GameOver를 호출합니다.", this);
        TriggerGameOverIfNeeded("active member death with no next member");
    }

    private void SetupInputRouter()
    {
        _inputRouter = new InputRouter2D(heavyAttackInputThreshold);
        _inputRouter.PotionRequested += OnPotionRequested;
        _inputRouter.QuickMenuHoldStarted += OnQuickMenuHoldStarted;
        _inputRouter.QuickMenuHoldEnded += OnQuickMenuHoldEnded;
        _inputRouter.InteractionRequested += OnInteractionRequested;
        _inputRouter.MapRequested += OnMapRequested;
        _inputRouter.PauseRequested += OnPauseRequested;
        _inputRouter.AimModeChanged += OnAimModeChanged;
    }

    private void HandleSwitchInput()
    {
        if (_inputRouter != null && _inputRouter.TryConsumePartySlotSwitch(out int slotIndex))
        {
            TrySwitchTo(slotIndex);
        }
    }

    private void RouteActiveMemberInput()
    {
        GameObject current = CurrentMember;
        if (current == null || !current.activeInHierarchy)
        {
            MaybeLogInputDebug(Vector2.zero, null, null);
            _inputRouter?.ResetBufferedState();
            return;
        }

        PlayerController2D controller = current.GetComponent<PlayerController2D>();
        BasePlayableCombat2D combat = GetCurrentCombat();
        Vector2 moveInput = _inputRouter != null ? _inputRouter.MoveInput : Vector2.zero;

        if (controller != null)
        {
            controller.SetMoveInput(moveInput);
            controller.SetRunHeld(_inputRouter != null && _inputRouter.RunHeld);

            if (_inputRouter != null &&
                _inputRouter.DashPressed &&
                CanCurrentCombatAccept(BasePlayableCombat2D.CombatInputKind.Dash))
            {
                controller.SetDashPressed();
                combat?.LogCombatStabilitySnapshot("대시 입력 직후");
            }
        }

        InputRouter2D.AttackInputResult attackInputResult = _inputRouter != null
            ? _inputRouter.ConsumeAttackInputResult()
            : InputRouter2D.AttackInputResult.None;

        if (combat != null && combat.isActiveAndEnabled && combat.gameObject.activeInHierarchy)
        {
            combat.SetUseGamepadAimDirection(_inputRouter != null && _inputRouter.UseGamepadAimDirection);

            if (_inputRouter != null &&
                _inputRouter.AttackPressedThisFrame &&
                combat.CanAcceptInput(BasePlayableCombat2D.CombatInputKind.HeavyAttack))
            {
                Debug.Log($"[공격 입력] 홀드 시작: member={current.name}, combat={combat.GetType().Name}, state={combat.CurrentCombatState}", combat);
                combat.RequestHeavyAttackStart();
            }

            switch (attackInputResult)
            {
                case InputRouter2D.AttackInputResult.Heavy:
                    Debug.Log($"[공격 입력] 강공격 실행: {current.name}", current);
                    combat.RequestHeavyAttackRelease();
                    break;
                case InputRouter2D.AttackInputResult.Basic:
                    Debug.Log($"[공격 입력] 기본 공격 실행: {current.name}", current);
                    combat.RequestAttack();
                    break;
            }
        }
        else if (_inputRouter != null &&
                 (_inputRouter.AttackPressedThisFrame || attackInputResult != InputRouter2D.AttackInputResult.None))
        {
            Debug.LogWarning($"[공격 입력] 공격 입력은 감지됐지만 activeCombat이 유효하지 않습니다. member={current.name}", current);
        }

        if (_inputRouter != null && _inputRouter.UltimatePressed)
        {
            if (combat == null)
            {
                Debug.LogWarning($"[PartyManager2D] {current.name}에 BasePlayableCombat2D가 없어 궁극기 입력을 처리할 수 없습니다.", current);
            }
            else if (combat.CanUseUltimate())
            {
                combat.RequestUltimate();
            }
            else
            {
                Debug.Log($"[PartyManager2D] 궁극기 게이지가 부족합니다: {current.name} {combat.GetUltimateGauge():0.##}/{combat.GetUltimateMax():0.##}", current);
            }
        }

        if (combat != null && _inputRouter != null && _inputRouter.SkillPressed)
        {
            combat.RequestSkill();
        }

        MaybeLogInputDebug(moveInput, current, controller);
    }

    private void OnPotionRequested()
    {
        PotionRequested?.Invoke();
        // TODO: 실제 포션 시스템이 준비되면 여기서 포션 사용 로직에 연결합니다.
        Debug.Log("[PartyManager2D] 포션 사용 요청 이벤트 호출. 실제 포션 시스템은 아직 연결하지 않았습니다.", this);
    }

    private void OnQuickMenuHoldStarted()
    {
        QuickMenuHoldStarted?.Invoke();
        // TODO: 퀵메뉴 UI가 준비되면 열기 로직을 연결합니다.
        Debug.Log("[PartyManager2D] 퀵메뉴 홀드 시작 요청. 실제 UI는 아직 연결하지 않았습니다.", this);
    }

    private void OnQuickMenuHoldEnded()
    {
        QuickMenuHoldEnded?.Invoke();
        // TODO: 퀵메뉴 UI가 준비되면 닫기 로직을 연결합니다.
        Debug.Log("[PartyManager2D] 퀵메뉴 홀드 종료 요청. 실제 UI는 아직 연결하지 않았습니다.", this);
    }

    private void OnInteractionRequested()
    {
        InteractionRequested?.Invoke();
        // TODO: 상호작용 시스템이 준비되면 현재 대상과 연결합니다.
        Debug.Log("[PartyManager2D] 상호작용 요청 이벤트 호출. 실제 상호작용 시스템은 아직 연결하지 않았습니다.", this);
    }

    private void OnMapRequested()
    {
        MapRequested?.Invoke();
        // TODO: 지도 UI가 준비되면 열기/닫기 로직을 연결합니다.
        Debug.Log("[PartyManager2D] 지도 요청 이벤트 호출. 실제 지도 UI는 아직 연결하지 않았습니다.", this);
    }

    private void OnPauseRequested()
    {
        PauseRequested?.Invoke();
        // TODO: 일시정지 메뉴가 준비되면 열기/닫기 로직을 연결합니다.
        Debug.Log("[PartyManager2D] 일시정지 요청 이벤트 호출. 실제 일시정지 UI는 아직 연결하지 않았습니다.", this);
    }

    private void OnAimModeChanged(bool isHeld)
    {
        AimModeChanged?.Invoke(isHeld);
        // TODO: 조준/기믹 모드가 준비되면 상태 전환 로직을 연결합니다.
        Debug.Log($"[PartyManager2D] 조준/기믹 모드 입력 변경: held={isHeld}", this);
    }

    private void TrySwitchTo(int targetIndex)
    {
        if (_hasTriggeredGameOver)
        {
            return;
        }

        BasePlayableCombat2D currentCombat = GetCurrentCombat();
        if (currentCombat != null && !currentCombat.CanAcceptInput(BasePlayableCombat2D.CombatInputKind.Switch))
        {
            Debug.Log("[PartyManager2D] 현재 전투 상태에서는 파티 교체를 할 수 없습니다.", currentCombat);
            return;
        }

        if (Time.time < _nextSwitchTime)
        {
            return;
        }

        if (IsValidIndex(targetIndex) && IsMemberDead(targetIndex))
        {
            GameObject targetMember = partyMembers[targetIndex];
            string memberName = targetMember != null ? targetMember.name : "null";
            Debug.Log($"[PartyManager2D] 사망한 멤버로 교체할 수 없습니다: {memberName}", targetMember);
            EvaluateGameOverState("attempted switch to dead member");
            return;
        }

        ForceSwitchTo(targetIndex, ignoreCooldown: false);
    }

    private bool CanCurrentCombatAccept(BasePlayableCombat2D.CombatInputKind inputKind)
    {
        BasePlayableCombat2D combat = GetCurrentCombat();
        return combat == null || combat.CanAcceptInput(inputKind);
    }

    private void ForceSwitchTo(int targetIndex, bool ignoreCooldown)
    {
        if (_hasTriggeredGameOver || !CanSwitchTo(targetIndex))
        {
            return;
        }

        _nextSwitchTime = ignoreCooldown ? Time.time : Time.time + switchCooldown;

        GameObject previousMember = CurrentMember;
        GameObject nextMember = partyMembers[targetIndex];

        Vector3 inheritPosition = previousMember != null
            ? previousMember.transform.position
            : nextMember.transform.position;

        SyncAllMemberPositions(inheritPosition);
        StopAllMemberRigidbodies();
        ResetCombatStateBeforeSwitch(previousMember);
        SetSingleMemberVisible(targetIndex);
        _currentIndex = targetIndex;
        LogPartyVisibilityState("교체 직후");

        Debug.Log($"[PartyManager2D] {nextMember.name}(slot {targetIndex + 1})로 교체했습니다.", this);
        GetCurrentCombat()?.LogCombatStabilitySnapshot("교체 직후");
    }

    private void ResetCombatStateBeforeSwitch(GameObject member)
    {
        _inputRouter?.ResetBufferedState();

        if (member == null)
        {
            return;
        }

        BasePlayableCombat2D[] combatComponents = member.GetComponents<BasePlayableCombat2D>();
        for (int i = 0; i < combatComponents.Length; i++)
        {
            if (combatComponents[i] != null)
            {
                combatComponents[i].ResetCombatState();
            }
        }
    }

    private void ForceActivateSingleMember(int activeIndex)
    {
        Vector3 activePosition = partyMembers[activeIndex] != null
            ? partyMembers[activeIndex].transform.position
            : transform.position;

        SyncAllMemberPositions(activePosition);
        StopAllMemberRigidbodies();
        SetSingleMemberVisible(activeIndex);

        _currentIndex = activeIndex;
        _nextSwitchTime = 0f;

        Debug.Log($"[PartyManager2D] 초기 활성 멤버: {partyMembers[activeIndex].name} (slot {activeIndex + 1})", this);
        WarnAboutConflictingComponents();
    }

    private void SyncAllMemberPositions(Vector3 position)
    {
        if (partyMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            member.transform.position = position;

            Rigidbody2D rb = member.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = position;
            }
        }
    }

    private void StopAllMemberRigidbodies()
    {
        if (partyMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            Rigidbody2D rb = member.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                continue;
            }

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void SetSingleMemberVisible(int activeIndex)
    {
        if (partyMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            bool shouldBeActive = i == activeIndex;
            member.SetActive(shouldBeActive);

            SpriteRenderer[] renderers = member.GetComponentsInChildren<SpriteRenderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                if (renderers[j] != null)
                {
                    renderers[j].enabled = shouldBeActive;
                }
            }
        }
    }

    private void DisableUnmanagedCharacterBaseObjects()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int arrivalCount = 0;
        int kasiaCount = 0;
        int agnieszkaCount = 0;
        int unmanagedCharacterBaseCount = 0;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name == "Arrival")
            {
                arrivalCount++;
            }
            else if (candidate.name == "Kasia")
            {
                kasiaCount++;
            }
            else if (candidate.name == "Agnieszka")
            {
                agnieszkaCount++;
            }

            if (candidate.parent != null || candidate.name != "Character_Base")
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (IsManagedPartyObject(candidateObject))
            {
                continue;
            }

            unmanagedCharacterBaseCount++;
            if (candidateObject.activeSelf)
            {
                candidateObject.SetActive(false);
                Debug.Log($"[PartyManager2D] 파티 배열에 없는 Character_Base를 비활성화했습니다: {candidateObject.name}", candidateObject);
            }
        }

        if (!_loggedPartySceneState)
        {
            _loggedPartySceneState = true;
            Debug.Log($"[PartyManager2D] 씬 파티 오브젝트 확인: Arrival={arrivalCount}, Kasia={kasiaCount}, Agnieszka={agnieszkaCount}, 미관리 Character_Base={unmanagedCharacterBaseCount}", this);
        }
    }

    private bool IsManagedPartyObject(GameObject candidate)
    {
        if (candidate == null || partyMembers == null)
        {
            return false;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            if (candidate == member || candidate.transform.IsChildOf(member.transform) || member.transform.IsChildOf(candidate.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void LogPartyVisibilityState(string context)
    {
        if (partyMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                Debug.Log($"[PartyManager2D] {context}: slot={i + 1} member=null", this);
                continue;
            }

            SpriteRenderer[] renderers = member.GetComponentsInChildren<SpriteRenderer>(true);
            int enabledRendererCount = 0;
            for (int j = 0; j < renderers.Length; j++)
            {
                if (renderers[j] != null && renderers[j].enabled)
                {
                    enabledRendererCount++;
                }
            }

            Debug.Log($"[PartyManager2D] {context}: slot={i + 1} member={member.name} activeSelf={member.activeSelf} activeInHierarchy={member.activeInHierarchy} rendererEnabled={enabledRendererCount}/{renderers.Length}", member);
        }
    }

    private int ResolveInitialIndex()
    {
        if (CanBeActive(startingMemberIndex))
        {
            return startingMemberIndex;
        }

        return FindFirstAliveMemberIndex(exceptIndex: -1);
    }

    private int FindFirstAliveMemberIndex(int exceptIndex)
    {
        for (int i = 0; i < partyMembers.Length; i++)
        {
            if (i == exceptIndex)
            {
                continue;
            }

            if (CanBeActive(i))
            {
                return i;
            }
        }

        return -1;
    }

    private int FindMemberIndex(GameObject member)
    {
        if (member == null || partyMembers == null)
        {
            return -1;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject partyMember = partyMembers[i];
            if (partyMember == null)
            {
                continue;
            }

            if (partyMember == member)
            {
                return i;
            }

            if (member.transform.root != null && partyMember == member.transform.root.gameObject)
            {
                return i;
            }

            if (member.transform.IsChildOf(partyMember.transform) || partyMember.transform.IsChildOf(member.transform))
            {
                return i;
            }
        }

        return -1;
    }

    private bool CanSwitchTo(int targetIndex)
    {
        if (!IsValidIndex(targetIndex) || targetIndex == _currentIndex)
        {
            return false;
        }

        return CanBeActive(targetIndex);
    }

    private bool CanBeActive(int index)
    {
        return IsValidIndex(index) &&
               partyMembers[index] != null &&
               !IsMemberDead(index);
    }

    private bool IsValidIndex(int index)
    {
        return partyMembers != null && index >= 0 && index < partyMembers.Length;
    }

    private void SyncDeadMemberFlagsFromHealth()
    {
        if (partyMembers == null || _deadMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length && i < _deadMembers.Length; i++)
        {
            Health health = GetMemberHealth(i);
            if (health != null && health.IsDead)
            {
                _deadMembers[i] = true;
            }
        }
    }

    private void MarkMemberDead(int index)
    {
        if (_deadMembers != null && index >= 0 && index < _deadMembers.Length)
        {
            _deadMembers[index] = true;
        }
    }

    private bool IsMemberDead(int index)
    {
        if (!IsValidIndex(index) || partyMembers[index] == null)
        {
            return true;
        }

        Health health = GetMemberHealth(index);
        bool flagDead = _deadMembers != null && index < _deadMembers.Length && _deadMembers[index];
        bool healthDead = health != null && health.IsDead;
        return flagDead || healthDead;
    }

    private Health GetMemberHealth(int index)
    {
        if (!IsValidIndex(index) || partyMembers[index] == null)
        {
            return null;
        }

        return partyMembers[index].GetComponentInChildren<Health>(true);
    }

    private void WarnAboutConflictingComponents()
    {
        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            PlayerInput playerInput = member.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                Debug.LogWarning($"[PartyManager2D] {member.name}에 PlayerInput이 남아 있습니다. 씬 정리 시 제거를 검토하세요.", member);
            }
        }
    }

    private void LogPartyAssignmentState()
    {
        if (partyMembers == null)
        {
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            Debug.Log(BuildMemberStateLog("[PartyManager2D] Assignment", i, member), this);
        }
    }

    private int LogAliveState(string context)
    {
        if (partyMembers == null)
        {
            return 0;
        }

        int aliveCount = 0;
        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            Health health = member != null ? member.GetComponentInChildren<Health>(true) : null;
            bool flagDead = _deadMembers != null && i < _deadMembers.Length && _deadMembers[i];
            bool healthDead = health != null && health.IsDead;
            bool isDead = flagDead || healthDead;
            if (member != null && !isDead)
            {
                aliveCount++;
            }

            Debug.Log($"{BuildMemberStateLog($"[PartyManager2D][AliveState] {context}", i, member)} flagDead={flagDead} healthDead={healthDead}", this);
        }

        Debug.Log($"[PartyManager2D][AliveState] {context} aliveCount={aliveCount}", this);
        return aliveCount;
    }

    private string BuildMemberStateLog(string prefix, int index, GameObject member)
    {
        Health health = member != null ? member.GetComponentInChildren<Health>(true) : null;
        string memberName = member != null ? member.name : "null";
        string healthName = health != null ? health.gameObject.name : "null";
        string hp = health != null ? $"{health.CurrentHP}/{health.MaxHP}" : "n/a";
        bool isDead = health != null && health.IsDead;
        bool activeSelf = member != null && member.activeSelf;
        bool activeInHierarchy = member != null && member.activeInHierarchy;

        return $"{prefix} slot={index + 1} member={memberName} activeSelf={activeSelf} activeInHierarchy={activeInHierarchy} healthObject={healthName} hp={hp} isDead={isDead}";
    }

    private void EvaluateGameOverState(string context)
    {
        int aliveCount = LogAliveState(context);
        if (aliveCount == 0)
        {
            TriggerGameOverIfNeeded($"{context} -> aliveCount zero");
        }
    }

    private void TriggerGameOverIfNeeded(string reason)
    {
        if (_hasTriggeredGameOver)
        {
            Debug.Log($"[PartyManager2D] GameOver가 이미 호출되어 중복 호출을 건너뜁니다. reason={reason}", this);
            return;
        }

        _hasTriggeredGameOver = true;
        Debug.Log($"[PartyManager2D] GameOver 호출. reason={reason}", this);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
        else
        {
            Debug.LogError("[PartyManager2D] GameManager.Instance가 없어 GameOver를 호출할 수 없습니다.", this);
        }
    }

    private void MaybeLogInputDebug(Vector2 moveInput, GameObject currentMember, PlayerController2D currentController)
    {
        if (!enableInputDebugLog || Time.time < _nextDebugLogTime)
        {
            return;
        }

        _nextDebugLogTime = Time.time + debugLogInterval;

        string memberName = currentMember != null ? currentMember.name : "null";
        bool memberActive = currentMember != null && currentMember.activeInHierarchy;
        string controllerState = currentController != null ? "present" : "null";
        Vector2 aimInput = _inputRouter != null ? _inputRouter.AimInput : Vector2.zero;
        bool useGamepadAim = _inputRouter != null && _inputRouter.UseGamepadAimDirection;

        Debug.Log(
            $"[PartyManager2D][InputDebug] move={moveInput} aim={aimInput} useGamepadAim={useGamepadAim} activeMember={memberName} activeInHierarchy={memberActive} controller={controllerState}",
            this);
    }
}
