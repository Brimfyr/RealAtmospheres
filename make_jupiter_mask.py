# -*- coding: utf-8 -*-
"""Build assets/JupiterCloudsMask.dds: the game's own Jupiter cloud mask with its grey and its white swapped.

Jupiter's main cloud deck takes both its coverage and its cloud type from one channel of the game's
JupiterMask.dds (the detail tile is a passthrough). Along the type axis the mask's flat grey, 171/255, is the low
type (LowDensityCloud, 15-77 km) and white the tall one (HighDensityCloud, 15-111 km); anything darker runs down
through the storm types to black, the deep vortices. The mask is the colour map's darkness (correlation -0.85 by
band): the white wisps follow the dark belts and the grey fills the bright zones, so the dark parts of Jupiter
stand tallest. Jupiter's bright zones are its high clouds, and its belts the lower ones.

So grey and white change places, and the vortices stay as they are. The mod sets the two deck types to morph into
each other (InterpolateShapes="true", Payloads.JupiterCloudEdits), which makes the deck's height linear in the mask
value between them, so the swap is a plain mirror:

  zones' grey            -> white: the tall type
  whiter than grey       -> as far toward the low type as it was toward the tall one
  a vortex's dark core   -> untouched
  a vortex's grey disc   -> left low. 552 of the 851 vortices sit in a small grey disc of their own with white
                            around it; swapped by value, each disc would rise into a ring around its vortex. A disc
                            is a connected "grey or darker" region that holds a core and is far smaller than a zone;
                            its treatment fades out over a few texels so no step is left at its edge.
  a vortex in a zone     -> its core untouched, its rim climbing to the deck's level around it instead of stopping
                            at the old grey

The mirror keeps all of the game's relief: every wisp of its mask is in the mod's, upside down (mean height
difference between texels two apart 1.36 km against the game's 1.24 with the types morphing). An earlier build took
everything narrower than 5 texels off the mirrored height and low-passed the rest, to be rid of walls, and left the
deck with half its relief (user, 2026-10-05: "it's too smooth now. There's none of the relief that was there before
that made it look so dynamic"). The walls that build was after came from the game cross-fading the two deck types,
which the morph ended. What the mirror itself adds is narrow: where a ramp from white down to a dark region (the
black poles, a vortex standing in a zone) passes through grey, the mirror turns the passing grey into a ring of the
tall type around the dark region. So only within 10 texels of such a region is the mirrored height opened (features
narrower than 7 texels come off), and nowhere is it low-passed.

The deck's face (optical depth 1 from above), by the colour map's brightness from its darkest tenth to its
brightest, goes from 100 / 99 / 97 / 95 / 91 / 82 / 73 / 71 / 70 / 70 km in the game to 73 / 81 / 85 / 89 / 93 / 97 /
100 / 101 / 101 / 101 km (the engine's cloud chain run on both masks, scratchpad giants/jupiter_mask_v3.py).

Reads the game's file, so it needs the game installed; the result ships with the mod. Needs numpy, scipy, Pillow
and NVIDIA Texture Tools (nvtt_export). BC4, no mips, 8192 x 4096: the game's own format for this mask.
"""
import os
import struct
import subprocess
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

Image.MAX_IMAGE_PIXELS = None

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "assets", "JupiterCloudsMask.dds")
GAME = os.environ.get("KSA_DIR", r"C:\Program Files\Kitten Space Agency")
STOCK = os.path.join(GAME, "Content", "Core", "Textures", "Clouds", "Compressed", "JupiterMask.dds")
NVTT = r"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvtt_export.exe"

GREY = 171 / 255.0          # the mask's flat grey: the low type
LOW = 2.0 / 3.0             # the low type's place on the type axis (the third of four types)
CORE = 0.60                 # below this a texel belongs to a vortex's dark core
DISC = 0.76                 # a vortex's disc: the connected "this grey or darker" region around its core
DISC_MAX = 60000            # texels; larger connected grey regions are zones (the largest disc is 24,880)
FEATHER = 8.0               # texels over which a disc's treatment fades into its surroundings
DARK = 0.62                 # below this a texel is part of a dark region: a pole's cap, a vortex
BY_DARK = 10.0              # texels from a dark region within which the mirrored height is opened
NARROW = 3                  # radius, texels, of that opening: what is narrower than 2 x this + 1 comes off
AROUND = 2.0                # sigma, texels, of the deck's level around a vortex's rim
PAD = 256                   # longitude wraps


def read_bc4(path):
    """a BC4 DDS (DX10 header or not) as floats 0-1"""
    raw = open(path, "rb").read()
    h, w = struct.unpack("<II", raw[12:20])
    dx10 = raw[84:88] == b"DX10"
    if dx10 and struct.unpack("<I", raw[128:132])[0] not in (80, 81):
        sys.exit(f"{path} is not BC4")
    blocks = np.frombuffer(raw, dtype=np.uint8, count=(w // 4) * (h // 4) * 8, offset=148 if dx10 else 128).reshape(h // 4, w // 4, 8)
    r0, r1 = blocks[..., 0].astype(np.float32), blocks[..., 1].astype(np.float32)
    bits = np.zeros(blocks.shape[:2], dtype=np.uint64)
    for k in range(6):
        bits |= blocks[..., 2 + k].astype(np.uint64) << np.uint64(8 * k)
    pal = np.zeros(blocks.shape[:2] + (8,), np.float32)
    pal[..., 0], pal[..., 1] = r0, r1
    eight = r0 > r1
    for i in range(1, 7):
        pal[..., 1 + i] = np.where(eight, ((7 - i) * r0 + i * r1) / 7, pal[..., 1 + i])
    for i in range(1, 5):
        pal[..., 1 + i] = np.where(~eight, ((5 - i) * r0 + i * r1) / 5, pal[..., 1 + i])
    pal[..., 6] = np.where(~eight, 0.0, pal[..., 6])
    pal[..., 7] = np.where(~eight, 255.0, pal[..., 7])
    out = np.zeros((h, w), np.float32)
    for p in range(16):
        idx = ((bits >> np.uint64(3 * p)) & np.uint64(7)).astype(int)
        out[(p // 4)::4, (p % 4)::4] = np.take_along_axis(pal, idx[..., None], -1)[..., 0]
    return out / 255.0


def disk(radius):
    y, x = np.mgrid[-radius:radius + 1, -radius:radius + 1]
    return (x * x + y * y) <= radius * radius + 0.5


def narrow_ridges(mask, sign=1.0):
    """share of the deck's texels standing 12 km (0.148 of the mask's range) above the deck 5 texels to both sides;
    with sign -1, below it: narrow trenches"""
    v = sign * mask
    ridge = ((v - np.roll(v, 5, 0) > 0.148) & (v - np.roll(v, -5, 0) > 0.148)) | \
            ((v - np.roll(v, 5, 1) > 0.148) & (v - np.roll(v, -5, 1) > 0.148))
    ridge[:8] = ridge[-8:] = False                                   # the rolls wrap at the poles
    return float(ridge[mask >= LOW].mean())


def swap(src):
    h, w = src.shape
    m = np.concatenate([src[:, -PAD:], src, src[:, :PAD]], axis=1)

    labels, count = ndi.label(m < DISC)
    size = np.bincount(labels.ravel(), minlength=count + 1)
    has_core = np.zeros(count + 1, bool)
    has_core[np.unique(labels[m < CORE])] = True
    has_core[0] = False
    is_disc = has_core & (size < DISC_MAX)
    disc = is_disc[labels]
    weight = np.clip(1.0 - ndi.distance_transform_edt(~disc) / FEATHER, 0.0, 1.0).astype(np.float32)

    # how far toward the tall type the deck stands: the game's mirrored, the vortices and their discs counted low
    is_deck = m >= GREY
    u = np.clip((m - GREY) / (1.0 - GREY), 0.0, 1.0)
    tall = (np.where(is_deck, 1.0 - u, 0.0) * (1.0 - weight)).astype(np.float32)

    # beside a dark region that is not a disc, a ring of passing grey would stand as a wall: opened there only
    dark = (m < DARK) & ~disc
    by_dark = np.clip(1.0 - ndi.distance_transform_edt(~dark) / BY_DARK, 0.0, 1.0).astype(np.float32)
    tall = tall * (1.0 - by_dark) + ndi.grey_opening(tall, footprint=disk(NARROW), mode="nearest") * by_dark

    # the deck's level around a vortex's rim: a mean over the deck's own texels, so the vortex does not pull it down
    around = ndi.gaussian_filter(tall * is_deck, AROUND, mode="nearest") / \
        np.maximum(ndi.gaussian_filter(is_deck.astype(np.float32), AROUND, mode="nearest"), 1e-3)

    kept_low = LOW + 0.02 * np.clip((m - GREY) / (DISC - GREY), 0.0, 1.0)
    deck = np.maximum(LOW + (1.0 - LOW) * tall, np.where(disc, kept_low, LOW))
    x = np.clip((m - 0.5) / (GREY - 0.5), 0.0, 1.0)
    rim = np.maximum(m + (LOW + (1.0 - LOW) * around - GREY) * x * x * (3.0 - 2.0 * x), m)
    new = np.where(is_deck, deck, np.where(disc, m, rim))
    crop = slice(PAD, PAD + w)
    return new[:, crop], disc[:, crop], weight[:, crop], int(is_disc.sum())


if __name__ == "__main__":
    if not os.path.exists(STOCK):
        sys.exit(f"the game's mask is not at {STOCK} (set KSA_DIR)")
    src = read_bc4(STOCK)
    new, disc, weight, discs = swap(src)
    out = np.clip(np.round(new * 255.0), 0, 255).astype(np.uint8)

    # well inside a zone, and well inside the white of a belt
    inside = disk(8)
    zone = ndi.binary_erosion((src >= GREY) & (src < GREY + 0.002), structure=inside) & (weight == 0)
    white = ndi.binary_erosion(src > 0.995, structure=inside) & (weight == 0)
    core = src < 0.5
    assert out[zone].min() >= 254, "a zone's grey did not become the tall type"
    assert abs(float(new[white].mean()) - LOW) < 0.002, "white did not become the low type"
    assert np.array_equal(out[core], np.round(src[core] * 255).astype(np.uint8)), "a vortex core changed"
    assert float(new[disc & (src >= GREY)].max()) < LOW + 0.05, "a vortex disc rose"
    # the mirror keeps the game's relief and adds no walls of its own: its narrow ridges are the game's narrow
    # trenches, and no more of them (a tenth allowed for the rims and the discs' edges)
    ridges, trenches_game = narrow_ridges(new), narrow_ridges(src, -1.0)
    assert ridges <= 1.1 * trenches_game, f"more narrow ridges ({ridges:.3%}) than the game's mask has narrow trenches ({trenches_game:.3%})"
    relief = float(np.abs(new - np.roll(new, 2, 1))[new >= LOW].mean() / max(np.abs(src - np.roll(src, 2, 1))[src >= GREY].mean(), 1e-9))
    assert relief > 0.9, f"the deck kept only {relief:.0%} of the game's relief"

    tmp = os.path.join(HERE, "assets", "_jupiter_mask_tmp.png")
    Image.fromarray(out, "L").save(tmp)
    subprocess.run([NVTT, "-f", "bc4", "--no-mips", "-o", OUT, tmp], check=True)
    os.remove(tmp)

    back = read_bc4(OUT)
    error = np.abs(back - out / 255.0)
    print(f"{os.path.basename(OUT)} <- the game's JupiterMask.dds, {src.shape[1]} x {src.shape[0]}: "
          f"zones' grey ({zone.mean():.0%} of the texels well inside one) to the tall type, white ({white.mean():.0%}) to the low one; "
          f"{discs} vortex discs ({disc.mean():.1%}) left low, cores untouched; relief {relief:.0%} of the game's, "
          f"narrow ridges {ridges:.3%} of the deck (the game's narrow trenches {trenches_game:.3%}); "
          f"BC4 error mean {error.mean() * 255:.2f}, worst {error.max() * 255:.0f} of 255; {os.path.getsize(OUT) / 1e6:.1f} MB")
