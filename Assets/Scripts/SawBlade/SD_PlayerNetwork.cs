using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 프리팹이 네트워크에 생성될 때 해야 할 일을 처리한다.
/// - 재시작할 때 돌아올 위치를 기억한다 (생성 위치는 SD_SpawnPoints가 접속 승인 단계에서 정한다)
/// - 내 캐릭터일 때만 카메라가 따라가게 하고, 관전 카메라에 내 플레이어를 알려준다
/// 플레이어 프리팹에 함께 붙인다.
/// </summary>
[RequireComponent(typeof(SD_PlayerLife))]
public class SD_PlayerNetwork : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // 재시작할 때 돌아올 위치 기억
        GetComponent<SD_PlayerLife>().SetSpawnPoint(transform.position, transform.rotation);

        SD_CameraFollow cameraFollow = FindObjectOfType<SD_CameraFollow>();
        if (cameraFollow != null) cameraFollow.SetTarget(transform);

        SD_SpectatorCamera spectator = FindObjectOfType<SD_SpectatorCamera>();
        if (spectator != null) spectator.SetLocalPlayer(GetComponent<SD_PlayerLife>());
    }
}