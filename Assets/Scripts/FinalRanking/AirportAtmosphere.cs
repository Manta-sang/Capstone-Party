using UnityEngine;
// All atmosphere is local to the ceremony: no global RenderSettings or physics mutations.
public sealed class AirportAtmosphere : MonoBehaviour
{
    public Transform[] clouds;
    public float driftSpeed=.45f;
    public Light[] embers;
    Vector3[] initial;
    void Awake(){initial=new Vector3[clouds.Length];for(int i=0;i<clouds.Length;i++)initial[i]=clouds[i].localPosition;}
    void Update(){for(int i=0;i<clouds.Length;i++){if(clouds[i]==null)continue;var p=initial[i];p.x+=Mathf.Repeat(Time.time*driftSpeed+i*3,110)-i*3;p.x=Mathf.Repeat(p.x+55,110)-55;p.y+=Mathf.Sin(Time.time*.08f+i)*.25f;clouds[i].localPosition=p;}for(int i=0;i<embers.Length;i++)if(embers[i]!=null)embers[i].intensity=.7f+Mathf.PerlinNoise(i*1.7f,Time.time*2)*1.1f;}
}
