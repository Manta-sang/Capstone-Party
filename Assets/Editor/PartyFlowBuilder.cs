#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PartyFlowBuilder {
 const string Request="Library/PartyFlow.request";
 static TMP_FontAsset font;static Sprite rounded;
 static Color Ink=new Color(.14f,.18f,.22f),Red=new Color(.88f,.025f,.17f),Paper=new Color(.97f,.96f,.93f);
 static PartyFlowBuilder(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty)Build();}
 static RectTransform Rect(GameObject g,Transform p,float x,float y,float w,float h){var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
 static TMP_Text Text(string n,string text,Transform p,float x,float y,float w,float h,int size,Color color){var go=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));Rect(go,p,x,y,w,h);var t=go.GetComponent<TMP_Text>();t.font=font;t.text=text;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=size-3;t.fontSizeMax=size;t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;return t;}
 static Button Button(string label,Transform p,float x,float y,float w,float h,UnityAction action){var go=new GameObject(label,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));Rect(go,p,x,y,w,h);var image=go.GetComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=3;image.color=Red;var b=go.GetComponent<Button>();b.targetGraphic=image;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white;colors.selectedColor=Color.white;b.colors=colors;var text=Text("Label",label,go.transform,0,0,w-20,h-8,20,Color.white);text.alignment=TextAlignmentOptions.Center;UnityEventTools.AddPersistentListener(b.onClick,action);go.AddComponent<PartyButtonFeedback>();return b;}
 [MenuItem("Tools/Party/Build Common Developer Tools")]
 public static void Build(){
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");rounded=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Rounded ticket.asset");if(font==null||rounded==null)throw new Exception("Project UI assets missing");
  var go=new GameObject("Party Developer Tools",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var tools=go.AddComponent<PartyDeveloperTools>();tools.enabled=false;
  var panel=new GameObject("Developer panel",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var pr=Rect(panel,go.transform,0,0,374,850);pr.anchorMin=pr.anchorMax=pr.pivot=new Vector2(0,1);pr.anchoredPosition=new Vector2(18,-18);var pi=panel.GetComponent<Image>();pi.sprite=rounded;pi.type=Image.Type.Sliced;pi.color=Paper;tools.panel=panel;
  Text("Heading","개발자 도구  /  ESC",panel.transform,187,-37,330,42,25,Ink);
  tools.status=Text("Network and game status","현재 씬 · 네트워크 상태\n추첨 진행 0 / 5",panel.transform,187,-130,330,134,18,Ink);
  Text("Game list heading","원하는 게임으로 이동 · 호스트 전용",panel.transform,187,-224,330,36,17,Ink);
  var actions=new UnityAction[]{tools.GoCrosswalk,tools.GoSawBlade,tools.GoQte,tools.GoTileFlip,tools.GoBlockFall};tools.sceneButtons=new Button[5];for(int i=0;i<5;i++)tools.sceneButtons[i]=Button(PartyGameRotation.Labels[i],panel.transform,187,-275-i*57,330,47,actions[i]);
  tools.restartButton=Button("현재 게임 재시작",panel.transform,187,-579,330,45,tools.Restart);
  tools.characterButton=Button("캐릭터 로비",panel.transform,101,-635,158,45,tools.GoCharacter);tools.rankingButton=Button("랭킹 화면",panel.transform,273,-635,158,45,tools.GoRanking);
  tools.retryResultsButton=Button("결과 이동 재시도",panel.transform,187,-691,330,45,tools.RetryResults);
  tools.hostButton=Button("Host 시작",panel.transform,101,-747,158,45,tools.StartHost);tools.clientButton=Button("Client 참가",panel.transform,273,-747,158,45,tools.StartClient);
  Text("Help","직접 이동은 일반 추첨 기록에 포함되지 않습니다.",panel.transform,187,-807,330,40,15,Ink);
  var shortcut=Text("Esc shortcut","ESC  개발자 도구",go.transform,0,0,300,40,18,Ink);shortcut.rectTransform.anchorMin=shortcut.rectTransform.anchorMax=new Vector2(0,1);shortcut.rectTransform.anchoredPosition=new Vector2(175,-28);shortcut.gameObject.SetActive(false);
  tools.gameInfo=Text("BlockFall game HUD","",go.transform,0,0,740,54,25,Ink);tools.gameInfo.alignment=TextAlignmentOptions.Center;tools.gameInfo.rectTransform.anchorMin=tools.gameInfo.rectTransform.anchorMax=new Vector2(.5f,1);tools.gameInfo.rectTransform.anchoredPosition=new Vector2(0,-32);tools.gameInfo.gameObject.SetActive(false);
  foreach(Transform child in panel.transform){var rect=child as RectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);}
  tools.enabled=true;canvas.enabled=false;var rootRect=go.GetComponent<RectTransform>();rootRect.localScale=Vector3.one;rootRect.sizeDelta=new Vector2(1920,1080);rootRect.pivot=new Vector2(.5f,.5f);System.IO.Directory.CreateDirectory("Assets/Resources");PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/PartyDeveloperTools.prefab");Object.DestroyImmediate(go);
  var startScene=EditorSceneManager.GetActiveScene().path;
  foreach(var name in new[]{"TitleScene","Lobby","Character","Ranking","SawBladeDodge","Qte","TileFlip","BlockFall"}){
   EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
   foreach(var button in Object.FindObjectsOfType<Button>(true)){
    if(button.GetComponent<PartyButtonFeedback>()==null)button.gameObject.AddComponent<PartyButtonFeedback>();
   }
   foreach(var text in Object.FindObjectsOfType<TMP_Text>(true)){text.font=font;text.fontSharedMaterial=font.material;}
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  }
  ValidateBag();ValidateFeedback();AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");Capture();if(!string.IsNullOrEmpty(startScene))EditorSceneManager.OpenScene(startScene);System.IO.File.Delete(Request);
  Debug.Log("PARTY_FLOW_PASS: random bag 100 full five-game cycles; failed/uncommitted picks retained; shared developer prefab and gentle button feedback applied; CrosswalkRun scene untouched.");
 }
 static void ValidateBag(){
  var bag=new PartyGameBag(PartyGameRotation.Scenes);var randomState=UnityEngine.Random.state;
  try{
   for(int cycle=0;cycle<100;cycle++){var seen=new HashSet<string>();for(int i=0;i<5;i++){var pick=bag.Pick(s=>true);if(!seen.Add(pick))throw new Exception("Repeated game before full rotation");bag.Commit(pick);}if(seen.Count!=5)throw new Exception("A game was skipped");}
   bag.Reset();bag.Pick(s=>true);if(bag.Count!=0)throw new Exception("Uncommitted selection consumed a game");bag.Commit("Qte");if(bag.Pick(s=>false)!=null||bag.Count!=1)throw new Exception("Missing scenes altered progress");
   foreach(var scene in PartyGameRotation.Scenes)if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path.EndsWith("/"+scene+".unity")))throw new Exception("Game missing from build settings: "+scene);
  }finally{UnityEngine.Random.state=randomState;}
 }
 static void Capture(){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PartyDeveloperTools.prefab");if(prefab.transform.localScale.sqrMagnitude<.1f)throw new Exception("Developer canvas has zero scale");var sample=Object.Instantiate(prefab);sample.hideFlags=HideFlags.HideAndDontSave;var tools=sample.GetComponent<PartyDeveloperTools>();tools.status.text="Character · 호스트\n추첨 진행 0 / 5\nEsc로 개발자 도구를 열고 닫습니다.";
  foreach(var button in tools.sceneButtons)if(button.onClick.GetPersistentEventCount()!=1||button.onClick.GetPersistentTarget(0)!=tools)throw new Exception("Developer game link missing");
  var canvases=Object.FindObjectsOfType<Canvas>();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();var camera=Camera.main;
  foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();
  var developerCanvas=sample.GetComponent<Canvas>();developerCanvas.renderMode=RenderMode.WorldSpace;developerCanvas.overrideSorting=true;developerCanvas.sortingOrder=1000;sample.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);sample.GetComponent<RectTransform>().pivot=new Vector2(.5f,.5f);sample.transform.position=camera.transform.position+camera.transform.forward*.8f;sample.transform.rotation=camera.transform.rotation;sample.transform.localScale=Vector3.one*(2*camera.orthographicSize/1080);developerCanvas.enabled=true;Canvas.ForceUpdateCanvases();
  var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/PartyDeveloperTools-preview.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
  for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}Object.DestroyImmediate(sample);
 }
 static void ValidateFeedback(){
  var go=new GameObject("Button feedback verification",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
  try{
   var image=go.GetComponent<Image>();image.color=Red;var button=go.GetComponent<Button>();button.targetGraphic=image;var behavior=go.AddComponent<PartyButtonFeedback>();behavior.SendMessage("OnEnable");
   behavior.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(null));behavior.SendMessage("LateUpdate");if(image.color.g>.2f||image.color.r<.85f)throw new Exception("Hover washed out ticket red");
   behavior.OnPointerExit(new UnityEngine.EventSystems.PointerEventData(null));behavior.SendMessage("LateUpdate");if(image.color!=Red)throw new Exception("Hover did not restore base color");
   button.interactable=false;behavior.SendMessage("LateUpdate");if(Mathf.Abs(image.color.r-image.color.g)>.04f||image.color.r<.7f)throw new Exception("Disabled button is not pale gray");
   button.interactable=true;image.color=new Color(.78f,.79f,.8f);behavior.SendMessage("LateUpdate");if(image.color.r>.85f)throw new Exception("Dynamic ready state color was lost");
  }finally{Object.DestroyImmediate(go);}
 }
}
#endif
