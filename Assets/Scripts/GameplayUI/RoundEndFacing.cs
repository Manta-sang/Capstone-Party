using Unity.Netcode;
using UnityEngine;
// Applies on the owner so the existing owner-authoritative transform replicates the final pose.
public sealed class RoundEndFacing : MonoBehaviour
{
    public static void Face(NetworkObject player)
    {
        if(player==null||!player.IsOwner)return;
        if(player.GetComponent<RoundEndFacing>()==null)player.gameObject.AddComponent<RoundEndFacing>();
    }
    void LateUpdate()
    {
        var cam=Camera.main;if(cam==null)return;
        Vector3 direction=-cam.transform.forward;direction.y=0;
        if(direction.sqrMagnitude<.001f){direction=-cam.transform.up;direction.y=0;}
        if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(direction.normalized);
        var body=GetComponent<Rigidbody>();if(body!=null&&!body.isKinematic){body.velocity=new Vector3(0,body.velocity.y,0);body.angularVelocity=Vector3.zero;}
        var animator=GetComponentInChildren<Animator>();if(animator!=null)animator.SetBool("isMoving",false);
    }
}
