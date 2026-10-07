using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// CrosswalkRun 미니게임의 생존자 추적 및 게임 오버/승리 조건을 판정하는 게임 매니저입니다.
    /// 플레이어가 1명 남았을 때 최후의 생존자 승리 판정을 내립니다.
    /// </summary>
    public class CrosswalkGameManager : MonoBehaviour
    {
        public static CrosswalkGameManager Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private AutoScrollCamera scrollCamera;

        [Header("설정")]
        [Tooltip("최소 요구 플레이어 수 (1인 솔로 테스트 시 1, 멀티플레이 시 2 이상)")]
        [SerializeField] private int minPlayersToStart = 1;
        [SerializeField] private bool showDebugHUD = true;

        // 등록된 전체 플레이어 및 현재 생존 플레이어 목록
        private readonly List<GameObject> allPlayers = new List<GameObject>();
        private readonly List<GameObject> alivePlayers = new List<GameObject>();

        private bool isGameActive = false;
        private GameObject winnerPlayer = null;
        private string gameResultText = "";

        // 이벤트 (외부 UI나 네트워크 동기화에 구독 가능)
        public event Action<GameObject, string> OnPlayerEliminated;
        public event Action<GameObject> OnGameOver;

        public bool IsGameActive => isGameActive;
        public int AlivePlayerCount => alivePlayers.Count;
        public IReadOnlyList<GameObject> AlivePlayers => alivePlayers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (scrollCamera == null)
            {
                scrollCamera = FindObjectOfType<AutoScrollCamera>();
            }
        }

        private void Start()
        {
            // 씬에 이미 배치된 플레이어가 있다면 자동 탐색 (Tag = "Player" 또는 IKillable)
            FindInitialPlayers();
            StartGame();
        }

        private void FindInitialPlayers()
        {
            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in taggedPlayers)
            {
                RegisterPlayer(p);
            }
        }

        /// <summary>
        /// 플레이어를 게임에 등록합니다. (동적 스폰 시 호출 가능)
        /// </summary>
        public void RegisterPlayer(GameObject player)
        {
            if (player == null || allPlayers.Contains(player)) return;

            allPlayers.Add(player);
            if (!alivePlayers.Contains(player))
            {
                alivePlayers.Add(player);
            }
        }

        public void StartGame()
        {
            isGameActive = true;
            winnerPlayer = null;
            gameResultText = "게임 진행 중!";

            if (scrollCamera != null)
            {
                scrollCamera.StartScrolling();
            }
        }

        /// <summary>
        /// 플레이어가 사망했을 때 호출됩니다.
        /// </summary>
        public void PlayerDied(GameObject player, string reason)
        {
            if (!isGameActive) return;

            if (alivePlayers.Contains(player))
            {
                alivePlayers.Remove(player);
                Debug.Log($"[CrosswalkGameManager] 플레이어 탈락: {player.name} (사유: {reason}) / 남은 생존자: {alivePlayers.Count}");

                OnPlayerEliminated?.Invoke(player, reason);

                CheckGameOverCondition();
            }
        }

        private void CheckGameOverCondition()
        {
            // 멀티플레이어 환경 (최소 2인 이상 시작 시)
            if (allPlayers.Count >= 2)
            {
                if (alivePlayers.Count == 1)
                {
                    // 최후의 1인 생존
                    winnerPlayer = alivePlayers[0];
                    EndGame($"승리! 최후의 생존자: {winnerPlayer.name}");
                }
                else if (alivePlayers.Count == 0)
                {
                    // 동시 탈락 무승부
                    EndGame("모두 탈락했습니다! (무승부)");
                }
            }
            // 1인 솔로 테스트 모드
            else if (allPlayers.Count == 1)
            {
                if (alivePlayers.Count == 0)
                {
                    EndGame("탈락했습니다! (게임 오버)");
                }
            }
        }

        private void EndGame(string result)
        {
            isGameActive = false;
            gameResultText = result;
            Debug.Log($"[CrosswalkGameManager] {result}");

            if (scrollCamera != null)
            {
                scrollCamera.StopScrolling();
            }

            OnGameOver?.Invoke(winnerPlayer);
        }

        private void OnGUI()
        {
            if (!showDebugHUD) return;

            GUILayout.BeginArea(new Rect(20, 20, 300, 150), GUI.skin.box);
            GUILayout.Label("<b>[ Crosswalk Run 미니게임 ]</b>");
            GUILayout.Label($"등록된 플레이어: {allPlayers.Count}명");
            GUILayout.Label($"생존 플레이어: {alivePlayers.Count}명");
            GUILayout.Label($"상태: {gameResultText}");

            if (!isGameActive && GUILayout.Button("씬 재시작 (Restart)"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
                );
            }
            GUILayout.EndArea();
        }
    }
}
