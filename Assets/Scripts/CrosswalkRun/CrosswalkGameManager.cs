using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; // ★ 에러 해결: 네트워크 패키지 추가!

namespace CrosswalkRun
{
    public class CrosswalkGameManager : MonoBehaviour
    {
        public static CrosswalkGameManager Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private AutoScrollCamera scrollCamera;

        [Header("설정")]
        [Tooltip("최소 요구 플레이어 수 (1인 솔로 테스트 시 1, 멀티플레이 시 2 이상)")]
        [SerializeField] private int minPlayersToStart = 1;
        [SerializeField] private bool showDebugHUD = true;

        private readonly List<GameObject> allPlayers = new List<GameObject>();
        private readonly List<GameObject> alivePlayers = new List<GameObject>();

        private bool isGameActive = false;
        private GameObject winnerPlayer = null;
        private string gameResultText = "";

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

        // ★ 탈락 시 점수 산정 로직
        public void PlayerDied(GameObject player, string reason)
        {
            if (!isGameActive) return;

            if (alivePlayers.Contains(player))
            {
                alivePlayers.Remove(player);

                int rank = alivePlayers.Count + 1;
                int points = Mathf.Max(0, 4 - rank); // 1등=3점, 2등=2점, 3등=1점, 4등=0점

                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && NetworkManager.Singleton.IsServer)
                {
                    string pName = player.GetComponent<CrosswalkPlayer>()?.PlayerName ?? $"Player {netObj.OwnerClientId}";
                    RankingManager.Instance.AddScore(netObj.OwnerClientId, pName, points);
                }

                Debug.Log($"[CrosswalkGameManager] {player.name} 탈락! ({rank}등, {points}점 획득)");

                OnPlayerEliminated?.Invoke(player, reason);
                CheckGameOverCondition();
            }
        }

        // ★ 최후의 1인 판정 및 랭킹 씬 이동 로직
        private void CheckGameOverCondition()
        {
            if (allPlayers.Count >= 2)
            {
                if (alivePlayers.Count == 1)
                {
                    winnerPlayer = alivePlayers[0];

                    NetworkObject netObj = winnerPlayer.GetComponent<NetworkObject>();
                    if (netObj != null && NetworkManager.Singleton.IsServer)
                    {
                        string pName = winnerPlayer.GetComponent<CrosswalkPlayer>()?.PlayerName ?? $"Player {netObj.OwnerClientId}";
                        RankingManager.Instance.AddScore(netObj.OwnerClientId, pName, 3);
                    }

                    EndGame($"승리! 최후의 생존자: {winnerPlayer.name}");
                    Invoke("MoveToRankingScene", 3f);
                }
                else if (alivePlayers.Count == 0)
                {
                    EndGame("모두 탈락했습니다! (무승부)");
                    Invoke("MoveToRankingScene", 3f);
                }
            }
            else if (allPlayers.Count == 1)
            {
                if (alivePlayers.Count == 0)
                {
                    EndGame("탈락했습니다! (게임 오버)");
                    Invoke("MoveToRankingScene", 3f);
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

        // ★ 서버에서 랭킹 씬으로 모두 이동시킴
        private void MoveToRankingScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // 따옴표 안의 "Ranking"이 실제 씬 이름과 정확히 같은지 확인해 주세요.
                NetworkManager.Singleton.SceneManager.LoadScene("Ranking", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
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