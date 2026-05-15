"""
Bake-off Pipeline A — Hunyuan3D-2.1 (shape dit-v2-1 + paintpbr-v2-1).

Adapted from unity-game-poc/pipeline/gen_v21.py to write into
Assets/Characters/Bakeoff/A_HY3D21/. The 2.1 source tree at
D:\\tmp\\Hunyuan3D-2.1-code already has the gen-4 patches applied
(verified 2026-05-15: trust_remote_code=True, simplify face_count= kwarg,
bpy try/except, mesh_inpaint_processor try/except).

Weights come from the HuggingFace cache at
C:\\Users\\andre\\.cache\\huggingface\\hub\\models--tencent--Hunyuan3D-2.1
(6.41 GB, already pulled). HY3DGEN_MODELS env var is intentionally NOT set
so the library falls through to HF hub (cache hits).

Run with:
    "D:/ComfyUI/venv/Scripts/python.exe" -u pipeline/gen_a_hy3d21.py
"""
import os
import sys
import gc
import numpy as np
from pathlib import Path

os.add_dll_directory(r"D:\ComfyUI\venv\lib\site-packages\torch\lib")
import torch

HY3D21_CODE = r"D:\tmp\Hunyuan3D-2.1-code"
sys.path.insert(0, os.path.join(HY3D21_CODE, "hy3dshape"))
sys.path.insert(0, os.path.join(HY3D21_CODE, "hy3dpaint"))
sys.path.insert(0, HY3D21_CODE)

# HY3DGEN_MODELS intentionally unset — fall through to HF hub (cache hit on
# tencent/Hunyuan3D-2.1 in ~/.cache/huggingface/hub).
os.environ.pop("HY3DGEN_MODELS", None)

import transformers.utils.import_utils as _tu
import transformers.modeling_utils as _mm
_noop = lambda: None
_tu.check_torch_load_is_safe = _noop
_mm.check_torch_load_is_safe = _noop

try:
    from torchvision_fix import apply_fix
    apply_fix()
    print("[A] torchvision compatibility fix applied")
except Exception as e:
    print(f"[A] torchvision fix not applied: {e}")

from PIL import Image
import trimesh

PROJECT_ROOT = Path(__file__).resolve().parent.parent
REF_IMAGE = str(PROJECT_ROOT / "pipeline" / "refs" / "succubus_ref.png")
OUT_DIR   = str(PROJECT_ROOT / "Assets" / "Characters" / "Bakeoff" / "A_HY3D21")
Path(OUT_DIR).mkdir(parents=True, exist_ok=True)

SHAPE_GLB     = os.path.join(OUT_DIR, "succubus_v21.glb")
TEXTURED_OBJ  = os.path.join(OUT_DIR, "succubus_textured_v21.obj")
TEXTURED_GLB  = os.path.join(OUT_DIR, "succubus_textured_v21.glb")
ALBEDO_JPG    = os.path.join(OUT_DIR, "succubus_pbr.jpg")
METALLIC_JPG  = os.path.join(OUT_DIR, "succubus_pbr_metallic.jpg")
ROUGHNESS_JPG = os.path.join(OUT_DIR, "succubus_pbr_roughness.jpg")

device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"[A] device={device} GPU={torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'none'}")
print(f"[A] VRAM free={torch.cuda.mem_get_info()[0]//(1024**2)} MB / {torch.cuda.mem_get_info()[1]//(1024**2)} MB")
print(f"[A] REF_IMAGE: {REF_IMAGE}")
print(f"[A] OUT_DIR  : {OUT_DIR}")

# ── SHAPE ────────────────────────────────────────────────────────────────────
SKIP_SHAPE = os.path.exists(SHAPE_GLB) and os.environ.get("FORCE_SHAPE") != "1"
if SKIP_SHAPE:
    print(f"\n[A] === Stage 1: SHAPE reusing {SHAPE_GLB} (FORCE_SHAPE=1 to regen) ===")
else:
    print("\n[A] === Stage 1: SHAPE (dit-v2-1) ===")
    from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline
    from hy3dshape.rembg import BackgroundRemover

    shape_pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
        "tencent/Hunyuan3D-2.1",
        subfolder="hunyuan3d-dit-v2-1",
        use_safetensors=False,
        variant="fp16",
        device=device,
        dtype=torch.float16,
    )
    print("[A] dit-v2-1 loaded")

    image = Image.open(REF_IMAGE).convert("RGBA")
    if image.mode == "RGB":
        rembg = BackgroundRemover()
        image = rembg(image)

    SHAPE_OCTREE_RES = 512
    SHAPE_INFER_STEPS = 50
    SHAPE_GUIDANCE = 5.0
    print(f"[A] octree={SHAPE_OCTREE_RES} steps={SHAPE_INFER_STEPS} cfg={SHAPE_GUIDANCE}")
    mesh_list = shape_pipeline(
        image=image,
        num_inference_steps=SHAPE_INFER_STEPS,
        octree_resolution=SHAPE_OCTREE_RES,
        guidance_scale=SHAPE_GUIDANCE,
    )
    mesh = mesh_list[0] if isinstance(mesh_list, (list, tuple)) else mesh_list
    mesh.export(SHAPE_GLB)
    print(f"[A] Shape: {SHAPE_GLB}  verts={len(mesh.vertices):,}  faces={len(mesh.faces):,}")

    # Ground-platform artifact cleanup
    try:
        _s = trimesh.load(SHAPE_GLB)
        _m = _s.dump(concatenate=True) if isinstance(_s, trimesh.Scene) else _s
        _comps = _m.split(only_watertight=False)
        if len(_comps) > 1:
            _h = max(1e-6, _m.bounds[1][2] - _m.bounds[0][2])
            _keep = []
            for _c in _comps:
                _zext = _c.bounds[1][2] - _c.bounds[0][2]
                _xyext = max(1e-6, max(_c.bounds[1][:2] - _c.bounds[0][:2]))
                _zmid = (_c.bounds[0][2] + _c.bounds[1][2]) * 0.5
                if _zext < 0.04 * _xyext and _zmid < 0.06 * _h:
                    print(f"[A] Ground artifact removed: {len(_c.vertices):,} verts")
                else:
                    _keep.append(_c)
            if len(_keep) < len(_comps) and _keep:
                _clean = trimesh.util.concatenate(_keep)
                _clean.export(SHAPE_GLB)
                print(f"[A] Shape cleaned: {len(_m.vertices):,} -> {len(_clean.vertices):,} verts")
    except Exception as _e:
        print(f"[A] Ground cleanup skipped: {_e}")

    del shape_pipeline
    gc.collect()
    torch.cuda.empty_cache()
    print(f"[A] VRAM after shape unload: {torch.cuda.mem_get_info()[0]//(1024**2)} MB free")

# ── PAINT ────────────────────────────────────────────────────────────────────
SKIP_PAINT = os.path.exists(TEXTURED_GLB) and os.environ.get("FORCE_PAINT") != "1"
if SKIP_PAINT:
    print(f"\n[A] === Stage 2: PAINT reusing {TEXTURED_GLB} (FORCE_PAINT=1 to regen) ===")
else:
    print("\n[A] === Stage 2: PBR PAINT (paintpbr-v2-1) ===")
    old_cwd = os.getcwd()
    os.chdir(HY3D21_CODE)

    from textureGenPipeline import Hunyuan3DPaintPipeline, Hunyuan3DPaintConfig

    # 6 views / 512 px: verified safe on RTX 3090 (24 GB). 9v/768px OOMs.
    PAINT_VIEWS = 6
    PAINT_RES = 512
    conf = Hunyuan3DPaintConfig(max_num_view=PAINT_VIEWS, resolution=PAINT_RES)
    conf.realesrgan_ckpt_path = "hy3dpaint/ckpt/RealESRGAN_x4plus.pth"
    conf.multiview_cfg_path = "hy3dpaint/cfgs/hunyuan-paint-pbr.yaml"
    conf.custom_pipeline = "hy3dpaint/hunyuanpaintpbr"

    print(f"[A] paint cfg: views={PAINT_VIEWS} res={PAINT_RES}")
    paint_pipeline = Hunyuan3DPaintPipeline(conf)
    print("[A] paint pipeline loaded")

    print(f"[A] Painting {SHAPE_GLB} -> {TEXTURED_OBJ}")
    result_path = paint_pipeline(
        mesh_path=SHAPE_GLB,
        image_path=REF_IMAGE,
        output_mesh_path=TEXTURED_OBJ,
    )
    print(f"[A] Painted OBJ: {result_path}")
    print(f"[A] Painted GLB: {TEXTURED_GLB}")

    os.chdir(old_cwd)
    del paint_pipeline
    gc.collect()
    torch.cuda.empty_cache()
    print(f"[A] VRAM after paint unload: {torch.cuda.mem_get_info()[0]//(1024**2)} MB free")

# ── PBR MAP EXTRACTION ───────────────────────────────────────────────────────
print("\n[A] === Stage 3: PBR map extraction ===")

def extract_pbr(glb_path):
    saved = []
    try:
        scene = trimesh.load(glb_path)
        mesh = scene.dump(concatenate=True) if isinstance(scene, trimesh.Scene) else scene
        mat = getattr(mesh.visual, "material", None)
        if mat is not None and hasattr(mat, "baseColorTexture") and mat.baseColorTexture is not None:
            img = mat.baseColorTexture
            if not isinstance(img, Image.Image):
                img = Image.fromarray(np.array(img))
            img.convert("RGB").save(ALBEDO_JPG, quality=95)
            saved.append(f"albedo {img.size}")
        if mat is not None and hasattr(mat, "metallicRoughnessTexture") and mat.metallicRoughnessTexture is not None:
            mr = mat.metallicRoughnessTexture
            if not isinstance(mr, Image.Image):
                mr = Image.fromarray(np.array(mr))
            mr_arr = np.array(mr.convert("RGB"))
            Image.fromarray(mr_arr[:, :, 1]).save(ROUGHNESS_JPG, quality=95)
            Image.fromarray(mr_arr[:, :, 2]).save(METALLIC_JPG, quality=95)
            saved.append(f"metal+rough {mr.size}")
    except Exception as e:
        print(f"[A] GLB extract error: {e}")

    base = os.path.splitext(glb_path)[0]
    for label, side, dst, mode in [
        ("albedo (sidecar)", base + ".jpg", ALBEDO_JPG, "RGB"),
        ("metallic (sidecar)", base + "_metallic.jpg", METALLIC_JPG, "L"),
        ("roughness (sidecar)", base + "_roughness.jpg", ROUGHNESS_JPG, "L"),
    ]:
        key = label.split()[0]
        if not any(key in s for s in saved) and os.path.exists(side):
            Image.open(side).convert(mode).save(dst, quality=95)
            saved.append(label)

    print(f"[A] extracted: {', '.join(saved) if saved else 'NOTHING'}")

extract_pbr(TEXTURED_GLB)
print("\n[A] DONE. Next: rig_and_animate adapted for A_HY3D21 paths.")
