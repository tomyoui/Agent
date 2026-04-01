 using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("기본 이동 속도")]
    [SerializeField] private float walkSpeed = 4.0f;
    [Tooltip("달리기 시 walkSpeed에 곱해지는 배율")]
    [SerializeField] private float runSpeedMultiplier = 3.0f;

    [Header("Dash")]
    [Tooltip("대시 이동 속도")]
    [SerializeField] private float dashSpeed = 12.0f;
    [Tooltip("대시 지속 시간 (초)")]
    [SerializeField] private float dashDuration = 0.15f;
    [Tooltip("대시 쿨다운 (초)")]
    [SerializeField] private float dashCooldown = 0.5f;

    private Rigidbody2D _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private InputAction _dashAction;
    private Vector2 _moveInput;
    private Vector2 _lastNonZeroMoveDirection = Vector2.down;
    private bool _isSprinting;
    private bool _isDashing;
    private Vector2 _dashDirection;
    private float _dashEndTime;
    private float _nextDashTime;

    // true인 동안 FixedUpdate의 일반 이동이 중단됨 (백스텝 등 외부 제어용)
    public bool IsVelocityLocked { get; set; }

    // 0~1 범위. 공격 중 감속 배율. 1 = 평속, 0 = 완전 정지.
    // PlayerCombat2D가 공격 시작 시 설정하고, 지속 시간 후 1로 복구.
    public float AttackSpeedMultiplier { get; set; } = 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        // throwIfNotFound=false 로 바꾸고 null 검사 — 액션 이름 오타 시 즉시 발견
        _moveAction   = _playerInput.actions.FindAction("Move",   throwIfNotFound: false);
        _sprintAction = _playerInput.actions.FindAction("Sprint", throwIfNotFound: false);
        _dashAction   = _playerInput.actions.FindAction("Dash",   throwIfNotFound: false);

        if (_moveAction   == null) Debug.LogError("[PlayerController2D] InputAction 'Move' 를 찾지 못했습니다. .inputactions 파일에 'Move' 가 있는지 확인하세요.", this);
        if (_sprintAction == null) Debug.LogError("[PlayerController2D] InputAction 'Sprint' 를 찾지 못했습니다.", this);
        if (_dashAction   == null) Debug.LogError("[PlayerController2D] InputAction 'Dash' 를 찾지 못했습니다.", this);
    }

    private void OnEnable()
    {
        Debug.Log($"[PlayerController2D] OnEnable ← {gameObject.name}", this);

        // PlayerInput은 키보드를 얻지 못한 캐릭터의 ActionMap을 DeactivateInput()으로 끔.
        // 명시적으로 개별 Action을 Enable해서 PlayerInput의 pairing 상태를 우회한다.
        _moveAction?.Enable();
        _sprintAction?.Enable();
        _dashAction?.Enable();

        if (_moveAction != null)   { _moveAction.performed   += OnMovePerformed;   _moveAction.canceled   += OnMoveCanceled; }
        if (_sprintAction != null) { _sprintAction.performed += OnSprintPerformed;  _sprintAction.canceled += OnSprintCanceled; }
        if (_dashAction != null)     _dashAction.performed   += OnDashPerformed;

        Debug.Log($"[PlayerController2D] OnEnable 완료 ← {gameObject.name}  moveAction.enabled={_moveAction?.enabled}  bindings={_moveAction?.bindings.Count}", this);
    }

    private void OnDisable()
    {
        Debug.Log($"[PlayerController2D] OnDisable ← {gameObject.name}", this);

        if (_moveAction != null)   { _moveAction.performed   -= OnMovePerformed;   _moveAction.canceled   -= OnMoveCanceled; }
        if (_sprintAction != null) { _sprintAction.performed -= OnSprintPerformed;  _sprintAction.canceled -= OnSprintCanceled; }
        if (_dashAction != null)     _dashAction.performed   -= OnDashPerformed;

        // standby 캐릭터의 action은 꺼서 입력 차단
        _moveAction?.Disable();
        _sprintAction?.Disable();
        _dashAction?.Disable();
    }

    private void FixedUpdate()
    {
        // 대시 중
        if (_isDashing)
        {
            if (Time.time >= _dashEndTime)
            {
                _isDashing = false;
            }
            else
            {
                _rb.linearVelocity = _dashDirection * dashSpeed;
                return;
            }
        }

        // 백스텝 등 외부 velocity 제어 중 — 일반 이동 중단
        if (IsVelocityLocked) return;

        float speed = _isSprinting ? walkSpeed * runSpeedMultiplier : walkSpeed;
        _rb.linearVelocity = _moveInput * speed * AttackSpeedMultiplier;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
        Debug.Log($"[PlayerController2D] OnMovePerformed ← {gameObject.name}  input={_moveInput}", this);

        if (_moveInput.sqrMagnitude > 0.0001f)
        {
            _lastNonZeroMoveDirection = _moveInput.normalized;
        }
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        _moveInput = Vector2.zero;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        _isSprinting = context.ReadValueAsButton();
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        _isSprinting = false;
    }

    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (Time.time < _nextDashTime)
        {
            return;
        }

        Vector2 dashDir = _moveInput.sqrMagnitude > 0.0001f
            ? _moveInput.normalized
            : _lastNonZeroMoveDirection;

        if (dashDir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _dashDirection = dashDir;
        _isDashing = true;
        _dashEndTime = Time.time + Mathf.Max(0.01f, dashDuration);
        _nextDashTime = Time.time + Mathf.Max(0.01f, dashCooldown);
    }
}
