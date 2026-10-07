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
        }

        private void Start()
        {
            SpawnInitialTrack();
        }

        /// <summary>
        /// 게임 시작 시 초기 트랙을 구축합니다.
        /// 씬에 이미 배치되어 있는 청크들이 있다면 그대로 첫 시작 트랙으로 등록하고,
        /// 없는 경우에만 시작 발판을 생성합니다. 이후 시야 거리만큼 도로를 확장합니다.
        /// </summary>
        public void SpawnInitialTrack()
        {
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
            int chosenIndex = Random.Range(0, roadChunkPrefabs.Length);
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
