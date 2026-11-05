using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public sealed class GameStartPresentation : MonoBehaviour
{
    public const float CountdownSeconds=3f;
    public static float ScheduledStartDelay => CountdownSeconds + PartyTicketTravel.UntilHalfReveal;
    public TMP_Text countdownText;
    public TMP_Text tileTimer;
    public CrosswalkRun.CrosswalkNetworkEntry crosswalk;
    public SD_GameManager blade;
    public TF_GameManager tile;
    public QteSceneController qte;
    public BlockFall.BlockFallGame block;
    static GameStartPresentation current;
    int lastTileSeconds=-1;
    Canvas presentationCanvas;int ordinaryOrder;
    public static bool HasSharedCountdown => current!=null;
    public static bool BlocksGameplayInput => current!=null&&!current.HasStarted;
    public bool HasStarted => crosswalk!=null?crosswalk.HasRoundStarted:blade!=null?blade.HasRoundStarted:tile!=null?tile.HasRoundStarted:qte!=null?qte.HasRoundStarted:block!=null&&block.HasRoundStarted;
    float Remaining => crosswalk!=null?crosswalk.CountdownRemaining:blade!=null?blade.CountdownRemaining:tile!=null?tile.CountdownRemaining:qte!=null?qte.CountdownRemaining:block!=null?block.CountdownRemaining:0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetState(){current=null;}
    void Awake(){current=this;presentationCanvas=GetComponent<Canvas>();if(presentationCanvas!=null)ordinaryOrder=presentationCanvas.sortingOrder;}
    void Update(){
        float remain=Remaining;
        if(presentationCanvas!=null)presentationCanvas.sortingOrder=!HasStarted&&PartyTicketTravel.UntilHalfReveal<=0?31001:ordinaryOrder;
        countdownText.gameObject.SetActive(!HasStarted && PartyTicketTravel.UntilHalfReveal<=0);
        if(!HasStarted){countdownText.fontSize=remain>0?150:32;countdownText.text=remain>0?Mathf.Clamp(Mathf.CeilToInt(remain),1,(int)CountdownSeconds).ToString():qte!=null?string.Empty:"참가자 준비 중...";}
        if(tileTimer!=null&&tile!=null){int seconds=Mathf.CeilToInt(tile.RemainingTime);if(seconds!=lastTileSeconds){lastTileSeconds=seconds;tileTimer.text=Mathf.Clamp(seconds,0,99).ToString("00");}}
    }
    void OnDestroy(){if(current==this)current=null;}
}
