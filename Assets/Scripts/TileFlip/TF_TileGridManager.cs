using System;
using UnityEngine;

/// <summary>
/// 타일 격자를 생성하고, 타일 소유자/플레이어별 점수를 관리한다. (오프라인 버전)
/// 
/// 나중에 멀티로 바꿀 때를 대비해 데이터 변경은 SetTileOwner / ResetBoard 두 함수로만 하고,
/// 화면 갱신은 이벤트(OnTileOwnerChanged, OnScoreChanged)로만 알린다.
/// 
/// 사용법: 씬에 빈 오브젝트를 만들고 이 스크립트를 붙인다.
///         (회전은 (0,0,0), 스케일은 (1,1,1)로 둘 것)
/// </summary>
public class TF_TileGridManager : MonoBehaviour
{
    public const int MaxPlayers = 4;
    public const int NoOwner = -1;

    [Header("격자 설정")]
    [SerializeField] private TF_Tile tilePrefab;
    [SerializeField, Min(1)] private int columns = 10;
    [SerializeField, Min(1)] private int rows = 10;
    [SerializeField, Min(0.1f)] private float tileSize = 2f;
    [SerializeField, Min(0f)] private float tileGap = 0.05f;

    [Header("머티리얼")]
    [SerializeField] private Material neutralMaterial;                              // 아무도 안 밟은 타일 (흰색)
    [Tooltip("플레이어 색. TF_GameManager의 characterPrefabs와 같은 번호끼리 짝이다 (캐릭터 k번 = 타일 색 k번)")]
    [SerializeField] private Material[] playerMaterials = new Material[MaxPlayers];

    // 타일 인덱스 -> 소유자 번호 (-1이면 주인 없음)
    private int[] tileOwners;
    // 플레이어 번호 -> 점유한 타일 수
    private int[] scores;

    // 플레이어 번호 -> playerMaterials에서 쓸 색 번호. 기본은 같은 번호이고, 캐릭터가 정해지면 캐릭터 번호로 바뀐다.
    private int[] playerColorIndex;

    private TF_Tile[] tiles;

    /// <summary>타일 소유자가 바뀌었을 때 (타일 인덱스, 새 소유자)</summary>
    public event Action<int, int> OnTileOwnerChanged;
    /// <summary>플레이어 점수가 바뀌었을 때 (플레이어 번호, 새 점수)</summary>
    public event Action<int, int> OnScoreChanged;

    public int Columns => columns;
    public int Rows => rows;
    public int TileCount => columns * rows;

    /// <summary>playerMaterials에 들어 있는 색의 개수</summary>
    public int ColorCount => playerMaterials == null ? 0 : playerMaterials.Length;

    // ───────────────────────── 생성 ─────────────────────────

    private void Awake()
    {
        playerColorIndex = new int[MaxPlayers];
        for (int i = 0; i < MaxPlayers; i++) playerColorIndex[i] = i;

        BuildTiles();
        ResetBoard();
    }

    private Vector3 GetGridCorner()
    {
        // 매니저 위치를 격자의 중심으로 보고, 좌하단 모서리를 구한다.
        return transform.position - new Vector3(columns * tileSize * 0.5f, 0f, rows * tileSize * 0.5f);
    }

    private void BuildTiles()
    {
        if (tilePrefab == null)
        {
            Debug.LogError("[TF_TileGridManager] tilePrefab이 비어 있습니다.");
            return;
        }

        tiles = new TF_Tile[TileCount];
        Vector3 corner = GetGridCorner();
        Vector3 prefabScale = tilePrefab.transform.localScale;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int index = r * columns + c;
                Vector3 pos = corner + new Vector3((c + 0.5f) * tileSize, 0f, (r + 0.5f) * tileSize);

                TF_Tile tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                tile.name = $"TF_Tile_{index}";
                tile.transform.localScale = new Vector3(tileSize - tileGap, prefabScale.y, tileSize - tileGap);
                tile.Init(index);

                tiles[index] = tile;
            }
        }
    }

    // ───────────────────────── 데이터 변경 (멀티 전환 시 서버 전용이 될 부분) ─────────────────────────

    /// <summary>모든 타일을 주인 없음으로, 점수를 0으로 되돌린다.</summary>
    public void ResetBoard()
    {
        tileOwners = new int[TileCount];
        scores = new int[MaxPlayers];

        for (int i = 0; i < tileOwners.Length; i++)
        {
            tileOwners[i] = NoOwner;
            ApplyTileVisual(i, NoOwner);
            OnTileOwnerChanged?.Invoke(i, NoOwner);
        }

        for (int p = 0; p < MaxPlayers; p++)
            OnScoreChanged?.Invoke(p, 0);
    }

    /// <summary>
    /// 타일의 소유자를 바꾸고 점수를 갱신한다.
    /// 실제로 바뀌었으면 true, 이미 내 타일이거나 잘못된 값이면 false.
    /// </summary>
    public bool SetTileOwner(int tileIndex, int playerIndex)
    {
        if (tileIndex < 0 || tileIndex >= tileOwners.Length) return false;
        if (playerIndex < 0 || playerIndex >= MaxPlayers) return false;

        int previousOwner = tileOwners[tileIndex];
        if (previousOwner == playerIndex) return false; // 같은 색 재방문은 무시

        // 점수는 전체 재계산 없이 증감으로만 관리
        if (previousOwner >= 0 && previousOwner < MaxPlayers)
        {
            scores[previousOwner]--;
            OnScoreChanged?.Invoke(previousOwner, scores[previousOwner]);
        }
        scores[playerIndex]++;
        OnScoreChanged?.Invoke(playerIndex, scores[playerIndex]);

        tileOwners[tileIndex] = playerIndex;
        ApplyTileVisual(tileIndex, playerIndex);
        OnTileOwnerChanged?.Invoke(tileIndex, playerIndex);
        return true;
    }

    /// <summary>
    /// 플레이어의 타일 색을 정한다. colorIndex는 playerMaterials의 번호이고, 그 플레이어 캐릭터의 프리팹 번호와 같게 넣는다.
    /// 점수는 플레이어(자리)별로 따로 세므로, 두 플레이어의 색이 같아도 점수는 섞이지 않는다.
    /// 이미 칠해진 타일이 있으면 새 색으로 다시 입힌다.
    /// </summary>
    public void SetPlayerColorIndex(int playerIndex, int colorIndex)
    {
        if (playerIndex < 0 || playerIndex >= MaxPlayers) return;
        if (playerColorIndex[playerIndex] == colorIndex) return;

        playerColorIndex[playerIndex] = colorIndex;

        for (int i = 0; i < tileOwners.Length; i++)
        {
            if (tileOwners[i] == playerIndex) ApplyTileVisual(i, playerIndex);
        }
    }

    // ───────────────────────── 조회 API ─────────────────────────

    /// <summary>월드 좌표가 속한 타일 인덱스를 구한다. 격자 밖이면 false.</summary>
    public bool TryGetTileIndex(Vector3 worldPosition, out int tileIndex)
    {
        Vector3 local = worldPosition - GetGridCorner();
        int c = Mathf.FloorToInt(local.x / tileSize);
        int r = Mathf.FloorToInt(local.z / tileSize);

        if (c < 0 || c >= columns || r < 0 || r >= rows)
        {
            tileIndex = -1;
            return false;
        }

        tileIndex = r * columns + c;
        return true;
    }

    public int GetTileOwner(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= tileOwners.Length) return NoOwner;
        return tileOwners[tileIndex];
    }

    public int GetScore(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= scores.Length) return 0;
        return scores[playerIndex];
    }

    // ───────────────────────── 내부 처리 ─────────────────────────

    private void ApplyTileVisual(int tileIndex, int ownerIndex)
    {
        if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length) return;
        tiles[tileIndex].SetOwner(ownerIndex, GetMaterial(ownerIndex));
    }

    private Material GetMaterial(int ownerIndex)
    {
        if (ownerIndex >= 0 && ownerIndex < MaxPlayers)
        {
            int colorIndex = playerColorIndex[ownerIndex];
            if (colorIndex >= 0 && colorIndex < playerMaterials.Length && playerMaterials[colorIndex] != null)
                return playerMaterials[colorIndex];
        }
        return neutralMaterial;
    }

    // ───────────────────────── 에디터 보조 ─────────────────────────

    private void OnDrawGizmos()
    {
        // 씬 뷰에서 격자 범위(초록)를 항상 확인할 수 있다.
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(columns * tileSize, 0.01f, rows * tileSize));
    }
}