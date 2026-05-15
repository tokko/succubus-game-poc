# HANDOFF — succubus-game-poc

## Current State

**Phase progress (per `~/.claude/plans/i-want-a-game-snug-flamingo.md`):**
- ✅ Phase 1 — Unity 6 URP project bootstrapped from `unity-game-poc/` engine config
- ✅ Phase 2 — PlayerController, CameraController (OTS + auto-recenter), SceneAutoSetup
- ✅ Phase 3 — AI character pipeline (Hunyuan3D-2.1 + TRELLIS-image-large bake-off — Hunyuan3D 3.0 doesn't exist; see `wiki/bakeoff.md`)
- ✅ Phase 4 — Dark gothic painterly look (`SetupGothicLook.cs`, auto-runs)

**Repo:** https://github.com/tokko/succubus-game-poc (public, main, `64f25ae` is Phase 1+2 bootstrap)

**Verified manually 2026-05-15:** WASD movement, jump, MMB-drag, scroll zoom, and auto-recenter on forward motion all working with cylinder placeholder. URP renders; `activeInputHandler: 2` (Both) in `ProjectSettings.asset` is the load-bearing setting that makes legacy `Input.GetAxis` work.

**PIVOT 2026-05-15:** Original plan was built on a false premise — Hunyuan3D 3.0
does NOT exist on HuggingFace (research agent hallucinated it). Real Tencent state
caps at the 2-series + new Oct-2025 variants (Omni, Part). Plan pivoted to a
**3-pipeline bake-off**.

**Bake-off status (2026-05-15, commit `7420e3b`):** see `wiki/bakeoff.md`.
- ✅ **A: Hunyuan3D-2.1** local Windows — `Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx`, 16 MB, 50,711 verts, textured (red dress, ponytail).
- ✅ **B: TRELLIS-image-large** local WSL Ubuntu 26.04 conda env `trellis` — `Assets/Characters/Bakeoff/B_TRELLIS/succubus_b.fbx`, 9.87 MB, **4,749 verts (10× cleaner topology)**. Currently renders gray — texture is embedded in the GLB, not yet extracted to sidecar maps.
- 🟡 **C** — deferred. Candidates documented in `wiki/bakeoff.md` (TRELLIS.2-4B / Hunyuan-Omni / Rodin / Daz3D).

Bake-off comparison scene: `Assets/Scenes/Bakeoff.unity` (built by
`SetupBakeoffScene.Build`, normalizes all slots to 1.8 m height). 9 PNG
screenshots at `wiki/bakeoff-images/` (3 slots × 3 angles, batchmode-captured
via `BakeoffScreenshots.BuildAndShoot`).

**Next session — pick one:**
1. **Texture Pipeline B.** Extract the embedded texture from `succubus_b.glb`,
   write `SuccubusB.mat` with it, re-screenshot. ~1 hr. Highest visual impact.
2. **Pipeline C.** Easiest path: swap `TRELLIS-image-large` for `TRELLIS.2-4B`
   in `pipeline/wsl_run_trellis.py` (same install, larger model, slower run).
   Alternative: Rodin Gen-2 API ($1.50) for a paid baseline.
3. **Real animations.** Replace the procedural Idle/Walk/Run from `rig_*.py`
   with Mixamo retargeted clips. Procedural NLA is the main "uncanny" source.
4. **Phase 4 verification.** Open Unity, confirm `SetupGothicLook` auto-ran
   cleanly (post-process volume + violet rim + fog applied to GameScene).

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

### 2026-05-15 — Bake-off complete (Pipelines A + B)
- Bootstrapped Unity 6 URP project, ported PlayerController + CameraController + SceneAutoSetup from unity-game-poc, added shoulder offset + auto-recenter on forward motion.
- Pivot off "Hunyuan3D 3.0" (turned out to be a hallucinated research claim).
- **Pipeline A (HY3D-2.1, Windows):** fixed dead-code rembg branch (was causing back-plate + body holes), fixed FBX clip-name prefix matching (was leaving Idle/Walk/Run motions null → T-pose). Final: textured succubus, 50k verts.
- **Pipeline B (TRELLIS-image-large, WSL):** 7 cascading install bugs fixed and documented in `wiki/bakeoff.md` (conda TOS, source-vs-bash setup.sh, conda torch's broken ITT dep, version-table mismatch, build-isolation, missing submodule, transformers 5.x breaking torch 2.4). Final: cleaner topology, 4.7k verts, gray (texture extraction TBD).
- Bake-off scene with height normalization (`TARGET_HEIGHT=1.8m`), 9 batchmode-captured screenshots in `wiki/bakeoff-images/`, full write-up in `wiki/bakeoff.md`.
- Commits in this chain: `64f25ae` bootstrap, `a6d02f0` Pipeline A first pass, `4b55902` rembg fix, `fe6cabb` animator clip-name fix, `ad33e9e` Pipeline B, `aa98250` height normalization, `7420e3b` Bakeoff scene + screenshots + wiki.
