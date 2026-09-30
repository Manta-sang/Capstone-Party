using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f; // 몸을 돌리는 속도
    public float jumpForce = 5f;

    private Animator anim;
    private Rigidbody rb;
    private bool isGrounded = true;

    // 캐릭터가 바라볼 목표 방향을 기억할 변수
    private Quaternion targetRotation;

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        // 게임 시작 시 처음 바라보는 방향 저장
        targetRotation = transform.rotation;
    }

    void Update()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        // 키보드를 눌렀을 때만 이동 및 목표 방향 업데이트
        if (movement.magnitude >= 0.1f)
        {
            // 월드 기준 이동
            transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);

            // 바라볼 목표 방향 계산
            targetRotation = Quaternion.LookRotation(movement);

            anim.SetBool("isMoving", true);
        }
        else
        {
            anim.SetBool("isMoving", false);
        }

        // if문 바깥에 배치! -> 이동 중이든 멈추든 항상 부드럽게 목표 방향으로 몸을 돌림
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // 점프 처리
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
}