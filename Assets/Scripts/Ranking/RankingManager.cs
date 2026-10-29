using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CrosswalkRun
{
    public struct PlayerScoreData : INetworkSerializable, IEquatable<PlayerScoreData>
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;
        public int Score;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Score);
        }

        public bool Equals(PlayerScoreData other) => ClientId == other.ClientId;
    }

    public class RankingManager : NetworkBehaviour
    {
        public static RankingManager Instance { get; private set; }

        public NetworkList<PlayerScoreData> ScoreList;

        // �� ���� �� ��° �������� ��� (�⺻�� 1)
        public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);

        [Header("�̴ϰ��� �� �帧 ����")]
        [Tooltip("������� 1~4���� �̴ϰ��� �� �̸��� �����ּ���.")]
        public string[] MiniGameScenes = new string[4] { "CrosswalkRun", "Game2", "Game3", "Game4" };
        public string TitleSceneName = "Lobby";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ScoreList = new NetworkList<PlayerScoreData>();
        }

        public void AddScore(ulong clientId, string playerName, int points)
        {
            if (!IsServer) return;

            for (int i = 0; i < ScoreList.Count; i++)
            {
                if (ScoreList[i].ClientId == clientId)
                {
                    var data = ScoreList[i];
                    data.Score += points;
                    data.PlayerName = playerName;
                    ScoreList[i] = data;
                    return;
                }
            }

            ScoreList.Add(new PlayerScoreData
            {
                ClientId = clientId,
                PlayerName = playerName,
                Score = points
            });
        }
    }
}