#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Groups QTE scenery while preserving object references and world poses.</summary>
[InitializeOnLoad]
public static class QteHierarchyOptimization
{
    private const string ScenePath = "Assets/Scenes/Qte.unity";
    private const string Request = "Library/QteHierarchyOptimization.request";
    private const string Result = "Library/QteHierarchyOptimization.result.txt";
    private static readonly string[] Groups = { "00 Systems", "01 Track", "02 School", "03 Audience", "04 Environment", "05 UI" };
    static QteHierarchyOptimization() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Optimize(); }
        catch (Exception ex) { File.WriteAllText(Result, "FAIL: " + ex); Debug.LogException(ex); }
    }

    [MenuItem("Party/Optimize QTE Hierarchy")]
    public static void Optimize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        else if (scene.isDirty) throw new InvalidOperationException("Save QTE scene changes before optimizing.");
        try
        {
            var roots = scene.GetRootGameObjects();
            int before = roots.Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            var poses = roots.ToDictionary(r => r.transform, r => (r.transform.position, r.transform.rotation, r.transform.lossyScale));
            var containers = Groups.Select(name =>
            {
                var group = roots.FirstOrDefault(r => r.name == name);
                if (group == null) { group = new GameObject(name); SceneManager.MoveGameObjectToScene(group, scene); }
                return group.transform;
            }).ToArray();
            foreach (var root in roots)
            {
                if (Groups.Contains(root.name) || root.GetComponentsInChildren<NetworkObject>(true).Length > 0) continue;
                root.transform.SetParent(containers[Category(root)], true);
            }
            int batched = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                var mesh = go.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null || go.transform.childCount != 0 ||
                    go.GetComponentInParent<Animator>() != null || go.GetComponentInParent<NetworkObject>() != null ||
                    go.GetComponentsInParent<MonoBehaviour>(true).Length > 0 || go.GetComponentInParent<Rigidbody>() != null ||
                    (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                batched++;
            }
            foreach (var pose in poses)
                if (Vector3.Distance(pose.Key.position, pose.Value.position) > .001f ||
                    Quaternion.Angle(pose.Key.rotation, pose.Value.rotation) > .01f ||
                    Vector3.Distance(pose.Key.lossyScale, pose.Value.lossyScale) > .001f)
                    throw new InvalidOperationException("World pose changed: " + pose.Key.name);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<QteSceneController>(true)).Single();
            var spawns = new SerializedObject(controller).FindProperty("spawnPoints");
            if (spawns.arraySize != 4 || Enumerable.Range(0, 4).Any(i => spawns.GetArrayElementAtIndex(i).objectReferenceValue == null))
                throw new InvalidOperationException("QTE spawn references are incomplete.");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new InvalidOperationException("Missing script: " + t.name);
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials.Any(m => m == null)) throw new InvalidOperationException("Missing material: " + t.name);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            int after = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            string report = $"PASS: roots {roots.Length} -> {scene.rootCount}; objects {before} -> {after}; newly batching-static renderers {batched}; four spawn references and world poses preserved; no missing scripts/materials.";
            File.WriteAllText(Result, report);
            Debug.Log(report);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static int Category(GameObject root)
    {
        string n = root.name;
        if (root.GetComponent<Canvas>() != null || n.Contains("Canvas") || n == "EventSystem") return 5;
        if (n.StartsWith("Audience Row ") || n.StartsWith("Bleacher") || n.Contains("Audience")) return 3;
        if (n.StartsWith("School ") && n != "School Daylight" && n != "School ground") return 2;
        if (n == "School ground" || n.StartsWith("Track") || n.StartsWith("Lane") || n.StartsWith("Start") || n.StartsWith("Finish") || n.StartsWith("Hurdle")) return 1;
        if (root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null || root.GetComponent<MonoBehaviour>() != null || n.StartsWith("QTE Spawn")) return 0;
        return 4;
    }
}
#endif
