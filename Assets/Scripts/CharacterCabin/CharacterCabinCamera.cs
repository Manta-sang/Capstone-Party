using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CharacterCabinCamera : MonoBehaviour {
 public bool followPassenger=true;
 public float floorMinX=-11, floorMaxX=17, deadZone=3.5f, smoothTime=.35f;
 float velocity;Camera view;
 void Awake(){view=GetComponent<Camera>();}
 void LateUpdate(){
  var n=NetworkManager.Singleton;
  if(!followPassenger||n==null||!n.IsListening||n.LocalClient==null||n.LocalClient.PlayerObject==null)return;
  float x=n.LocalClient.PlayerObject.transform.position.x,current=transform.position.x,target=current;
  if(x<current-deadZone)target=x+deadZone;else if(x>current+deadZone)target=x-deadZone;
  float half=view.orthographicSize*view.aspect,min=floorMinX+half-.5f,max=floorMaxX-half+.5f;
  target=min>max?(floorMinX+floorMaxX)*.5f:Mathf.Clamp(target,min,max);
  var p=transform.position;p.x=Mathf.SmoothDamp(current,target,ref velocity,smoothTime);transform.position=p;
 }
}
