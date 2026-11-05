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
 static int stage;static double at,timeout;static bool sawCountdown,sawJump;static float stoppedDistance;static bool sawFinalHurdleHidden;static bool sawFinishCamera;static Vector3 finishCameraAt;static double finishCameraTime;static QteSceneController race;
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
  CaptureQteLighting();
  var controller=Object.FindObjectOfType<QteSceneController>();Require(controller!=null,"QTE controller missing");
  var so=new SerializedObject(controller);var list=so.FindProperty("characterPrefabs");string[] guids={"b3f543bae91f9b94598a8372bfe747f8","0c76b793a5061c942beb17b723fa0688","43f35dc74b8af374c8ba48ec4ee160e0","07562273415467b4ca8e2b02136ecec3"};list.arraySize=4;for(int i=0;i<4;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));so.FindProperty("koreanFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");so.ApplyModifiedPropertiesWithoutUndo();
  var root=GameObject.Find("Round Start Canvas");if(root!=null)Object.DestroyImmediate(root);root=new GameObject("Round Start Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GameStartPresentation));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=150;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var text=new GameObject("Shared 3 2 1 countdown",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(root.transform,false);text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=Vector2.one*.5f;text.rectTransform.sizeDelta=new Vector2(900,250);text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/IllustratedTickets/Title prompt outline.mat");text.fontSize=150;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.text="3";var ui=root.GetComponent<GameStartPresentation>();ui.qte=controller;ui.countdownText=text;
 }
 static void CaptureQteLighting(){
  var old=Object.FindObjectOfType<SceneLightingProfile>();if(old!=null)Object.DestroyImmediate(old.gameObject);
  var p=new GameObject("QTE original lighting · scene isolation").AddComponent<SceneLightingProfile>();
  p.ambientMode=RenderSettings.ambientMode;p.sky=RenderSettings.ambientSkyColor;p.equator=RenderSettings.ambientEquatorColor;p.ground=RenderSettings.ambientGroundColor;p.ambientIntensity=RenderSettings.ambientIntensity;p.reflectionIntensity=RenderSettings.reflectionIntensity;p.skybox=RenderSettings.skybox;p.sun=RenderSettings.sun!=null?RenderSettings.sun:Object.FindObjectsOfType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);p.fog=RenderSettings.fog;p.fogMode=RenderSettings.fogMode;p.fogColor=RenderSettings.fogColor;p.fogDensity=RenderSettings.fogDensity;p.fogStart=RenderSettings.fogStartDistance;p.fogEnd=RenderSettings.fogEndDistance;
 }
 public static void Prepare(){
  stage=0;at=0;timeout=0;sawCountdown=false;sawJump=false;sawFinishCamera=false;sawFinalHurdleHidden=false;finishCameraTime=0;race=null;
  try{
   if(System.IO.File.Exists(Request))System.IO.File.Delete(Request);
   string previous=EditorSceneManager.GetActiveScene().path;SessionState.SetString("Qte.Import.PreviousScene",string.IsNullOrEmpty(previous)?"Assets/Scenes/Qte.unity":previous);
   EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");AttachSharedStartUI();
   var controller=Object.FindObjectOfType<QteSceneController>();var so=new SerializedObject(controller);
   var camera=Camera.main;camera.transform.position=so.FindProperty("overviewPosition").vector3Value;camera.transform.LookAt(so.FindProperty("overviewLookAt").vector3Value);camera.fieldOfView=so.FindProperty("overviewFieldOfView").floatValue;
   Require(GameObject.Find("School ground")!=null,"Fixed school track objects missing");Require(so.FindProperty("competitionHurdlePrefab").objectReferenceValue!=null,"Competition hurdle prefab missing");Require(GameObject.Find("QTE Football Pitch")!=null&&GameObject.Find("Continuous football markings")!=null,"New turf/continuous lines missing");Require(GameObject.Find("QTE School")!=null,"Grouped school missing");Require(Object.FindObjectOfType<QteSequenceHud>()!=null&&GameObject.Find("QTE Race HUD")==null,"Minimal HUD replacement missing");Require(Object.FindObjectOfType<PushCooldownHUD>()==null,"QTE must not contain push HUD");
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
  System.IO.File.WriteAllText("Library/QteScopedMerge/progress.txt","Stage "+stage);
  double now=EditorApplication.timeSinceStartup;
  if(stage==0){at=now;timeout=now+150;stage=1;}
  Require(now<timeout,"Local host QTE test timed out at stage "+stage);
  if(stage==1){if(now-at<1)return;var n=NetworkManager.Singleton;Require(n!=null,"Lobby NetworkManager missing");n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17892);Require(n.StartHost(),"Local host did not start");at=now;stage=2;}
  else if(stage==2){if(now-at<.5)return;Require(NetworkManager.Singleton.SceneManager.LoadScene("Qte",LoadSceneMode.Single)==SceneEventProgressStatus.Started,"Network QTE scene load failed");stage=3;}
  else if(stage==3){race=Object.FindObjectOfType<QteSceneController>();if(race==null||NetworkManager.Singleton.LocalClient.PlayerObject==null)return;
   if(race.CountdownRemaining>.15f){sawCountdown=true;var presentation=Object.FindObjectOfType<GameStartPresentation>();Require(!presentation.countdownText.text.Contains("<color")&&presentation.countdownText.color==Color.white,"QTE overrides shared countdown styling");Require(GameStartPresentation.BlocksGameplayInput,"Countdown input lock not active");var p=NetworkManager.Singleton.LocalClient.PlayerObject;Require(!p.GetComponent<PlayerMovement>().enabled&&!p.GetComponent<PlayerInteraction>().enabled,"Runner controls active before start");}
   if(!race.HasRoundStarted)return;Require(sawCountdown,"One-second ready countdown was not observed");Require(!GameStartPresentation.BlocksGameplayInput,"Input gate did not open at start");stage=4;}
  else if(stage==4){if(!(bool)Get("hurdlePrompted")||(bool)Get("hurdleResolved"))return;Require(((IDictionary)Get("hurdles")).Count>0,"Hurdles were not spawned");var hud=Object.FindObjectOfType<QteSequenceHud>();Require(hud.arrows.Length==4&&hud.timer!=null&&hud.GetComponentsInChildren<TMP_Text>(true).Length==9,"HUD must contain only four arrow/alias pairs and deadline");var expected=(int[])Get("expectedDirections");KeyCode[] arrows={KeyCode.UpArrow,KeyCode.DownArrow,KeyCode.LeftArrow,KeyCode.RightArrow},wasd={KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D};for(int i=0;i<expected.Length;i++){Call("HandleQteKey",i%2==0?wasd[expected[i]]:arrows[expected[i]]);if(i==1){hud.Present(true,expected,2,1.2f,2,false);Require(hud.cards[0].color.g>hud.cards[0].color.r&&hud.cards[2].color.r>hud.cards[2].color.g,"Completed/active keys lack distinct colors");typeof(TicketPresentationValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Library/QteSchoolPresentation/runtime-hud.png"});}}Require((bool)Get("queuedJump")&&(int)Get("hurdleNumber")==2,"Mixed WASD/arrow sequence failed");at=now;stage=5;}
  else if(stage==5){var p=NetworkManager.Singleton.LocalClient.PlayerObject;if(p.transform.position.y>.3f)sawJump=true;if((int)Get("hurdleNumber")<2||!(bool)Get("hurdlePrompted")||(bool)Get("hurdleResolved"))return;Require(sawJump,"Successful hurdle did not animate a jump");var expected=(int[])Get("expectedDirections");KeyCode[] wasd={KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D};Call("HandleQteKey",wasd[(expected[0]+1)%4]);Require((bool)Get("queuedFailure")&&(float)Get("stunUntil")==0,"Wrong key tripped runner before hurdle contact");at=now;stage=6;}
  else if(stage==6){if((float)Get("stunUntil")<=Time.unscaledTime)return;stoppedDistance=(float)Get("distanceAlongTrack");Require(Mathf.Abs(stoppedDistance-((int)Get("hurdleNumber")*(float)Get("hurdleSpacing")-.45f))<.06f,"Fall did not start at hurdle contact");Require(!(bool)Get("jumping")&&!(bool)Get("queuedJump"),"Failure left jump active");at=now;stage=61;}
  else if(stage==61){if(now-at<.2)return;if(now-at<.5)Require(Mathf.Abs((float)Get("distanceAlongTrack")-stoppedDistance)<.05f,"Trip penalty advanced race distance");if(now-at<1.2)return;var anim=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<Animator>();Require(Mathf.Abs(anim.speed-1)<.01f,"Fall animation speed was not restored");Set("runSpeed",50f);Set("failStopDuration",.05f);var fixtureOwners=(List<ulong>)Get("owners");fixtureOwners.AddRange(new ulong[]{2,3,4});var distances=(Dictionary<ulong,float>)Get("observedRaceDistance");distances[2]=90;distances[3]=150;distances[4]=50;stage=7;}
  else if(stage==7){if(SceneManager.GetActiveScene().name=="Qte"&&(bool)Get("allHurdlesResolved")){sawFinalHurdleHidden=true;Require(!(bool)Get("hurdlePrompted")&&Object.FindObjectOfType<QteSequenceHud>().group.alpha<.01f,"HUD remained/reappeared after final hurdle");}if(SceneManager.GetActiveScene().name=="Qte"&&(bool)Get("localFinished")){if(finishCameraTime==0)finishCameraTime=now;if(now-finishCameraTime>1){if(!sawFinishCamera){finishCameraAt=Camera.main.transform.position;sawFinishCamera=true;}else Require(Vector3.Distance(finishCameraAt,Camera.main.transform.position)<.15f,"Finish camera orbited after runner faced viewer");}}if(SceneManager.GetActiveScene().name!="Ranking")return;Require(sawFinishCamera,"Finish camera hold was not observed");Require(sawFinalHurdleHidden,"Final hurdle HUD hide was not observed");var ranking=CrosswalkRun.RankingManager.Instance;Require(ranking!=null&&ranking.IsSpawned&&Enumerable.Range(0,ranking.ScoreList.Count).Any(i=>ranking.ScoreList[i].ClientId==NetworkManager.Singleton.LocalClientId&&ranking.ScoreList[i].Score==6),"Winner score did not reach Ranking");Require(ranking.ScoreList.Count==4&&Enumerable.Range(0,ranking.ScoreList.Count).Any(i=>ranking.ScoreList[i].ClientId==3&&ranking.ScoreList[i].Score==4)&&Enumerable.Range(0,ranking.ScoreList.Count).Any(i=>ranking.ScoreList[i].ClientId==2&&ranking.ScoreList[i].Score==2)&&Enumerable.Range(0,ranking.ScoreList.Count).Any(i=>ranking.ScoreList[i].ClientId==4&&ranking.ScoreList[i].Score==1),"Five-second finish grace / distance ranking score table failed");System.IO.File.WriteAllText(Result,"PASS: shared countdown styling, final-hurdle HUD hide, mixed WASD/arrow input, contact-timed stumble/recovery, stable finish camera, 5-second grace and distance-ranking fixtures (4/2/1), scene references and local host network entry, shared 3-second input lock, hurdle success jump/failure pause, finish scoring (6 points) and Ranking transition verified. Completion phase accelerated in test memory only.");Debug.Log("QTE_IMPORT_RUNTIME_PASS");SessionState.SetBool(Key,false);SessionState.SetBool("Qte.Import.Restore",true);NetworkManager.Singleton.Shutdown();EditorApplication.isPlaying=false;}
 }
 static void Fail(Exception ex){System.IO.File.WriteAllText(Result,"FAIL: "+ex);SessionState.SetBool(Key,false);if(EditorApplication.isPlaying){SessionState.SetBool("Qte.Import.Restore",true);EditorApplication.isPlaying=false;}Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
}
#endif
