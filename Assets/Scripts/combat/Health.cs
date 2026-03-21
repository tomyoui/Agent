using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [Tooltip("최대 HP")]
    [SerializeField] private int maxHP = 30;
    [Tooltip("현재 HP")]
    [SerializeField] private int currentHP = 30;

    [Header("Hit Feedback")]
    [Tooltip("피격 플래시 지속 시간")]
    [SerializeField] private float hitFlashDuration = 0.08f;
    [Tooltip("피격 플래시 색상")]
    [SerializeField] private Color hitFlashColor = Color.red;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor = Color.white;
    private Coroutine _hitFlashRoutine;

    private void Awake()
    {
        if (maxHP < 1)
        {
            maxHP = 1;
        }

        if (currentHP <= 0 || currentHP > maxHP)
        {
            currentHP = maxHP;
        }

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            Debug.Log($"[Health] {gameObject.name} 유효하지 않은 데미지 무시: {damage}", this);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"[Health] {gameObject.name} {damage} 데미지 수신. HP: {currentHP}/{maxHP}", this);

        TriggerHitFlash();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // 프로토타입용 간단한 사망 처리: 오브젝트 제거
        Destroy(gameObject);
    }

    private void TriggerHitFlash()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _spriteRenderer.color = _originalColor;
        }

        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }

        _hitFlashRoutine = null;
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }
}
