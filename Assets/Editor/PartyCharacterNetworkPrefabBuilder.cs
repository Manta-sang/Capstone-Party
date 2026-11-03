using System;
using System.Collections.Generic;
using System.Linq;
using CrosswalkRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates network-ready wrappers for two additional Party Characters and assigns them to every player spawner.</summary>
public static class PartyCharacterNetworkPrefabBuilder
{
    private const string OutputFolder = "Assets/Prefabs/PartyCharacters";
    private const string NetworkPrefabListPath = "Assets/DefaultNetworkPrefabs.asset";
    private static readonly string[] SourcePaths =
    {
        "Assets/FREE/Pack_FREE_PartyCharacters/Prefabs/Character_0413da.prefab",
        "Assets/FREE/Pack_FREE_PartyCharacters/Prefabs/Character_6dd688.prefab"
    };
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Character.unity",
        "Assets/Scenes/CrosswalkRun.unity",
        "Assets/Scenes/SawBladeDodge.unity",
        "Assets/Scenes/TileFlip.unity",
        "Assets/Scenes/Qte.unity",
        "Assets/Scenes/BlockFall.unity"
    };

    [MenuItem("Tools/Party/Build Network Character Prefabs")]
    public static void Build()
    {
        EnsureFolder(OutputFolder);
        var generated = SourcePaths.Select(CreateNetworkPrefab).ToArray();
        RegisterPrefabs(generated);
        UpdateSpawnerScenes(generated);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PARTY_CHARACTER_NETWORK_PREFABS_READY: two additional character models were wrapped, registered, and assigned to all six player scenes.");
    }

    private static GameObject CreateNetworkPrefab(string sourcePath)
    {
        string sourceName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string outputPath = $"{OutputFolder}/{sourceName}_Network.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            contents.name = sourceName + "_Network";
            contents.tag = "Player";
            var animator = contents.GetComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException($"{sourcePath} has no root Animator.");

            if (contents.GetComponent<NetworkObject>() == null)
                contents.AddComponent<NetworkObject>();

            var networkTransform = contents.GetComponent<ClientNetworkTransform>();
            if (networkTransform == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/party_character.prefab");
                var templateTransform = template.GetComponent<ClientNetworkTransform>();
                networkTransform = contents.AddComponent<ClientNetworkTransform>();
                EditorUtility.CopySerialized(templateTransform, networkTransform);
            }
            networkTransform.enabled = true;

            if (contents.GetComponent<PlayerMovement>() == null)
            {
                var movement = contents.AddComponent<PlayerMovement>();
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/party_character.prefab");
                EditorUtility.CopySerialized(template.GetComponent<PlayerMovement>(), movement);
            }

            if (contents.GetComponent<PlayerInteraction>() == null)
            {
                var interaction = contents.AddComponent<PlayerInteraction>();
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/party_character.prefab");
                EditorUtility.CopySerialized(template.GetComponent<PlayerInteraction>(), interaction);
            }

            var networkAnimator = contents.GetComponent<ClientNetworkAnimator>();
            if (networkAnimator == null)
            {
                networkAnimator = contents.AddComponent<ClientNetworkAnimator>();
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/party_character.prefab");
                EditorUtility.CopySerialized(template.GetComponent<ClientNetworkAnimator>(), networkAnimator);
            }
            networkAnimator.Animator = animator;

            var body = contents.GetComponent<Rigidbody>();
            if (body == null) body = contents.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var capsule = contents.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = contents.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.52f;
            capsule.height = 2.36f;
            capsule.center = new Vector3(0f, 1.16f, 0f);

            var saved = PrefabUtility.SaveAsPrefabAsset(contents, outputPath);
            if (saved == null) throw new InvalidOperationException($"Could not create {outputPath}.");
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void RegisterPrefabs(IEnumerable<GameObject> prefabs)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabListPath);
        if (list == null) throw new InvalidOperationException($"Could not load {NetworkPrefabListPath}.");
        foreach (var prefab in prefabs)
        {
            if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
        }
        EditorUtility.SetDirty(list);
    }

    private static void UpdateSpawnerScenes(GameObject[] generated)
    {
        var allPrefabs = new[]
        {
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/party_character.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Another_player.prefab"),
            generated[0],
            generated[1]
        };

        foreach (string scenePath in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool updated = false;
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (component == null || component.gameObject.scene != scene) continue;
                var serialized = new SerializedObject(component);
                var array = serialized.FindProperty("characterPrefabs") ?? serialized.FindProperty("characters");
                if (array == null || !array.isArray) continue;
                array.arraySize = allPrefabs.Length;
                for (int i = 0; i < allPrefabs.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = allPrefabs[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                updated = true;
            }
            if (!updated) throw new InvalidOperationException($"No player prefab array found in {scenePath}.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
