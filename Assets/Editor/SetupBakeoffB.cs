using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot setup for the Hunyuan3D PBR succubus:
///   - Humanoid import settings on succubus_rig_pbr.fbx
///   - Packs metallic (R) + inverted-roughness (A) into a combined texture
///   - Creates SuccubusPBR.mat (URP Lit) with all maps assigned
///   - Creates SuccubusAnimatorPBR.controller (Idle / Walk / Run)
///   - Swaps the scene Player to use the PBR model
///
/// Runs automatically once on domain reload; re-run via Tools > Setup Succubus PBR.
/// </summary>
[InitializeOnLoad]
public static class SetupBakeoffB
{
    // ── Paths ─────────────────────────────────────────────────────────────────
    const string BASE        = "Assets/Characters/Bakeoff/B_TRELLIS";
    const string FBX         = BASE + "/succubus_b.fbx";
    const string ALBEDO      = BASE + "/succubus_b_albedo.jpg";   // extracted from succubus_b.glb baseColorTexture
    const string METALLIC    = BASE + "/succubus_b_metallic.jpg"; // TRELLIS doesn't produce these; skipped
    const string ROUGHNESS   = BASE + "/succubus_b_roughness.jpg";
    const string PACKED      = BASE + "/succubus_metalSmooth.png";
    const string MAT         = BASE + "/SuccubusB.mat";
    const string CTRL        = "Assets/Animations/Bakeoff/SuccubusB.controller";
    const string SCENE_PATH  = "Assets/Scenes/GameScene.unity";
    const string PREF_KEY    = "SetupBakeoffB_Done_v1";

    // ── Auto-run once ─────────────────────────────────────────────────────────
    static SetupBakeoffB()
    {
        if (EditorPrefs.GetBool(PREF_KEY, false)) return;
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Setup Bake-off B (TRELLIS.2)")]
    public static void ForceRun()
    {
        EditorPrefs.DeleteKey(PREF_KEY);
        Run();
    }

    // ── Main entry point ──────────────────────────────────────────────────────
    static void Run()
    {
        EditorApplication.delayCall -= Run;

        if (!File.Exists(Path.GetFullPath(FBX)))
        {
            Debug.LogError($"[SetupSuccubusPBR] FBX not found: {FBX}");
            return;
        }

        AssetDatabase.Refresh();

        Step1_ConfigureHumanoid();
        Step2_PackTextures();
        Step3_CreateMaterial();
        Step4_CreateAnimator();
        Step5_UpdateScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetBool(PREF_KEY, true);
        Debug.Log("[SetupSuccubusPBR] All steps complete.");
    }

    // ── Step 1: Generic rig import settings ──────────────────────────────────
    // Rigify bones don't match Unity humanoid names, so Generic is used.
    // Clips are imported fine with Generic and the animator still drives all
    // animations; we just lose humanoid retargeting (acceptable for a POC).
    static void Step1_ConfigureHumanoid()
    {
        var imp = AssetImporter.GetAtPath(FBX) as ModelImporter;
        if (imp == null) { Debug.LogWarning($"[SetupSuccubusPBR] ModelImporter not found for {FBX}"); return; }

        bool dirty = false;
        if (imp.animationType != ModelImporterAnimationType.Generic)
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup   = ModelImporterAvatarSetup.NoAvatar;
            dirty = true;
        }
        if (imp.importNormals != ModelImporterNormals.Import)
        {
            imp.importNormals = ModelImporterNormals.Import;
            dirty = true;
        }

        if (dirty) imp.SaveAndReimport();
        Debug.Log("[SetupSuccubusPBR] Step 1: Generic rig import settings OK.");
    }

    // ── Step 2: Pack metallic(R) + smoothness(A=1-roughness) ─────────────────
    static void Step2_PackTextures()
    {
        // Force max resolution + BC7 high-quality compression on all source maps
        EnsureHighQuality(ALBEDO,    sRGB: true);
        EnsureHighQuality(METALLIC,  sRGB: false);
        EnsureHighQuality(ROUGHNESS, sRGB: false);

        // Make source textures readable temporarily
        EnsureReadable(METALLIC, true);
        EnsureReadable(ROUGHNESS, true);

        var metalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(METALLIC);
        var roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ROUGHNESS);

        if (metalTex == null || roughTex == null)
        {
            Debug.LogWarning("[SetupSuccubusPBR] Step 2: metallic or roughness texture missing — skipping pack.");
            return;
        }

        int w = metalTex.width;
        int h = metalTex.height;

        // Sample roughness at metallic resolution
        var metalPx = metalTex.GetPixels();
        // GetPixels on a potentially-different-size roughTex via resize sample
        var roughResized = new Texture2D(w, h, TextureFormat.RGBA32, false);
        roughResized.SetPixels(
            Enumerable.Range(0, w * h).Select(i =>
            {
                float u = (i % w + 0.5f) / w;
                float v = (i / w + 0.5f) / h;
                return roughTex.GetPixelBilinear(u, v);
            }).ToArray()
        );

        var packed = new Color[w * h];
        for (int i = 0; i < packed.Length; i++)
        {
            float metallic   = metalPx[i].r;
            float roughness  = roughResized.GetPixel(i % w, i / w).r;
            float smoothness = 1f - roughness;          // URP: A = smoothness
            packed[i] = new Color(metallic, 0f, 0f, smoothness);
        }

        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels(packed);
        outTex.Apply();

        File.WriteAllBytes(Path.GetFullPath(PACKED), outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        Object.DestroyImmediate(roughResized);

        AssetDatabase.ImportAsset(PACKED);

        // Linear space for metallic/smoothness map (not sRGB)
        var packedImp = AssetImporter.GetAtPath(PACKED) as TextureImporter;
        if (packedImp != null)
        {
            packedImp.sRGBTexture = false;
            packedImp.SaveAndReimport();
        }

        // Restore source textures to non-readable (saves memory at runtime)
        EnsureReadable(METALLIC, false);
        EnsureReadable(ROUGHNESS, false);

        Debug.Log($"[SetupSuccubusPBR] Step 2: Packed metallic+smoothness -> {PACKED}  ({w}x{h})");
    }

    // ── Step 3: URP Lit material ──────────────────────────────────────────────
    static void Step3_CreateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[SetupSuccubusPBR] Step 3: URP Lit shader not found. Is URP installed?");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MAT);
        }
        else
        {
            mat.shader = shader;
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ALBEDO);
        if (albedo != null) mat.SetTexture("_BaseMap", albedo);

        // The Hunyuan3D-2.1 paint pipeline produces a metallic map with ~0.5 mid-grey
        // smeared across all skin areas, which makes skin render as wet/metallic in URP.
        // Ignore the AI's metallic+smoothness map and use uniform matte values instead.
        mat.SetTexture("_MetallicGlossMap", null);
        mat.SetFloat("_Metallic",   0.0f);  // skin is non-metallic
        mat.SetFloat("_Smoothness", 0.35f); // slightly soft, not glossy

        // Ensure double-sided (succubus mesh may have thin geometry)
        mat.doubleSidedGI = true;
        mat.SetFloat("_Cull", 0f); // CullMode.Off

        EditorUtility.SetDirty(mat);
        Debug.Log($"[SetupSuccubusPBR] Step 3: Material created -> {MAT}");
    }

    // ── Step 4: AnimatorController (Idle / Walk / Run) ────────────────────────
    static void Step4_CreateAnimator()
    {
        // Always recreate. The clip-name match logic has changed and any pre-existing
        // controller from a previous Run may have null motions on its states.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL) != null)
        {
            AssetDatabase.DeleteAsset(CTRL);
            Debug.Log("[SetupBakeoffB] Step 4: deleted stale controller; rebuilding.");
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(FBX)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__"))
            .ToArray();

        if (clips.Length == 0)
        {
            Debug.LogWarning($"[SetupSuccubusPBR] Step 4: No clips found in {FBX}.");
            return;
        }

        var dir = Path.GetDirectoryName(CTRL);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(dir),
                Path.GetFileName(dir));

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL);
        ctrl.AddParameter("speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        var walk = sm.AddState("Walk");
        var run  = sm.AddState("Run");
        sm.defaultState = idle;

        // Blender FBX export prefixes clip names with the armature: 'Succubus_Rig|Idle'.
        // With bake_anim_use_all_actions + bake_anim_use_nla_strips both true we also get
        // 'Succubus_Rig|Succubus_Rig|Idle'. Match on the last segment only.
        foreach (var clip in clips)
        {
            int pipe = clip.name.LastIndexOf('|');
            string suffix = pipe >= 0 ? clip.name.Substring(pipe + 1) : clip.name;
            switch (suffix)
            {
                case "Idle": idle.motion = clip; break;
                case "Walk": walk.motion = clip; break;
                case "Run":  run.motion  = clip; break;
            }
        }

        void AddTrans(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float thresh)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, thresh, "speed");
            t.duration     = (from == idle || to == idle) ? 0.25f : 0.1f;
            t.hasExitTime  = false;
        }

        AddTrans(idle, walk, AnimatorConditionMode.Greater, 0.1f);
        AddTrans(walk, idle, AnimatorConditionMode.Less,    0.1f);
        AddTrans(walk, run,  AnimatorConditionMode.Greater, 4.5f);
        AddTrans(run,  walk, AnimatorConditionMode.Less,    4.5f);

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupSuccubusPBR] Step 4: AnimatorController -> {CTRL}  ({clips.Length} clips)");
    }

    // ── Step 5: Swap Player model in GameScene ────────────────────────────────
    static void Step5_UpdateScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SetupSuccubusPBR] Step 5: Skipped — Unity is in Play Mode. Exit Play Mode and re-run via Tools > Setup Succubus PBR.");
            return;
        }

        if (!File.Exists(Path.GetFullPath(SCENE_PATH)))
        {
            Debug.Log("[SetupSuccubusPBR] Step 5: GameScene.unity not found — skipping scene update.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // Find the Player GameObject
        var player = scene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "Player");
        if (player == null)
        {
            Debug.LogWarning("[SetupSuccubusPBR] Step 5: 'Player' not found in GameScene — skipping.");
            EditorSceneManager.SaveScene(scene);
            return;
        }

        // Remove old SuccubusModel child
        var oldModel = player.transform.Find("SuccubusModel");
        if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

        // Instantiate PBR model
        var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
        if (fbxAsset != null)
        {
            var model = Object.Instantiate(fbxAsset, player.transform);
            model.name = "SuccubusModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Assign PBR material to all renderers
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(MAT);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (mat != null)
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }

            // Wire Animator
            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
            }

            Debug.Log("[SetupSuccubusPBR] Step 5: PBR model placed in scene.");
        }
        else
        {
            Debug.LogWarning($"[SetupSuccubusPBR] Step 5: Could not load {FBX} as GameObject.");
        }

        // Fix Ground plane: ensure it has a simple grey URP material (not pink/missing)
        var ground = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
        if (ground != null)
        {
            var groundShader = Shader.Find("Universal Render Pipeline/Lit");
            if (groundShader != null)
            {
                var groundRend = ground.GetComponent<MeshRenderer>();
                if (groundRend != null && (groundRend.sharedMaterial == null ||
                    groundRend.sharedMaterial.shader == null ||
                    groundRend.sharedMaterial.shader.name.Contains("Hidden/InternalError")))
                {
                    var gMat = new Material(groundShader);
                    gMat.SetColor("_BaseColor", new Color(0.35f, 0.32f, 0.30f, 1f)); // dark stone
                    gMat.SetFloat("_Smoothness", 0.1f);
                    groundRend.sharedMaterial = gMat;
                    Debug.Log("[SetupSuccubusPBR] Step 5: Ground plane material fixed.");
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupSuccubusPBR] Step 5: GameScene.unity saved.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void EnsureReadable(string assetPath, bool readable)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null || ti.isReadable == readable) return;
        ti.isReadable = readable;
        ti.SaveAndReimport();
    }

    /// Force 2048px max + BC7 high-quality compression so the texture isn't downscaled or
    /// DXT1-compressed (DXT1 introduces visible blocking on skin/organic surfaces).
    static void EnsureHighQuality(string assetPath, bool sRGB)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;

        bool dirty = false;
        if (ti.maxTextureSize < 2048)          { ti.maxTextureSize  = 2048;                               dirty = true; }
        if (ti.sRGBTexture != sRGB)            { ti.sRGBTexture     = sRGB;                               dirty = true; }
        if (ti.textureCompression !=
            TextureImporterCompression.CompressedHQ)
                                               { ti.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }
}
