using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QteRaceCoordinator : NetworkBehaviour
{
    public static QteRaceCoordinator Instance { get; private set; }
    public NetworkVariable<double> StartAtServerTime = new NetworkVariable<double>(0d);
    public float FinishLineZ { get; private set; }
    public bool RaceStarted => StartAtServerTime.Value > 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.ServerTime.Time >= StartAtServerTime.Value;

    private readonly List<ulong> finishOrder = new List<ulong>();
    private TextMeshProUGUI countdownText;
    private TextMeshProUGUI groggyText;
    private bool resultsSent;
    private bool sceneLoadHandlerRegistered;
    private bool resultsCleared;
    private bool countdownStarted;
    private readonly HashSet<ulong> clientsLoadedQte = new HashSet<ulong>();
    [SerializeField] private GameObject[] characterPrefabs;

    private void Awake()
    {
        Instance = this;
        RegisterSceneLoadHandler();
    }

    public override void OnNetworkSpawn()
    {
        RegisterSceneLoadHandler();
        Debug.Log($"[QTE] Coordinator network spawn. IsServer={IsServer}, IsClient={IsClient}.");
        FinishLineZ = GameObject.Find("FinishLine")?.transform.position.z ?? 98f;
        CreateCountdownText();
        CreateGroggyText();
        if (IsServer) HandleClientQteReady(NetworkManager.ServerClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (sceneLoadHandlerRegistered && NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnClientQteLoadComplete;
        sceneLoadHandlerRegistered = false;
        if (Instance == this) Instance = null;
    }

    private void RegisterSceneLoadHandler()
    {
        if (sceneLoadHandlerRegistered || NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnClientQteLoadComplete;
        sceneLoadHandlerRegistered = true;
    }

    private void OnClientQteLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (!IsServer || sceneName != "Qte") return;
        HandleClientQteReady(clientId);
    }

    private void HandleClientQteReady(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClientsIds.Contains(clientId)) return;
        clientsLoadedQte.Add(clientId);
        if (!resultsCleared)
        {
            ClearResultsClientRpc();
            resultsCleared = true;
        }
        SpawnPlayerAtAssignedPoint(clientId);
        TryStartCountdown();
    }

    private void SpawnPlayerAtAssignedPoint(ulong clientId)
    {
        List<ulong> connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        int lane = connectedClients.IndexOf(clientId);
        if (lane < 0 || lane >= 4)
        {
            Debug.LogWarning($"[QTE] No spawn point available for client {clientId}.");
            return;
        }

        Transform spawn = FindTransform("Spawn " + (lane + 1));
        if (spawn == null)
        {
            Debug.LogError($"[QTE] Spawn {lane + 1} was not found.");
            return;
        }

        NetworkObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (player == null && characterPrefabs != null && characterPrefabs.Length > 0)
        {
            GameObject prefab = characterPrefabs[(int)(clientId % (ulong)characterPrefabs.Length)];
            GameObject instance = Instantiate(prefab, spawn.position, spawn.rotation);
            player = instance.GetComponent<NetworkObject>();
            if (player != null) player.SpawnAsPlayerObject(clientId, true);
        }

        if (player == null)
        {
            Debug.LogError($"[QTE] Could not spawn a player for client {clientId}.");
            return;
        }

        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        PlacePlayerClientRpc(clientId, spawn.position, spawn.rotation);
        Debug.Log($"[QTE] Player for client {clientId} is ready at Spawn {lane + 1}.");
    }

    private void TryStartCountdown()
    {
        if (countdownStarted) return;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!clientsLoadedQte.Contains(clientId)) return;
            if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId) == null) return;
        }

        countdownStarted = true;
        StartAtServerTime.Value = NetworkManager.Singleton.ServerTime.Time + 3.0d;
        Debug.Log($"[QTE] Countdown started for {NetworkManager.Singleton.ConnectedClientsIds.Count} players.");
    }
    private void Update()
    {
        if (countdownText == null || StartAtServerTime.Value <= 0) return;
        double remaining = StartAtServerTime.Value - NetworkManager.Singleton.ServerTime.Time;
        if (remaining > 0)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt((float)remaining).ToString();
        }
        else
        {
            countdownText.text = "GO!";
            if (remaining < -0.8) countdownText.gameObject.SetActive(false);
        }
    }

    private Transform FindTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private void CreateCountdownText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject label = new GameObject("RaceCountdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(canvas.transform, false);
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = new Vector2(0.08f, 0.5f);
        rect.anchorMax = new Vector2(0.08f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(220f, 220f);

        countdownText = label.GetComponent<TextMeshProUGUI>();
        foreach (TextMeshProUGUI existingText in FindObjectsOfType<TextMeshProUGUI>(true))
        {
            if (existingText.gameObject.name != "QteText") continue;
            countdownText.font = existingText.font;
            countdownText.fontSharedMaterial = existingText.fontSharedMaterial;
            break;
        }
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.fontSize = 120f;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = Color.white;
        countdownText.outlineWidth = 0.2f;
        countdownText.outlineColor = Color.black;
        countdownText.text = string.Empty;
        label.SetActive(false);
    }


    private void CreateGroggyText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject label = new GameObject("GroggyStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(canvas.transform, false);
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = new Vector2(0.92f, 0.9f);
        rect.anchorMax = new Vector2(0.92f, 0.9f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(240f, 120f);

        groggyText = label.GetComponent<TextMeshProUGUI>();
        if (countdownText != null)
        {
            groggyText.font = countdownText.font;
            groggyText.fontSharedMaterial = countdownText.fontSharedMaterial;
        }
        groggyText.alignment = TextAlignmentOptions.Center;
        groggyText.fontSize = 40f;
        groggyText.fontStyle = FontStyles.Bold;
        groggyText.color = Color.white;
        groggyText.outlineWidth = 0.2f;
        groggyText.outlineColor = Color.black;
        label.SetActive(false);
    }

    public void SetGroggyStatus(bool active, float remainingSeconds)
    {
        if (groggyText == null) return;
        groggyText.gameObject.SetActive(active);
        if (active) groggyText.text = $"GROGGY\n{remainingSeconds:0.0}s";
    }
    [ClientRpc]
    private void ClearResultsClientRpc()
    {
        PlayerPrefs.DeleteKey("QteRankCount");
        for (int i = 1; i <= 4; i++)
        {
            PlayerPrefs.DeleteKey($"QteRankName{i}");
            PlayerPrefs.DeleteKey($"QteRankScore{i}");
        }
        PlayerPrefs.Save();
    }

    [ClientRpc]
    private void PlacePlayerClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        NetworkObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (player != null && player.IsOwner)
            player.transform.SetPositionAndRotation(position, rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportFinishServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != clientId || finishOrder.Contains(clientId)) return;
        finishOrder.Add(clientId);

        int expectedFinishers = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (finishOrder.Count < expectedFinishers || resultsSent) return;
        resultsSent = true;

        for (int i = 0; i < finishOrder.Count; i++)
        {
            string playerName = $"Player {finishOrder[i] + 1}";
            int score = finishOrder.Count - i;
            SaveResultClientRpc(i + 1, playerName, score);
        }
        StartCoroutine(LoadRankingAfterResults());
    }

    [ClientRpc]
    private void SaveResultClientRpc(int place, string playerName, int score)
    {
        PlayerPrefs.SetString($"QteRankName{place}", playerName);
        PlayerPrefs.SetInt($"QteRankScore{place}", score);
        PlayerPrefs.SetInt("QteRankCount", place);
        PlayerPrefs.Save();
    }

    private IEnumerator LoadRankingAfterResults()
    {
        yield return new WaitForSeconds(1f);
        NetworkManager.Singleton.SceneManager.LoadScene("Ranking", LoadSceneMode.Single);
    }
}