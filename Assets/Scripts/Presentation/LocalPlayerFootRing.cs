using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>미니게임에서 이 참가자가 조종하는 캐릭터의 발밑에 링을 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerFootRing : MonoBehaviour
{
    [SerializeField] private Color ringColor = new Color(0.3f, 0.95f, 1f);
    [SerializeField, Min(0.1f)] private float radius = 0.85f;
    [SerializeField, Min(0.01f)] private float width = 0.09f;
    [SerializeField] private float heightOffset = 0.06f;

    private const int Segments = 48;
    private NetworkObject player;
    private Collider bodyCollider;
    private LineRenderer ring;
    private Material ringMaterial;

    private void Awake()
    {
        player = GetComponent<NetworkObject>();
        bodyCollider = GetComponent<Collider>();
    }

    private void LateUpdate()
    {
        // 네트워크 소유권을 확인한 뒤 로컬 화면에만 표시합니다.
        // BlockFall에서는 탈락한 캐릭터의 콜라이더가 비활성화됩니다.
        bool visible = player != null && player.IsSpawned && player.IsLocalPlayer
            && Array.IndexOf(PartyGameRotation.Scenes, gameObject.scene.name) >= 0
            && (bodyCollider == null || bodyCollider.enabled)
            && GetComponent<CrosswalkRun.CrosswalkPlayer>()?.IsDead != true
            && GetComponent<SD_PlayerDeath>()?.IsDead != true;

        if (visible && ring == null) CreateRing();
        if (ring == null) return;

        ring.enabled = visible;
        if (!visible) return;

        // 캐릭터 회전과 상관없이 수평을 유지하고 콜라이더 바닥 높이를 따라갑니다.
        Vector3 center = transform.position;
        if (bodyCollider != null) center.y = bodyCollider.bounds.min.y;
        center.y += heightOffset;
        ring.transform.SetPositionAndRotation(center, Quaternion.Euler(90f, 0f, 0f));
    }

    private void CreateRing()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            enabled = false;
            return;
        }

        GameObject marker = new GameObject("Local Player Foot Ring");
        marker.transform.SetParent(transform, false);
        ring = marker.AddComponent<LineRenderer>();
        ringMaterial = new Material(shader);
        ring.sharedMaterial = ringMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.positionCount = Segments;
        ring.startColor = ring.endColor = ringColor;
        ring.startWidth = ring.endWidth = width;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * (2f * Mathf.PI / Segments);
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private void OnDisable()
    {
        if (ring != null) ring.enabled = false;
    }

    private void OnDestroy()
    {
        if (ring != null) Destroy(ring.gameObject);
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
