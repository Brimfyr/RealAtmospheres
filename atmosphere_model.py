"""A numpy port of KSA's atmosphere renderer, to predict what the game draws for a set of atmosphere
values without launching it.

Ported from the game's shaders (Atmosphere/TransmittanceLut.comp, MultipleScatteringLut.comp,
AerialPerspectiveLut.comp, AtmosphereFunctions.glsl, AtmosphereLuts.glsl; build 2026.9.x) with the
same LUT sizes, iteration counts and end-of-step sampling, so their biases come along too: the
transmittance LUT (256 x 64, 50 steps), the multiple-scattering LUT (32 x 32, 64 directions x 20
steps, summed as a geometric series) and the aerial-perspective path the disc is drawn with (96
steps). The Sun is the game's constant <Sunlight> (9, 9, 9): KSA never dims it with distance.
The display side is the game's Hable tone curve (PostProcess/Tonemapping, default curve, gamma 2.2).

Density comes from the shader tables in AtmosphereTables.Generated.cs, so the port always matches
what the generator last wrote. Its absolute brightness can be off (it doesn't model the cloud layers,
the haze laminae or the blooms), so compare it with a screenshot RELATIVELY: calibrate each channel on
the current values, then predict the change. fit_titan_colour.py shows how.
"""
import math
import os
import re
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
GENERATED = os.path.join(HERE, "AtmosphereTables.Generated.cs")
PI = math.pi
SUN = 9.0


# ---------------------------------------------------------------- the generated shader tables
def _generated():
    return open(GENERATED, encoding="utf-8").read()


def shader_table(body, kind="rayleigh"):
    """(step m, ln density, key scale height m) of a body's table in the generated GLSL."""
    src = _generated()
    fn = "RaDensityTable" if kind == "rayleigh" else "RaMieDensityTable"
    block = re.search(r"int %s\(float bodyRadius, float scaleHeight\)\s*\{(.*?)\}" % fn, src, re.S).group(1)
    m = re.search(r"abs\(scaleHeight - ([\d.]+)\) < 50\.0\) return (\d+);\s*// %s" % body, block)
    if not m:
        raise KeyError(f"no {kind} table for {body}")
    key, index = float(m.group(1)), int(m.group(2))
    vals = [float(v) for v in re.search(r"raDensityLn\[\d+\] = float\[\d+\]\((.*?)\);", src, re.S).group(1).split(",")]
    offs = [int(v) for v in re.search(r"raDensityOffset\[\d+\] = int\[\d+\]\((.*?)\);", src).group(1).split(",")]
    cnts = [int(v) for v in re.search(r"raDensityCount\[\d+\] = int\[\d+\]\((.*?)\);", src).group(1).split(",")]
    steps = [float(v) for v in re.search(r"raDensityStep\[\d+\] = float\[\d+\]\((.*?)\);", src).group(1).split(",")]
    return steps[index], np.array(vals[offs[index]:offs[index] + cnts[index]]), key


def generated_rgb(name):
    """A generated XML colour constant, e.g. 'TitanRayleighCoefficients', in per-metre units."""
    m = re.search(r'%s = "R=\\"([\d.e-]+)\\" G=\\"([\d.e-]+)\\" B=\\"([\d.e-]+)\\""' % name, _generated())
    return np.array([float(v) for v in m.groups()]) * 1e-3


def table_density(step, lnr):
    """RaTableDensity: linear in ln density, the last slope continues past the top."""
    def density(alt):
        x = np.maximum(alt, 0.0) / step
        i = np.minimum(x.astype(int), len(lnr) - 2)
        return np.exp(lnr[i] + (lnr[i + 1] - lnr[i]) * (x - i))
    return density


def visible_top(coefficients_per_m, scale_height_m):
    """The game's atmosphere edge above the visual bottom for one term: H ln(beta_max / 1e-10 per m)."""
    return scale_height_m * math.log(max(coefficients_per_m) / 1e-10)


# ---------------------------------------------------------------- geometry
def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def sphere_hits(center, radius, direction):
    """RaySphereIntersection from the origin: (near, far), (-1, -1) on a miss. center, direction (..., 3)."""
    b = np.sum(direction * center, -1)
    remedy = center - b[..., None] * direction
    disc = radius * radius - np.sum(remedy * remedy, -1)
    sq = np.sqrt(np.maximum(disc, 0.0))
    q = b + np.where(b >= 0, 1.0, -1.0) * sq
    c = np.sum(center * center, -1) - radius * radius
    r0, r1 = q, c / np.where(q == 0, 1e-30, q)
    miss = disc < 0
    return np.where(miss, -1.0, np.minimum(r0, r1)), np.where(miss, -1.0, np.maximum(r0, r1))


# ---------------------------------------------------------------- the renderer
class Atmosphere:
    """bottom / top / planet_r in m (bottomRadius = mean radius + StartHeight, planetRadius = mean radius).
    ray_coef(alt) -> (..., 3) Rayleigh coefficients per metre, allowed to change with altitude
    (RaRayleighCoefficients); ray_density / mie_density(alt) -> density at the altitude above the
    bottom; mie_coef (3,) per metre. As in the game the Mie extinction is mie_coef x mie_absorption
    and its scattering mie_coef. mie_g: the XML's asymmetry, one Cornette-Shanks lobe for all channels or one per
    channel, or (X, Y, Z) with Z below -1.5 for the two-part phase function (a forward lobe of asymmetry X carrying
    Y, the rest isotropic)."""

    def __init__(s, bottom, top, planet_r, ray_coef, ray_density, mie_coef, mie_density, mie_absorption,
                 mie_g=0.0, sun=SUN):
        s.bottom, s.top, s.planet_r = bottom, top, planet_r
        s.ray_coef, s.ray_density = ray_coef, ray_density
        s.mie_coef, s.mie_density, s.mie_absorption = np.asarray(mie_coef, float), mie_density, mie_absorption
        s.mie_g, s.sun = mie_g, sun
        s.T = s._transmittance_lut()
        s.MS = s._multiple_scattering_lut()

    def mie_phase(s, c):
        """MiePhaseFunction as Real Atmospheres patches it (the Mars dust branch left out), per channel: (..., 3)."""
        c = np.asarray(c, float)[..., None]
        if np.ndim(s.mie_g) and s.mie_g[2] < -1.5:
            g, w = s.mie_g[0], s.mie_g[1]
            p = w * (1 - g * g) / (4 * PI * (1 + g * g - 2 * g * c) ** 1.5) + (1 - w) / (4 * PI)
            return np.broadcast_to(p, c.shape[:-1] + (3,))
        g = np.broadcast_to(np.asarray(s.mie_g, float), (3,))
        g2 = g * g
        return 1.5 / (4 * PI) * (1 - g2) * (1 + g2 - 2 * g * c) ** -1.5 * (1 + c * c) / (2 + g2)

    def medium(s, alt):
        a = np.maximum(alt, 0.0)
        sr = s.ray_coef(a) * s.ray_density(a)[..., None]
        sm = s.mie_coef * s.mie_density(a)[..., None]
        return sr, sm, sr + sm * s.mie_absorption

    def _transmittance_lut(s, W=256, H=64, iters=50):
        cx, cy = np.meshgrid(np.arange(W) / (W - 1), np.arange(H) / (H - 1))
        maxh = math.sqrt(s.top ** 2 - s.bottom ** 2)
        rd = maxh * cy
        r = np.sqrt(rd ** 2 + s.bottom ** 2)
        dmin, dmax = s.top - r, rd + maxh
        dist = dmin + cx * (dmax - dmin)
        cosz = np.where(dist == 0, 1.0, (maxh ** 2 - rd ** 2 - dist ** 2) / (2 * r * np.where(dist == 0, 1, dist)))
        cosz = np.clip(cosz, -1, 1)
        exitd = -r * cosz + np.sqrt(np.maximum(r * r * cosz * cosz - r * r + s.top ** 2, 0))
        step = exitd / iters
        t = step[..., None] * np.arange(1, iters + 1)
        rr = np.sqrt(r[..., None] ** 2 + t ** 2 + 2 * r[..., None] * t * cosz[..., None])
        _, _, ext = s.medium(rr - s.bottom)
        return np.exp(-(ext * step[..., None, None]).sum(-2))

    def transmittance(s, r, cosz):
        """GetTransmittanceFromLut with the planet's soft shadow; r = distance from the centre (m)."""
        H, W, _ = s.T.shape
        maxh = math.sqrt(s.top ** 2 - s.bottom ** 2)
        rd = np.sqrt(np.maximum(0, r * r - s.bottom ** 2))
        raydist = np.maximum(0, -r * cosz + np.sqrt(np.maximum(r * r * (cosz * cosz - 1) + s.top ** 2, 0)))
        dmin, dmax = s.top - r, rd + maxh
        x = np.clip((raydist - dmin) / (dmax - dmin), 0, 1) * (W - 1)
        y = np.clip(rd / maxh, 0, 1) * (H - 1)
        T = _bilinear(s.T, x, y)
        sinh = s.planet_r / r
        cosh = -np.sqrt(np.maximum(1 - sinh * sinh, 0))
        return T * smoothstep(-sinh * 0.01, sinh * 0.01, cosz - cosh)[..., None]

    def _multiple_scattering_lut(s, N=32, G=8, iters=20):
        cx, cy = np.meshgrid(np.arange(N) / (N - 1), np.arange(N) / (N - 1))
        d = s.bottom + np.maximum(cy * (s.top - s.bottom), 1.0)
        mu = cx * 2 - 1
        sun = np.stack([np.zeros_like(mu), np.sqrt(1 - mu * mu), mu], -1)
        u, v = np.meshgrid(np.arange(G) / G, np.arange(G) / G)
        az, cp = 2 * PI * u.ravel(), 1 - 2 * v.ravel()
        sp = np.sin(np.arccos(cp))
        dirs = np.stack([np.cos(az) * sp, np.sin(az) * sp, cp], -1)
        center = np.zeros(d.shape + (1, 3))
        center[..., 2] = -d[..., None]
        D = np.broadcast_to(dirs, d.shape + dirs.shape)
        _, exitd = sphere_hits(center, s.top, D)
        hit, _ = sphere_hits(center, s.bottom, D)
        step = np.where(hit > 0, hit, exitd) / iters
        pos = D[..., None, :] * (step[..., None] * np.arange(1, iters + 1))[..., None]
        rel = pos - center[..., None, :]
        r = np.linalg.norm(rel, axis=-1)
        cosz = np.sum(rel / r[..., None] * sun[:, :, None, None, :], -1)
        sr, sm, ext = s.medium(r - s.bottom)
        st = np.exp(-ext * step[..., None, None])
        before = np.cumprod(np.concatenate([np.ones_like(st[..., :1, :]), st[..., :-1, :]], -2), -2)
        w = before * (1 - st) / np.maximum(ext, 1e-30)
        transfer = (w * (sr + sm)).sum(-2)
        second = (w * s.transmittance(r, cosz) * (sr + sm)).sum(-2) / (4 * PI)
        return second.mean(-2) / (1 - transfer.mean(-2))

    def multiple_scattering(s, alt, mu_s):
        N = s.MS.shape[0]
        x = np.clip(mu_s * 0.5 + 0.5, 0, 1) * (N - 1)
        y = np.clip(alt / (s.top - s.bottom), 0, 1) * (N - 1)
        return _bilinear(s.MS, x, y)

    def rays(s, camera, directions, sun_dir):
        """HDR (x the Sun) along many rays from a camera outside or inside the atmosphere: rays that
        hit the ground take the aerial-perspective path (96 steps), sky rays the sky path (50 steps)."""
        camera = np.asarray(camera, float)
        D = np.asarray(directions, float).reshape(-1, 3)
        sun_dir = np.asarray(sun_dir, float)
        out = np.zeros((len(D), 3))
        center = np.broadcast_to(-camera, D.shape)
        near, far = sphere_hits(center, s.top, D)
        ground_hit, _ = sphere_hits(center, s.bottom, D)
        idx = np.nonzero(far >= 0)[0]
        for chunk in np.array_split(idx, max(1, len(idx) // 3000)):
            d, start, ground = D[chunk], np.maximum(near[chunk], 0), ground_hit[chunk] >= 0
            end = np.where(ground, np.minimum(far[chunk], ground_hit[chunk]), far[chunk])
            for g, iters in ((True, 96), (False, 50)):
                m = ground == g
                if not m.any():
                    continue
                step = (end[m] - start[m]) / iters
                t = start[m][:, None] + step[:, None] * np.arange(1, iters + 1)
                pos = camera + t[..., None] * d[m][:, None, :]
                r = np.linalg.norm(pos, axis=-1)
                cosz = np.sum(pos / r[..., None] * sun_dir, -1)
                sr, sm, ext = s.medium(r - s.bottom)
                st = np.exp(-ext * step[:, None, None])
                before = np.cumprod(np.concatenate([np.ones_like(st[:, :1]), st[:, :-1]], 1), 1)
                w = before * (1 - st) / np.maximum(ext, 1e-30)
                c = d[m] @ sun_dir
                rphase = (3 / (16 * PI) * (1 + c * c))[:, None, None]
                mphase = s.mie_phase(c)[:, None, :]
                single = (w * s.transmittance(r, cosz) * (rphase * sr + mphase * sm)).sum(1)
                multi = (w * s.multiple_scattering(r - s.bottom, cosz) * (sr + sm)).sum(1)
                out[chunk[m]] = (single + multi) * s.sun
        return out

    def disc(s, phase_deg, x, y=0.0, distance=5e8):
        """HDR at a point of the disc, (x, y) in bottom radii from the centre, x toward the Sun, seen from
        far along +z with the Sun at the phase angle."""
        a = math.radians(phase_deg)
        camera = np.array([x * s.bottom, y * s.bottom, distance])
        return s.rays(camera, np.array([[0.0, 0.0, -1.0]]), np.array([math.sin(a), 0.0, math.cos(a)]))[0]

    def image(s, phase_deg=35.0, size=300, frame=None, distance=2.0e7):
        """HDR image (size x size x 3) lit from the left. frame = (centre x, half width) in metres at the
        planet; default: the whole planet with a margin."""
        a = math.radians(phase_deg)
        sun = np.array([-math.sin(a), 0.0, math.cos(a)])
        cx, half = frame if frame else (0.0, 1.45 * s.top)
        ys, xs = np.mgrid[0:size, 0:size]
        u = (cx + ((xs + 0.5) / size * 2 - 1) * half) / distance
        v = (1 - (ys + 0.5) / size * 2) * half / distance
        d = np.stack([u, v, -np.ones_like(u)], -1)
        d /= np.linalg.norm(d, axis=-1, keepdims=True)
        return s.rays(np.array([0.0, 0.0, distance]), d, sun).reshape(size, size, 3)


def _bilinear(table, x, y):
    H, W = table.shape[:2]
    x0 = np.minimum(x.astype(int), W - 2)
    y0 = np.minimum(y.astype(int), H - 2)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    return (table[y0, x0] * (1 - fx) * (1 - fy) + table[y0, x0 + 1] * fx * (1 - fy)
            + table[y0 + 1, x0] * (1 - fx) * fy + table[y0 + 1, x0 + 1] * fx * fy)


# ---------------------------------------------------------------- the display (Hable tone curve)
def _hable_curve(toe_strength=0.5, toe_length=0.5, shoulder_strength=2.0, shoulder_length=0.5, shoulder_angle=1.0):
    """The game's HableFilmicToneCurve with its default parameters (W = 1.4686)."""
    toe_length = min(max(toe_length, 0), 1) ** 2.2
    toe_strength = min(max(toe_strength, 0), 1)
    shoulder_angle = min(max(shoulder_angle, 0), 1)
    shoulder_length = max(1e-9, shoulder_length)
    shoulder_strength = min(max(shoulder_strength, 1e-9), 1)
    x0 = toe_length * 0.5
    y0 = (1 - toe_strength) * x0
    rest = 1 - y0
    y1_offset = (1 - shoulder_strength) * rest
    x1, y1 = x0 + y1_offset, y0 + y1_offset
    W = x0 + rest + 2 ** shoulder_length - 1
    over_x, over_y = W * 2 * shoulder_angle * shoulder_length, 0.5 * shoulder_angle * shoulder_length
    X0, X1, OX = x0 / W, x1 / W, over_x / W
    m = (y1 - y0) / (X1 - X0) if X1 != X0 else 1.0
    b = y0 - X0 * m

    def solve(xa, ya, slope):
        B = slope * xa / ya
        return math.log(ya) - B * math.log(xa), B

    def segment(ox, oy, sx, sy, lnA, B):
        return dict(ox=ox, oy=oy, sx=sx, sy=sy, lnA=lnA, B=B)

    mid = segment(-b / m, 0, 1, 1, math.log(m), 1.0)
    toe = segment(0, 0, 1, 1, *solve(X0, max(1e-9, y0), m))
    OY = (1 + over_y) - 1
    shoulder = segment(1 + OX, 1 + OY, -1, -1, *solve(1 + OX - X1, 1 + OY - max(1e-9, y1), m))
    segs = [toe, mid, shoulder]
    scale = 1.0 / _hable_eval(shoulder, 1.0)
    for sg in segs:
        sg["oy"] *= scale
        sg["sy"] *= scale
    return dict(invW=1 / W, X0=X0, X1=X1, segs=segs)


def _hable_eval(sg, x):
    x0 = (x - sg["ox"]) * sg["sx"]
    y0 = math.exp(sg["lnA"] + sg["B"] * math.log(x0)) if x0 > 0 else 0.0
    return y0 * sg["sy"] + sg["oy"]


_CURVE = _hable_curve()


def display(hdr, exposure=1.25, gamma=2.2):
    """HDR -> 0-1 display value per channel, as PostProcess/composite.frag does it (settings.toml's
    tonemapExposure; the Borea instance's is the default 1.25)."""
    def one(x):
        n = max(float(x), 0.0) * exposure * _CURVE["invW"]
        i = 0 if n < _CURVE["X0"] else (1 if n < _CURVE["X1"] else 2)
        return min(max(_hable_eval(_CURVE["segs"][i], n), 0.0), 1.0) ** (1 / gamma)
    return np.vectorize(one)(hdr)


def hdr_from_display(pixel, exposure=1.25, gamma=2.2):
    """Invert the display for 8-bit screenshot values (a pixel at 255 is clipped and has no answer)."""
    from scipy.optimize import brentq
    return np.array([brentq(lambda x: float(display(x, exposure, gamma)) - p / 255.0, 1e-7, 200.0)
                     for p in np.atleast_1d(pixel)])
