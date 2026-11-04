using UnityEngine;
public sealed class WinnerCrown : MonoBehaviour {
 public float rotationSpeed=58,hoverHeight=.09f,flashSpeed=3;
 public Renderer[] jewels;public Light glow;
 Vector3 origin;MaterialPropertyBlock properties;
 void Start(){origin=transform.localPosition;properties=new MaterialPropertyBlock();}
 void Update(){transform.localRotation=Quaternion.Euler(0,Time.unscaledTime*rotationSpeed,0);transform.localPosition=origin+Vector3.up*(Mathf.Sin(Time.unscaledTime*2)*hoverHeight);float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*flashSpeed);if(glow!=null)glow.intensity=1.4f+pulse*2;foreach(var jewel in jewels){jewel.GetPropertyBlock(properties);properties.SetColor("_EmissionColor",new Color(1,.72f,.15f)*(1+pulse*3));jewel.SetPropertyBlock(properties);}}
}
