# Motor de síntese de som (autoral). Blocos físicos usados pra montar tiros, mecânica e explosões.
# Tudo gerado do zero: nenhum sample de terceiros. O CS2 entra só como régua (medir.py / regua.py).
import numpy as np
from scipy.signal import butter, sosfilt, lfilter

SR = 48000


def t_(dur):
    return np.arange(int(dur * SR)) / SR


class Rng:
    def __init__(self, seed):
        self.r = np.random.default_rng(seed)

    def u(self, a, b):
        return float(self.r.uniform(a, b))

    def var(self, x, frac):          # x ± frac
        return x * (1 + self.u(-frac, frac))

    def ruido(self, n):
        return self.r.standard_normal(n)


# ---------- filtros ----------
def _sos(kind, f, ordem=2):
    if kind == 'bp':
        lo, hi = max(10, f[0]), min(SR / 2 - 100, f[1])
        return butter(ordem, [lo, hi], 'bandpass', fs=SR, output='sos')
    f = min(max(10, f), SR / 2 - 100)
    return butter(ordem, f, kind, fs=SR, output='sos')


def lp(x, f, o=2): return sosfilt(_sos('lowpass', f, o), x)
def hp(x, f, o=2): return sosfilt(_sos('highpass', f, o), x)
def bp(x, lo, hi, o=2): return sosfilt(_sos('bp', (lo, hi), o), x)


def pico(x, f, q, ganho_db):
    """EQ de pico (biquad RBJ)."""
    A = 10 ** (ganho_db / 40); w = 2 * np.pi * f / SR; al = np.sin(w) / (2 * q)
    b = [1 + al * A, -2 * np.cos(w), 1 - al * A]; a = [1 + al / A, -2 * np.cos(w), 1 - al / A]
    return lfilter(np.array(b) / a[0], np.array(a) / a[0], x)


def prateleira_grave(x, f, ganho_db):
    A = 10 ** (ganho_db / 40); w = 2 * np.pi * f / SR; al = np.sin(w) / 2 * np.sqrt(2)
    c = np.cos(w); sa = 2 * np.sqrt(A) * al
    b = [A * ((A + 1) - (A - 1) * c + sa), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - sa)]
    a = [(A + 1) + (A - 1) * c + sa, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - sa]
    return lfilter(np.array(b) / a[0], np.array(a) / a[0], x)


# ---------- envelopes ----------
def env_exp(n, tau, ataque=0.0005):
    t = np.arange(n) / SR
    a = np.clip(t / max(ataque, 1e-5), 0, 1)
    return a * np.exp(-t / tau)


def env_2(n, tau_rapido, tau_lento, mistura, ataque=0.0005):
    """Decaimento em dois estágios: queda rápida + rabo lento (típico de tiro)."""
    t = np.arange(n) / SR
    a = np.clip(t / max(ataque, 1e-5), 0, 1)
    return a * ((1 - mistura) * np.exp(-t / tau_rapido) + mistura * np.exp(-t / tau_lento))


def coloca(dest, src, em, ganho=1.0):
    i = max(0, int(em * SR))
    if i >= len(dest): return dest
    m = min(len(src), len(dest) - i)
    dest[i:i + m] += src[:m] * ganho
    return dest


# ---------- fontes ----------
def friedlander(T, b=2.2, dur=0.03):
    """Pulso de pressão de explosão (boca do cano): pico positivo seco e fase negativa."""
    t = t_(dur)
    p = (1 - t / T) * np.exp(-b * t / T)
    p[t > 6 * T] *= np.exp(-(t[t > 6 * T] - 6 * T) / T)
    return p


def onda_n(dur_ms):
    """Estalo supersônico da bala: onda em N bem curta."""
    n = max(4, int(dur_ms / 1000 * SR))
    x = np.linspace(1, -1, n)
    return np.concatenate([[0], x, [0]])


def boom(f0, f1, tau_f, tau_a, dur, rng, harm=0.25):
    """Grave com queda de pitch (estrondo). f0 -> f1 com constante tau_f."""
    t = t_(dur)
    f = f1 + (f0 - f1) * np.exp(-t / tau_f)
    fase = 2 * np.pi * np.cumsum(f) / SR + rng.u(0, 6.28)
    y = np.sin(fase) + harm * np.sin(2 * fase + 0.7)
    return y * env_exp(len(t), tau_a, 0.0008)


def corpo_ruido(dur, lo, hi, tau_r, tau_l, mist, rng, cor=0.0, grao=0.0):
    """Ruído filtrado com envelope de tiro. cor>0 escurece (rosa/marrom), grao = aspereza (AM aleatória)."""
    n = int(dur * SR)
    x = rng.ruido(n)
    if cor > 0:
        x = (1 - cor) * x + cor * lfilter([1], [1, -0.985], x) * 0.12
    x = bp(x, lo, hi, 2)
    if grao > 0:
        am = lp(np.abs(rng.ruido(n)), 900)
        am = am / (am.std() + 1e-9)
        x *= (1 - grao) + grao * am
    return x * env_2(n, tau_r, tau_l, mist)


def modal(freqs, decs, amps, dur, rng, desafina=0.01):
    """Ressonância metálica (síntese modal): soma de senos amortecidos inarmônicos."""
    t = t_(dur); y = np.zeros_like(t)
    for f, d, a in zip(freqs, decs, amps):
        f = f * (1 + rng.u(-desafina, desafina))
        y += a * np.sin(2 * np.pi * f * t + rng.u(0, 6.28)) * np.exp(-t / d)
    return y


def clique(rng, f=4000, dur=0.004, ganho=1.0):
    n = int(dur * SR)
    x = hp(rng.ruido(n), f) * env_exp(n, dur / 4, 0.0001)
    return x * ganho


def metal(rng, base, n_modos, dec, dur, brilho=1.0, desafina=0.03):
    """Batida metálica genérica (peças de arma): modos inarmônicos em volta de base."""
    razoes = [1.0, 1.47, 2.09, 2.56, 3.18, 3.91, 4.73, 5.6][:n_modos]
    fr = [base * r * (1 + rng.u(-desafina, desafina)) for r in razoes]
    dc = [dec / (1 + 0.6 * i) for i in range(n_modos)]
    am = [(0.8 ** i) * (brilho if i > 2 else 1) * rng.u(0.6, 1.0) for i in range(n_modos)]
    y = modal(fr, dc, am, dur, rng)
    c = clique(rng, 2500, 0.003); y[:len(c)] += 0.6 * c[:len(y)]
    return y


# ---------- ambiente ----------
def ir_ambiente(dur, t60, rng, reflexoes=(), escuro=4000, pre=0.012, estereo=False):
    """Resposta ao impulso sintética: reflexões discretas (paredes da vila) + cauda difusa
    com decaimento por banda (agudo morre antes do grave)."""
    n = int(dur * SR); t = np.arange(n) / SR
    canais = 2 if estereo else 1
    out = np.zeros((canais, n))
    for c in range(canais):
        cauda = np.zeros(n)
        bandas = [(40, 250, 1.25), (250, 1000, 1.0), (1000, 3500, 0.7), (3500, 12000, 0.42)]
        for lo, hi, k in bandas:
            r = bp(rng.ruido(n), lo, hi, 2)
            cauda += r * np.exp(-6.91 * t / (t60 * k))
        cauda *= np.clip((t - pre) / 0.03, 0, 1)
        cauda = lp(cauda, escuro)
        y = cauda * 0.35
        for (dt, g, f) in reflexoes:
            # reflexão DIFUSA (parede de pedra espalha): rajadinha de ruído de ~8-15 ms, não um impulso seco —
            # impulso seco somado ao direto vira filtro pente = timbre metálico/"lata"
            k = int((dt + rng.u(-0.003, 0.003)) * SR)
            if k < n:
                m_ = int(rng.u(0.008, 0.015) * SR); m_ = min(m_, n - k)
                raj = rng.ruido(m_) * np.exp(-np.arange(m_) / (m_ / 3)) * g * 0.6
                y[k:k + m_] += lp(raj, f)
        out[c] = y / (np.sqrt((y ** 2).sum()) + 1e-12)   # energia 1: o envio vira proporção molhado/seco
    return out if estereo else out[0]


def convolve(x, ir):
    m = len(x) + len(ir) - 1
    nfft = 1 << (m - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(x, nfft) * np.fft.rfft(ir, nfft), nfft)[:m]


# ---------- dinâmica / acabamento ----------
def satura(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


def limitador(x, teto=0.96, solta=0.06):
    """Limitador de pico simples com lookahead de 1 ms."""
    a = np.abs(x); la = int(0.001 * SR)
    g = np.ones_like(x); env = 0.0; rel = np.exp(-1 / (solta * SR))
    ahead = np.concatenate([a[la:], np.zeros(la)])
    for i in range(len(x)):
        v = max(a[i], ahead[i])
        env = v if v > env else env * rel + v * (1 - rel)
        if env > teto: g[i] = teto / env
    return x * g


def acabamento(x, pico_db=-0.3, fade_ms=40):
    x = hp(x, 25, 2)
    # corta silêncio do fim (-70 dB) e faz fade
    env = np.abs(x); lim = env.max() * 10 ** (-70 / 20)
    j = np.where(env > lim)[0]
    if len(j): x = x[: j[-1] + 1]
    f = min(len(x) // 4, int(fade_ms / 1000 * SR))
    if f > 0: x[-f:] *= np.linspace(1, 0, f) ** 2
    pk = np.abs(x).max() + 1e-12
    return x / pk * 10 ** (pico_db / 20)


def salvar(caminho, x, sr=SR):
    import soundfile as sf
    sf.write(caminho, np.clip(x, -1, 1).astype(np.float32), sr, subtype='PCM_16')


def compressor(x, limiar_db, razao, tau_ms=8.0, ganho_db=0.0):
    """Compressor RMS (sem laço em Python): encorpa o tiro como nos jogos (som 'cheio' por ~150 ms)."""
    a = np.exp(-1 / (tau_ms / 1000 * SR))
    e = np.sqrt(np.maximum(lfilter([1 - a], [1, -a], x ** 2), 1e-14))
    # segura o pico 3 ms pra não deixar passar o estalo inteiro por cima
    lim = 10 ** (limiar_db / 20) * (np.abs(x).max() + 1e-12)
    g = np.where(e > lim, (e / lim) ** (1 / razao - 1), 1.0)
    return x * g * 10 ** (ganho_db / 20)


def agudo(x, ganho_db, f=3500):
    """Prateleira de agudos aproximada (passa-alta somado)."""
    return x + (10 ** (ganho_db / 20) - 1) * hp(x, f, 2)


def direto_manda(x, ate_ms=50, teto=0.9):
    """Eco nunca passa do estouro direto (física): o que vem depois de 'ate_ms' fica abaixo de teto x pico do começo."""
    w = int(0.005 * SR); e = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same'))
    k = int(ate_ms / 1000 * SR)
    if len(x) <= k + w: return x
    cedo, tarde = e[:k].max() + 1e-12, e[k:].max()
    if tarde <= teto * cedo: return x
    g = teto * cedo / tarde
    rampa = np.ones(len(x)); j = max(0, k - w * 4)
    rampa[j:k] = np.linspace(1, g, k - j); rampa[k:] = g
    # só o que passa do teto é baixado: mistura o original abaixo do limite
    return x * rampa
