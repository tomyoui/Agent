using System.Collections;
using UnityEngine;

// 넉백 힘을 받아 Rigidbody2D로 이동시키는 컴포넌트
[DisallowMultipleComponent]
public class KnockbackReceiver2D : MonoBehaviour
{
    [Tooltip("넉백을 적용할 대상 Rigidbody2D (미할당 시 자신에서 자동 탐색)")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    private Coroutine _knockbackRoutine;

    // 피격 경직 종료 시각. Time.unscaledTime 기준 — hitstop(timeScale=0) 중에도 흘러야 함.
    private float _staggerEndTime;

    // EnemyMelee2D.FixedUpdate에서 넉백 중 AI 이동 차단에 사용
    public bool IsKnockedBack => _knockbackRoutine != null;

    // 피격 경직 상태. 넉백과 독립적으로 동작.
    // 넉백이 끝난 후에도 짧게 지속되어 "맞았다" 반응이 읽히게 함.
    public bool IsStaggered => Time.unscaledTime < _staggerEndTime;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }
    }

    /// <summary>
    /// 피격 경직을 적용한다. 넉백과 독립적으로 호출 가능.
    /// 연속 피격 시 이미 더 긴 경직이 남아 있으면 덮어쓰지 않는다 (샌드백 방지).
    /// </summary>
    public void ApplyStagger(float duration)
    {
        if (duration <= 0f) return;
        float newEnd = Time.unscaledTime + duration;
        // 더 긴 경직이 이미 남아 있으면 유지 — 짧은 타격이 긴 경직을 깎지 않음
        _staggerEndTime = Mathf.Max(_staggerEndTime, newEnd);
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

        // [수정] WaitForSeconds → WaitForSecondsRealtime
        // WaitForSeconds는 Time.timeScale에 종속되어 히트스탑(timeScale=0) 동안
        // 넉백 타이머가 멈추는 문제가 있었음.
        // WaitForSecondsRealtime은 timeScale과 무관하게 실제 경과 시간을 기준으로 대기하므로
        // 히트스탑 프레임 동안에도 타이머가 흘러 "멈춤 → 즉시 날아감" 연출이 연결됨.
        yield return new WaitForSecondsRealtime(duration);

        _knockbackRoutine = null;
    }
}
