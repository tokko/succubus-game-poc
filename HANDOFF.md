# HANDOFF — succubus-game-poc

## Current State

**Phase progress (per `~/.claude/plans/i-want-a-game-snug-flamingo.md`):**
- ✅ Phase 1 — Unity 6 URP project bootstrapped from `unity-game-poc/` engine config
- ✅ Phase 2 — PlayerController, CameraController (OTS + auto-recenter), SceneAutoSetup
- 🟡 Phase 3 — AI character pipeline (Hunyuan3D 3.0 + AccuRIG 2)
- ⬜ Phase 4 — Dark gothic painterly look

**Repo:** https://github.com/tokko/succubus-game-poc (public, main, `64f25ae` is Phase 1+2 bootstrap)

**Verified manually 2026-05-15:** WASD movement, jump, MMB-drag, scroll zoom, and auto-recenter on forward motion all working with cylinder placeholder. URP renders; `activeInputHandler: 2` (Both) in `ProjectSettings.asset` is the load-bearing setting that makes legacy `Input.GetAxis` work.

**PIVOT 2026-05-15:** Original plan was built on a false premise — Hunyuan3D 3.0
does NOT exist on HuggingFace (research agent hallucinated it). Real Tencent state
caps at the 2-series + new Oct-2025 variants (Omni, Part). Plan pivoted to a
**3-pipeline bake-off**.

**Bake-off status (2026-05-15):**
- ✅ **A: Hunyuan3D-2.1** — `Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx`
  (15.4 MB, 58k verts, Rigify rig with Idle/Walk/Run NLA). Generated from
  `pipeline/refs/succubus_ref.png` via `gen_a_hy3d21.py` (shape+paint, ~15 min on
  RTX 3090) then `rig_a_hy3d21.py` (Blender headless Rigify, ~1 min).
- 🟡 **B: Hunyuan3D-Omni** — DEFERRED. Investigation showed Omni's value comes
  from control signals (pose/voxel/bbox/point-cloud), not image-only. Running
  image-only is equivalent to 2.1 in disguise. Proper comparison needs a pose-
  extraction step (OpenPose / MediaPipe → 3D skeleton joints → Omni's pose
  control) which is a multi-hour integration. EU-gating did NOT block the
  download from this account.
- 🟡 **C: TRELLIS.2 (microsoft/TRELLIS.2-4B)** — DEFERRED. ~25 GB weight pull
  + ComfyUI node install required. Different architecture (structured-latent)
  so meaningful only with a real run, not a stub.

**Next session — do this first:**
Open Unity, verify `Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx`
auto-loads in `GameScene.unity` (the FBX path is now first-priority in
`SceneAutoSetup.cs`). Expected issue: the Rigify rig uses DEF-bone names that
don't map to Unity Humanoid muscles — animationType must be **Generic**, not
Humanoid (per `unity-game-poc/wiki/characters.md`). If Unity tries to import
as Humanoid by default and Avatar Configurator shows red bones, fix by either
(a) writing `Assets/Editor/SuccubusImporter.cs` AssetPostprocessor to force
animationType=Generic on this FBX path, or (b) doing it manually in the
Inspector and re-saving the .meta.

After verification, choose next sprint: either pursue Pipeline B with the
pose-extraction integration, OR pursue Pipeline C (TRELLIS.2 weight pull +
ComfyUI integration), OR skip the bake-off and move to Phase 4 (gothic
painterly look) using Pipeline A as the final character.

**Warnings:**
- Do NOT delete or modify `unity-game-poc/`. We're cribbing scripts from it.
- `pipeline/` Python scripts live at the project root, NOT under `Assets/`.
- SD Forge's k-diffusion samplers crash this build — use DDIM only.
- HY3D-2.1 paint pipeline at PAINT_VIEWS=9, PAINT_RES=768 OOMs on RTX 3090. Use
  6 views @ 512 px (verified in unity-game-poc).
- `texture_mesh.py` decimates to 30k faces before xatlas — DO NOT skip this.
- Subsequent sprints (B: Omni, C: TRELLIS) require multi-GB HF pulls.

**Out of scope for this iteration:**
Combat, abilities, UI, inventory, dialog, multi-scene, networking, save system, photoreal
visuals. See plan file for full non-goals list.

---

## History

(Empty — first entry will be appended by `/session-wrap` after Sprint 1.)
