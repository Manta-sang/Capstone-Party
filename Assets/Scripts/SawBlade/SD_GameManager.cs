using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 톱날 피하기 미니게임의 라운드 진행과 승패 판정. 마지막까지 살아남은 한 명이 승리한다.
///
/// 멀티에서는 Host(서버)만 판정하고, 결과는 모든 참가자에게 공유된다.
/// 죽은 플레이어의 오브젝트는 몇 초 뒤 제거되므로(SD_PlayerLife), 라운드를 (다시) 시작할 때
/// 접속해 있는데 플레이어 오브젝트가 없는 사람은 이 매니저가 새로 생성한다.
/// 네트워크 없이 단독으로 돌릴 때(NetworkManager 없음)도 그대로 동작한다.
///
/// 씬에 이 오브젝트를 하나 두고 NetworkObject를 같이 붙인다 (씬에 배치된 NetworkObject).
/// </summary>
public class SD_GameManager : NetworkBehaviour
{
    private const ulong NoWinner = ulong.MaxValue;

    [Header("참조")]
    [SerializeField] private SD_BladeSpawner spawner;
    [Tooltip("제거된 플레이어를 새로 생성할 위치를 정하는 SD_SpawnPoints. 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private SD_SpawnPoints spawnPoints;

    [Header("설정")]
    [Tooltip("이 인원 이상이 접속하면 라운드가 자동으로 시작된다. 혼자 테스트하려면 1 (1명일 때는 승패를 가리지 않는다)")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private bool showDebugHUD = true;

    // 모든 참가자에게 공유되는 상태 (값을 바꾸는 건 Host만)
    private readonly NetworkVariable<bool> runningNet = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<ulong> winnerIdNet = new NetworkVariable<ulong>(NoWinner);
    private readonly NetworkVariable<int> connectedCountNet = new NetworkVariable<int>(0);

    // 네트워크 없이 단독으로 돌릴 때 쓰는 상태
    private bool localRunning;
    private SD_PlayerLife localWinner;

    private bool gameOverRaised;
    private int playersAtStart;

    /// <summary>승자가 정해졌을 때 한 번 호출된다. 멀티에서는 모든 컴퓨터에서 호출된다</summary>
    public event Action<SD_PlayerLife> GameOver;

    // Host이거나 단독 실행일 때만 판정한다
    private bool RunsLogic => !IsSpawned || IsServer;

    public bool IsRunning => IsSpawned ? runningNet.Value : localRunning;
    public bool IsGameOver => IsSpawned ? winnerIdNet.Value != NoWinner : localWinner != null;

    // 접속해 있는 인원. 죽은 플레이어의 오브젝트가 제거돼도 줄어들지 않는다
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
    }

    public override void OnNetworkDespawn()
    {
        winnerIdNet.OnValueChanged -= OnWinnerChanged;
    }

    private void Update()
    {
        if (!RunsLogic) return;

        // 접속 인원을 모두에게 공유한다 (대기 중 화면 표시와 시작 조건에 사용)
        if (IsSpawned)
        {
            int connected = NetworkManager.ConnectedClientsIds.Count;
            if (connectedCountNet.Value != connected) connectedCountNet.Value = connected;
        }

        if (IsGameOver) return;

        // 대기 중: 인원이 모이면 라운드 시작
        if (!IsRunning)
        {
            if (ConnectedPlayerCount >= minPlayersToStart) StartRound();
            return;
        }

        // 진행 중: 시작할 때 2명 이상이었는데 생존자가 1명만 남으면 그 사람이 승자
        // (도중에 나간 사람은 탈락으로 본다. 모두 동시에 탈락한 경우는 처리하지 않는다)
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

    /// <summary>라운드를 처음부터 다시 시작한다 (Host만)</summary>
    public void RestartGame()
    {
        if (!RunsLogic) return;
        Debug.Log($"[GM t={Time.time:F2}] RestartGame 호출");

        // 죽었다가 제거된 플레이어는 새로 만들고, 살아 있는 플레이어는 시작 위치로 되돌린다
        PrepareAllPlayers();

        SetWinner(null);
        SetRunning(false); // 다음 Update에서 인원이 충분하면 StartRound가 호출된다
    }

    /// <summary>이전 이름과의 호환용. RestartGame과 같다</summary>
    public void InitGame()
    {
        RestartGame();
    }

    private void StartRound()
    {
        Debug.Log($"[GM t={Time.time:F2}] StartRound");

        PrepareAllPlayers();
        playersAtStart = ConnectedPlayerCount;

        // 남아있는 톱날을 지우고 처음부터 생성 시작
        if (spawner != null) spawner.ResetSpawner();

        SetRunning(true);
    }

    // 모든 접속자가 살아있는 플레이어를 갖게 하고 시작 위치로 되돌린다
    private void PrepareAllPlayers()
    {
        HashSet<SD_PlayerLife> newlySpawned = SpawnMissingPlayers();

        foreach (SD_PlayerLife player in SD_PlayerLife.All)
        {
            // 방금 만든 플레이어는 이미 시작 위치에 있으므로 되돌릴 필요가 없다
            if (player != null && !newlySpawned.Contains(player)) player.ResetLife();
        }
    }

    // 접속해 있는데 플레이어 오브젝트가 없는 사람(죽어서 제거된 경우)에게 새 플레이어를 만들어 준다
    private HashSet<SD_PlayerLife> SpawnMissingPlayers()
    {
        var spawned = new HashSet<SD_PlayerLife>();
        if (!IsSpawned || !IsServer) return spawned; // 단독 테스트에서는 제거된 플레이어를 ResetLife가 되살린다

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

            Debug.Log($"[GM t={Time.time:F2}] 플레이어 {clientId} 새로 생성");
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

        Debug.Log($"[SawDodge] 승리: {PlayerLabel(Winner)}");
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
            GUILayout.Label($"대기 중 ({ConnectedPlayerCount}/{minPlayersToStart}명)");
        }

        if (IsGameOver && RunsLogic && GUILayout.Button("다시 시작"))
        {
            RestartGame();
        }

        GUILayout.EndArea();
    }
}