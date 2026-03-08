using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
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