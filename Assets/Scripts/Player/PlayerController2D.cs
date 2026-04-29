using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed.")]
    [SerializeField] private float walkSpeed = 4.0f;
    [Tooltip("Sprint speed multiplier.")]
    [SerializeField] private float runSpeedMultiplier = 3.0f;

    [Header("Dash")]
    [Tooltip("Dash speed.")]
    [SerializeField] private float dashSpeed = 12.0f;
    [Tooltip("Dash duration in seconds.")]
    [SerializeField] private float dashDuration = 0.15f;
    [Tooltip("Dash cooldown in seconds.")]
    [SerializeField] private float dashCooldown = 0.5f;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private Vector2 _lastNonZeroMoveDirection = Vector2.down;
    private bool _isSprinting;
    private bool _isDashing;
    private Vector2 _dashDirection;
    private float _dashEndTime;
    private float _nextDashTime;

    public bool IsVelocityLocked { get; set; }

    // 0~1 range. PlayerCombat2D uses this to apply temporary movement slowdowns.
    public float AttackSpeedMultiplier { get; set; } = 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        _moveInput = Vector2.zero;
        _isSprinting = false;
        _isDashing = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
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

        if (IsVelocityLocked)
        {
            return;
        }

        float speed = _isSprinting ? walkSpeed * runSpeedMultiplier : walkSpeed;
        _rb.linearVelocity = _moveInput * speed * AttackSpeedMultiplier;
    }

    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);

        if (_moveInput.sqrMagnitude > 0.0001f)
        {
            _lastNonZeroMoveDirection = _moveInput.normalized;
        }
    }

    public void SetRunHeld(bool held)
    {
        _isSprinting = held;
    }

    public void SetDashPressed()
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
