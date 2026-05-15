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
**3-pipeline bake-off** — same reference image rendered through three local pipelines,
then evaluated side-by-side in a Unity scene.

**Bake-off pipelines:**
- **A: Hunyuan3D-2.1 + gen-4 patches** — known-working baseline from `unity-game-poc`
- **B: Hunyuan3D-Omni** (Oct 2025 variant) — likely incremental improvement on A
- **C: TRELLIS.2 + AccuRIG 2** — different architecture (structured-latent)

Output: `Assets/Characters/Bakeoff/{A_HY3D21,B_Omni,C_TRELLIS}/*.fbx` and a
`Bakeoff.unity` scene with all three side-by-side, same anim controller, same
lighting. Wiki write-up at the end.

**Next session — do this first:**
Sprint 1 of the bake-off — produce **A: Hunyuan3D-2.1 succubus FBX** in
`Assets/Characters/Bakeoff/A_HY3D21/`. Copy the working `pipeline/*.py` scripts
from `unity-game-poc/pipeline/` and adapt output paths to the bake-off subdir.
Run the full chain: SD Forge txt2img (port 7860 currently up) → HY3D-2.1 shape +
paint → Blender Rigify + animations → Unity FBX. Most of this is already
debugged in `unity-game-poc/wiki/characters.md` — re-apply the patches from
there.

The HY3D-2.1 weight cache referenced as `E:\hunyuan3d\models` in the old wiki is
GONE (E: drive empty). Use the HuggingFace cache at
`C:\Users\andre\.cache\huggingface\hub\models--tencent--Hunyuan3D-2.1` (6.41 GB,
already cached). `HY3DGEN_MODELS` env var may need to be unset or pointed at HF
cache root.

Success = `Assets/Characters/Bakeoff/A_HY3D21/succubus_rig_pbr.fbx` exists,
imports as Humanoid in Unity, plays Idle anim.

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
