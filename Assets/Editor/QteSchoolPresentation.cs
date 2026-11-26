#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QteSchoolPresentation
{
 const string Folder="Assets/Materials/QteGenerated/";
 static QteSchoolPresentation(){EditorApplication.update+=()=>{if(!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Library/QteSchoolPresentation.request")){File.Delete("Library/QteSchoolPresentation.request");try{Build();}catch(Exception ex){File.WriteAllText("Library/QteSchoolPresentation.result","FAIL: "+ex);Debug.LogException(ex);}}};}
 [MenuItem("Party/Refine QTE School and HUD")]
 public static void Build()
 {
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
  var root=GameObject.Find("QTE School");if(!root)root=new GameObject("QTE School");
  Transform Group(string n){var t=root.transform.Find(n);if(t)return t;var g=new GameObject(n).transform;g.SetParent(root.transform,false);return g;}
  Transform structure=Group("01 Buildings"),windows=Group("02 Windows"),entrance=Group("03 Entrance and garden"),roof=Group("04 Roof and drainage"),signs=Group("05 Signs and clock"),details=Group("06 Architectural details");
  foreach(var child in details.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
  foreach(var t in Object.FindObjectsOfType<Transform>(true).ToArray())
  {
   if(t==root.transform||t.IsChildOf(root.transform)||t.parent!=null)continue;
   string n=t.name;bool school=n.StartsWith("School ")&&n!="School ground"&&n!="School Daylight";
   if(!school&&!n.StartsWith("Main hall ")&&!n.StartsWith("West wing ")&&!n.StartsWith("East wing ")&&!n.StartsWith("Window "))continue;
   Transform target=n.Contains("window",StringComparison.OrdinalIgnoreCase)||n.StartsWith("Window ")?windows:n.Contains("roof",StringComparison.OrdinalIgnoreCase)||n.Contains("eave")||n.Contains("drain")?roof:n.Contains("sign")||n.Contains("clock")||n.Contains("banner")||n.Contains("pennant")?signs:n.Contains("entrance")||n.Contains("door")||n.Contains("step")?entrance:structure;
   t.SetParent(target,true);
  }
  foreach(var section in new[]{structure,windows})
  {
   Transform Wing(string n){var t=section.Find(n);if(t)return t;t=new GameObject(n).transform;t.SetParent(section,false);return t;}
   var main=Wing("Main hall");var west=Wing("West classrooms");var east=Wing("East classrooms");
   foreach(var t in section.Cast<Transform>().Where(t=>t!=main&&t!=west&&t!=east).ToArray())t.SetParent(t.position.x< -18?west:t.position.x>18?east:main,true);
  }
  var oldDetails=GameObject.Find("School facade details");if(oldDetails&&oldDetails.transform.parent!=root.transform)oldDetails.transform.SetParent(details,true);
  var trim=Material("Limestone",new Color(.68f,.66f,.58f));var metal=Material("Roof_zinc",new Color(.28f,.34f,.35f),.5f);var clay=Material("Roof_tile",new Color(.39f,.22f,.17f));var brick=Material("Brick_base",new Color(.66f,.46f,.33f));
  brick.mainTexture=SurfaceTexture();brick.mainTextureScale=new Vector2(12,2);EditorUtility.SetDirty(brick);
  foreach(float wing in new[]{-30f,0f,30f})
  {
   float width=wing==0?34:23,front=wing==0?26.6f:27.4f;
   Box(details,"Masonry foundation",new Vector3(wing,.72f,front),new Vector3(width,1.4f,.32f),brick);
   Box(details,"Stone water table",new Vector3(wing,1.46f,front-.06f),new Vector3(width+.2f,.14f,.45f),trim);
   float height=wing==0?20.15f:16.15f,rise=wing==0?3.1f:2.3f,depth=wing==0?14:12.5f;
   for(float x=wing-width*.5f;x<=wing+width*.5f;x+=.72f)
   {
    Rod(details,new Vector3(x,height+.35f,34-depth*.5f),new Vector3(x,height+rise+.35f,34),.055f,clay);
    Rod(details,new Vector3(x,height+rise+.24f,34),new Vector3(x,height+.24f,34+depth*.5f),.055f,clay);
   }
   for(int row=1;row<=8;row++)
   {
    float z=34-depth*.5f+depth*.5f*row/8f,y=height+rise*row/8f+.35f;
    Rod(details,new Vector3(wing-width*.5f,y,z),new Vector3(wing+width*.5f,y,z),.042f,clay);
   }
   for(float x=wing-width*.5f+.8f;x<wing+width*.5f;x+=5.2f)
   {Box(details,"Roof ridge tile cap",new Vector3(x,height+rise+.25f,34),new Vector3(4.9f,.21f,.46f),clay);}
  }
  foreach(var t in windows.GetComponentsInChildren<Transform>().Where(t=>t.name=="School classroom window").ToArray())
  {
   var b=t.GetComponent<Renderer>().bounds;
   Box(details,"Recessed window lintel shadow",b.center+new Vector3(0,b.size.y*.5f+.13f,-.09f),new Vector3(b.size.x+.3f,.12f,.45f),trim);
   // Small projecting sills and dark recessed reveals give the facade depth at runner height.
   foreach(float dx in new[]{-.5f,.5f})Box(details,"Window stone reveal",b.center+new Vector3(dx*(b.size.x+.24f),0,-.04f),new Vector3(.1f,b.size.y+.26f,.24f),trim);
  }
  foreach(float x in new[]{-1.05f,1.05f})Box(details,"Entrance glazed door frame",new Vector3(x,2.4f,24.15f),new Vector3(.07f,3.8f,.09f),metal);
  foreach(float x in new[]{-41.1f,41.1f})
  {Rod(details,new Vector3(x,.25f,27.1f),new Vector3(x,15.9f,27.1f),.17f,metal);for(float y=1;y<15;y+=3)Box(details,"Rain pipe bracket",new Vector3(x,y,27.02f),new Vector3(.34f,.09f,.3f),metal);}
  QteSchoolGeometry.Refine(root.transform,details);
  typeof(QteEnvironmentRefinement).GetMethod("Combine",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{details});
  BuildHud();
  foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==null)throw new Exception("Missing school material");
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Directory.CreateDirectory("Library/QteSchoolPresentation");Capture("Library/QteSchoolPresentation/school.png");
  var hud=Object.FindObjectOfType<QteSequenceHud>();hud.group.alpha=1;hud.Present(true,new[]{0,2,1,3},2,1.4f,2,false);hud.group.alpha=1;Canvas.ForceUpdateCanvases();Capture("Library/QteSchoolPresentation/hud.png");
  var camera=Camera.main;camera.transform.position=new Vector3(13,9,8);camera.transform.LookAt(new Vector3(0,6,27));Capture("Library/QteSchoolPresentation/entrance.png");
  File.WriteAllText("Library/QteSchoolPresentation.result","PASS: grouped school hierarchy, architectural meshes/materials and four-key HUD saved.");
  EditorSceneManager.OpenScene("Assets/Scenes/GameBriefing.unity");
  var guide=Object.FindObjectOfType<GameBriefing>();
  foreach(var entry in guide.guides)if(entry.scene=="Qte")
  {
   entry.rules="트랙을 자동으로 달립니다. 허들 앞에서 표시된 키 4개를 순서대로 입력하세요.\n성공하면 점프하며 잠시 빨라지고, 실패하면 허들에 걸려 넘어집니다.\n먼저 완주한 순서로 순위를 정하며, 첫 완주 후 5초가 지나면 남은 주자는 이동 거리로 순위를 정합니다.";
   entry.controls="방향키 또는 WASD: ↑ W / ↓ S / ← A / → D\n두 조작을 섞어서 입력할 수 있습니다.\n금색: 다음 키 · 청록색: 입력 완료 · 빨간색: 실패\n숫자와 게이지는 현재 허들의 입력 제한시간입니다. 밀치기는 사용할 수 없습니다.";
  }
  EditorUtility.SetDirty(guide);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
 }
 static void Capture(string path){var countdown=GameObject.Find("Round Start Canvas");bool shown=countdown!=null&&countdown.activeSelf;if(countdown)countdown.SetActive(false);try{typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{path});}finally{if(countdown)countdown.SetActive(shown);}}
 static Material Material(string name,Color color,float smooth=.15f){string path=Folder+"School_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Glossiness",smooth);EditorUtility.SetDirty(m);return m;}
 static Texture2D SurfaceTexture()
 {
  string path=Folder+"School_brick_texture.asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex)return tex;
  const int size=128;tex=new Texture2D(size,size,TextureFormat.RGB24,true){name="Weathered masonry",wrapMode=TextureWrapMode.Repeat};var pixels=new Color[size*size];
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){int row=y/16;bool mortar=y%16<1||(x+(row%2)*16)%32<1;float noise=Mathf.PerlinNoise(x*.31f,y*.31f)*.12f;float shade=mortar?.76f:.84f+noise;pixels[y*size+x]=new Color(shade,shade,shade);}
  tex.SetPixels(pixels);tex.Apply();AssetDatabase.CreateAsset(tex,path);return tex;
 }
 static GameObject Box(Transform parent,string name,Vector3 at,Vector3 size,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
 static void Rod(Transform parent,Vector3 a,Vector3 b,float width,Material material){var g=Box(parent,"Roof ceramic seam",(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material);g.transform.rotation=Quaternion.LookRotation(b-a);}
 static Sprite Rounded()
 {
  string path=Folder+"QTE_Hud_round.asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite)return sprite;const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name="QTE HUD rounded mask",filterMode=FilterMode.Bilinear};var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-19.5f),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-19.5f);pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(12-Mathf.Sqrt(dx*dx+dy*dy)));}tex.SetPixels(pixels);tex.Apply();AssetDatabase.CreateAsset(tex,Folder+"QTE_Hud_round_texture.asset");sprite=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(15,15,15,15));AssetDatabase.CreateAsset(sprite,path);return sprite;
 }
 static RectTransform Rect(Transform p,string n,Vector2 pos,Vector2 size){var t=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(p,false);t.anchorMin=t.anchorMax=t.pivot=Vector2.one*.5f;t.anchoredPosition=pos;t.sizeDelta=size;return t;}
 static Image Panel(Transform p,string n,Vector2 pos,Vector2 size,Color color,Sprite sprite){var t=Rect(p,n,pos,size);var image=t.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;return image;}
 static TMP_Text Text(Transform p,string n,string content,Vector2 pos,Vector2 size,float fontSize){var t=Rect(p,n,pos,size);var label=t.gameObject.AddComponent<TextMeshProUGUI>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");label.text=content;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;label.color=Color.white;label.raycastTarget=false;return label;}
 static void BuildHud()
 {
  foreach(string name in new[]{"QTE Race HUD","QTE Sequence HUD"}){var old=GameObject.Find(name);if(old)Object.DestroyImmediate(old);}
  var g=new GameObject("QTE Sequence HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup),typeof(QteSequenceHud));g.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;g.GetComponent<Canvas>().sortingOrder=30;var scaler=g.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var hud=g.GetComponent<QteSequenceHud>();hud.group=g.GetComponent<CanvasGroup>();hud.group.alpha=0;hud.group.blocksRaycasts=false;hud.cards=new Image[4];hud.accents=new Image[4];hud.arrows=new TMP_Text[4];hud.letters=new TMP_Text[4];var sprite=Rounded();
  var content=Rect(g.transform,"Key sequence and deadline",new Vector2(0,-338),new Vector2(620,160));
  for(int i=0;i<4;i++)
  {
   float x=-177+i*118;Panel(content,"Soft key shadow "+i,new Vector2(x,-6),new Vector2(104,112),new Color(.025f,.04f,.07f,.38f),sprite);
   var card=Panel(content,"Key "+(i+1),new Vector2(x,0),new Vector2(104,112),new Color(.095f,.14f,.2f,.96f),sprite);hud.cards[i]=card;
   hud.accents[i]=Panel(card.transform,"Input state accent",new Vector2(0,-47),new Vector2(44,3),new Color(.27f,.37f,.46f),sprite);
   hud.arrows[i]=Text(card.transform,"Direction",new[]{"↑","↓","←","→"}[i],new Vector2(0,12),new Vector2(90,66),49);
   hud.letters[i]=Text(card.transform,"WASD alias",new[]{"W","S","A","D"}[i],new Vector2(0,-27),new Vector2(70,27),18);
  }
  var deadline=Panel(content,"Deadline badge",new Vector2(0,86),new Vector2(108,36),new Color(.055f,.09f,.14f,.94f),sprite);hud.timer=Text(deadline.transform,"Seconds remaining","2.0",Vector2.zero,new Vector2(90,34),24);
  Panel(content,"Time track",new Vector2(0,-68),new Vector2(450,3),new Color(.07f,.11f,.16f,.75f),sprite);hud.timeFill=Panel(content,"Time remaining",new Vector2(0,-68),new Vector2(450,3),new Color(.92f,.96f,1),sprite);hud.timeFill.type=Image.Type.Filled;hud.timeFill.fillMethod=Image.FillMethod.Horizontal;hud.timeFill.fillOrigin=0;
  EditorUtility.SetDirty(hud);
 }
}
#endif

