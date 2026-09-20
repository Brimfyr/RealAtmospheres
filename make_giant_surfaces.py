# -*- coding: utf-8 -*-
"""Build higher-res Diffuse cubemaps for the gas/ice giants from the user's
equirect maps (2048/face BC7), replacing the low stock maps (Saturn 1024,
Uranus/Neptune 512). Gas giants use a Diffuse cubemap ONLY. Reuses the proven
Proxima cubemap pipeline via build_maps.
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "assets")
# build_maps.py ships with the Proxima mod's _build tools. Accept the current location or the
# legacy Content\ one, so moving that folder does not break every converter.
def _find_build():
    root = r"C:\Users\gunsh\Documents\Kitten Space Agency"
    for c in (os.path.join(root, "Proxima Centauri", "_build"),
              os.path.join(root, "Content", "Proxima Centauri", "_build")):
        if os.path.isfile(os.path.join(c, "build_maps.py")):
            return c
    raise SystemExit("build_maps.py not found - fix _find_build() in " + __file__)
BUILD = _find_build()
RES = 2048

JOBS = [
    ("SaturnMapSource.png", "SaturnDiffuse"),
    ("UranusMapSource.png", "UranusDiffuse"),
    ("NeptuneMapSource.png", "NeptuneDiffuse"),
]

sys.argv = ["build_maps.py", "__skip_all_builds__"]
sys.path.insert(0, BUILD)
import build_maps as bm
from PIL import Image
Image.MAX_IMAGE_PIXELS = None

bm.OUT = ASSETS
for src_name, out_name in JOBS:
    src = os.path.join(ASSETS, src_name)
    if not os.path.exists(src):
        print(f"skip: {src_name} not found"); continue
    img = Image.open(src).convert("RGB")
    import numpy as np
    arr = np.asarray(img, np.float32)
    bm.slot_bc(out_name.split("_")[0], arr, RES, "bc7", out_name)
    print(f"{out_name} <- {src_name}")
print("giant diffuse cubemaps built (2048/face, BC7)")
