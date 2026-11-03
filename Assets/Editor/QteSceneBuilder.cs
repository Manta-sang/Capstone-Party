using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>Creates the overview camera and wires the QTE race scene to the existing party prefabs.</summary>
public static class QteSceneBuilder
{
    [MenuItem("Party/Build QTE School Track Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Qte";
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.72f, .76f, .82f);
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

        var light = new GameObject("School Daylight", typeof(Light));
        light.transform.rotation = Quaternion.Euler(48, -32, 0);
        var sunlight = light.GetComponent<Light>();
        sunlight.type = LightType.Directional;
        sunlight.intensity = 1.1f;

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 90, -106);
        cameraObject.transform.LookAt(new Vector3(0, 0, 0));
        var camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = 58;
        camera.farClipPlane = 300;
        camera.backgroundColor = new Color(.52f, .72f, .88f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Player" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var prefabs = prefabPaths.Select(path =>
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            bool hasNetworkObject = root.GetComponent<NetworkObject>() != null;
            PrefabUtility.UnloadPrefabContents(root);
            return hasNetworkObject ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }).Where(p => p != null).Take(4).ToArray();
        if (prefabs.Length == 0) throw new System.InvalidOperationException("No network character prefabs were found in Assets/Prefabs/Player.");

        var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
            .Where(font => font != null).ToArray();
        var korean = fonts.FirstOrDefault(font => font.name.IndexOf("Korean", System.StringComparison.OrdinalIgnoreCase) >= 0)
            ?? fonts.FirstOrDefault(font => font.name.IndexOf("Noto", System.StringComparison.OrdinalIgnoreCase) >= 0)
            ?? fonts.FirstOrDefault();

        var spawnPoints = new Transform[4];
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var point = new GameObject("QTE Spawn " + (i + 1)).transform;
            float radius = 20.6f - i * 1.4f;
            float laneLap = 140f + 2f * Mathf.PI * radius;
            float innerLap = 140f + 2f * Mathf.PI * (20.6f - 3f * 1.4f);
            float stagger = laneLap - innerLap;
            point.position = new Vector3(-35f + stagger, .08f, -radius);
            point.rotation = Quaternion.LookRotation(Vector3.right);
            spawnPoints[i] = point;
        }

        var controllerObject = new GameObject("QTE School Track Controller");
        var controller = controllerObject.AddComponent<QteSceneController>();
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/QteGenerated"))
            AssetDatabase.CreateFolder("Assets/Scenes", "QteGenerated");
        var serialized = new SerializedObject(controller);
        var prefabArray = serialized.FindProperty("characterPrefabs");
        prefabArray.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++) prefabArray.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        var audiencePrefabs = FindIdleSpectatorPrefabs();
        var spectatorArray = serialized.FindProperty("spectatorPrefabs");
        spectatorArray.arraySize = audiencePrefabs.Length;
        for (int i = 0; i < audiencePrefabs.Length; i++) spectatorArray.GetArrayElementAtIndex(i).objectReferenceValue = audiencePrefabs[i];
        var spawnArray = serialized.FindProperty("spawnPoints");
        spawnArray.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++) spawnArray.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        serialized.FindProperty("koreanFont").objectReferenceValue = korean;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        controller.BuildSchoolTrackForEditor();
        EditorUtility.SetDirty(controllerObject);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Qte.unity");
        AssetDatabase.SaveAssets();
        Debug.Log($"QTE_SCHOOL_TRACK_BUILD_PASS: saved scene with four network character prefabs, {audiencePrefabs.Length} idle crowd prefabs, bleachers, four lane spawns, an overview camera, and a school-day directional light.");
    }

    private static GameObject[] FindIdleSpectatorPrefabs()
    {
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FREE/Pack_FREE_PartyCharacters/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string path in paths)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var animator = root.GetComponentInChildren<Animator>(true);
                bool hasNetworkObject = root.GetComponentInChildren<NetworkObject>(true) != null;
                if (animator != null && animator.runtimeAnimatorController != null && !hasNetworkObject)
                    found.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }
        if (found.Count == 0) Debug.LogWarning("No non-networked PartyCharacter prefabs with an Animator were found for the bleachers.");
        return found.Take(6).ToArray();
    }
}
