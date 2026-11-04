#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TicketPileUpgrade {
 const string Request="Library/TicketPile.request",Dir="Assets/UI/IllustratedTickets/";
 static Sprite paper,edge,bars,dashes;static TMP_FontAsset font;
 static readonly Color Ink=new Color(.32f,.25f,.19f),Cream=new Color(.99f,.96f,.88f),Blue=new Color(.25f,.44f,.49f);
 static TicketPileUpgrade(){EditorApplication.delayCall+=Run;}
 static void Run(){if(System.IO.File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorSceneManager.GetActiveScene().isDirty)Apply();}
 static RectTransform Rect(string n,Transform p,float x,float y,float w,float h){var g=new GameObject(n,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image Image(string n,Transform p,float x,float y,float w,float h,Color c,Sprite sprite=null){var r=Rect(n,p,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.color=c;i.sprite=sprite;i.raycastTarget=false;return i;}
 static TMP_Text Text(string n,string text,Transform p,float x,float y,float w,float h,int size,Color color){var r=Rect(n,p,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
 static Sprite Save(Texture2D t,string name){string path=Dir+name+".png";System.IO.File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=2048;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);}
 static void Assets(){
  const int w=1500,h=830;var silhouette=new Texture2D(w,h,TextureFormat.RGBA32,false);var contour=new Texture2D(w,h,TextureFormat.RGBA32,false);var colors=new Color32[w*h];var outlines=new Color32[w*h];
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){
   float px=x+.5f,py=y+.5f,qx=Mathf.Abs(px-w*.5f)-(w*.5f-25),qy=Mathf.Abs(py-h*.5f)-(h*.5f-25);
   float d=25-Mathf.Sqrt(Mathf.Max(0,qx)*Mathf.Max(0,qx)+Mathf.Max(0,qy)*Mathf.Max(0,qy))-Mathf.Min(Mathf.Max(qx,qy),0);
   // Both cutouts are part of the alpha silhouette, including its border.
   d=Mathf.Min(d,Mathf.Sqrt((px-1115)*(px-1115)+py*py)-18);
   d=Mathf.Min(d,Mathf.Sqrt((px-1115)*(px-1115)+(py-h)*(py-h))-18);
   float a=Mathf.Clamp01(d+.5f);colors[y*w+x]=new Color(1,1,1,a);outlines[y*w+x]=new Color(1,1,1,a*Mathf.Clamp01(3.1f-d));
  }
  silhouette.SetPixels32(colors);silhouette.Apply();contour.SetPixels32(outlines);contour.Apply();paper=Save(silhouette,"Die-cut ticket paper");edge=Save(contour,"Die-cut ticket contour");
  var barcode=new Texture2D(256,64,TextureFormat.RGBA32,false);var random=new System.Random(81);var pixels=new Color32[256*64];int at=2;while(at<252){int width=random.Next(1,5);for(int x=at;x<Mathf.Min(at+width,253);x++)for(int y=2;y<62;y++)pixels[y*256+x]=Color.white;at+=width+random.Next(1,4);}barcode.SetPixels32(pixels);barcode.Apply();bars=Save(barcode,"Printed barcode");
  var holes=new Texture2D(8,640,TextureFormat.RGBA32,false);for(int y=0;y<640;y++)for(int x=0;x<8;x++){float dx=x-3.5f,dy=(y%20)-9.5f;float d=Mathf.Sqrt(dx*dx+Mathf.Pow(Mathf.Max(0,Mathf.Abs(dy)-2.5f),2));holes.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1.5f-d)));}holes.Apply();dashes=Save(holes,"Fine paper perforation");
 }
 static void Delete(Transform p,string n){var t=p.Find(n);if(t!=null)Object.DestroyImmediate(t.gameObject);}
 static void Polish(string scene){
  EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");var panel=Object.FindObjectsOfType<Image>(true).First(i=>i.name==(scene=="Ranking"?"RankPanel":"LobbyPanel"));var r=panel.rectTransform;var parent=r.parent;
  Delete(panel.transform,"Fine tear perforation");Delete(parent,"Soft ticket outer outline");Delete(parent,panel.name+" soft shadow");
  foreach(var t in panel.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name=="Ticket tear notch"||t.name.StartsWith("Perforation ")||t.name.StartsWith("Tear seam ")||t.name=="Paper cut contour"||t.name=="Boarding validation stamp").ToArray())if(t!=null)Object.DestroyImmediate(t.gameObject);
  panel.sprite=paper;panel.type=UnityEngine.UI.Image.Type.Simple;panel.color=Cream;
  var shadow=Image(panel.name+" soft shadow",parent,0,-13,1506,836,new Color(.28f,.26f,.23f,.19f),paper);shadow.transform.SetSiblingIndex(r.GetSiblingIndex());
  var perf=Image("Fine tear perforation",r,365,0,8,640,new Color(.50f,.40f,.28f,.58f),dashes);
  Image("Paper cut contour",r,0,0,1500,830,new Color(.53f,.40f,.26f,.85f),edge);
  var stamp=Rect("Boarding validation stamp",r,550,-151,265,35);stamp.localRotation=Quaternion.Euler(0,0,-6);
  Image("Stamped top rule",stamp,0,19,250,1,new Color(.26f,.44f,.49f,.55f));Image("Stamped bottom rule",stamp,0,-19,250,1,new Color(.26f,.44f,.49f,.55f));Text("Stamped ink","READY TO BOARD",stamp,0,0,250,34,18,new Color(.26f,.44f,.49f,.8f));
  EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  if(scene=="NicknameScene"){
   var group=GameObject.Find("Ticket landing group");var copy=Object.Instantiate(group);copy.name="Selected check-in ticket visual";
   foreach(var feedback in copy.GetComponentsInChildren<PartyButtonFeedback>(true))Object.DestroyImmediate(feedback);
   foreach(var controller in copy.GetComponentsInChildren<MonoBehaviour>(true))if(controller!=null && !(controller is Graphic) && !(controller is UnityEngine.UI.BaseMeshEffect) && !(controller is Mask) && !(controller is RectMask2D) && !(controller is LayoutElement))Object.DestroyImmediate(controller);
   foreach(var button in copy.GetComponentsInChildren<Button>(true))Object.DestroyImmediate(button);
   foreach(var feedback in copy.GetComponentsInChildren<PartyButtonFeedback>(true))Object.DestroyImmediate(feedback);
   foreach(var g in copy.GetComponentsInChildren<Graphic>(true))g.raycastTarget=false;
   var cg=copy.GetComponent<CanvasGroup>();cg.interactable=false;cg.blocksRaycasts=false;
   PrefabUtility.SaveAsPrefabAsset(copy,Dir+"Selected check-in ticket.prefab");Object.DestroyImmediate(copy);
  }
  Debug.Log("DIE_CUT_TICKET_PASS: "+scene+"; true transparent circular cutouts, aligned paper edge and shadow.");
 }
 static RectTransform Generic(Transform parent,int index,Vector2 pos,float scale,float rotation){
  var r=Rect("Scattered boarding pass "+index,parent,pos.x,pos.y,1500,830);r.localScale=Vector3.one*scale;r.localRotation=Quaternion.Euler(0,0,rotation);
  Image("Paper shadow",r,0,-13,1506,836,new Color(.22f,.19f,.15f,.24f),paper);
  var p=Image("Die-cut paper",r,0,0,1500,830,index%3==0?new Color(.96f,.94f,.85f):Cream,paper);p.gameObject.AddComponent<Mask>().showMaskGraphic=true;
  var color=index%4==0?new Color(.47f,.68f,.70f):index%4==1?new Color(.98f,.73f,.37f):new Color(.99f,.60f,.24f);
  Image("Printed airline band",p.transform,0,373,1500,84,color);Image("Printed footer",p.transform,0,-389,1500,52,color);
  Text("Airline","PARTY AIR  /  BOARDING PASS",p.transform,-230,373,950,72,29,Ink);
  string[] codes={"ICN","NRT","LHR","CDG","JFK","SYD"},cities={"SEOUL","TOKYO","LONDON","PARIS","NEW YORK","SYDNEY"};int from=index%6,to=(index+3)%6;
  Text("Passenger","PASSENGER  /  "+new[]{"KIM / MIN","LEE / JUN","PARK / JI","CHOI / YUN"}[index%4],p.transform,-235,285,950,36,22,Ink);
  Text("Origin label","FROM",p.transform,-535,227,260,28,18,Blue);Text("Destination label","TO",p.transform,65,227,260,28,18,Blue);
  Text("Origin airport",codes[from],p.transform,-535,158,330,110,82,Ink);Text("Destination airport",codes[to],p.transform,65,158,330,110,82,Ink);
  Text("Origin city",cities[from],p.transform,-535,89,330,36,24,Blue);Text("Destination city",cities[to],p.transform,65,89,330,36,24,Blue);
  Image("Flight direction",p.transform,-235,158,170,92,Blue,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/BoardingPass/Aircraft silhouette.asset"));
  string[] labels={"FLIGHT","DATE","GATE","SEAT"},values={"PA "+(127+index).ToString("000"),"04 NOV","A"+(index%12+1).ToString("00"),(index%28+1).ToString("00")+new[]{"A","B","C","D"}[index%4]};
  for(int field=0;field<4;field++){float x=-600+field*255;Text(labels[field]+" label",labels[field],p.transform,x,-21,220,30,18,Blue);Text(labels[field]+" value",values[field],p.transform,x,-68,220,54,30,Ink);}
  Text("Boarding details","BOARDING  09:40     /     GROUP "+(index%4+1)+"     /     ECONOMY",p.transform,-235,-150,950,34,20,Blue);
  Image("Main security barcode",p.transform,-400,-250,520,67,Ink,bars);Text("Ticket document number","ETKT  004 "+(72910480+index)+"    /    KEEP THIS PASS",p.transform,-400,-307,620,32,16,Ink);
  Text("Boarding seal","CHECKED IN\nPARTY AIR",p.transform,155,-257,300,82,24,Blue);
  Image("Perforated fold",p.transform,365,0,8,780,new Color(.50f,.40f,.28f,.58f),dashes);
  Text("Cabin class","ECONOMY",p.transform,550,373,330,60,27,Ink);
  Text("Passenger copy",codes[from]+"  >  "+codes[to],p.transform,550,230,330,70,35,Ink);
  Text("Stub flight details","FLIGHT  "+values[0]+"\nGATE  "+values[2]+"\nSEAT  "+values[3],p.transform,550,70,320,150,25,Blue);
  Text("Airline footer","BOARDING PASS  /  PLEASE RETAIN FOR YOUR JOURNEY",p.transform,-235,-389,970,45,17,Ink);
  Image("Printed barcode",p.transform,550,-200,280,80,Ink,bars);
  Text("Serial","P A R T Y  /  "+(index+1).ToString("0000"),p.transform,550,-274,330,40,19,Ink);
  Image("Cut paper contour",p.transform,0,0,1500,830,new Color(.53f,.40f,.26f,.85f),edge);
  return r;
 }
 static RectTransform BuildPile(Transform parent,bool selected,out RectTransform focus,out RectTransform[] cards){
  var pile=Rect("Scattered ticket pile",parent,0,0,1920,1080);var list=new List<RectTransform>();var random=new System.Random(901);
  // The first overlapping layer guarantees full coverage; upper layers break the grid.
  for(int y=0;y<3;y++)for(int x=0;x<4;x++)list.Add(Generic(pile,list.Count,new Vector2((x-1.5f)*700,(y-1)*440),.68f,(float)random.NextDouble()*8-4));
  for(int j=0;j<30;j++)list.Add(Generic(pile,list.Count,new Vector2((float)random.NextDouble()*2600-1300,(float)random.NextDouble()*1600-800),.36f+(float)random.NextDouble()*.25f,(float)random.NextDouble()*64-32));
  focus=null;
  if(selected){focus=Generic(pile,list.Count,new Vector2(100,-55),.43f,-11);focus.name="Concealed check-in boarding pass";list.Add(focus);}
  cards=list.ToArray();return pile;
 }
 static void Splash(){
  EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");var old=Object.FindObjectOfType<TicketSweepTransition>();if(old!=null)Object.DestroyImmediate(old.gameObject);
  var root=new GameObject("Flying ticket transition",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup),typeof(TicketSweepTransition));
  var c=root.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=30000;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
  var transition=root.GetComponent<TicketSweepTransition>();transition.pile=BuildPile(root.transform,true,out transition.selectedTicket,out transition.tickets);transition.veil=root.GetComponent<CanvasGroup>();transition.veil.alpha=0;transition.veil.blocksRaycasts=false;
  var so=new SerializedObject(Object.FindObjectOfType<PartySplash>());so.FindProperty("transition").objectReferenceValue=transition;so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
 }
 static void Background(string scene){
  EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");var group=GameObject.Find("Ticket landing group").transform;Delete(group.parent,"Enlarged ticket background");RectTransform focus;RectTransform[] cards;var pile=BuildPile(group.parent,false,out focus,out cards);pile.name="Enlarged ticket background";pile.localScale=Vector3.one/.43f;pile.localRotation=Quaternion.Euler(0,0,11);pile.anchoredPosition=-(Vector2)(pile.localRotation*new Vector3(100,-55,0))/.43f;pile.SetSiblingIndex(group.GetSiblingIndex());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
 }
 [MenuItem("Tools/Party/Upgrade Die Cut Tickets And Pile Zoom")]
 public static void Apply(){font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");Assets();Splash();foreach(var scene in new[]{"NicknameScene","Lobby","Ranking"})Background(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");System.IO.File.Delete(Request);Debug.Log("TICKET_PILE_UPGRADE_PASS: detailed airline passes shared by Nickname/Lobby/Ranking; chosen ticket hides the form until final zoom.");}
}
#endif
