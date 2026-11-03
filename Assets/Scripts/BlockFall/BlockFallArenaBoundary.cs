using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
namespace BlockFall {
 [DefaultExecutionOrder(10000)]
 public sealed class BlockFallArenaBoundary : MonoBehaviour {
  public Vector2 minimum,maximum;
  public float floorHeight;
  readonly Dictionary<ulong,Collider[]> colliders=new Dictionary<ulong,Collider[]>();
  readonly Dictionary<ulong,float> lastCorrection=new Dictionary<ulong,float>();
  BlockFallGame game;
  void Awake(){game=GetComponent<BlockFallGame>();}
  public static Vector3 ClampPosition(Vector3 position,Bounds body,Vector2 min,Vector2 max){
   Vector3 offset=body.center-position;
   position.x=Mathf.Clamp(position.x,min.x+body.extents.x-offset.x+.015f,max.x-body.extents.x-offset.x-.015f);
   position.z=Mathf.Clamp(position.z,min.y+body.extents.z-offset.z+.015f,max.y-body.extents.z-offset.z-.015f);
   return position;
  }
  void FixedUpdate(){Enforce();}
  void LateUpdate(){Enforce();}
  void Enforce(){
   var n=NetworkManager.Singleton;if(n==null || !n.IsListening)return;
   foreach(var entry in n.SpawnManager.SpawnedObjects){
    var obj=entry.Value;
    if(!obj.IsPlayerObject || obj.gameObject.scene!=gameObject.scene || (!obj.IsOwner && !n.IsServer))continue;
    if(!colliders.TryGetValue(obj.NetworkObjectId,out var list)){
     list=obj.GetComponentsInChildren<Collider>();colliders[obj.NetworkObjectId]=list;
     var body=obj.GetComponent<Rigidbody>();
     if(body!=null && !body.isKinematic)body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
    }
    bool found=false;Bounds bounds=default;
    foreach(var c in list)if(c!=null && c.enabled && !c.isTrigger){if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);}
    if(!found)continue; // Eliminated players have their colliders disabled.
    Vector3 current=obj.transform.position;
    Vector3 target=ClampPosition(current,bounds,minimum,maximum);
    if(bounds.min.y<floorHeight-.2f)target.y+=floorHeight-bounds.min.y+.02f;
    bool moved=(target-current).sqrMagnitude>.000001f;
    if(moved){
     ApplyPosition(obj,target);
     if(n.IsServer && !obj.IsOwner && game!=null && game.IsSpawned &&
       (!lastCorrection.TryGetValue(obj.OwnerClientId,out float time) || Time.unscaledTime-time>.1f)){
      lastCorrection[obj.OwnerClientId]=Time.unscaledTime;
      game.CorrectBoundaryClientRpc(obj.OwnerClientId,target);
     }
    }
    var rb=obj.GetComponent<Rigidbody>();
    if(rb!=null && !rb.isKinematic){
     Vector3 v=rb.velocity;
     if(bounds.min.x<=minimum.x+.035f && v.x<0 || bounds.max.x>=maximum.x-.035f && v.x>0)v.x=0;
     if(bounds.min.z<=minimum.y+.035f && v.z<0 || bounds.max.z>=maximum.y-.035f && v.z>0)v.z=0;
     rb.velocity=v;
    }
   }
  }
  public static void ApplyPosition(NetworkObject obj,Vector3 position){
   var body=obj.GetComponent<Rigidbody>();
   if(body!=null){body.position=position;if(!body.isKinematic){Vector3 v=body.velocity;v.x=0;v.z=0;body.velocity=v;}}
   obj.transform.position=position;
  }
 }
}
