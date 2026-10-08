using UnityEngine;
using Unity.Netcode; // 네트워크 기능 추가
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadSceneByName(string sceneName)
    {
        // 1. 멀티플레이가 켜져 있고, 내가 방장(서버)일 경우
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // 방장이 씬을 넘기면 클라이언트들도 자동으로 따라갑니다.
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            Debug.Log("네트워크 씬 이동: " + sceneName);
        }
        // 2. 내가 클라이언트일 경우 (버튼 작동 막기)
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            Debug.LogWarning("클라이언트는 방장이 씬을 넘길 때까지 기다려야 합니다.");
        }
        // 3. 네트워크 없이 그냥 테스트 중일 경우
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}