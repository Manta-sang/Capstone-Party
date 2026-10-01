using UnityEngine;
using Unity.Netcode; // ★ 네트워크 패키지 추가

// ★ MonoBehaviour 대신 NetworkBehaviour를 상속받습니다.
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
    private bool isGrounded = true;
    private Quaternion targetRotation;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        targetRotation = transform.rotation;
    }

    void Update()
    {
        // ★ IsOwner: 서버가 인정한 '내 클라이언트 소유 캐릭터'일 때만 조종 가능
        if (!IsOwner) return;
        if (isKnockedBack) return;

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (movement.magnitude >= 0.1f)
        {
            targetRotation = Quaternion.LookRotation(movement);
            anim.SetBool("isMoving", true);

            rb.velocity = new Vector3(movement.x * moveSpeed, rb.velocity.y, movement.z * moveSpeed);
        }
        else
        {
            anim.SetBool("isMoving", false);
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            anim.SetTrigger("doJump");
            isGrounded = false;
        }
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