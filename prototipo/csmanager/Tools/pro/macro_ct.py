"""Caixa-preta dos pros x nossos bots — MACRO DO CT, na mesma régua (PLANO_SIMULACAO.md 7.1).
Lê os rounds de pro da Mirage (tactics_playbook.json, por região) e as caixas-pretas dos bots (Logs/partidas/<sessão>),
põe os dois no mesmo formato (cada jogador = lista de [posto, entra, sai]) e descreve, situação por situação, o que o CT
faz, com exemplos em linha do tempo. Número aqui só serve pra achar onde olhar.

Uso:  python -I Tools/pro/macro_ct.py pro                       -> rounds de pro (Mirage)
      python -I Tools/pro/macro_ct.py bot <pasta de caixas> ... -> nossos bots
Postos do CT: A (bomb A e plataforma) · Con (conector/selva) · Meio (janelão, praça, meio, rato) · B (bomb B, mercado, viela,
curta) · Base (spawn CT e rua do CT) · TR (terreno do TR: rampa, palácio, casarão, topo do meio, base TR).
"""
import sys, io, os, json, glob, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PLAYBOOK = r'D:\unity\demos_cs\cs-simulator\contract\artifacts\tactics_playbook.json'

# ---- posto de cada região (Mirage) e de cada área (Vila): a mesma função no round
POSTO_PRO = {
    'BombsiteA': 'A', 'Stairs': 'A', 'Scaffolding': 'TR',   # scaffolding = sacada do palácio (lado TR do A)
    'Jungle': 'Con', 'Connector': 'Con',
    'SnipersNest': 'Meio', 'Middle': 'Meio', 'Ladder': 'Meio', 'Underpass': 'Meio',
    'BombsiteB': 'B', 'Shop': 'B', 'Truck': 'B', 'Catwalk': 'B',
    'CTSpawn': 'Base',
    'TRamp': 'TR', 'PalaceAlley': 'TR', 'PalaceInterior': 'TR', 'TopofMid': 'TR', 'House': 'TR', 'TSpawn': 'TR',
    'SideAlley': 'TR', 'BackAlley': 'TR', 'Apartments': 'TR',
}
POSTO_VILA = {
    'A': 'A', 'Plataforma': 'A', 'Esc_plataforma': 'A',
    'Conector': 'Con', 'Conector_dobra': 'Con', 'Conector_fim': 'Con',
    'Janelao': 'Meio', 'Esc_janelao': 'Meio', 'Praca': 'Meio', 'Meio': 'Meio', 'Rato': 'Meio', 'Esc_rato': 'Meio', 'Rato_fundo': 'Meio',
    'B': 'B', 'Banca': 'B', 'Esc_banca': 'B', 'Carroca': 'B', 'Carroca_degrau': 'B', 'Mercado': 'B', 'Descida_mercado': 'B',
    'Passagem_viela': 'B', 'Viela': 'B', 'Esc_viela': 'B', 'Curta': 'B', 'Curta_sobe': 'B', 'Curta_fim': 'B',
    'CT': 'Base', 'Rua_CT': 'Base', 'Descida_CT': 'Base',
    'Rampa_saida': 'TR', 'Rampa_baixo': 'TR', 'Rampa_dobra': 'TR', 'Rampa': 'TR', 'Hall_palacio': 'TR', 'Esc_palacio': 'TR',
    'Palacio': 'TR', 'Palacio_sala': 'TR', 'Palacio_corredor': 'TR', 'Sacada_A': 'TR', 'Topo_meio': 'TR', 'Topo_entrada': 'TR', 'TR': 'TR',
    'Rua_B': 'TR', 'Beco_casarao': 'TR', 'Esc_casarao': 'TR', 'Casarao': 'TR', 'Casarao_corredor': 'TR', 'Casarao_dobra': 'TR',
    'Casarao_saida': 'TR', 'Tapete': 'TR',
}
# pro lado TR: de que bomb é cada região de aproximação (pra achar "o TR juntou num lado")
LADO_PRO = {'TRamp': 'A', 'PalaceAlley': 'A', 'PalaceInterior': 'A', 'Scaffolding': 'A', 'BombsiteA': 'A', 'Stairs': 'A', 'Jungle': 'A', 'Connector': 'A',
            'SideAlley': 'B', 'BackAlley': 'B', 'Apartments': 'B', 'BombsiteB': 'B', 'Shop': 'B', 'Truck': 'B', 'Catwalk': 'B', 'Underpass': 'B'}
LADO_VILA = {}
for a in ('Rampa_baixo', 'Rampa_dobra', 'Rampa', 'Palacio', 'Palacio_sala', 'Palacio_corredor', 'Sacada_A', 'A', 'Plataforma', 'Esc_plataforma', 'Conector', 'Conector_dobra', 'Conector_fim', 'Esc_palacio'): LADO_VILA[a] = 'A'
for a in ('Rua_B', 'Beco_casarao', 'Esc_casarao', 'Casarao', 'Casarao_corredor', 'Casarao_dobra', 'Casarao_saida', 'Tapete', 'B', 'Banca', 'Esc_banca', 'Carroca', 'Carroca_degrau',
          'Mercado', 'Descida_mercado', 'Passagem_viela', 'Viela', 'Esc_viela', 'Curta', 'Curta_sobe', 'Curta_fim', 'Rato', 'Esc_rato', 'Rato_fundo'): LADO_VILA[a] = 'B'
SITE_PRO = {'A': {'BombsiteA'}, 'B': {'BombsiteB'}}
SITE_VILA = {'A': {'A', 'Plataforma', 'Esc_plataforma'}, 'B': {'B', 'Banca', 'Esc_banca', 'Carroca', 'Carroca_degrau'}}


class Jog:
    def __init__(s, nome, steps, morreu): s.nome = nome; s.steps = steps; s.morreu = morreu   # steps: [(regiao, t0, t1)]
    def reg(s, t):
        if s.morreu is not None and t >= s.morreu: return None
        for r, a, b in s.steps:
            if a <= t < b: return r
        return s.steps[-1][0] if s.steps and t >= s.steps[-1][2] - 0.01 and (s.morreu is None or t < s.morreu) else None
    def vivo(s, t): return s.morreu is None or t < s.morreu


class Round:
    pass


def le_pro():
    d = json.load(open(PLAYBOOK, encoding='utf-8'))['maps']['de_mirage']; pl = d['places']; out = []
    for R in d['rounds']:
        r = Round(); r.id = R['id']; r.dur = R['duration_s']; r.venc = 'CT' if R['winner_side'] == 'CT' else 'TR'; r.motivo = R['reason']
        r.full = R['t_buy'] == 'full' and R['ct_buy'] == 'full'
        r.plant = (R['plant']['site'], R['plant']['t']) if R.get('plant') else None
        r.T = [Jog('T%d' % i, [(pl[a], b, c) for a, b, c in j['steps']], j.get('died_at')) for i, j in enumerate(R['T'])]
        r.CT = [Jog('CT%d' % i, [(pl[a], b, c) for a, b, c in j['steps']], j.get('died_at')) for i, j in enumerate(R['CT'])]
        r.posto = POSTO_PRO; r.lado = LADO_PRO; r.site = SITE_PRO; r.ordens = {}
        out.append(r)
    return out


def grade():
    g = {}; arq = os.path.join(RAIZ, 'Tools', 'pro', 'areas_vila.tsv')
    it = io.open(arq, encoding='utf-8'); p, nx, ny = map(int, next(it).split('\t'))
    for l in it:
        c = l.rstrip('\n').split('\t'); g[(int(c[0]), int(c[1]))] = c[2]
    return p, g


def le_bot(pastas):
    P, G = grade(); out = []
    for pasta in pastas:
        for arq in sorted(glob.glob(os.path.join(pasta, '*.tsv'))):
            quadros = {}; nomes = {}; time = {}; armas = {}; ordens = collections.defaultdict(list); plant = None; t = 0; fim = ('', ''); ultT = {}
            acabou = False
            for l in io.open(arq, encoding='utf-8'):
                c = l.rstrip('\n').split('\t')
                if c[0] == 'FIM': fim = (c[1] if len(c) > 1 else '', c[2] if len(c) > 2 else ''); continue
                # o round acaba na linha X (caixas novas) ou na primeira fase "round ganho" (caixas antigas): o pós-round não conta
                if c[0] == 'X' or c[0] == 'F' and len(c) > 2 and c[2].startswith('round ganho'): acabou = True
                if acabou: continue
                if c[0] == 'T':
                    t = float(c[1])
                    if c[2] == '1' and plant is None: plant = (t, float(c[3]), float(c[4]))
                elif c[0] == 'A':
                    i = int(c[1]); nomes[i] = c[2]; time[i] = c[3]; armas.setdefault(i, c[4])
                    a = G.get((int(float(c[5]) // P), int(float(c[6]) // P)))
                    if a is not None: quadros.setdefault(i, []).append((t, a))
                    ultT[i] = t
                elif c[0] == 'O': ordens[int(c[1])].append((t, c[2] if len(c) > 2 else ''))
                elif c[0] == 'FIM': fim = (c[1] if len(c) > 1 else '', c[2] if len(c) > 2 else '')
            if t < 15 or not fim[0]: continue
            r = Round(); r.id = os.path.basename(os.path.dirname(arq))[-8:] + '/' + os.path.basename(arq)[:-4]; r.venc = fim[0]; r.motivo = fim[1]
            # o round "de verdade" acaba quando alguém ganha; a caixa-preta grava também o pós-round
            r.dur = t
            r.T = []; r.CT = []; r.ordens = {}
            for i in sorted(nomes):
                st = []
                for tt, a in quadros.get(i, []):
                    if st and st[-1][0] == a: st[-1][2] = tt + 0.25
                    else: st.append([a, tt, tt + 0.25])
                # tira passagem relâmpago (< 0,75 s) entre dois trechos da mesma área
                lim = []
                for s in st:
                    if lim and s[2] - s[1] < 0.75 and len(lim) >= 1: lim[-1][2] = s[2]; continue
                    if lim and lim[-1][0] == s[0]: lim[-1][2] = s[2]
                    else: lim.append(s)
                morreu = ultT[i] + 0.25 if ultT[i] < t - 0.5 else None
                j = Jog(nomes[i], [tuple(s) for s in lim], morreu); j.arma = armas[i]
                (r.T if time[i] == 'TR' else r.CT).append(j); r.ordens[nomes[i]] = ordens[i]
            if len(r.T) != 5 or len(r.CT) != 5: continue
            rif = sum(1 for j in r.CT if j.arma in ('m4', 'm4s', 'ak', 'awp', 'aug', 'famas', 'galil', 'sg')) >= 3 and sum(1 for j in r.T if j.arma in ('m4', 'm4s', 'ak', 'awp', 'aug', 'famas', 'galil', 'sg')) >= 3
            r.full = rif
            r.plant = None
            if plant:
                a = G.get((int(plant[1] // P), int(plant[2] // P)))
                r.plant = ('A' if plant[1] > 500 else 'B', plant[0])
            # fim do round: último instante em que os dois times ainda decidiam (bomba explode/defuse/eliminação): usa a última morte ou o fim
            r.posto = POSTO_VILA; r.lado = LADO_VILA; r.site = SITE_VILA
            out.append(r)
    return out


# ---------------------------------------------------------------- leitura
NOMEP = {'A': 'A', 'Con': 'conector', 'Meio': 'meio', 'B': 'B', 'Base': 'base', 'TR': 'terreno TR', None: '—'}

def posto(r, j, t):
    g = j.reg(t); return r.posto.get(g) if g else None

def vivos(js, t): return [j for j in js if j.vivo(t)]

def linha(r, j, ate=None):
    """Linha do tempo de um jogador em palavras: posto(hora de entrada) → ..."""
    out = []; ult = None
    for g, a, b in j.steps:
        if ate is not None and a > ate: break
        p = r.posto.get(g, '?')
        if p != ult:
            out.append('%s %d s' % (NOMEP.get(p, p), a)); ult = p
    s = ' → '.join(out)
    if j.morreu is not None and (ate is None or j.morreu <= ate): s += ' ✝%d s' % j.morreu
    return '%-7s %s' % (j.nome, s)

def bloco(r, t0=None, marca=''):
    pl = 'plant %s aos %d s' % (r.plant[0], r.plant[1]) if r.plant else 'sem plant'
    s = ['  · %s — %d s, %s, venceu %s (%s)%s' % (r.id, r.dur, pl, r.venc, r.motivo, marca)]
    for j in r.CT: s.append('      ' + linha(r, j))
    mt = sorted((j.morreu, j.nome) for j in r.T if j.morreu is not None)
    if mt: s.append('      TR caem: ' + ', '.join('%d s' % m for m, _ in mt))
    return '\n'.join(s)

def pct(n, d): return '%d%%' % round(100.0 * n / d) if d else '—'

def tabela(titulo, cont, total=None, k=8):
    total = total or sum(cont.values())
    print('  ' + titulo + ' (%d casos)' % total)
    for v, n in cont.most_common(k): print('     %4s  %s' % (pct(n, total), v))


def analisa(rs, nome, nex=20):
    full = [r for r in rs if r.full]
    print('=' * 100); print('%s — %d rounds, %d de compra cheia dos dois lados (os que valem aqui)' % (nome, len(rs), len(full))); print('=' * 100)

    # ---------- 1. setup aos 20 s
    print('\n1. SETUP: onde estão os 5 CT aos 20 s (A / conector / meio / B / base+avançado)')
    c = collections.Counter(); av = collections.Counter(); ex = []
    for r in full:
        ps = [posto(r, j, 20) for j in r.CT if j.vivo(20)]
        if len(ps) < 5: continue
        k = (ps.count('A'), ps.count('Con'), ps.count('Meio'), ps.count('B'), ps.count('Base') + ps.count('TR') + ps.count(None))
        c['%d A · %d conector · %d meio · %d B · %d outro' % k] += 1
        ladoA = ps.count('A') + ps.count('Con'); ladoB = ps.count('B')
        av['lado A (bomb+conector) %d · meio %d · lado B %d' % (ladoA, ps.count('Meio'), ladoB)] += 1
    tabela('por posto', c); tabela('por lado', av)

    # ---------- 2. quanto o CT se mexe antes do primeiro contato
    print('\n2. ANTES DO CONTATO: dos 20 s até a primeira baixa (de qualquer lado), quantos CT trocam de posto')
    c = collections.Counter(); c2 = collections.Counter(); n = 0
    for r in full:
        m = sorted([j.morreu for j in r.T + r.CT if j.morreu is not None]); t1 = min(m[0] if m else r.dur, r.plant[1] if r.plant else 1e9, r.dur)
        if t1 < 30: continue
        n += 1; mud = 0
        for j in r.CT:
            a, b = posto(r, j, 20), posto(r, j, t1 - 0.5)
            if a != b: mud += 1; c2['%s → %s' % (NOMEP.get(a, a), NOMEP.get(b, b))] += 1
        c['%d trocaram de posto' % mud] += 1
    tabela('quantos trocam (rounds em que o 1º contato vem depois dos 30 s)', c, n); tabela('de onde pra onde (por jogador que trocou)', c2, k=10)

    # ---------- 3. primeira baixa do CT antes do plant: o que os outros 4 fazem em 10 s
    print('\n3. CAIU O PRIMEIRO CT (5 → 4) antes do plant, TR ainda com 4+: o que os outros 4 fazem nos 10 s seguintes')
    c = collections.Counter(); cq = collections.Counter(); ex = []; n = 0; viz = collections.Counter()
    for r in full:
        mc = sorted([(j.morreu, j) for j in r.CT if j.morreu is not None], key=lambda x: x[0])
        if not mc: continue
        t0, morto = mc[0]
        if r.plant and r.plant[1] <= t0 + 10: continue
        if len(vivos(r.T, t0)) < 4 or t0 + 10 > r.dur or t0 < 12: continue
        onde = posto(r, morto, t0 - 0.3); n += 1
        foi = fica = sai = 0; desc = []
        for j in r.CT:
            if j is morto: continue
            a, b = posto(r, j, t0), posto(r, j, min(t0 + 10, (j.morreu or 1e9) - 0.3))
            if a == b: fica += 1
            elif b == onde: foi += 1
            else: sai += 1
            if a == onde:
                viz['colega no MESMO posto do morto: ' + ('ficou no posto' if b == onde else 'saiu pra ' + NOMEP.get(b, str(b)))] += 1
            if a != b: cq['%s → %s (morte em %s)' % (NOMEP.get(a, a), NOMEP.get(b, b), NOMEP.get(onde, onde))] += 1
        c['%d ficam no posto · %d vão pro posto do morto · %d mudam pra outro lugar' % (fica, foi, sai)] += 1
        ex.append((r, ' — 1º CT cai aos %d s em %s' % (t0, NOMEP.get(onde, onde))))
    tabela('os outros 4', c, n); tabela('quem estava junto do morto', viz); tabela('trocas de posto nos 10 s', cq, k=10)
    for r, m in ex[:nex]: print(bloco(r, marca=m))

    # ---------- 4. CT em vantagem (caiu TR primeiro, 5x4): avança ou segura?
    print('\n4. CT EM VANTAGEM 5x4 antes do plant: nos 15 s seguintes alguém vai pro terreno do TR ou troca de posto?')
    c = collections.Counter(); n = 0; ex = []
    for r in full:
        m = sorted([(j.morreu, 'T') for j in r.T if j.morreu is not None] + [(j.morreu, 'C') for j in r.CT if j.morreu is not None])
        if not m or m[0][1] != 'T': continue
        t0 = m[0][0]
        if r.plant and r.plant[1] <= t0 + 15: continue
        if t0 + 15 > r.dur: continue
        n += 1; av = 0; tr = 0
        for j in r.CT:
            a = posto(r, j, t0); fim = min(t0 + 15, (j.morreu or 1e9) - 0.3)
            ps = set(posto(r, j, t0 + k * 0.5) for k in range(int((fim - t0) * 2) + 1))
            if 'TR' in ps and a != 'TR': av += 1
            elif len(ps - {None}) > 1: tr += 1
        c['%s · %s' % ('ninguém avança no terreno TR' if av == 0 else '%d avança(m) no terreno TR' % av, 'ninguém troca de posto' if tr == 0 else '%d troca(m) de posto' % tr)] += 1
        ex.append((r, ' — 1º TR cai aos %d s' % t0))
    tabela('reação à vantagem', c, n)
    for r, m in ex[:nex]: print(bloco(r, marca=m))

    # ---------- 5. TR junta 3+ num lado: quantos CT estão lá, quanto demora a ajuda, quem fica no outro bomb
    print('\n5. O TR JUNTA 3+ NUM LADO antes do plant: CT no lado atacado na hora, 5 s e 10 s depois; e quantos ficam no outro lado')
    cA = collections.Counter(); n = 0; ex = []; outro = collections.Counter()
    def no_lado(r, t, lado):
        k = 0
        for j in r.CT:
            p = posto(r, j, t)
            if p is None: continue
            if lado == 'A' and p in ('A', 'Con'): k += 1
            if lado == 'B' and p == 'B': k += 1
        return k
    for r in full:
        tf = min(r.plant[1] if r.plant else 1e9, r.dur); t = 15.0; ach = None
        while t < tf:
            ls = [r.lado.get(j.reg(t)) for j in r.T if j.vivo(t)]
            for L in 'AB':
                # 3+ no lado E pelo menos um já na boca do bomb (no posto do CT ou região de site)
                if ls.count(L) >= 3 and any(j.vivo(t) and j.reg(t) in r.site[L] for j in r.T): ach = (t, L)
            if ach: break
            t += 0.5
        if not ach: continue
        t0, L = ach; O = 'B' if L == 'A' else 'A'; n += 1
        a0 = no_lado(r, max(t0 - 5, 0), L); a1 = no_lado(r, t0, L); a2 = no_lado(r, min(t0 + 8, tf - 0.1), L)
        vv = len(vivos(r.CT, min(t0 + 8, tf - 0.1)))
        cA['5 s antes de o TR pisar no bomb: %d CT do lado · na hora: %d' % (a0, a1)] += 1
        outro['8 s depois: %d de %d CT vivos do lado atacado, %d no outro lado' % (a2, vv, no_lado(r, min(t0 + 8, tf - 0.1), O))] += 1
        ex.append((r, ' — TR entra no %s aos %d s' % (L, t0)))
    tabela('gente no lado atacado', cA, n); tabela('8 s depois', outro, n, k=10)
    for r, m in ex[:nex]: print(bloco(r, marca=m))

    # ---------- 6. retake
    print('\n6. PLANTOU: quem está vivo, onde, e como o CT volta pro bomb')
    c = collections.Counter(); res = collections.defaultdict(lambda: [0, 0]); ent = collections.Counter(); junto = collections.Counter(); ex = []; n = 0; salva = collections.Counter(); tempo = []
    for r in full:
        if not r.plant: continue
        L, tp = r.plant; cv = vivos(r.CT, tp); tv = vivos(r.T, tp)
        if not cv or not tv: continue
        n += 1; k = '%d CT x %d TR' % (len(cv), len(tv)); res[k][0] += 1; res[k][1] += r.venc == 'CT'
        dentro = sum(1 for j in cv if j.reg(tp) in r.site[L])
        c['%d de %d CT vivos já dentro do bomb na hora do plant' % (dentro, len(cv))] += 1
        # entradas no bomb depois do plant
        es = []
        for j in cv:
            if j.reg(tp) in r.site[L]: continue
            te = None; ant = None
            for g, a, b in j.steps:
                if a >= tp and g in r.site[L] and (j.morreu is None or a < j.morreu): te = a; break
                if b > tp: ant = g
            if te is not None: es.append((te - tp, r.posto.get(ant, '?') if ant else '?'))
        es.sort(); fora = len(cv) - dentro
        if fora:
            ent['dos %d que estavam fora, %d entram no bomb' % (fora, len(es))] += 1
            if es: tempo.append(es[0][0])
            if len(es) >= 2:
                d = es[1][0] - es[0][0]
                junto['1º e 2º entram com %s' % ('até 3 s de diferença (juntos)' if d <= 3 else '3 a 8 s' if d <= 8 else 'mais de 8 s (um de cada vez)')] += 1
                junto['vêm ' + ('pelo mesmo caminho' if es[0][1] == es[1][1] else 'por dois caminhos (%s)' % ' + '.join(sorted(NOMEP.get(e[1], e[1]) for e in es[:2])))] += 0
            if len(es) == 0 and len(cv) <= len(tv): salva['ninguém tentou (salvou ou morreu no caminho) com %s' % k] += 1
        ex.append((r, ' — %s no plant, %d já no bomb' % (k, dentro)))
    tabela('no instante do plant', c, n, k=10); tabela('quem estava fora', ent, k=10); tabela('entrada do 1º e do 2º', junto, sum(v for kk, v in junto.items() if kk.startswith('1º')))
    if tempo:
        tempo.sort(); print('  primeiro CT de fora pisa no bomb: mediana %d s depois do plant (1/4 dos casos em até %d s, 3/4 em até %d s)' % (tempo[len(tempo) // 2], tempo[len(tempo) // 4], tempo[3 * len(tempo) // 4]))
    print('  quem ganha depois do plant, por placar de vivos (casos · CT ganha)')
    for k in sorted(res, key=lambda x: -res[x][0])[:12]: print('     %-12s %4d casos · CT ganha %s' % (k, res[k][0], pct(res[k][1], res[k][0])))
    tot = sum(v[0] for v in res.values()); print('     TOTAL        %4d casos · CT ganha %s' % (tot, pct(sum(v[1] for v in res.values()), tot)))
    for r, m in ex[:nex]: print(bloco(r, marca=m))


if __name__ == '__main__':
    if len(sys.argv) < 2 or sys.argv[1] == 'pro': analisa(le_pro(), 'PROS (Mirage)')
    else: analisa(le_bot(sys.argv[2:]), 'BOTS ' + ' '.join(os.path.basename(p.rstrip('/\\')) for p in sys.argv[2:]))
