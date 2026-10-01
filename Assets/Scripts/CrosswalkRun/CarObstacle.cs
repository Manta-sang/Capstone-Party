using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 차선을 따라 이동하는 자동차 장애물 스크립트입니다.
    /// 플레이어와 접촉하면 IKillable을 통해 탈락 처리를 진행합니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CarObstacle : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float speed = 10f;
        [SerializeField] private Vector3 moveDirection = Vector3.right;
        [SerializeField] private float lifeTime = 12f;

        [Header("충돌 설정")]
        [Tooltip("충돌 시 플레이어에게 가할 튕겨내는 힘")]
        [SerializeField] private float hitImpactForce = 15f;

        private float timer = 0f;

        public void Initialize(float moveSpeed, Vector3 direction, float customLifeTime = 12f)
        {
            speed = moveSpeed;
            moveDirection = direction.normalized;
            lifeTime = customLifeTime;

            // 이동 방향을 바라보도록 회전 설정
            if (moveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }
        }

        private void Update()
        {
            // 지정된 방향과 속도로 이동
            transform.Translate(moveDirection * (speed * Time.deltaTime), Space.World);

            // 일정 시간 후 자동 삭제
            timer += Time.deltaTime;
            if (timer >= lifeTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.gameObject, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other.gameObject, other.ClosestPoint(transform.position));
        }

        private void HandleHit(GameObject target, Vector3 hitPoint)
        {
            // 1. IKillable 인터페이스를 통한 탈락 처리
            IKillable killable = target.GetComponent<IKillable>() ?? target.GetComponentInParent<IKillable>();
            if (killable != null && !killable.IsDead)
            {
                // 물리적 튕김 효과 (Rigidbody가 있는 경우)
                Rigidbody targetRb = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>();
                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector3 pushDir = (target.transform.position - hitPoint).normalized + Vector3.up * 0.5f;
                    targetRb.AddForce(pushDir * hitImpactForce, ForceMode.Impulse);
                }

                killable.Kill("Car Collision");
                return;
            }

            // 2. 만약 인터페이스가 아직 안 붙은 Player 태그 오브젝트일 경우의 대비책
            if (target.CompareTag("Player"))
            {
                Debug.Log($"[CarObstacle] Player 태그 오브젝트와 충돌: {target.name}");
                Rigidbody targetRb = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>();
                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector3 pushDir = (target.transform.position - hitPoint).normalized + Vector3.up * 0.5f;
                    targetRb.AddForce(pushDir * hitImpactForce, ForceMode.Impulse);
                }
            }
        }
    }
}
