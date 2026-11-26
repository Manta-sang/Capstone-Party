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

[InitializeOnLoad]
public static class QteSportsSurface
{
 const string Folder="Assets/Materials/QteGenerated/";
 struct Stroke {public Vector3 a,b; public Stroke(Vector3 a,Vector3 b){this.a=a;this.b=b;}}
 static List<Stroke> strokes;
 static QteSportsSurface(){EditorApplication.update+=()=>{if(!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Library/QteSportsSurface.request")){File.Delete("Library/QteSportsSurface.request");try{Build();}catch(Exception ex){File.WriteAllText("Library/QteSportsSurface.result","FAIL: "+ex);Debug.LogException(ex);}}};}
 [MenuItem("Party/Refine QTE Pitch and Hurdles")]
 public static void Build()
 {
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
  var old=GameObject.Find("QTE Football Pitch");if(old)Object.DestroyImmediate(old);
  var root=new GameObject("QTE Football Pitch").transform;
  foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>new[]{"Football pitch stripe","Pitch halfway line","Goal box line","Goal box side"}.Contains(t.name)).ToArray())Object.DestroyImmediate(t.gameObject);
  // The previous center circle shares a batch with the goal net. Replace only its material batch.
  var goal=GameObject.Find("Football goal open mesh");if(goal)foreach(var r in goal.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial&&r.sharedMaterial.name.Contains("Pitch_chalk"))Object.DestroyImmediate(r.gameObject);
  var field=GameObject.Find("Football field grass");field.GetComponent<Renderer>().sharedMaterial=GrassMaterial();field.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
  strokes=new List<Stroke>();
  void Line(float x1,float z1,float x2,float z2)=>strokes.Add(new Stroke(new Vector3(x1,.112f,z1),new Vector3(x2,.112f,z2)));
  void Rectangle(float x1,float x2,float z1,float z2){Line(x1,z1,x2,z1);Line(x2,z1,x2,z2);Line(x2,z2,x1,z2);Line(x1,z2,x1,z1);}
  void Arc(float x,float z,float radius,float start,float end){int count=Mathf.CeilToInt((end-start)*radius*5);for(int i=0;i<count;i++){float a=Mathf.Lerp(start,end,i/(float)count),b=Mathf.Lerp(start,end,(i+1)/(float)count);Line(x+Mathf.Cos(a)*radius,z+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,z+Mathf.Sin(b)*radius);}}
  Rectangle(-31,31,-13.5f,13.5f);Line(0,-13.5f,0,13.5f);Arc(0,0,5,0,Mathf.PI*2);
  foreach(float side in new[]{-1f,1f})
  {
   float end=31*side,area=22*side,small=28*side;Line(end,-10,area,-10);Line(area,-10,area,10);Line(area,10,end,10);
   Line(end,-5.5f,small,-5.5f);Line(small,-5.5f,small,5.5f);Line(small,5.5f,end,5.5f);
   float center=25*side;float angle=Mathf.Acos(3f/5f);if(side<0)Arc(center,0,5,-angle,angle);else Arc(center,0,5,Mathf.PI-angle,Mathf.PI+angle);
  }
  foreach(float x in new[]{-31f,31f})foreach(float z in new[]{-13.5f,13.5f}){float start=x<0?(z<0?0:-Mathf.PI*.5f):(z<0?Mathf.PI*.5f:Mathf.PI);Arc(x,z,.75f,start,start+Mathf.PI*.5f);}
  var v=new List<Vector3>();var triangles=new List<int>();foreach(var s in strokes){Vector3 side=Vector3.Cross(Vector3.up,(s.b-s.a).normalized)*.06f;int k=v.Count;v.AddRange(new[]{s.a-side,s.a+side,s.b-side,s.b+side});triangles.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
  foreach(float x in new[]{-25f,0,25f}){int k=v.Count;v.Add(new Vector3(x,.113f,0));for(int i=0;i<=24;i++){float a=i*Mathf.PI*2/24;v.Add(new Vector3(x+Mathf.Cos(a)*.12f,.113f,Mathf.Sin(a)*.12f));if(i>0)triangles.AddRange(new[]{k,k+i+1,k+i});}}
  var paint=Mat("Pitch_white_paint",new Color(.98f,.99f,.96f));MeshObject(root,"Continuous football markings",v,triangles,null,null,paint);
  GrassBlades(root);
  var prefab=Hurdle();var controller=Object.FindObjectOfType<QteSceneController>();var so=new SerializedObject(controller);so.FindProperty("competitionHurdlePrefab").objectReferenceValue=prefab;so.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();Directory.CreateDirectory("Library/QteSportsSurface");Capture("Library/QteSportsSurface/pitch.png");
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.position=new Vector3(0,.08f,-20.6f);var cam=Camera.main;cam.transform.position=new Vector3(1.5f,1.3f,-23);cam.transform.LookAt(new Vector3(0,.55f,-20.8f));cam.fieldOfView=48;Capture("Library/QteSportsSurface/hurdle.png");
  File.WriteAllText("Library/QteSportsSurface.result","PASS: textured short turf, eight batched grass patches, continuous football markings and competition hurdle prefab saved.");EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
 }
 static Material Mat(string name,Color color,float metallic=0,float smooth=.2f){string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
 static Material GrassMaterial()
 {
  var m=Mat("Pitch_mown_grass",Color.white,0,.12f);string path=Folder+"Pitch_grass_albedo.asset";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!t){const int n=512;t=new Texture2D(n,n,TextureFormat.RGB24,true){name="Mown football turf",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};var colors=new Color[n*n];var rng=new System.Random(419);for(int y=0;y<n;y++)for(int x=0;x<n;x++){float grain=(float)rng.NextDouble()*.11f,noise=Mathf.PerlinNoise(x*.024f,y*.024f)*.13f;float band=(x/51)%2==0?1:.91f;colors[y*n+x]=new Color(.16f+grain*.55f+noise*.2f,.32f+grain+noise,.105f+grain*.4f+noise*.25f)*band;}t.SetPixels(colors);t.Apply();AssetDatabase.CreateAsset(t,path);}m.mainTexture=t;m.mainTextureScale=Vector2.one;return m;
 }
 static bool OnPaint(Vector3 p){foreach(var s in strokes){Vector3 d=s.b-s.a,q=s.a+d*Mathf.Clamp01(Vector3.Dot(p-s.a,d)/d.sqrMagnitude);if((new Vector2(p.x-q.x,p.z-q.z)).sqrMagnitude<.015f)return true;}return false;}
 static void GrassBlades(Transform root)
 {
  var rng=new System.Random(1708);float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);var m=Mat("Pitch_short_grass_blades",new Color(.26f,.43f,.18f));
  for(int patch=0;patch<8;patch++)
  {
   var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var tr=new List<int>();float left=-34.8f+patch*8.7f;
   for(int tuft=0;tuft<1900;tuft++)
   {Vector3 p=new Vector3(R(left,left+8.7f),.033f,R(-14.8f,14.8f));if(OnPaint(new Vector3(p.x,.112f,p.z)))continue;
    for(int blade=0;blade<3;blade++){float a=R(0,Mathf.PI*2),h=R(.035f,.07f),width=R(.006f,.014f);Vector3 side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width,tip=p+new Vector3(R(-.016f,.016f),h,R(-.016f,.016f));int k=v.Count;v.AddRange(new[]{p-side,p+side,tip});tr.AddRange(new[]{k,k+2,k+1,k,k+1,k+2});for(int j=0;j<3;j++){n.Add(Vector3.up);float shade=R(.8f,1.15f);c.Add(new Color(shade,shade,shade));}}
   }
   MeshObject(root,"Short turf patch "+(patch+1),v,tr,n,c,m);
  }
 }
 static GameObject Hurdle()
 {
  if(!AssetDatabase.IsValidFolder("Assets/Prefabs/Qte"))AssetDatabase.CreateFolder("Assets/Prefabs","Qte");
  var root=new GameObject("Competition hurdle · L base");var metal=Mat("Hurdle_anodized_aluminium",new Color(.66f,.71f,.74f),.78f,.48f);var blue=Mat("Hurdle_blue_powdercoat",new Color(.055f,.20f,.38f),.24f,.32f);var white=Mat("Hurdle_white_board",new Color(.96f,.97f,.94f),0,.26f);var black=Mat("Hurdle_rubber_and_markings",new Color(.045f,.05f,.055f),.03f,.18f);
  void Box(string name,Vector3 p,Vector3 size,Material m)
  {var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root.transform,false);g.transform.localPosition=p;string path=Folder+"Hurdle_mesh_"+name.Replace(" ","_")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var fresh=BevelBox(size);if(mesh){EditorUtility.CopySerialized(fresh,mesh);Object.DestroyImmediate(fresh);EditorUtility.SetDirty(mesh);}else{mesh=fresh;AssetDatabase.CreateAsset(mesh,path);}g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=m;}
  foreach(float side in new[]{-1f,1f})
  {
   float x=side*.54f;Box("L shaped aluminium base",new Vector3(x,.043f,-.29f),new Vector3(.075f,.06f,.70f),metal);
   foreach(float z in new[]{-.65f,.065f})Box("Rubber end cap",new Vector3(x,.04f,z),new Vector3(.08f,.07f,.025f),black);
   Box("Counterweight slider",new Vector3(x,.082f,-.48f),new Vector3(.09f,.027f,.16f),blue);
   Box("Blue telescopic lower upright",new Vector3(x,.28f,0),new Vector3(.055f,.46f,.055f),blue);
   Box("Adjustable upper extrusion",new Vector3(x,.65f,0),new Vector3(.042f,.35f,.042f),metal);
   Box("Adjustment collar",new Vector3(x,.50f,0),new Vector3(.065f,.04f,.065f),black);
   for(int hole=0;hole<4;hole++)Box("Height adjustment hole",new Vector3(x,.55f+hole*.055f,-.023f),new Vector3(.009f,.009f,.003f),black);
   Box("Spring lock button",new Vector3(x,.48f,-.036f),new Vector3(.019f,.019f,.012f),metal);
   Box("Top bar mounting shoe",new Vector3(x,.81f,.007f),new Vector3(.075f,.08f,.06f),blue);
   Box("Crossbar fixing screw",new Vector3(x,.815f,-.031f),new Vector3(.014f,.014f,.004f),metal);
  }
  Box("Single lower cross member",new Vector3(0,.075f,0),new Vector3(1.10f,.042f,.042f),metal);
  Box("White ABS hurdle board",new Vector3(0,.815f,-.018f),new Vector3(1.20f,.07f,.025f),white);
  foreach(float side in new[]{-1f,1f}){Box("Dark crossbar visibility band",new Vector3(side*.43f,.815f,-.032f),new Vector3(.09f,.07f,.003f),black);Box("Dark board end cap",new Vector3(side*.60f,.815f,-.018f),new Vector3(.012f,.073f,.03f),black);}
  typeof(QteEnvironmentRefinement).GetMethod("Combine",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{root.transform});
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Qte/CompetitionHurdle.prefab");Object.DestroyImmediate(root);return prefab;
 }
 static Mesh BevelBox(Vector3 size)
 {
  var v=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();Vector3 h=size*.5f;float radius=Mathf.Min(.008f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.2f);Vector3 inner=h-Vector3.one*radius;
  float At(float half,int i)=>i==0?-half:i==1?-half+radius:i==2?half-radius:half;
  for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f})
  {
   int start=v.Count,u=(axis+1)%3,w=(axis+2)%3;
   for(int j=0;j<4;j++)for(int i=0;i<4;i++){Vector3 p=Vector3.zero;p[axis]=h[axis]*sign;p[u]=At(h[u],i);p[w]=At(h[w],j);Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var normal=(p-q).normalized;v.Add(q+normal*radius);normals.Add(normal);}
   for(int j=0;j<3;j++)for(int i=0;i<3;i++){int a=start+j*4+i,b=a+1,c=a+4,d=c+1;if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),normals[a])>0)triangles.AddRange(new[]{a,b,c,b,d,c});else triangles.AddRange(new[]{a,c,b,b,c,d});}
  }
  var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
 }
 static void MeshObject(Transform parent,string name,List<Vector3> v,List<int> tr,List<Vector3> normals,List<Color> colors,Material material)
 {
  var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name=name};mesh.SetVertices(v);mesh.SetTriangles(tr,0);if(normals!=null)mesh.SetNormals(normals);else mesh.RecalculateNormals();if(colors!=null)mesh.SetColors(colors);string path=Folder+"Sports_"+name.Replace(" ","_")+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=material;g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;g.isStatic=true;
 }
 static void Capture(string path){var hud=Object.FindObjectOfType<QteSequenceHud>();if(hud)hud.group.alpha=0;var countdown=GameObject.Find("Round Start Canvas");if(countdown)countdown.SetActive(false);typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{path});if(countdown)countdown.SetActive(true);}
}
#endif
