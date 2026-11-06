# Gera os tiros das armas (perto + distante, várias variações) com o motor autoral.
# Uso: python armas.py [arma,arma...] [--var N]   -> Tools/sons/saida/<arma>/tiro_K.wav, distante_K.wav
import sys, os, json, numpy as np
from motor import *

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, 'saida')

# Vila marroquina a céu aberto: paredes de pedra perto (eco curto) e casario longe (rolagem).
REF_PERTO = [(0.018, 0.30, 6000), (0.031, 0.22, 5000), (0.047, 0.18, 4000), (0.069, 0.12, 3500), (0.095, 0.08, 3000)]
REF_LONGE = [(0.13, 0.55, 2200), (0.24, 0.42, 1800), (0.37, 0.33, 1500), (0.52, 0.25, 1300), (0.71, 0.18, 1100), (0.93, 0.11, 900)]

# Cada arma: blocos do tiro. T = duração do pulso da boca (ms). Ver README_SONS.md.
ARMAS = {
    'ak':     dict(T=1.7, sopro=1.0, crack=(0.34, 0.45), boom=(150, 46, 0.022, 0.095, 0.95), corpo=(140, 5200, 0.013, 0.085, 0.32, 1.0, 0.38, 0.35),
                   eq=[(210, 1.0, 4), (1700, 1.4, 3), (4800, 1.2, -3)], mec=(1050, 6, 0.055, 0.20, 0.011), chocalho=0.10,
                   cauda=(1.35, 0.38, 4200), drive=2.3, dist_lp=1500, dist_boom=0.35),
    'm4':     dict(T=1.35, sopro=1.0, crack=(0.30, 0.55), boom=(170, 58, 0.018, 0.075, 0.72), corpo=(190, 7000, 0.010, 0.075, 0.30, 1.0, 0.22, 0.2),
                   eq=[(320, 1.0, 2), (2600, 1.3, 3.5), (6500, 1.0, -2)], mec=(1550, 6, 0.04, 0.16, 0.009), chocalho=0.04,
                   cauda=(1.25, 0.36, 4800), drive=2.5, dist_lp=1900, dist_boom=0.3),
    'm4s':    dict(T=2.4, sopro=0.22, supressor=True, puff=(1600, 0.016, 0.42), crack=None, boom=(115, 52, 0.02, 0.055, 0.9),
                   corpo=(120, 2400, 0.008, 0.05, 0.25, 0.45, 0.15, 0.4), eq=[(150, 1.0, 3), (900, 1.5, 2)],
                   mec=(1850, 7, 0.045, 0.48, 0.007), chocalho=0.06, cauda=(0.75, 0.13, 3000), drive=1.8, dist_lp=900, dist_boom=0.25),
    'awp':    dict(T=2.7, sopro=1.25, crack=(0.5, 0.8), boom=(115, 36, 0.04, 0.24, 1.25), corpo=(70, 6000, 0.022, 0.2, 0.42, 1.0, 0.42, 0.5),
                   eq=[(85, 0.9, 5), (1400, 1.2, 3), (5000, 1.0, -2)], mec=(900, 5, 0.06, 0.08, 0.02), chocalho=0.0,
                   cauda=(2.7, 0.62, 3600), drive=2.9, dist_lp=900, dist_boom=1.0),
    'deagle': dict(T=2.0, sopro=1.05, crack=(0.36, 0.55), boom=(125, 44, 0.03, 0.15, 1.15), corpo=(110, 6000, 0.015, 0.11, 0.35, 0.9, 0.3, 0.45),
                   eq=[(100, 0.9, 5), (2000, 1.3, 2.5)], mec=(1250, 6, 0.04, 0.14, 0.012), chocalho=0.0,
                   cauda=(1.7, 0.46, 4000), drive=2.6, dist_lp=1300, dist_boom=0.6),
    'glock':  dict(T=0.95, sopro=0.85, crack=None, boom=(210, 85, 0.012, 0.036, 0.5), corpo=(300, 8000, 0.006, 0.045, 0.25, 1.0, 0.15, 0.1),
                   eq=[(2200, 1.3, 3.5), (500, 1.0, 1)], mec=(2100, 6, 0.028, 0.2, 0.006), chocalho=0.0,
                   cauda=(0.95, 0.30, 5000), drive=2.0, dist_lp=2200, dist_boom=0.2),
    'usp':    dict(T=2.1, sopro=0.2, supressor=True, puff=(2000, 0.010, 0.3), crack=None, boom=(98, 50, 0.016, 0.045, 0.95),
                   corpo=(110, 2000, 0.006, 0.035, 0.2, 0.35, 0.1, 0.4), eq=[(110, 1.0, 4)],
                   mec=(2300, 6, 0.03, 0.36, 0.005), chocalho=0.0, cauda=(0.55, 0.10, 2800), drive=1.6, dist_lp=800, dist_boom=0.2),
    'p250':   dict(T=1.05, sopro=0.9, crack=None, boom=(185, 70, 0.014, 0.05, 0.62), corpo=(250, 7500, 0.007, 0.06, 0.28, 1.0, 0.18, 0.15),
                   eq=[(1400, 1.2, 3), (300, 1.0, 2)], mec=(1900, 6, 0.03, 0.18, 0.007), chocalho=0.0,
                   cauda=(1.05, 0.32, 4800), drive=2.1, dist_lp=2000, dist_boom=0.25),
    'mp9':    dict(T=0.85, sopro=0.8, crack=None, boom=(190, 92, 0.012, 0.032, 0.52), corpo=(220, 6000, 0.006, 0.04, 0.25, 1.0, 0.25, 0.2),
                   eq=[(550, 1.0, 4.5), (2500, 1.5, 2), (7000, 1.0, -3)], mec=(2400, 6, 0.022, 0.18, 0.005), chocalho=0.03,
                   cauda=(0.95, 0.30, 4200), drive=2.2, dist_lp=1700, dist_boom=0.2),
    'mac10':  dict(T=0.9, sopro=0.85, crack=None, boom=(170, 75, 0.012, 0.036, 0.56), corpo=(190, 6000, 0.007, 0.05, 0.28, 1.0, 0.5, 0.25),
                   eq=[(350, 1.0, 3), (900, 2.0, 4), (6000, 1.0, -2)], mec=(1400, 6, 0.025, 0.28, 0.006), chocalho=0.14,
                   cauda=(0.95, 0.30, 4000), drive=2.4, dist_lp=1600, dist_boom=0.2),
    'tec9':   dict(T=1.0, sopro=0.9, crack=None, boom=(200, 80, 0.013, 0.04, 0.55), corpo=(260, 7000, 0.007, 0.05, 0.27, 1.0, 0.3, 0.15),
                   eq=[(1800, 1.3, 3), (600, 1.0, 2)], mec=(1700, 6, 0.03, 0.24, 0.007), chocalho=0.08,
                   cauda=(1.0, 0.30, 4600), drive=2.2, dist_lp=2000, dist_boom=0.22),
    'fiveseven': dict(T=1.05, sopro=0.9, crack=(0.22, 0.4), boom=(195, 75, 0.013, 0.048, 0.6), corpo=(260, 8000, 0.007, 0.055, 0.27, 1.0, 0.16, 0.12),
                   eq=[(2600, 1.3, 3.5), (350, 1.0, 1)], mec=(2000, 6, 0.028, 0.18, 0.006), chocalho=0.0,
                   cauda=(1.05, 0.32, 5000), drive=2.1, dist_lp=2100, dist_boom=0.22),
    'galil':  dict(T=1.55, sopro=1.0, crack=(0.32, 0.5), boom=(160, 50, 0.02, 0.085, 0.88), corpo=(160, 5800, 0.012, 0.08, 0.31, 1.0, 0.32, 0.3),
                   eq=[(260, 1.0, 3), (2000, 1.4, 3), (5500, 1.1, -2)], mec=(1150, 6, 0.05, 0.18, 0.01), chocalho=0.07,
                   cauda=(1.3, 0.37, 4400), drive=2.4, dist_lp=1600, dist_boom=0.32),
    'famas':  dict(T=1.3, sopro=1.0, crack=(0.30, 0.55), boom=(180, 60, 0.017, 0.07, 0.7), corpo=(200, 7400, 0.010, 0.07, 0.30, 1.0, 0.2, 0.2),
                   eq=[(380, 1.0, 2), (3000, 1.3, 4), (7000, 1.0, -2)], mec=(1650, 6, 0.04, 0.16, 0.009), chocalho=0.05,
                   cauda=(1.2, 0.35, 4900), drive=2.5, dist_lp=2000, dist_boom=0.28),
}


# Botões do ajuste automático (ajusta.py). 1.0 = preset; ganhos em escala, *_db em dB.
BOTOES = ['sopro', 'crack', 'boom', 'boom_tau', 'corpo', 'corpo_tr', 'corpo_tl', 'corpo_mix', 'mec', 'envio', 't60',
          'drive', 'comp_db', 'comp_razao', 'agudo_db', 'grave_db', 'sub', 'sub_tau', 'eco']
NEUTRO = dict(sopro=1, crack=1, boom=1, boom_tau=1, corpo=1, corpo_tr=1, corpo_tl=1, corpo_mix=1, mec=1, envio=1, t60=1,
              drive=1, comp_db=-14, comp_razao=3.0, agudo_db=0, grave_db=0, sub=0.3, sub_tau=1, eco=0.3)
BOTOES_D = ['d_eco_t', 'd_eco_g', 'd_direto', 'd_sopro', 'd_boom', 'd_corpo', 'd_tl', 'd_lp', 'd_envio', 'd_t60', 'd_pre', 'd_comp_db', 'd_grave_db']
NEUTRO_D = dict(d_eco_t=0.5, d_eco_g=0.3, d_direto=1, d_sopro=1, d_boom=1, d_corpo=1, d_tl=1, d_lp=1, d_envio=1, d_t60=1, d_pre=1, d_comp_db=-14, d_grave_db=0)
AJUSTE = os.path.join(AQUI, 'ajuste.json')


def botoes(a):
    k = dict(NEUTRO); k.update(NEUTRO_D)
    if os.path.exists(AJUSTE):
        k.update(json.load(open(AJUSTE, encoding='utf-8')).get(a, {}))
    return k


def a_grande(p):
    return p['T'] >= 2.5 and not p.get('supressor')


def tiro_perto(p, seed, K):
    r = Rng(seed); j = lambda x, f=0.06: r.var(x, f)
    dur = max(1.6, p['cauda'][0] * K['t60'] * 1.25)
    n = int(dur * SR); seco = np.zeros(n)
    # 1) gatilho/martelo: clique pequeno logo antes do estouro
    coloca(seco, clique(r, 3500, 0.003, 0.08), 0.0)
    t0 = 0.002
    # 2) pulso da boca (Friedlander) excitando o corpo da arma
    T = j(p['T']) / 1000
    pulso = friedlander(T, j(2.2), 0.05) * p['sopro'] * K['sopro']
    if p.get('supressor'): pulso = lp(pulso, 1400)
    coloca(seco, pulso * 2.2, t0)
    # 3) estalo supersônico
    if p['crack']:
        cr = hp(onda_n(j(p['crack'][0])), 1800) * p['crack'][1] * K['crack']
        coloca(seco, cr, t0 + 0.0004)
    # 4) estrondo (grave com queda de pitch)
    f0, f1, tf, ta, g = p['boom']
    coloca(seco, satura(boom(j(f0), j(f1), tf, j(ta) * K['boom_tau'], dur, r), 1.6) * g * K['boom'], t0)
    # 5) corpo de ruído (o "bang")
    lo, hi, tr, tl, mi, g, grao, cor = p['corpo']
    coloca(seco, corpo_ruido(dur, lo, hi, j(tr) * K['corpo_tr'], j(tl) * K['corpo_tl'], min(0.95, mi * K['corpo_mix']), r, cor, grao) * g * 1.6 * K['corpo'], t0)
    # 5b) supressor: sopro de gás escapando
    if p.get('supressor'):
        f, tau, g = p['puff']
        nn = int(0.2 * SR); puf = hp(r.ruido(nn), j(f)) * env_exp(nn, j(tau), 0.001)
        coloca(seco, lp(puf, 9000) * g, t0 + 0.001)
    # 6) mecânica: ferrolho batendo (modal) + chocalho
    base, nm, dec, g, quando = p['mec']
    coloca(seco, metal(r, j(base, 0.04), nm, j(dec), 0.25) * g * K['mec'], t0 + j(quando, 0.15))
    if p['chocalho'] > 0:
        for k in range(3):
            coloca(seco, metal(r, j(base * 0.72, 0.08), 5, 0.018, 0.08) * p['chocalho'] * K['mec'] * (0.7 ** k), t0 + quando + 0.018 + 0.011 * k + r.u(0, 0.004))
    # caráter da arma
    for f, q, db in p['eq']: seco = pico(seco, j(f, 0.04), q, db)
    seco = prateleira_grave(seco, 150, K['grave_db'])
    seco = agudo(seco, K['agudo_db'])
    seco = satura(seco / (np.abs(seco).max() + 1e-9), p['drive'] * K['drive'])
    # 7) ambiente (vila): reflexões curtas + cauda
    t60, envio, escuro = p['cauda']; t60 *= K['t60']
    ir = ir_ambiente(t60 * 1.1, j(t60), r, REF_PERTO, escuro)
    mol = convolve(lp(seco, 7000), ir)[:n] * envio * K['envio']
    x = seco + mol * 3.0
    # 7b) eco longo do casario (o "estrondo" que volta das paredes longe: AWP/Deagle)
    if K['eco'] > 0.01:
        ire = ir_ambiente(t60 * 1.2, j(t60) * 1.2, r, REF_LONGE, 1800, pre=0.1)
        x = x + convolve(lp(seco, 2500), ire)[:n] * K['eco'] * 2.0
    # 7c) sub limpo (fora da saturação): o peso no peito
    f0, f1, tf, ta, g = p['boom']
    sub = boom(min(90, f1 * 1.6), max(32, f1 * 0.8), 0.03, 0.07 * K['sub_tau'] * (2.5 if a_grande(p) else 1), dur, r, harm=0.0)
    x = x + lp(sub, 140) * K['sub']
    # 8) compressão de jogo: segura o corpo cheio e o estalo na frente
    x = compressor(x, K['comp_db'], K['comp_razao'])
    return acabamento(direto_manda(x))


def tiro_distante(p, seed, K):
    """Camada distante: o que chega do outro lado do mapa. Sem mecânica, agudo comido pelo ar,
    estrondo e eco rolando pelo casario."""
    r = Rng(seed + 1000); j = lambda x, f=0.06: r.var(x, f)
    t60 = p['cauda'][0] * 1.45 * K['d_t60']
    dur = max(2.4, t60 * 1.15)
    n = int(dur * SR); seco = np.zeros(n)
    dlp = p['dist_lp'] * K['d_lp']
    T = j(p['T']) / 1000 * 1.6
    coloca(seco, lp(friedlander(T, 2.0, 0.08), dlp) * 2.0 * p['sopro'] * K['d_sopro'], 0.0)
    f0, f1, tf, ta, g = p['boom']
    coloca(seco, boom(j(f0) * 0.8, j(f1) * 0.85, tf * 1.4, j(ta) * 1.5, dur, r) * g * p['dist_boom'] * 1.6 * K['d_boom'], 0.0)
    lo, hi, tr, tl, mi, g, grao, cor = p['corpo']
    coloca(seco, lp(corpo_ruido(dur, max(80, lo), min(hi, dlp * 1.6), tr * 1.8, tl * 1.6 * K['d_tl'], mi, r, cor + 0.3, grao * 0.5), dlp) * g * 1.4 * K['d_corpo'], 0.0)
    seco = prateleira_grave(seco, 150, K['d_grave_db'])
    seco = satura(seco / (np.abs(seco).max() + 1e-9), 1.4)
    ir = ir_ambiente(t60 * 1.15, j(t60), r, REF_LONGE, min(2500, dlp * 1.4), pre=0.05 * K['d_pre'])
    x = seco * 0.8 * K['d_direto'] + convolve(seco, ir)[:n] * (2.6 + 3 * p['cauda'][1]) * K['d_envio']
    # eco discreto do casario (a AWP volta forte ~0,6 s depois)
    if K['d_eco_g'] > 0.01:
        k = int(np.clip(K['d_eco_t'], 0.12, 1.2) * SR)
        if k < n: x[k:] += lp(seco, dlp * 0.7)[:n - k] * K['d_eco_g']
    x = lp(x, dlp * 1.3)
    x = compressor(x, K['d_comp_db'], 3.0)
    return acabamento(direto_manda(x, 60), fade_ms=120)


def semente(a, k):
    import zlib
    return (zlib.crc32(a.encode()) % 99991) * 10 + k


def gerar(lista, nvar):
    rel = {}
    for a in lista:
        p = ARMAS[a]; d = os.path.join(SAIDA, a); os.makedirs(d, exist_ok=True)
        for k in range(nvar):
            salvar(os.path.join(d, f'tiro_{k}.wav'), tiro_perto(p, semente(a, k), botoes(a)))
            salvar(os.path.join(d, f'distante_{k}.wav'), tiro_distante(p, semente(a, k), botoes(a)))
        rel[a] = nvar
        print('ok', a)
    return rel


if __name__ == '__main__':
    args = [x for x in sys.argv[1:] if not x.startswith('--')]
    nvar = int(sys.argv[sys.argv.index('--var') + 1]) if '--var' in sys.argv else 4
    if '--var' in sys.argv: args.remove(str(nvar))
    lista = args[0].split(',') if args else list(ARMAS)
    gerar(lista, nvar)
