# Efeito de voz: rádio (jogadores) e locutor (anúncios). Nosso tratamento em cima da voz da ElevenLabs.
import numpy as np
import motor, hibrido
SR = motor.SR


def _chiado(r, ms, g):
    n = int(ms / 1000 * SR); x = motor.bp(r.standard_normal(n), 1500, 4500) * np.exp(-np.arange(n) / (n / 3)) * g
    return x


def radio(x, seed=0):
    r = np.random.default_rng(seed)
    x = hibrido.aparar(x, -45, 5)
    x = motor.hp(motor.lp(x, 3400, 4), 350, 4)
    x = motor.satura(x / (np.abs(x).max() + 1e-9), 2.2)
    x = motor.compressor(x, -18, 4)
    x = x / (np.abs(x).max() + 1e-9) * 0.85
    a, b = _chiado(r, 35, 0.18), _chiado(r, 70, 0.25)               # aperta / solta o botão do rádio
    pre = int(0.04 * SR)
    y = np.zeros(pre + len(x) + len(b) + int(0.02 * SR)); y[:len(a)] += a; y[pre:pre + len(x)] += x; y[pre + len(x):pre + len(x) + len(b)] += b
    y += motor.bp(r.standard_normal(len(y)), 1000, 4000) * 0.012     # chiado de fundo baixinho
    return motor.acabamento(y, -1, 15)


def locutor(x):
    x = hibrido.aparar(x, -45, 5)
    x = motor.hp(motor.lp(x, 7500, 2), 120, 2)
    x = motor.compressor(x / (np.abs(x).max() + 1e-9), -16, 3)
    return motor.acabamento(x, -1, 30)
