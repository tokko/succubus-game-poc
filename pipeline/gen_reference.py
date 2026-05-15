"""
Generate the shared succubus reference image for the bake-off (A: HY3D-2.1,
B: Omni, C: TRELLIS). Adapted from unity-game-poc/pipeline/gen_reference_v3.py.

Run with:
    "D:/ComfyUI/venv/Scripts/python.exe" -u pipeline/gen_reference.py

Requires SD Forge running with --api flag on http://127.0.0.1:7860.

The reference is the SAME for all three pipelines — pipeline differences should
come from architecture, not from generating distinct references.
"""
import base64
import io
import sys
import requests
from pathlib import Path
from PIL import Image

API_BASE = "http://127.0.0.1:7860"   # confirmed live this session

# Project-local refs dir so unity-game-poc is untouched.
PROJECT_ROOT = Path(__file__).resolve().parent.parent
OUTPUT_PATH = PROJECT_ROOT / "pipeline" / "refs" / "succubus_ref.png"

POSITIVE_PROMPT = (
    "masterpiece, best quality, highly detailed, "
    "solo, 1girl, single character, "
    "(t-pose:1.5), (outstretched arms:1.4), "
    "arms straight out to the sides at shoulder height, "
    "standing straight, front view, symmetrical, "
    "long black hair, high ponytail, hair tied back, "
    "red bikini top, long red loincloth, bare midriff, "
    "detailed face, beautiful eyes, sharp facial features, "
    "detailed hands, five fingers, "
    "fair skin, slender body, "
    "full body shot, head to toe, feet visible, "
    "white background, isolated, plain background, no shadow, even lighting"
)

NEGATIVE_PROMPT = (
    "2girls, multiple girls, twins, two characters, multiple views, "
    "character sheet, character reference, multiple poses, "
    "wings, demon wings, bat wings, feathered wings, "
    "horns, demon horns, devil horns, tail, "
    "(arms down:1.4), (hands at sides:1.4), arms at sides, hands behind back, hands on hips, "
    "side view, back view, three-quarter view, profile, "
    "cropped, partial body, headshot, half body, close-up, "
    "bad anatomy, deformed, mutated, missing limb, extra limb, missing fingers, extra fingers, "
    "fused fingers, mutated hands, malformed hands, "
    "nsfw, nude, fully nude, topless, "
    "blurry, low quality, jpeg artifacts, watermark, text, signature, "
    "arrows, symbols, markings, diagram lines, annotation, label, "
    "armor, helmet, weapon, sword, staff, "
    "background scenery, environment, indoor, outdoor"
)

WIDTH, HEIGHT = 1024, 1024
STEPS = 40
CFG_SCALE = 7
SAMPLER = "DDIM"   # k-diffusion samplers crash this Forge build (CFGDenoiserKDiffusion.inner_model missing)
SEED = 20260515    # fixed; rerun produces same reference across bake-off iterations


def check_api() -> bool:
    try:
        return requests.get(f"{API_BASE}/sdapi/v1/options", timeout=5).status_code == 200
    except Exception:
        return False


def main():
    if not check_api():
        print(f"[gen-ref] ERROR: SD Forge API not reachable at {API_BASE}")
        print("[gen-ref] Start SD Forge with --api flag and retry.")
        sys.exit(1)

    r = requests.get(f"{API_BASE}/sdapi/v1/options", timeout=5)
    current = r.json().get("sd_model_checkpoint", "unknown")
    print(f"[gen-ref] SD Forge OK at {API_BASE} — current model: {current}")
    print(f"[gen-ref] Using current model (avoiding API-crashing model switch)")

    payload = {
        "prompt": POSITIVE_PROMPT,
        "negative_prompt": NEGATIVE_PROMPT,
        "width": WIDTH,
        "height": HEIGHT,
        "steps": STEPS,
        "cfg_scale": CFG_SCALE,
        "sampler_name": SAMPLER,
        "seed": SEED,
        "batch_size": 1,
        "n_iter": 1,
        "restore_faces": False,
    }

    print(f"[gen-ref] Generating {WIDTH}x{HEIGHT}, steps={STEPS}, cfg={CFG_SCALE}, seed={SEED}")
    print(f"[gen-ref] Output: {OUTPUT_PATH}")
    r = requests.post(f"{API_BASE}/sdapi/v1/txt2img", json=payload, timeout=600)
    r.raise_for_status()

    images = r.json().get("images", [])
    if not images:
        print("[gen-ref] ERROR: API returned no images")
        sys.exit(1)

    img = Image.open(io.BytesIO(base64.b64decode(images[0])))
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUTPUT_PATH)
    print(f"[gen-ref] Saved: {OUTPUT_PATH} ({img.size})")


if __name__ == "__main__":
    main()
