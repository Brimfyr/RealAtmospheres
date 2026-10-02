"""Fit Titan's shroud colour to Titan's measured colour, with atmosphere_model.py standing in for the game.

1. Target: Titan's colour from Karkoschka's full-disc albedo spectrum (ESO 1995, PDS Atmospheres Node
   volume gbat_0001), as linear sRGB under a white Sun, which is what the game's Sun is.
2. Calibration: the model renders the shroud as it was in the user's screenshot of 2026-09-28 (a flat
   Rayleigh colour, before RaRayleighCoefficients); each channel's screenshot/model ratio corrects the
   model for what it doesn't draw (the haze laminae, the clouds, the blooms).
3. Fit: the haze's single-scattering albedo per channel (TITAN_HAZE_SSA in make_flight_atmospheres.py),
   so the disc takes the target colour at the screenshot's red brightness, which already matched
   Titan's measured albedo. The Rayleigh colours and the absorber's cutoff come from the generator.

Paste the printed TITAN_HAZE_SSA into make_flight_atmospheres.py and rerun it.
Usage: py fit_titan_colour.py [--blue-to-red 0.33] [--preview preview.png]
  --blue-to-red  fit a different blue (photos made with Cassini's 451 nm blue filter show ~0.33)
  --preview      render the screenshot's shroud next to the fitted one, tone-mapped like the game
"""
import argparse
import math
import numpy as np
import atmosphere_model as am
import make_flight_atmospheres as mfa

MEAN_R = 2575.5e3
BOTTOM = MEAN_R + mfa.TITAN_START * 1e3

# The screenshot the model is calibrated on: Titan at a ~35 degree phase angle, exposure 1.25; pixel
# colours across the disc (- dark side, + lit side, in disc radii) and the XML values it was drawn with.
SCREENSHOT = {-0.63: (157, 134, 113), 0.0: (203, 169, 138), 0.63: (220, 185, 153)}
SCREENSHOT_PHASE, SCREENSHOT_EXPOSURE = 35.0, 1.25
SCREENSHOT_RAYLEIGH = np.array([0.131276, 0.173045, 0.374435]) * 1e-3     # per m, one colour at all heights
SCREENSHOT_RAYLEIGH_KEY = 66000.0
SCREENSHOT_MIE = np.array([0.0001256, 0.000502399, 0.00274525]) * 1e-3

KARKOSCHKA = "https://atmos.nmsu.edu/PDS/data/gbat_0001/data/1995low.tab"


def measured_colour():
    """Linear sRGB albedo per channel of Titan and Saturn (both at 5.7 degrees phase), Sun = white."""
    rows = np.array([[float(v) for v in line.split()] for line in mfa.fetch(KARKOSCHKA, "gbat_0001_1995low.tab").splitlines() if line.strip()])
    wl, saturn, titan = rows[:, 0], rows[:, 4], rows[:, 7]

    def lobe(x, mu, s1, s2):
        return np.exp(-0.5 * ((x - mu) / np.where(x < mu, s1, s2)) ** 2)
    sel = (wl >= 380) & (wl <= 780)
    l = wl[sel]
    # CIE 1931 2-degree observer (Wyman, Sloan & Shirley 2013 fit) and a 5772 K Sun
    cmf = np.stack([1.056 * lobe(l, 599.8, 37.9, 31.0) + 0.362 * lobe(l, 442.0, 16.0, 26.7) - 0.065 * lobe(l, 501.1, 20.4, 26.2),
                    0.821 * lobe(l, 568.8, 46.9, 40.5) + 0.286 * lobe(l, 530.9, 16.3, 31.1),
                    1.217 * lobe(l, 437.0, 11.8, 36.0) + 0.681 * lobe(l, 459.0, 26.0, 13.8)])
    sun = 1.0 / ((l * 1e-9) ** 5 * (np.exp(1.4388e-2 / (l * 1e-9 * 5772.0)) - 1.0))
    xyz_to_rgb = np.array([[3.2406, -1.5372, -0.4986], [-0.9689, 1.8758, 0.0415], [0.0557, -0.2040, 1.0570]])
    white = xyz_to_rgb @ (cmf * sun).sum(1)
    return {name: (xyz_to_rgb @ (cmf * sun * a[sel]).sum(1)) / white for name, a in (("Titan", titan), ("Saturn", saturn))}


def cut(alt):
    top, drop = mfa.TITAN_ABSORBER_TOP * 1e3, mfa.TITAN_ABSORBER_DROP * 1e3
    return np.where(alt <= top, 1.0, np.exp(-(alt - top) / drop))


def shroud(rayleigh, rayleigh_key, mie, upper=None):
    """The shroud as the game draws it. upper: the Rayleigh above the absorber's cutoff (None = one colour)."""
    rs, rl, _ = am.shader_table("Titan", "rayleigh")
    ms, ml, _ = am.shader_table("Titan", "mie")
    if upper is None:
        colour = lambda a: np.broadcast_to(rayleigh, a.shape + (3,))
    else:
        colour = lambda a: rayleigh + (upper - rayleigh) * (1 - cut(a))[..., None]
    top = BOTTOM + am.visible_top(rayleigh, rayleigh_key)
    return am.Atmosphere(BOTTOM, top, MEAN_R, colour, am.table_density(rs, rl), mie, am.table_density(ms, ml),
                         mfa.TITAN_MIE_ABSORPTION)


def disc_colours(atmosphere):
    return np.array([atmosphere.disc(SCREENSHOT_PHASE, x) for x in SCREENSHOT])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--blue-to-red", type=float, help="target B/R instead of the measured spectrum's")
    ap.add_argument("--preview", help="write a before/after render to this PNG")
    args = ap.parse_args()
    from scipy.optimize import least_squares

    measured = measured_colour()
    t = measured["Titan"]
    target_gr, target_br = t[1] / t[0], args.blue_to_red or t[2] / t[0]
    print(f"Titan (Karkoschka 1995): linear albedo {np.round(t, 3)}, G/R {t[1] / t[0]:.3f}, B/R {t[2] / t[0]:.3f}"
          f"; Saturn G/R {measured['Saturn'][1] / measured['Saturn'][0]:.3f}, B/R {measured['Saturn'][2] / measured['Saturn'][0]:.3f}")

    shot = np.array([am.hdr_from_display(px, SCREENSHOT_EXPOSURE) for px in SCREENSHOT.values()])
    before = shroud(SCREENSHOT_RAYLEIGH, SCREENSHOT_RAYLEIGH_KEY, SCREENSHOT_MIE)
    k = (shot / disc_colours(before)).mean(0)
    print(f"calibration (screenshot / model) per channel: {np.round(k, 3)}")

    # today's generated values; the absorber for another albedo s scales as (1/s - 1)
    rayleigh = am.generated_rgb("TitanRayleighCoefficients")
    _, _, key = am.shader_table("Titan", "rayleigh")
    upper = rayleigh[1] * (mfa.RENDER_WL / 550.0) ** -mfa.TITAN_UPPER_SLOPE
    absorber_now = am.generated_rgb("TitanMieCoefficients") * mfa.TITAN_MIE_ABSORPTION
    per_unit = absorber_now / (1 / mfa.TITAN_HAZE_SSA - 1)

    def after(ssa):
        return shroud(rayleigh, key, per_unit * (1 / ssa - 1) / mfa.TITAN_MIE_ABSORPTION, upper)

    def residuals(p):
        d = after(1 / (1 + np.exp(-p))).disc(SCREENSHOT_PHASE, 0.0) * k
        return [math.log(d[1] / d[0] / target_gr), math.log(d[2] / d[0] / target_br), math.log(d[0] / shot[1][0])]

    start = np.log(mfa.TITAN_HAZE_SSA / (1 - mfa.TITAN_HAZE_SSA))
    ssa = 1 / (1 + np.exp(-least_squares(residuals, start, diff_step=1e-3, xtol=1e-6).x))
    fitted = after(ssa)
    print(f"\nTITAN_HAZE_SSA = np.array([{ssa[0]:.5f}, {ssa[1]:.5f}, {ssa[2]:.5f}])   (target G/R {target_gr:.3f}, B/R {target_br:.3f})")
    for x, (b, a) in zip(SCREENSHOT, zip(disc_colours(before) * k, disc_colours(fitted) * k)):
        show = lambda v: np.round(am.display(v, SCREENSHOT_EXPOSURE) * 255).astype(int)
        print(f"  disc {x:+.2f}: screenshot {SCREENSHOT[x]} model {show(b)} -> fitted {show(a)} (G/R {a[1] / a[0]:.3f}, B/R {a[2] / a[0]:.3f})")

    if args.preview:
        from PIL import Image
        tiles = [np.round(am.display(a.image(SCREENSHOT_PHASE) * k, SCREENSHOT_EXPOSURE) * 255).astype(np.uint8)
                 for a in (before, fitted)]
        Image.fromarray(np.hstack(tiles)).save(args.preview)
        print(f"wrote {args.preview} (screenshot's shroud | fitted)")


if __name__ == "__main__":
    main()
