using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class PushCooldownHUD : MonoBehaviour
{
    public CanvasGroup visibility;
    public Image progressRing;
    public RectTransform clockMarker;
    public Graphic gesture;
    public Color readyColor=new Color(.45f,.91f,.75f);
    public Color rechargingColor=new Color(1,.67f,.29f);
    NetworkObject observed;
    PlayerInteraction interaction;

    void Update()
    {
        var network=NetworkManager.Singleton;
        var player=network!=null&&network.IsListening&&network.LocalClient!=null?network.LocalClient.PlayerObject:null;
        if(player!=observed){observed=player;interaction=player!=null?player.GetComponent<PlayerInteraction>():null;}
        bool bound=interaction!=null&&interaction.IsSpawned&&interaction.IsOwner;
        visibility.alpha=!bound?0:interaction.isActiveAndEnabled?1:.35f;
        if(!bound)return;
        float progress=interaction.PushCooldownProgress;
        progressRing.fillAmount=progress;
        progressRing.color=progress>=1?readyColor:rechargingColor;
        clockMarker.localRotation=Quaternion.Euler(0,0,-360*progress);
        gesture.color=progress>=1?Color.white:new Color(.75f,.79f,.82f);
    }
}
