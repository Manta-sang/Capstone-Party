using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SD_SpawnPoints : MonoBehaviour
{
    [Tooltip("첫 번째 플레이어(Host)가 서는 위치")]
    [SerializeField] private Vector3 firstSpawnPosition = new Vector3(0f, 1f, 0f);

    [Tooltip("다음 플레이어마다 X축으로 이만큼 옆에 선다. 캐릭터 지름보다 충분히 크게")]
    [SerializeField] private float spacing = 2f;

    private readonly Dictionary<ulong, int> slotByClient = new Dictionary<ulong, int>();
    private NetworkManager manager;

    private void Start()
    {
        manager = NetworkManager.Singleton;
        if (manager == null) return;

        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = OnConnectionApproval;
        manager.OnClientDisconnectCallback += OnClientDisconnect;
        manager.OnServerStarted += OnServerStarted;
    }

    private void OnDestroy()
    {
        if (manager == null) return;

        manager.ConnectionApprovalCallback = null;
        manager.OnClientDisconnectCallback -= OnClientDisconnect;
        manager.OnServerStarted -= OnServerStarted;
    }

    private void OnServerStarted()
    {
        slotByClient.Clear();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        slotByClient.Remove(clientId);
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
                                      NetworkManager.ConnectionApprovalResponse response)
    {
        int slot = GetSlot(request.ClientNetworkId);

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = GetSpawnPosition(request.ClientNetworkId);
        response.Rotation = Quaternion.identity;
    }

    private int GetSlot(ulong clientId)
    {
        if (slotByClient.TryGetValue(clientId, out int existing)) return existing;

        int slot = 0;
        while (slotByClient.ContainsValue(slot)) slot++;

        slotByClient[clientId] = slot;
        return slot;
    }

    public Vector3 GetSpawnPosition(ulong clientId)
    {
        int slot = GetSlot(clientId);
        return firstSpawnPosition + Vector3.right * (spacing * slot);
    }
}