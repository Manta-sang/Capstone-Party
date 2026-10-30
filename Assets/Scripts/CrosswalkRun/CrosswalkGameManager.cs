using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; // [Crosswalk 연동] 기존 네트워크 연결 여부만 확인합니다.

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
        public GameObject WinnerPlayer => winnerPlayer;
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
            // [Crosswalk 연동] 네트워크 참가자는 씬 로딩과 스폰 확인이 끝난 뒤
            // CrosswalkNetworkEntry가 등록하고 StartGame을 호출합니다.
            // 연결하지 않은 기존 단독 실행은 아래 원래 흐름을 그대로 사용합니다.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                gameResultText = "참가자 로딩 및 캐릭터 생성 확인 중...";
                return;
            }
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

        /// <summary>
        /// [Crosswalk 연동] 준비 실패/대기 사유를 기존 HUD에 표시합니다. 게임 판정은 변경하지 않습니다.
        /// </summary>
        public void SetNetworkWaitingStatus(string status)
        {
            if (!isGameActive) gameResultText = status;
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
                // kjs2의 순위별 점수를 유지하며 실제 누적은 서버에서만 수행합니다.
                int rank = alivePlayers.Count + 1;
                AwardRankingScore(player, Mathf.Max(0, 4 - rank));
                Debug.Log($"[CrosswalkGameManager] 플레이어 탈락: {player.name} (사유: {reason}) / 남은 생존자: {alivePlayers.Count}");

                OnPlayerEliminated?.Invoke(player, reason);

                CheckGameOverCondition();
            }
        }

        // 참가자 화면의 결과 적용은 점수를 다시 더하지 않습니다. 누락된 매니저도 종료 동기화를 막지 않습니다.
        private void AwardRankingScore(GameObject player, int points)
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsListening || !network.IsServer || player == null) return;
            var netObject = player.GetComponent<NetworkObject>();
            if (netObject == null) return;
            var ranking = RankingManager.Instance;
            if (ranking == null || !ranking.IsSpawned)
            {
                Debug.LogError("[Crosswalk 점수] RankingManager가 네트워크에 생성되지 않아 점수를 기록하지 못했습니다.");
                return;
            }
            string playerName = player.GetComponent<CrosswalkPlayer>()?.PlayerName ?? $"Player {netObject.OwnerClientId}";
            ranking.AddScore(netObject.OwnerClientId, playerName, points);
        }

        private void CheckGameOverCondition()
        {
            // 온라인 승자와 종료 여부는 호스트만 계산하고 참가자는 확정 결과를 받습니다.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                !NetworkManager.Singleton.IsServer) return;
            // 멀티플레이어 환경 (최소 2인 이상 시작 시)
            if (allPlayers.Count >= 2)
            {
                if (alivePlayers.Count == 1)
                {
                    // 최후의 1인 생존
                    winnerPlayer = alivePlayers[0];
                    // 최후 생존자 점수는 종료 판정을 수행하는 호스트만 한 번 지급합니다.
                    AwardRankingScore(winnerPlayer, 3);
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

        // 참가자는 호스트가 지정한 승자만 적용하며 자체 생존자 수로 종료를 판단하지 않습니다.
        internal void ApplyNetworkRoundEnd(GameObject winner)
        {
            if (!isGameActive) return;
            winnerPlayer = winner;
            EndGame(winner != null ? $"승리! 최후의 생존자: {winner.name}" : "모두 탈락했습니다! (게임 오버)");
        }

        private void OnGUI()
        {
            if (!showDebugHUD) return;

            GUILayout.BeginArea(new Rect(20, 20, 300, 150), GUI.skin.box);
            GUILayout.Label("<b>[ Crosswalk Run 미니게임 ]</b>");
            GUILayout.Label($"등록된 플레이어: {allPlayers.Count}명");
            GUILayout.Label($"생존 플레이어: {alivePlayers.Count}명");
            GUILayout.Label($"상태: {gameResultText}");

            // 온라인에서는 호스트의 네트워크 씬 전환을 사용하므로 로컬 씬 재시작을 막습니다.
            bool online = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (!online && !isGameActive && GUILayout.Button("씬 재시작 (Restart)"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
                );
            }
            GUILayout.EndArea();
        }
    }
}
