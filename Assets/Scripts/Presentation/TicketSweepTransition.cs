using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// An ordinary boarding pass reveals the live form only near the end of the zoom.
public sealed class TicketSweepTransition : MonoBehaviour
{
    public RectTransform pile;
    public RectTransform[] tickets;
    public RectTransform selectedTicket;
    public CanvasGroup veil;
    [Min(.5f)] public float scatterDuration=2.25f;
    [Min(.5f)] public float zoomDuration=2.2f;
    bool running;
    public void Play(string scene)
    {
        if(running)return;
        running=true;
        transform.SetParent(null,false);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Sequence(scene));
    }
    static float Ease(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
    IEnumerator Sequence(string scene)
    {
        veil.blocksRaycasts=true;veil.alpha=1;
        var positions=new Vector2[tickets.Length];
        var angles=new float[tickets.Length];
        var scales=new Vector3[tickets.Length];
        var delays=new float[tickets.Length];
        var durations=new float[tickets.Length];
        var arcs=new float[tickets.Length];
        var random=new System.Random(7301);
        for(int i=0;i<tickets.Length;i++){
            positions[i]=tickets[i].anchoredPosition;angles[i]=Mathf.DeltaAngle(0,tickets[i].localEulerAngles.z);scales[i]=tickets[i].localScale;
            delays[i]=(float)random.NextDouble()*.95f;
            durations[i]=.85f+(float)random.NextDouble()*.45f;
            arcs[i]=((float)random.NextDouble()-.5f)*520;
            tickets[i].anchoredPosition=positions[i]+new Vector2(-3400,arcs[i]);
        }
        float time=0;
        while(time<scatterDuration){
            time+=Time.unscaledDeltaTime;
            for(int i=0;i<tickets.Length;i++){
                float t=Mathf.Clamp01((time-delays[i])/durations[i]);float e=Ease(t);
                tickets[i].anchoredPosition=positions[i]+new Vector2(-3400*(1-e),arcs[i]*(1-e)+Mathf.Sin(t*Mathf.PI)*arcs[i]*.45f);
                tickets[i].localRotation=Quaternion.Euler(0,0,angles[i]+(1-e)*Mathf.Sign(arcs[i])*24);
                tickets[i].localScale=scales[i]*Mathf.Lerp(.92f,1,e);
            }
            yield return null;
        }
        for(int i=0;i<tickets.Length;i++){tickets[i].anchoredPosition=positions[i];tickets[i].localRotation=Quaternion.Euler(0,0,angles[i]);tickets[i].localScale=scales[i];}
        var operation=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
        while(!operation.isDone)yield return null;
        var live=GameObject.Find("Ticket landing group");
        if(live==null){Debug.LogError("[Ticket transition] Destination ticket missing.");Destroy(gameObject);yield break;}
        var target=live.GetComponent<RectTransform>();var destination=target.parent;
        var input=target.GetComponent<CanvasGroup>();input.interactable=false;input.blocksRaycasts=false;input.alpha=0;
        var staticBackground=destination.Find("Enlarged ticket background");if(staticBackground!=null)Destroy(staticBackground.gameObject);
        Vector2 focus=selectedTicket.anchoredPosition;
        float angle=Mathf.DeltaAngle(0,selectedTicket.localEulerAngles.z),size=selectedTicket.localScale.x;
        target.SetParent(pile,false);target.anchoredPosition=focus;target.localRotation=Quaternion.Euler(0,0,angle);target.localScale=Vector3.one*size;target.SetAsLastSibling();
        // Keep the printed boarding pass visible, concealing the form until close-up.
        time=0;
        while(time<zoomDuration){
            time+=Time.unscaledDeltaTime;float e=Ease(time/zoomDuration);
            float rotation=-angle*e,scale=Mathf.Lerp(1,1/size,e);
            pile.localRotation=Quaternion.Euler(0,0,rotation);pile.localScale=Vector3.one*scale;
            pile.anchoredPosition=-(Vector2)(Quaternion.Euler(0,0,rotation)*(Vector3)focus)*scale*e;
            input.alpha=Ease(Mathf.InverseLerp(.68f,.97f,time/zoomDuration));
            yield return null;
        }
        var pilePosition=pile.anchoredPosition;var pileRotation=pile.localRotation;var pileScale=pile.localScale;
        target.SetParent(destination,false);target.anchoredPosition=Vector2.zero;target.localScale=Vector3.one;target.localRotation=Quaternion.identity;
        pile.SetParent(destination,false);pile.name="Enlarged ticket background";pile.anchoredPosition=pilePosition;pile.localRotation=pileRotation;pile.localScale=pileScale;pile.SetSiblingIndex(target.GetSiblingIndex());
        Destroy(selectedTicket.gameObject);
        input.alpha=1;input.interactable=true;input.blocksRaycasts=true;
        Destroy(gameObject);
    }
}
