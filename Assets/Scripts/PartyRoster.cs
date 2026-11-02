using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// RankingManager와 같은 지속 NetworkObject에서 현재 연결된 멤버만 동기화한다.
public class PartyRoster : NetworkBehaviour
{
    public struct Member : INetworkSerializable, IEquatable<Member>
    {
        public ulong Id;
        public FixedString32Bytes Name;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref Id); serializer.SerializeValue(ref Name); }
        public bool Equals(Member other) => Id == other.Id && Name.Equals(other.Name);
    }
    public static PartyRoster Current { get; private set; }
    public NetworkList<Member> Members;
    private void Awake() => Members = new NetworkList<Member>();
    public override void OnNetworkSpawn()
    {
        Current = this;
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += RemoveMember;
            NetworkManager.OnClientConnectedCallback += AddMember;
            foreach (ulong id in NetworkManager.ConnectedClientsIds) AddMember(id);
        }
        if (IsClient) RegisterServerRpc(new FixedString32Bytes(PartyTitle.Nickname));
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= RemoveMember;
            NetworkManager.OnClientConnectedCallback -= AddMember;
        }
        if (Current == this) Current = null;
    }

    private int Index(ulong id)
    {
        for (int i = 0; i < Members.Count; i++) if (Members[i].Id == id) return i;
        return -1;
    }
    private void AddMember(ulong id)
    {
        if (Index(id) < 0) Members.Add(new Member { Id = id, Name = new FixedString32Bytes($"Player {id}") });
    }
    private void RemoveMember(ulong id)
    {
        int index = Index(id);
        if (index >= 0) Members.RemoveAt(index);
    }
    // RPC 발신자의 ID를 사용하므로 다른 사람의 이름은 바꿀 수 없다.
    [ServerRpc(RequireOwnership = false)]
    private void RegisterServerRpc(FixedString32Bytes name, ServerRpcParams rpc = default)
    {
        ulong id = rpc.Receive.SenderClientId;
        if (!PartyTitle.ValidName(name.ToString()) || !NetworkManager.ConnectedClients.ContainsKey(id)) return;
        AddMember(id);
        Members[Index(id)] = new Member { Id = id, Name = name };
    }
    public string NameFor(ulong id, string fallback)
    {
        int index = Index(id);
        return index < 0 ? fallback : Members[index].Name.ToString();
    }
}
