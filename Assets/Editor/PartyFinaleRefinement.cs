#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class PartyFinaleRefinement
{
 static PartyFinaleRefinement(){EditorApplication.update+=Poll;}
 static void Poll(){if(!System.IO.File.Exists("Library/PartyFinale.refine")||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;string mode=System.IO.File.ReadAllText("Library/PartyFinale.refine").Trim();System.IO.File.Delete("Library/PartyFinale.refine");try{if(mode=="timing"){Timing();return;}if(mode=="layout"){Layout();return;}Apply();System.IO.File.WriteAllText("Library/PartyFinale.refined","PASS: automatic guide, detailed airport, network avatars, rank rows, tile timer and isolated scene lighting saved.");}catch(Exception e){Debug.LogException(e);System.IO.File.WriteAllText("Library/PartyFinale.refined",e.ToString());}}
 static void Layout(){var setup=EditorSceneManager.GetSceneManagerSetup();EditorSceneManager.OpenScene("Assets/Scenes/GameBriefing.unity");var guide=Object.FindObjectOfType<GameBriefing>();guide.countdownText.rectTransform.anchoredPosition=new Vector2(550,-150);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());EditorSceneManager.OpenScene("Assets/Scenes/FinalRanking.unity");var lettering=GameObject.Find("Fuselage airline lettering");lettering.transform.localPosition=new Vector3(.2f,.95f,-1.37f);lettering.GetComponent<TMP_Text>().fontSize=5.5f;EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());EditorSceneManager.RestoreSceneManagerSetup(setup);System.IO.File.WriteAllText("Library/PartyFinale.layout","PASS: countdown above barcode and aircraft lettering clearance.");}
 static void Timing()
 {
  if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Cannot save current edits");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  EditorSceneManager.OpenScene("Assets/Scenes/GameBriefing.unity");var guide=Object.FindObjectOfType<GameBriefing>();guide.readingSeconds=5;guide.countdownText.text="05";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  EditorSceneManager.OpenScene("Assets/Scenes/FinalRanking.unity");foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>t.name.StartsWith("Low survivor platform")||t.name.StartsWith("Survivor podium")).ToArray())Object.DestroyImmediate(t.gameObject);
  var final=Object.FindObjectOfType<FinalRanking>();for(int i=0;i<final.displaySpots.Length;i++){final.displaySpots[i].position=i==0?new Vector3(0,.02f,1.5f):new Vector3((i-2)*3.1f,0,i==2?5.7f:5.0f);final.displaySpots[i].rotation=Quaternion.Euler(0,180+(i==0?0:i*11-22),0);}
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  foreach(var name in PartyGameRotation.Scenes){string path="Assets/Scenes/"+name+".unity";string original=System.IO.File.ReadAllText(path);var lights=new Dictionary<string,string>();foreach(Match match in Regex.Matches(original,@"(?ms)--- !u!108 &(\d+).*?m_Intensity: ([^\r\n]+)"))lights[match.Groups[1].Value]=match.Groups[2].Value;
   EditorSceneManager.OpenScene(path);Object.FindObjectOfType<GameStartPresentation>().countdownText.text="1";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());string saved=System.IO.File.ReadAllText(path);saved=Regex.Replace(saved,@"(?ms)(--- !u!108 &(\d+).*?m_Intensity: )[^\r\n]+",m=>lights.TryGetValue(m.Groups[2].Value,out var value)?m.Groups[1].Value+value:m.Value);System.IO.File.WriteAllText(path,saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  }
  EditorSceneManager.RestoreSceneManagerSetup(setup);System.IO.File.WriteAllText("Library/PartyFinale.timing","PASS: five-second guide, one-second countdown text, front winner / rear fallen poses, all four platforms removed.");
 }
 public static void Apply()
 {
  if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Cannot save current edits");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  EditorSceneManager.OpenScene("Assets/Scenes/GameBriefing.unity");var prior=Object.FindObjectOfType<GameBriefing>();var guides=prior.guides;
  PartyFinaleBuilder.Build();
  EditorSceneManager.OpenScene("Assets/Scenes/GameBriefing.unity");Object.FindObjectOfType<GameBriefing>().guides=guides;EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  foreach(var scene in new[]{"CrosswalkRun","SawBladeDodge","TileFlip","Qte","BlockFall"})
  {
   string path="Assets/Scenes/"+scene+".unity";string original=System.IO.File.ReadAllText(path);var intensities=new Dictionary<string,string>();
   foreach(Match match in Regex.Matches(original,@"(?ms)--- !u!108 &(\d+).*?m_Intensity: ([^\r\n]+)"))intensities[match.Groups[1].Value]=match.Groups[2].Value;
   EditorSceneManager.OpenScene(path);
   var old=Object.FindObjectOfType<SceneLightingProfile>();if(old!=null)Object.DestroyImmediate(old.gameObject);
   var profile=new GameObject("Original game lighting · scene isolation").AddComponent<SceneLightingProfile>();
   profile.ambientMode=RenderSettings.ambientMode;profile.sky=RenderSettings.ambientSkyColor;profile.equator=RenderSettings.ambientEquatorColor;profile.ground=RenderSettings.ambientGroundColor;profile.ambientIntensity=RenderSettings.ambientIntensity;profile.reflectionIntensity=RenderSettings.reflectionIntensity;profile.skybox=RenderSettings.skybox;profile.sun=RenderSettings.sun!=null?RenderSettings.sun:Object.FindObjectsOfType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);profile.fog=RenderSettings.fog;profile.fogMode=RenderSettings.fogMode;profile.fogColor=RenderSettings.fogColor;profile.fogDensity=RenderSettings.fogDensity;profile.fogStart=RenderSettings.fogStartDistance;profile.fogEnd=RenderSettings.fogEndDistance;
   if(scene=="TileFlip"){var ui=Object.FindObjectOfType<GameStartPresentation>();ui.tileTimer.fontSize=64;ui.tileTimer.text="60";ui.tileTimer.rectTransform.sizeDelta=new Vector2(300,100);ui.tileTimer.rectTransform.anchoredPosition=new Vector2(0,-65);}
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
   // BlockFall's edit-time crystal pulses must not bake random phase intensities into the scene.
   string saved=System.IO.File.ReadAllText(path);saved=Regex.Replace(saved,@"(?ms)(--- !u!108 &(\d+).*?m_Intensity: )[^\r\n]+",m=>intensities.TryGetValue(m.Groups[2].Value,out var value)?m.Groups[1].Value+value:m.Value);System.IO.File.WriteAllText(path,saved);
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  }
  AssetDatabase.SaveAssets();EditorSceneManager.RestoreSceneManagerSetup(setup);
 }
}
#endif
