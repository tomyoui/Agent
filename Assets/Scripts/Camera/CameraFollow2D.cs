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

    private Vector3 _velocity;

    private void LateUpdate()
    {
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
    }
}