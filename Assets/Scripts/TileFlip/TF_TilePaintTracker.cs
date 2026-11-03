using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내 플레이어가 타일에 닿으면 TF_GameManager에 보고한다.
/// 플레이어 오브젝트(공유 캐릭터 프리팹)에는 아무 스크립트도 붙이지 않는다.
/// 
/// 이 스크립트는 네트워크를 모르는 일반 MonoBehaviour이다. (SD_BladeSpawner와 같은 위치)
///  - 타일(TF_Tile)이 충돌체에 닿으면 OnTileTouched로 알려 준다. (SD_Blade가 SD_GameManager.Instance를 부르는 것과 같은 방식)
///  - 멀티에서는 각 컴퓨터가 "내 플레이어" 하나만 등록한다. 모든 컴퓨터에서 모든 플레이어의 충돌이 일어나지만,
///    등록된 내 플레이어의 몸이 아니면 무시하므로 남의 충돌은 보고하지 않는다.
///  - 칠하기를 직접 적용하지 않고 TF_GameManager.ReportPaint로 보고한다.
///    (예측 표시, 서버 확정, 다른 컴퓨터로의 전달은 TF_GameManager가 한다)
/// 
/// 플레이어는 TF_GameManager가 Register(transform, 번호)로 등록해 준다.
/// 이 스크립트에는 플레이어를 직접 넣는 칸이 없다. (플레이어 등록은 TF_GameManager 한 곳에서만 한다)
/// 
/// 동작:
///  1) 타일에 몸이 닿으면(트리거에 들어오면) 그 타일을 보고한다. 점프로 공중에 떠 있는 동안은 타일 콜라이더에 닿지 않는다.
///  2) 밀쳐지는 동안(PlayerMovement.isKnockedBack)에 닿은 타일은 칠하지 않는다.
///     다시 움직일 수 있게 되는 순간(밀침이 끝나는 순간), 그때 몸이 닿아 있는 타일부터 다시 칠한다.
///  3) 칠하기는 StartPainting() ~ StopPainting() 사이에만 동작한다. (TF_GameManager가 시작/종료 시점을 정한다)
///  4) 칠하기가 시작되는 순간에도 이미 서 있는 타일은 "닿는 알림"이 없으므로, 그때 몸이 닿아 있는 타일을 한 번 칠한다.
/// 
/// 사용법: 씬에 하나만 둔다. TF_TileGridManager는 씬에서 자동으로 찾는다.
/// </summary>
[DisallowMultipleComponent]
public class TF_TilePaintTracker : MonoBehaviour
{
    public static TF_TilePaintTracker Instance { get; private set; }

    private class Tracked
    {
        public Transform target;
        public int playerIndex;
        public PlayerMovement movement; // 밀쳐지는 중인지(isKnockedBack) 확인하기 위해 들고 있는다
        public Collider body;           // 지금 몸이 닿아 있는 타일을 찾을 때 쓴다
        public bool wasKnockedBack;     // 직전 프레임에 밀쳐지는 중이었는지 (밀침이 끝나는 순간을 알기 위해)
    }

    [Header("참조")]
    [Tooltip("비워 두면 씬에서 자동으로 찾는다")]
    [SerializeField] private TF_TileGridManager grid;

    private readonly List<Tracked> tracked = new List<Tracked>();
    private readonly Collider[] overlapBuffer = new Collider[32];
    private bool isPainting;

    /// <summary>지금 칠하기가 동작 중인지</summary>
    public bool IsPainting => isPainting;

    private void Awake()
    {
        Instance = this;

        if (grid == null)
            grid = FindObjectOfType<TF_TileGridManager>();

        if (grid == null)
            Debug.LogError("[TF_TilePaintTracker] 씬에서 TF_TileGridManager를 찾지 못했습니다.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────────────────────── 진행 제어 ─────────────────────────

    /// <summary>칠하기를 시작한다. 이미 타일 위에 서 있는 플레이어는 그 타일을 칠한다.</summary>
    public void StartPainting()
    {
        isPainting = true;

        foreach (Tracked t in tracked)
        {
            if (t.target == null) continue;

            t.wasKnockedBack = t.movement != null && t.movement.isKnockedBack;
            if (!t.wasKnockedBack) PaintTilesUnder(t);
        }
    }

    /// <summary>칠하기를 멈춘다. (타이머 종료 후 점수가 더 이상 바뀌지 않게 할 때)</summary>
    public void StopPainting()
    {
        isPainting = false;
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
                return;
            }
        }

        tracked.Add(new Tracked
        {
            target = target,
            playerIndex = playerIndex,
            movement = target.GetComponent<PlayerMovement>(),
            body = target.GetComponent<Collider>()
        });
    }

    // ───────────────────────── 밀침이 끝나는 순간 ─────────────────────────

    private void Update()
    {
        foreach (Tracked t in tracked)
        {
            if (t.target == null || t.movement == null) continue;

            bool knocked = t.movement.isKnockedBack;

            // 밀침이 끝나 다시 움직일 수 있게 된 순간: 지금 몸이 닿아 있는 타일부터 칠한다
            if (t.wasKnockedBack && !knocked && isPainting)
                PaintTilesUnder(t);

            t.wasKnockedBack = knocked;
        }
    }

    // ───────────────────────── 충돌 ─────────────────────────

    /// <summary>타일(TF_Tile)이 "무언가 닿았다"고 알려 온다. 닿은 것이 등록된 내 플레이어의 몸일 때만 보고한다.</summary>
    public void OnTileTouched(TF_Tile tile, Collider other)
    {
        if (!isPainting || grid == null || tile == null || other == null) return;

        foreach (Tracked t in tracked)
        {
            if (t.target == null || !other.transform.IsChildOf(t.target)) continue;

            // 밀쳐지는 동안 닿은 타일은 칠하지 않는다
            if (t.movement != null && t.movement.isKnockedBack) return;

            ReportTile(tile, t.playerIndex);
            return;
        }
    }

    // 지금 플레이어의 몸이 닿아 있는 타일들을 모두 칠한다. (공중에 떠 있으면 닿은 타일이 없으므로 칠하지 않는다)
    private void PaintTilesUnder(Tracked t)
    {
        if (grid == null) return;

        // 몸 콜라이더를 찾지 못하면 위치(중심점)로 타일을 찾는다
        if (t.body == null)
        {
            if (grid.TryGetTileIndex(t.target.position, out int index))
                ReportPaint(index, t.playerIndex);
            return;
        }

        // 몸 주변의 충돌체 중 타일을 모으고, 실제로 몸과 겹치는 타일만 칠한다
        Bounds bounds = t.body.bounds;
        int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, overlapBuffer, Quaternion.identity,
                                               ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];
            TF_Tile tile = other.GetComponent<TF_Tile>();
            if (tile == null) continue;

            if (!Physics.ComputePenetration(t.body, t.body.transform.position, t.body.transform.rotation,
                                            other, other.transform.position, other.transform.rotation,
                                            out _, out _))
                continue;

            ReportTile(tile, t.playerIndex);
        }
    }

    // 타일은 칸의 중앙에 놓여 있으므로 타일의 위치로 번호를 구해 보고한다
    private void ReportTile(TF_Tile tile, int playerIndex)
    {
        if (grid.TryGetTileIndex(tile.transform.position, out int tileIndex))
            ReportPaint(tileIndex, playerIndex);
    }

    // 게임 매니저에 "내 플레이어가 이 타일에 닿았다"고 보고한다. 예측 표시와 칠하기 확정은 게임 매니저가 한다
    private void ReportPaint(int tileIndex, int playerIndex)
    {
        if (TF_GameManager.Instance != null)
            TF_GameManager.Instance.ReportPaint(tileIndex, playerIndex);
    }
}