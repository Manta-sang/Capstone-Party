using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// 타일 뒤집기 미니게임의 입장, 진행, 시간 관리, 승패 판정. 제한 시간 안에 타일을 가장 많이 차지한 플레이어가 승리한다.
/// 
/// SD_GameManager의 구조를 그대로 따른다.
///  1) 로비에서 Host가 이 씬을 네트워크로 불러온다 (SceneLoader)
///  2) 모두 씬 로딩을 마치면 Host가 캐릭터를 생성한다 (공유 캐릭터 프리팹, 접속 ID로 배정, 시작 위치는 섞어서 한 곳씩)
///  3) 각자 모든 캐릭터가 도착한 걸 확인하면 준비 응답을 보낸다
///  4) 모두 준비되면 서버 시간 기준 공통 시각에 같이 시작하고, 종료 시각도 같이 정해진다
/// 칠하기 판정은 Host만 하고(TF_TilePaintTracker가 보고), 결과는 ClientRpc로 모든 참가자에게 전달된다.
/// 다시 시작은 씬을 네트워크로 다시 불러온다.
/// 
/// 플레이어 번호(자리, 0~3)는 접속 ID 오름차순 순서이고, 타일 색은 그 캐릭터 프리팹 번호와 같은 번호의 색을 쓴다.
/// (캐릭터 프리팹은 접속 ID % 개수로 배정된다. 점수는 자리별로 따로 센다)
/// 
/// 씬에 이 오브젝트를 하나 두고 NetworkObject를 같이 붙인다 (씬에 배치된 NetworkObject).
/// NetworkManager의 Player Prefab은 비워둔다 (캐릭터는 이 매니저가 생성한다).
/// 캐릭터 프리팹에는 NetworkObject가 필요하다. TF_TileGridManager와 TF_TilePaintTracker는 일반 컴포넌트이고 씬에서 자동으로 찾는다.
/// </summary>
[DisallowMultipleComponent]
public class TF_GameManager : NetworkBehaviour
{
    public enum MatchState { Waiting, Countdown, Running, Ended }

    public static TF_GameManager Instance { get; private set; }

    private const int NoResult = -1; // 아직 승패가 정해지지 않음 (승자 없음으로 끝난 경우는 0)
    private const int MaxPlayers = TF_TileGridManager.MaxPlayers;

    [Header("참조 (비워 두면 씬에서 자동으로 찾는다)")]
    [SerializeField] private TF_TileGridManager grid;
    [SerializeField] private TF_TilePaintTracker tracker;
    [Tooltip("내 플레이어를 따라다니는 카메라. 없어도 동작한다")]
    [SerializeField] private TF_CameraFollow cameraFollow;

    [Header("플레이어 생성")]
    [Tooltip("캐릭터 프리팹 (NetworkObject 필요). 접속 ID % 개수로 배정된다. TF_TileGridManager의 playerMaterials와 같은 번호끼리 짝이다 (캐릭터 k번 = 타일 색 k번)")]
    [SerializeField] private GameObject[] characterPrefabs;
    [Tooltip("시작 위치들 (접속한 인원 수 이상). 이 중 서로 다른 곳이 랜덤으로 하나씩 배정된다. 회전 방향이 처음 바라보는 방향이 된다")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("진행")]
    [Tooltip("모두 준비된 뒤 시작까지 기다리는 시간(초). 이 시간 동안 조작이 잠겨 있다")]
    [SerializeField] private float startDelay = 3f;
    [Tooltip("게임 시간(초). 시작된 뒤 이 시간이 지나면 종료되고 승자가 정해진다")]
    [SerializeField] private float gameDuration = 60f;
    [Tooltip("이 시간(초) 안에 모두 준비되지 않으면 입장 실패로 처리")]
    [SerializeField] private float readyTimeout = 30f;
    [Tooltip("이 씬에서 NetworkManager를 직접 시작해 테스트할 때만 켠다 (씬 전환 이벤트가 없어서 시작 신호를 직접 준다)")]
    [SerializeField] private bool beginWithoutSceneEvent = false;
    [Tooltip("시작 전(카운트다운) 동안 플레이어 조작(이동/밀치기)을 잠근다")]
    [SerializeField] private bool lockControlsBeforeStart = true;
    [Tooltip("게임이 끝나면 플레이어 조작을 잠근다")]
    [SerializeField] private bool lockControlsAtEnd = true;

    // 모든 참가자에게 공유되는 상태 (값을 바꾸는 건 Host만)
    private readonly NetworkVariable<int> expectedPlayersNet = new NetworkVariable<int>(0);
    private readonly NetworkVariable<double> startTimeNet = new NetworkVariable<double>(-1d);
    private readonly NetworkVariable<double> endTimeNet = new NetworkVariable<double>(-1d);
    private readonly NetworkVariable<bool> runningNet = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> winnersMaskNet = new NetworkVariable<int>(NoResult); // 비트 i가 켜져 있으면 i번 자리가 승자

    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private bool beginRequested;
    private bool entryBegun;
    private bool entryFailed;
    private float entryDeadline;
    private bool localReady;
    private bool roundStarted;
    private bool gameOverRaised;

    // 이 컴퓨터에 도착한 플레이어들 (접속 ID 오름차순 = 자리 순서)
    private readonly List<NetworkObject> players = new List<NetworkObject>();
    private readonly HashSet<ulong> knownPlayerIds = new HashSet<ulong>();
    private readonly List<int> winners = new List<int>();

    /// <summary>카운트다운이 끝나고 진행이 시작될 때 한 번 호출된다 (모든 컴퓨터에서)</summary>
    public event Action GameStarted;
    /// <summary>
    /// 게임이 끝났을 때 한 번 호출된다 (모든 컴퓨터에서). 인자는 승자 자리 번호 목록.
    /// 1명이면 단독 승리, 2명 이상이면 공동 승리(무승부), 비어 있으면 아무도 칠하지 못한 것.
    /// </summary>
    public event Action<IReadOnlyList<int>> GameOver;

    /// <summary>가장 최근 게임의 승자 자리 번호 목록 (진행 중에는 비어 있다)</summary>
    public IReadOnlyList<int> Winners => winners;

    public MatchState State
    {
        get
        {
            if (!IsSpawned) return MatchState.Waiting;
            if (winnersMaskNet.Value != NoResult) return MatchState.Ended;
            if (runningNet.Value) return MatchState.Running;
            if (startTimeNet.Value >= 0) return MatchState.Countdown;
            return MatchState.Waiting;
        }
    }

    public bool IsRunning => State == MatchState.Running;
    public bool IsGameOver => IsSpawned && winnersMaskNet.Value != NoResult;

    /// <summary>카운트다운이 끝나기까지 남은 시간(초)</summary>
    public float CountdownRemaining
    {
        get
        {
            if (!IsSpawned || roundStarted || startTimeNet.Value < 0) return 0f;
            return Mathf.Max(0f, (float)(startTimeNet.Value - NetworkManager.ServerTime.Time));
        }
    }

    /// <summary>게임이 끝나기까지 남은 시간(초). 진행 전에는 전체 게임 시간, 종료 후에는 0</summary>
    public float RemainingTime
    {
        get
        {
            switch (State)
            {
                case MatchState.Running: return Mathf.Max(0f, (float)(endTimeNet.Value - NetworkManager.ServerTime.Time));
                case MatchState.Ended: return 0f;
                default: return gameDuration;
            }
        }
    }

    private void Awake()
    {
        Instance = this;

        if (grid == null) grid = FindObjectOfType<TF_TileGridManager>();
        if (tracker == null) tracker = FindObjectOfType<TF_TilePaintTracker>();
        if (cameraFollow == null) cameraFollow = FindObjectOfType<TF_CameraFollow>(); // 선택 사항

        if (grid == null) Debug.LogError("[TF_GameManager] 씬에서 TF_TileGridManager를 찾지 못했습니다.");
        if (tracker == null) Debug.LogError("[TF_GameManager] 씬에서 TF_TilePaintTracker를 찾지 못했습니다.");
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        winnersMaskNet.OnValueChanged += OnWinnersChanged;

        if (IsServer)
        {
            if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            if (beginWithoutSceneEvent) beginRequested = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        winnersMaskNet.OnValueChanged -= OnWinnersChanged;

        if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
    }

    // ---------------------------------------------------------------- 입장 (Host)

    // 모든 참가자의 씬 로딩이 끝나면 Host가 캐릭터를 생성한다
    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
                                      List<ulong> completed, List<ulong> timedOut)
    {
        if (!IsServer || sceneName != gameObject.scene.name || entryBegun) return;

        if (timedOut.Count > 0)
        {
            entryBegun = true;
            Fail("참가자의 씬 로딩이 시간 초과되었습니다. 방에서 다시 시작해 주세요.");
            return;
        }

        beginRequested = true; // 다음 Update에서 시작 (씬 초기화가 끝난 뒤 생성)
    }

    private void BeginEntry()
    {
        if (entryBegun) return;
        entryBegun = true;

        ulong[] clients = NetworkManager.ConnectedClientsIds.OrderBy(id => id).ToArray();

        if (clients.Length == 0 || clients.Length > MaxPlayers || spawnPoints == null || spawnPoints.Length < clients.Length ||
            characterPrefabs == null || characterPrefabs.Length == 0 ||
            spawnPoints.Any(p => p == null) ||
            characterPrefabs.Any(p => p == null || p.GetComponent<NetworkObject>() == null))
        {
            Fail("캐릭터 프리팹(NetworkObject 필요) 또는 시작 위치(접속 인원 수 이상) 설정을 확인해 주세요.");
            return;
        }

        // 기존 PlayerObject를 덮어쓰지 않는다. NetworkManager의 Player Prefab이 비어 있어야 한다
        if (clients.Any(id => NetworkManager.SpawnManager.GetPlayerNetworkObject(id) != null))
        {
            Fail("이전 PlayerObject가 남아 있습니다. NetworkManager의 Player Prefab이 비어 있는지 확인해 주세요.");
            return;
        }

        if (grid != null && grid.ColorCount < characterPrefabs.Length)
        {
            Debug.LogWarning($"[TF 입장] playerMaterials({grid.ColorCount}개)가 characterPrefabs({characterPrefabs.Length}개)보다 적습니다. " +
                             "모자란 번호의 캐릭터는 타일 색이 흰색으로 보입니다.");
        }

        // 시작 위치 번호를 섞어서 앞에서부터 한 명에 하나씩 배정한다 (같은 위치에 겹치지 않는다)
        List<int> order = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++) order.Add(i);
        Shuffle(order);

        for (int slot = 0; slot < clients.Length; slot++)
        {
            ulong id = clients[slot];
            int character = (int)(id % (ulong)characterPrefabs.Length);
            Transform point = spawnPoints[order[slot]];

            GameObject instance = Instantiate(characterPrefabs[character], point.position, point.rotation);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);

            // 칠하기 판정은 Host의 트래커가 한다. 자리 번호로 등록한다
            if (tracker != null) tracker.Register(instance.transform, slot);

            Debug.Log($"[TF 입장] 참가자 {id}, 자리 {slot}, 캐릭터 {character}, 시작 위치 {order[slot]} 생성 완료");
        }

        expectedPlayersNet.Value = clients.Length;
        entryDeadline = Time.realtimeSinceStartup + readyTimeout;
    }

    private void Fail(string reason)
    {
        entryFailed = true;
        Debug.LogError("[TF 입장] " + reason);
    }

    // Fisher-Yates 섞기
    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ---------------------------------------------------------------- 매 프레임

    private void Update()
    {
        if (!IsSpawned) return;

        // 모든 플레이어가 도착하기 전까지, 이 컴퓨터에 도착한 플레이어를 파악한다
        if (!localReady) RefreshPlayers();

        if (IsServer)
        {
            if (beginRequested && !entryBegun)
            {
                beginRequested = false;
                BeginEntry();
            }
            ServerTick();
        }

        ClientTick();
    }

    // 생성된 모든 PlayerObject를 접속 ID 오름차순(= 자리 순서)으로 모은다 (모든 컴퓨터에서, 프리팹은 수정하지 않는다)
    private void RefreshPlayers()
    {
        players.Clear();
        foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (spawned.IsPlayerObject) players.Add(spawned);
        }
        players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        // 새로 도착한 플레이어는 시작 전 대기 동안 조작을 잠근다
        foreach (NetworkObject player in players)
        {
            if (knownPlayerIds.Add(player.OwnerClientId) && !roundStarted && lockControlsBeforeStart)
                SetControls(player, false);
        }
    }

    // 모든 컴퓨터(Host 포함)에서 실행: 준비 확인과 공통 시작
    private void ClientTick()
    {
        NetworkObject localPlayer = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;

        // 모든 캐릭터가 이 컴퓨터에 도착했으면 자리/색을 정리하고 서버에 준비 응답
        if (!localReady && expectedPlayersNet.Value > 0 && localPlayer != null &&
            players.Count >= expectedPlayersNet.Value)
        {
            localReady = true;
            SetupRoster(localPlayer);
            ReadyServerRpc();
        }

        // 서버가 정한 공통 시각이 되면 시작
        if (!roundStarted && localReady && startTimeNet.Value >= 0 && NetworkManager.ServerTime.Time >= startTimeNet.Value)
        {
            StartRoundLocal();
        }
    }

    // 도착한 플레이어들의 자리 순서대로 타일 색을 정하고, 카메라를 내 플레이어에 연결한다
    // (모든 컴퓨터가 같은 순서로 계산하므로 같은 결과가 나온다)
    private void SetupRoster(NetworkObject localPlayer)
    {
        if (grid != null)
        {
            for (int slot = 0; slot < players.Count && slot < MaxPlayers; slot++)
            {
                int character = (int)(players[slot].OwnerClientId % (ulong)characterPrefabs.Length);
                grid.SetPlayerColorIndex(slot, character);
            }
        }

        if (cameraFollow != null) cameraFollow.SetTarget(localPlayer.transform);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        readyClients.Add(rpcParams.Receive.SenderClientId);
    }

    private void StartRoundLocal()
    {
        roundStarted = true;

        SetPlayersControl(true);
        GameStarted?.Invoke();

        if (IsServer)
        {
            if (tracker != null)
            {
                tracker.ResetTracking();
                tracker.StartPainting();
            }
            runningNet.Value = true;
            Debug.Log($"[TF] 게임 시작 ({players.Count}명)");
        }
    }

    // Host 전용: 시작/종료 시각 지정, 입장 실패 처리, 종료 판정
    private void ServerTick()
    {
        if (!entryBegun || entryFailed) return;

        if (startTimeNet.Value < 0)
        {
            if (expectedPlayersNet.Value > 0 && readyClients.Count >= expectedPlayersNet.Value)
            {
                double start = NetworkManager.ServerTime.Time + startDelay;
                startTimeNet.Value = start;
                endTimeNet.Value = start + gameDuration;
            }
            else if (Time.realtimeSinceStartup > entryDeadline)
            {
                Fail("플레이어 준비 확인 시간이 초과되었습니다. 연결 상태를 확인해 주세요.");
            }
            return;
        }

        if (!runningNet.Value || IsGameOver) return;

        if (NetworkManager.ServerTime.Time >= endTimeNet.Value) EndGame();
    }

    // ---------------------------------------------------------------- 칠하기

    /// <summary>
    /// 트래커가 "플레이어가 새 타일에 들어왔다"고 보고한다. (Host에서만 의미가 있다)
    /// Host에서 적용하고, 실제로 바뀌었으면 모든 참가자에게 알린다.
    /// (SD_GameManager.ReportHit이 ApplyDeath + KillClientRpc를 하는 것과 같다)
    /// </summary>
    public void ReportPaint(int tileIndex, int playerIndex)
    {
        if (!IsSpawned || !IsServer) return;
        if (!runningNet.Value || IsGameOver || grid == null) return;

        if (grid.SetTileOwner(tileIndex, playerIndex))
            PaintClientRpc(tileIndex, playerIndex);
    }

    [ClientRpc]
    private void PaintClientRpc(int tileIndex, int playerIndex)
    {
        if (IsServer) return; // Host는 이미 처리했다

        if (grid != null) grid.SetTileOwner(tileIndex, playerIndex);
    }

    // ---------------------------------------------------------------- 승패, 다시 시작

    // Host 전용: 가장 많은 타일을 차지한 자리가 승자. 동점이면 모두 승자(공동 승리). 아무도 못 칠했으면 승자 없음
    private void EndGame()
    {
        runningNet.Value = false;
        if (tracker != null) tracker.StopPainting();

        int max = 0;
        for (int p = 0; p < MaxPlayers; p++)
            max = Mathf.Max(max, grid.GetScore(p));

        int mask = 0;
        if (max > 0)
        {
            for (int p = 0; p < MaxPlayers; p++)
            {
                if (grid.GetScore(p) == max) mask |= 1 << p;
            }
        }

        string scoreText = "";
        for (int p = 0; p < MaxPlayers; p++)
            scoreText += $"P{p + 1}={grid.GetScore(p)} ";

        Debug.Log($"[TF] 게임 종료. 점수: {scoreText}/ 승자 마스크: {mask}");

        winnersMaskNet.Value = mask;
    }

    // 모든 컴퓨터에서 호출된다
    private void OnWinnersChanged(int previous, int current)
    {
        if (current == NoResult || gameOverRaised) return;
        gameOverRaised = true;

        winners.Clear();
        for (int p = 0; p < MaxPlayers; p++)
        {
            if ((current & (1 << p)) != 0) winners.Add(p);
        }

        if (lockControlsAtEnd) SetPlayersControl(false);

        GameOver?.Invoke(winners);
    }

    /// <summary>다시 시작한다 (Host만). 같은 씬을 네트워크로 다시 불러와서 모두를 새로 생성한다</summary>
    [ContextMenu("Restart Game")]
    public void RestartGame()
    {
        if (!IsSpawned || !IsServer) return;
        NetworkManager.SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
    }

    // ---------------------------------------------------------------- 조작 잠금

    // 이 컴퓨터에 도착한 모든 플레이어의 이동/밀치기 조작을 켜고 끈다 (SD_PlayerDeath.SetControlsEnabled와 같은 방식)
    private void SetPlayersControl(bool controlsEnabled)
    {
        foreach (NetworkObject player in players)
            SetControls(player, controlsEnabled);
    }

    private static void SetControls(NetworkObject player, bool controlsEnabled)
    {
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = controlsEnabled;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.enabled = controlsEnabled;
    }

    // ---------------------------------------------------------------- 에디터 보조

    // 씬 뷰에서 시작 위치와 바라보는 방향을 확인할 수 있다.
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;

            Vector3 center = point.position + Vector3.up * 0.5f;
            Gizmos.DrawWireSphere(center, 0.5f);
            Gizmos.DrawLine(center, center + point.forward * 1.2f);
        }
    }
}