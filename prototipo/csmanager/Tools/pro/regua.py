"""Régua do pro (camada geral): métricas de time por região, das 2.931 rounds do playbook.
Uso: python -I regua.py  -> imprime e grava Tools/pro/regua.json"""
import json, collections, statistics as st
P = r'D:\unity\demos_cs\cs-simulator\contract\artifacts\tactics_playbook.json'
d = json.load(open(P, encoding='utf-8'))
DT = 0.5
out = {}
def onde(steps, t):
    for r, a, b in steps:
        if a <= t < b: return r
    return None
geral = collections.defaultdict(list)
for mapa, M in d['maps'].items():
    places = M['places']
    met = collections.defaultdict(list)
    for R in M['rounds']:
        if R['t_buy'] != 'full' or R['ct_buy'] != 'full': continue
        dur = R['duration_s']; plant = R['plant']['t'] if R.get('plant') else 1e9
        for lado in ('T', 'CT'):
            js = R[lado]
            # mortes: região e hora
            mortes = []
            for j in js:
                if j.get('died_at') is not None and j['steps']:
                    mortes.append((j['died_at'], onde(j['steps'], j['died_at'] - 0.01) or j['steps'][-1][0]))
            mortes.sort()
            for i in range(1, len(mortes)):
                t1, r1 = mortes[i]
                for k in range(i - 1, -1, -1):
                    t0, r0 = mortes[k]
                    if t1 - t0 > 6: break
                    if r0 == r1:
                        met['seq_mesma_regiao_' + ('lt1.5' if t1 - t0 < 1.5 else '1.5-6')].append(1); break
            met['mortes'].append(len(mortes))
            # ocupação ao longo do round (antes do plant): regiões distintas e o maior grupo na mesma região
            t = 5.0
            while t < min(dur, plant, 90):
                regs = [onde(j['steps'], t) for j in js if not (j.get('died_at') is not None and j['died_at'] <= t)]
                regs = [r for r in regs if r is not None]
                if len(regs) >= 3:
                    c = collections.Counter(regs)
                    fase = 'cedo' if t < 25 else 'meio' if t < 50 else 'tarde'
                    met[f'{lado}_regioes_distintas_por_vivo_{fase}'].append(len(c) / len(regs))
                    met[f'{lado}_maior_grupo_{fase}'].append(max(c.values()))
                    met[f'{lado}_3mais_mesma_regiao_{fase}'].append(1 if max(c.values()) >= 3 else 0)
                # plantado (sim-apoio 2026-11-09): há quanto tempo cada vivo está na região atual (renovação: a idade tem a mesma
                # distribuição do que ainda falta — "ainda lá daqui a X s")
                for j in js:
                    if j.get('died_at') is not None and j['died_at'] <= t: continue
                    for r, a0, b0 in j['steps']:
                        if a0 <= t < b0:
                            for X in (4, 8, 15): met[f'{lado}_na_regiao_ha_{X}s_ou_mais'].append(1 if t - a0 >= X else 0)
                            break
                t += DT
            # tempo de permanência por região (passos com duração)
            for j in js:
                for r, a, b in j['steps']:
                    if b - a > 0.2: met[f'{lado}_permanencia_s'].append(b - a)
    res = {}
    nr = sum(1 for R in M['rounds'] if R['t_buy'] == 'full' and R['ct_buy'] == 'full')
    for k, v in met.items():
        if k.startswith('seq_'): res[k + '_por100'] = round(100 * len(v) / max(1, nr), 1)
        elif k == 'mortes': res['mortes_por_round_lado'] = round(st.mean(v), 2)
        elif k.endswith('permanencia_s'): q = st.quantiles(v, n=4); res[k] = {'p25': round(q[0], 1), 'p50': round(q[1], 1), 'p75': round(q[2], 1)}
        else: res[k] = round(st.mean(v), 3)
        if not k.endswith('permanencia_s') and k != 'mortes': geral[k].append(res.get(k + '_por100', res.get(k)))
    res['rounds_full'] = nr
    out[mapa] = res
out['GERAL (média dos mapas)'] = {k: round(st.mean(v), 3) for k, v in geral.items()}
json.dump(out, open(r'D:\unity\CSManager\Tools\pro\regua.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
for m in ('de_mirage', 'de_inferno', 'GERAL (média dos mapas)'):
    print('==', m)
    for k, v in sorted(out[m].items()): print('  ', k, v)
