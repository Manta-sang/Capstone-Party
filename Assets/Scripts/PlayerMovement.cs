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
    private ClientNetworkAnimator netAnim; // ★ 네트워크 애니메이터 추가
    private bool isGrounded = true;
    private Quaternion targetRotation;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
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

            // ★ Trigger(점프)는 반드시 네트워크 애니메이터를 통해서 실행해야 동기화됩니다!
            if (netAnim != null) netAnim.SetTrigger("doJump");
            else anim.SetTrigger("doJump");

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