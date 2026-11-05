#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
namespace BlockFall.Editor {
public static class BlockFallBuilder {
    static Material Mat(string name,Color color) {
        var m=new Material(Shader.Find("Standard")); m.color=color;
        AssetDatabase.CreateAsset(m,"Assets/Materials/BlockFall/"+name+".mat"); return m;
    }
    static GameObject Cube(string name,Vector3 p,Vector3 size,Material mat,Transform parent=null) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent);
        g.transform.position=p; g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=mat; return g;
    }
    public static void Build() {
        System.IO.Directory.CreateDirectory("Assets/Materials/BlockFall");
        System.IO.Directory.CreateDirectory("Assets/Prefabs/BlockFall");
        var floor=Mat("Floor",new Color(.38f,.43f,.46f)); var wall=Mat("Wall",new Color(.22f,.26f,.3f));
        var danger=Mat("Danger",new Color(1,.22f,.04f));
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var tiles=new GameObject("Arena Tiles").transform;
        for(int z=0;z<5;z++) for(int x=0;x<5;x++) {
            var t=Cube("Tile_"+z+"_"+x,new Vector3((x-2)*4,-.25f,(z-2)*4),new Vector3(4,.5f,4),floor,tiles);
            t.tag="Ground";
        }
        Cube("Endless Background",new Vector3(0,-.65f,0),new Vector3(400,.3f,400),floor);
        for(int i=0;i<4;i++) {
            bool side=i<2; float sign=i%2==0?-1:1;
            var g=Cube("Boundary "+i,side?new Vector3(sign*10.3f,.5f,0):new Vector3(0,.5f,sign*10.3f),
                side?new Vector3(.6f,1,21.2f):new Vector3(21.2f,1,.6f),wall);
            var c=g.GetComponent<BoxCollider>(); c.size=new Vector3(1,8,1); c.center=new Vector3(0,3,0);
        }
        var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,23,-17); camera.transform.LookAt(Vector3.zero); camera.orthographic=true; camera.orthographicSize=15;
        camera.gameObject.AddComponent<AudioListener>(); camera.backgroundColor=new Color(.45f,.5f,.55f);
        var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.2f; light.transform.rotation=Quaternion.Euler(50,-30,0);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogStartDistance=35; RenderSettings.fogEndDistance=100; RenderSettings.fogColor=camera.backgroundColor;
        var manager=new GameObject("BlockFall Manager"); manager.AddComponent<NetworkObject>();
        var game=manager.AddComponent<BlockFallGame>(); game.tiles=tiles; game.starts=new Transform[4];
        for(int i=0;i<4;i++) { var s=new GameObject("Spawn "+(i+1)); s.transform.position=new Vector3(i%2==0?-7:7,1,i<2?-7:7); game.starts[i]=s.transform; }
        // Resolve the four registered players by GUID so future folder moves keep working.
        game.characters=new[] {"b3f543bae91f9b94598a8372bfe747f8","0c76b793a5061c942beb17b723fa0688","43f35dc74b8af374c8ba48ec4ee160e0","07562273415467b4ca8e2b02136ecec3"}
            .Select(guid=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        if(game.characters.Any(prefab=>prefab==null))throw new System.InvalidOperationException("All four player prefabs must be imported before building BlockFall.");
        var visual=Cube("Block Visual",Vector3.zero,Vector3.one,danger); Object.DestroyImmediate(visual.GetComponent<Collider>());
        game.blockVisual=PrefabUtility.SaveAsPrefabAsset(visual,"Assets/Prefabs/BlockFall/BlockVisual.prefab"); Object.DestroyImmediate(visual);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/BlockFall.unity");
        var lobby=EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");
        new GameObject("BlockFall Entry").AddComponent<BlockFallLobby>();
        EditorSceneManager.SaveScene(lobby);
        var list=EditorBuildSettings.scenes.ToList(); if(!list.Any(s=>s.path=="Assets/Scenes/BlockFall.unity")) list.Add(new EditorBuildSettingsScene("Assets/Scenes/BlockFall.unity",true));
        EditorBuildSettings.scenes=list.ToArray(); AssetDatabase.SaveAssets();
    }
}}
#endif


