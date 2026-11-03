#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BlockFall.Editor {
[InitializeOnLoad]
public static class MagicLibraryRequest {
 const string Request="Library/BlockFallMagicLibrary.request";
 static MagicLibraryRequest() { EditorApplication.delayCall+=Run; }
 static void Run() {
  if(!System.IO.File.Exists(Request)) return;
  if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty) {
   Debug.LogWarning("Magic Library request waiting: save scene and use Tools/BlockFall/Apply Magic Library."); return;
  }
  Apply();
 }
 [MenuItem("Tools/BlockFall/Apply Magic Library")]
 public static void Apply() {
  if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="sideTier") BlockFallSideTier.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="uniformGrid") BlockFallUniformGrid.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="fitBounds") BlockFallBoundaryFit.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="allCrystalLights") BlockFallAllCrystalLights.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="arenaSix") BlockFallArenaSix.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="landingOnly") BlockFallLandingValidation.Validate(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="contactLayout") MagicLibraryContactLayout.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="frontLayout") MagicLibraryFrontLayout.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="validateMotion") MagicLibraryMotionUpgrade.Validate(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="motionUpgrade") MagicLibraryMotionUpgrade.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="validatePolish") MagicLibraryPolish.Validate(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="polish") MagicLibraryPolish.Apply(); else if(System.IO.File.Exists(Request) && System.IO.File.ReadAllText(Request).Trim()=="refine") MagicLibraryRefine.Apply(); else MagicLibraryBuilder.Apply();
  if(System.IO.File.Exists(Request)) System.IO.File.Delete(Request);
 }
}}
#endif
