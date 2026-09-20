# -*- coding: utf-8 -*-
"""Build the Mars high-altitude cirrus textures from Mars_Upper_Clouds.png.

Conventions (hard-won in the Proxima ice-cloud saga): the layer's textures must
match the body's FIRST cloud layer type -> DXT5 equirect with coverage in ALPHA
(volumetric) and RGBA white+alpha PNG (2D). Coverage is scaled faint (the alpha
IS the opacity), edges pre-blurred, bilinear downscale (no ringing).

Outputs into Mars Test/assets/ (kept for post-update redeploys).
Tuning knobs: ALPHA_SCALE (master opacity), GAMMA (patch-vs-hood balance), BLUR.
"""
import os, subprocess
import numpy as np
from PIL import Image, ImageFilter

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Mars_Upper_Clouds.png")
OUT = os.path.join(HERE, "assets")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"
os.makedirs(OUT, exist_ok=True)

ALPHA_SCALE_2D = 0.24   # 2D billboard opacity (hoods ~0.20)
ALPHA_SCALE_VOL = 0.70  # volumetric coverage (fill = this + shape - 1 -> wider footprint)
GAMMA = 1.1            # >1 suppresses the dim equatorial patches slightly vs hoods
BLUR = 4               # px at source res: soft edges (no hard rims)

src = Image.open(SRC).convert("L")
src = src.filter(ImageFilter.GaussianBlur(radius=BLUR))
luma = np.asarray(src, np.float32) / 255.0
alpha_2d = np.clip((luma ** GAMMA) * ALPHA_SCALE_2D, 0.0, 1.0)
alpha_vol = np.clip((luma ** GAMMA) * ALPHA_SCALE_VOL, 0.0, 1.0)

def write_rgba(alpha01, w, h, path):
    a8 = Image.fromarray((alpha01 * 255).astype(np.uint8))
    a8 = a8.resize((w, h), Image.BILINEAR)
    out = np.empty((h, w, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = np.asarray(a8)
    Image.fromarray(out, "RGBA").save(path)

# volumetric mask: DXT5 equirect 4096x2048, no mips (stock Mars mask convention)
tmp = os.path.join(OUT, "_vol_rgba.png")
write_rgba(alpha_vol, 4096, 2048, tmp)
subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                os.path.join(OUT, "MarsCirrusMaskVolumetric.dds"), tmp], check=True)
os.remove(tmp)

# 2D mask: plain RGBA png
write_rgba(alpha_2d, 2048, 1024, os.path.join(OUT, "MarsCirrusMask2D.png"))

print(f"built: 2D hoods ~{np.percentile(alpha_2d, 99):.2f} opacity; "
      f"volumetric coverage max {alpha_vol.max():.2f}")
