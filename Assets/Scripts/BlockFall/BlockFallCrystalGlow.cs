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
  void OnEnable(){crystal=GetComponent<Renderer>();properties=new MaterialPropertyBlock();haloProperties=new MaterialPropertyBlock();cameraReference=Camera.main;Apply();}
  void Update(){Apply();}
  void Apply(){
   if(crystal==null)return;
   float t=Application.isPlaying?Time.time:Time.realtimeSinceStartup;
   float pulse=Mathf.Pow(.5f+.5f*Mathf.Sin(t*(1.4f+seed*.009f)+seed),5);
   float glow=1.6f+3.2f*pulse;
   properties.SetColor("_EmissionColor",new Color(.12f,.5f,1)*glow);
   crystal.SetPropertyBlock(properties);
   if(source!=null)source.intensity=.65f+peakIntensity*pulse;
   if(halo!=null){
    haloProperties.SetColor("_Color",new Color(.08f,.45f,1,.28f+.18f*pulse));halo.SetPropertyBlock(haloProperties);
    if(cameraReference==null)cameraReference=Camera.main;
    if(cameraReference!=null)halo.transform.rotation=cameraReference.transform.rotation;
   }
  }
 }
}
