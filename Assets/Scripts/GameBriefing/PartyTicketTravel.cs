using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reuses the exact printed passes and arrival trajectories from the title screen.
public sealed class PartyTicketTravel : MonoBehaviour {
 public RectTransform[] tickets;
 public float duration=2.25f;
 static PartyTicketTravel current;
 public static bool Busy => current!=null;
 public static float UntilHalfReveal => current!=null&&current.revealing?Mathf.Max(0,current.duration*.5f-current.revealElapsed):0;
 public static float RemainingReveal => current!=null&&current.revealing?Mathf.Max(0,current.duration-current.revealElapsed):0;
 bool revealing;float revealElapsed;
 Vector2[] positions; Quaternion[] rotations; Vector3[] scales;
 public static void Cover(){
  if(current!=null)return;
  var prefab=Resources.Load<GameObject>("PartyTravelTickets");
  if(prefab!=null){current=Instantiate(prefab).GetComponent<PartyTicketTravel>();current.Begin();}
 }
 void Begin(){DontDestroyOnLoad(gameObject);positions=new Vector2[tickets.Length];rotations=new Quaternion[tickets.Length];scales=new Vector3[tickets.Length];for(int i=0;i<tickets.Length;i++){positions[i]=tickets[i].anchoredPosition;rotations[i]=tickets[i].localRotation;scales[i]=tickets[i].localScale;}SceneManager.sceneLoaded+=Loaded;StartCoroutine(Animate(false));}
 static float Ease(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
 IEnumerator Animate(bool reverse){
  var random=new System.Random(7301);var delays=new float[tickets.Length];var durations=new float[tickets.Length];var arcs=new float[tickets.Length];
  for(int i=0;i<tickets.Length;i++){delays[i]=(float)random.NextDouble()*.95f;durations[i]=.85f+(float)random.NextDouble()*.45f;arcs[i]=((float)random.NextDouble()-.5f)*520;}
  if(reverse)revealing=true;float time=0;while(time<duration){if(reverse)revealElapsed=time;for(int i=0;i<tickets.Length;i++){float t=Mathf.Clamp01((time-delays[i])/durations[i]);float e=reverse?Ease(t):1-Ease(t);tickets[i].anchoredPosition=positions[i]+new Vector2(-3400*e,arcs[i]*e+Mathf.Sin(t*Mathf.PI)*arcs[i]*.45f);tickets[i].localRotation=rotations[i]*Quaternion.Euler(0,0,e*Mathf.Sign(arcs[i])*24);tickets[i].localScale=scales[i]*Mathf.Lerp(1,.92f,e);}time+=Time.unscaledDeltaTime;yield return null;}
  if(reverse)Destroy(gameObject);else for(int i=0;i<tickets.Length;i++){tickets[i].anchoredPosition=positions[i];tickets[i].localRotation=rotations[i];tickets[i].localScale=scales[i];}
 }
 void Loaded(Scene scene,LoadSceneMode mode){StopAllCoroutines();StartCoroutine(Animate(true));}
 public static void LoadAfterCover(NetworkManager network,string scene){if(current==null)Cover();if(current!=null)current.StartCoroutine(current.Load(network,scene));else network.SceneManager.LoadScene(scene,LoadSceneMode.Single);}
 IEnumerator Load(NetworkManager network,string scene){yield return new WaitForSecondsRealtime(duration+.1f);var status=network.SceneManager.LoadScene(scene,LoadSceneMode.Single);if(status!=SceneEventProgressStatus.Started){Debug.LogError("Ticket travel failed: "+status);PartyGameRotation.CancelPending();Destroy(gameObject);}}
 void OnDestroy(){SceneManager.sceneLoaded-=Loaded;if(current==this)current=null;}
}
