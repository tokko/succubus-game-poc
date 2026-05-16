"""
Extract the embedded baseColorTexture from succubus_b.glb (TRELLIS output)
and save as succubus_b_albedo.jpg so Unity can pick it up via a sidecar
material. Run from Windows ComfyUI venv (has trimesh + Pillow).

Usage:
    "D:/ComfyUI/venv/Scripts/python.exe" -u pipeline/extract_trellis_texture.py
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image
import trimesh

PROJECT = Path(__file__).resolve().parent.parent
GLB = PROJECT / "Assets" / "Characters" / "Bakeoff" / "B_TRELLIS" / "succubus_b.glb"
OUT = PROJECT / "Assets" / "Characters" / "Bakeoff" / "B_TRELLIS" / "succubus_b_albedo.jpg"

if not GLB.exists():
    sys.exit(f"GLB not found: {GLB}")

scene = trimesh.load(str(GLB))
mesh = scene.dump(concatenate=True) if isinstance(scene, trimesh.Scene) else scene
mat = getattr(mesh.visual, "material", None)

saved = False
if mat is not None:
    for attr in ("baseColorTexture", "image"):
        img = getattr(mat, attr, None)
        if img is not None:
            if not isinstance(img, Image.Image):
                img = Image.fromarray(np.array(img))
            img.convert("RGB").save(OUT, quality=95)
            print(f"[trellis-extract] {attr}: {img.size} -> {OUT}")
            saved = True
            break

# Fallback: vertex colors baked to a flat atlas isn't worth it — print warning.
if not saved:
    # Check if mesh has vertex colors
    vc = getattr(mesh.visual, "vertex_colors", None)
    if vc is not None and len(vc) > 0:
        print(f"[trellis-extract] WARN: GLB has vertex colors (no baseColorTexture).")
        print(f"[trellis-extract]       Vertex-color UV projection not implemented — slot B will stay gray.")
    else:
        print(f"[trellis-extract] WARN: no texture data found at all.")
    sys.exit(1)

print("[trellis-extract] done")
