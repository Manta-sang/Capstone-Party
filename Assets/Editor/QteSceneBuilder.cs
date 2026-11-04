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
    private static Transform campusBackdropRoot;

    private static void ConfigureSportsDaySky()
    {
        const string skyPath = "Assets/Scenes/QteGenerated/QTE_SportsDay_Skybox.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) throw new System.InvalidOperationException("Unity procedural skybox shader is unavailable.");
            sky = new Material(shader) { name = "QTE Sports Day Skybox" };
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(.42f,.68f,1f));
        if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(.51f,.61f,.52f));
        if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", .72f);
        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", .88f);
        if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", .018f);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = .95f;
    }

    private static void BuildSportsDayClouds()
    {
        const string cloudMaterialPath = "Assets/Scenes/QteGenerated/QTE_SoftClouds.mat";
        var cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(cloudMaterialPath);
        if (cloudMaterial == null)
        {
            var shader = Shader.Find("Unlit/Color");
            cloudMaterial = new Material(shader) { name = "QTE Soft White Clouds", color = new Color(.90f,.95f,1f) };
            AssetDatabase.CreateAsset(cloudMaterial, cloudMaterialPath);
        }
        var cloudRoot = new GameObject("QTE Sports Day Cloud Layer");
        Camera mainCamera = Camera.main;
        Vector3 cameraPosition = mainCamera != null ? mainCamera.transform.position : new Vector3(0,58,-43);
        for (int cloud = 0; cloud < 12; cloud++)
        {
            float angle = cloud * 30f * Mathf.Deg2Rad;
            float radius = 225f + (cloud % 3) * 14f;
            Vector3 center = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            float distance = Vector2.Distance(new Vector2(center.x,center.z),new Vector2(cameraPosition.x,cameraPosition.z));
            center.y = cameraPosition.y + distance * (.085f + (cloud % 3) * .008f) + (cloud % 2) * 2.5f;
            float width = 23f + (cloud % 4) * 4f;
            AddCloudPuff(cloudRoot.transform,center,new Vector3(width,.22f*width,.48f*width),cloudMaterial);
            AddCloudPuff(cloudRoot.transform,center+new Vector3(-width*.34f,-.12f*width,-width*.04f),new Vector3(width*.57f,.19f*width,.42f*width),cloudMaterial);
            AddCloudPuff(cloudRoot.transform,center+new Vector3(width*.32f,-.08f*width,width*.03f),new Vector3(width*.64f,.2f*width,.43f*width),cloudMaterial);
            AddCloudPuff(cloudRoot.transform,center+new Vector3(-width*.12f,.08f*width,width*.20f),new Vector3(width*.49f,.23f*width,.4f*width),cloudMaterial);
            AddCloudPuff(cloudRoot.transform,center+new Vector3(width*.18f,.04f*width,-width*.22f),new Vector3(width*.46f,.2f*width,.38f*width),cloudMaterial);
        }
    }

    private static void AddCloudPuff(Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.name = "Soft sky cloud puff";
        puff.transform.SetParent(parent,false);
        puff.transform.position = position;
        puff.transform.localScale = scale;
        puff.GetComponent<Renderer>().sharedMaterial = material;
        var collider = puff.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
    }

    [MenuItem("Party/Build QTE School Track Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Qte";
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/QteGenerated"))
            AssetDatabase.CreateFolder("Assets/Scenes", "QteGenerated");
        ConfigureSportsDaySky();

        var light = new GameObject("School Daylight", typeof(Light));
          light.transform.rotation = Quaternion.Euler(30, -32, 0);
        var sunlight = light.GetComponent<Light>();
        sunlight.type = LightType.Directional;
        sunlight.intensity = 1.1f;
        RenderSettings.sun = sunlight;

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        // Start inside the campus fence with an elevated view that includes the sky horizon.
          cameraObject.transform.position = new Vector3(0, 55, -55);
          cameraObject.transform.LookAt(new Vector3(0, 27, 20));
        var camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = 82;
        camera.farClipPlane = 460;
        camera.backgroundColor = new Color(.52f, .72f, .88f);
        camera.clearFlags = CameraClearFlags.Skybox;
        BuildSportsDayClouds();

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
        BuildQteCampusBackdrop();

        QteImportValidation.AttachSharedStartUI();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Qte.unity");
        AssetDatabase.SaveAssets();
        Debug.Log($"QTE_SCHOOL_TRACK_BUILD_PASS: saved scene with four network character prefabs, {audiencePrefabs.Length} idle crowd prefabs, bleachers, four lane spawns, an overview camera, and a school-day directional light.");
    }

    private static void BuildQteCampusBackdrop()
    {
        campusBackdropRoot = new GameObject("QTE Campus Backdrop").transform;
        var road = CampusMaterial("Campus avenue asphalt", new Color(.20f, .24f, .26f));
        var sidewalk = CampusMaterial("Campus avenue sidewalk", new Color(.62f, .65f, .61f));
        var roadLine = CampusMaterial("Campus avenue markings", new Color(.90f, .83f, .64f));
        BuildCampusPerimeterFence();
        BuildLeftPark();
        BuildRightOcean();
        CampusCube("QTE Campus north avenue", new Vector3(0, .005f, 57), new Vector3(230, .08f, 8), road);
        CampusCube("QTE Campus north sidewalk", new Vector3(0, .035f, 51.8f), new Vector3(230, .12f, 2.2f), sidewalk);
        CampusCube("QTE Campus north curb", new Vector3(0, .11f, 53.2f), new Vector3(230, .24f, .22f), sidewalk);
        for (int x = -108; x <= 108; x += 9)
            CampusCube("QTE Campus avenue dash", new Vector3(x, .052f, 57), new Vector3(4.2f, .025f, .12f), roadLine);
        foreach (float x in new[] { -80f, 80f })
        {
            CampusCube("QTE Campus side avenue", new Vector3(x, .005f, -5), new Vector3(7, .08f, 108), road);
            CampusCube("QTE Campus side walk", new Vector3(x + Mathf.Sign(x) * 4.6f, .035f, -5), new Vector3(2.1f, .12f, 108), sidewalk);
            for (int z = -48; z <= 40; z += 18)
                CampusStreetLight(x - Mathf.Sign(x) * 2f, z);
        }

        // Replace the round, stacked low-poly trees with broad layered crowns and visible branches.
        var positions = new System.Collections.Generic.List<Vector3>();
        // Keep the full canopy well outside the oval's outer radius (21.5 m).
        // Cluster trees along the side fence and behind the school instead of on the bends.
        foreach (float x in new[] { -62f, 62f })
            foreach (float z in new[] { -44f, -31f, -6f, 7f, 32f, 44f })
                positions.Add(new Vector3(x, 0, z));
        foreach (float x in new[] { -54f, -47f, -26f, 26f, 47f, 54f })
            positions.Add(new Vector3(x, 0, 43f));
        foreach (float x in new[] { -54f, 54f })
            foreach (float z in new[] { -42f, -31f, 31f, 42f })
                positions.Add(new Vector3(x, 0, z));
        foreach (float x in new[] { -215f, -180f, -145f, -110f })
            foreach (float z in new[] { -90f, -30f, 35f, 95f })
                positions.Add(new Vector3(x, 0, z));
        for (int i = 0; i < positions.Count; i++) BuildCampusTree(positions[i], i);

        // Distant mid-rise blocks sit behind the school and form a full skyline in the overview.
        var palettes = new[]
        {
            new Color(.72f, .68f, .58f), new Color(.57f, .66f, .68f), new Color(.77f, .72f, .62f),
            new Color(.62f, .65f, .57f), new Color(.70f, .61f, .56f), new Color(.57f, .62f, .67f)
        };
        var glass = CampusMaterial("City blue gray window glass", new Color(.19f, .31f, .38f));
        var trim = CampusMaterial("City warm concrete trim", new Color(.87f, .81f, .69f));
        for (int i = 0; i < 11; i++)
        {
            float x = -90 + i * 18;
            float height = 23 + (i * 7 % 13);
            BuildDistantCityBlock(x, 122, 16 + (i % 2) * 4, height, palettes[(i + 2) % palettes.Length], glass, trim, i);
        }
        BuildDetailedNeighborhood();
    }

    private static void BuildDetailedNeighborhood()
    {
        // A compact, varied neighborhood with layered facade detail reads better than
        // repeating one oversized asset with a fixed orange texture.
        var glass = CampusMaterial("City blue gray window glass", new Color(.19f, .31f, .38f));
        var trim = CampusMaterial("City warm concrete trim", new Color(.87f, .81f, .69f));
        var colors = new[]
        {
            new Color(.68f,.70f,.68f), new Color(.75f,.70f,.62f), new Color(.62f,.68f,.68f),
            new Color(.73f,.69f,.64f), new Color(.65f,.69f,.62f), new Color(.72f,.67f,.65f), new Color(.64f,.68f,.72f)
        };
        for (int i = 0; i < colors.Length; i++)
        {
            float x = -84f + i * 28f;
            float width = 23f + (i % 3) * 2f;
            float height = 12f + (i % 4) * 2.2f;
            BuildCityBlock(x, 103f + (i % 2) * 3f, width, height, 13f, colors[i], glass, trim, i + 1);
        }
        ApplyCampusGrassTexture();
    }

    private static void ApplyCampusGrassTexture()
    {
        const string path = "Assets/Scenes/QteGenerated/Campus_grass_field_texture.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            const int size = 512;
            texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Campus grass field texture", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(64021);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float broad = Mathf.PerlinNoise(x * .009f, y * .009f);
                float blades = Mathf.PerlinNoise(x * .047f, y * .047f);
                float fleck = UnityEngine.Random.value * .13f;
                float shade = Mathf.Clamp01(.34f + broad * .35f + blades * .20f + fleck);
                Color color = Color.Lerp(new Color(.14f,.30f,.13f), new Color(.38f,.54f,.27f), shade);
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            UnityEngine.Random.state = previousState;
            AssetDatabase.CreateAsset(texture, path);
        }

        var ground = GameObject.Find("School ground");
        if (ground != null)
        {
            const string materialPath = "Assets/Scenes/QteGenerated/School_ground_natural_turf.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(ground.GetComponent<Renderer>().sharedMaterial) { name = "School ground natural turf" }; AssetDatabase.CreateAsset(material, materialPath); }
            material.mainTexture = texture; material.color = Color.white; material.mainTextureScale = new Vector2(48, 40);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }
        var park = GameObject.Find("QTE Left neighborhood park lawn");
        if (park != null)
        {
            const string materialPath = "Assets/Scenes/QteGenerated/Park_natural_turf.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(park.GetComponent<Renderer>().sharedMaterial) { name = "Park natural turf" }; AssetDatabase.CreateAsset(material, materialPath); }
            material.mainTexture = texture; material.color = Color.white; material.mainTextureScale = new Vector2(18, 21);
            park.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private static void BuildCampusPerimeterFence()
    {
        var paint = CampusMaterial("School perimeter fence deep green", new Color(.16f, .30f, .27f));
        paint.SetFloat("_Metallic", .38f);
        BuildFenceSide("West", false, -68f, -50f, 100f, paint);
        BuildFenceSide("East", false, 68f, -50f, 100f, paint);
        BuildFenceSide("South", true, -50f, -68f, 136f, paint);
        BuildFenceSide("North", true, 50f, -68f, 136f, paint);
    }

    private static void BuildFenceSide(string side, bool alongX, float fixedCoordinate, float start, float length, Material material)
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var unit = source.GetComponent<MeshFilter>().sharedMesh;
        var unitVertices = unit.vertices;
        var unitTriangles = unit.triangles;
        Object.DestroyImmediate(source);
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        void AddBox(float along, float y, float height, float width, float depth, float span)
        {
            Vector3 center = alongX ? new Vector3(along, y, fixedCoordinate) : new Vector3(fixedCoordinate, y, along);
            Vector3 size = alongX ? new Vector3(span, height, depth) : new Vector3(depth, height, span);
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, size);
            int vertexStart = vertices.Count;
            for (int i = 0; i < unitVertices.Length; i++) vertices.Add(matrix.MultiplyPoint3x4(unitVertices[i]));
            for (int i = 0; i < unitTriangles.Length; i++) triangles.Add(vertexStart + unitTriangles[i]);
        }
        for (float distance = 0; distance <= length; distance += 3.2f)
        {
            AddBox(start + Mathf.Min(distance, length), 1.95f, 3.9f, .20f, .20f, .20f);
            AddBox(start + Mathf.Min(distance, length), 4.02f, .24f, .32f, .32f, .32f);
        }
        for (float distance = 0; distance <= length; distance += .48f)
            AddBox(start + Mathf.Min(distance, length), 1.92f, 3.55f, .065f, .08f, .065f);
        foreach (float y in new[] { .28f, 1.95f, 3.72f })
            AddBox(start + length * .5f, y, .11f, .16f, .16f, length);
        var mesh = new Mesh { name = "School perimeter fence " + side };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        string path = "Assets/Scenes/QteGenerated/School_perimeter_fence_" + side + ".asset";
        var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (previous != null)
        {
            previous.Clear();
            previous.vertices = mesh.vertices;
            previous.triangles = mesh.triangles;
            previous.RecalculateNormals();
            Object.DestroyImmediate(mesh);
            mesh = previous;
        }
        else AssetDatabase.CreateAsset(mesh, path);
        var fence = new GameObject("QTE School perimeter fence " + side);
        fence.transform.SetParent(campusBackdropRoot, true);
        fence.AddComponent<MeshFilter>().sharedMesh = mesh;
        fence.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildLeftPark()
    {
        var grass = CampusMaterial("Park lawn green", new Color(.27f, .48f, .31f));
        var path = CampusMaterial("Park walking path warm stone", new Color(.70f, .62f, .48f));
        var wood = CampusMaterial("Park bench timber", new Color(.38f, .25f, .16f));
        var metal = CampusMaterial("Park bench dark metal", new Color(.20f, .28f, .27f));
        var pond = CampusMaterial("Park pond water", new Color(.23f, .56f, .63f));
        CampusCube("QTE Left neighborhood park lawn", new Vector3(-178, .006f, 0), new Vector3(214, .08f, 250), grass);
        CampusPathSegment("QTE Park main walking path", new Vector3(-88, .10f, -112), new Vector3(-178, .10f, 103), 4.2f, path);
        CampusPathSegment("QTE Park cross path", new Vector3(-244, .10f, -34), new Vector3(-91, .10f, -34), 3.2f, path);
        CampusPathSegment("QTE Park upper cross path", new Vector3(-245, .10f, 50), new Vector3(-91, .10f, 50), 3.2f, path);
        CampusSphere("QTE Park oval pond", new Vector3(-206, .14f, 8), new Vector3(28, .16f, 17), pond);
        for (int i = 0; i < 5; i++)
        {
            float x = -245 + i * 31;
            BuildParkBench(x, -72 + (i % 2) * 110, wood, metal);
        }
        var shrub = CampusMaterial("Park flowering shrubs", new Color(.33f, .52f, .34f));
        var flower = CampusMaterial("Park flower accents", new Color(.88f, .55f, .52f));
        for (int i = 0; i < 18; i++)
        {
            float x = -248 + (i % 6) * 25;
            float z = -100 + (i / 6) * 97;
            CampusSphere("QTE Park shrub", new Vector3(x, .64f, z), new Vector3(2.1f, 1.25f, 1.8f), shrub);
            if (i % 3 == 0) CampusSphere("QTE Park flower bed", new Vector3(x + 2.4f, .38f, z + 1.4f), new Vector3(1.2f, .42f, .9f), flower);
        }
    }

    private static void BuildParkBench(float x, float z, Material wood, Material metal)
    {
        for (int plank = 0; plank < 4; plank++)
            CampusCube("QTE Park bench seat plank", new Vector3(x, 1.0f, z + (plank - 1.5f) * .16f), new Vector3(2.6f, .12f, .13f), wood);
        for (int support = -1; support <= 1; support += 2)
        {
            CampusCube("QTE Park bench leg", new Vector3(x + support * .88f, .55f, z), new Vector3(.12f, .9f, .12f), metal);
            CampusCube("QTE Park bench back post", new Vector3(x + support * .88f, 1.62f, z + .57f), new Vector3(.12f, 1.25f, .12f), metal);
        }
        for (int plank = 0; plank < 3; plank++)
            CampusCube("QTE Park bench back plank", new Vector3(x, 1.45f + plank * .27f, z + .57f), new Vector3(2.6f, .13f, .12f), wood);
    }

    private static void BuildRightOcean()
    {
        var beach = CampusMaterial("Coastal pale sand", new Color(.82f, .73f, .55f));
        var shallow = CampusMaterial("Ocean shallow turquoise", new Color(.22f, .65f, .70f));
        var deep = CampusMaterial("Ocean blue water", new Color(.12f, .42f, .57f));
        var foam = CampusMaterial("Ocean wave foam", new Color(.78f, .90f, .86f));
        CampusCube("QTE Right seaside beach", new Vector3(100, .012f, 0), new Vector3(34, .08f, 310), beach);
        CampusCube("QTE Right ocean shallows", new Vector3(144, .014f, 0), new Vector3(58, .08f, 500), shallow);
        CampusCube("QTE Right ocean open water", new Vector3(340, .016f, 0), new Vector3(340, .08f, 540), deep);
        CampusCube("QTE Right seaside promenade", new Vector3(87, .09f, 0), new Vector3(2.8f, .16f, 300), CampusMaterial("Seaside promenade stone", new Color(.67f, .66f, .58f)));
        for (int i = 0; i < 25; i++)
        {
            float z = -220 + i * 18;
            float x = 119 + Mathf.Sin(i * 1.71f) * 2.3f;
            CampusPathSegment("QTE Ocean rolling foam", new Vector3(x, .11f, z), new Vector3(x + .6f, .11f, z + 8), .22f, foam);
        }
    }

    private static void CampusPathSegment(string name, Vector3 start, Vector3 end, float width, Material material)
    {
        Vector3 direction = end - start;
        var segment = CampusCube(name, (start + end) * .5f, new Vector3(width, .045f, direction.magnitude), material);
        segment.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static void BuildCampusTree(Vector3 position, int seed)
    {
        var trunk = CampusMaterial("Campus tree bark", new Color(.28f, .18f, .12f));
        var branch = CampusMaterial("Campus tree branch", new Color(.36f, .23f, .15f));
        var leaves = new[]
        {
            CampusMaterial("Campus tree leaves dark", new Color(.10f, .27f, .14f)),
            CampusMaterial("Campus tree leaves mid", new Color(.17f, .38f, .19f)),
            CampusMaterial("Campus tree leaves light", new Color(.29f, .47f, .23f))
        };
        foreach(var material in leaves)material.SetFloat("_Glossiness",.18f);
        trunk.SetFloat("_Glossiness",.22f);
        branch.SetFloat("_Glossiness",.2f);
        var random=new System.Random(seed*7919+1049);
        float scale=1.15f+(float)random.NextDouble()*.32f;
        float treeHeight=9.1f*scale;
        float baseRadius=.31f*scale;

        // Overlapping trunk sections taper toward the crown instead of forming one straight pole.
        CampusCylinder("QTE Campus tree trunk base",position+Vector3.up*1.95f*scale,baseRadius*1.25f,4f*scale,trunk);
        CampusCylinder("QTE Campus tree trunk middle",position+Vector3.up*5.2f*scale,baseRadius*.82f,3.2f*scale,trunk);
        CampusCylinder("QTE Campus tree trunk crown",position+Vector3.up*7.35f*scale,baseRadius*.48f,3.1f*scale,trunk);

        // Low root flares and a branching framework remain visible beneath the foliage.
        for(int root=0;root<5;root++)
        {
            float angle=(root*72f+(seed%13)*11f)*Mathf.Deg2Rad;
            Vector3 start=position+Vector3.up*.18f;
            Vector3 end=position+new Vector3(Mathf.Cos(angle)*1.15f*scale,.72f*scale,Mathf.Sin(angle)*1.15f*scale);
            CampusCylinderBetween("QTE Campus tree root flare",start,end,.14f*scale,trunk);
        }
        const int primaryBranchCount=8;
        for(int i=0;i<primaryBranchCount;i++)
        {
            float angle=(i*137.5f+seed*19f)*Mathf.Deg2Rad;
            float startHeight=(3.15f+(float)random.NextDouble()*2.25f)*scale;
            float reach=(2.05f+(float)random.NextDouble()*1.2f)*scale;
            Vector3 start=position+Vector3.up*startHeight;
            Vector3 end=start+new Vector3(Mathf.Cos(angle)*reach,(.85f+(float)random.NextDouble()*1.25f)*scale,Mathf.Sin(angle)*reach);
            CampusCylinderBetween("QTE Campus tree primary branch",start,end,(.135f-(i%3)*.018f)*scale,branch);
            for(int twig=0;twig<2;twig++)
            {
                float twigAngle=angle+(twig==0?-1f:1f)*(.42f+(float)random.NextDouble()*.36f);
                Vector3 twigStart=Vector3.Lerp(start,end,.58f+(float)random.NextDouble()*.2f);
                Vector3 twigEnd=twigStart+new Vector3(Mathf.Cos(twigAngle)*.92f*scale,(.6f+(float)random.NextDouble()*.75f)*scale,Mathf.Sin(twigAngle)*.92f*scale);
                CampusCylinderBetween("QTE Campus tree fine branch",twigStart,twigEnd,.055f*scale,branch);
            }
        }

        // A broad, irregular crown of varied leaf masses avoids the stacked candy-ball silhouette.
        const int leafMassCount=28;
        float crownCenter=treeHeight*.79f;
        for(int i=0;i<leafMassCount;i++)
        {
            float angle=(i*137.508f+seed*23f)*Mathf.Deg2Rad;
            float vertical=((float)random.NextDouble()*2f-1f)*2.45f*scale;
            float crownBand=Mathf.Sqrt(Mathf.Max(.12f,1f-Mathf.Pow(vertical/(2.85f*scale),2f)));
            float radius=(1.25f+(float)random.NextDouble()*2.45f)*scale*crownBand;
            Vector3 center=position+new Vector3(Mathf.Cos(angle)*radius,crownCenter+vertical,Mathf.Sin(angle)*radius*.82f);
            float leafWidth=(.95f+(float)random.NextDouble()*.75f)*scale;
            float leafHeight=(.68f+(float)random.NextDouble()*.72f)*scale;
            float leafDepth=(.95f+(float)random.NextDouble()*.8f)*scale;
            CampusSphere("QTE Campus natural leaf crown",center,new Vector3(leafWidth,leafHeight,leafDepth),leaves[random.Next(leaves.Length)]);
        }
    }

    private static void BuildDistantCityBlock(float x, float z, float width, float height, Color facade, Material glass, Material trim, int seed)
    {
        var wall = CampusMaterial("Distant city facade " + ColorUtility.ToHtmlStringRGB(facade), facade);
        var highlight = CampusMaterial("City window cool reflection", new Color(.38f, .56f, .61f));
        CampusCube("QTE Distant skyline tower", new Vector3(x, height * .5f, z), new Vector3(width, height, 18), wall);
        CampusCube("QTE Distant skyline roof", new Vector3(x, height + .22f, z), new Vector3(width + .6f, .44f, 18.6f), trim);
        CampusCube("QTE Distant skyline crown", new Vector3(x, height + .75f, z), new Vector3(width * .34f, .62f, 7), wall);
        CampusCube("QTE Distant skyline facade spine", new Vector3(x, height * .52f, z - 9.2f), new Vector3(.42f, height * .92f, .32f), trim);
        for (int row = 0; row < 5; row++)
        {
            float y = 3 + row * (height - 4) / 5f;
            for (int column = 0; column < 3; column++)
            {
                float wx = x + (column - 1) * width * .28f;
                var glassMat = (row + column + seed) % 6 == 0 ? highlight : glass;
                CampusCube("QTE Distant skyline window surround", new Vector3(wx, y, z - 9.07f), new Vector3(width * .22f, 1.72f, .12f), trim);
                CampusCube("QTE Distant skyline window", new Vector3(wx, y, z - 9.16f), new Vector3(width * .16f, 1.38f, .06f), glassMat);
            }
        }
    }

    private static void BuildCityBlock(float x, float z, float width, float height, float depth, Color facade, Material glass, Material trim, int seed)
    {
        var wall = CampusMaterial("City facade " + ColorUtility.ToHtmlStringRGB(facade), facade);
        var highlight = CampusMaterial("City window cool reflection", new Color(.38f, .56f, .61f));
        var shopGlass = CampusMaterial("City shopfront glass", new Color(.24f, .40f, .43f));
        CampusCube("QTE City mid-rise", new Vector3(x, height * .5f, z), new Vector3(width, height, depth), wall);
        CampusCube("QTE City podium", new Vector3(x, .7f, z - depth * .5f - .08f), new Vector3(width + .35f, 1.4f, .22f), trim);
        CampusCube("QTE City roof cornice", new Vector3(x, height + .18f, z), new Vector3(width + .5f, .36f, depth + .5f), trim);
        var rooftop = CampusMaterial("City rooftop equipment", new Color(.43f, .46f, .44f));
        CampusCube("QTE City rooftop utility", new Vector3(x + width * .27f, height + .75f, z), new Vector3(width * .28f, .8f, depth * .38f), rooftop);
        CampusCube("QTE City rooftop stairwell", new Vector3(x - width * .22f, height + .55f, z + depth * .12f), new Vector3(width * .30f, 1.05f, depth * .42f), wall);
        CampusCube("QTE City facade pilaster left", new Vector3(x - width * .5f + .16f, height * .51f, z - depth * .5f - .12f), new Vector3(.28f, height * .95f, .2f), trim);
        CampusCube("QTE City facade pilaster right", new Vector3(x + width * .5f - .16f, height * .51f, z - depth * .5f - .12f), new Vector3(.28f, height * .95f, .2f), trim);
        int columns = Mathf.Max(3, Mathf.FloorToInt(width / 4.8f));
        int rows = Mathf.Max(3, Mathf.FloorToInt((height - 2.4f) / 4.6f));
        int lobbyColumns = Mathf.Max(3, columns);
        for (int column = 0; column < lobbyColumns; column++)
        {
            float wx = x - width * .5f + (column + .5f) * width / lobbyColumns;
            CampusCube("QTE City shopfront mullion", new Vector3(wx, 1.35f, z - depth * .5f - .2f), new Vector3(.09f, 2.15f, .08f), trim);
            CampusCube("QTE City shopfront glass", new Vector3(wx, 1.35f, z - depth * .5f - .15f), new Vector3(width / lobbyColumns * .74f, 1.72f, .05f), shopGlass);
        }
        for (int row = 0; row < rows; row++)
        {
            float y = 2.3f + row * ((height - 3.4f) / rows);
            for (int column = 0; column < columns; column++)
            {
                float wx = x - width * .5f + (column + .5f) * width / columns;
                float windowWidth = width / columns;
                CampusCube("QTE City window frame", new Vector3(wx, y, z - depth * .5f - .13f), new Vector3(windowWidth * .72f, 1.68f, .13f), trim);
                var glassMat = (row * 3 + column + seed) % 7 == 0 ? highlight : glass;
                CampusCube("QTE City facade window", new Vector3(wx, y, z - depth * .5f - .22f), new Vector3(windowWidth * .58f, 1.38f, .06f), glassMat);
                CampusCube("QTE City window mullion", new Vector3(wx, y, z - depth * .5f - .27f), new Vector3(.075f, 1.34f, .045f), trim);
                CampusCube("QTE City window sill", new Vector3(wx, y - .79f, z - depth * .5f - .27f), new Vector3(windowWidth * .76f, .11f, .1f), trim);
                if (seed % 3 == 0 && row == 1 && column % 2 == 0)
                {
                    CampusCube("QTE City balcony slab", new Vector3(wx, y - .84f, z - depth * .5f - .62f), new Vector3(windowWidth * .84f, .12f, .72f), trim);
                    CampusCube("QTE City balcony rail", new Vector3(wx, y - .36f, z - depth * .5f - .96f), new Vector3(windowWidth * .82f, .62f, .06f), highlight);
                }
            }
        }
        for (int row = 1; row < rows; row++)
            CampusCube("QTE City floor band", new Vector3(x, 1.6f + row * ((height - 3.4f) / rows), z - depth * .5f - .13f), new Vector3(width, .11f, .14f), trim);
    }

    private static void CampusStreetLight(float x, float z)
    {
        var dark = CampusMaterial("Campus streetlight graphite", new Color(.18f, .22f, .23f));
        var lamp = CampusMaterial("Campus streetlight warm glass", new Color(1f, .78f, .39f));
        CampusCylinder("QTE Campus streetlight pole", new Vector3(x, 3, z), .07f, 6, dark);
        CampusCube("QTE Campus streetlight arm", new Vector3(x - Mathf.Sign(x) * .8f, 5.8f, z), new Vector3(1.6f, .08f, .08f), dark);
        CampusCube("QTE Campus streetlight head", new Vector3(x - Mathf.Sign(x) * 1.55f, 5.72f, z), new Vector3(.5f, .18f, .3f), lamp);
    }

    private static Material CampusMaterial(string name, Color color)
    {
        string path = "Assets/Scenes/QteGenerated/" + name.Replace(" ", "_") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        return material;
    }

    private static GameObject CampusCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetPositionAndRotation(position, Quaternion.identity);
        cube.transform.SetParent(campusBackdropRoot, true);
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube;
    }

    private static void CampusCylinder(string name, Vector3 position, float radius, float height, Material material)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.position = position;
        cylinder.transform.SetParent(campusBackdropRoot, true);
        cylinder.transform.localScale = new Vector3(radius * 2, height * .5f, radius * 2);
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
    }

    private static void CampusCylinderBetween(string name, Vector3 start, Vector3 end, float radius, Material material)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.position = (start + end) * .5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - start);
        cylinder.transform.localScale = new Vector3(radius * 2, Vector3.Distance(start, end) * .5f, radius * 2);
        cylinder.transform.SetParent(campusBackdropRoot, true);
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
    }

    private static void CampusSphere(string name, Vector3 position, Vector3 scale, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = scale;
        sphere.transform.SetParent(campusBackdropRoot, true);
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
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
