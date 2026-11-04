using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrosswalkRun;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Networked four-lane school track race. Each hurdle asks for a four-arrow sequence two seconds ahead.</summary>
public sealed class QteSceneController : MonoBehaviour
{
    private sealed class RunnerBodyState
    {
        public bool isKinematic;
        public bool useGravity;
    }

    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private GameObject[] spectatorPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private float runSpeed = 5.7f;
    [SerializeField] private float hurdleSpacing = 18.5f;
    [SerializeField] private float inputLeadTime = 2f;
    [SerializeField] private float failStopDuration = 1f;
    [SerializeField] private float cameraFollowSmoothTime = 0.18f;

    private const string RosterMessage = "QTE.Entry.Roster";
    private const string ReadyMessage = "QTE.Entry.Ready";
    private const string ReadyStatusMessage = "QTE.Entry.ReadyStatus";
    private const string StartMessage = "QTE.Entry.Start";
    private static readonly KeyCode[] Keys = { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
    private static readonly string[] Arrows = { "↑", "↓", "←", "→" };
    private const int ArrowInputsPerHurdle=4;
    private const float QteSuccessSpeedBonus=.65f;
    private const float QteSuccessBoostDuration=1.6f;
    private const float MapOverviewDuration=2.25f;
    private const float CameraTransitionDuration=.8f;
    private const int RaceCountdownSeconds=3;

    private readonly List<ulong> owners = new List<ulong>();
    private readonly List<ulong> objectIds = new List<ulong>();
    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private readonly HashSet<ulong> finishedPlayers = new HashSet<ulong>();
    private readonly HashSet<ulong> playersPastStart = new HashSet<ulong>();
    private readonly Dictionary<int, GameObject> hurdles = new Dictionary<int, GameObject>();
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private readonly Dictionary<Rigidbody, RunnerBodyState> runnerBodyStates = new Dictionary<Rigidbody, RunnerBodyState>();
    private NetworkManager network;
    private CustomMessagingManager messages;
    private NetworkObject activePlayer;
    private Animator activeAnimator;
    private Camera raceCamera;
    private TextMeshProUGUI instructionText, countdownText, statusText;
    private TMP_Text sharedCountdownText;
    private AudioSource whistleSource, musicSource;
    private Vector3 cameraVelocity;
    private float cameraShakeUntil,cameraShakeStrength,cameraShakeDuration;
    private int entryToken;
    private double startsAt = -1;
    public bool HasRoundStarted => gameStarted;
    public float CountdownRemaining => network == null || startsAt < 0 ? 0 : Mathf.Max(0, (float)(startsAt - network.ServerTime.Time));
    private bool rosterReceived, localReady, startScheduled, gameStarted, spawnRequested, localFinished, roundEnding;
    private bool overviewMode = true;
    private string entryError;
    private float distanceAlongTrack, stunUntil;
    private float speedBoostUntil;
    private int hurdleNumber, arrowProgress;
    private readonly int[] expectedDirections=new int[ArrowInputsPerHurdle];
    private int lastArrowPromptProgress=-1,lastArrowPromptTenths=-1;
    private bool hurdleResolved = true, hurdlePrompted, jumping, queuedJump;
    private bool fallRecoveryTriggered;
    private bool hurdlePreviewSpawned;
    private float jumpStartedAt;
    private float queuedJumpAt;
    private float startFlashUntil;
    private const float StraightLength = 70f;
    private const float TurnRadius = 20.6f;
    private const float FinishOffset = 13.2f;
    private const float StartClearance = 18f;
    private float RaceDistance => PlayerLapLength(3) + FinishOffset;
    private float CurrentRunSpeed => Time.time < speedBoostUntil ? runSpeed + QteSuccessSpeedBonus : runSpeed;
    public string DeveloperStatus => statusText != null ? statusText.text : "QTE 준비 중";

    private void Awake()
    {
        network = NetworkManager.Singleton;
        var sharedPresentation=FindObjectOfType<GameStartPresentation>();
        if(sharedPresentation!=null)sharedCountdownText=sharedPresentation.countdownText;
        raceCamera = Camera.main;
        if (raceCamera != null) raceCamera.farClipPlane = 460;
        HidePrototypeSceneObjects();
        if (GameObject.Find("School ground") == null) BuildSchoolTrack();
        SetSceneSpectatorsIdle();
        AddAudienceFillLights();
        BuildRaceUi();
        whistleSource = gameObject.AddComponent<AudioSource>();
        whistleSource.playOnAwake = false;
        whistleSource.spatialBlend = 0;
        StartSchoolSportsMusic();
        if (network == null || !network.IsListening) { entryError = "네트워크 연결을 기다리는 중입니다."; return; }
        messages = network.CustomMessagingManager;
        messages.RegisterNamedMessageHandler(RosterMessage, OnRosterReceived);
        messages.RegisterNamedMessageHandler(ReadyMessage, OnReadyReceived);
        messages.RegisterNamedMessageHandler(ReadyStatusMessage, OnReadyStatusReceived);
        messages.RegisterNamedMessageHandler(StartMessage, OnStartReceived);
        if (network.IsServer) network.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void OnSceneLoaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (scene != gameObject.scene.name || spawnRequested) return;
        if (timedOut.Count > 0 || network.ConnectedClientsIds.Any(id => !completed.Contains(id))) { FailEntry("참가자의 씬 로딩을 완료하지 못했습니다."); return; }
        spawnRequested = true;
        StartCoroutine(SpawnPlayers());
    }

    private IEnumerator SpawnPlayers()
    {
        yield return null;
        if (network == null || !network.IsServer) yield break;
        ulong[] clients = network.ConnectedClientsIds.OrderBy(id => id).ToArray();
        if (clients.Length == 0 || clients.Length > 4 || characterPrefabs == null || characterPrefabs.Length == 0 ||
            spawnPoints == null || spawnPoints.Length < clients.Length || spawnPoints.Take(clients.Length).Any(p => p == null) || characterPrefabs.Any(p => p == null))
        { FailEntry("참가자 4명과 캐릭터 스폰 설정을 확인해 주세요."); yield break; }
        foreach (ulong id in clients) { var old = network.SpawnManager.GetPlayerNetworkObject(id); if (old != null) old.Despawn(true); }
        entryToken = Guid.NewGuid().GetHashCode();
        for (int slot = 0; slot < clients.Length; slot++)
        {
            ulong id = clients[slot]; Transform point = spawnPoints[slot];
            GameObject instance = Instantiate(characterPrefabs[(int)(id % (ulong)characterPrefabs.Length)], point.position, point.rotation);
            var player = instance.GetComponent<NetworkObject>();
            if (player == null) { Destroy(instance); FailEntry("캐릭터 프리팹에 NetworkObject가 없습니다."); yield break; }
            player.SpawnAsPlayerObject(id, true);
            owners.Add(id); objectIds.Add(player.NetworkObjectId);
        }
        rosterReceived = true;
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe(entryToken); writer.WriteValueSafe(owners.Count);
            for (int i = 0; i < owners.Count; i++) { writer.WriteValueSafe(owners[i]); writer.WriteValueSafe(objectIds[i]); }
            foreach (ulong id in owners) if (id != NetworkManager.ServerClientId) messages.SendNamedMessage(RosterMessage, id, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void Update()
    {
        if (network == null || !network.IsListening) return;
        foreach (NetworkObject p in network.SpawnManager.SpawnedObjectsList)
        {
            var movement = p.GetComponent<PlayerMovement>(); if (movement != null) movement.enabled = false;
            var push = p.GetComponent<PlayerInteraction>(); if (push != null) push.enabled = false;
            var legacy = p.GetComponent<QteRunner>(); if (legacy != null) legacy.enabled = false;
            if(p.IsPlayerObject)FreezeRunnerPhysics(p);
        }
        if (rosterReceived && !localReady && LocalRosterIsPresent()) SendReady();
        if (network.IsServer && rosterReceived && !startScheduled && owners.All(id => !network.ConnectedClients.ContainsKey(id) || readyClients.Contains(id))) ScheduleStart();
        var local = network.SpawnManager.GetLocalPlayerObject();
        if (local != null && activePlayer != local)
        {
            activePlayer = local; activeAnimator = local.GetComponentInChildren<Animator>(); localFinished = false;
            int slot = objectIds.IndexOf(local.NetworkObjectId);
            if (slot >= 0) { float start = StartOffsetForLane(slot); Vector3 at = TrackPosition(start, slot); local.transform.SetPositionAndRotation(at, TrackRotation(start,slot)); }
        }
        if (!gameStarted)
        {
            UpdateCountdown();
            if (startsAt >= 0 && network.ServerTime.Time >= startsAt)
            {
                gameStarted = true; overviewMode = false; startFlashUntil = Time.unscaledTime + .8f;
                SetCountdownText("<color=#FF625C>출발!</color>"); distanceAlongTrack = 0;
                for (int hurdle = 1; hurdle <= Mathf.FloorToInt(RaceDistance / hurdleSpacing); hurdle++)
                    SpawnHurdle(hurdle, hurdle * hurdleSpacing);
                PrepareNextHurdle(); PlayWhistle();
            }
            return;
        }
        if (countdownText != null && Time.unscaledTime >= startFlashUntil) countdownText.text = string.Empty;
        if (activePlayer == null || localFinished || roundEnding) return;
        if (Time.unscaledTime < stunUntil)
        {
            SetWalking(false);
            instructionText.text = "<color=#FF625C>실패!</color>";
            if (!fallRecoveryTriggered && stunUntil - Time.unscaledTime <= Mathf.Min(.42f, failStopDuration * .42f))
            {
                TriggerAnimator("getup");
                fallRecoveryTriggered = true;
            }
            return;
        }
        if (stunUntil > 0) { stunUntil = 0; fallRecoveryTriggered = false; hurdleResolved = true; PrepareNextHurdle(); }
        if (jumping) UpdateJump();
        float remaining = Mathf.Max(0, hurdleNumber * hurdleSpacing - distanceAlongTrack);
        float timeToHurdle = remaining / CurrentRunSpeed;
        if (!hurdlePrompted && timeToHurdle <= inputLeadTime)
        {
            hurdlePrompted = true;
            arrowProgress=0;lastArrowPromptProgress=-1;lastArrowPromptTenths=-1;
            for(int i=0;i<expectedDirections.Length;i++)expectedDirections[i]=UnityEngine.Random.Range(0,Keys.Length);
        }
        if (hurdlePrompted && !hurdleResolved)
        {
            ShowInstruction(timeToHurdle);
            // Resolve this hurdle before advancing to the next one in ReadArrowInput.
            if(timeToHurdle<=.02f)ResolveHurdle(false);
            else ReadArrowInput();
        }
        float speed = stunUntil > 0 ? 0 : CurrentRunSpeed;
        distanceAlongTrack = Mathf.Min(RaceDistance, distanceAlongTrack + speed * Time.deltaTime);
        if (queuedJump && distanceAlongTrack >= queuedJumpAt)
        {
            queuedJump = false; jumping = true; jumpStartedAt = Time.time;
            PulseCameraShake(.07f,.16f);
            if(activeAnimator!=null&&activeAnimator.parameters.Any(p=>p.name=="doJump"&&p.type==AnimatorControllerParameterType.Trigger))activeAnimator.SetTrigger("doJump");
        }
        int movingLane=Mathf.Max(0,objectIds.IndexOf(activePlayer.NetworkObjectId));
        float laneDistance=StartOffsetForLane(movingLane)+distanceAlongTrack;
        Vector3 position=TrackPosition(laneDistance,movingLane);
        if (jumping) position.y += Mathf.Sin(Mathf.Clamp01((Time.time - jumpStartedAt) / .8f) * Mathf.PI) * 1.8f;
        activePlayer.transform.SetPositionAndRotation(position,TrackRotation(laneDistance,movingLane));
        SetWalking(speed > 0 && !jumping);
        if (distanceAlongTrack >= RaceDistance) { localFinished = true; PulseCameraShake(.13f,.24f); SetWalking(false); instructionText.text = "완주! 다른 주자를 기다리는 중"; }
    }

    private void ReadArrowInput()
    {
        for(int i=0;i<Keys.Length;i++)
        {
            if(!Input.GetKeyDown(Keys[i]))continue;
            if(i!=expectedDirections[arrowProgress]){ResolveHurdle(false);return;}
            arrowProgress++;
            if(arrowProgress>=ArrowInputsPerHurdle)ResolveHurdle(true);
            return;
        }
    }
    private void ResolveHurdle(bool success)
    {
        if (hurdleResolved) return;
        hurdleResolved = true;
        int lane = Mathf.Max(0, objectIds.IndexOf(activePlayer.NetworkObjectId));
        int hurdleKey = hurdleNumber * 4 + lane;
        if (!success && hurdles.TryGetValue(hurdleKey, out var hurdle) && hurdle != null)
        { foreach (var collider in hurdle.GetComponentsInChildren<Collider>()) collider.enabled = false; }
        if (success)
        {
            speedBoostUntil=Time.time+QteSuccessBoostDuration;
            queuedJump = true;
            queuedJumpAt = hurdleNumber * hurdleSpacing - CurrentRunSpeed * .4f;
            instructionText.text = "<color=#69EFA0>점프!</color>";
            PrepareNextHurdle();
        }
        else
        {
            speedBoostUntil=0;
            stunUntil = Time.unscaledTime + failStopDuration;
            fallRecoveryTriggered = false;
            PulseCameraShake(.2f,.3f);
            SetWalking(false);
            TriggerAnimator("fall");
            // Keep the missed hurdle on the track, tip it backward, and leave it translucent.
            if (hurdles.TryGetValue(hurdleKey, out var missed) && missed != null)
                StartCoroutine(TipAndFadeHurdle(missed));
            instructionText.text = "<color=#FF625C>실패!</color>";
        }
    }
    private IEnumerator TipAndFadeHurdle(GameObject hurdle)
    {
        var renderers = hurdle.GetComponentsInChildren<Renderer>();
        var renderMaterials = new List<Material>();
        foreach (var renderer in renderers)
        foreach (var material in renderer.materials)
        {
            if (material == null) continue;
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000;
            renderMaterials.Add(material);
        }
        Quaternion upright = hurdle.transform.localRotation;
        Quaternion fallenBack = upright * Quaternion.Euler(-76f, 0, 0);
        float elapsed = 0;
        const float toppleDuration = .38f;
        const float fadeDuration = .55f;
        while (elapsed < fadeDuration && hurdle != null)
        {
            elapsed += Time.deltaTime;
            float tip = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / toppleDuration));
            hurdle.transform.localRotation = Quaternion.Slerp(upright, fallenBack, tip);
            float alpha = Mathf.Lerp(1f, .38f, Mathf.Clamp01(elapsed / fadeDuration));
            foreach (var material in renderMaterials) if (material != null) { var color = material.color; color.a = alpha; material.color = color; }
            yield return null;
        }
    }
    private void TriggerAnimator(string parameterName)
    {
        if (activeAnimator == null) return;
        if (activeAnimator.parameters.Any(parameter => parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger))
            activeAnimator.SetTrigger(parameterName);
    }
    private void UpdateJump()
    {
        if (Time.time - jumpStartedAt < .8f)return;
        jumping=false;
        PulseCameraShake(.12f,.2f);
    }
    private void PrepareNextHurdle()
    {
        float hurdleAt=(hurdleNumber+1)*hurdleSpacing;
        if(hurdleAt>RaceDistance){hurdlePrompted=false;hurdleResolved=true;return;}
        hurdleNumber++;hurdlePrompted=false;hurdleResolved=false;
        SpawnHurdle(hurdleNumber,hurdleAt);
    }
    private void ShowInstruction(float secondsLeft)
    {
        int tenths=Mathf.CeilToInt(Mathf.Max(0,secondsLeft)*10f);
        if(lastArrowPromptProgress==arrowProgress&&lastArrowPromptTenths==tenths)return;
        lastArrowPromptProgress=arrowProgress;lastArrowPromptTenths=tenths;
        var sequence=new string[ArrowInputsPerHurdle];
        for(int i=0;i<sequence.Length;i++)
        {
            string arrow=Arrows[expectedDirections[i]];
            sequence[i]=i<arrowProgress?$"<color=#69EFA0>{arrow}</color>":i==arrowProgress?$"<color=#FFD55A><b>{arrow}</b></color>":$"<color=#D6DDE7>{arrow}</color>";
        }
        instructionText.text=$"허들까지 {secondsLeft:0.0}초 · 방향키 4개 순서 입력\n{string.Join("  ",sequence)}\n<color=#AFC4D8>{arrowProgress}/{ArrowInputsPerHurdle}</color>";
    }
    private void UpdateCountdown()
    {
        if(startsAt<0||network==null)
        {
            overviewMode=true;
            SetCountdownText(string.Empty);
            return;
        }
        double now=network.ServerTime.Time;
        double countdownAt=startsAt-RaceCountdownSeconds;
        overviewMode=now<countdownAt-CameraTransitionDuration;
        if(!overviewMode&&!hurdlePreviewSpawned)
        {
            int totalHurdles=Mathf.FloorToInt(RaceDistance/hurdleSpacing);
            for(int hurdle=1;hurdle<=totalHurdles;hurdle++)SpawnHurdle(hurdle,hurdle*hurdleSpacing);
            hurdlePreviewSpawned=true;
        }
        if(now<countdownAt)
        {
            SetCountdownText(string.Empty);
            return;
        }
        int tick=Mathf.CeilToInt((float)(startsAt-now)-.001f);
        if(tick>0&&tick<=RaceCountdownSeconds)
        {
            SetCountdownText($"<color=#FFD55A>{tick}</color>");
            PlayCountdownTone(tick);
        }
        else SetCountdownText(string.Empty);
    }
    private void SetCountdownText(string value)
    {
        if(sharedCountdownText!=null)
        {
            sharedCountdownText.text=gameStarted?string.Empty:value;
            if(countdownText!=null)
            {
                bool showGo=gameStarted&&!string.IsNullOrEmpty(value);
                countdownText.gameObject.SetActive(showGo);
                countdownText.text=showGo?value:string.Empty;
            }
            return;
        }
        if(countdownText!=null)countdownText.text=value;
    }
    private int lastTick = -1;
    private void PlayCountdownTone(int tick) { if (tick == lastTick) return; lastTick = tick; PlayTone(tick == 1 ? 1250 : 760, .12f); }
    private void PlayWhistle()
    {
        const float duration = .72f;
        const int rate = 22050;
        int count = Mathf.CeilToInt(rate * duration);
        var clip = AudioClip.Create("Starting whistle", count, 1, rate, false);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float phase = 2f * Mathf.PI * (1900f * t - 325f * t * t / duration);
            float envelope = Mathf.Min(1f, t * 35f) * Mathf.Min(1f, (duration - t) * 18f);
            samples[i] = (Mathf.Sin(phase) + .18f * Mathf.Sin(phase * 2.02f)) * envelope * .2f;
        }
        clip.SetData(samples, 0);
        whistleSource.PlayOneShot(clip);
    }
    private void StartSchoolSportsMusic()
    {
        const int sampleRate=22050;
        const float bpm=140f;
        const int bars=16;
        float beatLength=60f/bpm;
        float duration=bars*4f*beatLength;
        int sampleCount=Mathf.CeilToInt(duration*sampleRate);
        var samples=new float[sampleCount*2];
        var frequencies=new float[128];
        for(int note=0;note<frequencies.Length;note++)frequencies[note]=440f*Mathf.Pow(2f,(note-69)/12f);
        int[] melody={72,76,79,76,81,79,76,72,69,72,77,72,76,77,76,72,69,72,76,72,76,79,81,79,67,71,74,71,79,74,71,67,72,76,79,81,79,76,72,76,81,84,81,79,76,79,81,79,76,79,84,79,83,84,83,79,74,79,83,79,81,83,86,83,72,76,79,76};
        int[] chordRoots={60,53,57,55};
        var random=new System.Random(1847);
        for(int i=0;i<sampleCount;i++)
        {
            float t=i/(float)sampleRate;
            float beatPosition=t/beatLength;
            int beatIndex=Mathf.FloorToInt(beatPosition);
            float beatPhase=beatPosition-beatIndex;
            int bar=Mathf.FloorToInt(beatPosition/4f);
            int root=chordRoots[bar%chordRoots.Length];

            float eighthLength=beatLength*.5f;
            int noteIndex=Mathf.FloorToInt(t/eighthLength)%melody.Length;
            float noteTime=t%eighthLength;
            int midi=melody[noteIndex];
            float lead=0f;
            if(midi>0)
            {
                float frequency=frequencies[midi];
                float phase=2f*Mathf.PI*frequency*noteTime;
                float noteEnvelope=Mathf.Min(1f,noteTime/.025f)*Mathf.Clamp01((eighthLength-noteTime)/.075f);
                lead=(Mathf.Sin(phase)+.22f*Mathf.Sin(phase*2f)+.08f*Mathf.Sin(phase*3f))*noteEnvelope*.13f;
            }

            float pad=(Mathf.Sin(2f*Mathf.PI*frequencies[root]*t)
                +Mathf.Sin(2f*Mathf.PI*frequencies[root+4]*t)
                +Mathf.Sin(2f*Mathf.PI*frequencies[root+7]*t))*.018f;
            float bassFrequency=frequencies[root-12];
            float bass=Mathf.Sin(2f*Mathf.PI*bassFrequency*t)*Mathf.Exp(-beatPhase*5f)*.045f;

            float percussion=0f;
            float noise=(float)random.NextDouble()*2f-1f;
            float beatSeconds=beatPhase*beatLength;
            int beatInBar=beatIndex%4;
            if((beatInBar==0||beatInBar==2)&&beatSeconds<.24f)
                percussion+=Mathf.Sin(2f*Mathf.PI*(105f*beatSeconds-125f*beatSeconds*beatSeconds))*Mathf.Exp(-beatSeconds*17f)*.17f;
            if((beatInBar==1||beatInBar==3)&&beatSeconds<.14f)
                percussion+=(noise*.62f+Mathf.Sin(2f*Mathf.PI*220f*beatSeconds)*.38f)*Mathf.Exp(-beatSeconds*24f)*.085f;
            float eighthPhase=(t/eighthLength)-Mathf.Floor(t/eighthLength);
            if(eighthPhase<.045f)percussion+=noise*Mathf.Exp(-eighthPhase*62f)*.028f;
            float offbeatPhase=((t+beatLength*.5f)/beatLength)-Mathf.Floor((t+beatLength*.5f)/beatLength);
            if(offbeatPhase<.035f)percussion+=noise*Mathf.Exp(-offbeatPhase*72f)*.012f;

            float fade=Mathf.Clamp01(Mathf.Min(t/.045f,(duration-t)/.045f));
            float mix=(lead+pad+bass+percussion)*fade;
            samples[i*2]=mix+lead*.025f;
            samples[i*2+1]=mix-lead*.025f;
        }
        var clip=AudioClip.Create("School Sports Day Loop",sampleCount,2,sampleRate,false);
        clip.SetData(samples,0);
        musicSource=gameObject.AddComponent<AudioSource>();
        musicSource.clip=clip;musicSource.loop=true;musicSource.volume=.5f;musicSource.spatialBlend=0;musicSource.playOnAwake=false;
        musicSource.Play();
    }
    private void PlayTone(float hz, float duration)
    {
        int rate = 22050, count = Mathf.CeilToInt(rate * duration); var clip = AudioClip.Create("QTE whistle", count, 1, rate, false); var samples = new float[count];
        for (int i = 0; i < count; i++) { float t = i / (float)rate; float envelope = Mathf.Min(1, t * 35) * Mathf.Min(1, (duration - t) * 18); samples[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * envelope * .22f; }
        clip.SetData(samples, 0); whistleSource.PlayOneShot(clip);
    }
    private void SetWalking(bool moving) { if (activeAnimator != null) activeAnimator.SetBool("isMoving", moving); }
    private void FreezeRunnerPhysics(NetworkObject player)
    {
        foreach(var body in player.GetComponentsInChildren<Rigidbody>())
        {
            if(!runnerBodyStates.ContainsKey(body))runnerBodyStates.Add(body,new RunnerBodyState{isKinematic=body.isKinematic,useGravity=body.useGravity});
            if(!body.isKinematic){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.useGravity=false;body.isKinematic=true;
        }
    }
    private bool LocalRosterIsPresent()
    {
        if (!owners.Contains(network.LocalClientId)) return false;
        for (int i = 0; i < objectIds.Count; i++) if (!network.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i], out var p) || !p.IsPlayerObject || p.OwnerClientId != owners[i]) return false;
        return true;
    }
    private void SendReady()
    {
        localReady = true;
        if (network.IsServer) MarkReady(network.LocalClientId);
        else using (var w = new FastBufferWriter(4, Allocator.Temp)) { w.WriteValueSafe(entryToken); messages.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced); }
    }
    private void OnRosterReceived(ulong sender, FastBufferReader r)
    {
        if (sender != NetworkManager.ServerClientId || network.IsServer || rosterReceived) return;
        r.ReadValueSafe(out entryToken); r.ReadValueSafe(out int count); if (count < 1 || count > 4) { FailEntry("참가자 목록이 올바르지 않습니다."); return; }
        owners.Clear(); objectIds.Clear(); for (int i=0;i<count;i++){r.ReadValueSafe(out ulong owner);r.ReadValueSafe(out ulong obj);owners.Add(owner);objectIds.Add(obj);} rosterReceived=true;
    }
    private void OnReadyReceived(ulong sender, FastBufferReader r) { if (!network.IsServer || !rosterReceived || !owners.Contains(sender)) return; r.ReadValueSafe(out int token); if(token==entryToken)MarkReady(sender); }
    private void MarkReady(ulong id)
    {
        if(!readyClients.Add(id))return;
        using(var w=new FastBufferWriter(12,Allocator.Temp)){w.WriteValueSafe(entryToken);w.WriteValueSafe(id);foreach(ulong owner in owners)if(owner!=NetworkManager.ServerClientId&&network.ConnectedClients.ContainsKey(owner))messages.SendNamedMessage(ReadyStatusMessage,owner,w,NetworkDelivery.ReliableSequenced);}
    }
    private void OnReadyStatusReceived(ulong sender, FastBufferReader r){if(sender!=NetworkManager.ServerClientId||network.IsServer)return;r.ReadValueSafe(out int token);r.ReadValueSafe(out ulong id);if(token==entryToken&&owners.Contains(id))readyClients.Add(id);}
    private void ScheduleStart()
    {
        startScheduled=true;
        startsAt=network.ServerTime.Time+PartyTicketTravel.RemainingReveal+MapOverviewDuration+CameraTransitionDuration+RaceCountdownSeconds;
        using(var w=new FastBufferWriter(12,Allocator.Temp)){w.WriteValueSafe(entryToken);w.WriteValueSafe(startsAt);foreach(ulong id in owners)if(id!=NetworkManager.ServerClientId&&network.ConnectedClients.ContainsKey(id))messages.SendNamedMessage(StartMessage,id,w,NetworkDelivery.ReliableSequenced);}
    }
    private void OnStartReceived(ulong sender,FastBufferReader r){if(sender!=NetworkManager.ServerClientId||network.IsServer)return;r.ReadValueSafe(out int token);r.ReadValueSafe(out double t);if(token==entryToken)startsAt=t;}

    private void LateUpdate()
    {
        if (raceCamera != null)
        {
            bool running=gameStarted&&activePlayer!=null&&!localFinished&&Time.unscaledTime>=stunUntil;
            bool speedBoosted=running&&Time.time<speedBoostUntil;
            float targetFov=jumping?70f:(running?(speedBoosted?69f:67f):58f);
            raceCamera.fieldOfView=Mathf.Lerp(raceCamera.fieldOfView,targetFov,Time.unscaledDeltaTime*3.2f);
            if (overviewMode) { /* Editor scene camera remains in its elevated overview while players get ready. */ }
            else if (activePlayer != null)
            {
                Vector3 target=activePlayer.transform.position-activePlayer.transform.forward*6.2f+Vector3.up*3.8f;
                if(running)
                {
                    float stride=Time.time*13f;
                    target+=Vector3.up*(Mathf.Sin(stride)*.065f);
                    target+=activePlayer.transform.right*(Mathf.Sin(stride*.5f)*.025f);
                }
                if(Time.unscaledTime<cameraShakeUntil)
                {
                    float fade=Mathf.Clamp01((cameraShakeUntil-Time.unscaledTime)/Mathf.Max(.01f,cameraShakeDuration));
                    float shake=cameraShakeStrength*fade;
                    target+=raceCamera.transform.right*(Mathf.Sin(Time.unscaledTime*92f)*shake);
                    target+=Vector3.up*(Mathf.Sin(Time.unscaledTime*117f)*shake*.55f);
                }
                raceCamera.transform.position = Vector3.SmoothDamp(raceCamera.transform.position, target, ref cameraVelocity, cameraFollowSmoothTime);
                raceCamera.transform.LookAt(activePlayer.transform.position+activePlayer.transform.forward*5.5f+Vector3.up*1.1f);
            }
        }
        RefreshStatus();
        CheckFinishScores();
    }
    private void PulseCameraShake(float strength,float duration)
    {
        if(Time.unscaledTime>=cameraShakeUntil)
        {
            cameraShakeStrength=strength;
            cameraShakeDuration=duration;
        }
        else
        {
            cameraShakeStrength=Mathf.Max(cameraShakeStrength,strength);
            cameraShakeDuration=Mathf.Max(cameraShakeDuration,duration);
        }
        cameraShakeUntil=Mathf.Max(cameraShakeUntil,Time.unscaledTime+duration);
    }
    private void CheckFinishScores()
    {
        if(network==null||!network.IsListening||!network.IsServer||!gameStarted||!rosterReceived)return;
        var ranking=RankingManager.Instance; bool canScore=ranking!=null&&ranking.IsSpawned;
        for(int i=0;i<objectIds.Count;i++)
        {
            ulong owner=owners[i];if(finishedPlayers.Contains(owner)||!network.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i],out var p))continue;
            // Host observes each owner-authoritative runner crossing the same oval's finish mark after one full lap.
            int slot=i;
            Vector3 start=TrackPosition(StartOffsetForLane(slot),slot);
            if(Vector3.Distance(p.transform.position,start)>StartClearance)playersPastStart.Add(owner);
            if(!playersPastStart.Contains(owner))continue;
            Vector3 finish=TrackPosition(FinishOffset,slot);
            if(Vector3.Distance(p.transform.position,finish)>2.75f)continue;
            int place=finishedPlayers.Count+1; if(canScore)ranking.AddScore(owner,$"Player {owner}",Mathf.Max(0,4-place)); finishedPlayers.Add(owner);
        }
        if(!roundEnding&&owners.Count>0&&owners.All(id=>!network.ConnectedClients.ContainsKey(id)||finishedPlayers.Contains(id)))
        {roundEnding=true;instructionText.text="모든 주자가 완주했습니다!";StartCoroutine(ReturnToRanking());}
    }
    private IEnumerator ReturnToRanking()
    {
        yield return new WaitForSecondsRealtime(3f);
        if(network==null||!network.IsListening||!network.IsServer||network.SceneManager==null)yield break;
        var result=network.SceneManager.LoadScene(PartyGameRotation.ResultScene(),LoadSceneMode.Single);
        if(result!=SceneEventProgressStatus.Started){roundEnding=false;entryError="랭킹 화면 이동 실패: "+result;}
    }
    private void RefreshStatus()
    {
        if(statusText==null)return;
        if(entryError!=null){statusText.text=entryError;return;}
        if(!rosterReceived){statusText.text="참가자 동기화 중";return;}
        var lines=new List<string>{"학교 운동회 · QTE 허들"};
        foreach(var id in owners){bool connected=!network.IsServer||network.ConnectedClients.ContainsKey(id);lines.Add($"P{id+1} {(connected?(readyClients.Contains(id)?"준비 완료":"동기화 중"):"연결 끊김")}");}
        statusText.text=string.Join("  ·  ",lines);
    }
    private void FailEntry(string reason){entryError=reason;Debug.LogError("[QTE 입장] "+reason);}
    private void OnDestroy()
    {
        foreach(var state in runnerBodyStates)
        {
            if(state.Key==null)continue;
            state.Key.isKinematic=state.Value.isKinematic;state.Key.useGravity=state.Value.useGravity;
            if(!state.Key.isKinematic){state.Key.velocity=Vector3.zero;state.Key.angularVelocity=Vector3.zero;}
        }
        if(network!=null&&network.IsServer&&network.SceneManager!=null)network.SceneManager.OnLoadEventCompleted-=OnSceneLoaded;
        if(messages!=null){messages.UnregisterNamedMessageHandler(RosterMessage);messages.UnregisterNamedMessageHandler(ReadyMessage);messages.UnregisterNamedMessageHandler(ReadyStatusMessage);messages.UnregisterNamedMessageHandler(StartMessage);}
    }

    private Vector3 TrackPosition(float distance, int lane)
    {
        float r=TurnRadius-lane*1.4f, straight=StraightLength; float lap=2*straight+2*Mathf.PI*r; float d=Mathf.Repeat(distance,lap);
        if(d<straight)return new Vector3(-straight/2+d, .08f, -r);
        d-=straight; float arc=r*Mathf.PI;
        if(d<arc){float a=-Mathf.PI/2+d/r;return new Vector3(straight/2+r*Mathf.Cos(a),.08f, r*Mathf.Sin(a));}
        d-=arc;if(d<straight)return new Vector3(straight/2-d,.08f,r);
        d-=straight;float a2=Mathf.PI/2+d/r;return new Vector3(-straight/2+r*Mathf.Cos(a2),.08f,r*Mathf.Sin(a2));
    }
    private float PlayerLapLength(int lane) => 2f * StraightLength + 2f * Mathf.PI * (TurnRadius - lane * 1.4f);
    private float StartOffsetForLane(int lane) => PlayerLapLength(lane) - PlayerLapLength(3);
    private Quaternion TrackRotation(float distance,int lane=0)
    {
        Vector3 ahead=TrackPosition(distance+.15f,lane)-TrackPosition(distance,lane);return ahead.sqrMagnitude>.001f?Quaternion.LookRotation(ahead.normalized,Vector3.up):Quaternion.identity;
    }
    private void BuildSchoolTrack()
    {
        RenderSettings.ambientLight=new Color(.68f,.72f,.78f); RenderSettings.fog=false;
        var grass=MakeMat("School field",new Color(.22f,.48f,.24f));var track=MakeMat("Track terracotta",new Color(.66f,.22f,.16f));var lane=MakeMat("Track cream lines",new Color(.95f,.85f,.66f));var turf=MakeMat("Football turf",new Color(.16f,.42f,.19f));
        Cube("School ground",new Vector3(0,-.28f,0),new Vector3(620,.5f,520),grass);
        MakeOvalRibbon("Running track",0,0,15.2f,21.5f,track);
        for(int i=0;i<5;i++)MakeOvalLine("Lane marking",0,0,21.3f-i*1.4f,lane,.045f);
        // Keep the football pitch inside the inner lane boundary so the grass never covers the running track.
        Cube("Football field grass",new Vector3(0,.005f,0),new Vector3(70,.05f,30),turf);
        for(int x=-32;x<=32;x+=8)Cube("Football pitch stripe",new Vector3(x,.04f,0),new Vector3(4,.02f,27),new Color(.18f,.46f,.2f));
        Cube("Pitch halfway line",new Vector3(0,.04f,0),new Vector3(.16f,.03f,27),lane);
        var circle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);circle.name="Center circle";circle.transform.position=new Vector3(0,.06f,0);circle.transform.localScale=new Vector3(10,.025f,10);circle.GetComponent<Renderer>().sharedMaterial=lane;RemoveCollider(circle);
        foreach(float end in new[]{-31f,31f})
        {
            float inward=Mathf.Sign(end);
            Cube("Goal box line",new Vector3(end,.052f,0),new Vector3(.12f,.03f,14),lane);
            Cube("Goal box side",new Vector3(end-inward*4f,.052f,-7),new Vector3(8,.03f,.12f),lane);
            Cube("Goal box side",new Vector3(end-inward*4f,.052f,7),new Vector3(8,.03f,.12f),lane);
            BuildFootballGoal(end,inward);
        }
        for(int i=0;i<4;i++){float start=StartOffsetForLane(i);var spawn=new GameObject("QTE Spawn "+(i+1)).transform;spawn.position=TrackPosition(start,i);spawn.rotation=TrackRotation(start,i);}
        BuildRaceStartAndFinish();
        BuildSchoolBuilding();
        BuildBleachersAndSpectators();
        hurdleNumber=0;
    }
    private void BuildFootballGoal(float end,float inward)
    {
        var white=MakeMat("Football goal enamel white",new Color(.94f,.96f,.92f));
        var net=MakeMat("Football goal net soft gray",new Color(.76f,.83f,.80f));
        float goalX=end-inward*.14f;
        foreach(float z in new[]{-3.66f,3.66f})
            Cube("Football goal upright",new Vector3(goalX,1.22f,z),new Vector3(.16f,2.44f,.16f),white);
        Cube("Football goal crossbar",new Vector3(goalX,2.44f,0),new Vector3(.16f,.16f,7.32f),white);
        float backX=end+inward*2.1f;
        foreach(float z in new[]{-3.66f,3.66f})
            Cube("Football goal net side",new Vector3((goalX+backX)*.5f,1.18f,z),new Vector3(2.1f,2.36f,.045f),net);
        Cube("Football goal net roof",new Vector3((goalX+backX)*.5f,2.4f,0),new Vector3(2.1f,.045f,7.28f),net);
        for(int row=1;row<5;row++)
            Cube("Goal net vertical weave",new Vector3(backX,.12f+row*.46f,0),new Vector3(.04f,.025f,7.25f),net);
        for(int column=1;column<9;column++)
            Cube("Goal net horizontal weave",new Vector3(backX,.08f,column*.8f-3.6f),new Vector3(.04f,2.3f,.025f),net);
        foreach(float z in new[]{-3.2f,3.2f})
            Cylinder("Football goal ground anchor",new Vector3(goalX,.08f,z),.12f,.16f,new Color(.72f,.74f,.69f));
    }
    private void BuildRaceStartAndFinish()
    {
        var white=MakeMat("Race line white",new Color(.98f,.97f,.88f));
        var startGold=MakeMat("Race start line gold",new Color(1f,.79f,.18f));
        var finishRed=MakeMat("Race finish line red",new Color(.82f,.12f,.12f));
        float laneWidth=1.4f;
        for(int lane=0;lane<4;lane++)
        {
            float startDistance=StartOffsetForLane(lane);
            Vector3 start=TrackPosition(startDistance,lane);
            start.y=.045f;
            Quaternion startRotation=TrackRotation(startDistance,lane);
            var startLine=Cube($"Lane {lane+1} staggered start line",start,new Vector3(laneWidth*.9f,.045f,.24f),startGold);
            startLine.transform.rotation=startRotation;
            foreach(float ahead in new[]{-.21f,.21f})
            {
                Vector3 edge=start+startRotation*new Vector3(0,0,ahead);
                edge.y=.047f;
                var edgeMark=Cube("Staggered start line white edge",edge,new Vector3(laneWidth*.9f,.035f,.10f),white);
                edgeMark.transform.rotation=startRotation;
            }
            AddLaneNumberMarking("Start",lane,startDistance-1.35f,new Color(1f,.86f,.32f));
        }
        float finishX=-StraightLength*.5f+FinishOffset;
        float trackCenterZ=-TurnRadius+1.5f*laneWidth;
        for(int row=0;row<2;row++)
        for(int tile=0;tile<8;tile++)
        {
            float z=trackCenterZ-2.8f+tile*.8f;
            float x=finishX-.24f+row*.48f;
            Cube("Finish checker",new Vector3(x,.045f,z),new Vector3(.48f,.05f,.8f),(tile+row)%2==0?white:finishRed);
        }
        for(int lane=0;lane<4;lane++)AddLaneNumberMarking("Finish",lane,FinishOffset-1.35f,white.color);
        foreach(float z in new[]{trackCenterZ-3.5f,trackCenterZ+3.5f})
            Cube("Finish arch post",new Vector3(finishX,2.1f,z),new Vector3(.32f,4.2f,.32f),finishRed);
        Cube("Finish arch header",new Vector3(finishX,4.25f,trackCenterZ),new Vector3(.42f,.42f,7.35f),finishRed);
        Cube("Finish arch white band",new Vector3(finishX-.02f,4.25f,trackCenterZ),new Vector3(.45f,.12f,7.4f),white);
        var labelObject=new GameObject("Finish arch label",typeof(TextMeshPro));
        var label=labelObject.GetComponent<TextMeshPro>();
        label.font=koreanFont;label.text="FINISH";label.fontSize=2.2f;label.fontStyle=FontStyles.Bold;
        label.alignment=TextAlignmentOptions.Center;label.color=white.color;label.enableWordWrapping=false;
        labelObject.transform.SetPositionAndRotation(new Vector3(finishX-.25f,4.25f,trackCenterZ),Quaternion.Euler(0,270,0));
        labelObject.transform.localScale=new Vector3(-1,1,1);
    }
    private void AddLaneNumberMarking(string lineName,int lane,float distance,Color color)
    {
        Vector3 position=TrackPosition(distance,lane);
        position.y=.075f;
        Vector3 runningDirection=TrackRotation(distance,lane)*Vector3.forward;
        var marker=new GameObject($"{lineName} Lane {lane+1} Number",typeof(TextMeshPro));
        var text=marker.GetComponent<TextMeshPro>();
        text.font=koreanFont!=null?koreanFont:TMP_Settings.defaultFontAsset;
        text.text=(lane+1).ToString();
        text.fontSize=1.15f;
        text.fontStyle=FontStyles.Bold;
        text.alignment=TextAlignmentOptions.Center;
        text.color=color;
        text.outlineColor=new Color(.12f,.14f,.15f);
        text.outlineWidth=.18f;
        text.enableWordWrapping=false;
        marker.transform.SetPositionAndRotation(position,Quaternion.LookRotation(Vector3.up,runningDirection));
        var renderer=marker.GetComponent<MeshRenderer>();
        if(renderer!=null)renderer.receiveShadows=false;
    }
    private void BuildSchoolBuilding()
    {
        var wall=MakeMat("School warm ivory facade",new Color(.78f,.75f,.66f));
        var trim=MakeMat("School cream trim",new Color(.91f,.87f,.76f));
        var roof=MakeMat("School muted brick roof",new Color(.38f,.24f,.20f));
        var glass=MakeMat("School blue window glass",new Color(.16f,.34f,.43f));
        var door=MakeMat("School entrance door",new Color(.27f,.20f,.15f));
        var accent=MakeMat("School entry accent",new Color(.72f,.42f,.26f));

        // A broad three-storey main hall with lower classroom wings and a connected corridor.
        Cube("School main building",new Vector3(0,10,34),new Vector3(34,20,14),wall);
        Cube("School west classroom wing",new Vector3(-30,8,34),new Vector3(23,16,12.5f),wall);
        Cube("School east classroom wing",new Vector3(30,8,34),new Vector3(23,16,12.5f),wall);
        Cube("School west corridor",new Vector3(-21.5f,7.2f,34),new Vector3(6,14.4f,12),trim);
        Cube("School east corridor",new Vector3(21.5f,7.2f,34),new Vector3(6,14.4f,12),trim);

        BuildSchoolGableRoof("Main hall",0,34,35,14,20.15f,3.1f,roof,trim);
        BuildSchoolGableRoof("West wing",-30,34,24,12.5f,16.15f,2.3f,roof,trim);
        BuildSchoolGableRoof("East wing",30,34,24,12.5f,16.15f,2.3f,roof,trim);
        Cube("Main hall floor band",new Vector3(0,10.1f,27.0f),new Vector3(33.8f,0.32f,.28f),trim);
        Cube("West wing floor band",new Vector3(-30,8.2f,27.72f),new Vector3(22.8f,0.28f,.25f),trim);
        Cube("East wing floor band",new Vector3(30,8.2f,27.72f),new Vector3(22.8f,0.28f,.25f),trim);
        Cube("Main hall third floor cornice",new Vector3(0,15.1f,26.78f),new Vector3(34.6f,.34f,.35f),trim);
        Cube("Main hall upper cornice",new Vector3(0,19.55f,26.78f),new Vector3(34.6f,.42f,.42f),trim);
        Cube("Main hall front canopy gutter",new Vector3(0,19.95f,26.86f),new Vector3(35.2f,.22f,.3f),trim);
        foreach(float wingX in new[]{-30f,30f})
        {
            Cube("School classroom wing upper cornice",new Vector3(wingX,15.5f,27.7f),new Vector3(23.6f,.34f,.32f),trim);
            Cube("School classroom wing front canopy gutter",new Vector3(wingX,15.95f,27.75f),new Vector3(24.3f,.2f,.28f),trim);
        }
        Cube("Main hall lower plinth",new Vector3(0,.7f,26.75f),new Vector3(34.5f,1.3f,.5f),new Color(.63f,.60f,.53f));
        Cube("School sign plaque",new Vector3(0,8.35f,26.48f),new Vector3(11.4f,1.55f,.2f),new Color(.19f,.31f,.34f));
        Cube("School sign accent",new Vector3(0,7.47f,26.42f),new Vector3(11.8f,.12f,.28f),accent);
        foreach(float x in new[]{-16.2f,16.2f})
        {
            Cube("School facade pilaster",new Vector3(x,10.1f,26.84f),new Vector3(.24f,18.8f,.3f),trim);
            Cube("School roof drain",new Vector3(x,19.3f,27.1f),new Vector3(.16f,1.6f,.16f),new Color(.37f,.42f,.42f));
        }

        foreach(float y in new[]{3f,9.5f,16f})
            foreach(float x in new[]{-13.5f,-8.5f,8.5f,13.5f}) AddSchoolWindow(x,y,26.84f,glass,trim);
        foreach(float wingX in new[]{-30f,30f})
            foreach(float y in new[]{3f,10.5f})
                foreach(float offset in new[]{-8f,-3f,3f,8f}) AddSchoolWindow(wingX+offset,y,27.68f,glass,trim);

        // Recessed, covered entrance facing the track.
        Cube("School entrance porch",new Vector3(0,2.8f,26f),new Vector3(12,5.6f,2.8f),trim);
        Cube("School double front door",new Vector3(0,2.2f,24.35f),new Vector3(4.4f,4.4f,.2f),door);
        Cube("School door glass",new Vector3(0,3.2f,24.23f),new Vector3(3.5f,1.6f,.1f),glass);
        Cube("School entrance canopy",new Vector3(0,5.8f,25.3f),new Vector3(13,.48f,2.6f),accent);
        Cube("School entrance canopy soffit",new Vector3(0,5.48f,25.25f),new Vector3(11.8f,.12f,2.3f),trim);
        foreach(float x in new[]{-5.6f,5.6f}) Cube("School entrance column",new Vector3(x,3.2f,24.7f),new Vector3(.55f,6.4f,.7f),trim);
        Cube("School front step upper landing",new Vector3(0,.45f,24.45f),new Vector3(15,.34f,.8f),trim);
        Cube("School front step middle",new Vector3(0,.25f,23.75f),new Vector3(16,.3f,.6f),trim);
        Cube("School front step lower",new Vector3(0,.1f,23.25f),new Vector3(17,.18f,.4f),trim);
        var gardenSoil=MakeMat("School entrance garden soil",new Color(.25f,.34f,.22f));
        foreach(float x in new[]{-9f,9f})
        {
            Cube("School entrance planter",new Vector3(x,.5f,24.4f),new Vector3(3.8f,.9f,1.8f),trim);
            Cube("School entrance shrub bed",new Vector3(x,.98f,24.4f),new Vector3(3.35f,.16f,1.5f),gardenSoil);
            for(int plant=0;plant<5;plant++)
                Sphere("School entrance clipped shrub",new Vector3(x-1.3f+plant*.65f,1.45f+(plant%2)*.1f,24.4f),.48f,plant%2==0?new Color(.18f,.39f,.24f):new Color(.26f,.48f,.28f));
        }
        var clock=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        clock.name="School main hall clock";clock.transform.SetPositionAndRotation(new Vector3(0,18.1f,26.45f),Quaternion.Euler(90,0,0));clock.transform.localScale=new Vector3(1.05f,.16f,1.05f);clock.GetComponent<Renderer>().sharedMaterial=trim;RemoveCollider(clock);
        Cube("School clock hand vertical",new Vector3(0,18.1f,26.32f),new Vector3(.065f,.52f,.06f),door);
        Cube("School clock hand horizontal",new Vector3(.12f,18.22f,26.31f),new Vector3(.34f,.065f,.06f),door);
        Cylinder("School roof flagpole",new Vector3(0,25.4f,34),.075f,5.1f,new Color(.34f,.38f,.37f));
        Cube("School roof pennant",new Vector3(1.05f,26.7f,34),new Vector3(2.1f,1.35f,.1f),new Color(.94f,.9f,.77f));
        Cube("School roof pennant blue stripe",new Vector3(1.05f,26.7f,33.92f),new Vector3(1.72f,.16f,.06f),new Color(.2f,.38f,.55f));
        Cube("School roof pennant red mark",new Vector3(.72f,26.7f,33.88f),new Vector3(.32f,.32f,.05f),accent);

        var signObject=new GameObject("School name sign",typeof(TextMeshPro));
        var sign=signObject.GetComponent<TextMeshPro>();
        sign.font=koreanFont;sign.text="한빛중학교";sign.fontSize=3.8f;sign.fontStyle=FontStyles.Bold;sign.alignment=TextAlignmentOptions.Center;sign.color=new Color(.97f,.91f,.73f);sign.enableWordWrapping=false;
        signObject.transform.position=new Vector3(0,8.35f,26.30f);signObject.transform.rotation=Quaternion.Euler(0,180,0);signObject.transform.localScale=new Vector3(-1,1,1);
        var eventSignObject=new GameObject("School sports day banner",typeof(TextMeshPro));
        var eventSign=eventSignObject.GetComponent<TextMeshPro>();eventSign.font=koreanFont;eventSign.text="오늘은 학교 체육대회";eventSign.fontSize=1.15f;eventSign.fontStyle=FontStyles.Bold;
        eventSign.alignment=TextAlignmentOptions.Center;eventSign.color=new Color(.28f,.18f,.13f);eventSign.enableWordWrapping=false;
        eventSignObject.transform.SetPositionAndRotation(new Vector3(0,5.82f,24.12f),Quaternion.Euler(0,180,0));eventSignObject.transform.localScale=new Vector3(-1,1,1);
    }
    private void BuildSchoolGableRoof(string label,float centerX,float centerZ,float width,float depth,float eaveY,float rise,Material roof,Material trim)
    {
        float halfDepth=depth*.5f;
        float halfSlopeLength=Mathf.Sqrt(halfDepth*halfDepth+rise*rise);
        float angle=Mathf.Atan2(rise,halfDepth)*Mathf.Rad2Deg;
        var front=Cube(label+" front pitched roof",new Vector3(centerX,eaveY+rise*.5f,centerZ-halfDepth*.5f),new Vector3(width+.9f,.48f,halfSlopeLength+.35f),roof);
        front.transform.rotation=Quaternion.Euler(-angle,0,0);
        var back=Cube(label+" back pitched roof",new Vector3(centerX,eaveY+rise*.5f,centerZ+halfDepth*.5f),new Vector3(width+.9f,.48f,halfSlopeLength+.35f),roof);
        back.transform.rotation=Quaternion.Euler(angle,0,0);
        Cube(label+" roof ridge",new Vector3(centerX,eaveY+rise,centerZ),new Vector3(width+1f,.42f,.72f),roof);
        Cube(label+" front eave",new Vector3(centerX,eaveY,centerZ-halfDepth+.05f),new Vector3(width+1.3f,.35f,.42f),trim);
        Cube(label+" rear eave",new Vector3(centerX,eaveY,centerZ+halfDepth-.05f),new Vector3(width+1.3f,.35f,.42f),trim);
    }
    private void BuildBleachersAndSpectators()
    {
        var concrete=MakeMat("Bleacher concrete",new Color(.48f,.54f,.52f));
        var seatWood=MakeMat("Bleacher weathered timber",new Color(.48f,.38f,.27f));
        var rail=MakeMat("Bleacher painted green rail",new Color(.28f,.46f,.39f));
        Cube("Bleacher foundation",new Vector3(0,.08f,-31),new Vector3(76,.3f,17),concrete);
        for(int row=0;row<8;row++)
        {
            float z=-24.8f-row*1.65f;
            float top=.48f+row*.42f;
            Cube($"Bleacher step {row+1}",new Vector3(0,top-.11f,z),new Vector3(74,.22f,1.35f),seatWood);
            Cube($"Bleacher riser {row+1}",new Vector3(0,top-.34f,z+.61f),new Vector3(74,.48f,.16f),concrete);
        }
        // A low guardrail at the track edge and handrails along the aisles match the reference stands.
        Cube("Bleacher front handrail",new Vector3(0,1.25f,-23.8f),new Vector3(75,.12f,.12f),rail);
        for(int i=0;i<=16;i++)
        {
            float x=-37f+i*4.625f;
            Cube("Bleacher front rail post",new Vector3(x,.72f,-23.8f),new Vector3(.12f,1.15f,.12f),rail);
        }
        foreach(float x in new[]{-37f,37f})
        {
            Cube("Bleacher side handrail",new Vector3(x,2.05f,-31f),new Vector3(.12f,.12f,15.5f),rail);
            for(int i=0;i<6;i++)Cube("Bleacher side rail post",new Vector3(x,.95f+i*.32f,-24.5f-i*2.5f),new Vector3(.12f,1.2f,.12f),rail);
        }

        if(spectatorPrefabs==null||spectatorPrefabs.Length==0)return;
        int[] seatedRows={1,3,5,7};
        var crowdRandom=new System.Random(80421);
        for(int rowIndex=0;rowIndex<seatedRows.Length;rowIndex++)
        {
            int row=seatedRows[rowIndex];
            float z=-24.8f-row*1.65f;
            float y=.52f+row*.42f;
            for(int seat=0;seat<12;seat++)
            {
                if(crowdRandom.NextDouble()<.17)continue;
                GameObject prefab=spectatorPrefabs[crowdRandom.Next(spectatorPrefabs.Length)];
                if(prefab==null)continue;
                float x=-33f+seat*6f+(float)(crowdRandom.NextDouble()*3.6-1.8);
                float staggeredZ=z+(float)(crowdRandom.NextDouble()*1.05-.525);
                float seatHeight=y+(float)(crowdRandom.NextDouble()*.16-.08);
                float facing=(float)(crowdRandom.NextDouble()*30-15);
                bool jumpCheer=crowdRandom.NextDouble()<.19;
                var audience=CreateSpectator(prefab,new Vector3(x,seatHeight,staggeredZ),Quaternion.Euler(0,facing,0));
                if(audience==null)continue;
                audience.name=$"Audience Row {row+1} Seat {seat+1}"+(jumpCheer?" Jump Cheer":"");
                audience.transform.localScale*=.84f+(float)crowdRandom.NextDouble()*.16f;
            }
        }
    }
    private GameObject CreateSpectator(GameObject prefab,Vector3 position,Quaternion rotation)
    {
#if UNITY_EDITOR
        if(!Application.isPlaying)
        {
            var instance=PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene()) as GameObject;
            if(instance!=null)
            {
                instance.transform.SetPositionAndRotation(position,rotation);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))renderer.receiveShadows=false;
            }
            return instance;
        }
#endif
        var audience=Instantiate(prefab,position,rotation);
        foreach(var renderer in audience.GetComponentsInChildren<Renderer>(true))renderer.receiveShadows=false;
        return audience;
    }
    private void SetSceneSpectatorsIdle()
    {
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if(!root.name.StartsWith("Audience Row ",StringComparison.Ordinal))continue;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.receiveShadows=false;
            var animator=root.GetComponentInChildren<Animator>();if(animator==null)continue;
            animator.applyRootMotion=false;
            foreach(var parameter in animator.parameters)
            {
                if(parameter.name=="isMoving"&&parameter.type==AnimatorControllerParameterType.Bool)animator.SetBool(parameter.name,false);
                else if(parameter.name=="idle"&&parameter.type==AnimatorControllerParameterType.Trigger)animator.SetTrigger(parameter.name);
                else if(parameter.type==AnimatorControllerParameterType.Trigger)animator.ResetTrigger(parameter.name);
            }
            animator.Play("Idle",0,0f);
            bool jumpCheer=root.name.Contains("Jump Cheer");
            if(jumpCheer)StartCoroutine(CheerJumpLoop(animator,UnityEngine.Random.Range(.3f,2.2f)));
        }
    }
    private IEnumerator CheerJumpLoop(Animator animator,float delay)
    {
        yield return new WaitForSeconds(delay);
        while(animator!=null&&animator.isActiveAndEnabled)
        {
            if(animator.parameters.Any(p=>p.name=="doJump"&&p.type==AnimatorControllerParameterType.Trigger))animator.SetTrigger("doJump");
            yield return new WaitForSeconds(UnityEngine.Random.Range(3.5f,6f));
        }
    }
    private void AddAudienceFillLights()
    {
        if(GameObject.Find("QTE Audience Fill Lights")!=null)return;
        var group=new GameObject("QTE Audience Fill Lights");
        group.transform.SetParent(transform,false);
        foreach(float x in new[]{-27f,-9f,9f,27f})
        {
            var lightObject=new GameObject("Soft front fill");
            lightObject.transform.SetParent(group.transform,false);
            lightObject.transform.localPosition=new Vector3(x,6.5f,-23f);
            var fill=lightObject.AddComponent<Light>();
            fill.type=LightType.Point;
            fill.color=new Color(1f,.88f,.72f);
            fill.intensity=1.5f;
            fill.range=20f;
            fill.shadows=LightShadows.None;
        }
    }
    private void AddSchoolWindow(float x,float y,float z,Material glass,Material trim)
    {
        Cube("School window surround",new Vector3(x,y,z),new Vector3(2.75f,2.05f,.28f),trim);
        Cube("School classroom window",new Vector3(x,y,z-.2f),new Vector3(2.42f,1.72f,.1f),glass);
        Cube("School window outer sill",new Vector3(x,y-1.02f,z-.12f),new Vector3(3.05f,.16f,.42f),trim);
        Cube("School window upper lintel",new Vector3(x,y+1.02f,z-.1f),new Vector3(2.98f,.14f,.36f),trim);
        Cube("Window vertical mullion",new Vector3(x,y,z-.29f),new Vector3(.11f,1.72f,.08f),trim);
        Cube("Window horizontal mullion",new Vector3(x,y,z-.29f),new Vector3(2.42f,.11f,.08f),trim);
        Cube("Window interior vertical divider",new Vector3(x-.63f,y,z-.27f),new Vector3(.055f,1.56f,.06f),trim);
        Cube("Window interior vertical divider",new Vector3(x+.63f,y,z-.27f),new Vector3(.055f,1.56f,.06f),trim);
    }
    public void BuildSchoolTrackForEditor()
    {
        if (GameObject.Find("School ground") == null) BuildSchoolTrack();
    }
    private void HidePrototypeSceneObjects()
    {
        foreach (string objectName in new[] { "Track", "FinishLine", "line 1", "line 2", "line 3", "Canvas", "EventSystem" })
        {
            var old = GameObject.Find(objectName);
            if (old != null && old != gameObject) old.SetActive(false);
        }
    }
    private void SpawnHurdle(int number,float at)
    {
        for(int lane=0;lane<4;lane++)
        {
            int key=number*4+lane;if(hurdles.ContainsKey(key))continue;
            float laneDistance=StartOffsetForLane(lane)+at;
            var root=new GameObject($"Hurdle {number} · Lane {lane+1}");root.transform.position=TrackPosition(laneDistance,lane);root.transform.rotation=TrackRotation(laneDistance,lane);
            var frame=MakeMat("Hurdle brushed aluminum",new Color(.63f,.70f,.69f));frame.SetFloat("_Metallic",.62f);frame.SetFloat("_Glossiness",.48f);
            var darkMetal=MakeMat("Hurdle dark metal joints",new Color(.20f,.25f,.25f));darkMetal.SetFloat("_Metallic",.72f);
            var rubber=MakeMat("Hurdle rubber feet",new Color(.10f,.12f,.12f));
            var topBar=MakeMat("Hurdle ivory crossbar",new Color(.93f,.92f,.84f));
            var safetyOrange=MakeMat("Hurdle safety orange",new Color(.91f,.24f,.10f));
            var reflective=MakeMat("Hurdle reflective silver",new Color(.94f,.83f,.46f));

            // Stable rubberized feet run along the athlete's direction; the frame spans the lane.
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*.56f;
                HurdleBox(root.transform,"Hurdle rubberized floor foot",new Vector3(x,.075f,0),new Vector3(.16f,.12f,.92f),rubber);
                HurdleTube(root.transform,"Hurdle aluminum upright",new Vector3(x,.12f,0),new Vector3(x,.82f,0),.043f,frame);
                HurdleTube(root.transform,"Hurdle front diagonal brace",new Vector3(x,.14f,-.40f),new Vector3(x,.68f,-.02f),.027f,frame);
                HurdleTube(root.transform,"Hurdle rear diagonal brace",new Vector3(x,.14f,.40f),new Vector3(x,.68f,.02f),.027f,frame);
                HurdleBox(root.transform,"Hurdle upright adjustment sleeve",new Vector3(x,.47f,0),new Vector3(.12f,.24f,.12f),darkMetal);
                HurdleBox(root.transform,"Hurdle crossbar end collar",new Vector3(x,.84f,0),new Vector3(.18f,.16f,.17f),darkMetal);
                HurdleBox(root.transform,"Hurdle safety end cap",new Vector3(x,.84f,0),new Vector3(.08f,.13f,.18f),safetyOrange);
                for(int mark=0;mark<3;mark++)
                    HurdleBox(root.transform,"Hurdle height adjustment mark",new Vector3(x,.27f+mark*.12f,-.064f),new Vector3(.045f,.035f,.012f),reflective);
            }
            HurdleBox(root.transform,"Hurdle ivory top bar",new Vector3(0,.84f,0),new Vector3(1.25f,.105f,.115f),topBar);
            foreach(float x in new[]{-.43f,-.15f,.15f,.43f})
                HurdleBox(root.transform,"Hurdle orange impact stripe",new Vector3(x,.84f,-.061f),new Vector3(.105f,.11f,.012f),safetyOrange);
            HurdleBox(root.transform,"Hurdle center reflective stripe",new Vector3(0,.84f,-.068f),new Vector3(.055f,.112f,.014f),reflective);
            HurdleBox(root.transform,"Hurdle front stabilizer bar",new Vector3(0,.12f,-.40f),new Vector3(1.18f,.09f,.11f),frame);
            HurdleBox(root.transform,"Hurdle rear stabilizer bar",new Vector3(0,.12f,.40f),new Vector3(1.18f,.09f,.11f),frame);
            HurdleBox(root.transform,"Hurdle number plate",new Vector3(0,1.02f,-.015f),new Vector3(.48f,.22f,.09f),safetyOrange);
            var numberObject=new GameObject("Hurdle number label",typeof(TextMeshPro));
            var numberLabel=numberObject.GetComponent<TextMeshPro>();
            numberLabel.font=koreanFont;numberLabel.text=$"{number:00}";numberLabel.fontSize=3f;numberLabel.fontStyle=FontStyles.Bold;
            numberLabel.alignment=TextAlignmentOptions.Center;numberLabel.color=new Color(.12f,.14f,.14f);numberLabel.enableWordWrapping=false;
            numberObject.transform.SetParent(root.transform,false);
            numberObject.transform.localPosition=new Vector3(0,1.02f,-.067f);
            numberObject.transform.localRotation=Quaternion.Euler(0,180,0);
            numberObject.transform.localScale=Vector3.one*.07f;
            hurdles[key]=root;
        }
    }
    private GameObject HurdleBox(Transform root,string label,Vector3 localPosition,Vector3 size,Material material)
    {
        var part=Cube(label,root.position,size,material);
        part.transform.SetParent(root,false);part.transform.localPosition=localPosition;part.transform.localRotation=Quaternion.identity;part.transform.localScale=size;
        return part;
    }
    private GameObject HurdleTube(Transform root,string label,Vector3 localStart,Vector3 localEnd,float radius,Material material)
    {
        var tube=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name=label;tube.transform.SetParent(root,false);
        Vector3 delta=localEnd-localStart;
        tube.transform.localPosition=(localStart+localEnd)*.5f;
        tube.transform.localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
        tube.transform.localScale=new Vector3(radius*2,delta.magnitude*.5f,radius*2);
        tube.GetComponent<Renderer>().sharedMaterial=material;RemoveCollider(tube);
        return tube;
    }
    private Material MakeMat(string name,Color color)
    {
        string key=name+"_"+ColorUtility.ToHtmlStringRGBA(color);
        if(materials.TryGetValue(key,out var cached)&&cached!=null)return cached;
        string path="Assets/Scenes/QteGenerated/"+key.Replace(" ","_")+".mat";
#if UNITY_EDITOR
        if(!Application.isPlaying)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing!=null){existing.color=color;materials[key]=existing;return existing;}
        }
#endif
        var created=new Material(Shader.Find("Standard")){name=name,color=color};
#if UNITY_EDITOR
        if(!Application.isPlaying)AssetDatabase.CreateAsset(created,path);
#endif
        materials[key]=created;return created;
    }
    private GameObject Cube(string name,Vector3 pos,Vector3 scale,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=pos;g.transform.localScale=scale;if(material!=null)g.GetComponent<Renderer>().sharedMaterial=material;return g;}
    private GameObject Cube(string name,Vector3 pos,Vector3 scale,Color color)=>Cube(name,pos,scale,MakeMat(name,color));
    private void Cylinder(string name,Vector3 pos,float radius,float height,Color color){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.position=pos;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=MakeMat(name,color);RemoveCollider(g);}
    private void Sphere(string name,Vector3 pos,float radius,Color color){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.position=pos;g.transform.localScale=Vector3.one*radius*2;g.GetComponent<Renderer>().sharedMaterial=MakeMat(name,color);RemoveCollider(g);}
    private void RemoveCollider(GameObject target)
    {
        var collider=target.GetComponent<Collider>();if(collider==null)return;
        if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
    }
    private void MakeOvalLine(string name,float cx,float cz,float radius,Material mat,float width){MakeOvalRibbon(name,cx,cz,radius-width/2,radius+width/2,mat);}
    private void MakeOvalRibbon(string name,float cx,float cz,float inner,float outer,Material mat)
    {
        int segments=160;var vertices=new Vector3[(segments+1)*2];var triangles=new int[segments*6];
        for(int i=0;i<=segments;i++){float phase=i/(float)segments;Vector3 a=OvalAtFraction(phase,inner),b=OvalAtFraction(phase,outer);vertices[i*2]=new Vector3(a.x,.015f,a.z);vertices[i*2+1]=new Vector3(b.x,.015f,b.z);if(i<segments){int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;}}
        string meshName=$"{name}_{inner:0.0}_{outer:0.0}";
        var mesh=new Mesh{name=meshName};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
#if UNITY_EDITOR
        if(!Application.isPlaying)
        {
            string path="Assets/Scenes/QteGenerated/"+meshName.Replace(" ","_")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){existing.Clear();existing.vertices=vertices;existing.triangles=triangles;existing.RecalculateNormals();DestroyImmediate(mesh);mesh=existing;}
            else AssetDatabase.CreateAsset(mesh,path);
        }
#endif
        var go=new GameObject(name);var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
    private Vector3 OvalAtFraction(float phase,float r){float d=phase*(2*StraightLength+2*Mathf.PI*r);if(d<StraightLength)return new Vector3(-StraightLength/2+d,0,-r);d-=StraightLength;float arc=r*Mathf.PI;if(d<arc){float a=-Mathf.PI/2+d/r;return new Vector3(StraightLength/2+r*Mathf.Cos(a),0,r*Mathf.Sin(a));}d-=arc;if(d<StraightLength)return new Vector3(StraightLength/2-d,0,r);d-=StraightLength;float a2=Mathf.PI/2+d/r;return new Vector3(-StraightLength/2+r*Mathf.Cos(a2),0,r*Mathf.Sin(a2));}
    private void BuildRaceUi()
    {
        var canvasGo=new GameObject("QTE Race HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        instructionText=Label(canvasGo.transform,"QTE Prompt",42,TextAlignmentOptions.Center);Anchor(instructionText.rectTransform,new Vector2(.5f,.18f),new Vector2(.5f,.18f),new Vector2(900,170));instructionText.color=Color.white;instructionText.text="허들에 맞춰 방향키를 누르세요";
        countdownText=Label(canvasGo.transform,"Race Countdown",88,TextAlignmentOptions.Center);Anchor(countdownText.rectTransform,new Vector2(.5f,.56f),new Vector2(.5f,.56f),new Vector2(700,280));
        statusText=Label(canvasGo.transform,"Race Status",21,TextAlignmentOptions.TopLeft);Anchor(statusText.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(850,45));statusText.rectTransform.anchoredPosition=new Vector2(22,-18);
        var bg=new GameObject("Prompt backing",typeof(RectTransform),typeof(Image));bg.transform.SetParent(canvasGo.transform,false);Anchor(bg.GetComponent<RectTransform>(),new Vector2(.5f,.18f),new Vector2(.5f,.18f),new Vector2(950,190));bg.GetComponent<RectTransform>().SetAsFirstSibling();bg.GetComponent<Image>().color=new Color(.04f,.08f,.1f,.78f);instructionText.transform.SetAsLastSibling();
    }
    private TextMeshProUGUI Label(Transform parent,string name,float size,TextAlignmentOptions align){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.font=koreanFont;t.fontSize=size;t.alignment=align;t.color=Color.white;t.enableWordWrapping=true;t.richText=true;t.raycastTarget=false;return t;}
    private void Anchor(RectTransform r,Vector2 min,Vector2 max,Vector2 size){r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;}
}
