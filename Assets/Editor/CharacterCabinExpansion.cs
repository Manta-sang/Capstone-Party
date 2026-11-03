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
public static class CharacterCabinExpansion {
 const string Request="Library/CharacterCabinExpansion.request",Dir="Assets/Materials/CharacterCabin/Expansion/";
 const float AddedDepth=2.3f,OuterSeat=4.95f,Walkway=6.35f;
 static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
 static CharacterCabinExpansion(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty){if(System.IO.File.ReadAllText(Request)=="signs")PolishSigns();else Apply();}}
 static T Save<T>(T value,string name)where T:Object{string path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);return old;}
 static Material Material(string name,Color color,float metal=0,float smooth=.35f){var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",smooth);string p=Dir+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(p);if(old==null){AssetDatabase.CreateAsset(m,p);return m;}EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);return old;}
 static GameObject Part(string name,Transform parent,Mesh mesh,Material material,Vector3 position){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=position;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);return g;}
 static float Half(Vector3 s,Vector3 a)=>Vector3.Dot(s*.5f,new Vector3(Mathf.Abs(a.x),Mathf.Abs(a.y),Mathf.Abs(a.z)));
 static Mesh Rounded(Vector3 size,float r){
  r=Mathf.Min(r,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f);string name="Panel "+size.ToString("F3")+" r"+r.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);if(cache.TryGetValue(name,out var saved))return saved;
  var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var inner=size*.5f-Vector3.one*r;
  foreach(var n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
   var u=Vector3.Cross(n,Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(n,u);var xs=Axis(Half(size,u),r);var ys=Axis(Half(size,v),r);int start=vertices.Count;
   for(int y=0;y<ys.Length;y++)for(int x=0;x<xs.Length;x++){var p=n*Half(size,n)+u*xs[x]+v*ys[y];var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var normal=(p-q).normalized;vertices.Add(q+normal*r);normals.Add(normal);uv.Add(new Vector2(xs[x],ys[y]));}
   for(int y=0;y<ys.Length-1;y++)for(int x=0;x<xs.Length-1;x++){int a=start+y*xs.Length+x,b=a+1,c=a+xs.Length,d=c+1;triangles.AddRange(new[]{a,b,c,b,d,c});}
  }
  var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh=Save(mesh,name);cache[name]=mesh;return mesh;
 }
 static float[] Axis(float h,float r)=>new[]{-h,-h+r*.08f,-h+r*.30f,-h+r*.62f,-h+r,0,h-r,h-r*.62f,h-r*.30f,h-r*.08f,h};
 static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,float r,Material m)=>Part(name,p,Rounded(size,r),m,pos);
 static GameObject Plate(string name,Transform p,Vector3 pos,float w,float h,float depth,float radius,Material m){float d=Mathf.Max(depth,radius*2.5f);var g=Box(name,p,pos,new Vector3(w,h,d),radius,m);g.transform.localScale=new Vector3(1,1,depth/d);return g;}
 static Mesh Floor(float z){
  var size=new Vector3(28,.5f,AddedDepth);var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
  foreach(var n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
   var u=Vector3.Cross(n,Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(n,u);int a=vertices.Count;
   foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(1,1)}){var p=n*Half(size,n)+u*corner.x*Half(size,u)+v*corner.y*Half(size,v);vertices.Add(p);normals.Add(n);uv.Add(new Vector2(p.x/28,-(p.z+z)/.5f));}
   triangles.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});
  }
  var mesh=new Mesh{name=z<0?"Front floor addition":"Rear floor addition"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return Save(mesh,mesh.name);
 }
 static bool RearFixture(string n)=>new[]{"Back wall lower liner","Continuous curved airframe lining","Wall kick plate","Oval window frame","Window recessed seal","Gradient daylight window","Window sill","Raised window shade","Overhead bin body","Overhead bin door","Storage latch","Passenger service console","Circular air vent","Vent aperture","Reading light lens","Cabin sign","Indirect ceiling wash","Wall reading light rail"}.Any(n.StartsWith);
 [MenuItem("Tools/Party/Expand Cabin Side Seating And Aisles")]
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=GameObject.Find("Aircraft Cabin").transform;if(cabin.Find("Side aisles extension")!=null)throw new Exception("Expansion already applied");System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();cache.Clear();
  var floor=cabin.Find("Walkable cabin floor");var floorMatrix=floor.localToWorldMatrix;var floorMesh=floor.GetComponent<MeshFilter>().sharedMesh;var floorMaterial=floor.GetComponent<Renderer>().sharedMaterial;var floorCollider=floor.GetComponent<BoxCollider>();var floorSize=floorCollider.size;var floorCenter=floorCollider.center;
  var seats=cabin.Cast<Transform>().Where(t=>t.name.StartsWith("Row ")).ToArray();if(seats.Length!=32)throw new Exception("Expected original 32 seats");
  var matrices=seats.SelectMany(t=>t.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>t.localToWorldMatrix);var seatMeshes=seats.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>()).ToDictionary(t=>t,t=>t.sharedMesh);
  var group=new GameObject("Side aisles extension").transform;group.SetParent(cabin,false);
  var seatPrefab=PrefabUtility.GetCorrespondingObjectFromSource(seats[0].gameObject);if(seatPrefab==null)throw new Exception("Detailed seat prefab missing");
  var rows=seats.Select(t=>t.localPosition.x).Distinct().OrderBy(x=>x).ToArray();if(rows.Length!=8)throw new Exception("Original seat rows changed");
  foreach(float x in rows)foreach(float side in new[]{-1f,1f}){var seat=(GameObject)PrefabUtility.InstantiatePrefab(seatPrefab);seat.name="Additional outer seat "+x+" "+side;seat.transform.SetParent(group,false);seat.transform.localPosition=new Vector3(x,0,side*OuterSeat);seat.transform.localRotation=seats[0].localRotation;seat.transform.localScale=seats[0].localScale;}
  foreach(float side in new[]{-1f,1f}){float z=side*(5+AddedDepth/2);var strip=Part("Added walkable floor "+side,group,Floor(z),floorMaterial,new Vector3(3,-.25f,z));strip.AddComponent<BoxCollider>();strip.tag="Ground";}
  foreach(Transform t in cabin.Cast<Transform>().ToArray()){
   var p=t.localPosition;
   if(RearFixture(t.name)){p.z+=AddedDepth;if(t.name=="Back wall lower liner")p.z+=.12f;t.localPosition=p;if(t.name.StartsWith("Cabin sign")){var sign=t.GetComponent<TMP_Text>();if(sign!=null){sign.text=sign.text.Replace("A B  /  C D","A B C  /  D E F");sign.enableAutoSizing=true;sign.fontSizeMin=1.1f;sign.fontSizeMax=1.45f;sign.rectTransform.anchoredPosition=new Vector2(sign.rectTransform.anchoredPosition.x,5.28f);}}}
   else if(t.name=="Front cutaway sill"||t.name=="Front interior lower shell"){p.z-=AddedDepth;t.localPosition=p;}
   else if(t.name=="Solid cabin boundary"){var c=t.GetComponent<BoxCollider>();var center=c.center;center.z+=Mathf.Sign(center.z)*AddedDepth;c.center=center;}
   else if(t.name=="End bulkhead"){var scale=t.localScale;scale.z*=1+2*AddedDepth/10;t.localScale=scale;}
  }
  var pearl=Material("Molded airliner wall panels",new Color(.83f,.845f,.81f),.06f,.43f);var seam=Material("Soft panel joint shadow",new Color(.30f,.36f,.36f),.18f,.35f);var vent=Material("Cabin return air grille",new Color(.11f,.17f,.19f),.3f,.36f);var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CharacterCabin/Interior/Satin aluminium.mat");var warm=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CharacterCabin/Interior/Warm cabin light.mat");
  for(int i=0;i<rows.Length;i++){
   float x=rows[i]+.65f;
   Plate("Raised window wall module "+i,group,new Vector3(x,2.22f,7.25f),2.88f,3.16f,.10f,.30f,pearl);
   Plate("Lower wall access hatch "+i,group,new Vector3(x,.88f,7.075f),2.53f,.68f,.045f,.11f,pearl);
   Box("Vertical airframe panel joint "+i,group,new Vector3(x+1.49f,2.24f,7.13f),new Vector3(.028f,3.12f,.03f),.012f,seam);
   Plate("Vent surround "+i,group,new Vector3(x,.37f,6.99f),2.52f,.20f,.035f,.055f,metal);
   for(int slit=0;slit<16;slit++)Box("Vent grille slot",group,new Vector3(x-1.12f+slit*.149f,.37f,6.963f),new Vector3(.075f,.095f,.013f),.005f,vent);
   foreach(float dx in new[]{-1.12f,1.12f})foreach(float dy in new[]{-.23f,.23f})Box("Flush wall fastener",group,new Vector3(x+dx,.88f+dy,7.043f),new Vector3(.028f,.028f,.016f),.007f,metal);
   Plate("Front cutaway liner module "+i,group,new Vector3(x,.05f,-7.12f),2.91f,.36f,.10f,.10f,pearl);
  }
  foreach(float side in new[]{-1f,1f})Box("Side aisle guidance lighting "+side,group,new Vector3(3,.012f,side*6.9f),new Vector3(28,.025f,.035f),.01f,warm);
  var lobby=Object.FindObjectOfType<CharacterCabinLobby>();lobby.walkableZ=new Vector2(-6.55f,6.55f);EditorUtility.SetDirty(lobby);
  var camera=Camera.main;camera.orthographicSize=7.9f;camera.transform.position+=camera.transform.up*.55f;
  Physics.SyncTransforms();
  foreach(var pair in matrices)if(pair.Key.localToWorldMatrix!=pair.Value)throw new Exception("An original seat moved");foreach(var pair in seatMeshes)if(pair.Key.sharedMesh!=pair.Value)throw new Exception("An original seat mesh changed");
  if(floor.localToWorldMatrix!=floorMatrix||floor.GetComponent<MeshFilter>().sharedMesh!=floorMesh||floor.GetComponent<Renderer>().sharedMaterial!=floorMaterial||floorCollider.size!=floorSize||floorCollider.center!=floorCenter)throw new Exception("Original floor changed");
  foreach(float side in new[]{-1f,1f}){
   float z=side*Walkway;var a=new Vector3(-10,.55f,z);var b=new Vector3(-10,1.45f,z);
   if(Physics.CapsuleCast(a,b,.4f,Vector3.right,out var hit,26,~0,QueryTriggerInteraction.Ignore))throw new Exception("Side aisle blocked by "+hit.collider.name);
   foreach(float x in new[]{-9f,0f,8f,15f})if(!Physics.Raycast(new Vector3(x,2,z),Vector3.down,out var ground,3)||!ground.collider.CompareTag("Ground"))throw new Exception("Side aisle lacks walkable floor");
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Capture(camera);EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");System.IO.File.Delete(Request);
  Debug.Log("CABIN_EXPANSION_PASS: original 32 seats and center floor unchanged; 16 outer seats added (3+3); front/back floor strips only; both 26-unit side aisles passed capsule casts and ground checks; molded wall modules and vents saved.");
 }
 static void Capture(Camera camera){var canvases=Object.FindObjectsOfType<Canvas>();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();System.IO.File.WriteAllBytes("Library/CharacterCabin-expanded.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}}
 static void PolishSigns(){
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=GameObject.Find("Aircraft Cabin").transform;
  foreach(Transform t in cabin)if(t.name.StartsWith("Cabin sign")){var sign=t.GetComponent<TMP_Text>();if(sign!=null)sign.rectTransform.anchoredPosition=new Vector2(sign.rectTransform.anchoredPosition.x,5.28f);}
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Capture(Camera.main);EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");System.IO.File.Delete(Request);Debug.Log("CABIN_EXPANSION_SIGNS_PASS: seat identifiers mounted on overhead doors, clear of window panes.");
 }
}
#endif
