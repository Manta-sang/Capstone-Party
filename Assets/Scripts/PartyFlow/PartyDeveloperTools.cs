using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class PartyDeveloperTools : MonoBehaviour {
 public GameObject panel;
 public TMP_Text status;
 public TMP_Text gameInfo;
 public Button[] sceneButtons;
 public Button hostButton,clientButton,restartButton,characterButton,rankingButton,retryResultsButton;
 public Button soloStartButton;
 public static bool Visible {get;private set;}=false;
 static PartyDeveloperTools instance;
 Canvas canvas;NetworkManager observed;bool wasOnline;float refreshAt,nextThemeAt;string feedback="";BlockFall.BlockFallGame block;SD_GameManager blade;QteSceneController qte;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Clear(){instance=null;Visible=false;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Install(){if(instance!=null)return;var prefab=Resources.Load<GameObject>("PartyDeveloperTools");if(prefab!=null)Instantiate(prefab);else Debug.LogError("PartyDeveloperTools prefab missing; build it with Tools/Party/Build Common Developer Tools.");}
 void Awake(){if(instance!=null){Destroy(gameObject);return;}instance=this;transform.localScale=Vector3.one;DontDestroyOnLoad(gameObject);canvas=GetComponent<Canvas>();SceneManager.sceneLoaded+=Loaded;RefreshScene();}
 void Loaded(Scene scene,LoadSceneMode mode){RefreshScene();}
 void RefreshScene(){
  canvas.enabled=true;panel.SetActive(Visible);feedback="";
  block=FindObjectOfType<BlockFall.BlockFallGame>();blade=FindObjectOfType<SD_GameManager>();
  qte=FindObjectOfType<QteSceneController>();
  gameInfo.gameObject.SetActive(block!=null);
  foreach(var b in FindObjectsOfType<Button>(true))if(b.GetComponent<PartyButtonFeedback>()==null)b.gameObject.AddComponent<PartyButtonFeedback>();
  if(FindObjectOfType<EventSystem>()==null)new GameObject("Developer UI EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
 }
 void Update(){
  var network=NetworkManager.Singleton;
  if(observed!=network){if(observed!=null)observed.OnServerStarted-=NewSession;observed=network;if(observed!=null)observed.OnServerStarted+=NewSession;}
  bool online=network!=null&&network.IsListening;if(wasOnline&&!online){PartyGameRotation.Reset();CrosswalkRun.CharacterRoundRestart.DeveloperSoloStart=false;}wasOnline=online;
  if(Time.unscaledTime>=nextThemeAt){
   nextThemeAt=Time.unscaledTime+1;
   foreach(var b in FindObjectsOfType<Button>(true))if(b.GetComponent<PartyButtonFeedback>()==null)b.gameObject.AddComponent<PartyButtonFeedback>();
   foreach(var t in FindObjectsOfType<TMP_Text>(true))if(t.font!=status.font){t.font=status.font;t.fontSharedMaterial=status.font.material;}
  }
  if(Input.GetKeyDown(KeyCode.Escape)){Visible=!Visible;panel.SetActive(Visible);}
  if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.15f;
  bool authority=!online||network.IsServer;bool available=authority&&!PartyGameRotation.Loading;
  foreach(var b in sceneButtons)b.interactable=available;
  restartButton.interactable=characterButton.interactable=rankingButton.interactable=available;
  hostButton.interactable=clientButton.interactable=network!=null&&!online;
  retryResultsButton.interactable=online&&network.IsServer&&(blade!=null||block!=null);
  if(soloStartButton!=null){var round=FindObjectOfType<CrosswalkRun.CharacterRoundRestart>();soloStartButton.interactable=round!=null&&round.CanStartSolo&&!PartyGameRotation.Loading;}
  string game=block!=null?block.DeveloperStatus:blade!=null?blade.DeveloperStatus:qte!=null?qte.DeveloperStatus:"";
  if(block!=null)gameInfo.text=block.GameplayStatus;
  status.text=SceneManager.GetActiveScene().name+" · "+(!online?"오프라인":network.IsServer?"호스트":"참가자 · 이동은 호스트만")+"\n추첨 진행 "+PartyGameRotation.PlayedCount+" / 5"+(string.IsNullOrEmpty(game)?"":"\n"+game)+(string.IsNullOrEmpty(feedback)?"":"\n"+feedback);
 }
 void NewSession(){PartyGameRotation.Reset();CrosswalkRun.CharacterRoundRestart.DeveloperSoloStart=false;}
 public void StartSoloGame(){var round=FindObjectOfType<CrosswalkRun.CharacterRoundRestart>();if(round!=null&&round.CanStartSolo){round.StartSoloGame();feedback="1인 테스트 시작";}}
 public void GoCrosswalk()=>Travel("CrosswalkRun");public void GoSawBlade()=>Travel("SawBladeDodge");public void GoQte()=>Travel("Qte");public void GoTileFlip()=>Travel("TileFlip");public void GoBlockFall()=>Travel("BlockFall");
 public void GoCharacter()=>Travel("Character");public void GoRanking()=>Travel("Ranking");public void Restart()=>Travel(SceneManager.GetActiveScene().name);
 void Travel(string scene){
  if(PartyGameRotation.Loading){feedback="씬 로딩 완료를 기다려 주세요.";return;}
  if(!Application.CanStreamedLevelBeLoaded(scene)){feedback="Build Settings에 없는 씬입니다.";return;}
  var n=NetworkManager.Singleton;
  if(n!=null&&n.IsListening){if(!n.IsServer){feedback="호스트만 이동할 수 있습니다.";return;}var result=n.SceneManager.LoadScene(scene,LoadSceneMode.Single);feedback=result==SceneEventProgressStatus.Started?"이동 중...":"이동 실패: "+result;}
  else SceneManager.LoadScene(scene);
 }
 public void StartHost(){var n=NetworkManager.Singleton;if(n!=null&&!n.IsListening)feedback=n.StartHost()?"호스트 시작":"호스트 시작 실패";}
 public void StartClient(){var n=NetworkManager.Singleton;if(n!=null&&!n.IsListening)feedback=n.StartClient()?"참가 요청":"참가 요청 실패";}
 public void RetryResults(){if(blade!=null)blade.RetryDeveloperResults();else if(block!=null)block.LoadResults();}
 void OnDestroy(){if(instance!=this)return;SceneManager.sceneLoaded-=Loaded;if(observed!=null)observed.OnServerStarted-=NewSession;instance=null;}
}
