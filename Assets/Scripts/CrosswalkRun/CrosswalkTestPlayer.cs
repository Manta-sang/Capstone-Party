using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 다른 브랜치의 플레이어를 가져오기 전이나 단독 테스트를 위한 기본 테스트 플레이어 스크립트입니다.
    /// 바닥 틈새나 모서리에 걸리지 않도록 둥근 CapsuleCollider와 마찰 없는 물리 설정을 자동 보정합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CrosswalkTestPlayer : MonoBehaviour, IKillable
    {
        [Header("이동 설정")]
        [SerializeField] private float moveSpeed = 8f;

        [Header("플레이어 식별")]
        [SerializeField] private string playerName = "Player_1";

        private Rigidbody rb;
        private bool isDead = false;
        private Vector3 inputDirection = Vector3.zero;

        public bool IsDead => isDead;

        private void Awake()
        {
            SetupSmoothCollider();
        }

        private void SetupSmoothCollider()
        {
            // 각진 BoxCollider가 있다면 바닥 이음새 걸림 방지를 위해 CapsuleCollider로 교체
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Destroy(box);
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = 0.45f;
                capsule.height = 1.2f;
                capsule.center = Vector3.zero;
            }

            // 바닥 걸림 방지 무마찰 물리 머티리얼 적용
            PhysicMaterial smoothMat = new PhysicMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounciness = 0f
            };
            capsule.material = smoothMat;
        }

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (CrosswalkGameManager.Instance != null)
            {
                CrosswalkGameManager.Instance.RegisterPlayer(gameObject);
            }
        }

        private void Update()
        {
            if (isDead) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            inputDirection = new Vector3(h, 0f, v).normalized;
        }

        private void FixedUpdate()
        {
            if (isDead) return;

            // 물리 FixedUpdate에서 정확하고 부드러운 속도 적용
            Vector3 targetVelocity = inputDirection * moveSpeed;
            targetVelocity.y = rb.velocity.y; // 중력 유지
            rb.velocity = targetVelocity;
        }

        public void Kill(string reason = "")
        {
            if (isDead) return;

            isDead = true;
            Debug.Log($"[{playerName}] 사망 처리됨! 사유: {reason}");

            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = Color.gray;
            }

            rb.freezeRotation = false;
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            if (CrosswalkGameManager.Instance != null)
            {
                CrosswalkGameManager.Instance.PlayerDied(gameObject, reason);
            }
        }
    }
}
