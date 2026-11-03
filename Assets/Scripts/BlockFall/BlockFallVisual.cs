using UnityEngine;
namespace BlockFall {
 public sealed class BlockFallVisual : MonoBehaviour {
  public GameObject warning;
  public GameObject model;
  public GameObject[] variants;
  [Min(1f)] public float spawnHeight = 27f;
  Renderer[] warningRenderers;
  Color[] warningColors;
  MaterialPropertyBlock properties;
  int selected=-1;
  Renderer[][] variantRenderers;
  Bounds damageBounds;
  bool hasBounds;
  void OnEnable(){hasBounds=false;}
  public bool TryGetDamageBounds(out Bounds bounds){bounds=damageBounds;return hasBounds && isActiveAndEnabled && model.activeInHierarchy;}
  public void Animate(float remaining,float fallTime,float warningTime,int variantSeed) {
   if(properties==null) {
    properties=new MaterialPropertyBlock(); warningRenderers=warning.GetComponentsInChildren<Renderer>(true);
    warningColors=new Color[warningRenderers.Length];
    for(int i=0;i<warningColors.Length;i++)warningColors[i]=warningRenderers[i].sharedMaterial.color;
    variantRenderers=new Renderer[variants.Length][];
    for(int i=0;i<variants.Length;i++)variantRenderers[i]=variants[i].GetComponentsInChildren<Renderer>(true);
   }
   warning.SetActive(remaining>0); model.SetActive(remaining<=fallTime+.18f);
   if(variants!=null && variants.Length>0) {
    int pick=(variantSeed & int.MaxValue)%variants.Length;
    if(selected!=pick) {for(int i=0;i<variants.Length;i++) variants[i].SetActive(i==pick);selected=pick;}
   }
   float fallProgress=1-Mathf.Clamp01(remaining/Mathf.Max(.05f,fallTime));
   float seed=(variantSeed & 1023)*.071f;
   float settle=1-Mathf.SmoothStep(.35f,.5f,fallProgress);
   float height=Mathf.Lerp(spawnHeight,.8f,fallProgress*fallProgress);
   float sway=Mathf.Sin(fallProgress*Mathf.PI*2+seed)*.12f*settle;
   model.transform.localPosition=new Vector3(sway,height/2f,Mathf.Cos(fallProgress*5+seed)*.08f*settle);
   model.transform.localRotation=Quaternion.Euler(
    Mathf.Sin(fallProgress*7+seed)*18*settle,
    (Mathf.Sin(seed)*22+fallProgress*75*(Mathf.Sin(seed)>.0f?1:-1))*settle,
    Mathf.Cos(fallProgress*6+seed)*14*settle);
   if(remaining<0){
    float age=-remaining*2;
    float bounce=Mathf.Abs(Mathf.Sin(age*16))*Mathf.Exp(-age*5)*.55f;
    float wobble=Mathf.Sin(age*34)*Mathf.Exp(-age*7)*10;
    if(age>=.65f){bounce=0;wobble=0;}
    model.transform.localPosition=new Vector3(0,(.8f+bounce)/2f,0);
    model.transform.localRotation=Quaternion.Euler(wobble,0,wobble*.65f);
   }
   hasBounds=remaining<=0 && model.activeSelf && selected>=0;
   if(hasBounds){
    var renderers=variantRenderers[selected];
    Bounds current=renderers[0].bounds;
    for(int i=1;i<renderers.Length;i++)current.Encapsulate(renderers[i].bounds);
    // A landing wobble must never make a book dangerous outside its warning cell.
    Vector3 low=current.min,high=current.max;
    low.x=Mathf.Max(low.x,transform.position.x-transform.lossyScale.x*.5f);
    low.z=Mathf.Max(low.z,transform.position.z-transform.lossyScale.z*.5f);
    high.x=Mathf.Min(high.x,transform.position.x+transform.lossyScale.x*.5f);
    high.z=Mathf.Min(high.z,transform.position.z+transform.lossyScale.z*.5f);
    current.SetMinMax(low,high);
    damageBounds=current;
    // Only the landed book is dangerous; never retain the airborne wobble envelope.
   }
   float progress=1-Mathf.Clamp01(remaining/Mathf.Max(.1f,warningTime+fallTime));
   float phase=2*Mathf.PI*(progress+5*progress*progress);
   float pulse=.5f+.5f*Mathf.Sin(phase);
   float alpha=Mathf.Lerp(.12f,1,progress)*Mathf.Lerp(1,.22f+.78f*pulse,Mathf.SmoothStep(.2f,.8f,progress));
   for(int i=0;i<warningRenderers.Length;i++){
    Color c=warningColors[i];c.a=alpha;properties.SetColor("_Color",c);warningRenderers[i].SetPropertyBlock(properties);
   }
  }
 }
}
