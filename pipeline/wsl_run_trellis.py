"""
TRELLIS-image-large inference for Pipeline B of the bake-off.

Runs INSIDE WSL's `trellis` conda env. Invoked from the project root
on the Windows side via:

  wsl -- bash -c 'source ~/miniconda3/etc/profile.d/conda.sh && \
                  conda activate trellis && \
                  python /mnt/d/claude\ projects/succubus-game-poc/pipeline/wsl_run_trellis.py'

Reads:  /mnt/d/claude projects/succubus-game-poc/pipeline/refs/succubus_ref.png
Writes: /mnt/d/claude projects/succubus-game-poc/Assets/Characters/Bakeoff/B_TRELLIS/succubus_b.glb
"""
from pathlib import Path
import os
import sys

# The trellis package lives at ~/TRELLIS/trellis (not pip-installed) — put the
# repo root on sys.path so `from trellis.pipelines import ...` works regardless
# of cwd or where this script was launched from.
TRELLIS_REPO = Path.home() / "TRELLIS"
sys.path.insert(0, str(TRELLIS_REPO))

# WSL sees the Windows project under /mnt/d
PROJ = Path("/mnt/d/claude projects/succubus-game-poc")
REF  = PROJ / "pipeline" / "refs" / "succubus_ref.png"
OUT  = PROJ / "Assets" / "Characters" / "Bakeoff" / "B_TRELLIS" / "succubus_b.glb"

if not REF.exists():
    sys.exit(f"Reference not found: {REF}")
OUT.parent.mkdir(parents=True, exist_ok=True)

# Suppress some unused-extension noise
os.environ.setdefault("ATTN_BACKEND",  "xformers")
os.environ.setdefault("SPCONV_ALGO",   "native")

print(f"[trellis] loading pipeline ...")
from trellis.pipelines import TrellisImageTo3DPipeline
from trellis.utils import postprocessing_utils
from PIL import Image

pipeline = TrellisImageTo3DPipeline.from_pretrained("microsoft/TRELLIS-image-large")
pipeline.cuda()

image = Image.open(REF)
print(f"[trellis] reference loaded: mode={image.mode} size={image.size}")

print(f"[trellis] running ...")
outputs = pipeline.run(
    image,
    seed=20260515,
    formats=["mesh", "gaussian"],
)

print(f"[trellis] exporting GLB ...")
glb = postprocessing_utils.to_glb(
    outputs["gaussian"][0],
    outputs["mesh"][0],
    simplify=0.95,
    texture_size=1024,
)
glb.export(str(OUT))
print(f"[trellis] done -> {OUT}  ({OUT.stat().st_size/1e6:.1f} MB)")
