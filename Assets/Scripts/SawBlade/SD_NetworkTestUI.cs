using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 개발 중 테스트용: 로비/Relay 없이 같은 PC에서 바로 Host/Client로 접속하는 버튼.
/// 테스트 씬의 NetworkManager 오브젝트에 붙인다. (팀원의 LobbyRelayManager와 같은 씬에 두지 말 것)
/// UnityTransport 기본 설정(127.0.0.1:7777)으로 접속한다.
/// </summary>
public class SD_NetworkTestUI : MonoBehaviour
{
    private void OnGUI()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            GUILayout.Label("NetworkManager가 씬에 없습니다");
            return;
        }

        GUILayout.BeginArea(new Rect(10f, 10f, 240f, 140f), GUI.skin.box);

        if (!manager.IsClient && !manager.IsServer)
        {
            if (GUILayout.Button("Host 시작")) manager.StartHost();
            if (GUILayout.Button("Client 접속 (127.0.0.1)")) manager.StartClient();
        }
        else
        {
            string role = manager.IsHost ? "Host" : "Client";
            GUILayout.Label($"{role} (내 ID: {manager.LocalClientId})");
            if (GUILayout.Button("연결 종료")) manager.Shutdown();
        }

        GUILayout.EndArea();
    }
}
