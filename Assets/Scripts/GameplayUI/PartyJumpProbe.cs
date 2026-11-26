#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

[DefaultExecutionOrder(1000)]
public sealed class PartyJumpProbe : MonoBehaviour
{
    public static bool Done;
    public static string Error;
    PlayerMovement movement; Rigidbody body; bool pressing;
    Vector3 savedPosition;Quaternion savedRotation;
    CharacterCabinLobby cabin;FinalCelebrationPlayer celebration;
    GameObject fixture;
    void FixedUpdate(){if(pressing&&body!=null&&!body.isKinematic)body.velocity=new Vector3(5,body.velocity.y,0);}
    IEnumerator Start()
    {
        Done=false;Error="";System.IO.File.WriteAllText("Library/JumpProbe.progress","Started "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        var player=NetworkManager.Singleton.LocalClient.PlayerObject;
        movement=player.GetComponent<PlayerMovement>();body=player.GetComponent<Rigidbody>();
        celebration=player.GetComponent<FinalCelebrationPlayer>();if(celebration!=null)celebration.enabled=false;
        savedPosition=body.position;savedRotation=player.transform.rotation;
        cabin=FindObjectOfType<CharacterCabinLobby>();if(cabin!=null)cabin.enabled=false;
        fixture=new GameObject("Temporary untagged ground and high-friction wall");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(fixture.transform);floor.transform.position=new Vector3(100,29.5f,0);floor.transform.localScale=new Vector3(8,1,8);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(fixture.transform);wall.transform.position=new Vector3(101.2f,33,0);wall.transform.localScale=new Vector3(1,8,8);wall.GetComponent<Collider>().material=new PhysicMaterial("High friction test wall"){dynamicFriction=1,staticFriction=1};
        body.position=new Vector3(100,30.08f,0);body.velocity=Vector3.zero;Physics.SyncTransforms();pressing=true;
        yield return new WaitForSeconds(.45f);
        for(int jump=0;jump<3;jump++)
        {
            System.IO.File.AppendAllText("Library/JumpProbe.progress","\nJump "+jump+" grounded="+movement.IsGrounded+" y="+body.position.y);
            if(!movement.TryJump()){Error="Jump rejected on untagged floor at wall, cycle "+jump;break;}
            yield return new WaitForSeconds(.25f);
            if(body.position.y<30.4f){Error="Jump did not rise at wall";break;}
            if(movement.TryJump()){Error="Air jump permitted";break;}
            float until=Time.time+2.5f;
            while(Time.time<until&&(!movement.IsGrounded||body.position.y>30.2f))yield return new WaitForFixedUpdate();
            if(!movement.IsGrounded||body.position.y>30.2f){Error="Wall friction prevented landing / re-jump";break;}
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        }
        // Land on a raised step beside the wall, then jump into a low ceiling repeatedly.
        var step=GameObject.CreatePrimitive(PrimitiveType.Cube);step.name="Platform edge regression fixture";step.transform.SetParent(fixture.transform);step.transform.position=new Vector3(99.8f,30.3f,0);step.transform.localScale=new Vector3(1.1f,.6f,3);
        var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Low ceiling regression fixture";ceiling.transform.SetParent(fixture.transform);ceiling.transform.position=new Vector3(100,33.8f,0);ceiling.transform.localScale=new Vector3(5,.3f,5);
        body.position=new Vector3(99.8f,30.68f,0);body.velocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForSeconds(.5f);
        for(int jump=0;Error==""&&jump<3;jump++){
            float baseline=body.position.y;
            if(!movement.TryJump()){Error="Raised platform re-jump rejected";break;}
            float until=Time.time+2.2f;bool rose=false;
            do{yield return new WaitForFixedUpdate();rose|=body.position.y>baseline+.15f;if(body.position.y>baseline+1.65f){Error="Platform/ceiling contact launched player above normal jump envelope";break;}}while(Time.time<until&&(!rose||!movement.IsGrounded||body.velocity.y>.1f));
            if(Error==""&&(!rose||!movement.IsGrounded))Error="Platform/ceiling contact left player flying";
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        }
        if(Error==""){body.velocity=new Vector3(35,20,0);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();if(body.velocity.y>movement.NormalJumpSpeed+.2f||new Vector2(body.velocity.x,body.velocity.z).magnitude>movement.moveSpeed*1.5f)Error="Contact velocity spike was not bounded";}
        pressing=false;
        // A vertical surface in midair cannot count as ground.
        body.position=new Vector3(100.15f,35,0);body.velocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        if(Error==""&&movement.TryJump())Error="Vertical wall counted as ground";
        Destroy(fixture);body.position=savedPosition;body.velocity=Vector3.zero;player.transform.rotation=savedRotation;
        if(cabin!=null)cabin.enabled=true;if(celebration!=null)celebration.enabled=true;Done=true;System.IO.File.AppendAllText("Library/JumpProbe.progress","\nFinished: "+Error);
        Destroy(gameObject);
    }
}
#endif
