using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 톱날 피하기 미니게임 전용 플레이어 생존 관리.
/// 톱날에 한 번이라도 닿으면 죽고, 죽은 플레이어는 아무 동작도 할 수 없으며 정면의 반대(뒤쪽)로 포물선을 그리며 날아간다.
/// (방어막 등은 이후 단계에서 이 컴포넌트에 추가 예정)
///
/// 기존 PlayerMovement / PlayerInteraction은 수정하지 않고, 컴포넌트를 끄는 방식으로만 동작을 막는다.
/// 플레이어 오브젝트(SawDodgePlayerMovement가 붙은 오브젝트)에 함께 붙인다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SawDodgePlayerLife : MonoBehaviour
{
    [Header("Death")]
    [Tooltip("죽으면 콜라이더를 꺼서 톱날과 다른 플레이어가 통과하게 한다. 끄면 시체가 장애물로 남는다")]
    [SerializeField] private bool disableCollidersOnDeath = true;

    [Header("Death Flight")]
    [Tooltip("뒤쪽으로 날아가는 수평 거리")]
    [SerializeField] private float flightDistance = 12f;
    [Tooltip("포물선의 가장 높은 지점의 높이 (시작 높이 기준)")]
    [SerializeField] private float arcHeight = 4f;
    [Tooltip("날아가는 데 걸리는 시간(초)")]
    [SerializeField] private float flightDuration = 1.2f;

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    private Rigidbody rb;
    private Animator anim;
    private SawDodgePlayerMovement movement;
    private PlayerInteraction interaction;

    public bool IsDead { get; private set; }

    /// <summary>플레이어가 죽은 순간(날아가기 시작할 때) 한 번 호출된다 (승리 조건 처리 등을 연결할 때 사용)</summary>
    public event Action<SawDodgePlayerLife> Died;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        movement = GetComponent<SawDodgePlayerMovement>();
        interaction = GetComponent<PlayerInteraction>();
    }

    /// <summary>톱날에 맞았을 때 호출. 여러 번 호출돼도 한 번만 처리된다</summary>
    public void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // 입력, 이동, 점프, 다른 플레이어 밀치기 모두 차단
        if (movement != null) movement.enabled = false;
        if (interaction != null) interaction.enabled = false;

        // 달리던 모션이 남지 않게
        if (anim != null) anim.SetBool(IsMovingHash, false);

        // 물리 영향에서 제외 (속도를 먼저 지운 뒤 kinematic으로). 이후 이동은 아래 코드가 직접 처리한다
        rb.velocity = Vector3.zero; // Unity 6 이상이면 linearVelocity
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;

        if (disableCollidersOnDeath)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        // 날아갈 방향: 캐릭터 정면의 반대(뒤쪽). 맞은 방향과는 상관없다
        Vector3 flyDirection = -transform.forward;
        flyDirection.y = 0f;
        flyDirection.Normalize();

        StartCoroutine(FlyRoutine(flyDirection));
        Died?.Invoke(this);
    }

    private IEnumerator FlyRoutine(Vector3 flyDirection)
    {
        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(0.01f, flightDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 수평: 일정한 속도로 뒤쪽으로
            Vector3 position = startPosition + flyDirection * (flightDistance * t);

            // 수직: 포물선으로 떠올랐다가 시작 높이로 돌아옴 (t=0.5에서 가장 높음)
            position.y = startPosition.y + 4f * arcHeight * t * (1f - t);

            transform.position = position;
            yield return null;
        }
    }
}