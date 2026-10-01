using UnityEngine;

/// <summary>
/// 벽에 닿으면 반사각으로 튕기는 톱날 (탑다운 3D, XZ 평면 이동)
/// 필요: Rigidbody + Collider(Sphere/Capsule 등)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SawBlade : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private bool randomStartDirection = true;
    [SerializeField] private Vector3 startDirection = new Vector3(1f, 0f, 1f);

    private Rigidbody rb;
    private Vector3 direction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 속도에서 벽 뚫림 방지
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        if (randomStartDirection)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        else
        {
            direction = Flatten(startDirection);
        }
    }

    private void FixedUpdate()
    {
        // 물리 엔진의 감속/누적 오차 없이 항상 일정한 속도 유지
        rb.velocity = direction * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 접촉면 법선으로 반사: 입사각 = 반사각
        Vector3 normal = Flatten(collision.GetContact(0).normal);
        if (normal == Vector3.zero) return;

        direction = Vector3.Reflect(direction, normal).normalized;
        direction = Flatten(direction);
    }

    // Y축 이동 제거 후 정규화
    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }

    public void SetSpeed(float newSpeed) => speed = newSpeed;
}
