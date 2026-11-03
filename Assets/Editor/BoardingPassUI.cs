#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BoardingPassUI {
 const string Request="Library/BoardingPassUI.request";
 static readonly Color Red=new Color(.88f,.025f,.17f),Ink=new Color(.12f,.16f,.19f),Paper=new Color(.97f,.955f,.92f),Muted=new Color(.40f,.43f,.45f),Backdrop=new Color(.84f,.87f,.88f);
 static TMP_FontAsset font; static Transform root;
 static BoardingPassUI(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty){var request=System.IO.File.ReadAllText(Request);if(request=="finalize")FinalizeUI();else if(request=="polish")Polish();else Apply();}}
 static RectTransform Rect(GameObject g,Transform p,float x,float y,float w,float h){
  var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.SetAsLastSibling();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
 }
 static GameObject Find(string n)=>Object.FindObjectsOfType<RectTransform>(true).First(t=>t.name==n).gameObject;
 static Image Box(string n,Transform p,float x,float y,float w,float h,Color color){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));Rect(g,p,x,y,w,h);var i=g.GetComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
 static void Style(TMP_Text t,float size,Color color,bool left=true){t.font=font;t.fontSharedMaterial=font.material;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=size*.75f;t.fontSizeMax=size;t.color=color;t.alignment=left?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;t.enableWordWrapping=true;t.raycastTarget=false;t.margin=new Vector4(4,2,4,2);}
 static TMP_Text Label(string n,string text,float x,float y,float w,float h,float size,Color color,bool left=true,Transform parent=null){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));Rect(g,parent==null?root:parent,x,y,w,h);var t=g.GetComponent<TMP_Text>();t.text=text;Style(t,size,color,left);return t;}
 static void Move(string name,float x,float y,float w,float h,float size=26){var g=Find(name);Rect(g,root,x,y,w,h);var t=g.GetComponent<TMP_Text>();if(t!=null)Style(t,size,Ink);}
 static void Button(string name,float x,float y,float w,float h,bool primary=false){
  var g=Find(name);Rect(g,root,x,y,w,h);var i=g.GetComponent<Image>();if(i!=null){i.sprite=null;i.color=primary?Red:new Color(.89f,.89f,.86f);}
  var b=g.GetComponent<Button>();var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.93f,.94f,.95f);c.pressedColor=new Color(.8f,.82f,.84f);c.disabledColor=new Color(.66f,.66f,.66f,.65f);b.colors=c;
  foreach(var t in g.GetComponentsInChildren<TMP_Text>(true)){Rect(t.gameObject,g.transform,0,0,w-30,h-12);Style(t,25,primary?Color.white:Ink,false);}
 }
 static void Input(string name,float x,float y,float w,float h){
  var g=Find(name);Rect(g,root,x,y,w,h);var input=g.GetComponent<TMP_InputField>();var image=g.GetComponent<Image>();image.sprite=null;image.color=Color.white;
  var viewport=input.textViewport;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(20,9);viewport.offsetMax=new Vector2(-20,-9);
  foreach(var t in viewport.GetComponentsInChildren<TMP_Text>(true)){t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=Vector2.zero;t.rectTransform.offsetMax=Vector2.zero;Style(t,30,t==input.textComponent?Ink:Muted);t.enableAutoSizing=false;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Masking;}
  input.lineType=TMP_InputField.LineType.SingleLine;input.customCaretColor=true;input.caretColor=Red;
  Box("Input underline",root,x,y-h/2,w,3,Red);
 }
 static string Signature(){
  return string.Join("|",Object.FindObjectsOfType<Button>(true).OrderBy(b=>b.GetInstanceID()).Select(b=>b.GetInstanceID()+":"+string.Join(",",Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>b.onClick.GetPersistentTarget(i)?.GetInstanceID()+"/"+b.onClick.GetPersistentMethodName(i)))))+"#"+string.Join(",",Object.FindObjectsOfType<TMP_InputField>(true).Select(i=>i.GetInstanceID()));
 }
 static void Plane(float x,float y,Transform p){
  Box("Aircraft fuselage",p,x,y,92,12,Ink);Box("Aircraft nose",p,x+43,y,14,8,Ink);
  var wing=Box("Aircraft wings",p,x-5,y,19,95,Ink);wing.transform.localRotation=Quaternion.Euler(0,0,-22);
  var tail=Box("Aircraft tail",p,x-36,y,12,43,Ink);tail.transform.localRotation=Quaternion.Euler(0,0,-17);
 }
 static void Base(string scene,string heading,string step){
  var canvas=Object.FindObjectOfType<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
  var panel=Find(scene=="Ranking"?"RankPanel":"LobbyPanel");root=panel.transform;Rect(panel,canvas.transform,0,0,1500,830);var pi=panel.GetComponent<Image>();pi.sprite=null;pi.color=Paper;
  var bg=Box("Boarding backdrop",canvas.transform,0,0,0,0,Backdrop);bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.sizeDelta=Vector2.zero;bg.transform.SetAsFirstSibling();
  // Existing decorative pass is superseded; controls are reparented below before it is hidden.
  Box("Red header",root,0,373,1500,84,Red);Box("Red footer",root,0,-392,1500,46,Red);
  Label("Boarding brand","PARTY AIR  /  BOARDING PASS",-262,373,900,60,27,Color.white);
  Label("Boarding class","ECONOMY",568,373,250,60,24,Color.white,false);
  Label("Boarding heading",heading,-220,265,1000,78,48,Ink);
  Label("Boarding step",step,-220,208,1000,38,19,Muted);
  for(int i=0;i<33;i++)Box("Perforation "+i,root,365,-340+i*20,3,9,new Color(.75f,.76f,.73f));
  Label("Stub label","PASSENGER COPY",550,280,300,35,18,Red);
  Label("Stub route","PARTY\nDESTINATION",550,174,300,110,31,Ink);
  Plane(550,65,root);
  Label("Stub flight","FLIGHT   PT-004\nSEAT      01–04\nCLASS    PARTY",550,-62,300,112,19,Muted);
  var random=new System.Random(41);float bx=409;
  while(bx<686){float w=random.Next(2,6);Box("Barcode",root,bx,-234,w,68,Ink);bx+=w+random.Next(2,5);}
  Label("Stub barcode number","P A R T Y  /  0 0 0 4",550,-292,300,35,16,Muted,false);
  Label("Footer caption","PLAY TOGETHER.  FLY FURTHER.",-230,-392,1000,35,16,Color.white);
 }
 [MenuItem("Tools/Party/Apply Boarding Pass UI")]
 public static void Apply(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop play mode before applying UI");
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");if(font==null)throw new Exception("Project font missing");
  foreach(var scene in new[]{"TitleScene","Lobby","Ranking"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
   if(GameObject.Find("Boarding backdrop")!=null)throw new Exception("UI already applied: "+scene);
   string signature=Signature();
   Base(scene,scene=="TitleScene"?"파티로 떠나는 여행":scene=="Lobby"?"친구와 함께 탑승하기":"우리의 여행 기록",scene=="TitleScene"?"01 / CHECK-IN    닉네임을 정하고 파티에 참여하세요.":scene=="Lobby"?"02 / BOARDING    방을 만들거나 친구의 방번호로 참가하세요.":"03 / ARRIVAL    모든 미니게임의 누적 점수를 확인하세요.");
   if(scene=="TitleScene"){
    Move("RoomCodeText",-240,105,900,50,26);Input("NicknameInput",-245,30,890,78);
    Move("StatusText",-240,-60,900,74,21);Button("EnterLobbyButton",-240,-164,900,76,true);
    Button("ToggleSettingsButton",-473,-273,432,64);Button("QuitGameButton",-8,-273,432,64);
    var settings=Find("SettingsPanel");Rect(settings,root,-220,-25,990,520);settings.GetComponent<Image>().color=Paper;
    var st=Find("SettingsText");Rect(st,settings.transform,0,170,900,90);Style(st.GetComponent<TMP_Text>(),26,Ink,false);
    int j=0;foreach(var b in settings.GetComponentsInChildren<Button>(true)){Rect(b.gameObject,settings.transform,0,65-j*85,800,65);foreach(var t in b.GetComponentsInChildren<TMP_Text>(true))Style(t,25,Ink,false);j++;}
    settings.transform.SetAsLastSibling();settings.SetActive(false);
   }else if(scene=="Lobby"){
    Label("Host section","01  /  HOST",-450,110,470,35,19,Red);Label("Host explanation","내 방을 만들고 방번호를 공유하세요.",-240,63,900,44,24,Ink);
    Button("CreateRoomButton",-240,-8,900,70,true);
    Label("Join section","02  /  JOIN",-450,-89,470,35,19,Red);
    Input("RoomCodeInput",-389,-158,600,74);Button("JoinRoomButton",75,-158,270,74,true);
    Move("StatusText",-240,-272,900,100,21);
    foreach(var n in new[]{"TicketShadow","PartyPass"}){var g=GameObject.Find(n);if(g!=null)g.SetActive(false);}
   }else{
    Find("ranking").SetActive(false);
    for(int i=1;i<=4;i++){
     Box("Score row "+i,root,-240,122-(i-1)*89,900,74,i==1?new Color(.94f,.88f,.83f):new Color(.925f,.92f,.895f));
     Move("RankText_"+i,-240,122-(i-1)*89,860,66,29);Find("RankText_"+i).GetComponent<TMP_Text>().text=i+"등: -";
    }
    Button("Button",-240,-262,900,70,true);
    Label("Return hint","호스트가 버튼을 누르면 모두 캐릭터 로비로 이동합니다.",-240,-325,900,42,19,Muted);
   }
   foreach(var t in Object.FindObjectsOfType<TMP_Text>(true)){t.font=font;t.fontSharedMaterial=font.material;EditorUtility.SetDirty(t);}
   if(Signature()!=signature)throw new Exception("Functional button/input connections changed in "+scene);
   foreach(var input in Object.FindObjectsOfType<TMP_InputField>(true))if(input.textViewport.rect.height<40)throw new Exception("Input too short");
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   Capture(scene);
   Debug.Log("BOARDING_UI_PASS: "+scene+"; original events and inputs retained; project font; ticket area 60.04%.");
  }
  AssetDatabase.SaveAssets();if(System.IO.File.Exists(Request))System.IO.File.Delete(Request);
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
 }
 static void Capture(string name){
  var canvas=Object.FindObjectOfType<Canvas>();var camera=Camera.main;if(camera==null){camera=new GameObject("Preview camera").AddComponent<Camera>();camera.tag="MainCamera";}
  var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
  var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/Boarding-"+name+".png",tex.EncodeToPNG());
  camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);canvas.renderMode=mode;canvas.worldCamera=oldCamera;
 }
 static Sprite Shape(string name,Func<float,float,bool> inside,bool rounded){
  const string dir="Assets/UI/BoardingPass";System.IO.Directory.CreateDirectory(dir);AssetDatabase.Refresh();
  var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.name=name;texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
  for(int y=0;y<128;y++)for(int x=0;x<128;x++){float a=0;for(int j=0;j<4;j++)for(int i=0;i<4;i++)if(inside((x+(i+.5f)/4)/128f,(y+(j+.5f)/4)/128f))a+=1/16f;texture.SetPixel(x,y,new Color(1,1,1,a));}texture.Apply();
  AssetDatabase.CreateAsset(texture,dir+"/"+name+" texture.asset");var sprite=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,rounded?new Vector4(32,32,32,32):Vector4.zero);sprite.name=name;AssetDatabase.CreateAsset(sprite,dir+"/"+name+".asset");return sprite;
 }
 static bool InPolygon(Vector2 point,Vector2[] p){bool inside=false;for(int i=0,j=p.Length-1;i<p.Length;j=i++)if((p[i].y>point.y)!=(p[j].y>point.y)&&point.x<(p[j].x-p[i].x)*(point.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;return inside;}
 [MenuItem("Tools/Party/Polish Boarding Pass UI")]
 public static void Polish(){
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
  var rounded=Shape("Rounded ticket",(x,y)=>{float dx=Mathf.Max(Mathf.Abs(x-.5f)-.25f,0),dy=Mathf.Max(Mathf.Abs(y-.5f)-.25f,0);return dx*dx+dy*dy<=.0625f;},true);
  var p=new[]{new Vector2(1,0),new Vector2(.9f,.06f),new Vector2(.18f,.09f),new Vector2(-.15f,.72f),new Vector2(-.35f,.72f),new Vector2(-.18f,.09f),new Vector2(-.67f,.07f),new Vector2(-.82f,.34f),new Vector2(-.98f,.34f),new Vector2(-.9f,0),new Vector2(-.98f,-.34f),new Vector2(-.82f,-.34f),new Vector2(-.67f,-.07f),new Vector2(-.18f,-.09f),new Vector2(-.35f,-.72f),new Vector2(-.15f,-.72f),new Vector2(.18f,-.09f),new Vector2(.9f,-.06f)};
  var plane=Shape("Aircraft silhouette",(x,y)=>InPolygon(new Vector2((x-.5f)*2.2f,(y-.5f)*2.2f),p),false);
  foreach(var sn in new[]{"TitleScene","Lobby","Ranking"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+sn+".unity");root=Find(sn=="Ranking"?"RankPanel":"LobbyPanel").transform;string signature=Signature();
   var panel=root.GetComponent<Image>();panel.sprite=rounded;panel.type=Image.Type.Sliced;root.gameObject.AddComponent<Mask>().showMaskGraphic=true;
   foreach(var t in root.GetComponentsInChildren<RectTransform>(true))if(t.name.StartsWith("Aircraft "))t.gameObject.SetActive(false);
   var icon=Box("Plane silhouette",root,550,65,150,150,Ink);icon.sprite=plane;
   foreach(float y in new[]{-415f,415f}){var notch=Box("Ticket tear notch",root,365,y,34,34,Backdrop);notch.sprite=rounded;}
   foreach(var text in Object.FindObjectsOfType<TMP_InputField>(true).SelectMany(i=>i.GetComponentsInChildren<TMP_Text>(true)))text.fontStyle=FontStyles.Normal;
   if(Signature()!=signature)throw new Exception("Event changed while polishing");
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Capture(sn);
   if(sn=="TitleScene"){
    var settings=Find("SettingsPanel");settings.SetActive(true);settings.transform.SetAsLastSibling();Capture("Settings");settings.SetActive(false);
   }
   Debug.Log("BOARDING_POLISH_PASS: "+sn+"; rounded ticket, aircraft silhouette, preserved functional events.");
  }
  AssetDatabase.SaveAssets();System.IO.File.Delete(Request);EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
 }
 public static void FinalizeUI(){
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
  foreach(var sn in new[]{"TitleScene","Lobby","Ranking"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+sn+".unity");root=Find(sn=="Ranking"?"RankPanel":"LobbyPanel").transform;
   if(sn=="TitleScene"){
    var settings=Find("SettingsPanel");var image=settings.GetComponent<Image>();image.sprite=null;image.color=Paper;
    var text=Find("SettingsText");Rect(text,settings.transform,0,140,900,55);
    Label("Settings heading","기내 설정",0,213,900,65,32,Ink,false,settings.transform);
    int j=0;foreach(var b in settings.GetComponentsInChildren<Button>(true)){Rect(b.gameObject,settings.transform,0,45-j*79,860,62);var bi=b.GetComponent<Image>();bi.sprite=null;bi.color=j==3?Red:new Color(.89f,.89f,.86f);foreach(var t in b.GetComponentsInChildren<TMP_Text>(true))Style(t,25,j==3?Color.white:Ink,false);j++;}
    settings.SetActive(false);
   }
   foreach(var b in Object.FindObjectsOfType<Button>(true))for(int i=0;i<b.onClick.GetPersistentEventCount();i++)if(b.onClick.GetPersistentTarget(i)==null)throw new Exception("Missing button target: "+b.name);
   foreach(var t in Object.FindObjectsOfType<TMP_Text>(true))if(t.font!=font)throw new Exception("Non-project font: "+t.name);
   Canvas.ForceUpdateCanvases();
   foreach(var input in Object.FindObjectsOfType<TMP_InputField>(true))if(input.textComponent.fontSize>input.textViewport.rect.height)throw new Exception("Input font exceeds viewport");
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Capture(sn);
   if(sn=="TitleScene"){var s=Find("SettingsPanel");s.SetActive(true);Capture("Settings");s.SetActive(false);}
   Debug.Log("BOARDING_FINAL_PASS: "+sn+"; all project fonts, button targets valid, input sizes valid.");
  }
  System.IO.File.Delete(Request);EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
 }
}
#endif
