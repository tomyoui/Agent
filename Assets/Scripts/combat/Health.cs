using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHP = 30;
    [SerializeField] private int currentHP = 30;
    
    [Header("Hit Feedback")]
    [SerializeField] private float hitFlashDuration = 0.08f;
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
            Debug.Log($"[Health] {gameObject.name} ignored non-positive damage: {damage}", this);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"[Health] {gameObject.name} took {damage} damage. HP: {currentHP}/{maxHP}", this);

        TriggerHitFlash();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Simple death handling for prototype: remove object.
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
