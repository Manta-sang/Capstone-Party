#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using CrosswalkRun;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterCabinBuilder {
 const string Request="Library/CharacterCabin.request",Dir="Assets/Materials/CharacterCabin/";
 static TMP_FontAsset font;static Sprite round;static Mesh rounded;
 static Color Red=new Color(.88f,.025f,.17f),Ink=new Color(.13f,.17f,.2f),Paper=new Color(.97f,.96f,.93f);
 static CharacterCabinBuilder(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty){if(System.IO.File.ReadAllText(Request)=="polish")Polish();else Apply();}}
 static Material Mat(string name,Color color,bool glow=false){var m=new Material(Shader.Find("Standard"));m.name=name;m.color=color;m.SetFloat("_Glossiness",.25f);if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.7f);}AssetDatabase.CreateAsset(m,Dir+name+".mat");return m;}
 static GameObject Volume(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool solid=false){
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=rounded;g.GetComponent<Renderer>().sharedMaterial=mat;
  if(solid)g.AddComponent<BoxCollider>();GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);return g;
 }
 static RectTransform Place(GameObject g,Transform p,float x,float y,float w,float h){var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;return r;}
 static Image Panel(string n,Transform p,float x,float y,float w,float h,Color color){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));Place(g,p,x,y,w,h);var image=g.GetComponent<Image>();image.sprite=round;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;return image;}
 static TMP_Text Text(string n,string text,Transform p,float x,float y,float w,float h,float size,Color color,bool left=false){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));Place(g,p,x,y,w,h);var t=g.GetComponent<TMP_Text>();t.text=text;t.font=font;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=size*.8f;t.fontSizeMax=size;t.alignment=left?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center;t.color=color;t.raycastTarget=false;return t;}
 static Button Button(string name,string label,Transform p,float x,float y,float w,float h){var image=Panel(name,p,x,y,w,h,Color.white);image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;var c=b.colors;c.normalColor=Red;c.highlightedColor=new Color(.98f,.12f,.25f);c.pressedColor=new Color(.7f,.02f,.12f);c.disabledColor=new Color(.8f,.81f,.82f);b.colors=c;Text("Label",label,b.transform,0,0,w-24,h-10,26,Color.white);return b;}
 static GameObject Find(string name)=>Object.FindObjectsOfType<Transform>(true).First(t=>t.name==name).gameObject;
 [MenuItem("Tools/Party/Build Character Cabin")]
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");if(GameObject.Find("Aircraft Cabin")!=null)throw new Exception("Cabin already exists");
  System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");round=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Rounded ticket.asset");rounded=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Materials/BlockFall/MagicLibrary/Rounded volume 2.asset");if(font==null||round==null||rounded==null)throw new Exception("Required project visual assets missing");
  var shell=Mat("Ivory fuselage",new Color(.85f,.87f,.86f));var white=Mat("Warm cabin lining",new Color(.95f,.94f,.9f));var dark=Mat("Deep navy carpet",new Color(.12f,.22f,.29f));var seat=Mat("Cabin red upholstery",new Color(.62f,.07f,.16f));var metal=Mat("Brushed aluminium",new Color(.45f,.52f,.57f));var sky=Mat("Window sky",new Color(.48f,.76f,.87f),true);var light=Mat("Warm strip lighting",new Color(1,.84f,.57f),true);var red=Mat("Ticket red accent",Red);
  Find("Ground").SetActive(false);var cabin=new GameObject("Aircraft Cabin").transform;
  var floor=Volume("Walkable cabin floor",cabin,new Vector3(3,-.25f,0),new Vector3(28,.5f,10),dark,true);floor.tag="Ground";
  Volume("Hull underside",cabin,new Vector3(3,-.85f,0),new Vector3(29,1,10.6f),shell);
  Volume("Back cabin wall",cabin,new Vector3(3,2.1f,5),new Vector3(28,4.7f,.35f),white,true);
  Volume("Cutaway front rim",cabin,new Vector3(3,.1f,-5),new Vector3(28,.65f,.4f),shell);
  Volume("Front airline stripe",cabin,new Vector3(3,-.2f,-5.24f),new Vector3(28,.20f,.05f),red);
  Volume("Back lighting strip",cabin,new Vector3(3,4.3f,4.65f),new Vector3(27,.10f,.13f),light);
  Volume("Overhead storage",cabin,new Vector3(3,4.1f,4.35f),new Vector3(27,.6f,1.1f),shell);
  foreach(float x in new[]{-11.2f,17.2f}){
   Volume("Cabin end bulkhead",cabin,new Vector3(x,2,0),new Vector3(.4f,4.6f,10),white,true);
   Volume("Bulkhead red trim",cabin,new Vector3(x,3.65f,0),new Vector3(.45f,.14f,9),red);
  }
  // A segmented nose and tail retain a readable aircraft silhouette without obscuring the cabin.
  for(int i=0;i<6;i++){
   float taper=1-i/7f;
   Volume("Rounded nose segment "+i,cabin,new Vector3(17.5f+i*.55f,-.25f,0),new Vector3(.7f,1.6f*taper,9.8f*taper),shell);
   Volume("Tail cone segment "+i,cabin,new Vector3(-11.5f-i*.55f,-.35f,0),new Vector3(.7f,1.3f*taper,9.8f*taper),shell);
  }
  var fin=Volume("Tail stabilizer",cabin,new Vector3(-12.5f,2.5f,2.8f),new Vector3(2.1f,5.8f,.35f),red);fin.transform.localRotation=Quaternion.Euler(0,0,18);
  for(int i=0;i<10;i++){
   float x=-9+i*2.65f;
   Volume("Window frame "+i,cabin,new Vector3(x,2.75f,4.76f),new Vector3(1.25f,1.55f,.18f),metal);
   Volume("Sky window "+i,cabin,new Vector3(x,2.75f,4.64f),new Vector3(.99f,1.27f,.12f),sky);
   Volume("Storage seam "+i,cabin,new Vector3(x,4.08f,3.76f),new Vector3(.025f,.42f,.025f),metal);
   foreach(float z in new[]{-3.2f,3.15f}){
    var chair=new GameObject("Passenger seat "+i+" "+z).transform;chair.SetParent(cabin,false);chair.localPosition=new Vector3(x,0,z);
    Volume("Seat pedestal",chair,new Vector3(0,.35f,0),new Vector3(.75f,.65f,.8f),metal);
    Volume("Seat cushion",chair,new Vector3(0,.8f,0),new Vector3(1.15f,.25f,1.05f),seat,true);
    Volume("Seat back",chair,new Vector3(0,1.4f,.4f),new Vector3(1.15f,1.25f,.3f),seat);
    Volume("Headrest cover",chair,new Vector3(0,1.78f,.22f),new Vector3(.8f,.38f,.045f),white);
    foreach(float side in new[]{-.62f,.62f})Volume("Armrest",chair,new Vector3(side,1.07f,0),new Vector3(.13f,.15f,1.05f),metal);
   }
  }
  foreach(float z in new[]{-4.75f,4.75f}){var barrier=new GameObject("Cabin boundary").AddComponent<BoxCollider>();barrier.transform.SetParent(cabin,false);barrier.center=new Vector3(3,4,z);barrier.size=new Vector3(28,9,.35f);}
  var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=12.4f;camera.transform.position=new Vector3(3,20,-29);camera.transform.rotation=Quaternion.LookRotation(new Vector3(3,1,0)-camera.transform.position);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.76f,.84f,.88f);
  var sun=Object.FindObjectOfType<Light>();sun.intensity=1.1f;sun.color=new Color(1,.96f,.9f);
  var canvas=Object.FindObjectOfType<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var ui=new GameObject("Character Cabin UI",typeof(RectTransform));Place(ui,canvas.transform,0,0,1920,1080);var ur=ui.GetComponent<RectTransform>();ur.anchorMin=Vector2.zero;ur.anchorMax=Vector2.one;ur.sizeDelta=Vector2.zero;
  var controller=ui.AddComponent<CharacterCabinLobby>();controller.enabled=false;controller.round=Object.FindObjectOfType<CharacterRoundRestart>();controller.round.ParticipantsReady=false;controller.roundedSprite=round;
  var room=Panel("Room number badge",ui.transform,0,468,430,68,Paper);room.rectTransform.anchorMin=room.rectTransform.anchorMax=new Vector2(.5f,1);room.rectTransform.anchoredPosition=new Vector2(0,-60);controller.roomNumber=Text("Room number","ROOM  /  ----",room.transform,0,0,410,58,28,Ink);
  var roster=Panel("Passenger manifest",ui.transform,0,0,300,478,Paper);roster.rectTransform.anchorMin=roster.rectTransform.anchorMax=Vector2.one;roster.rectTransform.anchoredPosition=new Vector2(-178,-270);controller.rosterText=Text("Player list","탑승객  0 / 4",roster.transform,0,0,250,430,23,Ink,true);controller.rosterText.enableAutoSizing=false;controller.rosterText.alignment=TextAlignmentOptions.TopLeft;
  controller.hint=Text("Cabin guidance","이동 WASD · 점프 Space · 참가자들의 준비를 기다려 주세요.",ui.transform,0,0,1200,58,21,Ink);controller.hint.rectTransform.anchorMin=controller.hint.rectTransform.anchorMax=new Vector2(.5f,0);controller.hint.rectTransform.anchoredPosition=new Vector2(-100,47);
  controller.startButton=Button("Host start game","게임 시작",ui.transform,0,0,300,76);var sr=controller.startButton.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new Vector2(1,0);sr.anchoredPosition=new Vector2(-178,62);controller.startButton.interactable=false;UnityEventTools.AddPersistentListener(controller.startButton.onClick,controller.OpenGames);
  controller.readyButton=Button("Passenger ready","준비하기",ui.transform,0,0,300,76);var rc=controller.readyButton.colors;rc.normalColor=Color.white;rc.highlightedColor=Color.white;rc.pressedColor=new Color(.85f,.85f,.85f);controller.readyButton.colors=rc;var rr=controller.readyButton.GetComponent<RectTransform>();rr.anchorMin=rr.anchorMax=new Vector2(1,0);rr.anchoredPosition=new Vector2(-178,62);controller.readyLabel=controller.readyButton.GetComponentInChildren<TMP_Text>();UnityEventTools.AddPersistentListener(controller.readyButton.onClick,controller.ToggleReady);controller.readyButton.gameObject.SetActive(false);
  var selection=Panel("Minigame selection",ui.transform,0,0,800,650,Paper);selection.raycastTarget=true;controller.gameSelection=selection.gameObject;
  Text("Choose game heading","어디로 떠날까요?",selection.transform,0,264,730,64,34,Ink);
  var names=new[]{"StartCrosswalk","StartSawBlade","StartQte","StartTileFlip","BlockFall Button"};var titles=new[]{"횡단보도 달리기","톱날 피하기","QTE","타일 뒤집기","블록폴 · 마법 도서관"};
  for(int i=0;i<names.Length;i++){
   var b=Find(names[i]).GetComponent<Button>();Place(b.gameObject,selection.transform,0,176-i*79,680,64);var image=b.GetComponent<Image>();image.sprite=round;image.type=Image.Type.Sliced;image.color=Color.white;var colors=b.colors;colors.normalColor=Red;colors.disabledColor=new Color(.8f,.81f,.82f);b.colors=colors;
   var t=b.GetComponentInChildren<TMP_Text>();Place(t.gameObject,b.transform,0,0,650,55);t.font=font;t.fontSize=26;t.color=Color.white;t.text=titles[i];
  }
  var close=Button("Close game selection","닫기",selection.transform,0,-244,210,55);UnityEventTools.AddPersistentListener(close.onClick,controller.CloseGames);selection.gameObject.SetActive(false);
  Find("PartyRoomInfo").SetActive(false);Find("LobbyStatus").SetActive(false);var oldView=Object.FindObjectOfType<PartyRoomView>();if(oldView!=null)oldView.enabled=false;
  controller.nameTemplate=Find("PlayerNameTemplate").GetComponent<TMP_Text>();controller.nameTemplate.font=font;controller.nameTemplate.fontSize=22;controller.nameTemplate.color=Color.white;controller.nameTemplate.alignment=TextAlignmentOptions.Center;controller.nameTemplate.gameObject.SetActive(false);
  controller.enabled=true;foreach(var t in Object.FindObjectsOfType<TMP_Text>(true)){t.font=font;t.fontSharedMaterial=font.material;}
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Capture(camera,canvas);
  Debug.Log("CHARACTER_CABIN_PASS: static cutaway aircraft, walkable floor, 20 seats, ten windows, room/roster UI, host start, ready toggle and original five game links.");System.IO.File.Delete(Request);
 }
 static void Capture(Camera camera,Canvas canvas){var mode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/CharacterCabin-preview.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);canvas.renderMode=mode;canvas.worldCamera=null;}
 public static void Polish(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=GameObject.Find("Aircraft Cabin").transform;
  foreach(var t in cabin.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Rounded nose segment")||t.name.StartsWith("Tail cone segment")||t.name=="Smooth tail shell"||t.name=="Smooth nose shell").ToArray())Object.DestroyImmediate(t.gameObject);
  var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Ivory fuselage.mat");
  foreach(float x in new[]{-11.2f,17.2f}){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=x<0?"Smooth tail shell":"Smooth nose shell";g.transform.SetParent(cabin,false);g.transform.localPosition=new Vector3(x,-.9f,0);g.transform.localScale=new Vector3(6.8f,2,10);g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic);}
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.76f,.8f);RenderSettings.ambientIntensity=1;
  var sun=Object.FindObjectOfType<Light>();sun.shadowStrength=.4f;sun.shadows=LightShadows.Soft;
  var controller=Object.FindObjectOfType<CharacterCabinLobby>();
  foreach(var name in new[]{"StartCrosswalk","StartSawBlade","StartQte","StartTileFlip","BlockFall Button"}){
   var button=Find(name).GetComponent<Button>();if(button.onClick.GetPersistentEventCount()!=1||button.onClick.GetPersistentTarget(0)==null)throw new Exception("Game link invalid: "+name);
  }
  if(controller.startButton.onClick.GetPersistentEventCount()!=1||controller.readyButton.onClick.GetPersistentEventCount()!=1)throw new Exception("Start/ready event duplicated");
  var floor=GameObject.Find("Walkable cabin floor").GetComponent<Collider>();foreach(float x in new[]{0,2,4,6})if(!floor.bounds.Contains(new Vector3(x,-.25f,0)))throw new Exception("Spawn is outside cabin floor");
  var fontAsset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");font=fontAsset;foreach(var t in Object.FindObjectsOfType<TMP_Text>(true))if(t.font!=fontAsset)throw new Exception("Wrong font "+t.name);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Capture(Camera.main,Object.FindObjectOfType<Canvas>());
  var snapshot=new System.Collections.Generic.List<GameObject>();
  var spawner=Object.FindObjectOfType<CustomSpawner>();var serialized=new SerializedObject(spawner);var prefabs=serialized.FindProperty("characterPrefabs");
  var canvas=Object.FindObjectOfType<Canvas>();
  for(int i=0;i<4;i++){
   var sample=Object.Instantiate(prefabs.GetArrayElementAtIndex(i%prefabs.arraySize).objectReferenceValue as GameObject);sample.hideFlags=HideFlags.HideAndDontSave;sample.transform.position=new Vector3(i*2,1,0);snapshot.Add(sample);
   var go=new GameObject("Preview name",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.hideFlags=HideFlags.HideAndDontSave;var image=go.GetComponent<Image>();image.sprite=controller.roundedSprite;image.type=Image.Type.Sliced;image.color=new Color(.25f,.27f,.29f,.68f);var pos=Camera.main.WorldToViewportPoint(sample.transform.position+Vector3.up*2.2f);var rect=Place(go,canvas.transform,(pos.x-.5f)*1920,(pos.y-.5f)*1080+(i%2)*42,150,38);Text("Preview nickname","플레이어 "+(i+1),rect,0,0,140,34,22,Color.white);snapshot.Add(go);
  }
  var old=controller.rosterText.text;controller.rosterText.text="탑승객  4 / 4\n\n01  플레이어 1\n      HOST\n\n02  플레이어 2\n      준비 완료\n\n03  플레이어 3\n      준비 완료\n\n04  플레이어 4\n      준비 중";
  Capture(Camera.main,canvas);controller.rosterText.text=old;foreach(var g in snapshot)Object.DestroyImmediate(g);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());System.IO.File.Delete(Request);
  Debug.Log("CHARACTER_CABIN_VALIDATION_PASS: five original game events retained, one start/ready event each, four spawn locations on floor, all project fonts; four-character layout preview captured.");
 }
}
#endif
