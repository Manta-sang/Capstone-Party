#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class GameplayUIValidation {
 public static void BuildAndRun(){GameStartUIBuilder.Apply();Run();}
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  Require(GameStartPresentation.CountdownSeconds==1,"Countdown duration must be 1 second");
  Require(typeof(CrosswalkRun.CrosswalkGameManager).GetMethod("OnGUI",BindingFlags.NonPublic|BindingFlags.Instance)==null,"Crosswalk debug box still exists");Require(typeof(NetController).GetMethod("OnGUI",BindingFlags.NonPublic|BindingFlags.Instance)==null,"Legacy network box still exists");
  CrosswalkRun.CharacterRoundRestart.DeveloperSoloStart=false;Require(!CrosswalkRun.CharacterRoundRestart.HasEnoughPlayers(1)&&CrosswalkRun.CharacterRoundRestart.HasEnoughPlayers(2),"Normal start player threshold incorrect");CrosswalkRun.CharacterRoundRestart.DeveloperSoloStart=true;Require(CrosswalkRun.CharacterRoundRestart.HasEnoughPlayers(1),"Developer solo mode unavailable");CrosswalkRun.CharacterRoundRestart.DeveloperSoloStart=false;
  foreach(string name in new[]{"CrosswalkRun","SawBladeDodge","TileFlip","Qte","BlockFall"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");var start=Object.FindObjectOfType<GameStartPresentation>();Require(start!=null&&start.countdownText!=null,"Countdown UI missing: "+name);Require(start.crosswalk!=null||start.blade!=null||start.tile!=null||start.qte!=null||start.block!=null,"Countdown manager binding missing: "+name);
   var push=Object.FindObjectOfType<PushCooldownHUD>();Require(name=="Qte"?push==null:push!=null,"Push UI scope incorrect: "+name);
   if(push!=null){Require(push.gesture.GetComponent<CanvasRenderer>()!=null,"Push icon renderer missing");Require(push.GetComponentsInChildren<TMPro.TMP_Text>(true).All(t=>AssetDatabase.GetAssetPath(t.font)=="Assets/Fonts/NotoSansKR-Black SDF.asset"),"Push hint must use project Korean font");Require(push.progressRing.type==Image.Type.Filled&&push.progressRing.fillMethod==Image.FillMethod.Radial360&&push.progressRing.fillClockwise,"Radial direction invalid");Require(push.progressRing.raycastTarget==false&&push.gesture.raycastTarget==false,"Push HUD blocks pointer input");Require(push.GetComponent<RectTransform>().anchorMin==Vector2.zero,"Push HUD is not bottom-left");}
   if(name=="TileFlip")Require(start.tileTimer!=null&&start.tileTimer.color==Color.black&&start.tileTimer.rectTransform.anchorMin==new Vector2(.5f,1),"Tile timer must be black and top-center");
   if(name=="CrosswalkRun"||name=="TileFlip")Preview(name,push);
   Debug.Log("GAMEPLAY_UI_SCENE_VALIDATION_PASS: "+name);
  }
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=Object.FindObjectOfType<CharacterCabinLobby>();Require(cabin.readyLabel.color.r<.3f,"Ready label contrast too low");Require(cabin.readyButton.GetComponent<PartyButtonFeedback>().disabledColor.r<.75f,"Ready disabled state too pale");
  var dev=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PartyDeveloperTools.prefab").GetComponent<PartyDeveloperTools>();Require(dev.soloStartButton!=null&&dev.soloStartButton.onClick.GetPersistentMethodName(0)=="StartSoloGame","Developer solo start event missing");
  var sample=new GameObject("Temporary cooldown check",typeof(Unity.Netcode.NetworkObject));try{var action=sample.AddComponent<PlayerInteraction>();action.pushCooldown=3;var stamp=typeof(PlayerInteraction).GetField("lastPushTime",BindingFlags.NonPublic|BindingFlags.Instance);stamp.SetValue(action,Time.time);Require(action.PushCooldownProgress<.01f,"Cooldown should start empty");stamp.SetValue(action,Time.time-1.5f);Require(Mathf.Abs(action.PushCooldownProgress-.5f)<.01f,"Cooldown midpoint incorrect");stamp.SetValue(action,Time.time-3);Require(action.PushCooldownProgress==1,"Cooldown should finish full");action.pushCooldown=0;Require(action.PushCooldownProgress==1,"Zero cooldown must be ready");}finally{Object.DestroyImmediate(sample);}
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");System.IO.File.WriteAllText("Library/gameplay-ui-validation.txt","PASS: C# compilation, five countdown bindings, four radial HUDs excluding Qte, TileFlip timer placement/color, ready contrast, min-two/solo rules and cooldown 0/50/100% checked.");Debug.Log("GAMEPLAY_UI_VALIDATION_PASS");
 }
 static void Preview(string name,PushCooldownHUD push){
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
  push.visibility.alpha=1;push.progressRing.fillAmount=.55f;push.progressRing.color=push.rechargingColor;push.clockMarker.localRotation=Quaternion.Euler(0,0,-198);
  var capture=typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);capture.Invoke(null,new object[]{"Library/gameplay-ui-"+name+".png"});
 }
}
#endif
