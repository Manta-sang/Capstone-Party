using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원통형(Cylinder) 형태의 Convex MeshCollider를 생성해 주는 컴포넌트.
/// - 같은 (반지름, 높이, 세그먼트, 축) 설정을 가진 오브젝트끼리 메시를 공유합니다.
/// - 생성 메시는 씬/프리팹에 저장되지 않습니다 (HideAndDontSave).
/// - 움직이는 오브젝트에 쓸 때는 Rigidbody(Kinematic)와 함께 사용하세요.
/// </summary>
[RequireComponent(typeof(MeshCollider))]
[DisallowMultipleComponent]
[ExecuteAlways]
public class CustomCylinderCollider : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Cylinder Collider Settings")]
    [Tooltip("원통의 반지름")]
    [Min(0.001f)] public float radius = 0.5f;

    [Tooltip("원통의 높이 (선택한 축 방향 길이)")]
    [Min(0.001f)] public float height = 0.2f;

    [Tooltip("원통의 정교함 (높을수록 원형에 가깝지만 Convex 제한(255)에 주의)")]
    [Range(8, 64)] public int segments = 24;

    [Tooltip("원통이 향하는 축. 기본은 Y(위아래로 서 있는 원통)")]
    public Axis axis = Axis.Y;

    [Header("Physics Settings")]
    [Tooltip("트리거로만 사용할 때 체크 (원통은 항상 Convex로 생성됩니다)")]
    public bool isTrigger = false;

    // ---------- 메시 캐시 (설정이 같으면 공유) ----------
    private class CacheEntry
    {
        public Mesh mesh;
        public int refCount;
    }

    private static readonly Dictionary<(float, float, int, Axis), CacheEntry> cache =
        new Dictionary<(float, float, int, Axis), CacheEntry>();

    private MeshCollider meshCollider;
    private (float, float, int, Axis) currentKey;
    private bool hasMesh;
    private bool rebuildQueued;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        // 파괴될 메시를 콜라이더가 계속 참조하지 않도록 먼저 해제
        if (meshCollider != null)
            meshCollider.sharedMesh = null;

        ReleaseCurrent();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate 안에서 직접 메시를 파괴/교체하면 경고가 날 수 있어서 한 틱 뒤에 처리
        if (rebuildQueued) return;
        rebuildQueued = true;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (this != null && isActiveAndEnabled)
                Rebuild();
        };
    }
#endif

    /// <summary>
    /// 코드에서 radius/height 등을 바꾼 뒤 호출하면 콜라이더가 갱신됩니다.
    /// </summary>
    public void Rebuild()
    {
        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            return;

        var newKey = (radius, height, segments, axis);

        // 메시 설정이 그대로면 물리 옵션만 갱신
        if (hasMesh && currentKey.Equals(newKey) && meshCollider.sharedMesh != null)
        {
            meshCollider.isTrigger = isTrigger;
            return;
        }

        Mesh mesh = Acquire(newKey);

        // 이전 메시를 놓기 전에 콜라이더 참조부터 끊기
        meshCollider.sharedMesh = null;
        ReleaseCurrent();

        currentKey = newKey;
        hasMesh = true;

        meshCollider.convex = true;   // 원통은 항상 Convex (Trigger / Rigidbody 충돌에 필요)
        meshCollider.isTrigger = isTrigger;
        meshCollider.sharedMesh = mesh;
    }

    // ---------- 캐시 관리 ----------
    private static Mesh Acquire((float, float, int, Axis) key)
    {
        if (!cache.TryGetValue(key, out CacheEntry entry) || entry.mesh == null)
        {
            entry = new CacheEntry
            {
                mesh = CreateCylinderMesh(key.Item1, key.Item2, key.Item3, key.Item4),
                refCount = 0
            };
            cache[key] = entry;
        }

        entry.refCount++;
        return entry.mesh;
    }

    private void ReleaseCurrent()
    {
        if (!hasMesh) return;
        hasMesh = false;

        if (!cache.TryGetValue(currentKey, out CacheEntry entry))
            return;

        entry.refCount--;
        if (entry.refCount <= 0)
        {
            cache.Remove(currentKey);
            if (entry.mesh != null)
            {
                if (Application.isPlaying) Destroy(entry.mesh);
                else DestroyImmediate(entry.mesh);
            }
        }
    }

    // ---------- 메시 생성 ----------
    private static Mesh CreateCylinderMesh(float r, float h, int segs, Axis axis)
    {
        var mesh = new Mesh
        {
            name = "GeneratedCylinderColliderMesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        Quaternion rot;
        switch (axis)
        {
            case Axis.X: rot = Quaternion.Euler(0f, 0f, 90f); break;
            case Axis.Z: rot = Quaternion.Euler(90f, 0f, 0f); break;
            default: rot = Quaternion.identity; break;
        }

        float halfHeight = h * 0.5f;

        // 0: 윗면 중심, 1: 밑면 중심, 이후 (윗점, 밑점) 쌍이 segs개
        var vertices = new Vector3[2 + segs * 2];
        vertices[0] = rot * new Vector3(0f, halfHeight, 0f);
        vertices[1] = rot * new Vector3(0f, -halfHeight, 0f);

        for (int i = 0; i < segs; i++)
        {
            float angle = (float)i / segs * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;

            vertices[2 + i * 2] = rot * new Vector3(x, halfHeight, z);
            vertices[2 + i * 2 + 1] = rot * new Vector3(x, -halfHeight, z);
        }

        var triangles = new int[segs * 12];
        int t = 0;
        for (int i = 0; i < segs; i++)
        {
            int topCurr = 2 + i * 2;
            int botCurr = topCurr + 1;
            int next = (i + 1) % segs;
            int topNext = 2 + next * 2;
            int botNext = topNext + 1;

            // 옆면
            triangles[t++] = topCurr;
            triangles[t++] = topNext;
            triangles[t++] = botCurr;

            triangles[t++] = botCurr;
            triangles[t++] = topNext;
            triangles[t++] = botNext;

            // 윗면
            triangles[t++] = 0;
            triangles[t++] = topNext;
            triangles[t++] = topCurr;

            // 밑면
            triangles[t++] = 1;
            triangles[t++] = botCurr;
            triangles[t++] = botNext;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }
}