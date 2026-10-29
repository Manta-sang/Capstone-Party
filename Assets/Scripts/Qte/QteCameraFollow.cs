using UnityEngine;

public class QteCameraFollow : MonoBehaviour
{
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 4.5f, -7.5f);
    [SerializeField] private float followSharpness = 8f;
    [SerializeField] private float lookAheadDistance = 4f;

    private Transform target;

    public static void Attach(Transform target)
    {
        if (target == null) return;
        Camera camera = Camera.main;
        if (camera == null) camera = FindObjectOfType<Camera>();
        if (camera == null) return;

        QteCameraFollow follow = camera.GetComponent<QteCameraFollow>();
        if (follow == null) follow = camera.gameObject.AddComponent<QteCameraFollow>();
        follow.target = target;
    }

    public static void ClearTarget(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        QteCameraFollow follow = camera.GetComponent<QteCameraFollow>();
        if (follow != null && follow.target == target) follow.target = null;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + followOffset;
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);

        Vector3 lookTarget = target.position + Vector3.up * 1.2f + Vector3.forward * lookAheadDistance;
        Vector3 direction = lookTarget - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), blend);
    }
}