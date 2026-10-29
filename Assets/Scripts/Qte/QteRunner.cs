using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class QteRunner : NetworkBehaviour
{
    [Header("QTE Settings")]
    [SerializeField] private float stepDistance = 0.65f;
    [SerializeField] private float stepMoveDuration = 0.3f;
    [SerializeField] private float groggyDuration = 1f;
    [SerializeField] private int sequenceLength = 4;

    private static readonly char[] AvailableKeys = { 'W', 'A', 'S', 'D' };
    private TextMeshProUGUI qteText;
    private PlayerMovement playerMovement;
    private Animator animator;
    private string currentSequence;
    private int currentIndex;
    private bool raceFinished;
    private bool isQteScene;
    private bool isStepMoving;
    private int pendingSteps;
    private float groggyUntil;
    private float stepElapsed;
    private Vector3 stepStart;
    private Vector3 stepTarget;

    public override void OnNetworkSpawn()
    {
        playerMovement = GetComponent<PlayerMovement>();
        animator = GetComponentInChildren<Animator>(true);
        RefreshSceneMode(true);
    }

    private void RefreshSceneMode(bool force = false)
    {
        bool inQteScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Qte";
        if (!force && inQteScene == isQteScene) return;

        isQteScene = inQteScene;
        if (playerMovement != null) playerMovement.enabled = !isQteScene;
        isStepMoving = false;
        pendingSteps = 0;
        groggyUntil = 0f;
        if (!isQteScene) QteCameraFollow.ClearTarget(transform);

        if (isQteScene && IsOwner)
        {
            qteText = FindQteText();
            if (qteText != null) qteText.gameObject.SetActive(true);
            GenerateNewQte();
            raceFinished = false;
            QteCameraFollow.Attach(transform);
        }
        else if (animator != null)
        {
            animator.SetBool("isMoving", false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerMovement != null) playerMovement.enabled = true;
        if (animator != null) animator.SetBool("isMoving", false);
        QteCameraFollow.ClearTarget(transform);
    }

    private void Update()
    {
        RefreshSceneMode();
        if (!isQteScene || !IsOwner || raceFinished) return;

        QteRaceCoordinator coordinator = QteRaceCoordinator.Instance;
        if (coordinator == null || !coordinator.RaceStarted) return;

        if (isStepMoving) UpdateStepMovement();
        else if (pendingSteps > 0) StartNextStepMovement();

        float groggyRemaining = groggyUntil - Time.time;
        coordinator.SetGroggyStatus(groggyRemaining > 0f, Mathf.Max(0f, groggyRemaining));
        if (groggyRemaining <= 0f) CheckQteInput();
        CheckFinish(coordinator);
    }

    private TextMeshProUGUI FindQteText()
    {
        foreach (TextMeshProUGUI text in FindObjectsOfType<TextMeshProUGUI>(true))
            if (text.gameObject.name == "QteText") return text;
        return null;
    }

    private void GenerateNewQte()
    {
        currentSequence = string.Empty;
        currentIndex = 0;
        for (int i = 0; i < sequenceLength; i++)
            currentSequence += AvailableKeys[Random.Range(0, AvailableKeys.Length)];
        RefreshQteText();
    }

    private void RefreshQteText()
    {
        if (qteText == null || string.IsNullOrEmpty(currentSequence)) return;

        StringBuilder display = new StringBuilder();
        for (int i = 0; i < currentSequence.Length; i++)
        {
            if (i > 0) display.Append(' ');
            if (i == currentIndex) display.Append("<color=#FFD54F><u>");
            display.Append(currentSequence[i]);
            if (i == currentIndex) display.Append("</u></color>");
        }
        qteText.text = display.ToString();
    }
    private void CheckQteInput()
    {
        if (string.IsNullOrEmpty(currentSequence)) return;

        char pressedKey = GetPressedQteKey();
        if (pressedKey == '\0') return;

        if (pressedKey != currentSequence[currentIndex])
        {
            groggyUntil = Time.time + groggyDuration;
            return;
        }

        pendingSteps++;
        if (!isStepMoving) StartNextStepMovement();
        currentIndex++;
        if (currentIndex >= currentSequence.Length) GenerateNewQte();
        else RefreshQteText();
    }

    private char GetPressedQteKey()
    {
        if (Input.GetKeyDown(KeyCode.W)) return 'W';
        if (Input.GetKeyDown(KeyCode.A)) return 'A';
        if (Input.GetKeyDown(KeyCode.S)) return 'S';
        if (Input.GetKeyDown(KeyCode.D)) return 'D';
        return '\0';
    }

    private void StartNextStepMovement()
    {
        if (pendingSteps <= 0 || raceFinished) return;
        pendingSteps--;
        isStepMoving = true;
        stepElapsed = 0f;
        stepStart = transform.position;
        stepTarget = stepStart + Vector3.forward * stepDistance;
        if (animator != null) animator.SetBool("isMoving", true);
    }

    private void UpdateStepMovement()
    {
        stepElapsed += Time.deltaTime;
        float progress = stepMoveDuration <= 0f ? 1f : Mathf.Clamp01(stepElapsed / stepMoveDuration);
        transform.position = Vector3.Lerp(stepStart, stepTarget, Mathf.SmoothStep(0f, 1f, progress));

        if (progress < 1f) return;
        transform.position = stepTarget;
        isStepMoving = false;
        if (pendingSteps > 0) StartNextStepMovement();
        else if (animator != null) animator.SetBool("isMoving", false);
    }

    private void CheckFinish(QteRaceCoordinator coordinator)
    {
        if (coordinator.FinishLineZ <= 0f || transform.position.z < coordinator.FinishLineZ) return;
        raceFinished = true;
        pendingSteps = 0;
        isStepMoving = false;
        if (animator != null) animator.SetBool("isMoving", false);
        if (qteText != null) qteText.text = "FINISH!";
        coordinator.SetGroggyStatus(false, 0f);
        coordinator.ReportFinishServerRpc(OwnerClientId);
    }
}