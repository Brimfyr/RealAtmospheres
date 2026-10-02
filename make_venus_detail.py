# -*- coding: utf-8 -*-
"""Build Venus's cloud detail tile, VenusCloudDetail.png: the game's own Earth cumulus tile
(Core/Textures/Clouds/CloudDetail2048.png, which the stock Venus layer uses) with its coverage
lifted from 0-1 to 0.7-1. Its cloud-type channels are untouched, so the deck keeps the stock
variety of flat patches, lumps and taller cells.

Why: the cloud shader fills where coverage + shape - 1 > 0, noise can only open the cloud where
that is below 0.65, and coverage can never exceed this tile's value (GetCoverageAndCloudType in
Core/Shaders/Clouds/CloudFunctions.glsl). The Earth tile has open sky, so on Venus 0.8% of the deck
had no cloud from top to bottom and another 3.6% only a porous base: holes to the bare atmosphere,
hidden while the stock clouds sat low in the thick air. From 0.7 up the base is solid everywhere.
The lift is linear, so a crown's height still follows the tile's whole coverage range: the cloud
types (Payloads.VenusCloudDeckTypes) spread their crowns over 0.7-1.

RG = coverage, type of tile A; BA = coverage, type of tile B (Venus's selector picks B).
"""
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = r"C:\Program Files\Kitten Space Agency\Content\Core\Textures\Clouds\CloudDetail2048.png"
OUT = os.path.join(HERE, "assets", "VenusCloudDetail.png")
FLOOR = 0.7                    # lowest coverage anywhere: above 0.65 the noise cannot open the base

tile = np.asarray(Image.open(SOURCE).convert("RGBA")).astype(np.float64) / 255.0
out = tile.copy()
for ch in (0, 2):                                        # coverage of tiles A and B
    out[..., ch] = FLOOR + (1.0 - FLOOR) * tile[..., ch]
Image.fromarray(np.round(out * 255.0).astype(np.uint8), "RGBA").save(OUT, optimize=True)

b = out[..., 2]
print(f"VenusCloudDetail.png <- CloudDetail2048.png, {tile.shape[1]}x{tile.shape[0]}: tile B coverage "
      f"{b.min():.2f}-{b.max():.2f} (was {tile[..., 2].min():.2f}-{tile[..., 2].max():.2f}), types unchanged")
