#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Authored mesh assets and editable scene objects. Nothing here runs during a match.
public static class AirportFinaleArt
{
 const string Dir="Assets/Materials/FinalRanking/";
 static Material ivory,blue,steel,glass,rubber,concrete,asphalt,paint,ember;
 static TMP_FontAsset font;
 static Material Mat(string name,Color c,float metallic=0,float gloss=.25f){string path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",gloss);return m;}
 static Transform Group(string name,Transform p,Vector3 pos){var g=new GameObject(name).transform;g.SetParent(p,false);g.localPosition=pos;return g;}
 static GameObject Primitive(string name,Transform p,PrimitiveType type,Vector3 pos,Vector3 size,Material m){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
 static GameObject Box(string n,Transform p,Vector3 pos,Vector3 size,Material m)=>Primitive(n,p,PrimitiveType.Cube,pos,size,m);
 static GameObject MeshObject(string name,Transform p,List<Vector3> vertices,List<int> triangles,Material m){string path="Assets/Prefabs/FinalRanking/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.name=name;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=m;g.isStatic=true;return g;}
 static void Quad(List<int> t,int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d});}
 static void Beam(string n,Transform p,Vector3 a,Vector3 b,float width,Material m){var g=Box(n,p,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),m);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
 static void Sign(string n,string value,Transform p,Vector3 pos,float width,float height,float size,Color color){var t=new GameObject(n).AddComponent<TextMeshPro>();t.transform.SetParent(p,false);t.transform.localPosition=pos;t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,height);}
 static void Lathe(string name,Transform p,float[] xs,float[] rs,Material material,float begin=0,float end=Mathf.PI*2){var v=new List<Vector3>();var t=new List<int>();const int n=48;for(int x=0;x<xs.Length;x++)for(int j=0;j<=n;j++){float a=Mathf.Lerp(begin,end,j/(float)n);v.Add(new Vector3(xs[x],Mathf.Sin(a)*rs[x],Mathf.Cos(a)*rs[x]));}for(int i=0;i<xs.Length-1;i++)for(int j=0;j<n;j++){int a=i*(n+1)+j,b=a+n+1;Quad(t,a,b,b+1,a+1);}MeshObject(name,p,v,t,material);}
 static void Extrude(string name,Transform p,Vector3[] outline,Vector3 depth,Material material){var v=new List<Vector3>();var t=new List<int>();foreach(var pt in outline)v.Add(pt-depth*.5f);foreach(var pt in outline)v.Add(pt+depth*.5f);int count=outline.Length;for(int i=1;i<count-1;i++){t.AddRange(new[]{0,i+1,i,count,count+i,count+i+1});}for(int i=0;i<count;i++)Quad(t,i,(i+1)%count,(i+1)%count+count,i+count);if(Vector3.Dot(Vector3.Cross(outline[1]-outline[0],outline[2]-outline[0]),depth)<0)for(int j=0;j<t.Count;j+=3){int swap=t[j+1];t[j+1]=t[j+2];t[j+2]=swap;}MeshObject(name,p,v,t,material);}
 public static void Rebuild()
 {
  var old=GameObject.Find("Ruined airport · editable fixed scenery");if(old!=null)Object.DestroyImmediate(old);
  font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Black SDF.asset");
  ivory=Mat("Aircraft ceramic pearl",new Color(.72f,.75f,.73f),.32f,.53f);blue=Mat("Party Air midnight teal",new Color(.035f,.25f,.34f),.4f,.48f);steel=Mat("Brushed structural alloy",new Color(.28f,.34f,.38f),.72f,.55f);glass=Mat("Smoked terminal glazing",new Color(.08f,.18f,.24f),.65f,.82f);rubber=Mat("Carbon and rubber",new Color(.055f,.065f,.07f),.12f,.16f);concrete=Mat("Fractured light concrete",new Color(.42f,.42f,.39f));asphalt=Mat("Weathered apron",new Color(.21f,.24f,.26f),0,.18f);paint=Mat("Weathered runway markings",new Color(.87f,.76f,.38f));ember=Mat("Ember detail",new Color(1,.32f,.065f));ember.EnableKeyword("_EMISSION");ember.SetColor("_EmissionColor",new Color(1,.25f,.025f)*2);
  var world=Group("Ruined airport · editable fixed scenery",null,Vector3.zero);
  var floor=Box("Airport apron · playable collision",world,new Vector3(0,-.28f,8),new Vector3(80,.5f,75),asphalt);floor.AddComponent<BoxCollider>();
  for(int x=-5;x<=5;x++)Box("Concrete expansion joint",world,new Vector3(x*5,.003f,8),new Vector3(.025f,.012f,60),rubber);
  for(int z=-2;z<9;z++)Box("Apron panel joint",world,new Vector3(0,.004f,z*5),new Vector3(65,.012f,.025f),rubber);
  for(int i=0;i<12;i++)Box("Taxiway dash",world,new Vector3(-25+i*4.5f,.012f,-4),new Vector3(2.3f,.015f,.22f),paint);
  for(int i=0;i<3;i++){var line=Box("Aircraft stand guidance",world,new Vector3(-12+i*12,.014f,11),new Vector3(.12f,.02f,18),paint);Box("Stand stop bar",world,new Vector3(-12+i*12,.015f,2.2f),new Vector3(3,.02f,.13f),paint);}
  Aircraft(world);Terminal(world);Tower(world);Props(world);Smoke(world);
  var random=new System.Random(719);
  for(int i=0;i<48;i++){var pile=Group("Shattered cladding "+i,world,new Vector3(-24+(float)random.NextDouble()*48,.1f,10+(float)random.NextDouble()*15));float w=.3f+(float)random.NextDouble()*1.7f;Extrude("Jagged rubble "+i,pile,new[]{new Vector3(-w,0,-.2f),new Vector3(w*.7f,.1f,-.4f),new Vector3(w*.8f,.5f,.1f),new Vector3(-w*.4f,.9f,.5f)},Vector3.forward*.35f,i%3==0?steel:concrete);pile.localRotation=Quaternion.Euler(0,random.Next(360),random.Next(-20,20));}
  var cam=Camera.main;cam.transform.position=new Vector3(1,7.5f,-15.5f);cam.transform.LookAt(new Vector3(1,2.8f,8));cam.fieldOfView=52;cam.backgroundColor=new Color(.43f,.51f,.57f);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.54f,.60f);RenderSettings.fog=true;RenderSettings.fogColor=cam.backgroundColor;RenderSettings.fogDensity=.007f;
  var sun=GameObject.Find("Late afternoon through smoke").GetComponent<Light>();sun.intensity=1.25f;sun.color=new Color(1,.89f,.73f);sun.transform.rotation=Quaternion.Euler(38,-30,0);RenderSettings.sun=sun;
 }
 static void Aircraft(Transform world)
 {
  var p=Group("PARTY AIR · detailed damaged passenger jet",world,new Vector3(-8,2.4f,12));p.localRotation=Quaternion.Euler(0,-12,-3);
  float[] xs={-9,-8,-6,-3,1,4,5.7f,7,7.8f,8.25f};float[] rs={.2f,.6f,1.22f,1.35f,1.35f,1.3f,1.1f,.75f,.35f,.04f};
  Lathe("Contoured airliner fuselage",p,xs,rs,ivory);
  var belly=Group("Lower painted fuselage",p,new Vector3(0,-.008f,0));Lathe("Curved teal belly livery",belly,xs,System.Array.ConvertAll(rs,r=>r+.012f),blue,Mathf.PI,Mathf.PI*2);
  for(int side=-1;side<=1;side+=2){
   for(int i=0;i<16;i++){float x=-5.7f+i*.61f;var rim=Primitive("Window surround "+side+" "+i,p,PrimitiveType.Sphere,new Vector3(x,.42f,side*1.259f),new Vector3(.32f,.43f,.065f),steel);Primitive("Recessed oval passenger glass "+side+" "+i,p,PrimitiveType.Sphere,new Vector3(x,.42f,side*1.30f),new Vector3(.25f,.35f,.045f),glass);}
   for(int d=0;d<2;d++){float x=d==0?4.6f:-6.1f;Box("Door panel",p,new Vector3(x,-.03f,side*1.27f),new Vector3(.72f,1.65f,.055f),ivory);for(int edge=-1;edge<=1;edge+=2)Box("Door recessed seal",p,new Vector3(x+edge*.37f,-.03f,side*1.307f),new Vector3(.025f,1.7f,.02f),rubber);Box("Door upper seam",p,new Vector3(x,.81f,side*1.30f),new Vector3(.76f,.025f,.02f),rubber);Box("Door lower seam",p,new Vector3(x,-.87f,side*1.30f),new Vector3(.76f,.025f,.02f),rubber);Box("Flush door handle",p,new Vector3(x+.18f,-.1f,side*1.32f),new Vector3(.18f,.05f,.03f),steel);}
   Extrude("Swept main wing "+side,p,new[]{new Vector3(1.4f,-.55f,side*.8f),new Vector3(-2.5f,-.35f,side*8.8f),new Vector3(-4.15f,-.35f,side*8.65f),new Vector3(-3.1f,-.55f,side*1)},Vector3.up*.18f,ivory);
   Beam("Wing flap joint "+side,p,new Vector3(-3,-.43f,side*2),new Vector3(-3.9f,-.25f,side*7.7f),.035f,steel);
   Extrude("Upturned winglet "+side,p,new[]{new Vector3(-2.5f,-.3f,side*8.8f),new Vector3(-3.2f,1.05f,side*9),new Vector3(-4.15f,.9f,side*8.85f),new Vector3(-4.15f,-.3f,side*8.65f)},Vector3.right*.06f,blue);
   var engine=Group("Turbofan engine "+side,p,new Vector3(.25f,-1.15f,side*3.6f));
   Lathe("Engine nacelle "+side,engine,new[]{-1.9f,-1.5f,.9f,1.25f,1.32f},new[]{.55f,.77f,.84f,.8f,.7f},ivory);
   Lathe("Metal intake lip "+side,engine,new[]{1.25f,1.38f,1.4f,1.34f},new[]{.80f,.78f,.67f,.64f},steel);
   var disc=Primitive("Dark engine fan throat",engine,PrimitiveType.Cylinder,new Vector3(1.16f,0,0),new Vector3(1.3f,.035f,1.3f),rubber);disc.transform.localRotation=Quaternion.Euler(0,0,90);
   for(int blade=0;blade<18;blade++){float a=blade*Mathf.PI/9;var fan=Box("Swept fan blade "+blade,engine,new Vector3(1.2f,Mathf.Cos(a)*.4f,Mathf.Sin(a)*.4f),new Vector3(.04f,.09f,.47f),steel);fan.transform.localRotation=Quaternion.Euler(blade*20+30,0,0);}
   Primitive("Engine spinner",engine,PrimitiveType.Sphere,new Vector3(1.28f,0,0),new Vector3(.38f,.30f,.30f),steel);
   Box("Engine pylon",p,new Vector3(-.3f,-.65f,side*3.6f),new Vector3(1.5f,.45f,.18f),steel);
   Extrude("Horizontal tailplane "+side,p,new[]{new Vector3(-6.5f,.3f,side*.5f),new Vector3(-8,.5f,side*3.5f),new Vector3(-9,.5f,side*3.6f),new Vector3(-8.3f,.3f,side*.3f)},Vector3.up*.11f,ivory);
   Beam("Main gear oleo "+side,p,new Vector3(-1,-.8f,side*1),new Vector3(-1,-2,side*1.2f),.16f,steel);
   for(int wheel=0;wheel<2;wheel++){var tire=Primitive("Main landing tire",p,PrimitiveType.Cylinder,new Vector3(-1,-2,side*(1.15f+wheel*.36f)),new Vector3(.65f,.15f,.65f),rubber);tire.transform.localRotation=Quaternion.Euler(90,0,0);}
  }
  Extrude("Swept vertical tail fin",p,new[]{new Vector3(-5.8f,.8f,0),new Vector3(-7.5f,4.4f,0),new Vector3(-8.6f,4.4f,0),new Vector3(-8.8f,.45f,0)},Vector3.forward*.18f,blue);
  Sign("Fuselage airline lettering","PARTY AIR",p,new Vector3(.2f,.95f,-1.37f),7,1.2f,5.5f,new Color(.04f,.17f,.22f));
  for(int side=-1;side<=1;side+=2)Extrude("Faceted cockpit glazing "+side,p,new[]{new Vector3(5.1f,.65f,side*1.12f),new Vector3(5.9f,.78f,side*.84f),new Vector3(6.85f,.40f,side*.72f),new Vector3(6.5f,.12f,side*1.04f),new Vector3(5.25f,.18f,side*1.23f)},Vector3.forward*.015f,glass);
  Sign("Tail airline emblem","PA",p,new Vector3(-7.65f,2.65f,-.115f),2,1.5f,9f,Color.white);
  Beam("Nose gear strut",p,new Vector3(5,-.65f,0),new Vector3(5,-2,0),.13f,steel);var nose=Primitive("Twin nose tire",p,PrimitiveType.Cylinder,new Vector3(5,-2,0),new Vector3(.56f,.25f,.56f),rubber);nose.transform.localRotation=Quaternion.Euler(90,0,0);
  // Torn skin and burn scars distinguish the aircraft from a clean toy prop.
  for(int i=0;i<7;i++){float x=-4.4f+i*.19f;Primitive("Blast soot scar "+i,p,PrimitiveType.Sphere,new Vector3(x,-.05f,-1.34f),new Vector3(.5f,.7f,.035f),rubber);Beam("Exposed fuselage rib "+i,p,new Vector3(x,-.5f,-1.39f),new Vector3(x,.4f,-1.38f),.035f,steel);}
 }
 static void Terminal(Transform world)
 {
  var terminal=Group("Damaged international terminal · curtain wall",world,new Vector3(0,0,25));
  Box("Terminal foundation",terminal,new Vector3(0,.25f,1),new Vector3(47,.5f,9),concrete);
  for(int bay=0;bay<14;bay++){float x=-22.1f+bay*3.4f;Beam("Terminal vertical mullion "+bay,terminal,new Vector3(x,.5f,-1),new Vector3(x,8,-1),.14f,steel);
   for(int level=0;level<3;level++){float y=1.65f+level*2.45f;Box("Window transom",terminal,new Vector3(x+1.7f,y-1.18f,-1),new Vector3(3.4f,.12f,.16f),steel);if((bay+level*3)%7!=0)Box("Curtain wall glazing "+bay+" "+level,terminal,new Vector3(x+1.7f,y,-.96f),new Vector3(3.22f,2.28f,.07f),glass);else{Beam("Blast-open glazing diagonal",terminal,new Vector3(x,y-1,-1),new Vector3(x+3.1f,y+.8f,-1),.07f,steel);}}
   var roof=Group("Curved roof bay "+bay,terminal,new Vector3(x+1.7f,8,0));var outline=new[]{new Vector3(0,0,-2),new Vector3(0,.45f,0),new Vector3(0,.7f,3),new Vector3(0,.3f,7)};
   if(bay%5!=2)Extrude("Curved roof cladding "+bay,roof,outline,Vector3.right*3.3f,ivory);
   for(int r=0;r<3;r++)Beam("Exposed roof truss",roof,new Vector3(-1.6f,r*.23f,r*3-2),new Vector3(1.6f,.4f+r*.1f,r*3-1),.10f,steel);
  }
  Box("Terminal fascia",terminal,new Vector3(0,6.2f,-1.16f),new Vector3(46,.85f,.20f),blue);
  Sign("Airport identity","PARTY INTERNATIONAL AIRPORT   /   국제공항",terminal,new Vector3(0,6.2f,-1.29f),43,1.5f,8,Color.white);
  for(int i=0;i<5;i++){Box("Gate signage",terminal,new Vector3(-16+i*8,3.1f,-1.25f),new Vector3(2.1f,.85f,.2f),blue);Sign("Gate identifier","GATE "+(i+1).ToString("00"),terminal,new Vector3(-16+i*8,3.1f,-1.38f),2,.7f,4,Color.white);}
  var bridge=Group("Damaged telescopic boarding bridge",world,new Vector3(-10,3.3f,18));bridge.localRotation=Quaternion.Euler(0,15,0);
  Box("Bridge floor",bridge,new Vector3(0,-.8f,0),new Vector3(2.8f,.25f,9),steel);Box("Bridge roof",bridge,new Vector3(0,.9f,0),new Vector3(3,.18f,9),ivory);
  for(int i=0;i<10;i++){float z=-4+i*.9f;for(int side=-1;side<=1;side+=2){Box("Bridge glazing",bridge,new Vector3(side*1.3f,.1f,z),new Vector3(.06f,1.5f,.77f),glass);Box("Bridge accordion rib",bridge,new Vector3(side*1.4f,.05f,z),new Vector3(.14f,1.75f,.14f),steel);}}
  Beam("Bridge support",world,new Vector3(-10,.2f,20),new Vector3(-10,2.6f,20),.36f,steel);
 }
 static void Tower(Transform world)
 {
  var tower=Group("Airport control tower",world,new Vector3(19,0,31));
  Primitive("Tapered tower core",tower,PrimitiveType.Cylinder,new Vector3(0,5,0),new Vector3(2.5f,5,2.5f),concrete);
  Primitive("Tower observation ring",tower,PrimitiveType.Cylinder,new Vector3(0,10,0),new Vector3(5.6f,.2f,5.6f),steel);
  for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var pane=Box("Angled control room glazing "+i,tower,new Vector3(Mathf.Cos(a)*2.35f,11,Mathf.Sin(a)*2.35f),new Vector3(1.95f,1.9f,.09f),glass);pane.transform.localRotation=Quaternion.Euler(0,90-i*45,0);Beam("Control room corner",tower,new Vector3(Mathf.Cos(a)*2.55f,10,Mathf.Sin(a)*2.55f),new Vector3(Mathf.Cos(a)*2.7f,12.1f,Mathf.Sin(a)*2.7f),.12f,steel);}
  Primitive("Control tower roof",tower,PrimitiveType.Cylinder,new Vector3(0,12.15f,0),new Vector3(6,.15f,6),blue);Beam("Radio mast",tower,new Vector3(0,12.2f,0),new Vector3(0,15,0),.09f,steel);
 }
 static void Smoke(Transform world)
 {
  string texturePath=Dir+"Soft drifting smoke.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(texture==null){texture=new Texture2D(128,128,TextureFormat.RGBA32,false);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float radius=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f))/64;float noise=.55f+.45f*Mathf.PerlinNoise(x*.085f,y*.085f);texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.8f)*noise));}texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);}
  string materialPath=Dir+"Soft ash particles.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null){material=new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));AssetDatabase.CreateAsset(material,materialPath);}material.mainTexture=texture;
  for(int i=0;i<3;i++){var root=Group("Smoldering blast plume "+i,world,new Vector3(-16+i*14,.15f,14+i%2*7));Primitive("Burnt apron patch",root,PrimitiveType.Sphere,Vector3.zero,new Vector3(3,.025f,2),rubber);for(int coal=0;coal<5;coal++)Primitive("Dying ember",root,PrimitiveType.Sphere,new Vector3(Mathf.Cos(coal*2)*.7f,.05f,Mathf.Sin(coal*2)*.4f),new Vector3(.16f,.07f,.13f),ember);var emitter=Group("Soft rising ash",root,new Vector3(0,.2f,0));emitter.localRotation=Quaternion.Euler(-90,0,0);var ps=emitter.gameObject.AddComponent<ParticleSystem>();var main=ps.main;main.loop=true;main.prewarm=true;main.startLifetime=8;main.startSpeed=.65f;main.startSize=.9f;main.startColor=new Color(.16f,.18f,.2f,.38f);main.maxParticles=40;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=ps.emission;emission.rateOverTime=4;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=16;shape.radius=.4f;var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,4));var colors=ps.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.15f),new GradientAlphaKey(0,1)});colors.color=gradient;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;var light=root.gameObject.AddComponent<Light>();light.color=new Color(1,.35f,.12f);light.range=4;light.intensity=1.1f;}
 }
 static void Props(Transform world)
 {
  for(int i=0;i<3;i++){var cart=Group("Abandoned baggage cart "+i,world,new Vector3(6+i*3,.6f,12+i*1.4f));cart.localRotation=Quaternion.Euler(0,15+i*27,0);Box("Cart deck",cart,Vector3.zero,new Vector3(2.1f,.16f,1.2f),steel);for(int side=-1;side<=1;side+=2){Beam("Cart rail",cart,new Vector3(-1,.6f,side*.6f),new Vector3(1,.6f,side*.6f),.06f,steel);for(int end=-1;end<=1;end+=2){Beam("Cart rail post",cart,new Vector3(end,0,side*.6f),new Vector3(end,.6f,side*.6f),.06f,steel);var wheel=Primitive("Baggage cart tire",cart,PrimitiveType.Cylinder,new Vector3(end*.7f,-.38f,side*.6f),new Vector3(.4f,.12f,.4f),rubber);wheel.transform.localRotation=Quaternion.Euler(90,0,0);}}for(int bag=0;bag<3;bag++){var luggage=Box("Scuffed suitcase",cart,new Vector3(-.6f+bag*.6f,.45f,0),new Vector3(.5f,.75f,.7f),bag%2==0?blue:ivory);for(int side=-1;side<=1;side+=2)Box("Suitcase strap",cart,new Vector3(-.6f+bag*.6f+side*.15f,.45f,-.36f),new Vector3(.035f,.72f,.02f),steel);}}
  for(int i=0;i<4;i++){var light=new GameObject("Apron light pole "+i).transform;light.SetParent(world);light.position=new Vector3(-24+i*16,0,20);Beam("Apron floodlight mast",light,Vector3.zero,new Vector3(0,11,0),.12f,steel);Box("Floodlight crossbar",light,new Vector3(0,11,0),new Vector3(2,.12f,.12f),steel);for(int j=-1;j<=1;j++)Box("Floodlight housing",light,new Vector3(j*.7f,10.8f,-.1f),new Vector3(.5f,.3f,.35f),ivory);}
 }
}
#endif
