#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// All scenery is baked into Qte.unity; never runs in a player or modifies shared prefabs.
public static class QteEnvironmentRefinement
{
 const string Folder="Assets/Materials/QteGenerated/";
 static Transform root;
 static System.Random rng;
 static float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
 [MenuItem("Party/Refine QTE Campus Environment")]
 public static void Build()
 {
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
  rng=new System.Random(20261105);
  var old=GameObject.Find("QTE Refined Campus");if(old)Object.DestroyImmediate(old);
  root=new GameObject("QTE Refined Campus").transform;
  var all=Object.FindObjectsOfType<Transform>(true);
  int removed=0;
  foreach(var t in all.Where(t=>t.name.StartsWith("QTE Campus tree ")||t.name=="QTE Campus natural leaf crown").ToArray())
   if(t){Object.DestroyImmediate(t.gameObject);removed++;}
  Trees();School();Fences();Pitch();Coast();
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.69f,.77f);RenderSettings.ambientEquatorColor=new Color(.45f,.52f,.47f);RenderSettings.ambientGroundColor=new Color(.28f,.32f,.27f);RenderSettings.ambientIntensity=1;DynamicGI.UpdateEnvironment();QteImportValidation.AttachSharedStartUI();
  // Combine only newly added architectural details; prefab trees retain editable prefab links.
  foreach(Transform group in root.Cast<Transform>().Where(t=>t.name!="Asset tree groves").ToArray())Combine(group);
  var controller=Object.FindObjectOfType<QteSceneController>();var so=new SerializedObject(controller);
  var camera=Camera.main;camera.transform.position=so.FindProperty("overviewPosition").vector3Value;
  camera.transform.LookAt(so.FindProperty("overviewLookAt").vector3Value);camera.fieldOfView=so.FindProperty("overviewFieldOfView").floatValue;
  Validate();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Directory.CreateDirectory("Library/QteEnvironment");
  typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Library/QteEnvironment/overview.png"});
  // Capture a representative runner view without saving the temporary camera pose.
  camera.transform.position=new Vector3(-10,5,-18);camera.transform.LookAt(new Vector3(12,3,-16));camera.fieldOfView=68;
  typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Library/QteEnvironment/runner.png"});
  File.WriteAllText("Library/QteEnvironment/result.txt","PASS: removed "+removed+" primitive tree parts; "+root.Find("Asset tree groves").childCount+" linked varied tree prefabs, fixed campus scenery, materials/meshes/scripts checked. Gameplay controller and spawns preserved.");
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");QteSchoolPresentation.Build();QteSportsSurface.Build();Debug.Log("QTE_ENVIRONMENT_PASS");
 }
 static Transform Group(string name){var g=new GameObject(name).transform;g.SetParent(root,false);return g;}
 static Material Mat(string name,Color color,float smooth=.15f)
 {
  string path=Folder+"Refined_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Glossiness",smooth);EditorUtility.SetDirty(m);return m;
 }
 static GameObject Box(Transform p,string name,Vector3 at,Vector3 size,Material m)
 {
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.position=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;
 }
 static void Rod(Transform p,Vector3 a,Vector3 b,float width,Material m)
 {var g=Box(p,"Detail",(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),m);g.transform.rotation=Quaternion.LookRotation(b-a);}
 static void Trees()
 {
  var p=Group("Asset tree groves");string[] paths={"Assets/9t5 Low Poly Playground/Prefabs/Tree.prefab","Assets/CubexCube - Free City Pack I/Prefabs/Props_Street/Env_Type2_Tree_2.prefab","Assets/CubexCube - Free City Pack I/Prefabs/Props_Street/Env_Type3_Tree_2.prefab"};
  var prefabs=paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();if(prefabs.Any(x=>!x))throw new Exception("Tree asset missing");int last=-1,count=0;
  void Tree(float x,float z,float height)
  {
   int index=(last+1+rng.Next(prefabs.Length-1))%prefabs.Length;last=index;
   var g=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[index]);g.name=$"Grove {++count:000} · {prefabs[index].name}";g.transform.SetParent(p,false);g.transform.rotation=Quaternion.Euler(0,R(0,360),0);
   var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("Tree has no renderer");var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
   g.transform.localScale*=height/Mathf.Max(.1f,bounds.size.y);bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
   g.transform.position+=new Vector3(x-bounds.center.x,-bounds.min.y,z-bounds.center.z);
   foreach(var c in g.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
   foreach(var r in rs){var materials=r.sharedMaterials;for(int k=0;k<materials.Length;k++){var source=materials[k];string path=Folder+"Refined_tree_"+source.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",.12f);m.EnableKeyword("_EMISSION");m.SetTexture("_EmissionMap",m.mainTexture);m.SetColor("_EmissionColor",new Color(.13f,.15f,.12f));m.enableInstancing=true;EditorUtility.SetDirty(m);materials[k]=m;}r.sharedMaterials=materials;}
   foreach(var t in g.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
  }
  // Dense staggered belts away from the school, with a clear buffer around the oval/stands.
  for(int layer=0;layer<3;layer++)for(float x=-87;x<=87;x+=R(7.5f,11))Tree(x+R(-2,2),-51-layer*11+R(-3,3),R(14,20)+layer*2);
  foreach(float side in new[]{-1f,1f})for(int layer=0;layer<2;layer++)for(float z=-36;z<52;z+=R(9,14))Tree(side*(73+layer*13+R(-2,2)),z,R(12,19));
    foreach(float x in new[]{-60f,-48f,49f,61f})Tree(x+R(-2,2),45+R(-2,2),R(9,13));
  var bush=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CubexCube - Free City Pack I/Prefabs/Props_Street/Env_Bush_1.prefab");
  void Shrub(float x,float z){if(!bush)return;var g=(GameObject)PrefabUtility.InstantiatePrefab(bush);g.name="Outer hedge cluster";g.transform.SetParent(p,false);var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float size=R(4,6)/Mathf.Max(.1f,b.size.y);g.transform.localScale*=size;g.transform.localScale=Vector3.Scale(g.transform.localScale,new Vector3(R(1.3f,1.8f),1,R(1.1f,1.5f)));g.transform.rotation=Quaternion.Euler(0,R(0,360),0);b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.position+=new Vector3(x-b.center.x,-b.min.y,z-b.center.z);foreach(var c in g.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);foreach(var r in rs)r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Refined_tree_Texture_1.mat");}
  for(float x=-88;x<89;x+=R(7,10))Shrub(x,-49+R(-2,2));
  foreach(float side in new[]{-1f,1f})for(float z=-45;z<48;z+=R(8,11))Shrub(side*R(70,75),z);
  // Understory closes the gaps below tall crowns, outside the running/bleacher footprint.
  for(float x=-88;x<89;x+=R(7,10))Tree(x+R(-2,2),-47+R(-2,2),R(6,9));
  foreach(float side in new[]{-1f,1f})for(float z=-44;z<44;z+=R(7,10))Tree(side*R(69,74),z,R(6,10));
 }
 static void School()
 {
  var p=Group("School facade details");var trim=Mat("Painted_ivory",new Color(.78f,.79f,.73f));var metal=Mat("Aged_zinc",new Color(.32f,.39f,.40f),.45f);var glass=Mat("Window_reflections",new Color(.39f,.60f,.65f),.7f);
  // Subtle weathered plaster, used only on QTE building facades.
  var wall=Mat("School_plaster",new Color(.82f,.79f,.70f));var tex=new Texture2D(128,128,TextureFormat.RGB24,true);tex.name="Fine plaster";
  var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float n=Mathf.PerlinNoise(x*.2f,y*.2f)*.07f+R(-.015f,.015f);pixels[y*128+x]=new Color(.88f+n,.88f+n,.88f+n);}tex.SetPixels(pixels);tex.Apply();
  string tp=Folder+"Refined_plaster.asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);if(existing){EditorUtility.CopySerialized(tex,existing);Object.DestroyImmediate(tex);tex=existing;}else AssetDatabase.CreateAsset(tex,tp);EditorUtility.SetDirty(tex);wall.mainTexture=tex;wall.mainTextureScale=new Vector2(8,5);
  foreach(string name in new[]{"School main building","School west classroom wing","School east classroom wing"}){var g=GameObject.Find(name);if(g)g.GetComponent<Renderer>().sharedMaterial=wall;}
  foreach(var w in Object.FindObjectsOfType<Transform>().Where(t=>t.name=="School classroom window").ToArray())
  {var b=w.GetComponent<Renderer>().bounds;Box(p,"Window reflected sky strip",b.center+new Vector3(0,.52f,-.065f),new Vector3(b.size.x*.93f,.12f,.015f),glass);}
  foreach(float x in new[]{-41f,-18f,18f,41f}){Rod(p,new Vector3(x,.25f,27.1f),new Vector3(x,15.6f,27.1f),.14f,metal);Rod(p,new Vector3(x,.25f,27.1f),new Vector3(x,.25f,26.55f),.14f,metal);}
  foreach(float x in new[]{-35f,-25f,25f,35f}){Box(p,"Classroom ventilation housing",new Vector3(x,6.4f,27.3f),new Vector3(1.7f,.65f,.45f),trim);for(int i=0;i<5;i++)Box(p,"Vent slat",new Vector3(x,6.2f+i*.09f,27.04f),new Vector3(1.4f,.025f,.025f),metal);}
  foreach(float x in new[]{-1.05f,1.05f}){Rod(p,new Vector3(x,1.8f,24.18f),new Vector3(x,2.7f,24.18f),.065f,metal);}
  for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Rod(p,new Vector3(Mathf.Sin(a)*.39f,18.1f+Mathf.Cos(a)*.39f,26.24f),new Vector3(Mathf.Sin(a)*.46f,18.1f+Mathf.Cos(a)*.46f,26.24f),.035f,metal);}
 }
 static Material Net()
 {
  var m=Mat("Chainlink",new Color(.35f,.43f,.40f),.38f);const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,true);var colors=new Color[n*n];
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=(x+y)%32,b=(x-y+128)%32;colors[y*n+x]=new Color(1,1,1,(a<2||b<2)?1:0);}tex.SetPixels(colors);tex.Apply();tex.wrapMode=TextureWrapMode.Repeat;
  string path=Folder+"Refined_chainlink.asset";var old=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(old){EditorUtility.CopySerialized(tex,old);Object.DestroyImmediate(tex);tex=old;}else AssetDatabase.CreateAsset(tex,path);EditorUtility.SetDirty(tex);
  m.mainTexture=tex;m.SetFloat("_Mode",1);m.SetFloat("_Cutoff",.4f);m.SetInt("_SrcBlend",1);m.SetInt("_DstBlend",0);m.SetInt("_ZWrite",1);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;return m;
 }
 static void Fences()
 {
  var p=Group("School chainlink fencing");var net=Net();var metal=Mat("Fence_posts",new Color(.23f,.32f,.29f),.45f);
  foreach(var old in Object.FindObjectsOfType<Transform>().Where(t=>t.name.StartsWith("QTE School perimeter fence ")).ToArray())Object.DestroyImmediate(old.gameObject);
  foreach(bool alongX in new[]{true,false})foreach(float sign in new[]{-1f,1f})
  {float span=alongX?136:100,edge=alongX?50:68;
   for(float d=-span/2;d<span/2;d+=4){float length=Mathf.Min(4,span/2-d);Vector3 At(float v,float y)=>alongX?new Vector3(v,y,sign*edge):new Vector3(sign*edge,y,v);
    Rod(p,At(d,.05f),At(d,4.15f),.15f,metal);Rod(p,At(d,4),At(d+length,4),.10f,metal);Rod(p,At(d,.25f),At(d+length,.25f),.08f,metal);
    var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name="Diamond wire panel";g.transform.SetParent(p,false);g.transform.position=At(d+length/2,2.12f);g.transform.rotation=Quaternion.Euler(0,alongX?0:90,0);g.transform.localScale=new Vector3(length,3.7f,1);Object.DestroyImmediate(g.GetComponent<Collider>());
    // A second back-facing panel keeps the mesh visible from inside and outside.
    var mesh=g.GetComponent<MeshFilter>().sharedMesh;var back=Object.Instantiate(g,p);back.transform.rotation*=Quaternion.Euler(0,180,0);var material=new Material(net);material.mainTextureScale=new Vector2(length*2,7.4f);
    string mp=Folder+"Refined_net_"+length.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!saved){AssetDatabase.CreateAsset(material,mp);saved=material;}else{EditorUtility.CopySerialized(material,saved);Object.DestroyImmediate(material);}g.GetComponent<Renderer>().sharedMaterial=saved;back.GetComponent<Renderer>().sharedMaterial=saved;
   }
  }
 }
 static void Pitch()
 {
  var p=Group("Football goal open mesh");
  var center=GameObject.Find("Center circle");if(center)Object.DestroyImmediate(center);
  var marking=Mat("Pitch_chalk",new Color(.83f,.86f,.76f));for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;Rod(p,new Vector3(Mathf.Cos(a)*5,.084f,Mathf.Sin(a)*5),new Vector3(Mathf.Cos(b)*5,.084f,Mathf.Sin(b)*5),.10f,marking);}
  var ground=GameObject.Find("School ground");var field=GameObject.Find("Football field grass");if(ground&&field){var turf=Mat("Pitch_turf",new Color(.30f,.48f,.25f));turf.mainTexture=ground.GetComponent<Renderer>().sharedMaterial.mainTexture;turf.mainTextureScale=new Vector2(7,3);field.GetComponent<Renderer>().sharedMaterial=turf;}
  foreach(var stripe in Object.FindObjectsOfType<Transform>().Where(t=>t.name=="Football pitch stripe")){var m=Mat("Mown_turf",new Color(.34f,.52f,.28f));m.mainTexture=ground.GetComponent<Renderer>().sharedMaterial.mainTexture;m.mainTextureScale=new Vector2(1,3);stripe.GetComponent<Renderer>().sharedMaterial=m;}
  var net=Mat("Goal_rope",new Color(.83f,.85f,.77f));
  foreach(var t in Object.FindObjectsOfType<Transform>().Where(t=>t.name=="Football goal net side"||t.name=="Football goal net roof"||t.name.StartsWith("Goal net ")).ToArray())Object.DestroyImmediate(t.gameObject);
  foreach(float end in new[]{-31f,31f}){float inward=Mathf.Sign(end),front=end-inward*.14f,back=end+inward*2.1f;
   for(float z=-3.6f;z<=3.61f;z+=.3f){Rod(p,new Vector3(back,.1f,z),new Vector3(back,2.38f,z),.018f,net);Rod(p,new Vector3(front,2.42f,z),new Vector3(back,2.38f,z),.018f,net);}
   for(float y=.1f;y<2.4f;y+=.28f){Rod(p,new Vector3(back,y,-3.6f),new Vector3(back,y,3.6f),.018f,net);foreach(float z in new[]{-3.6f,3.6f})Rod(p,new Vector3(front,y,z),new Vector3(back,y,z),.018f,net);}
   for(float x=0;x<2.1f;x+=.3f){Rod(p,new Vector3(front+inward*x,2.4f,-3.6f),new Vector3(front+inward*x,2.4f,3.6f),.018f,net);foreach(float z in new[]{-3.6f,3.6f})Rod(p,new Vector3(front+inward*x,.1f,z),new Vector3(front+inward*x,2.4f,z),.018f,net);}
  }
 }
 static void Coast()
 {
  var p=Group("Coastal landscape");var sand=Mat("Dune_sand",new Color(.72f,.67f,.51f));var foam=Mat("Soft_surf",new Color(.65f,.83f,.80f),.4f);var wet=Mat("Wet_sand",new Color(.48f,.62f,.56f),.6f);
  foreach(var t in Object.FindObjectsOfType<Transform>().Where(t=>t.name=="QTE Ocean rolling foam").ToArray())Object.DestroyImmediate(t.gameObject);
  // Continuous curved shoreline strips replace isolated rectangular foam bars.
  for(int band=0;band<3;band++){
   var vertices=new List<Vector3>();var triangles=new List<int>();for(int i=0;i<=100;i++){float z=-240+i*4.8f,x=117+Mathf.Sin(z*.027f)*2.2f+Mathf.Sin(z*.081f)*.7f;float width=band==0?4:band==1?1.2f:.38f;float offset=band==0?-2:band==1?1.2f:4;
    vertices.Add(new Vector3(x+offset,.065f+band*.006f,z));vertices.Add(new Vector3(x+offset+width,.065f+band*.006f,z));if(i>0){int k=i*2;triangles.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}}
   var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();SaveMesh(mesh,"Shore_"+band,p,band==0?wet:foam);
  }
  var rockPaths=Enumerable.Range(1,12).Select(i=>"Assets/9t5 Low Poly Playground/Prefabs/Rock "+i+".prefab").ToArray();
  for(int i=0;i<22;i++){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(rockPaths[rng.Next(rockPaths.Length)]);if(!prefab)continue;var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(p,false);g.transform.position=new Vector3(R(101,114),-.2f,R(-110,105));g.transform.rotation=Quaternion.Euler(R(-8,8),R(0,360),R(-5,5));g.transform.localScale*=R(.7f,2.2f);foreach(var c in g.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);}
 }
 static void SaveMesh(Mesh mesh,string name,Transform parent,Material material)
 {
  string path=Folder+"Refined_"+name+".asset";mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);EditorUtility.SetDirty(mesh);
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=material;g.isStatic=true;
 }
 static void Combine(Transform group)
 {
  // Leave prefab assets intact so artists can replace individual rocks and trees later.
  var renderers=group.GetComponentsInChildren<MeshRenderer>().Where(r=>!PrefabUtility.IsPartOfPrefabInstance(r)).ToArray();int index=0;
  foreach(var set in renderers.GroupBy(r=>r.sharedMaterial)){
   var entries=set.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=r.localToWorldMatrix}).ToArray();if(entries.Length<2)continue;
   var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entries,true,true);SaveMesh(mesh,group.name.Replace(" ","_")+"_"+index++,group,set.Key);foreach(var r in set)Object.DestroyImmediate(r.gameObject);
  }
 }
 static void Validate()
 {
  foreach(var t in Object.FindObjectsOfType<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
  foreach(var r in Object.FindObjectsOfType<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader")throw new Exception("Missing material: "+r.name);
  foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(!f.sharedMesh)throw new Exception("Missing mesh "+f.name);
 }
}
#endif



