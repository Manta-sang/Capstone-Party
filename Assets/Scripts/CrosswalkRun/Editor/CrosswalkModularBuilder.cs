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

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // 컴파일 후 1회 자동 실행 (이미 빌드된 세션이면 건너뜀)
            if (!SessionState.GetBool("CrosswalkModularBuilt_v1", false))
            {
                SessionState.SetBool("CrosswalkModularBuilt_v1", true);
                EditorApplication.delayCall += () =>
                {
                    // 현재 씬이 CrosswalkRun인 경우 자동 빌드
                    if (SceneManager.GetActiveScene().name == "CrosswalkRun")
                    {
                        BuildAll();
                    }
                };
            }
        }

        [MenuItem("Tools/CrosswalkRun/Build Modular Scene and Prefabs", false, 2)]
        public static void BuildAll()
        {
            EnsureDirectories();
            CreateMaterials(out Material matAsphalt, out Material matSidewalk, out Material matStripe, out Material matCar, out PhysicMaterial physMat);
            CreatePrefabs(matAsphalt, matSidewalk, matStripe, matCar, physMat, out GameObject carPrefab, out GameObject sidewalkPrefab, out GameObject lanePrefab);
            SetupModularScene(carPrefab, sidewalkPrefab, lanePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CrosswalkRun] 프리팹 에셋 생성 및 모듈형 씬(CrosswalkRun) 빌드가 완벽히 완료되었습니다!");
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
            out GameObject carPrefab, out GameObject sidewalkPrefab, out GameObject lanePrefab)
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

            // 2. Sidewalk_Module.prefab
            string sidewalkPath = $"{PrefabDir}/Sidewalk_Module.prefab";
            GameObject tempSidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempSidewalk.name = "Sidewalk_Module";
            tempSidewalk.transform.localScale = new Vector3(36f, 0.5f, 3.5f);
            tempSidewalk.GetComponent<Renderer>().sharedMaterial = matSidewalk;
            tempSidewalk.GetComponent<Collider>().sharedMaterial = physMat;

            sidewalkPrefab = PrefabUtility.SaveAsPrefabAsset(tempSidewalk, sidewalkPath);
            Object.DestroyImmediate(tempSidewalk);

            // 3. RoadLane_Module.prefab
            string lanePath = $"{PrefabDir}/RoadLane_Module.prefab";
            GameObject tempLane = new GameObject("RoadLane_Module");

            // 차도 바닥
            GameObject roadSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadSurface.name = "Road_Surface";
            roadSurface.transform.SetParent(tempLane.transform, false);
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
                stripe.transform.SetParent(tempLane.transform, false);
                stripe.transform.localPosition = new Vector3(startX + (s * stripeSpacing), 0.002f, 0);
                stripe.transform.localScale = new Vector3(1.0f, 0.004f, 3.3f);
                stripe.GetComponent<Renderer>().sharedMaterial = matStripe;
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }

            // 스포너 자식 오브젝트 (기본 우측 진행)
            GameObject spawnerObj = new GameObject("CarSpawner");
            spawnerObj.transform.SetParent(tempLane.transform, false);
            spawnerObj.transform.localPosition = new Vector3(-20f, 0.6f, 0);
            CarSpawner spawner = spawnerObj.AddComponent<CarSpawner>();
            spawner.Configure(Vector3.right, 9f, 15f, 1.5f, 3.0f);

            // Spawner에 carPrefab 연결 (SerializedObject 활용)
            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("carPrefab").objectReferenceValue = carPrefab;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            lanePrefab = PrefabUtility.SaveAsPrefabAsset(tempLane, lanePath);
            Object.DestroyImmediate(tempLane);
        }

        private static void SetupModularScene(GameObject carPrefab, GameObject sidewalkPrefab, GameObject lanePrefab)
        {
            Scene scene = SceneManager.GetActiveScene();

            // 기존 자동 런타임 매니저가 씬에 있다면 교체
            GameObject oldSystem = GameObject.Find("[Crosswalk_System]");
            if (oldSystem != null) Object.DestroyImmediate(oldSystem);

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

            // 4. 플레이어
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

            // 5. 하이어라키에 실제 눈에 보이는 모듈들 배치 (Map_Environment)
            GameObject mapRoot = GameObject.Find("Map_Environment");
            if (mapRoot != null) Object.DestroyImmediate(mapRoot);

            mapRoot = new GameObject("Map_Environment");
            mapRoot.AddComponent<ModularRoadLooper>();

            // (1) 시작 발판 (인도)
            GameObject startPlatform = (GameObject)PrefabUtility.InstantiatePrefab(sidewalkPrefab, mapRoot.transform);
            startPlatform.name = "Sidewalk_Start";
            startPlatform.transform.position = new Vector3(0, 0, -4.5f);
            startPlatform.transform.localScale = new Vector3(36f, 0.5f, 11f);

            // (2) 차선 및 인도 모듈들을 일렬로 배치 (총 4개 구역, 12개 차선 + 4개 인도)
            float currentZ = 1.0f; // 시작 발판 끝 좌표 (Z = -4.5 + 5.5 = 1.0f)
            int laneGlobalIndex = 1;

            for (int section = 1; section <= 4; section++)
            {
                GameObject sectionGroup = new GameObject($"Section_{section:00}");
                sectionGroup.transform.SetParent(mapRoot.transform, false);
                sectionGroup.transform.position = new Vector3(0, 0, currentZ);

                float sectionLocalZ = 0f;

                // 3개 차선 배치
                for (int l = 0; l < 3; l++)
                {
                    float laneCenterZ = sectionLocalZ + (4.5f * 0.5f);
                    GameObject laneObj = (GameObject)PrefabUtility.InstantiatePrefab(lanePrefab, sectionGroup.transform);
                    laneObj.name = $"Lane_{laneGlobalIndex:00}";
                    laneObj.transform.localPosition = new Vector3(0, 0, laneCenterZ);

                    // 좌/우 방향 교대 설정
                    bool moveRight = (laneGlobalIndex % 2 == 1);
                    CarSpawner spawnerComp = laneObj.GetComponentInChildren<CarSpawner>();
                    if (spawnerComp != null)
                    {
                        Transform spawnerTr = spawnerComp.transform;
                        spawnerTr.localPosition = new Vector3(moveRight ? -20f : 20f, 0.6f, 0);

                        float minSpd = 8f + (section * 1.5f);
                        float maxSpd = minSpd + 6f;
                        float minInt = Mathf.Max(1.0f, 2.0f - (section * 0.2f));
                        float maxInt = minInt + 1.2f;

                        spawnerComp.Configure(moveRight ? Vector3.right : Vector3.left, minSpd, maxSpd, minInt, maxInt);
                    }

                    sectionLocalZ += 4.5f;
                    laneGlobalIndex++;
                }

                // 1개 중간 안전지대(인도) 배치
                float sidewalkCenterZ = sectionLocalZ + (3.5f * 0.5f);
                GameObject sidewalkObj = (GameObject)PrefabUtility.InstantiatePrefab(sidewalkPrefab, sectionGroup.transform);
                sidewalkObj.name = $"Sidewalk_Safety_{section:00}";
                sidewalkObj.transform.localPosition = new Vector3(0, 0, sidewalkCenterZ);
                sidewalkObj.transform.localScale = new Vector3(36f, 0.5f, 3.5f);

                sectionLocalZ += 3.5f;

                currentZ += sectionLocalZ;
            }

            // Looper 컴포넌트 세그먼트 등록 갱신
            mapRoot.GetComponent<ModularRoadLooper>().CollectSegments();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
