#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterCabinRefit {
 const string Request="Library/CharacterCabinRefit.request",Dir="Assets/Materials/CharacterCabin/Interior/";
 static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
 static Material ivory,metal,navy,fabric,piping,linen,carpet,glass,light,dark;
 static TMP_FontAsset font;
 static CharacterCabinRefit(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty){if(System.IO.File.ReadAllText(Request)=="clearWindows")ClearWindows();else Apply();}}
 static T Save<T>(T asset,string path)where T:Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(asset,path);return asset;}EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);return old;}
 static Material Material(string name,Color color,float metallic=0,float smooth=.3f,bool emissive=false){var m=new Material(Shader.Find("Standard"));m.name=name;m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.25f);}return Save(m,Dir+name+".mat");}
 static Texture2D Weave(string name,bool window=false){var tex=new Texture2D(128,128,TextureFormat.RGBA32,true);tex.name=name;tex.wrapMode=TextureWrapMode.Repeat;var rng=new System.Random(114);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float n=(float)rng.NextDouble();Color c;if(window)c=Color.Lerp(new Color(.80f,.89f,.91f),new Color(.34f,.62f,.77f),Mathf.SmoothStep(0,1,y/127f));else{float value=.84f+.10f*n+((x+y)%4==0?.04f:0);c=new Color(value,value,value);}tex.SetPixel(x,y,c);}tex.Apply();return Save(tex,Dir+name+".asset");}
 static float Half(Vector3 size,Vector3 axis)=>Vector3.Dot(size*.5f,new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)));
 static Mesh Rounded(Vector3 size,float radius){
  radius=Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f);
  string key="Round "+size.ToString("F3")+" "+radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);if(meshes.TryGetValue(key,out var cached))return cached;
  var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var half=size*.5f;var inner=half-Vector3.one*radius;
  foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
   var u=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(normal,u);float hu=Half(size,u),hv=Half(size,v);int start=vertices.Count;
   var us=Coordinates(hu,radius);var vs=Coordinates(hv,radius);
   for(int y=0;y<vs.Length;y++)for(int x=0;x<us.Length;x++){
    var p=normal*Half(size,normal)+u*us[x]+v*vs[y];var clamped=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var n=(p-clamped).normalized;
    vertices.Add(clamped+n*radius);normals.Add(n);uv.Add(new Vector2(us[x]/size.x,vs[y]/size.y));
   }
   for(int y=0;y<vs.Length-1;y++)for(int x=0;x<us.Length-1;x++){int a=start+y*us.Length+x,b=a+1,c=a+us.Length,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}
  }
  var mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh=Save(mesh,Dir+key+".asset");meshes[key]=mesh;return mesh;
 }
 static float[] Coordinates(float h,float r)=>new[]{-h,-h+r*.08f,-h+r*.29f,-h+r*.62f,-h+r,0,h-r,h-r*.62f,h-r*.29f,h-r*.08f,h};
 static GameObject Part(string name,Transform parent,Mesh mesh,Vector3 position,Material material){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=position;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);return g;}
 static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,float radius,Material mat,bool solid=false){var g=Part(name,p,Rounded(size,radius),pos,mat);if(solid)g.AddComponent<BoxCollider>();return g;}
 static GameObject Plate(string name,Transform p,Vector3 pos,float w,float h,float depth,float radius,Material mat){float sourceDepth=Mathf.Max(depth,radius*2.5f);var g=Box(name,p,pos,new Vector3(w,h,sourceDepth),radius,mat);g.transform.localScale=new Vector3(1,1,depth/sourceDepth);return g;}
 static Vector2 Oval(float a,float w,float h){float c=Mathf.Cos(a),s=Mathf.Sin(a);return new Vector2(Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.75f)*w,Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.75f)*h);}
 static Mesh Ring(string name,float w,float h,float rim,float depth,bool disk=false){
  if(meshes.TryGetValue(name,out var cached))return cached;
  const int segments=64;var v=new List<Vector3>();var t=new List<int>();
  for(int i=0;i<segments;i++){
   float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;var outerA=Oval(a,w,h);var outerB=Oval(b,w,h);var innerA=Oval(a,w-rim,h-rim);var innerB=Oval(b,w-rim,h-rim);
   if(disk){int k=v.Count;v.Add(Vector3.zero);v.Add(new Vector3(outerB.x,outerB.y,0));v.Add(new Vector3(outerA.x,outerA.y,0));t.AddRange(new[]{k,k+1,k+2});continue;}
   Quad(v,t,new Vector3(outerA.x,outerA.y,-depth/2),new Vector3(innerA.x,innerA.y,-depth/2),new Vector3(innerB.x,innerB.y,-depth/2),new Vector3(outerB.x,outerB.y,-depth/2));
   Quad(v,t,new Vector3(outerB.x,outerB.y,depth/2),new Vector3(innerB.x,innerB.y,depth/2),new Vector3(innerA.x,innerA.y,depth/2),new Vector3(outerA.x,outerA.y,depth/2));
   Quad(v,t,new Vector3(outerA.x,outerA.y,depth/2),new Vector3(outerA.x,outerA.y,-depth/2),new Vector3(outerB.x,outerB.y,-depth/2),new Vector3(outerB.x,outerB.y,depth/2));
   Quad(v,t,new Vector3(innerA.x,innerA.y,-depth/2),new Vector3(innerA.x,innerA.y,depth/2),new Vector3(innerB.x,innerB.y,depth/2),new Vector3(innerB.x,innerB.y,-depth/2));
  }
  var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var uv=new Vector2[v.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(v[i].x/(w*2)+.5f,v[i].y/(h*2)+.5f);mesh.uv=uv;mesh=Save(mesh,Dir+name+".asset");meshes[name]=mesh;return mesh;
 }
 static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d){int k=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
 static void Pipe(string name,Transform parent,List<Vector3> points,float radius,Material mat){
  var v=new List<Vector3>();var t=new List<int>();const int sides=8;
  for(int i=0;i<points.Count;i++){var tangent=(points[(i+1)%points.Count]-points[(i-1+points.Count)%points.Count]).normalized;var u=Vector3.Cross(tangent,Vector3.forward).normalized;if(u.sqrMagnitude<.1f)u=Vector3.right;var normal=Vector3.Cross(tangent,u);for(int j=0;j<sides;j++)v.Add(points[i]+radius*(u*Mathf.Cos(j*2*Mathf.PI/sides)+normal*Mathf.Sin(j*2*Mathf.PI/sides)));}
  for(int i=0;i<points.Count;i++)for(int j=0;j<sides;j++){int a=i*sides+j,b=i*sides+(j+1)%sides,c=((i+1)%points.Count)*sides+j,d=((i+1)%points.Count)*sides+(j+1)%sides;t.AddRange(new[]{a,c,b,b,c,d});}
  var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();Part(name,parent,Save(mesh,Dir+name+".asset"),Vector3.zero,mat);
 }
 static void CombineSeat(Transform root){
  var groups=new Dictionary<Material,List<CombineInstance>>();foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){var m=mf.GetComponent<Renderer>().sharedMaterial;if(!groups.ContainsKey(m))groups.Add(m,new List<CombineInstance>());groups[m].Add(new CombineInstance{mesh=mf.sharedMesh,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});}
  foreach(Transform child in root.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
  foreach(var pair in groups){var m=new Mesh{name="Seat "+pair.Key.name,indexFormat=IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray());Part(m.name,root,Save(m,Dir+m.name+".asset"),Vector3.zero,pair.Key);}
  var bottom=root.gameObject.AddComponent<BoxCollider>();bottom.center=new Vector3(0,.68f,-.05f);bottom.size=new Vector3(1.2f,.3f,1.15f);var back=root.gameObject.AddComponent<BoxCollider>();back.center=new Vector3(0,1.35f,.42f);back.size=new Vector3(1.2f,1.45f,.35f);
 }
 static GameObject Seat(){
  var root=new GameObject("Detailed passenger seat").transform;
  foreach(float x in new[]{-.42f,.42f})Box("Aluminium leg",root,new Vector3(x,.30f,.12f),new Vector3(.10f,.58f,.63f),.045f,metal);
  Box("Underseat storage",root,new Vector3(0,.27f,-.03f),new Vector3(.75f,.12f,.7f),.05f,navy);
  Box("Cushion shell",root,new Vector3(0,.60f,-.05f),new Vector3(1.2f,.23f,1.16f),.10f,navy);
  Box("Contoured cushion",root,new Vector3(0,.76f,-.11f),new Vector3(1.10f,.28f,1.08f),.125f,fabric);
  var back=new GameObject("Reclined seat back").transform;back.SetParent(root,false);back.localPosition=new Vector3(0,.72f,.35f);back.localRotation=Quaternion.Euler(7,0,0);
  Box("Back shell",back,new Vector3(0,.65f,.06f),new Vector3(1.2f,1.48f,.29f),.13f,navy);
  Box("Padded backrest",back,new Vector3(0,.65f,-.13f),new Vector3(1.1f,1.38f,.24f),.115f,fabric);
  Box("Lumbar support",back,new Vector3(0,.25f,-.27f),new Vector3(.94f,.38f,.21f),.09f,fabric);
  Box("Headrest",back,new Vector3(0,1.15f,-.24f),new Vector3(1.02f,.43f,.27f),.12f,fabric);
  foreach(float x in new[]{-.46f,.46f}){var wing=Box("Headrest side wing",back,new Vector3(x,1.15f,-.34f),new Vector3(.20f,.44f,.35f),.085f,fabric);wing.transform.localRotation=Quaternion.Euler(0,-Mathf.Sign(x)*18,0);}
  Plate("Linen antimacassar",back,new Vector3(0,1.16f,-.39f),.70f,.34f,.035f,.06f,linen);
  var outline=new List<Vector3>();for(int i=0;i<64;i++){var p=Oval(i*2*Mathf.PI/64,.52f,.63f);outline.Add(new Vector3(p.x,p.y+.65f,-.255f));}Pipe("Upholstery edge piping",back,outline,.012f,piping);
  Plate("Folded tray frame",back,new Vector3(0,.62f,.23f),.92f,.62f,.065f,.10f,ivory);
  Plate("Tray inset",back,new Vector3(0,.62f,.272f),.80f,.48f,.025f,.07f,linen);
  Box("Tray release latch",back,new Vector3(0,.97f,.26f),new Vector3(.20f,.055f,.06f),.022f,metal);
  Plate("Seatback literature pocket",back,new Vector3(0,.17f,.24f),.88f,.26f,.06f,.06f,navy);
  Plate("Safety card",back,new Vector3(.12f,.28f,.28f),.33f,.16f,.012f,.015f,linen);
  foreach(float x in new[]{-.65f,.65f}){Box("Armrest support",root,new Vector3(x,.85f,.14f),new Vector3(.08f,.35f,.15f),.035f,metal);Box("Soft armrest",root,new Vector3(x,1.03f,-.10f),new Vector3(.16f,.15f,1.08f),.07f,navy);}
  foreach(float x in new[]{-.28f,.28f}){var belt=Box("Seatbelt webbing",root,new Vector3(x,.915f,-.14f),new Vector3(.47f,.025f,.10f),.01f,navy);belt.transform.localRotation=Quaternion.Euler(0,x<0?-18:18,0);}
  Box("Brushed belt buckle",root,new Vector3(0,.937f,-.14f),new Vector3(.15f,.035f,.13f),.015f,metal);
  CombineSeat(root);System.IO.Directory.CreateDirectory("Assets/Prefabs/CharacterCabin");var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Prefabs/CharacterCabin/Detailed passenger seat.prefab");Object.DestroyImmediate(root.gameObject);return prefab;
 }
 static Mesh Shell(){
  var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=24;i++){float a=i/24f*Mathf.PI*.5f;float y=2.15f+4*Mathf.Sin(a),z=4.45f+.65f*Mathf.Cos(a);v.Add(new Vector3(-12,y,z));v.Add(new Vector3(18,y,z));}
  for(int i=0;i<24;i++)t.AddRange(new[]{i*2,i*2+2,i*2+1,i*2+1,i*2+2,i*2+3});var mesh=new Mesh{name="Curved cabin liner"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return Save(mesh,Dir+mesh.name+".asset");
 }
 static void Sign(string text,Transform p,Vector3 pos,float size,Color color){var g=new GameObject("Cabin sign "+text,typeof(TextMeshPro));g.transform.SetParent(p,false);g.transform.localPosition=pos;var t=g.GetComponent<TextMeshPro>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(2.7f,.4f);t.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
 [MenuItem("Tools/Party/Refit Character Aircraft Interior")]
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();meshes.Clear();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
  ivory=Material("Pearl cabin panels",new Color(.84f,.85f,.81f),.08f,.42f);metal=Material("Satin aluminium",new Color(.51f,.58f,.63f),.78f,.58f);navy=Material("Graphite seat shells",new Color(.10f,.15f,.19f),.12f,.36f);fabric=Material("Woven burgundy upholstery",new Color(.57f,.045f,.12f),0,.22f);fabric.mainTexture=Weave("Upholstery weave");fabric.mainTextureScale=new Vector2(4,4);piping=Material("Burgundy stitching",new Color(.76f,.12f,.21f));linen=Material("Warm linen",new Color(.94f,.93f,.87f),0,.24f);carpet=Material("Woven midnight carpet",new Color(.09f,.16f,.22f),0,.15f);carpet.mainTexture=Weave("Carpet weave");carpet.mainTextureScale=new Vector2(24,8);glass=Material("Daylight window glass",Color.white,.05f,.9f);glass.mainTexture=Weave("Sky gradient",true);light=Material("Warm cabin light",new Color(1,.90f,.69f),0,.5f,true);dark=Material("Window recess and vents",new Color(.055f,.09f,.12f),.3f,.4f);
  var old=GameObject.Find("Aircraft Cabin");if(old!=null)Object.DestroyImmediate(old);var cabin=new GameObject("Aircraft Cabin").transform;
  var floor=Box("Walkable cabin floor",cabin,new Vector3(3,-.25f,0),new Vector3(28,.5f,10),.08f,carpet,true);floor.tag="Ground";
  Box("Back wall lower liner",cabin,new Vector3(3,1.05f,5.03f),new Vector3(30,2.3f,.3f),.10f,ivory);
  Part("Continuous curved airframe lining",cabin,Shell(),Vector3.zero,ivory);
  Box("Wall kick plate",cabin,new Vector3(3,.43f,4.8f),new Vector3(30,.55f,.14f),.06f,navy);
  Box("Front cutaway sill",cabin,new Vector3(3,-.03f,-5.03f),new Vector3(30,.60f,.28f),.11f,ivory);
  Box("Front interior lower shell",cabin,new Vector3(3,-.67f,-4.8f),new Vector3(30,.85f,.60f),.2f,ivory);
  foreach(float z in new[]{-1.37f,1.37f})Box("Aisle edge guidance strip",cabin,new Vector3(3,.012f,z),new Vector3(28,.025f,.035f),.01f,light);
  foreach(float z in new[]{-4.95f,4.95f}){var wall=new GameObject("Solid cabin boundary").AddComponent<BoxCollider>();wall.transform.SetParent(cabin,false);wall.center=new Vector3(3,4,z);wall.size=new Vector3(28,9,.35f);}
  foreach(float x in new[]{-11.2f,17.2f}){Box("End bulkhead",cabin,new Vector3(x,2,0),new Vector3(.3f,5,10),.12f,ivory,true);Box("Door inset",cabin,new Vector3(x+(x<0?.18f:-.18f),1.8f,.1f),new Vector3(.10f,3.5f,2.5f),.045f,navy);}
  var seat=Seat();var outer=Ring("Oval outer window surround",.68f,.88f,.12f,.17f);var reveal=Ring("Oval inner window seal",.565f,.765f,.035f,.15f);var pane=Ring("Oval sky pane",.54f,.74f,0,0,true);
  for(int row=0;row<8;row++){
   float x=-8.5f+row*3.1f;
   foreach(float z in new[]{-3.5f,-2.05f,2.05f,3.5f}){var instance=(GameObject)PrefabUtility.InstantiatePrefab(seat);instance.name="Row "+(row+1).ToString("00")+" / "+(z<0?"Left bank ":"Right bank ")+Mathf.Abs(z);instance.transform.SetParent(cabin,false);instance.transform.localPosition=new Vector3(x,0,z);instance.transform.localRotation=Quaternion.Euler(0,90,0);}
   float wx=x+.65f;Part("Oval window frame "+row,cabin,outer,new Vector3(wx,2.65f,4.83f),linen);Part("Window recessed seal "+row,cabin,reveal,new Vector3(wx,2.65f,4.85f),dark);Part("Gradient daylight window "+row,cabin,pane,new Vector3(wx,2.65f,4.87f),glass);
   Box("Window sill "+row,cabin,new Vector3(wx,1.74f,4.73f),new Vector3(1.45f,.15f,.40f),.065f,ivory);
   Plate("Raised window shade "+row,cabin,new Vector3(wx,3.31f,4.71f),.86f,.17f,.04f,.06f,ivory);
   Box("Overhead bin body "+row,cabin,new Vector3(x+.5f,4.05f,4.02f),new Vector3(3.04f,.77f,1.28f),.28f,ivory);
   Plate("Overhead bin door "+row,cabin,new Vector3(x+.5f,4.04f,3.35f),2.87f,.60f,.05f,.17f,linen);
   Box("Storage latch "+row,cabin,new Vector3(x+.5f,3.86f,3.29f),new Vector3(.40f,.055f,.055f),.024f,metal);
   Plate("Passenger service console "+row,cabin,new Vector3(wx,3.56f,4.0f),1.55f,.30f,.09f,.10f,ivory);
   foreach(float dx in new[]{-.46f,-.14f}){Part("Circular air vent",cabin,Ring("Vent rim",.095f,.095f,.027f,.04f),new Vector3(wx+dx,3.56f,3.93f),metal);Part("Vent aperture",cabin,Ring("Vent aperture disk",.067f,.067f,0,0,true),new Vector3(wx+dx,3.56f,3.94f),dark);}
   Part("Reading light lens",cabin,Ring("Reading light lens",.085f,.085f,0,0,true),new Vector3(wx+.29f,3.56f,3.93f),light);
   Sign((row+1).ToString("00")+"  A B  /  C D",cabin,new Vector3(x+.5f,4.13f,3.29f),1.45f,new Color(.25f,.30f,.32f));
  }
  Box("Indirect ceiling wash",cabin,new Vector3(3,4.62f,3.16f),new Vector3(30,.07f,.15f),.028f,light);
  Box("Wall reading light rail",cabin,new Vector3(3,3.69f,4.30f),new Vector3(30,.045f,.08f),.018f,light);
  var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=6;var target=new Vector3(3,1.3f,.3f);camera.transform.position=target+new Vector3(0,23,-19.3f);camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);camera.backgroundColor=new Color(.82f,.85f,.85f);camera.clearFlags=CameraClearFlags.SolidColor;if(camera.GetComponent<CharacterCabinCamera>()==null)camera.gameObject.AddComponent<CharacterCabinCamera>();
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.63f,.68f);var sun=Object.FindObjectsOfType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);if(sun!=null){sun.intensity=.85f;sun.color=new Color(1,.96f,.89f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.32f;}
  var seats=cabin.Cast<Transform>().Where(t=>t.name.StartsWith("Row ")).ToArray();if(seats.Length!=32||seats.Any(t=>Vector3.Dot(t.TransformDirection(Vector3.back),Vector3.left)<.99f))throw new Exception("2+2 seats must all face left");
  foreach(float x in new[]{0,2,4,6})if(!floor.GetComponent<Collider>().bounds.Contains(new Vector3(x,-.25f,0)))throw new Exception("Spawn floor changed");
  if(Object.FindObjectOfType<CharacterCabinLobby>()==null||Object.FindObjectOfType<CustomSpawner>()==null)throw new Exception("Lobby functions missing");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();ClearWindows();Debug.Log("CABIN_REFIT_PASS: 32 left-facing seats in 2+2 layout; rounded upholstery, oval windows, trays, belts, service consoles, continuous cabin liner; existing lobby and spawn floor preserved.");
 }
 public static void ClearWindows(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=GameObject.Find("Aircraft Cabin").transform;var shell=cabin.Find("Continuous curved airframe lining");shell.GetComponent<MeshFilter>().sharedMesh=Shell();
  foreach(Transform child in cabin){
   var p=child.localPosition;
   if(child.name.StartsWith("Overhead bin body")){p.y=5.2f;p.z=4.2f;}
   else if(child.name.StartsWith("Overhead bin door")){p.y=5.19f;p.z=3.53f;}
   else if(child.name.StartsWith("Storage latch")){p.y=5.01f;p.z=3.47f;}
   else if(child.name.StartsWith("Cabin sign")){p.y=5.28f;p.z=3.47f;}
   else if(child.name.StartsWith("Passenger service console")){p.y=4.3f;p.z=4.3f;}
   else if(child.name=="Circular air vent"||child.name=="Vent aperture"||child.name=="Reading light lens"){p.y=4.3f;p.z=child.name=="Vent aperture"?4.24f:4.23f;}
   else if(child.name=="Indirect ceiling wash"){p.y=5.76f;p.z=3.34f;}
   else if(child.name=="Wall reading light rail"){p.y=4.43f;p.z=4.60f;}
   child.localPosition=p;
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Capture(Camera.main);EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");System.IO.File.Delete(Request);Debug.Log("CABIN_WINDOWS_PASS: overhead fixtures raised to expose oval windows in the close interior camera.");
 }
 static void Capture(Camera camera){var canvases=Object.FindObjectsOfType<Canvas>();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();System.IO.File.WriteAllBytes("Library/CharacterCabin-refit.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}}
}
#endif
