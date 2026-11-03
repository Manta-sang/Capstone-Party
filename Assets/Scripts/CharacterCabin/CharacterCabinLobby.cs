using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using CrosswalkRun;

public sealed class CharacterCabinLobby : MonoBehaviour {
 public TMP_Text roomNumber, rosterText, hint, nameTemplate;
 public Button startButton, readyButton;
 public TMP_Text readyLabel;
 public GameObject gameSelection;
 public CharacterRoundRestart round;
 [Header("버튼 표시 On / Off")]
 public bool showGameStartButton=true;
 public bool showReadyButton=true;
 public Sprite roundedSprite;
 readonly Dictionary<ulong,RectTransform> labels=new Dictionary<ulong,RectTransform>();
 readonly Dictionary<ulong,NetworkObject> players=new Dictionary<ulong,NetworkObject>();
 readonly List<ulong> stale=new List<ulong>();
 readonly List<Rect> nameBounds=new List<Rect>(4);
 PartyRoster roster;
 Canvas canvas;
 Camera sceneCamera;
 bool initialized;
 float refreshAt;
 string lastRoster="";
 void Awake(){canvas=GetComponentInParent<Canvas>();sceneCamera=Camera.main;round.ParticipantsReady=false;gameSelection.SetActive(false);}
 void Bind(){
  if(roster!=null)roster.Members.OnListChanged-=Changed;
  roster=PartyRoster.Current;
  if(roster!=null){roster.Members.OnListChanged+=Changed;if(roster.IsServer&&!initialized){initialized=true;roster.ResetReady();}}
  Refresh();
 }
 void Changed(NetworkListEvent<PartyRoster.Member> change){Refresh();}
 void Update(){
  if(roster!=PartyRoster.Current)Bind();
  if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.25f;Refresh();}
  var n=NetworkManager.Singleton;bool online=n!=null&&n.IsListening,host=online&&n.IsServer;
  startButton.gameObject.SetActive(showGameStartButton&&host);
  readyButton.gameObject.SetActive(showReadyButton&&online&&!host);
  startButton.interactable=host&&round.CanStart;
  readyButton.interactable=online&&roster!=null&&n.LocalClient!=null&&n.LocalClient.PlayerObject!=null;
  if(gameSelection.activeSelf&&(!host||!round.CanStart))gameSelection.SetActive(false);
 }
 void Refresh(){
  var n=NetworkManager.Singleton;bool online=n!=null&&n.IsListening;
  roomNumber.text="ROOM  /  "+(string.IsNullOrEmpty(LobbyRelayManager.RoomNumber)?"----":LobbyRelayManager.RoomNumber);
  int count=roster==null?0:roster.Members.Count;bool all=online&&n.IsServer&&count==n.ConnectedClientsIds.Count&&count>0,localReady=false;
  string text="탑승객  "+count+" / 4\n";stale.Clear();foreach(var id in labels.Keys)stale.Add(id);
  for(int i=0;i<count;i++){
   var m=roster.Members[i];bool host=m.Id==NetworkManager.ServerClientId;
   if(showReadyButton&&!host&&!m.Ready)all=false;
   if(online&&m.Id==n.LocalClientId)localReady=m.Ready;
   text+="\n"+(i+1).ToString("00")+"  "+m.Name+"\n      "+(host?"HOST":m.Ready?"준비 완료":"준비 중")+"\n";
   if(!labels.ContainsKey(m.Id)){
    var go=new GameObject("Nickname "+m.Id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var rt=go.GetComponent<RectTransform>();rt.SetParent(canvas.transform,false);rt.sizeDelta=new Vector2(180,42);
    var image=go.GetComponent<Image>();image.sprite=roundedSprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;image.color=new Color(.25f,.27f,.29f,.68f);image.raycastTarget=false;
    var label=Instantiate(nameTemplate,rt);label.name="Nickname";label.gameObject.SetActive(true);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(12,3);label.rectTransform.offsetMax=new Vector2(-12,-3);labels.Add(m.Id,rt);
   }
   var tag=labels[m.Id].GetComponentInChildren<TMP_Text>(true);tag.text=m.Name.ToString();
   labels[m.Id].sizeDelta=new Vector2(Mathf.Clamp(tag.GetPreferredValues(tag.text).x+28,100,224),38);stale.Remove(m.Id);
  }
  foreach(var id in stale){Destroy(labels[id].gameObject);labels.Remove(id);players.Remove(id);}
  if(text!=lastRoster){rosterText.text=text;lastRoster=text;}
  round.ParticipantsReady=all;
  readyLabel.text=localReady?"준비 완료  ·  취소":"준비하기";
  readyButton.GetComponent<Image>().color=localReady?new Color(.88f,.025f,.17f):new Color(.78f,.79f,.80f);
  hint.text=!online?"방에 참가하면 기내에서 함께 기다릴 수 있습니다.":n.IsServer?all?"게임 시작을 누르면 무작위 게임으로 출발합니다.":"참가자들의 준비와 캐릭터 로딩을 기다리는 중입니다.":localReady?"준비 완료! 다음 게임을 기다리며 자유롭게 움직이세요.":"이동 WASD · 점프 Space · 준비하기를 눌러 탑승을 완료하세요.";
  if(online){players.Clear();foreach(var p in n.SpawnManager.SpawnedObjectsList)if(p.IsPlayerObject)players[p.OwnerClientId]=p;}
 }
 void LateUpdate(){
  if(sceneCamera==null)sceneCamera=Camera.main;if(sceneCamera==null)return;
  nameBounds.Clear();
  foreach(var pair in labels){
   bool visible=players.TryGetValue(pair.Key,out var player)&&player!=null;
   var point=visible?sceneCamera.WorldToScreenPoint(player.transform.position+Vector3.up*2.2f):Vector3.back;
   pair.Value.gameObject.SetActive(visible&&point.z>0);
   if(visible&&point.z>0&&RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform,point,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var pos)){
    var size=pair.Value.sizeDelta;
    var bounds=new Rect(pos-size*.5f,size);
    for(int attempt=0;attempt<8;attempt++){
     bool overlaps=false;foreach(var used in nameBounds)if(bounds.Overlaps(used)){overlaps=true;break;}
     if(!overlaps)break;pos.y+=42;bounds.y+=42;
    }
    nameBounds.Add(bounds);pair.Value.anchoredPosition=pos;
   }
  }
 }
 void FixedUpdate(){
  foreach(var player in players.Values){
   if(player==null||!player.IsOwner)continue;
   var body=player.GetComponent<Rigidbody>();if(body==null)continue;
   var p=body.position;var corrected=new Vector3(Mathf.Clamp(p.x,-10f,16f),p.y,Mathf.Clamp(p.z,-4f,4f));
   if(p.y<-.5f)corrected.y=1;
   if(corrected!=p){body.position=corrected;var v=body.velocity;if(corrected.x!=p.x)v.x=0;if(corrected.z!=p.z)v.z=0;if(corrected.y!=p.y)v.y=0;body.velocity=v;}
  }
 }
 public void OpenGames(){if(round.CanStart&&NetworkManager.Singleton.IsServer)round.StartRandomGame();}
 public void CloseGames(){gameSelection.SetActive(false);}
 public void ToggleReady(){
  if(roster==null||NetworkManager.Singleton==null)return;
  foreach(var member in roster.Members)if(member.Id==NetworkManager.Singleton.LocalClientId){roster.SetReadyServerRpc(!member.Ready);return;}
 }
 void OnDestroy(){if(roster!=null)roster.Members.OnListChanged-=Changed;}
}
