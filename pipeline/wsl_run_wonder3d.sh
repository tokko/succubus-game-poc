#!/usr/bin/env bash
# Pipeline F — Wonder3D end-to-end inference.
#   Step 1: test_mvdiffusion_seq.py    image -> 6 views (PNG + normals)
#   Step 2: instant-nsr-pl/launch.py   views   -> reconstructed mesh (OBJ)
#
# Inputs:  /mnt/d/.../pipeline/refs/succubus_ref.png  (front view, RGBA preferred)
# Outputs: /mnt/d/.../Assets/Characters/Bakeoff/F_WONDER3D/succubus_f.obj
set -uo pipefail

source ~/miniconda3/etc/profile.d/conda.sh
conda activate trellis
export CUDA_HOME="$CONDA_PREFIX"
export PATH="$CUDA_HOME/bin:$PATH"

W3D="$HOME/Wonder3D"
cd "$W3D"

PROJ="/mnt/d/claude projects/succubus-game-poc"
REF="$PROJ/pipeline/refs/succubus_ref.png"
OUT_DIR="$PROJ/Assets/Characters/Bakeoff/F_WONDER3D"
mkdir -p "$OUT_DIR"

# Wonder3D test_mvdiffusion expects images under example_images/ — symlink
STAGE_DIR="$W3D/example_images_succubus"
mkdir -p "$STAGE_DIR"
cp -f "$REF" "$STAGE_DIR/succubus_ref.png"

MV_OUT="$W3D/outputs_succubus"
rm -rf "$MV_OUT"   # fresh each run; the recon step is picky about layout

echo "=== Step 1: multi-view diffusion ==="
# Hydra-style config overrides on the command line; quote the list argument.
accelerate launch --config_file 1gpu.yaml test_mvdiffusion_seq.py \
  --config configs/mvdiffusion-joint-ortho-6views.yaml \
  validation_dataset.root_dir="$STAGE_DIR" \
  validation_dataset.filepaths="['succubus_ref.png']" \
  save_dir="$MV_OUT" 2>&1 | tail -20

echo "=== Step 1 outputs ==="
find "$MV_OUT" -name "*.png" | head -20

echo "=== Step 2: NeuS reconstruction ==="
# instant-nsr-pl's launch.py expects dataset.root_dir to be the parent of the
# cropsize-192-cfg1.0 dir created by step 1; pass dataset.scene to pick the
# subfolder named after the input filename.
cd "$W3D/instant-nsr-pl"
python launch.py \
  --config configs/neuralangelo-ortho-wmask.yaml \
  --gpu 0 --train \
  dataset.root_dir="$MV_OUT/cropsize-192-cfg1.0/" \
  dataset.scene=succubus_ref 2>&1 | tail -20

echo "=== Step 2 outputs ==="
RECON_OUT=$(find "$W3D/instant-nsr-pl/exp" -name "*.obj" -newer "$REF" | head -1)
if [ -n "$RECON_OUT" ]; then
  echo "found: $RECON_OUT"
  cp -f "$RECON_OUT" "$OUT_DIR/succubus_f.obj"
  echo "copied to: $OUT_DIR/succubus_f.obj"
else
  echo "WARN: no OBJ produced under instant-nsr-pl/exp"
  ls "$W3D/instant-nsr-pl/exp" 2>/dev/null | head
fi
