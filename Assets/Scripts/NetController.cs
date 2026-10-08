using UnityEngine;
using Unity.Netcode;

public class NetController : MonoBehaviour
{
    void OnGUI()
    {
        // 화면 좌측 상단에 박스 영역을 그림
        GUILayout.BeginArea(new Rect(20, 20, 250, 150));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host (방 만들기)", GUILayout.Height(40)))
            {
                Debug.Log("호스트 버튼 클릭됨!"); // 콘솔창에 찍히는지 확인
                NetworkManager.Singleton.StartHost();
            }

            if (GUILayout.Button("Start Client (참가하기)", GUILayout.Height(40)))
            {
                Debug.Log("클라이언트 버튼 클릭됨!"); // 콘솔창에 찍히는지 확인
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host (호스트 중)" : "Client (클라이언트 중)";
            GUILayout.Label("현재 상태: " + mode);
        }

        GUILayout.EndArea();
    }
}