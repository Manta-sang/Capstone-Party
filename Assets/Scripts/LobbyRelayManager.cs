using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

/// <summary>
/// Lobby Code를 통한 방 생성, 참가, 퇴장을 담당합니다.
/// 방장은 NGO Host로 실행되고, 참가자는 Relay를 통해 Host에 연결됩니다.
/// </summary>
public class LobbyRelayManager : MonoBehaviour
{
    // 방장 포함 전체 방 정원입니다.
    private const int MaxPlayers = 4;

    // Lobby 안에 Relay Join Code를 저장할 때 사용할 이름입니다.
    private const string RelayJoinCodeKey = "relayJoinCode";

    // PC 환경에서 사용할 Relay 연결 방식입니다.
    private const string ConnectionType = "dtls";

    [Header("필수 컴포넌트")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;

    // 현재 만들었거나 참가한 Lobby 정보입니다.
    private Lobby currentLobby;

    // 현재 사용자가 방장인지 저장합니다.
    private bool isHost;
    private bool busy;
    public static string RoomNumber { get; private set; }
    [SerializeField] private string waitingScene = "Character";

    // 공개 검색 인덱스로 네 자리 번호만 공유하고 Relay 접속 정보는 멤버에게만 공개한다.
    private static async System.Threading.Tasks.Task<List<Lobby>> FindRooms(string number)
    {
        var result = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
        {
            Count = 2,
            Filters = new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.S1, number, QueryFilter.OpOptions.EQ) }
        });
        return result.Results;
    }
    private static async System.Threading.Tasks.Task<Lobby> CreateNumberedRoom(CreateLobbyOptions options)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string number = UnityEngine.Random.Range(0, 10000).ToString("D4");
            if ((await FindRooms(number)).Count == 0)
            {
                options.Data["partyNumber"] = new DataObject(DataObject.VisibilityOptions.Public, number, DataObject.IndexOptions.S1);
                var room = await LobbyService.Instance.CreateLobbyAsync("Party " + number, MaxPlayers, options);
                bool selected = false;
                try
                {
                    // 같은 시점에 선택된 번호나 아직 검색되지 않는 방은 사용자에게 공개하지 않는다.
                    await System.Threading.Tasks.Task.Delay(1200);
                    var matches = await FindRooms(number);
                    selected = matches.Count == 1 && matches[0].Id == room.Id;
                    if (selected) return room;
                }
                finally { if (!selected) await LobbyService.Instance.DeleteLobbyAsync(room.Id); }
            }
            await System.Threading.Tasks.Task.Delay(1200);
        }
        throw new Exception("사용 가능한 방번호를 찾지 못했습니다. 다시 시도하세요.");
    }

    // 방장이 Lobby를 유지하기 위해 보내는 heartbeat 코루틴입니다.
    private Coroutine heartbeatCoroutine;

    /// <summary>
    /// UI가 상태 메시지를 받을 수 있도록 알립니다.
    /// </summary>
    public event Action<string> StatusChanged;

    private void Awake()
    {
        // Inspector에 참조를 넣지 않았어도 같은 오브젝트에서 자동 탐색합니다.
        if (networkManager == null)
        {
            networkManager = GetComponent<NetworkManager>();
        }

        if (unityTransport == null)
        {
            unityTransport = GetComponent<UnityTransport>();
        }
        if (networkManager != null) networkManager.OnClientStopped += OnNetworkStopped;
    }

    private void OnDestroy()
    {
        if (networkManager != null) networkManager.OnClientStopped -= OnNetworkStopped;
    }

    // 연결 타임아웃이나 호스트 종료 시에도 Lobby 슬롯과 로컬 방번호를 해제한다.
    private async void OnNetworkStopped(bool wasHost)
    {
        if (currentLobby == null) return;
        busy = true;
        try { await ClearRoom(false); SetStatus("방 연결이 종료되었습니다."); }
        finally { busy = false; }
    }

    /// <summary>
    /// 방장을 Host로 시작하고 새로운 Lobby를 생성합니다.
    /// </summary>
    public async void CreateRoom()
    {
        if (!CanStartNetwork())
        {
            return;
        }

        try
        {
            busy = true;
            SetStatus("Relay와 방을 생성하는 중입니다...");

            // 방장을 제외한 참가자 최대 3명을 위한 Relay 공간을 생성합니다.
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);

            // NGO 통신이 Relay를 통과하도록 설정합니다.
            unityTransport.SetRelayServerData(
                new RelayServerData(allocation, ConnectionType)
            );

            // 참가자가 Relay에 연결할 때 필요한 코드를 발급받습니다.
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(
                allocation.AllocationId
            );

            // Lobby 안에 Relay Join Code를 멤버 전용 데이터로 저장합니다.
            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        RelayJoinCodeKey,
                        new DataObject(
                            DataObject.VisibilityOptions.Member,
                            relayJoinCode
                        )
                    }
                }
            };

            // 참가자가 공유받을 Lobby Code를 생성합니다.
            currentLobby = await CreateNumberedRoom(lobbyOptions);
            RoomNumber = currentLobby.Data["partyNumber"].Value;
            isHost = true;
            StartHeartbeat();

            // 방장은 서버와 클라이언트를 함께 실행합니다.
            if (!networkManager.StartHost())
            {
                throw new Exception("NGO Host를 시작하지 못했습니다.");
            }

            SetStatus("대기실로 이동 중…");
            var status = networkManager.SceneManager.LoadScene(waitingScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (status != Unity.Netcode.SceneEventProgressStatus.Started) throw new Exception($"대기실 이동 실패: {status}");
        }
        catch (Exception exception)
        {
            await ClearRoom();
            SetStatus($"방 생성 실패: {exception.Message}", true);
        }
        finally { busy = false; }
    }

    /// <summary>
    /// 참가자가 Lobby Code를 입력해 Host의 방에 접속합니다.
    /// </summary>
    public async void JoinRoom(string lobbyCode)
    {
        if (!CanStartNetwork())
        {
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(lobbyCode ?? "", @"^[0-9]{4}$"))
        {
            SetStatus("숫자 네 자리 방번호를 입력하세요.", true);
            return;
        }

        try
        {
            busy = true;
            SetStatus("방에 참가하는 중입니다...");

            // 사용자가 입력한 Lobby Code로 방에 참가합니다.
            var rooms = await FindRooms(lobbyCode);
            if (rooms.Count != 1) throw new Exception(rooms.Count == 0 ? "해당 방번호를 찾지 못했습니다." : "방번호가 중복되어 참가할 수 없습니다.");
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(rooms[0].Id);
            RoomNumber = lobbyCode;

            isHost = false;

            // 방에 들어온 멤버만 Relay Join Code를 읽을 수 있습니다.
            if (!currentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayData))
            {
                throw new Exception("Relay 연결 정보를 찾지 못했습니다.");
            }

            // Relay에 참가하고 NGO Client용 연결 정보를 설정합니다.
            var allocation = await RelayService.Instance.JoinAllocationAsync(relayData.Value);

            unityTransport.SetRelayServerData(
                new RelayServerData(allocation, ConnectionType)
            );

            // 참가자는 Client로 실행합니다.
            if (!networkManager.StartClient())
            {
                throw new Exception("NGO Client를 시작하지 못했습니다.");
            }

            SetStatus("방 참가 완료 / Host 연결을 기다리는 중입니다.");
        }
        catch (Exception exception)
        {
            await ClearRoom();
            SetStatus($"방 참가 실패: {exception.Message}", true);
        }
        finally { busy = false; }
    }

    /// <summary>
    /// 현재 방에서 나가고 NGO 연결을 종료합니다.
    /// </summary>
    public async void LeaveRoom()
    {
        if (busy) return;
        busy = true;
        try { await ClearRoom(); SetStatus("방에서 나왔습니다."); }
        finally { busy = false; }
    }

    // 연결 실패와 자발적 퇴장에서 같은 방식으로 Lobby 멤버십을 정리한다.
    private async System.Threading.Tasks.Task ClearRoom(bool stopNetwork = true)
    {
        StopHeartbeat();
        var room = currentLobby;
        bool owned = isHost;
        currentLobby = null;
        isHost = false;
        RoomNumber = null;
        if (stopNetwork && networkManager.IsListening) networkManager.Shutdown();
        if (room == null) return;
        try
        {
            if (owned) await LobbyService.Instance.DeleteLobbyAsync(room.Id);
            else await LobbyService.Instance.RemovePlayerAsync(room.Id, AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e) { Debug.LogWarning($"방 정리 실패: {e.Message}"); }
    }

    /// <summary>
    /// Host/Client 시작 전에 로그인 및 중복 실행 여부를 확인합니다.
    /// </summary>
    private bool CanStartNetwork()
    {
        if (busy || currentLobby != null) return false;
        if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
        {
            SetStatus("UGS 로그인이 아직 완료되지 않았습니다.", true);
            return false;
        }

        if (networkManager.IsListening)
        {
            SetStatus("이미 방에 접속해 있습니다.", true);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Lobby가 자동으로 삭제되지 않도록 방장이 주기적으로 heartbeat를 보냅니다.
    /// </summary>
    private void StartHeartbeat()
    {
        StopHeartbeat();
        heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }

    private IEnumerator HeartbeatRoutine()
    {
        while (isHost && currentLobby != null)
        {
            // Lobby는 일정 시간 활동이 없으면 사라질 수 있으므로 15초마다 유지 신호를 보냅니다.
            yield return new WaitForSecondsRealtime(15f);
            SendHeartbeat();
        }
    }

    private async void SendHeartbeat()
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
        catch (Exception exception)
        {
            SetStatus($"Lobby 유지 신호 실패: {exception.Message}", true);
        }
    }

    /// <summary>
    /// Console과 UI에 동일한 상태 메시지를 전달합니다.
    /// </summary>
    private void SetStatus(string message, bool isError = false)
    {
        if (isError)
        {
            Debug.LogError($"[Lobby] {message}");
        }
        else
        {
            Debug.Log($"[Lobby] {message}");
        }

        StatusChanged?.Invoke(message);
    }
}