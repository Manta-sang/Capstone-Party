using UnityEngine;

/// <summary>
/// 자기 캐릭터를 따라다니는 카메라 (톱날 피하기 미니게임 전용).
/// 카메라는 위치만 움직이고, 바라보는 각도는 씬에서 정해둔 회전값을 그대로 유지한다.
///
/// 멀티에서는 각 플레이어의 컴퓨터가 카메라를 하나씩 갖고, 자기 플레이어를 target으로 지정하면 된다.
/// (지정 시점은 멀티 전환 단계에서 연결 예정. 그 전에는 인스펙터의 Target에 플레이어를 넣어 테스트)
/// </summary>
public class SD_CameraFollow : MonoBehaviour
{
    [Tooltip("따라갈 캐릭터. 멀티에서는 코드(SetTarget)로 내 플레이어를 지정한다")]
    [SerializeField] private Transform target;

    [Tooltip("캐릭터 기준 카메라의 위치")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);

    [Tooltip("클수록 천천히 따라간다. 0이면 딱 붙어서 따라간다")]
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 velocity;

    /// <summary>따라갈 캐릭터를 바꾼다. snap이 true면 카메라를 바로 그 위치로 옮긴다</summary>
    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        velocity = Vector3.zero;

        if (snap && target != null)
        {
            transform.position = target.position + offset;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime)
            : desiredPosition;
    }
}
