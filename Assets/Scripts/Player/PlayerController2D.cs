using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float runSpeedMultiplier = 3.0f;

    private Rigidbody2D _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private Vector2 _moveInput;
    private bool _isSprinting;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions.FindAction("Move", true);
        _sprintAction = _playerInput.actions.FindAction("Sprint", true);
    }

    private void OnEnable()
    {
        _moveAction.performed += OnMovePerformed;
        _moveAction.canceled += OnMoveCanceled;
        _sprintAction.performed += OnSprintPerformed;
        _sprintAction.canceled += OnSprintCanceled;
    }

    private void OnDisable()
    {
        _moveAction.performed -= OnMovePerformed;
        _moveAction.canceled -= OnMoveCanceled;
        _sprintAction.performed -= OnSprintPerformed;
        _sprintAction.canceled -= OnSprintCanceled;
    }

    private void FixedUpdate()
    {
        float speed = _isSprinting ? walkSpeed * runSpeedMultiplier : walkSpeed;
        _rb.linearVelocity = _moveInput * speed;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
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
}