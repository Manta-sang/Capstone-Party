using UnityEngine;
using Unity.Netcode;

public class CustomSpawner : MonoBehaviour
{
    [Header("스폰할 캐릭터 프리팹 리스트 (원하는 만큼 넣으세요)")]
    [SerializeField] private GameObject[] characterPrefabs;

    void Start()
    {
        // 네트워크가 켜져 있고, 내가 방장(서버)일 때만 작동
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 씬이 열리자마자 이미 방에 들어와 있는 사람들 전부 스폰
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerIfNoCharacter(clientId);
        }

        // 게임 중에 뒤늦게 들어오는 사람들을 위해 이벤트 연결
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        SpawnPlayerIfNoCharacter(clientId);
    }

    void SpawnPlayerIfNoCharacter(ulong clientId)
    {
        // 이미 씬에 내 캐릭터가 있다면 중복 스폰 방지
        if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;

        // 인스펙터에 등록된 프리팹이 하나도 없으면 경고
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("CustomSpawner에 등록된 캐릭터 프리팹이 없습니다!");
            return;
        }

        // ★ 핵심: 등록된 프리팹 개수에 맞춰서 알아서 번갈아가며 스폰 (2개면 0, 1, 0, 1...)
        int selection = (int)(clientId % (ulong)characterPrefabs.Length);
        GameObject prefabToSpawn = characterPrefabs[selection];

        if (prefabToSpawn == null) return;

        // 안 겹치게 옆으로 살짝 띄워서 스폰
        Vector3 spawnPosition = new Vector3(clientId * 2f, 1f, 0f);
        GameObject spawnedInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        NetworkObject netObj = spawnedInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"플레이어 {clientId}번 스폰 완료! (선택된 캐릭터 인덱스: {selection})");
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}