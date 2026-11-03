using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PartyGameBag {
 readonly string[] scenes;readonly HashSet<string> played=new HashSet<string>();readonly List<string> candidates=new List<string>();
 public int Count=>played.Count;
 public PartyGameBag(string[] scenes){this.scenes=(string[])scenes.Clone();}
 public string Pick(System.Func<string,bool> enabled){
  candidates.Clear();foreach(var s in scenes)if(!played.Contains(s)&&enabled(s))candidates.Add(s);
  if(candidates.Count==0){foreach(var s in scenes)if(enabled(s))candidates.Add(s);if(candidates.Count>0)played.Clear();}
  return candidates.Count==0?null:candidates[Random.Range(0,candidates.Count)];
 }
 public void Commit(string scene){if(System.Array.IndexOf(scenes,scene)>=0)played.Add(scene);}
 public void Reset(){played.Clear();}
}
public static class PartyGameRotation {
 public static readonly string[] Scenes={"CrosswalkRun","SawBladeDodge","Qte","TileFlip","BlockFall"};
 public static readonly string[] Labels={"횡단보도 달리기","톱날 피하기","QTE","타일 뒤집기","블록폴"};
 static readonly PartyGameBag bag=new PartyGameBag(Scenes);
 static NetworkManager pendingNetwork;
 static string pending;
 public static int PlayedCount=>bag.Count;
 public static bool IsComplete=>bag.Count>=Scenes.Length;
 public static bool Loading=>pending!=null;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
 public static void Reset(){ClearPending();bag.Reset();if(PartyRoster.Current!=null&&PartyRoster.Current.IsSpawned&&PartyRoster.Current.IsServer)PartyRoster.Current.GamesStarted.Value=0;}
 static void ClearPending(){if(pendingNetwork!=null&&pendingNetwork.SceneManager!=null)pendingNetwork.SceneManager.OnLoadEventCompleted-=Completed;pendingNetwork=null;pending=null;}
 public static bool StartRandom(NetworkManager network,out string error){
  error="";
  if(network==null||!network.IsServer||!network.IsListening||Loading){error="호스트의 준비 완료 후 시작할 수 있습니다.";return false;}
  pending=bag.Pick(Application.CanStreamedLevelBeLoaded);
  if(pending==null){error="등록된 미니게임이 없습니다.";return false;}
  pendingNetwork=network;network.SceneManager.OnLoadEventCompleted+=Completed;
  var status=network.SceneManager.LoadScene(pending,LoadSceneMode.Single);
  if(status==SceneEventProgressStatus.Started)return true;
  error="씬 이동 실패: "+status;ClearPending();return false;
 }
 static void Completed(string scene,LoadSceneMode mode,List<ulong> completed,List<ulong> timedOut){
  if(scene!=pending)return;
  bool all=timedOut.Count==0;
  if(pendingNetwork!=null)foreach(var id in pendingNetwork.ConnectedClientsIds)if(!completed.Contains(id))all=false;
  if(all){bag.Commit(scene);if(PartyRoster.Current!=null&&PartyRoster.Current.IsSpawned)PartyRoster.Current.GamesStarted.Value=bag.Count;}
  ClearPending();
 }
}
