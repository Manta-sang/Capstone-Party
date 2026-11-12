#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Authored scene geometry only: no additional runtime Update, lights or colliders.
[InitializeOnLoad]
public static class QteSchoolQuality
{
    private const string Folder = "Assets/Materials/QteGenerated/";
    private const string Output = "Library/QteSchoolQuality/";
    static QteSchoolQuality() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Library/QteSchoolQuality.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "result.txt", "FAIL: " + ex); Debug.LogException(ex); }
    }

    [MenuItem("Party/Polish QTE School Quality")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Qte.unity");
        if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save QTE scene changes first.");
        scene = EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
        Directory.CreateDirectory(Output);
        Capture("before.png", new Vector3(45, 22, -12), new Vector3(0, 10, 31));
        var school = GameObject.Find("QTE School");
        if (school == null) throw new InvalidOperationException("School group is missing.");
        var previous = school.transform.Find("07 Facade craftsmanship");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var root = new GameObject("07 Facade craftsmanship").transform;
        root.SetParent(school.transform, false);
        var stone = Mat("Warm cut limestone", new Color(.78f, .76f, .66f), .18f);
        var dark = Mat("Recessed charcoal bronze", new Color(.13f, .19f, .20f), .45f, .35f);
        var glass = Mat("Soft reflected sky", new Color(.42f, .64f, .72f), .78f, .18f);
        var curtain = Mat("Classroom linen", new Color(.66f, .70f, .63f), .05f);
        var wood = Mat("Entrance warm timber", new Color(.38f, .23f, .13f), .22f);
        var soil = Mat("Planter soil", new Color(.16f, .13f, .10f), .05f);
        var leaf = Mat("Garden clipped foliage", new Color(.23f, .37f, .17f), .08f);
        var lamp = Mat("Warm opal lantern", new Color(.96f, .80f, .48f), .3f);
        lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(.40f, .24f, .08f));
        EditorUtility.SetDirty(lamp);

        // Shallow overlays sit in front of the existing glazing, preserving all authored frames.
        var windows = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == "School classroom window").ToArray();
        for (int i = 0; i < windows.Length; i++)
        {
            var renderer = windows[i].GetComponent<Renderer>();
            if (renderer == null) continue;
            var b = renderer.bounds; float z = b.min.z - .014f;
            // Narrow sky highlights and alternating lowered blinds make each classroom read differently.
            Box(root, "Glazing reflected horizon", new Vector3(b.center.x, b.center.y + b.size.y * .28f, z), new Vector3(b.size.x * .87f, .10f, .016f), glass);
            Box(root, "Recessed glazing header", new Vector3(b.center.x, b.max.y - .04f, z - .01f), new Vector3(b.size.x * .93f, .065f, .045f), dark);
            if (i % 4 != 0)
            {
                float h = b.size.y * (i % 3 == 0 ? .29f : .17f);
                Box(root, "Linen roller blind", new Vector3(b.center.x, b.max.y - h * .5f - .07f, z - .016f), new Vector3(b.size.x * .84f, h, .018f), curtain);
                Box(root, "Blind weighted hem", new Vector3(b.center.x, b.max.y - h - .07f, z - .025f), new Vector3(b.size.x * .85f, .025f, .025f), stone);
            }
        }
        foreach (float x in new[] { -30f, 0f, 30f })
        {
            bool main = x == 0; float width = main ? 34 : 23, front = main ? 26.6f : 27.4f;
            foreach (float y in main ? new[] { 6.3f, 12.6f, 18.2f } : new[] { 6.3f, 13.3f })
            {
                Box(root, "Floor stringcourse", new Vector3(x, y, front - .12f), new Vector3(width + .16f, .13f, .22f), stone);
                Box(root, "Stringcourse shadow reveal", new Vector3(x, y - .10f, front - .08f), new Vector3(width, .045f, .12f), dark);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float corner = x + side * (width * .5f - .18f);
                for (float y = 2; y < (main ? 19.5f : 15.5f); y += .72f)
                    Box(root, "Alternating corner quoin", new Vector3(corner, y, front - .10f), new Vector3(((int)(y / .72f) % 2 == 0) ? .7f : .46f, .32f, .20f), stone);
            }
        }
        // A warm, sheltered entrance is the focal point against the cool classroom glass.
        for (int i = 0; i < 23; i++)
            Box(root, "Entrance soffit timber slat", new Vector3(-5.5f + i * .5f, 5.63f, 25.1f), new Vector3(.12f, .12f, 2.35f), wood);
        foreach (float x in new[] { -4.7f, 4.7f })
        {
            Box(root, "Lantern bronze backplate", new Vector3(x, 3.4f, 24.45f), new Vector3(.38f, .86f, .18f), dark);
            Box(root, "Lantern opal face", new Vector3(x, 3.4f, 24.34f), new Vector3(.23f, .59f, .07f), lamp);
        }
        foreach (float x in new[] { -10.5f, 10.5f })
        {
            Box(root, "Raised entrance planter", new Vector3(x, .5f, 24.5f), new Vector3(4.8f, .85f, 1.5f), stone);
            Box(root, "Inset planter soil", new Vector3(x, .94f, 24.5f), new Vector3(4.5f, .07f, 1.24f), soil);
            for (int i = 0; i < 7; i++)
            {
                var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bush.name = "Trimmed entrance planting"; bush.transform.SetParent(root, false);
                bush.transform.position = new Vector3(x - 1.8f + i * .6f, 1.22f, 24.5f);
                bush.transform.localScale = new Vector3(.85f, .62f + (i % 2) * .10f, .95f);
                bush.GetComponent<Renderer>().sharedMaterial = leaf;
                Object.DestroyImmediate(bush.GetComponent<Collider>());
            }
        }
        Combine(root);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        QteHierarchyOptimization.Optimize();
        Capture("school.png", new Vector3(45, 22, -12), new Vector3(0, 10, 31));
        Capture("entrance.png", new Vector3(18, 8, 3), new Vector3(0, 4.8f, 26));
        Capture("overview.png", new Vector3(0, 52, -36), new Vector3(0, 2, 5), 64);
        File.WriteAllText(Output + "result.txt", $"PASS: {windows.Length} classroom windows refined; {root.childCount} combined material meshes; hierarchy/spawn/material validation passed. No runtime lights, scripts or colliders added.");
    }

    private static Material Mat(string name, Color color, float smooth, float metal = 0)
    {
        string path = Folder + "Quality_" + name.Replace(' ', '_') + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal); m.enableInstancing = true;
        EditorUtility.SetDirty(m); return m;
    }
    private static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        g.transform.SetParent(parent, false); g.transform.position = at; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    private static void Combine(Transform root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        int index = 0;
        foreach (var set in renderers.GroupBy(r => r.sharedMaterial))
        {
            var mesh = new Mesh { name = "School craft " + set.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(set.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = root.worldToLocalMatrix * r.localToWorldMatrix }).ToArray(), true, true);
            string path = Folder + "Quality_school_batch_" + index++ + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            var go = new GameObject(set.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = saved; go.GetComponent<MeshRenderer>().sharedMaterial = set.Key;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
        foreach (var r in renderers) Object.DestroyImmediate(r.gameObject);
    }
    private static void Capture(string name, Vector3 at, Vector3 target, float fov = 48)
    {
        var go = new GameObject("School quality preview", typeof(Camera));
        var cam = go.GetComponent<Camera>(); cam.CopyFrom(Camera.main);
        cam.transform.position = at; cam.transform.LookAt(target); cam.fieldOfView = fov;
        var rt = RenderTexture.GetTemporary(1600, 900, 24); var previous = RenderTexture.active;
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply(); File.WriteAllBytes(Output + name, texture.EncodeToPNG()); }
        finally { cam.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
    }
}
#endif
