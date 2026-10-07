using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 카메라를 일정한 속도 및 가속도로 전진시키는 오토스크롤 스크립트입니다.
    /// 화면 밖으로 벗어난 플레이어(뷰포트 이탈)를 실시간 감지하여 자동 탈락시키며,
    /// 지면(Y=0)을 관통하는 후방 물리 데스존을 함께 제공하여 완벽한 탈락 판정을 보장합니다.
    /// </summary>
    public class AutoScrollCamera : MonoBehaviour
    {
        [Header("스크롤 이동 설정")]
        [SerializeField] private Vector3 scrollDirection = Vector3.forward;
        [SerializeField] private float initialSpeed = 3.5f;
        [SerializeField] private float speedIncreasePerSecond = 0.08f;
        [SerializeField] private float maxSpeed = 25f;
        [SerializeField] private float startDelay = 2.0f;

        [Header("화면 뷰포트 이탈(탈락) 판정")]
        [Tooltip("카메라 뷰포트 기준으로 화면 밖으로 나간 플레이어를 자동 탈락시킬지 여부")]
        [SerializeField] private bool enableViewportBoundaryCheck = true;

        [Tooltip("화면 아래(뒤처짐) 탈락 마진 (-0.05 = 화면 하단 끝에서 살짝 벗어남)")]
        [SerializeField] private float bottomThreshold = -0.05f;

        [Tooltip("화면 좌우 이탈 마진 (0.05 = 화면 좌우 바깥 5%)")]
        [SerializeField] private float horizontalMargin = 0.05f;

        [Header("후방 물리 데스존 보조 설정")]
        [Tooltip("지면(Y=0)에 배치되는 후방 물리 데스존 오브젝트")]
        [SerializeField] private GameObject deathZoneObject;
        [Tooltip("카메라 Z 위치 대비 지면 데스존의 후방 거리 (미터)")]
        [SerializeField] private float deathZoneBehindDistance = 16f;
        [SerializeField] private Vector3 deathZoneSize = new Vector3(80f, 20f, 12f);

        private Camera cam;
        private float currentSpeed;
        private float elapsedTime = 0f;
        private bool isScrolling = false;

        public float CurrentSpeed => currentSpeed;
        public float InitialSpeed => initialSpeed;
        public float ElapsedTime => elapsedTime;
        public float DifficultyMultiplier => Mathf.Max(1.0f, currentSpeed / Mathf.Max(0.1f, initialSpeed));

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            currentSpeed = initialSpeed;
            SetupGroundDeathZone();
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

        private void LateUpdate()
        {
            // 1. 뷰포트(화면 경계) 기준 실시간 이탈 검사 (100% 확실한 탈락 판정)
            CheckPlayersInViewport();

            // 2. 지면 물리 데스존 위치를 카메라 Z에 맞춰 갱신
            UpdateDeathZonePosition();
        }

        /// <summary>
        /// 카메라 시야(뷰포트)를 벗어난 모든 생존 플레이어를 감지하고 즉시 탈락 처리합니다.
        /// </summary>
        private void CheckPlayersInViewport()
        {
            if (!enableViewportBoundaryCheck) return;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            // 게임 매니저에 등록된 생존자 목록 우선 검사
            if (CrosswalkGameManager.Instance != null)
            {
                var aliveList = CrosswalkGameManager.Instance.AlivePlayers;
                for (int i = aliveList.Count - 1; i >= 0; i--)
                {
                    if (i < aliveList.Count)
                    {
                        CheckAndEliminateIfOutOfBounds(aliveList[i]);
                    }
                }
            }
            else
            {
                // 단독 테스트 시 Tag = "Player" 오브젝트 직접 검사
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                foreach (var p in players)
                {
                    CheckAndEliminateIfOutOfBounds(p);
                }
            }
        }

        private void CheckAndEliminateIfOutOfBounds(GameObject playerObj)
        {
            if (playerObj == null) return;

            IKillable killable = playerObj.GetComponent<IKillable>() ?? playerObj.GetComponentInParent<IKillable>();
            if (killable != null && killable.IsDead) return;

            Vector3 vp = cam.WorldToViewportPoint(playerObj.transform.position);

            // vp.z <= 0 : 카메라 뒤쪽으로 완전히 넘어감
            // vp.y < bottomThreshold : 화면 아래(오토스크롤 뒤처짐)로 이탈
            // vp.x < -horizontalMargin || vp.x > 1 + horizontalMargin : 화면 좌/우 밖으로 이탈
            bool isOutside = (vp.z <= 0f) ||
                             (vp.y < bottomThreshold) ||
                             (vp.x < -horizontalMargin) ||
                             (vp.x > (1f + horizontalMargin));

            if (isOutside)
            {
                string reason = (vp.y < bottomThreshold || vp.z <= 0f) ? "Behind Camera (Fell Behind)" : "Outside Screen Sides";
                if (killable != null)
                {
                    killable.Kill(reason);
                }
                else
                {
                    Debug.Log($"[AutoScrollCamera] 화면 밖 이탈 감지: {playerObj.name}");
                    playerObj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 카메라 회전에 영향을 받지 않도록 지면(Y=0)을 관통하는 수평 물리 데스존을 생성합니다.
        /// </summary>
        private void SetupGroundDeathZone()
        {
            if (deathZoneObject == null)
            {
                deathZoneObject = new GameObject("Ground_DeathZone");
                // 카메라 자식으로 두지 않거나, 회전을 독립시킴 (월드 회전 Identity)
                deathZoneObject.transform.rotation = Quaternion.identity;

                BoxCollider box = deathZoneObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = deathZoneSize;

                deathZoneObject.AddComponent<CameraDeathZone>();
            }

            UpdateDeathZonePosition();
        }

        private void UpdateDeathZonePosition()
        {
            if (deathZoneObject != null)
            {
                // 지면(Y=0) 중심, X=0, Z는 카메라 뒤쪽 거리로 항상 수평 배치
                float targetZ = transform.position.z - deathZoneBehindDistance;
                deathZoneObject.transform.position = new Vector3(0f, 0f, targetZ);
                deathZoneObject.transform.rotation = Quaternion.identity;
            }
        }

        private void OnDestroy()
        {
            if (deathZoneObject != null)
            {
                Destroy(deathZoneObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            float targetZ = transform.position.z - deathZoneBehindDistance;
            Gizmos.DrawCube(new Vector3(0f, 0f, targetZ), deathZoneSize);
        }
    }
}
