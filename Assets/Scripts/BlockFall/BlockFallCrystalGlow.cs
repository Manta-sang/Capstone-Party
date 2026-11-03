using UnityEngine;
namespace BlockFall {
 [ExecuteAlways]
 public sealed class BlockFallCrystalGlow : MonoBehaviour {
  public float seed;
  public Light source;
  public Renderer halo;
  public float peakIntensity=1.3f;
  Renderer crystal; MaterialPropertyBlock properties,haloProperties;
  Camera cameraReference;
  void OnEnable(){
   crystal=GetComponent<Renderer>();properties=new MaterialPropertyBlock();haloProperties=new MaterialPropertyBlock();cameraReference=Camera.main;
   if(source!=null){
    // Vertex lighting exposes the large floor's triangles as the light pulses.
    source.renderMode=LightRenderMode.ForcePixel;
    source.range=7.6f;
   }
   if(halo!=null){
    var parentScale=halo.transform.parent!=null?halo.transform.parent.lossyScale:Vector3.one;
    halo.transform.localScale=new Vector3(2.6f/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),2.6f/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),halo.transform.localScale.z);
   }
   Apply();
  }
  void Update(){Apply();}
  void Apply(){
   if(crystal==null)return;
   float t=Application.isPlaying?Time.time:Time.realtimeSinceStartup;
   float pulse=Mathf.Pow(.5f+.5f*Mathf.Sin(t*(1.1f+seed*.007f)+seed),2);
   float glow=2.88f+4.32f*pulse;
   properties.SetColor("_EmissionColor",new Color(.12f,.5f,1)*glow);
   crystal.SetPropertyBlock(properties);
   if(source!=null)source.intensity=.42f+peakIntensity*1.02f*pulse;
   if(halo!=null){
    haloProperties.SetColor("_Color",new Color(.12f,.48f,1,.276f+.24f*pulse));halo.SetPropertyBlock(haloProperties);
    if(cameraReference==null)cameraReference=Camera.main;
    if(cameraReference!=null)halo.transform.rotation=cameraReference.transform.rotation;
   }
  }
 }
}
