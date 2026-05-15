using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds Assets/Scenes/Bakeoff.unity — a static comparison scene with up to three
/// character slots (A, B, C) side-by-side, sharing one animator clip and the gothic
/// volume profile from Phase 4. Slots without a corresponding FBX get a label-only
/// placeholder ("B: TRELLIS — pending upload") so the layout is stable as you fill
/// the bake-off in.
///
/// Re-trigger via Tools > Build Bake-off Scene. Does NOT auto-run.
/// </summary>
public static class SetupBakeoffScene
{
    const string SCENE_PATH = "Assets/Scenes/Bakeoff.unity";

    // Slot definitions in display order, left-to-right.
    static readonly Slot[] Slots = new[]
    {
        new Slot { Id = "A", Label = "HY3D-2.1\n(local)",   FbxPath = "Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx",  MaterialPath = "Assets/Characters/Bakeoff/A_HY3D21/SuccubusA.mat", AnimatorPath = "Assets/Animations/Bakeoff/SuccubusA.controller" },
        new Slot { Id = "B", Label = "TRELLIS.2\n(WSL/HF)", FbxPath = "Assets/Characters/Bakeoff/B_TRELLIS/succubus_b.fbx",  MaterialPath = "Assets/Characters/Bakeoff/B_TRELLIS/SuccubusB.mat", AnimatorPath = "Assets/Animations/Bakeoff/SuccubusB.controller" },
        new Slot { Id = "C", Label = "(slot C reserved)",   FbxPath = "Assets/Characters/Bakeoff/C/succubus_c.fbx",          MaterialPath = "",                                                AnimatorPath = "" },
    };

    const float SLOT_SPACING = 3.0f;   // metres between slot centres
    const float GROUND_SCALE = 20f;

    [MenuItem("Tools/Build Bake-off Scene")]
    public static void Build()
    {
        // Create fresh scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(GROUND_SCALE, 1f, GROUND_SCALE);
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var groundMat = new Material(litShader);
            groundMat.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.13f, 1f));
            groundMat.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
        }

        // Directional light — same warm-sunset as Phase 4
        var dirGo = new GameObject("Directional Light");
        var dir = dirGo.AddComponent<Light>();
        dir.type = LightType.Directional;
        dir.color = new Color(1.0f, 0.62f, 0.4f, 1f);
        dir.intensity = 0.6f;
        dir.shadows = LightShadows.Soft;
        dirGo.transform.rotation = Quaternion.Euler(20f, -40f, 0f);

        // Global Volume — reuse the gothic profile from Phase 4
        var volumeGo = new GameObject("Global Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/GothicVolumeProfile.asset");

        // Render settings (skybox + fog)
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/GothicSkybox.mat");
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.10f, 0.06f, 0.14f, 1f);
        RenderSettings.fogDensity = 0.02f;

        // Slots
        float halfSpan = SLOT_SPACING * (Slots.Length - 1) * 0.5f;
        for (int i = 0; i < Slots.Length; i++)
        {
            float x = i * SLOT_SPACING - halfSpan;
            BuildSlot(Slots[i], new Vector3(x, 0f, 0f));
        }

        // Camera — frames all slots
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 2.0f, -6.5f);
        camGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();

        // Save
        Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();
        Debug.Log($"[SetupBakeoffScene] Saved {SCENE_PATH} with {Slots.Length} slots ({Slots.Count(s => File.Exists(Path.GetFullPath(s.FbxPath)))} populated).");
    }

    static void BuildSlot(Slot slot, Vector3 position)
    {
        var root = new GameObject($"Slot_{slot.Id}");
        root.transform.position = position;

        var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(slot.FbxPath);
        if (fbxAsset != null)
        {
            var model = Object.Instantiate(fbxAsset, root.transform);
            model.name = $"Model_{slot.Id}";
            model.transform.localPosition = Vector3.zero;

            // Material
            var mat = string.IsNullOrEmpty(slot.MaterialPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(slot.MaterialPath);
            if (mat != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }

            // Animator (always plays Idle in the bake-off scene)
            var ctrl = string.IsNullOrEmpty(slot.AnimatorPath) ? null : AssetDatabase.LoadAssetAtPath<AnimatorController>(slot.AnimatorPath);
            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
        }
        else
        {
            // Placeholder cylinder so the slot is visible
            var ph = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ph.name = $"Placeholder_{slot.Id}";
            ph.transform.SetParent(root.transform);
            ph.transform.localPosition = new Vector3(0f, 1f, 0f);
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var phMat = new Material(litShader);
                phMat.SetColor("_BaseColor", new Color(0.2f, 0.15f, 0.25f, 1f));
                phMat.SetFloat("_Smoothness", 0.1f);
                ph.GetComponent<MeshRenderer>().sharedMaterial = phMat;
            }
        }

        // Label (TextMesh — legacy 3D text, no TMP dependency)
        var labelGo = new GameObject($"Label_{slot.Id}");
        labelGo.transform.SetParent(root.transform);
        labelGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        labelGo.transform.localRotation = Quaternion.identity;
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = $"{slot.Id}: {slot.Label}";
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.characterSize = 0.1f;
        tm.fontSize = 64;
        tm.color = new Color(0.9f, 0.85f, 1f, 1f);

        // Face the camera (camera is at +Z=-6.5, looking +Z toward origin)
        labelGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    class Slot
    {
        public string Id;
        public string Label;
        public string FbxPath;
        public string MaterialPath;
        public string AnimatorPath;
    }
}
