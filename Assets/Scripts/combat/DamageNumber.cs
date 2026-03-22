using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 피격 시 데미지 숫자를 월드 공간에 표시하는 컴포넌트.
///
/// 사용법:
///   DamageNumber.Spawn(prefab, worldPosition, damage);
///
/// 프리팹 구성:
///   - TextMeshPro (TMP_Text) 컴포넌트 부착 (UGUI 아님 — 월드 스페이스 직접 렌더)
///   - Sorting Layer: "UI" 또는 "Overlay" 권장 (Enemy보다 위여야 보임)
///   - Order in Layer: 10 이상 권장
/// </summary>
[DisallowMultipleComponent]
public class DamageNumber : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────
    [Header("References")]
    [Tooltip("숫자를 표시할 TextMeshPro 컴포넌트. 비워두면 자신에서 자동 탐색.")]
    [SerializeField] private TMP_Text label;

    [Header("Animation")]
    [Tooltip("위로 떠오르는 속도 (유닛/초)")]
    [SerializeField] private float riseSpeed = 1.8f;

    [Tooltip("전체 표시 지속 시간 (초). 이 시간 동안 위로 이동하며 서서히 사라짐.")]
    [SerializeField] private float lifetime = 0.55f;

    [Tooltip("스폰 위치에서 위쪽으로 추가 오프셋 (유닛)")]
    [SerializeField] private float spawnHeightOffset = 0.4f;

    [Tooltip("좌우 랜덤 분산 범위 (유닛). 연속 피격 시 숫자 겹침 방지.")]
    [SerializeField] private float randomXRange = 0.25f;

    // ─────────────────────────────────────────────
    // 정적 진입점 — Health.cs에서 호출
    // ─────────────────────────────────────────────

    /// <summary>
    /// 데미지 숫자를 지정 위치에 스폰한다.
    /// prefab이 null이면 경고 후 스킵 — 다른 피드백(사운드 등)은 정상 동작함.
    /// </summary>
    public static void Spawn(DamageNumber prefab, Vector3 worldPosition, int damage)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[DamageNumber] 프리팹이 연결되지 않아 데미지 숫자를 표시할 수 없습니다. " +
                             "Health 컴포넌트의 'Damage Number Prefab' 슬롯에 프리팹을 연결하세요.");
            return;
        }

        // 스폰 위치: 피격 대상 월드 위치 + 위 오프셋 + 좌우 랜덤 분산
        Vector3 spawnPos = worldPosition;
        spawnPos.y += prefab.spawnHeightOffset;
        spawnPos.x += Random.Range(-prefab.randomXRange, prefab.randomXRange);

        DamageNumber instance = Instantiate(prefab, spawnPos, Quaternion.identity);
        instance.Play(damage);
    }

    // ─────────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();

        if (label == null)
            Debug.LogWarning("[DamageNumber] TMP_Text 컴포넌트를 찾을 수 없습니다. " +
                             "프리팹에 TextMeshPro 컴포넌트를 추가하세요.", this);
    }

    private void Play(int damage)
    {
        if (label != null)
            label.text = damage.ToString();

        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        if (label == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 startPos = transform.position;
        Color startColor = label.color;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            // Time.unscaledDeltaTime: 히트스탑(timeScale=0) 중에도 애니메이션 계속 진행
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            // 위치: 위로 이동 (easeOut — 초반 빠르게, 후반 느리게)
            float riseCurve = 1f - (1f - t) * (1f - t);
            transform.position = startPos + Vector3.up * (riseSpeed * lifetime * riseCurve);

            // 투명도: 후반 50% 구간에서만 페이드 아웃
            float fadeT = Mathf.Clamp01((t - 0.5f) / 0.5f);
            label.color = new Color(startColor.r, startColor.g, startColor.b, 1f - fadeT);

            yield return null;
        }

        Destroy(gameObject);
    }
}
