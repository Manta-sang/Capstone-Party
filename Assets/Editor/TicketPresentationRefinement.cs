#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TicketPresentationRefinement {
 const string Request="Library/TicketPresentation.request";
 static Sprite round;static TMP_FontAsset font;
 static readonly Color Ink=new Color(.30f,.23f,.17f),Paper=new Color(.99f,.955f,.87f),Orange=new Color(1,.62f,.24f);
 static TicketPresentationRefinement(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty)Apply();}
 static RectTransform Rect(string n,Transform p,float x,float y,float w,float h){var g=new GameObject(n,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image Img(string n,Transform p,float x,float y,float w,float h,Color c,bool rounded=true){var r=Rect(n,p,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.color=c;i.raycastTarget=false;if(rounded){i.sprite=round;i.type=Image.Type.Sliced;i.pixelsPerUnitMultiplier=3;}return i;}
 static TMP_Text Text(string n,string s,Transform p,float x,float y,float w,float h,int size,Color c){var r=Rect(n,p,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=s;t.fontSize=size;t.color=c;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
 static Button Button(string n,string label,Transform p,float x,float y,float w,float h){var i=Img(n,p,x,y,w,h,Orange);i.raycastTarget=true;var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var c=b.colors;c.highlightedColor=c.selectedColor=new Color(1,1,1,.95f);b.colors=c;Text("Label",label,i.transform,0,0,w-24,h-12,26,Ink);return b;}
 static void Bevel(Button b){var i=b.targetGraphic as Image;if(i==null)return;
  foreach(string suffix in new[]{" rim"," depth"}){var old=b.transform.parent.Find(b.name+suffix);if(old!=null)Object.DestroyImmediate(old.gameObject);}
  var shadow=b.GetComponent<Shadow>();if(shadow==null)shadow=b.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.34f,.23f,.13f,.24f);shadow.effectDistance=new Vector2(0,-5);shadow.useGraphicAlpha=true;
  var outline=b.GetComponent<Outline>();if(outline==null)outline=b.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.53f,.36f,.20f,.8f);outline.effectDistance=new Vector2(1.3f,-1.3f);
  var gradient=b.GetComponent<SoftTicketGradient>();if(gradient==null)gradient=b.gameObject.AddComponent<SoftTicketGradient>();gradient.top=new Color(1,.99f,.94f);gradient.bottom=new Color(.91f,.85f,.76f);
  var high=b.transform.Find("Soft top edge highlight");if(high!=null)Object.DestroyImmediate(high.gameObject);
  var r=b.GetComponent<RectTransform>();var line=Img("Inset satin highlight",b.transform,0,r.sizeDelta.y*.5f-5,r.sizeDelta.x-28,2,new Color(1,1,1,.48f));
  foreach(var t in b.GetComponentsInChildren<TMP_Text>(true))t.color=Ink;
 }
 static void Polish(string scene){
  EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");var panel=Object.FindObjectsOfType<Image>(true).First(i=>i.name==(scene=="Ranking"?"RankPanel":"LobbyPanel"));
  // Embossed punched holes replace flat rectangular dashes.
  foreach(var dash in panel.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("Perforation")).ToArray()){
   dash.sprite=round;dash.type=Image.Type.Sliced;dash.pixelsPerUnitMultiplier=12;dash.rectTransform.sizeDelta=new Vector2(3,8);dash.color=new Color(.60f,.48f,.33f,.45f);
   var inset=Img("Perforation bevel",dash.transform,.6f,1,1,5,new Color(1,1,1,.8f));
  }
  Img("Tear seam shadow",panel.transform,361,-2,1,650,new Color(.64f,.48f,.29f,.13f),false).transform.SetSiblingIndex(2);
  Img("Tear seam paper edge",panel.transform,369,0,1,650,new Color(1,1,1,.65f),false).transform.SetSiblingIndex(3);
  // Keep rounded input borders aligned with the native sliced input shape.
  foreach(var input in panel.GetComponentsInChildren<TMP_InputField>(true)){var edge=input.transform.parent.Find(input.name+" recessed rim");if(edge!=null)edge.GetComponent<Image>().pixelsPerUnitMultiplier=input.GetComponent<Image>().pixelsPerUnitMultiplier;}
  if(scene=="NicknameScene"){
   foreach(var button in panel.GetComponentsInChildren<Button>(true).Where(b=>b.name=="ToggleSettingsButton"||b.name=="QuitGameButton").ToArray()){
    foreach(string suffix in new[]{" rim"," depth"}){var d=button.transform.parent.Find(button.name+suffix);if(d!=null)Object.DestroyImmediate(d.gameObject);}Object.DestroyImmediate(button.gameObject);
   }
   var settings=panel.transform.Find("SettingsPanel");if(settings!=null)Object.DestroyImmediate(settings.gameObject);
   var start=panel.GetComponentsInChildren<Button>(true).First(b=>b.name=="EnterLobbyButton");start.GetComponent<RectTransform>().anchoredPosition=new Vector2(-240,-190);start.GetComponent<RectTransform>().sizeDelta=new Vector2(900,88);
   Text("Boarding invitation","YOUR ADVENTURE STARTS HERE",panel.transform,-240,-295,900,30,18,new Color(.65f,.42f,.23f));
   Text("Boarding invitation detail","닉네임을 정하고 친구들과 함께 탑승하세요.",panel.transform,-240,-330,900,32,20,new Color(.42f,.44f,.42f));
  }
  if(scene=="Lobby"){
   var ui=Object.FindObjectOfType<LobbyUI>();var back=Button("BackToNicknameButton","이전으로",panel.transform,80,-300,230,60);UnityEventTools.AddPersistentListener(back.onClick,ui.BackToNickname);var so=new SerializedObject(ui);so.FindProperty("backButton").objectReferenceValue=back;so.ApplyModifiedPropertiesWithoutUndo();
   var status=so.FindProperty("statusText").objectReferenceValue as TMP_Text;status.rectTransform.anchoredPosition=new Vector2(-390,-280);status.rectTransform.sizeDelta=new Vector2(620,100);
  }
  foreach(var b in panel.GetComponentsInChildren<Button>(true))Bevel(b);
  // Move the complete paper, its outline and its shadow together when landing.
  var group=Rect("Ticket landing group",panel.transform.parent,0,0,1920,1080);group.gameObject.AddComponent<CanvasGroup>();
  foreach(var n in new[]{panel.name+" soft shadow","Soft ticket outer outline",panel.name}){var t=group.parent.Find(n);if(t!=null)t.SetParent(group,false);}
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  Debug.Log("TICKET_REFINEMENT_PASS: "+scene+"; saved editable ticket details and navigation.");
 }
 static void Transition(){
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");var splash=Object.FindObjectOfType<PartySplash>();var root=new GameObject("Flying ticket transition",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup),typeof(TicketSweepTransition));
  var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var transition=root.GetComponent<TicketSweepTransition>();transition.veil=root.GetComponent<CanvasGroup>();transition.veil.alpha=0;transition.veil.blocksRaycasts=false;transition.tickets=new RectTransform[16];
  for(int row=0;row<4;row++)for(int col=0;col<4;col++){
   int index=row*4+col;var paper=Img("Flying boarding pass "+index,root.transform,(col-1.5f)*620,(row-1.5f)*355,850,520,Paper);var r=paper.rectTransform;r.localRotation=Quaternion.Euler(0,0,((index*17)%9)-4);transition.tickets[index]=r;
   var shadow=paper.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.25f,.17f,.10f,.22f);shadow.effectDistance=new Vector2(-8,-12);
   Img("Orange header",r,0,208,850,86,index%3==0?new Color(.98f,.72f,.31f):Orange);
   Text("Airline","PARTY AIR  /  BOARDING PASS",r,-65,208,630,70,23,Ink);
   Text("Destination","PARTY   >   ADVENTURE",r,-45,40,700,100,37,Ink);
   Text("Flight","FLIGHT  PT-004     SEAT  01-04",r,-65,-95,600,50,21,Ink);
   for(int j=0;j<14;j++)Img("Tear hole",r,265,-180+j*26,3,10,new Color(.58f,.45f,.31f,.5f));
   for(int j=0;j<22;j++)Img("Barcode",r,-330+j*17,-187,j%3==0?7:3,44,Ink,false);
  }
  var so=new SerializedObject(splash);so.FindProperty("transition").objectReferenceValue=transition;so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
 }
 static void Cabin(){EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=Object.FindObjectOfType<CharacterCabinLobby>();var start=cabin.startButton.GetComponent<RectTransform>();var button=Button("LeaveRoomButton","방 나가기",start.parent,0,0,240,76);var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=start.anchorMin;rect.anchoredPosition=start.anchoredPosition+Vector2.left*292;UnityEventTools.AddPersistentListener(button.onClick,cabin.LeaveRoom);cabin.leaveButton=button;Bevel(button);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());}
 [MenuItem("Tools/Party/Refine Ticket Presentation")]
 public static void Apply(){
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");round=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Rounded ticket.asset");
  foreach(var s in new[]{"NicknameScene","Lobby","Ranking"})Polish(s);Transition();Cabin();
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");System.IO.File.Delete(Request);Debug.Log("TICKET_PRESENTATION_PASS: blink, sweep, landing, nickname simplification, lobby back and room exit bindings saved.");
 }
}
#endif
