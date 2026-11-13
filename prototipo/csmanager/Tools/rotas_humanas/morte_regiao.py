# O que o pro faz nos 6 s depois que um colega morre numa região (sim-apoio, 2026-11-08).
# Rodar: python -I Tools/rotas_humanas/morte_regiao.py [saida.json]
# Pra cada morte (died_at, região R onde morreu) e cada colega vivo a 0-2 regiões de R (grafo = transições vistas no mapa):
#   desfecho em 6 s: entra em R (quando; sozinho ou com outro em ±1,5 s) · fica onde está · recua (mais longe de R) ·
#   outra região (mesma distância ou mais perto, mas não R) · morre (antes de qualquer outra coisa).
# Separa por lado, distância (0 = já estava em R) e se já ia pra lá (nos 3 s antes ficou mais perto de R ou entrou na região
# atual vindo de mais longe). Só compra cheia dos dois lados.
import json, sys, collections
P = 'D:/unity/demos_cs/cs-simulator/contract/artifacts/tactics_playbook.json'
d = json.load(open(P, encoding='utf-8'))['maps']
JAN = 6.0


def onde(steps, t):
    for r, a, b in steps:
        if a <= t < b: return r
    return None


def dist_graf(viz, a, b, maxd=3):
    if a == b: return 0
    vist, fr = {a}, [a]
    for k in range(1, maxd + 1):
        nx = []
        for x in fr:
            for y in viz[x]:
                if y == b: return k
                if y not in vist: vist.add(y); nx.append(y)
        fr = nx
    return 99


res = collections.defaultdict(collections.Counter); tempos = collections.defaultdict(list)
for mapa, M in d.items():
    viz = collections.defaultdict(set)
    for R in M['rounds']:
        for lado in ('T', 'CT'):
            for j in R[lado]:
                st = j['steps']
                for i in range(1, len(st)):
                    if st[i][0] != st[i - 1][0]: viz[st[i - 1][0]].add(st[i][0]); viz[st[i][0]].add(st[i - 1][0])
    for R in M['rounds']:
        if R['t_buy'] != 'full' or R['ct_buy'] != 'full': continue
        tp = R['plant']['t'] if R.get('plant') else 1e9
        for lado in ('T', 'CT'):
            js = R[lado]
            for x in js:
                dm = x.get('died_at')
                if dm is None or not x['steps']: continue
                rm = onde(x['steps'], dm - 0.01)
                if rm is None: rm = x['steps'][-1][0]
                plant = 'pós-plant' if dm >= tp else 'antes do plant'
                for y in js:
                    if y is x or (y.get('died_at') is not None and y['died_at'] <= dm): continue
                    ry = onde(y['steps'], dm)
                    if ry is None: continue
                    dd = dist_graf(viz, ry, rm)
                    if dd > 2: continue
                    # já ia pra lá: nos 3 s antes, esteve mais longe de R do que está agora
                    antes = onde(y['steps'], dm - 3)
                    indo = antes is not None and dist_graf(viz, antes, rm) > dd
                    # desfecho: primeira mudança de região em (dm, dm+6]
                    fim = 'fica'; te = None
                    morreu = y.get('died_at') is not None and y['died_at'] <= dm + JAN
                    for r, a, b in y['steps']:
                        if a <= dm or a > dm + JAN: continue
                        if morreu and y['died_at'] < a: break
                        if r == ry: continue
                        if r == rm: fim = 'entra em R'; te = a - dm
                        elif dist_graf(viz, r, rm) > dd: fim = 'recua'
                        else: fim = 'outra região (mesma dist. ou mais perto)'
                        break
                    if fim == 'fica' and morreu: fim = 'morre (sem sair)'
                    if dd == 0: fim = {'fica': 'fica em R', 'entra em R': 'fica em R'}.get(fim, fim)
                    k = (lado, plant, dd, 'já ia pra lá' if indo else 'não ia')
                    res[k][fim] += 1
                    if te is not None:
                        # sozinho ou em dois: outro colega vivo entrou em R a ±1,5 s
                        junto = any(z is not x and z is not y and any(r == rm and abs(a - (dm + te)) <= 1.5 for r, a, b in z['steps']) for z in js)
                        tempos[k].append((te, junto))
                        # e se morreu lá dentro em até 6 s depois de entrar
                        if y.get('died_at') is not None and y['died_at'] <= dm + te + JAN: res[k]['…e morreu em R'] += 1

out = {}
for k in sorted(res):
    c = res[k]; n = sum(v for kk, v in c.items() if not kk.startswith('…'))
    if n < 30: continue
    tv = sorted(t for t, _ in tempos[k]); jt = sum(1 for _, j in tempos[k] if j)
    linha = {kk: round(100 * v / n, 1) for kk, v in c.most_common()}
    q = (lambda p: round(tv[min(len(tv) - 1, int(p * len(tv)))], 1)) if tv else (lambda p: None)
    out[' | '.join(map(str, k))] = {'n': n, '%': linha, 'entrou: p25/p50/p75 s': [q(.25), q(.5), q(.75)], 'entrou junto (±1,5 s)': round(100 * jt / max(1, len(tv)), 1),
                                    'entrou <1,5 s': round(100 * sum(1 for t in tv if t < 1.5) / n, 1), 'entrou 1,5-6 s': round(100 * sum(1 for t in tv if t >= 1.5) / n, 1)}
for k, v in out.items():
    print(f"{k}  (n={v['n']})")
    print('    ' + ' · '.join(f'{a} {b}%' for a, b in v['%'].items()))
    print(f"    entrou <1,5 s {v['entrou <1,5 s']}% · 1,5-6 s {v['entrou 1,5-6 s']}% · tempo p25/p50/p75 {v['entrou: p25/p50/p75 s']} · junto {v['entrou junto (±1,5 s)']}%")
if len(sys.argv) > 1: json.dump(out, open(sys.argv[1], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
