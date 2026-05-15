# Scene Setup

<!-- 2026-05-15 -->

## SceneAutoSetup.cs

Editor-only `[InitializeOnLoad]` script that creates `Assets/Scenes/GameScene.unity`
the first time the project loads. Guarded by `EditorPrefs` key `SceneAutoSetup_Done`
— runs once, then never again unless you use `Tools > Re-run Scene Setup` (which
clears the key).

### Play-mode guard
Differs from the `unity-game-poc/` version. The old version crashed with
`InvalidOperationException` if the first domain reload happened during play-mode
transition. The new `SafeRun` checks `EditorApplication.isPlayingOrWillChangePlaymode`
and re-queues itself instead of throwing.

### Scene contents
- **Ground**: Plane primitive, scale (10, 1, 10)
- **Player** (root): Rigidbody (freezeRotation), CapsuleCollider, PlayerController
- **Character** (child of Player):
  - Priority 1: `Assets/Characters/Succubus/succubus.fbx` (AI-generated, Phase 3)
  - Priority 2: `Assets/Characters/Placeholder/YBot.fbx` (Mixamo, not yet downloaded)
  - Priority 3: Unity cylinder primitive (fallback for camera testing)
- **Directional Light**: Soft shadows, angled (50°, -30°)
- **Main Camera**: with `CameraController`, target = Player transform

### Why three-tier fallback
Lets us verify movement + camera math BEFORE the character pipeline (Phase 3) is
ready. With cylinder fallback active, WASD + MMB drag + scroll zoom + auto-recenter
are all testable; only animation transitions need a real Humanoid rig.

### Re-running
`Tools > Re-run Scene Setup` from the menu bar. Editor must be in **edit mode**
(not play mode) — the guard re-queues if you forget.
