using System.Collections.Generic;
using CrosswalkRun;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FinalRanking : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    public Transform[] displaySpots;
    public GameObject crownPrefab;
    public TMP_Text winnerText, rankingText, returnText;
    public TMP_Text[] scoreRows;
    public TMP_Text[] scoreValues;
    [Min(1)] public float celebrationSeconds = 15;
    public string returnScene = "Character";
    public Vector2 walkableX = new Vector2(-8.5f, 7.2f), walkableZ = new Vector2(-3, 5.5f);
    NetworkManager network;
    RankingManager ranking;
    bool leaving, bound;
    int lastSecond = int.MinValue;
    float nextBind;
    readonly List<PlayerScoreData> scores = new List<PlayerScoreData>();
    readonly Dictionary<ulong, NetworkObject> players = new Dictionary<ulong, NetworkObject>();
    readonly HashSet<ulong> crowned = new HashSet<ulong>();
    void Awake()
    {
        network = NetworkManager.Singleton;
        if (network != null && network.IsServer && PartyRoster.Current != null) PartyRoster.Current.FinalReturnAt.Value = -1;
        if (network != null && network.IsListening) network.SceneManager.OnLoadEventCompleted += Loaded;
    }
    void Loaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (scene != gameObject.scene.name || !network.IsServer) return;
        foreach (var id in network.ConnectedClientsIds) if (!completed.Contains(id)) return;
        RefreshScores();
        int slot = 0;
        foreach (var entry in scores)
        {
            if (!network.ConnectedClients.ContainsKey(entry.ClientId)) continue;
            Spawn(entry.ClientId, slot++);
        }
        foreach (var id in network.ConnectedClientsIds)
            if (network.SpawnManager.GetPlayerNetworkObject(id) == null) Spawn(id, slot++);
        PartyRoster.Current.FinalReturnAt.Value = network.ServerTime.Time + celebrationSeconds;
    }
    void Spawn(ulong id, int slot)
    {
        var old = network.SpawnManager.GetPlayerNetworkObject(id);
        if (old != null) old.Despawn(true);
        GetPresentationPose(id, slot, out var position, out var rotation);
        var avatar = Instantiate(characterPrefabs[(int)(id % (ulong)characterPrefabs.Length)], position, rotation);
        avatar.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
    }
    void Update()
    {
        if (!bound || ranking != RankingManager.Instance)
        {
            if (ranking != null) ranking.ScoreList.OnListChanged -= ScoreChanged;
            ranking = RankingManager.Instance; bound=ranking!=null;
            if (ranking != null) { ranking.ScoreList.OnListChanged += ScoreChanged; RefreshScores(); }
        }
        if (Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + .2f; BindPlayers(); }
        double returnAt = PartyRoster.Current != null ? PartyRoster.Current.FinalReturnAt.Value : -1;
        double remaining = network != null && returnAt > 0 ? returnAt - network.ServerTime.Time : -1;
        int seconds = returnAt < 0 ? -1 : Mathf.Max(0, Mathf.CeilToInt((float)remaining));
        if (seconds != lastSecond)
        {
            lastSecond = seconds;
            string hint=network!=null&&IsWinner(network.LocalClientId)?"WASD 이동 / SPACE 점프":"우승을 축하해 주세요";
            returnText.text = seconds < 0 ? "모두의 도착을 기다리는 중..." : $"{seconds}초 후 캐릭터 로비로 돌아갑니다  ·  {hint}";
        }
        if (network == null || !network.IsServer || returnAt <= 0 || remaining > 0 || leaving) return;
        leaving = true;
        if (network.SceneManager.LoadScene(returnScene, LoadSceneMode.Single) != SceneEventProgressStatus.Started) { leaving = false; return; }
        PartyGameRotation.Reset(); PartyRoster.Current.ResetReady();
        if (ranking != null) ranking.ScoreList.Clear();
    }
    void BindPlayers()
    {
        if (network == null || !network.IsListening) return;
        players.Clear();
        foreach (var player in network.SpawnManager.SpawnedObjectsList)
        {
            if (!player.IsPlayerObject) continue;
            players[player.OwnerClientId] = player;
            // Winners remain controllable; the other contestants are placed in their scene-authored resting positions.
            var push = player.GetComponent<PlayerInteraction>(); if (push != null) push.enabled = false;
            var bounds = player.GetComponent<FinalCelebrationPlayer>();
            if (bounds == null) bounds = player.gameObject.AddComponent<FinalCelebrationPlayer>();
            bounds.walkableX = walkableX; bounds.walkableZ = walkableZ;
            GetPresentationPose(player.OwnerClientId, players.Count-1, out var position, out var rotation);
            bounds.Configure(IsWinner(player.OwnerClientId),position,rotation);
        }
        if (scores.Count == 0) return;
        foreach (var entry in scores)
        {
            if (entry.Score != scores[0].Score || crowned.Contains(entry.ClientId) || !players.TryGetValue(entry.ClientId, out var player)) continue;
            var crown = Instantiate(crownPrefab, player.transform);
            crown.transform.localPosition = new Vector3(0, 2.65f, 0); crown.SetActive(true);
            crowned.Add(entry.ClientId);
        }
    }
    public bool IsWinner(ulong id)
    {
        if (scores.Count == 0) return false;
        foreach (var entry in scores) if (entry.ClientId == id) return entry.Score == scores[0].Score;
        return false;
    }
    void GetPresentationPose(ulong id, int fallbackSlot, out Vector3 position, out Quaternion rotation)
    {
        int index = scores.FindIndex(s => s.ClientId == id);
        if (IsWinner(id))
        {
            int winners = 0; foreach (var entry in scores) if (entry.Score == scores[0].Score) winners++;
            position = displaySpots[0].position + Vector3.right * ((index - (winners-1)*.5f)*2.6f) + Vector3.up*.06f;
            rotation = displaySpots[0].rotation;
            return;
        }
        int slot = Mathf.Clamp(index >= 0 ? index : fallbackSlot,1,displaySpots.Length-1);
        var spot = displaySpots[slot];
        // Side fall, face-down and face-up, each with a distinct heading.
        rotation = spot.rotation * (slot==1?Quaternion.Euler(8,0,90):slot==2?Quaternion.Euler(90,18,12):Quaternion.Euler(-90,-23,-14));
        // The avatar's pivot is at its feet; offset by half its height to center the horizontal body on the marker.
        position = spot.position + Vector3.up*.53f - rotation*Vector3.up*1.1f;
    }
    void ScoreChanged(NetworkListEvent<PlayerScoreData> change) => RefreshScores();
    void RefreshScores()
    {
        if (ranking == null) ranking = RankingManager.Instance;
        if (ranking == null) return;
        scores.Clear(); foreach (var entry in ranking.ScoreList) scores.Add(entry);
        scores.Sort((a, b) => { int order = b.Score.CompareTo(a.Score); return order != 0 ? order : a.ClientId.CompareTo(b.ClientId); });
        string winners = ""; int rank = 1;
        if (rankingText != null) rankingText.text = "";
        for (int i = 0; i < scoreRows.Length; i++)
        {
            var row = scoreRows[i]; row.gameObject.SetActive(i < scores.Count);
            if (i >= scores.Count){if(scoreValues!=null&&i<scoreValues.Length&&scoreValues[i]!=null)scoreValues[i].gameObject.SetActive(false);continue;}
            var entry = scores[i]; if (i > 0 && entry.Score != scores[i - 1].Score) rank = i + 1;
            bool won = rank == 1;
            if (won) winners += (winners.Length > 0 ? " · " : "") + entry.PlayerName;
            row.fontSize = won ? 44 : 29;
            row.fontSizeMax = won ? 44 : 29; row.fontSizeMin = won ? 30 : 23; row.enableAutoSizing = true;
            row.color = won ? new Color(1, .81f, .32f) : new Color(.91f, .95f, 1);
            row.text = $"{rank}위  {entry.PlayerName}";
            if(scoreValues!=null&&i<scoreValues.Length&&scoreValues[i]!=null){var value=scoreValues[i];value.gameObject.SetActive(true);value.text=entry.Score+"점";value.color=row.color;value.fontSize=won?44:29;}
        }
        winnerText.text = scores.Count == 0 ? "최종 결과를 기다리는 중..." : winners + "\n최종 우승을 축하합니다!";
    }
    void OnDestroy()
    {
        if (ranking != null) ranking.ScoreList.OnListChanged -= ScoreChanged;
        if (network != null && network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= Loaded;
    }
}
