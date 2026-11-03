#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryRefine {
 const string Dir="Assets/Materials/BlockFall/MagicLibrary/";
 static Material floor,gold,wood;
 static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat){
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
  g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;
  Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;
 }
 static void Stroke(Transform parent,Vector3 a,Vector3 b,float width){
  var g=Box("Star inlay",parent,(a+b)*.5f,new Vector3(width,.014f,Vector3.Distance(a,b)),gold);
  g.transform.rotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
 }
 static Bounds BoundsOf(Transform t){
  var rs=t.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;
  foreach(var r in rs)b.Encapsulate(r.bounds);return b;
 }
 static bool Visible(Bounds b,Camera cam){
  float minX=10,minY=10,maxX=-10,maxY=-10;
  for(int j=0;j<8;j++){
   var p=cam.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1)));
   minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
  }
  return maxX>=0 && minX<=1 && maxY>=0 && minY<=1;
 }
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();var decor=GameObject.Find("Magic Library Decor").transform;
  floor=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Solid lavender floor.mat");
  if(floor==null){floor=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(floor,Dir+"Solid lavender floor.mat");}
  floor.color=new Color(.38f,.31f,.47f);floor.SetFloat("_Glossiness",.15f);EditorUtility.SetDirty(floor);
  gold=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Ancient pale gold.mat");
  wood=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Dusty plum wood.mat");
  foreach(Transform t in game.tiles)t.GetComponent<Renderer>().sharedMaterial=floor;
  for(int i=0;i<4;i++)GameObject.Find("Boundary "+i).GetComponent<Renderer>().enabled=false;
  var old=GameObject.Find("Library finishing");if(old!=null)Object.DestroyImmediate(old);
  var finish=new GameObject("Library finishing").transform;finish.SetParent(decor,false);
  // Extend the visible floor under the shelving without changing the playable tiles or collision boundary.
  Box("Continuous library floor",finish,new Vector3(0,-.3f,2),new Vector3(26,.5f,26),floor);
  int removed=0;
  var bays=new List<Transform>();foreach(Transform t in decor)if(t.name.StartsWith("Library bay "))bays.Add(t);
  foreach(var bay in bays){
   bool side=Mathf.Abs(bay.position.x)>12 && Mathf.Abs(bay.position.x)<13;
   if(side){
    var bounds=BoundsOf(bay);float height=bounds.max.y-bay.position.y;
    bay.localScale=new Vector3(bay.localScale.x,bay.localScale.y*8.1f/Mathf.Max(1,height),bay.localScale.z);
    // End the side wall at the rear junction instead of crossing into the rear shelves.
    if(bay.position.z>9){Object.DestroyImmediate(bay.gameObject);removed++;continue;}
   }else if(Mathf.Abs(bay.position.x)>10){
    Object.DestroyImmediate(bay.gameObject);removed++;continue;
   }
   if(!Visible(BoundsOf(bay),Camera.main)){Object.DestroyImmediate(bay.gameObject);removed++;}
  }
  // Solid corner joinery and short return panels terminate the book compartments cleanly.
  foreach(float side in new[]{-1f,1f}){
   Box("Corner return back",finish,new Vector3(side*11.45f,4,12.4f),new Vector3(2.1f,8,.35f),wood);
   Box("Corner return side",finish,new Vector3(side*12.4f,4,10.45f),new Vector3(.35f,8,4.2f),wood);
   foreach(var p in new[]{new Vector3(side*10.55f,4,11.85f),new Vector3(side*12.4f,4,8.4f),new Vector3(side*12.4f,4,12.4f)}){
    Box("Corner terminal pilaster",finish,p,new Vector3(.28f,8,.45f),wood);
    Box("Pilaster fine gold",finish,p+new Vector3(0,0,-.24f),new Vector3(.045f,8,.025f),gold);
    Box("Corner capital",finish,p+Vector3.up*4.05f,new Vector3(.5f,.2f,.65f),gold);
   }
   Box("Return crown rear",finish,new Vector3(side*11.45f,8.1f,12.4f),new Vector3(2.3f,.2f,.85f),gold);
   Box("Return crown side",finish,new Vector3(side*12.4f,8.1f,10.45f),new Vector3(.85f,.2f,4.3f),gold);
  }
  var star=new GameObject("Great five-point star").transform;star.SetParent(finish,false);
  for(int i=0;i<5;i++){
   float a=(90+i*144)*Mathf.Deg2Rad,b=(90+(i+1)*144)*Mathf.Deg2Rad;
   Stroke(star,new Vector3(Mathf.Cos(a)*6.5f,.055f,Mathf.Sin(a)*6.5f),new Vector3(Mathf.Cos(b)*6.5f,.055f,Mathf.Sin(b)*6.5f),.09f);
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  var camera=Camera.main;var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();System.IO.File.WriteAllBytes("BlockFall-preview.png",image.EncodeToPNG());
  camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
  Debug.Log("MAGIC_LIBRARY_REFINE_PASS: solid floor, great star, hidden boundaries, tall sides, finished corners; removed "+removed+" bays.");
 }
}}
#endif
