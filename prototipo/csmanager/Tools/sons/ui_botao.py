# Som de interface (usuário, 2026-11-05): "estalinho" ao passar o mouse no botão e um som parecido ao clicar.
# Sintético de propósito (som de interface É sintético; no CS2 também). Régua = sons/ui do CS2 (mede_ui no scratchpad):
#   passar o mouse: 20-50 ms, centroide 6-12 kHz, pico -13..-16 dBFS
#   clicar:        100-200 ms, centroide 6-10 kHz, pico  -1..-10 dBFS (3-6 dB acima do passar)
# Cada FAMÍLIA = um par passar/clicar com o mesmo timbre (o clique é o mesmo material, mais cheio e mais forte).
# Uso: python ui_botao.py          -> saida/_opcoes/ui_K.wav (demo: passa, passa, passa, clica, passa+clica)
#      python ui_botao.py usar K   -> saida/ui/passar_0-2.wav e clicar_0-1.wav (variações derivadas, pitch ±3%)
import os, sys, glob, numpy as np
import motor
from motor import SR

AQUI = os.path.dirname(os.path.abspath(__file__))
OP = os.path.join(AQUI, 'saida', '_opcoes')
NOMES = ['estalo seco', 'vidro', 'tecla de plástico', 'digital suave', 'madeira', 'metal leve']
P_PASSAR, P_CLICAR = 10 ** (-14 / 20), 10 ** (-8 / 20)


def t(d): return np.arange(int(d * SR)) / SR


def ping(f, tau, d, fase_rng=None):
    x = t(d); return (np.sin(2 * np.pi * f * x) * np.exp(-x / tau) * np.minimum(1, x / 0.0004)).view(Som)


def ruido(d, lo, hi, tau, rng):
    n = rng.standard_normal(int(d * SR)); x = t(d)
    return (motor.bp(n, lo, min(hi, SR / 2 - 100), 2) * np.exp(-x / tau)).view(Som)


def modal(fs, taus, amps, d):
    return sum(a * ping(f, tt, d) for f, tt, a in zip(fs, taus, amps))


class Som(np.ndarray):
    """array que soma com comprimentos diferentes (completa com zero)."""
    def __add__(s, o):
        o = np.asarray(o)
        if o.ndim == 0: return np.asarray(s).__add__(o).view(Som)
        n = max(len(s), len(o)); y = np.zeros(n); y[:len(s)] += s; y[:len(o)] += o; return y.view(Som)
    __radd__ = __add__
    def __mul__(s, o): return np.multiply(np.asarray(s), o).view(Som)
    __rmul__ = __mul__


def junta(partes, d):
    y = np.zeros(int(d * SR))
    for em, x in partes:
        i = int(em * SR); n = min(len(x), len(y) - i); y[i:i + n] += x[:n]
    return y


def familia(k, rng, p=1.0):
    """(passar, clicar) da família k; p = fator de pitch pras variações."""
    f = lambda v: v * p
    if k == 0:   # estalo seco: ruído filtrado curtinho + ping agudo
        a = ruido(0.03, f(5000), f(11000), 0.0025, rng) + 0.35 * ping(f(3200), 0.006, 0.03)
        c = junta([(0, ruido(0.05, f(3500), f(10000), 0.004, rng) + 0.5 * ping(f(2100), 0.018, 0.08)),
                   (0.045, 0.4 * ruido(0.03, f(5000), f(11000), 0.003, rng))], 0.14)
    elif k == 1:   # vidro: parciais inarmônicas limpas
        a = modal([f(4200), f(6900)], [0.007, 0.004], [1, 0.5], 0.04) + 0.2 * ruido(0.01, 6000, 12000, 0.001, rng)
        c = modal([f(3100), f(5150), f(8300)], [0.03, 0.018, 0.008], [1, 0.55, 0.3], 0.16) + 0.35 * ruido(0.02, 4000, 12000, 0.0015, rng)
    elif k == 2:   # tecla de plástico: aperta e solta
        a = modal([f(2600), f(5300)], [0.004, 0.003], [0.7, 0.5], 0.03) + 0.6 * ruido(0.02, f(4000), f(12000), 0.0018, rng)
        dn = modal([f(1500), f(3700)], [0.012, 0.006], [0.8, 0.5], 0.06) + 0.7 * ruido(0.03, f(3000), f(11000), 0.0025, rng)
        up = 0.55 * (modal([f(2200), f(4900)], [0.006, 0.004], [0.6, 0.5], 0.04) + 0.6 * ruido(0.02, f(4500), f(12000), 0.0018, rng))
        c = junta([(0, dn), (0.075, up)], 0.15)
    elif k == 3:   # digital suave: blip com o tom caindo; clique = dois blips subindo
        def blip(f0, f1, d, tau):
            x = t(d); fi = np.cumsum(f0 + (f1 - f0) * (1 - np.exp(-x / 0.01))) / SR
            return (np.sin(2 * np.pi * fi) * np.exp(-x / tau) * np.minimum(1, x / 0.0015)).view(Som)
        a = blip(f(3000), f(2500), 0.04, 0.008)
        c = junta([(0, blip(f(2300), f(2000), 0.06, 0.012)), (0.05, 0.85 * blip(f(3450), f(3000), 0.09, 0.02))], 0.15)
    elif k == 4:   # madeira: modos graves-médios abafados
        a = modal([f(1700), f(4100)], [0.005, 0.003], [1, 0.5], 0.03) + 0.4 * ruido(0.015, f(3000), f(9000), 0.0015, rng)
        c = modal([f(1150), f(2750), f(5200)], [0.02, 0.01, 0.005], [1, 0.6, 0.3], 0.12) + 0.5 * ruido(0.03, f(2000), f(9000), 0.0025, rng)
    else:   # metal leve: parciais de chapa, brilhante como o CS
        a = modal([f(5200), f(7600), f(9900)], [0.005, 0.004, 0.003], [1, 0.7, 0.4], 0.035) + 0.3 * ruido(0.01, 7000, 14000, 0.001, rng)
        c = modal([f(2500), f(3900), f(6100), f(8800)], [0.05, 0.03, 0.015, 0.008], [1, 0.7, 0.5, 0.3], 0.18) + 0.4 * ruido(0.02, 5000, 14000, 0.0015, rng)
    a = motor.hp(np.asarray(a, float), 300); c = motor.hp(np.asarray(c, float), 200)
    for x in (a, c): x[-int(0.004 * SR):] *= np.linspace(1, 0, int(0.004 * SR))
    return a / np.abs(a).max() * P_PASSAR, c / np.abs(c).max() * P_CLICAR


def demo(a, c):
    return junta([(0.05, a), (0.33, a), (0.61, a), (1.0, c), (1.6, a), (1.85, c)], 2.2)


def gera():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'ui_*.wav')): os.remove(f)
    for k in range(len(NOMES)):
        a, c = familia(k, np.random.default_rng(k))
        motor.salvar(os.path.join(OP, f'ui_{k}.wav'), demo(a, c))
        print(k + 1, NOMES[k], f'passar {len(a) / SR * 1000:.0f} ms, clicar {len(c) / SR * 1000:.0f} ms', flush=True)


def usar(k):
    d = os.path.join(AQUI, 'saida', 'ui'); os.makedirs(d, exist_ok=True)
    for f in glob.glob(os.path.join(d, '*.wav')): os.remove(f)
    for i, p in enumerate([1.0, 1.03, 0.97]):
        a, c = familia(k, np.random.default_rng(100 + i), p)
        motor.salvar(os.path.join(d, f'passar_{i}.wav'), a)
        if i < 2: motor.salvar(os.path.join(d, f'clicar_{i}.wav'), c)
    print('interface = família', k + 1, NOMES[k])


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: gera()
    os._exit(0)
