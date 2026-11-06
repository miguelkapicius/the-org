# AWP com protagonismo (usuário, 2026-11-06): "triple kill de AWP fica uma bosta, não sente a emoção".
# Medido contra o CS2 (sounds/weapons/awp/awp_01 vs ak47_01, mede_awp.py no scratchpad):
#   MEIO (150-2000 Hz, o que a caixinha/celular toca): CS2 AWP = AK +4,1 dB; o nosso AWP = AK -1,4 dB (tudo no sub).
#   CAUDA: CS2 AWP fica acima de -20 dB por 2,4 s (AK 0,43 s); o nosso AWP 0,39 s (AK 0,16 s) -> estrondo curto.
# Opções em cima do tiro APROVADO (boom nº 3): meio (corpo/peito), cauda levantada (o rolar do estrondo que já existe
# no clipe, só que lá embaixo), trovão (cauda escura sintética de 2,5 s) e, no jogo, abafar o resto quando a AWP fala.
# Uso: python awp_protag.py          -> saida/_opcoes/awp_p_K.wav (cena: troca de AK ao fundo + triple kill de AWP)
#      python awp_protag.py usar K   -> reescreve saida/awp/tiro_0-3 (guarda os de antes em saida/_opcoes/awp_antes/)
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor
from motor import SR

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes'); ANTES = os.path.join(OP, 'awp_antes')


def ler(n): return sf.read(os.path.join(S, n))[0]


def env(x, ms=10):
    w = int(ms / 1000 * SR); return np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')) + 1e-9


def meio(x, db):
    """Corpo/peito (150-2000 Hz) sobe e o sub do estouro é segurado, senão o pico do sub manda na normalização e o meio
    some de novo (era o caso: CS2 AWP tem meio = AK +4 dB, o nosso tinha AK -1,4 dB)."""
    if db <= 0: return x
    lo = motor.lp(x, 150, 4); resto = x - lo
    lo = motor.compressor(lo, -10, 3.0, 6.0)
    y = lo + resto
    y = motor.pico(y, 330, 0.7, db); y = motor.pico(y, 1000, 0.9, db * 0.8); return motor.pico(y, 2500, 1.2, db * 0.4)


def cauda(x, db, ini=0.15):
    """Levanta o rolar do estrondo depois do estouro (ganho que sobe suave a partir de 'ini' s), sem passar do direto."""
    if db <= 0: return x
    t = np.arange(len(x)) / SR
    g = 10 ** (db / 20 * np.clip((t - ini) / 0.35, 0, 1) * np.exp(-np.maximum(t - 2.6, 0) / 1.5))
    y = x * g
    return motor.direto_manda(y, 60, 0.6)


def trovao(x, nivel, seed=7):
    """Cauda escura de 2,5 s (vale/rua de pedra): o estouro convolvido numa sala grande escura, entra 50 ms depois."""
    if nivel <= 0: return x
    ir = motor.ir_ambiente(3.6, 3.0, motor.Rng(seed), reflexoes=((0.07, 0.6, 2500), (0.16, 0.45, 1800), (0.31, 0.3, 1200)), escuro=1600, pre=0.05)
    seco = x[:int(0.25 * SR)]
    w = motor.convolve(seco, ir); w = motor.hp(w, 45)
    w *= np.abs(x).max() / (np.abs(w).max() + 1e-12) * 10 ** (nivel / 20)
    y = np.zeros(max(len(x), len(w))); y[:len(x)] += x; y[:len(w)] += w
    return y


def limita(x, teto=0.95, solta=0.05, ganho=1.0):
    """Limitador rápido (vetorizado): envelope de pico com 1 ms de antecipação e soltura exponencial."""
    from scipy.ndimage import maximum_filter1d
    from scipy.signal import lfilter
    x = x * ganho; la = int(0.001 * SR)
    a = maximum_filter1d(np.abs(x), 2 * la + 1)
    need = np.maximum(a / teto, 1.0)
    r = np.exp(-1 / (solta * SR))
    # pico segurado: sobe na hora, desce exponencial (aproximação: filtro de 1ª ordem sobre o máximo móvel)
    sm = lfilter([1 - r], [1, -r], need); need = np.maximum(need, sm)
    return x / need


def final(y, ref, empurra=1.6):
    """Mesmo pico do original e mais volume (o limitador segura o estouro): a AWP sai na frente."""
    y = y / (np.abs(y).max() + 1e-12)
    y = limita(y, 0.95, 0.06, empurra)
    return motor.acabamento(y, -0.3, 80)


# (nome, meio dB, cauda dB, trovão dB rel. pico, abafar o resto no jogo)
CAND = [
    ('atual (boom nº 3, sem mudança)', 0, 0, 0, False),
    ('+ meio (corpo que a caixinha toca)', 7, 0, 0, False),
    ('+ cauda (o estrondo rola mais)', 0, 16, 0, False),
    ('+ meio + cauda', 7, 16, 0, False),
    ('+ meio + cauda + trovão (vale)', 7, 14, -8, False),
    ('forte: + meio + cauda + trovão, tudo mais alto', 9, 18, -5, False),
    ('nº 5 + no jogo o resto abaixa 4 dB por meio segundo', 7, 14, -8, True),
]


def faz(x, k):
    nome, m, c, tv, _ = CAND[k]
    if k == 0: return x
    return final(trovao(cauda(meio(x, m), c), tv), x, 2.0 if k == 5 else 1.6)


def cena(awp, abafa):
    """Ao fundo: mercado + troca de AK longe. Triple kill: tiro, acerto no corpo, ferrolho; três vezes."""
    dur = 9.0; L = int(dur * SR)
    fundo = np.zeros(L); frente = np.zeros(L)
    amb = ler('ambiente/mercado_0.wav')[:L]; fundo[:len(amb)] += amb * 0.25
    ak = ler('ak/tiro_0.wav'); ak2 = ler('ak/tiro_1.wav') if os.path.exists(os.path.join(S, 'ak/tiro_1.wav')) else ak
    for i, t in enumerate([0.3, 0.42, 0.54, 0.66, 2.0, 2.12, 2.24, 3.9, 4.02, 5.6, 5.72, 5.84, 5.96]):
        motor.coloca(fundo, ak if i % 2 else ak2, t, 0.3)
    corpo = [ler(f'impacto/corpo_{i}.wav') for i in range(3)]
    ft, ff = ler('awp/ferrolho_tras_0.wav'), ler('awp/ferrolho_frente_0.wav')
    tiros = [1.0, 3.0, 5.0]
    for i, t in enumerate(tiros):
        motor.coloca(frente, awp[i % len(awp)], t, 0.9)
        motor.coloca(frente, corpo[i], t + 0.06, 0.5)
        motor.coloca(frente, ft, t + 0.85, 0.45); motor.coloca(frente, ff, t + 1.1, 0.45)
    if abafa:   # o que o Som3D faria: cada tiro de AWP abaixa o resto 4 dB, volta em 0,5 s
        t = np.arange(L) / SR; g = np.ones(L)
        for t0 in tiros:
            d = t - t0; m = (d >= 0) & (d < 0.8)
            g[m] = np.minimum(g[m], 10 ** (-4 / 20 * np.clip(1 - (d[m] - 0.3) / 0.5, 0, 1)))
        fundo *= g
    y = fundo + frente
    return y / (np.abs(y).max() + 1e-12) * 10 ** (-1 / 20)


def tiros_atuais():
    fs = sorted(glob.glob(os.path.join(ANTES, 'tiro_*.wav'))) or sorted(glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')))
    return [sf.read(f)[0] for f in fs]


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'awp_p*_*.wav')): os.remove(f)
    base = tiros_atuais()
    for k, (nome, *_r) in enumerate(CAND):
        awp = [faz(x, k) for x in base[:3]]
        motor.salvar(os.path.join(OP, f'awp_p_{k}.wav'), cena(awp, CAND[k][4]))
        motor.salvar(os.path.join(OP, f'awp_ps_{k}.wav'), awp[0])
        print(k + 1, nome, flush=True)


def usar(k):
    os.makedirs(ANTES, exist_ok=True)
    if not glob.glob(os.path.join(ANTES, 'tiro_*.wav')):
        for f in glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')): shutil.copy2(f, ANTES)
    for f in sorted(glob.glob(os.path.join(ANTES, 'tiro_*.wav'))):
        motor.salvar(os.path.join(S, 'awp', os.path.basename(f)), faz(sf.read(f)[0], k))
    print('AWP =', k + 1, CAND[k][0], '| abafar no jogo:', CAND[k][4])


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: main()
    os._exit(0)
