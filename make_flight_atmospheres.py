# -*- coding: utf-8 -*-
"""Build Real Atmospheres' atmosphere tables: AtmosphereTables.Generated.cs.

KSA's flight model (PhysicalAtmosphereReference) gives every body one exponential for density
and pressure. Real atmospheres are not exponential: their temperatures change with height, and
the upper atmospheres of Titan, the giants and Pluto reach far higher than one scale height from
the surface would say. This script builds, for each body, density and pressure against altitude
from measurements, the way NASA's GRAM models do (measured profiles where a probe flew through,
elsewhere measured temperatures integrated with the hydrostatic equation):

  Earth    US Standard Atmosphere 1976, 0-1000 km (pyatmos's coesa76).
  Venus    VIRA (Seiff et al. 1985) temperatures to 100 km, hydrostatic from 92.1 bar; above,
           a day-night mean thermosphere (VIRA/VTS3: ~160 K at 100-120 km warming to ~230 K,
           CO2 giving way to O).
  Mars     Mean of the Mars Pathfinder (1997, night, 19 N) and Phoenix (2008, afternoon, 68 N)
           entry measurements (PDS), anchored at the datum's 610 Pa, 214 K; thermosphere above.
  Titan    Huygens HASI entry and descent profiles (PDS), 1380 km to the surface.
  Jupiter  Galileo probe ASI profiles (PDS, Seiff et al. 1998), 1029 km above to 132 km below 1 bar.
  Saturn   Voyager/Cassini radio occultations (Lindal et al. 1985: 135 K at 1 bar, 82 K
           tropopause), Cassini CIRS stratosphere (~140 K), UVIS thermosphere (~400 K).
  Uranus   Uranus-GRAM 2024 structure: Orton et al. 2014 (76 K at 1 bar, 53 K tropopause),
           Voyager-era thermosphere (~600 K mean of its 400-800 K top).
  Neptune  Lindal 1992 (72 K at 1 bar, 52 K tropopause), stratosphere ~150 K, Voyager UVS
           thermosphere 750 K.
  Triton   Voyager 2 radio occultation (1.5 Pa, 38 K surface, PDS model atmospheres to 65 km),
           warming to the ~95 K UVS thermosphere.
  Pluto    New Horizons (11.5 microbar, 40 K surface, ~110 K at 25 km, ~70 K upper atmosphere).

It also writes the visual density tables the atmosphere shaders read for Earth, Venus, the four giants,
Titan's golden shroud, Pluto's air and haze and Triton's air (ln of density relative to the bottom of the
visual atmosphere, on even steps), Titan's, Pluto's, Triton's, Mars's and Venus's XML values, and the
giants' gas strength.

Downloads are cached in .cache/ (not in the repository). Needs numpy and, for Earth, pyatmos
(pip install pyatmos; only its coesa76 module is used).
"""
import io, math, os, sys, types, urllib.request
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache")
OUT = os.path.join(HERE, "AtmosphereTables.Generated.cs")
RGAS = 8.314462618
PDS = "https://atmos.nmsu.edu/PDS/data"
KSA_MIN_DENSITY, KSA_MIN_PRESSURE = 1e-9, 1e-4       # PhysicalAtmosphereReference's boundary thresholds


def fetch(url, name):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        print(f"  downloading {url}")
        with urllib.request.urlopen(url, timeout=120) as r, open(path, "wb") as f:
            f.write(r.read())
    return open(path, encoding="latin-1").read()


def gravity(gm_km3, r_km):
    return lambda z_km: gm_km3 * 1e9 / ((r_km + z_km) * 1e3) ** 2


def integrate_z(z_km, t_of_z, mu_of_z, g_of_z, p0):
    """Hydrostatic pressure on an altitude grid (km) from temperature and molecular weight (g/mol)."""
    t, mu, g = t_of_z(z_km), mu_of_z(z_km), g_of_z(z_km)
    dlnp = -(mu * 1e-3) * g / (RGAS * t) * 1e3                   # per km
    lnp = np.concatenate([[0.0], np.cumsum(0.5 * (dlnp[1:] + dlnp[:-1]) * np.diff(z_km))])
    p = p0 * np.exp(lnp)
    return p, p * mu * 1e-3 / (RGAS * t)


def integrate_p(lnp_nodes, t_nodes, mu_of_lnp, g_of_z, z_ref_lnp=math.log(1e5)):
    """Altitude on a log-pressure grid from T(p) nodes (giants): dz = -R T / (mu g) dln p, z = 0 at 1 bar."""
    lnp = np.linspace(lnp_nodes.max(), lnp_nodes.min(), 40001)          # bottom to top
    t = np.interp(-lnp, -lnp_nodes, t_nodes)
    mu = mu_of_lnp(lnp)
    z = np.zeros_like(lnp)
    for i in range(1, len(lnp)):                                         # g depends on z: march
        ga = g_of_z(z[i - 1])
        h = RGAS * 0.5 * (t[i] + t[i - 1]) / (0.5 * (mu[i] + mu[i - 1]) * 1e-3 * ga) / 1e3
        z[i] = z[i - 1] - h * (lnp[i] - lnp[i - 1])
    z -= np.interp(z_ref_lnp, lnp[::-1], z[::-1])                        # 1 bar at z = 0
    p = np.exp(lnp)
    return z, p, p * mu * 1e-3 / (RGAS * t), t


def smooth_nodes(x, xs, ys):
    return np.interp(x, xs, ys)


# ---------------------------------------------------------------------------------------- bodies
def earth():
    lib = os.environ.get("PYATMOS_LIB")
    if lib:
        sys.path.insert(0, lib)
    import importlib.util
    spec = importlib.util.find_spec("pyatmos")
    root = os.path.dirname(spec.origin)
    pkg = types.ModuleType("pyatmos"); pkg.__path__ = [root]; sys.modules["pyatmos"] = pkg
    sys.modules.setdefault("wget", types.ModuleType("wget"))
    cwd = os.getcwd(); os.chdir(root)                                    # its data path is relative
    try:
        from pyatmos.standardatmos.coesa76 import coesa76
        z = np.concatenate([np.arange(0, 120, 1.0), np.arange(120, 400, 5.0), np.arange(400, 1001, 20.0)])
        r = coesa76(z)
    finally:
        os.chdir(cwd)
    return z, np.asarray(r.rho), np.asarray(r.P), np.asarray(r.T), 28.9644, 9.80665


def venus():
    # VIRA low-latitude temperatures (degC), 0-100 km, then a day-night mean thermosphere
    zt = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 180, 200, 250, 300]
    tc = [462, 424, 385, 348, 306, 264, 222, 180, 143, 110, 75, 27, -10, -30, -43, -76, -104, -112]
    tk = [c + 273.15 for c in tc] + [160, 165, 175, 190, 205, 215, 225, 230, 230, 230]
    # CO2 below ~130 km, O (and CO) taking over above ~150 km on the dayside
    zm = [0, 120, 140, 160, 180, 200, 250, 300]
    mm = [43.45, 43.45, 40.0, 32.0, 24.0, 18.0, 16.0, 16.0]
    z = np.linspace(0, 300, 30001)
    p, rho = integrate_z(z, lambda q: np.interp(q, zt, tk), lambda q: np.interp(q, zm, mm), gravity(324858.6, 6051.8), 92.10e5)
    return z, rho, p, np.interp(z, zt, tk), 43.45, 8.87


def mars():
    # Measured entries, altitude above the landing site -> above the datum (site elevations, MOLA)
    mpf = fetch(f"{PDS}/mpam_0001/edl_ddr/edl_ddr.tab", "mpf_edl_ddr.tab")
    a = np.array([[float(v) for v in ln.split(",")[2:9]] for ln in mpf.strip().splitlines()])
    mpf_z, mpf_t = a[:, 0] - 3.682, a[:, 5]
    phx = fetch(f"{PDS}/phxase_0002/DATA/PHXCOMPACT.TAB", "phx_compact.tab")
    b = np.array([[float(v) for v in ln.split()] for ln in phx.strip().splitlines()])
    ok = b[:, 4] > 0
    phx_z, phx_t = b[ok, 2] - 4.13, b[ok, 8]
    grid = np.arange(0, 301, 1.0)
    t_mpf = np.interp(grid, mpf_z[::-1] if mpf_z[0] > mpf_z[-1] else mpf_z, mpf_t[::-1] if mpf_z[0] > mpf_z[-1] else mpf_t, left=np.nan, right=np.nan)
    t_phx = np.interp(grid, phx_z, phx_t, left=np.nan, right=np.nan)
    t_meas = np.nanmean(np.vstack([t_mpf, t_phx]), axis=0)
    # below the probes: a 2.5 K/km lapse from the datum's 214 K; above ~125 km: thermosphere to ~220 K
    have = ~np.isnan(t_meas)
    lo, hi = grid[have].min(), grid[have].max()
    t = t_meas.copy()
    t[grid < lo] = np.nan
    t_surface = 214.0
    blend_lo = lo + 5
    for i, zz in enumerate(grid):
        if zz <= blend_lo:
            meas = t_meas[int(blend_lo)] if not np.isnan(t_meas[int(blend_lo)]) else 180.0
            t[i] = t_surface + (meas - t_surface) * zz / blend_lo
        elif zz > hi - 5:
            t_top = t_meas[int(hi - 5)]
            t[i] = t_top + (220.0 - t_top) * min((zz - (hi - 5)) / 60.0, 1.0)
    t = np.array([t[int(v)] for v in grid])
    zm = [0, 120, 140, 160, 180, 200, 300]
    mm = [43.34, 43.34, 40.0, 34.0, 28.0, 22.0, 16.0]
    z = np.linspace(0, 300, 30001)
    p, rho = integrate_z(z, lambda q: np.interp(q, grid, t), lambda q: np.interp(q, zm, mm), gravity(42828.37, 3389.5), 610.0)
    meas = {"Pathfinder": (mpf_z, a[:, 3]), "Phoenix": (phx_z, b[ok, 4])}
    return z, rho, p, np.interp(z, grid, t), 43.34, 3.71, meas


def titan():
    ent = fetch(f"{PDS}/hphasi_0001/DATA/PROFILES/HASI_L4_ATMO_PROFILE_ENTRY.TAB", "hasi_entry.tab")
    des = fetch(f"{PDS}/hphasi_0001/DATA/PROFILES/HASI_L4_ATMO_PROFILE_DESCEN.TAB", "hasi_descent.tab")
    e = np.array([[float(v) for v in ln.split(";")[1:4]] for ln in ent.strip().splitlines()])
    d = np.array([[float(v) for v in ln.split(";")[1:5]] for ln in des.strip().splitlines()])
    mu = 27.8
    ze, pe, te = e[:, 0] / 1e3, e[:, 1], e[:, 2]
    zd, pd, td, rd = d[:, 0] / 1e3, d[:, 1], d[:, 2], d[:, 3]
    re_ = pe * mu * 1e-3 / (RGAS * te)
    z = np.concatenate([zd[zd < ze.min()], ze])
    p = np.concatenate([pd[zd < ze.min()], pe])
    t = np.concatenate([td[zd < ze.min()], te])
    rho = np.concatenate([rd[zd < ze.min()], re_])
    order = np.argsort(z)
    z, p, t, rho = z[order], p[order], t[order], rho[order]
    # the probe's own profile is noisy at the 1% level: smooth ln on a regular grid
    grid = np.concatenate([np.arange(0, 200, 1.0), np.arange(200, 1380, 5.0)])
    lnr = np.interp(grid, z, np.log(rho)); lnp = np.interp(grid, z, np.log(p)); tt = np.interp(grid, z, t)
    k = 5
    ker = np.ones(k) / k
    lnr = np.concatenate([lnr[:2], np.convolve(lnr, ker, "valid"), lnr[-2:]])
    lnp = np.concatenate([lnp[:2], np.convolve(lnp, ker, "valid"), lnp[-2:]])
    return grid, np.exp(lnr), np.exp(lnp), tt, mu, 1.352


def jupiter():
    up = fetch(f"{PDS}/gp_0001/data/asi/upperatm.tab", "gp_upperatm.tab")
    de = fetch(f"{PDS}/gp_0001/data/asi/descent.tab", "gp_descent.tab")
    u = np.array([[float(v) for v in ln.split()[:6]] for ln in up.strip().splitlines()])
    dd = np.array([[float(v) for v in ln.split()[:5]] for ln in de.strip().splitlines()])
    zu, pu, tu, ru = u[:, 1], u[:, 2] * 100.0, u[:, 3], u[:, 4]              # mbar -> Pa
    zd, pd, td, rd = dd[:, 1], dd[:, 2] * 1e5, dd[:, 3], dd[:, 4]            # bar -> Pa
    z = np.concatenate([zd, zu]); p = np.concatenate([pd, pu]); t = np.concatenate([td, tu]); rho = np.concatenate([rd, ru])
    order = np.argsort(z)
    z, p, t, rho = z[order], p[order], t[order], rho[order]
    keep = np.concatenate([[True], np.diff(z) > 0.05])
    z, p, t, rho = z[keep], p[keep], t[keep], rho[keep]
    grid = np.concatenate([np.arange(-130, 200, 2.0), np.arange(200, 1029, 10.0)])
    return grid, np.exp(np.interp(grid, z, np.log(rho))), np.exp(np.interp(grid, z, np.log(p))), np.interp(grid, z, t), 2.31, 24.79


def giant(p_nodes, t_nodes, mu_deep, mu_upper, mu_top, gm, r, top_pa=1e-7):
    """Giant planet from T(p) nodes; mu: deep (below 1 bar) -> upper (1 bar to ~1e-2 Pa) -> top (H2)."""
    lnp_nodes = np.log(np.asarray(p_nodes, float))
    def mu_of(lnp):
        return np.interp(-lnp, -np.log([1e7, 1.2e5, 1e5, 1e-2, 1e-4, 1e-9]), [mu_deep, mu_deep, mu_upper, mu_upper, mu_top, mu_top])
    nodes = np.append(lnp_nodes, math.log(top_pa))
    tn = np.append(np.asarray(t_nodes, float), t_nodes[-1])
    z, p, rho, t = integrate_p(nodes, tn, mu_of, gravity(gm, r))
    order = np.argsort(z)
    return z[order], rho[order], p[order], t[order]


def saturn():
    p = [1e6, 1e5, 3e4, 1e4, 1e3, 1e2, 10, 1, 0.1, 1e-2, 1e-3, 1e-4]
    t = [285, 134.8, 97, 82, 95, 140, 145, 145, 150, 250, 360, 400]
    z, rho, pp, tt = giant(p, t, 2.2, 2.2, 2.02, 37931207.7, 58232.0)
    return z, rho, pp, tt, 2.2, 10.44


def uranus():
    p = [1e6, 1e5, 3e4, 1e4, 1e3, 1e2, 10, 1, 0.1, 1e-2, 1e-3, 1e-4]
    t = [160, 76.4, 60, 53, 58, 75, 105, 150, 250, 400, 520, 600]
    z, rho, pp, tt = giant(p, t, 2.61, 2.31, 2.02, 5793951.3, 25362.0)
    return z, rho, pp, tt, 2.61, 8.87


def neptune():
    p = [1e6, 1e5, 3e4, 1e4, 1e3, 1e2, 10, 1, 0.1, 1e-2, 1e-3, 1e-4]
    t = [150, 72, 58, 52, 60, 100, 140, 150, 160, 400, 650, 750]
    z, rho, pp, tt = giant(p, t, 2.6, 2.39, 2.02, 6835099.5, 24622.0)
    return z, rho, pp, tt, 2.6, 11.15


def triton():
    rocc = fetch(f"{PDS}/vg_2499/data/atm_modl.tab", "triton_atm_modl.tab")
    r = np.array([[float(v) for v in ln.split(",")[:5]] for ln in rocc.strip().splitlines()])
    zr, t_sat = r[:, 0], r[:, 2]                                   # the saturated (38 K surface) model
    zt = list(zr) + [100, 200, 300, 400, 600, 1000]
    tt = list(t_sat) + [65, 85, 95, 95, 95, 95]
    z = np.linspace(0, 1000, 20001)
    p, rho = integrate_z(z, lambda q: np.interp(q, zt, tt), lambda q: np.full_like(q, 28.0), gravity(1427.6, 1353.4), 1.5)
    return z, rho, p, np.interp(z, zt, tt), 28.0, 0.779


def pluto():
    zt = [0, 5, 10, 20, 25, 30, 50, 100, 200, 300, 400, 600, 1000, 1500]
    tt = [40, 70, 90, 106, 110, 108, 100, 90, 80, 75, 70, 68, 66, 66]
    z = np.linspace(0, 1500, 30001)
    p, rho = integrate_z(z, lambda q: np.interp(q, zt, tt), lambda q: np.full_like(q, 28.0), gravity(869.6, 1188.3), 1.15)
    return z, rho, p, np.interp(z, zt, tt), 28.0, 0.616


# ------------------------------------------------------------------------------------- products
FLIGHT = {}      # body -> (z_km, rho, p)
VISUAL = {}      # body -> (step_km, ln rho/rho0 values)
REPORT = []


def boundary(z, rho, p):
    zr = np.interp(math.log(KSA_MIN_DENSITY), np.log(rho)[::-1], z[::-1]) if rho.min() < KSA_MIN_DENSITY else z.max()
    zp = np.interp(math.log(KSA_MIN_PRESSURE), np.log(p)[::-1], z[::-1]) if p.min() < KSA_MIN_PRESSURE else z.max()
    return max(zr, zp)


def decimate(z, rho, p, max_err=0.01):
    """Keep the fewest nodes that reproduce ln(rho) and ln(p) to within max_err by linear interpolation."""
    lr, lp = np.log(rho), np.log(p)
    keep = [0]
    i = 0
    while i < len(z) - 1:
        j = len(z) - 1
        lo, hi = i + 1, len(z) - 1
        best = i + 1
        while lo <= hi:                                          # binary search the longest good segment
            m = (lo + hi) // 2
            seg = slice(i, m + 1)
            f = (z[seg] - z[i]) / max(z[m] - z[i], 1e-12)
            er = np.max(np.abs(lr[seg] - (lr[i] + f * (lr[m] - lr[i]))))
            ep = np.max(np.abs(lp[seg] - (lp[i] + f * (lp[m] - lp[i]))))
            if max(er, ep) <= max_err:
                best, lo = m, m + 1
            else:
                hi = m - 1
        keep.append(best)
        i = best
    return np.array(keep)


def add(body, z, rho, p, t, mu, g0, old):
    z, rho, p = np.asarray(z, float), np.asarray(rho, float), np.asarray(p, float)
    i0 = int(np.argmin(np.abs(z)))
    h0 = RGAS * t[i0] / (mu * 1e-3 * g0) / 1e3
    b = boundary(z, rho, p)
    zk, rk, pk = z, rho, p                                      # the whole measured range
    idx = decimate(zk, rk, pk)
    FLIGHT[body] = (zk[idx], rk[idx], pk[idx], b, p[i0], rho[i0], h0)
    orho, op, oh = old
    ob = max(oh * math.log(orho / KSA_MIN_DENSITY), oh * math.log(op / KSA_MIN_PRESSURE))
    probe = [a for a in (10, 50, 100, 150, 200, 300, 500, 1000) if a < z.max()]
    ratio = "  ".join(f"{a}km x{orho * math.exp(-a / oh) / np.exp(np.interp(a, z, np.log(rho))):.2g}" for a in probe)
    REPORT.append(f"{body:8s} 0 km {p[i0]:10.4g} Pa {rho[i0]:9.4g} kg/m3 H {h0:6.2f} km | top {ob:5.0f} -> {b:5.0f} km | "
                  f"{len(idx):3d} nodes | old/new {ratio}")


KEYS = {"Earth": (6371000.0, 8430.0), "Venus": (6051800.0, 15900.0), "Jupiter": (69911000.0, 25000.0),
        "Saturn": (58232000.0, 52000.0), "Uranus": (25362000.0, 28000.0), "Neptune": (24622000.0, 21000.0)}


def visual(body, z, rho, step_km, top_km, kind="rayleigh", key=None):
    """A shader density table: ln(density / density at the visual bottom) on even steps."""
    grid = np.arange(0, top_km + 0.5 * step_km, step_km)
    lnr = np.interp(grid, z, np.log(rho))
    VISUAL[(body, kind)] = (step_km, lnr - lnr[0], key or KEYS[body])


# ---- Titan's golden shroud (visual only) --------------------------------------------------------
# KSA draws the shroud with two terms: the Rayleigh carries the haze's scattering, the Mie (absorption
# x100) its absorption, so their ratio per channel is the haze's colour. The haze as measured: optical
# depth 7 at 0.5-0.6 um (Huygens DISR, Tomasko et al. 2008); scale height 65 km below 140 km, 45 km above
# (Doose et al. 2016, which fits Cassini's limb). The Rayleigh also carries the nitrogen at its measured
# density (Huygens HASI, the flight table) and N2 Rayleigh strength; the gas absorbs nothing, so the Mie
# is haze only. The Mie follows the haze to the top of the main haze and then drops fast: the upper haze
# is small particles that scatter blue, which Cassini sees as a short gold-to-blue crossfade. The Rayleigh
# colour switches there too (RaRayleighCoefficients): the XML colour is the main haze's measured spectrum,
# and above the cutoff the small particles scatter like the gas.
TITAN_START = -2.3                       # km: Real Surfaces' visual bottom (below the terrain floor)
TITAN_TAU_G = 7.0                        # haze optical depth at 0.5-0.6 um
TITAN_H_LOW, TITAN_H_HIGH, TITAN_H_BREAK = 65.0, 45.0, 140.0
TITAN_ABSORBER_TOP = 280.0               # km above the bottom where the absorber starts to drop (to tune)
TITAN_ABSORBER_DROP = 12.0               # km: how fast it drops above that (to tune)
RENDER_WL = np.array([680.0, 550.0, 440.0])  # nm: the wavelengths behind the renderer's R, G, B (Earth's air)
TITAN_MAIN_SLOPE = 2.339                 # main haze extinction ~ lambda^-2.339 above 80 km (Tomasko et al. 2008)
TITAN_UPPER_SLOPE = 4.0                  # upper haze: particles far smaller than the wavelength, like the gas
# The haze's scattering over its extinction per channel, as KSA has to see it: fitted by
# fit_titan_colour.py (with atmosphere_model.py, a port of the game's atmosphere renderer) so the disc
# takes Titan's measured colour (Karkoschka's 1995 full-disc albedo, PDS gbat_0001: linear G/R 0.758,
# B/R 0.409 under a white Sun) at the screenshot's red brightness, which already matched the measured
# albedo. Below the real haze's (~0.95/0.89/0.70): KSA's Rayleigh phase function sends far more light
# back than the haze's forward-scattering aggregates.
TITAN_HAZE_SSA = np.array([0.86483, 0.73866, 0.44623])
TITAN_MIE_ABSORPTION = 100.0             # the XML AbsorptionMultiplier: the Mie term is all but a pure absorber
TITAN_MIE_KEY_KM = 45.0                  # Mie scale height in the XML: the shader's key for Titan's absorber
TITAN_VISUAL = {}


def titan_shroud(z_flight, rho_flight, boundary_km):
    zb = np.arange(0.0, 1000.0 + 2.5, 5.0)                                      # km above the visual bottom
    ln_h = np.where(zb <= TITAN_H_BREAK, -zb / TITAN_H_LOW,
                    -TITAN_H_BREAK / TITAN_H_LOW - (zb - TITAN_H_BREAK) / TITAN_H_HIGH)
    haze = np.exp(ln_h)
    fine = np.linspace(0, 3000, 300001)
    ln_f = np.where(fine <= TITAN_H_BREAK, -fine / TITAN_H_LOW, -TITAN_H_BREAK / TITAN_H_LOW - (fine - TITAN_H_BREAK) / TITAN_H_HIGH)
    column = np.trapz(np.exp(ln_f), fine)                                      # km, per unit bottom extinction
    haze_g = TITAN_TAU_G / column                                             # /km at 0.5-0.6 um, at the bottom
    # nitrogen: measured density (flight table, above the mean radius) at N2's Rayleigh strength
    gas_rel = np.exp(np.interp(zb + TITAN_START, z_flight, np.log(rho_flight)))
    n_bottom = gas_rel[0] / 0.0278 * 6.02214e23
    gas_g = 0.0135 * n_bottom / 2.547e25                                      # air's 0.0135/km at 550 nm
    gas_shape = gas_rel / gas_rel[0]
    ray = (haze_g * haze + gas_g * gas_shape) / (haze_g + gas_g)
    ray_coef = (RENDER_WL / 550.0) ** -TITAN_MAIN_SLOPE * (haze_g + gas_g)   # /km, the main haze's colour
    absorber = ray_coef * haze_g / (haze_g + gas_g) * (1.0 / TITAN_HAZE_SSA - 1.0)
    mie_coef = absorber / TITAN_MIE_ABSORPTION
    upper = (RENDER_WL / 550.0) ** -TITAN_UPPER_SLOPE                         # upper haze, per unit of green
    cut = np.where(zb <= TITAN_ABSORBER_TOP, 1.0, np.exp(-(zb - TITAN_ABSORBER_TOP) / TITAN_ABSORBER_DROP))
    mie = haze * cut
    # the XML scale height is the shader's key and sets where the game ends the visible atmosphere:
    # H ln(beta_max / 1e-10 per m) + bottom = the flight atmosphere's edge
    ray_key_km = round((boundary_km - TITAN_START) / math.log(ray_coef.max() * 1e-3 / 1e-10), 1)
    top = ray_key_km * math.log(ray_coef.max() * 1e-3 / 1e-10) + TITAN_START
    TITAN_VISUAL.update(ray=ray_coef, ray_key=ray_key_km, mie=mie_coef, mie_key=TITAN_MIE_KEY_KM, top=top,
                        gas_share=gas_g / (haze_g + gas_g), upper=upper)
    VISUAL[("Titan", "rayleigh")] = (5.0, np.log(ray), (2575500.0, ray_key_km * 1000.0))
    VISUAL[("Titan", "mie")] = (5.0, np.log(mie[:81]), (2575500.0, TITAN_MIE_KEY_KM * 1000.0))    # 0-400 km
    print(f"  Titan shroud: haze {haze_g:.4f}/km + N2 {gas_g:.4f}/km at the bottom (gas {100 * gas_g / (haze_g + gas_g):.0f}%), "
          f"Rayleigh {ray_coef.round(4)} key H {ray_key_km} km -> visible top {top:.0f} km, upper haze {upper.round(4)} x G; "
          f"Mie {mie_coef} key H {TITAN_MIE_KEY_KM} km; absorber drops above {TITAN_ABSORBER_TOP + TITAN_START:.0f} km ({TITAN_ABSORBER_DROP} km)")


# ---- Pluto's air and haze (visual) ----------------------------------------------------------------------
# New Horizons measured both. The air is the flight table's nitrogen at N2's Rayleigh strength. The haze's
# extinction follows the nitrogen's density from 26 to 100 km (Young et al. 2018, the Alice UV solar
# occultation); above that it is taken to go on doing so, and below 26 km it carries on as the exponential it
# has there on average (the cold boundary layer's dense nitrogen does not show in the haze). Its strength and
# phase function are fitted to its limb brightness at four phase angles (Cheng et al. 2017, Table 4: LORRI,
# I/F at 45 km): one forward Henyey-Greenstein lobe plus an isotropic part, which the shader draws when the XML
# asymmetry's Z is below -1.5 (one lobe cannot fit: the haze is as bright at 20 as at 67 degrees, then five
# times brighter by 148 and 37 times by 167). Its colour: MVIC's blue over red at 167 degrees, ~2.5 (0.7-0.8
# over 0.3, Cheng et al. 2017), as a power of wavelength between the two bands' centres.
PLUTO_START = -5.3                       # km: Real Surfaces' visual bottom (below the terrain floor)
PLUTO_RADIUS = 1188.3                    # km
PLUTO_HAZE_FROM, PLUTO_HAZE_TO = 26.0, 100.0   # km: where the haze is measured to follow the nitrogen
PLUTO_LORRI = 607.6                      # nm: LORRI's pivot wavelength
PLUTO_PHASE = np.array([20.0, 67.0, 148.0, 167.0])        # degrees
PLUTO_IF_45 = np.array([0.004, 0.004, 0.02, 0.15])        # haze I/F at 45 km (Cheng et al. 2017, Table 4)
PLUTO_BLUE_RED, PLUTO_MVIC = 2.5, (475.0, 620.0)          # MVIC blue/red haze I/F; its bands' centres (nm)
PLUTO_MIE_ABSORPTION = 1.5               # the XML AbsorptionMultiplier: tholin haze, kept
PLUTO_VISUAL = {}


def pluto_visual(z_flight, rho_flight, boundary_km):
    ln_r = np.log(rho_flight)
    h0 = RGAS * 40.0 / (0.028 * 0.616) / 1e3                                # km: 40 K isothermal below 0 km

    def ln_gas(h):                                                          # ln density, h above the mean radius
        h = np.asarray(h, float)
        return np.where(h >= 0.0, np.interp(h, z_flight, ln_r), ln_r[0] - h / h0)

    lo, hi = ln_gas(PLUTO_HAZE_FROM), ln_gas(PLUTO_HAZE_TO)
    h_below = (PLUTO_HAZE_TO - PLUTO_HAZE_FROM) / (lo - hi)                 # the haze's mean scale height there

    def ln_haze(h):                                                         # ln extinction, relative to 26 km
        h = np.asarray(h, float)
        return np.where(h >= PLUTO_HAZE_FROM, ln_gas(h) - lo, (PLUTO_HAZE_FROM - h) / h_below)

    # the limb path at 45 km: optical depth per unit extinction there
    x = np.linspace(-4000.0, 4000.0, 80001)
    alt = np.sqrt((PLUTO_RADIUS + 45.0) ** 2 + x ** 2) - PLUTO_RADIUS
    path = np.trapz(np.exp(ln_haze(alt) - ln_haze(45.0)), x)
    # phase function: forward lobe g carrying w, the rest isotropic; I/F = tau_los P(theta) 4 pi / 4
    c = np.cos(np.radians(180.0 - PLUTO_PHASE))
    gs, ws = np.meshgrid(np.arange(0.50, 0.985, 0.001), np.arange(0.30, 0.995, 0.001), indexing="ij")
    p4 = ws[..., None] * (1 - gs[..., None] ** 2) / (1 + gs[..., None] ** 2 - 2 * gs[..., None] * c) ** 1.5 + (1 - ws[..., None])
    resid = np.log(PLUTO_IF_45) - np.log(path * p4 / 4.0)
    offset = resid.mean(-1)
    err = ((resid - offset[..., None]) ** 2).sum(-1)
    i, j = np.unravel_index(np.argmin(err), err.shape)
    g, w, beta45 = gs[i, j], ws[i, j], math.exp(offset[i, j])               # beta45: /km at 607.6 nm
    fitted = beta45 * path * p4[i, j] / 4.0
    slope = math.log(PLUTO_BLUE_RED) / math.log(PLUTO_MVIC[1] / PLUTO_MVIC[0])
    colour = (RENDER_WL / PLUTO_LORRI) ** -slope
    # the tables, from the visual bottom, and the coefficients there (per km)
    zb = np.arange(0.0, 700.0 + 1.25, 2.5)
    gas = ln_gas(zb + PLUTO_START) - ln_gas(PLUTO_START)
    n_bottom = math.exp(ln_gas(PLUTO_START)) / 0.028 * 6.02214e23
    ray_coef = 0.0135 * n_bottom / 2.547e25 * (RENDER_WL / 550.0) ** -4.0   # air's 0.0135/km at 550 nm
    zh = np.arange(0.0, 700.0 + 2.5, 5.0)
    haze = ln_haze(zh + PLUTO_START) - ln_haze(PLUTO_START)
    mie_coef = beta45 * math.exp(ln_haze(PLUTO_START) - ln_haze(45.0)) * colour
    column = np.trapz(np.exp(ln_haze(np.linspace(0.0, 1500.0, 150001))), np.linspace(0.0, 1500.0, 150001))
    tau = beta45 * math.exp(ln_haze(0.0) - ln_haze(45.0)) * column / math.exp(ln_haze(0.0))
    # the XML scale heights are the shader's keys and set where the game ends the visible atmosphere: both at the
    # flight atmosphere's edge
    top = boundary_km - PLUTO_START
    ray_key = round(top / math.log(ray_coef.max() * 1e-3 / 1e-10), 1)
    mie_key = round(top / math.log(mie_coef.max() * 1e-3 / 1e-10), 1)
    PLUTO_VISUAL.update(ray=ray_coef, ray_key=ray_key, mie=mie_coef, mie_key=mie_key, g=g, w=w)
    VISUAL[("Pluto", "rayleigh")] = (2.5, gas, (PLUTO_RADIUS * 1e3, ray_key * 1e3))
    VISUAL[("Pluto", "mie")] = (5.0, haze, (PLUTO_RADIUS * 1e3, mie_key * 1e3))
    print(f"  Pluto air: {ray_coef.round(9)}/km at the bottom; haze below {PLUTO_HAZE_FROM:.0f} km with a {h_below:.1f} km scale height, "
          f"limb path at 45 km {path:.0f} km")
    print(f"  Pluto haze: {g:.3f} forward lobe carrying {w:.3f} + isotropic; beta(607.6 nm, 45 km) {beta45:.3e}/km; "
          f"I/F at 45 km {fitted.round(4)} vs {PLUTO_IF_45}; lambda^-{slope:.2f} -> {mie_coef.round(7)}/km at the bottom; "
          f"vertical scattering depth {tau:.4f} (extinction {tau * PLUTO_MIE_ABSORPTION:.4f}, Gladstone et al. 2016 ~0.013)")
    print(f"  Pluto keys: Rayleigh {ray_key} km, Mie {mie_key} km -> visible top {boundary_km:.0f} km (the flight edge)")


# ---- Venus's lower haze (visual) ------------------------------------------------------------------------
# Below the cloud deck (48-72 km, a cloud layer of its own) Venus's aerosols thicken toward the cloud base: Venera 13's
# and 14's spectrophotometers saw ~0.1-0.25/km at 600 nm from 33 to 46 km, then the jump into the clouds (Grieger et
# al. 2004; plotted with Knollenberg & Hunten 1980's Pioneer Venus data by Woitke et al. 2025). Below ~30 km the
# measurements disagree, and the Venera landers' photographs show clear horizons. So the haze rises exponentially
# toward the cloud base from almost nothing at the ground, and stops at the base, where the cloud layer takes over.
# Its colour and phase function stay as tuned (no blue: the blue absorption lives in the Ozone block).
VENUS_HAZE_BASE_KM = 47.0                # km: just below the cloud base
VENUS_HAZE_BASE = 0.1                    # /km green extinction there (Venera 13; Venera 14 saw ~0.25)
VENUS_HAZE_H = 10.0                      # km: how fast it thins downward (Venera 13 ~11, Venera 14 ~7)
VENUS_HAZE_DROP = 2.0                    # km: how fast it ends above the base, inside the cloud deck
VENUS_HAZE_COLOUR = np.array([0.00424, 0.00318, 0.0]) / 0.00318    # the tuned R:G:B
VENUS_MIE_KEY_KM = 12.5                  # the XML scale height: the shader's key (its top stays below the gas's)
VENUS_VISUAL = {}


def venus_haze():
    zb = np.arange(0.0, 100.0 + 0.5, 1.0)
    ln_h = np.where(zb <= VENUS_HAZE_BASE_KM, (zb - VENUS_HAZE_BASE_KM) / VENUS_HAZE_H,
                    -(zb - VENUS_HAZE_BASE_KM) / VENUS_HAZE_DROP)                       # relative to the base
    coef = VENUS_HAZE_BASE * VENUS_HAZE_COLOUR
    column = VENUS_HAZE_BASE * np.trapz(np.exp(ln_h), zb)
    VENUS_VISUAL.update(mie=coef)
    VISUAL[("Venus", "mie")] = (1.0, ln_h, (6051800.0, VENUS_MIE_KEY_KM * 1e3))
    print(f"  Venus haze: {VENUS_HAZE_BASE}/km at {VENUS_HAZE_BASE_KM:.0f} km, thinning downward over {VENUS_HAZE_H} km "
          f"({VENUS_HAZE_BASE * math.exp(-30 / VENUS_HAZE_H):.4f}/km at 17 km, {VENUS_HAZE_BASE * math.exp(-VENUS_HAZE_BASE_KM / VENUS_HAZE_H):.5f} at the ground); "
          f"green optical depth below the clouds {column:.2f}; XML {coef.round(4)}/km, key {VENUS_MIE_KEY_KM} km")


# ---- Mars's air (visual) --------------------------------------------------------------------------------
# The air is the flight table's CO2 (Pathfinder and Phoenix, 610 Pa and 214 K at the datum), carried down to the
# game's visual bottom, the floor of Hellas, at the table's own lapse rate near the datum. Its Rayleigh strength is
# air's times each gas's share times its scattering over air's per molecule, ((n - 1) / (n_air - 1))^2 F / F_air
# (refractivity and King factor), lambda^-4. The dust is not touched here: its strength, shape and phase function
# were tuned by eye.
MARS_START = -8.2                        # km: the game's visual bottom (Hellas)
MARS_GASES = ((0.953, 4.49, 1.15), (0.027, 2.98, 1.035), (0.016, 2.81, 1.0))   # CO2, N2, Ar: share, (n-1) x 1e4, F
AIR_REFRACTIVITY, AIR_KING = 2.93, 1.048
MARS_VISUAL = {}


def mars_visual(z_flight, rho_flight, t_flight, boundary_km):
    ln_r = np.log(rho_flight)
    lapse = (t_flight[np.searchsorted(z_flight, 2.0)] - t_flight[0]) / 2.0            # K/km near the datum
    zd = np.linspace(MARS_START, 0.0, 821)
    td = t_flight[0] + lapse * zd
    # hydrostatic, downward from the datum: d ln p / dz = -mu g / (R T)
    dlnp = -(0.04334 * 3.71) / (RGAS * td) * 1e3
    lnp = -np.concatenate([np.cumsum((0.5 * (dlnp[1:] + dlnp[:-1]) * np.diff(zd))[::-1])[::-1], [0.0]])
    ln_below = ln_r[0] + lnp + np.log(t_flight[0] / td)                                # ln rho = ln p - ln T + const

    def ln_gas(h):
        h = np.asarray(h, float)
        return np.where(h >= 0.0, np.interp(h, z_flight, ln_r), np.interp(h, zd, ln_below))

    composition = sum(x * (r / AIR_REFRACTIVITY) ** 2 * f / AIR_KING for x, r, f in MARS_GASES)
    zb = np.arange(0.0, 150.0 + 1.0, 2.0)
    gas = ln_gas(zb + MARS_START) - ln_gas(MARS_START)
    n_bottom = math.exp(ln_gas(MARS_START)) / 0.04334 * 6.02214e23
    ray_coef = 0.0135 * composition * n_bottom / 2.547e25 * (RENDER_WL / 550.0) ** -4.0
    top = boundary_km - MARS_START
    ray_key = round(top / math.log(ray_coef.max() * 1e-3 / 1e-10), 1)
    MARS_VISUAL.update(ray=ray_coef, ray_key=ray_key)
    VISUAL[("Mars", "rayleigh")] = (2.0, gas, (3389500.0, ray_key * 1e3))
    print(f"  Mars air: CO2+N2+Ar x{composition:.3f} air per molecule, {math.exp(ln_gas(MARS_START) - ln_gas(0.0)):.2f}x the datum's "
          f"density at {MARS_START} km ({t_flight[0] + lapse * MARS_START:.0f} K); {ray_coef.round(7)}/km at the bottom "
          f"({(ray_coef * math.exp(ln_gas(0.0) - ln_gas(MARS_START))).round(7)}/km at the datum), key {ray_key} km -> "
          f"visible top {boundary_km:.0f} km (the flight edge)")


# ---- Mars's dust (visual) -------------------------------------------------------------------------------
# The dust's double-exponential profile (Schneegans et al. 2024: a compact layer carrying 3/4 of it, 0.44 x the XML's
# 9 km scale height, and an extended one, 2.2 x) is measured from the datum, the mean surface, instead of from the
# sky's bottom at the floor of Hellas, 8.2 km lower (the shader adds the difference): the dust now thickens into the
# basins and thins over the heights, as Mars's does. Its spectrum and phase function stay as tuned (bhmie: red-heavy
# scattering, extinction x1.2). Its strength is MARS_DUST_TAU, the green extinction optical depth above the datum:
#   "tuned"   the tuned look's (0.0129): the same dust above the datum as before the move, so Mars looks much as it
#             did from orbit
#   0.5       Mars's own outside dust storms (the rovers measure ~0.3-0.9 in visible light)
MARS_DUST_OLD = np.array([0.00943, 0.0080, 0.0061])    # /km: the tuned coefficients, which applied at the floor of Hellas
MARS_DUST_H, MARS_DUST_ABSORPTION = 9.0, 1.2
MARS_DUST_TAU = "tuned"


def mars_dust():
    def profile(z):                                                          # z above the anchor, km
        return (0.75 * np.exp(1.0 - np.exp(z / (0.44 * MARS_DUST_H)))
                + 0.25 * np.exp(1.0 - np.exp(z / (2.2 * MARS_DUST_H))))
    z = np.linspace(0.0, 300.0, 300001)
    tau_old = MARS_DUST_OLD[1] * MARS_DUST_ABSORPTION * np.trapz(profile(z - MARS_START), z)   # above the datum, as it was
    tau = tau_old if MARS_DUST_TAU == "tuned" else float(MARS_DUST_TAU)
    coef = MARS_DUST_OLD / MARS_DUST_OLD[1] * tau / (MARS_DUST_ABSORPTION * np.trapz(profile(z), z))
    hellas = profile(MARS_START) * coef[1] * MARS_DUST_ABSORPTION
    MARS_VISUAL.update(dust=coef)
    print(f"  Mars dust: from the datum; green optical depth above it {tau:.4f} ({MARS_DUST_TAU}; it was {tau_old:.4f}), "
          f"{coef.round(6)}/km at the datum, {profile(MARS_START):.2f}x that on the floor of Hellas "
          f"(extinction {hellas:.4f}/km there, was {MARS_DUST_OLD[1] * MARS_DUST_ABSORPTION:.4f})")


# ---- Triton's air and haze (visual) ---------------------------------------------------------------------
# The air is the flight table's nitrogen at N2's Rayleigh strength. The haze is Voyager's, as Rages & Pollack (1992)
# retrieved it from limb photometry at high phase (Ohno et al. 2021, Table 2): 2.0e6 particles per cm^2, an 11.0 km
# scale height, spheres of 0.173 um cross-section-averaged radius (Hansen-Hovenier, b_eff 0.05) with refractive index
# 1.44 + 0i. Mie theory turns that into the haze's optical depth, colour and phase function at the renderer's three
# wavelengths: the game's own phase function, one lobe per channel, fits it as well as any (the asymmetry rises
# toward blue), and the haze absorbs nothing. Checked against what Voyager saw: optical depth ~0.003 (Pollack et al.
# 1990), lambda^-2 and asymmetry ~0.6 (Hillier et al. 1990, 1991), limb I/F 0.05-0.1 at 140-160 degrees of phase.
TRITON_START = -1.25                     # km: Real Surfaces' visual bottom (below the terrain floor)
TRITON_HAZE_COLUMN = 2.0e6 * 1e4         # particles per m^2
TRITON_HAZE_H = 11.0                     # km
TRITON_HAZE_R, TRITON_HAZE_B, TRITON_HAZE_N = 0.173, 0.05, 1.44
TRITON_VISUAL = {}


def triton_visual(z_flight, rho_flight, boundary_km):
    from mie import bhmie
    ln_r = np.log(rho_flight)
    h0 = RGAS * 38.0 / (0.028 * 0.779) / 1e3                                # km: 38 K isothermal below 0 km

    def ln_gas(h):
        h = np.asarray(h, float)
        return np.where(h >= 0.0, np.interp(h, z_flight, ln_r), ln_r[0] - h / h0)

    # the haze: Mie over the Hansen-Hovenier distribution at each renderer wavelength
    radii = np.linspace(0.02, 0.6, 400)                                     # um
    nr = radii ** ((1 - 3 * TRITON_HAZE_B) / TRITON_HAZE_B) * np.exp(-radii / (TRITON_HAZE_R * TRITON_HAZE_B))
    w = nr / np.trapz(nr, radii) * np.gradient(radii)
    theta = np.linspace(0.0, math.pi, 361)
    ct, st = np.cos(theta), np.sin(theta)
    tau, asym = np.zeros(3), np.zeros(3)
    for c, lam in enumerate(RENDER_WL / 1000.0):
        k2 = (2 * math.pi / lam) ** 2
        csca, agg = 0.0, np.zeros(len(theta))
        for ri, wi in zip(radii, w):
            inten, qs, _ = bhmie(2 * math.pi * ri / lam, TRITON_HAZE_N, 0.0, len(theta))
            csca += wi * qs * math.pi * (ri * 1e-6) ** 2
            agg += wi * inten / (2 * k2) * 1e-12
        p4 = 4 * math.pi * agg / csca                                       # the phase function, 4 pi normalised
        tau[c] = TRITON_HAZE_COLUMN * csca
        gs = np.arange(0.0, 0.95, 0.001)                                    # the game's Cornette-Shanks lobe, fitted
        cs = 1.5 * (1 - gs[:, None] ** 2) * (1 + ct ** 2) / ((2 + gs[:, None] ** 2) * (1 + gs[:, None] ** 2 - 2 * gs[:, None] * ct) ** 1.5)
        asym[c] = gs[np.argmin(np.trapz((np.log(cs) - np.log(p4)) ** 2 * st, theta, axis=1))]
    mie_coef = tau / TRITON_HAZE_H * math.exp(-TRITON_START / TRITON_HAZE_H)  # /km at the visual bottom
    # the air: a table from the visual bottom, and the coefficients there
    zb = np.arange(0.0, 400.0 + 1.25, 2.5)
    gas = ln_gas(zb + TRITON_START) - ln_gas(TRITON_START)
    n_bottom = math.exp(ln_gas(TRITON_START)) / 0.028 * 6.02214e23
    ray_coef = 0.0135 * n_bottom / 2.547e25 * (RENDER_WL / 550.0) ** -4.0
    # the Rayleigh scale height is the table's key and puts the visible top at the flight edge; the haze keeps its
    # measured scale height, a plain exponential the game draws itself, and reaches far less high
    top = boundary_km - TRITON_START
    ray_key = round(top / math.log(ray_coef.max() * 1e-3 / 1e-10), 1)
    haze_top = TRITON_HAZE_H * math.log(mie_coef.max() * 1e-3 / 1e-10) + TRITON_START
    TRITON_VISUAL.update(ray=ray_coef, ray_key=ray_key, mie=mie_coef, asym=asym)
    VISUAL[("Triton", "rayleigh")] = (2.5, gas, (1353400.0, ray_key * 1e3))
    print(f"  Triton air: {ray_coef.round(9)}/km at the bottom, key {ray_key} km -> visible top {boundary_km:.0f} km (the flight edge)")
    print(f"  Triton haze: vertical tau {tau.round(4)} (lambda^-{math.log(tau[2] / tau[0]) / math.log(RENDER_WL[0] / RENDER_WL[2]):.2f}), "
          f"{mie_coef.round(7)}/km at the bottom, {TRITON_HAZE_H} km scale height (its own top {haze_top:.0f} km), "
          f"Cornette-Shanks asymmetry {asym.round(3)}")


# ---- The giants' gas (visual) ---------------------------------------------------------------------------
# The gas at 1 bar, the game's surface on the giants: molecules from the flight table's pressure and temperature
# there, times each gas's scattering over air's per molecule, ((n - 1) / (n_air - 1))^2 F / F_air, lambda^-4, as
# for Mars. Shares at 1 bar: Jupiter He 13.6%, CH4 0.2% (Galileo probe: von Zahn et al. 1998, Wong et al. 2004);
# Saturn He 11%, CH4 0.47% (Koskinen & Guerlet 2018, Fletcher et al. 2009); Uranus He 15.2% (Conrath et al.
# 1987), Neptune He 19% (Conrath et al. 1991), each with methane at its saturation at 1 bar (1% at 76 K, 0.4% at
# 72 K; deeper down there is more). Refractivity at STP, 589 nm: H2 1.388e-4 (Peck & Hung 1977), He 3.50e-5
# (Mansfield & Peck 1969), CH4 4.44e-4; H2's King factor ~1.02. The scale heights stay the shader's keys.
# GIANT_RAYLEIGH picks the strength:
#   "measured"  that gas: the whole column above 1 bar
#   "tuned"     the eye-tuned values, only the gas above the haze deck (x0.56, 0.032, 0.019, 0.028 of measured)
GIANT_GASES = {"Jupiter": (0.862, 0.136, 0.002), "Saturn": (0.8853, 0.11, 0.0047),     # H2, He, CH4 at 1 bar
               "Uranus": (0.838, 0.152, 0.010), "Neptune": (0.806, 0.19, 0.004)}
GIANT_REFRACTIVITY = ((1.388, 1.02), (0.350, 1.0), (4.44, 1.0))                         # (n - 1) x 1e4, F
GIANT_TUNED = {"Jupiter": (0.00109, 0.00254, 0.00622), "Saturn": (0.00008, 0.000186, 0.000456),
               "Uranus": (0.000084, 0.000196, 0.00048), "Neptune": (0.00012, 0.00028, 0.000684)}
GIANT_RAYLEIGH = "measured"
GIANT_VISUAL = {}


def giant_gas(body, z, p, t):
    p0, t0 = math.exp(np.interp(0.0, z, np.log(p))), float(np.interp(0.0, z, t))
    composition = sum(x * (r / AIR_REFRACTIVITY) ** 2 * f / AIR_KING for x, (r, f) in zip(GIANT_GASES[body], GIANT_REFRACTIVITY))
    n0 = p0 / (1.380649e-23 * t0)
    measured = 0.0135 * composition * n0 / 2.547e25 * (RENDER_WL / 550.0) ** -4.0
    tuned = np.array(GIANT_TUNED[body])
    coef = measured if GIANT_RAYLEIGH == "measured" else tuned
    GIANT_VISUAL[body] = coef
    key = KEYS[body][1] / 1e3
    print(f"  {body} gas: x{composition:.3f} air per molecule, {n0 / 2.547e25:.2f}x air's molecules ({p0 / 1e5:.3f} bar, "
          f"{t0:.1f} K): {measured.round(6)}/km, tuned x{tuned[1] / measured[1]:.3f}, vertical optical depth above 1 bar "
          f"(B) ~{measured[2] * key:.2f}; using {GIANT_RAYLEIGH}: visible top {key * math.log(coef.max() * 1e-3 / 1e-10):.0f} km "
          f"(tuned {key * math.log(tuned.max() * 1e-3 / 1e-10):.0f})")


# ---- The giants' stratospheric hazes (visual) ------------------------------------------------------------
# From afar a giant is its cloud layer's 2D shell, at the layer's top, and the gas above it. On Jupiter and Saturn the
# shell stands at the visible edge (Saturn: 100 km, 52 mbar, where Cassini found the top of its tropospheric haze,
# ~40 mbar over the equator and ~150 mbar at mid-latitudes, Roman et al. 2013; Jupiter: 68 km, 29 mbar, where its
# stratospheric haze below turns opaque to a grazing view). Above it each has a thin stratospheric haze, which the gas alone leaves
# out: faint looking down, it is optically thick along the limb, where it whitens the gas's blue rim. Each follows the
# gas from the tropopause (100 mbar) to 1 mbar, as the retrievals assume, and its colour, absorption and phase
# function (the game's lobe per channel) come from Mie theory on the measured particles:
#   Saturn   r_eff 0.1 um, v_eff 0.1, index 1.43 + 0.001i, optical depth 0.01 at 0.9 um (HST at 55 N, Sanz-Requena et
#            al. 2019; 0.025 at 69 N; 1-100 mbar, Perez-Hoyos et al. 2005)
#   Jupiter  low latitudes: compact spheres of 0.2-0.5 um, optical depth ~0.03 at both 0.26 and 0.9 um (Cassini ISS,
#            Zhang et al. 2013), which r_eff 0.4 um matches; imaginary index 0.02 at 255 nm to 0.001 at 900 nm
#            (log-log, as Guerlet et al. 2020 after Zhang et al.); real index taken as Saturn's 1.43
# The polar hazes of both are far thicker and darker; the game has one haze per planet, so these are low latitudes'.
GIANT_HAZE = {
    "Saturn": dict(radius=58232000.0, tau09=0.01, r=0.10, v=0.10, n=1.43, shell=100.0,
                   ni=lambda lam: 0.001),
    "Jupiter": dict(radius=69911000.0, tau09=0.03, r=0.40, v=0.10, n=1.43, shell=67.8,
                    ni=lambda lam: math.exp(math.log(0.02) + (math.log(lam) - math.log(0.255))
                                            / (math.log(0.9) - math.log(0.255)) * (math.log(0.001) - math.log(0.02)))),
}
GIANT_HAZE_P = (100e2, 1e2)               # Pa: from the tropopause to 1 mbar
GIANT_HAZE_EDGES = (2.0, 10.0)            # km: how fast each ends below its base and above its top
GIANT_HAZE_VISUAL = {}


def giant_haze(body, z_flight, rho_flight, p_flight):
    from mie import bhmie
    c = GIANT_HAZE[body]
    # Mie over the Hansen-Hovenier distribution, at 0.9 um (the measured optical depth) and the renderer's wavelengths
    radii = np.linspace(0.005, 5.0 * c["r"], 800)                           # um
    b = c["v"]
    nr = radii ** ((1 - 3 * b) / b) * np.exp(-radii / (c["r"] * b))
    w = nr / np.trapz(nr, radii) * np.gradient(radii)
    theta = np.linspace(0.0, math.pi, 361)
    ct, st = np.cos(theta), np.sin(theta)
    wl = np.concatenate([[900.0], RENDER_WL]) / 1000.0
    cext, csca, asym = np.zeros(4), np.zeros(4), np.zeros(3)
    for k, lam in enumerate(wl):
        k2 = (2 * math.pi / lam) ** 2
        agg = np.zeros(len(theta))
        for ri, wi in zip(radii, w):
            inten, qs, qe = bhmie(2 * math.pi * ri / lam, c["n"], c["ni"](lam), len(theta))
            cext[k] += wi * qe * math.pi * ri ** 2
            csca[k] += wi * qs * math.pi * ri ** 2
            agg += wi * inten / (2 * k2)
        if k:
            p4 = 4 * math.pi * agg / csca[k]
            gs = np.arange(0.0, 0.95, 0.001)                                # the game's Cornette-Shanks lobe, fitted
            cs = 1.5 * (1 - gs[:, None] ** 2) * (1 + ct ** 2) / ((2 + gs[:, None] ** 2) * (1 + gs[:, None] ** 2 - 2 * gs[:, None] * ct) ** 1.5)
            asym[k - 1] = gs[np.argmin(np.trapz((np.log(cs) - np.log(p4)) ** 2 * st, theta, axis=1))]
    tau = c["tau09"] * cext[1:] / cext[0]
    ssa = csca[1:] / cext[1:]
    # where: the gas's own profile from the tropopause to 1 mbar, ending fast below and more gently above
    zb = np.arange(0.0, 400.0 + 1.0, 2.0)                                   # km above 1 bar, the visual bottom
    z_base = float(np.interp(-math.log(GIANT_HAZE_P[0]), -np.log(p_flight), z_flight))
    z_top = float(np.interp(-math.log(GIANT_HAZE_P[1]), -np.log(p_flight), z_flight))
    ln_gas = np.interp(zb, z_flight, np.log(rho_flight))
    ln_base = float(np.interp(z_base, z_flight, np.log(rho_flight)))
    ln_h = np.where(zb < z_base, (zb - z_base) / GIANT_HAZE_EDGES[0],
                    ln_gas - ln_base - np.maximum(zb - z_top, 0.0) / GIANT_HAZE_EDGES[1])     # 0 at its base
    zf = np.linspace(0.0, 400.0, 8001)
    lnf = np.interp(zf, zb, ln_h)
    column = np.trapz(np.exp(lnf), zf)                                       # km at the base's density
    coef = tau * ssa / column                                                # scattering per km at the base
    key = round(column, 1)
    top = key * math.log(coef.max() * 1e-3 / 1e-10)                         # the engine: H ln(beta_max / 1e-10 per m)
    above = np.trapz(np.exp(lnf[zf >= c["shell"]]), zf[zf >= c["shell"]]) / column
    GIANT_HAZE_VISUAL[body] = dict(mie=coef, key=key, asym=asym, absorption=1.0 / ssa.mean())
    VISUAL[(body, "mie")] = (2.0, ln_h, (c["radius"], key * 1e3))
    print(f"  {body} haze: {z_base:.1f}-{z_top:.1f} km (100-1 mbar); optical depth R G B {tau.round(4)}, "
          f"{100 * above:.0f}% above the 2D shell at {c['shell']:g} km; single-scattering albedo {ssa.round(3)}, "
          f"lobe {asym.round(3)}; XML {coef.round(6)}/km at its base, key {key} km (column-equivalent), its own top {top:.0f} km")


def cs_array(values, fmt="{:.6e}", per_line=8):
    items = [fmt.format(v) for v in values]
    return ",\n            ".join(", ".join(items[i:i + per_line]) for i in range(0, len(items), per_line))


def main():
    print("Earth"); z, rho, p, t, mu, g = earth(); add("Earth", z, rho, p, t, mu, g, (1.225, 101325, 8)); visual("Earth", z, rho, 2.0, 120)
    print("Venus"); z, rho, p, t, mu, g = venus(); add("Venus", z, rho, p, t, mu, g, (65, 92 * 101325, 18)); visual("Venus", z, rho, 5.0, 100)
    venus_haze()
    print("Mars"); z, rho, p, t, mu, g, meas = mars(); add("Mars", z, rho, p, t, mu, g, (0.02, 0.006 * 101325, 11))
    mars_visual(z, rho, t, FLIGHT["Mars"][3])
    mars_dust()
    for name, (mz, mr) in meas.items():
        sel = (mz > 15) & (mz < 120)
        dev = np.exp(np.interp(mz[sel], z, np.log(rho))) / mr[sel]
        print(f"  Mars table vs {name}: density ratio {dev.min():.2f}-{dev.max():.2f} over 15-120 km")
    print("Titan"); z, rho, p, t, mu, g = titan(); add("Titan", z, rho, p, t, mu, g, (5.3, 1.447 * 101325, 21.45))
    titan_shroud(z, rho, FLIGHT["Titan"][3])
    print("Pluto"); z, rho, p, t, mu, g = pluto(); add("Pluto", z, rho, p, t, mu, g, (0.0000842, 0.00000987 * 101325, 19.2))
    pluto_visual(z, rho, FLIGHT["Pluto"][3])
    print("Jupiter"); z, rho, p, t, mu, g = jupiter(); add("Jupiter", z, rho, p, t, mu, g, (0.16, 101325, 25)); visual("Jupiter", z, rho, 10.0, 500); giant_gas("Jupiter", z, p, t); giant_haze("Jupiter", z, rho, p)
    print("Saturn"); z, rho, p, t, mu, g = saturn(); add("Saturn", z, rho, p, t, mu, g, (0.19, 101325, 52)); visual("Saturn", z, rho, 20.0, 1000); giant_gas("Saturn", z, p, t); giant_haze("Saturn", z, rho, p)
    print("Uranus"); z, rho, p, t, mu, g = uranus(); add("Uranus", z, rho, p, t, mu, g, (1.225, 101325, 8)); visual("Uranus", z, rho, 12.0, 600); giant_gas("Uranus", z, p, t)
    print("Neptune"); z, rho, p, t, mu, g = neptune(); add("Neptune", z, rho, p, t, mu, g, (1.225, 101325, 8)); visual("Neptune", z, rho, 10.0, 500); giant_gas("Neptune", z, p, t)
    print("Triton"); z, rho, p, t, mu, g = triton(); add("Triton", z, rho, p, t, mu, g, (0.000133, 0.0000148 * 101325, 14.8))
    triton_visual(z, rho, FLIGHT["Triton"][3])
    print("\n".join(REPORT))

    # ---------------------------------------------------------------------- C#
    lines = ["// <auto-generated> by make_flight_atmospheres.py from measured atmospheres; do not edit by hand.",
             "namespace RealAtmospheres;", "",
             "internal static partial class AtmosphereTables", "{",
             "    /// <summary>Flight atmospheres: altitude (m above the body's mean radius; the 1-bar level on the",
             "    /// giants), ln density (kg/m^3), ln pressure (Pa), the altitude where KSA's boundary thresholds",
             "    /// (1e-9 kg/m^3, 1e-4 Pa) are crossed, and the 0 km values written into the XML.</summary>",
             "    internal static readonly FlightTable[] Flight =", "    {"]
    for body, (zk, rk, pk, b, p0, r0, h0) in FLIGHT.items():
        lines += [f"        new(\"{body}\", {b * 1e3:.1f},",
                  f"            \"{p0 / 101325:.6g}\", \"{r0:.6g}\", \"{h0:.4g}\",",
                  "            new double[] {", "            " + cs_array(zk * 1e3, "{:.1f}"), "            },",
                  "            new double[] {", "            " + cs_array(np.log(rk), "{:.5f}"), "            },",
                  "            new double[] {", "            " + cs_array(np.log(pk), "{:.5f}"), "            }),"]
    lines += ["    };", ""]
    # GLSL: one concatenated table, per-table offset/count/step, keyed by radius + our scale height
    values, offsets, counts, steps, conds = [], [], [], [], {"rayleigh": [], "mie": []}
    for i, ((body, kind), (step, lnr, (r, h))) in enumerate(VISUAL.items()):
        offsets.append(len(values)); counts.append(len(lnr)); steps.append(step * 1000.0); values += list(lnr)
        conds[kind].append(f"    if (abs(bodyRadius - {r:.1f}) < 20000.0 && abs(scaleHeight - {h:.1f}) < 50.0) return {i};   // {body}")
    glsl = ["// Density from measured atmospheres (make_flight_atmospheres.py): ln(density / density at the visual",
            "// bottom) on even steps. Rayleigh: " + ", ".join(b for b, k in VISUAL if k == "rayleigh")
            + " (Titan's is its haze plus its nitrogen); Mie: " + ", ".join(b for b, k in VISUAL if k == "mie") + ".",
            "// Each body is recognised by its radius AND our scale height, so a body of the same size elsewhere,",
            "// or one whose atmosphere someone else replaced, keeps the plain exponential. Past a table's top",
            "// the last slope continues.",
            f"const float raDensityLn[{len(values)}] = float[{len(values)}](",
            "    " + ",\n    ".join(", ".join(f"{v:.4f}" for v in values[i:i + 10]) for i in range(0, len(values), 10)) + ");",
            f"const int raDensityOffset[{len(offsets)}] = int[{len(offsets)}](" + ", ".join(map(str, offsets)) + ");",
            f"const int raDensityCount[{len(counts)}] = int[{len(counts)}](" + ", ".join(map(str, counts)) + ");",
            f"const float raDensityStep[{len(steps)}] = float[{len(steps)}](" + ", ".join(f"{s:.1f}" for s in steps) + ");",
            "",
            "int RaDensityTable(float bodyRadius, float scaleHeight)",
            "{"] + conds["rayleigh"] + ["    return -1;", "}", "",
            "int RaMieDensityTable(float bodyRadius, float scaleHeight)",
            "{"] + conds["mie"] + ["    return -1;", "}", "",
            "float RaTableDensity(int table, float seaLevelAltitude)",
            "{",
            "    float x = max(seaLevelAltitude, 0.0) / raDensityStep[table];",
            "    int i = min(int(x), raDensityCount[table] - 2);",
            "    int o = raDensityOffset[table] + i;",
            "    return exp(mix(raDensityLn[o], raDensityLn[o + 1], x - float(i)));",
            "}"]
    tv = TITAN_VISUAL
    glsl += ["",
             "// Titan's Rayleigh colour by altitude: the XML colour is the main haze's measured spectrum; above the",
             "// absorber's cutoff the upper haze's small particles scatter like the gas, at the same green. Every",
             "// other body, and Titan once its Rayleigh scale height is moved off our key, keeps its XML colour.",
             "vec3 RaRayleighCoefficients(float seaLevelAltitude, vec4 rayleighCoefficientsAndScaleHeight, float bodyRadius)",
             "{",
             "    vec3 coefficients = rayleighCoefficientsAndScaleHeight.xyz;",
             f"    if (abs(bodyRadius - 2575500.0) < 20000.0 && abs(rayleighCoefficientsAndScaleHeight.w - {tv['ray_key'] * 1000:.1f}) < 50.0)",
             "    {",
             f"        float upper = 1.0 - exp(-max(seaLevelAltitude - {TITAN_ABSORBER_TOP * 1000:.1f}, 0.0) / {TITAN_ABSORBER_DROP * 1000:.1f});",
             f"        coefficients = mix(coefficients, coefficients.g * vec3({tv['upper'][0]:.4f}, 1.0, {tv['upper'][2]:.4f}), upper);",
             "    }",
             "    return coefficients;",
             "}"]
    def rgb(v):
        return " ".join(f'{c}=\\"{x:.6g}\\"' for c, x in zip("RGB", v))
    lines += ["    /// <summary>Titan's golden shroud in the XML (make_flight_atmospheres.py): the main haze's Rayleigh",
              "    /// colour, the Mie absorber, and the scale heights the shader recognises Titan by; the Rayleigh's",
              "    /// also puts the visible top at the flight atmosphere's edge.</summary>",
              f"    internal const string TitanRayleighCoefficients = \"{rgb(tv['ray'])}\";",
              f"    internal const string TitanRayleighScaleHeightKm = \"{tv['ray_key']:g}\";",
              f"    internal const string TitanMieCoefficients = \"{rgb(tv['mie'])}\";",
              f"    internal const string TitanMieScaleHeightKm = \"{tv['mie_key']:g}\";",
              ""]
    pv = PLUTO_VISUAL
    lines += ["    /// <summary>Pluto's air and haze in the XML (make_flight_atmospheres.py): New Horizons' nitrogen and its",
              "    /// haze, whose phase function is one forward lobe (asymmetry X, carrying Y) plus an isotropic part, drawn",
              "    /// when Z is below -1.5; the scale heights are the shader's keys and put the visible top at the flight",
              "    /// atmosphere's edge.</summary>",
              f"    internal const string PlutoRayleighCoefficients = \"{rgb(pv['ray'])}\";",
              f"    internal const string PlutoRayleighScaleHeightKm = \"{pv['ray_key']:g}\";",
              f"    internal const string PlutoMieCoefficients = \"{rgb(pv['mie'])}\";",
              f"    internal const string PlutoMieScaleHeightKm = \"{pv['mie_key']:g}\";",
              f"    internal const string PlutoMieAsymmetry = \"X=\\\"{pv['g']:.3f}\\\" Y=\\\"{pv['w']:.3f}\\\" Z=\\\"-2\\\"\";",
              ""]
    vv = VENUS_VISUAL
    lines += ["    /// <summary>Venus's lower haze in the XML (make_flight_atmospheres.py): its strength just below the cloud base,",
              "    /// where the shader's table peaks; the scale height is the table's key.</summary>",
              f"    internal const string VenusMieCoefficients = \"{rgb(vv['mie'])}\";",
              f"    internal const string VenusMieScaleHeightKm = \"{VENUS_MIE_KEY_KM:g}\";",
              ""]
    mv = MARS_VISUAL
    lines += ["    /// <summary>Mars's air in the XML (make_flight_atmospheres.py): CO2 as Pathfinder and Phoenix measured it, at the",
              "    /// floor of Hellas, where the game starts Mars's sky; the scale height is the shader's key and puts the visible",
              "    /// top at the flight atmosphere's edge.</summary>",
              f"    internal const string MarsRayleighCoefficients = \"{rgb(mv['ray'])}\";",
              f"    internal const string MarsRayleighScaleHeightKm = \"{mv['ray_key']:g}\";",
              f"    /// <summary>Mars's dust at the datum, where the shader now measures its profile from (MARS_DUST_TAU: {MARS_DUST_TAU}).</summary>",
              f"    internal const string MarsDustCoefficients = \"{rgb(mv['dust'])}\";",
              ""]
    tr = TRITON_VISUAL
    lines += ["    /// <summary>Triton's air and haze in the XML (make_flight_atmospheres.py): Voyager's nitrogen and haze; the",
              "    /// Rayleigh scale height is the shader's key and puts the visible top at the flight atmosphere's edge, the",
              "    /// haze's is the measured one, and its asymmetry is the game's lobe fitted per channel to Mie theory.</summary>",
              f"    internal const string TritonRayleighCoefficients = \"{rgb(tr['ray'])}\";",
              f"    internal const string TritonRayleighScaleHeightKm = \"{tr['ray_key']:g}\";",
              f"    internal const string TritonMieCoefficients = \"{rgb(tr['mie'])}\";",
              f"    internal const string TritonMieScaleHeightKm = \"{TRITON_HAZE_H:g}\";",
              "    internal const string TritonMieAsymmetry = \"" + " ".join(f'{c}=\\"{v:.3f}\\"' for c, v in zip("XYZ", tr["asym"])) + "\";",
              ""]
    def positional(v):
        return " ".join(f'{c}=\\"{np.format_float_positional(x, precision=4, fractional=False, trim="-")}\\"' for c, x in zip("RGB", v))
    lines += ["    /// <summary>The giants' gas in the XML (make_flight_atmospheres.py, GIANT_RAYLEIGH = \"" + GIANT_RAYLEIGH + "\"): its",
              "    /// Rayleigh strength at 1 bar, the game's surface; the scale heights stay the shader's keys.</summary>"]
    lines += [f"    internal const string {b}RayleighCoefficients = \"{positional(GIANT_VISUAL[b])}\";" for b in GIANT_VISUAL]
    lines += [""]
    lines += ["    /// <summary>Jupiter's and Saturn's stratospheric hazes in the XML (make_flight_atmospheres.py): each one's",
              "    /// strength at its base (100 mbar), where the shader's table peaks; the scale height is the table's key; the",
              "    /// game's lobe per channel.</summary>"]
    for b, hv in GIANT_HAZE_VISUAL.items():
        lines += [f"    internal const string {b}MieCoefficients = \"{rgb(hv['mie'])}\";",
                  f"    internal const string {b}MieScaleHeightKm = \"{hv['key']:g}\";",
                  f"    internal const string {b}MieAsymmetry = \"" + " ".join(f'{c}=\\"{v:.3f}\\"' for c, v in zip("XYZ", hv["asym"])) + "\";",
                  f"    internal const string {b}MieAbsorption = \"{hv['absorption']:.4f}\";"]
    lines += [""]
    lines += ["    /// <summary>GLSL for AtmosphereData.glsl, inserted ahead of AtmosphereDensityFalloff: the density",
              "    /// tables and Titan's Rayleigh colour by altitude.</summary>",
              "    internal const string VisualDensityGlsl =", "\"\"\"", *glsl, "\"\"\";", "}", ""]
    io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print(f"\nwrote {os.path.basename(OUT)}: {len(FLIGHT)} flight tables, {len(VISUAL)} visual tables ({len(values)} values)")


if __name__ == "__main__":
    main()
