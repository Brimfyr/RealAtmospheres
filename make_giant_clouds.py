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

DETAIL_N = 256         # detail tile resolution
DETAIL_TYPE_MAX = 0.33 # calm deck drifts between type 0 and ~1/3, never into storms
DETAIL_BETA = 2.1      # spectral slope: higher = smoother, fewer small features

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
# The tiled detail texture: RG is tile A (coverage, type), BA is tile B. Coverage
# stays 1 in both (the global mask owns coverage, as Jupiter's own detail does).
# Tile A's type is the smooth drift; tile B's type is 1.0, the storm end, which the
# mask's red channel fades in.
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

# Jupiter's deep deck. Stock's lower-deck mask covers ~3% of the planet, so the
# storm holes in its main deck open onto the 1-bar mesh. This one has no holes:
# coverage stays well above the shader's 0.01 cutoff everywhere, with a gentle
# large-scale drift so it reads as cloud rather than a plate. Red is 0, which
# selects the detail tile whose green channel varies, so the deck still picks up
# both of that layer's cloud types.
DEEP_N = 1024
DEEP_LO, DEEP_HI = 0.55, 0.82
deep_cov = periodic_fbm(DEEP_N, 2.3, seed=11)
deep = np.empty((DEEP_N // 2, DEEP_N, 4), np.uint8)
deep[..., 0] = 0
deep[..., 1] = 255
deep[..., 2] = 255
deep[..., 3] = ((DEEP_LO + (DEEP_HI - DEEP_LO) * deep_cov[:DEEP_N // 2]) * 255).astype(np.uint8)
tmp = os.path.join(OUT, "_gdeep_tmp.png")
Image.fromarray(deep, "RGBA").save(tmp)
subprocess.run([NVTT, "-f", "bc3", "--no-mips", "-o",
                os.path.join(OUT, "JupiterDeepDeckMask.dds"), tmp], check=True)
os.remove(tmp)
print(f"JupiterDeepDeckMask.dds: coverage {DEEP_LO}-{DEEP_HI}, no holes")

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
