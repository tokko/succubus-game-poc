"""
Generate 3 side/back views of the succubus from the existing front reference.
Used as input to Hunyuan3D-2mv (Pipeline C, multi-view bake-off slot).

Reads:
    pipeline/refs/succubus_ref.png            (front, generated earlier)
Writes:
    pipeline/refs/succubus_ref_left.png
    pipeline/refs/succubus_ref_back.png
    pipeline/refs/succubus_ref_right.png

Run with:
    "D:/ComfyUI/venv/Scripts/python.exe" -u pipeline/gen_multi_view_refs.py

Requires SD Forge running at http://127.0.0.1:7860 with --api flag.
"""
import base64
import io
import sys
import requests
from pathlib import Path
from PIL import Image

API_BASE = "http://127.0.0.1:7860"
PROJECT = Path(__file__).resolve().parent.parent
FRONT = PROJECT / "pipeline" / "refs" / "succubus_ref.png"

# Same character design as the front reference — keep description consistent so
# img2img stays anchored to that look. Only the view direction changes.
COMMON_POSITIVE = (
    "masterpiece, best quality, highly detailed, "
    "solo, 1girl, single character, "
    "(t-pose:1.5), (outstretched arms:1.4), "
    "long black hair, high ponytail, hair tied back, "
    "red bikini top, long red loincloth, bare midriff, "
    "detailed face, beautiful eyes, "
    "fair skin, slender body, "
    "full body shot, head to toe, feet visible, "
    "white background, isolated, plain background, no shadow, even lighting"
)
COMMON_NEGATIVE = (
    "2girls, multiple girls, twins, multiple views, character sheet, "
    "wings, demon wings, horns, tail, "
    "(arms down:1.4), arms at sides, "
    "cropped, partial body, headshot, "
    "bad anatomy, deformed, extra limb, missing limb, missing fingers, "
    "nsfw, nude, "
    "blurry, low quality, watermark, text, signature, "
    "armor, weapon, background scenery"
)

VIEWS = [
    {
        "name": "left",
        "prompt": COMMON_POSITIVE + ", (side view:1.4), (left profile:1.3), facing right",
        "negative": COMMON_NEGATIVE + ", back view, front view, three-quarter",
        "denoising": 0.62,
    },
    {
        "name": "back",
        "prompt": COMMON_POSITIVE + ", (back view:1.5), facing away, viewed from behind, ponytail visible from behind",
        "negative": COMMON_NEGATIVE + ", side view, front view, face visible",
        "denoising": 0.75,
    },
    {
        "name": "right",
        "prompt": COMMON_POSITIVE + ", (side view:1.4), (right profile:1.3), facing left",
        "negative": COMMON_NEGATIVE + ", back view, front view, three-quarter",
        "denoising": 0.62,
    },
]

WIDTH, HEIGHT = 1024, 1024
STEPS = 35
CFG_SCALE = 7
SAMPLER = "DDIM"   # the only stable sampler in our SD Forge build
SEED_BASE = 20260516

def b64(p):
    with open(p, "rb") as f: return base64.b64encode(f.read()).decode()

def check_api():
    try: return requests.get(f"{API_BASE}/sdapi/v1/options", timeout=5).status_code == 200
    except Exception: return False

def gen_view(view, ref_b64, out_path):
    payload = {
        "init_images": [ref_b64],
        "prompt": view["prompt"],
        "negative_prompt": view["negative"],
        "denoising_strength": view["denoising"],
        "width": WIDTH, "height": HEIGHT,
        "steps": STEPS, "cfg_scale": CFG_SCALE,
        "sampler_name": SAMPLER,
        "seed": SEED_BASE + abs(hash(view["name"])) % 1000,
        "batch_size": 1, "n_iter": 1,
    }
    print(f"[multi-view] {view['name']}: denoise={view['denoising']}")
    r = requests.post(f"{API_BASE}/sdapi/v1/img2img", json=payload, timeout=600)
    r.raise_for_status()
    images = r.json().get("images", [])
    if not images:
        print(f"[multi-view] {view['name']}: NO IMAGES returned")
        return False
    Image.open(io.BytesIO(base64.b64decode(images[0]))).save(out_path)
    print(f"[multi-view] saved: {out_path}")
    return True

if not FRONT.exists():
    sys.exit(f"Front reference missing: {FRONT}")
if not check_api():
    sys.exit(f"SD Forge API not reachable at {API_BASE}")

ref_b64 = b64(FRONT)
print(f"[multi-view] front ref: {FRONT}")
ok = 0
for v in VIEWS:
    out = FRONT.parent / f"succubus_ref_{v['name']}.png"
    if gen_view(v, ref_b64, out): ok += 1

print(f"\n[multi-view] {ok}/{len(VIEWS)} views generated")
sys.exit(0 if ok == len(VIEWS) else 1)
