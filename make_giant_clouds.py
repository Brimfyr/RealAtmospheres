# -*- coding: utf-8 -*-
"""Build volumetric-cloud COVERAGE masks for Saturn/Uranus/Neptune from their
diffuse equirects (the banding drives where clouds sit). DXT5 equirect, coverage
in ALPHA (KSA convention). The volumetric layer then samples the planet's own
Diffuse cubemap for COLOUR (VolumetricsColorMap), so the clouds are coloured by
the map - exactly like Jupiter. Reuses Jupiter's detail+flowmap textures.
"""
import os, subprocess
import numpy as np
from PIL import Image, ImageFilter
Image.MAX_IMAGE_PIXELS = None

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"

W, H = 4096, 2048
# coverage range: gas giants are fully clouded, so keep a high floor with band
# variation. fill = coverage + shape - 1, so this is deliberately generous.
LO, HI = 0.45, 0.9

JOBS = [
    ("saturn_texture_map___mixed_by_jcp_johncarlo_dc28gow.png", "SaturnCloudsMask"),
    ("uranus_texture_map_by_askaniy_dcmlkco.png", "UranusCloudsMask"),
    ("neptune clouds.png", "NeptuneCloudsMask"),
]

for src_name, out_base in JOBS:
    src = os.path.join(OUT, src_name)
    if not os.path.exists(src):
        print(f"skip: {src_name} not found"); continue
    im = Image.open(src).convert("L").resize((W, H), Image.LANCZOS)
    luma = np.asarray(im, np.float32) / 255.0
    # normalize to full range then map into [LO,HI]; mild blur to avoid speckle
    p2, p98 = np.percentile(luma, 2), np.percentile(luma, 98)
    luma = np.clip((luma - p2) / max(p98 - p2, 1e-6), 0.0, 1.0)
    cov = LO + (HI - LO) * luma
    a8 = Image.fromarray((cov * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1))
    out = np.empty((H, W, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = np.asarray(a8)
    tmp = os.path.join(OUT, "_gcloud_tmp.png")
    Image.fromarray(out, "RGBA").save(tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, out_base + ".dds"), tmp], check=True)
    os.remove(tmp)
    print(f"{out_base}.dds <- {src_name} (coverage {LO}-{HI})")
print("giant cloud coverage masks built")
