#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class IllustratedMenuBuilder {
 const string Request="Library/IllustratedMenu.request",Art="Assets/Textures/TitleScreen/PartySurvivalTitle.png",Dir="Assets/UI/IllustratedTickets/";
 static readonly Color Paper=new Color(.98f,.945f,.86f),Ink=new Color(.28f,.22f,.18f),Orange=new Color(.99f,.61f,.21f),Blue=new Color(.22f,.48f,.56f),SkyTop=new Color(.39f,.72f,.87f),SkyBottom=new Color(.77f,.90f,.94f);
 static Sprite round,cloud;static TMP_FontAsset font;
 static IllustratedMenuBuilder(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty)Apply();}
 static RectTransform Rect(GameObject g,Transform parent,float x,float y,float w,float h){var r=g.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image Image(string name,Transform parent,float x,float y,float w,float h,Color color,Sprite sprite=null){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));Rect(go,parent,x,y,w,h);var image=go.GetComponent<Image>();image.sprite=sprite;image.type=sprite==round?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.color=color;image.raycastTarget=false;return image;}
 static SoftTicketGradient Gradient(Image image,Color bottom,Color? top=null){var effect=image.GetComponent<SoftTicketGradient>();if(effect==null)effect=image.gameObject.AddComponent<SoftTicketGradient>();effect.top=top??Color.white;effect.bottom=bottom;image.SetVerticesDirty();return effect;}
 static TMP_Text Text(string name,string text,Transform parent,float x,float y,float w,float h,int size,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));Rect(go,parent,x,y,w,h);var t=go.GetComponent<TMP_Text>();t.font=font;t.fontSize=size;t.color=color;t.text=text;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
 static Image Frame(RectTransform target,string suffix,float expand,Vector2 offset,Color color){var image=Image(target.name+suffix,target.parent,0,0,1,1,color,round);var r=image.rectTransform;r.anchorMin=target.anchorMin;r.anchorMax=target.anchorMax;r.pivot=target.pivot;r.anchoredPosition=target.anchoredPosition+offset;r.sizeDelta=target.sizeDelta+Vector2.one*expand;r.localScale=target.localScale;image.transform.SetSiblingIndex(target.GetSiblingIndex());return image;}
 static Sprite CloudSprite(){
  var existing=AssetDatabase.LoadAssetAtPath<Sprite>(Dir+"Soft cloud.asset");if(existing!=null)return existing;
  var tex=new Texture2D(128,128,TextureFormat.RGBA32,true){name="Soft cloud shading",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
  for(int y=0;y<128;y++)for(int x=0;x<128;x++){float u=(x+.5f)/64-1,v=(y+.5f)/64-1,r=u*u+v*v;float alpha=Mathf.SmoothStep(0,.06f,1-r);float z=Mathf.Sqrt(Mathf.Max(0,1-r));float light=Mathf.Clamp01((-.35f*u+.55f*v+.75f*z)*.65f+.35f);var c=Color.Lerp(new Color(.77f,.86f,.91f),Color.white,light);c.a=alpha;tex.SetPixel(x,y,c);}tex.Apply();AssetDatabase.CreateAsset(tex,Dir+"Soft cloud texture.asset");var sprite=Sprite.Create(tex,new Rect(0,0,128,128),new Vector2(.5f,.5f));sprite.name="Soft cloud";AssetDatabase.CreateAsset(sprite,Dir+"Soft cloud.asset");return sprite;
 }
 static void Cloud(Transform parent,float x,float y,float scale){var go=new GameObject("Illustrated sky cloud",typeof(RectTransform));Rect(go,parent,x,y,400*scale,150*scale);Image("Cloud base",go.transform,0,-12*scale,340*scale,85*scale,Color.white,cloud);Image("Cloud left puff",go.transform,-95*scale,15*scale,145*scale,125*scale,Color.white,cloud);Image("Cloud middle puff",go.transform,-15*scale,40*scale,175*scale,160*scale,Color.white,cloud);Image("Cloud right puff",go.transform,90*scale,15*scale,150*scale,120*scale,Color.white,cloud);}
 static string Signature()=>string.Join("|",Object.FindObjectsOfType<Button>(true).OrderBy(b=>b.name).ThenBy(b=>b.transform.parent.name).Select(b=>b.name+":"+string.Join(",",Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>b.onClick.GetPersistentTarget(i)?.GetType().FullName+"/"+b.onClick.GetPersistentMethodName(i)))))+"#"+string.Join(",",Object.FindObjectsOfType<TMP_InputField>(true).Select(i=>i.name+"/"+i.characterLimit));
 static GameObject Find(string name)=>Object.FindObjectsOfType<RectTransform>(true).First(t=>t.name==name).gameObject;
 static void Theme(string scene){
  EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");string before=Signature();var canvas=Object.FindObjectOfType<Canvas>();var panel=Find(scene=="Ranking"?"RankPanel":"LobbyPanel").GetComponent<Image>();var rect=panel.rectTransform;Vector2 oldSize=rect.sizeDelta;
  if(GameObject.Find("Soft ticket outer outline")!=null)throw new Exception("Ticket theme already applied");
  var backdrop=Find("Boarding backdrop").GetComponent<Image>();backdrop.color=Color.white;Gradient(backdrop,SkyBottom,SkyTop);
  var sky=new GameObject("Soft sky illustrations",typeof(RectTransform));var skyRect=Rect(sky,canvas.transform,0,0,1920,1080);skyRect.SetSiblingIndex(backdrop.transform.GetSiblingIndex()+1);
  Cloud(sky.transform,-805,400,.95f);Cloud(sky.transform,805,355,1.10f);Cloud(sky.transform,-830,-365,.9f);Cloud(sky.transform,800,-375,1.15f);
  Frame(rect," soft shadow",30,new Vector2(0,-20),new Color(.16f,.32f,.39f,.12f));var border=Frame(rect," outline",10,new Vector2(0,-2),new Color(.43f,.31f,.21f));border.name="Soft ticket outer outline";
  panel.color=Paper;Gradient(panel,new Color(.97f,.94f,.89f));
  foreach(var image in panel.GetComponentsInChildren<Image>(true)){
   string n=image.name;
   if(n=="Red header"||n=="Red footer"){image.color=Orange;Gradient(image,new Color(.93f,.82f,.66f));}
   else if(n.StartsWith("Perforation"))image.color=new Color(.64f,.53f,.39f,.55f);
   else if(n=="Barcode")image.color=Ink;
   else if(n=="Plane silhouette")image.color=Blue;
   else if(n=="Input underline")image.color=new Color(.80f,.51f,.23f);
   else if(n.StartsWith("Score row")){image.sprite=round;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=3;image.color=n.EndsWith("1")?new Color(1,.87f,.59f):new Color(.94f,.89f,.77f);Gradient(image,new Color(.97f,.94f,.88f));}
   else if(n=="Ticket tear notch")image.color=Color.Lerp(SkyBottom,SkyTop,image.rectTransform.anchoredPosition.y>0?.884f:.116f);
   else if(n=="SettingsPanel"){image.color=Paper;Gradient(image,new Color(.97f,.94f,.89f));}
  }
  foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true)){t.font=font;t.fontSharedMaterial=font.material;t.color=t.name.Contains("step")||t.name.Contains("hint")||t.name.Contains("flight")?new Color(.36f,.42f,.43f):t.name.Contains("section")||t.name=="Stub label"?new Color(.68f,.34f,.13f):Ink;}
  foreach(var button in panel.GetComponentsInChildren<Button>(true)){
   var image=button.targetGraphic as Image;if(image==null)continue;bool primary=button.name=="EnterLobbyButton"||button.name=="CreateRoomButton"||button.name=="JoinRoomButton"||scene=="Ranking"||button.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="닫기");
   Frame(image.rectTransform," depth",5,new Vector2(0,-7),new Color(.48f,.33f,.21f,.42f));Frame(image.rectTransform," rim",5,new Vector2(0,-1),new Color(.48f,.33f,.21f));image.sprite=round;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=2.7f;image.color=primary?Orange:new Color(.86f,.92f,.91f);Gradient(image,new Color(.91f,.87f,.79f));
   var colors=button.colors;colors.normalColor=colors.selectedColor=colors.highlightedColor=colors.pressedColor=Color.white;colors.disabledColor=new Color(.8f,.81f,.82f);button.colors=colors;
   foreach(var t in button.GetComponentsInChildren<TMP_Text>(true))t.color=Ink;
   var highlight=Image("Soft top edge highlight",button.transform,0,0,1,3,new Color(1,1,1,.40f),round);highlight.rectTransform.anchorMin=new Vector2(0,1);highlight.rectTransform.anchorMax=new Vector2(1,1);highlight.rectTransform.pivot=new Vector2(.5f,1);highlight.rectTransform.anchoredPosition=new Vector2(0,-6);highlight.rectTransform.sizeDelta=new Vector2(-22,3);
  }
  foreach(var input in panel.GetComponentsInChildren<TMP_InputField>(true)){
   var image=input.GetComponent<Image>();Frame(image.rectTransform," recessed rim",6,Vector2.zero,new Color(.67f,.52f,.35f));image.sprite=round;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;image.color=new Color(1,.982f,.93f);Gradient(image,new Color(1,1,1),new Color(.96f,.94f,.90f));
   input.caretColor=Ink;foreach(var t in input.GetComponentsInChildren<TMP_Text>(true))t.color=t==input.textComponent?Ink:new Color(.49f,.46f,.39f);
  }
  if(before!=Signature()||rect.sizeDelta!=oldSize)throw new Exception("Menu layout or functionality changed in "+scene);
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture(scene);
  Debug.Log("ILLUSTRATED_MENU_PASS: "+scene+"; existing button events, input rules and ticket dimensions preserved; project font, soft cream/orange/sky palette.");
 }
 static void Splash(){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=5.4f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.66f,.81f,.88f);
  var root=new GameObject("Title Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var image=Image("Original title illustration",root.transform,0,0,1920,1080,Color.white,AssetDatabase.LoadAssetAtPath<Sprite>(Art));image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.sizeDelta=Vector2.zero;image.preserveAspect=true;
  var prompt=Text("Press any key","PRESS ANY KEY TO START",root.transform,0,0,900,64,34,new Color(1,.97f,.86f));prompt.rectTransform.anchorMin=prompt.rectTransform.anchorMax=new Vector2(.5f,0);prompt.rectTransform.anchoredPosition=new Vector2(0,72);prompt.characterSpacing=1;
  var material=new Material(font.material){name="Title prompt outline"};material.SetFloat("_OutlineWidth",.22f);material.SetColor("_OutlineColor",Ink);material.EnableKeyword("UNDERLAY_ON");material.SetColor("_UnderlayColor",new Color(.22f,.17f,.13f,.7f));material.SetFloat("_UnderlayOffsetY",-.16f);material.SetFloat("_UnderlaySoftness",.15f);string path=Dir+"Title prompt outline.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old==null)AssetDatabase.CreateAsset(material,path);else{EditorUtility.CopySerialized(material,old);Object.DestroyImmediate(material);material=old;}prompt.fontSharedMaterial=material;
  var group=prompt.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;var splash=root.AddComponent<PartySplash>();var so=new SerializedObject(splash);so.FindProperty("prompt").objectReferenceValue=group;so.FindProperty("nicknameScene").stringValue="NicknameScene";so.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.SaveScene(scene,"Assets/Scenes/TitleScene.unity");Capture("TitleScene");
 }
 [MenuItem("Tools/Party/Apply Illustrated Title And Tickets")]
 public static void Apply(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit play mode before applying menu scenes");
  System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");round=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Rounded ticket.asset");if(font==null||round==null)throw new Exception("Project font/ticket sprite missing");cloud=CloudSprite();
  if(!AssetDatabase.CopyAsset("Assets/Scenes/TitleScene.unity","Assets/Scenes/NicknameScene.unity"))throw new Exception("Cannot create NicknameScene; refusing to overwrite an existing scene");
  var importer=(TextureImporter)AssetImporter.GetAtPath(Art);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
  foreach(var scene in new[]{"NicknameScene","Lobby","Ranking"})Theme(scene);
  Splash();
  var builds=EditorBuildSettings.scenes.ToList();int index=builds.FindIndex(s=>s.path=="Assets/Scenes/TitleScene.unity");if(!builds.Any(s=>s.path=="Assets/Scenes/NicknameScene.unity"))builds.Insert(index+1,new EditorBuildSettingsScene("Assets/Scenes/NicknameScene.unity",true));EditorBuildSettings.scenes=builds.ToArray();
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/NicknameScene.unity");var title=Object.FindObjectOfType<PartyTitle>();if(title==null)throw new Exception("Nickname controller missing");var controller=new SerializedObject(title);foreach(var field in new[]{"nickname","message","settingsText","settingsPanel"})if(controller.FindProperty(field).objectReferenceValue==null)throw new Exception("Nickname function missing "+field);if(controller.FindProperty("nextScene").stringValue!="Lobby")throw new Exception("Nickname route changed");
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");var art=GameObject.Find("Original title illustration").GetComponent<Image>();if(art.color!=Color.white||!art.preserveAspect||art.sprite.texture.width!=1376||art.sprite.texture.height!=768)throw new Exception("Original title illustration altered");
  if(Object.FindObjectOfType<PartySplash>()==null||Object.FindObjectOfType<PartyTitle>()!=null)throw new Exception("Splash and nickname scenes not separated");
  System.IO.File.Delete(Request);Debug.Log("ILLUSTRATED_TITLE_PASS: original 1376x768 image, aspect preserved, any-key splash -> NicknameScene -> Lobby; input/settings/login references and Ranking continuation retained.");
 }
 static void Capture(string name){var canvas=Object.FindObjectOfType<Canvas>();var camera=Camera.main;var mode=canvas.renderMode;var old=canvas.worldCamera;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/Illustrated-"+name+".png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);canvas.renderMode=mode;canvas.worldCamera=old;}
}
#endif
