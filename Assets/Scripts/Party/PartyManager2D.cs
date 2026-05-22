using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal party switch manager.
/// - Only one party member stays active.
/// - Other members are fully disabled via GameObject.SetActive(false).
/// - All player input is collected only here and routed to the active member.
/// </summary>
public class PartyManager2D : MonoBehaviour
{
    public static PartyManager2D Instance { get; private set; }

    [Header("Party Members")]
    [Tooltip("Index 0/1/2 maps to keyboard 1/2/3.")]
    [SerializeField] private GameObject[] partyMembers = new GameObject[3];

    [Header("Switch Settings")]
    [SerializeField, Min(0f)] private float switchCooldown = 0.5f;
    [SerializeField] private int startingMemberIndex;

    [Header("Attack Input")]
    [SerializeField, Min(0f)] private float heavyAttackInputThreshold = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool enableInputDebugLog;
    [SerializeField, Min(0.1f)] private float debugLogInterval = 0.5f;

    private const float GamepadMoveAimThreshold = 0.2f;

    private int _currentIndex = -1;
    private float _nextSwitchTime;
    private bool[] _deadMembers;
    private float _nextDebugLogTime;
    private bool _hasTriggeredGameOver;
    private bool _isAttackPressPending;
    private float _attackPressStartTime;
    private BasePlayableCombat2D _attackPressCombat;
    private bool _attackPressStartedByGamepad;
    private float _nextGamepadAttackPerformedLogTime;
    private bool _loggedPartySceneState;
    private bool _useGamepadAimDirection;

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
            Debug.LogError("[PartyManager2D] partyMembers is empty.", this);
            return;
        }

        _deadMembers = new bool[partyMembers.Length];
        startingMemberIndex = Mathf.Clamp(startingMemberIndex, 0, partyMembers.Length - 1);
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
            Debug.LogError("[PartyManager2D] No valid party member found to activate.", this);
            return;
        }

        DisableUnmanagedCharacterBaseObjects();
        ForceActivateSingleMember(initialIndex);
        LogPartyVisibilityState("초기 활성화 후");
        LogPartyAssignmentState();
    }

    private void Update()
    {
        if (_hasTriggeredGameOver)
        {
            return;
        }

        if (_currentIndex < 0)
        {
            return;
        }

        SyncDeadMemberFlagsFromHealth();
        if (IsMemberDead(_currentIndex))
        {
            OnMemberDied(CurrentMember);
            return;
        }

        HandleSwitchInput();
        RouteActiveMemberInput();
    }

    public void OnMemberDied(GameObject deadMember)
    {
        Debug.Log($"[PartyManager2D] OnMemberDied received: member={(deadMember != null ? deadMember.name : "null")}", this);
        int deadIndex = FindMemberIndex(deadMember);
        if (deadIndex < 0)
        {
            Debug.LogError($"[PartyManager2D] Dead member was not found in partyMembers. incoming={(deadMember != null ? deadMember.name : "null")}", this);
            LogPartyAssignmentState();
            EvaluateGameOverState("dead member not found");
            return;
        }

        MarkMemberDead(deadIndex);
        int aliveCount = LogAliveState("After death mark");

        if (deadIndex != _currentIndex)
        {
            Debug.Log($"[PartyManager2D] Dead member slot {deadIndex + 1} was not active. currentIndex={_currentIndex + 1}", this);
            if (aliveCount == 0)
            {
                TriggerGameOverIfNeeded("non-active member death resulted in zero alive members");
            }
            return;
        }

        int nextIndex = FindFirstAliveMemberIndex(exceptIndex: deadIndex);
        if (nextIndex >= 0)
        {
            Debug.Log($"[PartyManager2D] Switching to next alive member at slot {nextIndex + 1}.", this);
            ForceSwitchTo(nextIndex, ignoreCooldown: true);
            return;
        }

        _currentIndex = -1;
        Debug.Log("[PartyManager2D] No alive members remain. Triggering GameOver.", this);
        TriggerGameOverIfNeeded("active member death with no next member");
    }

    private void HandleSwitchInput()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        if ((keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)) ||
            (gamepad != null && gamepad.dpad.up.wasPressedThisFrame))
        {
            TrySwitchTo(0);
        }
        else if ((keyboard != null && (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)) ||
                 (gamepad != null && gamepad.dpad.right.wasPressedThisFrame))
        {
            TrySwitchTo(1);
        }
        else if ((keyboard != null && (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)) ||
                 (gamepad != null && gamepad.dpad.down.wasPressedThisFrame))
        {
            TrySwitchTo(2);
        }
    }

    private void RouteActiveMemberInput()
    {
        GameObject current = CurrentMember;
        if (current == null || !current.activeInHierarchy)
        {
            MaybeLogInputDebug(Vector2.zero, null, null);
            _isAttackPressPending = false;
            _attackPressCombat = null;
            _attackPressStartedByGamepad = false;
            return;
        }

        PlayerController2D controller = current.GetComponent<PlayerController2D>();
        BasePlayableCombat2D combat = GetCurrentCombat();
        Vector2 moveInput = ReadMoveInput();
        UpdateAimInputMode();
        bool gamepadAttackStarted = WasGamepadAttackPressed();
        bool gamepadAttackPerformed = IsGamepadAttackHeld();
        bool attackHoldPressed = WasAttackHoldPressed();
        bool attackHoldReleased = WasAttackHoldReleased();
        bool basicAttackPressed = WasKeyboardAttackPressed();

        if (attackHoldPressed || attackHoldReleased || basicAttackPressed)
        {
            Debug.Log(
                $"[공격 추적] activeCombat 확인: member={current.name}, combat={(combat != null ? combat.GetType().Name : "null")}, " +
                $"state={(combat != null ? combat.CurrentCombatState.ToString() : "n/a")}, active={(combat != null && combat.isActiveAndEnabled && combat.gameObject.activeInHierarchy)}",
                current);
        }

        if (gamepadAttackStarted)
        {
            Debug.Log($"[패드 공격 입력] X started time={Time.time:0.000}, state={(combat != null ? combat.CurrentCombatState.ToString() : "n/a")}", combat != null ? combat : current);
        }

        if (controller != null)
        {
            controller.SetMoveInput(moveInput);
            controller.SetRunHeld(IsRunHeld());

            if (WasDashPressed() && CanCurrentCombatAccept(BasePlayableCombat2D.CombatInputKind.Dash))
            {
                controller.SetDashPressed();
                combat?.LogCombatStabilitySnapshot("대시 입력 직후");
            }
        }

        if (combat != null && combat.isActiveAndEnabled && combat.gameObject.activeInHierarchy)
        {
            combat.SetUseGamepadAimDirection(_useGamepadAimDirection);

            if (attackHoldPressed && combat.CanAcceptInput(BasePlayableCombat2D.CombatInputKind.HeavyAttack))
            {
                Debug.Log($"[공격 추적] PartyManager 공격 홀드 시작: pending 설정 전 state={combat.CurrentCombatState}", combat);
                _isAttackPressPending = true;
                _attackPressStartTime = Time.time;
                _attackPressCombat = combat;
                _attackPressStartedByGamepad = gamepadAttackStarted;
                _nextGamepadAttackPerformedLogTime = Time.time;
                Debug.Log($"[공격 추적] _isAttackPressPending=true, pendingCombat={_attackPressCombat.GetType().Name}", combat);
                combat.RequestHeavyAttackStart();
            }

            if (_isAttackPressPending &&
                _attackPressStartedByGamepad &&
                gamepadAttackPerformed &&
                Time.time >= _nextGamepadAttackPerformedLogTime)
            {
                float holdTime = Time.time - _attackPressStartTime;
                Debug.Log($"[패드 공격 입력] X performed time={Time.time:0.000}, holdTime={holdTime:0.000}, heavyThreshold={heavyAttackInputThreshold:0.000}", combat);
                _nextGamepadAttackPerformedLogTime = Time.time + 0.25f;
            }

            if (attackHoldReleased)
            {
                Debug.Log($"[공격 추적] PartyManager 공격 홀드 해제: pending={_isAttackPressPending}", combat);
                ResolveAttackHoldRelease(current);
            }

            if (basicAttackPressed)
            {
                Debug.Log($"[공격 추적] PartyManager 공격 입력 감지: RequestAttack 호출 예정, state={combat.CurrentCombatState}", combat);
                combat.RequestAttack();
            }

        }
        else if (attackHoldPressed || attackHoldReleased || basicAttackPressed)
        {
            Debug.LogWarning($"[공격 추적] 공격 입력은 감지됐지만 activeCombat이 유효하지 않습니다. member={current.name}", current);
        }

        if (WasUltimatePressed())
        {
            if (combat == null)
            {
                Debug.LogWarning($"[PartyManager2D] {current.name} has no BasePlayableCombat2D for ultimate input.", current);
            }
            else if (combat.CanUseUltimate())
            {
                combat.RequestUltimate();
            }
            else
            {
                Debug.Log($"[PartyManager2D] Ultimate is not ready for {current.name}: {combat.GetUltimateGauge():0.##}/{combat.GetUltimateMax():0.##}", current);
            }
        }

        if (combat != null && WasSkillPressed())
        {
            combat.RequestSkill();
        }

        MaybeLogInputDebug(moveInput, current, controller);
    }

    private Vector2 ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed))
        {
            x -= 1f;
        }

        if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed))
        {
            x += 1f;
        }

        if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
        {
            y -= 1f;
        }

        if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
        {
            y += 1f;
        }

        Vector2 keyboardInput = new Vector2(x, y);
        Vector2 gamepadInput = ReadGamepadMoveInput();

        return Vector2.ClampMagnitude(keyboardInput + gamepadInput, 1f);
    }

    private Vector2 ReadGamepadMoveInput()
    {
        return Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
    }

    private void UpdateAimInputMode()
    {
        if (HasKeyboardMouseInput())
        {
            _useGamepadAimDirection = false;
        }

        if (HasGamepadInput())
        {
            _useGamepadAimDirection = true;
        }
    }

    private bool HasKeyboardMouseInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool keyboardInput = keyboard != null &&
                             (keyboard.aKey.isPressed ||
                              keyboard.dKey.isPressed ||
                              keyboard.sKey.isPressed ||
                              keyboard.wKey.isPressed ||
                              keyboard.leftArrowKey.isPressed ||
                              keyboard.rightArrowKey.isPressed ||
                              keyboard.downArrowKey.isPressed ||
                              keyboard.upArrowKey.isPressed ||
                              keyboard.leftShiftKey.isPressed ||
                              keyboard.spaceKey.wasPressedThisFrame ||
                              keyboard.enterKey.wasPressedThisFrame ||
                              keyboard.eKey.wasPressedThisFrame ||
                              keyboard.qKey.wasPressedThisFrame ||
                              keyboard.digit1Key.wasPressedThisFrame ||
                              keyboard.digit2Key.wasPressedThisFrame ||
                              keyboard.digit3Key.wasPressedThisFrame ||
                              keyboard.numpad1Key.wasPressedThisFrame ||
                              keyboard.numpad2Key.wasPressedThisFrame ||
                              keyboard.numpad3Key.wasPressedThisFrame);

        bool mouseInput = mouse != null &&
                          (mouse.delta.ReadValue().sqrMagnitude > 0.01f ||
                           mouse.leftButton.wasPressedThisFrame ||
                           mouse.leftButton.wasReleasedThisFrame);

        return keyboardInput || mouseInput;
    }

    private bool HasGamepadInput()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return false;
        }

        return ReadGamepadMoveInput().magnitude >= GamepadMoveAimThreshold ||
               gamepad.buttonWest.wasPressedThisFrame ||
               gamepad.buttonWest.isPressed ||
               gamepad.buttonWest.wasReleasedThisFrame ||
               gamepad.buttonSouth.wasPressedThisFrame ||
               gamepad.buttonNorth.wasPressedThisFrame ||
               gamepad.rightShoulder.isPressed ||
               gamepad.rightTrigger.wasPressedThisFrame ||
               gamepad.dpad.up.wasPressedThisFrame ||
               gamepad.dpad.right.wasPressedThisFrame ||
               gamepad.dpad.down.wasPressedThisFrame;
    }

    private bool IsRunHeld()
    {
        return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
               (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed);
    }

    private bool WasDashPressed()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private bool WasAttackHoldPressed()
    {
        return WasMouseAttackPressed() || WasGamepadAttackPressed();
    }

    private bool WasAttackHoldReleased()
    {
        return WasMouseAttackReleased() || WasGamepadAttackReleased();
    }

    private bool WasMouseAttackPressed()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasMouseAttackReleased()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    private bool WasGamepadAttackPressed()
    {
        return Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
    }

    private bool IsGamepadAttackHeld()
    {
        return Gamepad.current != null && Gamepad.current.buttonWest.isPressed;
    }

    private bool WasGamepadAttackReleased()
    {
        return Gamepad.current != null && Gamepad.current.buttonWest.wasReleasedThisFrame;
    }

    private bool WasKeyboardAttackPressed()
    {
        return Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
    }

    private void ResolveAttackHoldRelease(GameObject current)
    {
        if (current == null || !current.activeInHierarchy)
        {
            Debug.Log("[PartyManager2D] 비활성 캐릭터 대상 공격 입력 해제 무시", this);
            _isAttackPressPending = false;
            _attackPressCombat = null;
            _attackPressStartedByGamepad = false;
            return;
        }

        if (!_isAttackPressPending || _attackPressCombat == null)
        {
            if (_attackPressStartedByGamepad)
            {
                Debug.Log($"[패드 공격 입력] X canceled time={Time.time:0.000}, holdTime=n/a, heavyThreshold={heavyAttackInputThreshold:0.000}, pending=false", this);
            }

            _isAttackPressPending = false;
            _attackPressCombat = null;
            _attackPressStartedByGamepad = false;
            return;
        }

        float heldTime = Time.time - _attackPressStartTime;
        BasePlayableCombat2D pressedCombat = _attackPressCombat;
        bool startedByGamepad = _attackPressStartedByGamepad;
        _isAttackPressPending = false;
        _attackPressCombat = null;
        _attackPressStartedByGamepad = false;

        if (pressedCombat == null || !pressedCombat.isActiveAndEnabled || !pressedCombat.gameObject.activeInHierarchy)
        {
            if (startedByGamepad)
            {
                Debug.Log($"[패드 공격 입력] X canceled time={Time.time:0.000}, holdTime={heldTime:0.000}, heavyThreshold={heavyAttackInputThreshold:0.000}");
                Debug.Log("[패드 공격 입력] 결과: 무시", this);
            }

            Debug.Log("[PartyManager2D] 비활성 전투 컴포넌트의 공격 입력 해제 무시", this);
            return;
        }

        if (startedByGamepad)
        {
            Debug.Log($"[패드 공격 입력] X canceled time={Time.time:0.000}, holdTime={heldTime:0.000}, heavyThreshold={heavyAttackInputThreshold:0.000}", pressedCombat);
        }

        if (heldTime >= heavyAttackInputThreshold)
        {
            if (startedByGamepad)
            {
                Debug.Log("[패드 공격 입력] 결과: 강공", pressedCombat);
            }

            Debug.Log($"[PartyManager2D] Heavy Attack Release: {current.name} held={heldTime:0.00}s", current);
            pressedCombat.RequestHeavyAttackRelease();
            return;
        }

        if (startedByGamepad)
        {
            Debug.Log("[패드 공격 입력] 결과: 평타", pressedCombat);
        }

        Debug.Log($"[PartyManager2D] Short Click Attack: {current.name} held={heldTime:0.00}s", current);
        pressedCombat.RequestAttack();
    }

    private bool WasSkillPressed()
    {
        return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
    }

    private bool WasUltimatePressed()
    {
        return (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
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
            Debug.Log("[PartyManager2D] 현재 전투 상태에서는 캐릭터 교체를 할 수 없습니다.", currentCombat);
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
            Debug.Log($"[PartyManager2D] Cannot switch to dead member: {memberName}", targetMember);
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
        if (_hasTriggeredGameOver)
        {
            return;
        }

        if (!CanSwitchTo(targetIndex))
        {
            return;
        }

        if (!ignoreCooldown)
        {
            _nextSwitchTime = Time.time + switchCooldown;
        }
        else
        {
            _nextSwitchTime = Time.time;
        }

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
        LogPartyVisibilityState("스위칭 후");

        Debug.Log($"[PartyManager2D] Switched to {nextMember.name} (slot {targetIndex + 1}).", this);
        GetCurrentCombat()?.LogCombatStabilitySnapshot("교체 직후");
    }

    private void ResetCombatStateBeforeSwitch(GameObject member)
    {
        _isAttackPressPending = false;
        _attackPressCombat = null;
        _attackPressStartedByGamepad = false;

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

        Debug.Log($"[PartyManager2D] Initial active member: {partyMembers[activeIndex].name} (slot {activeIndex + 1}).", this);
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
        if (!IsValidIndex(targetIndex))
        {
            return false;
        }

        if (targetIndex == _currentIndex)
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
                Debug.LogWarning(
                    $"[PartyManager2D] {member.name} still has PlayerInput. Remove it once the scene is cleaned up.",
                    member);
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
            Debug.Log($"[PartyManager2D] GameOver already triggered. Skip duplicate call. reason={reason}", this);
            return;
        }

        _hasTriggeredGameOver = true;
        Debug.Log($"[PartyManager2D] TriggerGameOverIfNeeded reason={reason}", this);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
        else
        {
            Debug.LogError("[PartyManager2D] GameManager.Instance is null. Cannot trigger GameOver.", this);
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

        Debug.Log(
            $"[PartyManager2D][InputDebug] move={moveInput} activeMember={memberName} activeInHierarchy={memberActive} controller={controllerState}",
            this);
    }
}
