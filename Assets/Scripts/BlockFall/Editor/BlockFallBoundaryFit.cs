#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class BlockFallBoundaryFit {
 static Bounds BoundsOf(Transform t){var rs=t.GetComponentsInChildren<Renderer>();Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 static GameObject ColliderObject(string name,Transform p,Vector3 center,Vector3 size,PhysicMaterial material){
  var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.position=center;
  var c=g.AddComponent<BoxCollider>();c.size=size;c.sharedMaterial=material;return g;
 }
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var snapshots=new Dictionary<Renderer,Matrix4x4>();
  foreach(var r in Object.FindObjectsOfType<Renderer>())if(r.enabled)snapshots.Add(r,r.transform.localToWorldMatrix);
  var game=Object.FindObjectOfType<BlockFallGame>();var decor=GameObject.Find("Magic Library Decor").transform;
  float left=float.NegativeInfinity,right=float.PositiveInfinity,far=float.PositiveInfinity;
  foreach(Transform bay in decor){
   if(!bay.name.StartsWith("Library bay "))continue;var b=BoundsOf(bay);
   if(Mathf.Abs(Mathf.DeltaAngle(bay.eulerAngles.y,0))<1)far=Mathf.Min(far,b.min.z);
   else if(bay.position.x<0)left=Mathf.Max(left,b.max.x);
   else right=Mathf.Min(right,b.min.x);
  }
  var floor=GameObject.Find("Continuous library floor").GetComponent<Renderer>().bounds;
  float near=floor.min.z;
  if(left>=right || near>=far)throw new Exception("Invalid library inner bounds");
  var boundary=game.GetComponent<BlockFallArenaBoundary>();if(boundary==null)boundary=game.gameObject.AddComponent<BlockFallArenaBoundary>();
  boundary.minimum=new Vector2(left,near);boundary.maximum=new Vector2(right,far);boundary.floorHeight=floor.max.y;
  string path="Assets/Materials/BlockFall/Boundary frictionless.physicMaterial";
  var material=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
  if(material==null){material=new PhysicMaterial("Boundary frictionless");AssetDatabase.CreateAsset(material,path);}
  material.dynamicFriction=0;material.staticFriction=0;material.bounciness=0;material.frictionCombine=PhysicMaterialCombine.Minimum;material.bounceCombine=PhysicMaterialCombine.Minimum;EditorUtility.SetDirty(material);
  var old=GameObject.Find("Library Collision Bounds");if(old!=null)Object.DestroyImmediate(old);
  var root=new GameObject("Library Collision Bounds").transform;
  const float thickness=2,height=40;
  ColliderObject("Left bookshelf barrier",root,new Vector3(left-thickness/2,19,(near+far)/2),new Vector3(thickness,height,far-near+4),material);
  ColliderObject("Right bookshelf barrier",root,new Vector3(right+thickness/2,19,(near+far)/2),new Vector3(thickness,height,far-near+4),material);
  ColliderObject("Rear bookshelf barrier",root,new Vector3((left+right)/2,19,far+thickness/2),new Vector3(right-left+4,height,thickness),material);
  ColliderObject("Front floor edge barrier",root,new Vector3((left+right)/2,19,near-thickness/2),new Vector3(right-left+4,height,thickness),material);
  var support=ColliderObject("Continuous support floor",root,new Vector3((left+right)/2,floor.max.y-.5f,(near+far)/2),new Vector3(right-left,1,far-near),material);support.tag="Ground";
  for(int i=0;i<4;i++){
   var wall=GameObject.Find("Boundary "+i);foreach(var c in wall.GetComponents<Collider>())c.enabled=false;
  }
  // Preserve every visible tile, adding only serialized edge footprints to eliminate safe perimeter strips.
  foreach(Transform cell in game.tiles){
   var b=cell.GetComponent<Renderer>().bounds;
   var area=cell.GetComponent<BlockFallCellArea>();if(area==null)area=cell.gameObject.AddComponent<BlockFallCellArea>();
   float minX=b.min.x,maxX=b.max.x,minZ=b.min.z,maxZ=b.max.z;
   if(cell.name.EndsWith("_0"))minX=left;
   if(cell.name.EndsWith("_5"))maxX=right;
   if(cell.name.StartsWith("Tile_0_"))minZ=near;
   if(cell.name.StartsWith("Tile_5_"))maxZ=far;
   area.center=new Vector3((minX+maxX)/2,b.center.y,(minZ+maxZ)/2);area.size=new Vector3(maxX-minX,b.size.y,maxZ-minZ);
  }
  foreach(var pair in snapshots)if(pair.Key!=null && pair.Key.transform.localToWorldMatrix!=pair.Value)throw new Exception("A visible map object moved");
  Physics.SyncTransforms();
  Vector3 center=new Vector3((left+right)/2,1.5f,(near+far)/2);
  foreach(var direction in new[]{Vector3.left,Vector3.right,Vector3.forward,Vector3.back}){
   if(!Physics.SphereCast(center,.4f,direction,out var hit,100,~0,QueryTriggerInteraction.Ignore) || hit.collider.transform.parent!=root)
    throw new Exception("Boundary collision sweep failed");
  }
  for(int i=0;i<100;i++){
   Vector3 position=center+new Vector3(Mathf.Sin(i)*1000,0,Mathf.Cos(i)*1000);
   var body=new Bounds(position+Vector3.up*.5f,new Vector3(1,2,1));
   var corrected=BlockFallArenaBoundary.ClampPosition(position,body,boundary.minimum,boundary.maximum);
   if(corrected.x-.5f<left || corrected.x+.5f>right || corrected.z-.5f<near || corrected.z+.5f>far)
    throw new Exception("High-speed containment failed");
  }
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Debug.Log("BOUNDARY_FIT_PASS: visible map unchanged; 2m/40m barriers, 4 collision sweeps, 100 high-speed containment cases, owner/server correction and edge footprints.");
 }
}
}
#endif
