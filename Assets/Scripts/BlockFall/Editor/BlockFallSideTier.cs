#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace BlockFall.Editor {
public static class BlockFallSideTier {
 const string Marker="Side shelf additional tier applied";
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();
  var boundary=game.GetComponent<BlockFallArenaBoundary>();
  var min=boundary.minimum;var max=boundary.maximum;
  var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/BlockFall/BlockVisual.prefab");
  prefab.GetComponent<BlockFallVisual>().spawnHeight=27;
  PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/BlockFall/BlockVisual.prefab");
  PrefabUtility.UnloadPrefabContents(prefab);
  int count=0;
  var decor=GameObject.Find("Magic Library Decor").transform;
  if(GameObject.Find(Marker)==null){
   foreach(Transform bay in decor){
    if(!bay.name.StartsWith("Library bay ") || Mathf.Abs(Mathf.DeltaAngle(bay.eulerAngles.y,0))<1)continue;
    float height=float.NegativeInfinity;
    foreach(var mf in bay.GetComponentsInChildren<MeshFilter>())height=Mathf.Max(height,mf.sharedMesh.bounds.max.y);
    height-=.2f;
    foreach(var mf in bay.GetComponentsInChildren<MeshFilter>()){
     var source=mf.sharedMesh;var v=source.vertices;var uv=source.uv;var normals=source.normals;var tris=source.triangles;
     var output=new List<Vector3>();var tex=new List<Vector2>();var ns=new List<Vector3>();var indices=new List<int>();
     for(int i=0;i<tris.Length;i+=3){
      float low=Mathf.Min(v[tris[i]].y,Mathf.Min(v[tris[i+1]].y,v[tris[i+2]].y));
      float high=Mathf.Max(v[tris[i]].y,Mathf.Max(v[tris[i+1]].y,v[tris[i+2]].y));
      bool crown=low>=height-.005f && high>height+.08f;
      for(int j=0;j<3;j++){
       int k=tris[i+j];var p=v[k];if(crown)p.y+=1.5f;
       indices.Add(output.Count);output.Add(p);tex.Add(uv.Length==v.Length?uv[k]:Vector2.zero);ns.Add(normals[k]);
      }
      // Copy the books and upper shelf, leaving the previous upper shelf as the new tier's base.
      if(low>height-1.5f+.09f && high<=height+.075f){
       for(int j=0;j<3;j++){int k=tris[i+j];indices.Add(output.Count);output.Add(v[k]+Vector3.up*1.5f);tex.Add(uv.Length==v.Length?uv[k]:Vector2.zero);ns.Add(normals[k]);}
      }
     }
     var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(output);mesh.SetUVs(0,tex);mesh.SetNormals(ns);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
     var path="Assets/Materials/BlockFall/MagicLibrary/Side extra tier "+count+" "+mf.name+".asset";
     AssetDatabase.CreateAsset(mesh,path);mf.sharedMesh=mesh;
    }
    var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/MagicLibrary/Dusty plum wood.mat");
    var rounded=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Materials/BlockFall/MagicLibrary/Rounded volume 2.asset");
    for(int part=0;part<3;part++){
     var g=new GameObject("Additional tier frame "+part,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(bay,false);g.isStatic=true;
     g.transform.localPosition=part==0?new Vector3(0,height+.75f,.18f):new Vector3((part==1?-1:1)*2.1f,height+.75f,-.3f);
     g.transform.localScale=part==0?new Vector3(4.2f,1.5f,.25f):new Vector3(.18f,1.5f,1);
     g.GetComponent<MeshFilter>().sharedMesh=rounded;g.GetComponent<Renderer>().sharedMaterial=wood;
    }
    count++;
   }
   if(count==0)throw new Exception("No side shelves found");
   new GameObject(Marker).transform.SetParent(decor,false);
  }
  foreach(Transform bay in decor){
   if(!bay.name.StartsWith("Library bay ") || Mathf.Abs(Mathf.DeltaAngle(bay.eulerAngles.y,0))<1 || bay.Find("Additional tier frame 0")!=null)continue;
   float top=float.NegativeInfinity;foreach(var mf in bay.GetComponentsInChildren<MeshFilter>())top=Mathf.Max(top,mf.sharedMesh.bounds.max.y);
   float center=top-.2f-.75f;
   for(int part=0;part<3;part++){
    var g=new GameObject("Additional tier frame "+part,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(bay,false);g.isStatic=true;
    g.transform.localPosition=part==0?new Vector3(0,center,.18f):new Vector3((part==1?-1:1)*2.1f,center,-.3f);
    g.transform.localScale=part==0?new Vector3(4.2f,1.5f,.25f):new Vector3(.18f,1.5f,1);
    g.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Materials/BlockFall/MagicLibrary/Rounded volume 2.asset");
    g.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/MagicLibrary/Dusty plum wood.mat");
   }
  }
  if(boundary.minimum!=min || boundary.maximum!=max || game.tiles.childCount!=36)throw new Exception("Arena layout changed");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("SIDE_TIER_PASS: spawn height 27 (1.5x18), one extra side shelf tier; front and arena bounds unchanged. Side bays: "+count);
 }
}}
#endif
