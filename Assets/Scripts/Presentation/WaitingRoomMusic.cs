using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One continuous music source shared by the title and waiting-room scenes.</summary>
public sealed class WaitingRoomMusic : MonoBehaviour
{
    private const string MusicResource = "WaitingRoomAudio/happybeat";
    private static WaitingRoomMusic instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterScenes()
    {
        // Also supports entering Play mode directly in any of the four scenes.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && scene != SceneManager.GetActiveScene()) return;
        bool waitingScene = scene.name == "TitleScene" || scene.name == "NicknameScene" ||
                            scene.name == "Lobby" || scene.name == "Character";
        if (!waitingScene)
        {
            if (instance != null)
            {
                // Stop immediately, before deferred destruction, to avoid overlapping game music.
                instance.GetComponent<AudioSource>().Stop();
                Destroy(instance.gameObject);
                instance = null;
            }
            return;
        }
        // Keep the same source and playback position throughout waiting-room transitions.
        if (instance != null) return;
        var clip = Resources.Load<AudioClip>(MusicResource);
        if (clip == null)
        {
            Debug.LogError("[WaitingRoomMusic] Missing music: " + MusicResource);
            return;
        }
        var root = new GameObject("Waiting Room BGM");
        instance = root.AddComponent<WaitingRoomMusic>();
        DontDestroyOnLoad(root);
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.loop = true;
        source.volume = .35f;
        source.clip = clip;
        source.Play();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
