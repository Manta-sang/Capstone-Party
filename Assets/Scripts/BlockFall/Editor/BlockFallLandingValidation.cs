#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace BlockFall.Editor {
public static class BlockFallLandingValidation {
 public static void Validate(){
  EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
  var game=Object.FindObjectOfType<BlockFallGame>();var sample=Object.Instantiate(game.blockVisual);
  sample.hideFlags=HideFlags.HideAndDontSave;
  var view=sample.GetComponent<BlockFallVisual>();
  foreach(float t in new[]{1.2f,.6f,.3f,.05f,.001f}){
   view.Animate(t,game.fallTime,game.warningTime,15);
   if(view.TryGetDamageBounds(out var ignored))throw new System.Exception("Airborne book must not damage at "+t);
  }
  foreach(float t in new[]{0f,-.1f,-.4f,-.64f}){
   view.Animate(t,game.fallTime,game.warningTime,15);
   if(!view.TryGetDamageBounds(out var bounds))throw new System.Exception("Landed book must damage");
   if(bounds.size.y>3)throw new System.Exception("Airborne swept bounds leaked into landed hitbox");
  }
  Object.DestroyImmediate(sample);
  Debug.Log("LANDING_ONLY_VALIDATION_PASS: airborne contact disabled, landing and resting contact enabled, no swept airborne bounds.");
 }
}}
#endif
