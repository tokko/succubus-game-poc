using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot Phase 4 setup — dark gothic painterly atmosphere.
///   - GothicVolumeProfile: ColorAdjustments + Vignette + Bloom + FilmGrain
///   - Global Volume in GameScene
///   - Directional light retuned warm-sunset, low intensity
///   - Violet rim point light near the character
///   - Dark stone ground material
///   - Skybox material: dark solid colour (no default blue sky)
///
/// Runs once on domain reload; re-run via Tools > Setup Gothic Look.
/// </summary>
[InitializeOnLoad]
public static class SetupGothicLook
{
    const string PROFILE_PATH = "Assets/Settings/GothicVolumeProfile.asset";
    const string SKYBOX_PATH  = "Assets/Settings/GothicSkybox.mat";
    const string SCENE_PATH   = "Assets/Scenes/GameScene.unity";
    const string PREF_KEY     = "SetupGothicLook_Done_v1";

    static SetupGothicLook()
    {
        if (EditorPrefs.GetBool(PREF_KEY, false)) return;
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Setup Gothic Look")]
    public static void ForceRun()
    {
        EditorPrefs.DeleteKey(PREF_KEY);
        Run();
    }

    static void Run()
    {
        EditorApplication.delayCall -= Run;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        Step1_BuildVolumeProfile();
        Step2_BuildSkyboxMaterial();
        Step3_UpdateScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetBool(PREF_KEY, true);
        Debug.Log("[SetupGothicLook] Done.");
    }

    // ── 1. VolumeProfile ──────────────────────────────────────────────────────
    static VolumeProfile Step1_BuildVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
        if (profile != null)
        {
            AssetDatabase.DeleteAsset(PROFILE_PATH);
            Debug.Log("[SetupGothicLook] Step 1: deleted stale profile; rebuilding.");
        }
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, PROFILE_PATH);

        // ColorAdjustments — shadows deep blue/black, midtones desaturated red-violet
        var color = profile.Add<ColorAdjustments>(overrides: true);
        color.postExposure.Override(-0.4f);
        color.contrast.Override(15f);
        color.saturation.Override(-25f);
        color.colorFilter.Override(new Color(0.95f, 0.85f, 0.9f, 1f)); // slight desaturated violet wash

        // White balance: shift cool
        var wb = profile.Add<WhiteBalance>(overrides: true);
        wb.temperature.Override(-20f); // cooler
        wb.tint.Override(15f);          // toward magenta

        // Bloom — subtle, not overblown
        var bloom = profile.Add<Bloom>(overrides: true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(0.7f, 0.55f, 0.85f, 1f));

        // Vignette — heavy, dark violet
        var vignette = profile.Add<Vignette>(overrides: true);
        vignette.color.Override(new Color(0.04f, 0f, 0.06f, 1f));
        vignette.intensity.Override(0.45f);
        vignette.smoothness.Override(0.5f);

        // Film grain — painterly noise
        var grain = profile.Add<FilmGrain>(overrides: true);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(0.25f);
        grain.response.Override(0.85f);

        // Lift Gamma Gain — push shadows toward blue, highlights toward warm
        var lgg = profile.Add<LiftGammaGain>(overrides: true);
        lgg.lift.Override(new Vector4(0.85f, 0.85f, 1.05f, -0.05f)); // shadows blue
        lgg.gain.Override(new Vector4(1.1f, 0.95f, 0.85f,  0.05f));  // highlights warm

        EditorUtility.SetDirty(profile);
        Debug.Log($"[SetupGothicLook] Step 1: GothicVolumeProfile built @ {PROFILE_PATH}");
        return profile;
    }

    // ── 2. Skybox material (dark gradient via solid color) ────────────────────
    static Material Step2_BuildSkyboxMaterial()
    {
        var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_PATH);
        // Skybox/Procedural is built-in and works in URP for a solid-color sky
        var sky = Shader.Find("Skybox/Procedural");
        if (sky == null) sky = Shader.Find("Skybox/Cubemap");
        if (sky == null)
        {
            Debug.LogWarning("[SetupGothicLook] Step 2: No Skybox shader found — leaving skybox alone.");
            return null;
        }

        if (skyboxMat == null)
        {
            skyboxMat = new Material(sky);
            AssetDatabase.CreateAsset(skyboxMat, SKYBOX_PATH);
        }
        else
        {
            skyboxMat.shader = sky;
        }

        // Pull sun + atmosphere to a deep night-violet look
        skyboxMat.SetFloat("_SunDisk", 0f);              // hide sun
        skyboxMat.SetFloat("_AtmosphereThickness", 0.3f); // thin
        skyboxMat.SetFloat("_Exposure", 0.4f);            // dim
        skyboxMat.SetColor("_SkyTint",    new Color(0.18f, 0.08f, 0.22f, 1f)); // violet-purple
        skyboxMat.SetColor("_GroundColor",new Color(0.04f, 0.02f, 0.06f, 1f)); // near-black

        EditorUtility.SetDirty(skyboxMat);
        Debug.Log($"[SetupGothicLook] Step 2: GothicSkybox.mat built @ {SKYBOX_PATH}");
        return skyboxMat;
    }

    // ── 3. Scene wiring: Global Volume + lights + ground + skybox ─────────────
    static void Step3_UpdateScene()
    {
        if (!File.Exists(Path.GetFullPath(SCENE_PATH)))
        {
            Debug.LogWarning($"[SetupGothicLook] Step 3: {SCENE_PATH} not found — skipping.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // 3a. Global Volume
        var volumeGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Global Volume");
        if (volumeGo == null)
        {
            volumeGo = new GameObject("Global Volume");
            volumeGo.transform.position = Vector3.zero;
        }
        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);

        // 3b. Directional light → warm sunset, low intensity
        var dirLightGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Directional Light");
        if (dirLightGo != null)
        {
            var dir = dirLightGo.GetComponent<Light>();
            if (dir != null)
            {
                dir.color = new Color(1.0f, 0.62f, 0.4f, 1f); // warm orange
                dir.intensity = 0.6f;
                dir.shadows = LightShadows.Soft;
                dirLightGo.transform.rotation = Quaternion.Euler(20f, -40f, 0f); // low sun
            }
        }

        // 3c. Violet rim point light near character
        var rimGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "RimLight");
        if (rimGo == null)
        {
            rimGo = new GameObject("RimLight");
        }
        rimGo.transform.position = new Vector3(0f, 2.5f, -2f);
        var rim = rimGo.GetComponent<Light>();
        if (rim == null) rim = rimGo.AddComponent<Light>();
        rim.type = LightType.Point;
        rim.color = new Color(0.55f, 0.3f, 0.95f, 1f);  // violet
        rim.intensity = 2.5f;
        rim.range = 8f;
        rim.shadows = LightShadows.None;

        // 3d. Ground material → dark stone
        var ground = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
        if (ground != null)
        {
            var mr = ground.GetComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (mr != null && litShader != null)
            {
                var groundMat = new Material(litShader);
                groundMat.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.13f, 1f)); // very dark stone
                groundMat.SetFloat("_Smoothness", 0.05f);
                groundMat.SetFloat("_Metallic", 0f);
                mr.sharedMaterial = groundMat;
            }
        }

        // 3e. Skybox + render settings: ambient dark
        var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_PATH);
        if (skyboxMat != null) RenderSettings.skybox = skyboxMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor    = new Color(0.10f, 0.07f, 0.13f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.04f, 0.09f, 1f);
        RenderSettings.ambientGroundColor  = new Color(0.02f, 0.02f, 0.03f, 1f);

        // 3f. Fog — purple haze
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.10f, 0.06f, 0.14f, 1f);
        RenderSettings.fogDensity = 0.025f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupGothicLook] Step 3: scene wired with volume, lights, ground, skybox, fog.");
    }
}
