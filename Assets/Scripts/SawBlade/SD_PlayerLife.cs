using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 톱날 피하기 미니게임 전용 플레이어 생존 관리.
/// 톱날에 닿으면 죽고(Host만 판정), 뒤쪽으로 날아간 뒤 몇 초 뒤에 오브젝트가 제거된다.
/// 제거된 플레이어는 라운드를 다시 시작할 때 Host(SD_GameManager)가 새로 생성한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SD_PlayerLife : NetworkBehaviour
{
    [Header("Death Flight")]
    [SerializeField] private float flightDistance = 12f;
    [SerializeField] private float arcHeight = 4f;
    [SerializeField] private float flightDuration = 1.2f;

    [Header("Death Options")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [Tooltip("죽은 뒤 이 시간(초)이 지나면 플레이어 오브젝트가 제거된다. 날아가는 시간보다 길게 두세요")]
    [SerializeField] private float removeDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true; // 원인 확인용 로그 (해결되면 끄세요)

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    private Rigidbody rb;
    private Animator anim;
    private SD_PlayerMovement movement;
    private PlayerInteraction interaction;

    // 죽음 상태를 모든 참가자에게 공유한다 (값을 바꾸는 건 Host만)
    private readonly NetworkVariable<bool> isDeadNet = new NetworkVariable<bool>(false);

    /// <summary>이 컴퓨터에 있는 모든 플레이어</summary>
    public static readonly List<SD_PlayerLife> All = new List<SD_PlayerLife>();

    public bool IsDead { get; private set; } = false;

    /// <summary>플레이어가 죽은 순간 한 번 호출된다. 멀티에서는 모든 컴퓨터에서 호출된다</summary>
    public event Action<SD_PlayerLife> Died;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Coroutine flightRoutine;

    private void Log(string message)
    {
        if (!debugLog) return;
        string owner = IsSpawned ? OwnerClientId.ToString() : "-";
        string role = (IsServer ? "서버" : "클라") + (IsOwner ? "·내캐릭터" : "");
        Debug.Log($"[Life 오너={owner} {role} t={Time.time:F2}] {message}", this);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        All.Add(this);
        anim = GetComponentInChildren<Animator>();
        movement = GetComponent<SD_PlayerMovement>();
        interaction = GetComponent<PlayerInteraction>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        IsDead = false;
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        isDeadNet.OnValueChanged += OnDeadChanged;

        // 이미 죽은 상태에서 접속한 경우: 죽은 상태만 반영한다 (날아가는 연출은 재생하지 않음)
        if (isDeadNet.Value) ApplyDeath(false);
    }

    public override void OnNetworkDespawn()
    {
        isDeadNet.OnValueChanged -= OnDeadChanged;
    }

    /// <summary>재시작할 때 돌아올 위치를 지정한다</summary>
    public void SetSpawnPoint(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation;
    }

    /// <summary>
    /// 되살리고 시작 위치로 되돌린다. 멀티에서는 Host만 호출할 수 있다.
    /// (이미 제거된 플레이어는 여기서 되살릴 수 없고, SD_GameManager가 새로 생성한다)
    /// </summary>
    public void ResetLife()
    {
        Log($"ResetLife 호출 (지금 죽음={IsDead})");

        if (IsSpawned)
        {
            if (!IsServer) return;

            isDeadNet.Value = false;
            ResetClientRpc(); // 값이 안 바뀐 경우(살아 있던 플레이어)에도 모든 컴퓨터에서 되돌린다
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true); // 단독 테스트: 제거(비활성화)된 플레이어를 다시 켠다
        }

        ApplyRevive(); // Host 자신의 사본도 확실히 되돌린다
    }

    /// <summary>톱날에 맞았을 때 호출. 여러 번 호출돼도 한 번만 처리된다. 멀티에서는 Host만 죽음을 확정한다</summary>
    public void Die()
    {
        Log($"Die() 호출 (이미 죽음={IsDead})");
        if (IsDead) return;

        if (IsSpawned)
        {
            if (!IsServer) return; // 죽음 판정은 Host만
            isDeadNet.Value = true; // 모든 컴퓨터에서 OnDeadChanged가 호출된다
        }

        ApplyDeath(true); // 이미 처리됐다면 아무 일도 일어나지 않는다
    }

    [ClientRpc]
    private void ResetClientRpc()
    {
        Log("ResetClientRpc 수신");
        ApplyRevive();
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        Log($"죽음 상태 변경 {previous} -> {current}");

        if (current) ApplyDeath(true);
        else ApplyRevive();
    }

    // 이 컴퓨터가 이 캐릭터의 위치를 정하는가 (단독 테스트이거나 내 캐릭터일 때)
    private bool ControlsThisPlayer => !IsSpawned || IsOwner;

    private void ApplyDeath(bool playFlight)
    {
        Log($"ApplyDeath(연출={playFlight}) 이미 죽음={IsDead}");
        if (IsDead) return;
        IsDead = true;

        if (movement != null) movement.enabled = false;
        if (interaction != null) interaction.enabled = false;
        if (anim != null) anim.SetBool(IsMovingHash, false);

        // 다른 사람의 캐릭터 사본은 이미 kinematic이라서 건드리지 않는다
        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        rb.interpolation = RigidbodyInterpolation.None;

        if (disableCollidersOnDeath)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        // 날아가는 연출: 위치는 이 캐릭터를 조종하는 컴퓨터만 움직이고, 다른 컴퓨터에는 NetworkTransform으로 공유된다
        if (playFlight && ControlsThisPlayer)
        {
            Vector3 flyDirection = -transform.forward;
            flyDirection.y = 0f;
            flyDirection.Normalize();

            if (flightRoutine != null) StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(FlyRoutine(flyDirection));
        }

        // 몇 초 뒤 이 오브젝트를 제거한다. 제거는 Host(서버)만 한다 (제거하면 모든 참가자 화면에서 사라진다)
        if (playFlight && (!IsSpawned || IsServer))
        {
            StartCoroutine(RemoveRoutine());
        }

        Died?.Invoke(this);
    }

    private void ApplyRevive()
    {
        Log("ApplyRevive 실행");

        // 날아가기와 제거 예약을 모두 취소한다
        StopAllCoroutines();
        flightRoutine = null;

        IsDead = false;

        // 물리 상태를 먼저 되돌린다 (kinematic 상태에서 속도를 대입하면 경고가 나므로 순서가 중요)
        // 다른 사람의 캐릭터 사본은 NetworkRigidbody가 kinematic으로 관리하므로 그대로 둔다
        rb.isKinematic = IsSpawned && !IsOwner;
        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }

        if (movement != null) movement.enabled = true;
        if (interaction != null) interaction.enabled = true;
        if (anim != null) anim.SetBool(IsMovingHash, false);

        // 위치 복구는 마지막에 한다 (여기서 문제가 생겨도 위의 복구는 이미 끝난 상태)
        if (ControlsThisPlayer) MoveToStart();
    }

    private void MoveToStart()
    {
        Log($"MoveToStart -> {initialPosition}");

        NetworkTransform networkTransform = IsSpawned ? GetComponent<NetworkTransform>() : null;
        if (networkTransform != null)
        {
            try
            {
                networkTransform.Teleport(initialPosition, initialRotation, transform.localScale);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SD_PlayerLife] Teleport 실패: {e.Message}", this);
            }
        }

        // Teleport가 없거나 실패해도 위치가 확실히 돌아가도록 직접 지정한다
        transform.position = initialPosition;
        transform.rotation = initialRotation;
    }

    // 죽은 뒤 일정 시간이 지나면 플레이어 오브젝트를 제거한다
    private IEnumerator RemoveRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(removeDelay, flightDuration + 0.1f));

        Log("오브젝트 제거");
        if (IsSpawned) NetworkObject.Despawn(true); // 모든 참가자에게서 삭제된다
        else gameObject.SetActive(false);           // 단독 테스트: 비활성화 (ResetLife가 다시 켠다)
    }

    private IEnumerator FlyRoutine(Vector3 flyDirection)
    {
        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(0.01f, flightDuration);
        float elapsed = 0f;
        Log("날아가기 시작");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = startPosition + flyDirection * (flightDistance * t);
            position.y = startPosition.y + 4f * arcHeight * t * (1f - t);

            transform.position = position;
            yield return null;
        }

        Log("날아가기 끝");
    }
}