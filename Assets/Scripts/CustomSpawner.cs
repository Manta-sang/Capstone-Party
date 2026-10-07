using UnityEngine;
using Unity.Netcode;

public class CustomSpawner : MonoBehaviour
{
    [Header("만들어둔 캐릭터 프리팹 4개 등록")]
    [SerializeField] private GameObject characterPrefabA;
    [SerializeField] private GameObject characterPrefabB;
    [SerializeField] private GameObject characterPrefabC; // 새로 추가
    [SerializeField] private GameObject characterPrefabD; // 새로 추가

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
    }

    void OnServerStarted()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // ★ [핵심] 클라이언트 ID에 따라 4개의 프리팹 중 하나를 번갈아가며 선택 (나머지 연산 % 4)
        GameObject prefabToSpawn = null;
        int selection = (int)(clientId % 4);

        switch (selection)
        {
            case 0: prefabToSpawn = characterPrefabA; break;
            case 1: prefabToSpawn = characterPrefabB; break;
            case 2: prefabToSpawn = characterPrefabC; break;
            case 3: prefabToSpawn = characterPrefabD; break;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"클라이언트 {clientId}번에 해당하는 스폰 프리팹이 비어있습니다!");
            return;
        }

        // 플레이어들이 겹치지 않게 옆으로 살짝 간격을 두고 스폰
        Vector3 spawnPosition = new Vector3(clientId * 2f, 1f, 0f);

        GameObject spawnedInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        NetworkObject netObj = spawnedInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
        }
    }
    //변경
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}