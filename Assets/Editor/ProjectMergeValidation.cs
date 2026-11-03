#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ProjectMergeValidation {
 const string Request="Library/ProjectMerge.request",Result="Library/ProjectMerge.result";
 static ProjectMergeValidation(){EditorApplication.update+=Run;}
 static void Run(){
  if(!System.IO.File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(EditorApplication.isPlayingOrWillChangePlaymode){System.IO.File.WriteAllText(Result,"WAIT: exit Play Mode before saving/validating scene edits.");return;}
  string mode;
  try{mode=System.IO.File.ReadAllText(Request).Trim();if(mode.Length==0)return;System.IO.File.Delete(Request);}
  catch(System.IO.IOException){return;} // A request writer may still hold the file briefly.
  try {
   if(mode=="save"){
    if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Could not save open scene changes");
    AssetDatabase.SaveAssets();System.IO.File.WriteAllText(Result,"SAVED: open scenes and assets saved before merge.");Debug.Log("PROJECT_MERGE_SAVE_PASS");return;
   }
   // Read the merged scene files; never save stale in-memory scenes over Git's result.
   var setup=EditorSceneManager.GetSceneManagerSetup();
   foreach(string name in new[]{"Character","CrosswalkRun","SawBladeDodge","TileFlip","Qte","BlockFall"}){
    EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");int lists=0;
    foreach(var component in Object.FindObjectsOfType<MonoBehaviour>(true)){
     if(component==null)throw new Exception("Missing scene script in "+name);
     var so=new SerializedObject(component);var list=so.FindProperty(name=="BlockFall"?"characters":"characterPrefabs");
     if(list==null||!list.isArray)continue;lists++;
     if(list.arraySize!=4)throw new Exception(name+" does not have four character prefabs");
     for(int i=0;i<4;i++){var prefab=list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;if(prefab==null||prefab.GetComponent<NetworkObject>()==null)throw new Exception(name+" invalid network prefab "+i);}
    }
    if(lists==0)throw new Exception("Character list missing in "+name);
    if(name=="Character"&&Object.FindObjectOfType<CharacterCabinLobby>().leaveButton==null)throw new Exception("Character cabin leave button lost");
    Debug.Log("PROJECT_MERGE_CHARACTER_PASS: "+name+" = 4 valid network character prefabs");
   }
   foreach(var name in new[]{"NicknameScene","Lobby","Ranking"}){
    EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");var background=GameObject.Find("Enlarged ticket background");if(background==null||background.transform.childCount!=42)throw new Exception("Ticket background lost: "+name);
    foreach(var b in Object.FindObjectsOfType<Button>(true))for(int i=0;i<b.onClick.GetPersistentEventCount();i++)if(b.onClick.GetPersistentTarget(i)==null)throw new Exception("Button event lost: "+name+"/"+b.name);
   }
   EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");var transition=Object.FindObjectOfType<TicketSweepTransition>();if(transition==null||transition.tickets.Length!=43||transition.selectedTicket==null)throw new Exception("Title transition lost");
   var prefabList=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");if(prefabList==null||prefabList.PrefabList.Any(p=>p.Prefab==null))throw new Exception("Network prefab registration missing");
   foreach(var name in new[]{"party_character","Another_player1","AnotherPlayer2","AnotherPlayer3"}){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/"+name+".prefab");if(prefab==null||!prefabList.PrefabList.Any(p=>p.Prefab==prefab))throw new Exception("Unregistered player: "+name);}
   EditorSceneManager.RestoreSceneManagerSetup(setup);
   System.IO.File.WriteAllText(Result,"PASS: all six game/lobby character lists contain 4 valid registered prefabs; three ticket backgrounds, button events and title transition retained.");Debug.Log("PROJECT_MERGE_VALIDATION_PASS");
  }catch(Exception exception){System.IO.File.WriteAllText(Result,"FAIL: "+exception);Debug.LogException(exception);}
 }
}
#endif
