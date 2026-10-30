using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 등록된 모든 플레이어의 위치를 감시해서, 새 타일에 들어오면 TF_GameManager에 보고한다.
/// 플레이어 오브젝트(공유 캐릭터 프리팹)에는 아무 스크립트도 붙이지 않는다.
/// 
/// 이 스크립트는 네트워크를 모르는 일반 MonoBehaviour이다. (SD_BladeSpawner와 같은 위치)
///  - 판정은 Host(서버)에서만 한다. 서버가 아닌 컴퓨터에서는 아무것도 하지 않는다.
///    (SD_BladeSpawner의 CanSpawn과 같은 방식. 네트워크가 없는 단독 실행에서는 그대로 동작한다)
///  - 칠하기를 직접 적용하지 않고 TF_GameManager.ReportPaint로 보고한다.
///    SD_Blade가 플레이어에 닿으면 SD_GameManager.ReportHit으로 보고하는 것과 같다.
///    (확정과 다른 컴퓨터로의 전달은 TF_GameManager가 한다)
/// 
/// 플레이어는 TF_GameManager가 생성하면서 Register(transform, 번호)로 등록해 준다.
/// 이 스크립트에는 플레이어를 직접 넣는 칸이 없다. (플레이어 등록은 TF_GameManager 한 곳에서만 한다)
/// 
/// 동작:
///  1) 매 프레임 등록된 플레이어마다 월드 위치(x, z)로 "어느 타일 위인지"를 계산한다.
///  2) 직전 프레임과 다른 타일로 들어왔을 때만 보고한다.
///  3) 격자 밖으로 나가면 그 플레이어의 기록을 지운다.
///  4) 칠하기는 StartPainting() ~ StopPainting() 사이에만 동작한다. (TF_GameManager가 시작/종료 시점을 정한다)
/// 
/// 사용법: 씬에 하나만 둔다. TF_TileGridManager는 씬에서 자동으로 찾는다.
/// </summary>
[DisallowMultipleComponent]
public class TF_TilePaintTracker : MonoBehaviour
{
    private class Tracked
    {
        public Transform target;
        public int playerIndex;
        public int lastTileIndex = -1;
    }

    [Header("참조")]
    [Tooltip("비워 두면 씬에서 자동으로 찾는다")]
    [SerializeField] private TF_TileGridManager grid;

    private readonly List<Tracked> tracked = new List<Tracked>();
    private bool isPainting;

    // 판정은 Host(서버)에서만 한다.
    // 씬에 NetworkManager가 없으면(네트워크를 쓰지 않는 단독 실행) 그대로 동작한다
    private bool RunsLogic
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager == null || manager.IsServer;
        }
    }

    /// <summary>지금 칠하기가 동작 중인지</summary>
    public bool IsPainting => isPainting;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<TF_TileGridManager>();

        if (grid == null)
            Debug.LogError("[TF_TilePaintTracker] 씬에서 TF_TileGridManager를 찾지 못했습니다.");
    }

    // ───────────────────────── 진행 제어 ─────────────────────────

    /// <summary>칠하기를 시작한다. 모든 플레이어의 "마지막 타일" 기록도 함께 지운다.</summary>
    public void StartPainting()
    {
        ResetTracking();
        isPainting = true;
    }

    /// <summary>칠하기를 멈춘다. (타이머 종료 후 점수가 더 이상 바뀌지 않게 할 때)</summary>
    public void StopPainting()
    {
        isPainting = false;
    }

    /// <summary>모든 플레이어의 "마지막 타일" 기록을 지운다.</summary>
    public void ResetTracking()
    {
        foreach (Tracked t in tracked)
            t.lastTileIndex = -1;
    }

    // ───────────────────────── 등록 ─────────────────────────

    /// <summary>플레이어를 등록한다. 이미 등록된 Transform이면 번호만 바꾼다. 게임 도중에 불러도 된다.</summary>
    public void Register(Transform target, int playerIndex)
    {
        if (target == null) return;
        if (playerIndex < 0 || playerIndex >= TF_TileGridManager.MaxPlayers)
        {
            Debug.LogWarning($"[TF_TilePaintTracker] 잘못된 플레이어 번호: {playerIndex}");
            return;
        }

        foreach (Tracked t in tracked)
        {
            if (t.target == target)
            {
                t.playerIndex = playerIndex;
                t.lastTileIndex = -1;
                return;
            }
        }

        tracked.Add(new Tracked { target = target, playerIndex = playerIndex });
    }

    public void Unregister(Transform target)
    {
        tracked.RemoveAll(t => t.target == target);
    }

    /// <summary>등록된 모든 플레이어를 (Transform, 플레이어 번호)로 한 번씩 돌려준다.</summary>
    public void ForEachPlayer(Action<Transform, int> action)
    {
        if (action == null) return;

        foreach (Tracked t in tracked)
        {
            if (t.target != null) action(t.target, t.playerIndex);
        }
    }

    // ───────────────────────── 감시 ─────────────────────────

    private void Update()
    {
        if (!RunsLogic) return; // 판정은 Host에서만. 다른 컴퓨터는 Host가 알려주는 결과만 받는다
        if (!isPainting || grid == null) return;

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            Tracked t = tracked[i];

            // 플레이어 오브젝트가 사라졌으면(나갔거나 제거됨) 목록에서 제거. 이미 칠한 타일은 그대로 남는다.
            if (t.target == null)
            {
                tracked.RemoveAt(i);
                continue;
            }

            // 격자 밖이면 기록을 지우고 다음 플레이어로
            if (!grid.TryGetTileIndex(t.target.position, out int tileIndex))
            {
                t.lastTileIndex = -1;
                continue;
            }

            // 같은 타일 위에 계속 서 있으면 아무것도 하지 않음
            if (tileIndex == t.lastTileIndex) continue;

            t.lastTileIndex = tileIndex;
            ReportPaint(tileIndex, t.playerIndex);
        }
    }

    // 게임 매니저에 "이 플레이어가 이 타일에 들어왔다"고 보고한다. 칠하기 확정은 게임 매니저가 한다
    private void ReportPaint(int tileIndex, int playerIndex)
    {
        if (TF_GameManager.Instance != null)
            TF_GameManager.Instance.ReportPaint(tileIndex, playerIndex);
    }
}