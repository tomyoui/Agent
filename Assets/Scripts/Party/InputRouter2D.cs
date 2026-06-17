using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class InputRouter2D
{
    public enum AttackInputResult
    {
        None,
        Basic,
        Heavy
    }

    private const float GamepadMoveAimThreshold = 0.2f;
    private const float LbTapMaxDuration = 0.25f;
    private const float LbHoldMinDuration = 0.45f;

    private readonly float _heavyAttackHoldThreshold;
    private bool _useGamepadAimDirection;

    private bool _attackPressPending;
    private float _attackPressStartTime;
    private AttackInputResult _attackInputResult;

    private bool _lbPressPending;
    private bool _lbHoldTriggered;
    private float _lbPressStartTime;

    private bool _quickMenuHeld;
    private bool _aimModeHeld;

    public event Action PotionRequested;
    public event Action QuickMenuHoldStarted;
    public event Action QuickMenuHoldEnded;
    public event Action InteractionRequested;
    public event Action MapRequested;
    public event Action PauseRequested;
    public event Action<bool> AimModeChanged;

    public InputRouter2D(float heavyAttackHoldThreshold)
    {
        _heavyAttackHoldThreshold = Mathf.Max(0f, heavyAttackHoldThreshold);
    }

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public bool UseGamepadAimDirection => _useGamepadAimDirection;
    public bool RunHeld { get; private set; }
    public bool DashPressed { get; private set; }
    public bool SkillPressed { get; private set; }
    public bool UltimatePressed { get; private set; }
    public bool AttackPressedThisFrame { get; private set; }

    public void Tick()
    {
        MoveInput = ReadMoveInput();
        AimInput = ReadAimInput();
        UpdateAimDeviceState();

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad gamepad = Gamepad.current;

        RunHeld = IsPressed(keyboard?.leftShiftKey) || IsPressed(gamepad?.rightShoulder);
        DashPressed = WasPressed(keyboard?.spaceKey) || WasPressed(gamepad?.buttonSouth);
        SkillPressed = WasPressed(keyboard?.eKey) || WasPressed(gamepad?.buttonNorth);
        UltimatePressed = WasPressed(keyboard?.qKey) || WasPressed(gamepad?.rightTrigger);

        if (WasPressed(keyboard?.fKey) || WasPressed(gamepad?.buttonEast))
        {
            InteractionRequested?.Invoke();
        }

        if (WasPressed(keyboard?.zKey))
        {
            PotionRequested?.Invoke();
        }

        if (WasPressed(keyboard?.mKey) || WasPressed(gamepad?.selectButton))
        {
            MapRequested?.Invoke();
        }

        if (WasPressed(keyboard?.escapeKey) || WasPressed(gamepad?.startButton))
        {
            PauseRequested?.Invoke();
        }

        UpdateAttackHold(mouse, gamepad);
        UpdateLbTapHold(gamepad);
        UpdateQuickMenuHold(keyboard);
        UpdateAimModeHold(keyboard, gamepad);
    }

    public bool TryConsumePartySlotSwitch(out int slotIndex)
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        if (WasPressed(keyboard?.digit1Key) || WasPressed(keyboard?.numpad1Key) || WasPressed(gamepad?.dpad.up))
        {
            slotIndex = 0;
            return true;
        }

        if (WasPressed(keyboard?.digit2Key) || WasPressed(keyboard?.numpad2Key) || WasPressed(gamepad?.dpad.right))
        {
            slotIndex = 1;
            return true;
        }

        if (WasPressed(keyboard?.digit3Key) || WasPressed(keyboard?.numpad3Key) || WasPressed(gamepad?.dpad.down))
        {
            slotIndex = 2;
            return true;
        }

        slotIndex = -1;
        return false;
    }

    public AttackInputResult ConsumeAttackInputResult()
    {
        AttackInputResult result = _attackInputResult;
        _attackInputResult = AttackInputResult.None;
        return result;
    }

    public void ResetBufferedState()
    {
        _attackPressPending = false;
        _attackInputResult = AttackInputResult.None;
        _lbPressPending = false;
        _lbHoldTriggered = false;
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 keyboardInput = Vector2.zero;

        if (IsPressed(keyboard?.aKey))
        {
            keyboardInput.x -= 1f;
        }

        if (IsPressed(keyboard?.dKey))
        {
            keyboardInput.x += 1f;
        }

        if (IsPressed(keyboard?.sKey))
        {
            keyboardInput.y -= 1f;
        }

        if (IsPressed(keyboard?.wKey))
        {
            keyboardInput.y += 1f;
        }

        Vector2 gamepadInput = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
        return Vector2.ClampMagnitude(keyboardInput + gamepadInput, 1f);
    }

    private Vector2 ReadAimInput()
    {
        return Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
    }

    private void UpdateAimDeviceState()
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
                              keyboard.leftShiftKey.isPressed ||
                              keyboard.spaceKey.wasPressedThisFrame ||
                              keyboard.eKey.wasPressedThisFrame ||
                              keyboard.qKey.wasPressedThisFrame ||
                              keyboard.fKey.wasPressedThisFrame ||
                              keyboard.zKey.wasPressedThisFrame ||
                              keyboard.rKey.isPressed ||
                              keyboard.tabKey.isPressed ||
                              keyboard.mKey.wasPressedThisFrame ||
                              keyboard.escapeKey.wasPressedThisFrame ||
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

        return MoveInput.magnitude >= GamepadMoveAimThreshold ||
               AimInput.magnitude >= GamepadMoveAimThreshold ||
               gamepad.buttonSouth.wasPressedThisFrame ||
               gamepad.buttonEast.wasPressedThisFrame ||
               gamepad.buttonWest.wasPressedThisFrame ||
               gamepad.buttonWest.isPressed ||
               gamepad.buttonWest.wasReleasedThisFrame ||
               gamepad.buttonNorth.wasPressedThisFrame ||
               gamepad.leftShoulder.isPressed ||
               gamepad.rightShoulder.isPressed ||
               gamepad.leftTrigger.isPressed ||
               gamepad.rightTrigger.wasPressedThisFrame ||
               gamepad.dpad.up.wasPressedThisFrame ||
               gamepad.dpad.right.wasPressedThisFrame ||
               gamepad.dpad.down.wasPressedThisFrame ||
               gamepad.selectButton.wasPressedThisFrame ||
               gamepad.startButton.wasPressedThisFrame;
    }

    private void UpdateAttackHold(Mouse mouse, Gamepad gamepad)
    {
        AttackPressedThisFrame = WasPressed(mouse?.leftButton) || WasPressed(gamepad?.buttonWest);

        if (AttackPressedThisFrame)
        {
            _attackPressPending = true;
            _attackPressStartTime = Time.time;
        }

        if (!WasReleased(mouse?.leftButton) && !WasReleased(gamepad?.buttonWest))
        {
            return;
        }

        if (!_attackPressPending)
        {
            _attackInputResult = AttackInputResult.Basic;
            return;
        }

        float heldTime = Time.time - _attackPressStartTime;
        _attackPressPending = false;
        _attackInputResult = heldTime >= _heavyAttackHoldThreshold
            ? AttackInputResult.Heavy
            : AttackInputResult.Basic;
    }

    private void UpdateLbTapHold(Gamepad gamepad)
    {
        if (gamepad == null)
        {
            _lbPressPending = false;
            _lbHoldTriggered = false;
            return;
        }

        if (gamepad.leftShoulder.wasPressedThisFrame)
        {
            _lbPressPending = true;
            _lbHoldTriggered = false;
            _lbPressStartTime = Time.time;
        }

        if (_lbPressPending &&
            !_lbHoldTriggered &&
            gamepad.leftShoulder.isPressed &&
            Time.time - _lbPressStartTime >= LbHoldMinDuration)
        {
            _lbHoldTriggered = true;
            QuickMenuHoldStarted?.Invoke();
        }

        if (!gamepad.leftShoulder.wasReleasedThisFrame)
        {
            return;
        }

        float heldTime = Time.time - _lbPressStartTime;
        bool shouldLateHold = _lbPressPending && !_lbHoldTriggered && heldTime >= LbHoldMinDuration;
        bool shouldTap = _lbPressPending && !_lbHoldTriggered && heldTime <= LbTapMaxDuration;
        bool shouldEndHold = _lbHoldTriggered || shouldLateHold;

        _lbPressPending = false;
        _lbHoldTriggered = false;

        if (shouldLateHold)
        {
            QuickMenuHoldStarted?.Invoke();
        }

        if (shouldTap)
        {
            PotionRequested?.Invoke();
        }

        if (shouldEndHold)
        {
            QuickMenuHoldEnded?.Invoke();
        }
    }

    private void UpdateQuickMenuHold(Keyboard keyboard)
    {
        bool tabHeld = IsPressed(keyboard?.tabKey);
        if (tabHeld == _quickMenuHeld)
        {
            return;
        }

        _quickMenuHeld = tabHeld;
        if (_quickMenuHeld)
        {
            QuickMenuHoldStarted?.Invoke();
        }
        else
        {
            QuickMenuHoldEnded?.Invoke();
        }
    }

    private void UpdateAimModeHold(Keyboard keyboard, Gamepad gamepad)
    {
        bool aimHeld = IsPressed(keyboard?.rKey) || IsPressed(gamepad?.leftTrigger);
        if (aimHeld == _aimModeHeld)
        {
            return;
        }

        _aimModeHeld = aimHeld;
        AimModeChanged?.Invoke(_aimModeHeld);
    }

    private static bool IsPressed(ButtonControl control)
    {
        return control != null && control.isPressed;
    }

    private static bool WasPressed(ButtonControl control)
    {
        return control != null && control.wasPressedThisFrame;
    }

    private static bool WasReleased(ButtonControl control)
    {
        return control != null && control.wasReleasedThisFrame;
    }
}
