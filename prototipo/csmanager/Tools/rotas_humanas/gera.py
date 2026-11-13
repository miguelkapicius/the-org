# Modelo de rota humanizada (sim-apoio, 2026-11-08): transições região -> próxima região e permanência, tiradas das demos pro.
# Rodar da raiz do CSManager:  python -I Tools/rotas_humanas/gera.py [valida]
# Entrada: D:/unity/demos_cs/cs-simulator/contract/artifacts/{tactics_playbook.json, tactics.json}
# Saída:   Assets/Game/Sim/RotaHumanaDadosVila.cs (Mirage -> Vila) e RotaHumanaDadosBrasa.cs (Inferno -> Brasa). Gerados: não editar.
#
# Chave da transição: lado (T/CT), faixa do round (0-15, 15-30, 30-50, 50+ s, na hora em que sai da região), plantado (sim/não).
# Suavização em degraus: contagem da chave  <- lado+plantado de todas as faixas  <- função do lugar nos 7 mapas
#   p = (n_chave*p_chave + K1*p_lado) / (n_chave + K1);  p_lado = (n_lado*p_lado_obs + K2*p_funcao) / (n_lado + K2)
# p_funcao: P(função da próxima | função da atual, lado, faixa, plantado) nos 7 mapas, repartida entre os vizinhos reais da
# região (vizinho = qualquer próxima já vista nesse mapa, qualquer lado), com um piso pequeno pra todo vizinho.
# Permanência: p25/p50/p75 por lado e região (só passagens completas: a última, cortada por morte ou fim de round, não entra);
# com menos de 15 passagens usa a da função do lugar nos 7 mapas.
import json, os, sys, io, re, math, random, collections
R = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = 'D:/unity/demos_cs/cs-simulator/contract/artifacts/'
sys.path.insert(0, os.path.join(R, 'Tools/estrategias'))


def mapa_de(arq):
    src = io.open(os.path.join(R, 'Tools/estrategias', arq), encoding='utf-8').read()
    m = re.search(r'^MAPA = (\{.*?\n\})', src, re.S | re.M)
    return eval(m.group(1), {})


ALVO = {'vila': ('de_mirage', mapa_de('converter.py')), 'brasa': ('de_inferno', mapa_de('converter_brasa.py'))}
FAIXAS = (15, 30, 50)            # 0-15, 15-30, 30-50, 50+
K1, K2, K3, PISO, MINP, MIN2 = 8.0, 8.0, 8.0, 0.02, 0.005, 5
pb = json.load(open(ART + 'tactics_playbook.json', encoding='utf-8'))['maps']
tac = json.load(open(ART + 'tactics.json', encoding='utf-8'))['maps']


def faixa(t): return sum(t >= f for f in FAIXAS)


def sorteia(qs, u):
    xs = (0.0,) + QS + (1.0,); ys = (qs[0] * 0.5,) + tuple(qs) + (qs[-1] * 1.15,)
    for i in range(1, len(xs)):
        if u <= xs[i]: return ys[i - 1] + (ys[i] - ys[i - 1]) * (u - xs[i - 1]) / (xs[i] - xs[i - 1])
    return ys[-1]


def funcao(mapa, lugar):
    p = tac[mapa]['places'][lugar]
    if p['kind'] == 'spawn': return 'spawn_' + ('T' if p['side_share']['T'] > 0.5 else 'CT')
    if p['kind'] == 'site': return 'bomb'
    s = p['side_share']['T']
    return p['zone'] + '_' + ('tr' if s > 0.7 else 'ct' if s < 0.3 else 'disputa')


def passagens(mapa, nomes=None):
    """Por jogador: lista de (região, entra, sai, completa, t_plant). nomes: índice -> nome (None = nome do pro)."""
    M = pb[mapa]; P = M['places']
    for r in M['rounds']:
        tp = r['plant']['t'] if r.get('plant') else 1e9
        for lado in ('T', 'CT'):
            for pj in r[lado]:
                seq = []
                for reg, a, b in pj['steps']:
                    if not (0 <= reg < len(P)): continue
                    n = P[reg] if nomes is None else nomes.get(P[reg])
                    if n is None: continue
                    if seq and seq[-1][0] == n: seq[-1][2] = b
                    else: seq.append([n, a, b])
                for i, (n, a, b) in enumerate(seq):
                    yield lado, n, a, b, i < len(seq) - 1, tp, (seq[i + 1][0] if i < len(seq) - 1 else None), i, (seq[i - 1][0] if i > 0 else None)


QS = (0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99)   # quantis guardados da permanência (o sorteio interpola entre eles)
def quartis(v):
    """Quantis por Kaplan-Meier: v = [(duração, completa)]. A passagem cortada por morte ou fim de round é censurada (ficou
    pelo menos isso): sem isso quem segura/lurka 50 s some da conta (quem fica muito sai morto ou no fim do round)."""
    v = sorted(v); n = len(v); S = 1.0; curva = []
    for i, (d, comp) in enumerate(v):
        if comp: S *= 1 - 1 / (n - i)
        curva.append((d, 1 - S))
    def q(p):
        for d, F in curva:
            if F >= p: return d
        return curva[-1][0]
    return tuple(q(x) for x in QS)


# ---- função do lugar, nos 7 mapas
tr_fun = collections.defaultdict(collections.Counter); perm_fun = collections.defaultdict(list)
for mp in pb:
    fn = {p: funcao(mp, p) for p in pb[mp]['places'] if p in tac[mp]['places']}
    for lado, n, a, b, comp, tp, prox, _, _ant in passagens(mp):
        if n not in fn: continue
        perm_fun[(lado, fn[n])].append((b - a, comp))
        if comp:
            if prox in fn: tr_fun[(lado, faixa(b), b >= tp, fn[n])][fn[prox]] += 1


def gera(alvo):
    mapa, MAPA = ALVO[alvo]
    fn = {MAPA[p]: funcao(mapa, p) for p in MAPA}
    regs = sorted(set(MAPA.values()))
    cheia = collections.defaultdict(collections.Counter); porlado = collections.defaultdict(collections.Counter)
    viz = collections.defaultdict(set); perm = collections.defaultdict(list); ctx2 = collections.defaultdict(collections.Counter)
    for lado, n, a, b, comp, tp, prox, _, ant in passagens(mapa, MAPA):
        perm[(lado, n)].append((b - a, comp))
        if not comp: continue
        cheia[(lado, faixa(b), b >= tp, n)][prox] += 1
        porlado[(lado, b >= tp, n)][prox] += 1
        viz[n].add(prox)
        if ant: ctx2[(lado, b >= tp, ant, n)][prox] += 1
    trans = []
    for lado in ('T', 'CT'):
        for fx in range(len(FAIXAS) + 1):
            for pl in (False, True):
                for de in regs:
                    V = sorted(viz[de])
                    if not V: continue
                    # prior pela função, repartido entre os vizinhos de cada função
                    cf = tr_fun.get((lado, fx, pl, fn[de])) or tr_fun.get((lado, fx, not pl, fn[de])) or collections.Counter()
                    porf = collections.Counter(fn[v] for v in V); tf = sum(cf[f] for f in porf) or 1
                    pf = {v: (cf[fn[v]] / tf) / porf[fn[v]] for v in V}
                    pf = {v: (1 - PISO * len(V)) * pf[v] + PISO for v in V} if sum(pf.values()) > 0 else {v: 1 / len(V) for v in V}
                    s = sum(pf.values()); pf = {v: x / s for v, x in pf.items()}
                    c2 = porlado[(lado, pl, de)]; n2 = sum(c2.values())
                    p2 = {v: (c2[v] + K2 * pf[v]) / (n2 + K2) for v in V}
                    c1 = cheia[(lado, fx, pl, de)]; n1 = sum(c1.values())
                    p1 = {v: (c1[v] + K1 * p2[v]) / (n1 + K1) for v in V}
                    p1 = {v: x for v, x in p1.items() if x >= MINP}; s = sum(p1.values())
                    for v, x in sorted(p1.items(), key=lambda q: -q[1]): trans.append((lado, fx, pl, de, v, x / s, n1))
    perms = []
    for lado in ('T', 'CT'):
        for rg in regs:
            v = perm[(lado, rg)]
            if len(v) < 15: v = perm_fun[(lado, fn[rg])]; fonte = 'funcao'
            else: fonte = 'regiao'
            if v: perms.append((lado, rg, quartis(v), len(v), fonte))
    # 2ª ordem (de onde veio): só a contagem crua com n >= MIN2; o C# mistura com a de 1ª ordem: (n2*p2 + K3*p1)/(n2 + K3)
    trans2 = []
    for (lado, pl, ant, de), c in sorted(ctx2.items()):
        n2 = sum(c.values())
        if n2 < MIN2: continue
        for v, x in c.most_common(): trans2.append((lado, pl, ant, de, v, x / n2, n2))
    return regs, fn, trans, perms, trans2


def cs(alvo, regs, fn, trans, perms, trans2):
    sufixo = alvo.capitalize()
    q = lambda s: '"' + s.replace('"', '\\"') + '"'
    L = [f'// GERADO por Tools/rotas_humanas/gera.py a partir das demos pro ({ALVO[alvo][0]}) — não editar à mão.',
         'namespace CSM.Sim', '{', '    public static partial class RotaHumana', '    {',
         f'        /// <summary>Regiões do pro traduzidas pro mapa ({alvo}) e a função de cada uma.</summary>',
         f'        static readonly (string regiao, string funcao)[] REG_{sufixo.upper()} = {{ ' + ', '.join(f'({q(r)}, {q(fn[r])})' for r in regs) + ' };',
         '        // lado (0 = TR, 1 = CT), faixa (0: 0-15 s, 1: 15-30, 2: 30-50, 3: 50+), plantado, de, para, probabilidade, n da chave',
         f'        static readonly (byte lado, byte faixa, bool plantado, string de, string para, float p, int n)[] TRANS_{sufixo.upper()} = {{']
    L += [f'            ({0 if l == "T" else 1}, {fx}, {"true" if pl else "false"}, {q(de)}, {q(v)}, {p:.4f}f, {n}),' for l, fx, pl, de, v, p, n in trans]
    L += ['        };', '        // lado, região, quantis da permanência em s (p5, p10, p25, p50, p75, p90, p95, p99), n, fonte (regiao = medida nessa região; funcao = função do lugar nos 7 mapas)',
          f'        static readonly (byte lado, string regiao, float[] q, int n, string fonte)[] PERM_{sufixo.upper()} = {{']
    L += [f'            ({0 if l == "T" else 1}, {q(rg)}, new[] {{ ' + ', '.join(f'{x:.2f}f' for x in qs) + f' }}, {n}, {q(f)}),' for l, rg, qs, n, f in perms]
    L += ['        };', '        // 2ª ordem: lado, plantado, de onde veio, região atual, próxima, probabilidade observada, n do contexto (só n >= 5)',
          f'        static readonly (byte lado, bool plantado, string anterior, string de, string para, float p, int n)[] TRANS2_{sufixo.upper()} = {{']
    L += [f'            ({0 if l == "T" else 1}, {"true" if pl else "false"}, {q(an)}, {q(de)}, {q(v)}, {p:.4f}f, {n}),' for l, pl, an, de, v, p, n in trans2]
    L += ['        };', '    }', '}']   # K3 fica no RotaHumana.cs (igual ao K3 daqui)
    out = os.path.join(R, f'Assets/Game/Sim/RotaHumanaDados{sufixo}.cs')
    io.open(out, 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(L) + '\n')
    print('ok', out, len(trans), 'transições', len(trans2), 'de 2ª ordem', len(perms), 'permanências')


# ---- validação: 1.000 sequências TR do modelo x as do pro (rota = 4 primeiras regiões depois do spawn, até o bomb ou 50 s)
def valida(alvo, trans, perms, trans2, N=1000, tam=4, seed=1, ordem2=True):
    mapa, MAPA = ALVO[alvo]
    T = collections.defaultdict(list)
    for l, fx, pl, de, v, p, n in trans: T[(l, fx, pl, de)].append((v, p))
    Pm = {(l, rg): qs for l, rg, qs, n, f in perms}
    T2 = collections.defaultdict(dict); N2 = {}
    for l, pl, an, de, v, p, n in trans2: T2[(l, pl, an, de)][v] = p; N2[(l, pl, an, de)] = n
    rnd = random.Random(seed)

    def dur(l, rg):   # mesmo sorteio do RotaHumana.SorteiaPermanencia: interpola entre os quantis (0 e 1 nas pontas)
        return sorteia(Pm[(l, rg)], rnd.random())
    def amostra():
        rg, t, rota, ant = 'TR', 0.0, [], None
        while t < 50 and len(rota) < tam:
            t += dur('T', rg)
            if t >= 50: break   # entra na próxima depois dos 50 s: não conta (igual ao pro)
            ops = T.get(('T', faixa(t), False, rg))
            if not ops: break
            k2 = ('T', False, ant, rg)
            if ordem2 and k2 in N2:
                n2 = N2[k2]; ops = [(v, (n2 * T2[k2].get(v, 0) + K3 * p) / (n2 + K3)) for v, p in ops]
            x = rnd.random() * sum(p for _, p in ops)
            for v, p in ops:
                x -= p
                if x <= 0: break
            ant, rg = rg, v; rota.append(rg)
            if rg in ('A', 'B'): break
        return tuple(rota)
    mod = collections.Counter(amostra() for _ in range(N))
    pro = collections.Counter(); rota = None; fim = 0; parou = False
    def fecha():
        # só rota completa: chegou no bomb, fez as 'tam' regiões ou durou 50 s (quem morreu antes não entra: o modelo não morre)
        if rota and (parou or len(rota) >= tam or fim >= 50): pro[tuple(rota)] += 1
    for lado, n, a, b, comp, tp, prox, i, _ant in passagens(mapa, MAPA):
        if lado != 'T': continue
        if i == 0:
            fecha(); rota, parou, fim = [], n != 'TR', b
            continue  # o spawn
        fim = max(fim, b)
        if parou or len(rota) >= tam or a >= 50: continue
        rota.append(n)
        if n in ('A', 'B'): parou = True
    fecha()
    tot = sum(pro.values())
    print(f'\n{alvo}: 10 rotas TR mais comuns do pro (rota = até {tam} regiões depois do spawn, para no bomb), pro x modelo (N={N})')
    dif = []
    for rt, c in pro.most_common(10):
        a, b = 100 * c / tot, 100 * mod[rt] / N; dif.append(abs(a - b))
        print(f'  {a:5.1f}%  {b:5.1f}%   ' + ' > '.join(rt))
    top10m = sum(mod[rt] for rt, _ in pro.most_common(10)) / N * 100
    print(f'  soma das 10: pro {sum(c for _, c in pro.most_common(10)) / tot * 100:.1f}% · modelo {top10m:.1f}% · erro médio {sum(dif) / len(dif):.1f} pontos')
    print('  10 mais comuns do modelo:', '; '.join(f"{100 * c / N:.1f}% {' > '.join(rt)}" for rt, c in mod.most_common(10)))


for alvo in ('vila', 'brasa'):
    regs, fn, trans, perms, trans2 = gera(alvo)
    cs(alvo, regs, fn, trans, perms, trans2)
    if 'valida' in sys.argv:
        print('\n--- só 1ª ordem'); valida(alvo, trans, perms, trans2, ordem2=False)
        print('\n--- com a 2ª ordem (de onde veio)'); valida(alvo, trans, perms, trans2)
