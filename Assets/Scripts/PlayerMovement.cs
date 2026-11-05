using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("기본 설정")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float jumpForce = 5f;

    [Header("상태 확인")]
    public bool isKnockedBack = false;

    private Animator anim;
    private Rigidbody rb;
    private Collider movementCollider; // 미끄러운 구역은 캐릭터 발 위치에서만 판정합니다.
    private ClientNetworkAnimator netAnim; // ★ 네트워크 애니메이터 추가
    private bool isGrounded;
    private float jumpBlockedUntil;
    private int groundedSteps;
    private bool jumpInFlight;
    private float jumpTakeoffY;
    public float NormalJumpSpeed => Mathf.Max(.1f,jumpForce/Mathf.Max(.1f,rb!=null?rb.mass:1));
    private PhysicMaterial antiStickMaterial, originalMaterial;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    public bool IsGrounded => isGrounded;
    private Quaternion targetRotation;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        movementCollider = GetComponent<Collider>();
        netAnim = GetComponent<ClientNetworkAnimator>(); // ★ 컴포넌트 가져오기
        targetRotation = transform.rotation;
        rb.maxDepenetrationVelocity=2f;
        if(!rb.isKinematic)rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        if(movementCollider!=null){originalMaterial=movementCollider.sharedMaterial;antiStickMaterial=new PhysicMaterial("Player wall slide"){dynamicFriction=0,staticFriction=0,frictionCombine=PhysicMaterialCombine.Minimum,bounciness=0,bounceCombine=PhysicMaterialCombine.Minimum};movementCollider.sharedMaterial=antiStickMaterial;}
    }

    void Update()
    {
        if (!IsOwner) return;
        if (GameStartPresentation.BlocksGameplayInput) return;
        if (isKnockedBack) return;

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (movement.magnitude >= 0.1f)
        {
            targetRotation = Quaternion.LookRotation(movement);
            anim.SetBool("isMoving", true); // Bool은 기본 anim으로 해도 자동 동기화됨

            ApplyHorizontalMovement(movement * moveSpeed);
        }
        else
        {
            anim.SetBool("isMoving", false);
            ApplyHorizontalMovement(Vector3.zero);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space)) TryJump();
    }
    public bool TryJump()
    {
        if(!IsOwner||!enabled||rb==null||rb.isKinematic||GameStartPresentation.BlocksGameplayInput||isKnockedBack||jumpInFlight||groundedSteps<2||!CheckGround())return false;
        {
            // A jump has a fixed takeoff speed; contact impulses cannot be added to it.
            rb.velocity=new Vector3(rb.velocity.x,NormalJumpSpeed,rb.velocity.z);
            jumpInFlight=true;jumpTakeoffY=rb.position.y;groundedSteps=0;

            // ★ Trigger(점프)는 반드시 네트워크 애니메이터를 통해서 실행해야 동기화됩니다!
            if (netAnim != null) netAnim.SetTrigger("doJump");
            else anim.SetTrigger("doJump");

            isGrounded = false;
            jumpBlockedUntil=Time.time+.16f;
            return true;
        }
    }

    // 일반 바닥은 기존 즉시 이동을 유지하고 구역 안에서만 수평 속도를 천천히 변경합니다.
    // 수직 속도와 점프·넉백 분기는 기존 코드가 계속 담당합니다.
    private void ApplyHorizontalMovement(Vector3 desired)
    {
        Vector3 feet = rb.position;
        if (movementCollider != null)
        {
            Bounds bounds = movementCollider.bounds;
            feet = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
        }
        if (CrosswalkRun.SlipperyZone.TryGetAcceleration(feet, out float acceleration))
        {
            Vector3 current = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            desired = Vector3.MoveTowards(current, desired, acceleration * Time.deltaTime);
        }
        // Stop driving into a wall or platform face before the solver can launch us.
        for(int pass=0;pass<2&&desired.sqrMagnitude>.001f;pass++){
            if(!rb.SweepTest(desired.normalized,out var obstruction,desired.magnitude*Time.fixedDeltaTime+.04f,QueryTriggerInteraction.Ignore))break;
            if(obstruction.collider.GetComponentInParent<PlayerMovement>()!=null||obstruction.normal.y>=.60f)break;
            Vector3 normal=new Vector3(obstruction.normal.x,0,obstruction.normal.z).normalized;
            float inward=Vector3.Dot(desired,normal);if(inward>=0)break;desired-=normal*inward;
        }
        rb.velocity = new Vector3(desired.x, rb.velocity.y, desired.z);
    }

    void FixedUpdate()
    {
        if(!IsOwner||rb==null||movementCollider==null||rb.isKinematic)return;
        isGrounded=CheckGround();
        groundedSteps=isGrounded?groundedSteps+1:0;
        if(groundedSteps>=2)jumpInFlight=false;
        if(!isKnockedBack){
            Vector3 velocity=rb.velocity;
            float maximumRise=NormalJumpSpeed;
            if(jumpInFlight){float rise=Mathf.Max(0,rb.position.y-jumpTakeoffY);maximumRise=Mathf.Sqrt(Mathf.Max(0,NormalJumpSpeed*NormalJumpSpeed-2*Mathf.Abs(Physics.gravity.y)*rise));}
            velocity.y=Mathf.Min(velocity.y,maximumRise);
            Vector2 horizontal=Vector2.ClampMagnitude(new Vector2(velocity.x,velocity.z),moveSpeed*1.35f);
            rb.velocity=new Vector3(horizontal.x,velocity.y,horizontal.y);
        }
    }
    private bool CheckGround()
    {
        if(Time.time<jumpBlockedUntil||rb.velocity.y>.3f)return false;
        Bounds bounds=movementCollider.bounds;
        float radius=Mathf.Min(bounds.extents.x,bounds.extents.z)*.78f;
        Vector3 origin=new Vector3(bounds.center.x,bounds.min.y+radius+.06f,bounds.center.z);
        int count=Physics.SphereCastNonAlloc(origin,radius,Vector3.down,groundHits,.15f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++){
            var hit=groundHits[i];
            if(hit.collider==null||hit.collider.transform.IsChildOf(transform)||hit.normal.y<.60f)continue;
            // Another character and a vertical wall are never a landing surface.
            if(hit.collider.GetComponentInParent<PlayerMovement>()!=null)continue;
            return true;
        }
        return false;
    }
    void OnCollisionStay(Collision collision)
    {
        if(rb==null||movementCollider==null||Time.time<jumpBlockedUntil||rb.velocity.y>.3f)return;
        if(collision.collider.GetComponentInParent<PlayerMovement>()!=null)return;
        float feet=movementCollider.bounds.min.y;
        for(int i=0;i<collision.contactCount;i++){var contact=collision.GetContact(i);if(contact.normal.y>=.60f&&contact.point.y<=feet+.22f){isGrounded=true;return;}}
    }
    public override void OnDestroy()
    {
        if(movementCollider!=null&&movementCollider.sharedMaterial==antiStickMaterial)movementCollider.sharedMaterial=originalMaterial;
        if(antiStickMaterial!=null)Destroy(antiStickMaterial);
        base.OnDestroy();
    }

    public void ResetKnockback()
    {
        isKnockedBack = false;
    }
}
