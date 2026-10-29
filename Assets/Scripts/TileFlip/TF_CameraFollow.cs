using UnityEngine;

/// <summary>
/// 자기 캐릭터를 따라다니는 카메라 (타일 뒤집기 미니게임 전용).
/// 카메라는 위치만 움직이고, 바라보는 각도는 씬에서 정해둔 회전값을 그대로 유지한다.
///
/// 따라갈 캐릭터는 TF_GameManager가 플레이어를 생성할 때 SetTarget으로 지정해 준다.
/// (오프라인에서는 0번 플레이어, 멀티에서는 각 컴퓨터가 자기 플레이어를 지정한다)
/// 인스펙터의 Target은 비워 두면 된다. 게임 매니저 없이 카메라만 테스트할 때만 직접 넣는다.
///
/// 사용법: 카메라 오브젝트에 붙인다. offset과 카메라의 회전값을 조절해서 시점을 정한다.
/// </summary>
public class TF_CameraFollow : MonoBehaviour
{
    [Tooltip("따라갈 캐릭터. 보통 비워 두고 TF_GameManager가 코드(SetTarget)로 지정한다")]
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
