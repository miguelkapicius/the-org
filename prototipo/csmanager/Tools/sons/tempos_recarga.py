# Mede, nas animações exportadas (Logs/bonecos3d/boneco_<time>.json/.bin), em que fração da recarga o pente SAI e o
# pente novo ENCAIXA, no espaço da arma. Grava tempos_recarga.json pro Som3D (som casado com a nossa animação).
import json, struct, numpy as np, sys
B = 'D:/unity/CSManager/Logs/bonecos3d/'
MAPA = {'AK47': 'ak', 'M4A4': 'm4', 'M4A1S': 'm4s', 'AWP': 'awp', 'MP9': 'mp9', 'MAC10': 'mac10', 'USP': 'usp', 'Glock': 'glock', 'Deagle': 'deagle', 'Tec9': 'tec9', 'FiveSeven': 'fiveseven', 'P250': 'p250', 'Galil': 'galil', 'Famas': 'famas'}
def carrega(time):
    h = json.load(open(B + f'boneco_{time}.json', encoding='utf-8'))
    blob = np.fromfile(B + f'boneco_{time}.bin', dtype='<f4')
    return h, blob
res = {}
for time in ('tr', 'ct'):
    h, blob = carrega(time); nomes = [p['nome'] for p in h['pecas']]; P = len(nomes)
    for a in h['anims']:
        if not a['nome'].startswith('recarga_'): continue
        W = a['nome'][8:]
        if W not in MAPA or W not in nomes: continue
        q, n = a['q'], a['n']; F = blob[q: q + n * P * 12].reshape(n, P, 3, 4)
        iw = nomes.index(W)
        def rel(i, f):   # posição da peça i no espaço da arma, quadro f (None se escondida)
            M = F[f, iw]; m = F[f, i]
            if not m.any() or not M.any(): return None
            R, t = M[:, :3], M[:, 3]
            return np.linalg.solve(R, m[:, 3] - t)
        L = 0.3   # comprimento típico de arma (m): limiares relativos
        velhos = [k for k, nm in enumerate(nomes) if nm.startswith(W + '_pente') and not nm.endswith('.001')]
        novos = [k for k, nm in enumerate(nomes) if nm.startswith(W + '_pente') and nm.endswith('.001')]
        sai = entra = None
        for k in velhos:   # SAI: o pente velho (ou a cópia "inteira") começa a se afastar da pose em que apareceu
            r0 = None
            for f in range(n):
                r = rel(k, f)
                if r is None: continue
                if r0 is None: r0 = r
                elif np.linalg.norm(r - r0) > 0.03 * L: sai = f / (n - 1) if sai is None else min(sai, f / (n - 1)); break
        for k in novos:    # ENTRA: último quadro em que o pente novo ainda se mexe (depois disso está encaixado)
            ult = None; rp = None
            for f in range(n):
                r = rel(k, f)
                if r is not None and rp is not None and np.linalg.norm(r - rp) > 0.004 * L: ult = f
                rp = r
            if ult is not None: entra = ult / (n - 1) if entra is None else max(entra, ult / (n - 1))
        res.setdefault(MAPA[W], {})[time] = dict(sai=None if sai is None else round(sai, 3), entra=None if entra is None else round(entra, 3), quadros=n)
for k, v in res.items(): print(f'{k:7s}', v)
json.dump(res, open('tempos_recarga.json', 'w'), indent=1)
