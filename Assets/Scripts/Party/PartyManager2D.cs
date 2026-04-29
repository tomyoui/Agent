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

    [Header("Debug")]
    [SerializeField] private bool enableInputDebugLog;
    [SerializeField, Min(0.1f)] private float debugLogInterval = 0.5f;

    private int _currentIndex = -1;
    private float _nextSwitchTime;
    private bool[] _deadMembers;
    private float _nextDebugLogTime;

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
        return current != null ? current.GetComponent<BasePlayableCombat2D>() : null;
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

        ForceActivateSingleMember(initialIndex);
        LogPartyAssignmentState();
    }

    private void Update()
    {
        if (_currentIndex < 0)
        {
            return;
        }

        HandleSwitchInput();
        RouteActiveMemberInput();
    }

    public void OnMemberDied(GameObject deadMember)
    {
        int deadIndex = FindMemberIndex(deadMember);
        if (deadIndex < 0)
        {
            return;
        }

        _deadMembers[deadIndex] = true;

        if (deadIndex != _currentIndex)
        {
            return;
        }

        int nextIndex = FindFirstAliveMemberIndex(exceptIndex: deadIndex);
        if (nextIndex >= 0)
        {
            ForceSwitchTo(nextIndex, ignoreCooldown: true);
            return;
        }

        _currentIndex = -1;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }

    private void HandleSwitchInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            TrySwitchTo(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            TrySwitchTo(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
        {
            TrySwitchTo(2);
        }
    }

    private void RouteActiveMemberInput()
    {
        GameObject current = CurrentMember;
        if (current == null)
        {
            MaybeLogInputDebug(Vector2.zero, null, null);
            return;
        }

        PlayerController2D controller = current.GetComponent<PlayerController2D>();
        PlayerCombat2D combat = current.GetComponent<PlayerCombat2D>();
        BasePlayableCombat2D ultimateCombat = current.GetComponent<BasePlayableCombat2D>();
        PlayerRangedAttack2D rangedAttack = current.GetComponent<PlayerRangedAttack2D>();
        Vector2 moveInput = ReadMoveInput();

        if (controller != null)
        {
            controller.SetMoveInput(moveInput);
            controller.SetRunHeld(IsRunHeld());

            if (WasDashPressed())
            {
                controller.SetDashPressed();
            }
        }

        if (combat != null)
        {
            if (WasAttackPressed())
            {
                combat.RequestHeavyAttackStart();
                combat.RequestAttack();
            }

            if (WasAttackReleased())
            {
                combat.RequestHeavyAttackRelease();
            }

        }

        if (WasUltimatePressed())
        {
            if (ultimateCombat == null)
            {
                Debug.LogWarning($"[PartyManager2D] {current.name} has no BasePlayableCombat2D for ultimate input.", current);
            }
            else if (ultimateCombat.CanUseUltimate())
            {
                ultimateCombat.TryTriggerUltimate();
            }
            else
            {
                Debug.Log($"[PartyManager2D] Ultimate is not ready for {current.name}: {ultimateCombat.GetUltimateGauge():0.##}/{ultimateCombat.GetUltimateMax():0.##}", current);
            }
        }

        if (rangedAttack != null && WasSkillPressed())
        {
            rangedAttack.TryFire();
        }

        MaybeLogInputDebug(moveInput, current, controller);
    }

    private Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        float x = 0f;
        float y = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            x += 1f;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            y += 1f;
        }

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private bool IsRunHeld()
    {
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }

    private bool WasDashPressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private bool WasAttackPressed()
    {
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool enterPressed = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        return mousePressed || enterPressed;
    }

    private bool WasAttackReleased()
    {
        bool mouseReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        bool enterReleased = Keyboard.current != null && Keyboard.current.enterKey.wasReleasedThisFrame;
        return mouseReleased || enterReleased;
    }

    private bool WasSkillPressed()
    {
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool WasUltimatePressed()
    {
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
    }

    private void TrySwitchTo(int targetIndex)
    {
        if (Time.time < _nextSwitchTime)
        {
            return;
        }

        ForceSwitchTo(targetIndex, ignoreCooldown: false);
    }

    private void ForceSwitchTo(int targetIndex, bool ignoreCooldown)
    {
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

        nextMember.transform.position = inheritPosition;

        if (previousMember != null && previousMember != nextMember)
        {
            previousMember.SetActive(false);
        }

        nextMember.SetActive(true);
        _currentIndex = targetIndex;

        Debug.Log($"[PartyManager2D] Switched to {nextMember.name} (slot {targetIndex + 1}).", this);
    }

    private void ForceActivateSingleMember(int activeIndex)
    {
        for (int i = 0; i < partyMembers.Length; i++)
        {
            GameObject member = partyMembers[i];
            if (member == null)
            {
                continue;
            }

            member.SetActive(i == activeIndex);
        }

        _currentIndex = activeIndex;
        _nextSwitchTime = 0f;

        Debug.Log($"[PartyManager2D] Initial active member: {partyMembers[activeIndex].name} (slot {activeIndex + 1}).", this);
        WarnAboutConflictingComponents();
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
            if (partyMembers[i] == member)
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
               (_deadMembers == null || !_deadMembers[index]);
    }

    private bool IsValidIndex(int index)
    {
        return partyMembers != null && index >= 0 && index < partyMembers.Length;
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
            string memberName = member != null ? member.name : "null";
            bool isActiveInHierarchy = member != null && member.activeInHierarchy;
            Debug.Log($"[PartyManager2D] Slot {i + 1}: {memberName}, activeInHierarchy={isActiveInHierarchy}", this);
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
