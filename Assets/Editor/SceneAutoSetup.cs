using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;

[InitializeOnLoad]
public static class SceneAutoSetup
{
    const string PrefKey = "SceneAutoSetup_Done";

    static SceneAutoSetup()
    {
        if (!EditorPrefs.GetBool(PrefKey, false))
            EditorApplication.delayCall += SafeRun;
    }

    [MenuItem("Tools/Re-run Scene Setup")]
    static void ForceRun()
    {
        EditorPrefs.DeleteKey(PrefKey);
        SafeRun();
    }

    // Play-mode guard: re-queue if the editor is mid play-mode transition.
    // Prevents EditorSceneManager.NewScene from throwing InvalidOperationException.
    static void SafeRun()
    {
        EditorApplication.delayCall -= SafeRun;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += SafeRun;
            return;
        }
        Run();
    }

    static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);

        // Player — prefer the bake-off Pipeline A succubus, then legacy paths, then cylinder.
        const string succubusFbx = "Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx";
        const string succubusCtrl = "Assets/Animations/Succubus/SuccubusAnimator.controller";
        const string placeholderFbx = "Assets/Characters/Placeholder/YBot.fbx";
        const string placeholderCtrl = "Assets/Animations/Placeholder/PlaceholderAnimator.controller";

        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;
        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(succubusFbx);
        string ctrlPath = succubusCtrl;
        string modelName = "SuccubusModel";

        if (fbxAsset == null)
        {
            fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(placeholderFbx);
            ctrlPath = placeholderCtrl;
            modelName = "YBotModel";
        }

        if (fbxAsset != null)
        {
            var model = Object.Instantiate(fbxAsset, player.transform);
            model.name = modelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var cap = player.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.3f;
            cap.center = new Vector3(0f, 0.9f, 0f);

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl != null)
            {
                var anim = model.GetComponentInChildren<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
            }

            var pc = player.AddComponent<PlayerController>();
            pc.groundCheckDistance = 0.15f;
        }
        else
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "PlaceholderCylinder";
            cyl.transform.SetParent(player.transform);
            cyl.transform.localPosition = new Vector3(0f, 1f, 0f);
            var pc = player.AddComponent<PlayerController>();
            pc.groundCheckDistance = 1.1f;
            Debug.Log("[SceneAutoSetup] No character FBX found — using cylinder placeholder.");
        }

        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 1.5f, -4f);
        camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        var cc = camGO.AddComponent<CameraController>();
        cc.target = player.transform;

        const string scenePath = "Assets/Scenes/GameScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        AssetDatabase.Refresh();
        EditorPrefs.SetBool(PrefKey, true);

        Debug.Log("[SceneAutoSetup] GameScene.unity created and added to Build Settings.");
    }
}
