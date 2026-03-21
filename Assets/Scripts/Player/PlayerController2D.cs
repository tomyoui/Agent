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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions.FindAction("Move", true);
        _sprintAction = _playerInput.actions.FindAction("Sprint", true);
        _dashAction = _playerInput.actions.FindAction("Dash", true);
    }

    private void OnEnable()
    {
        _moveAction.performed += OnMovePerformed;
        _moveAction.canceled += OnMoveCanceled;
        _sprintAction.performed += OnSprintPerformed;
        _sprintAction.canceled += OnSprintCanceled;
        _dashAction.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        _moveAction.performed -= OnMovePerformed;
        _moveAction.canceled -= OnMoveCanceled;
        _sprintAction.performed -= OnSprintPerformed;
        _sprintAction.canceled -= OnSprintCanceled;
        _dashAction.performed -= OnDashPerformed;
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
        _rb.linearVelocity = _moveInput * speed;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();

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
