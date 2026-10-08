using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사각형 경기장 바깥에서 톱날을 생성해, 랜덤한 각도로 안쪽에 진입시킨다.
/// 이 오브젝트의 위치가 경기장 중심이다.
/// </summary>
public class SD_BladeSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private SD_Blade bladePrefab;

    [Header("Arena (가로 X, 세로 Z)")]
    [SerializeField] private Vector2 arenaSize = new Vector2(20f, 20f);
    [Tooltip("벽 오브젝트들의 부모")]
    [SerializeField] private Transform wallsRoot;

    [Header("Spawn")]
    [Tooltip("생성 위치(벽 두께 + 톱날 반지름보다 크게)")]
    [SerializeField] private float spawnOffset = 3f;
    [Tooltip("최대 진입 각도")]
    [Range(0f, 75f)]
    [SerializeField] private float maxEntryAngle = 60f;

    [Header("Lifetime (사라지는 조건)")]
    [Tooltip("벽에 튕기는 최소 횟수")]
    [SerializeField] private int minBounces = 3;
    [Tooltip("벽에 튕기는 최대 횟수")]
    [SerializeField] private int maxBounces = 6;
    [Tooltip("안전장치")]
    [SerializeField] private float timeLimit = 15f;

    [Header("Rate")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxBlades = 10;
    [Tooltip("최소 톱날 속도(초당 유니티 단위)")]
    [SerializeField] private float minSpeed = 4f;
    [Tooltip("최대 톱날 속도(초당 유니티 단위)")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private bool autoStart = true;

    [Header("Despawn (사라지는 범위)")]
    [Tooltip("경기장 가장자리에서 이 거리만큼 바깥으로 벗어난 톱날은 상태와 관계없이 삭제된다. 생성 위치 범위보다 작게 두면 자동으로 그보다 조금 크게 맞춰진다")]
    [SerializeField] private float despawnMargin = 12f;

    [Header("Difficulty (시간이 지날수록 톱날이 많아짐)")]
    [Tooltip("생성이 시작된 뒤 이 시간(초) 동안 서서히 어려워진다. 0이면 난이도가 올라가지 않는다")]
    [SerializeField] private float rampDuration = 60f;
    [Tooltip("난이도가 최대일 때의 생성 간격(초). 시작 값은 위의 Spawn Interval")]
    [SerializeField] private float endSpawnInterval = 0.8f;
    [Tooltip("난이도가 최대일 때 동시에 존재할 수 있는 최대 톱날 수. 시작 값은 위의 Max Blades")]
    [SerializeField] private int endMaxBlades = 16;

    private readonly List<SD_Blade> blades = new List<SD_Blade>();
    private Collider[] wallColliders = new Collider[0];
    private Coroutine spawnRoutine; // 생성 루프는 항상 하나만 돌게 관리

    private void Awake()
    {
        if (wallsRoot != null) wallColliders = wallsRoot.GetComponentsInChildren<Collider>();
        else Debug.LogWarning("SawBladeSpawner: wallsRoot가 비어 있어요. 벽을 통과하지 못합니다.", this);
    }

    private void Start()
    {
        if (autoStart) StartSpawning();
    }

    // 이미 돌고 있는 생성 루프가 있으면 멈추고 새로 시작한다 (루프가 두 개 돌지 않게)
    private void StartSpawning()
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>톱날 생성을 멈춘다 (이미 나온 톱날은 그대로 남는다)</summary>
    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        float startTime = Time.time;
        while (true)
        {
            // 0(시작) ~ 1(최대 난이도). 시작 값에서 끝 값까지 선형으로 변한다
            float t = rampDuration > 0f ? Mathf.Clamp01((Time.time - startTime) / rampDuration) : 0f;
            float currentInterval = Mathf.Lerp(spawnInterval, endSpawnInterval, t);
            int currentMaxBlades = Mathf.RoundToInt(Mathf.Lerp(maxBlades, endMaxBlades, t));

            blades.RemoveAll(b => b == null);
            if (blades.Count < currentMaxBlades) Spawn();

            yield return new WaitForSeconds(currentInterval);
        }
    }

    [ContextMenu("Spawn One")]
    public void Spawn()
    {
        GetSide(Random.value, out Vector3 inward, out Vector3 lateral, out float halfNormal, out float halfLateral);

        float depth = bladePrefab.EnterDepth + 0.1f;          // 벽 평면에서 안쪽으로 이만큼 들어온 지점
        float range = Mathf.Max(0f, halfLateral - depth);     // P의 좌우 범위
        float pLateral = Random.Range(-range, range);
        float tan = Mathf.Tan(Random.Range(-maxEntryAngle, maxEntryAngle) * Mathf.Deg2Rad);

        Vector3 c = transform.position;
        float backDistance = spawnOffset + depth;
        Vector3 spawnPos = c - inward * (halfNormal + spawnOffset) + lateral * (pLateral - tan * backDistance);
        Vector3 dir = (inward + lateral * tan).normalized;

        SD_Blade blade = Instantiate(bladePrefab, spawnPos, Quaternion.identity);
        blade.Launch(dir, Random.Range(minSpeed, maxSpeed), c, arenaSize, wallColliders,
                     Random.Range(minBounces, maxBounces + 1), timeLimit, spawnOffset, GetDespawnDistance());
        blades.Add(blade);
    }

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
    // 실제로 적용되는 사라지는 범위 거리. 생성 위치(진입 각도 때문에 옆으로 밀려난 범위 포함)보다 항상 크게 유지한다
    private float GetDespawnDistance()
    {
        float depth = bladePrefab != null ? bladePrefab.EnterDepth + 0.1f : 1f;
        float tan = Mathf.Tan(maxEntryAngle * Mathf.Deg2Rad);
        float spawnReach = Mathf.Max(spawnOffset, tan * (spawnOffset + depth) - depth); // 경기장 가장자리에서 생성 위치까지 가장 먼 거리
        return Mathf.Max(despawnMargin, spawnReach + 1f);
    }

    public void ResetSpawner()
    {
        StopSpawning();

        // 씬에 남아있던 모든 톱날 제거
        SD_Blade.DestroyAll();
        blades.Clear();

        if (autoStart)
        {
            StartSpawning();
        }
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

        // 사라지는 범위 (파랑): 이 영역 밖으로 벗어난 톱날은 삭제된다
        float d = GetDespawnDistance();
        Gizmos.color = new Color(0.3f, 0.6f, 1f);
        Gizmos.DrawWireCube(c, new Vector3(arenaSize.x + d * 2f, 0.05f, arenaSize.y + d * 2f));
    }
}