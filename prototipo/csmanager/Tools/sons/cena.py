# Mixador offline com a MESMA lógica do Som3D.cs (curvas do CS2, perto+distante, ar, apagar o tiro anterior em 0,1 s).
# Serve pra ouvir rajada e distância antes de abrir a Unity. Uso: python cena.py  -> saida/_cenas/*.wav
import os, glob, numpy as np, soundfile as sf
from scipy.signal import butter, sosfilt
from motor import SR, salvar

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida')
C_TIRO = [(0, 1), (175.7, 1), (440, 0.735), (1288, 0.331), (2500, 0.015), (2600, 0)]
C_TIRO_AWP = [(0, 1), (175.7, 1), (440, 0.735), (1288, 0.331), (2800, 0.048), (3000, 0)]
C_DIST = [(800, 0), (2335.8, 0.54), (2868.6, 0.096)]
C_DIST_P = [(800, 0), (2354.3, 0.337), (2868.6, 0.096)]
PERFIL = {'ak': (1.0, 0.5, C_TIRO, C_DIST), 'm4': (0.95, 0.8, C_TIRO, C_DIST_P), 'm4s': (0.8, 0.6, C_TIRO, C_DIST_P),
          'awp': (1.0, 1.0, C_TIRO_AWP, C_DIST_P), 'deagle': (0.9, 1.0, C_TIRO, C_DIST_P), 'glock': (0.85, 0.7, C_TIRO, C_DIST_P),
          'usp': (0.75, 0.5, C_TIRO, C_DIST_P), 'p250': (0.85, 0.9, C_TIRO, C_DIST_P), 'mp9': (0.75, 0.9, C_TIRO, C_DIST_P),
          'mac10': (0.7, 0.9, C_TIRO, C_DIST_P), 'tec9': (0.8, 0.6, C_TIRO, C_DIST_P), 'fiveseven': (0.85, 0.9, C_TIRO, C_DIST_P),
          'galil': (0.95, 0.6, C_TIRO, C_DIST), 'famas': (0.95, 0.8, C_TIRO, C_DIST_P)}
RPM = {'ak': 600, 'm4': 666, 'm4s': 600, 'awp': 41, 'deagle': 267, 'glock': 400, 'usp': 353, 'p250': 400, 'mp9': 857, 'mac10': 800, 'tec9': 500, 'fiveseven': 400, 'galil': 666, 'famas': 666}


def curva(c, d):
    if d <= c[0][0]: return c[0][1]
    for (x0, y0), (x1, y1) in zip(c, c[1:]):
        if d <= x1: return y0 + (y1 - y0) * (d - x0) / max(1e-3, x1 - x0)
    return c[-1][1]


_cache = {}
def clipes(a, tipo):
    k = (a, tipo)
    if k not in _cache: _cache[k] = [sf.read(f)[0] for f in sorted(glob.glob(os.path.join(S, a, tipo + '_*.wav')))]
    return _cache[k]


def lpf(x, f):
    return sosfilt(butter(2, min(f, SR / 2 - 100), 'lowpass', fs=SR, output='sos'), x)


def rajada(a, n, dist, ocluso=False, dur=None, rng=None, pan=0.0):
    """n tiros na cadência da arma a 'dist' unidades do CS. Devolve estéreo."""
    rng = rng or np.random.default_rng(1)
    vp, vd, cp, cd = PERFIL[a]; per = 60 / RPM[a]
    dur = dur or n * per + 3.5
    L = int(dur * SR); out = np.zeros(L)
    gp, gd = curva(cp, dist) * vp, curva(cd, dist) * vd
    if ocluso: gp *= 0.55; gd *= 0.55
    corte = 2200 if ocluso else 22000 - (22000 - 5500) * min(1, dist / 2500)
    ult = -1; ant_fim = None
    for i in range(n):
        t = int(i * per * SR); cs = clipes(a, 'tiro'); k = rng.integers(len(cs))
        if k == ult and len(cs) > 1: k = (k + 1) % len(cs)
        ult = k
        # pitch ±2% (reamostragem simples)
        x = cs[k]; f = 1 + rng.uniform(-0.02, 0.02); x = np.interp(np.arange(0, len(x) - 1, f), np.arange(len(x)), x)
        # tiro novo apaga o anterior em 0,1 s
        if ant_fim is not None:
            a0, a1 = ant_fim; j0 = t; j1 = min(L, t + int(0.1 * SR))
            out[j0:j1] -= out_ant[j0 - a0:j1 - a0] * np.linspace(0, 1, j1 - j0) if j1 > j0 else 0
            out[j1:a1] -= out_ant[j1 - a0:a1 - a0]
        m = min(len(x), L - t); seg = np.zeros(L); seg[t:t + m] = x[:m] * gp
        out += seg[:]; out_ant = seg[t:]; ant_fim = (t, L)
        cds = clipes(a, 'distante')
        if cds and gd > 0.003:
            y = cds[rng.integers(len(cds))]; m = min(len(y), L - t); out[t:t + m] += y[:m] * gd * 0.8
    out = lpf(out, corte)
    g = 1 - 0.6 * pan; h = 1 + 0.6 * pan   # pan -1 esquerda .. 1 direita
    return np.stack([out * g, out * h], 1) * 0.7


def junta(partes, dur):
    L = int(dur * SR); out = np.zeros((L, 2))
    for t, x in partes:
        i = int(t * SR); m = min(len(x), L - i); out[i:i + m] += x[:m]
    pk = np.abs(out).max()
    return out / pk * 0.89 if pk > 0.89 else out


if __name__ == '__main__':
    d = os.path.join(S, '_cenas'); os.makedirs(d, exist_ok=True)
    for a in PERFIL:
        if not clipes(a, 'tiro'): continue
        n = 1 if a == 'awp' else 5 if a in ('deagle', 'usp', 'glock', 'p250') else 8
        sf.write(os.path.join(d, f'rajada_{a}.wav'), rajada(a, n, 150), SR, subtype='PCM_16')
        # o mesmo tiro em 4 distâncias, um depois do outro
        partes = [(i * 2.2, rajada(a, 1 if a == 'awp' else 3, dd, dur=2.2 if a != 'awp' else 3.0)) for i, dd in enumerate([150, 700, 1500, 2400])]
        sf.write(os.path.join(d, f'distancias_{a}.wav'), junta(partes, 4 * 2.2 + 2), SR, subtype='PCM_16')
    # um lance de round: TR entra de AK, CT responde de M4 atrás da parede, AWP lá do outro lado, glock fecha
    r = np.random.default_rng(5)
    lance = [(0.0, rajada('ak', 4, 300, rng=r, pan=-0.4)), (0.35, rajada('m4', 6, 650, ocluso=True, rng=r, pan=0.5)),
             (1.4, rajada('awp', 1, 2100, rng=r, pan=0.2)), (2.3, rajada('ak', 3, 300, rng=r, pan=-0.4)),
             (3.1, rajada('glock', 3, 500, rng=r, pan=0.6)), (3.9, rajada('m4s', 4, 250, rng=r, pan=-0.1))]
    sf.write(os.path.join(d, 'lance_round.wav'), junta(lance, 8), SR, subtype='PCM_16')
    print('cenas em', d)
