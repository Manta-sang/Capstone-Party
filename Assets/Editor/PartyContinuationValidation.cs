#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PartyContinuationValidation {
 const string Request="Library/PartyContinuation.request";
 static PartyContinuationValidation(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty)Validate();}
 [MenuItem("Tools/Party/Validate Five Game Continuation")]
 public static void Validate(){
  var bag=new PartyGameBag(PartyGameRotation.Scenes);var state=UnityEngine.Random.state;
  try{for(int run=0;run<100;run++){bag.Reset();var seen=new HashSet<string>();for(int round=1;round<=5;round++){string game=bag.Pick(s=>true);if(!seen.Add(game))throw new Exception("Repeated next game");bag.Commit(game);if(bag.Count!=round)throw new Exception("Incorrect round progression");}if(bag.Count!=PartyGameRotation.Scenes.Length)throw new Exception("Final result not reached");}}
  finally{UnityEngine.Random.state=state;}
  var original=EditorSceneManager.GetActiveScene().path;
  EditorSceneManager.OpenScene("Assets/Scenes/Ranking.unity");var ranking=Object.FindObjectOfType<RankingUI>();var so=new SerializedObject(ranking);
  if(ranking.actionButtonText.text!="다음 게임으로 가기"||so.FindProperty("flowHint").objectReferenceValue==null)throw new Exception("Ranking UI binding missing");
  if(ranking.actionButton.onClick.GetPersistentTarget(0)!=ranking||ranking.actionButton.onClick.GetPersistentMethodName(0)!="OnActionButtonClicked")throw new Exception("Ranking next button disconnected");
  EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");var qte=Object.FindObjectOfType<QteSceneController>();if(qte==null)throw new Exception("QTE scene controller missing");var q=new SerializedObject(qte);if(q.FindProperty("spawnPoints").arraySize<4||q.FindProperty("qteText").objectReferenceValue==null||q.FindProperty("koreanFont").objectReferenceValue==null)throw new Exception("QTE setup incomplete");
  foreach(var component in Object.FindObjectsOfType<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject)>0)throw new Exception("Missing QTE script: "+component.name);
  EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");var cabin=Object.FindObjectOfType<CharacterCabinLobby>();if(cabin==null)throw new Exception("Cabin missing");
  foreach(var text in Object.FindObjectsOfType<TMP_Text>())if(text.gameObject.activeInHierarchy&&(text.text.Contains("/5")||text.text.Contains("5종")||text.text.Contains("5개")))throw new Exception("Cabin reveals game count");
  if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
  System.IO.File.Delete(Request);Debug.Log("PARTY_CONTINUATION_PASS: 100 five-game sequences without duplicates; final result boundary; ranking button/hint; QTE controller and four spawn references restored; no visible cabin game count.");
 }
}
#endif
