# Characters

<!-- 2026-05-15 -->

## Succubus — Player Character (planned, not yet implemented)

**Status:** Pipeline planned, not yet executed. Placeholder is Mixamo Y-Bot.

### Primary pipeline — Hunyuan3D 3.0 local + AccuRIG 2 fallback

Pre-existing lessons from `unity-game-poc/wiki/characters.md` (Gen-4 with HY3D-2.1)
that **still apply** under HY3D 3.0:

- **Reference prompt design**: T-pose + no wings/horns + single-character emphasis
  weights (`(t-pose:1.5)`, `(outstretched arms:1.4)`). `2girls, multiple girls` in
  negative or model will produce twins. Square 1024×1024 — portrait aspect spawns
  duplicates.
- **SD Forge gotchas** (still current): port may be 7861 not 7860; k-diffusion
  samplers crash with `'CFGDenoiserKDiffusion' object has no attribute 'inner_model'`
  in current build — use **DDIM**. Don't switch models via API mid-session
  (`POST /sdapi/v1/options { sd_model_checkpoint }` crashes). Wait ~20 s after
  `Model loaded in X.Xs` before first txt2img.

What HY3D 3.0 is expected to fix vs the painful 2.1 pipeline:

- **Automatic UV unwrap** — kills the `xatlas.parametrize` crash class (was: decimate
  to 30k faces before painting).
- **1536³ shape resolution** — better fingers/face vs 2.1 mini.
- **Integrated rigging** (Hunyuan3D Studio beta) — hopefully eliminates the
  envelope-weighting fallback. If beta rigging fails: AccuRIG 2 (Reallusion, free)
  is the planned fallback before Rigify envelope.

### Rigging branches (try in order)

| Branch | Tool | Expected outcome | Fallback trigger |
|---|---|---|---|
| A | HY3D 3.0 Studio integrated rigging | T-pose mesh + Humanoid-mappable skeleton | Bones don't match Mecanim, weights bad |
| B | AccuRIG 2 (Reallusion) | Per-surface skin weights, Mixamo-compatible bone names | AI mesh open-edges still defeat rigging |
| C | Blender + Rigify ENVELOPE weighting | Proven recipe from gen-2 in unity-game-poc | (last resort) |

### Paid escape hatch

Rodin Gen-2 API (~$1.50/character). Mature pipeline, T-pose enforcement, rigging
baked in. Treat as escape hatch only — do not write `submit_rodin.py` unless all
three local branches fail.

### Last-resort plan D

Daz3D Genesis 8 + Daz to Unity Bridge. Proven path, but contradicts the
"text-prompt-generated" goal. Trigger: all local AI paths fail OR cumulative
AI-pipeline time > 1 day.

### Anti-pattern: do NOT retry HY3D-2.1

The Gen-4 pipeline in `unity-game-poc/wiki/characters.md` works but requires ~10
monkey-patches (delight model, transformers safety check, UNet from_pretrained,
trust_remote_code, etc.). HY3D 3.0's automatic UV unwrap + integrated rigging are
exactly the features designed to kill that failure class. If 3.0 stumbles, prefer
escape hatches over re-debugging 2.1.

## Y-Bot Placeholder

While the succubus pipeline is in flight, the player character is **Mixamo Y-Bot**
with Idle/Walking/Running animations. Same Humanoid avatar contract: when the
succubus FBX is ready, swap the SkinnedMeshRenderer source — animations retarget
automatically.
