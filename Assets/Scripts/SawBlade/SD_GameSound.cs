using System.Collections;
using UnityEngine;

/// <summary>
/// 톱날 피하기의 배경음과 라운드 종료 소리. (SD_GameManager와 같은 오브젝트에 붙인다)
///
/// SD_GameManager는 이 스크립트를 모른다. 이 스크립트가 매니저가 공개한 값과 이벤트만 읽어서 소리를 낸다.
///  - 라운드 시작: SD_GameManager.HasRoundStarted를 확인해서 배경음 시작
///  - 라운드 종료: SD_GameManager.GameOver 이벤트를 받아 종료 소리 재생 + 배경음 페이드아웃
/// 모든 소리는 이 컴퓨터의 스피커로만 재생한다 (네트워크 동기화 불필요).
/// 톱날의 회전 소리와 피격 소리는 톱날 오브젝트(SD_Blade)가 낸다.
/// </summary>
public class SD_GameSound : MonoBehaviour
{
    [Header("배경음")]
    [Tooltip("배경음을 재생할 AudioSource (Clip을 넣어 둔다. Play On Awake는 끈다). 비워 두면 이 오브젝트의 AudioSource를 쓴다. Loop와 Spatial Blend는 코드가 맞춘다")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("배경음의 기본 볼륨. 톱날 소리와 피격 소리보다 작게 시작한다")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.4f;
    [Tooltip("라운드가 시작될 때 서서히 커지는 시간(초). 0이면 바로 시작")]
    [SerializeField, Min(0f)] private float bgmFadeInTime = 0.5f;
    [Tooltip("게임이 끝날 때 서서히 작아지는 시간(초). 결과 화면으로 넘어가기까지의 대기 시간(Result Delay)보다 짧게 둔다")]
    [SerializeField, Min(0f)] private float bgmFadeOutTime = 1f;

    [Header("라운드 종료 소리")]
    [Tooltip("라운드가 끝나는 순간 모든 참가자가 똑같이 듣는 소리 (승패 구분 없음). 비워 두면 소리가 나지 않는다")]
    [SerializeField] private AudioClip endClip;
    [SerializeField, Range(0f, 1f)] private float endVolume = 0.8f;
    [Tooltip("게임이 끝난 뒤 이 시간(초) 뒤에 재생한다. 마지막으로 맞는 순간의 피격 소리와 겹치지 않게 한다")]
    [SerializeField, Min(0f)] private float endSoundDelay = 0.5f;

    private SD_GameManager manager;
    private bool bgmStarted;
    private Coroutine bgmRoutine;

    private void Awake()
    {
        if (bgmSource == null) bgmSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // SD_GameManager.Instance는 매니저의 Awake에서 정해지므로, 모든 Awake가 끝난 Start에서는 항상 찾을 수 있다
        manager = SD_GameManager.Instance;
        if (manager != null) manager.GameOver += OnGameOver;
        else Debug.LogWarning("[SD_GameSound] 씬에서 SD_GameManager를 찾지 못했습니다. 소리가 나지 않습니다.", this);
    }

    private void OnDestroy()
    {
        if (manager != null) manager.GameOver -= OnGameOver;
    }

    private void Update()
    {
        // 라운드 시작을 알려 주는 이벤트가 없어서, 공개된 HasRoundStarted를 한 번만 확인한다
        if (bgmStarted || manager == null) return;

        if (manager.HasRoundStarted)
        {
            bgmStarted = true;
            StartBgm();
        }
    }

    private void OnGameOver(SD_PlayerDeath winner)
    {
        PlayEndSound();
        StopBgm();
    }

    // ───────────────────────── 종료 소리 ─────────────────────────

    // 배경음의 AudioSource와 따로, 코드가 만든 임시 오브젝트에서 한 번 재생한다 (배경음 페이드아웃/정지에 영향을 받지 않는다)
    private void PlayEndSound()
    {
        if (endClip == null) return;

        GameObject go = new GameObject("SD_EndSound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = endClip;
        source.volume = endVolume;
        source.spatialBlend = 0f; // 위치와 상관없이 항상 같은 크기로 들린다
        source.PlayDelayed(endSoundDelay);

        Destroy(go, endSoundDelay + endClip.length + 0.1f);
    }

    // ───────────────────────── 배경음 ─────────────────────────

    private void StartBgm()
    {
        if (bgmSource == null || bgmSource.clip == null) return;

        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = bgmFadeInTime > 0f ? 0f : bgmVolume;
        bgmSource.Play();

        if (bgmFadeInTime > 0f) RestartBgmFade(bgmVolume, bgmFadeInTime, false);
    }

    private void StopBgm()
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;

        if (bgmFadeOutTime > 0f) RestartBgmFade(0f, bgmFadeOutTime, true);
        else bgmSource.Stop();
    }

    // 진행 중인 페이드가 있으면 멈추고 새로 시작한다 (페이드가 두 개 겹쳐 돌지 않게)
    private void RestartBgmFade(float targetVolume, float duration, bool stopAtEnd)
    {
        if (bgmRoutine != null) StopCoroutine(bgmRoutine);
        bgmRoutine = StartCoroutine(FadeBgm(targetVolume, duration, stopAtEnd));
    }

    private IEnumerator FadeBgm(float targetVolume, float duration, bool stopAtEnd)
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (bgmSource == null) yield break; // 씬이 바뀌어 사라진 경우
            bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (bgmSource == null) yield break;
        bgmSource.volume = targetVolume;
        if (stopAtEnd) bgmSource.Stop();
        bgmRoutine = null;
    }
}
