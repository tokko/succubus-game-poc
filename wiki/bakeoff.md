# Bake-off — AI 3D character pipelines

<!-- 2026-05-15 -->

Comparison of two text-prompt-to-rigged-FBX pipelines on the same source
image (`pipeline/refs/succubus_ref.png`, 1024×1024, SD Forge `ilustmix_v111`,
seed 20260515, DDIM 40 steps, T-pose succubus reference).

## Quick verdict

| Pipeline | Cleanliness | Setup pain | Re-runnable | Verdict |
|---|---|---|---|---|
| A: Hunyuan3D-2.1 (local Windows) | sausagy 90s-look, 50k verts | medium (gen-4 patches) | yes | works, ceiling-bound |
| B: TRELLIS-image-large (WSL Linux) | cleaner, 4.7k verts (10× simpler) | high (one-time, 5 cascading bugs) | yes | clear winner on topology |

Both still need rigging via Blender Rigify + envelope weighting (same script
`rig_*.py`). Both use Unity Generic rig (Rigify DEF-bones don't map to
Mecanim Humanoid muscle definitions).

Pipeline C (originally TRELLIS.2, Hunyuan3D-Omni, or Rodin) deferred — see
"Open questions" at bottom.

## Side-by-side specs

|  | A: HY3D-2.1 | B: TRELLIS-image-large |
|---|---|---|
| Where it runs | Windows ComfyUI venv | WSL Ubuntu 26.04, conda env `trellis` |
| Free / paid | free, local | free, local |
| Pull size (weights) | 6.4 GB (already cached) | ~5 GB (DINOv2 + TRELLIS ckpts) |
| Shape model | dit-v2-1, octree 512, 50 steps | structured-latent + sparse 3D conv |
| Texture model | paintpbr-v2-1, 6 views @ 512 | gaussian splatting + mesh extraction |
| Output mesh verts | 50,711 (post-rig) | 4,749 (post-rig) |
| Output FBX size | 16.0 MB | 9.87 MB |
| Output texture | 2048×2048 PBR (albedo+metallic+roughness JPGs) | embedded in GLB (no sidecar maps) |
| Gen time | ~12 min (shape ~7 + paint ~5) on RTX 3090 | ~3 min on RTX 3090 |
| Patches required to run | ~10 monkey-patches in HY3D-2.1 codebase | 0 (after install — but install itself was painful) |

## Bake-off scene

`Assets/Scenes/Bakeoff.unity` (build via `Tools > Build Bake-off Scene`).
Three slots side-by-side at 3 m spacing, each character normalized to 1.8 m
height (TARGET_HEIGHT constant), feet on ground. Slot C is a placeholder
capsule until a third pipeline lands. Lighting + post-processing inherited
from Phase 4 GothicVolumeProfile.

Each pipeline's character is loaded from the same Rigify-rigged FBX, plays
its own Animator controller (Idle clip in the bake-off context).

## Screenshots

See `wiki/bakeoff-images/`. Three angles per slot — front, three-quarter,
side. Re-capture via `Unity.exe -batchmode -executeMethod
BakeoffScreenshots.BuildAndShoot -quit`.

## Cost — what each "easy local AI char" actually took

### Pipeline A (HY3D-2.1)
Inherited from `unity-game-poc`. Two real bugs caught:
- `gen_v21.py` rembg branch was dead code: `Image.open().convert("RGBA"); if mode == "RGB"` — mode is always RGBA after the convert, so the background-remover never ran. White-background reference produced a flat "back plate" and body-mesh holes (commit `a6d02f0` → fix in `4b55902`).
- FBX clip names: Blender's `bake_anim_use_all_actions=True` + `bake_anim_use_nla_strips=True` exports 6 clips with names like `Succubus_Rig|Idle` and `Succubus_Rig|Succubus_Rig|Idle`. SetupBakeoffA's exact-match switch missed all of them, animator stuck in T-pose (fix in `fe6cabb`).

### Pipeline B (TRELLIS-image-large in WSL)
Five cascading bugs, in order:

1. **conda TOS not auto-accepted.** Newer miniconda (26.3.2) default-denies the
   `pkgs/main` and `pkgs/r` channel TOS, env creation fails with
   `CondaToSNonInteractiveError`. Fix: `conda tos accept --override-channels`.
2. **`bash setup.sh` doesn't propagate the `conda` shell function.** setup.sh
   calls `conda activate trellis` internally, but bash launches a child shell
   without the function, so activate silently no-ops. Result: env created but
   torch installed into BASE env. Fix: `source setup.sh` instead.
3. **Conda's pytorch 2.4.0 build has stale Intel ITT dep.** Imports fail with
   `undefined symbol: iJIT_NotifyEvent`. Fix: replace with pip wheel
   `torch==2.4.0+cu118` (bundles its own runtime libs).
4. **setup.sh's version table is keyed on plain version, our wheel reports
   `2.4.0+cu118`.** Switch-case falls through to "Unsupported PyTorch & CUDA
   version" for every extension. Fix: install extensions manually with the
   xformers / kaolin URLs from the table.
5. **pip build-isolation hides torch from extension builds.** nvdiffrast,
   diffoctreerast, diff_gaussian_rasterization can't `import torch` during
   their build. Fix: `pip install --no-build-isolation`.

Plus two TRELLIS-specific issues:
- **`git clone --depth=1` skipped flexicubes submodule** — empty dir, import chain breaks. Fix: `--recurse-submodules`.
- **`transformers==5.x` newer than torch 2.4 expects** — string-typed annotations in `torch.library.custom_op` reject. Fix: pin `transformers==4.46.3`.

All seven fixes are now in `pipeline/wsl_install_trellis.sh` +
`pipeline/wsl_build_extensions.sh`. Re-install is one command.

## What we learned about output quality

- **TRELLIS produces much cleaner topology** (10× fewer verts, comparable apparent
  detail). The HY3D mesh is "sausagy" because it's an over-tessellated displaced
  sphere with no topology cleanup; TRELLIS's structured-latent representation
  appears to retopologize internally.
- **TRELLIS texture is embedded in the GLB** (vertex colors or single UV-atlas).
  The HY3D paint pipeline produces proper 2k PBR maps (albedo + metallic +
  roughness). TRELLIS output is therefore gray-untextured in Unity unless we
  extract+assign the embedded texture (TODO — `pipeline/extract_trellis_texture.py`
  if Pipeline B's quality justifies the effort).
- **Both fall apart on hands and face** — the underlying limitation is that a
  single 1024² front-view reference doesn't constrain enough geometry on these
  parts. Multi-view input (Hunyuan3D-2mv, or rendering the SD reference at
  multiple angles via img2img + ControlNet) would likely help — future work.

## Open questions / what's NOT in the bake-off

1. **Pipeline C is missing.** Candidates considered:
   - **TRELLIS.2-4B** (newer, larger) — install path same as TRELLIS-image-large now that the workarounds exist. Mostly a matter of switching the `from_pretrained` ID.
   - **Hunyuan3D-Omni** (Oct 2025) — image+control architecture; image-only mode isn't formally exposed in `inference.py`. Would need pose-extraction integration for fair comparison.
   - **Rodin Gen-2** (paid, ~$1.50/char) — best "easy" cloud baseline. Account + payment required.
   - **Daz3D Genesis 8 + Daz to Unity Bridge** (owned assets, GUI clicks) — fastest path to a polished result, but breaks the "AI text-prompt-generated" intent.
2. **PBR texture extraction from TRELLIS GLB** — currently slot B renders gray.
3. **Manual screenshot quality check** — batchmode captures may have URP shader
   issues; visual inspection in Editor still needed to confirm.

## Files of interest

| Path | What |
|---|---|
| `pipeline/refs/succubus_ref.png` | Shared reference image (1024², seed 20260515) |
| `pipeline/gen_reference.py` | SD Forge reference generator |
| `pipeline/gen_a_hy3d21.py` | Pipeline A shape+paint (HY3D-2.1, Windows) |
| `pipeline/rig_a_hy3d21.py` | Pipeline A Blender rig |
| `pipeline/wsl_install_trellis.sh` | Pipeline B WSL installer (idempotent) |
| `pipeline/wsl_build_extensions.sh` | Pipeline B CUDA extension builder |
| `pipeline/wsl_run_trellis.py` | Pipeline B inference (WSL conda env `trellis`) |
| `pipeline/rig_b_trellis.py` | Pipeline B Blender rig |
| `Assets/Editor/SetupBakeoffA.cs` | Pipeline A Unity-side wire-up |
| `Assets/Editor/SetupBakeoffB.cs` | Pipeline B Unity-side wire-up |
| `Assets/Editor/SetupBakeoffScene.cs` | Builds `Bakeoff.unity` with height normalization |
| `Assets/Editor/BakeoffScreenshots.cs` | Batchmode capture for `wiki/bakeoff-images/` |
| `Assets/Characters/Bakeoff/A_HY3D21/succubus_a.fbx` | Pipeline A character |
| `Assets/Characters/Bakeoff/B_TRELLIS/succubus_b.fbx` | Pipeline B character |
