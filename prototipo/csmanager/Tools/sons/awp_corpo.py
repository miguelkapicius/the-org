# AWP com IMPACTO (usuário 2026-11-06: "parece um pote de maionese caindo no chão, não tem o impacto de uma AWP, até a
# Deagle tem mais"). Causa medida: o tratamento "boom nº 3" (v17: +7,5 dB de prateleira grave + 'tum' 80->42 Hz) virou
# um tum afinado em 158 Hz com BURACO de 22 dB em 150-500 Hz (o soco da explosão). As tomadas cruas da IA tinham
# buraco de 1,5-4,5 dB; a Deagle 5,6. Aqui: corpos sem o boom (IA original, IA nova n5/n0, Deagle mais grave), cada um
# com a receita seca de awp_seco (estalo supersônico, chak, corpo alguns ms depois, clip de joelho, cauda cortada).
# Uso: python awp_corpo.py         -> saida/_opcoes/awp_c_K.wav (cena triple kill) e awp_cs_K.wav (só o tiro)
#      python awp_corpo.py usar K  -> saida/awp/tiro_0-3 (4 variações sutis do mesmo, como as outras armas)
import os, sys, glob, shutil, numpy as np, soundfile as sf
from scipy.signal import resample
import motor
from motor import SR
from hibrido import carregar, aparar_tiro, variacao
from awp_protag import cena, S, OP, ANTES
from awp_seco import corta_cauda, estalo, sub_curto, mecanico, soma, kclip

B = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'eleven', 'bruto')


def ia(n): return aparar_tiro(carregar(os.path.join(B, 'awp', f'tiro_{n}.mp3')))


def deagle_grave(st=4):
    x = sf.read(os.path.join(S, 'deagle', 'tiro_0.wav'))[0]
    return resample(x, int(len(x) * 2 ** (st / 12)))   # mais lento = mais grave e maior


def junta(a, b):
    y = np.zeros(max(len(a), len(b))); y[:len(a)] += a; y[:len(b)] += b; return y


def norm(x): return x / (np.abs(x).max() + 1e-12)


def doma(x, buraco_alvo=4.0):
    """Tira o anel do grave (o 'tum' de pote) e enche o soco 150-500 Hz até o buraco sub-soco ficar ~buraco_alvo dB."""
    pk = int(np.argmax(np.abs(x[:int(0.05 * SR)]))); a = x[pk:pk + int(0.12 * SR)] * np.hanning(int(0.12 * SR))
    S_ = 20 * np.log10(np.abs(np.fft.rfft(a, 1 << 15)) + 1e-9); f = np.fft.rfftfreq(1 << 15, 1 / SR)
    b = lambda lo, hi: 10 * np.log10(np.mean(10 ** (S_[(f >= lo) & (f < hi)] / 10)))
    m = (f > 60) & (f < 400); fr = f[m][np.argmax(S_[m])]
    y = motor.pico(x, fr, 2.5, -8)                                     # o anel
    exc = (b(40, 150) - b(150, 500)) - buraco_alvo
    if exc > 0: y = motor.prateleira_grave(y, 130, -min(exc * 0.6, 9)); y = motor.pico(y, 280, 0.8, min(exc * 0.5, 7))
    return y


# (nome, corpo(), parâmetros)
CAND_V1 = [   # rodada 1 (página v36): usuário marcou nº 4 "bom caminho, mas ainda não chegou lá"
    ('atual (boom nº 3 — o "pote de maionese")', None, None),
    ('IA original sem o boom + estalo seco', lambda: ia('c1'), dict(est=0.7, mec=0.2, sub=0.25, atr=3, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('IA nova nº 5 (soco forte) + estalo seco', lambda: ia('n5'), dict(est=0.7, mec=0.2, sub=0.25, atr=3, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('IA nova nº 0 com o grave domado + estalo', lambda: doma(ia('n0')), dict(est=0.7, mec=0.2, sub=0.2, atr=3, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('Deagle 4 tons abaixo (Deagle gigante) + estalo', lambda: deagle_grave(4), dict(est=0.7, mec=0.25, sub=0.35, atr=3, clip=2.5, kg=2.0, cauda=(0.14, 0.07, 0.45))),
    ('nº 3 + nº 2 juntas (soco de uma, estalo da outra)', lambda: junta(norm(ia('n5')), 0.7 * norm(motor.hp(ia('c1'), 400))), dict(est=0.6, mec=0.2, sub=0.25, atr=3, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('nº 3 cinema pesado: mais clip, estalo forte', lambda: ia('n5'), dict(est=1.0, mec=0.25, sub=0.35, atr=5, clip=4.0, kg=2.8, cauda=(0.11, 0.05, 0.35))),
]



# rodada 2: tudo em volta da nº 4 da rodada 1 (IA n0 com o grave domado)
N4 = lambda: doma(ia('n0'))
CAND = [
    ('nº 4 da rodada anterior (referência)', N4, dict(seed=14, est=0.7, mec=0.2, sub=0.2, atr=3, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('+ soco (150-500 Hz mais cheio)', lambda: doma(ia('n0'), 0.5), dict(est=0.7, mec=0.2, sub=0.2, atr=3, clip=2.5, kg=2.0, soco=4, cauda=(0.12, 0.06, 0.4))),
    ('+ estalo mais forte, corpo 5 ms depois', N4, dict(est=1.0, mec=0.25, sub=0.2, atr=5, clip=2.5, kg=2.0, cauda=(0.12, 0.06, 0.4))),
    ('+ soco + estalo + mais clipado (cinema)', lambda: doma(ia('n0'), 0.5), dict(est=1.0, mec=0.25, sub=0.25, atr=5, clip=4.0, kg=2.8, soco=4, cauda=(0.12, 0.06, 0.4))),
    ('irmã: a outra tomada do mesmo pedido (n1), domada igual', lambda: doma(ia('n1')), dict(est=0.8, mec=0.2, sub=0.2, atr=4, clip=2.5, kg=2.2, cauda=(0.12, 0.06, 0.4))),
    ('nº 4 + soco da nº 3 (n5) por baixo', lambda: junta(norm(doma(ia('n0'))), 0.6 * norm(motor.lp(ia('n5'), 600))), dict(est=0.85, mec=0.2, sub=0.15, atr=4, clip=3.0, kg=2.3, cauda=(0.12, 0.06, 0.4))),
    ('+ soco + estalo, corpo um pouco mais longo (ainda sem eco)', lambda: doma(ia('n0'), 0.5), dict(est=0.9, mec=0.25, sub=0.25, atr=5, clip=3.0, kg=2.4, soco=4, cauda=(0.18, 0.08, 0.6))),
]


def faz(k, seed=11):
    nome, fc, p = CAND[k]
    rng = motor.Rng(p.get('seed', seed + k))
    x = fc(); pk = int(np.argmax(np.abs(x[:int(0.05 * SR)]))); x = x[max(0, pk - int(0.001 * SR)):]
    if p.get('soco'): x = motor.pico(x, 260, 0.9, p['soco'])
    corpo = corta_cauda(norm(x), *p['cauda'])
    n = int(0.025 * SR); w = np.clip(np.arange(n) / n, 0, 1) ** 4
    corpo[:n] = motor.satura(corpo[:n], p['clip']) * (1 - w) + corpo[:n] * w
    corpo = norm(motor.compressor(corpo, -12, 3.0, 6.0)) * 0.7
    partes = [(p['atr'] / 1000, corpo), (0.0, estalo(rng, p['est'])), (0.001, mecanico(rng, p['mec']))]
    if p['sub'] > 0: partes.append((p['atr'] / 1000, sub_curto(rng, p['sub'] * 0.7)))
    y = kclip(norm(soma(*partes)) * p['kg'])
    return motor.acabamento(y, -0.3, 20)


def tiros(k):
    if CAND[k][1] is None:
        fs = sorted(glob.glob(os.path.join(ANTES, 'tiro_*.wav'))) or sorted(glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')))
        return [sf.read(f)[0] for f in fs[:4]]
    x = faz(k); return [x] + [variacao(x, i) for i in range(1, 4)]


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'awp_c*_*.wav')): os.remove(f)
    for k, (nome, *_r) in enumerate(CAND):
        t = tiros(k)
        motor.salvar(os.path.join(OP, f'awp_c_{k}.wav'), cena(t[:3], False))
        motor.salvar(os.path.join(OP, f'awp_cs_{k}.wav'), t[0])
        print(k + 1, nome, flush=True)


def usar(k):
    os.makedirs(ANTES, exist_ok=True)
    if not glob.glob(os.path.join(ANTES, 'tiro_*.wav')):
        for f in glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')): shutil.copy2(f, ANTES)
    for i, x in enumerate(tiros(k)): motor.salvar(os.path.join(S, 'awp', f'tiro_{i}.wav'), x)
    print('AWP =', k + 1, CAND[k][0])


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: main()
    os._exit(0)
