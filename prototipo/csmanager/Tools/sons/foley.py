# Foley e sons de mundo (autoral): recarga, sacar, impactos, granadas, C4, faca, passo.
# Uso: python foley.py [grupo,...] [--var N]  -> saida/<grupo>/<nome>_K.wav
import sys, os, numpy as np
from motor import *

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, 'saida')


def atrito(r, dur, lo, hi, aspereza=0.6, sobe=0.3):
    """Raspar metal/polímero: ruído de banda com grão e envelope que sobe e cai."""
    n = int(dur * SR); t = np.arange(n) / SR
    x = bp(r.ruido(n), lo, hi, 2)
    am = lp(np.abs(r.ruido(n)), 300 + 600 * aspereza); am /= am.max() + 1e-9
    x *= (1 - aspereza) + aspereza * am
    pico_t = dur * sobe
    env = np.where(t < pico_t, t / max(pico_t, 1e-4), np.exp(-(t - pico_t) / (dur * 0.35)))
    return x * env


def batida(r, base, dec, peso=0.0, dur=0.3, brilho=1.0, n=6):
    """Peça metálica batendo: modal + clique, e 'peso' = corpo grave da arma junto."""
    y = metal(r, base, n, dec, dur, brilho)
    if peso > 0:
        y += peso * boom(base * 0.22, base * 0.15, 0.01, 0.03, dur, r, harm=0.1)
    return y


def chocalho_balas(r, dur=0.12, n=5):
    y = np.zeros(int(dur * SR))
    for k in range(n): coloca(y, metal(r, r.u(2800, 4200), 4, 0.012, 0.04) * r.u(0.2, 0.5), r.u(0, dur * 0.7))
    return y


def fim(x, pico=-1.0): return acabamento(x, pico, fade_ms=15)


# ---------------- armas (recarga / sacar) ----------------
ARMA_MEC = {  # base da batida (Hz), peso grave, plástico (0 metal .. 1 polímero), tamanho
    'ak': (900, 0.8, 0.1, 1.15), 'm4': (1300, 0.5, 0.3, 1.0), 'm4s': (1300, 0.5, 0.3, 1.0), 'awp': (1000, 0.7, 0.1, 1.2),
    'mp9': (1700, 0.3, 0.7, 0.85), 'mac10': (1500, 0.35, 0.2, 0.85), 'glock': (1900, 0.25, 0.8, 0.7), 'usp': (1800, 0.3, 0.5, 0.75),
    'p250': (1850, 0.25, 0.6, 0.7), 'deagle': (1200, 0.5, 0.0, 0.95),
    'tec9': (1600, 0.3, 0.1, 0.8), 'fiveseven': (1900, 0.25, 0.7, 0.72), 'galil': (950, 0.75, 0.1, 1.1), 'famas': (1250, 0.5, 0.5, 1.0),
}


def pente_sai(a, r):
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.45 * SR))
    coloca(x, clique(r, 3000, 0.004, 0.6), 0)                                    # botão/trava
    coloca(x, batida(r, base * 1.6, 0.02, 0, 0.1, 1 - plast) * 0.5, 0.004)
    coloca(x, atrito(r, 0.12 * tam, 900, 5000, 0.7, 0.25) * 0.8, 0.02)           # desliza pra fora
    coloca(x, chocalho_balas(r) * 0.6, 0.06)
    if a == 'ak': coloca(x, batida(r, 700, 0.04, 0.6, 0.2) * 0.6, 0.0)         # AK: rola o pente pra frente
    return fim(lp(x, 9000 - 3000 * plast))


def pente_entra(a, r):
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.5 * SR))
    coloca(x, atrito(r, 0.08 * tam, 700, 4500, 0.6, 0.6) * 0.5, 0)               # entrando no poço
    coloca(x, batida(r, base, 0.035, peso, 0.3, 1 - plast * 0.6) * 1.0, 0.075 * tam)   # encaixa
    coloca(x, clique(r, 2500, 0.004, 0.9), 0.078 * tam)                          # trava estalando
    coloca(x, chocalho_balas(r, 0.08, 3) * 0.4, 0.085 * tam)
    return fim(lp(x, 10000 - 3000 * plast))


def pente_bate(a, r):   # tapa no fundo do pente (AWP)
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.3 * SR)); coloca(x, batida(r, base * 0.8, 0.03, peso * 1.2, 0.25, 0.7), 0)
    coloca(x, lp(r.ruido(int(0.02 * SR)), 2000) * env_exp(int(0.02 * SR), 0.004) * 0.6, 0)
    return fim(x)


def ferrolho(a, r):     # puxa e solta (rifle/SMG): raspa pra trás + mola + batida forte na frente
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.55 * SR))
    coloca(x, clique(r, 3500, 0.003, 0.5), 0)
    coloca(x, atrito(r, 0.09, 1500, 7000, 0.5, 0.7) * 0.7, 0.003)
    coloca(x, modal([2600, 3900], [0.03, 0.02], [0.2, 0.12], 0.1, r) * 0.6, 0.09)   # mola
    t = 0.2 if a == 'ak' else 0.16
    coloca(x, batida(r, base * 1.1, 0.05, peso * 1.3, 0.3, 1.0, 7) * 1.2, t)      # bate na frente
    coloca(x, clique(r, 2000, 0.005, 1.0), t)
    return fim(x)


def ferrolho_tras(a, r):   # AWP: levanta e puxa
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.4 * SR))
    coloca(x, batida(r, base * 1.8, 0.02, 0, 0.1) * 0.7, 0)                       # levanta a alavanca
    coloca(x, atrito(r, 0.16, 1200, 6500, 0.4, 0.5) * 0.9, 0.03)
    coloca(x, batida(r, base * 1.2, 0.03, 0.3, 0.2) * 0.8, 0.19)                  # fim de curso
    return fim(x)


def ferrolho_frente(a, r):   # AWP empurra e tranca / pistola solta o ferrolho
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.4 * SR))
    if a == 'awp':
        coloca(x, atrito(r, 0.12, 1000, 6000, 0.4, 0.6) * 0.8, 0)
        coloca(x, batida(r, base, 0.04, peso, 0.25) * 1.0, 0.12)
        coloca(x, batida(r, base * 1.7, 0.02, 0, 0.1) * 0.7, 0.2)                 # abaixa a alavanca e tranca
    else:
        coloca(x, clique(r, 3000, 0.003, 0.4), 0)
        coloca(x, batida(r, base * 1.15, 0.035, peso * 1.4, 0.3, 1 - plast * 0.4, 7) * 1.2, 0.012)
    return fim(x)


def sacar(a, r):
    base, peso, plast, tam = ARMA_MEC[a]
    x = np.zeros(int(0.5 * SR))
    coloca(x, atrito(r, 0.22 * tam, 300, 2600, 0.8, 0.4) * 0.6, 0)                # pano/coldre
    coloca(x, batida(r, base * 1.4, 0.02, 0, 0.1, 0.7) * 0.4, 0.17 * tam)          # metal pega na mão
    coloca(x, clique(r, 3500, 0.003, 0.4), 0.2 * tam)
    return fim(x, -3)


# ---------------- impactos ----------------
def imp_parede(r):
    x = np.zeros(int(0.5 * SR))
    coloca(x, hp(onda_n(0.15), 1500) * 1.0, 0)
    coloca(x, corpo_ruido(0.3, 800, 9000, 0.004, 0.03, 0.2, r, 0, 0.6) * 0.9, 0)
    for k in range(8): coloca(x, bp(r.ruido(int(0.006 * SR)), 2000, 9000) * env_exp(int(0.006 * SR), 0.0015) * r.u(0.05, 0.25), r.u(0.01, 0.2))  # lasca/pó
    return fim(x, -2)


def imp_corpo(r, forte=1.0):
    x = np.zeros(int(0.35 * SR))
    coloca(x, boom(220, 120, 0.01, 0.025, 0.2, r) * 0.9 * forte, 0)
    coloca(x, corpo_ruido(0.15, 300, 3500, 0.006, 0.03, 0.2, r, 0.4, 0.4) * 0.8, 0)
    coloca(x, lp(atrito(r, 0.06, 400, 1800, 0.9, 0.2), 2000) * 0.4, 0.004)          # pano/carne
    return fim(x, -2)


def imp_capacete(r):   # HS com capacete, no formato do CS2: cabeça (aprovada) + "tonc" curto + chiado crocante agudo — sem sino
    x = np.zeros(int(0.45 * SR))
    coloca(x, imp_cabeca(r) * 0.8, 0)
    tonc = modal([r.u(680, 760), r.u(1450, 1600), r.u(2350, 2600)], [0.1, 0.03, 0.015], [1, 0.4, 0.2], 0.4, r, 0.02)
    coloca(x, tonc * 0.9, 0.001)
    n = int(0.08 * SR); crocante = bp(r.ruido(n), 6000, 12000) * env_exp(n, 0.012, 0.0003)
    crocante *= 0.6 + 0.4 * (lp(np.abs(r.ruido(n)), 2500) / 0.4)
    coloca(x, crocante * 0.7, 0)
    return fim(x, -1)


def imp_cabeca(r):
    x = imp_corpo(r, 1.3); x = pico(x, 1800, 1.2, 4)
    return fim(x, -1)


def imp_colete(r):
    x = np.zeros(int(0.35 * SR))
    coloca(x, boom(170, 100, 0.01, 0.03, 0.2, r) * 1.0, 0)
    coloca(x, lp(corpo_ruido(0.12, 200, 2500, 0.008, 0.04, 0.2, r, 0.6, 0.3), 2500) * 0.9, 0)
    return fim(x, -3)


# ---------------- granadas ----------------
def explosao(r, T_ms, f0, f1, tau, corpo_tau, t60, detritos=1.0, lpf=9000):
    dur = max(2.5, t60 * 1.3); n = int(dur * SR); x = np.zeros(n)
    coloca(x, friedlander(T_ms / 1000, 1.8, 0.2) * 2.5, 0)
    coloca(x, satura(boom(f0, f1, 0.05, tau, dur, r), 1.8) * 1.3, 0)
    coloca(x, corpo_ruido(dur, 60, 7000, corpo_tau * 0.3, corpo_tau, 0.5, r, 0.6, 0.5) * 1.8, 0)
    for k in range(int(25 * detritos)):  # pedra e lasca caindo
        coloca(x, batida(r, r.u(600, 2600), 0.015, 0, 0.06, 0.6) * r.u(0.03, 0.15), r.u(0.15, 1.1))
    x = satura(x / np.abs(x).max(), 2.2)
    ir = ir_ambiente(t60 * 1.1, t60, r, [(0.13, 0.55, 2200), (0.24, 0.42, 1800), (0.37, 0.33, 1500), (0.52, 0.25, 1300), (0.71, 0.18, 1100)], 3000)
    x = x + convolve(x, ir)[:n] * 2.5
    x = compressor(lp(x, lpf), -16, 3)
    return acabamento(x, -0.3, 200)


def distante_de(x, r, lpf=1200):
    n = len(x) + int(1.5 * SR); y = np.zeros(n); y[:len(x)] = lp(x, lpf)
    ir = ir_ambiente(2.4, 2.2, r, [(0.2, 0.6, 1200), (0.45, 0.45, 1000), (0.8, 0.3, 900), (1.2, 0.2, 800)], 1500, pre=0.08)
    y = y * 0.6 + convolve(y, ir)[:n] * 4
    return acabamento(lp(y, lpf), -0.5, 300)


def he(r): return explosao(r, 6.0, 90, 34, 0.35, 0.25, 2.0)
def c4_explode(r): return explosao(r, 14.0, 70, 26, 0.7, 0.55, 3.4, detritos=2.5, lpf=7000)


def flash(r):
    n = int(2.2 * SR); x = np.zeros(n)
    coloca(x, hp(friedlander(0.0016, 2.2, 0.03), 300) * 2.0, 0)                   # estalo seco
    coloca(x, corpo_ruido(0.4, 1500, 12000, 0.006, 0.05, 0.15, r, 0, 0.3) * 1.2, 0)
    coloca(x, boom(160, 70, 0.02, 0.06, 0.4, r) * 0.6, 0)
    coloca(x, modal([2350, 3480, 5120], [0.5, 0.35, 0.2], [0.18, 0.1, 0.06], 1.2, r) * 0.6, 0.004)   # lata vibrando
    coloca(x, hp(r.ruido(int(1.2 * SR)), 4000) * env_exp(int(1.2 * SR), 0.35, 0.02) * 0.08, 0.02)   # chiado de queima
    ir = ir_ambiente(1.6, 1.4, r, [(0.12, 0.4, 3000), (0.25, 0.3, 2500), (0.4, 0.2, 2000)], 5000)
    x = x + convolve(x, ir)[:n] * 2.0
    return acabamento(compressor(x, -14, 3), -0.5, 150)


def smoke(r):   # estoura a espoleta e chia soltando fumaça
    n = int(3.2 * SR); x = np.zeros(n)
    coloca(x, hp(friedlander(0.001, 2, 0.02), 500) * 1.2, 0)
    t = np.arange(int(3.0 * SR)) / SR
    chiado = bp(r.ruido(len(t)), 1800, 9000) * (np.clip(t / 0.08, 0, 1) * np.exp(-t / 1.4)) * 0.7
    chiado *= 0.7 + 0.3 * lp(np.abs(r.ruido(len(t))), 15) / 0.05
    coloca(x, chiado, 0.02)
    coloca(x, lp(corpo_ruido(2.8, 150, 1200, 0.2, 0.9, 0.6, r, 0.5, 0.5), 1200) * 0.5, 0.05)   # rugido da fumaça
    return acabamento(x, -2, 400)


def vidro(r, forte=1.0):
    y = modal([r.u(1800, 2400) * k for k in (1, 2.32, 3.9, 5.4)], [0.08, 0.05, 0.03, 0.02], [1, 0.7, 0.5, 0.3], 0.2, r, 0.03)
    return y * forte


def molotov(r):   # garrafa estoura + labareda acendendo
    n = int(2.5 * SR); x = np.zeros(n)
    coloca(x, vidro(r) * 0.8, 0)
    for k in range(18): coloca(x, vidro(r, r.u(0.1, 0.4)) * env_exp(int(0.2 * SR), 0.03), r.u(0.0, 0.25))   # cacos
    coloca(x, hp(r.ruido(int(0.01 * SR)), 2000) * 0.8, 0)
    t = np.arange(int(2.2 * SR)) / SR
    fogo = lp(r.ruido(len(t)), 2500) * np.clip(t / 0.15, 0, 1) * np.exp(-t / 0.9)
    coloca(x, fogo * 0.9, 0.05)
    coloca(x, boom(120, 50, 0.08, 0.25, 1.0, r) * 0.7, 0.06)                       # "vuush"
    return acabamento(x, -1, 300)


def molotov_ar(r):   # pegou fogo no ar e apagou sem chão
    n = int(1.4 * SR); x = np.zeros(n); t = np.arange(n) / SR
    coloca(x, vidro(r) * 0.6, 0)
    x += lp(r.ruido(n), 3000) * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.3) * 0.7
    return acabamento(x, -2, 200)


def apagou(r):   # fogo apagado pela smoke: vapor
    n = int(1.6 * SR); t = np.arange(n) / SR
    x = bp(r.ruido(n), 2500, 11000) * np.clip(t / 0.03, 0, 1) * np.exp(-t / 0.5)
    return acabamento(x, -3, 200)


def fogo_loop(r, dur=4.0):   # estalos de fogo em laço (o fim emenda no começo)
    n = int(dur * SR); t = np.arange(n) / SR
    x = lp(r.ruido(n), 1800) * 0.4 * (0.8 + 0.2 * np.sin(2 * np.pi * 0.7 * t))
    for k in range(int(dur * 22)):
        coloca(x, hp(r.ruido(int(0.004 * SR)), 1500) * env_exp(int(0.004 * SR), 0.0008) * r.u(0.1, 0.8), r.u(0, dur - 0.01))
    f = int(0.25 * SR); x[:f] = x[:f] * np.linspace(0, 1, f) + x[-f:] * np.linspace(1, 0, f); x = x[:-f]
    return x / np.abs(x).max() * 0.6


def quique_metal(r):
    x = np.zeros(int(0.3 * SR)); coloca(x, metal(r, r.u(1100, 1500), 6, 0.04, 0.25, 0.8), 0)
    coloca(x, lp(r.ruido(int(0.01 * SR)), 1500) * env_exp(int(0.01 * SR), 0.002) * 0.8, 0)
    return fim(x, -2)


def quique_vidro(r):
    x = np.zeros(int(0.3 * SR)); coloca(x, vidro(r, 0.8), 0)
    coloca(x, lp(r.ruido(int(0.01 * SR)), 1200) * env_exp(int(0.01 * SR), 0.002) * 0.9, 0)
    return fim(x, -2)


def pino(r):
    x = np.zeros(int(0.4 * SR))
    coloca(x, clique(r, 3000, 0.004, 0.6), 0)
    coloca(x, modal([4100, 6200, 8300], [0.12, 0.08, 0.05], [1, 0.5, 0.3], 0.3, r) * 0.5, 0.01)   # argola
    coloca(x, batida(r, 2500, 0.02, 0, 0.1) * 0.5, 0.1)                          # alavanca solta
    return fim(x, -3)


def arremesso(r):   # braço + "fuu" do ar
    n = int(0.45 * SR); t = np.arange(n) / SR
    x = atrito(r, 0.18, 300, 2000, 0.8, 0.4) * 0.5
    y = np.zeros(n); y[:len(x)] = x
    sw = bp(r.ruido(n), 500, 3000) * np.exp(-((t - 0.2) / 0.07) ** 2)
    return fim(y + sw * 0.9, -3)


# ---------------- C4 ----------------
def bip(r, f=3700, dur=0.11, rapido=False):
    n = int(dur * SR); t = np.arange(n) / SR
    fase = 2 * np.pi * np.cumsum(f * (1 + 0.012 * np.exp(-t / 0.01))) / SR
    y = np.sign(np.sin(fase)) * 0.35 + np.sin(fase) * 0.65                          # timbre de buzzer piezo
    y = lp(y, 9000) * np.clip(t / 0.002, 0, 1) * np.clip((dur - t) / 0.006, 0, 1)
    return acabamento(y, -3, 4)


def digita(r):   # teclado da C4: 7 teclas com bipinho
    x = np.zeros(int(1.6 * SR))
    for k in range(7):
        t = 0.2 * k + r.u(-0.02, 0.02)
        coloca(x, batida(r, 2200, 0.008, 0, 0.04, 0.4) * 0.5, t)
        coloca(x, bip(r, 2900 + 120 * (k % 3), 0.05) * 0.35, t + 0.01)
    return fim(x, -3)


def plantou(r):
    x = np.zeros(int(0.6 * SR))
    coloca(x, bip(r, 3700, 0.08) * 0.8, 0); coloca(x, bip(r, 4400, 0.16) * 0.9, 0.11)
    return fim(x, -2)


def desarme(r, kit=False):   # abre a tampa, corta/mexe nos fios
    d = 0.7 if kit else 1.2; x = np.zeros(int(d * SR))
    coloca(x, batida(r, 1800, 0.02, 0.2, 0.1) * 0.6, 0)
    for k in range(5 if kit else 8): coloca(x, clique(r, 2500, 0.004, r.u(0.3, 0.7)), r.u(0.1, d - 0.1))
    coloca(x, atrito(r, d * 0.6, 400, 3000, 0.9, 0.3) * 0.3, 0.05)
    return fim(x, -4)


# ---------------- faca / passo ----------------
def golpe(r):
    n = int(0.35 * SR); t = np.arange(n) / SR
    c = 0.12 + r.u(-0.02, 0.02)
    x = bp(r.ruido(n), 700, 6000) * np.exp(-((t - c) / 0.04) ** 2)
    return fim(x, -3)


def faca_acerto(r):
    x = golpe(r) * 0.4
    coloca(x, imp_corpo(r, 1.2) * 0.9, 0.11)
    coloca(x, atrito(r, 0.08, 1500, 7000, 0.5, 0.2) * 0.5, 0.115)
    return fim(x, -2)


def passo_pedra(r):
    x = np.zeros(int(0.25 * SR))
    coloca(x, boom(140, 90, 0.01, 0.018, 0.1, r) * 0.7, 0)                          # calcanhar
    coloca(x, corpo_ruido(0.08, 200, 5000, 0.005, 0.02, 0.2, r, 0.3, 0.6) * 0.6, 0)
    coloca(x, corpo_ruido(0.06, 1500, 8000, 0.003, 0.015, 0.2, r, 0, 0.8) * 0.35, r.u(0.03, 0.05))   # areia/pedrinha
    coloca(x, boom(170, 110, 0.01, 0.012, 0.08, r) * 0.4, r.u(0.06, 0.09))         # ponta do pé
    return fim(x, -6)


# ---------------- catálogo ----------------
def catalogo():
    c = {}
    for a in ARMA_MEC:
        c[(a, 'sacar')] = lambda r, a=a: sacar(a, r)
        c[(a, 'pente_sai')] = lambda r, a=a: pente_sai(a, r)
        c[(a, 'pente_entra')] = lambda r, a=a: pente_entra(a, r)
        if a == 'awp':
            c[(a, 'pente_bate')] = lambda r, a=a: pente_bate(a, r)
            c[(a, 'ferrolho_tras')] = lambda r, a=a: ferrolho_tras(a, r)
            c[(a, 'ferrolho_frente')] = lambda r, a=a: ferrolho_frente(a, r)
        elif a in ('glock', 'usp', 'p250', 'deagle'):
            c[(a, 'ferrolho_frente')] = lambda r, a=a: ferrolho_frente(a, r)
        else:
            c[(a, 'ferrolho')] = lambda r, a=a: ferrolho(a, r)
    c.update({('impacto', 'parede'): imp_parede, ('impacto', 'corpo'): imp_corpo, ('impacto', 'capacete'): imp_capacete,
              ('impacto', 'cabeca'): imp_cabeca, ('impacto', 'colete'): imp_colete,
              ('granada', 'he'): he, ('granada', 'flash'): flash, ('granada', 'smoke'): smoke, ('granada', 'molotov'): molotov,
              ('granada', 'molotov_ar'): molotov_ar, ('granada', 'apagou'): apagou, ('granada', 'quique_metal'): quique_metal,
              ('granada', 'quique_vidro'): quique_vidro, ('granada', 'pino'): pino, ('granada', 'arremesso'): arremesso,
              ('c4', 'bip'): lambda r: bip(r, 3700, 0.11), ('c4', 'bip_rapido'): lambda r: bip(r, 4400, 0.055),
              ('c4', 'digita'): digita, ('c4', 'plantou'): plantou, ('c4', 'desarme'): desarme, ('c4', 'desarme_kit'): lambda r: desarme(r, True),
              ('c4', 'explode'): c4_explode, ('faca', 'golpe'): golpe, ('faca', 'acerto'): faca_acerto, ('passo', 'pedra'): passo_pedra})
    return c


UNICOS = {('c4', 'bip'), ('c4', 'bip_rapido'), ('c4', 'plantou'), ('c4', 'explode'), ('granada', 'smoke'), ('granada', 'apagou')}
DISTANTES = {('granada', 'he'): ('granada', 'he_distante'), ('granada', 'flash'): ('granada', 'flash_distante'), ('c4', 'explode'): ('c4', 'explode_distante')}


def gerar(grupos, nvar):
    import zlib
    cat = catalogo(); feitos = 0
    for (g, nome), fn in cat.items():
        if grupos and g not in grupos: continue
        d = os.path.join(SAIDA, g); os.makedirs(d, exist_ok=True)
        nv = 1 if (g, nome) in UNICOS else (8 if g == 'passo' else nvar)
        for k in range(nv):
            r = Rng(zlib.crc32(f'{g}/{nome}'.encode()) % 99991 * 10 + k)
            x = fn(r); salvar(os.path.join(d, f'{nome}_{k}.wav'), x); feitos += 1
            if (g, nome) in DISTANTES:
                g2, n2 = DISTANTES[(g, nome)]; salvar(os.path.join(SAIDA, g2, f'{n2}_{k}.wav'), distante_de(x, r)); feitos += 1
    salvar(os.path.join(SAIDA, 'granada', 'fogo_loop_0.wav'), fogo_loop(Rng(77))); feitos += 1
    print('foley:', feitos, 'arquivos')


if __name__ == '__main__':
    args = [x for x in sys.argv[1:] if not x.startswith('--')]
    nvar = int(sys.argv[sys.argv.index('--var') + 1]) if '--var' in sys.argv else 3
    if '--var' in sys.argv: args.remove(str(nvar))
    gerar(set(args[0].split(',')) if args else None, nvar)
