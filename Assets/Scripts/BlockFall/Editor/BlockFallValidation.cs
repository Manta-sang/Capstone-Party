#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
namespace BlockFall.Editor {
public static class BlockFallValidation {
 public static void Validate() {
  foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Materials/BlockFall"})) {
   var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));
   Color color=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.color; mat.shader=Shader.Find("Standard"); mat.color=color; EditorUtility.SetDirty(mat);
  }
  AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var g=UnityEngine.Object.FindObjectOfType<BlockFallGame>();
  if(g==null || g.GetComponent<NetworkObject>()==null) throw new Exception("Missing network game manager");
  if(g.tiles.childCount!=25 || g.starts.Length!=4 || g.starts.Any(x=>x==null)) throw new Exception("Arena/spawn configuration invalid");
  if(g.characters.Length!=2 || g.characters.Any(x=>x==null || x.GetComponent<NetworkObject>()==null)) throw new Exception("Invalid characters");
  if(g.blockVisual==null || g.duration!=60) throw new Exception("Invalid visual/duration");
  if(!EditorBuildSettings.scenes.Any(x=>x.enabled && x.path=="Assets/Scenes/BlockFall.unity")) throw new Exception("Scene missing from build");
  foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>())
   if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new Exception("Missing script "+t.name);
  var alternate=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockFall/Alternate.mat");
  if(alternate==null) { alternate=new Material(Shader.Find("Standard")); alternate.color=new Color(.51f,.57f,.6f); AssetDatabase.CreateAsset(alternate,"Assets/Materials/BlockFall/Alternate.mat"); }
  for(int i=0;i<g.tiles.childCount;i++) if((i+i/5)%2==0) g.tiles.GetChild(i).GetComponent<Renderer>().sharedMaterial=alternate;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  AssetDatabase.SaveAssets();
  var cam=Camera.main;
  var rt=new RenderTexture(1280,720,24); cam.targetTexture=rt; cam.Render();
  RenderTexture.active=rt; var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
  System.IO.File.WriteAllBytes("BlockFall-preview.png",image.EncodeToPNG());
  cam.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");
  var controller = UnityEngine.Object.FindObjectOfType<CrosswalkRun.CharacterRoundRestart>();
  var entry = new SerializedObject(controller).FindProperty("blockFallButton").objectReferenceValue as UnityEngine.UI.Button;
  if(entry == null || entry.onClick.GetPersistentMethodName(0) != "StartBlockFall") throw new Exception("Lobby button binding invalid");
  Debug.Log("BLOCKFALL_VALIDATION_PASS: scene references, 25 tiles, 4 spawns, network components, build registration, lobby entry, render.");
 }
}}
#endif
