using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 카메라를 일정한 속도 및 가속도로 전진시키는 오토스크롤 스크립트입니다.
    /// 카메라 후방에 데스존(DeathZone)을 부착하여 뒤처진 플레이어를 자동으로 탈락시키며,
    /// 난이도 계수(DifficultyMultiplier)를 제공하여 게임이 진행될수록 차량 속도 등과 연동합니다.
    /// </summary>
    public class AutoScrollCamera : MonoBehaviour
    {
        [Header("스크롤 이동 설정")]
        [SerializeField] private Vector3 scrollDirection = Vector3.forward;
        [SerializeField] private float initialSpeed = 3.5f;
        [SerializeField] private float speedIncreasePerSecond = 0.08f;
        [SerializeField] private float maxSpeed = 25f;
        [SerializeField] private float startDelay = 2.0f;

        [Header("데스존 (후방 탈락 영역) 설정")]
        [Tooltip("비워두면 카메라 하단/후방에 자동으로 트리거 콜라이더를 생성합니다.")]
        [SerializeField] private BoxCollider deathZoneCollider;
        [SerializeField] private Vector3 deathZoneLocalOffset = new Vector3(0, -5f, -12f);
        [SerializeField] private Vector3 deathZoneSize = new Vector3(60f, 20f, 10f);

        private float currentSpeed;
        private float elapsedTime = 0f;
        private bool isScrolling = false;

        public float CurrentSpeed => currentSpeed;
        public float InitialSpeed => initialSpeed;
        public float ElapsedTime => elapsedTime;

        /// <summary>
        /// 초기 속도 대비 현재 속도 비율 (1.0에서 시작하여 점차 증가하는 난이도 배수)
        /// </summary>
        public float DifficultyMultiplier => Mathf.Max(1.0f, currentSpeed / Mathf.Max(0.1f, initialSpeed));

        private void Start()
        {
            currentSpeed = initialSpeed;
            SetupDeathZone();
            Invoke(nameof(StartScrolling), startDelay);
        }

        public void StartScrolling()
        {
            isScrolling = true;
        }

        public void StopScrolling()
        {
            isScrolling = false;
        }

        private void Update()
        {
            if (!isScrolling) return;

            elapsedTime += Time.deltaTime;

            // 시간에 따른 점진적 가속 (무한 서바이벌 긴장감)
            if (currentSpeed < maxSpeed)
            {
                currentSpeed += speedIncreasePerSecond * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }

            // 카메라 전진
            transform.Translate(scrollDirection.normalized * (currentSpeed * Time.deltaTime), Space.World);
        }

        private void SetupDeathZone()
        {
            if (deathZoneCollider == null)
            {
                GameObject zoneObj = new GameObject("AutoScroll_DeathZone");
                zoneObj.transform.SetParent(transform, false);
                zoneObj.transform.localPosition = deathZoneLocalOffset;

                deathZoneCollider = zoneObj.AddComponent<BoxCollider>();
                deathZoneCollider.isTrigger = true;
                deathZoneCollider.size = deathZoneSize;

                zoneObj.AddComponent<CameraDeathZone>();
            }
            else
            {
                deathZoneCollider.isTrigger = true;
                if (deathZoneCollider.GetComponent<CameraDeathZone>() == null)
                {
                    deathZoneCollider.gameObject.AddComponent<CameraDeathZone>();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            Vector3 center = transform.TransformPoint(deathZoneLocalOffset);
            Gizmos.DrawCube(center, deathZoneSize);
        }
    }
}
