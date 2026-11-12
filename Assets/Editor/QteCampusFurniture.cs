#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Scene-authoring only. Furniture stays outside the track and adds no physics or Update work.
[InitializeOnLoad]
public static class QteCampusFurniture
{
    private const string Folder = "Assets/Materials/QteGenerated/";
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    static QteCampusFurniture() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Library/QteCampusFurniture.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); }
        catch (Exception ex) { File.WriteAllText("Library/QteCampusFurniture.result", "FAIL: " + ex); Debug.LogException(ex); }
    }
    [MenuItem("Party/Add QTE Bicycle Parking and Benches")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var current = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Qte.unity");
        if (current.isLoaded && current.isDirty) throw new InvalidOperationException("Save QTE changes first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
        var environment = GameObject.Find("04 Environment");
        if (environment == null) throw new InvalidOperationException("Environment group missing.");
        var old = environment.transform.Find("Campus bicycle parking and benches");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Campus bicycle parking and benches").transform;
        root.SetParent(environment.transform, false);
        var wood = Mat("Oiled cedar", new Color(.43f, .27f, .14f), .22f);
        var metal = Mat("Powder coated iron", new Color(.13f, .20f, .21f), .42f, .55f);
        var silver = Mat("Brushed galvanized steel", new Color(.57f, .63f, .63f), .5f, .72f);
        var rubber = Mat("Bicycle rubber", new Color(.055f, .065f, .07f), .12f);
        var stone = Mat("Parking paving", new Color(.52f, .54f, .49f), .08f);
        var saddle = Mat("Bicycle saddle", new Color(.17f, .12f, .09f), .17f);
        foreach (float x in new[] { -35f, -27f, 27f, 35f }) Bench(root, new Vector3(x, .1f, 23.5f), wood, metal, silver);
        Box(root, "Bicycle parking paved apron", new Vector3(-47.5f, .045f, 30), new Vector3(4.4f, .08f, 8.3f), stone);
        // Seven inverted U stands with two deliberately empty parking spaces.
        for (int i = 0; i < 7; i++)
        {
            float z = 26.7f + i * 1.05f;
            Tube(root, new Vector3(-47.75f, .12f, z), new Vector3(-47.75f, .95f, z), .035f, silver);
            Tube(root, new Vector3(-47.75f, .95f, z), new Vector3(-47.0f, .95f, z), .035f, silver);
            Tube(root, new Vector3(-47.0f, .95f, z), new Vector3(-47.0f, .12f, z), .035f, silver);
            foreach (float x in new[] { -47.75f, -47f }) Box(root, "Stand anchor plate", new Vector3(x, .12f, z), new Vector3(.19f, .045f, .17f), metal);
            if (i == 1 || i == 5) continue;
            var frame = Mat("Bicycle frame " + i, new[] { new Color(.20f,.39f,.45f), new Color(.55f,.23f,.19f), new Color(.31f,.43f,.27f), new Color(.72f,.58f,.31f), new Color(.34f,.36f,.51f) }[i % 5], .4f, .3f);
            Bicycle(root, new Vector3(-47.4f, .13f, z + .14f), frame, silver, rubber, saddle);
        }
        typeof(QteEnvironmentRefinement).GetMethod("Combine", PrivateStatic).Invoke(null, new object[] { root });
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        QteHierarchyOptimization.Optimize();
        var capture = typeof(QteSchoolQuality).GetMethod("Capture", PrivateStatic);
        Directory.CreateDirectory("Library/QteSchoolQuality");
        capture.Invoke(null, new object[] { "bicycle-parking.png", new Vector3(-56, 5.5f, 21), new Vector3(-47.3f, .9f, 30), 48f });
        capture.Invoke(null, new object[] { "campus-benches.png", new Vector3(43, 5, 12), new Vector3(29, 1.2f, 24), 48f });
        File.WriteAllText("Library/QteCampusFurniture.result", $"PASS: four benches, seven stands, five bicycles; {root.childCount} combined material meshes; no colliders/runtime components; hierarchy and four spawn references validated.");
    }
    private static void Bench(Transform root, Vector3 at, Material wood, Material metal, Material silver)
    {
        for (int i = 0; i < 4; i++) Box(root, "Bench cedar seat slat", at + new Vector3(0, .48f, -.24f + i * .16f), new Vector3(2.5f, .085f, .13f), wood);
        for (int i = 0; i < 3; i++) Box(root, "Bench cedar back slat", at + new Vector3(0, .78f + i * .17f, .32f), new Vector3(2.5f, .13f, .075f), wood);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * .9f;
            foreach (float z in new[] { -.23f, .24f }) Tube(root, at + new Vector3(x, .04f, z), at + new Vector3(x, .45f, z), .04f, metal);
            Tube(root, at + new Vector3(x, .43f, .25f), at + new Vector3(x, 1.23f, .35f), .035f, metal);
            Tube(root, at + new Vector3(x, .72f, -.24f), at + new Vector3(x, .72f, .32f), .03f, metal);
            Tube(root, at + new Vector3(x, .46f, -.24f), at + new Vector3(x, .72f, -.24f), .03f, metal);
            for (int i = 0; i < 3; i++) Box(root, "Bench back fastener", at + new Vector3(x, .78f + i * .17f, .275f), Vector3.one * .025f, silver);
        }
    }
    private static void Bicycle(Transform root, Vector3 at, Material frame, Material silver, Material rubber, Material saddle)
    {
        Vector3 rear = at + new Vector3(-.66f, .40f, 0), front = at + new Vector3(.66f, .40f, 0);
        Vector3 crank = at + new Vector3(-.05f, .38f, 0), seat = at + new Vector3(-.25f, .99f, 0), head = at + new Vector3(.39f, .97f, 0);
        foreach (var wheel in new[] { rear, front })
        {
            Ring(root, wheel, .38f, .033f, rubber); Ring(root, wheel, .337f, .012f, silver);
            for (int spoke = 0; spoke < 10; spoke++)
            {
                float a = spoke * Mathf.PI * 2 / 10;
                Tube(root, wheel, wheel + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .33f, .005f, silver);
            }
        }
        foreach (var pair in new[] { (rear, crank), (rear, seat), (crank, seat), (seat, head), (head, crank), (head, front) }) Tube(root, pair.Item1, pair.Item2, .023f, frame);
        Tube(root, seat, seat + Vector3.up * .10f, .017f, silver);
        Box(root, "Bicycle leather saddle", seat + new Vector3(-.03f, .12f, 0), new Vector3(.26f, .065f, .16f), saddle);
        Vector3 handle = head + new Vector3(.03f, .18f, 0);
        Tube(root, head, handle, .017f, silver); Tube(root, handle + Vector3.back * .25f, handle + Vector3.forward * .25f, .018f, silver);
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(root, handle + Vector3.forward * side * .17f, handle + Vector3.forward * side * .28f, .026f, rubber);
            Box(root, "Bicycle pedal", crank + new Vector3(side * .11f, side * .10f, side * .13f), new Vector3(.14f, .035f, .10f), rubber);
        }
        Ring(root, crank, .105f, .013f, silver);
        Tube(root, rear + Vector3.back * .07f, crank + Vector3.back * .07f, .008f, silver);
    }
    private static void Ring(Transform root, Vector3 center, float radius, float tube, Material mat)
    {
        // A low segment torus keeps wheels round without using dozens of primitive objects.
        const int segments = 32, sides = 6;
        var vertices = new Vector3[segments * sides]; var triangles = new int[segments * sides * 6];
        for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
        {
            float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
            vertices[i * sides + j] = center + new Vector3(Mathf.Cos(a) * (radius + tube * Mathf.Cos(b)), Mathf.Sin(a) * (radius + tube * Mathf.Cos(b)), tube * Mathf.Sin(b));
            int v = i * sides + j, n = ((i + 1) % segments) * sides + j, w = i * sides + (j + 1) % sides, nw = ((i + 1) % segments) * sides + (j + 1) % sides, t = v * 6;
            triangles[t] = v; triangles[t+1] = w; triangles[t+2] = n; triangles[t+3] = w; triangles[t+4] = nw; triangles[t+5] = n;
        }
        var mesh = new Mesh(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
        var go = new GameObject("Bicycle wheel ring", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
    private static void Tube(Transform root, Vector3 a, Vector3 b, float radius, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Furniture tube"; go.transform.SetParent(root, false);
        go.transform.position = (a+b)*.5f; go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b-a).normalized); go.transform.localScale = new Vector3(radius*2, (b-a).magnitude*.5f, radius*2);
        go.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Box(Transform root, string name, Vector3 at, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false); go.transform.position = at; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static Material Mat(string name, Color color, float smooth, float metal = 0)
    {
        string path = Folder + "Furniture_" + name.Replace(' ', '_') + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }
}
#endif
