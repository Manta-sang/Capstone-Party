using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; // [Crosswalk 연동] 네트워크 시작 대기 여부를 확인합니다.

namespace CrosswalkRun
{
    /// <summary>
    /// 카메라를 일정한 속도 및 가속도로 전진시키는 오토스크롤 스크립트입니다.
    /// 화면 밖으로 벗어난 플레이어(뷰포트 이탈)를 실시간 감지하여 자동 탈락시키며,
    /// 경계 탈락은 화면 기준으로만 판정하며 물리 데스존은 생성하지 않습니다.
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

        private Camera cam;
        private float currentSpeed;
        private float elapsedTime = 0f;
        private bool isScrolling = false;

        public float CurrentSpeed => currentSpeed;
        public float InitialSpeed => initialSpeed;
        // [Crosswalk 연동] 기존 Inspector 대기 시간을 네트워크 공통 시작 시각 계산에 재사용합니다.
        public float StartDelay => startDelay;
        public float ElapsedTime => elapsedTime;
        public float DifficultyMultiplier => Mathf.Max(1.0f, currentSpeed / Mathf.Max(0.1f, initialSpeed));

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            currentSpeed = initialSpeed;
            // [Crosswalk 연동] 네트워크에서는 참가자 등록 완료 후 게임 매니저가 시작합니다.
            // 기존 단독 실행의 지연 시작 동작은 그대로 유지합니다.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
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
            // [Crosswalk 연동] 로딩 대기 중인 캐릭터를 화면 이탈로 잘못 탈락시키지 않습니다.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !isScrolling)
            {
                return;
            }

            // 물리 데스존 없이 기존 뷰포트(화면 경계) 기준으로 이탈을 검사합니다.
            CheckPlayersInViewport();

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

    }
}
