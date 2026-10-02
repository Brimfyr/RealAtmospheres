# -*- coding: utf-8 -*-
"""Mie scattering by one sphere (Bohren & Huffman 1983, BHMIE), shared by mars_dust_mie.py and
make_flight_atmospheres.py (Triton's haze)."""
import math
import numpy as np


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
