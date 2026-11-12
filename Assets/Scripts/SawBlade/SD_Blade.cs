using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 벽에 닿으면 반사각으로 튕기는 톱날 (탑다운 3D, XZ 평면 이동)
/// 상태: Entering(벽을 뚫고 진입) -> Active(벽 반사) -> Leaving(벽을 뚫고 퇴장 후 파괴)
/// 정해진 횟수만큼 튕기면, 또는 시간 제한이 지나면 벽을 뚫고 나가 경기장 밖에서 사라진다.
/// 필요: Rigidbody + Collider
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SD_Blade : NetworkBehaviour
{
    private enum State { Entering, Active, Leaving }

    // 톱날끼리의 충돌을 관리하기 위한 전체 목록
    private static readonly List<SD_Blade> allBlades = new List<SD_Blade>();

    private float speed = 6f; // 스포너가 Launch()로 덮어씀 (스포너 없이 단독 테스트할 때만 쓰는 기본값)
    [SerializeField] private float enterMargin = 0.2f;   // 완전히 들어온 뒤 여유 거리
    [SerializeField] private float maxEnterTime = 10f;   // 이 시간 안에 못 들어오면 제거 (안전장치)
    [SerializeField] private float maxLeaveTime = 10f;   // 이 시간 안에 못 나가면 제거 (안전장치)

    [Header("소리 (등장 + 회전 공용 반복 소리)")]
    [Tooltip("톱날이 생성되는 순간부터 없어질 때까지 반복 재생할 소리가 들어 있는 AudioSource. 비워 두면 이 오브젝트의 AudioSource를 쓴다. Play On Awake는 꺼 둘 것")]
    [SerializeField] private AudioSource loopSource;
    [Tooltip("톱날마다 재생 시작 위치를 무작위로 해서, 여러 개가 같은 박자로 겹쳐 울리지 않게 한다")]
    [SerializeField] private bool randomizeLoopStart = true;
    [Tooltip("톱날마다 음높이를 ±이 비율만큼 무작위로 바꾼다 (0이면 그대로)")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.08f;

    [Header("피격 소리")]
    [Tooltip("이 톱날이 플레이어를 맞혀 죽인 순간 재생할 소리. AudioSource 없이 클립만 넣으면 된다. 거리에 따른 소리 크기(2D/3D 등)는 회전 소리(Loop Source)의 설정을 따라간다. 비워 두면 피격 소리는 나지 않는다")]
    [SerializeField] private AudioClip hitClip;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;

    [Header("불꽃 이펙트 (톱날끼리 / 벽에 부딪칠 때)")]
    [Tooltip("톱날끼리 또는 벽에 부딪친 지점에서 재생할 파티클 프리팹. NetworkObject는 붙이지 않는다 (각 컴퓨터가 로컬에서 재생). Looping은 끄고 Burst로 한 번만 터지게 만든다. 비워 두면 불꽃은 나지 않는다")]
    [SerializeField] private ParticleSystem sparkPrefab;
    [Tooltip("같은 톱날이 불꽃을 연달아 내는 최소 간격(초). 서로 비비며 지나갈 때 불꽃이 쏟아지는 것을 막는다")]
    [SerializeField, Min(0f)] private float sparkCooldown = 0.1f;
    [Tooltip("불꽃 파티클 오브젝트를 지우는 시간(초). 파티클이 모두 사라질 만큼 길게 둔다")]
    [SerializeField, Min(0.1f)] private float sparkLifetime = 2f;

    private bool loopStopped;
    private float lastSparkTime = -999f;

    private Rigidbody rb;
    private Collider[] myColliders;
    private Collider[] wallColliders;
    private Vector3 direction;
    private State state = State.Active; // Launch 없이 단독 테스트할 때는 바로 Active
    private float stateTimer;
    private Vector3 arenaCenter;
    private Vector2 arenaHalfSize;
    private float radius;

    // 소멸 조건
    private int maxBounces = int.MaxValue;
    private float timeLimit = float.PositiveInfinity;
    private float exitDistance = 3f;
    private float despawnDistance = float.PositiveInfinity; // 경기장 경계에서 이 거리 이상 벗어나면 상태와 관계없이 삭제 (단독 테스트 시에는 무제한)
    private int bounceCount;

    // 활성화된 뒤, 겹침이 풀리면 충돌을 켜줄 다른 톱날들
    private readonly List<SD_Blade> pendingBlades = new List<SD_Blade>();

    // 톱날의 이동/충돌 계산은 Host(서버)에서만 한다. 네트워크에 생성되기 전(단독 테스트)에는 항상 계산
    private bool RunsLogic => !IsSpawned || IsServer;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        PlayLoop(); // Host와 Client 모두: 생성된 순간부터 소리가 난다

        if (IsServer) return;

        // Client의 톱날은 Host가 보내주는 위치를 따라가기만 한다.
        // 내 캐릭터를 물리적으로 밀지 않도록 충돌을 끄고(트리거), 보간은 NetworkTransform에 맡긴다
        foreach (Collider col in myColliders) col.isTrigger = true;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        myColliders = GetComponentsInChildren<Collider>();
        radius = EstimateRadius();
        if (loopSource == null) loopSource = GetComponent<AudioSource>();

        allBlades.Add(this);
    }

    public override void OnDestroy()
    {
        allBlades.Remove(this);
        base.OnDestroy();
    }

    /// <summary>"완전히 안쪽에 들어왔다"고 판정되는 데 필요한 깊이 (반지름 + 여유). 프리팹에서도 계산 가능</summary>
    public float EnterDepth => EstimateRadius() + enterMargin;

    private float EstimateRadius()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return 0.5f;

        Vector3 s = transform.lossyScale;
        float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);

        if (col is SphereCollider sphere) return sphere.radius * Mathf.Max(ax, ay, az);
        if (col is CapsuleCollider capsule) return capsule.radius * Mathf.Max(ax, az);
        if (col is BoxCollider box) return 0.5f * Mathf.Max(box.size.x * ax, box.size.z * az);
        return Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
    }

    // 다른 모든 톱날과의 충돌 on/off (ignore = true면 서로 통과)
    private void SetIgnoreOtherBlades(bool ignore)
    {
        foreach (SD_Blade other in allBlades)
        {
            if (other == null || other == this) continue;
            SetIgnorePair(other, ignore);
        }
    }

    private void SetIgnorePair(SD_Blade other, bool ignore)
    {
        foreach (Collider mine in myColliders)
            foreach (Collider theirs in other.myColliders)
                Physics.IgnoreCollision(mine, theirs, ignore);
    }

    private bool OverlapsBlade(SD_Blade other)
    {
        foreach (Collider mine in myColliders)
        {
            foreach (Collider theirs in other.myColliders)
            {
                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        theirs, theirs.transform.position, theirs.transform.rotation,
                        out _, out _))
                    return true;
            }
        }
        return false;
    }

    // 활성화된 시점에 이미 활성화되어 있던 톱날들을 "충돌 켜기 대기" 목록에 넣는다
    private void QueueBladeCollisions()
    {
        pendingBlades.Clear();
        foreach (SD_Blade other in allBlades)
        {
            if (other != null && other != this && other.state == State.Active)
                pendingBlades.Add(other);
        }
    }

    // 서로 겹쳐 있지 않게 되면 그 톱날과의 충돌을 켠다 (겹친 채로 켜면 튕겨 나가는 현상 방지)
    private void EnableBladeCollisionsWhenSeparated()
    {
        for (int i = pendingBlades.Count - 1; i >= 0; i--)
        {
            SD_Blade other = pendingBlades[i];
            if (other == null || other.state != State.Active)
            {
                pendingBlades.RemoveAt(i);
                continue;
            }
            if (!OverlapsBlade(other))
            {
                SetIgnorePair(other, false);
                pendingBlades.RemoveAt(i);
            }
        }
    }

    private void Start()
    {
        if (!IsSpawned) PlayLoop(); // 네트워크 없이 단독 테스트할 때 (네트워크에서는 OnNetworkSpawn이 처리)

        // 스포너 없이 단독 테스트할 때: 랜덤 방향으로 바로 반사 시작
        if (RunsLogic && direction == Vector3.zero)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
    }

    // 라운드가 끝나면 모든 컴퓨터에서 소리를 끈다. (Client의 톱날은 Freeze가 불리지 않으므로 직접 확인한다)
    private void Update()
    {
        if (loopStopped) return;

        SD_GameManager manager = SD_GameManager.Instance;
        if (manager != null && manager.IsSpawned && manager.IsGameOver) StopLoop();
    }

    // 이 톱날이 플레이어를 죽였다: 모든 컴퓨터에서 이 톱날 위치에서 피격 소리를 낸다
    private void NotifyHit()
    {
        if (IsSpawned) HitSoundClientRpc(); // Host에서 호출하면 Host 자신을 포함한 모든 컴퓨터에서 실행된다
        else PlayHitSound();                // 네트워크 없이 단독 테스트할 때
    }

    [ClientRpc]
    private void HitSoundClientRpc()
    {
        PlayHitSound();
    }

    // 피격 소리는 임시 오브젝트에서 한 번 재생한다. 회전 소리의 AudioSource를 쓰면 라운드 종료 때 회전 소리를 끄면서
    // 마지막 피격 소리까지 끊기므로 따로 재생하고, 거리 감쇠 설정은 회전 소리의 것을 그대로 따라간다
    private void PlayHitSound()
    {
        if (hitClip == null) return;

        GameObject go = new GameObject("SD_BladeHitSound");
        go.transform.position = transform.position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = hitClip;
        source.volume = hitVolume;
        source.playOnAwake = false;
        source.loop = false;

        if (loopSource != null)
        {
            source.spatialBlend = loopSource.spatialBlend;
            source.minDistance = loopSource.minDistance;
            source.maxDistance = loopSource.maxDistance;
            source.rolloffMode = loopSource.rolloffMode;
            if (loopSource.rolloffMode == AudioRolloffMode.Custom)
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, loopSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            source.outputAudioMixerGroup = loopSource.outputAudioMixerGroup;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
        Destroy(go, hitClip.length + 0.1f);
    }

    // 톱날끼리 부딪쳤다: 모든 컴퓨터에서 부딪친 지점에 불꽃을 낸다 (피격 소리와 같은 방식)
    private void NotifySpark(Vector3 point, Vector3 normal)
    {
        if (sparkPrefab == null) return;

        if (IsSpawned) SparkClientRpc(point, normal); // Host에서 호출하면 Host 자신을 포함한 모든 컴퓨터에서 실행된다
        else PlaySpark(point, normal);                // 네트워크 없이 단독 테스트할 때
    }

    [ClientRpc]
    private void SparkClientRpc(Vector3 point, Vector3 normal)
    {
        PlaySpark(point, normal);
    }

    private void PlaySpark(Vector3 point, Vector3 normal)
    {
        if (sparkPrefab == null) return;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        ParticleSystem fx = Instantiate(sparkPrefab, point, rotation);
        Destroy(fx.gameObject, sparkLifetime);
    }

    // 반복 소리 재생. 톱날마다 시작 위치와 음높이를 조금씩 달리한다
    private void PlayLoop()
    {
        if (loopSource == null || loopSource.clip == null) return;

        loopSource.loop = true;
        if (pitchVariation > 0f) loopSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        if (randomizeLoopStart) loopSource.time = Random.Range(0f, loopSource.clip.length * 0.99f);
        loopSource.Play();
    }

    private void StopLoop()
    {
        loopStopped = true;
        if (loopSource != null) loopSource.Stop();
    }

    /// <summary>경기장 밖에서 안쪽으로 진입시키며 발사</summary>
    /// <param name="bounces">이 횟수만큼 튕긴 뒤, 다음 벽 접촉에서 튕기지 않고 뚫고 나감</param>
    /// <param name="newTimeLimit">활성화(벽 반사 시작) 후 이 시간이 지나면 벽을 뚫고 나감</param>
    /// <param name="newExitDistance">경기장 경계에서 이 거리만큼 바깥으로 나가면 파괴</param>
    /// <param name="newDespawnDistance">사라지는 범위: 경기장 경계에서 이 거리 이상 벗어나면 어떤 상태든 삭제</param>
    public void Launch(Vector3 dir, float newSpeed, Vector3 center, Vector2 arenaSize, Collider[] walls,
                       int bounces, float newTimeLimit, float newExitDistance, float newDespawnDistance)
    {
        direction = Flatten(dir);
        speed = newSpeed;
        arenaCenter = center;
        arenaHalfSize = arenaSize * 0.5f;
        wallColliders = walls;
        maxBounces = bounces;
        timeLimit = newTimeLimit;
        exitDistance = newExitDistance;
        despawnDistance = newDespawnDistance;

        ChangeState(State.Entering);
        SetIgnoreWalls(true);
        SetIgnoreOtherBlades(true); // 진입 중에는 다른 톱날과도 충돌 없음
    }

    private void FixedUpdate()
    {
        if (!RunsLogic) return; // Client의 톱날은 Host가 보내주는 위치를 따라가기만 한다

        rb.velocity = direction * speed; // Unity 6 이상이면 rb.linearVelocity

        // 사라지는 범위를 벗어났으면 상태와 관계없이 삭제 (진입이 끝나지 않고 멀리 날아가는 톱날 대비)
        if (IsOutsideDespawn())
        {
            DespawnSelf();
            return;
        }

        switch (state)
        {
            case State.Entering:
                // 경기장 안쪽에 들어왔고, 어떤 벽과도 겹쳐 있지 않을 때만 벽 충돌을 다시 켠다
                if (IsFullyInside() && !OverlapsAnyWall())
                {
                    ChangeState(State.Active);
                    SetIgnoreWalls(false); // 이 순간부터 벽과 충돌 -> 반사 시작
                    QueueBladeCollisions(); // 이미 활성화된 톱날과는 겹침이 풀리면 충돌 시작
                }
                else
                {
                    stateTimer += Time.fixedDeltaTime;
                    if (stateTimer > maxEnterTime) DespawnSelf();
                }
                break;

            case State.Active:
                stateTimer += Time.fixedDeltaTime;
                EnableBladeCollisionsWhenSeparated();
                if (stateTimer >= timeLimit) StartLeaving(); // 시간 안에 다 못 튕김
                break;

            case State.Leaving:
                stateTimer += Time.fixedDeltaTime;
                if (IsOutsideExit() || stateTimer > maxLeaveTime) DespawnSelf();
                break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!RunsLogic) return; // 충돌 판정과 반사는 Host에서만

        // 플레이어와 닿으면 톱날 상태와 관계없이 즉사. 톱날은 반사나 횟수 계산 없이 그대로 진행한다
        SD_PlayerDeath player = collision.collider.GetComponentInParent<SD_PlayerDeath>();
        if (player != null)
        {
            if (SD_GameManager.Instance != null)
            {
                bool wasAlive = !player.IsDead;
                SD_GameManager.Instance.ReportHit(player); // 죽음 확정은 게임 매니저가 한다
                if (wasAlive && player.IsDead) NotifyHit(); // 이번 접촉으로 실제로 죽었을 때만 (이미 죽었거나 라운드 밖의 접촉은 제외)
            }
            return;
        }

        if (state != State.Active) return; // 진입/퇴장 중에는 반사하지 않음

        Vector3 normal = Flatten(collision.GetContact(0).normal);
        if (normal == Vector3.zero) return; // 바닥 등 수직 면은 무시

        // 다른 톱날과의 충돌은 반사만 하고 튕김 횟수에는 포함하지 않는다
        SD_Blade otherBlade = collision.collider.GetComponentInParent<SD_Blade>();
        bool hitBlade = otherBlade != null;

        // 불꽃: 톱날끼리 부딪칠 때는 같은 충돌이 양쪽 톱날에서 한 번씩 오므로 한쪽(ID가 작은 쪽)만 낸다.
        // 벽에 부딪칠 때는 이 톱날 혼자이므로 그대로 낸다 (마지막 튕김으로 벽을 뚫고 나가는 순간에도 낸다)
        bool emitSpark = !hitBlade || GetInstanceID() < otherBlade.GetInstanceID();
        if (emitSpark && Time.time - lastSparkTime >= sparkCooldown)
        {
            lastSparkTime = Time.time;
            ContactPoint contact = collision.GetContact(0);
            NotifySpark(contact.point, contact.normal);
        }

        if (!hitBlade && bounceCount >= maxBounces)
        {
            // 정해진 횟수를 다 튕겼으면 이번 벽은 튕기지 않고 그대로 뚫고 나감
            StartLeaving();
            return;
        }

        direction = Flatten(Vector3.Reflect(direction, normal));
        if (!hitBlade) bounceCount++;
    }

    private void StartLeaving()
    {
        ChangeState(State.Leaving);
        SetIgnoreWalls(true); // 현재 진행 방향 그대로 벽을 뚫고 나감
        pendingBlades.Clear();
        SetIgnoreOtherBlades(true); // 퇴장 중에는 다른 톱날과도 충돌 없음
    }

    private void ChangeState(State newState)
    {
        state = newState;
        stateTimer = 0f;
    }

    private bool IsFullyInside()
    {
        Vector3 p = rb.position - arenaCenter;
        float limitX = arenaHalfSize.x - radius - enterMargin;
        float limitZ = arenaHalfSize.y - radius - enterMargin;
        return Mathf.Abs(p.x) < limitX && Mathf.Abs(p.z) < limitZ;
    }

    // 네트워크에 생성된 톱날은 Despawn(모든 참가자의 화면에서 삭제), 아니면 일반 삭제
    private void DespawnSelf()
    {
        if (IsSpawned && IsServer) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // 사라지는 범위(경기장 경계에서 despawnDistance만큼 바깥)를 벗어났는지
    private bool IsOutsideDespawn()
    {
        Vector3 p = rb.position - arenaCenter;
        return Mathf.Abs(p.x) > arenaHalfSize.x + despawnDistance
            || Mathf.Abs(p.z) > arenaHalfSize.y + despawnDistance;
    }

    // 경기장 경계에서 exitDistance만큼 바깥으로 나갔는지
    private bool IsOutsideExit()
    {
        Vector3 p = rb.position - arenaCenter;
        return Mathf.Abs(p.x) > arenaHalfSize.x + exitDistance
            || Mathf.Abs(p.z) > arenaHalfSize.y + exitDistance;
    }

    public void Freeze()
    {
        StopLoop(); // enabled = false가 되면 Update가 멈추므로 여기서도 끈다
        speed = 0f;
        if (rb != null)
        {
            rb.velocity = Vector3.zero; // Unity 6 이상이면 rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // 더 이상 물리 충돌이나 이동 영향을 받지 않도록 고정
        }
        enabled = false; // FixedUpdate 등 업데이트 스크립트 비활성화
    }

    public static void FreezeAll()
    {
        foreach (SD_Blade blade in allBlades)
        {
            if (blade != null)
            {
                blade.Freeze();
            }
        }
    }

    public static void DestroyAll()
    {
        for (int i = allBlades.Count - 1; i >= 0; i--)
        {
            if (allBlades[i] != null)
            {
                allBlades[i].DespawnSelf();
            }
        }
        allBlades.Clear();
    }

    // 톱날이 아직 벽 콜라이더와 겹쳐 있는지 (겹친 채로 충돌을 켜면 벽에 끼어 멈춤)
    private bool OverlapsAnyWall()
    {
        if (wallColliders == null) return false;
        foreach (Collider mine in myColliders)
        {
            foreach (Collider wall in wallColliders)
            {
                if (wall == null || !wall.enabled) continue;
                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        wall, wall.transform.position, wall.transform.rotation,
                        out _, out _))
                    return true;
            }
        }
        return false;
    }

    private void SetIgnoreWalls(bool ignore)
    {
        if (wallColliders == null) return;
        foreach (Collider mine in myColliders)
        {
            foreach (Collider wall in wallColliders)
            {
                if (wall != null) Physics.IgnoreCollision(mine, wall, ignore);
            }
        }
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }

    public void SetSpeed(float newSpeed) => speed = newSpeed;
}