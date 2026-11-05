# Híbrido: candidato cru da ElevenLabs (eleven/bruto) + camadas do nosso motor -> som de jogo (saida/<grupo>/<nome>_K.wav).
#  1) carrega, corta silêncio, rejeita candidato ruim (dois estouros no tiro, começo atrasado, sem corpo);
#  2) tiro: corpo da IA + sub limpo + estalo + ambiente da vila + compressão + trava do eco, botões ajustados pela régua
#     do CS2 (mesmo custo do ajusta.py); distante feito do MESMO seco (ar + eco do casario);
#  3) foley/mundo: limpa, nivela e escolhe os melhores candidatos (régua do CS2 quando há par).
# Uso: python hibrido.py [grupo/nome,grupo,...] [--geracoes G]
import os, sys, glob, json, numpy as np, soundfile as sf
from scipy.signal import resample_poly
from multiprocessing import Pool
from motor import *
import armas, ajusta, regua

AQUI = os.path.dirname(os.path.abspath(__file__))
BRUTO = os.path.join(AQUI, 'eleven', 'bruto')
SAIDA = os.path.join(AQUI, 'saida')
AJ_H = os.path.join(AQUI, 'ajuste_hibrido.json')
REF = regua.REF
# aprovados pelo usuário no sintético (página, 2026-11-04): o híbrido NÃO sobrescreve
MANTER = {('passo', 'pedra'), ('granada', 'smoke'), ('impacto', 'corpo'), ('impacto', 'colete'), ('impacto', 'cabeca'), ('granada', 'apagou')}
# par do CS2 pra régua de foley (só referência de forma/duração/brilho)
REF_FOLEY = {('ak', 'pente_sai'): 'ak47/ak47_clipout_01.wav', ('ak', 'ferrolho'): 'ak47/ak47_boltpull_01.wav', ('ak', 'sacar'): 'ak47/ak47_draw.wav',
             ('awp', 'ferrolho_tras'): 'awp/awp_boltback.wav', ('awp', 'ferrolho_frente'): 'awp/awp_boltforward.wav', ('awp', 'sacar'): 'awp/awp_draw.wav',
             ('glock', 'pente_sai'): 'glock18/glock_clipout.wav', ('glock', 'pente_entra'): 'glock18/glock_clipin.wav',
             ('glock', 'ferrolho_frente'): 'glock18/glock_sliderelease.wav', ('glock', 'sacar'): 'glock18/glock_draw.wav'}


def carregar(p):
    x, sr = sf.read(p, always_2d=True); x = x.mean(1)
    if sr != SR: x = resample_poly(x, SR, sr)
    return x


def env5(x):
    w = int(0.005 * SR); return np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')) + 1e-9


def aparar(x, piso_db=-45, antes_ms=4):
    e = env5(x); db = 20 * np.log10(e / e.max())
    vivo = np.where(db > piso_db)[0]
    if not len(vivo): return x
    i0 = max(0, vivo[0] - int(antes_ms / 1000 * SR)); i1 = min(len(x), vivo[-1] + int(0.02 * SR))
    y = x[i0:i1].copy(); f = min(len(y) // 6, int(0.03 * SR)); y[-f:] *= np.linspace(1, 0, f) ** 2
    y[:int(0.001 * SR)] *= np.linspace(0, 1, int(0.001 * SR))
    return y


def aparar_tiro(x):
    """Corta o pré-ruído: começa ~5 ms antes do estouro principal (último ponto antes do pico a -24 dB)."""
    e = env5(x); db = 20 * np.log10(e / e.max()); pk = int(np.argmax(e))
    baixo = np.where(db[:pk] < -24)[0]
    i0 = max(0, (baixo[-1] if len(baixo) else 0) - int(0.005 * SR))
    y = x[i0:].copy(); y[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))
    return aparar(y, -50, 0)


def estouros(x, lim_db=-7, sep_ms=90):
    """Quantos picos fortes separados: tiro bom tem 1."""
    e = env5(x); db = 20 * np.log10(e / e.max()); n = 0; ult = -10 ** 9
    for i in np.where(db > lim_db)[0]:
        if i - ult > sep_ms / 1000 * SR: n += 1
        ult = i
    return n


TOM_MAX = 9.0   # CS2: nenhum tiro passa de 7,9 dB (tonal.py); acima disso soa "laser/furadeira" (nota do usuário)


def aceita_tiro(x):
    from tonal import tonalidade
    if tonalidade(x, SR) > TOM_MAX: return False, f'tom {tonalidade(x, SR):.1f} dB (laser)'
    if estouros(x, -3, 250) > 2: return False, 'vários estouros'
    e = env5(x); cedo = e[:int(0.04 * SR)].max()
    if 20 * np.log10(cedo / e.max()) < -4: return False, 'estouro fraco no começo'
    if len(x) < 0.15 * SR: return False, 'curto'
    return True, ''


# ---------------- tiro híbrido ----------------
BOT_H = ['ia', 'sub', 'sub_tau', 'crack', 'envio', 't60', 'seca', 'comp_db', 'comp_razao', 'agudo_db', 'grave_db', 'drive']
NEU_H = dict(ia=1.0, sub=0.35, sub_tau=1.0, crack=0.4, envio=0.5, t60=1.0, seca=0.25, comp_db=-14, comp_razao=3.0, agudo_db=0, grave_db=0, drive=1.0)
LIM_H = {'comp_db': (-30, -4), 'comp_razao': (1, 8), 'agudo_db': (-8, 10), 'grave_db': (-8, 8)}


def tiro_hibrido(a, ia, seed, K):
    p = armas.ARMAS[a]; r = Rng(seed); j = lambda v, f=0.05: r.var(v, f)
    t60 = p['cauda'][0] * K['t60']; dur = max(1.6, t60 * 1.25); n = int(dur * SR)
    seco = np.zeros(n); coloca(seco, ia / (np.abs(ia).max() + 1e-9) * K['ia'], 0.0)
    # secador: a IA já vem com eco rolando; depois de 60 ms a cauda dela cai com tau 'seca'
    t = np.arange(n) / SR; seco *= np.where(t < 0.06, 1.0, np.exp(-(t - 0.06) / max(0.02, K['seca'])))
    if p['crack'] and K['crack'] > 0.01: coloca(seco, hp(onda_n(j(p['crack'][0])), 1800) * K['crack'], 0.0004)
    seco = prateleira_grave(seco, 150, K['grave_db']); seco = agudo(seco, K['agudo_db'])
    seco = satura(seco / (np.abs(seco).max() + 1e-9), 1.2 * K['drive'])
    ir = ir_ambiente(t60 * 1.1, j(t60), r, armas.REF_PERTO, p['cauda'][2])
    x = seco + convolve(lp(seco, 7000), ir)[:n] * p['cauda'][1] * K['envio'] * 3.0
    f0, f1, tf, ta, g = p['boom']
    x += lp(boom(min(90, f1 * 1.6), max(32, f1 * 0.8), 0.03, 0.07 * K['sub_tau'] * (2.5 if armas.a_grande(p) else 1), dur, r, harm=0), 140) * K['sub']
    x = compressor(x, K['comp_db'], K['comp_razao'])
    return acabamento(direto_manda(x, 50, 0.7)), seco


def distante_hibrido(a, seco, seed, K=None):
    """Mesmo tiro visto de longe: ar come o agudo, sem mecânica, eco do casario (botões d_* já ajustados do sintético)."""
    p = armas.ARMAS[a]; K = K or botoes_dist(a); r = Rng(seed + 1000)
    t60 = p['cauda'][0] * 1.45 * K['d_t60']; dur = max(2.4, t60 * 1.15); n = int(dur * SR)
    dlp = p['dist_lp'] * K['d_lp']
    # de longe só chega o ESTOURO (150 ms); a cauda vem do nosso eco do casario, nunca da cauda da IA
    m = int(0.15 * SR); est = seco[:m].copy(); est[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR)) ** 2
    s = np.zeros(n); coloca(s, lp(est, dlp), 0)
    s = prateleira_grave(s, 150, K['d_grave_db']); s = satura(s / (np.abs(s).max() + 1e-9), 1.4)
    ir = ir_ambiente(t60 * 1.15, t60, r, armas.REF_LONGE, min(2500, dlp * 1.4), pre=0.05 * K['d_pre'])
    x = s * 0.8 * K['d_direto'] + convolve(s, ir)[:n] * (2.6 + 3 * p['cauda'][1]) * K['d_envio']
    if K['d_eco_g'] > 0.01:
        k = int(np.clip(K['d_eco_t'], 0.12, 1.2) * SR)
        if k < n: x[k:] += lp(s, dlp * 0.7)[:n - k] * K['d_eco_g']
    x = compressor(lp(x, dlp * 1.3), K['d_comp_db'], 3.0)
    return acabamento(direto_manda(x, 60), fade_ms=120)


def eco(x):
    """Repiques do envelope depois do estouro (som subindo de novo = eco). Soma dos dB de subida acima de 2 dB."""
    w = int(0.012 * SR); e = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')) + 1e-9
    db = 20 * np.log10(e / e.max()); i0 = int(np.argmax(e)) + int(0.05 * SR); i1 = min(len(db), i0 + int(1.0 * SR))
    tot = 0.0; vale = db[i0] if i0 < len(db) else 0
    for i in range(i0, i1, int(0.005 * SR)):
        if db[i] < -45: break
        vale = min(vale, db[i])
        if db[i] - vale > 2: tot += db[i] - vale - 2; vale = db[i]
    return tot


AJ_D = os.path.join(AQUI, 'ajuste_dist_hibrido.json')


def botoes_dist(a):
    K = armas.botoes(a)
    if os.path.exists(AJ_D): K.update(json.load(open(AJ_D, encoding='utf-8')).get(a, {}))
    return K


def ajustar_dist(a, ger=30, lam=8):
    """(1+λ)-ES dos botões d_* em cima do estouro da IA, contra o distante do CS2."""
    aj = json.load(open(AJ_H, encoding='utf-8'))
    if a not in aj or not regua.MAPA[a][1]: return a, None
    cands = [x for x in (aparar_tiro(carregar(c)) for c in sorted(glob.glob(os.path.join(BRUTO, a, 'tiro_c*.mp3')))) if aceita_tiro(x)[0]]
    secos = [tiro_hibrido(a, cands[k % len(cands)], 100 + k, aj[a]['K'])[1] for k in range(2)]
    A = ajusta.alvo(sorted(glob.glob(os.path.join(REF, regua.MAPA[a][1]))))
    custo = lambda K: float(np.mean([ajusta.custo(ajusta.feat(distante_hibrido(a, sc, 100 + k, K)), A) for k, sc in enumerate(secos)]))
    K = botoes_dist(a); c = custo(K); rng = np.random.default_rng(9); sig = 0.4
    for g in range(ger):
        fs = [ajusta.muta(K, armas.BOTOES_D, sig, rng) for _ in range(lam)]; cs = [custo(f) for f in fs]; i = int(np.argmin(cs))
        if cs[i] < c: K, c = fs[i], cs[i]; sig = min(0.6, sig * 1.15)
        else: sig = max(0.05, sig * 0.82)
    return a, ({n: K[n] for n in armas.BOTOES_D}, round(c, 2))


def custo_h(args):
    a, ias, K, A = args
    cs = []
    for i, ia in enumerate(ias):
        x = tiro_hibrido(a, ia, 11 + i, K)[0]
        f = ajusta.feat(x)
        # usuário: "som de perto com muito eco" -> nada de eco ou cauda ALÉM do que o CS2 tem
        cs.append(ajusta.custo(f, A) + max(0.0, eco(x) - A['eco']) / 2 + max(0.0, f['env'][7] - A['env'][7]) / 2 + max(0.0, f['env'][8] - A['env'][8]) / 2)
    return float(np.mean(cs))


def muta_h(K, sig, rng):
    N = dict(K)
    for n in BOT_H:
        if n in LIM_H: lo, hi = LIM_H[n]; N[n] = float(np.clip(K[n] + rng.normal() * sig * (hi - lo) * 0.25, lo, hi))
        else: N[n] = float(np.clip(max(K[n], 0.02) * np.exp(rng.normal() * sig), 0.0 if n in ('sub', 'crack') else 0.03 if n == 'seca' else 0.3, 0.6 if n in ('envio', 'seca') else 4.0))
    return N


def variacao(x, k):
    """Variação no jeito do CS (mesma gravação, diferença sutil): pitch ±1,5% e ataque ±1 dB. k=0 é a base."""
    if k == 0: return x
    r = np.random.default_rng(1000 + k)
    f = 1 + r.uniform(-0.015, 0.015); y = np.interp(np.arange(0, len(x) - 1, f), np.arange(len(x)), x)
    g = 10 ** (r.uniform(-1, 1) / 20); n = int(0.03 * SR)
    y[:n] *= np.linspace(g, 1, n)
    return y


def ajustar_tiro(a, ger=35, lam=8, evita=None):
    cands = sorted(glob.glob(os.path.join(BRUTO, a, 'tiro_c*.mp3')))
    novos = [c for c in cands if int(os.path.basename(c).rsplit('_c', 1)[1][:-4]) >= 10]
    if novos: cands = novos
    ias, motivos = [], []
    for c in cands:
        x = aparar_tiro(carregar(c)); ok, m = aceita_tiro(x)
        (ias if ok else motivos).append(x if ok else f'{os.path.basename(c)}: {m}')
    if not ias: return a, None, motivos
    refs = sorted(glob.glob(os.path.join(REF, regua.MAPA[a][0])))
    A = ajusta.alvo(refs); A['eco'] = float(np.mean([eco(carregar(r)) for r in refs]))
    # escolhe os melhores candidatos pela régua (com botões neutros) e ajusta os botões em cima deles
    notas = [custo_h((a, [ia], dict(NEU_H), A)) for ia in ias]
    if evita is not None:   # MAC-10 tem que soar diferente da MP9 (nota do usuário): pune quem fica perto dela
        v_ev = regua.vetor({**regua.media([os.path.join(SAIDA, evita, 'tiro_0.wav')])})
        for i, ia in enumerate(ias):
            f = ajusta.feat(tiro_hibrido(a, ia, 11, dict(NEU_H))[0]); d = np.linalg.norm(regua.vetor(f) - v_ev)
            notas[i] += max(0.0, 1.2 - d) * 6
    ordem = list(np.argsort(notas)); ias = [ias[i] for i in ordem[:1]]   # UMA base (variações sutis saem dela)
    K = dict(NEU_H); c = custo_h((a, ias[:1], K, A)); rng = np.random.default_rng(3); sig = 0.35
    for g in range(ger):
        fs = [muta_h(K, sig, rng) for _ in range(lam)]; cs = [custo_h((a, ias[:1], f, A)) for f in fs]; i = int(np.argmin(cs))
        if cs[i] < c: K, c = fs[i], cs[i]; sig = min(0.6, sig * 1.15)
        else: sig = max(0.05, sig * 0.82)
    d = os.path.join(SAIDA, a); os.makedirs(d, exist_ok=True)
    for k in range(4):
        ia = variacao(ias[0], k); x, seco = tiro_hibrido(a, ia, 100 + k, K)
        salvar(os.path.join(d, f'tiro_{k}.wav'), x); salvar(os.path.join(d, f'distante_{k}.wav'), distante_hibrido(a, seco, 100 + k))
    return a, dict(K=K, custo=round(c, 2), usados=len(ias)), motivos


# ---------------- foley / mundo ----------------
def nivel(g, n):
    if g == 'passo' or n in ('escada',): return -9
    if g in ('ambiente',): return -14
    if g == 'hud': return -6
    if g == 'musica': return -4
    if n in ('sacar', 'pegar', 'zoom', 'vazio_fuzil', 'vazio_pistola'): return -6
    return -1.5


def foley(g, n):
    cands = sorted(glob.glob(os.path.join(BRUTO, g, f'{n}_c*.mp3')))
    if not cands: return 0
    novos = [c for c in cands if int(os.path.basename(c).rsplit('_c', 1)[1][:-4]) >= 10]
    if novos: cands = novos   # pedido refeito (v3): só os candidatos novos
    laco = g == 'ambiente' or n in ('fogo_loop', 'molotov_voando') or (g == 'musica' and n in ('menu', 'intervalo'))
    xs = [carregar(c) if laco else aparar(carregar(c)) for c in cands]
    ref = REF_FOLEY.get((g, n))
    if ref and os.path.exists(os.path.join(REF, ref)):
        A = ajusta.alvo([os.path.join(REF, ref)])
        def nota(x):
            f = ajusta.feat(x); return abs(np.log(f['brilho'] / A['brilho'])) + abs(np.log(f['d40'] / A['d40'])) + float(np.abs(f['env'] - A['env']).mean()) / 6
        xs = [xs[i] for i in np.argsort([nota(x) for x in xs])]
    d = os.path.join(SAIDA, g); os.makedirs(d, exist_ok=True)
    for k, x in enumerate(xs):
        x = hp(x, 30)
        if laco:   # emenda do laço: cruza o fim com o começo
            f = int(0.4 * SR); x[:f] = x[:f] * np.linspace(0, 1, f) + x[-f:] * np.linspace(1, 0, f); x = x[:-f]
            x = x / (np.abs(x).max() + 1e-9) * 10 ** (nivel(g, n) / 20)
        else:
            x = acabamento(x, nivel(g, n), 20)
        salvar(os.path.join(d, f'{n}_{k}.wav'), x)
    import re   # tira sobra de versão anterior com mais variações (senão o sorteio mistura)
    for f in glob.glob(os.path.join(d, f'{n}_*.wav')):
        m = re.fullmatch(rf'{re.escape(n)}_(\d+)\.wav', os.path.basename(f))
        if m and int(m.group(1)) >= len(xs): os.remove(f)
    return len(xs)


def main():
    args = sys.argv[1:]
    if '--geracoes' in args: i = args.index('--geracoes'); del args[i:i + 2]
    so = [x for x in args if not x.startswith('--')]
    so = so[0].split(',') if so else None
    ger = int(sys.argv[sys.argv.index('--geracoes') + 1]) if '--geracoes' in sys.argv else 35
    quer = lambda g, n: not so or g in so or f'{g}/{n}' in so
    tiros = [a for a in armas.ARMAS if quer(a, 'tiro') and glob.glob(os.path.join(BRUTO, a, 'tiro_c*.mp3'))]
    aj = json.load(open(AJ_H, encoding='utf-8')) if os.path.exists(AJ_H) else {}
    if tiros:
        with Pool(min(12, len(tiros))) as pool:
            for a, info, mot in pool.starmap(ajustar_tiro, [(a, ger, 8, 'mp9' if a == 'mac10' else None) for a in tiros]):
                print(f'tiro {a}: ' + (f"custo {info['custo']} com {info['usados']} candidatos" if info else 'NENHUM candidato aceito') + (' | rejeitados: ' + '; '.join(mot) if mot else ''))
                if info: aj[a] = info
        json.dump(aj, open(AJ_H, 'w', encoding='utf-8'), indent=1)
    total = 0
    for gdir in sorted(glob.glob(os.path.join(BRUTO, '*'))):
        g = os.path.basename(gdir)
        for n in sorted({os.path.basename(f).rsplit('_c', 1)[0] for f in glob.glob(os.path.join(gdir, '*_c*.mp3'))}):
            if n == 'tiro' or not quer(g, n) or (g, n) in MANTER: continue
            total += foley(g, n)
    print('foley/mundo do híbrido:', total, 'arquivos')


if __name__ == '__main__':
    main()
