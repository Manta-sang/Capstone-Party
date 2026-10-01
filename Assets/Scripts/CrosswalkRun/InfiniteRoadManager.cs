using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 오토스크롤 카메라의 진행에 맞춰 무한히 도로 청크(차도, 횡단보도, 안전지대, 차량 스포너)를 생성하고
    /// 지나간 청크를 안전하게 회수/삭제하는 무한 맵 매니저입니다.
    /// 모든 바닥의 상단 높이를 Y = 0으로 완벽히 통일하고, 빈틈 없는 좌표 연결과 마찰 없는 표면을 제공합니다.
    /// </summary>
    public class InfiniteRoadManager : MonoBehaviour
    {
        [Header("카메라 추적")]
        [SerializeField] private Transform targetCamera;

        [Header("청크 생성 설정")]
        [Tooltip("카메라 전방 유지 시야 거리 (미터)")]
        [SerializeField] private float viewDistanceAhead = 90f;
        [Tooltip("카메라 후방 완전 통과 후 청크 삭제 거리 (미터)")]
        [SerializeField] private float despawnDistanceBehind = 55f;

        [Header("도로 및 차선 규격")]
        [SerializeField] private float roadWidth = 36f; // X축 도로 폭
        [SerializeField] private float laneDepth = 4.5f; // 각 차선의 Z축 폭
        [SerializeField] private int lanesPerChunk = 3;  // 청크당 차선 개수
        [SerializeField] private float sidewalkDepth = 3.5f; // 안전지대(인도) Z축 폭
        [SerializeField] private float floorThickness = 0.5f; // 바닥 두께

        [Header("색상 테마")]
        [SerializeField] private Color asphaltColor = new Color(0.2f, 0.2f, 0.22f);
        [SerializeField] private Color stripeColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color sidewalkColor = new Color(0.6f, 0.6f, 0.62f);

        private struct ChunkData
        {
            public GameObject ChunkObject;
            public float EndZ;
        }

        private readonly Queue<ChunkData> activeChunks = new Queue<ChunkData>();
        private float nextSpawnZ = 0f;
        private int chunkIndex = 0;

        private Material matAsphalt;
        private Material matStripe;
        private Material matSidewalk;
        private PhysicMaterial frictionlessMat;

        private void Awake()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            InitMaterials();
            InitPhysicsMaterial();
        }

        private void InitMaterials()
        {
            Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            matAsphalt = new Material(standardShader) { color = asphaltColor };
            matStripe = new Material(standardShader) { color = stripeColor };
            matSidewalk = new Material(standardShader) { color = sidewalkColor };
        }

        private void InitPhysicsMaterial()
        {
            // 바닥 이음매(Seam) 걸림 방지를 위해 마찰력 0의 물리 머티리얼 적용
            frictionlessMat = new PhysicMaterial("FrictionlessRoad")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicMaterialCombine.Minimum
            };
        }

        private void Start()
        {
            CreateStartingPlatform();

            while (nextSpawnZ < viewDistanceAhead)
            {
                SpawnNextChunk();
            }
        }

        private void Update()
        {
            if (targetCamera == null) return;

            // 1. 카메라 전방에 도로 연속 생성
            while (nextSpawnZ - targetCamera.position.z < viewDistanceAhead)
            {
                SpawnNextChunk();
            }

            // 2. 청크의 끝 지점(EndZ)까지 카메라 후방 삭제 거리를 완전히 벗어났을 때만 안전 삭제
            if (activeChunks.Count > 0)
            {
                ChunkData oldest = activeChunks.Peek();
                if (targetCamera.position.z - oldest.EndZ > despawnDistanceBehind)
                {
                    activeChunks.Dequeue();
                    if (oldest.ChunkObject != null)
                    {
                        Destroy(oldest.ChunkObject);
                    }
                }
            }
        }

        private void CreateStartingPlatform()
        {
            float startPlatformDepth = 14f;
            float startCenterZ = -startPlatformDepth * 0.5f + 1.0f; // 끝이 정확히 Z = 1.0f가 되도록 배치

            GameObject startPlatform = new GameObject("Start_Platform");
            startPlatform.transform.SetParent(transform);
            startPlatform.transform.position = new Vector3(0, 0, startCenterZ);

            GameObject sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sidewalk.name = "Sidewalk_Floor";
            sidewalk.transform.SetParent(startPlatform.transform, false);
            // 상단 표면을 정확히 Y = 0.0f로 맞춤
            sidewalk.transform.localPosition = new Vector3(0, -floorThickness * 0.5f, 0);
            sidewalk.transform.localScale = new Vector3(roadWidth, floorThickness, startPlatformDepth);
            sidewalk.GetComponent<Renderer>().material = matSidewalk;

            ApplyFrictionlessMaterial(sidewalk);

            // 좌우 추락 방지 투명 벽 추가
            CreateSideWalls(startPlatform.transform, 0, startPlatformDepth);

            float endZ = startCenterZ + (startPlatformDepth * 0.5f); // 정확히 1.0f
            activeChunks.Enqueue(new ChunkData { ChunkObject = startPlatform, EndZ = endZ });

            // 다음 도로 청크의 시작 좌표를 0cm 오차로 정확히 연결
            nextSpawnZ = endZ;
        }

        private void SpawnNextChunk()
        {
            GameObject chunkObj = new GameObject($"RoadChunk_{chunkIndex++}");
            chunkObj.transform.SetParent(transform);
            chunkObj.transform.position = new Vector3(0, 0, nextSpawnZ);

            float currentLocalZ = 0f;

            // 1. 차선들(Lanes) 생성
            for (int i = 0; i < lanesPerChunk; i++)
            {
                float laneCenterZ = currentLocalZ + (laneDepth * 0.5f);

                // 차도 바닥 (상단 표면 Y = 0.0f로 완벽 통일)
                GameObject roadCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roadCube.name = $"Lane_{i}_Road";
                roadCube.transform.SetParent(chunkObj.transform, false);
                roadCube.transform.localPosition = new Vector3(0, -floorThickness * 0.5f, laneCenterZ);
                roadCube.transform.localScale = new Vector3(roadWidth, floorThickness, laneDepth);
                roadCube.GetComponent<Renderer>().material = matAsphalt;
                ApplyFrictionlessMaterial(roadCube);

                // 횡단보도 줄무늬 (콜라이더 없음, Z-파이팅 방지를 위해 0.002f만 살짝 올림)
                CreateCrosswalkStripes(chunkObj.transform, laneCenterZ);

                // 차선별 차량 스포너
                bool isMovingRight = (i % 2 == 0);
                CreateLaneSpawner(chunkObj.transform, laneCenterZ, isMovingRight);

                currentLocalZ += laneDepth;
            }

            // 2. 중간 안전지대 (인도) - 도로와 똑같이 상단 표면 Y = 0.0f로 완전 평탄화
            float sidewalkCenterZ = currentLocalZ + (sidewalkDepth * 0.5f);
            GameObject sidewalkCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sidewalkCube.name = "Safety_Sidewalk";
            sidewalkCube.transform.SetParent(chunkObj.transform, false);
            sidewalkCube.transform.localPosition = new Vector3(0, -floorThickness * 0.5f, sidewalkCenterZ);
            sidewalkCube.transform.localScale = new Vector3(roadWidth, floorThickness, sidewalkDepth);
            sidewalkCube.GetComponent<Renderer>().material = matSidewalk;
            ApplyFrictionlessMaterial(sidewalkCube);

            currentLocalZ += sidewalkDepth;

            // 청크 좌우 추락 방지 투명 벽 추가
            CreateSideWalls(chunkObj.transform, currentLocalZ * 0.5f, currentLocalZ);

            float chunkStartZ = nextSpawnZ;
            float chunkEndZ = chunkStartZ + currentLocalZ;

            nextSpawnZ = chunkEndZ;
            activeChunks.Enqueue(new ChunkData { ChunkObject = chunkObj, EndZ = chunkEndZ });
        }

        private void CreateSideWalls(Transform parent, float centerZ, float length)
        {
            // 플레이어가 도로 좌우 밖으로 떨어지지 않도록 투명 벽 콜라이더 설치
            float halfWidth = roadWidth * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wall = new GameObject($"SideWall_{(side < 0 ? "Left" : "Right")}");
                wall.transform.SetParent(parent, false);
                wall.transform.localPosition = new Vector3(side * (halfWidth + 0.5f), 1.5f, centerZ);

                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 4f, length);
            }
        }

        private void CreateCrosswalkStripes(Transform parent, float centerZ)
        {
            float stripeWidth = 1.0f;
            float stripeSpacing = 2.0f;
            float stripeLength = laneDepth * 0.75f;
            int stripeCount = 6;
            float startX = -((stripeCount - 1) * stripeSpacing) * 0.5f;

            for (int s = 0; s < stripeCount; s++)
            {
                float stripeX = startX + (s * stripeSpacing);
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"Stripe_{s}";
                stripe.transform.SetParent(parent, false);
                // 물리적 턱이 되지 않도록 콜라이더를 즉시 제거하고 시각적으로만 바닥 바로 위에 표시
                stripe.transform.localPosition = new Vector3(stripeX, 0.002f, centerZ);
                stripe.transform.localScale = new Vector3(stripeWidth, 0.004f, stripeLength);
                stripe.GetComponent<Renderer>().material = matStripe;

                Collider col = stripe.GetComponent<Collider>();
                if (col != null)
                {
                    DestroyImmediate(col);
                }
            }
        }

        private void CreateLaneSpawner(Transform parent, float centerZ, bool isMovingRight)
        {
            GameObject spawnerObj = new GameObject($"CarSpawner_{(isMovingRight ? "Right" : "Left")}");
            spawnerObj.transform.SetParent(parent, false);

            float spawnerX = isMovingRight ? -(roadWidth * 0.5f + 2f) : (roadWidth * 0.5f + 2f);
            spawnerObj.transform.localPosition = new Vector3(spawnerX, 0.6f, centerZ);

            CarSpawner spawner = spawnerObj.AddComponent<CarSpawner>();
            Vector3 direction = isMovingRight ? Vector3.right : Vector3.left;

            float baseMin = Random.Range(8f, 11f);
            float baseMax = baseMin + Random.Range(5f, 8f);
            float minInterval = Random.Range(1.4f, 2.0f);
            float maxInterval = minInterval + Random.Range(1.2f, 2.0f);

            spawner.Configure(direction, baseMin, baseMax, minInterval, maxInterval);
        }

        private void ApplyFrictionlessMaterial(GameObject obj)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null && frictionlessMat != null)
            {
                col.material = frictionlessMat;
            }
        }

        private void OnDestroy()
        {
            if (matAsphalt != null) Destroy(matAsphalt);
            if (matStripe != null) Destroy(matStripe);
            if (matSidewalk != null) Destroy(matSidewalk);
            if (frictionlessMat != null) Destroy(frictionlessMat);
        }
    }
}
