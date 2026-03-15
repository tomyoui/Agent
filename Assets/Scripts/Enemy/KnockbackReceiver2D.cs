using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class KnockbackReceiver2D : MonoBehaviour
{
    [SerializeField] private Rigidbody2D targetRigidbody;

    private Coroutine _knockbackRoutine;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        if (targetRigidbody == null)
        {
            return;
        }

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        if (safeDirection == Vector2.zero || force <= 0f || duration <= 0f)
        {
            return;
        }

        if (_knockbackRoutine != null)
        {
            StopCoroutine(_knockbackRoutine);
        }

        _knockbackRoutine = StartCoroutine(KnockbackRoutine(safeDirection, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force, float duration)
    {
        targetRigidbody.linearVelocity = Vector2.zero;
        targetRigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(duration);

        _knockbackRoutine = null;
    }
}
