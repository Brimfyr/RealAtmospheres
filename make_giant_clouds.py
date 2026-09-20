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

# Real vortices, placed explicitly. The storminess cues above read each planet's own
# map, and these maps carry little discrete structure, so documented features are
# placed by hand at their observed latitudes. Entries: (name, lat, lon, half-width
# and half-height in degrees, storminess, coverage dip). Longitudes are ours to
# choose except on Neptune, where they match the features in its own map.
VORTICES = {
    "Saturn": [
        ("north polar hexagon and cyclone", 78.0,  20.0, 34.0,  7.0, 0.85, 0.35),
        ("south polar vortex",             -87.0,  0.0, 70.0,  4.0, 0.85, 0.35),
        ("Great White Spot (2010-11)",      35.0, 140.0, 46.0,  6.0, 0.70, 0.25),
        ("string of pearls",                40.0, 255.0, 24.0,  3.0, 0.45, 0.20),
    ],
    "Uranus": [
        # Nearly featureless in visible light; the 2006 dark spot is the exception.
        ("Uranus Dark Spot (2006)",         27.0, 200.0, 11.0,  5.5, 0.60, 0.30),
    ],
    "Neptune": [
        ("Great Dark Spot (Voyager 2)",    -20.0, 149.0, 18.0,  9.0, 1.00, 0.55),
        ("Dark Spot 2",                    -56.0, 252.0, 10.0,  6.0, 0.70, 0.40),
    ],
}

# Bright methane clouds ride alongside Neptune's dark spots, which is how Voyager
# found them. Same shape, raising coverage instead of clearing it.
COMPANIONS = {
    "Neptune": [
        ("Great Dark Spot companion", -27.0, 143.0, 10.0, 4.0, 0.30),
        ("Scooter",                   -42.0, 190.0, 12.0, 4.5, 0.25),
    ],
}

def place_vortices(body, storm, cov, w, h):
    """Clear the deck over each documented vortex and raise the storm cloud types;
    then lay any bright companion clouds alongside."""
    lon = (np.arange(w) + 0.5) / w * 360.0
    lat = 90.0 - (np.arange(h) + 0.5) / h * 180.0
    def falloff(vlat, vlon, rx, ry):
        dlon = (lon[None, :] - vlon + 180.0) % 360.0 - 180.0      # wrap east-west
        return np.exp(-((dlon / rx) ** 2 + (lat[:, None] - vlat) ** 2 / ry ** 2))
    for name, vlat, vlon, rx, ry, strength, dip in VORTICES.get(body, []):
        fall = falloff(vlat, vlon, rx, ry)
        storm = np.maximum(storm, strength * fall)
        cov = cov * (1.0 - dip * fall)
        print(f"     {name}: lat {vlat:+.0f}, lon {vlon:.0f}, clears {dip*100:.0f}% of the deck")
    for name, vlat, vlon, rx, ry, lift in COMPANIONS.get(body, []):
        fall = falloff(vlat, vlon, rx, ry)
        cov = np.clip(cov + lift * fall, 0.0, 1.0)
        print(f"     {name}: lat {vlat:+.0f}, lon {vlon:.0f}, brightens by {lift*100:.0f}%")
    return storm, cov

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
    cov_arr = np.asarray(a8, np.float32) / 255.0
    storm, cov_arr = place_vortices(body, storm, cov_arr, W, H)
    a8 = Image.fromarray((np.clip(cov_arr, 0.0, 1.0) * 255).astype(np.uint8))

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
