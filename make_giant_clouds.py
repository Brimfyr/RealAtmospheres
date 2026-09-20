# -*- coding: utf-8 -*-
"""Build volumetric-cloud masks for Saturn/Uranus/Neptune from their diffuse
equirects (the banding drives where clouds sit). DXT5 equirect, KSA convention.

The mask carries two things the cloud shader reads (GetCoverageAndCloudType in
Core/Shaders/Clouds/CloudFunctions.glsl):

  ALPHA  coverage. The volumetric layer then samples the planet's own Diffuse
         cubemap for COLOUR (VolumetricsColorMap), exactly like Jupiter.
  RED    the detail-tile selector. The shader blends the detail texture's two
         tiles with it, `mix(detailMaps.rg, detailMaps.ba, thisRedChannel)`, and
         takes the cloud TYPE from the green channel of the result. Writing a flat
         1.0 here (as the first version did) pins every pixel to one tile and one
         type, which is why those decks came out uniform.

Storminess is where the stormy types get selected. Two physical cues, both read
off the planet's own map:

  shear    storms and vortices live in the shear between zonal jets, so the
           latitudinal gradient of the zonal-mean brightness marks the belts'
           edges.
  anomaly  a pixel that departs from its own latitude's mean is already a
           discrete feature on the map: the Great Dark Spot, Saturn's storms.

Tuning: STORM_SHEAR / STORM_SPOT weight the two cues, STORM_GAMMA sets how much
of the planet stays calm (higher = calmer), STORM_BLUR_DEG their smallest scale.
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

STORM_SHEAR = 0.85     # weight of the jet-shear cue
STORM_SPOT = 0.60      # weight of the discrete-feature cue
STORM_GAMMA = 1.40     # >1 keeps more of the planet on the calm deck
STORM_BLUR_DEG = 1.5   # storms are regional, not pixel-scale
# Absolute references, so a bland planet stays bland. Normalising each cue by the
# planet's own maximum made Uranus the stormiest of the three, which is backwards.
SHEAR_REF = 0.015      # zonal-mean gradient that counts as a full-strength jet edge
ANOM_REF = 0.30        # departure from the latitude mean that counts as a feature
# The cues cannot tell a real feature from a map artefact: Uranus's map carries a
# large non-zonal brightness variation that survives blurring and is not weather.
# These weights carry what we know about the planets themselves.
ACTIVITY = {"Saturn": 0.70, "Uranus": 0.25, "Neptune": 1.00}
# Gain chosen so Neptune's Great Dark Spot reaches the storm-centre type, Saturn's
# belt edges reach the storm-edge type, and Uranus stays on the deck.
STORM_GAIN = 2.5

JOBS = [
    ("SaturnMapSource.png", "SaturnCloudsMask"),
    ("UranusMapSource.png", "UranusCloudsMask"),
    ("NeptuneMapSource.png", "NeptuneCloudsMask"),
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

    body = out_base.replace("CloudsMask", "")
    zonal = luma.mean(axis=1, keepdims=True)                     # the band profile
    shear = np.clip(np.abs(np.gradient(zonal[:, 0])) / SHEAR_REF, 0.0, 1.0)
    shear = np.repeat(shear[:, None], W, axis=1)
    anomaly = np.clip(np.abs(luma - zonal) / ANOM_REF, 0.0, 1.0)
    storm = np.clip(STORM_SHEAR * shear + STORM_SPOT * anomaly, 0.0, 1.0) ** STORM_GAMMA
    storm = np.clip(storm * ACTIVITY[body] * STORM_GAIN, 0.0, 1.0)
    storm = np.asarray(Image.fromarray((storm * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(STORM_BLUR_DEG / 360.0 * W)), np.float32) / 255.0

    out = np.empty((H, W, 4), np.uint8)
    out[..., 0] = (storm * 255).astype(np.uint8)   # detail-tile selector -> cloud type
    out[..., 1] = 255                              # unused by the shader
    out[..., 2] = 255                              # unused by the shader
    out[..., 3] = np.asarray(a8)                   # coverage
    tmp = os.path.join(OUT, "_gcloud_tmp.png")
    Image.fromarray(out, "RGBA").save(tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, out_base + ".dds"), tmp], check=True)
    os.remove(tmp)
    print(f"{out_base}.dds <- {src_name}: coverage {LO}-{HI}, activity {ACTIVITY[body]}, "
          f"storminess mean {storm.mean():.2f} p90 {np.percentile(storm, 90):.2f} "
          f"p99 {np.percentile(storm, 99):.2f} (0 = calm deck, 1 = storm type)")
print("giant cloud masks built")
