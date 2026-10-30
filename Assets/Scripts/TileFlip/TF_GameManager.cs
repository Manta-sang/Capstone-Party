using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 타일 뒤집기 미니게임의 플레이어 생성, 진행, 시간 관리, 승패 판정. (오프라인 버전)
/// 타일을 가장 많이 차지한 플레이어가 승리한다.
/// 
/// 진행 순서: 플레이어 생성 -> 카운트다운(조작 잠금) -> 진행(칠하기 시작) -> 종료(칠하기 멈춤, 승자 판정)
/// 
/// 플레이어 생성:
///  - 씬에 배치한 시작 위치(spawnPoints) 중 서로 다른 곳을 랜덤으로 하나씩 골라 생성한다.
///  - 플레이어 번호(= 칠해지는 색)는 생성 순서이고, 시작 위치만 랜덤이다.
///  - 캐릭터 프리팹은 SD_GameManager와 같이 "번호 % 프리팹 개수"로 번갈아 배정한다.
///  - 생성한 플레이어는 TF_TilePaintTracker에 번호와 함께 등록한다.
///  - 플레이어 수는 시작 위치 개수(최대 4명)로 정해진다. (멀티에서는 접속한 사람 수)
///  - 카메라(TF_CameraFollow)는 내 플레이어를 따라가게 지정한다. 오프라인에서는 0번 플레이어.
///  - 타일 색은 그 플레이어 캐릭터의 프리팹 번호와 같은 번호의 playerMaterials를 쓴다. (점수는 플레이어별로 따로 센다)
/// 
/// SD_GameManager와 같은 구조로 만들어서, 멀티 전환 시 아래 부분만 바꾸면 된다.
///  - 플레이어 생성은 Host만. 접속 ID 오름차순 순서가 플레이어 번호(slot)가 되고, 시작 위치는 지금처럼 섞어서 배정한 뒤
///    NetworkObject.SpawnAsPlayerObject(접속 ID, true)로 생성한다. (NetworkManager의 Player Prefab은 비워 둔다)
///  - 시작/종료 시각(startTime, endTime)을 서버 시간 기준 NetworkVariable로 공유
///  - 상태 변경과 승패 판정은 Host만
///  - BeginMatch / RestartGame은 씬 재로딩 + 준비 확인 절차로 대체
/// 
/// 사용법: 씬의 MinigameManager 오브젝트 등에 붙인다.
///         플레이어 등록은 이 매니저 한 곳에서만 한다. (생성한 플레이어를 TF_TilePaintTracker에 자동으로 등록한다)
///         TF_TileGridManager와 TF_TilePaintTracker는 비워 두면 씬에서 자동으로 찾는다.
/// </summary>
[DisallowMultipleComponent]
public class TF_GameManager : MonoBehaviour
{
    public enum MatchState { Waiting, Countdown, Running, Ended }

    [Header("참조 (비워 두면 씬에서 자동으로 찾는다)")]
    [SerializeField] private TF_TileGridManager grid;
    [SerializeField] private TF_TilePaintTracker tracker;
    [Tooltip("내 플레이어를 따라다니는 카메라. 없어도 동작한다")]
    [SerializeField] private TF_CameraFollow cameraFollow;

    [Header("플레이어 생성")]
    [Tooltip("캐릭터 프리팹. 플레이어 번호 % 개수로 번갈아 배정된다. TF_TileGridManager의 playerMaterials와 같은 번호끼리 짝이다 (캐릭터 k번 = 타일 색 k번)")]
    [SerializeField] private GameObject[] characterPrefabs;
    [Tooltip("시작 위치들. 플레이어 수는 이 개수(최대 4명)로 정해지고, 서로 다른 곳이 랜덤으로 하나씩 배정된다. " +
             "4개보다 많으면 그중 랜덤한 4곳이 쓰인다. 회전 방향이 처음 바라보는 방향이 된다 (멀티에서는 접속한 사람 수로 정해진다)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("진행")]
    [Tooltip("카운트다운 시간(초). 이 시간이 지나면 조작이 풀리고 칠하기가 시작된다")]
    [SerializeField] private float startDelay = 3f;
    [Tooltip("게임 시간(초). 이 시간이 지나면 종료되고 승자가 정해진다")]
    [SerializeField] private float gameDuration = 60f;
    [Tooltip("켜면 씬이 시작하자마자 카운트다운을 시작한다. 오프라인 테스트용 (멀티에서는 모두 준비된 뒤 시작한다)")]
    [SerializeField] private bool autoStart = true;
    [Tooltip("카운트다운 동안 플레이어 조작(이동/밀치기)을 잠근다")]
    [SerializeField] private bool lockControlsBeforeStart = true;
    [Tooltip("게임이 끝나면 플레이어 조작을 잠근다")]
    [SerializeField] private bool lockControlsAtEnd = true;

    private MatchState state = MatchState.Waiting;
    private double startTime;   // 진행이 시작되는 시각 (Time.timeAsDouble 기준)
    private double endTime;     // 게임이 끝나는 시각
    private bool initialized;
    private readonly List<int> winners = new List<int>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    public MatchState State => state;
    public bool IsRunning => state == MatchState.Running;
    public bool IsGameOver => state == MatchState.Ended;

    // 생성할 플레이어 수 = 시작 위치 개수 (최대 4명)
    private int PlayerCount => spawnPoints == null ? 0 : Mathf.Min(spawnPoints.Length, TF_TileGridManager.MaxPlayers);

    /// <summary>지금 생성되어 있는 플레이어들 (플레이어 번호 순서)</summary>
    public IReadOnlyList<GameObject> Players => spawned;

    /// <summary>카운트다운이 끝나고 진행이 시작될 때 한 번 호출된다</summary>
    public event Action GameStarted;
    /// <summary>
    /// 게임이 끝났을 때 한 번 호출된다. 인자는 승자 플레이어 번호 목록.
    /// 1명이면 단독 승리, 2명 이상이면 공동 승리(무승부), 비어 있으면 아무도 칠하지 못한 것.
    /// </summary>
    public event Action<IReadOnlyList<int>> GameOver;

    /// <summary>가장 최근 게임의 승자 번호 목록 (진행 중에는 비어 있다)</summary>
    public IReadOnlyList<int> Winners => winners;

    /// <summary>카운트다운이 끝나기까지 남은 시간(초)</summary>
    public float CountdownRemaining => state == MatchState.Countdown ? Mathf.Max(0f, (float)(startTime - Now)) : 0f;

    /// <summary>게임이 끝나기까지 남은 시간(초). 진행 전에는 전체 게임 시간, 종료 후에는 0</summary>
    public float RemainingTime
    {
        get
        {
            switch (state)
            {
                case MatchState.Running: return Mathf.Max(0f, (float)(endTime - Now));
                case MatchState.Ended: return 0f;
                default: return gameDuration;
            }
        }
    }

    private static double Now => Time.timeAsDouble;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<TF_TileGridManager>();
        if (tracker == null) tracker = FindObjectOfType<TF_TilePaintTracker>();
        if (cameraFollow == null) cameraFollow = FindObjectOfType<TF_CameraFollow>(); // 선택 사항

        if (grid == null) Debug.LogError("[TF_GameManager] 씬에서 TF_TileGridManager를 찾지 못했습니다.");
        if (tracker == null) Debug.LogError("[TF_GameManager] 씬에서 TF_TilePaintTracker를 찾지 못했습니다.");
    }

    private void Update()
    {
        // 모든 Start()가 끝난 뒤에 시작하기 위해 첫 Update에서 시작한다
        if (!initialized)
        {
            initialized = true;

            // 플레이어를 먼저 생성하고 등록한 뒤에 시작한다. 생성에 실패하면 시작하지 않는다
            if (!SpawnPlayers()) return;

            if (autoStart) BeginMatch();
            return;
        }

        switch (state)
        {
            case MatchState.Countdown:
                if (Now >= startTime) StartRunning();
                break;

            case MatchState.Running:
                if (Now >= endTime) EndGame();
                break;
        }
    }

    // ---------------------------------------------------------------- 플레이어 생성

    /// <summary>
    /// 기존 플레이어를 지우고, 시작 위치를 새로 섞어서 플레이어를 생성한다.
    /// (멀티에서는 Host가 접속한 참가자 수만큼 SpawnAsPlayerObject로 생성하는 형태로 바뀐다)
    /// </summary>
    /// <returns>성공하면 true, 설정이 잘못되어 생성하지 못하면 false</returns>
    private bool SpawnPlayers()
    {
        ClearPlayers();

        if (!ValidateSpawnSettings(out string error))
        {
            Debug.LogError("[TF 입장] " + error);
            return false;
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

        for (int player = 0; player < PlayerCount; player++)
        {
            Transform point = spawnPoints[order[player]];
            int character = player % characterPrefabs.Length;

            GameObject instance = Instantiate(characterPrefabs[character], point.position, point.rotation);
            spawned.Add(instance);

            if (tracker != null) tracker.Register(instance.transform, player);

            // 타일 색을 캐릭터 프리팹 번호와 맞춘다
            if (grid != null) grid.SetPlayerColorIndex(player, character);

            Debug.Log($"[TF 입장] 플레이어 {player}: 캐릭터 {character}, 시작 위치 {order[player]} 생성 완료");
        }

        // 카메라가 내 플레이어를 따라가게 한다. 오프라인에서는 0번 플레이어가 "나"이다.
        // (멀티에서는 각 컴퓨터가 자기 접속의 PlayerObject를 지정한다. SD_GameManager.HookLocalPlayer와 같은 방식)
        if (cameraFollow != null && spawned.Count > 0)
            cameraFollow.SetTarget(spawned[0].transform);

        return true;
    }

    private void ClearPlayers()
    {
        foreach (GameObject player in spawned)
        {
            if (player == null) continue;

            if (tracker != null) tracker.Unregister(player.transform);
            Destroy(player);
        }
        spawned.Clear();
    }

    private bool ValidateSpawnSettings(out string error)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            error = "characterPrefabs가 비어 있습니다.";
            return false;
        }

        foreach (GameObject prefab in characterPrefabs)
        {
            if (prefab == null)
            {
                error = "characterPrefabs에 비어 있는 칸이 있습니다.";
                return false;
            }
        }

        if (spawnPoints == null || spawnPoints.Length < 1)
        {
            error = "spawnPoints가 비어 있습니다. 시작 위치가 최소 1개 필요하고, 개수만큼(최대 4명) 플레이어가 생성됩니다.";
            return false;
        }

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
            {
                error = "spawnPoints에 비어 있는 칸이 있습니다.";
                return false;
            }
        }

        error = null;
        return true;
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

    // ---------------------------------------------------------------- 진행

    /// <summary>
    /// 새 게임을 시작한다. 타일과 점수를 초기화하고 카운트다운에 들어간다.
    /// (멀티 전환 시: 모두 준비된 뒤 Host가 공통 시작 시각을 정해서 호출하는 형태로 바뀐다)
    /// </summary>
    public void BeginMatch()
    {
        if (grid == null || tracker == null) return;

        winners.Clear();
        grid.ResetBoard();
        tracker.StopPainting();
        tracker.ResetTracking();
        SetPlayersControl(!lockControlsBeforeStart);

        startTime = Now + startDelay;
        endTime = startTime + gameDuration;
        state = MatchState.Countdown;

        Debug.Log("[TF] 카운트다운 시작");
    }

    private void StartRunning()
    {
        state = MatchState.Running;
        SetPlayersControl(true);
        tracker.StartPainting();
        GameStarted?.Invoke();

        Debug.Log("[TF] 게임 시작");
    }

    private void EndGame()
    {
        state = MatchState.Ended;
        tracker.StopPainting();
        if (lockControlsAtEnd) SetPlayersControl(false);

        DecideWinners();
        GameOver?.Invoke(winners);

        string scoreText = "";
        for (int p = 0; p < TF_TileGridManager.MaxPlayers; p++)
            scoreText += $"P{p + 1}={grid.GetScore(p)} ";

        Debug.Log($"[TF] 게임 종료. 점수: {scoreText}/ 승자(번호 0~3): {(winners.Count == 0 ? "없음" : string.Join(", ", winners))}");
    }

    /// <summary>다시 시작한다. (멀티에서는 씬을 다시 불러오는 방식으로 바뀐다)</summary>
    [ContextMenu("Restart Game")]
    public void RestartGame()
    {
        // 시작 위치를 새로 섞어서 플레이어를 다시 생성한다
        if (!SpawnPlayers()) return;

        BeginMatch();
    }

    // 가장 많은 타일을 차지한 플레이어. 동점이면 모두 승자(공동 승리). 아무도 못 칠했으면 승자 없음
    private void DecideWinners()
    {
        winners.Clear();

        int max = 0;
        for (int p = 0; p < TF_TileGridManager.MaxPlayers; p++)
            max = Mathf.Max(max, grid.GetScore(p));

        if (max <= 0) return;

        for (int p = 0; p < TF_TileGridManager.MaxPlayers; p++)
        {
            if (grid.GetScore(p) == max) winners.Add(p);
        }
    }

    // 등록된 모든 플레이어의 이동/밀치기 조작을 켜고 끈다 (SD_PlayerDeath.SetControlsEnabled와 같은 방식)
    private void SetPlayersControl(bool controlsEnabled)
    {
        if (tracker == null) return;

        tracker.ForEachPlayer((playerTransform, playerIndex) =>
        {
            PlayerMovement movement = playerTransform.GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = controlsEnabled;

            PlayerInteraction interaction = playerTransform.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.enabled = controlsEnabled;
        });
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