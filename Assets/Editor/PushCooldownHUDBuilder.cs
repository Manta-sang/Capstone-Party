#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PushCooldownHUDBuilder {
 const string Request="Library/PushHUD.request",Dir="Assets/UI/Gameplay/";
 static PushCooldownHUDBuilder(){EditorApplication.update+=Run;}
 static void Run(){if(!System.IO.File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;try{Apply();System.IO.File.Delete(Request);}catch(Exception ex){System.IO.File.Delete(Request);Debug.LogException(ex);}}
 static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
 static Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Color color,Sprite sprite){var r=Rect(name,parent,pos,size);var i=r.gameObject.AddComponent<Image>();i.sprite=sprite;i.color=color;i.raycastTarget=false;return i;}
 static Sprite Texture(string name,bool ring){var t=new Texture2D(256,256,TextureFormat.RGBA32,false);for(int y=0;y<256;y++)for(int x=0;x<256;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*128);float a=Mathf.Clamp01(124-d);if(ring)a*=Mathf.Clamp01(d-103);t.SetPixel(x,y,new Color(1,1,1,a));}t.Apply();string path=Dir+name+".png";System.IO.File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);}
 [MenuItem("Tools/Party/Build Push Cooldown HUD")]
 public static void Apply(){
  if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Could not save current scene edits");AssetDatabase.SaveAssets();var setup=EditorSceneManager.GetSceneManagerSetup();System.IO.Directory.CreateDirectory(Dir);var ring=Texture("Push cooldown ring",true);var disc=Texture("Push cooldown disc",false);var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
  foreach(var name in new[]{"CrosswalkRun","SawBladeDodge","TileFlip","BlockFall"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");var existing=GameObject.Find("Push Cooldown Canvas");if(existing!=null)Object.DestroyImmediate(existing);
   var root=new GameObject("Push Cooldown Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=45;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
   var panel=Rect("Push cooldown",root.transform,new Vector2(88,99),new Vector2(112,146));panel.anchorMin=panel.anchorMax=Vector2.zero;var hud=panel.gameObject.AddComponent<PushCooldownHUD>();hud.visibility=panel.gameObject.AddComponent<CanvasGroup>();hud.visibility.blocksRaycasts=false;hud.visibility.interactable=false;hud.visibility.alpha=0;
   Image("Soft shadow",panel,new Vector2(0,7),new Vector2(114,114),new Color(0,0,0,.24f),disc);
   Image("Circle backing",panel,new Vector2(0,11),new Vector2(110,110),new Color(.09f,.15f,.20f,.94f),disc);
   Image("Cooldown track",panel,new Vector2(0,11),new Vector2(106,106),new Color(.27f,.35f,.39f),ring);
   var fill=Image("Clockwise cooldown",panel,new Vector2(0,11),new Vector2(106,106),hud.readyColor,ring);fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;fill.fillOrigin=(int)UnityEngine.UI.Image.Origin360.Top;fill.fillClockwise=true;fill.fillAmount=1;hud.progressRing=fill;
   var gesture=Rect("Push emoji",panel,new Vector2(0,11),new Vector2(64,64)).gameObject.AddComponent<PushGestureGraphic>();gesture.raycastTarget=false;hud.gesture=gesture;
   hud.clockMarker=Rect("Clock hand pivot",panel,new Vector2(0,11),new Vector2(106,106));Image("Clock hand tip",hud.clockMarker,new Vector2(0,49),new Vector2(4,12),new Color(1,.97f,.83f),disc);
   var text=Rect("Push key hint",panel,new Vector2(0,-61),new Vector2(160,30)).gameObject.AddComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");text.fontSharedMaterial=text.font.material;text.text="SHIFT · 밀치기";text.fontSize=19;text.color=new Color(1,.96f,.83f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Debug.Log("PUSH_HUD_SCENE_PASS: "+name+"; serialized bottom-left radial UI bound to owner cooldown.");
  }
  AssetDatabase.SaveAssets();EditorSceneManager.RestoreSceneManagerSetup(setup);Debug.Log("PUSH_HUD_BUILD_PASS: four games updated; Qte excluded; Crosswalk uses common Esc tools.");
 }
}
#endif
