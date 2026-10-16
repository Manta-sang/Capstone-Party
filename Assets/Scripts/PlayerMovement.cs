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
    private bool isGrounded = true;
    private Quaternion targetRotation;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        movementCollider = GetComponent<Collider>();
        netAnim = GetComponent<ClientNetworkAnimator>(); // ★ 컴포넌트 가져오기
        targetRotation = transform.rotation;
    }

    void Update()
    {
        if (!IsOwner) return;
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

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // ★ Trigger(점프)는 반드시 네트워크 애니메이터를 통해서 실행해야 동기화됩니다!
            if (netAnim != null) netAnim.SetTrigger("doJump");
            else anim.SetTrigger("doJump");

            isGrounded = false;
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
        rb.velocity = new Vector3(desired.x, rb.velocity.y, desired.z);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    public void ResetKnockback()
    {
        isKnockedBack = false;
    }
}