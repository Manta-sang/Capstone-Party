#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BlockFall.Editor
{
 public static class BlockFallAppearance
 {
  public static void Apply()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
   var game=Object.FindObjectOfType<BlockFallGame>();
   var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/Floor.mat");
   for(int i=0;i<game.tiles.childCount;i++) game.tiles.GetChild(i).GetComponent<Renderer>().sharedMaterial=floor;
   foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (root.name == "Endless Background") Object.DestroyImmediate(root);
   RenderSettings.fog=false;
   var camera=Camera.main; camera.clearFlags=CameraClearFlags.Skybox; camera.backgroundColor=Color.black;
   // Fit the visible arena walls vertically, with a small margin for their edges.
   float min=float.PositiveInfinity,max=float.NegativeInfinity;
   for(int i=0;i<4;i++)
   {
    var bounds=GameObject.Find("Boundary "+i).GetComponent<Renderer>().bounds;
    for(int j=0;j<8;j++)
    {
     var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1));
     float y=Vector3.Dot(p-camera.transform.position,camera.transform.up);
     min=Mathf.Min(min,y); max=Mathf.Max(max,y);
    }
   }
   camera.transform.position+=camera.transform.up*((min+max)*.5f);
   camera.orthographicSize=(max-min)*.5f*1.015f;
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   var rt=new RenderTexture(1280,720,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
   var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
   System.IO.File.WriteAllBytes("BlockFall-preview.png",image.EncodeToPNG());
   camera.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
   Debug.Log("BLOCKFALL_APPEARANCE_PASS: solid floor, black background, vertical camera fit.");
  }
 }
}
#endif
