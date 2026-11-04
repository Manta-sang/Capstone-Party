#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class GameStartUIBuilder {
 static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=position;r.sizeDelta=size;return r;}
 static TextMeshProUGUI Text(string name,Transform parent,Vector2 position,Vector2 size,int fontSize,Color color){var r=Rect(name,parent,position,size);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");text.fontSize=fontSize;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;}
 public static void Apply(){
  if(!Application.isBatchMode&&!EditorSceneManager.SaveOpenScenes())throw new Exception("Save current scene edits before generating UI");
  foreach(string scene in new[]{"CrosswalkRun","SawBladeDodge","TileFlip","Qte","BlockFall"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");var old=GameObject.Find("Round Start Canvas");if(old!=null)Object.DestroyImmediate(old);
   var root=new GameObject("Round Start Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GameStartPresentation));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
   var ui=root.GetComponent<GameStartPresentation>();ui.crosswalk=Object.FindObjectOfType<CrosswalkRun.CrosswalkNetworkEntry>();ui.blade=Object.FindObjectOfType<SD_GameManager>();ui.tile=Object.FindObjectOfType<TF_GameManager>();ui.qte=Object.FindObjectOfType<QteSceneController>();ui.block=Object.FindObjectOfType<BlockFall.BlockFallGame>();
   ui.countdownText=Text("Shared 3 2 1 countdown",root.transform,Vector2.zero,new Vector2(900,250),150,Color.white);ui.countdownText.text="3";var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/IllustratedTickets/Title prompt outline.mat");if(material!=null)ui.countdownText.fontSharedMaterial=material;
   if(scene=="TileFlip"){var timer=Text("Remaining time",root.transform,new Vector2(0,-46),new Vector2(300,70),40,Color.black);timer.rectTransform.anchorMin=timer.rectTransform.anchorMax=new Vector2(.5f,1);timer.text="01:00";ui.tileTimer=timer;}
   if(scene=="Qte"&&Object.FindObjectOfType<PushCooldownHUD>()!=null)throw new Exception("Qte must not have a push cooldown HUD");
   if(scene!="Qte"&&Object.FindObjectOfType<PushCooldownHUD>()==null)throw new Exception("Push HUD missing in "+scene);
   var push=Object.FindObjectOfType<PushCooldownHUD>();if(push!=null){if(push.gesture.GetComponent<CanvasRenderer>()==null)push.gesture.gameObject.AddComponent<CanvasRenderer>();foreach(var hint in push.GetComponentsInChildren<TMP_Text>(true)){hint.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");hint.fontSharedMaterial=hint.font.material;}}
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Debug.Log("SHARED_START_UI_PASS: "+scene);
  }
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=Object.FindObjectOfType<CharacterCabinLobby>();var ready=cabin.readyButton;var feedback=ready.GetComponent<PartyButtonFeedback>();if(feedback==null)feedback=ready.gameObject.AddComponent<PartyButtonFeedback>();feedback.disabledColor=new Color(.68f,.72f,.77f);ready.GetComponent<Image>().color=feedback.disabledColor;cabin.readyLabel.color=new Color(.16f,.20f,.25f);
  var outline=ready.GetComponent<Outline>();if(outline==null)outline=ready.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.25f,.30f,.36f,.7f);outline.effectDistance=new Vector2(1,-1);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  var prefab=PrefabUtility.LoadPrefabContents("Assets/Resources/PartyDeveloperTools.prefab");try{
   var tools=prefab.GetComponent<PartyDeveloperTools>();var parent=tools.hostButton.transform.parent;var existing=parent.Find("Solo test start");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
   var solo=Object.Instantiate(tools.restartButton,parent);solo.name="Solo test start";var r=solo.GetComponent<RectTransform>();var host=tools.hostButton.GetComponent<RectTransform>();r.anchorMin=host.anchorMin;r.anchorMax=host.anchorMax;r.pivot=host.pivot;r.anchoredPosition=new Vector2(187,-804);r.sizeDelta=new Vector2(330,45);
   solo.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(solo.onClick,tools.StartSoloGame);solo.GetComponentInChildren<TMP_Text>(true).text="1인 테스트 시작";tools.soloStartButton=solo;
   var help=parent.Find("Help") as RectTransform;if(help!=null)help.anchoredPosition=new Vector2(187,-854);var panel=tools.panel.GetComponent<RectTransform>();panel.sizeDelta=new Vector2(panel.sizeDelta.x,900);
   PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Resources/PartyDeveloperTools.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(prefab);}
  AssetDatabase.SaveAssets();Debug.Log("GAMEPLAY_UI_BUILD_PASS: five synchronized 3-second starts, TileFlip black timer, readable ready button, developer solo-start action.");
 }
}
#endif
