using System;
using System.Collections.Generic;
using UnityEngine;

public class SD_GameManager : MonoBehaviour
{
    [Header("참조 (선택)")]
    [Tooltip("연결하면 승자가 정해지는 순간 톱날 생성을 멈춘다")]
    [SerializeField] private SD_BladeSpawner spawner;

    private readonly List<SD_PlayerLife> players = new List<SD_PlayerLife>();
    private readonly List<SD_PlayerLife> alivePlayers = new List<SD_PlayerLife>();

    public bool IsGameOver { get; private set; }
    public SD_PlayerLife Winner { get; private set; }
    public int AliveCount => alivePlayers.Count;

    /// <summary>승자가 정해졌을 때 한 번 호출된다 (UI나 결과 처리를 연결할 때 사용)</summary>
    public event Action<SD_PlayerLife> GameOver;

    private void Start()
    {
        // 게임 시작 시 초기화 및 플레이어 등록 진행
        InitGame();
    }

    /// <summary>
    /// 게임 상태, 플레이어 생존 상태, 톱날 스포너를 이전 결과 없이 깨끗하게 초기화합니다.
    /// 미니게임을 재시작할 때 이 함수를 호출하세요.
    /// </summary>
    public void InitGame()
    {
        IsGameOver = false;
        Winner = null;

        players.Clear();
        alivePlayers.Clear();

        // 씬 내의 모든 플레이어를 등록하고 생존 상태 및 위치를 초기화
        foreach (SD_PlayerLife player in FindObjectsOfType<SD_PlayerLife>())
        {
            player.ResetLife(); // 플레이어 상태 복구 (먼저 복구해야 생존자로 등록된다)
            Register(player);
        }

        // 남아있는 톱날 제거 및 스포너 재시작
        if (spawner != null)
        {
            spawner.ResetSpawner();
        }
    }

    /// <summary>플레이어를 게임에 등록한다. 나중에 생성되는 플레이어는 이 함수로 등록</summary>
    public void Register(SD_PlayerLife player)
    {
        if (player == null || players.Contains(player)) return;

        // 이벤트 중복 등록 방지
        player.Died -= OnPlayerDied;
        player.Died += OnPlayerDied;

        players.Add(player);

        if (!player.IsDead)
        {
            alivePlayers.Add(player);
        }
    }

    private void OnPlayerDied(SD_PlayerLife player)
    {
        if (IsGameOver) return;

        alivePlayers.Remove(player);

        // 혼자 테스트할 때(등록된 플레이어가 1명)는 승패를 가리지 않는다
        if (players.Count < 2) return;

        if (alivePlayers.Count == 1)
        {
            EndGame(alivePlayers[0]);
        }
    }

    private void EndGame(SD_PlayerLife winner)
    {
        IsGameOver = true;
        Winner = winner;
        Debug.Log($"[SD_] 승리: {winner.name}");

        // 1. 새로운 톱날 스폰 중단
        if (spawner != null) spawner.StopSpawning();

        // 2. 현재 경기장 내 톱날들을 즉시 제자리에 정지
        SD_Blade.FreezeAll();

        GameOver?.Invoke(winner);
    }

    private void OnDestroy()
    {
        foreach (SD_PlayerLife player in players)
        {
            if (player != null) player.Died -= OnPlayerDied;
        }
    }
}