using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batchmode-friendly screenshot capture for the bake-off scene.
///
/// Run via:
///   Unity.exe -batchmode -projectPath . -executeMethod BakeoffScreenshots.RunAll -quit -logFile -
///
/// Opens Bakeoff.unity, then for each slot positions a render camera around the
/// character, renders to a RenderTexture, and writes PNG to wiki/bakeoff-images/.
/// </summary>
public static class BakeoffScreenshots
{
    const string SCENE_PATH = "Assets/Scenes/Bakeoff.unity";
    const int    WIDTH      = 1024;
    const int    HEIGHT     = 1024;

    // Camera offsets relative to slot, in WORLD space (the slot's local +Z is "front").
    static readonly (string suffix, Vector3 offset, Vector3 lookAtOffset)[] Angles = new[]
    {
        ("front",   new Vector3(0f,  1.2f, -2.3f), new Vector3(0f, 1.0f, 0f)),
        ("threequarter", new Vector3(1.5f, 1.2f, -1.8f), new Vector3(0f, 1.0f, 0f)),
        ("side",    new Vector3(2.5f, 1.2f, 0f),    new Vector3(0f, 1.0f, 0f)),
    };

    /// <summary>One-shot: build scene + capture screenshots in a single batchmode run.</summary>
    public static void BuildAndShoot()
    {
        SetupBakeoffScene.Build();
        RunAll();
    }

    public static void RunAll()
    {
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        Debug.Log($"[Screenshots] Opened {SCENE_PATH}");

        var outDir = Path.GetFullPath("wiki/bakeoff-images");
        Directory.CreateDirectory(outDir);

        var slots = scene.GetRootGameObjects()
            .Where(g => g.name.StartsWith("Slot_"))
            .OrderBy(g => g.name)
            .ToArray();

        if (slots.Length == 0)
        {
            Debug.LogError("[Screenshots] No Slot_* root objects found. Run Tools > Build Bake-off Scene first.");
            EditorApplication.Exit(1);
            return;
        }

        // One temp camera + render texture for all captures
        var rt = new RenderTexture(WIDTH, HEIGHT, 24, RenderTextureFormat.ARGB32);
        rt.Create();

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.fieldOfView = 30f;       // tight for portrait framing
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.Skybox;

        foreach (var slot in slots)
        {
            string id = slot.name.Replace("Slot_", "");
            Vector3 slotPos = slot.transform.position;

            foreach (var (suffix, offset, lookOffset) in Angles)
            {
                cam.transform.position = slotPos + offset;
                cam.transform.LookAt(slotPos + lookOffset);

                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string path = Path.Combine(outDir, $"slot-{id}-{suffix}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[Screenshots] {path}");
            }
        }

        Object.DestroyImmediate(camGo);
        rt.Release();
        Object.DestroyImmediate(rt);

        Debug.Log($"[Screenshots] Done. {slots.Length} slots × {Angles.Length} angles = {slots.Length * Angles.Length} images at {outDir}");
    }
}
