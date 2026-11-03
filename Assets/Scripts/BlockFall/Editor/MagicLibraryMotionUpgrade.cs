#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryMotionUpgrade {
 const string Dir="Assets/Materials/BlockFall/MagicLibrary/";
 static Mesh rounded;
 static Material paper,gold,wood,atlas;
 static System.Random random=new System.Random(9204);
 static float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
 static GameObject MeshObject(string name,Transform p,Mesh mesh,Material mat,Vector3 pos,Vector3 scale){
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;
  g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=mat;return g;
 }
 static void Box(string name,Transform p,Vector3 pos,Vector3 scale,Material mat)=>MeshObject(name,p,rounded,mat,pos,scale);
 static Mesh Save(string name,Mesh mesh){
  var path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}return mesh;
 }
 static Mesh Jacket(int index){
  float left=index%6/6f+.005f,right=(index%6+1)/6f-.005f,top=1-(.004f+index/6*.2325f),bottom=top-.224f;
  var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
  mesh.uv=new[]{new Vector2(left,bottom),new Vector2(right,bottom),new Vector2(right,top),new Vector2(left,top)};
  mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();return Save("Shelf jacket "+index,mesh);
 }
 static Mesh Crystal(){
  var vertices=new List<Vector3>();var triangles=new List<int>();
  for(int i=0;i<8;i++){
   float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
   Vector3 p=new Vector3(Mathf.Cos(a)*.43f,-.08f,Mathf.Sin(a)*.43f),q=new Vector3(Mathf.Cos(b)*.43f,-.08f,Mathf.Sin(b)*.43f);
   Vector3 r=new Vector3(Mathf.Cos(a)*.3f,.17f,Mathf.Sin(a)*.3f),s=new Vector3(Mathf.Cos(b)*.3f,.17f,Mathf.Sin(b)*.3f);
   Add(vertices,triangles,new Vector3(0,-.6f,0),q,p);
   Add(vertices,triangles,p,q,s);Add(vertices,triangles,p,s,r);
   Add(vertices,triangles,r,s,new Vector3(0,.65f,0));
  }
  var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return Save("Faceted eight sided crystal",mesh);
 }
 static void Add(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c){int i=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(i);t.Add(i+2);t.Add(i+1);}
 static void Combine(Transform root,string name){
  var groups=new Dictionary<Material,List<CombineInstance>>();
  foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){
   var mat=mf.GetComponent<Renderer>().sharedMaterial;if(!groups.ContainsKey(mat))groups.Add(mat,new List<CombineInstance>());
   groups[mat].Add(new CombineInstance{mesh=mf.sharedMesh,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});
  }
  var children=new List<GameObject>();foreach(Transform c in root)children.Add(c.gameObject);foreach(var c in children)Object.DestroyImmediate(c);
  foreach(var pair in groups){
   var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh=Save(name+"_"+pair.Key.name,mesh);
   MeshObject(pair.Key.name,root,mesh,pair.Key,Vector3.zero,Vector3.one).isStatic=true;
  }
 }
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");var decor=GameObject.Find("Magic Library Decor").transform;
  rounded=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Rounded volume 2.asset");
  paper=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Aged layered parchment.mat");gold=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Aged fine bronze.mat");wood=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Dusty plum wood.mat");atlas=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Illustrated covers atlas.mat");
  var jackets=new Mesh[24];for(int i=0;i<24;i++)jackets[i]=Jacket(i);
  bool first=GameObject.Find("Extra shelf tier applied")==null;
  AssetDatabase.StartAssetEditing();
  try {
   int bayIndex=0;
   foreach(Transform bay in decor){
    if(!bay.name.StartsWith("Library bay "))continue;
    var rs=bay.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
    float oldHeight=bounds.max.y-bay.position.y;
    float height=oldHeight+(first?1.5f:0);
    int rows=Mathf.RoundToInt(height/1.5f);
    var children=new List<GameObject>();foreach(Transform c in bay)children.Add(c.gameObject);foreach(var c in children)Object.DestroyImmediate(c);
    bay.localScale=Vector3.one;
    Box("Recessed walnut wall",bay,new Vector3(0,height/2,.18f),new Vector3(4.2f,height,.25f),wood);
    foreach(float side in new[]{-1f,1f})Box("Finished pilaster",bay,new Vector3(side*2.1f,height/2,-.3f),new Vector3(.18f,height,1),wood);
    for(int row=0;row<=rows;row++){
     float y=row*height/rows;
     Box("Shelf",bay,new Vector3(0,y,-.3f),new Vector3(4.25f,.14f,1.05f),wood);
     Box("Brass shelf lip",bay,new Vector3(0,y+.02f,-.83f),new Vector3(4.2f,.025f,.035f),gold);
     if(row==rows)continue;
     float x=-1.95f;
     while(x<1.8f){
      float w=R(.17f,.33f),h=R(.85f,1.21f);int variant=random.Next(24);
      var root=new GameObject("Matching grimoire "+variant).transform;root.SetParent(bay,false);root.localPosition=new Vector3(x+w*.5f,y+.14f+h*.5f,-.36f);root.localRotation=Quaternion.Euler(0,R(-3,3),R(-4,4));
      var leather=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Ornate leather "+variant+".mat");
      Box("Page edges",root,Vector3.zero,new Vector3(w*.82f,h*.94f,.67f),paper);
      foreach(float sign in new[]{-1f,1f})Box("Rounded cover",root,new Vector3(sign*w*.47f,0,0),new Vector3(w*.12f,h,.75f),leather);
      Box("Curved spine",root,new Vector3(0,0,-.36f),new Vector3(w,h,.09f),leather);
      MeshObject("Matching illustrated binding",root,jackets[variant],atlas,new Vector3(0,0,-.411f),new Vector3(w*.9f,h*.95f,1));
      for(int band=0;band<3;band++)Box("Binding rib",root,new Vector3(0,(band-1)*h*.32f,-.42f),new Vector3(w,.018f,.02f),gold);
      x+=w+R(.025f,.055f);
     }
    }
    Box("Carved crown",bay,new Vector3(0,height+.1f,-.3f),new Vector3(4.35f,.2f,1.12f),gold);
    Combine(bay,"MatchedGrimoireBay"+bayIndex++);
   }
  }finally{AssetDatabase.StopAssetEditing();}
  if(first){
   var marker=new GameObject("Extra shelf tier applied");marker.transform.SetParent(decor,false);
   foreach(var name in new[]{"Library finishing","Library polish"}){
    var group=GameObject.Find(name);if(group==null)continue;
    foreach(Transform t in group.transform){
     if(t.name.Contains("wall divider")){
      var wall=t.Find("Walnut separation wall");if(wall!=null){wall.localScale+=new Vector3(0,1.5f,0);wall.localPosition+=Vector3.up*.75f;}
     }else if(name=="Library finishing" && (t.name.Contains("Corner")||t.name.Contains("Pilaster"))){
      bool crown=t.name.Contains("capital")||t.name.Contains("crown");
      t.localPosition+=Vector3.up*(crown?1.5f:.75f);
      if(!crown)t.localScale+=new Vector3(0,1.5f,0);
     }
    }
   }
  }
  var crystalMesh=Crystal();
  foreach(var glow in Object.FindObjectsOfType<BlockFallCrystalGlow>()){
   glow.GetComponent<MeshFilter>().sharedMesh=crystalMesh;glow.transform.localScale=new Vector3(.34f,.8f,.34f);glow.transform.localRotation=Quaternion.Euler(0,glow.seed*37,0);
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("LIBRARY_MOTION_UPGRADE_PASS: extra tier, shared 24 grimoire bindings, faceted crystals. Bay count: "+CountBays(decor));
 }
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var mesh=Crystal();
  foreach(var glow in Object.FindObjectsOfType<BlockFallCrystalGlow>())glow.GetComponent<MeshFilter>().sharedMesh=mesh;
  var game=Object.FindObjectOfType<BlockFallGame>();
  var sample=Object.Instantiate(game.blockVisual);sample.hideFlags=HideFlags.HideAndDontSave;
  var view=sample.GetComponent<BlockFallVisual>();
  view.Animate(.3f,game.fallTime,game.warningTime,27);var moving=view.model.transform.localRotation;
  view.Animate(0,game.fallTime,game.warningTime,27);
  if(Quaternion.Angle(moving,Quaternion.identity)<1 || Quaternion.Angle(view.model.transform.localRotation,Quaternion.identity)>.1f)
   throw new System.Exception("Falling motion must rotate and settle before impact");
  if(GameObject.Find("Extra shelf tier applied")==null)throw new System.Exception("Extra shelf tier missing");
  Object.DestroyImmediate(sample);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("LIBRARY_MOTION_VALIDATION_PASS: outward crystal facets, extra tier, animated tumble and aligned impact.");
 }
 static int CountBays(Transform t){int n=0;foreach(Transform c in t)if(c.name.StartsWith("Library bay "))n++;return n;}
}
}
#endif
