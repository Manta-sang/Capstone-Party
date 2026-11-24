using UnityEngine;

/// <summary>
/// 생존형 미니게임의 탈락 확정 처리에서 호출하는 로컬 연출입니다.
/// 각 게임의 기존 탈락 알림을 사용하며 캐릭터마다 한 번만 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SurvivalDeathEffect : MonoBehaviour
{
    [Header("생존형 게임 탈락 이펙트")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField, Min(0.01f)] private float effectScale = 2f;
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, Min(0.1f)] private float cleanupDelay = 5f;

    private bool played;

    /// <summary>탈락을 전달받은 각 참가자 화면에서 캐릭터와 독립된 연출을 생성합니다.</summary>
    public void Play()
    {
        if (played || effectPrefab == null) return;
        played = true;

        // 캐릭터가 날아가거나 숨겨져도 탈락 위치에서 연출을 끝까지 재생합니다.
        GameObject effect = Instantiate(effectPrefab, transform.position + positionOffset, effectPrefab.transform.rotation);
        effect.transform.localScale *= effectScale;

        // 프리팹에 포함된 소리도 거리에 따라 작아지는 3D 사운드로 설정합니다.
        foreach (AudioSource source in effect.GetComponentsInChildren<AudioSource>(true))
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
        }
        Destroy(effect, cleanupDelay);
    }
}
