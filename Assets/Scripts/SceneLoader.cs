using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // 버튼에 연결할 함수 (이동할 씬 이름을 글자로 적어줍니다)
    public void LoadSceneByName(string Character)
    {
        SceneManager.LoadScene(Character);
        Debug.Log(Character + " 씬으로 이동합니다!");
    }
}