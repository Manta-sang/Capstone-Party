using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
public sealed class PartySplash : MonoBehaviour {
 [SerializeField] private string nicknameScene="NicknameScene";
 [SerializeField] private CanvasGroup prompt;
 [SerializeField] private TicketSweepTransition transition;
 private bool entering;
 private float acceptAfter;
 private void OnEnable(){acceptAfter=Time.unscaledTime+.25f;}
 private void Update(){
  if(prompt!=null)prompt.alpha=Mathf.Lerp(.12f,1f,.5f+.5f*Mathf.Sin(Time.unscaledTime*3.6f));
  // Esc remains reserved for the existing developer-panel toggle.
  if(entering||Time.unscaledTime<acceptAfter||PartyDeveloperTools.Visible||Input.GetKeyDown(KeyCode.Escape))return;
  bool touch=Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began;
  if(Input.anyKeyDown||touch)EnterNickname();
 }
 public void EnterNickname(){
  if(entering)return;
  if(!Application.CanStreamedLevelBeLoaded(nicknameScene)){Debug.LogError("[Title] NicknameScene must be enabled in Build Settings.");return;}
  entering=true;
  if(transition!=null)transition.Play(nicknameScene);
  else SceneManager.LoadSceneAsync(nicknameScene,LoadSceneMode.Single);
 }
}
