#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BlockFall.Editor {
public static class BlockFallAllCrystalLights {
 public static void Apply(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  const string dir="Assets/Materials/BlockFall/MagicLibrary/";
  var path=dir+"Crystal halo.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(material==null){material=new Material(Shader.Find("BlockFall/CrystalHalo"));AssetDatabase.CreateAsset(material,path);}
  var glows=Object.FindObjectsOfType<BlockFallCrystalGlow>();
  foreach(var g in glows){
   var renderer=g.GetComponent<Renderer>();var m=renderer.sharedMaterial;
   m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.2f,.8f,1.6f));EditorUtility.SetDirty(m);
   if(g.source==null){
    var light=new GameObject("Blue crystal light").AddComponent<Light>();light.transform.SetParent(g.transform.parent,false);light.transform.position=g.transform.position;light.type=LightType.Point;g.source=light;
   }
   g.source.color=new Color(.12f,.42f,1);g.source.range=5;g.source.shadows=LightShadows.None;g.source.intensity=1.1f;
   g.peakIntensity=Mathf.Max(1.1f,g.peakIntensity);
   if(g.halo==null){
    var halo=GameObject.CreatePrimitive(PrimitiveType.Quad);halo.name="Soft crystal halo";Object.DestroyImmediate(halo.GetComponent<Collider>());
    halo.transform.SetParent(g.transform.parent,false);halo.transform.position=g.transform.position;
    var scale=g.transform.parent.lossyScale;
    halo.transform.localScale=new Vector3(1.3f/scale.x,1.3f/scale.y,1);
    halo.transform.rotation=Camera.main.transform.rotation;
    g.halo=halo.GetComponent<Renderer>();g.halo.sharedMaterial=material;g.halo.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;g.halo.receiveShadows=false;
   }
   EditorUtility.SetDirty(g);
  }
  foreach(var g in glows)if(g.source==null || g.halo==null || !g.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"))throw new System.Exception("Crystal glow missing");
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  MagicLibraryPolish.Capture(Camera.main,"BlockFall-preview.png");
  Debug.Log("ALL_CRYSTALS_GLOW_PASS: "+glows.Length+" crystals have constant emission, blue illumination and halo.");
 }
}
}
#endif
