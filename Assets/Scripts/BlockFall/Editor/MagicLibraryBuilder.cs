#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryBuilder {
 const string Dir="Assets/Materials/BlockFall/MagicLibrary";
 static Material gold, wood, paper, floor, glow, ink;
 static Material[] covers;
 static Mesh rounded, detailedRounded;
 static System.Random rng;
 static float R(float a,float b) { return Mathf.Lerp(a,b,(float)rng.NextDouble()); }
 static Material Mat(string name,Color color,bool emission=false) {
  string path=Dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
  m.color=color;m.SetFloat("_Glossiness",.3f);m.SetFloat("_Metallic",name.Contains("gold")?.45f:0);
  if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.8f);}
  EditorUtility.SetDirty(m);return m;
 }
 static Mesh SaveMesh(string name,Mesh mesh) {
  string path=Dir+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null) AssetDatabase.CreateAsset(mesh,path);
  else {EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
  return mesh;
 }
 // A true rounded cuboid: project a subdivided box onto a box with a spherical edge radius.
 static Mesh RoundedMesh(int steps) {
  var v=new List<Vector3>();var n=new List<Vector3>();var tri=new List<int>();
  Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
  foreach(var normal in normals) {
   Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
   Vector3 w=Vector3.Cross(normal,u); int start=v.Count;
   for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++){
    Vector3 p=normal*.5f+u*((float)x/steps-.5f)+w*((float)y/steps-.5f);
    Vector3 core=new Vector3(Mathf.Clamp(p.x,-.43f,.43f),Mathf.Clamp(p.y,-.43f,.43f),Mathf.Clamp(p.z,-.43f,.43f));
    Vector3 dir=(p-core).normalized;v.Add(core+dir*.07f);n.Add(dir);
   }
   for(int y=0;y<steps;y++)for(int x=0;x<steps;x++){
    int a=start+y*(steps+1)+x,b=a+1,c=a+steps+1,d=c+1;
    tri.Add(a);tri.Add(b);tri.Add(d);tri.Add(a);tri.Add(d);tri.Add(c);
   }
  }
  var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(tri,0);mesh.RecalculateBounds();return SaveMesh("Rounded volume "+steps,mesh);
 }
 static Transform Root(string name,Transform p,Vector3 pos) {
  var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=pos;return t;
 }
 static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,Material mat,bool soft=true) {
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);
  g.transform.localPosition=pos;g.transform.localScale=size;
  g.GetComponent<MeshFilter>().sharedMesh=rounded;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;
 }
 static void Stroke(Transform p,Vector3 a,Vector3 b,float width,Material material) {
  var g=Box("Inscribed stroke",p,(a+b)*.5f,new Vector3(width,.012f,Vector3.Distance(a,b)),material);
  if((b-a).sqrMagnitude>0) g.transform.localRotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
 }
 static void Circle(Transform p,float r,float width,Material m,int count=128) {
  for(int i=0;i<count;i++){
   float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
   Stroke(p,new Vector3(Mathf.Cos(a)*r,.018f,Mathf.Sin(a)*r),new Vector3(Mathf.Cos(b)*r,.018f,Mathf.Sin(b)*r),width,m);
  }
 }
 // Merge each editable shelf/inscription into material groups: many detailed books, few renderers.
 static void Bake(Transform root,string assetName) {
  var groups=new Dictionary<Material,List<CombineInstance>>();
  foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){
   var renderer=mf.GetComponent<MeshRenderer>(); if(renderer==null)continue;
   var mat=renderer.sharedMaterial;if(!groups.ContainsKey(mat))groups.Add(mat,new List<CombineInstance>());
   groups[mat].Add(new CombineInstance{mesh=mf.sharedMesh,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});
  }
  foreach(var child in new List<Transform>(Children(root)))Object.DestroyImmediate(child.gameObject);
  foreach(var pair in groups){
   var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);
   mesh=SaveMesh(assetName+"_"+pair.Key.name,mesh);
   var g=new GameObject(pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);
   g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;g.isStatic=true;
  }
 }
 static IEnumerable<Transform> Children(Transform t){foreach(Transform c in t)yield return c;}
 static void UprightBook(Transform parent,float x,float bottom,float height,float width,float depth,int style) {
  var root=Root("Individual volume",parent,new Vector3(x,bottom+height*.5f,-.42f));
  root.localRotation=Quaternion.Euler(0,R(-4,4),R(-7,7));
  var cover=covers[style%covers.Length];
  Box("Deckled page block",root,new Vector3(0,0,.025f),new Vector3(width*.8f,height*.94f,depth*.88f),paper);
  Box("Left cover",root,new Vector3(-width*.47f,0,0),new Vector3(width*.13f,height,depth),cover);
  Box("Right cover",root,new Vector3(width*.47f,0,0),new Vector3(width*.13f,height,depth),cover);
  Box("Curved spine",root,new Vector3(0,0,-depth*.48f),new Vector3(width,height,depth*.13f),cover);
  int bands=style%3+2;
  for(int i=0;i<bands;i++)Box("Raised binding band",root,new Vector3(0,(i-(bands-1)*.5f)*height/(bands+1),-depth*.55f),new Vector3(width*1.02f,.018f,.028f),gold);
  if(style%4==0)Box("Spine title plate",root,new Vector3(0,.08f,-depth*.57f),new Vector3(width*.6f,height*.22f,.02f),paper);
 }
 static void Shelf(Transform decor,int index,Vector3 pos,float rotation,float height) {
  var root=Root("Library bay "+index,decor,pos);root.localRotation=Quaternion.Euler(0,rotation,0);
  const float width=4.2f;
  Box("Recessed timber",root,new Vector3(0,height/2,.18f),new Vector3(width,height,.3f),wood);
  foreach(float x in new[]{-width/2,width/2}){
   Box("Sculpted pilaster",root,new Vector3(x,height/2,-.25f),new Vector3(.2f,height,.8f),wood);
   Box("Gold edge",root,new Vector3(x,height/2,-.68f),new Vector3(.035f,height,.035f),gold);
  }
  int rows=Mathf.RoundToInt(height/1.05f);
  for(int row=0;row<=rows;row++){
   float y=row*height/rows;
   Box("Shelf moulding",root,new Vector3(0,y,-.3f),new Vector3(width+.12f,.13f,1),wood);
   Box("Fine gold lip",root,new Vector3(0,y+.025f,-.815f),new Vector3(width,.025f,.03f),gold);
   if(row==rows)continue;
   float x=-1.91f;
   while(x<1.85f){
    float thickness=R(.13f,.38f);if(x+thickness>2)break;
    int style=rng.Next(1000);
    if(style%9==0 && x<1.3f){
     for(int k=0;k<3;k++)Box("Horizontal folio stack",root,new Vector3(x+.3f,y+.19f+k*.16f,-.42f),new Vector3(.62f,.14f,R(.56f,.8f)),covers[(style+k)%covers.Length]);
     x+=.77f;
    }else{
     UprightBook(root,x+thickness/2,y+.13f,R(.58f,.88f),thickness,R(.55f,.84f),style);
     x+=thickness+R(.015f,.055f);
    }
   }
  }
  Box("Crown",root,new Vector3(0,height+.12f,-.25f),new Vector3(width+.2f,.24f,1.12f),gold);
  Bake(root,"Bay"+index);
 }
 static void FallingBook(Transform root,int style) {
  var savedMesh=rounded;rounded=detailedRounded;
  var cover=covers[style%covers.Length]; float thick=.54f+style*.035f;
  Box("Soft page edges",root,Vector3.zero,new Vector3(.88f,thick,.86f),paper);
  Box("Upper embossed cover",root,new Vector3(0,thick*.55f,0),new Vector3(1,.09f,1),cover);
  Box("Lower cover",root,new Vector3(0,-thick*.55f,0),new Vector3(1,.09f,1),cover);
  Box("Curved leather spine",root,new Vector3(-.455f,0,0),new Vector3(.16f,thick*1.2f,1),cover);
  foreach(float z in new[]{-.32f,.32f}) Box("Spine rib",root,new Vector3(-.5f,0,z),new Vector3(.07f,thick*1.18f,.045f),gold);
  for(int j=0;j<8;j++)Box("Individual page lines",root,new Vector3(.443f,-thick*.4f+j*thick*.11f,0),new Vector3(.006f,.007f,.8f),gold);
  var emboss=Root("Cover ornament",root,new Vector3(0,thick*.61f,0));
  if(style%2==0){Circle(emboss,.25f,.018f,gold,32);Circle(emboss,.19f,.008f,gold,32);}
  else {
   for(int j=0;j<4;j++){var g=Box("Corner emboss",emboss,new Vector3((j%2==0?-1:1)*.32f,0,(j<2?-1:1)*.32f),new Vector3(.15f,.018f,.15f),gold);g.transform.localRotation=Quaternion.Euler(0,45,0);}
  }
  var gem=Box("Cover gem",emboss,Vector3.up*.025f,new Vector3(.13f,.06f,.18f),glow);gem.transform.localRotation=Quaternion.Euler(0,style*30,0);
  Bake(root,"FallingTome"+style);
  foreach(var r in root.GetComponentsInChildren<Renderer>()) r.gameObject.isStatic=false;
  rounded=savedMesh;
 }
 public static void Apply(){
  System.IO.Directory.CreateDirectory(Dir);rng=new System.Random(73951);
  rounded=RoundedMesh(2); detailedRounded=RoundedMesh(6);
  gold=Mat("Soft champagne gold",new Color(.72f,.59f,.37f));wood=Mat("Dusty plum wood",new Color(.28f,.19f,.31f));paper=Mat("Warm ivory paper",new Color(.83f,.77f,.64f));
  glow=Mat("Soft cyan crystal",new Color(.35f,.73f,.78f),true);ink=Mat("Ancient pale gold",new Color(.64f,.51f,.34f));
  Color[] colors={new Color(.53f,.38f,.65f),new Color(.36f,.57f,.59f),new Color(.69f,.43f,.48f),new Color(.71f,.58f,.39f),new Color(.4f,.46f,.65f),new Color(.6f,.65f,.49f),new Color(.72f,.53f,.66f),new Color(.4f,.27f,.42f),new Color(.54f,.63f,.72f),new Color(.65f,.4f,.31f),new Color(.31f,.48f,.43f),new Color(.74f,.65f,.55f)};
  covers=new Material[colors.Length];for(int i=0;i<covers.Length;i++) covers[i]=Mat("Leather "+i,colors[i]);
  floor=Mat("Pastel gradient marble",new Color(.24f,.2f,.35f));floor.shader=Shader.Find("BlockFall/PastelMarble");floor.SetColor("_Tint",new Color(.62f,.55f,.7f));EditorUtility.SetDirty(floor);
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var old=GameObject.Find("Magic Library Decor");if(old!=null)Object.DestroyImmediate(old);
  var decor=Root("Magic Library Decor",null,Vector3.zero);var game=Object.FindObjectOfType<BlockFallGame>();
  foreach(Transform tile in game.tiles)tile.GetComponent<Renderer>().sharedMaterial=floor;
  for(int i=0;i<4;i++) GameObject.Find("Boundary "+i).GetComponent<Renderer>().sharedMaterial=wood;
  int index=0;
  for(int i=-7;i<=7;i++)Shelf(decor,index++,new Vector3(i*4.2f,0,12.4f),0,R(6.5f,8));
  for(int i=-4;i<=6;i++){
   Shelf(decor,index++,new Vector3(-12.4f,0,i*4.2f),-90,R(5.7f,7.4f));
   Shelf(decor,index++,new Vector3(12.4f,0,i*4.2f),90,R(5.7f,7.4f));
  }
  var inscriptions=Root("Ancient floor inscriptions",decor,Vector3.zero);
  foreach(float r in new[]{3f,3.5f,7.9f,8.8f})Circle(inscriptions,r,.025f,ink);
  for(int i=0;i<56;i++){
   float angle=i*Mathf.PI*2/56;float radius=i%3==0?3.8f:8.35f;
   var rune=Root("Ancient glyph "+i,inscriptions,new Vector3(Mathf.Cos(angle)*radius,.025f,Mathf.Sin(angle)*radius));rune.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
   Vector3[] nodes={new Vector3(-.14f,0,-.2f),new Vector3(0,0,-.2f),new Vector3(.14f,0,-.2f),new Vector3(-.14f,0,0),Vector3.zero,new Vector3(.14f,0,0),new Vector3(-.14f,0,.2f),new Vector3(0,0,.2f),new Vector3(.14f,0,.2f)};
   Stroke(rune,nodes[1],nodes[7],.027f,ink);
   int strokes=3+i%4;for(int j=0;j<strokes;j++){int a=rng.Next(9),b=rng.Next(9);if(a!=b)Stroke(rune,nodes[a],nodes[b],.026f,ink);}
  }
  Bake(inscriptions,"AncientGlyphs");
  int crystalIndex=0;
  foreach(float side in new[]{-1f,1f})for(int i=0;i<5;i++){
   var c=Root("Inset crystal "+crystalIndex,decor,new Vector3(side*10.95f,1.8f+R(-.35f,.5f),-7+i*4.4f+R(-.5f,.5f)));
   Box("Sconce",c,Vector3.zero,new Vector3(.45f,.3f,.5f),gold);
   var crystal=Box("Crystal",c,new Vector3(0,.33f,0),new Vector3(.24f,.62f,.24f),glow);crystal.transform.localRotation=Quaternion.Euler(0,R(0,80),20);
   crystal.AddComponent<BlockFallCrystalGlow>().seed=crystalIndex*2.73f+R(0,2);crystalIndex++;
  }
  for(int i=0;i<4;i++){
   var c=Root("Rear crystal "+i,decor,new Vector3(-8.3f+i*5.1f+R(-.4f,.4f),2.5f+R(-.3f,.5f),11.3f));
   Box("Bracket",c,Vector3.zero,new Vector3(.5f,.15f,.5f),gold);
   var crystal=Box("Crystal",c,Vector3.up*.35f,new Vector3(.3f,.65f,.3f),glow);crystal.transform.localRotation=Quaternion.Euler(15,35,15);crystal.AddComponent<BlockFallCrystalGlow>().seed=30+i*3.17f;
  }
  var visual=new GameObject("Enchanted falling tome");var model=Root("Tome variants",visual.transform,Vector3.zero);
  var variants=new GameObject[6];for(int i=0;i<6;i++){var t=Root("Cover variation "+i,model,Vector3.zero);FallingBook(t,i);variants[i]=t.gameObject;t.gameObject.SetActive(i==0);}
  var warning=Root("Single exclamation warning",visual.transform,Vector3.zero);
  var warningMat=Mat("Warning single symbol",Color.red);warningMat.shader=Shader.Find("BlockFall/Warning");EditorUtility.SetDirty(warningMat);
  Box("Exclamation stem",warning,new Vector3(0,.01f,.08f),new Vector3(.075f,.012f,.4f),warningMat);
  Box("Exclamation dot",warning,new Vector3(0,.01f,-.22f),new Vector3(.085f,.012f,.085f),warningMat);
  var view=visual.AddComponent<BlockFallVisual>();view.model=model.gameObject;view.warning=warning.gameObject;view.variants=variants;
  game.blockVisual=PrefabUtility.SaveAsPrefabAsset(visual,"Assets/Prefabs/BlockFall/BlockVisual.prefab");Object.DestroyImmediate(visual);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  var camera=Camera.main;var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();System.IO.File.WriteAllBytes("BlockFall-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
  Debug.Log("MAGIC_LIBRARY_PASS: pastel library, merged varied books, rounded tomes, single animated warning.");
 }
}
}
#endif
