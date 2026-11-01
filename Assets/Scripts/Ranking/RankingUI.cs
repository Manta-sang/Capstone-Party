using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using CrosswalkRun;

// 알파 테스트: 서버에서 누적된 점수를 표시하고 호스트 버튼으로 같은 방의 로비에 돌아갑니다.
public class RankingUI : MonoBehaviour
{
    [Header("순위 텍스트 UI (1등~4등)")]
    public TextMeshProUGUI[] rankTexts;
    [Header("버튼 UI 연결")]
    public UnityEngine.UI.Button actionButton;
    public TextMeshProUGUI actionButtonText;
    [Header("랭킹 확인 후 이동")]
    [SerializeField] private string returnSceneName = "Character";
    [SerializeField] private string returnButtonLabel = "캐릭터 로비로 돌아가기";
    private RankingManager ranking;
    private NetworkManager network;
    private bool sceneReady;
    private bool loading;

    // 모든 참가자의 랭킹 씬 로딩이 끝나기 전에 다음 씬을 요청하지 않습니다.
    private void Awake()
    {
        network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
            network.SceneManager.OnLoadEventCompleted += OnLoadCompleted;
    }

    private void Start()
    {
        if (actionButtonText != null) actionButtonText.text = returnButtonLabel;
        RefreshScores();
    }

    private void OnLoadCompleted(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (scene != gameObject.scene.name || network == null || !network.IsServer) return;
        sceneReady = timedOut.Count == 0;
        foreach (ulong id in network.ConnectedClientsIds)
            if (!completed.Contains(id)) sceneReady = false;
    }

    // 점수 동기화가 UI Start보다 늦어도 연결하고, 이후 변경은 이벤트로 갱신합니다.
    private void Update()
    {
        var current = RankingManager.Instance;
        if (ranking != current)
        {
            if (ranking != null && ranking.ScoreList != null) ranking.ScoreList.OnListChanged -= OnScoreChanged;
            ranking = current;
            if (ranking != null && ranking.ScoreList != null) ranking.ScoreList.OnListChanged += OnScoreChanged;
            RefreshScores();
        }
        if (actionButton != null)
            actionButton.interactable = network != null && network.IsListening && network.IsServer && sceneReady && !loading;
    }

    private void OnScoreChanged(NetworkListEvent<PlayerScoreData> change) => RefreshScores();

    // 각 게임의 호스트가 기록한 누적 점수를 내림차순으로 표시합니다. 로비 복귀 시 초기화하지 않습니다.
    private void RefreshScores()
    {
        var scores = new List<PlayerScoreData>();
        if (ranking != null && ranking.ScoreList != null)
            foreach (var entry in ranking.ScoreList) scores.Add(entry);
        scores.Sort((a, b) => { int order = b.Score.CompareTo(a.Score); return order != 0 ? order : a.ClientId.CompareTo(b.ClientId); });
        if (rankTexts == null) return;
        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] == null) continue;
            int rank = i + 1;
            while (rank > 1 && i < scores.Count && scores[rank - 2].Score == scores[i].Score) rank--;
            rankTexts[i].text = i < scores.Count ? $"{rank}등: {scores[i].PlayerName} ({scores[i].Score}점)" : $"{i + 1}등: -";
        }
    }

    // Inspector의 On Click에서 연결합니다. 목적지는 이 컴포넌트의 Return Scene Name으로 지정합니다.
    public void OnActionButtonClicked()
    {
        if (network == null || !network.IsListening || !network.IsServer || !sceneReady || loading) return;
        if (string.IsNullOrWhiteSpace(returnSceneName) || !Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            Debug.LogError("[Ranking 이동] Build Settings에서 이동할 씬을 확인해 주세요: " + returnSceneName);
            return;
        }
        loading = true;
        if (actionButton != null) actionButton.interactable = false;
        var status = network.SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            loading = false;
            Debug.LogError("[Ranking 이동] 씬 이동 실패: " + status);
        }
    }

    private void OnDestroy()
    {
        if (ranking != null && ranking.ScoreList != null) ranking.ScoreList.OnListChanged -= OnScoreChanged;
        if (network != null && network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= OnLoadCompleted;
    }
}
