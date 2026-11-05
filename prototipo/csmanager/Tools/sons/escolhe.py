# Sem regressão: pra cada arma e camada, fica com o conjunto de botões de menor custo na régua (entre versões salvas).
import json, sys, glob, os, numpy as np
from multiprocessing import Pool
import armas, ajusta, regua
def custo(args):
    a, tipo, K = args
    pr, dr = regua.MAPA[a]; pad = pr if tipo == 'tiro' else dr
    if not pad: return 0.0
    A = ajusta.alvo(sorted(glob.glob(os.path.join(regua.REF, pad)))); p = armas.ARMAS[a]
    f = armas.tiro_perto if tipo == 'tiro' else armas.tiro_distante
    return float(np.mean([ajusta.custo(ajusta.feat(f(p, armas.semente(a, s), K)), A) for s in range(4)]))
if __name__ == '__main__':
    versoes = sys.argv[1:]
    cfgs = [json.load(open(v, encoding='utf-8')) for v in versoes]
    def K(c, a):
        k = dict(armas.NEUTRO); k.update(armas.NEUTRO_D); k.update(c.get(a, {})); return k
    jobs = [(a, t, vi) for a in armas.ARMAS for t in ('tiro', 'distante') for vi in range(len(cfgs))]
    with Pool(15) as pool: cs = pool.map(custo, [(a, t, K(cfgs[vi], a)) for a, t, vi in jobs])
    res = {}
    for (a, t, vi), c in zip(jobs, cs): res.setdefault((a, t), []).append(c)
    final = {}
    for (a, t), lst in res.items():
        b = int(np.argmin(lst)); nomes = armas.BOTOES if t == 'tiro' else armas.BOTOES_D
        final.setdefault(a, {}).update({n: v for n, v in K(cfgs[b], a).items() if n in nomes})
        print(f'{a:7s} {t:8s} ' + '  '.join(f'{os.path.basename(versoes[i])}={c:.2f}' for i, c in enumerate(lst)) + f'  -> {os.path.basename(versoes[b])}')
    json.dump(final, open('ajuste.json', 'w', encoding='utf-8'), indent=1)
