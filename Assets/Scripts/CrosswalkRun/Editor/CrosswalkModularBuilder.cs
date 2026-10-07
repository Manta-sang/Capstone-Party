#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrosswalkRun.Editor
{
    public static class CrosswalkModularBuilder
    {
        private const string MatDir = "Assets/Materials/CrosswalkRun";
        private const string PrefabDir = "Assets/Prefabs/CrosswalkRun";

       /* [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // 컴파일 후 1회 자동 실행
            if (!SessionState.GetBool("CrosswalkVisibleSceneBuilt_v4", false))
            {
                SessionState.SetBool("CrosswalkVisibleSceneBuilt_v4", true);
                EditorApplication.delayCall += () =>
                {
                    BuildAll();
                };
            }
        }*/

        [MenuItem("Tools/CrosswalkRun/Build Visible Scene and Prefabs", false, 1)]
        [MenuItem("Tools/CrosswalkRun/Build Prefab Pool Scene and Assets", false, 2)]
        public static void BuildAll()
        {
            if (SceneManager.GetActiveScene().name != "CrosswalkRun")
            {
                if (File.Exists("Assets/Scenes/CrosswalkRun.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/CrosswalkRun.unity");
                }
            }

            EnsureDirectories();
            CreateMaterials(out Material matAsphalt, out Material matSidewalk, out Material matStripe, out Material matCar, out PhysicMaterial physMat);

            CreatePrefabs(matAsphalt, matSidewalk, matStripe, matCar, physMat,
                out GameObject carPrefab,
                out GameObject startPlatformPrefab,
                out GameObject chunkStandard,
                out GameObject chunkHighway,
                out GameObject chunkRestStop);

            SetupVisibleModularScene(startPlatformPrefab, chunkStandard, chunkHighway, chunkRestStop);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CrosswalkRun] 에디터 씬에 실제 도로 모듈들이 눈에 보이게 배치되었으며, 프리팹 풀 빌드가 완료되었습니다!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "CrosswalkRun");

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "CrosswalkRun");
        }

        private static void CreateMaterials(out Material matAsphalt, out Material matSidewalk, out Material matStripe, out Material matCar, out PhysicMaterial physMat)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");

            string pathAsphalt = $"{MatDir}/Mat_Asphalt.mat";
            matAsphalt = AssetDatabase.LoadAssetAtPath<Material>(pathAsphalt);
            if (matAsphalt == null)
            {
                matAsphalt = new Material(shader) { color = new Color(0.2f, 0.2f, 0.22f) };
                AssetDatabase.CreateAsset(matAsphalt, pathAsphalt);
            }

            string pathSidewalk = $"{MatDir}/Mat_Sidewalk.mat";
            matSidewalk = AssetDatabase.LoadAssetAtPath<Material>(pathSidewalk);
            if (matSidewalk == null)
            {
                matSidewalk = new Material(shader) { color = new Color(0.6f, 0.6f, 0.63f) };
                AssetDatabase.CreateAsset(matSidewalk, pathSidewalk);
            }

            string pathStripe = $"{MatDir}/Mat_CrosswalkStripe.mat";
            matStripe = AssetDatabase.LoadAssetAtPath<Material>(pathStripe);
            if (matStripe == null)
            {
                matStripe = new Material(shader) { color = new Color(0.96f, 0.96f, 0.96f) };
                AssetDatabase.CreateAsset(matStripe, pathStripe);
            }

            string pathCar = $"{MatDir}/Mat_Car.mat";
            matCar = AssetDatabase.LoadAssetAtPath<Material>(pathCar);
            if (matCar == null)
            {
                matCar = new Material(shader) { color = new Color(0.88f, 0.25f, 0.25f) };
                AssetDatabase.CreateAsset(matCar, pathCar);
            }

            string pathPhys = $"{MatDir}/PhysMat_Frictionless.physicMaterial";
            physMat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(pathPhys);
            if (physMat == null)
            {
                physMat = new PhysicMaterial("PhysMat_Frictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(physMat, pathPhys);
            }
        }

        private static void CreatePrefabs(Material matAsphalt, Material matSidewalk, Material matStripe, Material matCar, PhysicMaterial physMat,
            out GameObject carPrefab,
            out GameObject startPlatformPrefab,
            out GameObject chunkStandard,
            out GameObject chunkHighway,
            out GameObject chunkRestStop)
        {
            // 1. Car_Obstacle.prefab
            string carPath = $"{PrefabDir}/Car_Obstacle.prefab";
            GameObject tempCar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempCar.name = "Car_Obstacle";
            tempCar.transform.localScale = new Vector3(2.2f, 1.2f, 3.8f);
            tempCar.GetComponent<Renderer>().sharedMaterial = matCar;

            Rigidbody carRb = tempCar.AddComponent<Rigidbody>();
            carRb.isKinematic = true;
            carRb.useGravity = false;
            tempCar.AddComponent<CarObstacle>();

            carPrefab = PrefabUtility.SaveAsPrefabAsset(tempCar, carPath);
            Object.DestroyImmediate(tempCar);

            // 2. Sidewalk_Start_Platform.prefab (시작 발판: 길이 11m, local 0~11)
            string startPlatformPath = $"{PrefabDir}/Sidewalk_Start_Platform.prefab";
            GameObject tempStart = new GameObject("Sidewalk_Start_Platform");
            RoadChunk startChunk = tempStart.AddComponent<RoadChunk>();
            startChunk.SetCustomLength(11f);

            GameObject startSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            startSurface.name = "Sidewalk_Surface";
            startSurface.transform.SetParent(tempStart.transform, false);
            startSurface.transform.localPosition = new Vector3(0, -0.25f, 5.5f);
            startSurface.transform.localScale = new Vector3(36f, 0.5f, 11f);
            startSurface.GetComponent<Renderer>().sharedMaterial = matSidewalk;
            startSurface.GetComponent<Collider>().sharedMaterial = physMat;

            startPlatformPrefab = PrefabUtility.SaveAsPrefabAsset(tempStart, startPlatformPath);
            Object.DestroyImmediate(tempStart);

            // 3. Chunk_Standard_3Lanes.prefab (3차선 + 안전 인도 = 17m)
            string stdPath = $"{PrefabDir}/Chunk_Standard_3Lanes.prefab";
            GameObject tempStd = new GameObject("Chunk_Standard_3Lanes");
            RoadChunk stdChunk = tempStd.AddComponent<RoadChunk>();
            stdChunk.SetCustomLength(17f);

            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempStd.transform, "Lane_01", new Vector3(0, 0, 2.25f), Vector3.right, 8f, 14f, 1.5f, 3.0f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempStd.transform, "Lane_02", new Vector3(0, 0, 6.75f), Vector3.left, 9f, 15f, 1.4f, 2.8f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempStd.transform, "Lane_03", new Vector3(0, 0, 11.25f), Vector3.right, 10f, 16f, 1.2f, 2.5f);
            CreateSidewalkChild(matSidewalk, physMat, tempStd.transform, "Sidewalk_Safety", new Vector3(0, 0, 15.25f), 3.5f);

            chunkStandard = PrefabUtility.SaveAsPrefabAsset(tempStd, stdPath);
            Object.DestroyImmediate(tempStd);

            // 4. Chunk_Highway_4Lanes.prefab (4차선 고속도로 = 18m)
            string hwyPath = $"{PrefabDir}/Chunk_Highway_4Lanes.prefab";
            GameObject tempHwy = new GameObject("Chunk_Highway_4Lanes");
            RoadChunk hwyChunk = tempHwy.AddComponent<RoadChunk>();
            hwyChunk.SetCustomLength(18f);

            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempHwy.transform, "Lane_01_FastRight", new Vector3(0, 0, 2.25f), Vector3.right, 14f, 20f, 1.0f, 2.2f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempHwy.transform, "Lane_02_FastRight", new Vector3(0, 0, 6.75f), Vector3.right, 15f, 22f, 0.9f, 2.0f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempHwy.transform, "Lane_03_FastLeft", new Vector3(0, 0, 11.25f), Vector3.left, 15f, 21f, 1.0f, 2.2f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempHwy.transform, "Lane_04_FastLeft", new Vector3(0, 0, 15.75f), Vector3.left, 16f, 24f, 0.8f, 1.8f);

            chunkHighway = PrefabUtility.SaveAsPrefabAsset(tempHwy, hwyPath);
            Object.DestroyImmediate(tempHwy);

            // 5. Chunk_RestStop_1Lane.prefab (넓은 인도 6m + 1차선 4.5m + 인도 4.5m = 15m)
            string restPath = $"{PrefabDir}/Chunk_RestStop_1Lane.prefab";
            GameObject tempRest = new GameObject("Chunk_RestStop_1Lane");
            RoadChunk restChunk = tempRest.AddComponent<RoadChunk>();
            restChunk.SetCustomLength(15f);

            CreateSidewalkChild(matSidewalk, physMat, tempRest.transform, "Sidewalk_RestArea_A", new Vector3(0, 0, 3.0f), 6.0f);
            CreateLaneChild(carPrefab, matAsphalt, matStripe, physMat, tempRest.transform, "Lane_Slow", new Vector3(0, 0, 8.25f), Vector3.right, 6f, 10f, 2.5f, 4.5f);
            CreateSidewalkChild(matSidewalk, physMat, tempRest.transform, "Sidewalk_RestArea_B", new Vector3(0, 0, 12.75f), 4.5f);

            chunkRestStop = PrefabUtility.SaveAsPrefabAsset(tempRest, restPath);
            Object.DestroyImmediate(tempRest);
        }

        private static void CreateLaneChild(GameObject carPrefab, Material matAsphalt, Material matStripe, PhysicMaterial physMat,
            Transform parent, string name, Vector3 localCenter, Vector3 moveDir, float minSpd, float maxSpd, float minInt, float maxInt)
        {
            GameObject lane = new GameObject(name);
            lane.transform.SetParent(parent, false);
            lane.transform.localPosition = localCenter;

            // 차도 바닥 (Y 표면 = 0.0)
            GameObject roadSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadSurface.name = "Road_Surface";
            roadSurface.transform.SetParent(lane.transform, false);
            roadSurface.transform.localPosition = new Vector3(0, -0.25f, 0);
            roadSurface.transform.localScale = new Vector3(36f, 0.5f, 4.5f);
            roadSurface.GetComponent<Renderer>().sharedMaterial = matAsphalt;
            roadSurface.GetComponent<Collider>().sharedMaterial = physMat;

            // 횡단보도 줄무늬 6개
            float stripeSpacing = 2.0f;
            float startX = -((6 - 1) * stripeSpacing) * 0.5f;
            for (int s = 0; s < 6; s++)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"Stripe_{s}";
                stripe.transform.SetParent(lane.transform, false);
                stripe.transform.localPosition = new Vector3(startX + (s * stripeSpacing), 0.002f, 0);
                stripe.transform.localScale = new Vector3(1.0f, 0.004f, 3.3f);
                stripe.GetComponent<Renderer>().sharedMaterial = matStripe;
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }

            // 스포너 자식 오브젝트
            GameObject spawnerObj = new GameObject("CarSpawner");
            spawnerObj.transform.SetParent(lane.transform, false);
            bool moveRight = (moveDir.x > 0);
            spawnerObj.transform.localPosition = new Vector3(moveRight ? -20f : 20f, 0.6f, 0);

            CarSpawner spawner = spawnerObj.AddComponent<CarSpawner>();
            spawner.Configure(moveDir, minSpd, maxSpd, minInt, maxInt);

            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("carPrefab").objectReferenceValue = carPrefab;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSidewalkChild(Material matSidewalk, PhysicMaterial physMat,
            Transform parent, string name, Vector3 localCenter, float depth)
        {
            GameObject sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sidewalk.name = name;
            sidewalk.transform.SetParent(parent, false);
            sidewalk.transform.localPosition = localCenter + new Vector3(0, -0.25f, 0);
            sidewalk.transform.localScale = new Vector3(36f, 0.5f, depth);
            sidewalk.GetComponent<Renderer>().sharedMaterial = matSidewalk;
            sidewalk.GetComponent<Collider>().sharedMaterial = physMat;
        }

        private static void SetupVisibleModularScene(GameObject startPlatformPrefab, GameObject chunkStd, GameObject chunkHwy, GameObject chunkRest)
        {
            Scene scene = SceneManager.GetActiveScene();

            // 이전 버전의 시스템 정리
            GameObject oldSystem = GameObject.Find("[Crosswalk_System]");
            if (oldSystem != null) Object.DestroyImmediate(oldSystem);

            GameObject oldMap = GameObject.Find("Map_Environment");
            if (oldMap != null) Object.DestroyImmediate(oldMap);

            // 1. 카메라 설정
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 15f, -9f);
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            cam.fieldOfView = 60f;
            if (cam.GetComponent<AutoScrollCamera>() == null)
            {
                cam.gameObject.AddComponent<AutoScrollCamera>();
            }

            // 2. 조명
            Light dirLight = Object.FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lObj = new GameObject("Directional Light");
                dirLight = lObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.color = new Color(1f, 0.96f, 0.85f);

            // 3. 게임 매니저
            GameObject managerObj = GameObject.Find("[Game_Manager]");
            if (managerObj == null)
            {
                managerObj = new GameObject("[Game_Manager]");
            }
            if (managerObj.GetComponent<CrosswalkGameManager>() == null)
            {
                managerObj.AddComponent<CrosswalkGameManager>();
            }

            // 4. 테스트 플레이어 (출발 인도 위 Z = -3.0m에 배치)
            GameObject playerObj = GameObject.Find("Test_Player");
            if (playerObj == null)
            {
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                playerObj.name = "Test_Player";
                playerObj.tag = "Player";
                playerObj.transform.position = new Vector3(0f, 0.6f, -3f);
                playerObj.transform.localScale = new Vector3(1f, 1.2f, 1f);

                Renderer r = playerObj.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader sh = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                    r.material = new Material(sh) { color = new Color(0.2f, 0.8f, 0.3f) };
                }

                BoxCollider box = playerObj.GetComponent<BoxCollider>();
                if (box != null) Object.DestroyImmediate(box);

                CapsuleCollider capsule = playerObj.AddComponent<CapsuleCollider>();
                capsule.radius = 0.45f;
                capsule.height = 1.2f;

                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                if (rb == null) rb = playerObj.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                playerObj.AddComponent<CrosswalkTestPlayer>();
            }

            // 5. Map_RoadManager 구성
            GameObject roadManagerObj = GameObject.Find("Map_RoadManager");
            if (roadManagerObj == null)
            {
                roadManagerObj = new GameObject("Map_RoadManager");
            }

            // 기존 자식 오브젝트 정리 (중복 생성 방지)
            while (roadManagerObj.transform.childCount > 0)
            {
                Object.DestroyImmediate(roadManagerObj.transform.GetChild(0).gameObject);
            }

            // 6. [핵심] 에디터 씬에 실제 도로 모듈들을 눈에 보이게 펼쳐서 배치!
            // (1) 출발 인도 (Z = -10m ~ +1m)
            GameObject instStart = (GameObject)PrefabUtility.InstantiatePrefab(startPlatformPrefab, roadManagerObj.transform);
            instStart.name = "Sidewalk_Start_Platform";
            instStart.transform.position = new Vector3(0, 0, -10f);

            // (2) 표준 3차선 청크 (Z = 1m ~ 18m)
            GameObject instStd = (GameObject)PrefabUtility.InstantiatePrefab(chunkStd, roadManagerObj.transform);
            instStd.name = "Chunk_Standard_3Lanes";
            instStd.transform.position = new Vector3(0, 0, 1.0f);

            // (3) 고속도로 4차선 청크 (Z = 18m ~ 36m)
            GameObject instHwy = (GameObject)PrefabUtility.InstantiatePrefab(chunkHwy, roadManagerObj.transform);
            instHwy.name = "Chunk_Highway_4Lanes";
            instHwy.transform.position = new Vector3(0, 0, 18.0f);

            // (4) 쉼터 1차선 청크 (Z = 36m ~ 51m)
            GameObject instRest = (GameObject)PrefabUtility.InstantiatePrefab(chunkRest, roadManagerObj.transform);
            instRest.name = "Chunk_RestStop_1Lane";
            instRest.transform.position = new Vector3(0, 0, 36.0f);

            // 7. PrefabRoadManager 컴포넌트 설정 및 바인딩
            PrefabRoadManager roadManager = roadManagerObj.GetComponent<PrefabRoadManager>();
            if (roadManager == null)
            {
                roadManager = roadManagerObj.AddComponent<PrefabRoadManager>();
            }

            SerializedObject roadManagerSo = new SerializedObject(roadManager);
            roadManagerSo.FindProperty("targetCamera").objectReferenceValue = cam.transform;
            roadManagerSo.FindProperty("startPlatformPrefab").objectReferenceValue = startPlatformPrefab;
            roadManagerSo.FindProperty("chunksParent").objectReferenceValue = roadManagerObj.transform;
            roadManagerSo.FindProperty("viewDistanceAhead").floatValue = 90f;
            roadManagerSo.FindProperty("despawnDistanceBehind").floatValue = 50f;

            GameObject[] poolPrefabs = new GameObject[] { chunkStd, chunkHwy, chunkRest };
            SerializedProperty prefabsProp = roadManagerSo.FindProperty("roadChunkPrefabs");
            prefabsProp.arraySize = poolPrefabs.Length;
            for (int i = 0; i < poolPrefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = poolPrefabs[i];
            }
            roadManagerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
