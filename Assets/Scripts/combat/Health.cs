using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Health : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHP = 30;
    [SerializeField] private int currentHP;

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
        EnsureCollider();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
        else
        {
            Debug.LogWarning($"[Health] {gameObject.name} has no SpriteRenderer on the same object. Hit flash will be skipped.", this);
        }

        currentHP = maxHP;
        Debug.Log($"[Health] {gameObject.name} spawned. HP: {currentHP}/{maxHP}", this);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            Debug.Log($"[Health] {gameObject.name} ignored non-positive damage: {damage}", this);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"[Health] {gameObject.name} took {damage} damage. Current HP: {currentHP}/{maxHP}", this);

        TriggerHitFlash();

        if (currentHP <= 0)
        {
            Die();
        }
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

        _hitFlashRoutine = StartCoroutine(HitFlashCoroutine());
    }

    private IEnumerator HitFlashCoroutine()
    {
        _spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        _spriteRenderer.color = _originalColor;
        _hitFlashRoutine = null;
    }

    private void Die()
    {
        Debug.Log($"[Health] {gameObject.name} defeated.", this);
        Destroy(gameObject);
    }

    private void EnsureCollider()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
            Debug.LogWarning($"[Health] {gameObject.name} had no Collider2D. BoxCollider2D was added automatically.", this);
        }
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }
}