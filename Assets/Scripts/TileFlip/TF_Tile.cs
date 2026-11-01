using UnityEngine;

/// <summary>
/// 타일 한 칸. 네트워크 컴포넌트 없이 "내 인덱스 / 현재 소유자 / 보이는 머티리얼"만 담당한다.
/// 소유자 정보의 동기화는 TF_TileGridManager가 한다.
/// </summary>
public class TF_Tile : MonoBehaviour
{
    [SerializeField] private Renderer tileRenderer;

    /// <summary>타일의 실제 크기(월드 좌표). 렌더러가 없으면 스케일을 크기로 본다. TF_TileGridManager가 칸 간격을 계산할 때 쓴다</summary>
    public Vector3 WorldSize => tileRenderer != null ? tileRenderer.bounds.size : transform.lossyScale;

    public int Index { get; private set; }
    public int OwnerIndex { get; private set; } = -1;

    private void Awake()
    {
        if (tileRenderer == null)
            tileRenderer = GetComponentInChildren<Renderer>();
    }

    public void Init(int index)
    {
        Index = index;
    }

    /// <summary>소유자 번호와 그에 맞는 머티리얼을 적용한다. (sharedMaterial 사용으로 머티리얼 인스턴스가 늘어나지 않음)</summary>
    public void SetOwner(int ownerIndex, Material material)
    {
        OwnerIndex = ownerIndex;

        if (tileRenderer != null && material != null)
            tileRenderer.sharedMaterial = material;
    }
}