using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using CrosswalkRun;

public class RankingUI : MonoBehaviour
{
    public TextMeshProUGUI[] rankTexts;
    public Button actionButton;
    public TextMeshProUGUI actionButtonText;
    [SerializeField] private string nextMapSceneName = "CrosswalkRun";

    private void Start()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        if (actionButton != null) actionButton.gameObject.SetActive(isHost);

        if (RankingManager.Instance == null)
        {
            ShowQteResults();
            if (actionButtonText != null) actionButtonText.text = "Next Map";
            return;
        }

        List<PlayerScoreData> sortedScores = new List<PlayerScoreData>();
        foreach (var data in RankingManager.Instance.ScoreList) sortedScores.Add(data);
        sortedScores.Sort((a, b) => b.Score.CompareTo(a.Score));

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] == null) continue;
            rankTexts[i].text = i < sortedScores.Count
                ? $"{i + 1}. {sortedScores[i].PlayerName} ({sortedScores[i].Score} pts)"
                : $"{i + 1}. -";
        }

        int round = RankingManager.Instance.CurrentRound.Value;
        if (actionButtonText != null)
            actionButtonText.text = round < 4 ? $"Next game ({round}/4)" : "Return to title";
    }

    private void ShowQteResults()
    {
        int count = PlayerPrefs.GetInt("QteRankCount", 0);
        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] == null) continue;
            if (i < count)
            {
                string playerName = PlayerPrefs.GetString($"QteRankName{i + 1}", $"Player {i + 1}");
                int score = PlayerPrefs.GetInt($"QteRankScore{i + 1}", 0);
                rankTexts[i].text = $"{i + 1}. {playerName} ({score} pts)";
            }
            else
            {
                rankTexts[i].text = $"{i + 1}. -";
            }
        }
    }

    public void OnActionButtonClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (RankingManager.Instance == null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(nextMapSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            return;
        }

        int round = RankingManager.Instance.CurrentRound.Value;
        if (round < 4)
        {
            string nextScene = RankingManager.Instance.MiniGameScenes[round];
            RankingManager.Instance.CurrentRound.Value++;
            NetworkManager.Singleton.SceneManager.LoadScene(nextScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            RankingManager.Instance.ScoreList.Clear();
            RankingManager.Instance.CurrentRound.Value = 1;
            NetworkManager.Singleton.SceneManager.LoadScene(RankingManager.Instance.TitleSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}