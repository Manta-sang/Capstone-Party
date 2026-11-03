using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrosswalkRun;
using Unity.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>QTE 씬 전용 플레이어 배치, 참가자 준비 확인, 카운트다운과 로컬 달리기 입력을 관리합니다.</summary>
public sealed class QteSceneController : MonoBehaviour
{
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private TextMeshProUGUI qteText;
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private float qteBoostDistance = 1.5f;
    [SerializeField] private float movementDurationPerLetter = 0.25f;
    [SerializeField] private float finishLineZ = 98f;
    [SerializeField] private int sequenceLength = 4;
    [SerializeField] private float stunDuration = 1.5f;
    [SerializeField] private Vector3 cameraFollowOffset = new Vector3(0f, 6f, -7f);
    [SerializeField] private float cameraFollowSmoothTime = 0.15f;

    private const string RosterMessage = "QTE.Entry.Roster";
    private const string ReadyMessage = "QTE.Entry.Ready";
    private const string ReadyStatusMessage = "QTE.Entry.ReadyStatus";
    private const string StartMessage = "QTE.Entry.Start";

    private readonly char[] availableKeys = { 'W', 'A', 'S', 'D' };
    private readonly List<ulong> owners = new List<ulong>();
    private readonly List<ulong> objectIds = new List<ulong>();
    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
    private readonly HashSet<ulong> finishedPlayers = new HashSet<ulong>();

    private NetworkManager network;
    private CustomMessagingManager messages;
    private NetworkObject activePlayer;
    private Animator activeAnimator;
    private Vector3 targetPosition;
    private string currentSequence;
    private int currentIndex;
    private int entryToken;
    private bool rosterReceived;
    private bool localReady;
    private bool startScheduled;
    private bool gameStarted;
    private bool rankingWarningLogged;
    private bool localFinished;
    private bool roundEnding;
    private double startsAt = -1;
    private float stunUntil;
    private string entryError;
    private RectTransform statusPanelRect;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI countdownText;
    private Camera qteCamera;
    private Vector3 cameraVelocity;
    private bool spawnRequested;
    public string DeveloperStatus => statusText != null ? statusText.text : "QTE 준비 중";

    private void Awake()
    {
        CreateStatusUI();
        qteCamera = Camera.main;
        network = NetworkManager.Singleton;
        if (network == null || !network.IsListening) return;

        messages = network.CustomMessagingManager;
        messages.RegisterNamedMessageHandler(RosterMessage, OnRosterReceived);
        messages.RegisterNamedMessageHandler(ReadyMessage, OnReadyReceived);
        messages.RegisterNamedMessageHandler(ReadyStatusMessage, OnReadyStatusReceived);
        messages.RegisterNamedMessageHandler(StartMessage, OnStartReceived);
        if(network.IsServer)network.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void Start()
    {
        if (network == null || !network.IsListening)
            entryError = "네트워크 연결을 기다리는 중입니다.";
    }
    private void OnSceneLoaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if(scene!=gameObject.scene.name||spawnRequested)return;
        if(timedOut.Count>0||network.ConnectedClientsIds.Any(id=>!completed.Contains(id))){FailEntry("참가자의 씬 로딩을 완료하지 못했습니다.");return;}
        spawnRequested=true;StartCoroutine(SpawnPlayers());
    }

    private IEnumerator SpawnPlayers()
    {
        yield return null;
        if (network == null || !network.IsServer) yield break;

        ulong[] clients = network.ConnectedClientsIds.OrderBy(id => id).ToArray();
        if (clients.Length == 0 || clients.Length > 4 || characterPrefabs == null || characterPrefabs.Length == 0 ||
            spawnPoints == null || spawnPoints.Length < clients.Length ||
            spawnPoints.Take(clients.Length).Any(point => point == null) || characterPrefabs.Any(prefab => prefab == null))
        {
            FailEntry("캐릭터 프리팹과 참가자 수만큼의 스폰포인트를 확인해 주세요.");
            yield break;
        }

        foreach(ulong id in clients)
        {
            var previous=network.SpawnManager.GetPlayerNetworkObject(id);
            if(previous!=null)previous.Despawn(true);
        }

        entryToken = Guid.NewGuid().GetHashCode();
        for (int slot = 0; slot < clients.Length; slot++)
        {
            ulong clientId = clients[slot];
            int prefabIndex = (int)(clientId % (ulong)characterPrefabs.Length);
            Transform point = spawnPoints[slot];
            GameObject instance = Instantiate(characterPrefabs[prefabIndex], point.position, point.rotation);
            NetworkObject player = instance.GetComponent<NetworkObject>();
            if (player == null)
            {
                Destroy(instance);
                FailEntry($"프리팹에 NetworkObject가 없습니다: {instance.name}");
                yield break;
            }
            player.SpawnAsPlayerObject(clientId, true);
            owners.Add(clientId);
            objectIds.Add(player.NetworkObjectId);
            Debug.Log($"[QTE 입장] 플레이어 {clientId}를 스폰포인트 {slot + 1}에서 생성했습니다.");
        }

        rosterReceived = true;
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe(entryToken);
            writer.WriteValueSafe(owners.Count);
            for (int i = 0; i < owners.Count; i++)
            {
                writer.WriteValueSafe(owners[i]);
                writer.WriteValueSafe(objectIds[i]);
            }
            foreach (ulong clientId in owners)
                if (clientId != NetworkManager.ServerClientId)
                    messages.SendNamedMessage(RosterMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void Update()
    {
        if (network == null || !network.IsListening) return;

        // QTE 씬에서는 기존 자유 이동 입력을 끕니다.
        foreach (NetworkObject player in network.SpawnManager.SpawnedObjectsList)
        {
            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null && movement.enabled) movement.enabled = false;
            var legacy = player.GetComponent<QteRunner>();
            if(legacy!=null)legacy.enabled=false;
        }

        if (rosterReceived && !localReady && LocalRosterIsPresent())
            SendReady();

        if (network.IsServer && rosterReceived && !startScheduled && owners.All(id=>!network.ConnectedClients.ContainsKey(id)||readyClients.Contains(id)))
            ScheduleStart();

        if (!gameStarted && startsAt >= 0 && network.ServerTime.Time >= startsAt)
        {
            gameStarted = true;
            if (activePlayer != null && currentSequence == null) GenerateSequence();
        }

        var localPlayer = network.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;
        if (activePlayer != localPlayer)
        {
            activePlayer = localPlayer;
            activeAnimator = localPlayer.GetComponentInChildren<Animator>();
            targetPosition = activePlayer.transform.position;
            currentSequence = null;
            localFinished = false;
        }

        if (!gameStarted)
        {
            SetWalking(false);
            if (qteText != null && startsAt < 0) qteText.text = localReady ? "모든 참가자 준비 대기 중" : "캐릭터 동기화 중";
            return;
        }

        if (currentSequence == null) GenerateSequence();
        if (activePlayer.transform.position.z >= finishLineZ - 0.5f && targetPosition.z >= finishLineZ - 0.5f)
        {
            if (!localFinished)
            {
                localFinished = true;
                if (qteText != null) qteText.text = "완주했습니다. 다른 플레이어를 기다리는 중...";
            }
            SetWalking(false);
            return;
        }

        if (Time.unscaledTime < stunUntil)
        {
            targetPosition = activePlayer.transform.position;
            SetWalking(false);
            if (qteText != null)
                qteText.text = $"<color=#FF5555>그로기 {Mathf.Max(0, stunUntil - Time.unscaledTime):0.0}초</color>\n{FormatSequence()}";
            return;
        }
        else if (stunUntil > 0)
        {
            stunUntil = 0;
            if (qteText != null) qteText.text = FormatSequence();
        }

        CheckInput();
        MoveTowardTarget();
    }

    private bool LocalRosterIsPresent()
    {
        if (!owners.Contains(network.LocalClientId)) return false;
        for (int i = 0; i < objectIds.Count; i++)
        {
            if (!network.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i], out NetworkObject player) ||
                !player.IsPlayerObject || player.OwnerClientId != owners[i])
                return false;
        }
        return true;
    }

    private void SendReady()
    {
        localReady = true;
        if (network.IsServer)
            MarkReady(network.LocalClientId);
        else
        {
            using (var writer = new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe(entryToken);
                messages.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }
    }

    private void OnRosterReceived(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || network.IsServer || rosterReceived) return;

        reader.ReadValueSafe(out entryToken);
        reader.ReadValueSafe(out int count);
        if (count < 1 || count > 4)
        {
            FailEntry("호스트에서 받은 참가자 목록이 올바르지 않습니다.");
            return;
        }
        owners.Clear();
        objectIds.Clear();
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ulong owner);
            reader.ReadValueSafe(out ulong objectId);
            owners.Add(owner);
            objectIds.Add(objectId);
        }
        rosterReceived = true;
    }

    private void OnReadyReceived(ulong sender, FastBufferReader reader)
    {
        if (!network.IsServer || !rosterReceived || !owners.Contains(sender)) return;
        reader.ReadValueSafe(out int token);
        if (token == entryToken) MarkReady(sender);
    }

    private void MarkReady(ulong clientId)
    {
        if (!readyClients.Add(clientId)) return;
        using (var writer = new FastBufferWriter(12, Allocator.Temp))
        {
            writer.WriteValueSafe(entryToken);
            writer.WriteValueSafe(clientId);
            foreach (ulong owner in owners)
                if (owner != NetworkManager.ServerClientId && network.ConnectedClients.ContainsKey(owner))
                    messages.SendNamedMessage(ReadyStatusMessage, owner, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void OnReadyStatusReceived(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || network.IsServer) return;
        reader.ReadValueSafe(out int token);
        reader.ReadValueSafe(out ulong clientId);
        if (token == entryToken && owners.Contains(clientId)) readyClients.Add(clientId);
    }

    private void ScheduleStart()
    {
        startScheduled = true;
        startsAt = network.ServerTime.Time + 3.0;
        using (var writer = new FastBufferWriter(12, Allocator.Temp))
        {
            writer.WriteValueSafe(entryToken);
            writer.WriteValueSafe(startsAt);
            foreach (ulong owner in owners)
                if (owner != NetworkManager.ServerClientId && network.ConnectedClients.ContainsKey(owner))
                    messages.SendNamedMessage(StartMessage, owner, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void OnStartReceived(ulong sender, FastBufferReader reader)
    {
        if (sender != NetworkManager.ServerClientId || network.IsServer) return;
        reader.ReadValueSafe(out int token);
        reader.ReadValueSafe(out double time);
        if (token == entryToken) startsAt = time;
    }

    private void CheckInput()
    {
        if (currentIndex >= currentSequence.Length) return;
        bool pressedW = Input.GetKeyDown(KeyCode.W);
        bool pressedA = Input.GetKeyDown(KeyCode.A);
        bool pressedS = Input.GetKeyDown(KeyCode.S);
        bool pressedD = Input.GetKeyDown(KeyCode.D);
        if (!pressedW && !pressedA && !pressedS && !pressedD) return;

        char pressed = pressedW ? 'W' : pressedA ? 'A' : pressedS ? 'S' : 'D';
        if (pressed != currentSequence[currentIndex])
        {
            stunUntil = Time.unscaledTime + stunDuration;
            targetPosition = activePlayer.transform.position;
            SetWalking(false);
            if (qteText != null)
                qteText.text = $"<color=#FF5555>그로기 {stunDuration:0.0}초</color>\n{FormatSequence()}";
            return;
        }

        float stepDistance = qteBoostDistance / Mathf.Max(1, sequenceLength);
        targetPosition = new Vector3(targetPosition.x, targetPosition.y, Mathf.Min(finishLineZ, targetPosition.z + stepDistance));
        currentIndex++;
        if (currentIndex >= currentSequence.Length)
            GenerateSequence();
        else if (qteText != null)
            qteText.text = FormatSequence();
    }

    private void MoveTowardTarget()
    {
        float remaining = Vector3.Distance(activePlayer.transform.position, targetPosition);
        bool isMoving = remaining > 0.02f;
        SetWalking(isMoving);
        if (!isMoving) return;

        float stepDistance = qteBoostDistance / Mathf.Max(1, sequenceLength);
        float speed = stepDistance / Mathf.Max(0.05f, movementDurationPerLetter);
        activePlayer.transform.position = Vector3.MoveTowards(
            activePlayer.transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void SetWalking(bool walking)
    {
        if (activeAnimator != null) activeAnimator.SetBool("isMoving", walking);
    }

    private void GenerateSequence()
    {
        currentSequence = string.Empty;
        currentIndex = 0;
        for (int i = 0; i < sequenceLength; i++)
            currentSequence += availableKeys[UnityEngine.Random.Range(0, availableKeys.Length)];
        if (qteText != null) qteText.text = FormatSequence();
    }

    private string FormatSequence()
    {
        var display = new List<string>();
        for (int i = 0; i < currentSequence.Length; i++)
            display.Add(i < currentIndex ? "<color=#55DD88>" + currentSequence[i] + "</color>" : currentSequence[i].ToString());
        return string.Join(" ", display);
    }

    private void LateUpdate()
    {
        RefreshStatusUI();
        FollowLocalPlayerCamera();
        CheckFinishLineScores();
    }

    private void CheckFinishLineScores()
    {
        if (network == null || !network.IsListening || !network.IsServer || !gameStarted || !rosterReceived) return;

        var ranking = RankingManager.Instance;
        bool canRecordScore = ranking != null && ranking.IsSpawned;
        if (!canRecordScore && !rankingWarningLogged)
        {
            rankingWarningLogged = true;
            Debug.LogError("[QTE 점수] RankingManager가 네트워크에 생성되지 않아 완주 점수를 기록할 수 없습니다.");
        }

        for (int i = 0; i < objectIds.Count; i++)
        {
            ulong owner = owners[i];
            if (finishedPlayers.Contains(owner) ||
                !network.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i], out NetworkObject player))
                continue;

            // QTE 플레이어는 소유 클라이언트 권한으로 위치를 동기화하므로 호스트가 좌표를 직접 감시합니다.
            // 결승선 콜라이더 두께의 앞쪽(z - 0.5)부터 도착으로 처리해 트리거 콜백 누락을 피합니다.
            if (player.transform.position.z < finishLineZ - 0.5f) continue;

            int place = finishedPlayers.Count + 1;
            int points = Mathf.Max(0, 4 - place);
            if (canRecordScore) ranking.AddScore(owner, $"Player {owner}", points);
            finishedPlayers.Add(owner);
            Debug.Log($"[QTE 점수] 플레이어 {owner} 완주 {place}등: {points}점 (호스트 위치 판정)");
        }

        if (!roundEnding && owners.Count > 0 && owners.All(id=>!network.ConnectedClients.ContainsKey(id)||finishedPlayers.Contains(id)))
        {
            roundEnding = true;
            if (qteText != null) qteText.text = "모든 플레이어가 완주했습니다!";
            StartCoroutine(EndRoundAndReturnToRanking());
        }
    }

    private IEnumerator EndRoundAndReturnToRanking()
    {
        Debug.Log("[QTE] 전체 참가자 완주. 잠시 후 랭킹 화면으로 이동합니다.");
        yield return new WaitForSecondsRealtime(3f);

        if (network == null || !network.IsListening || !network.IsServer || network.SceneManager == null)
            yield break;

        var status = network.SceneManager.LoadScene("Ranking", LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            roundEnding = false;
            entryError = "랭킹 화면 이동에 실패했습니다: " + status;
            Debug.LogError("[QTE] " + entryError);
        }
    }

    private void FollowLocalPlayerCamera()
    {
        if (qteCamera == null || activePlayer == null) return;

        Vector3 destination = activePlayer.transform.position + cameraFollowOffset;
        qteCamera.transform.position = cameraFollowSmoothTime <= 0f
            ? destination
            : Vector3.SmoothDamp(qteCamera.transform.position, destination, ref cameraVelocity, cameraFollowSmoothTime);
    }

    private void CreateStatusUI()
    {
        var canvasObject = new GameObject("QTE Status Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("QTE Connection Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        statusPanelRect = panel.GetComponent<RectTransform>();
        statusPanelRect.anchorMin = new Vector2(0, 1);
        statusPanelRect.anchorMax = new Vector2(0, 1);
        statusPanelRect.pivot = new Vector2(0, 1);
        statusPanelRect.anchoredPosition = new Vector2(16, -16);
        statusPanelRect.sizeDelta = new Vector2(500, 120);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.82f);
        panel.SetActive(false); // Connection diagnostics are displayed in the common Esc developer panel.

        statusText = CreateStatusLabel("QTE Connection Text", statusPanelRect, 22, TextAlignmentOptions.TopLeft);
        var statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(0, 1);
        statusRect.pivot = new Vector2(0, 1);
        statusRect.anchoredPosition = new Vector2(16, -12);
        statusRect.sizeDelta = new Vector2(468, 240);

        countdownText = CreateStatusLabel("QTE Countdown Text", canvas.transform as RectTransform, 100, TextAlignmentOptions.Center);
        var countdownRect = countdownText.rectTransform;
        countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        countdownRect.pivot = new Vector2(0.5f, 0.5f);
        countdownRect.anchoredPosition = Vector2.zero;
        countdownRect.sizeDelta = new Vector2(400, 180);
    }

    private TextMeshProUGUI CreateStatusLabel(string objectName, RectTransform parent, float size, TextAlignmentOptions alignment)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = koreanFont;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.richText = true;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private void RefreshStatusUI()
    {
        if (statusText == null) return;
        var lines = new List<string> { "QTE 참가자 연결 상태" };
        if (entryError != null)
            lines.Add(entryError);
        else if (network == null || !network.IsListening)
            lines.Add("서버 연결을 기다리는 중...");
        else if (!rosterReceived)
            lines.Add("호스트의 참가자 목록을 기다리는 중...");
        else if (roundEnding)
            lines.Add("모든 참가자가 완주했습니다. 랭킹 화면으로 이동합니다.");
        else
        {
            foreach (ulong owner in owners)
            {
                bool connected = !network.IsServer || network.ConnectedClients.ContainsKey(owner);
                string state = !connected ? "연결 끊김" : readyClients.Contains(owner) ? "준비 완료" : "캐릭터 동기화 중";
                string role = owner == NetworkManager.ServerClientId ? "호스트" : $"플레이어 {owner}";
                lines.Add($"{role}: {state}");
            }
        }
        statusText.text = string.Join("\n", lines);
        statusPanelRect.sizeDelta = new Vector2(500, 54 + Mathf.Max(1, lines.Count - 1) * 36);

        if (countdownText != null)
        {
            int countdown = network != null && network.IsListening && startsAt >= 0
                ? Mathf.CeilToInt((float)(startsAt - network.ServerTime.Time) - 0.001f)
                : 0;
            countdownText.text = !gameStarted && countdown > 0 ? countdown.ToString() : string.Empty;
        }
    }

    private void FailEntry(string reason)
    {
        entryError = reason;
        Debug.LogError("[QTE 입장] " + reason);
    }

    private void OnDestroy()
    {
        if(network!=null&&network.IsServer&&network.SceneManager!=null)network.SceneManager.OnLoadEventCompleted-=OnSceneLoaded;
        if (messages != null)
        {
            messages.UnregisterNamedMessageHandler(RosterMessage);
            messages.UnregisterNamedMessageHandler(ReadyMessage);
            messages.UnregisterNamedMessageHandler(ReadyStatusMessage);
            messages.UnregisterNamedMessageHandler(StartMessage);
        }
    }
}
