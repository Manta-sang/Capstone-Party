using UnityEngine;

/// <summary>
/// 톱날의 파티클 이펙트(톱날끼리/벽에 부딪칠 때의 불꽃, 플레이어를 맞혔을 때의 피격 이펙트)를 만든다. (톱날 프리팹에 SD_Blade와 같이 붙인다)
///
/// 네트워크는 모르는 일반 컴포넌트이다. "언제 터뜨릴지"는 SD_Blade가 Host에서 판단하고 ClientRpc로 모든 컴퓨터에 알리면,
/// 각 컴퓨터의 SD_Blade가 이 스크립트의 PlaySpark / PlayHit을 불러서 자기 화면에서 파티클을 만든다.
/// 이 컴포넌트가 없으면 톱날은 이펙트 없이 그대로 동작한다.
/// </summary>
public class SD_BladeEffects : MonoBehaviour
{
    [Header("불꽃 (톱날끼리 / 벽에 부딪칠 때)")]
    [Tooltip("부딪친 지점에서 재생할 파티클 프리팹. NetworkObject는 붙이지 않는다 (각 컴퓨터가 로컬에서 재생). Looping은 끄고 Burst로 한 번만 터지게 만든다. 비워 두면 불꽃은 나지 않는다")]
    [SerializeField] private ParticleSystem sparkPrefab;
    [Tooltip("같은 톱날이 불꽃을 연달아 내는 최소 간격(초). 서로 비비며 지나갈 때 불꽃이 쏟아지는 것을 막는다")]
    [SerializeField, Min(0f)] private float sparkCooldown = 0.1f;
    [Tooltip("불꽃 파티클 오브젝트를 지우는 시간(초). 파티클이 모두 사라질 만큼 길게 둔다")]
    [SerializeField, Min(0.1f)] private float sparkLifetime = 2f;

    [Header("피격 (플레이어를 맞혔을 때)")]
    [Tooltip("플레이어를 맞혀 죽인 순간, 닿은 지점에서 재생할 파티클 프리팹. NetworkObject는 붙이지 않는다. 파티클은 톱날에서 플레이어 쪽(밀려나는 방향)으로 터진다. 비워 두면 이펙트는 나지 않는다")]
    [SerializeField] private ParticleSystem hitPrefab;
    [Tooltip("피격 이펙트 오브젝트를 지우는 시간(초). 파티클이 모두 사라질 만큼 길게 둔다")]
    [SerializeField, Min(0.1f)] private float hitLifetime = 2f;

    private float lastSparkTime = -999f;

    /// <summary>
    /// 지금 불꽃을 낼 수 있는지 확인하고, 낼 수 있으면 쿨타임을 시작한다. (SD_Blade가 Host에서 부른다)
    /// 불꽃 프리팹이 없으면 false라서, 알릴 필요가 없을 때는 네트워크로 보내지도 않는다.
    /// </summary>
    public bool TryUseSparkCooldown()
    {
        if (sparkPrefab == null) return false;
        if (Time.time - lastSparkTime < sparkCooldown) return false;

        lastSparkTime = Time.time;
        return true;
    }

    /// <summary>부딪친 지점에서 불꽃을 터뜨린다. normal은 터지는 방향</summary>
    public void PlaySpark(Vector3 point, Vector3 normal)
    {
        Spawn(sparkPrefab, point, normal, sparkLifetime);
    }

    /// <summary>플레이어에게 닿은 지점에서 피격 이펙트를 터뜨린다. pushDirection은 톱날에서 플레이어 쪽</summary>
    public void PlayHit(Vector3 point, Vector3 pushDirection)
    {
        Spawn(hitPrefab, point, pushDirection, hitLifetime);
    }

    private static void Spawn(ParticleSystem prefab, Vector3 point, Vector3 direction, float lifetime)
    {
        if (prefab == null) return;

        Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        ParticleSystem fx = Instantiate(prefab, point, rotation);
        Destroy(fx.gameObject, lifetime);
    }
}
