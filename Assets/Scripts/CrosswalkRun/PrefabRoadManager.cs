using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 등록된 도로 청크 프리팹(Road Chunk Prefab) 풀에서 무작위로 청크를 선택하여
    /// 카메라 진행 방향 앞쪽으로 끊김 없이 무한 스폰하고,
    /// 카메라 뒤로 지나간 청크는 자동으로 회수/삭제하는 매니저입니다.
    /// 씬에 이미 배치된 청크들이 있다면 이를 시작 트랙으로 우선 등록합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrefabRoadManager : MonoBehaviour
    {
        [Header("카메라 추적")]
        [Tooltip("추적할 카메라 (비워둘 시 Camera.main 자동 감지)")]
        [SerializeField] private Transform targetCamera;

        [Header("시작 발판 프리팹 (선택)")]
        [Tooltip("게임 시작 시 플레이어가 서 있을 안전한 시작 인도 프리팹")]
        [SerializeField] private GameObject startPlatformPrefab;

        [Header("랜덤 도로 청크 프리팹 풀")]
        [Tooltip("무작위로 생성될 도로 청크 프리팹 목록 (각 프리팹 루트에 RoadChunk 컴포넌트 필요)")]
        [SerializeField] private GameObject[] roadChunkPrefabs;

        [Header("스트리밍 거리 설정")]
        [Tooltip("카메라 전방 유지 시야 거리 (미터) - 이 거리만큼 앞쪽에 도로를 미리 생성해 둡니다.")]
        [SerializeField] private float viewDistanceAhead = 90f;

        [Tooltip("카메라 후방 청크 삭제 거리 (미터) - 카메라 뒤로 완전히 통과한 청크를 파괴합니다.")]
        [SerializeField] private float despawnDistanceBehind = 50f;

        [Header("스폰 부모 트랜스폼")]
        [Tooltip("생성된 청크들이 묶일 부모 오브젝트 (비워둘 시 본 오브젝트 자식으로 생성)")]
        [SerializeField] private Transform chunksParent;

        [Header("안전지대 미끄러운 구역")]
        [SerializeField] private SlipperyZone slipperyZonePrefab;
        [SerializeField, Range(0f, 1f)] private float slipperyChance = 0.45f;
        [SerializeField] private Vector2 slipperyWidth = new Vector2(2f, 4.5f);
        [SerializeField] private Vector2 slipperyDepth = new Vector2(0.6f, 1.8f);
        [Tooltip("구역의 왼쪽 또는 오른쪽에 남길 최소 우회 폭입니다. 중앙 통로를 고정하지 않습니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("clearPathWidth")]
        [SerializeField, Min(1.5f)] private float minimumBypassWidth = 3f;
        [Tooltip("작을수록 방향 전환과 정지가 느려집니다. 단위: 초당 속도 변화량")]
        [SerializeField, Min(0.1f)] private float slipperyAcceleration = 2.5f;

        public bool IsLayoutReady { get; private set; }
        // 배치 설정의 메시지 구성이 바뀌었으므로 이전 형식과 구분합니다.
        private const string LayoutMessage = "Crosswalk.Road.Layout.V2";
        private Unity.Netcode.NetworkManager network;
        private System.Random roadRandom;
        private int layoutSeed;
        private int chunkSerial;

        private struct ActiveChunk
        {
            public GameObject Object;
            public float StartZ;
            public float EndZ;
        }

        private readonly Queue<ActiveChunk> activeChunks = new Queue<ActiveChunk>();
        private float nextSpawnZ = 0f;
        private int lastSpawnedIndex = -1;

        public int ActiveChunkCount => activeChunks.Count;
        public float NextSpawnZ => nextSpawnZ;

        private void Awake()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (chunksParent == null)
            {
                chunksParent = transform;
            }
            // 입장 시 한 번만 시드를 정하고, 모든 참가자의 씬 로딩 완료 후 전달합니다.
            network = Unity.Netcode.NetworkManager.Singleton;
            layoutSeed = System.Guid.NewGuid().GetHashCode();
            if (network != null && network.IsListening)
            {
                network.CustomMessagingManager.RegisterNamedMessageHandler(LayoutMessage, OnLayoutReceived);
                network.SceneManager.OnLoadEventCompleted += OnRoadSceneLoaded;
            }
        }

        private void Start()
        {
            // 참가자는 호스트 시드가 도착하기 전까지 랜덤 도로를 따로 만들지 않습니다.
            if (network == null || !network.IsListening || network.IsServer) InitializeLayout(layoutSeed);
        }

        /// <summary>
        /// 게임 시작 시 초기 트랙을 구축합니다.
        /// 씬에 이미 배치되어 있는 청크들이 있다면 그대로 첫 시작 트랙으로 등록하고,
        /// 없는 경우에만 시작 발판을 생성합니다. 이후 시야 거리만큼 도로를 확장합니다.
        /// </summary>
        public void SpawnInitialTrack()
        {
            if (!IsLayoutReady) return;
            roadRandom = new System.Random(layoutSeed);
            chunkSerial = 0;
            lastSpawnedIndex = -1;
            activeChunks.Clear();

            // 1. 씬에 이미 배치된 자식 청크들 수집
            List<RoadChunk> existingChunks = new List<RoadChunk>();
            for (int i = 0; i < chunksParent.childCount; i++)
            {
                RoadChunk rc = chunksParent.GetChild(i).GetComponent<RoadChunk>();
                if (rc != null)
                {
                    existingChunks.Add(rc);
                }
            }

            if (existingChunks.Count > 0)
            {
                // Z 위치 기준 오름차순 정렬
                existingChunks.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

                for (int i = 0; i < existingChunks.Count; i++)
                {
                    RoadChunk rc = existingChunks[i];
                    float startZ = rc.transform.position.z;
                    float endZ = startZ + rc.ChunkLength;

                    activeChunks.Enqueue(new ActiveChunk
                    {
                        Object = rc.gameObject,
                        StartZ = startZ,
                        EndZ = endZ
                    });

                    nextSpawnZ = endZ;
                }
            }
            else
            {
                // 씬에 사전 배치된 청크가 없는 경우 시작 발판 생성
                if (startPlatformPrefab != null)
                {
                    GameObject startObj = Instantiate(startPlatformPrefab, new Vector3(0, 0, -10f), Quaternion.identity, chunksParent);
                    startObj.name = "Sidewalk_Start_Platform";

                    RoadChunk startChunkComp = startObj.GetComponent<RoadChunk>();
                    float startLength = (startChunkComp != null) ? startChunkComp.ChunkLength : 11f;
                    nextSpawnZ = -10f + startLength;

                    activeChunks.Enqueue(new ActiveChunk
                    {
                        Object = startObj,
                        StartZ = -10f,
                        EndZ = nextSpawnZ
                    });
                }
                else
                {
                    nextSpawnZ = 0f;
                }
            }

            // 2. 카메라 시야 거리만큼 부족한 앞쪽 도로를 추가로 미리 채우기
            float targetZ = (targetCamera != null ? targetCamera.position.z : 0f) + viewDistanceAhead;
            while (nextSpawnZ < targetZ)
            {
                if (!SpawnNextRandomChunk())
                {
                    break;
                }
            }
        }

        private void Update()
        {
            if (!IsLayoutReady) return;
            if (targetCamera == null)
            {
                if (Camera.main != null)
                {
                    targetCamera = Camera.main.transform;
                }
                else
                {
                    return;
                }
            }

            // 1. 전방 스폰 검사: 카메라 전방 시야 거리만큼 도로 유지
            float targetFrontZ = targetCamera.position.z + viewDistanceAhead;
            while (nextSpawnZ < targetFrontZ)
            {
                if (!SpawnNextRandomChunk())
                {
                    break;
                }
            }

            // 2. 후방 삭제 검사: 카메라 뒤로 지나간 오래된 청크 파괴
            while (activeChunks.Count > 0)
            {
                ActiveChunk oldestChunk = activeChunks.Peek();
                if (targetCamera.position.z - oldestChunk.EndZ > despawnDistanceBehind)
                {
                    activeChunks.Dequeue();
                    if (oldestChunk.Object != null)
                    {
                        Destroy(oldestChunk.Object);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 풀에서 무작위 청크를 하나 선택하여 다음 Z 좌표에 오차 없이 이어 붙입니다.
        /// </summary>
        private bool SpawnNextRandomChunk()
        {
            if (roadChunkPrefabs == null || roadChunkPrefabs.Length == 0)
            {
                return false;
            }

            // 동일한 청크가 연속으로 나오는 빈도를 줄이는 랜덤 선택
            int chosenIndex = roadRandom.Next(roadChunkPrefabs.Length);
            if (roadChunkPrefabs.Length > 1 && chosenIndex == lastSpawnedIndex)
            {
                chosenIndex = (chosenIndex + 1) % roadChunkPrefabs.Length;
            }
            lastSpawnedIndex = chosenIndex;

            GameObject prefabToSpawn = roadChunkPrefabs[chosenIndex];
            if (prefabToSpawn == null)
            {
                return false;
            }

            Vector3 spawnPos = new Vector3(0f, 0f, nextSpawnZ);
            GameObject spawnedObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, chunksParent);

            // 길이 측정 (RoadChunk 컴포넌트 우선, 없을 경우 자동 계산)
            RoadChunk chunkComp = spawnedObj.GetComponent<RoadChunk>();
            float chunkLength;

            if (chunkComp != null)
            {
                chunkLength = chunkComp.ChunkLength;
            }
            else
            {
                chunkComp = spawnedObj.AddComponent<RoadChunk>();
                chunkLength = chunkComp.ChunkLength;
            }

            // 길이 계산 이후 배치하여 구역 외형이 도로의 자동 길이 계산에 영향을 주지 않게 합니다.
            AddSlipperyZones(spawnedObj);

            float chunkStartZ = nextSpawnZ;
            float chunkEndZ = chunkStartZ + chunkLength;

            activeChunks.Enqueue(new ActiveChunk
            {
                Object = spawnedObj,
                StartZ = chunkStartZ,
                EndZ = chunkEndZ
            });

            nextSpawnZ = chunkEndZ;
            return true;
        }

        // 네트워크 도로와 구역은 호스트 시드로 재현합니다. 차량의 기존 난수와 분리합니다.
        private void InitializeLayout(int seed)
        {
            if (IsLayoutReady) return;
            layoutSeed = seed;
            roadRandom = new System.Random(seed);
            IsLayoutReady = true;
            SpawnInitialTrack();
        }

        // 모든 참가자가 씬을 로드한 뒤 한 번만 배치 시드와 기믹 설정을 전달합니다.
        private void OnRoadSceneLoaded(string scene, UnityEngine.SceneManagement.LoadSceneMode mode,
            List<ulong> completed, List<ulong> timedOut)
        {
            if (!network.IsServer || scene != gameObject.scene.name) return;
            if (!IsLayoutReady) InitializeLayout(layoutSeed);
            using (var writer = new Unity.Netcode.FastBufferWriter(64, Unity.Collections.Allocator.Temp))
            {
                writer.WriteValueSafe(layoutSeed);
                writer.WriteValueSafe(slipperyChance);
                writer.WriteValueSafe(slipperyWidth);
                writer.WriteValueSafe(slipperyDepth);
                writer.WriteValueSafe(minimumBypassWidth);
                writer.WriteValueSafe(slipperyAcceleration);
                foreach (ulong client in completed)
                    if (client != Unity.Netcode.NetworkManager.ServerClientId && network.ConnectedClients.ContainsKey(client))
                        network.CustomMessagingManager.SendNamedMessage(LayoutMessage, client, writer);
            }
        }

        // 참가자는 호스트가 전송한 설정으로만 최초 도로를 생성합니다.
        private void OnLayoutReceived(ulong sender, Unity.Netcode.FastBufferReader reader)
        {
            if (network.IsServer || sender != Unity.Netcode.NetworkManager.ServerClientId || IsLayoutReady) return;
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out slipperyChance);
            reader.ReadValueSafe(out slipperyWidth);
            reader.ReadValueSafe(out slipperyDepth);
            reader.ReadValueSafe(out minimumBypassWidth);
            reader.ReadValueSafe(out slipperyAcceleration);
            InitializeLayout(seed);
        }

        // 현재 프리팹의 Sidewalk_* 바닥만 대상으로 하며 도로 차선과 시작 발판은 제외합니다.
        private void AddSlipperyZones(GameObject chunk)
        {
            int serial = ++chunkSerial;
            if (slipperyZonePrefab == null) return;
            var random = new System.Random(unchecked(layoutSeed ^ (serial * 486187739)));
            foreach (var floor in chunk.GetComponentsInChildren<BoxCollider>())
            {
                if (!floor.name.StartsWith("Sidewalk_", System.StringComparison.Ordinal) || floor.isTrigger) continue;
                if (random.NextDouble() >= Mathf.Clamp01(slipperyChance)) continue;
                Bounds bounds = floor.bounds;
                // 전체 안전지대가 배치 대상입니다. 가장자리 여백이나 중앙 고정 통로는 두지 않습니다.
                // 현재 캐릭터가 통과할 수 있도록 최소 1.5m 이상의 우회 폭을 확보합니다.
                float bypass = Mathf.Max(1.5f, minimumBypassWidth);
                float maxWidth = bounds.size.x - bypass;
                float maxDepth = bounds.size.z;
                if (maxWidth < 0.5f || maxDepth < 0.5f) continue;
                float width = Mathf.Min(maxWidth, SampleRange(random, slipperyWidth));
                float depth = Mathf.Min(maxDepth, SampleRange(random, slipperyDepth));
                float freeWidth = bounds.size.x - width;
                float leftGap;
                if (freeWidth >= bypass * 2f)
                {
                    // 어느 위치를 골라도 한쪽 통로가 충분하므로 중앙을 포함한 전체 범위에서 선택합니다.
                    leftGap = (float)random.NextDouble() * freeWidth;
                }
                else
                {
                    // 넓은 구역은 양쪽 틈이 모두 좁아지지 않도록 유효한 좌우 배치 범위에서만 선택합니다.
                    float range = Mathf.Max(0f, freeWidth - bypass);
                    leftGap = (float)random.NextDouble() * range;
                    if (random.Next(2) == 0) leftGap += bypass;
                }
                float x = bounds.min.x + leftGap + width * 0.5f;
                float z = bounds.min.z + depth * 0.5f +
                    (float)random.NextDouble() * (maxDepth - depth);
                var zone = Instantiate(slipperyZonePrefab,
                    new Vector3(x, bounds.max.y + 0.005f, z), Quaternion.identity);
                // 가로·세로만 조정하여 얇은 외형과 판정 높이를 유지합니다.
                zone.transform.localScale = new Vector3(width, 1f, depth);
                zone.transform.SetParent(chunk.transform, true);
                zone.Configure(slipperyAcceleration);
            }
        }

        // Inspector의 최소·최대 값이 뒤바뀌어도 양수 크기로 정규화합니다.
        private static float SampleRange(System.Random random, Vector2 range)
        {
            float low = Mathf.Max(0.5f, Mathf.Min(range.x, range.y));
            float high = Mathf.Max(low, Mathf.Max(range.x, range.y));
            return low + (high - low) * (float)random.NextDouble();
        }

        // 씬 복귀 시 메시지 구독을 해제하여 재시작에서도 이전 매니저가 남지 않게 합니다.
        private void OnDestroy()
        {
            if (network == null) return;
            if (network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= OnRoadSceneLoaded;
            network.CustomMessagingManager?.UnregisterNamedMessageHandler(LayoutMessage);
        }

        /// <summary>
        /// 활성화된 모든 청크를 제거합니다.
        /// </summary>
        public void ClearExistingTrack()
        {
            while (activeChunks.Count > 0)
            {
                ActiveChunk c = activeChunks.Dequeue();
                if (c.Object != null)
                {
                    Destroy(c.Object);
                }
            }
            nextSpawnZ = 0f;
        }
    }
}
