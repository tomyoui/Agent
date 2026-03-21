using UnityEngine;

// HP 비율에 따라 fillTransform 스케일을 조정하는 체력바 UI
public class HealthBarUI : MonoBehaviour
{
    [Tooltip("HP를 읽어올 대상 Health 컴포넌트")]
    [SerializeField] private Health targetHealth;
    [Tooltip("HP 비율에 따라 스케일이 변경될 채우기 Transform")]
    [SerializeField] private Transform fillTransform;

    private Vector3 _initialScale;
    private Vector3 _initialLocalPosition;

    private void Awake()
    {
        if (fillTransform != null)
        {
            _initialScale = fillTransform.localScale;
            _initialLocalPosition = fillTransform.localPosition;
        }
    }

    private void Update()
    {
        if (targetHealth == null || fillTransform == null)
        {
            return;
        }

        float ratio = targetHealth.MaxHP > 0 ? (float)targetHealth.CurrentHP / targetHealth.MaxHP : 0f;

        Vector3 scale = _initialScale;
        scale.x = _initialScale.x * ratio;
        fillTransform.localScale = scale;

        Vector3 pos = _initialLocalPosition;
        pos.x = _initialLocalPosition.x - (_initialScale.x - scale.x) * 0.5f;
        fillTransform.localPosition = pos;
    }
}
