"""
Bake-off Pipeline C — Hunyuan3D-2mv (multi-view shape + 2.1 paint).

Uses the same conda/torch/ComfyUI env as gen_a_hy3d21.py. The shape model is
hunyuan3d-dit-v2-mv (multi-view DiT) from tencent/Hunyuan3D-2mv, fed 3-4
SD-generated views. Paint pass reuses the 2.1 paintpbr model from gen_a.

Run:
    "D:/ComfyUI/venv/Scripts/python.exe" -u pipeline/gen_c_hy3dmv.py
"""
import os
import sys
import gc
import numpy as np
from pathlib import Path

os.add_dll_directory(r"D:\ComfyUI\venv\lib\site-packages\torch\lib")
import torch

HY3D2_CODE  = r"D:\tmp\Hunyuan3D-2-code"      # has hy3dgen (2.0 namespace) — 2mv loads here
HY3D21_CODE = r"D:\tmp\Hunyuan3D-2.1-code"    # has hy3dshape + hy3dpaint — paint loads here
sys.path.insert(0, HY3D2_CODE)
sys.path.insert(0, os.path.join(HY3D21_CODE, "hy3dpaint"))

os.environ.pop("HY3DGEN_MODELS", None)

import transformers.utils.import_utils as _tu
import transformers.modeling_utils as _mm
_noop = lambda: None
_tu.check_torch_load_is_safe = _noop
_mm.check_torch_load_is_safe = _noop

try:
    from torchvision_fix import apply_fix
    apply_fix()
except Exception: pass

from PIL import Image
import trimesh

PROJECT = Path(__file__).resolve().parent.parent
REFS = PROJECT / "pipeline" / "refs"
VIEW_PATHS = {
    "front": str(REFS / "succubus_ref.png"),
    "left":  str(REFS / "succubus_ref_left.png"),
    "back":  str(REFS / "succubus_ref_back.png"),
    "right": str(REFS / "succubus_ref_right.png"),
}
for k, v in VIEW_PATHS.items():
    if not Path(v).exists():
        print(f"[C] missing view {k}: {v}")
        sys.exit(1)

OUT_DIR = PROJECT / "Assets" / "Characters" / "Bakeoff" / "C_HY3DMV"
OUT_DIR.mkdir(parents=True, exist_ok=True)
SHAPE_GLB    = str(OUT_DIR / "succubus_vmv.glb")
TEXTURED_OBJ = str(OUT_DIR / "succubus_textured_vmv.obj")
TEXTURED_GLB = str(OUT_DIR / "succubus_textured_vmv.glb")
ALBEDO_JPG   = str(OUT_DIR / "succubus_pbr.jpg")
METALLIC_JPG = str(OUT_DIR / "succubus_pbr_metallic.jpg")
ROUGHNESS_JPG= str(OUT_DIR / "succubus_pbr_roughness.jpg")

device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"[C] device={device} VRAM free={torch.cuda.mem_get_info()[0]//(1024**2)} MB")

# ── 1. SHAPE via Hunyuan3D-2mv ─────────────────────────────────────────
SKIP_SHAPE = os.path.exists(SHAPE_GLB) and os.environ.get("FORCE_SHAPE") != "1"
if SKIP_SHAPE:
    print(f"[C] reusing shape {SHAPE_GLB}")
else:
    print("[C] === Stage 1: multi-view SHAPE (hunyuan3d-dit-v2-mv) ===")
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    from hy3dgen.rembg import BackgroundRemover

    pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
        "tencent/Hunyuan3D-2mv",
        subfolder="hunyuan3d-dit-v2-mv",
        use_safetensors=True,
        device=device,
        dtype=torch.float16,
    )
    print("[C] hunyuan3d-dit-v2-mv loaded")

    # Background-remove each view (HY3D expects clean alpha; otherwise produces back-plate).
    rembg = BackgroundRemover()
    view_imgs = {}
    for name, path in VIEW_PATHS.items():
        img = Image.open(path)
        print(f"[C] {name}: pre {img.mode} {img.size}")
        img = rembg(img)
        view_imgs[name] = img
        print(f"[C] {name}: rembg -> {img.mode} {img.size}")

    print("[C] running multi-view inference ...")
    # Per the README, the input is a dict of view name -> PIL Image (or path).
    # Pass PIL since we've already applied rembg.
    mesh_list = pipeline(
        image=view_imgs,
        num_inference_steps=50,
        octree_resolution=512,
        guidance_scale=5.0,
    )
    mesh = mesh_list[0] if isinstance(mesh_list, (list, tuple)) else mesh_list
    mesh.export(SHAPE_GLB)
    print(f"[C] shape: {SHAPE_GLB}  verts={len(mesh.vertices):,}  faces={len(mesh.faces):,}")

    del pipeline
    gc.collect(); torch.cuda.empty_cache()

# ── 2. PAINT (reuse HY3D-2.1 paintpbr-v2-1) ─────────────────────────────
SKIP_PAINT = os.path.exists(TEXTURED_GLB) and os.environ.get("FORCE_PAINT") != "1"
if SKIP_PAINT:
    print(f"[C] reusing paint {TEXTURED_GLB}")
else:
    print("[C] === Stage 2: PBR PAINT (paintpbr-v2-1) ===")
    old_cwd = os.getcwd()
    os.chdir(HY3D21_CODE)
    from textureGenPipeline import Hunyuan3DPaintPipeline, Hunyuan3DPaintConfig

    conf = Hunyuan3DPaintConfig(max_num_view=6, resolution=512)
    conf.realesrgan_ckpt_path = "hy3dpaint/ckpt/RealESRGAN_x4plus.pth"
    conf.multiview_cfg_path   = "hy3dpaint/cfgs/hunyuan-paint-pbr.yaml"
    conf.custom_pipeline      = "hy3dpaint/hunyuanpaintpbr"

    paint = Hunyuan3DPaintPipeline(conf)
    print("[C] paint loaded; painting against the front view")
    paint(
        mesh_path=SHAPE_GLB,
        image_path=VIEW_PATHS["front"],
        output_mesh_path=TEXTURED_OBJ,
    )
    os.chdir(old_cwd)
    del paint; gc.collect(); torch.cuda.empty_cache()

# ── 3. PBR map extraction ──────────────────────────────────────────────
print("[C] === Stage 3: PBR map extract ===")
def extract_pbr(glb):
    saved = []
    try:
        s = trimesh.load(glb)
        m = s.dump(concatenate=True) if isinstance(s, trimesh.Scene) else s
        mat = getattr(m.visual, "material", None)
        if mat is not None:
            if getattr(mat, "baseColorTexture", None) is not None:
                img = mat.baseColorTexture
                if not isinstance(img, Image.Image): img = Image.fromarray(np.array(img))
                img.convert("RGB").save(ALBEDO_JPG, quality=95)
                saved.append(f"albedo {img.size}")
            mr = getattr(mat, "metallicRoughnessTexture", None)
            if mr is not None:
                if not isinstance(mr, Image.Image): mr = Image.fromarray(np.array(mr))
                arr = np.array(mr.convert("RGB"))
                Image.fromarray(arr[:,:,1]).save(ROUGHNESS_JPG, quality=95)
                Image.fromarray(arr[:,:,2]).save(METALLIC_JPG, quality=95)
                saved.append(f"metal+rough {mr.size}")
    except Exception as e:
        print(f"[C] GLB extract err: {e}")

    base = os.path.splitext(glb)[0]
    for label, side, dst, mode in [
        ("albedo (sidecar)", base + ".jpg", ALBEDO_JPG, "RGB"),
        ("metallic (sidecar)", base + "_metallic.jpg", METALLIC_JPG, "L"),
        ("roughness (sidecar)", base + "_roughness.jpg", ROUGHNESS_JPG, "L"),
    ]:
        k = label.split()[0]
        if not any(k in s for s in saved) and os.path.exists(side):
            Image.open(side).convert(mode).save(dst, quality=95)
            saved.append(label)
    print(f"[C] extracted: {', '.join(saved) if saved else 'NOTHING'}")

extract_pbr(TEXTURED_GLB)
print("[C] DONE. Next: rig_c_hy3dmv.py")
