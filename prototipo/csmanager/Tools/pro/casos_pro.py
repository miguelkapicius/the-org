"""Casos de pro (Mirage) em linha do tempo, com as regioes pelo nome real e onde estao os TR.
Uso: python -I Tools/pro/casos_pro.py <1..6> [quantos=20] [passo=1]
Situacoes (7 = so retakes com 3+ CT vivos no plant): 1 setup 0-25s · 2 antes do contato · 3 1o CT cai · 4 CT em vantagem · 5 TR junta 3+ · 6 pos-plant.
Numero aqui so serve pra achar onde olhar; a leitura e feita nos casos."""
import sys, io, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import macro_ct as M

ABREV = {'Apartments':'Apts','BackAlley':'BackAl','BombsiteA':'siteA','BombsiteB':'siteB','CTSpawn':'CTsp','Catwalk':'Cat','Connector':'Conn',
         'House':'House','Jungle':'Jung','Ladder':'Ladd','Middle':'Mid','PalaceAlley':'PalAl','PalaceInterior':'PalInt','Scaffolding':'Scaff',
         'Shop':'Shop','SideAlley':'SideAl','SnipersNest':'Snip','Stairs':'Stairs','TRamp':'Ramp','TSpawn':'Tsp','TopofMid':'TopMid','Truck':'Truck','Underpass':'Under'}
def ab(g): return ABREV.get(g, g)

def trecho(j, t0, t1):
    """regioes do jogador entre t0 e t1: 'Reg@entrada' (entrada relativa ao inicio da janela nao; absoluta)"""
    out = []
    for g, a, b in j.steps:
        if b <= t0 or a >= t1: continue
        if j.morreu is not None and a >= j.morreu: break
        out.append('%s@%d' % (ab(g), max(a, t0) if a < t0 else a))
    s = ' > '.join(out)
    if j.morreu is not None and t0 <= j.morreu <= t1 + 0.01: s += ' X%d' % j.morreu
    elif j.morreu is not None and j.morreu < t0: s = 'morto antes'
    return s

def tr_em(r, t):
    import collections
    c = collections.Counter(ab(j.reg(t)) for j in r.T if j.vivo(t) and j.reg(t))
    return ' '.join('%s%s' % (k, '' if n == 1 else 'x%d' % n) for k, n in c.most_common())

def mostra(r, t0, t1, marca, passoT=5):
    pl = 'plant %s@%d' % (r.plant[0], r.plant[1]) if r.plant else 'sem plant'
    print('--- %s | %ds, %s, venceu %s (%s) | %s' % (r.id, r.dur, pl, r.venc, r.motivo, marca))
    for j in r.CT: print('   %-4s %s' % (j.nome, trecho(j, t0, t1)))
    t = t0
    while t <= t1 + 0.01:
        nT = sum(1 for j in r.T if j.vivo(t))
        print('   T%3d s (%d vivos): %s' % (t, nT, tr_em(r, t))); t += passoT
    mt = sorted(int(j.morreu) for j in r.T if j.morreu is not None and t0 <= j.morreu <= t1)
    if mt: print('   TR caem (s): %s' % mt)

def escolhe(lista, n):
    if len(lista) <= n: return lista
    return [lista[int(i * len(lista) / n + len(lista) / n / 2)] for i in range(n)]

def main():
    sit = int(sys.argv[1]); n = int(sys.argv[2]) if len(sys.argv) > 2 else 20
    full = [r for r in M.le_pro() if r.full]
    print('rounds full x full:', len(full))
    casos = []
    for r in full:
        mc = sorted(j.morreu for j in r.CT if j.morreu is not None); mt = sorted(j.morreu for j in r.T if j.morreu is not None)
        allm = sorted(mc + mt); pt = r.plant[1] if r.plant else 1e9
        if sit == 1:
            casos.append((r, 0, 25, 'setup'))
        elif sit == 2:
            t1 = min(allm[0] if allm else r.dur, pt, r.dur)
            if t1 >= 30: casos.append((r, 15, t1 + 1, 'ate a 1a baixa aos %d' % t1))
        elif sit == 3:
            if mc and (not mt or True):
                t0 = mc[0]
                if t0 >= 12 and t0 < pt - 10 and sum(1 for j in r.T if j.vivo(t0)) >= 4: casos.append((r, t0 - 6, t0 + 15, '1o CT cai aos %d' % t0))
        elif sit == 4:
            if mt and (not mc or mt[0] < mc[0]):
                t0 = mt[0]
                if t0 >= 12 and t0 < pt - 10: casos.append((r, t0 - 5, t0 + 20, '1o TR cai aos %d' % t0))
        elif sit == 5:
            tf = min(pt, r.dur); t = 15.0; ach = None
            while t < tf:
                ls = [r.lado.get(j.reg(t)) for j in r.T if j.vivo(t)]
                for L in 'AB':
                    if ls.count(L) >= 3 and any(j.vivo(t) and j.reg(t) in r.site[L] for j in r.T): ach = (t, L)
                if ach: break
                t += 0.5
            if ach: casos.append((r, max(ach[0] - 10, 0), ach[0] + 15, 'TR 3+ entra no %s aos %d' % (ach[1], ach[0])))
        elif sit in (6, 7):
            if r.plant:
                tp = r.plant[1]
                if any(j.vivo(tp) for j in r.CT) and any(j.vivo(tp) for j in r.T):
                    cv = sum(1 for j in r.CT if j.vivo(tp)); tv = sum(1 for j in r.T if j.vivo(tp))
                    if sit == 7 and cv < 3: continue
                    casos.append((r, tp - 4, min(tp + 35, r.dur), '%dCT x %dTR no plant do %s aos %d' % (cv, tv, r.plant[0], tp)))
    print('casos qualificados:', len(casos))
    for c in escolhe(casos, n): mostra(*c)

main()
