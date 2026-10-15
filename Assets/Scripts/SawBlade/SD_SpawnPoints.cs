using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 최대 4명의 플레이어를 미리 지정된 4개의 스폰 위치 중 랜덤으로 중복 없이 배정합니다.
/// </summary>
public class SD_SpawnPoints : MonoBehaviour
{
    [Header("스폰 포인트 설정 (정확히 4개 등록)")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, int> assignedIndicesByClient = new Dictionary<ulong, int>();
    private List<int> availableIndices = new List<int>();
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
        ResetSpawnPool();
    }

    private void ResetSpawnPool()
    {
        assignedIndicesByClient.Clear();
        availableIndices.Clear();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                availableIndices.Add(i);
            }
            // 랜덤으로 섞기 (Fisher-Yates Shuffle)
            for (int i = 0; i < availableIndices.Count; i++)
            {
                int rand = Random.Range(i, availableIndices.Count);
                int temp = availableIndices[i];
                availableIndices[i] = availableIndices[rand];
                availableIndices[rand] = temp;
            }
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        assignedIndicesByClient.Remove(clientId);
        // 필요시 나간 자리를 다시 풀에 넣는 로직을 추가할 수 있습니다.
    }

    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
                                      NetworkManager.ConnectionApprovalResponse response)
    {
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (GetSpawnTransform(request.ClientNetworkId, out Transform t))
        {
            spawnPos = t.position;
            spawnRot = t.rotation;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = spawnPos;
        response.Rotation = spawnRot;
    }

    private bool GetSpawnTransform(ulong clientId, out Transform spawnTransform)
    {
        spawnTransform = null;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        if (assignedIndicesByClient.TryGetValue(clientId, out int existingIndex))
        {
            spawnTransform = spawnPoints[existingIndex];
            return true;
        }

        // 남은 스폰 포인트가 있다면 앞에서부터 하나씩 할당
        if (availableIndices.Count > 0)
        {
            int index = availableIndices[0];
            availableIndices.RemoveAt(0);

            assignedIndicesByClient[clientId] = index;
            spawnTransform = spawnPoints[index];
            return true;
        }

        // 4명을 초과할 경우 첫 번째 위치로 예외 처리
        spawnTransform = spawnPoints[0];
        return false;
    }

    public Vector3 GetSpawnPosition(ulong clientId)
    {
        if (GetSpawnTransform(clientId, out Transform t))
        {
            return t.position;
        }
        return (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints[0].position : Vector3.zero;
    }
}