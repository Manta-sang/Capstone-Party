using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타일 뒤집기의 배경음, 타일 칠하기 소리, 라운드 종료 소리. (TF_GameManager와 같은 오브젝트에 붙인다)
///
/// TF_GameManager는 이 스크립트를 모른다. 이 스크립트가 매니저와 격자가 공개한 이벤트와 값만 읽어서 소리를 낸다.
///  - 라운드 시작: TF_GameManager.GameStarted 이벤트 -> 배경음 시작
///  - 라운드 종료: TF_GameManager.GameOver 이벤트 -> 종료 소리 재생 + 배경음 페이드아웃
///  - 타일 칠하기: TF_TileGridManager.OnTileOwnerChanged에서 새 주인이 "내 자리(TF_GameManager.LocalSlot)"일 때만 소리
///    (다른 참가자가 칠하는 것은 소리를 내지 않는다. 내 화면에서 내 타일이 바뀌는 순간 바로 나므로 서버 확정을 기다리지 않는다)
/// 모든 소리는 이 컴퓨터의 스피커로만 재생한다 (네트워크 동기화 불필요).
/// </summary>
public class TF_GameSound : MonoBehaviour
{
    [Header("배경음")]
    [Tooltip("배경음을 재생할 AudioSource (Clip을 넣어 둔다. Play On Awake는 끈다). 비워 두면 이 오브젝트의 AudioSource를 쓴다. Loop와 Spatial Blend는 코드가 맞춘다")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("배경음의 기본 볼륨")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.4f;
    [Tooltip("라운드가 시작될 때 서서히 커지는 시간(초). 0이면 바로 시작")]
    [SerializeField, Min(0f)] private float bgmFadeInTime = 0.5f;
    [Tooltip("게임이 끝날 때 서서히 작아지는 시간(초). 결과 화면으로 넘어가기까지의 대기 시간(Result Delay)보다 짧게 둔다")]
    [SerializeField, Min(0f)] private float bgmFadeOutTime = 1f;

    [Header("타일 칠하기 소리")]
    [Tooltip("내가 타일을 칠한 순간 재생할 소리. 내가 칠할 때만 나고, 다른 참가자가 칠할 때는 소리가 나지 않는다. 비워 두면 소리가 나지 않는다 (AudioSource는 코드가 만든다)")]
    [SerializeField] private AudioClip paintClip;
    [SerializeField, Range(0f, 1f)] private float paintVolume = 0.6f;
    [Tooltip("칠할 때마다 음높이를 ±이 비율만큼 무작위로 바꿔 같은 소리가 반복되는 느낌을 줄인다 (0이면 그대로)")]
    [SerializeField, Range(0f, 0.3f)] private float paintPitchVariation = 0.1f;
    [Tooltip("칠하기 소리 사이의 최소 간격(초). 한 번에 여러 타일이 칠해질 때 소리가 겹쳐 커지는 것을 막는다")]
    [SerializeField, Min(0f)] private float paintSoundMinInterval = 0.05f;

    [Header("라운드 종료 소리")]
    [Tooltip("라운드가 끝나는 순간 모든 참가자가 똑같이 듣는 소리 (승패 구분 없음). 비워 두면 소리가 나지 않는다")]
    [SerializeField] private AudioClip endClip;
    [SerializeField, Range(0f, 1f)] private float endVolume = 0.8f;
    [Tooltip("게임이 끝난 뒤 이 시간(초) 뒤에 재생한다")]
    [SerializeField, Min(0f)] private float endSoundDelay = 0.5f;

    private TF_GameManager manager;
    private TF_TileGridManager grid;
    private AudioSource paintSource; // 칠하기 소리용. Awake에서 코드가 만든다
    private float lastPaintSoundTime = -999f;
    private Coroutine bgmRoutine;

    private void Awake()
    {
        if (bgmSource == null) bgmSource = GetComponent<AudioSource>();

        // 칠하기 소리는 AudioSource를 직접 붙이지 않아도 되게, 코드가 자식 오브젝트에 만든다
        // (배경음용 AudioSource와 헷갈리지 않는다)
        if (paintClip != null)
        {
            GameObject go = new GameObject("TF_PaintSound");
            go.transform.SetParent(transform, false);
            paintSource = go.AddComponent<AudioSource>();
            paintSource.playOnAwake = false;
            paintSource.spatialBlend = 0f; // 위치와 상관없이 항상 같은 크기로 들린다
        }
    }

    private void Start()
    {
        // TF_GameManager.Instance는 매니저의 Awake에서 정해지므로, 모든 Awake가 끝난 Start에서는 항상 찾을 수 있다
        manager = TF_GameManager.Instance;
        if (manager != null)
        {
            manager.GameStarted += OnGameStarted;
            manager.GameOver += OnGameOver;
        }
        else Debug.LogWarning("[TF_GameSound] 씬에서 TF_GameManager를 찾지 못했습니다. 소리가 나지 않습니다.", this);

        grid = FindObjectOfType<TF_TileGridManager>();
        if (grid != null) grid.OnTileOwnerChanged += OnTileOwnerChanged;
        else Debug.LogWarning("[TF_GameSound] 씬에서 TF_TileGridManager를 찾지 못했습니다. 칠하기 소리가 나지 않습니다.", this);
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.GameStarted -= OnGameStarted;
            manager.GameOver -= OnGameOver;
        }
        if (grid != null) grid.OnTileOwnerChanged -= OnTileOwnerChanged;
    }

    private void OnGameStarted()
    {
        StartBgm();
    }

    private void OnGameOver(IReadOnlyList<int> winners)
    {
        PlayEndSound();
        StopBgm();
    }

    // 타일 소유자가 바뀔 때마다 불린다. 새 주인이 내 자리일 때만 소리를 낸다
    // (내 칠하기는 Client에서는 예측 표시, Host에서는 확정 시점에 바로 여기로 온다. 다른 참가자가 칠한 것은 자리 번호가 달라 걸러진다)
    private void OnTileOwnerChanged(int tileIndex, int newOwner)
    {
        if (manager == null || newOwner < 0 || newOwner != manager.LocalSlot) return;
        PlayPaintSound();
    }

    // ───────────────────────── 칠하기 소리 ─────────────────────────

    // 칠할 때마다 음높이를 조금씩 달리하고, 너무 촘촘하면 건너뛴다
    private void PlayPaintSound()
    {
        if (paintSource == null || paintClip == null) return;
        if (Time.unscaledTime - lastPaintSoundTime < paintSoundMinInterval) return;
        lastPaintSoundTime = Time.unscaledTime;

        paintSource.pitch = 1f + Random.Range(-paintPitchVariation, paintPitchVariation);
        paintSource.PlayOneShot(paintClip, paintVolume);
    }

    // ───────────────────────── 종료 소리 ─────────────────────────

    // 배경음의 AudioSource와 따로, 코드가 만든 임시 오브젝트에서 한 번 재생한다 (배경음 페이드아웃/정지에 영향을 받지 않는다)
    private void PlayEndSound()
    {
        if (endClip == null) return;

        GameObject go = new GameObject("TF_EndSound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = endClip;
        source.volume = endVolume;
        source.spatialBlend = 0f;
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
