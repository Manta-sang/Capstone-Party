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
    [SerializeField] private float cameraFollowSmoothTime = 0.22f;

    private const string RosterMessage = "QTE.Entry.Roster";
    private const string ReadyMessage = "QTE.Entry.Ready";
    private const string ReadyStatusMessage = "QTE.Entry.ReadyStatus";
    private const string StartMessage = "QTE.Entry.Start";
    private static readonly KeyCode[] Keys = { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
    private static readonly string[] Arrows = { "↑", "↓", "←", "→" };
    private const int ArrowInputsPerHurdle=4;

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
    private AudioSource whistleSource, musicSource;
    private Vector3 cameraVelocity;
    private int entryToken;
    private double startsAt = -1;
    public bool HasRoundStarted => gameStarted;
    public float CountdownRemaining => network==null||startsAt<0?0:Mathf.Max(0,(float)(startsAt-network.ServerTime.Time));
    private bool rosterReceived, localReady, startScheduled, gameStarted, spawnRequested, localFinished, roundEnding;
    private bool overviewMode = true;
    private string entryError;
    private float distanceAlongTrack, stunUntil;
    private int hurdleNumber, arrowProgress;
    private readonly int[] expectedDirections=new int[ArrowInputsPerHurdle];
    private int lastArrowPromptProgress=-1,lastArrowPromptTenths=-1;
    private bool hurdleResolved = true, hurdlePrompted, jumping, queuedJump;
    private float jumpStartedAt;
    private float queuedJumpAt;
    private float startFlashUntil;
    private const float StraightLength = 70f;
    private const float TurnRadius = 20.6f;
    private const float FinishOffset = 13.2f;
    private const float StartClearance = 18f;
    private float RaceDistance => PlayerLapLength(3) + FinishOffset;
    public string DeveloperStatus => statusText != null ? statusText.text : "QTE 준비 중";

    private void Awake()
    {
        network = NetworkManager.Singleton;
        raceCamera = Camera.main;
        if (raceCamera != null) { raceCamera.transform.position = new Vector3(0, 90, -106); raceCamera.transform.LookAt(Vector3.zero); raceCamera.fieldOfView = 58; }
        HidePrototypeSceneObjects();
        if (GameObject.Find("School ground") == null) BuildSchoolTrack();
        SetSceneSpectatorsIdle();
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
            var push=p.GetComponent<PlayerInteraction>();if(push!=null)push.enabled=false;
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
                countdownText.text = "<color=#FF625C>출발!</color>"; distanceAlongTrack = 0;
                for (int hurdle = 1; hurdle <= Mathf.FloorToInt(RaceDistance / hurdleSpacing); hurdle++)
                    SpawnHurdle(hurdle, hurdle * hurdleSpacing);
                PrepareNextHurdle(); PlayWhistle();
            }
            return;
        }
        if (countdownText != null && Time.unscaledTime >= startFlashUntil) countdownText.text = string.Empty;
        if (activePlayer == null || localFinished || roundEnding) return;
        if (Time.unscaledTime < stunUntil) { SetWalking(false); instructionText.text = $"<color=#FF625C>잠깐 멈춤 · {Mathf.Max(0, stunUntil - Time.unscaledTime):0.0}초</color>"; return; }
        if (stunUntil > 0) { stunUntil = 0; hurdleResolved = true; PrepareNextHurdle(); }
        if (jumping) UpdateJump();
        float remaining = Mathf.Max(0, hurdleNumber * hurdleSpacing - distanceAlongTrack);
        float timeToHurdle = remaining / runSpeed;
        if (!hurdlePrompted && timeToHurdle <= inputLeadTime)
        {
            hurdlePrompted = true;
            arrowProgress=0;lastArrowPromptProgress=-1;lastArrowPromptTenths=-1;
            for(int i=0;i<expectedDirections.Length;i++)expectedDirections[i]=UnityEngine.Random.Range(0,Keys.Length);
        }
        if (hurdlePrompted && !hurdleResolved) { ShowInstruction(timeToHurdle);ReadArrowInput(); }
        if (!hurdleResolved && hurdlePrompted && timeToHurdle <= 0.02f) ResolveHurdle(false);
        float speed = stunUntil > 0 ? 0 : runSpeed;
        distanceAlongTrack = Mathf.Min(RaceDistance, distanceAlongTrack + speed * Time.deltaTime);
        if (queuedJump && distanceAlongTrack >= queuedJumpAt)
        {
            queuedJump = false; jumping = true; jumpStartedAt = Time.time;
            if(activeAnimator!=null&&activeAnimator.parameters.Any(p=>p.name=="doJump"&&p.type==AnimatorControllerParameterType.Trigger))activeAnimator.SetTrigger("doJump");
        }
        int movingLane=Mathf.Max(0,objectIds.IndexOf(activePlayer.NetworkObjectId));
        float laneDistance=StartOffsetForLane(movingLane)+distanceAlongTrack;
        Vector3 position=TrackPosition(laneDistance,movingLane);
        if (jumping) position.y += Mathf.Sin(Mathf.Clamp01((Time.time - jumpStartedAt) / .8f) * Mathf.PI) * 1.8f;
        activePlayer.transform.SetPositionAndRotation(position,TrackRotation(laneDistance,movingLane));
        SetWalking(speed > 0 && !jumping);
        if (distanceAlongTrack >= RaceDistance) { localFinished = true; SetWalking(false); instructionText.text = "완주! 다른 주자를 기다리는 중"; }
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
        { SetTransparent(hurdle); foreach (var collider in hurdle.GetComponentsInChildren<Collider>()) collider.enabled = false; }
        if (success)
        {
            queuedJump = true;
            queuedJumpAt = hurdleNumber * hurdleSpacing - runSpeed * .4f;
            instructionText.text = "<color=#69EFA0>좋아요! 점프 성공</color>";
            PrepareNextHurdle();
        }
        else
        {
            stunUntil = Time.unscaledTime + failStopDuration;
            // The missed hurdle fades out so the runner can continue through it after the one-second pause.
            if (hurdles.TryGetValue(hurdleKey, out var missed) && missed != null) StartCoroutine(FadeHurdle(missed));
            instructionText.text = $"<color=#FF625C>실패! {failStopDuration:0}초 멈춤</color>";
        }
    }
    private IEnumerator FadeHurdle(GameObject hurdle)
    {
        var renderers = hurdle.GetComponentsInChildren<Renderer>(); float elapsed = 0;
        foreach (var r in renderers) foreach (var m in r.materials) { m.SetFloat("_Mode", 3); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0); m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.renderQueue = 3000; }
        while (elapsed < .45f && hurdle != null)
        {
            elapsed += Time.deltaTime; foreach (var r in renderers) if (r != null) foreach (var m in r.materials) { var c = m.color; c.a = Mathf.Lerp(1, .25f, elapsed / .45f); m.color = c; }
            yield return null;
        }
    }
    private void SetTransparent(GameObject hurdle) { foreach (var r in hurdle.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) { var c = m.color; c.a = .35f; m.color = c; } }
    private void UpdateJump() { if (Time.time - jumpStartedAt >= .8f) jumping = false; }
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
        if(GameStartPresentation.HasSharedCountdown){int sharedTick=Mathf.CeilToInt(CountdownRemaining-.001f);if(sharedTick>0&&sharedTick<=GameStartPresentation.CountdownSeconds)PlayCountdownTone(sharedTick);if(countdownText!=null)countdownText.text=string.Empty;return;}
        int tick = startsAt < 0 ? 0 : Mathf.CeilToInt((float)(startsAt - network.ServerTime.Time) - .001f);
        if (countdownText == null) return;
        countdownText.text = tick > 0 ? $"<size=30>출발 준비</size>\n<color=#FFD55A>{tick}</color>" : (startsAt >= 0 ? "<color=#FF625C>출발!</color>" : "");
        if (tick > 0 && tick <= GameStartPresentation.CountdownSeconds) PlayCountdownTone(tick);
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
        const float bpm=118f;
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
            if((beatInBar==0||beatInBar==2)&&beatSeconds<.2f)
                percussion+=Mathf.Sin(2f*Mathf.PI*(92f*beatSeconds-105f*beatSeconds*beatSeconds))*Mathf.Exp(-beatSeconds*22f)*.11f;
            if((beatInBar==1||beatInBar==3)&&beatSeconds<.14f)
                percussion+=(noise*.7f+Mathf.Sin(2f*Mathf.PI*185f*beatSeconds)*.3f)*Mathf.Exp(-beatSeconds*28f)*.055f;
            float eighthPhase=(t/eighthLength)-Mathf.Floor(t/eighthLength);
            if(eighthPhase<.035f)percussion+=noise*Mathf.Exp(-eighthPhase*80f)*.018f;

            float fade=Mathf.Clamp01(Mathf.Min(t/.045f,(duration-t)/.045f));
            float mix=(lead+pad+bass+percussion)*fade;
            samples[i*2]=mix+lead*.025f;
            samples[i*2+1]=mix-lead*.025f;
        }
        var clip=AudioClip.Create("School Sports Day Loop",sampleCount,2,sampleRate,false);
        clip.SetData(samples,0);
        musicSource=gameObject.AddComponent<AudioSource>();
        musicSource.clip=clip;musicSource.loop=true;musicSource.volume=.42f;musicSource.spatialBlend=0;musicSource.playOnAwake=false;
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
            body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.useGravity=false;body.isKinematic=true;
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
        startScheduled=true;startsAt=network.ServerTime.Time+GameStartPresentation.ScheduledStartDelay;
        using(var w=new FastBufferWriter(12,Allocator.Temp)){w.WriteValueSafe(entryToken);w.WriteValueSafe(startsAt);foreach(ulong id in owners)if(id!=NetworkManager.ServerClientId&&network.ConnectedClients.ContainsKey(id))messages.SendNamedMessage(StartMessage,id,w,NetworkDelivery.ReliableSequenced);}
    }
    private void OnStartReceived(ulong sender,FastBufferReader r){if(sender!=NetworkManager.ServerClientId||network.IsServer)return;r.ReadValueSafe(out int token);r.ReadValueSafe(out double t);if(token==entryToken)startsAt=t;}

    private void LateUpdate()
    {
        if (raceCamera != null)
        {
            bool running=gameStarted&&activePlayer!=null&&!localFinished&&Time.unscaledTime>=stunUntil;
            float targetFov=running?66f:58f;
            raceCamera.fieldOfView=Mathf.Lerp(raceCamera.fieldOfView,targetFov,Time.unscaledDeltaTime*3.2f);
            if (overviewMode) { /* Editor scene camera remains in its elevated overview while players get ready. */ }
            else if (activePlayer != null)
            {
                Vector3 target=activePlayer.transform.position-activePlayer.transform.forward*6.2f+Vector3.up*3.8f;
                if(running)target+=Vector3.up*(Mathf.Sin(Time.time*11f)*.045f);
                raceCamera.transform.position = Vector3.SmoothDamp(raceCamera.transform.position, target, ref cameraVelocity, cameraFollowSmoothTime);
                raceCamera.transform.LookAt(activePlayer.transform.position+activePlayer.transform.forward*5.5f+Vector3.up*1.1f);
            }
        }
        RefreshStatus();
        CheckFinishScores();
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
            state.Key.velocity=Vector3.zero;state.Key.angularVelocity=Vector3.zero;
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
        Cube("School ground",new Vector3(0,-.28f,0),new Vector3(150,.5f,100),grass);
        MakeOvalRibbon("Running track",0,0,15.2f,21.5f,track);
        for(int i=0;i<5;i++)MakeOvalLine("Lane marking",0,0,21.3f-i*1.4f,lane,.045f);
        // Keep the football pitch inside the inner lane boundary so the grass never covers the running track.
        Cube("Football field grass",new Vector3(0,.005f,0),new Vector3(70,.05f,30),turf);
        for(int x=-32;x<=32;x+=8)Cube("Football pitch stripe",new Vector3(x,.04f,0),new Vector3(4,.02f,27),new Color(.18f,.46f,.2f));
        Cube("Pitch halfway line",new Vector3(0,.04f,0),new Vector3(.16f,.03f,27),lane);
        var circle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);circle.name="Center circle";circle.transform.position=new Vector3(0,.06f,0);circle.transform.localScale=new Vector3(10,.025f,10);circle.GetComponent<Renderer>().sharedMaterial=lane;RemoveCollider(circle);
        foreach(float z in new[]{-12f,12f}){Cube("Goal box line",new Vector3(0,.05f,z),new Vector3(12,.03f,.12f),lane);Cube("Goal box side",new Vector3(-6,.05f,z/Mathf.Abs(z)*9),new Vector3(.12f,.03f,6),lane);Cube("Goal box side",new Vector3(6,.05f,z/Mathf.Abs(z)*9),new Vector3(.12f,.03f,6),lane);}
        for(int i=0;i<4;i++){float start=StartOffsetForLane(i);var spawn=new GameObject("QTE Spawn "+(i+1)).transform;spawn.position=TrackPosition(start,i);spawn.rotation=TrackRotation(start,i);}
        BuildRaceStartAndFinish();
        BuildSchoolBuilding();
        BuildBleachersAndSpectators();
        for(int i=0;i<11;i++){float x=-50+i*10;Cylinder("Trackside tree trunk",new Vector3(x,1.2f,-46),.28f,2.4f,new Color(.34f,.22f,.12f));Sphere("Trackside tree crown",new Vector3(x,3,-46),1.5f,new Color(.18f,.42f,.2f));}
        hurdleNumber=0;
    }
    private void BuildRaceStartAndFinish()
    {
        var white=MakeMat("Race line white",new Color(.98f,.97f,.88f));
        var startGold=MakeMat("Race start line gold",new Color(1f,.79f,.18f));
        var finishRed=MakeMat("Race finish line red",new Color(.82f,.12f,.12f));
        float laneWidth=1.4f;
        for(int lane=0;lane<4;lane++)
        {
            Vector3 start=TrackPosition(StartOffsetForLane(lane),lane);
            start.y=.045f;
            Cube($"Lane {lane+1} staggered start line",start,new Vector3(.24f,.045f,laneWidth*.9f),startGold);
        }
        float finishX=-StraightLength*.5f+FinishOffset;
        float trackCenterZ=-TurnRadius+1.5f*laneWidth;
        for(int tile=0;tile<8;tile++)
        {
            float z=trackCenterZ-2.8f+tile*.8f;
            Cube("Finish checker",new Vector3(finishX,.045f,z),new Vector3(.48f,.05f,.8f),tile%2==0?white:finishRed);
        }
        foreach(float z in new[]{trackCenterZ-3.5f,trackCenterZ+3.5f})
            Cube("Finish arch post",new Vector3(finishX,2.1f,z),new Vector3(.32f,4.2f,.32f),finishRed);
        Cube("Finish arch header",new Vector3(finishX,4.25f,trackCenterZ),new Vector3(.42f,.42f,7.35f),finishRed);
        Cube("Finish arch white band",new Vector3(finishX-.02f,4.25f,trackCenterZ),new Vector3(.45f,.12f,7.4f),white);
        var labelObject=new GameObject("Finish arch label",typeof(TextMeshPro));
        var label=labelObject.GetComponent<TextMeshPro>();
        label.font=koreanFont;label.text="FINISH";label.fontSize=2.2f;label.fontStyle=FontStyles.Bold;
        label.alignment=TextAlignmentOptions.Center;label.color=white.color;label.enableWordWrapping=false;
        labelObject.transform.SetPositionAndRotation(new Vector3(finishX-.25f,4.25f,trackCenterZ),Quaternion.Euler(0,270,0));
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
        Cube("School main building",new Vector3(0,6,34),new Vector3(25,12,10),wall);
        Cube("School west classroom wing",new Vector3(-23,4.6f,34),new Vector3(17,9.2f,9),wall);
        Cube("School east classroom wing",new Vector3(23,4.6f,34),new Vector3(17,9.2f,9),wall);
        Cube("School west corridor",new Vector3(-14.5f,4.2f,34),new Vector3(4,8.4f,8.5f),trim);
        Cube("School east corridor",new Vector3(14.5f,4.2f,34),new Vector3(4,8.4f,8.5f),trim);

        Cube("Main hall roof cap",new Vector3(0,12.2f,34),new Vector3(26,0.55f,10.8f),roof);
        Cube("West wing roof cap",new Vector3(-23,9.35f,34),new Vector3(18,0.45f,9.8f),roof);
        Cube("East wing roof cap",new Vector3(23,9.35f,34),new Vector3(18,0.45f,9.8f),roof);
        Cube("Main hall floor band",new Vector3(0,6.1f,28.88f),new Vector3(24.8f,0.25f,0.22f),trim);
        Cube("West wing floor band",new Vector3(-23,4.7f,29.38f),new Vector3(16.8f,0.22f,0.2f),trim);
        Cube("East wing floor band",new Vector3(23,4.7f,29.38f),new Vector3(16.8f,0.22f,0.2f),trim);

        foreach(float y in new[]{2.5f,5.2f,8.8f})
            foreach(float x in new[]{-9f,-4.5f,4.5f,9f}) AddSchoolWindow(x,y,28.72f,glass,trim);
        foreach(float wingX in new[]{-23f,23f})
            foreach(float y in new[]{2.4f,6.6f})
                foreach(float offset in new[]{-5.8f,-2f,2f,5.8f}) AddSchoolWindow(wingX+offset,y,29.28f,glass,trim);

        // Recessed, covered entrance facing the track.
        Cube("School entrance porch",new Vector3(0,2.1f,27.1f),new Vector3(9,4.2f,3.4f),trim);
        Cube("School double front door",new Vector3(0,1.65f,25.34f),new Vector3(3.3f,3.3f,.18f),door);
        Cube("School door glass",new Vector3(0,2.45f,25.22f),new Vector3(2.65f,1.15f,.08f),glass);
        Cube("School entrance canopy",new Vector3(0,4.35f,26.4f),new Vector3(10,.35f,3.3f),accent);
        foreach(float x in new[]{-4.2f,4.2f}) Cube("School entrance column",new Vector3(x,2.4f,25.5f),new Vector3(.45f,4.8f,.55f),trim);
        Cube("School front steps",new Vector3(0,.25f,23.9f),new Vector3(11,.5f,2),trim);

        var signObject=new GameObject("School name sign",typeof(TextMeshPro));
        var sign=signObject.GetComponent<TextMeshPro>();
        sign.font=koreanFont;sign.text="학교";sign.fontSize=4.4f;sign.alignment=TextAlignmentOptions.Center;sign.color=new Color(.18f,.24f,.27f);sign.enableWordWrapping=false;
        signObject.transform.position=new Vector3(0,10.65f,28.67f);signObject.transform.rotation=Quaternion.Euler(0,180,0);
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
        int[] seatedRows={1,3,5};
        for(int rowIndex=0;rowIndex<seatedRows.Length;rowIndex++)
        {
            int row=seatedRows[rowIndex];
            float z=-24.8f-row*1.65f;
            float y=.52f+row*.42f;
            for(int seat=0;seat<10;seat++)
            {
                GameObject prefab=spectatorPrefabs[(seat+rowIndex*2)%spectatorPrefabs.Length];
                if(prefab==null)continue;
                float x=-31.5f+seat*7f;
                var audience=CreateSpectator(prefab,new Vector3(x,y,z),Quaternion.LookRotation(Vector3.forward));
                if(audience==null)continue;
                audience.name=$"Audience Row {row+1} Seat {seat+1}"+(seat==1||seat==6?" Jump Cheer":"");
                audience.transform.localScale*=.86f;
            }
        }
    }
    private GameObject CreateSpectator(GameObject prefab,Vector3 position,Quaternion rotation)
    {
#if UNITY_EDITOR
        if(!Application.isPlaying)
        {
            var instance=PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene()) as GameObject;
            if(instance!=null)instance.transform.SetPositionAndRotation(position,rotation);
            return instance;
        }
#endif
        return Instantiate(prefab,position,rotation);
    }
    private void SetSceneSpectatorsIdle()
    {
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if(!root.name.StartsWith("Audience Row ",StringComparison.Ordinal))continue;
            var animator=root.GetComponentInChildren<Animator>();if(animator==null)continue;
            animator.applyRootMotion=false;
            foreach(var parameter in animator.parameters)
            {
                if(parameter.name=="isMoving"&&parameter.type==AnimatorControllerParameterType.Bool)animator.SetBool(parameter.name,false);
                else if(parameter.name=="idle"&&parameter.type==AnimatorControllerParameterType.Trigger)animator.SetTrigger(parameter.name);
                else if(parameter.type==AnimatorControllerParameterType.Trigger)animator.ResetTrigger(parameter.name);
            }
            animator.Play("Idle",0,0f);
            string[] nameParts=root.name.Split(' ');
            bool jumpCheer=root.name.Contains("Jump Cheer")||(nameParts.Length>=5&&int.TryParse(nameParts[4],out int seatNumber)&&(seatNumber==2||seatNumber==7));
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
    private void AddSchoolWindow(float x,float y,float z,Material glass,Material trim)
    {
        Cube("School window surround",new Vector3(x,y,z),new Vector3(2.75f,2.05f,.28f),trim);
        Cube("School classroom window",new Vector3(x,y,z-.18f),new Vector3(2.42f,1.72f,.1f),glass);
        Cube("Window vertical mullion",new Vector3(x,y,z-.25f),new Vector3(.11f,1.72f,.08f),trim);
        Cube("Window horizontal mullion",new Vector3(x,y,z-.25f),new Vector3(2.42f,.11f,.08f),trim);
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
            var legMat=MakeMat("Hurdle painted wood",new Color(.96f,.72f,.24f));
            var bar=Cube("Hurdle crossbar",root.transform.position,new Vector3(1.15f,.17f,.2f),legMat);bar.transform.SetParent(root.transform,false);bar.transform.localPosition=Vector3.up*.72f;bar.transform.localRotation=Quaternion.identity;bar.transform.localScale=new Vector3(1.15f,.17f,.2f);
            foreach(float side in new[]{-1f,1f}){var leg=Cube("Hurdle upright",root.transform.position,new Vector3(.16f,.8f,.16f),legMat);leg.transform.SetParent(root.transform,false);leg.transform.localPosition=new Vector3(side*.43f,.38f,0);leg.transform.localRotation=Quaternion.identity;leg.transform.localScale=new Vector3(.16f,.8f,.16f);}
            hurdles[key]=root;
        }
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
