using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 톱날 피하기 미니게임의 라운드 진행 및 승패 판정
/// (2번 묶음 CrosswalkNetworkEntry의 Named Message 동기화 패턴 적용 버전)
/// </summary>
public class SD_GameManager : NetworkBehaviour
{
    private const ulong NoWinner = ulong.MaxValue;

    [Header("참조")]
    [SerializeField] private SD_BladeSpawner spawner;
    [Tooltip("제거된 플레이어를 새로 생성할 위치를 정하는 SD_SpawnPoints. 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private SD_SpawnPoints spawnPoints;

    [Header("설정")]
    [Tooltip("이 인원 이상이 접속하면 라운드가 자동으로 시작된다. 혼자 테스트하려면 1")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private bool showDebugHUD = true;

    // 레퍼런스(2번 묶음)와 동일한 네트워크 메시지 프로토콜 상수
    private const string RosterMessage = "SD.Entry.Roster";
    private const string ReadyMessage = "SD.Entry.Ready";
    private const string StartMessage = "SD.Entry.Start";
    private const float ReadyTimeout = 30f;

    // 모든 참가자에게 공유되는 상태 (값을 바꾸는 건 Host만)
    private readonly NetworkVariable<bool> runningNet = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<ulong> winnerIdNet = new NetworkVariable<ulong>(NoWinner);
    private readonly NetworkVariable<int> connectedCountNet = new NetworkVariable<int>(0);

    // 레퍼런스 동기화 관련 변수들
    private NetworkManager network;
    private NetworkSceneManager sceneManager;
    private CustomMessagingManager messages;
    private readonly List<ulong> owners = new List<ulong>();
    private readonly List<ulong> objectIds = new List<ulong>();
    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private int entryToken;
    private bool spawnRequested;
    private bool rosterReceived;
    private bool localReady;
    private bool networkStarted;
    private double startsAt = -1;
    private float readyDeadline;

    // 네트워크 없이 단독으로 돌릴 때 쓰는 상태
    private bool localRunning;
    private SD_PlayerLife localWinner;

    private bool gameOverRaised;
    private int playersAtStart;

    public event Action<SD_PlayerLife> GameOver;

    private bool RunsLogic => !IsSpawned || IsServer;

    public bool IsRunning => IsSpawned ? runningNet.Value : localRunning;
    public bool IsGameOver => IsSpawned ? winnerIdNet.Value != NoWinner : localWinner != null;

    private int ConnectedPlayerCount => IsSpawned ? connectedCountNet.Value : SD_PlayerLife.All.Count;

    public SD_PlayerLife Winner
    {
        get
        {
            if (!IsSpawned) return localWinner;
            if (winnerIdNet.Value == NoWinner) return null;

            foreach (SD_PlayerLife player in SD_PlayerLife.All)
            {
                if (player.OwnerClientId == winnerIdNet.Value) return player;
            }
            return null;
        }
    }

    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (SD_PlayerLife player in SD_PlayerLife.All)
            {
                if (!player.IsDead) count++;
            }
            return count;
        }
    }

    public override void OnNetworkSpawn()
    {
        winnerIdNet.OnValueChanged += OnWinnerChanged;
        if (winnerIdNet.Value != NoWinner) RaiseGameOver();

        // 레퍼런스 메시지 시스템 초기화
        network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            sceneManager = network.SceneManager;
            messages = network.CustomMessagingManager;

            sceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            messages.RegisterNamedMessageHandler(RosterMessage, OnRosterReceived);
            messages.RegisterNamedMessageHandler(ReadyMessage, OnReadyReceived);
            messages.RegisterNamedMessageHandler(StartMessage, OnStartReceived);
        }
    }

    public override void OnNetworkDespawn()
    {
        winnerIdNet.OnValueChanged -= OnWinnerChanged;

        if (sceneManager != null) sceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        if (messages != null)
        {
            messages.UnregisterNamedMessageHandler(RosterMessage);
            messages.UnregisterNamedMessageHandler(ReadyMessage);
            messages.UnregisterNamedMessageHandler(StartMessage);
        }
    }

    // 2번 묶음 벤치마킹: 씬 로딩 완료 시 서버가 참가자 명단(Roster) 전파
    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (!IsServer || sceneName != gameObject.scene.name || spawnRequested) return;
        spawnRequested = true;

        var clients = network.ConnectedClientsIds.OrderBy(id => id).ToArray();
        StartCoroutine(SetupRoster(clients));
    }

    private System.Collections.IEnumerator SetupRoster(ulong[] clients)
    {
        yield return null;
        if (!IsServer) yield break;

        clients = clients.Where(id => network.ConnectedClients.ContainsKey(id)).ToArray();
        if (clients.Length == 0) yield break;

        entryToken = Guid.NewGuid().GetHashCode();

        // 현재 씬에 스폰된 플레이어들의 객체 ID 수집
        foreach (ulong id in clients)
        {
            var playerObj = network.SpawnManager.GetPlayerNetworkObject(id);
            if (playerObj != null)
            {
                owners.Add(id);
                objectIds.Add(playerObj.NetworkObjectId);
            }
        }

        rosterReceived = true;
        readyDeadline = Time.realtimeSinceStartup + ReadyTimeout;

        // 클라이언트들에게 Roster 전송 (2번 묶음 패턴)
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe(entryToken);
            writer.WriteValueSafe(owners.Count);
            for (int i = 0; i < owners.Count; i++)
            {
                writer.WriteValueSafe(owners[i]);
                writer.WriteValueSafe(objectIds[i]);
            }
            foreach (ulong id in owners)
            {
                if (id != NetworkManager.ServerClientId)
                    messages.SendNamedMessage(RosterMessage, id, writer, NetworkDelivery.ReliableSequenced);
            }
        }
    }

    private void OnRosterReceived(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || IsServer || rosterReceived) return;
        reader.ReadValueSafe(out entryToken);
        reader.ReadValueSafe(out int count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ulong owner);
            reader.ReadValueSafe(out ulong objectId);
            owners.Add(owner);
            objectIds.Add(objectId);
        }
        rosterReceived = true;
        readyDeadline = Time.realtimeSinceStartup + ReadyTimeout;
    }

    private void Update()
    {
        // 멀티플레이어 환경에서의 동기화 시작 대기 처리
        if (IsSpawned && !networkStarted && rosterReceived)
        {
            if (!localReady)
            {
                bool allPresent = true;
                foreach (ulong objId in objectIds)
                {
                    if (!network.SpawnManager.SpawnedObjects.TryGetValue(objId, out var player) || !player.IsPlayerObject)
                    {
                        allPresent = false;
                        break;
                    }
                }

                if (allPresent)
                {
                    localReady = true;
                    if (IsServer)
                    {
                        readyClients.Add(network.LocalClientId);
                    }
                    else
                    {
                        using (var writer = new FastBufferWriter(4, Allocator.Temp))
                        {
                            writer.WriteValueSafe(entryToken);
                            messages.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer);
                        }
                    }
                }
            }

            // 서버: 모든 클라이언트가 Ready이고 최소 인원이 찼을 때 시작 시간 예약 및 전파
            if (IsServer && startsAt < 0 && owners.Count > 0 && owners.All(readyClients.Contains))
            {
                if (ConnectedPlayerCount >= minPlayersToStart)
                {
                    startsAt = network.ServerTime.Time + 2.0; // 2초 뒤 공통 시작
                    using (var writer = new FastBufferWriter(12, Allocator.Temp))
                    {
                        writer.WriteValueSafe(entryToken);
                        writer.WriteValueSafe(startsAt);
                        foreach (ulong id in owners)
                        {
                            if (id != NetworkManager.ServerClientId)
                                messages.SendNamedMessage(StartMessage, id, writer);
                        }
                    }
                }
            }

            // 공통 시작 시점에 도달하면 게임 시작
            if (startsAt >= 0 && localReady && network.ServerTime.Time >= startsAt)
            {
                networkStarted = true;
                if (RunsLogic) StartRound();
            }
            return;
        }

        // --- 기존 SD_GameManager 핵심 로직 ---
        if (!RunsLogic) return;

        if (IsSpawned)
        {
            int connected = NetworkManager.ConnectedClientsIds.Count;
            if (connectedCountNet.Value != connected) connectedCountNet.Value = connected;
        }

        if (IsGameOver) return;

        if (!IsRunning)
        {
            // 네트워크가 아닐 때의 단독 실행 방어 코드
            if (!IsSpawned && ConnectedPlayerCount >= minPlayersToStart) StartRound();
            return;
        }

        if (playersAtStart < 2) return;

        SD_PlayerLife lastAlive = null;
        int alive = 0;
        foreach (SD_PlayerLife player in SD_PlayerLife.All)
        {
            if (player.IsDead) continue;
            alive++;
            lastAlive = player;
        }

        if (alive == 1) EndGame(lastAlive);
    }

    private void OnReadyReceived(ulong sender, FastBufferReader reader)
    {
        if (!IsServer || !rosterReceived || !owners.Contains(sender)) return;
        reader.ReadValueSafe(out int token);
        if (token == entryToken) readyClients.Add(sender);
    }

    private void OnStartReceived(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || IsServer || !rosterReceived) return;
        reader.ReadValueSafe(out int token);
        reader.ReadValueSafe(out double time);
        if (token == entryToken) startsAt = time;
    }

    public void RestartGame()
    {
        if (!RunsLogic) return;
        Debug.Log($"[GM t={Time.time:F2}] RestartGame 호출");

        PrepareAllPlayers();
        SetWinner(null);
        SetRunning(false);
        networkStarted = false;
        startsAt = -1;
        readyClients.Clear();
        localReady = false;
    }

    public void InitGame()
    {
        RestartGame();
    }

    private void StartRound()
    {
        Debug.Log($"[GM t={Time.time:F2}] StartRound 실행");

        PrepareAllPlayers();
        playersAtStart = ConnectedPlayerCount;

        if (spawner != null) spawner.ResetSpawner();

        SetRunning(true);
    }

    private void PrepareAllPlayers()
    {
        HashSet<SD_PlayerLife> newlySpawned = SpawnMissingPlayers();

        foreach (SD_PlayerLife player in SD_PlayerLife.All)
        {
            if (player != null && !newlySpawned.Contains(player)) player.ResetLife();
        }
    }

    private HashSet<SD_PlayerLife> SpawnMissingPlayers()
    {
        var spawned = new HashSet<SD_PlayerLife>();
        if (!IsSpawned || !IsServer) return spawned;

        NetworkManager manager = NetworkManager.Singleton;
        GameObject prefab = manager.NetworkConfig.PlayerPrefab;
        if (prefab == null) return spawned;

        if (spawnPoints == null) spawnPoints = FindObjectOfType<SD_SpawnPoints>();

        foreach (ulong clientId in manager.ConnectedClientsIds)
        {
            if (manager.SpawnManager.GetPlayerNetworkObject(clientId) != null) continue;

            Vector3 position = spawnPoints != null ? spawnPoints.GetSpawnPosition(clientId) : Vector3.zero;
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

            SD_PlayerLife life = instance.GetComponent<SD_PlayerLife>();
            if (life != null) spawned.Add(life);
        }

        return spawned;
    }

    private void EndGame(SD_PlayerLife winner)
    {
        Debug.Log($"[GM t={Time.time:F2}] EndGame 승자={PlayerLabel(winner)}");

        SetRunning(false);
        SetWinner(winner);

        if (spawner != null) spawner.StopSpawning();
        SD_Blade.FreezeAll();
    }

    private void SetRunning(bool value)
    {
        if (IsSpawned) runningNet.Value = value;
        else localRunning = value;
    }

    private void SetWinner(SD_PlayerLife winner)
    {
        if (IsSpawned)
        {
            winnerIdNet.Value = winner == null ? NoWinner : winner.OwnerClientId;
        }
        else
        {
            localWinner = winner;
        }

        if (winner == null) gameOverRaised = false;
        else RaiseGameOver();
    }

    private void OnWinnerChanged(ulong previous, ulong current)
    {
        if (current == NoWinner) gameOverRaised = false;
        else RaiseGameOver();
    }

    private void RaiseGameOver()
    {
        if (gameOverRaised) return;
        gameOverRaised = true;

        GameOver?.Invoke(Winner);
    }

    private static string PlayerLabel(SD_PlayerLife player)
    {
        if (player == null) return "-";
        return player.IsSpawned ? $"플레이어 {player.OwnerClientId}" : player.name;
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUILayout.BeginArea(new Rect(Screen.width - 260f, 10f, 250f, 160f), GUI.skin.box);
        GUILayout.Label("[ 톱날 피하기 ]");

        if (IsGameOver)
        {
            GUILayout.Label($"승리: {PlayerLabel(Winner)}");
        }
        else if (IsRunning)
        {
            GUILayout.Label($"진행 중 / 생존 {AliveCount}명");
        }
        else
        {
            string status = (IsSpawned && !networkStarted) ? "참가자 동기화 대기 중..." : $"대기 중 ({ConnectedPlayerCount}/{minPlayersToStart}명)";
            GUILayout.Label(status);
        }

        if (IsGameOver && RunsLogic && GUILayout.Button("다시 시작"))
        {
            RestartGame();
        }

        GUILayout.EndArea();
    }
}