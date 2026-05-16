"""
Pipeline D — TRELLIS.2-4B (newer, larger TRELLIS) bake-off slot.

Same conda env (trellis) and same install path as TRELLIS-image-large, just
swaps the model id. ~3-4x slower than -image-large; outputs go to
Assets/Characters/Bakeoff/D_TRELLIS2/.

Run via:
    wsl -- bash -c 'source ~/miniconda3/etc/profile.d/conda.sh && \
                    conda activate trellis && \
                    export CUDA_HOME=$CONDA_PREFIX && \
                    cd ~/TRELLIS && \
                    python /mnt/d/claude\\ projects/succubus-game-poc/pipeline/wsl_run_trellis2.py'
"""
from pathlib import Path
import os
import sys

TRELLIS_REPO = Path.home() / "TRELLIS"
sys.path.insert(0, str(TRELLIS_REPO))

PROJ = Path("/mnt/d/claude projects/succubus-game-poc")
REF  = PROJ / "pipeline" / "refs" / "succubus_ref.png"
OUT  = PROJ / "Assets" / "Characters" / "Bakeoff" / "D_TRELLIS2" / "succubus_d.glb"

if not REF.exists():
    sys.exit(f"Reference not found: {REF}")
OUT.parent.mkdir(parents=True, exist_ok=True)

os.environ.setdefault("ATTN_BACKEND", "xformers")
os.environ.setdefault("SPCONV_ALGO",  "native")

print(f"[trellis2] loading TRELLIS.2-4B pipeline ...")
from trellis.pipelines import TrellisImageTo3DPipeline
from trellis.utils import postprocessing_utils
from PIL import Image

# Try a couple of likely IDs — TRELLIS.2 hasn't standardized its repo name yet
candidates = ["microsoft/TRELLIS.2-4B", "microsoft/TRELLIS-2-4B", "microsoft/TRELLIS2-4B"]
pipeline = None
last_err = None
for rid in candidates:
    try:
        print(f"[trellis2] trying {rid} ...")
        pipeline = TrellisImageTo3DPipeline.from_pretrained(rid)
        print(f"[trellis2] loaded: {rid}")
        break
    except Exception as e:
        last_err = e
        print(f"[trellis2] {rid}: {type(e).__name__}: {e}")
if pipeline is None:
    sys.exit(f"All candidates failed. Last: {last_err}")

pipeline.cuda()
image = Image.open(REF)
print(f"[trellis2] reference: {image.mode} {image.size}")
print(f"[trellis2] running inference ...")
outputs = pipeline.run(image, seed=20260516, formats=["mesh", "gaussian"])

print(f"[trellis2] exporting GLB ...")
glb = postprocessing_utils.to_glb(
    outputs["gaussian"][0],
    outputs["mesh"][0],
    simplify=0.95,
    texture_size=1024,
)
glb.export(str(OUT))
print(f"[trellis2] done -> {OUT} ({OUT.stat().st_size/1e6:.1f} MB)")
