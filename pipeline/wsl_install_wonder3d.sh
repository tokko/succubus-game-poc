#!/usr/bin/env bash
# Install Wonder3D in WSL. Tries to reuse the trellis conda env first
# (both want torch 2.x + cu118 + xformers). If imports clash we'll fork
# a separate env.
set -uo pipefail

source ~/miniconda3/etc/profile.d/conda.sh
conda activate trellis
export CUDA_HOME="$CONDA_PREFIX"
export PATH="$CUDA_HOME/bin:$PATH"

WONDER3D_DIR="$HOME/Wonder3D"
LOG="$HOME/wonder3d_install.log"

log() { echo "[$(date -u +%H:%M:%S)] $*" | tee -a "$LOG"; }

if [ ! -d "$WONDER3D_DIR" ]; then
  log "Cloning Wonder3D"
  git clone --depth=1 https://github.com/xxlong0/Wonder3D.git "$WONDER3D_DIR" >>"$LOG" 2>&1
fi
cd "$WONDER3D_DIR"

log "Installing pip requirements (in trellis env)"
# Use --no-deps where possible to avoid blowing away pinned versions
pip install --no-build-isolation -r requirements.txt 2>&1 | tail -5 | tee -a "$LOG"

log "Trying a smoke import"
python -c "
import sys
sys.path.insert(0, '$WONDER3D_DIR')
try:
    from mvdiffusion.data.single_image_dataset import SingleImageDataset
    print('Wonder3D core importable: OK')
except Exception as e:
    print(f'Wonder3D import FAILED: {type(e).__name__}: {e}')
"

log "DONE"
