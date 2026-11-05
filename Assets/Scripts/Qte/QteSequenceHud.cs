using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The hierarchy is authored by the QTE presentation editor; this only updates state.
public sealed class QteSequenceHud : MonoBehaviour
{
    public CanvasGroup group;
    public Image[] cards, accents;
    public TMP_Text[] arrows, letters;
    public TMP_Text timer;
    public Image timeFill;
    private float holdUntil;
    private bool failed;
    private int lastTimerTenth = -1;
    private readonly int[] displayedDirections = { -1, -1, -1, -1 };
    private int lastProgress=-1;
    private static readonly string[] Symbols={"↑","↓","←","→"};
    private static readonly string[] Letters={"W","S","A","D"};
    private static readonly Color Idle=new Color(.095f,.14f,.20f,.96f), Done=new Color(.12f,.65f,.51f), Active=new Color(.96f,.77f,.37f), Miss=new Color(.86f,.24f,.28f);
    void Awake(){group.alpha=0;group.interactable=false;group.blocksRaycasts=false;}
    public void HideImmediately(){holdUntil=0;group.alpha=0;}
    public void Complete(bool success){failed=!success;holdUntil=Time.unscaledTime+.45f;Paint(success?4:lastProgress);}
    public void Present(bool visible,int[] directions,int progress,float seconds,float limit,bool missed)
    {
        bool show=visible||Time.unscaledTime<holdUntil;
        group.alpha=Mathf.MoveTowards(group.alpha,show?1:0,Time.unscaledDeltaTime*10);
        if(!visible)return;
        failed=missed;lastProgress=progress;
        for(int i=0;i<4;i++)
        {
            if (displayedDirections[i] == directions[i]) continue;
            displayedDirections[i] = directions[i];
            arrows[i].text=Symbols[directions[i]];letters[i].text=Letters[directions[i]];
        }
        Paint(progress);
        float remaining = Mathf.Max(0, seconds);
        int timerTenth = Mathf.RoundToInt(remaining * 10f);
        if (timerTenth != lastTimerTenth)
        {
            lastTimerTenth = timerTenth;
            timer.text=remaining.ToString("0.0");
        }
        timer.color=missed?Miss:seconds<.65f?Active:new Color(.92f,.96f,1);
        timeFill.fillAmount=Mathf.Clamp01(seconds/Mathf.Max(.01f,limit));timeFill.color=timer.color;
    }
    void Paint(int progress)
    {
        for(int i=0;i<cards.Length;i++)
        {
            bool done=i<progress,current=i==progress;
            Color color=failed?Miss:done?Done:current?Active:Idle;
            cards[i].color=color;accents[i].color=failed?new Color(1,.62f,.62f):done?new Color(.64f,1,.84f):current?Color.white:new Color(.27f,.37f,.46f);
            arrows[i].color=(!failed&&current)?new Color(.12f,.17f,.22f):Color.white;
            letters[i].color=arrows[i].color*new Color(1,1,1,.7f);
            cards[i].transform.localScale=Vector3.Lerp(cards[i].transform.localScale,Vector3.one*(current&&!failed?1.045f:1f),Time.unscaledDeltaTime*18);
        }
    }
}
