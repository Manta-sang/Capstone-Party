using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrosswalkRun
{
    // 기존 CustomSpawner를 유지하면서 Character 씬에 호스트용 재시작 버튼만 제공합니다.
    public sealed class CharacterRoundRestart : MonoBehaviour
    {
        private NetworkManager network;
        [Header("Character 씬에 배치된 UI 참조")]
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private TMPro.TMP_Text caption;
        [Header("실행 중 상태 안내 문구")]
        [SerializeField] private string disconnectedText = "Connect to a room first";
        [SerializeField] private string loadingText = "Loading Crosswalk...";
        [SerializeField] private string waitingHostText = "Waiting for host";
        [SerializeField] private string waitingPlayersText = "Waiting for players...";
        private string readyText;
        private bool loadCompleted;
        private bool loading;

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
            if (startButton == null || caption == null)
            {
                Debug.LogError("[Character 재시작] Inspector에서 Start Button과 Caption을 연결해 주세요.");
                if (startButton != null) startButton.interactable = false;
                enabled = false;
                return;
            }
            // 에디터의 Label 문구를 보관하여 준비 완료 시 사용자 지정 문구를 복원합니다.
            readyText = caption.text;
            startButton.onClick.AddListener(StartCrosswalk);
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
            if (startButton == null) return;
            bool online = network != null && network.IsListening;
            bool ready = online && network.IsServer && loadCompleted && !loading;
            if (ready)
                foreach (ulong id in network.ConnectedClientsIds)
                    if (network.SpawnManager.GetPlayerNetworkObject(id) == null) ready = false;
            startButton.interactable = ready;
            // 기본 버튼 문구는 Label에서, 상태별 안내는 이 컴포넌트의 Inspector에서 수정합니다.
            caption.text = !online ? disconnectedText : loading ? loadingText :
                !network.IsServer ? waitingHostText : ready ? readyText : waitingPlayersText;
        }

        // 기존 방을 유지한 네트워크 씬 전환이며 클릭을 연속해도 한 번만 실행합니다.
        private void StartCrosswalk()
        {
            if (network == null || !network.IsServer || !startButton.interactable || loading) return;
            loading = true;
            var status = network.SceneManager.LoadScene("CrosswalkRun", LoadSceneMode.Single);
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
            if (startButton != null) startButton.onClick.RemoveListener(StartCrosswalk);
        }
    }
}
