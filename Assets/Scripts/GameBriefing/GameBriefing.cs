using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameBriefing : MonoBehaviour
{
    [System.Serializable] public class Guide { public string scene, title; [TextArea(3,8)] public string rules; [TextArea(3,8)] public string controls; }
    public Guide[] guides;
    public TMP_Text titleText, rulesText, controlsText, statusText, countdownText;
    [Min(1)] public float readingSeconds = 5;
    NetworkManager network;
    bool ready, leaving;
    int shown = -1, lastSecond = -2;
    void Awake()
    {
        network = NetworkManager.Singleton;
        if (network != null && network.IsServer && PartyRoster.Current != null) PartyRoster.Current.BriefingEndsAt.Value = -1;
        if (network != null && network.IsListening) network.SceneManager.OnLoadEventCompleted += Loaded;
    }
    void Loaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (scene != gameObject.scene.name || !network.IsServer) return;
        ready = true;
        foreach (var id in network.ConnectedClientsIds) if (!completed.Contains(id)) ready = false;
    }
    void Update()
    {
        var roster = PartyRoster.Current;
        int index = roster != null ? roster.BriefingGame.Value : 0;
        if (index >= 0 && index < guides.Length && index != shown)
        {
            shown = index; titleText.text = guides[index].title;
            rulesText.text = guides[index].rules; controlsText.text = guides[index].controls;
        }
        // Start the reading period only when every current participant is here and the tickets have cleared.
        if (network != null && network.IsServer && ready && roster != null && !PartyTicketTravel.Busy && roster.BriefingEndsAt.Value < 0)
            roster.BriefingEndsAt.Value = network.ServerTime.Time + readingSeconds;
        double endsAt = roster != null ? roster.BriefingEndsAt.Value : -1;
        int seconds = endsAt < 0 || network == null ? -1 : Mathf.Max(0, Mathf.CeilToInt((float)(endsAt - network.ServerTime.Time)));
        if (seconds != lastSecond)
        {
            lastSecond = seconds;
            countdownText.text = seconds < 0 ? "—" : seconds.ToString("00");
            statusText.text = seconds < 0 ? "참가자의 로딩을 기다리고 있습니다." : "조작법을 확인해 주세요. 잠시 후 자동으로 출발합니다.";
        }
        if (network == null || !network.IsServer || endsAt < 0 || seconds > 0 || leaving || shown < 0) return;
        leaving = true;
        roster.CoverTravelClientRpc();
        PartyTicketTravel.LoadAfterCover(network, guides[shown].scene);
    }
    void OnDestroy() { if (network != null && network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= Loaded; }
}
