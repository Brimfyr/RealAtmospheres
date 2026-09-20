# -*- coding: utf-8 -*-
"""Mie-theory preprocessing for Martian dust (Level 1 KSA integration).

Port of the user's bhmie JS prototype (Bohren & Huffman 1983), following
Schneegans et al. 2024 (CGF 43(2), doi:10.1111/cgf.15010): feldspar + hematite
dust, bimodal log-normal size distribution, lambdas = 680/550/440 nm.

Outputs:
  1. spectral scattering/absorption cross-sections (-> XML Mie coefficients ratio
     and AbsorptionMultiplier),
  2. a 3-lobe Henyey-Greenstein fit of the wavelength-dependent phase function
     (-> GLSL constants for the MiePhaseFunction shader patch; the per-channel
     forward lobe is what produces the blue halo around the sun).

Run:  py mars_dust_mie.py   (writes mars_dust_phase.glsl next to this script)
"""
import math, os
import numpy as np

HEMATITE = 1.0
LAMBDAS = [0.680, 0.550, 0.440]              # um (R, G, B)
M_RE = [1.52, 1.52, 1.52]
M_IM = [0.0008 * HEMATITE, 0.0100 * HEMATITE, 0.0400 * HEMATITE]
N_ANG = 361                                   # 0..180 deg, 0.5 deg steps


def bhmie(x, mre, mim, n_ang):
    """Single-sphere Mie scattering; returns per-angle |S1|^2+|S2|^2, Qsca, Qext."""
    y = complex(mre, mim) * x
    xstop = x + 4.0 * x ** (1.0 / 3.0) + 2.0
    nstop = int(xstop)
    nmx = int(max(xstop, abs(y)) + 15.0)

    # logarithmic derivative D_n by downward recurrence
    D = np.zeros(nmx + 1, dtype=complex)
    for n in range(nmx, 0, -1):
        a = n / y
        D[n - 1] = a - 1.0 / (D[n] + a)

    amu = np.cos(np.linspace(0.0, math.pi, n_ang))
    pin0 = np.zeros(n_ang)
    pin1 = np.ones(n_ang)
    S1 = np.zeros(n_ang, dtype=complex)
    S2 = np.zeros(n_ang, dtype=complex)

    psi0, psi1 = math.cos(x), math.sin(x)
    chi0, chi1 = -math.sin(x), math.cos(x)
    xi1 = complex(psi1, -chi1)
    m = complex(mre, mim)
    qsca = 0.0
    qext = 0.0

    for n in range(1, nstop + 1):
        psi = (2.0 * n - 1.0) / x * psi1 - psi0
        chi = (2.0 * n - 1.0) / x * chi1 - chi0
        xi = complex(psi, -chi)

        ta = D[n] / m + n / x
        tb = D[n] * m + n / x
        an = (ta * psi - psi1) / (ta * xi - xi1)
        bn = (tb * psi - psi1) / (tb * xi - xi1)

        tn = 2.0 * n + 1.0
        qsca += tn * (abs(an) ** 2 + abs(bn) ** 2)
        qext += tn * (an.real + bn.real)

        fn = tn / (n * (n + 1.0))
        tau = n * amu * pin1 - (n + 1.0) * pin0
        S1 += fn * (an * pin1 + bn * tau)
        S2 += fn * (an * tau + bn * pin1)
        pin0, pin1 = pin1, ((2.0 * n + 1.0) * amu * pin1 - (n + 1.0) * pin0) / n

        psi0, psi1 = psi1, psi
        chi0, chi1 = chi1, chi
        xi1 = complex(psi1, -chi1)

    inten = np.abs(S1) ** 2 + np.abs(S2) ** 2
    f = 2.0 / (x * x)
    return inten, qsca * f, qext * f


# bimodal log-normal size distribution (Hansen-Travis conversion, cf. prototype)
MODES = [
    dict(rg=0.04 * 1.1 ** -2.5, sig=math.sqrt(math.log(1.1)), w=1000.0),
    dict(rg=0.8 * 1.4 ** -2.5, sig=math.sqrt(math.log(1.4)), w=1.0),
]

def ndist(r):
    s = 0.0
    for m in MODES:
        lr = math.log(r / m["rg"])
        s += m["w"] / (r * m["sig"] * math.sqrt(2 * math.pi)) * math.exp(-lr * lr / (2 * m["sig"] ** 2))
    return s

NR, R_MIN, R_MAX = 120, 0.005, 5.0
radii = R_MIN * (R_MAX / R_MIN) ** (np.arange(NR) / (NR - 1))
wgt = np.zeros(NR)
for i in range(NR):
    dr = (radii[min(i + 1, NR - 1)] - radii[max(i - 1, 0)]) * 0.5
    wgt[i] = ndist(radii[i]) * dr

phase = np.zeros((3, N_ANG))
sca = np.zeros(3)
absb = np.zeros(3)
for c in range(3):
    lam = LAMBDAS[c]
    k2 = (2 * math.pi / lam) ** 2
    agg = np.zeros(N_ANG)
    csca = cext = 0.0
    for i in range(NR):
        if wgt[i] <= 0:
            continue
        inten, qs, qe = bhmie(2 * math.pi * radii[i] / lam, M_RE[c], M_IM[c], N_ANG)
        pir2 = math.pi * radii[i] ** 2
        csca += wgt[i] * qs * pir2
        cext += wgt[i] * qe * pir2
        agg += wgt[i] * inten / (2 * k2)
    phase[c] = agg / csca                     # normalized: integrates to 1 over sphere
    sca[c] = csca
    absb[c] = max(cext - csca, 0.0)

print("relative sca  (R:G:B) =", np.round(sca / sca[1], 4))
print("relative abs  (R:G:B) =", np.round(absb / sca[1], 4))
print("ext/sca per channel   =", np.round((sca + absb) / sca, 4))

# ---------------- fit: 3-lobe Henyey-Greenstein mixture per channel ----------------
theta = np.linspace(0.0, math.pi, N_ANG)
mu = np.cos(theta)

def hg(g, mu):
    return (1 - g * g) / (4 * math.pi * (1 + g * g - 2 * g * mu) ** 1.5)

# loss weight: solid angle x forward emphasis (the halo region 0-30 deg matters most)
wl = np.sin(theta) + 1e-4
wl *= 1.0 + 9.0 * np.exp(-np.degrees(theta) / 20.0)

g1s = np.arange(0.80, 0.995, 0.005)
g2s = np.arange(0.20, 0.80, 0.03)
g3s = np.arange(-0.50, 0.01, 0.05)

fits = []
for c in range(3):
    tgt = phase[c]
    best = None
    for g1 in g1s:
        h1 = hg(g1, mu)
        for g2 in g2s:
            h2 = hg(g2, mu)
            for g3 in g3s:
                A = np.stack([h1, h2, hg(g3, mu)], axis=1)
                Aw = A * wl[:, None]
                w, *_ = np.linalg.lstsq(Aw, tgt * wl, rcond=None)
                w = np.clip(w, 0.0, None)
                s = w.sum()
                if s <= 0:
                    continue
                w /= s
                model = A @ w
                loss = np.sum(wl * (np.log10(np.maximum(model, 1e-8)) - np.log10(np.maximum(tgt, 1e-8))) ** 2)
                if best is None or loss < best[0]:
                    best = (loss, g1, g2, g3, w.copy())
    fits.append(best)
    print(f"ch{c}: loss={best[0]:.3f} g=({best[1]:.3f},{best[2]:.2f},{best[3]:.2f}) w={np.round(best[4],3)}")

# ---------------- emit GLSL constants ----------------
def v3(vals, fmt="%.4f"):
    return "vec3(" + ", ".join(fmt % v for v in vals) + ")"

g1 = [f[1] for f in fits]; g2 = [f[2] for f in fits]; g3 = [f[3] for f in fits]
w1 = [f[4][0] for f in fits]; w2 = [f[4][1] for f in fits]; w3 = [f[4][2] for f in fits]

glsl = f"""// ---- Martian dust phase function (Mie theory, Schneegans et al. 2024) ----
// 3-lobe HG fit per RGB channel of bhmie output for feldspar+hematite dust
// (bimodal log-normal sizes). Generated by _build/mars_dust_mie.py - do not hand-edit.
vec3 MarsDustHG(float g_scale, vec3 g, float cosLight)
{{
    vec3 gg = g * g;
    return (vec3(1.0) - gg) / (4.0 * PI * pow(vec3(1.0) + gg - 2.0 * g * cosLight, vec3(1.5))) * g_scale;
}}

// Soft radiance compression of the forward spike: KSA has no exposure control, so
// the physically-correct peak (P~10/sr near 0 deg) washes the sunset out. This is
// "exposure applied in the medium": identity for P << MARS_DUST_PHASE_KNEE, smooth
// compression above it. Only the innermost few degrees around the sun are affected.
#define MARS_DUST_PHASE_KNEE 8.0

vec3 MarsDustPhaseFunction(float cosLight)
{{
    vec3 p = vec3(0.0);
    p += {v3(w1)} * MarsDustHG(1.0, {v3(g1)}, cosLight);
    p += {v3(w2)} * MarsDustHG(1.0, {v3(g2)}, cosLight);
    p += {v3(w3)} * MarsDustHG(1.0, {v3(g3)}, cosLight);
    return p / (vec3(1.0) + p / MARS_DUST_PHASE_KNEE);
}}
// ---- end Martian dust phase function ----
"""
out = os.path.join(os.path.dirname(__file__), "mars_dust_phase.glsl")
open(out, "w").write(glsl)
print("wrote", out)
