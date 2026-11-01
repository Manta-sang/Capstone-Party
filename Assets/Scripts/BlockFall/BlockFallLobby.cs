using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BlockFall {
public class BlockFallLobby : MonoBehaviour {
    void OnGUI() {
        var n=NetworkManager.Singleton;
        if(n!=null && n.IsServer && GUI.Button(new Rect(20,Screen.height-65,220,45),"Start BlockFall"))
            n.SceneManager.LoadScene("BlockFall",LoadSceneMode.Single);
    }
}}
