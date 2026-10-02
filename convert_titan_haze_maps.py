# -*- coding: utf-8 -*-
"""Convert the user's SE cloud exports into cloud masks (white RGB + alpha coverage, so the layer
Color owns the tint):
  assets/TitanLowerHazeSource.png -> TitanUpperHazeMask*  (380 km lamina)
  assets/TitanUpperHazeSource.png -> TitanLowerHazeMask*  (290 km lamina)
  assets/TritonCloudsSource.png   -> TritonCloudsMask*
  assets/TitanCloudsSource.png    -> TitanCloudsMask*     (methane clouds, two polar caps)
The haze and Triton exports carry coverage in their luminance (p99-normalized per map, which drops
SE's yellow tint); the Titan cloud export carries it in alpha and is used as authored.

2D alpha is deliberately faint for the haze laminae: the distinct limb strips come from the
VOLUMETRIC laminae; the 2D billboard must only add a barely-there roughness to the featureless
yellow globe (user-tuned intent). Titan's methane clouds sit under the haze and never show from
orbit, so their 2D mask keeps the full coverage.

Usage: py convert_titan_haze_maps.py [source names...]   (no names: every job)
"""
import os, subprocess, sys
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"

# per-map alphas (vol, 2d) and where the coverage is: the upper lamina is fainter than the lower
# one (user-tuned); 2D stays near-invisible - strips live in the volumetrics.
# NOTE sources are deliberately CROSSED (user preferred the maps swapped):
# clouds1 feeds the upper lamina, clouds0 the lower.
JOBS = [
    ("TitanUpperHazeSource.png", "TitanUpperHazeMask", 0.55, 0.04, "luma"),
    ("TitanLowerHazeSource.png", "TitanLowerHazeMask", 0.70, 0.05, "luma"),
    # vol alpha = far-field cloud shadow darkness (= coverage) AND volumetric
    # fill footprint. 0.35: user prefers the softer far shadow over wider fill.
    ("TritonCloudsSource.png", "TritonCloudsMask", 0.35, 0.06, "luma"),
    ("TitanCloudsSource.png", "TitanCloudsMask", 1.0, 1.0, "alpha"),
]


def write_rgba(coverage, scale, w, h, path):
    a8 = Image.fromarray((np.clip(coverage * scale, 0, 1) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    out = np.empty((h, w, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = np.asarray(a8)
    Image.fromarray(out, "RGBA").save(path)


only = set(sys.argv[1:])
for src_name, dst_base, alpha_vol, alpha_2d, channel in JOBS:
    if only and src_name not in only:
        continue
    src_path = os.path.join(OUT, src_name)
    if not os.path.exists(src_path):
        print(f"skip: {src_name} not found")
        continue
    src = Image.open(src_path)
    if channel == "alpha":
        coverage = np.asarray(src.convert("RGBA").getchannel("A").resize((4096, 2048), Image.LANCZOS), np.float32) / 255.0
        note = "alpha as authored"
    else:
        luma = np.asarray(src.convert("L").resize((4096, 2048), Image.LANCZOS), np.float32) / 255.0
        p99 = np.percentile(luma, 99)
        coverage = np.clip(luma / max(p99, 1e-6), 0.0, 1.0)
        note = f"p99 {p99:.3f}"

    tmp = os.path.join(OUT, "_conv_tmp.png")
    write_rgba(coverage, alpha_vol, 4096, 2048, tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, dst_base + "Volumetric.dds"), tmp], check=True)
    os.remove(tmp)
    write_rgba(coverage, alpha_2d, 2048, 1024, os.path.join(OUT, dst_base + "2D.png"))
    print(f"{src_name} -> {dst_base}* ({note}, vol {alpha_vol}, 2d {alpha_2d})")
