# -*- coding: utf-8 -*-
"""Convert the user's SE cloud exports into the Titan haze masks:
  assets/TitanLowerHazeSource.png -> TitanUpperHazeMask*  (290 km lamina)
  assets/TitanUpperHazeSource.png -> TitanLowerHazeMask*  (230 km lamina)
Luminance -> coverage (drops SE's yellow tint; output is white RGB + alpha so
the layer Color owns the tint), p99-normalized per map.

2D alpha is deliberately faint: the distinct limb strips come from the
VOLUMETRIC laminae; the 2D billboard must only add a barely-there roughness to
the featureless yellow globe (user-tuned intent).
"""
import os, subprocess
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"

# per-map alphas (vol, 2d): the upper lamina is fainter than the lower one
# (user-tuned); 2D stays near-invisible - strips live in the volumetrics.
# NOTE sources are deliberately CROSSED (user preferred the maps swapped):
# clouds1 feeds the upper lamina, clouds0 the lower.
JOBS = [
    ("TitanUpperHazeSource.png", "TitanUpperHazeMask", 0.55, 0.04),
    ("TitanLowerHazeSource.png", "TitanLowerHazeMask", 0.70, 0.05),
    # vol alpha = far-field cloud shadow darkness (= coverage) AND volumetric
    # fill footprint. 0.35: user prefers the softer far shadow over wider fill.
    ("TritonCloudsSource.png", "TritonCloudsMask", 0.35, 0.06),
]


def write_rgba(luma, scale, w, h, path):
    a8 = Image.fromarray((np.clip(luma * scale, 0, 1) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    out = np.empty((h, w, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = np.asarray(a8)
    Image.fromarray(out, "RGBA").save(path)


for src_name, dst_base, alpha_vol, alpha_2d in JOBS:
    src_path = os.path.join(OUT, src_name)
    if not os.path.exists(src_path):
        print(f"skip: {src_name} not found")
        continue
    src = Image.open(src_path).convert("L").resize((4096, 2048), Image.LANCZOS)
    luma = np.asarray(src, np.float32) / 255.0
    p99 = np.percentile(luma, 99)
    luma = np.clip(luma / max(p99, 1e-6), 0.0, 1.0)

    tmp = os.path.join(OUT, "_conv_tmp.png")
    write_rgba(luma, alpha_vol, 4096, 2048, tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, dst_base + "Volumetric.dds"), tmp], check=True)
    os.remove(tmp)
    write_rgba(luma, alpha_2d, 2048, 1024, os.path.join(OUT, dst_base + "2D.png"))
    print(f"{src_name} -> {dst_base}* (p99 {p99:.3f}, vol {alpha_vol}, 2d {alpha_2d})")
