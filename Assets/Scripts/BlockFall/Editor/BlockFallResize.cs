#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BlockFall.Editor {
public static class BlockFallResize {
 public static void Apply() {
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var g=Object.FindObjectOfType<BlockFallGame>();
  var template=g.tiles.GetChild(0).gameObject;
  var a=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/Floor.mat");
  var b=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/Alternate.mat");
  while(g.tiles.childCount>25) Object.DestroyImmediate(g.tiles.GetChild(g.tiles.childCount-1).gameObject);
  for(int i=0;i<25;i++) {
   var t=g.tiles.GetChild(i); int x=i%5,z=i/5;
   t.name="Tile_"+z+"_"+x; t.position=new Vector3((x-2)*4,-.25f,(z-2)*4);
   t.localScale=new Vector3(4,.5f,4); t.GetComponent<Renderer>().sharedMaterial=(x+z)%2==0?b:a;
  }
  for(int i=0;i<4;i++) {
   var t=GameObject.Find("Boundary "+i).transform; bool side=i<2; float sign=i%2==0?-1:1;
   t.position=side?new Vector3(sign*10.3f,.5f,0):new Vector3(0,.5f,sign*10.3f);
   t.localScale=side?new Vector3(.6f,1,21.2f):new Vector3(21.2f,1,.6f);
   g.starts[i].position=new Vector3(i%2==0?-7:7,1,i<2?-7:7);
  }
  Camera.main.orthographicSize=15;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  BlockFallValidation.Validate();
 }
}}
#endif
