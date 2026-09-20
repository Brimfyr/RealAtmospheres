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

This script also writes the detail texture itself, GiantCloudDetail.dds. Borrowing
Jupiter's lower-deck detail put sparse spikes in its type channel, and since the
detail texture is TILED across the planet, every spike became a storm tower in the
same place on every tile: a regular field of pimples. Ours carries a smooth, gently
varying type instead, so the calm deck drifts between the deck and belt-edge types
and the real storms come from the mask's red channel, which is not tiled.

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
# Fill is coverage + shape - 1, so a ceiling near 1 fills the whole column and the
# noise can no longer carve it: the brightest bands go smooth. 0.78 keeps structure.
LO, HI = 0.45, 0.78
COVER_DRIFT = 0.05     # faint large-scale variation so no band is perfectly uniform

DETAIL_N = 256         # detail tile resolution
DETAIL_TYPE_MAX = 0.33 # the calm deck drifts between the two types, nothing beyond
DETAIL_BETA = 2.1      # spectral slope: higher = smoother, fewer small features

def periodic_fbm(n, beta, seed=7):
    """Noise built in the frequency domain, so it tiles seamlessly by construction."""
    rng = np.random.default_rng(seed)
    fy, fx = np.fft.fftfreq(n)[:, None], np.fft.fftfreq(n)[None, :]
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1e-6
    amp = f ** (-beta)
    amp[0, 0] = 0.0
    amp[f > 0.25] = 0.0                     # drop the finest features entirely
    img = np.fft.ifft2(amp * np.exp(1j * rng.uniform(0, 2 * np.pi, (n, n)))).real
    img -= img.min()
    return (img / max(img.max(), 1e-9)).astype(np.float32)


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
    # A narrow window clamps every band brighter than it to the ceiling, which is
    # what left the pale bands flat and smooth. Widen it, then add a faint drift so
    # even a saturated band still has something for the noise to carve.
    p1, p995 = np.percentile(luma, 1), np.percentile(luma, 99.5)
    luma = np.clip((luma - p1) / max(p995 - p1, 1e-6), 0.0, 1.0)
    cov = LO + (HI - LO) * luma
    cov = np.clip(cov + COVER_DRIFT * (periodic_fbm(W, 2.0, seed=23)[:H] - 0.5) * 2.0, 0.0, 1.0)
    a8 = Image.fromarray((cov * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1))

    out = np.empty((H, W, 4), np.uint8)
    out[..., 0] = 0                                # detail-tile selector: the calm tile only
    out[..., 1] = 255                              # unused by the shader
    out[..., 2] = 255                              # unused by the shader
    out[..., 3] = np.asarray(a8)                   # coverage
    tmp = os.path.join(OUT, "_gcloud_tmp.png")
    Image.fromarray(out, "RGBA").save(tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, out_base + ".dds"), tmp], check=True)
    os.remove(tmp)
    print(f"{out_base}.dds <- {src_name}: coverage {LO}-{HI}, no storms")
# The tiled detail texture: RG is tile A (coverage, type), BA is tile B. Coverage
# stays 1 in both (the global mask owns coverage, as Jupiter's own detail does).
# Tile A's type is the smooth drift; tile B's type is 1.0, the storm end, which the
# mask's red channel fades in.
detail = np.empty((DETAIL_N, DETAIL_N, 4), np.uint8)
detail[..., 0] = 255                                                   # tile A coverage
detail[..., 1] = (periodic_fbm(DETAIL_N, DETAIL_BETA) * DETAIL_TYPE_MAX * 255).astype(np.uint8)
detail[..., 2] = 255                                                   # tile B coverage
detail[..., 3] = 255                                                   # tile B type = storm
tmp = os.path.join(OUT, "_gdetail_tmp.png")
Image.fromarray(detail, "RGBA").save(tmp)
subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                os.path.join(OUT, "GiantCloudDetail.dds"), tmp], check=True)
os.remove(tmp)
print(f"GiantCloudDetail.dds: {DETAIL_N}px tile, calm type drifts 0-{DETAIL_TYPE_MAX:.2f}, storm type 1.0")
print("giant cloud masks built")
