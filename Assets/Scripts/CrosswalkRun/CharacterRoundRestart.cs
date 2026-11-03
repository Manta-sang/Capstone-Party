using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrosswalkRun
{
    // 기존 CustomSpawner를 유지하면서 Character 씬에 호스트용 미니게임 선택 버튼을 제공합니다.
    public sealed class CharacterRoundRestart : MonoBehaviour
    {
        private NetworkManager network;
        [Header("Character 씬에 배치된 UI 참조")]
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private TMPro.TMP_Text caption;
        [SerializeField] private UnityEngine.UI.Button sawBladeButton;
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private UnityEngine.UI.Button qteButton;
        [SerializeField] private UnityEngine.UI.Button tileFlipButton;
        [SerializeField] private UnityEngine.UI.Button blockFallButton;
        [SerializeField] private string blockFallScene = "BlockFall";
        [Header("버튼별 이동할 씬 이름 (Build Settings 등록 필요)")]
        [SerializeField] private string crosswalkScene = "CrosswalkRun";
        [SerializeField] private string sawBladeScene = "SawBladeDodge";
        [SerializeField] private string qteScene = "Qte";
        [SerializeField] private string tileFlipScene = "TileFlip";
        [Header("실행 중 상태 안내 문구")]
        [SerializeField] private string disconnectedText = "Connect to a room first";
        [SerializeField] private string loadingText = "Loading minigame...";
        [SerializeField] private string waitingHostText = "Waiting for host";
        [SerializeField] private string waitingPlayersText = "Waiting for players...";

        private bool loadCompleted;
        private bool loading;
        public bool IsLoading => loading;
        public bool ParticipantsReady { get; set; } = true;
        public bool CanStart => startButton != null && startButton.interactable && !loading;

        // 씬 로딩 완료 이벤트를 Start 이전에 구독하여 빠른 호스트의 중복 전환을 막습니다.
        private void Awake()
        {
            network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
                network.SceneManager.OnLoadEventCompleted += OnLoadCompleted;
        }

        // 씬에 저장된 UI를 연결합니다. 위치·크기·색상·폰트는 에디터 설정을 그대로 사용합니다.
        private void Start()
        {
            if (startButton == null || caption == null || sawBladeButton == null || statusText == null)
            {
                Debug.LogError("[Character 재시작] Inspector에서 두 미니게임 버튼과 Caption, Status Text를 연결해 주세요.");
                if (startButton != null) startButton.interactable = false;
                if (sawBladeButton != null) sawBladeButton.interactable = false;
                enabled = false;
                return;
            }
            // 버튼 이름은 씬의 Label을 유지하고, 실행 상태는 별도 안내 문구에 표시합니다.
            // On Click은 씬에 저장되어 Inspector에서도 연결 함수를 확인할 수 있습니다.
            Update();
        }

        // 호스트가 모든 참가자의 로딩 완료를 확인한 뒤에만 재시작할 수 있습니다.
        private void OnLoadCompleted(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (scene != gameObject.scene.name || !network.IsServer) return;
            loadCompleted = timedOut.Count == 0;
            foreach (ulong id in network.ConnectedClientsIds)
                if (!completed.Contains(id)) loadCompleted = false;
        }

        // 입력 권한과 캐릭터 생성 상태를 확인하며 참가자에게는 호스트 대기를 표시합니다.
        private void Update()
        {
            if (startButton == null || sawBladeButton == null || statusText == null) return;
            bool online = network != null && network.IsListening;
            bool ready = online && network.IsServer && loadCompleted && !loading;
            if (ready)
                foreach (ulong id in network.ConnectedClientsIds)
                    if (network.SpawnManager.GetPlayerNetworkObject(id) == null) ready = false;
            ready &= ParticipantsReady;
            startButton.interactable = ready;
            sawBladeButton.interactable = ready;
            if (qteButton != null) qteButton.interactable = ready;
            if (tileFlipButton != null) tileFlipButton.interactable = ready;
            if (blockFallButton != null) blockFallButton.interactable = ready;
            // 기본 버튼 문구는 Label에서, 상태별 안내는 이 컴포넌트의 Inspector에서 수정합니다.
            statusText.text = !online ? disconnectedText : loading ? loadingText :
                !network.IsServer ? waitingHostText : ready ? "호스트가 미니게임을 선택해 주세요" : waitingPlayersText;
        }

        // 기존 방을 유지한 네트워크 씬 전환이며 클릭을 연속해도 한 번만 실행합니다.
        public void StartCrosswalk()
        {
            StartMiniGame(crosswalkScene);
        }

        // 톱날 게임도 같은 방의 참가자 전원이 함께 이동합니다.
        public void StartSawBlade()
        {
            StartMiniGame(sawBladeScene);
        }

        // 알파용 씬 진입 버튼입니다. QTE와 TileFlip의 게임 로직 자체는 이번 작업에서 변경하지 않습니다.
        public void StartQte() => StartMiniGame(qteScene);
        public void StartTileFlip() => StartMiniGame(tileFlipScene);
        public void StartBlockFall() => StartMiniGame(blockFallScene);
        public void StartRandomGame()
        {
            if (!CanStart || network == null || !network.IsServer) return;
            loading = true; Update();
            if (!PartyGameRotation.StartRandom(network, out string error))
            { loading = false; Update(); statusText.text = error; Debug.LogWarning(error); }
        }

        // 준비된 호스트만 선택할 수 있으며, 로딩 중에는 모든 버튼을 잠급니다.
        private void StartMiniGame(string sceneName)
        {
            if (network == null || !network.IsServer || !startButton.interactable || loading) return;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[Character 이동] Build Settings에서 이동할 씬을 확인해 주세요: " + sceneName);
                return;
            }
            loading = true;
            Update();
            var status = network.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                loading = false;
                Debug.LogWarning("[Character 재시작] 씬 이동 실패: " + status);
            }
        }

        // 다음 복귀에서 이벤트와 클릭 처리가 중복되지 않도록 구독을 해제합니다.
        private void OnDestroy()
        {
            if (network != null && network.SceneManager != null)
                network.SceneManager.OnLoadEventCompleted -= OnLoadCompleted;
        }
    }
}
