#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class MagicLibraryContactLayout {
 static Bounds BoundsOf(Transform t){var rs=t.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");var decor=GameObject.Find("Magic Library Decor").transform;
  var front=new List<Transform>();float left=float.NegativeInfinity,right=float.PositiveInfinity,leftEnd=float.NegativeInfinity,rightEnd=float.NegativeInfinity;
  foreach(Transform bay in decor){
   if(!bay.name.StartsWith("Library bay "))continue;var b=BoundsOf(bay);
   if(Mathf.Abs(Mathf.DeltaAngle(bay.eulerAngles.y,0))<1)front.Add(bay);
   else if(bay.position.x<0){left=Mathf.Max(left,b.max.x);leftEnd=Mathf.Max(leftEnd,b.max.z);}
   else {right=Mathf.Min(right,b.min.x);rightEnd=Mathf.Max(rightEnd,b.max.z);}
  }
  if(front.Count!=6 || !float.IsFinite(left) || !float.IsFinite(right))throw new Exception("Expected six front and two side bookshelf walls");
  front.Sort((a,b)=>a.position.x.CompareTo(b.position.x));
  float width=right-left+.08f;float minZ=float.PositiveInfinity;
  foreach(var mf in front[0].GetComponentsInChildren<MeshFilter>())minZ=Mathf.Min(minZ,mf.sharedMesh.bounds.min.z);
  float z=Mathf.Min(leftEnd,rightEnd)-minZ*front[0].localScale.z-.025f;
  for(int i=0;i<front.Count;i++){
   float min=float.PositiveInfinity,max=float.NegativeInfinity;
   foreach(var mf in front[i].GetComponentsInChildren<MeshFilter>()){min=Mathf.Min(min,mf.sharedMesh.bounds.min.x);max=Mathf.Max(max,mf.sharedMesh.bounds.max.x);}
   front[i].localScale=new Vector3(width/6/(max-min),front[i].localScale.y,front[i].localScale.z);
   front[i].position=new Vector3((left+right)*.5f+(i-2.5f)*width/6,front[i].position.y,z);
  }
  var group=GameObject.Find("Front and high sconces").transform;
  float oldMin=float.PositiveInfinity,oldMax=float.NegativeInfinity;
  foreach(Transform t in group)if(t.name.StartsWith("Front divider ")){oldMin=Mathf.Min(oldMin,t.position.x);oldMax=Mathf.Max(oldMax,t.position.x);}
  foreach(Transform t in group){
   bool divider=t.name.StartsWith("Front divider ");
   bool crystal=t.name.StartsWith("Upper crystal ") && t.position.z>10;
   if(!divider && !crystal)continue;
   float amount=Mathf.InverseLerp(oldMin,oldMax,t.position.x);
   t.position=new Vector3(Mathf.Lerp(left-.04f,right+.04f,amount),t.position.y,z-(divider?.6f:.9f));
  }
  float frontLeft=BoundsOf(front[0]).min.x,frontRight=BoundsOf(front[5]).max.x;
  if(frontLeft>left || frontRight<right || BoundsOf(front[0]).min.z>Mathf.Min(leftEnd,rightEnd))
   throw new Exception("Bookshelf corner gap remains");
  var game=Object.FindObjectOfType<BlockFallGame>();
  var sample=Object.Instantiate(game.blockVisual);sample.hideFlags=HideFlags.HideAndDontSave;
  var view=sample.GetComponent<BlockFallVisual>();
  view.Animate(1.2f,game.fallTime,game.warningTime,5);
  if(view.TryGetDamageBounds(out var bounds))throw new Exception("Warning-only phase must not deal damage");
  view.Animate(-.4f,game.fallTime,game.warningTime,5);
  if(!view.TryGetDamageBounds(out bounds))throw new Exception("Resting book must remain dangerous");
  if(Quaternion.Angle(view.model.transform.localRotation,Quaternion.identity)>.01f)throw new Exception("Bounce must settle in half the original time");
  var near=new Bounds(bounds.center,Vector3.one*.1f);var far=new Bounds(bounds.center+Vector3.right*20,Vector3.one);
  if(!bounds.Intersects(near) || bounds.Intersects(far))throw new Exception("Book contact test failed");
  Object.DestroyImmediate(sample);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("BOOK_CONTACT_LAYOUT_PASS: front corners overlap sides; warning safe, resting book lethal, half-time motion verified.");
 }
}
}
#endif
