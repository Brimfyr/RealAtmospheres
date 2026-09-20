# -*- coding: utf-8 -*-
"""Build volumetric-cloud masks for Saturn/Uranus/Neptune from their diffuse
equirects (the banding drives where clouds sit). DXT5 equirect, KSA convention.

The mask carries two things the cloud shader reads (GetCoverageAndCloudType in
Core/Shaders/Clouds/CloudFunctions.glsl):

  ALPHA  coverage. The volumetric layer then samples the planet's own Diffuse
         cubemap for COLOUR (VolumetricsColorMap), exactly like Jupiter.
  RED    the cloud TYPE, which is what sets how high a column builds. The shader
         blends the detail texture's two tiles with it and takes the type from the
         green channel of the result, `mix(detailMaps.rg, detailMaps.ba,
         thisRedChannel).g`. Our tile carries a flat pair (see below), so the blend
         is this channel alone: 0 is the tall deck type, 1 the low one.

Height therefore comes off the map, and bright means high, which is both what the
eye expects and what the planets do: bright cloud on a giant is fresh ice carried
up above the deck, so Neptune's white wisps ride high and the darker lanes sit low.
Earlier versions took the type from the detail texture instead, and since that
texture tiles across the planet and its type channel was noise, the decks did vary
in height with nothing visible to tie it to, which reads as no variation at all.
(Jupiter's stock artwork runs the other way, towers over its darker regions. That
is its mask's business and this script does not touch it.)

This script also writes the detail texture itself, GiantCloudDetail.dds, now just
the two endpoints the blend needs: tall at one end, low at the other. Borrowing
Jupiter's lower-deck detail for it put sparse spikes in the type channel, and every
spike raised a tower in the same place on every tile: a regular field of pimples.
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
# Cloud tops sit where coverage + shape - 1 turns positive, so this range is what
# swings them: too narrow and the deck is a slab with a dimpled surface.
LO, HI = 0.35, 0.85
COVER_DRIFT = 0.07     # large-scale drift, so no band is perfectly uniform
COVER_LOCAL = 0.10     # finer drift, so tops undulate within a band as well

# Cloud type, and so column height, read off the map: 0 is the tall type, 1 the low
# one. The gain is how much of that span the map is allowed to use; the noise keeps
# a band from being one flat altitude from end to end.
TYPE_GAIN = 0.85
TYPE_NOISE = 0.15

DETAIL_N = 8           # detail tile resolution; it is a flat pair, so this is plenty

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
    cov = np.clip(cov + COVER_DRIFT * (periodic_fbm(W, 2.0, seed=23)[:H] - 0.5) * 2.0
                      + COVER_LOCAL * (periodic_fbm(W, 1.5, seed=57)[:H] - 0.5) * 2.0, 0.0, 1.0)
    a8 = Image.fromarray((cov * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1))

    # Type 0 is the tall deck and 1 the low one, so invert brightness: the bright
    # cloud rides above the dark lanes, and the relief follows what you can see.
    ctype = np.clip(TYPE_GAIN * (1.0 - luma)
                    + TYPE_NOISE * (periodic_fbm(W, 1.7, seed=91)[:H] - 0.5) * 2.0, 0.0, 1.0)

    out = np.empty((H, W, 4), np.uint8)
    out[..., 0] = (ctype * 255).astype(np.uint8)   # cloud type -> how high a column builds
    out[..., 1] = 255                              # unused by the shader
    out[..., 2] = 255                              # unused by the shader
    out[..., 3] = np.asarray(a8)                   # coverage
    tmp = os.path.join(OUT, "_gcloud_tmp.png")
    Image.fromarray(out, "RGBA").save(tmp)
    subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                    os.path.join(OUT, out_base + ".dds"), tmp], check=True)
    os.remove(tmp)
    bright, dark = luma > np.percentile(luma, 90), luma < np.percentile(luma, 10)
    print(f"{out_base}.dds <- {src_name}: coverage {LO}-{HI}, no storms; "
          f"type {ctype.mean():.2f} mean, {ctype[bright].mean():.2f} over the brightest "
          f"tenth vs {ctype[dark].mean():.2f} over the darkest (lower = taller)")
# The tiled detail texture: RG is tile A (coverage, type), BA is tile B. Coverage
# stays 1 in both, so the global mask owns coverage, as Jupiter's own detail does.
# The two types are the ends of the blend the mask's red channel runs between, flat
# so that nothing about the deck's height comes from a texture that tiles.
detail = np.empty((DETAIL_N, DETAIL_N, 4), np.uint8)
detail[..., 0] = 255                                                   # tile A coverage
detail[..., 1] = 0                                                     # tile A type: tall
detail[..., 2] = 255                                                   # tile B coverage
detail[..., 3] = 255                                                   # tile B type: low
tmp = os.path.join(OUT, "_gdetail_tmp.png")
Image.fromarray(detail, "RGBA").save(tmp)
subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                os.path.join(OUT, "GiantCloudDetail.dds"), tmp], check=True)
os.remove(tmp)
print(f"GiantCloudDetail.dds: {DETAIL_N}px flat pair, tall type at one end, low at the other")
print("giant cloud masks built")
