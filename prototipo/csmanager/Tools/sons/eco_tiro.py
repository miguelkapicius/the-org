# Mede o "eco" da camada PERTO dos tiros (o que toca quando a troca está na tela) contra o tiro perto do CS2.
# cauda = energia de 120-900 ms em relação aos primeiros 120 ms (dB); t40 = quando o envelope cai 40 dB e não volta;
# reflexos = picos do envelope depois de 80 ms que sobem >= 3 dB sobre o vale anterior (eco "batendo").
import os, sys, glob, numpy as np, soundfile as sf
from scipy.signal import resample_poly
import regua
SR = 48000
NOSSO = 'D:/unity/CSManager/Assets/Game/Audio/Resources/Sons'
REF = 'D:/unity/Tools/ref_sons_cs2/sounds/weapons'


def le(f):
    x, sr = sf.read(f, always_2d=True); x = x.mean(1)
    if sr != SR: x = resample_poly(x, SR, sr)
    return x


def medir(x):
    p = int(np.argmax(np.abs(x))); x = x[max(0, p - int(0.005 * SR)):]
    e = x ** 2; a = int(0.12 * SR); b = int(0.9 * SR)
    cauda = 10 * np.log10((e[a:b].sum() + 1e-12) / (e[:a].sum() + 1e-12))
    w = int(0.01 * SR); env = np.sqrt(np.convolve(e, np.ones(w) / w, 'same')) + 1e-9
    db = 20 * np.log10(env / env.max())
    acima = np.where(db > -40)[0]; t40 = acima[-1] / SR if len(acima) else 0
    h = int(0.005 * SR); v = db[::h]; refl = 0; vale = 0
    for i in range(int(0.08 * SR / h), len(v) - 1):
        vale = min(vale, v[i]) if v[i] < v[i - 1] else vale
        if v[i] > v[i - 1] and v[i] >= v[i + 1] and v[i] > -45 and v[i] - vale >= 3: refl += 1; vale = v[i]
    return cauda, t40, refl


if __name__ == '__main__':
    print(f'{"arma":10s} {"cauda nossa":>11s} {"CS2":>6s}   {"t40 nosso":>9s} {"CS2":>6s}   {"reflexos":>8s} {"CS2":>4s}')
    for a, (perto, _) in regua.MAPA.items():
        ns = [medir(le(f)) for f in sorted(glob.glob(f'{NOSSO}/{a}/tiro_*.wav'))]
        rs = [medir(le(f)) for f in sorted(glob.glob(f'{REF}/{perto}'))]
        if not ns or not rs: print(a, 'sem arquivos', len(ns), len(rs)); continue
        n = np.mean(ns, 0); r = np.mean(rs, 0)
        marca = '  <<' if n[0] - r[0] > 3 or n[1] > r[1] * 1.3 else ''
        print(f'{a:10s} {n[0]:11.1f} {r[0]:6.1f}   {n[1]:9.2f} {r[1]:6.2f}   {n[2]:8.1f} {r[2]:4.1f}{marca}', flush=True)
    os._exit(0)
