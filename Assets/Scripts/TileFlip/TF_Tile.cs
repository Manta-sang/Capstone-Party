using UnityEngine;

/// <summary>
/// 타일 한 칸. 네트워크 컴포넌트 없이 "보이는 머티리얼"과 "무언가 닿았다는 알림"만 담당한다.
/// 소유자와 색을 정하는 일은 TF_TileGridManager가, 닿은 것이 내 플레이어인지 판단하는 일은 TF_TilePaintTracker가 한다.
/// 
/// 타일 프리팹에는 Collider가 필요하고, Is Trigger를 켜는 것을 권장한다.
///  - 트리거는 타일 영역에 몸이 들어올 때 알려 준다. 점프로 공중에 떠서 지나갈 때는 닿지 않도록,
///    콜라이더의 높이는 낮게(바닥 근처만) 둔다. 서 있을 때는 캐릭터의 발이 콜라이더 안에 들어와 있어야 한다.
///  - 트리거를 켜면 타일이 바닥 역할을 하지 못하므로, 캐릭터가 설 바닥은 따로 있어야 한다.
/// </summary>
public class TF_Tile : MonoBehaviour
{
    private static bool warnedNoCollider;

    [SerializeField] private Renderer tileRenderer;

    /// <summary>타일의 실제 크기(월드 좌표). 렌더러가 없으면 스케일을 크기로 본다. TF_TileGridManager가 칸 간격을 계산할 때 쓴다</summary>
    public Vector3 WorldSize => tileRenderer != null ? tileRenderer.bounds.size : transform.lossyScale;

    private void Awake()
    {
        if (tileRenderer == null)
            tileRenderer = GetComponentInChildren<Renderer>();

        if (GetComponent<Collider>() == null && !warnedNoCollider)
        {
            warnedNoCollider = true;
            Debug.LogWarning("[TF_Tile] 타일 프리팹에 Collider가 없어서 플레이어가 닿아도 칠해지지 않습니다. Collider를 추가하고 Is Trigger를 켜 주세요.", this);
        }
    }

    /// <summary>머티리얼을 적용한다. (sharedMaterial 사용으로 머티리얼 인스턴스가 늘어나지 않음)</summary>
    public void SetMaterial(Material material)
    {
        if (tileRenderer != null && material != null)
            tileRenderer.sharedMaterial = material;
    }

    // 트리거 콜라이더에 무언가 들어왔을 때 (권장 설정)
    private void OnTriggerEnter(Collider other)
    {
        Notify(other);
    }

    // Is Trigger를 끄고 일반 충돌체로 쓰는 경우를 위해 같은 처리를 한다
    private void OnCollisionEnter(Collision collision)
    {
        Notify(collision.collider);
    }

    // 닿은 것이 내 플레이어인지는 트래커가 판단한다
    private void Notify(Collider other)
    {
        TF_TilePaintTracker tracker = TF_TilePaintTracker.Instance;
        if (tracker != null) tracker.OnTileTouched(this, other);
    }
}