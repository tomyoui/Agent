using UnityEngine;

// 카메라가 타겟을 부드럽게 따라가는 2D 팔로우 컴포넌트
public class CameraFollow2D : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("카메라가 추적할 대상 Transform")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Tooltip("카메라 이동 부드러움 시간 (0이면 즉시 이동)")]
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("카메라 Z축 고정값")]
    [SerializeField] private float fixedZ = -10f;

    [Header("Shake")]
    [Tooltip("셰이크 감쇠 속도. 클수록 빨리 멈춤. 권장: 8~15")]
    [SerializeField] private float shakeDecaySpeed = 10f;

    private Vector3 _velocity;
    private float _shakeIntensity;

    // 공격 적중, 피격 등 외부에서 셰이크를 요청할 때 호출.
    // 현재 강도보다 강할 때만 갱신 — 약한 히트가 강한 히트 셰이크를 덮어쓰지 않음.
    public void Shake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    private void LateUpdate()
    {
        if (PartyManager2D.Instance != null)
            target = PartyManager2D.Instance.CurrentMember?.transform;

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = new Vector3(target.position.x, target.position.y, fixedZ);

        if (smoothTime <= 0f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);
        }

        transform.rotation = Quaternion.identity;

        // [추가] 셰이크: SmoothDamp 이후 additive offset으로 적용.
        // SmoothDamp의 목표 위치에는 영향을 주지 않아 follow 로직과 독립적으로 동작.
        // Time.unscaledDeltaTime: timeScale=0(히트스탑) 중에도 감쇠가 계속 진행됨.
        if (_shakeIntensity > 0.001f)
        {
            transform.position += (Vector3)(Random.insideUnitCircle * _shakeIntensity);
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, Time.unscaledDeltaTime * shakeDecaySpeed);
        }
        else
        {
            _shakeIntensity = 0f;
        }
    }
}
