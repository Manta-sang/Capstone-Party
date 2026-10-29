using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 톱날 피하기 미니게임의 입장, 라운드 진행, 승패 판정. 마지막까지 살아남은 한 명이 승리한다.
///
/// 팀 방식(CrosswalkNetworkEntry)을 따른다.
///  1) 로비에서 Host가 이 씬을 네트워크로 불러온다 (SceneLoader)
///  2) 모두 씬 로딩을 마치면 Host가 캐릭터를 생성한다 (공유 캐릭터 프리팹, 접속 ID로 번갈아 배정)
///  3) 각자 모든 캐릭터가 도착한 걸 확인하면 준비 응답을 보낸다
///  4) 모두 준비되면 서버 시간 기준 공통 시각에 같이 시작한다
/// 죽음 판정은 Host만 하고, 결과는 모든 참가자에게 전달된다. 종료 3초 후 같은 방을 유지한 채 Character 로비로 함께 복귀한다.
///
/// 씬에 이 오브젝트를 하나 두고 NetworkObject를 같이 붙인다 (씬에 배치된 NetworkObject).
/// NetworkManager의 Player Prefab은 비워둔다 (캐릭터는 이 매니저가 생성한다).
/// 스포너의 Auto Start는 끈다 (시작할 때 이 매니저가 켠다).
/// </summary>
public class SD_GameManager : NetworkBehaviour
{
    public static SD_GameManager Instance { get; private set; }

    private const ulong NoWinner = ulong.MaxValue;

    [Header("플레이어 생성 (팀 방식)")]
    [Tooltip("로비/Character 씬과 같은 순서의 캐릭터 프리팹 (NetworkObject 필요). 접속 ID % 개수로 번갈아 배정된다")]
    [SerializeField] private GameObject[] characterPrefabs;
    [Tooltip("참가 순서대로 배정할 시작 위치 (최대 인원 수 이상)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("참조")]
    [SerializeField] private SD_BladeSpawner spawner;
    [SerializeField] private SD_CameraFollow cameraFollow;
    [SerializeField] private SD_SpectatorCamera spectatorCamera;

    [Header("진행")]
    [Tooltip("모두 준비된 뒤 시작까지 기다리는 시간(초)")]
    [SerializeField] private float startDelay = 3f;
    [Tooltip("이 시간(초) 안에 모두 준비되지 않으면 입장 실패로 처리")]
    [SerializeField] private float readyTimeout = 30f;
    [Tooltip("이 씬에서 NetworkManager를 직접 시작해 테스트할 때만 켠다 (씬 전환 이벤트가 없어서 시작 신호를 직접 준다)")]
    [SerializeField] private bool beginWithoutSceneEvent = false;
    [SerializeField] private bool showDebugHUD = true;

    [Header("죽음 연출 (생성된 플레이어에 전달됨)")]
    [SerializeField] private float flightDistance = 12f;
    [SerializeField] private float arcHeight = 4f;
    [SerializeField] private float flightDuration = 1.2f;
    [Tooltip("죽은 뒤 이 시간(초)이 지나면 플레이어 오브젝트가 제거된다")]
    [SerializeField] private float removeDelay = 3f;

    // 모든 참가자에게 공유되는 상태 (값을 바꾸는 건 Host만)
    private readonly NetworkVariable<int> expectedPlayersNet = new NetworkVariable<int>(0);
    private readonly NetworkVariable<double> startTimeNet = new NetworkVariable<double>(-1d);
    // 승자가 없는 전원 탈락도 종료로 전달하기 위해 승자 ID와 종료 여부를 분리합니다.
    private readonly NetworkVariable<bool> endedNet = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<bool> runningNet = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<ulong> winnerIdNet = new NetworkVariable<ulong>(NoWinner);

    private readonly System.Collections.Generic.HashSet<ulong> readyClients = new System.Collections.Generic.HashSet<ulong>();
    private bool beginRequested;
    private bool entryBegun;
    private bool entryFailed;
    private float entryDeadline;
    private bool localReady;
    private bool roundStarted;
    private int playersAtStart;
    private bool gameOverRaised;
    private bool returningToLobby;
    private string returnError;

    /// <summary>게임 종료 시 한 번 호출된다. 승자 없는 종료는 null을 전달한다 (모든 컴퓨터에서)</summary>
    public event Action<SD_PlayerDeath> GameOver;

    public bool IsGameOver => endedNet.Value;

    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (SD_PlayerDeath player in SD_PlayerDeath.All)
            {
                if (!player.IsDead) count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {

        if (IsServer)
        {
            if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            if (beginWithoutSceneEvent) beginRequested = true;
        }
    }

    public override void OnNetworkDespawn()
    {

        if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
    }

    // ---------------------------------------------------------------- 입장 (Host)

    // 모든 참가자의 씬 로딩이 끝나면 Host가 캐릭터를 생성한다
    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
                                      System.Collections.Generic.List<ulong> completed, System.Collections.Generic.List<ulong> timedOut)
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

        if (clients.Length == 0 || clients.Length > 4 || spawnPoints == null || spawnPoints.Length < clients.Length ||
            characterPrefabs == null || characterPrefabs.Length == 0 ||
            spawnPoints.Take(clients.Length).Any(p => p == null) ||
            characterPrefabs.Any(p => p == null || p.GetComponent<NetworkObject>() == null))
        {
            Fail("캐릭터 프리팹(NetworkObject 필요) 또는 시작 위치 설정을 확인해 주세요.");
            return;
        }

        // 기존 PlayerObject를 덮어쓰지 않는다. NetworkManager의 Player Prefab이 비어 있어야 한다
        if (clients.Any(id => NetworkManager.SpawnManager.GetPlayerNetworkObject(id) != null))
        {
            Fail("이전 PlayerObject가 남아 있습니다. NetworkManager의 Player Prefab이 비어 있는지 확인해 주세요.");
            return;
        }

        for (int slot = 0; slot < clients.Length; slot++)
        {
            ulong id = clients[slot];
            int character = (int)(id % (ulong)characterPrefabs.Length);

            GameObject instance = Instantiate(characterPrefabs[character], spawnPoints[slot].position, spawnPoints[slot].rotation);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            Debug.Log($"[SD 입장] 참가자 {id}, 캐릭터 {character}, 시작 위치 {slot + 1} 생성 완료");
        }

        expectedPlayersNet.Value = clients.Length;
        entryDeadline = Time.realtimeSinceStartup + readyTimeout;
    }

    private void Fail(string reason)
    {
        entryFailed = true;
        Debug.LogError("[SD 입장] " + reason);
    }

    // ---------------------------------------------------------------- 매 프레임

    private void Update()
    {
        if (!IsSpawned) return;

        EnsureDeathComponents();

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

    // 생성된 모든 캐릭터에 죽음 처리 컴포넌트를 붙인다 (모든 컴퓨터에서, 프리팹은 수정하지 않는다)
    private void EnsureDeathComponents()
    {
        foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!spawned.IsPlayerObject || spawned.GetComponent<SD_PlayerDeath>() != null) continue;

            SD_PlayerDeath death = spawned.gameObject.AddComponent<SD_PlayerDeath>();
            death.Configure(flightDistance, arcHeight, flightDuration, removeDelay);

            // 시작 전 대기 동안에는 조작을 잠근다
            if (!roundStarted) death.SetControlsEnabled(false);
        }
    }

    // 모든 컴퓨터(Host 포함)에서 실행: 준비 확인과 공통 시작
    private void ClientTick()
    {
        // 네트워크 변수 적용이 끝난 Update에서 결과를 읽어 승자/종료 수신 순서에 의존하지 않습니다.
        if (IsGameOver)
        {
            ApplyRoundEnd();
            return;
        }
        NetworkObject localPlayer = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;

        // 모든 캐릭터가 이 컴퓨터에 도착했으면 서버에 준비 응답
        if (!localReady && expectedPlayersNet.Value > 0 && localPlayer != null &&
            SD_PlayerDeath.All.Count >= expectedPlayersNet.Value)
        {
            localReady = true;
            HookLocalPlayer(localPlayer);
            ReadyServerRpc();
        }

        // 서버가 정한 공통 시각이 되면 시작
        if (!roundStarted && localReady && startTimeNet.Value >= 0 && NetworkManager.ServerTime.Time >= startTimeNet.Value)
        {
            StartRoundLocal();
        }
    }

    // 내 캐릭터를 카메라와 관전 카메라에 연결한다
    private void HookLocalPlayer(NetworkObject localPlayer)
    {
        if (cameraFollow != null) cameraFollow.SetTarget(localPlayer.transform);
        if (spectatorCamera != null) spectatorCamera.SetLocalPlayer(localPlayer.GetComponent<SD_PlayerDeath>());
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        readyClients.Add(rpcParams.Receive.SenderClientId);
    }

    private void StartRoundLocal()
    {
        roundStarted = true;

        foreach (SD_PlayerDeath player in SD_PlayerDeath.All) player.SetControlsEnabled(true);

        if (IsServer)
        {
            playersAtStart = SD_PlayerDeath.All.Count;
            if (spawner != null) spawner.ResetSpawner();
            runningNet.Value = true;
            Debug.Log($"[SD] 라운드 시작 ({playersAtStart}명)");
        }
    }

    // Host 전용: 시작 시각 지정, 입장 실패 처리, 승패 판정
    private void ServerTick()
    {
        if (!entryBegun || entryFailed) return;

        if (startTimeNet.Value < 0)
        {
            if (expectedPlayersNet.Value > 0 && readyClients.Count >= expectedPlayersNet.Value)
            {
                startTimeNet.Value = NetworkManager.ServerTime.Time + startDelay;
            }
            else if (Time.realtimeSinceStartup > entryDeadline)
            {
                Fail("플레이어 준비 확인 시간이 초과되었습니다. 연결 상태를 확인해 주세요.");
            }
            return;
        }

        if (!runningNet.Value || IsGameOver) return;

        // 다인전의 최후 생존자와, 솔로/다인전의 생존자 0명 종료를 모두 처리합니다.

        SD_PlayerDeath lastAlive = null;
        int alive = 0;
        foreach (SD_PlayerDeath player in SD_PlayerDeath.All)
        {
            if (player.IsDead) continue;
            alive++;
            lastAlive = player;
        }

        if (alive == 0) EndGame(null);
        else if (playersAtStart >= 2 && alive == 1) EndGame(lastAlive);
    }

    // ---------------------------------------------------------------- 죽음

    /// <summary>톱날이 플레이어에 닿았을 때 호출된다 (Host에서만 의미가 있다). 죽음을 확정하고 모두에게 알린다</summary>
    public void ReportHit(SD_PlayerDeath player)
    {
        if (!IsSpawned || !IsServer) return;
        if (!runningNet.Value || player == null || player.IsDead) return;

        player.ApplyDeath();
        KillClientRpc(player.OwnerId);
    }

    [ClientRpc]
    private void KillClientRpc(ulong ownerId)
    {
        if (IsServer) return; // Host는 이미 처리했다

        foreach (SD_PlayerDeath player in SD_PlayerDeath.All)
        {
            if (player.OwnerId == ownerId) player.ApplyDeath();
        }
    }

    // ---------------------------------------------------------------- 승패, 다시 시작

    private void EndGame(SD_PlayerDeath winner)
    {
        // 서버에서 한 번만 확정하여 복귀 코루틴이 중복 실행되지 않게 합니다.
        if (!IsServer || IsGameOver) return;
        runningNet.Value = false;
        winnerIdNet.Value = winner != null ? winner.OwnerId : NoWinner;
        endedNet.Value = true;

        if (spawner != null) spawner.StopSpawning();
        SD_Blade.FreezeAll();
        ApplyRoundEnd();
        BeginLobbyReturn();
        Debug.Log(winner != null ? $"[SD] 승리: 플레이어 {winner.OwnerId}" : "[SD] 생존자 없이 게임 종료");
    }

    // 모든 참가자의 입력만 차단합니다. 이미 진행 중인 탈락 비행 연출은 유지합니다.
    private void ApplyRoundEnd()
    {
        if (gameOverRaised) return;
        gameOverRaised = true;
        SD_PlayerDeath winner = null;
        foreach (SD_PlayerDeath player in SD_PlayerDeath.All)
        {
            if (player == null) continue;
            player.SetControlsEnabled(false);
            if (winnerIdNet.Value != NoWinner && player.OwnerId == winnerIdNet.Value) winner = player;
        }
        GameOver?.Invoke(winner);
    }

    // 최초 복귀와 실패 후 재시도 모두 호스트만 실행합니다.
    private void BeginLobbyReturn()
    {
        if (!IsSpawned || !IsServer || !IsGameOver || returningToLobby) return;
        returningToLobby = true;
        returnError = null;
        StartCoroutine(ReturnToLobby());
    }

    private IEnumerator ReturnToLobby()
    {
        // 타임스케일과 무관하게 탈락 연출을 3초 보여 준 뒤 방 연결을 유지하며 이동합니다.
        yield return new WaitForSecondsRealtime(3f);
        if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening || !IsServer)
        {
            returningToLobby = false;
            yield break;
        }
        var sceneManager = NetworkManager.SceneManager;
        if (sceneManager == null)
        {
            returnError = "네트워크 씬 관리자가 없습니다.";
        }
        else
        {
            var status = sceneManager.LoadScene("Character", LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started) yield break;
            returnError = "Character 복귀 실패: " + status;
        }
        returningToLobby = false;
        Debug.LogError("[SD 복귀] " + returnError);
    }

    /// <summary>다시 시작한다 (Host만). 같은 씬을 네트워크로 다시 불러와서 모두를 새로 생성한다</summary>
    public void RestartGame()
    {
        if (!IsSpawned || !IsServer || IsGameOver || returningToLobby) return;
        NetworkManager.SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUILayout.BeginArea(new Rect(Screen.width - 260f, 10f, 250f, 160f), GUI.skin.box);
        GUILayout.Label("[ 톱날 피하기 ]");

        if (!IsSpawned)
        {
            GUILayout.Label("네트워크 없음: 로비에서 방을 만들고 시작하세요");
        }
        else if (IsGameOver)
        {
            GUILayout.Label(winnerIdNet.Value == NoWinner ? "게임 종료: 생존자 없음" : $"승리: 플레이어 {winnerIdNet.Value}");
            GUILayout.Label("Character 로비로 돌아갑니다...");
            // 이동 요청 실패 시 자동 반복 대신 호스트가 명시적으로 다시 시도합니다.
            if (IsServer && !returningToLobby && !string.IsNullOrEmpty(returnError))
            {
                GUILayout.Label(returnError);
                if (GUILayout.Button("로비 복귀 재시도")) BeginLobbyReturn();
            }
        }
        else if (runningNet.Value)
        {
            GUILayout.Label($"진행 중 / 생존 {AliveCount}명");
        }
        else if (startTimeNet.Value >= 0)
        {
            double remain = Math.Max(0d, startTimeNet.Value - NetworkManager.ServerTime.Time);
            GUILayout.Label($"{remain:F0}초 뒤 시작");
        }
        else
        {
            GUILayout.Label(entryFailed ? "입장 실패 (Console 확인)" : "참가자 입장 확인 중...");
        }

        GUILayout.EndArea();
    }
}