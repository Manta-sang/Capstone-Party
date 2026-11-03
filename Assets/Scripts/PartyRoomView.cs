using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// 같은 명단을 방 화면과 대기실에서 표시한다. 이름표는 Character 씬에서만 연결한다.
public class PartyRoomView : MonoBehaviour
{
    [SerializeField] private TMP_Text roomInfo;
    [SerializeField] private TMP_Text nameTemplate;
    private readonly List<TMP_Text> tags = new List<TMP_Text>();
    private PartyRoster roster;
    private string roomCode;
    private void LateUpdate()
    {
        var current = PartyRoster.Current;
        string code = LobbyRelayManager.RoomNumber;
        if (current != roster || code != roomCode)
        {
            if (roster != null) roster.Members.OnListChanged -= OnMembersChanged;
            roster = current;
            roomCode = code;
            if (roster != null) roster.Members.OnListChanged += OnMembersChanged;
            Refresh();
        }
        if (nameTemplate == null || roster == null || Camera.main == null) return;
        for (int i = 0; i < roster.Members.Count; i++)
        {
            var member = roster.Members[i];
            NetworkObject player = null;
            // 클라이언트에서는 다른 사람의 PlayerObject를 SpawnManager의 로컬 조회로 얻을 수 없다.
            if (!NetworkManager.Singleton.IsServer && member.Id != NetworkManager.Singleton.LocalClientId)
            {
                foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
                    if (obj.IsPlayerObject && obj.OwnerClientId == member.Id) { player = obj; break; }
            }
            else player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(member.Id);
            var label = tags[i];
            Vector3 point = player == null ? Vector3.back : Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up * 2.2f);
            label.gameObject.SetActive(point.z > 0);
            if (point.z > 0) label.rectTransform.position = new Vector3(point.x, point.y, 0);
        }
    }
    private void OnMembersChanged(NetworkListEvent<PartyRoster.Member> change) => Refresh();
    private void Refresh()
    {
        int count = roster == null ? 0 : roster.Members.Count;
        var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            string name = roster.Members[i].Name.ToString();
            names.Add(name);
            if (nameTemplate == null) continue;
            if (tags.Count == i) tags.Add(Instantiate(nameTemplate, nameTemplate.transform.parent));
            tags[i].text = name;
        }
        for (int i = count; i < tags.Count; i++) tags[i].gameObject.SetActive(false);
        roomInfo.text = $"방번호 {roomCode ?? "----"}   인원 {count}/4\n{string.Join(" · ", names)}";
    }
    private void OnEnable() => Refresh();
    private void OnDestroy() { if (roster != null) roster.Members.OnListChanged -= OnMembersChanged; }
}
