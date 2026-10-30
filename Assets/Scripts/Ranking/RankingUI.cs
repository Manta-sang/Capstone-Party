using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using CrosswalkRun;

public class RankingUI : MonoBehaviour
{
    [Header("순위 텍스트 UI (1등~4등)")]
    public TextMeshProUGUI[] rankTexts;

    [Header("버튼 UI 연결")]
    public Button actionButton; // 버튼 컴포넌트
    public TextMeshProUGUI actionButtonText; // 버튼 안의 텍스트 컴포넌트

    void Start()
    {
        if (RankingManager.Instance == null) return;

        // 1. 점수 정렬 및 텍스트 표시
        List<PlayerScoreData> sortedScores = new List<PlayerScoreData>();
        foreach (var data in RankingManager.Instance.ScoreList)
        {
            sortedScores.Add(data);
        }
        sortedScores.Sort((a, b) => b.Score.CompareTo(a.Score));

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (i < sortedScores.Count)
            {
                rankTexts[i].text = $"{i + 1}등: {sortedScores[i].PlayerName} ({sortedScores[i].Score}점)";
            }
            else
            {
                rankTexts[i].text = $"{i + 1}등: -";
            }
        }

        // 2. ★ 라운드에 맞춰 버튼 글자 바꾸기
        int round = RankingManager.Instance.CurrentRound.Value;
        if (round < 4)
        {
            actionButtonText.text = $"다음 게임으로 ({round}/4)";
        }
        else
        {
            actionButtonText.text = "게임 타이틀로 가기";
        }

        // 3. 클라이언트(참가자)는 버튼을 조작할 수 없도록 숨기기 (방장만 조작 가능)
        if (!NetworkManager.Singleton.IsServer)
        {
            actionButton.gameObject.SetActive(false);
        }
    }

    // ★ 인스펙터의 버튼 OnClick에 연결할 함수
    public void OnActionButtonClicked()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        int round = RankingManager.Instance.CurrentRound.Value;

        if (round < 4)
        {
            // 다음 미니게임 씬으로 이동 (배열 인덱스는 0부터 시작하므로 round값을 바로 넣으면 다음 게임이 됨)
            string nextScene = RankingManager.Instance.MiniGameScenes[round];
            RankingManager.Instance.CurrentRound.Value++; // 라운드 증가
            NetworkManager.Singleton.SceneManager.LoadScene(nextScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            // 4라운드 종료: 점수판과 라운드 초기화 후 타이틀 씬으로 이동
            RankingManager.Instance.ScoreList.Clear();
            RankingManager.Instance.CurrentRound.Value = 1;
            NetworkManager.Singleton.SceneManager.LoadScene(RankingManager.Instance.TitleSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}