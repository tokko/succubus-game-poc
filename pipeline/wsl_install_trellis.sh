#!/usr/bin/env bash
# One-shot TRELLIS install for WSL Ubuntu 26.04.
# Idempotent: re-running skips finished steps.
# Run via: wsl -- bash /mnt/d/claude\ projects/succubus-game-poc/pipeline/wsl_install_trellis.sh
set -euo pipefail

HOME_DIR="$HOME"
MINICONDA_DIR="$HOME_DIR/miniconda3"
TRELLIS_DIR="$HOME_DIR/TRELLIS"
LOG="$HOME_DIR/trellis_install.log"

log() { echo "[$(date -u +%H:%M:%S)] $*" | tee -a "$LOG"; }

# ── 1. miniconda ─────────────────────────────────────────────────────────────
if [ ! -d "$MINICONDA_DIR" ]; then
  log "Installing miniconda to $MINICONDA_DIR"
  cd /tmp
  if [ ! -f miniconda.sh ]; then
    wget -q https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh -O miniconda.sh
  fi
  bash miniconda.sh -b -p "$MINICONDA_DIR" >>"$LOG" 2>&1
fi
source "$MINICONDA_DIR/etc/profile.d/conda.sh"
log "conda: $(conda --version)"

# ── 2. TRELLIS repo ──────────────────────────────────────────────────────────
if [ ! -d "$TRELLIS_DIR" ]; then
  log "Cloning microsoft/TRELLIS"
  git clone --depth=1 https://github.com/microsoft/TRELLIS.git "$TRELLIS_DIR" >>"$LOG" 2>&1
fi
cd "$TRELLIS_DIR"

# ── 3. setup.sh ──────────────────────────────────────────────────────────────
# Creates conda env 'trellis' with custom CUDA extensions.
if conda env list | grep -q '^trellis '; then
  log "trellis env already exists — skipping setup.sh"
else
  log "Running TRELLIS setup.sh — this builds 4+ custom CUDA extensions, takes 20-40 min."
  # Non-interactive: feed yes to any prompts.
  yes | bash setup.sh --new-env --basic --xformers --flash-attn --diffoctreerast --spconv --mipgaussian --kaolin --nvdiffrast >>"$LOG" 2>&1 || {
    log "setup.sh FAILED — check $LOG for the failing step"
    exit 1
  }
fi

# ── 4. Verify ────────────────────────────────────────────────────────────────
conda activate trellis
python -c "
import torch
print(f'torch: {torch.__version__}  cuda: {torch.cuda.is_available()}')
from trellis.pipelines import TrellisImageTo3DPipeline
print('TrellisImageTo3DPipeline importable: OK')
" 2>&1 | tee -a "$LOG"

log "SETUP COMPLETE — env 'trellis' ready for inference"
