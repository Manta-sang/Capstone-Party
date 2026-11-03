#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class BlockFallArenaSix {
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();var tiles=game.tiles;
  Bounds oldBounds=tiles.GetChild(0).GetComponent<Renderer>().bounds;
  foreach(Transform t in tiles)oldBounds.Encapsulate(t.GetComponent<Renderer>().bounds);
  float factor=22f/oldBounds.size.x;
  while(tiles.childCount<36)Object.Instantiate(tiles.GetChild(0).gameObject,tiles);
  while(tiles.childCount>36)Object.DestroyImmediate(tiles.GetChild(tiles.childCount-1).gameObject);
  const float size=22f/6;
  for(int i=0;i<36;i++){
   var tile=tiles.GetChild(i);tile.name="Tile_"+i/6+"_"+i%6;
   tile.localPosition=new Vector3((i%6-2.5f)*size,-.25f,(i/6-2.5f)*size);
   tile.localScale=new Vector3(size,.5f,size);
  }
  for(int i=0;i<4;i++){
   var wall=GameObject.Find("Boundary "+i).transform;
   wall.position=new Vector3(wall.position.x*factor,wall.position.y,wall.position.z*factor);
   wall.localScale=new Vector3(wall.localScale.x*factor,wall.localScale.y,wall.localScale.z*factor);
  }
  foreach(var spawn in game.starts)spawn.position=new Vector3(spawn.position.x*factor,spawn.position.y,spawn.position.z*factor);
  var decor=GameObject.Find("Magic Library Decor").transform;
  decor.localScale=new Vector3(decor.localScale.x*factor,decor.localScale.y,decor.localScale.z*factor);
  var camera=Camera.main;var anchor=new Vector3(0,.5f,0);
  camera.transform.position=anchor+(camera.transform.position-anchor)*factor;
  // Keep the selected drop cadence, round length, and landing-only collision rules.
  if(BlockFallGame.CoverageLimit(36,0)!=9 || BlockFallGame.CoverageLimit(36,1)!=18 || BlockFallGame.CoverageLimit(36,2)!=27)
   throw new Exception("Six-by-six coverage limits invalid");
  Bounds bounds=tiles.GetChild(0).GetComponent<Renderer>().bounds;
  foreach(Transform t in tiles)bounds.Encapsulate(t.GetComponent<Renderer>().bounds);
  if(Mathf.Abs(bounds.size.x-22)>.01f || Mathf.Abs(bounds.size.z-22)>.01f)throw new Exception("Arena must measure 22 by 22");
  if(game.starts.Length!=4 || game.blockVisual==null)throw new Exception("Player/visual references invalid");
  for(int i=0;i<4;i++)if(!bounds.Contains(new Vector3(game.starts[i].position.x,bounds.center.y,game.starts[i].position.z)))throw new Exception("Spawn outside arena");
  if(Mathf.Abs(game.initialDropInterval-.4f)>.001f || Mathf.Abs(game.middleDropInterval-.3f)>.001f || Mathf.Abs(game.finalDropInterval-.22f)>.001f)
   throw new Exception("Drop intervals changed unexpectedly");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(camera,"BlockFall-preview.png");
  BlockFallLandingValidation.Validate();
  Debug.Log("ARENA_SIX_PASS: 36 editable tiles, 22x22 bounds, 4 valid spawns, coverage 9/18/27, unchanged drop intervals, landing-only damage.");
 }
}
}
#endif
