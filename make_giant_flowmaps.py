# -*- coding: utf-8 -*-
"""Build 2D-cloud FLOWMAPS for Saturn/Uranus/Neptune from their diffuse equirects,
matching the stock Jupiter flowmap encoding (measured):
  R = 0.5 + horizontal (zonal) flow   (Jupiter range ~0.31..0.85, i.e. +-0.2..0.35)
  G ~= 0.5 (flow is nearly pure horizontal)
  B  = 0
The zonal flow is the planet's own latitudinal band structure: detrended row-mean
brightness -> alternating east/west jets (bright zones vs dark belts shear against
each other), so banded Saturn flows strongly and bland Uranus barely moves. A little
mottled turbulence adds the organic small-scale swirl. Output: bc7 equirect .dds
(+ .png preview). Delete the .dds to fall back to Jupiter's flowmap.
"""
import os, subprocess
import numpy as np
from PIL import Image
Image.MAX_IMAGE_PIXELS = None

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"

W, H = 2048, 1024          # match Jupiter's flowmap resolution
ZONAL_GAIN = 2.6           # band-brightness deviation -> zonal flow
ZONAL_CLAMP = 0.32         # max |flow| around 0.5 (~ Jupiter's extremes)
EDDY_AMP = 0.16            # green turbulent eddies (blobs) in R
LEAN_AMP = 0.12            # west-red / east-green spiral lean on each eddy
LEAN_PX = 6               # dipole half-offset (px)
FINE_AMP = 0.03            # fine mottling everywhere
GTURB_AMP = 0.02           # tiny vertical turbulence in G

# Storms placed per body: each rotates + shows the west-red/east-green lean, like
# Jupiter's Great Red Spot. Coords (u,v) are the storm centre in [0,1] equirect;
# (rx,ry) radii in px, amp dipole strength, rot swirl. Neptune's Great Dark Spot
# is the big soft oval at u~0.41,v~0.61 (read off NeptuneMapSource.png), with a
# fainter companion lower-right.
# Per-planet displacement (ShadowBuilder sets these, calibrated to real peak jets).
# The eddy terms below are amplitudes in flow units, so the distance they actually
# advect is amplitude x displacement: without scaling, Uranus's 18000 km carries its
# eddies nearly 3x as far as Saturn's and shears the deck apart up close.
DISPLACEMENT_KM = {"SaturnFlowmap": 6200.0, "UranusFlowmap": 18000.0, "NeptuneFlowmap": 9000.0}
EDDY_TRAVEL_KM = 1000.0     # how far eddies should drift per loop on every planet
                            # (Saturn already sits at this, so it is the reference and
                            #  only the planets with larger displacements come down)

JOBS = [
    ("SaturnMapSource.png", "SaturnFlowmap", []),
    ("UranusMapSource.png", "UranusFlowmap", []),
    ("NeptuneMapSource.png", "NeptuneFlowmap",
        [(0.415, 0.610, 120.0, 72.0, 0.24, 0.07),     # Great Dark Spot (toned down: gentler swirl)
         (0.700, 0.810, 60.0, 42.0, 0.14, 0.05)]),    # companion dark spot
]

xs, ys = np.meshgrid(np.arange(W), np.arange(H))

def add_storm(R, G, cx, cy, rx, ry, amp, rot):
    """Great-Red-Spot-style feature: west-red/east-green horizontal dipole plus a
    tangential swirl, Gaussian-localized. Handles longitude wrap in x."""
    dx = xs - cx
    dx = (dx + W / 2) % W - W / 2               # shortest wrap distance
    dy = ys - cy
    nx, ny = dx / rx, dy / ry
    fall = np.exp(-(nx * nx + ny * ny))
    R += (-nx * fall) * amp                     # red west (dx<0), green east
    R += rot * (-ny * fall)                     # rotational (adds vertical shear)
    G += rot * (nx * fall)
    return R, G

def smooth1d(x, sigma):
    r = max(1, int(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2); k /= k.sum()
    xp = np.pad(x, r, mode="reflect")          # reflect: no pole edge artifact
    return np.convolve(xp, k, mode="valid")

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def value_noise(scale, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((max(2, H // scale), max(2, W // scale))).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
    return np.asarray(img, np.float32) / 255.0 - 0.5

def blob_field(scale, seed, thresh):
    # isolate the peaks of a value-noise field into distinct round blobs (0..1)
    n = value_noise(scale, seed) + 0.5
    return np.clip((n - thresh) / (1.0 - thresh), 0.0, 1.0)

yy = (np.arange(H) / H)                                    # 0 (N pole) .. 1 (S pole)
zonalTaper = smoothstep(0.06, 0.17, yy) * smoothstep(0.94, 0.83, yy)   # jets fade at poles
eddyTaper2d = np.repeat((smoothstep(0.03, 0.11, yy) *
                         smoothstep(0.97, 0.89, yy))[:, None], W, axis=1)

for src_name, out_base, storms in JOBS:
    src = os.path.join(OUT, src_name)
    if not os.path.exists(src):
        print(f"skip: {src_name} not found"); continue
    luma = np.asarray(Image.open(src).convert("L").resize((W, H), Image.LANCZOS), np.float32) / 255.0

    # eddy amplitudes scaled so they advect EDDY_TRAVEL_KM whatever the planet's
    # displacement; the zonal jets keep their calibrated speeds
    flow_scale = min(1.0, (EDDY_TRAVEL_KM / DISPLACEMENT_KM[out_base]) / EDDY_AMP)
    eddy_amp, lean_amp = EDDY_AMP * flow_scale, LEAN_AMP * flow_scale
    fine_amp, gturb_amp = FINE_AMP * flow_scale, GTURB_AMP * flow_scale
    print(f"  {out_base}: displacement {DISPLACEMENT_KM[out_base]:.0f} km, "
          f"eddy amplitude x{flow_scale:.2f} -> drift ~{eddy_amp * DISPLACEMENT_KM[out_base]:.0f} km")

    # latitudinal band structure -> zonal flow (detrend the pole->equator baseline)
    Lrow = luma.mean(axis=1)
    band_dev = Lrow - smooth1d(Lrow, H * 0.05)
    zf = np.clip(ZONAL_GAIN * band_dev, -ZONAL_CLAMP, ZONAL_CLAMP) * zonalTaper   # per-row
    zf2d = np.repeat(zf[:, None], W, axis=1)

    # eddies cluster where the zonal shear is strongest (belt/zone boundaries)
    shear = np.abs(np.gradient(zf)); shear /= max(shear.max(), 1e-6)
    eddyW = eddyTaper2d * np.repeat((0.45 + 0.9 * shear)[:, None], W, axis=1)
    B0 = blob_field(46, 11, 0.55)
    eddy = -EDDY_AMP * B0 + 0.4 * eddy_amp * blob_field(70, 12, 0.62)   # green-dominant
    eddy += LEAN_AMP * (np.roll(B0, -LEAN_PX, axis=1) - np.roll(B0, LEAN_PX, axis=1))  # red W / green E
    fine = FINE_AMP * value_noise(10, 21)

    R = 0.5 + zf2d + eddy * eddyW + fine
    G = 0.5 + GTURB_AMP * value_noise(24, 3)

    # storms (Neptune's Great Dark Spot + companion): rotational + west-red/east-green
    for u, v, rx, ry, amp, rot in storms:
        R, G = add_storm(R, G, u * W, v * H, rx, ry, amp, rot)

    R = np.clip(R, 0.0, 1.0); G = np.clip(G, 0.0, 1.0)
    B = np.zeros((H, W), np.float32)
    rgb = (np.stack([R, G, B], axis=2) * 255).astype(np.uint8)

    Image.fromarray(rgb, "RGB").save(os.path.join(OUT, out_base + ".png"))
    tmp = os.path.join(OUT, "_flow_tmp.png"); Image.fromarray(rgb, "RGB").save(tmp)
    # BC5 (RG), matching stock Jupiter2DFlowmap.dds (FourCC 'BC5U'). The game's DDS
    # loader (GLI) supports the legacy BC formats but NOT DX10/BC7 -> boot crash.
    # BC5 is 2-channel: R=horizontal, G=vertical flow, exactly what the shader reads.
    # mips ON to match stock (its 2D <FlowMap> is MipMaps=true).
    subprocess.run([NVTT, "-f", "bc5", "-o",
                    os.path.join(OUT, out_base + ".dds"), tmp], check=True)
    os.remove(tmp)
    print(f"{out_base}.dds <- {src_name}  (flow +-{np.abs(zf).max():.2f}, mean|dev| {np.abs(band_dev).mean():.3f})")
print("giant flowmaps built")
