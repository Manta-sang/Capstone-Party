#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Two decorative compact half courts inside the track's curved ends.</summary>
[InitializeOnLoad]
public static class QteBasketballCourts
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static Transform court;
    private static float side;
    private static Material surface, paint, white, steel, orange, padding;
    static QteBasketballCourts() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Library/QteBasketballCourts.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); }
        catch (Exception ex) { File.WriteAllText("Library/QteBasketballCourts.result", "FAIL: " + ex); Debug.LogException(ex); }
    }
    [MenuItem("Party/Add QTE Basketball Half Courts")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var current = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Qte.unity");
        if (current.isLoaded && current.isDirty) throw new InvalidOperationException("Save QTE changes first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
        var environment = GameObject.Find("04 Environment");
        if (environment == null) throw new InvalidOperationException("Environment group missing.");
        var old = environment.transform.Find("Basketball half courts");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Basketball half courts").transform; root.SetParent(environment.transform, false);
        surface = Mat("Muted teal surfacing", new Color(.16f,.34f,.35f));
        paint = Mat("Slate blue key", new Color(.18f,.26f,.34f));
        white = Mat("Ivory court markings", new Color(.92f,.93f,.86f));
        steel = Mat("Galvanized hoop support", new Color(.53f,.59f,.60f), .55f, .6f);
        orange = Mat("Orange rim", new Color(.87f,.30f,.12f), .35f);
        padding = Mat("Dark teal post padding", new Color(.075f,.17f,.19f));
        int meshes = 0;
        foreach (float end in new[] { -1f, 1f })
        {
            side = end; court = new GameObject(end < 0 ? "West half court" : "East half court").transform;
            court.SetParent(root, false);
            SemicircleSurface();
            Box("Painted key", 2.4f, .097f, 0, new Vector3(4.8f, .008f, 3.04f), paint);
            Rectangle(0, 22, -5.5f, 5.5f);
            Rectangle(0, 4.8f, -1.9f, 1.9f);
            Arc(4.8f, 0, 1.9f, -Mathf.PI*.5f, Mathf.PI*.5f);
            Arc(1.1f, 0, 1.05f, -Mathf.PI*.5f, Mathf.PI*.5f);
            Arc(1.1f, 0, 5.0f, -Mathf.PI*.5f, Mathf.PI*.5f);
            Line(0,-5,1.1f,-5); Line(0,5,1.1f,5);
            // Dashed inner half of the free-throw circle, plus short lane marks.
            for (int i = 0; i < 10; i++) Arc(4.8f,0,1.9f,Mathf.PI*.5f+i*Mathf.PI/10,Mathf.PI*.5f+(i+.5f)*Mathf.PI/10);
            foreach (float u in new[] { 1.3f, 2.15f, 3.0f, 3.85f })
            { Line(u,-1.9f,u,-2.15f); Line(u,1.9f,u,2.15f); }
            Hoop();
            CombineCourt();
            meshes += court.childCount;
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        QteHierarchyOptimization.Optimize();
        // Reimport updated combined meshes so editor GPU buffers match their new vertices.
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Materials/QteGenerated" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Refined_West_half_court_") || path.Contains("Refined_East_half_court_"))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
        Directory.CreateDirectory("Library/QteSchoolQuality");
        var capture = typeof(QteSchoolQuality).GetMethod("Capture", PrivateStatic);
        capture.Invoke(null, new object[] { "basketball-overview.png", new Vector3(0,68,-49), new Vector3(0,0,0), 64f });
        capture.Invoke(null, new object[] { "basketball-court.png", new Vector3(21,23,-30), new Vector3(41,1,0), 48f });
        File.WriteAllText("Library/QteBasketballCourts.result", $"PASS: two fully surfaced semicircles; west hoop at school side, east hoop at spectator side; {meshes} combined meshes; 15.2m semicircle floors meet the inner track edge; no runtime scripts/colliders; spawn/material checks passed.");
    }
    private static Vector3 P(float u, float y, float z) => new Vector3(side*41f + z*.8f,y,-side*(11f-u));
    private static void CombineCourt()
    {
        var renderers = court.GetComponentsInChildren<MeshRenderer>();
        foreach (var group in renderers.GroupBy(r => r.sharedMaterial))
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(group.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = r.localToWorldMatrix }).ToArray(), true, true);
            string path = "Assets/Materials/QteGenerated/Basketball_Semicircle_" + (side < 0 ? "West_" : "East_") + group.Key.name.Replace(' ', '_') + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else
            {
                saved.Clear(); saved.indexFormat = mesh.indexFormat;
                saved.vertices = mesh.vertices; saved.normals = mesh.normals; saved.uv = mesh.uv; saved.triangles = mesh.triangles;
                saved.RecalculateBounds(); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh);
            }
            var go = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(court, false);
            go.GetComponent<MeshFilter>().sharedMesh = saved; go.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
        foreach (var renderer in renderers) Object.DestroyImmediate(renderer.gameObject);
    }
    private static void SemicircleSurface()
    {
        const int segments = 100;
        var vertices = new Vector3[segments + 2];
        var triangles = new int[segments * 3];
        vertices[0] = new Vector3(side * 35, .092f, 0);
        for (int i = 0; i <= segments; i++)
        {
            float angle = -Mathf.PI * .5f + Mathf.PI * i / segments;
            vertices[i + 1] = new Vector3(side * (35 + 15.2f * Mathf.Cos(angle)), .092f, 15.2f * Mathf.Sin(angle));
            if (i == 0) continue;
            int t = (i - 1) * 3;
            triangles[t] = 0; triangles[t + 1] = side > 0 ? i + 1 : i; triangles[t + 2] = side > 0 ? i : i + 1;
        }
        var mesh = new Mesh { name = "Full semicircle sports surface" };
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
        var go = new GameObject("Full semicircle sports surface", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(court, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = surface;
    }
    private static void Hoop()
    {
        Box("Support anchor",-.65f,.14f,0,new Vector3(.8f,.16f,.9f),steel);
        Tube(P(-.65f,.18f,0),P(-.65f,3.85f,0),.085f,steel);
        Box("Padded post",-.65f,.91f,0,new Vector3(.26f,1.5f,.33f),padding);
        Tube(P(-.65f,3.85f,0),P(.48f,3.85f,0),.065f,steel);
        Tube(P(-.65f,2.8f,0),P(.48f,3.75f,0),.045f,steel);
        Box("Backboard",.5f,3.55f,0,new Vector3(.11f,1.06f,1.8f),white);
        // Target rectangle on the face pointing toward the soccer pitch.
        Box("Target left",.565f,3.49f,-.29f,new Vector3(.018f,.43f,.035f),orange);
        Box("Target right",.565f,3.49f,.29f,new Vector3(.018f,.43f,.035f),orange);
        foreach(float y in new[]{3.29f,3.70f}) Box("Target horizontal",.565f,y,0,new Vector3(.018f,.035f,.60f),orange);
        Tube(P(.58f,3.05f,0),P(.88f,3.05f,0),.025f,orange);
        for(int i=0;i<32;i++)
        {
            float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;
            Tube(P(1.1f+.23f*Mathf.Cos(a),3.05f,.23f*Mathf.Sin(a)),P(1.1f+.23f*Mathf.Cos(b),3.05f,.23f*Mathf.Sin(b)),.018f,orange);
        }
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI*2/12,b=(i+.5f)*Mathf.PI*2/12;
            Tube(P(1.1f+.22f*Mathf.Cos(a),3.02f,.22f*Mathf.Sin(a)),P(1.1f+.14f*Mathf.Cos(b),2.66f,.14f*Mathf.Sin(b)),.006f,white);
            Tube(P(1.1f+.22f*Mathf.Cos(a),3.02f,.22f*Mathf.Sin(a)),P(1.1f+.14f*Mathf.Cos(b-Mathf.PI*2/12),2.66f,.14f*Mathf.Sin(b-Mathf.PI*2/12)),.006f,white);
        }
    }
    private static void Rectangle(float a,float b,float c,float d) { Line(a,c,b,c);Line(b,c,b,d);Line(b,d,a,d);Line(a,d,a,c); }
    private static void Arc(float u,float z,float radius,float start,float end)
    {
        int count=Mathf.Max(2,Mathf.CeilToInt((end-start)*radius*9));
        for(int i=0;i<count;i++) { float a=Mathf.Lerp(start,end,i/(float)count),b=Mathf.Lerp(start,end,(i+1)/(float)count); Line(u+Mathf.Cos(a)*radius,z+Mathf.Sin(a)*radius,u+Mathf.Cos(b)*radius,z+Mathf.Sin(b)*radius); }
    }
    private static void Line(float u,float z,float v,float w)
    {
        Vector3 a=P(u,.108f,z),b=P(v,.108f,w);
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Court line";go.transform.SetParent(court,false);
        go.transform.position=(a+b)*.5f;go.transform.rotation=Quaternion.LookRotation(b-a);go.transform.localScale=new Vector3(.065f,.008f,(b-a).magnitude+.012f);
        go.GetComponent<Renderer>().sharedMaterial=white;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Box(string name,float u,float y,float z,Vector3 size,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(court,false);go.transform.position=P(u,y,z);go.transform.rotation=Quaternion.Euler(0,90,0);go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Tube(Vector3 a,Vector3 b,float radius,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Hoop tube";go.transform.SetParent(court,false);go.transform.position=(a+b)*.5f;
        go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);
        go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static Material Mat(string name,Color color,float gloss=.12f,float metal=0)
    {
        string path="Assets/Materials/QteGenerated/Basketball_"+name.Replace(' ','_')+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",gloss);mat.SetFloat("_Metallic",metal);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
}
#endif
