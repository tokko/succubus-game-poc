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

    // Slot definitions in display order, left-to-right. Add a third entry when
    // Pipeline C lands — keeping the empty placeholder around is more confusing
    // than useful.
    static readonly Slot[] Slots = new[]
    {
        new Slot { Id = "A", Label = "HY3D-2.1\n(single view)", FbxPath = "Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx", MaterialPath = "Assets/Characters/Bakeoff/A_HY3D21/SuccubusA.mat", AnimatorPath = "Assets/Animations/Bakeoff/SuccubusA.controller" },
        new Slot { Id = "B", Label = "TRELLIS\n(WSL)",          FbxPath = "Assets/Characters/Bakeoff/B_TRELLIS/succubus_b.fbx", MaterialPath = "Assets/Characters/Bakeoff/B_TRELLIS/SuccubusB.mat", AnimatorPath = "Assets/Animations/Bakeoff/SuccubusB.controller" },
        new Slot { Id = "C", Label = "HY3D-2mv\n(4 views)",     FbxPath = "Assets/Characters/Bakeoff/C_HY3DMV/succubus_c.fbx", MaterialPath = "Assets/Characters/Bakeoff/C_HY3DMV/SuccubusC.mat", AnimatorPath = "Assets/Animations/Bakeoff/SuccubusC.controller" },
    };

    const float SLOT_SPACING   = 3.0f;   // metres between slot centres
    const float GROUND_SCALE   = 20f;
    const float TARGET_HEIGHT  = 1.8f;   // each slot's character is scaled so its world-space
                                          // bounds height = this many metres. Fair comparison
                                          // regardless of what each AI tool's normalization is.

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

        // Camera — InspectorCamera fly-cam, framed on all slots at start
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 2.0f, -6.5f);
        camGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<InspectorCamera>();

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

            // Animator (always plays Idle in the bake-off scene). Build the controller
            // here if it doesn't exist yet — don't rely on SetupBakeoffA/B having run
            // first, since they're gated on EditorPrefs and may not have re-fired.
            AnimatorController ctrl = null;
            if (!string.IsNullOrEmpty(slot.AnimatorPath))
            {
                ctrl = EnsureControllerForFbx(slot.FbxPath, slot.AnimatorPath);
            }
            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            // Normalize size so each pipeline's character renders at TARGET_HEIGHT metres,
            // regardless of what scale the AI tool's mesh originally has. Measure the union
            // of all SkinnedMeshRenderer bounds, compute the scale ratio, apply uniformly.
            NormalizeHeight(model, slot.Id);
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

    /// <summary>
    /// Force the FBX to import as Generic + CreateFromThisModel avatar. Without an
    /// avatar, the AnimationClips' bone curves can't bind to the SkinnedMeshRenderer's
    /// skeleton at runtime, and the model T-poses. SetupBakeoffA/B Step 1 originally
    /// set NoAvatar — that's wrong; fixed here at scene-build time.
    /// </summary>
    static void EnsureGenericAvatar(string fbxPath)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) return;
        bool dirty = false;
        if (imp.animationType != ModelImporterAnimationType.Generic)
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            dirty = true;
        }
        if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            dirty = true;
        }
        if (imp.importAnimation != true)
        {
            imp.importAnimation = true;
            dirty = true;
        }
        if (dirty)
        {
            imp.SaveAndReimport();
            Debug.Log($"[SetupBakeoffScene] {fbxPath}: reimported as Generic + CreateFromThisModel.");
        }
    }

    /// <summary>
    /// Build or rebuild an Idle/Walk/Run AnimatorController that pulls clips out of an
    /// FBX. Handles Blender's "Succubus_Rig|Idle" / "Succubus_Rig|Succubus_Rig|Idle"
    /// naming by matching the last `|`-delimited segment. Always recreates so the same
    /// path doesn't keep an old null-motion controller around.
    /// </summary>
    static AnimatorController EnsureControllerForFbx(string fbxPath, string ctrlPath)
    {
        if (!File.Exists(Path.GetFullPath(fbxPath))) return null;
        EnsureGenericAvatar(fbxPath);

        // Ensure target folder exists
        var dir = Path.GetDirectoryName(ctrlPath);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(dir), Path.GetFileName(dir));

        // Recreate so clip-name fix from earlier always propagates
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);

        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__"))
            .ToArray();
        if (clips.Length == 0)
        {
            Debug.LogWarning($"[SetupBakeoffScene] No animation clips found in {fbxPath} — Animator will T-pose.");
            return null;
        }

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");
        var walk = sm.AddState("Walk");
        var run  = sm.AddState("Run");
        sm.defaultState = idle;

        foreach (var c in clips)
        {
            int pipe = c.name.LastIndexOf('|');
            string suf = pipe >= 0 ? c.name.Substring(pipe + 1) : c.name;
            switch (suf)
            {
                case "Idle": idle.motion = c; break;
                case "Walk": walk.motion = c; break;
                case "Run":  run.motion  = c; break;
            }
        }

        void AddTrans(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float th)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, th, "speed");
            t.duration = (from == idle || to == idle) ? 0.25f : 0.1f;
            t.hasExitTime = false;
        }
        AddTrans(idle, walk, AnimatorConditionMode.Greater, 0.1f);
        AddTrans(walk, idle, AnimatorConditionMode.Less,    0.1f);
        AddTrans(walk, run,  AnimatorConditionMode.Greater, 4.5f);
        AddTrans(run,  walk, AnimatorConditionMode.Less,    4.5f);

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupBakeoffScene] Built controller {ctrlPath} from {clips.Length} clips ({string.Join(",", clips.Select(c => c.name))}).");
        return ctrl;
    }

    /// <summary>
    /// Rescale the model so the world-space height of its renderer bounds equals
    /// TARGET_HEIGHT. After scaling, push its localPosition down by the minimum-Y of the
    /// scaled bounds so feet end at slot y=0.
    /// </summary>
    static void NormalizeHeight(GameObject model, string slotId)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // Union of renderer bounds in world space (must be after the model is parented and
        // its initial transform applied — Unity computes bounds in world coords).
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        float currentHeight = b.size.y;
        if (currentHeight <= 0.001f)
        {
            Debug.LogWarning($"[SetupBakeoffScene] Slot {slotId}: degenerate bounds, skipping normalize.");
            return;
        }

        float scale = TARGET_HEIGHT / currentHeight;
        model.transform.localScale *= scale;

        // Bounds invalidate after scale; recompute and snap feet to y=0 relative to slot root.
        Bounds b2 = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
        float feetY = b2.min.y;
        Vector3 slotWorld = model.transform.parent != null ? model.transform.parent.position : Vector3.zero;
        Vector3 lp = model.transform.localPosition;
        lp.y += slotWorld.y - feetY;
        model.transform.localPosition = lp;

        Debug.Log($"[SetupBakeoffScene] Slot {slotId}: normalized to {TARGET_HEIGHT:F2}m " +
                  $"(was {currentHeight:F2}m, scale x{scale:F3}).");
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
