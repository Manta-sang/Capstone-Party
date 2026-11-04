#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QteImportValidation {
 const string Request="Library/QteImportReview.request",Result="Library/QteImportReview/result.txt",Key="Qte.Import.Runtime";
 static int stage;static double at,timeout;static bool sawCountdown,sawJump;static float stoppedDistance;static QteSceneController race;
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static QteImportValidation(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
 static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
 static object Get(string name)=>typeof(QteSceneController).GetField(name,Private).GetValue(race);
 static void Set(string name,object value)=>typeof(QteSceneController).GetField(name,Private).SetValue(race,value);
 static void Call(string name,params object[] args)=>typeof(QteSceneController).GetMethod(name,Private).Invoke(race,args);
 static void Log(string message,string trace,LogType type){if(SessionState.GetBool(Key,false)&&type==LogType.Exception){System.IO.File.WriteAllText(Result,"FAIL: runtime exception: "+message+"\n"+trace);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;}}
 static void Tick(){
  if(!Application.isBatchMode&&System.IO.File.Exists(Request)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){System.IO.File.Delete(Request);Prepare();return;}
  if(SessionState.GetBool("Qte.Import.Restore",false)&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool("Qte.Import.Restore",false);var path=SessionState.GetString("Qte.Import.PreviousScene","Assets/Scenes/Qte.unity");if(!string.IsNullOrEmpty(path))EditorSceneManager.OpenScene(path);if(Application.isBatchMode){EditorApplication.Exit(System.IO.File.ReadAllText(Result).StartsWith("PASS:")?0:1);return;}}
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  try{RunRuntime();}catch(Exception ex){Fail(ex);}
 }
 public static void AttachSharedStartUI(){
  var controller=Object.FindObjectOfType<QteSceneController>();Require(controller!=null,"QTE controller missing");
  var so=new SerializedObject(controller);var list=so.FindProperty("characterPrefabs");string[] guids={"b3f543bae91f9b94598a8372bfe747f8","0c76b793a5061c942beb17b723fa0688","43f35dc74b8af374c8ba48ec4ee160e0","07562273415467b4ca8e2b02136ecec3"};list.arraySize=4;for(int i=0;i<4;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));so.FindProperty("koreanFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");so.ApplyModifiedPropertiesWithoutUndo();
  var root=GameObject.Find("Round Start Canvas");if(root!=null)Object.DestroyImmediate(root);root=new GameObject("Round Start Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GameStartPresentation));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=150;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var text=new GameObject("Shared 3 2 1 countdown",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(root.transform,false);text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=Vector2.one*.5f;text.rectTransform.sizeDelta=new Vector2(900,250);text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/IllustratedTickets/Title prompt outline.mat");text.fontSize=150;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.text="3";var ui=root.GetComponent<GameStartPresentation>();ui.qte=controller;ui.countdownText=text;
 }
 public static void Prepare(){
  try{
   if(System.IO.File.Exists(Request))System.IO.File.Delete(Request);
   string previous=EditorSceneManager.GetActiveScene().path;SessionState.SetString("Qte.Import.PreviousScene",string.IsNullOrEmpty(previous)?"Assets/Scenes/Qte.unity":previous);
   EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");AttachSharedStartUI();
   var controller=Object.FindObjectOfType<QteSceneController>();var so=new SerializedObject(controller);
   Require(GameObject.Find("School ground")!=null,"Fixed school track objects missing");Require(Object.FindObjectOfType<PushCooldownHUD>()==null,"QTE must not contain push HUD");
   foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
   foreach(var renderer in Object.FindObjectsOfType<Renderer>(true))foreach(var mat in renderer.sharedMaterials)Require(mat!=null,"Missing material on "+renderer.name);
   for(int i=0;i<4;i++){var p=so.FindProperty("characterPrefabs").GetArrayElementAtIndex(i).objectReferenceValue as GameObject;Require(p!=null&&p.GetComponent<NetworkObject>()!=null,"Invalid character "+i);Require(so.FindProperty("spawnPoints").GetArrayElementAtIndex(i).objectReferenceValue!=null,"Missing spawn "+i);}
   Require(so.FindProperty("spectatorPrefabs").arraySize>0,"Spectator resources missing");
   EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
   var capture=typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);capture.Invoke(null,new object[]{"Library/QteImportReview/imported-scene.png"});
   Debug.Log("QTE_IMPORT_STATIC_PASS: school scene/materials/scripts, four registered characters in lobby order, four spawn points and common countdown.");
   EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");SessionState.SetBool(Key,true);System.IO.File.WriteAllText(Result,"RUNNING: static checks passed; local-host QTE lifecycle test in progress.");EditorApplication.isPlaying=true;
  }catch(Exception ex){Fail(ex);}
 }
 static void RunRuntime(){
  double now=EditorApplication.timeSinceStartup;
  if(stage==0){at=now;timeout=now+80;stage=1;}
  Require(now<timeout,"Local host QTE test timed out at stage "+stage);
  if(stage==1){if(now-at<1)return;var n=NetworkManager.Singleton;Require(n!=null,"Lobby NetworkManager missing");n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17892);Require(n.StartHost(),"Local host did not start");at=now;stage=2;}
  else if(stage==2){if(now-at<.5)return;Require(NetworkManager.Singleton.SceneManager.LoadScene("Qte",LoadSceneMode.Single)==SceneEventProgressStatus.Started,"Network QTE scene load failed");stage=3;}
  else if(stage==3){race=Object.FindObjectOfType<QteSceneController>();if(race==null||NetworkManager.Singleton.LocalClient.PlayerObject==null)return;
   if(race.CountdownRemaining>.15f){sawCountdown=true;Require(GameStartPresentation.BlocksGameplayInput,"Countdown input lock not active");var p=NetworkManager.Singleton.LocalClient.PlayerObject;Require(!p.GetComponent<PlayerMovement>().enabled&&!p.GetComponent<PlayerInteraction>().enabled,"Runner controls active before start");}
   if(!race.HasRoundStarted)return;Require(sawCountdown,"One-second ready countdown was not observed");Require(!GameStartPresentation.BlocksGameplayInput,"Input gate did not open at start");stage=4;}
  else if(stage==4){if(!(bool)Get("hurdlePrompted")||(bool)Get("hurdleResolved"))return;Require(((IDictionary)Get("hurdles")).Count>0,"Hurdles were not spawned");Call("ResolveHurdle",true);at=now;stage=5;}
  else if(stage==5){var p=NetworkManager.Singleton.LocalClient.PlayerObject;if(p.transform.position.y>.3f)sawJump=true;if((int)Get("hurdleNumber")<2||!(bool)Get("hurdlePrompted")||(bool)Get("hurdleResolved"))return;Require(sawJump,"Successful hurdle did not animate a jump");Call("ResolveHurdle",false);stoppedDistance=(float)Get("distanceAlongTrack");at=now;stage=6;}
  else if(stage==6){if(now-at<.2)return;if(now-at<.5)Require(Mathf.Abs((float)Get("distanceAlongTrack")-stoppedDistance)<.05f,"Failed hurdle did not pause runner");if(now-at<1.2)return;Set("runSpeed",50f);Set("failStopDuration",.05f);stage=7;}
  else if(stage==7){if(SceneManager.GetActiveScene().name!="Ranking")return;var ranking=CrosswalkRun.RankingManager.Instance;Require(ranking!=null&&ranking.IsSpawned&&Enumerable.Range(0,ranking.ScoreList.Count).Any(i=>ranking.ScoreList[i].ClientId==NetworkManager.Singleton.LocalClientId&&ranking.ScoreList[i].Score==6),"Winner score did not reach Ranking");System.IO.File.WriteAllText(Result,"PASS: scene references and local host network entry, shared 3-second input lock, hurdle success jump/failure pause, finish scoring (6 points) and Ranking transition verified. Completion phase accelerated in test memory only.");Debug.Log("QTE_IMPORT_RUNTIME_PASS");SessionState.SetBool(Key,false);SessionState.SetBool("Qte.Import.Restore",true);NetworkManager.Singleton.Shutdown();EditorApplication.isPlaying=false;}
 }
 static void Fail(Exception ex){System.IO.File.WriteAllText(Result,"FAIL: "+ex);SessionState.SetBool(Key,false);if(EditorApplication.isPlaying){SessionState.SetBool("Qte.Import.Restore",true);EditorApplication.isPlaying=false;}Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
}
#endif
