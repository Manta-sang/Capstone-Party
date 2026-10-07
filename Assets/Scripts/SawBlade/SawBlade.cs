using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벽에 닿으면 반사각으로 튕기는 톱날 (탑다운 3D, XZ 평면 이동)
/// 상태: Entering(벽을 뚫고 진입) -> Active(벽 반사) -> Leaving(벽을 뚫고 퇴장 후 파괴)
/// 정해진 횟수만큼 튕기면, 또는 시간 제한이 지나면 벽을 뚫고 나가 경기장 밖에서 사라진다.
/// 필요: Rigidbody + Collider
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SawBlade : MonoBehaviour
{
    private enum State { Entering, Active, Leaving }

    // 톱날끼리의 충돌을 관리하기 위한 전체 목록
    private static readonly List<SawBlade> allBlades = new List<SawBlade>();

    private float speed = 6f; // 스포너가 Launch()로 덮어씀 (스포너 없이 단독 테스트할 때만 쓰는 기본값)
    [SerializeField] private float enterMargin = 0.2f;   // 완전히 들어온 뒤 여유 거리
    [SerializeField] private float maxEnterTime = 10f;   // 이 시간 안에 못 들어오면 제거 (안전장치)
    [SerializeField] private float maxLeaveTime = 10f;   // 이 시간 안에 못 나가면 제거 (안전장치)

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
    private int bounceCount;

    // 활성화된 뒤, 겹침이 풀리면 충돌을 켜줄 다른 톱날들
    private readonly List<SawBlade> pendingBlades = new List<SawBlade>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        myColliders = GetComponentsInChildren<Collider>();
        radius = EstimateRadius();

        allBlades.Add(this);
    }

    private void OnDestroy()
    {
        allBlades.Remove(this);
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
        foreach (SawBlade other in allBlades)
        {
            if (other == null || other == this) continue;
            SetIgnorePair(other, ignore);
        }
    }

    private void SetIgnorePair(SawBlade other, bool ignore)
    {
        foreach (Collider mine in myColliders)
            foreach (Collider theirs in other.myColliders)
                Physics.IgnoreCollision(mine, theirs, ignore);
    }

    private bool OverlapsBlade(SawBlade other)
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
        foreach (SawBlade other in allBlades)
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
            SawBlade other = pendingBlades[i];
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
        // 스포너 없이 단독 테스트할 때: 랜덤 방향으로 바로 반사 시작
        if (direction == Vector3.zero)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
    }

    /// <summary>경기장 밖에서 안쪽으로 진입시키며 발사</summary>
    /// <param name="bounces">이 횟수만큼 튕긴 뒤, 다음 벽 접촉에서 튕기지 않고 뚫고 나감</param>
    /// <param name="newTimeLimit">활성화(벽 반사 시작) 후 이 시간이 지나면 벽을 뚫고 나감</param>
    /// <param name="newExitDistance">경기장 경계에서 이 거리만큼 바깥으로 나가면 파괴</param>
    public void Launch(Vector3 dir, float newSpeed, Vector3 center, Vector2 arenaSize, Collider[] walls,
                       int bounces, float newTimeLimit, float newExitDistance)
    {
        direction = Flatten(dir);
        speed = newSpeed;
        arenaCenter = center;
        arenaHalfSize = arenaSize * 0.5f;
        wallColliders = walls;
        maxBounces = bounces;
        timeLimit = newTimeLimit;
        exitDistance = newExitDistance;

        ChangeState(State.Entering);
        SetIgnoreWalls(true);
        SetIgnoreOtherBlades(true); // 진입 중에는 다른 톱날과도 충돌 없음
    }

    private void FixedUpdate()
    {
        rb.velocity = direction * speed; // Unity 6 이상이면 rb.linearVelocity

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
                    if (stateTimer > maxEnterTime) Destroy(gameObject);
                }
                break;

            case State.Active:
                stateTimer += Time.fixedDeltaTime;
                EnableBladeCollisionsWhenSeparated();
                if (stateTimer >= timeLimit) StartLeaving(); // 시간 안에 다 못 튕김
                break;

            case State.Leaving:
                stateTimer += Time.fixedDeltaTime;
                if (IsOutsideExit() || stateTimer > maxLeaveTime) Destroy(gameObject);
                break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 플레이어와 닿으면 톱날 상태와 관계없이 즉사. 톱날은 반사나 횟수 계산 없이 그대로 진행한다
        SawDodgePlayerLife player = collision.collider.GetComponentInParent<SawDodgePlayerLife>();
        if (player != null)
        {
            player.Die();
            return;
        }

        if (state != State.Active) return; // 진입/퇴장 중에는 반사하지 않음

        Vector3 normal = Flatten(collision.GetContact(0).normal);
        if (normal == Vector3.zero) return; // 바닥 등 수직 면은 무시

        // 다른 톱날과의 충돌은 반사만 하고 튕김 횟수에는 포함하지 않는다
        bool hitBlade = collision.collider.GetComponentInParent<SawBlade>() != null;

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

    // 경기장 경계에서 exitDistance만큼 바깥으로 나갔는지
    private bool IsOutsideExit()
    {
        Vector3 p = rb.position - arenaCenter;
        return Mathf.Abs(p.x) > arenaHalfSize.x + exitDistance
            || Mathf.Abs(p.z) > arenaHalfSize.y + exitDistance;
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