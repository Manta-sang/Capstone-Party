using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SawBladeSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private SawBlade bladePrefab;

    [Header("Arena (가로 X, 세로 Z)")]
    [SerializeField] private Vector2 arenaSize = new Vector2(20f, 20f);
    [Tooltip("벽 오브젝트들의 부모. Collider 진입 중 무시")]
    [SerializeField] private Transform wallsRoot;

    [Header("Spawn")]
    [Tooltip("벽 바깥쪽으로 얼마나 떨어진 곳에서 생성할지")]
    [SerializeField] private float spawnOffset = 3f;
    [Tooltip("진입 각도의 최대값")]
    [Range(0f, 75f)]
    [SerializeField] private float maxEntryAngle = 60f;

    [Header("Lifetime (사라지는 조건)")]
    [Tooltip("벽에 튕기는 최솟값")]
    [SerializeField] private int minBounces = 3;
    [Tooltip("벽에 튕기는 최댓값")]
    [SerializeField] private int maxBounces = 6;
    [SerializeField] private float timeLimit = 15f; //안전장치

    [Header("Rate")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxBlades = 10;
    [Tooltip("최소속도")]
    [SerializeField] private float minSpeed = 4f;
    [Tooltip("최대속도")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private bool autoStart = true;

    private readonly List<SawBlade> blades = new List<SawBlade>();
    private Collider[] wallColliders = new Collider[0];

    private void Awake()
    {
        if (wallsRoot != null) wallColliders = wallsRoot.GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        if (autoStart) StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            blades.RemoveAll(b => b == null);
            if (blades.Count < maxBlades) Spawn();
            yield return wait;
        }
    }

    [ContextMenu("Spawn One")]
    public void Spawn()
    {
        GetSide(Random.value, out Vector3 inward, out Vector3 lateral, out float halfNormal, out float halfLateral);

        // 톱날이 "완전히 안쪽에 들어왔다"고 판정되는 지점(P)을 먼저 정하고, 거기서 거꾸로 생성 위치를 계산한다.
        // P는 모서리에서 충분히 떨어져 있어서, 어떤 각도로 들어와도 옆벽에 먼저 닿지 않고 진입을 마친다.
        float depth = bladePrefab.EnterDepth + 0.1f;          // 벽 평면에서 안쪽으로 이만큼 들어온 지점
        float range = Mathf.Max(0f, halfLateral - depth);     // P의 좌우 범위
        float pLateral = Random.Range(-range, range);
        float tan = Mathf.Tan(Random.Range(-maxEntryAngle, maxEntryAngle) * Mathf.Deg2Rad);

        Vector3 c = transform.position;
        float backDistance = spawnOffset + depth;
        Vector3 spawnPos = c - inward * (halfNormal + spawnOffset) + lateral * (pLateral - tan * backDistance);
        Vector3 dir = (inward + lateral * tan).normalized;

        SawBlade blade = Instantiate(bladePrefab, spawnPos, Quaternion.identity);
        blade.Launch(dir, Random.Range(minSpeed, maxSpeed), c, arenaSize, wallColliders,
                     Random.Range(minBounces, maxBounces + 1), timeLimit, spawnOffset);
        blades.Add(blade);
    }

    // 네 변 중 하나를 변 길이에 비례해 선택 (0~1 난수)
    private void GetSide(float value, out Vector3 inward, out Vector3 lateral, out float halfNormal, out float halfLateral)
    {
        float hx = arenaSize.x * 0.5f;
        float hz = arenaSize.y * 0.5f;
        float r = value * 2f * (arenaSize.x + arenaSize.y);

        if (r < arenaSize.x) { inward = Vector3.back; lateral = Vector3.right; halfNormal = hz; halfLateral = hx; } // 위(+Z)
        else if (r < arenaSize.x * 2f) { inward = Vector3.forward; lateral = Vector3.right; halfNormal = hz; halfLateral = hx; } // 아래(-Z)
        else if (r < arenaSize.x * 2f + arenaSize.y) { inward = Vector3.left; lateral = Vector3.forward; halfNormal = hx; halfLateral = hz; } // 오른쪽(+X)
        else { inward = Vector3.right; lateral = Vector3.forward; halfNormal = hx; halfLateral = hz; } // 왼쪽(-X)
    }

    private void OnDrawGizmos()
    {
        Vector3 c = transform.position;
        float hx = arenaSize.x * 0.5f;
        float hz = arenaSize.y * 0.5f;

        // 경기장 영역 (초록)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(c, new Vector3(arenaSize.x, 0.05f, arenaSize.y));

        // 생성 위치 범위: 네 변 바깥쪽 선 (빨강). 비스듬한 진입 때문에 양끝이 조금 더 늘어남
        float extra = spawnOffset * Mathf.Tan(maxEntryAngle * Mathf.Deg2Rad);
        float ox = hx + spawnOffset;
        float oz = hz + spawnOffset;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(c + new Vector3(-hx - extra, 0f, oz), c + new Vector3(hx + extra, 0f, oz));
        Gizmos.DrawLine(c + new Vector3(-hx - extra, 0f, -oz), c + new Vector3(hx + extra, 0f, -oz));
        Gizmos.DrawLine(c + new Vector3(ox, 0f, -hz - extra), c + new Vector3(ox, 0f, hz + extra));
        Gizmos.DrawLine(c + new Vector3(-ox, 0f, -hz - extra), c + new Vector3(-ox, 0f, hz + extra));
    }
}