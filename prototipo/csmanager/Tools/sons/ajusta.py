# Ajuste automático dos botões de cada arma pra encostar na régua do CS2 (envelope, faixas, brilho, decaimento).
# (1+L)-ES em paralelo, como o treino dos bots. Grava ajuste.json. Uso: python ajusta.py [arma,...] [--geracoes G] [--so tiro|distante]
import sys, os, glob, json, numpy as np
from multiprocessing import Pool
import armas, regua
from medir import medir_arr
from motor import SR
import soundfile as sf

T_ENV = [2, 5, 10, 20, 40, 80, 160, 320, 640]
PESO_ENV = [0.4, 0.4, 0.6, 1, 1, 1, 1, 1, 0.7]
LIM = {'comp_db': (-30, -4), 'comp_razao': (1.0, 8.0), 'agudo_db': (-6, 12), 'grave_db': (-8, 8), 'd_comp_db': (-30, -4), 'd_grave_db': (-10, 8)}
DB = {'comp_db', 'comp_razao', 'agudo_db', 'grave_db', 'd_comp_db', 'd_grave_db'}


def env(x):
    w = int(0.005 * SR); e = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')) + 1e-9
    db = 20 * np.log10(e / e.max()); ini = int(np.argmax(db > -30))
    return np.array([db[min(len(db) - 1, ini + int(ms * SR / 1000))] for ms in T_ENV])


def feat(x):
    m = medir_arr(x, SR); m['env'] = env(x); return m


def alvo(arqs):
    fs = []
    for a in arqs:
        x, sr = sf.read(a, always_2d=True); x = x.mean(1)
        if sr != SR:
            from scipy.signal import resample_poly
            x = resample_poly(x, SR, sr)
        fs.append(feat(x))
    return dict(env=np.mean([f['env'] for f in fs], 0), bandas=np.mean([f['bandas'] for f in fs], 0),
                brilho=np.mean([f['brilho'] for f in fs]), d20=np.mean([f['d20'] for f in fs]), d40=np.mean([f['d40'] for f in fs]))


def custo(f, a):
    ce = float(np.sum(np.abs(f['env'] - a['env']) * PESO_ENV) / np.sum(PESO_ENV)) / 3
    cb = float(np.abs(np.array(f['bandas']) - a['bandas']).sum()) / 0.2
    cbr = abs(np.log(f['brilho'] / a['brilho'])) / np.log(1.25)
    cd = abs(np.log(f['d20'] / a['d20'])) / np.log(1.3) + abs(np.log(f['d40'] / a['d40'])) / np.log(1.3)
    ca = max(0, f['ataque_ms'] - 25) / 10
    return ce + cb + cbr + 0.7 * cd + ca


def avalia(args):
    a, tipo, K, A = args
    p = armas.ARMAS[a]; c = 0
    for s in (0, 1):
        x = armas.tiro_perto(p, armas.semente(a, s), K) if tipo == 'tiro' else armas.tiro_distante(p, armas.semente(a, s), K)
        c += custo(feat(x), A)
    return c / 2


def muta(K, nomes, sig, rng):
    N = dict(K)
    for n in nomes:
        if n in DB:
            lo, hi = LIM[n]; N[n] = float(np.clip(K[n] + rng.normal() * sig * (hi - lo) * 0.25, lo, hi))
        else:
            lo, hi = (0.0, 4.0) if n in ('sub', 'eco', 'd_eco_g') else (0.12, 1.2) if n == 'd_eco_t' else (0.3, 3.0)
            N[n] = float(np.clip(max(K[n], 0.02) * np.exp(rng.normal() * sig), lo, hi))
    return N


def ajustar(a, tipo, ger, lam=8):
    pr, dr = regua.MAPA[a]; pad = pr if tipo == 'tiro' else dr
    if not pad: return None
    A = alvo(sorted(glob.glob(os.path.join(regua.REF, pad))))
    nomes = armas.BOTOES if tipo == 'tiro' else armas.BOTOES_D
    K = armas.botoes(a); rng = np.random.default_rng(7)
    c = avalia((a, tipo, K, A)); sig = 0.35; c0 = c
    for g in range(ger):
        filhos = [muta(K, nomes, sig, rng) for _ in range(lam)]
        cs = [avalia((a, tipo, f, A)) for f in filhos]
        i = int(np.argmin(cs))
        if cs[i] < c: K, c = filhos[i], cs[i]; sig = min(0.6, sig * 1.15)
        else: sig = max(0.05, sig * 0.82)
    print(f'{a:7s} {tipo:8s} custo {c0:6.2f} -> {c:6.2f}', flush=True)
    return {n: round(K[n], 4) for n in nomes}


if __name__ == '__main__':
    args = [x for x in sys.argv[1:] if not x.startswith('--')]
    ger = int(sys.argv[sys.argv.index('--geracoes') + 1]) if '--geracoes' in sys.argv else 20
    so = sys.argv[sys.argv.index('--so') + 1] if '--so' in sys.argv else None
    lista = args[0].split(',') if args and not args[0].isdigit() and args[0] not in ('tiro', 'distante') else list(armas.ARMAS)
    aj = json.load(open(armas.AJUSTE, encoding='utf-8')) if os.path.exists(armas.AJUSTE) else {}
    jobs = [(a, t, ger) for a in lista for t in ('tiro', 'distante') if not so or so == t]
    with Pool(min(15, len(jobs))) as pool:
        for (a, t, _), k in zip(jobs, pool.starmap(ajustar, jobs)):
            if k: aj.setdefault(a, {}).update(k)
    json.dump(aj, open(armas.AJUSTE, 'w', encoding='utf-8'), indent=1)
