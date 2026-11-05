#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TicketPresentationValidation {
 const string Request="Library/TicketValidation.request",Key="Party.TicketValidation";
 static int stage;static double at;static float low=1,high;static bool scatterCaptured,zoomCaptured;
 static TicketPresentationValidation(){EditorApplication.delayCall+=Begin;EditorApplication.update+=Tick;}
 static void Begin(){
  if(EditorApplication.isPlaying||!System.IO.File.Exists(Request)||System.IO.File.Exists("Library/TicketPile.request"))return;
  if(EditorSceneManager.GetActiveScene().isDirty)return;
  scatterCaptured=zoomCaptured=false;low=1;high=0;
  foreach(var name in new[]{"NicknameScene","Lobby","Ranking","Character","TitleScene"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
   if(name=="NicknameScene"||name=="Lobby"||name=="Ranking"){
    var background=GameObject.Find("Enlarged ticket background");
    if(background==null||background.transform.childCount!=42)throw new Exception("Shared boarding-pass background missing in "+name);
    if(background.GetComponentsInChildren<TMP_Text>(true).Count(t=>t.name=="Origin airport")!=42)throw new Exception("Detailed airport passes missing in "+name);
   }
   if(name=="NicknameScene"){
    if(Object.FindObjectsOfType<Button>(true).Any(b=>b.name=="QuitGameButton"||b.name=="ToggleSettingsButton"))throw new Exception("Nickname buttons remain");
    var title=Object.FindObjectOfType<PartyTitle>();var so=new SerializedObject(title);foreach(var f in new[]{"nickname","message"})if(so.FindProperty(f).objectReferenceValue==null)throw new Exception("Nickname binding missing");
   }
   if(name=="Lobby"&&GameObject.Find("BackToNicknameButton")==null)throw new Exception("Lobby back missing");
   if(name=="Character"&&Object.FindObjectOfType<CharacterCabinLobby>().leaveButton==null)throw new Exception("Room exit missing");
   Capture("Library/Ticket-"+name+".png");
  }
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");SessionState.SetBool(Key,true);System.IO.File.Delete(Request);EditorApplication.isPlaying=true;
 }
 static void Tick(){
  if(System.IO.File.Exists(Request)&&!System.IO.File.Exists("Library/TicketPile.request")&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling){Begin();return;}
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  try{
   double now=EditorApplication.timeSinceStartup;
   if(stage==0){at=now;stage=1;}
   if(stage==1){var prompt=GameObject.Find("Press any key");if(prompt!=null){float a=prompt.GetComponent<CanvasGroup>().alpha;low=Mathf.Min(low,a);high=Mathf.Max(high,a);}if(now-at<2.2)return;if(high-low<.6f)throw new Exception("Prompt did not blink");Object.FindObjectOfType<PartySplash>().EnterNickname();at=now;stage=2;}
   else if(stage==2){if(!scatterCaptured && now-at>2.05){Capture("Library/Ticket-Pile-covered.png");scatterCaptured=true;}if(!zoomCaptured && now-at>3.65){Capture("Library/Ticket-Pile-zoom.png");zoomCaptured=true;}if(now-at<6.5)return;if(SceneManager.GetActiveScene().name!="NicknameScene")throw new Exception("Transition did not enter nickname");if(Object.FindObjectOfType<TicketSweepTransition>()!=null)throw new Exception("Transition canvas was not released");if(GameObject.Find("Enlarged ticket background")==null)throw new Exception("Enlarged background did not remain");var group=GameObject.Find("Ticket landing group").GetComponent<RectTransform>();if(group.localScale!=Vector3.one||group.localRotation!=Quaternion.identity||!group.GetComponent<CanvasGroup>().interactable)throw new Exception("Landing/input not restored");Capture("Library/Ticket-Nickname-landed.png");Debug.Log("TICKET_RUNTIME_TRANSITION_PASS: prompt alpha "+low+".."+high+"; sweep completed, nickname landing restored and input unlocked.");SceneManager.LoadScene("Lobby");at=now;stage=3;}
   else if(stage==3){if(now-at<1)return;Object.FindObjectOfType<LobbyUI>().BackToNickname();at=now;stage=4;}
   else if(stage==4){if(now-at<.7)return;if(SceneManager.GetActiveScene().name!="NicknameScene"||NetworkManager.Singleton!=null)throw new Exception("Lobby back left stale network");SceneManager.LoadScene("Lobby");at=now;stage=5;}
   else if(stage==5){if(now-at<.7)return;var n=NetworkManager.Singleton;n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7777);if(!n.StartHost())throw new Exception("Local host failed");at=now;stage=6;}
   else if(stage==6){if(now-at<.8)return;Object.FindObjectOfType<LobbyRelayManager>().LeaveRoom();at=now;stage=7;}
   else if(stage==7){if(now-at<1.5)return;if(SceneManager.GetActiveScene().name!="Lobby"||Object.FindObjectsOfType<NetworkManager>().Length!=1||NetworkManager.Singleton.IsListening)throw new Exception("Room return did not reset network manager");Debug.Log("TICKET_NAVIGATION_PASS: lobby back -> nickname -> lobby; local host shutdown -> fresh lobby, one idle NetworkManager.");Finish();}
  }catch(Exception ex){Debug.LogException(ex);Finish();}
 }
 static void Finish(){SessionState.SetBool(Key,false);stage=0;EditorApplication.isPlaying=false;}
 static void Capture(string path){
  if(path.EndsWith("Ticket-Pile-covered.png")){
   var transition=Object.FindObjectOfType<TicketSweepTransition>();
   if(transition==null||transition.selectedTicket.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("닉네임"))||transition.selectedTicket.GetComponentsInChildren<TMP_InputField>(true).Length>0)
    throw new Exception("The selected boarding pass exposes nickname UI before close-up");
  }
  var cam=Camera.main;if(cam==null)return;var canvases=Object.FindObjectsOfType<Canvas>();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
  for(int j=0;j<canvases.Length;j++)if(modes[j]==RenderMode.ScreenSpaceOverlay){canvases[j].renderMode=RenderMode.ScreenSpaceCamera;canvases[j].worldCamera=cam;canvases[j].planeDistance=1;}
  Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(1920,1080,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1920,1080),0,0);t.Apply();System.IO.File.WriteAllBytes(path,t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);
  for(int j=0;j<canvases.Length;j++){canvases[j].renderMode=modes[j];canvases[j].worldCamera=cameras[j];canvases[j].planeDistance=distances[j];}
 }
}
#endif
// Verify pile continuity and retained enlarged background.
