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

        [Header("전방 경적")]
        [Tooltip("차량 밖에서 들리는 경적입니다. 차량 한 대당 한 번 재생합니다.")]
        [SerializeField] private AudioClip hornClip;
        [Tooltip("차량 앞끝에서 플레이어를 감지할 거리입니다.")]
        [SerializeField, Min(0.1f)] private float hornDistance = 6f;
        [Tooltip("진행 방향 중심에서 좌우로 감지할 폭입니다.")]
        [SerializeField, Min(0.1f)] private float hornHalfWidth = 2.5f;
        [SerializeField, Range(0f, 1f)] private float hornVolume = 0.55f;

        // 충돌체를 추가하지 않고 기존 플레이어 충돌체를 주기적으로 조회합니다.
        private readonly Collider[] hornTargets = new Collider[64];
        private Collider vehicleCollider;
        private bool hornPlayed;
        private float nextHornCheck;
        private float timer = 0f;

        private void Awake()
        {
            vehicleCollider = GetComponent<Collider>();
        }

        /// <summary>
        /// 전방에 살아 있는 플레이어가 접근하면 차량 위치에서 경적을 한 번 울립니다.
        /// 각 클라이언트의 기존 차량 위치와 플레이어 복제본을 사용합니다.
        /// </summary>
        private void CheckHorn()
        {
            if (hornPlayed || hornClip == null || Time.time < nextHornCheck) return;
            nextHornCheck = Time.time + 0.1f;

            Vector3 forward = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;
            if (forward == Vector3.zero) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Bounds bounds = vehicleCollider.bounds;
            Vector3 extents = bounds.extents;
            float frontExtent = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.z) * extents.z;
            Vector3 front = bounds.center + forward * frontExtent;
            Vector3 center = front + forward * (hornDistance * 0.5f);
            float radius = Mathf.Sqrt(hornDistance * hornDistance * 0.25f + hornHalfWidth * hornHalfWidth);
            int count = Physics.OverlapSphereNonAlloc(center, radius, hornTargets, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider target = hornTargets[i];
                IKillable player = target.GetComponentInParent<IKillable>();
                if (player == null || player.IsDead) continue;

                Vector3 offset = target.bounds.center - front;
                float ahead = Vector3.Dot(offset, forward);
                if (ahead < 0f || ahead > hornDistance || Mathf.Abs(Vector3.Dot(offset, right)) > hornHalfWidth) continue;

                // 경적 전용 음원을 만들고 멀어질수록 음량이 줄어드는 3D 사운드로 재생합니다.
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 30f;
                source.dopplerLevel = 0f;
                source.PlayOneShot(hornClip, hornVolume);
                hornPlayed = true;
                break;
            }
        }

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
            CheckHorn();

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
