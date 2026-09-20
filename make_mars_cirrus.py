# -*- coding: utf-8 -*-
"""Build the Mars high-altitude cirrus textures from our own SpaceEngine PRO export.

Source: MarsCirrusSource.png, exported from SpaceEngine PRO with the
procedural-texture option OFF (with it on, the exporter writes procedural stand-ins
instead of the real Solar System maps). Distributing derivatives of our own export is
what the PRO EULA allows; see CREDITS.md.

Conventions (hard-won in the Proxima ice-cloud saga): the layer's textures must
match the body's FIRST cloud layer type -> DXT5 equirect with coverage in ALPHA
(volumetric) and RGBA white+alpha PNG (2D). Coverage is scaled faint (the alpha
IS the opacity), edges pre-blurred, bilinear downscale (no ringing).

Two knobs exist for bringing a new export in line with the masks shipped in 1.0.0,
both measured against those masks and both currently inactive, because the export in
use already matches them (percentile drift 0.004, longitude correlation 0.9997):

  ROLL_DEG   longitude offset, if an export is framed differently. Measure it with a
             detrended longitude cross-correlation against the shipped mask.
  TONE_OUT   a monotone quantile mapping onto the shipped mask's tone curve, for an
             export whose background sits at a different level. A scale or gamma
             cannot do this job. None means identity.

Tuning knobs: ALPHA_SCALE (master opacity), GAMMA (patch-vs-hood balance), BLUR.
"""
import os, subprocess
import numpy as np
from PIL import Image, ImageFilter

Image.MAX_IMAGE_PIXELS = None
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets")
SRC = os.path.join(OUT, "MarsCirrusSource.png")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"
os.makedirs(OUT, exist_ok=True)

ALPHA_SCALE_2D = 0.24   # 2D billboard opacity (hoods ~0.20)
ALPHA_SCALE_VOL = 0.70  # volumetric coverage (fill = this + shape - 1 -> wider footprint)
GAMMA = 1.1            # >1 suppresses the dim equatorial patches slightly vs hoods
BLUR_FRAC = 4.0 / 8192  # soft edges, as a fraction of width so it survives a resolution change
ROLL_DEG = 0.0         # this export is already aligned with the shipped masks
TONE_IN = np.linspace(0.0, 1.0, 17)
TONE_OUT = None        # identity; this export is already in the shipped tone space

src = Image.open(SRC).convert("L")
blur = max(1.0, BLUR_FRAC * src.size[0])
src = src.filter(ImageFilter.GaussianBlur(radius=blur))
luma = np.asarray(src, np.float32) / 255.0
if ROLL_DEG:
    luma = np.roll(luma, int(round(ROLL_DEG / 360.0 * luma.shape[1])), axis=1)
if TONE_OUT is not None:
    luma = np.interp(luma, TONE_IN, TONE_OUT).astype(np.float32)

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

print(f"built from {os.path.basename(SRC)} ({src.size[0]}x{src.size[1]}, blur {blur:.1f}px, "
      f"roll {ROLL_DEG:.2f} deg): 2D hoods ~{np.percentile(alpha_2d, 99):.2f} opacity; "
      f"volumetric coverage max {alpha_vol.max():.2f}")
