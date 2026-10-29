using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 톱날 피하기 미니게임 전용 죽음 처리.
/// 팀 방식대로 "생성된 캐릭터에 코드로 붙이는" 일반 컴포넌트라서 공유 캐릭터 프리팹을 수정하지 않는다.
/// (NetworkBehaviour가 아니라서 죽음 신호는 SD_GameManager가 모든 참가자에게 전달한다)
///
/// 죽으면: 조작 차단 -> 정면의 반대쪽으로 포물선을 그리며 날아감 -> 몇 초 뒤 오브젝트 제거(Host)
/// </summary>
[DisallowMultipleComponent]
public class SD_PlayerDeath : MonoBehaviour
{
    /// <summary>이 컴퓨터에 있는 모든 플레이어</summary>
    public static readonly List<SD_PlayerDeath> All = new List<SD_PlayerDeath>();

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    // SD_GameManager가 Configure()로 덮어쓴다
    private float flightDistance = 12f;
    private float arcHeight = 4f;
    private float flightDuration = 1.2f;
    private float removeDelay = 3f;
    private bool disableCollidersOnDeath = true;

    private NetworkObject networkObject;
    private Rigidbody rb;
    private Animator anim;
    private PlayerMovement movement;
    private PlayerInteraction interaction;

    public bool IsDead { get; private set; }

    /// <summary>죽은 순간 한 번 호출된다 (모든 컴퓨터에서)</summary>
    public event Action<SD_PlayerDeath> Died;

    /// <summary>이 캐릭터를 조종하는 참가자의 ID</summary>
    public ulong OwnerId => networkObject != null ? networkObject.OwnerClientId : 0UL;

    // 이 컴퓨터가 이 캐릭터의 위치를 정하는가 (내 캐릭터일 때)
    private bool ControlsThisPlayer => networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;

    private bool OnServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        movement = GetComponent<PlayerMovement>();
        interaction = GetComponent<PlayerInteraction>();

        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    public void Configure(float distance, float height, float duration, float removeAfter)
    {
        flightDistance = distance;
        arcHeight = height;
        flightDuration = duration;
        removeDelay = removeAfter;
    }

    /// <summary>이동/밀치기 조작을 켜고 끈다 (시작 전 대기 동안 잠그는 용도). 죽은 뒤에는 영향을 주지 않는다</summary>
    public void SetControlsEnabled(bool controlsEnabled)
    {
        if (IsDead) return;
        if (movement != null) movement.enabled = controlsEnabled;
        if (interaction != null) interaction.enabled = controlsEnabled;
    }

    /// <summary>죽음을 적용한다. 여러 번 불려도 한 번만 처리된다</summary>
    public void ApplyDeath()
    {
        if (IsDead) return;
        IsDead = true;

        if (movement != null) movement.enabled = false;
        if (interaction != null) interaction.enabled = false;
        if (anim != null && ControlsThisPlayer) anim.SetBool(IsMovingHash, false);

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            rb.interpolation = RigidbodyInterpolation.None;
        }

        if (disableCollidersOnDeath)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        // 날아가는 연출: 위치는 이 캐릭터를 조종하는 컴퓨터만 움직이고, 다른 컴퓨터에는 위치 동기화로 보인다
        if (ControlsThisPlayer)
        {
            Vector3 flyDirection = -transform.forward;
            flyDirection.y = 0f;
            flyDirection.Normalize();
            StartCoroutine(FlyRoutine(flyDirection));
        }

        // 몇 초 뒤 제거: Host만 한다 (제거하면 모든 참가자 화면에서 사라진다)
        bool networked = networkObject != null && networkObject.IsSpawned;
        if (!networked) StartCoroutine(RemoveRoutine());       // 단독 테스트: 비활성화
        else if (OnServer) StartCoroutine(RemoveRoutine());

        Died?.Invoke(this);
    }

    private IEnumerator RemoveRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(removeDelay, flightDuration + 0.1f));

        if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn(true);
        else gameObject.SetActive(false);
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

            Vector3 position = startPosition + flyDirection * (flightDistance * t);
            position.y = startPosition.y + 4f * arcHeight * t * (1f - t);

            transform.position = position;
            yield return null;
        }
    }
}
