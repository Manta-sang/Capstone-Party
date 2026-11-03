#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryFrontLayout {
 const string Dir="Assets/Materials/BlockFall/MagicLibrary/";
 static GameObject Box(string name,Transform p,Vector3 position,Vector3 scale,Material mat){
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Rounded volume 2.asset");
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.position=position;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=mat;return g;
 }
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var decor=GameObject.Find("Magic Library Decor").transform;
  var rear=new List<Transform>();
  foreach(Transform bay in decor)if(bay.name.StartsWith("Library bay ") && Mathf.Abs(Mathf.DeltaAngle(bay.eulerAngles.y,0))<1)rear.Add(bay);
  rear.Sort((a,b)=>a.position.x.CompareTo(b.position.x));
  if(rear.Count==5){var clone=Object.Instantiate(rear[2].gameObject,decor).transform;clone.name="Library bay Front 6";rear.Add(clone);}
  if(rear.Count!=6)throw new Exception("Expected six front bookshelf bays");
  float maxHeight=0;
  for(int i=0;i<rear.Count;i++){
   float minX=float.PositiveInfinity,maxX=float.NegativeInfinity;
   foreach(var mf in rear[i].GetComponentsInChildren<MeshFilter>()){
    var b=mf.sharedMesh.bounds;minX=Mathf.Min(minX,b.min.x);maxX=Mathf.Max(maxX,b.max.x);
   }
   float scale=3.8f/(maxX-minX);
   rear[i].localScale=new Vector3(scale,rear[i].localScale.y,rear[i].localScale.z);
   rear[i].position=new Vector3((i-2.5f)*3.8f,rear[i].position.y,12.4f);
   foreach(var r in rear[i].GetComponentsInChildren<Renderer>())maxHeight=Mathf.Max(maxHeight,r.bounds.max.y);
  }
  var old=GameObject.Find("Front and high sconces");if(old!=null)Object.DestroyImmediate(old);
  var group=new GameObject("Front and high sconces").transform;group.SetParent(decor,false);
  var wall=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Dusty plum wood.mat");var brass=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Aged fine bronze.mat");
  // Update the six former front seams to match seven edges of the six bays.
  var polish=GameObject.Find("Library polish").transform;var oldColumns=new List<GameObject>();
  foreach(Transform t in polish)if(t.name.StartsWith("Rear wall divider"))oldColumns.Add(t.gameObject);
  foreach(var c in oldColumns)Object.DestroyImmediate(c);
  for(int i=0;i<=6;i++){
   float x=(i-3)*3.8f;
   Box("Front divider "+i,group,new Vector3(x,maxHeight/2,11.8f),new Vector3(.25f,maxHeight,.35f),wall);
   if(i%2==0)Sconce(group,new Vector3(x,2.5f+(i%3)*.35f,11.5f),i,0,wall,brass);
   if(i>0 && i<6)Sconce(group,new Vector3(x,6.2f+(i%2)*.65f,11.5f),i+10,0,wall,brass);
   if(i==1 || i==4)Sconce(group,new Vector3(x,maxHeight-1.5f+(i%2)*.25f,11.5f),i+20,0,wall,brass);
  }
  foreach(float side in new[]{-1f,1f})for(int i=0;i<4;i++){
   float z=-7.4f+i*4.8f+(side<0?.35f:-.2f);
   float y=i%2==0?6.1f+i*.17f:9.3f-i*.12f;
   Sconce(group,new Vector3(side*11.45f,y,z),i+(side<0?31:47),side<0?-90:90,wall,brass);
  }
  var game=Object.FindObjectOfType<BlockFallGame>();
  var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/BlockFall/BlockVisual.prefab");
  prefab.GetComponent<BlockFallVisual>().spawnHeight=18;
  PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/BlockFall/BlockVisual.prefab");PrefabUtility.UnloadPrefabContents(prefab);
  var sample=Object.Instantiate(game.blockVisual);sample.hideFlags=HideFlags.HideAndDontSave;var view=sample.GetComponent<BlockFallVisual>();
  view.Animate(.45f,game.fallTime,game.warningTime,9);float high=view.model.transform.localPosition.y*2;
  view.Animate(-.08f,game.fallTime,game.warningTime,9);float bounce=view.model.transform.localPosition.y*2;
  if(high<17.9f || bounce<=.8f)throw new Exception("Spawn height or bounce invalid");
  Object.DestroyImmediate(sample);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("FRONT_LAYOUT_PASS: six front bays, width 22.8, middle/high sconces, spawn 18 and impact bounce verified.");
 }
 static void Sconce(Transform p,Vector3 pos,int seed,float yaw,Material wall,Material brass){
  var root=new GameObject("Upper crystal "+seed).transform;root.SetParent(p,false);root.position=pos;root.localRotation=Quaternion.Euler(0,yaw,0);
  Box("Walnut inset",root,pos,new Vector3(.36f,.9f,.3f),wall);
  var mount=Box("Mount",root,pos,new Vector3(.44f,.09f,.42f),brass);mount.transform.localPosition=new Vector3(0,-.37f,0);
  var crystal=new GameObject("Faceted crystal",typeof(MeshFilter),typeof(MeshRenderer));crystal.transform.SetParent(root,false);crystal.transform.localPosition=new Vector3(0,0,-.24f);crystal.transform.localScale=new Vector3(.34f,.8f,.34f);
  crystal.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Faceted eight sided crystal.asset");crystal.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Blue pulsing crystal.mat");
  var glow=crystal.AddComponent<BlockFallCrystalGlow>();glow.seed=seed*2.73f;
  var light=new GameObject("Blue illumination").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.localPosition=new Vector3(0,0,-.6f);light.type=LightType.Point;light.color=new Color(.12f,.42f,1);light.range=4.8f;light.intensity=.7f;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForceVertex;glow.source=light;glow.peakIntensity=.9f;
 }
}
}
#endif
