using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 생성, 방 참가, 방 나가기 UI를 관리합니다.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("연결할 스크립트")]
    [SerializeField] private LobbyRelayManager lobbyRelayManager;

    [Header("UI 컴포넌트")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text roomCodeText;

    private void Start()
    {
        // 각 버튼에 기능을 연결합니다.
        createRoomButton.onClick.AddListener(CreateRoom);
        joinRoomButton.onClick.AddListener(JoinRoom);
        leaveRoomButton.onClick.AddListener(LeaveRoom);

        // LobbyRelayManager의 상태 메시지를 화면에도 출력합니다.
        lobbyRelayManager.StatusChanged += UpdateStatus;

        statusText.text = "방을 만들거나 방 코드를 입력하세요.";
        roomCodeText.text = "방 코드: -";
    }

    private void Update()
    {
        // 방장이 방을 만들면 생성된 Lobby Code를 화면에 표시합니다.
        if (!string.IsNullOrEmpty(lobbyRelayManager.CurrentLobbyCode))
        {
            roomCodeText.text = $"방 코드: {lobbyRelayManager.CurrentLobbyCode}";
        }
    }

    private void CreateRoom()
    {
        lobbyRelayManager.CreateRoom();
    }

    private void JoinRoom()
    {
        // Input Field에 입력된 방 코드를 LobbyRelayManager로 전달합니다.
        lobbyRelayManager.JoinRoom(roomCodeInput.text);
    }

    private void LeaveRoom()
    {
        lobbyRelayManager.LeaveRoom();
        roomCodeText.text = "방 코드: -";
    }

    private void UpdateStatus(string message)
    {
        statusText.text = message;
    }

    private void OnDestroy()
    {
        // 오브젝트가 삭제될 때 이벤트 연결을 해제합니다.
        if (lobbyRelayManager != null)
        {
            lobbyRelayManager.StatusChanged -= UpdateStatus;
        }
    }
}