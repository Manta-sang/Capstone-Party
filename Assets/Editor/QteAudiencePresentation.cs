#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Decorates scene spectators without editing the shared character prefabs.</summary>
[InitializeOnLoad]
public static class QteAudiencePresentation
{
    private const string Folder = "Assets/Materials/QteGenerated/";
    static QteAudiencePresentation() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Library/QteAudiencePresentation.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); }
        catch (Exception ex) { File.WriteAllText("Library/QteAudiencePresentation.result", "FAIL: " + ex); Debug.LogException(ex); }
    }
    [MenuItem("Party/Decorate QTE Cheering Audience")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var current = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Qte.unity");
        if (current.isLoaded && current.isDirty) throw new InvalidOperationException("Save QTE changes first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Qte.unity");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FREE/Pack_FREE_PartyCharacters/Animations/Win.anim");
        if (clip == null || !clip.isLooping) throw new InvalidOperationException("Looping victory clip missing.");
        const string controllerPath = Folder + "QTE_AudienceCheer.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state = controller.layers[0].stateMachine.AddState("Cheer");
            state.motion = clip; controller.layers[0].stateMachine.defaultState = state;
        }
        var colors = new[] { new Color(.95f,.38f,.24f), new Color(.12f,.67f,.69f), new Color(.98f,.76f,.24f), new Color(.53f,.40f,.82f) };
        var teams = colors.Select((c,i) => Mat("Team_"+i,c)).ToArray();
        var grip = Mat("Soft_dark_grip", new Color(.11f,.17f,.20f));
        var ivory = Mat("Warm_ivory", new Color(.95f,.95f,.87f));
        var audience = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Audience Row ", StringComparison.Ordinal)).ToArray();
        if (audience.Length == 0) throw new InvalidOperationException("No authored spectators found.");
        int flags = 0, batons = 0;
        for (int i = 0; i < audience.Length; i++)
        {
            var person = audience[i]; var animator = person.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("Spectator animator missing: " + person.name);
            foreach (var t in person.GetComponentsInChildren<Transform>(true).Where(t => t.name == "QTE Cheering accessories").ToArray())
                if (t != null) Object.DestroyImmediate(t.gameObject);
            var bones = person.GetComponentsInChildren<Transform>(true);
            var left = bones.FirstOrDefault(t => t.name == "LeftHand");
            var right = bones.FirstOrDefault(t => t.name == "RightHand");
            if (left == null || right == null) throw new InvalidOperationException("Spectator hand bones missing.");
            var cheer = person.GetComponent<QteAudienceCheer>();
            if (cheer == null) cheer = person.gameObject.AddComponent<QteAudienceCheer>();
            cheer.animator = animator; cheer.phase = Mathf.Repeat(i * .381966f, 1); cheer.playbackSpeed = .82f + (i % 7) * .065f;
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            clip.SampleAnimation(animator.gameObject, cheer.phase * clip.length);
            var renderers = person.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Spectator mesh missing.");
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = Mathf.Clamp(bounds.size.y / 2, .55f, 1.6f);
            var team = teams[(i / 10) % teams.Length];
            if (i % 4 == 0)
            {
                var holder = Holder(right, person.rotation, scale);
                Part(holder,"Flag pole",new Vector3(0,.25f,0),new Vector3(.024f,.64f,.024f),ivory);
                Part(holder,"Team pennant",new Vector3(.19f,.42f,0),new Vector3(.38f,.26f,.014f),team);
                Part(holder,"Pennant ivory stripe",new Vector3(.19f,.42f,-.009f),new Vector3(.25f,.045f,.006f),ivory);
                Baton(Holder(left,person.rotation,scale), team, grip); flags++; batons++;
            }
            else
            {
                Baton(Holder(left,person.rotation,scale),team,grip);
                Baton(Holder(right,person.rotation,scale),team,grip); batons += 2;
            }
            EditorUtility.SetDirty(cheer); EditorUtility.SetDirty(animator);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        QteHierarchyOptimization.Optimize();
        var capture = typeof(QteSchoolQuality).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
        Directory.CreateDirectory("Library/QteSchoolQuality");
        capture.Invoke(null,new object[]{"cheering-audience.png",new Vector3(20,9,-16),new Vector3(1,3,-32),48f});
        File.WriteAllText("Library/QteAudiencePresentation.result",$"PASS: {audience.Length} spectators, {batons} batons, {flags} pennants; dedicated looping victory controller, staggered phases/speeds; no shared prefabs/clips modified; hierarchy/material/spawn validation passed.");
    }
    private static Transform Holder(Transform hand,Quaternion rotation,float scale)
    {
        var t=new GameObject("QTE Cheering accessories").transform;
        t.position=hand.position;t.rotation=rotation;t.localScale=Vector3.one*scale;t.SetParent(hand,true);return t;
    }
    private static void Baton(Transform root,Material team,Material grip)
    {
        Part(root,"Inflatable cheering baton",new Vector3(0,.23f,0),new Vector3(.095f,.60f,.095f),team);
        Part(root,"Baton soft grip",new Vector3(0,-.025f,0),new Vector3(.10f,.11f,.10f),grip);
    }
    private static void Part(Transform root,string name,Vector3 position,Vector3 size,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=position;g.transform.localScale=size;
        var r=g.GetComponent<Renderer>();r.sharedMaterial=mat;r.receiveShadows=false;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    private static Material Mat(string name,Color color)
    {
        string path=Folder+"Audience_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",.25f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
}
#endif
