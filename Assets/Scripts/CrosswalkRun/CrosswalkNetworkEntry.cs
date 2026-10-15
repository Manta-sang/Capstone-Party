using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrosswalkRun
{
    /// <summary>
    /// 기존 방 연결을 그대로 사용하여 Crosswalk 입장, 캐릭터 생성, 참가 등록과 시작 시점만 연결합니다.
    /// 차량 생성 및 탈락·승리 판정은 기존 코드에 맡기며 이 클래스에서 변경하지 않습니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CrosswalkNetworkEntry : MonoBehaviour
    {
        [Header("Character 씬과 같은 순서의 기존 캐릭터 프리팹")]
        [SerializeField] private GameObject[] characterPrefabs;
        [Header("참가 순서대로 배정할 4개의 시작 위치")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private CrosswalkGameManager gameManager;
        [SerializeField] private AutoScrollCamera scrollCamera;

        private const string RosterMessage = "Crosswalk.Entry.Roster";
        private const string ReadyMessage = "Crosswalk.Entry.Ready";
        private const string StartMessage = "Crosswalk.Entry.Start";
        private const float ReadyTimeout = 30f;
        private NetworkManager network;
        private NetworkSceneManager sceneManager;
        private CustomMessagingManager messages;
        private readonly List<ulong> owners = new List<ulong>();
        private readonly List<ulong> objectIds = new List<ulong>();
        private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
        private readonly Dictionary<Behaviour, bool> pausedControls = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<Rigidbody, bool> pausedBodies = new Dictionary<Rigidbody, bool>();
        private int entryToken;
        private bool spawnRequested;
        private bool rosterReceived;
        private bool localReady;
        private bool started;
        private bool failed;
        private double startsAt = -1;
        private float readyDeadline;

        // 전체 씬 로딩 완료 알림을 놓치지 않도록 Start보다 먼저 구독합니다.
        private void Awake()
        {
            network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                Debug.LogWarning("[Crosswalk 입장] SampleScene에서 기존 방 생성/참가 후 시작 버튼을 눌러 주세요.");
                enabled = false;
                return;
            }
            sceneManager = network.SceneManager;
            messages = network.CustomMessagingManager;
            sceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            messages.RegisterNamedMessageHandler(RosterMessage, OnRosterReceived);
            messages.RegisterNamedMessageHandler(ReadyMessage, OnReadyReceived);
            messages.RegisterNamedMessageHandler(StartMessage, OnStartReceived);
        }

        // 호스트만 생성합니다. 로딩 실패자가 있으면 일부 인원만으로 조용히 시작하지 않습니다.
        private void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!network.IsServer || sceneName != gameObject.scene.name || spawnRequested) return;
            spawnRequested = true;
            if (timedOut.Count > 0)
            {
                Fail("참가자의 씬 로딩이 시간 초과되었습니다. 방에서 다시 시작해 주세요.");
                return;
            }
            var clients = network.ConnectedClientsIds.OrderBy(id => id).ToArray();
            if (clients.Any(id => !completed.Contains(id)))
            {
                Fail("아직 로딩을 완료하지 않은 참가자가 있습니다.");
                return;
            }
            StartCoroutine(SpawnPlayers(clients));
        }

        // 기존 CustomSpawner와 같은 캐릭터 배정 규칙을 사용하되 위치는 접속 ID가 아닌 참가 순번으로 배정합니다.
        private IEnumerator SpawnPlayers(ulong[] clients)
        {
            yield return null; // 이전 씬의 오브젝트 정리와 현재 씬 초기화가 끝난 다음 생성합니다.
            if (network == null || !network.IsServer) yield break;
            clients = clients.Where(id => network.ConnectedClients.ContainsKey(id)).ToArray();
            if (clients.Length == 0 || clients.Length > 4 || spawnPoints == null || spawnPoints.Length < clients.Length ||
                characterPrefabs == null || characterPrefabs.Length == 0 || gameManager == null || scrollCamera == null ||
                spawnPoints.Take(clients.Length).Any(point => point == null) ||
                characterPrefabs.Any(prefab => prefab == null || prefab.GetComponent<NetworkObject>() == null))
            {
                Fail("캐릭터 프리팹, 게임 매니저 또는 시작 위치 설정을 확인해 주세요.");
                yield break;
            }
            // 기존 PlayerObject를 덮어쓰지 않고 중복 생성 설정을 명확히 알립니다.
            if (clients.Any(id => network.SpawnManager.GetPlayerNetworkObject(id) != null))
            {
                Fail("이전 PlayerObject가 남아 있습니다. 자동 생성 설정을 확인해 주세요.");
                yield break;
            }

            entryToken = Guid.NewGuid().GetHashCode();
            for (int slot = 0; slot < clients.Length; slot++)
            {
                ulong id = clients[slot];
                int character = (int)(id % (ulong)characterPrefabs.Length);
                var instance = Instantiate(characterPrefabs[character], spawnPoints[slot].position, spawnPoints[slot].rotation);
                var player = instance.GetComponent<NetworkObject>();
                player.SpawnAsPlayerObject(id, true);
                owners.Add(id);
                objectIds.Add(player.NetworkObjectId);
                Debug.Log($"[Crosswalk 입장] 참가자 {id}, 캐릭터 {character}, 시작 위치 {slot + 1} 생성 완료");
            }

            // 생성 메시지와 참가 목록의 도착 순서가 달라도 각 클라이언트가 실제 오브젝트를 확인한 뒤 준비 응답합니다.
            rosterReceived = true;
            readyDeadline = Time.realtimeSinceStartup + ReadyTimeout;
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(entryToken);
                writer.WriteValueSafe(owners.Count);
                for (int i = 0; i < owners.Count; i++)
                {
                    writer.WriteValueSafe(owners[i]);
                    writer.WriteValueSafe(objectIds[i]);
                }
                foreach (ulong id in owners)
                    if (id != NetworkManager.ServerClientId)
                        messages.SendNamedMessage(RosterMessage, id, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        // 참가 목록은 서버가 보낸 것만 받습니다. 중복 메시지로 등록 상태를 초기화하지 않습니다.
        private void OnRosterReceived(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer || rosterReceived || failed) return;
            reader.ReadValueSafe(out entryToken);
            reader.ReadValueSafe(out int count);
            if (count < 1 || count > 4) { Fail("잘못된 참가자 수를 받았습니다."); return; }
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong owner);
                reader.ReadValueSafe(out ulong objectId);
                owners.Add(owner);
                objectIds.Add(objectId);
            }
            rosterReceived = true;
            readyDeadline = Time.realtimeSinceStartup + ReadyTimeout;
        }

        // 생성된 각자의 캐릭터를 모든 화면에서 등록한 다음에만 준비 응답과 게임 시작을 진행합니다.
        private void Update()
        {
            if (network == null || !network.IsListening || failed || started || !rosterReceived) return;
            if (!localReady)
            {
                bool allPresent = true;
                for (int i = 0; i < objectIds.Count; i++)
                {
                    if (!network.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i], out var player) ||
                        !player.IsPlayerObject || player.OwnerClientId != owners[i])
                    { allPresent = false; continue; }
                    PauseControls(player.gameObject);
                    gameManager.RegisterPlayer(player.gameObject);
                }
                if (allPresent)
                {
                    localReady = true;
                    if (network.IsServer) readyClients.Add(network.LocalClientId);
                    else
                    {
                        using (var writer = new FastBufferWriter(4, Allocator.Temp))
                        {
                            writer.WriteValueSafe(entryToken);
                            messages.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer);
                        }
                    }
                }
            }

            if (network.IsServer && startsAt < 0 && owners.All(readyClients.Contains)) ScheduleStart();
            if (startsAt >= 0 && localReady && network.ServerTime.Time >= startsAt)
            {
                // 프리팹 원본은 수정하지 않고, 이번 씬의 생성 인스턴스에 기존 참가/탈락 컴포넌트만 연결합니다.
                foreach (ulong id in objectIds)
                    if (network.SpawnManager.SpawnedObjects.TryGetValue(id, out var player) && player.GetComponent<CrosswalkPlayer>() == null)
                        player.gameObject.AddComponent<CrosswalkPlayer>();
                RestoreControls();
                started = true;
                gameManager.StartGame();
                Debug.Log("[Crosswalk 입장] 전체 참가자 등록 완료, 게임 시작");
            }
            else if (startsAt < 0 && Time.realtimeSinceStartup > readyDeadline)
                Fail("플레이어 준비 확인 시간이 초과되었습니다. 연결 상태를 확인해 주세요.");
        }

        // 기다리는 동안에만 이동·밀치기와 물리를 잠시 보류하고 기존 enabled/kinematic 값을 보관합니다.
        private void PauseControls(GameObject player)
        {
            foreach (Behaviour control in new Behaviour[] { player.GetComponent<PlayerMovement>(), player.GetComponent<PlayerInteraction>() })
                if (control != null && !pausedControls.ContainsKey(control))
                {
                    pausedControls.Add(control, control.enabled);
                    control.enabled = false;
                }
            var body = player.GetComponent<Rigidbody>();
            if (body != null && !pausedBodies.ContainsKey(body))
            {
                pausedBodies.Add(body, body.isKinematic);
                body.isKinematic = true;
            }
        }

        // 게임 시작 또는 씬 종료 시 이 컴포넌트가 잠근 값만 원래대로 돌립니다.
        private void RestoreControls()
        {
            foreach (var pair in pausedControls) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in pausedBodies) if (pair.Key != null) pair.Key.isKinematic = pair.Value;
            pausedControls.Clear();
            pausedBodies.Clear();
        }

        // 서버는 이번 참가 목록에 있는 클라이언트의 준비 응답만 인정합니다.
        private void OnReadyReceived(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || failed || !rosterReceived || !owners.Contains(sender)) return;
            reader.ReadValueSafe(out int token);
            if (token == entryToken) readyClients.Add(sender);
        }

        // 기존 카메라의 대기 시간을 사용하여 서버 시간 기준의 공통 시작 시점을 전달합니다.
        private void ScheduleStart()
        {
            startsAt = network.ServerTime.Time + Math.Max(1.0, scrollCamera.StartDelay);
            using (var writer = new FastBufferWriter(12, Allocator.Temp))
            {
                writer.WriteValueSafe(entryToken);
                writer.WriteValueSafe(startsAt);
                foreach (ulong id in owners)
                    if (id != NetworkManager.ServerClientId)
                        messages.SendNamedMessage(StartMessage, id, writer);
            }
        }

        // 클라이언트는 호스트가 이번 입장에 대해 지정한 시작 시각만 사용합니다.
        private void OnStartReceived(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || network.IsServer || failed || !rosterReceived) return;
            reader.ReadValueSafe(out int token);
            reader.ReadValueSafe(out double time);
            if (token == entryToken) startsAt = time;
        }

        // 실패 시 일부 인원만으로 시작하지 않고 원인을 콘솔과 기존 게임 상태 표시로 알립니다.
        private void Fail(string reason)
        {
            failed = true;
            if (gameManager != null) gameManager.SetNetworkWaitingStatus(reason);
            Debug.LogError("[Crosswalk 입장] " + reason);
        }

        // 씬을 나갈 때 구독과 메시지 핸들러를 해제하여 다음 입장에 중복 실행되지 않게 합니다.
        private void OnDestroy()
        {
            RestoreControls();
            if (sceneManager != null) sceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
            if (messages == null) return;
            messages.UnregisterNamedMessageHandler(RosterMessage);
            messages.UnregisterNamedMessageHandler(ReadyMessage);
            messages.UnregisterNamedMessageHandler(StartMessage);
        }
    }
}
