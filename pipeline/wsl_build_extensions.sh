#!/usr/bin/env bash
# Build TRELLIS C++/CUDA extensions in the trellis env.
# Run AFTER wsl_install_trellis.sh has created the env + installed torch.
# Adds --no-build-isolation so each extension's build can see the installed torch.
set -uo pipefail   # not -e so a single extension failure doesn't abort the whole run

source ~/miniconda3/etc/profile.d/conda.sh
conda activate trellis

# CUDA toolchain comes from conda, not the system
export CUDA_HOME="$CONDA_PREFIX"
export PATH="$CUDA_HOME/bin:$PATH"
export LD_LIBRARY_PATH="$CUDA_HOME/lib64:${LD_LIBRARY_PATH:-}"

echo "[ext] nvcc: $(nvcc --version | tail -1)"
echo "[ext] torch: $(python -c 'import torch; print(torch.__version__)')"

ok=()
fail=()

# 1. xformers — version table in setup.sh has 2.4.0+cu118 -> 0.0.27.post2
echo "=== xformers ==="
if pip install xformers==0.0.27.post2 --index-url https://download.pytorch.org/whl/cu118 2>&1 | tail -3; then
  python -c "import xformers; print('xformers', xformers.__version__)" && ok+=(xformers) || fail+=(xformers)
else
  fail+=(xformers)
fi

# 2. kaolin — install from NVIDIA wheel index for torch 2.4.0 + cu118
echo "=== kaolin ==="
if pip install kaolin==0.17.0 -f https://nvidia-kaolin.s3.us-east-2.amazonaws.com/torch-2.4.0_cu118.html 2>&1 | tail -3; then
  python -c "import kaolin; print('kaolin', kaolin.__version__)" && ok+=(kaolin) || fail+=(kaolin)
else
  fail+=(kaolin)
fi

# 3. nvdiffrast — local build, must use --no-build-isolation
echo "=== nvdiffrast ==="
if [ ! -d /tmp/extensions/nvdiffrast ]; then
  mkdir -p /tmp/extensions
  git clone --depth=1 https://github.com/NVlabs/nvdiffrast.git /tmp/extensions/nvdiffrast
fi
if pip install --no-build-isolation /tmp/extensions/nvdiffrast 2>&1 | tail -5; then
  python -c "import nvdiffrast; print('nvdiffrast ok')" && ok+=(nvdiffrast) || fail+=(nvdiffrast)
else
  fail+=(nvdiffrast)
fi

# 4. diffoctreerast — TRELLIS-specific octree rasterizer
echo "=== diffoctreerast ==="
if [ ! -d /tmp/extensions/diffoctreerast ]; then
  git clone --depth=1 --recurse-submodules https://github.com/JeffreyXiang/diffoctreerast.git /tmp/extensions/diffoctreerast
fi
if pip install --no-build-isolation /tmp/extensions/diffoctreerast 2>&1 | tail -5; then
  python -c "import diffoctreerast; print('diffoctreerast ok')" && ok+=(diffoctreerast) || fail+=(diffoctreerast)
else
  fail+=(diffoctreerast)
fi

# 5. mip-splatting / diff-gaussian-rasterization
echo "=== diff_gaussian_rasterization ==="
if [ ! -d /tmp/extensions/mip-splatting ]; then
  git clone --depth=1 https://github.com/autonomousvision/mip-splatting.git /tmp/extensions/mip-splatting
fi
if pip install --no-build-isolation /tmp/extensions/mip-splatting/submodules/diff-gaussian-rasterization/ 2>&1 | tail -5; then
  python -c "import diff_gaussian_rasterization; print('diff_gauss ok')" && ok+=(diff_gaussian_rasterization) || fail+=(diff_gaussian_rasterization)
else
  fail+=(diff_gaussian_rasterization)
fi

# 6. flash-attn — optional, large compile. Skip if it fails; TRELLIS works without it.
echo "=== flash-attn (optional) ==="
if pip install --no-build-isolation flash-attn==2.6.3 2>&1 | tail -5; then
  python -c "import flash_attn; print('flash_attn', flash_attn.__version__)" && ok+=(flash-attn) || fail+=(flash-attn)
else
  fail+=(flash-attn)
fi

echo
echo "=== SUMMARY ==="
echo "OK:    ${ok[*]}"
echo "FAIL:  ${fail[*]}"
