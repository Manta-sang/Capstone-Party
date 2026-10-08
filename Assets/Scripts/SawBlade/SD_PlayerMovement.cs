using UnityEngine;

/// <summary>
/// 톱날 피하기 미니게임 전용 플레이어 이동 (기본 이동 + 점프)
/// 기존 PlayerMovement를 베이스로 만들었고, 기존 스크립트는 수정하지 않았다.
///
/// 사용법
/// - 이 미니게임의 플레이어에는 PlayerMovement 대신 이 스크립트를 붙인다 (둘을 같이 붙이지 말 것).
/// - PlayerInteraction(플레이어끼리 밀기)은 같이 붙여도 된다.
/// - 플레이어에는 Rigidbody(Use Gravity 켬)와 Collider가 필요하다.
/// - 바닥 오브젝트에는 "Ground" 태그가 필요하다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SD_PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f; // 몸을 돌리는 속도

    [Header("Jump")]
    public float jumpForce = 5f;
    [SerializeField] private string groundTag = "Ground";

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int DoJumpHash = Animator.StringToHash("doJump");

    private Rigidbody rb;
    private Animator anim;

    private Vector3 moveInput;            // Update에서 읽은 입력 (월드 기준)
    private Quaternion targetRotation;    // 캐릭터가 바라볼 목표 방향
    private bool jumpRequested;
    private bool isGrounded = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        rb.freezeRotation = true;                           // 충돌로 캐릭터가 쓰러지지 않게
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        targetRotation = transform.rotation;
    }

    private void Update()
    {
        // 입력은 Update에서 읽고, 실제 이동은 FixedUpdate에서 처리한다
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        if (isMoving) targetRotation = Quaternion.LookRotation(moveInput);
        if (anim != null) anim.SetBool(IsMovingHash, isMoving);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpRequested = true;
            isGrounded = false;
            if (anim != null) anim.SetTrigger(DoJumpHash);
        }
    }

    private void FixedUpdate()
    {
        // 월드 기준 이동. 속도(velocity)를 건드리지 않아서 PlayerInteraction의 밀치기 힘이 지워지지 않는다
        if (moveInput != Vector3.zero)
            rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);

        // 이동 중이든 멈췄든 항상 부드럽게 목표 방향으로 회전
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));

        if (jumpRequested)
        {
            jumpRequested = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(groundTag)) return;

        // 위쪽을 향한 면(바닥)에 닿았을 때만 착지로 인정 (Ground 태그가 붙은 벽에 닿아도 착지로 보지 않게)
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
    }
}
