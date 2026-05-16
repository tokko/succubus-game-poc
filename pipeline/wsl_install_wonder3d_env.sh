#!/usr/bin/env bash
# Fresh conda env for Wonder3D with its 2023-era pinned deps.
# Wonder3D's torch 1.13.1 + cu117 + diffusers 0.19.3 + xformers 0.0.16
# stack doesn't coexist with newer envs.
set -uo pipefail

source ~/miniconda3/etc/profile.d/conda.sh
LOG="$HOME/wonder3d_env.log"
log() { echo "[$(date -u +%H:%M:%S)] $*" | tee -a "$LOG"; }

if conda env list | grep -q "^wonder3d "; then
  log "wonder3d env exists already"
else
  log "creating wonder3d env (python 3.10)"
  conda create -y -n wonder3d python=3.10 >>"$LOG" 2>&1
fi
conda activate wonder3d

# torch 1.13.1 + cu117 (Wonder3D's pinned set)
log "installing torch 1.13.1 + cu117"
pip install torch==1.13.1+cu117 torchvision==0.14.1+cu117 --extra-index-url https://download.pytorch.org/whl/cu117 >>"$LOG" 2>&1 | tail -3

# Rest of requirements
log "installing Wonder3D requirements.txt"
cd ~/Wonder3D
pip install -r requirements.txt >>"$LOG" 2>&1 | tail -10 || log "(requirements.txt partial — checking)"

# pytorch-lightning + omegaconf already in requirements.txt; but instant-nsr-pl has its own:
cd ~/Wonder3D/instant-nsr-pl
if [ -f requirements.txt ]; then
  log "installing instant-nsr-pl requirements"
  pip install -r requirements.txt >>"$LOG" 2>&1 | tail -10 || log "(instant-nsr-pl reqs partial)"
fi

log "smoke checks"
python -c "
import sys
sys.path.insert(0, '/home/andre/Wonder3D')
import torch
print('torch:', torch.__version__, 'cuda:', torch.cuda.is_available())
try: from mvdiffusion.data.single_image_dataset import SingleImageDataset; print('mvdiffusion: ok')
except Exception as e: print('mvdiffusion FAIL:', e)
try: import pytorch_lightning as pl; print('pl:', pl.__version__)
except Exception as e: print('pl FAIL:', e)
try: from diffusers import DiffusionPipeline; print('diffusers: ok')
except Exception as e: print('diffusers FAIL:', e)
" 2>&1 | tee -a "$LOG"

log "DONE"
