#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class BlockFallUniformGrid {
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();var boundary=game.GetComponent<BlockFallArenaBoundary>();
  Vector2 originalMin=boundary.minimum,originalMax=boundary.maximum;
  var matrices=new Dictionary<Transform,Matrix4x4>();
  foreach(var r in Object.FindObjectsOfType<Renderer>())matrices[r.transform]=r.transform.localToWorldMatrix;
  var collisionSnapshot=new Dictionary<Collider,Bounds>();
  foreach(var c in GameObject.Find("Library Collision Bounds").GetComponentsInChildren<Collider>())collisionSnapshot[c]=c.bounds;
  float width=(boundary.maximum.x-boundary.minimum.x)/6f,depth=(boundary.maximum.y-boundary.minimum.y)/6f;
  var seen=new HashSet<int>();
  foreach(Transform cell in game.tiles){
   var parts=cell.name.Split('_');int row=int.Parse(parts[1]),col=int.Parse(parts[2]);
   if(row<0 || row>=6 || col<0 || col>=6 || !seen.Add(row*6+col))throw new Exception("Invalid grid indices");
   var area=cell.GetComponent<BlockFallCellArea>();if(area==null)area=cell.gameObject.AddComponent<BlockFallCellArea>();
   var floorBounds=cell.GetComponent<Renderer>().bounds;
   area.center=new Vector3(boundary.minimum.x+(col+.5f)*width,floorBounds.center.y,boundary.minimum.y+(row+.5f)*depth);
   area.size=new Vector3(width,floorBounds.size.y,depth);
   EditorUtility.SetDirty(area);
  }
  if(seen.Count!=36)throw new Exception("Expected 36 equal cells");
  var path="Assets/Prefabs/BlockFall/BlockVisual.prefab";
  var prefab=PrefabUtility.LoadPrefabContents(path);var style=prefab.GetComponent<BlockFallVisual>();
  foreach(var variant in style.variants){
   Bounds bounds=default;bool first=true;
   foreach(var mf in variant.GetComponentsInChildren<MeshFilter>()){
    if(first){bounds=mf.sharedMesh.bounds;first=false;}else bounds.Encapsulate(mf.sharedMesh.bounds);
   }
   variant.transform.localScale=new Vector3(.98f/bounds.size.x,1,.98f/bounds.size.z);
   variant.transform.localPosition=new Vector3(-bounds.center.x*variant.transform.localScale.x,0,-bounds.center.z*variant.transform.localScale.z);
  }
  PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
  foreach(var pair in matrices)if(pair.Key.localToWorldMatrix!=pair.Value)throw new Exception("Visible map changed");
  foreach(var pair in collisionSnapshot)if(pair.Key.bounds!=pair.Value)throw new Exception("Outer collision bounds changed");
  if(boundary.minimum!=originalMin || boundary.maximum!=originalMax)throw new Exception("Outer size changed");
  float total=0;
  foreach(Transform cell in game.tiles){
   var a=cell.GetComponent<BlockFallCellArea>();
   if(Mathf.Abs(a.size.x-width)>.001f || Mathf.Abs(a.size.z-depth)>.001f)throw new Exception("Unequal cells");
   total+=a.size.x*a.size.z;
  }
  if(Mathf.Abs(total-(originalMax.x-originalMin.x)*(originalMax.y-originalMin.y))>.01f)throw new Exception("Floor coverage mismatch");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  var samples=new List<GameObject>();
  foreach(Transform cell in game.tiles){
   var a=cell.GetComponent<BlockFallCellArea>();
   var sample=Object.Instantiate(game.blockVisual);sample.hideFlags=HideFlags.HideAndDontSave;
   sample.transform.position=a.center+Vector3.up*(a.size.y*.5f+.035f);
   sample.transform.localScale=new Vector3(a.size.x,2,a.size.z);
   var visual=sample.GetComponent<BlockFallVisual>();visual.Animate(1.2f,game.fallTime,game.warningTime,0);samples.Add(sample);
  }
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-grid-preview.png");
  foreach(var sample in samples)Object.DestroyImmediate(sample);
  EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Debug.Log("UNIFORM_FLOOR_GRID_PASS: outer map unchanged; entire floor split equally into 6x6; cell "+width+" x "+depth+"; normalized books and clipped landing hitboxes.");
 }
}
}
#endif
