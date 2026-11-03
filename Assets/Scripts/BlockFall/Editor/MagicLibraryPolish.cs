#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryPolish {
 const string Dir="Assets/Materials/BlockFall/MagicLibrary/";
 static Mesh rounded;static Material paper,gold,atlas;
 static Material Mat(string name,Color color,string shader="Standard"){
  var path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
  m.shader=Shader.Find(shader);m.color=color;if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.3f);
  EditorUtility.SetDirty(m);return m;
 }
 static Transform Root(string name,Transform p,Vector3 position){
  var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=position;return t;
 }
 static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,Material mat){
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;
  g.GetComponent<MeshFilter>().sharedMesh=rounded;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;
 }
 static Mesh Save(string name,Mesh mesh){
  string path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}return mesh;
 }
 static void Bake(Transform root,int index){
  var groups=new Dictionary<Material,List<CombineInstance>>();
  foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){
   var m=mf.GetComponent<Renderer>().sharedMaterial;if(!groups.ContainsKey(m))groups.Add(m,new List<CombineInstance>());
   groups[m].Add(new CombineInstance{mesh=mf.sharedMesh,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});
  }
  var children=new List<Transform>();foreach(Transform c in root)children.Add(c);foreach(var c in children)Object.DestroyImmediate(c.gameObject);
  foreach(var pair in groups){
   var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh=Save("OrnateTome"+index+"_"+pair.Key.name,mesh);
   var g=new GameObject(pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=pair.Key;
  }
 }
 static void CoverFace(Transform root,int index,float height){
  const int xs=10,zs=14;var v=new Vector3[(xs+1)*(zs+1)];var uv=new Vector2[v.Length];var tri=new List<int>();
  float u0=(index%6)/6f+.004f,u1=(index%6+1)/6f-.004f;
  float top=1-(.004f+index/6*.2325f),bottom=top-.224f;
  for(int z=0;z<=zs;z++)for(int x=0;x<=xs;x++){
   float a=(float)x/xs,b=(float)z/zs;int i=z*(xs+1)+x;
   v[i]=new Vector3((a-.5f)*.85f,height+.007f*Mathf.Sin(a*Mathf.PI), (b-.5f)*.96f);
   uv[i]=new Vector2(Mathf.Lerp(u0,u1,a),Mathf.Lerp(bottom,top,b));
   if(x<xs && z<zs){int n=i+xs+1;tri.Add(i);tri.Add(n);tri.Add(i+1);tri.Add(i+1);tri.Add(n);tri.Add(n+1);}
  }
  var mesh=new Mesh();mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh=Save("CoverFace"+index,mesh);
  var g=new GameObject("Embossed illustrated leather",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=atlas;
 }
 static void Tome(Transform root,int index){
  Color[] colors={new Color(.27f,.07f,.05f),new Color(.045f,.07f,.12f),new Color(.045f,.16f,.1f),new Color(.17f,.065f,.22f),new Color(.21f,.12f,.055f),new Color(.28f,.05f,.12f)};
  Color color=colors[index%6]*Mathf.Lerp(.85f,1.2f,(index/6)/3f);color.a=1;
  var leather=Mat("Ornate leather "+index,color);float thick=.5f+(index%5)*.035f;
  Box("Rounded deckled paper",root,Vector3.zero,new Vector3(.83f,thick,.94f),paper);
  Box("Curved upper cover",root,new Vector3(0,thick*.54f,0),new Vector3(.92f,.085f,1.04f),leather);
  Box("Curved lower cover",root,new Vector3(0,-thick*.54f,0),new Vector3(.92f,.085f,1.04f),leather);
  Box("Curved spine",root,new Vector3(-.425f,0,0),new Vector3(.16f,thick*1.2f,1.035f),leather);
  for(int i=0;i<5;i++) Box("Raised spine bands",root,new Vector3(-.47f,0,-.39f+i*.195f),new Vector3(.065f,thick*1.23f,.027f),gold);
  for(int i=0;i<14;i++)Box("Fine page edges",root,new Vector3(.416f,-thick*.44f+i*thick*.067f,0),new Vector3(.005f,.003f,.88f),gold);
  Box("Book clasp",root,new Vector3(.45f,.1f,index%2==0?.18f:-.18f),new Vector3(.07f,.12f,.09f),gold);
  foreach(float x in new[]{-.38f,.38f})foreach(float z in new[]{-.45f,.45f}){
   Box("Cast corner guard",root,new Vector3(x,thick*.59f,z),new Vector3(.13f,.028f,.13f),gold);
  }
  CoverFace(root,index,thick*.54f+.046f);
  Bake(root,index);
 }
 static void Square(Transform parent,Material mat){
  var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)};
  mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh=Save("Warning full tile",mesh);
  var g=new GameObject("Full red tile",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=mat;
 }
 public static void Apply(){
  AssetDatabase.Refresh();
  rounded=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Rounded volume 6.asset");
  gold=Mat("Aged fine bronze",new Color(.48f,.33f,.13f));gold.SetFloat("_Metallic",.65f);
  paper=Mat("Aged layered parchment",new Color(.63f,.54f,.38f));
  atlas=Mat("Illustrated covers atlas",Color.white);
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/BlockFall/GrimoireCovers24.png");atlas.mainTexture=texture;atlas.SetFloat("_Metallic",.18f);EditorUtility.SetDirty(atlas);
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();var decor=GameObject.Find("Magic Library Decor").transform;
  var marker=GameObject.Find("Library polish");
  bool first=marker==null;
  if(marker!=null)Object.DestroyImmediate(marker);
  var polish=Root("Library polish",decor,Vector3.zero);
  if(first){
   foreach(Transform t in decor) if(t.name.StartsWith("Library bay ")) t.localScale=new Vector3(t.localScale.x,t.localScale.y*1.5f,t.localScale.z);
   var finishing=GameObject.Find("Library finishing").transform;
   foreach(Transform t in finishing) if(t.name.Contains("Corner")||t.name.Contains("Pilaster")){
    t.localPosition=new Vector3(t.localPosition.x,t.localPosition.y*1.5f,t.localPosition.z);
    t.localScale=new Vector3(t.localScale.x,t.localScale.y*1.5f,t.localScale.z);
   }
  }
  var oldCrystals=new List<GameObject>();
  foreach(Transform t in decor)if(t.name.StartsWith("Inset crystal")||t.name.StartsWith("Rear crystal"))oldCrystals.Add(t.gameObject);
  foreach(var c in oldCrystals)Object.DestroyImmediate(c);
  var wall=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Dusty plum wood.mat");
  var crystalMat=Mat("Blue pulsing crystal",new Color(.1f,.32f,.62f));crystalMat.EnableKeyword("_EMISSION");crystalMat.SetColor("_EmissionColor",new Color(.15f,.5f,1));EditorUtility.SetDirty(crystalMat);
  int lightIndex=0;
  for(int i=0;i<6;i++){
   float x=-10.5f+i*4.2f;
   var column=Root("Rear wall divider "+i,polish,new Vector3(x,0,11.72f));
   Box("Walnut separation wall",column,new Vector3(0,5.8f,0),new Vector3(.34f,11.6f,.45f),wall);
   Sconce(column,new Vector3(0,2.7f+(i%2)*.6f,-.38f),lightIndex++,crystalMat);
  }
  foreach(float side in new[]{-1f,1f})for(int i=0;i<5;i++){
   var column=Root("Side wall divider "+lightIndex,polish,new Vector3(side*11.72f,0,-10.5f+i*4.2f));
   Box("Walnut separation wall",column,new Vector3(0,5.9f,0),new Vector3(.45f,11.8f,.34f),wall);
   Sconce(column,new Vector3(-side*.4f,2.3f+(i%3)*.45f,0),lightIndex++,crystalMat);
  }
  var sun=GameObject.Find("Sun").GetComponent<Light>();sun.intensity=.8f;sun.color=new Color(.83f,.81f,1);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
  RenderSettings.ambientSkyColor=new Color(.32f,.33f,.43f);RenderSettings.ambientEquatorColor=new Color(.23f,.22f,.3f);RenderSettings.ambientGroundColor=new Color(.1f,.085f,.13f);
  var visual=new GameObject("Ornate grimoire collection");var model=Root("24 distinct tomes",visual.transform,Vector3.zero);
  var variants=new GameObject[24];
  AssetDatabase.StartAssetEditing();
  try{
   for(int i=0;i<24;i++){var t=Root("Grimoire "+(i+1),model,Vector3.zero);Tome(t,i);variants[i]=t.gameObject;t.gameObject.SetActive(i==0);}
  }finally{AssetDatabase.StopAssetEditing();}
  var warning=Root("Red tile with white exclamation",visual.transform,Vector3.zero);
  var red=Mat("Red warning tile",new Color(1,.035f,.025f),"BlockFall/Warning");
  var white=Mat("White warning symbol",Color.white,"BlockFall/Warning");
  Square(warning,red);
  Box("White exclamation stem",warning,new Vector3(0,.008f,.09f),new Vector3(.075f,.006f,.4f),white);
  Box("White exclamation dot",warning,new Vector3(0,.008f,-.22f),new Vector3(.085f,.006f,.085f),white);
  var view=visual.AddComponent<BlockFallVisual>();view.model=model.gameObject;view.warning=warning.gameObject;view.variants=variants;
  game.blockVisual=PrefabUtility.SaveAsPrefabAsset(visual,"Assets/Prefabs/BlockFall/BlockVisual.prefab");Object.DestroyImmediate(visual);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Capture(Camera.main,"BlockFall-preview.png");
  ValidateAndPreview(game);
  Debug.Log("MAGIC_LIBRARY_POLISH_PASS: 24 textured tomes, full-tile red/white warnings, real blue lights, 1.5x shelves.");
 }
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var sun=GameObject.Find("Sun").GetComponent<Light>();sun.intensity=.8f;
  RenderSettings.ambientSkyColor=new Color(.32f,.33f,.43f);RenderSettings.ambientEquatorColor=new Color(.23f,.22f,.3f);
  foreach(var light in Object.FindObjectsOfType<Light>())if(light.type==LightType.Point)light.intensity=1.2f;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Capture(Camera.main,"BlockFall-preview.png");
  ValidateAndPreview(Object.FindObjectOfType<BlockFallGame>());
 }
 static void ValidateAndPreview(BlockFallGame game){
  var sample=Object.Instantiate(game.blockVisual);sample.hideFlags=HideFlags.HideAndDontSave;
  var view=sample.GetComponent<BlockFallVisual>();
  if(view.variants.Length!=24)throw new System.Exception("Expected 24 grimoire variants");
  sample.transform.position=new Vector3(-2,.055f,-2);sample.transform.localScale=new Vector3(4,2,4);
  view.Animate(1f,game.fallTime,game.warningTime,0);
  var panel=view.warning.transform.Find("Full red tile").GetComponent<Renderer>();
  if(Mathf.Abs(panel.bounds.size.x-4)>.001f || Mathf.Abs(panel.bounds.size.z-4)>.001f)throw new System.Exception("Warning must cover full tile");
  var white=view.warning.transform.Find("White exclamation stem").GetComponent<Renderer>();
  var property=new MaterialPropertyBlock();white.GetPropertyBlock(property);
  Color c=property.GetColor("_Color");
  if(c.r<.99f || c.g<.99f || c.b<.99f)throw new System.Exception("Exclamation must remain white");
  Object.DestroyImmediate(sample);
  var samples=new List<GameObject>();
  float[] times={1.55f,1.1f,.7f,.4f,.2f,.06f};
  for(int i=0;i<times.Length;i++){
   var g=Object.Instantiate(game.blockVisual);g.hideFlags=HideFlags.HideAndDontSave;
   g.transform.position=new Vector3(-4+(i%3)*4,.055f,-4+(i/3)*4);g.transform.localScale=new Vector3(4,2,4);
   g.GetComponent<BlockFallVisual>().Animate(times[i],game.fallTime,game.warningTime,i*4);samples.Add(g);
  }
  Capture(Camera.main,"BlockFall-warning-preview.png");foreach(var g in samples)Object.DestroyImmediate(g);
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Debug.Log("BLOCKFALL_WARNING_VALIDATION_PASS: 24 variants, exact 4x4 warning footprint, white symbol, visual samples.");
 }
 static void Sconce(Transform p,Vector3 pos,int index,Material mat){
  var t=Root("Recessed blue sconce "+index,p,pos);
  var back=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Dusty plum wood.mat");
  Box("Wall recess",t,Vector3.zero,new Vector3(.5f,.8f,.34f),back);
  Box("Bronze mounting",t,new Vector3(0,-.35f,0),new Vector3(.45f,.1f,.42f),gold);
  var crystal=Box("Crystal",t,new Vector3(0,.05f,-.19f),new Vector3(.17f,.55f,.17f),mat);crystal.transform.localRotation=Quaternion.Euler(10,index*37,15);
  var glow=crystal.AddComponent<BlockFallCrystalGlow>();glow.seed=index*2.71f;
  var l=new GameObject("Pulsing blue light").AddComponent<Light>();l.transform.SetParent(t,false);l.transform.localPosition=new Vector3(0,.1f,-.7f);
  l.type=LightType.Point;l.color=new Color(.12f,.42f,1);l.range=5.5f;l.intensity=.65f;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.Auto;
  glow.source=l;glow.peakIntensity=1.7f;
 }
 public static void Capture(Camera camera,string filename){
  var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var img=new Texture2D(1600,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1600,900),0,0);img.Apply();System.IO.File.WriteAllBytes(filename,img.EncodeToPNG());
  camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);
 }
}
}
#endif
