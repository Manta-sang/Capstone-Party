using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타일 뒤집기의 점수판. 플레이어마다 "초상화 + 타일 개수"를 위쪽에 보여 준다. (TF_GameManager와 같은 오브젝트가 아니어도 된다. Canvas 쪽에 붙인다)
///
/// TF_GameManager와 TF_TileGridManager는 이 스크립트를 모른다. 이 스크립트가 공개된 이벤트와 값만 읽어서 화면을 갱신한다. (TF_GameSound과 같은 방식)
///  - 라운드 시작: TF_GameManager.GameStarted -> 참가 인원만큼 칸을 켜고 초상화와 현재 점수를 채운다
///  - 점수 변화: TF_TileGridManager.OnScoreChanged -> 해당 칸의 숫자만 바꾼다
/// 점수는 칠하기 확정 결과를 모든 컴퓨터가 똑같이 적용해서 같은 값이 되므로, 네트워크 동기화는 필요 없다.
///
/// 칸 배치: 인스펙터의 Slots는 화면 순서대로 4칸이다. [0],[1] = 왼쪽 두 칸, [2],[3] = 오른쪽 두 칸.
/// 참가 인원과 상관없이 0번 칸부터 순서대로 쓴다. (1명: [0] / 2명: [0],[1] / 3명: [0],[1],[2] / 4명: 전부)
/// 플레이어 자리 번호 순서대로 칸이 채워지고, 쓰지 않는 칸은 꺼진다.
/// </summary>
public class TF_ScoreHUD : MonoBehaviour
{
    [System.Serializable]
    private class Slot
    {
        [Tooltip("이 칸 전체. 쓰지 않는 칸은 이 오브젝트를 꺼서 숨긴다")]
        public GameObject root;
        [Tooltip("캐릭터 초상화가 들어갈 Image. 비워 두면 초상화는 없다")]
        public Image portrait;
        [Tooltip("타일 개수를 보여 줄 텍스트")]
        public TMP_Text scoreText;
    }

    [Header("칸 (화면 순서: 왼쪽 2칸 -> 오른쪽 2칸)")]
    [SerializeField] private Slot[] slots = new Slot[TF_TileGridManager.MaxPlayers];

    [Header("초상화")]
    [Tooltip("캐릭터 번호 순서대로 초상화를 넣는다. TF_GameManager의 characterPrefabs와 같은 번호끼리 짝이다. 번호가 모자라거나 비어 있으면 그 캐릭터는 초상화 없이 숫자만 보인다")]
    [SerializeField] private Sprite[] portraits;

    [Header("내 칸 강조")]
    [Tooltip("내 칸(Root)의 크기 배율. 1이면 다른 칸과 같다")]
    [SerializeField, Min(0.1f)] private float localScale = 1.1f;

    // 참가 인원별로 쓰는 칸 번호. 인덱스 = 참가 인원, 값 = 플레이어 자리 순서대로 쓸 칸
    private static readonly int[][] Layouts =
    {
        new int[0],
        new[] { 0 },
        new[] { 0, 1 },
        new[] { 0, 1, 2 },
        new[] { 0, 1, 2, 3 },
    };

    private TF_GameManager manager;
    private TF_TileGridManager grid;
    private Vector3[] baseScales;
    private readonly int[] slotOfPlayer = new int[TF_TileGridManager.MaxPlayers]; // 플레이어 자리 번호 -> 칸 번호 (-1이면 안 보임)

    private void Awake()
    {
        baseScales = new Vector3[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].root != null)
            {
                baseScales[i] = slots[i].root.transform.localScale;
                slots[i].root.SetActive(false); // 라운드가 시작되기 전에는 숨긴다
            }
        }

        for (int p = 0; p < slotOfPlayer.Length; p++) slotOfPlayer[p] = -1;
    }

    private void Start()
    {
        // TF_GameManager.Instance는 매니저의 Awake에서 정해지므로, 모든 Awake가 끝난 Start에서는 항상 찾을 수 있다
        manager = TF_GameManager.Instance;
        grid = FindObjectOfType<TF_TileGridManager>();

        if (manager == null || grid == null)
        {
            Debug.LogWarning("[TF_ScoreHUD] 씬에서 TF_GameManager 또는 TF_TileGridManager를 찾지 못했습니다. 점수판이 나오지 않습니다.", this);
            return;
        }

        manager.GameStarted += OnGameStarted;
        grid.OnScoreChanged += OnScoreChanged;

        // 이 스크립트가 라운드 시작 뒤에 켜진 경우를 위해 바로 확인한다
        if (manager.HasRoundStarted) Build();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.GameStarted -= OnGameStarted;
        if (grid != null) grid.OnScoreChanged -= OnScoreChanged;
    }

    private void OnGameStarted()
    {
        Build();
    }

    // 참가 인원만큼 칸을 켜고, 각 칸의 색/초상화/내 칸 표시/현재 점수를 정한다
    private void Build()
    {
        int count = Mathf.Clamp(manager.PlayerCount, 0, Layouts.Length - 1);
        int[] layout = Layouts[count];

        // 일단 모든 칸을 끈다 (재시작으로 참가 인원이 달라져도 깨끗하게 다시 만든다)
        for (int p = 0; p < slotOfPlayer.Length; p++) slotOfPlayer[p] = -1;
        foreach (Slot slot in slots)
        {
            if (slot != null && slot.root != null) slot.root.SetActive(false);
        }

        for (int player = 0; player < layout.Length; player++)
        {
            int slotIndex = layout[player];
            if (slotIndex < 0 || slotIndex >= slots.Length) continue;

            Slot slot = slots[slotIndex];
            if (slot == null) continue;

            slotOfPlayer[player] = slotIndex;
            bool isLocal = player == manager.LocalSlot;

            if (slot.root != null)
            {
                slot.root.transform.localScale = baseScales[slotIndex] * (isLocal ? localScale : 1f);
                slot.root.SetActive(true);
            }

            if (slot.portrait != null)
            {
                int character = grid.GetPlayerColorIndex(player);
                Sprite sprite = portraits != null && character >= 0 && character < portraits.Length ? portraits[character] : null;
                slot.portrait.sprite = sprite;
                slot.portrait.enabled = sprite != null;
            }

            SetScoreText(slot, grid.GetScore(player));
        }
    }

    // 점수가 바뀔 때마다 불린다. 화면에 보이는 칸이면 숫자만 바꾼다
    private void OnScoreChanged(int player, int score)
    {
        if (player < 0 || player >= slotOfPlayer.Length) return;

        int slotIndex = slotOfPlayer[player];
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) return;

        SetScoreText(slots[slotIndex], score);
    }

    private static void SetScoreText(Slot slot, int score)
    {
        // 세 자리로 맞춘다 (0 -> 000, 29 -> 029). 세 자리를 넘으면 그대로 보여 준다
        if (slot.scoreText != null) slot.scoreText.text = score.ToString("D3");
    }
}