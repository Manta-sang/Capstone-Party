#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class PartyFinaleBuilder {
 const string Request="Library/PartyFinale.request";
 static TMP_FontAsset font;
 static PartyFinaleBuilder(){EditorApplication.update+=Poll;}
 static void Poll(){if(!System.IO.File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;System.IO.File.Delete(Request);try{Build();System.IO.File.WriteAllText("Library/PartyFinale.result","PASS: GameBriefing and FinalRanking generated and registered.");}catch(Exception e){Debug.LogException(e);System.IO.File.WriteAllText("Library/PartyFinale.result",e.ToString());}}
 static RectTransform Rect(string n,Transform p,Vector2 pos,Vector2 size){var g=new GameObject(n,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
 static TMP_Text Text(string n,string value,Transform p,float x,float y,float w,float h,int size,Color color){var r=Rect(n,p,new Vector2(x,y),new Vector2(w,h));var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSharedMaterial=font.material;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
 static Material Mat(string n,Color color,bool glow=false){string path="Assets/Materials/FinalRanking/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}return m;}
 static GameObject Shape(string n,Transform p,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat){var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
 [MenuItem("Tools/Party/Build Finale and Briefing")]
 public static void Build(){
 if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
 var setup=EditorSceneManager.GetSceneManagerSetup();
 System.IO.Directory.CreateDirectory("Assets/Materials/FinalRanking");System.IO.Directory.CreateDirectory("Assets/Prefabs/FinalRanking");AssetDatabase.Refresh();
 EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");var source=Object.FindObjectOfType<TicketSweepTransition>();var travel=Object.Instantiate(source.gameObject);travel.name="Party travel tickets";var original=travel.GetComponent<TicketSweepTransition>();var cards=original.tickets;Object.DestroyImmediate(original);var effect=travel.AddComponent<PartyTicketTravel>();effect.tickets=cards;travel.GetComponent<Canvas>().sortingOrder=30000;var cg=travel.GetComponent<CanvasGroup>();cg.alpha=1;cg.blocksRaycasts=true;travel.SetActive(true);PrefabUtility.SaveAsPrefabAsset(travel,"Assets/Resources/PartyTravelTickets.prefab");Object.DestroyImmediate(travel);
 EditorSceneManager.OpenScene("Assets/Scenes/Ranking.unity");font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");foreach(var old in Object.FindObjectsOfType<RankingUI>(true))Object.DestroyImmediate(old);
 var panel=Object.FindObjectsOfType<Image>(true).First(i=>i.name=="RankPanel").transform;
 foreach(var b in panel.GetComponentsInChildren<Button>(true))Object.DestroyImmediate(b.gameObject);
 foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true))if(t!=null)Object.DestroyImmediate(t.gameObject);
 foreach(Transform child in panel.Cast<Transform>().ToArray())if(child.name.StartsWith("Score row")||child.name.StartsWith("Aircraft")||child.name=="Plane silhouette"||child.name=="Boarding validation stamp")Object.DestroyImmediate(child.gameObject);
 var guide=new GameObject("Game briefing controller").AddComponent<GameBriefing>();var ink=new Color(.25f,.20f,.17f);
 guide.titleText=Text("Game title · editable","GAME GUIDE",panel,-190,285,1000,85,48,ink);
 guide.rulesText=Text("Rules · editable in controller","",panel,-190,90,990,180,29,ink);guide.rulesText.alignment=TextAlignmentOptions.TopLeft;
 guide.controlsText=Text("Controls · editable in controller","",panel,-190,-140,990,140,29,ink);guide.controlsText.alignment=TextAlignmentOptions.TopLeft;
 guide.statusText=Text("Loading status","",panel,-190,-285,990,65,22,ink);
 Text("Airline stub","PARTY AIR\n\nGAME GUIDE\n\nREADY TO PLAY",panel,550,95,300,320,28,ink);
 Text("Printed heading","PARTY AIR  /  HOW TO PLAY",panel,0,373,1400,60,26,ink);
 Text("Rules heading","RULES · 게임 규칙",panel,-190,215,990,40,22,new Color(.25f,.44f,.49f));
 Text("Controls heading","CONTROLS · 조작법",panel,-190,-35,990,40,22,new Color(.25f,.44f,.49f));
 
 guide.countdownText=Text("Automatic departure countdown", "05",panel,550,-150,280,110,76,ink);
 Text("Countdown caption","DEPARTING IN",panel,550,-100,280,40,20,ink);
 guide.guides=new[]{G("CrosswalkRun","횡단보도 달리기","차량을 피하며 결승선까지 달리세요.\n먼저 도착할수록 높은 순위와 점수를 얻습니다.","이동  WASD / 방향키    점프  SPACE\n밀치기  SHIFT · 왼쪽 아래 원으로 재사용 확인"),G("SawBladeDodge","톱날 피하기","움직이는 톱날을 피해 오래 살아남으세요.\n다른 참가자를 밀쳐 생존 공간을 확보할 수 있습니다.","이동  WASD / 방향키    점프  SPACE\n밀치기  SHIFT"),G("Qte","학교 운동회 · 허들 레이스","허들 앞에 나타나는 방향키를 순서대로 입력하세요.\n성공하면 점프! 실패하면 잠시 멈춥니다.\n먼저 결승선을 통과하세요.","화면에 표시되는 방향키  ↑ ↓ ← →\n이 게임에서는 자유 이동과 밀치기를 사용하지 않습니다."),G("TileFlip","타일 뒤집기","바닥을 밟아 자신의 색으로 바꾸세요.\n종료 시 더 많은 타일을 가진 참가자가 승리합니다.","이동  WASD / 방향키    점프  SPACE\n밀치기  SHIFT"),G("BlockFall","마법 도서관 · 낙하물 피하기","붉은 경고가 표시된 칸을 피하세요.\n책은 착지한 뒤 사라질 때까지 위험합니다.\n한 번 맞으면 탈락! 60초 동안 오래 살아남으세요.","이동  WASD / 방향키    점프  SPACE\n밀치기  SHIFT · 시간이 지날수록 위험 구역 증가")};
 EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/GameBriefing.unity");
 BuildFinal();var builds=EditorBuildSettings.scenes.ToList();foreach(var name in new[]{"GameBriefing","FinalRanking"})if(!builds.Any(s=>s.path=="Assets/Scenes/"+name+".unity"))builds.Add(new EditorBuildSettingsScene("Assets/Scenes/"+name+".unity",true));EditorBuildSettings.scenes=builds.ToArray();AssetDatabase.SaveAssets();EditorSceneManager.RestoreSceneManagerSetup(setup);Debug.Log("PARTY_FINALE_BUILD_PASS");
 }
 static GameBriefing.Guide G(string scene,string title,string rules,string controls)=>new GameBriefing.Guide{scene=scene,title=title,rules=rules,controls=controls};
 static Mesh CrownMesh(){
 string path="Assets/Prefabs/FinalRanking/Crown silhouette.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
 const int segments=64;var vertices=new Vector3[segments*4];var triangles=new System.Collections.Generic.List<int>();
 for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;float peak=1-Mathf.Abs((i%8)-4)/4f;float top=.14f+peak*.32f;vertices[i*4]=new Vector3(Mathf.Cos(a)*.47f,0,Mathf.Sin(a)*.47f);vertices[i*4+1]=new Vector3(Mathf.Cos(a)*.47f,top,Mathf.Sin(a)*.47f);vertices[i*4+2]=new Vector3(Mathf.Cos(a)*.40f,0,Mathf.Sin(a)*.40f);vertices[i*4+3]=new Vector3(Mathf.Cos(a)*.40f,top,Mathf.Sin(a)*.40f);}
 for(int i=0;i<segments;i++){int a=i*4,b=((i+1)%segments)*4;foreach(var q in new[]{new[]{a,b,b+1,a+1},new[]{b+2,a+2,a+3,b+3},new[]{a+1,b+1,b+3,a+3},new[]{b,a,a+2,b+2}})triangles.AddRange(new[]{q[0],q[1],q[2],q[0],q[2],q[3]});}
 mesh=new Mesh{name="Eight point hollow crown"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
 }
 static void BuildFinal(){
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
 var concrete=Mat("Charred terminal concrete",new Color(.20f,.22f,.24f));var asphalt=Mat("Scorched runway",new Color(.075f,.085f,.10f));var metal=Mat("Burnt airport steel",new Color(.12f,.17f,.20f));var white=Mat("Weathered ivory",new Color(.66f,.64f,.53f));var ember=Mat("Residual fire",new Color(1,.22f,.025f),true);var gold=Mat("Winner gold",new Color(1,.68f,.10f),true);var gem=Mat("Crown ruby",new Color(.9f,.12f,.18f),true);
 var world=new GameObject("Ruined airport · editable fixed scenery").transform;
 Shape("Damaged runway",world,PrimitiveType.Cube,new Vector3(0,-.3f,5),new Vector3(65,.5f,60),asphalt);
 var random=new System.Random(471);for(int i=0;i<12;i++){float x=(i-5.5f)*3.7f;float height=3+(float)random.NextDouble()*4;Shape("Broken terminal bay "+i,world,PrimitiveType.Cube,new Vector3(x,height*.5f,14),new Vector3(.65f,height,.8f),concrete);var roof=Shape("Collapsed roof beam "+i,world,PrimitiveType.Cube,new Vector3(x,height,14),new Vector3(3.8f,.3f,3),metal);roof.transform.localRotation=Quaternion.Euler(i%3*9,0,i%2==0?12:-17);if(i%3!=0)Shape("Cracked wall panel "+i,world,PrimitiveType.Cube,new Vector3(x+1,1.1f,15),new Vector3(2,2.2f,.35f),concrete);}
 for(int i=0;i<65;i++){float x=(float)random.NextDouble()*45-22,z=7+(float)random.NextDouble()*15;var rubble=Shape("Blast rubble "+i,world,PrimitiveType.Cube,new Vector3(x,.15f,z),new Vector3(.3f+(float)random.NextDouble()*1.5f,.3f+(float)random.NextDouble(),.4f+(float)random.NextDouble()),i%3==0?metal:concrete);rubble.transform.rotation=Quaternion.Euler(random.Next(50),random.Next(180),random.Next(50));}
 for(int i=0;i<8;i++)Shape("Faded runway stripe "+i,world,PrimitiveType.Cube,new Vector3(-17+i*5,.0f,-2),new Vector3(2.4f,.02f,.3f),white);
 var plane=new GameObject("Wrecked aircraft section").transform;plane.SetParent(world);plane.position=new Vector3(-12,1.5f,8);plane.rotation=Quaternion.Euler(0,15,8);Shape("Broken fuselage",plane,PrimitiveType.Capsule,Vector3.zero,new Vector3(2.3f,4,2.3f),white).transform.localRotation=Quaternion.Euler(0,0,90);Shape("Damaged wing",plane,PrimitiveType.Cube,new Vector3(0,-.5f,-2),new Vector3(3,.2f,6),metal);for(int i=0;i<5;i++)Shape("Dark cabin window "+i,plane,PrimitiveType.Sphere,new Vector3(i-2,.5f,-1.12f),new Vector3(.36f,.45f,.08f),metal);
 for(int i=0;i<5;i++){var flame=Shape("Smoldering wreck "+i,world,PrimitiveType.Sphere,new Vector3(-17+i*8,.4f,10+i%2*4),new Vector3(.7f,.3f,.7f),ember);var light=flame.AddComponent<Light>();light.color=new Color(1,.3f,.08f);light.range=5;light.intensity=2;}
 var sign=Shape("Fallen airport sign",world,PrimitiveType.Cube,new Vector3(10,2,12),new Vector3(9,2.2f,.3f),metal);sign.transform.rotation=Quaternion.Euler(0,-10,-9);var label=new GameObject("Terminal sign text").AddComponent<TextMeshPro>();label.transform.SetParent(sign.transform,false);label.transform.localPosition=new Vector3(0,0,-.6f);label.transform.localRotation=Quaternion.identity;label.transform.localScale=new Vector3(.11f,.45f,1);label.font=font;label.text="DEPARTURES  /  출국";label.fontSize=6;label.alignment=TextAlignmentOptions.Center;
 var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,7,-14);camera.transform.LookAt(new Vector3(0,2,5));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.21f,.25f,.3f);camera.fieldOfView=52;
 RenderSettings.ambientLight=new Color(.45f,.47f,.54f);RenderSettings.fog=true;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogDensity=.018f;
 var sun=new GameObject("Late afternoon through smoke").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.78f,.53f);sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(38,-32,0);sun.shadows=LightShadows.Soft;
 var crownRoot=new GameObject("Floating winner crown");var band=new GameObject("Eight point gold crown",typeof(MeshFilter),typeof(MeshRenderer));band.transform.SetParent(crownRoot.transform,false);band.GetComponent<MeshFilter>().sharedMesh=CrownMesh();band.GetComponent<MeshRenderer>().sharedMaterial=gold;var jewels=new System.Collections.Generic.List<Renderer>();for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var tip=Shape("Crown point "+i,crownRoot.transform,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a+Mathf.PI/8)*.47f,.46f,Mathf.Sin(a+Mathf.PI/8)*.47f),new Vector3(.07f,.07f,.07f),gold);Object.DestroyImmediate(tip.GetComponent<Collider>());var jewel=Shape("Sparkling ruby "+i,crownRoot.transform,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.43f,.08f,Mathf.Sin(a)*.43f),Vector3.one*.10f,gem);Object.DestroyImmediate(jewel.GetComponent<Collider>());jewels.Add(jewel.GetComponent<Renderer>());}var animation=crownRoot.AddComponent<WinnerCrown>();animation.jewels=jewels.ToArray();animation.glow=crownRoot.AddComponent<Light>();animation.glow.color=new Color(1,.75f,.25f);animation.glow.range=4;var crown=PrefabUtility.SaveAsPrefabAsset(crownRoot,"Assets/Prefabs/FinalRanking/Winner crown.prefab");Object.DestroyImmediate(crownRoot);
 var final=new GameObject("Final ranking controller").AddComponent<FinalRanking>();final.crownPrefab=crown;final.characterPrefabs=new[]{"party_character","Another_player1","AnotherPlayer2","AnotherPlayer3"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/"+n+".prefab")).ToArray();final.displaySpots=new Transform[4];for(int i=0;i<4;i++){var spot=new GameObject("Contestant display spot "+(i+1)).transform;spot.position=i==0?new Vector3(0,.02f,1.5f):new Vector3((i-2)*3.1f,0,i==2?5.7f:5.0f);spot.rotation=Quaternion.Euler(0,180+(i==0?0:i*11-22),0);final.displaySpots[i]=spot;}
 var canvas=new GameObject("Final celebration UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
 Text("Celebration heading","SURVIVORS · FINAL BOARDING",canvas.transform,0,460,1600,70,36,new Color(1,.8f,.35f));final.winnerText=Text("Winner nickname and congratulations","우승을 축하합니다!",canvas.transform,0,330,1500,180,58,Color.white);final.rankingText=Text("Final scores","",canvas.transform,680,-210,460,300,28,Color.white);final.returnText=Text("Return countdown","",canvas.transform,0,-465,1500,65,28,new Color(1,.88f,.65f));
 var scoreBoard=Rect("Airport final departures board",canvas.transform,new Vector2(680,-150),new Vector2(500,510));var backing=scoreBoard.gameObject.AddComponent<Image>();backing.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Rounded ticket.asset");backing.type=Image.Type.Sliced;backing.color=new Color(.025f,.065f,.09f,.82f);backing.raycastTarget=false;Text("Final ranking heading","FINAL STANDINGS",canvas.transform,680,85,450,40,23,new Color(.70f,.86f,.91f));
 final.scoreRows=new TMP_Text[4];for(int i=0;i<4;i++){final.scoreRows[i]=Text("Score row "+(i+1),"",canvas.transform,680,15-i*100,450,95,i==0?44:29,Color.white);final.scoreRows[i].alignment=TextAlignmentOptions.MidlineLeft;}final.winnerText.enableAutoSizing=true;final.winnerText.fontSizeMin=28;final.winnerText.fontSizeMax=58;final.rankingText.gameObject.SetActive(false);
 AirportFinaleArt.Rebuild();
 EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/FinalRanking.unity");
 }
}
#endif
