#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrosswalkRun.Editor
{
    public static class CrosswalkSceneSetup
    {
        [MenuItem("Tools/CrosswalkRun/Setup CrosswalkRun Scene", false, 1)]
        public static void SetupScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Setup CrosswalkRun Scene");

            // 1. 카메라 설정 (탑다운 쿼터뷰 각도)
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            mainCam.transform.position = new Vector3(0f, 15f, -9f);
            mainCam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            mainCam.fieldOfView = 60f;

            AutoScrollCamera scrollCam = mainCam.GetComponent<AutoScrollCamera>();
            if (scrollCam == null)
            {
                scrollCam = mainCam.gameObject.AddComponent<AutoScrollCamera>();
            }

            // 2. 조명(Directional Light) 설정
            Light dirLight = Object.FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.color = new Color(1f, 0.96f, 0.85f);
            dirLight.intensity = 1.0f;

            // 3. 매니저 시스템 오브젝트 생성
            GameObject systemObj = GameObject.Find("[Crosswalk_System]");
            if (systemObj == null)
            {
                systemObj = new GameObject("[Crosswalk_System]");
            }

            CrosswalkGameManager gameManager = systemObj.GetComponent<CrosswalkGameManager>();
            if (gameManager == null)
            {
                gameManager = systemObj.AddComponent<CrosswalkGameManager>();
            }

            InfiniteRoadManager roadManager = systemObj.GetComponent<InfiniteRoadManager>();
            if (roadManager == null)
            {
                roadManager = systemObj.AddComponent<InfiniteRoadManager>();
            }

            // 4. 테스트 큐브 플레이어 생성 (혼자 즉시 테스트 가능)
            GameObject playerObj = GameObject.Find("Test_Player");
            if (playerObj == null)
            {
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                playerObj.name = "Test_Player";
                playerObj.tag = "Player";
                playerObj.transform.position = new Vector3(0f, 0.6f, -3f);
                playerObj.transform.localScale = new Vector3(1f, 1.2f, 1f);

                Renderer rend = playerObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                    rend.material = new Material(standardShader) { color = new Color(0.2f, 0.8f, 0.3f) }; // 밝은 초록색
                }

                // 둥근 캡슐 콜라이더로 교체하여 바닥 걸림 방지
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

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("[CrosswalkRun] CrosswalkRun 씬 기본 세팅이 완료되었습니다! Play 버튼을 눌러 바로 테스트해보세요.");
        }
    }
}
#endif
