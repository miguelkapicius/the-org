# AWP rodada 3 (usuário 2026-11-06, nota na v37: "bom caminho, mas meio esquisito ainda e o ferrolho não está bom,
# o caminho é a 7"). Suspeita do "esquisito": as camadas SINTÉTICAS (o 'chak' modal e a onda em N) — ele já reprovou
# sintético puro como "metálico, cara de IA". Aqui a 7 da rodada 2 sem elas, trocando pelo estalo da própria gravação.
# Ferrolho: o do CS2 é curto (~0,18 s) com 5-6 cliques colados; o nosso tinha 0,3-0,5 s, poucos cliques e um arrasto
# antes do clack. Tomadas novas (eleven/bruto/awp/ferrolho_*_n0-3) cortadas no formato do CS. No jogo o ferrolho agora
# toca depois de CADA tiro (Som3D.Tiro, picos em 0,62 e 0,77 s = mão direita da animação tiro_AWP).
# Uso: python awp_r3.py              -> _opcoes/awp_r_K.wav (cena sem ferrolho), awp_rs_K.wav (tiro), awp_f_K.wav (ferrolho)
#      python awp_r3.py usar TIRO FERR -> saida/awp/tiro_0-3 e ferrolho_tras/frente_0-2
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor
from motor import SR
from hibrido import carregar, aparar, variacao
from awp_protag import S, OP, ANTES, ler
from awp_seco import corta_cauda, estalo, sub_curto, mecanico, soma, kclip
from awp_corpo import ia, doma, norm, junta

B = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'eleven', 'bruto', 'awp')


def estalo_ia(nivel):
    """Estalo tirado de uma gravação de verdade (o começo da IA original c1, só 2-9 kHz, 25 ms)."""
    x = ia('c1'); pk = int(np.argmax(np.abs(x[:int(0.05 * SR)]))); x = x[max(0, pk - 48):][:int(0.025 * SR)]
    y = motor.bp(x, 2000, 9000, 2) * motor.env_exp(len(x), 0.008, 0.0001)
    return norm(y) * nivel


BASE = dict(est=0.9, mec=0.25, sub=0.25, atr=5, clip=3.0, kg=2.4, soco=4, cauda=(0.18, 0.08, 0.6))
def P(**k): d = dict(BASE); d.update(k); return d
CAND = [
    ('nº 7 da rodada 2 (referência)', P(seed=17)),
    ('sem o "chak" sintético', P(mec=0)),
    ('sem nada sintético: estalo da própria gravação', P(mec=0, est=0, est_ia=0.9)),
    ('sem sintético + estalo mais suave', P(mec=0, est=0, est_ia=0.6)),
    ('sem sintético + mais soco', P(mec=0, est=0, est_ia=0.9, soco=6)),
    ('sem sintético + corpo ainda mais longo (sem eco)', P(mec=0, est=0, est_ia=0.9, cauda=(0.22, 0.1, 0.75))),
    ('sem sintético e sem o sub extra (só a gravação)', P(mec=0, est=0, est_ia=0.9, sub=0)),
]


def faz(k):
    p = CAND[k][1]; rng = motor.Rng(p.get('seed', 30 + k))
    x = doma(ia('n0'), 0.5); pk = int(np.argmax(np.abs(x[:int(0.05 * SR)]))); x = x[max(0, pk - 48):]
    x = motor.pico(x, 260, 0.9, p['soco'])
    corpo = corta_cauda(norm(x), *p['cauda'])
    n = int(0.025 * SR); w = np.clip(np.arange(n) / n, 0, 1) ** 4
    corpo[:n] = motor.satura(corpo[:n], p['clip']) * (1 - w) + corpo[:n] * w
    corpo = norm(motor.compressor(corpo, -12, 3.0, 6.0)) * 0.7
    partes = [(p['atr'] / 1000, corpo)]
    if p['est'] > 0: partes.append((0.0, estalo(rng, p['est'])))
    if p.get('est_ia'): partes.append((0.0, estalo_ia(p['est_ia'])))
    if p['mec'] > 0: partes.append((0.001, mecanico(rng, p['mec'])))
    if p['sub'] > 0: partes.append((p['atr'] / 1000, sub_curto(rng, p['sub'] * 0.7)))
    return motor.acabamento(kclip(norm(soma(*partes)) * p['kg']), -0.3, 20)


# ferrolho: (nome, trás, frente)
FERR = [('atual', None, None), ('novo A (n0 + n0)', 'n0', 'n0'), ('novo B (n1 + n1)', 'n1', 'n1'),
        ('novo C (n1 + n2, trava num clique só)', 'n1', 'n2'), ('novo D (n0 + n1)', 'n0', 'n1')]


def peca(nome, tom=None):
    if tom is None: return sf.read(os.path.join(S, 'awp', f'{nome}_0.wav'))[0]
    x = aparar(carregar(os.path.join(B, f'{nome}_{tom}.mp3')), -30, 6)[:int(0.26 * SR)].copy()
    f = int(0.03 * SR); x[-f:] *= np.linspace(1, 0, f)
    x = motor.compressor(motor.hp(x, 150), -14, 2.0, 4.0)
    return motor.acabamento(x, -1.0, 20)


def ferrolho(k): _n, t, f = FERR[k]; return peca('ferrolho_tras', t), peca('ferrolho_frente', f)


def pico(x): return int(np.argmax(np.abs(x))) / SR


def cena(awp, ferr=None):
    """Mercado + troca de AK longe; três tiros com acerto (e, se dado, o ferrolho onde a animação puxa e trava)."""
    dur = 7.5; L = int(dur * SR); fundo = np.zeros(L); frente = np.zeros(L)
    amb = ler('ambiente/mercado_0.wav')[:L]; fundo[:len(amb)] += amb * 0.25
    ak = ler('ak/tiro_0.wav')
    for t in [0.3, 0.42, 0.54, 0.66, 2.0, 2.12, 2.24, 3.9, 4.02, 5.3, 5.42]: motor.coloca(fundo, ak, t, 0.3)
    corpo = [ler(f'impacto/corpo_{i}.wav') for i in range(3)]
    for i, t in enumerate([1.0, 2.8, 4.6]):
        motor.coloca(frente, awp[i % len(awp)], t, 0.9); motor.coloca(frente, corpo[i], t + 0.06, 0.5)
        if ferr:
            tr, fr = ferr
            motor.coloca(frente, tr, t + 0.62 - pico(tr), 0.5); motor.coloca(frente, fr, t + 0.77 - pico(fr), 0.5)
    y = fundo + frente
    return y / (np.abs(y).max() + 1e-12) * 10 ** (-1 / 20)


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'awp_r*_*.wav')) + glob.glob(os.path.join(OP, 'awp_f_*.wav')): os.remove(f)
    for k, (nome, _p) in enumerate(CAND):
        x = faz(k); t = [x, variacao(x, 1), variacao(x, 2)]
        motor.salvar(os.path.join(OP, f'awp_r_{k}.wav'), cena(t))
        motor.salvar(os.path.join(OP, f'awp_rs_{k}.wav'), x); print('tiro', k + 1, nome, flush=True)
    ref = faz(0); t = [ref, variacao(ref, 1), variacao(ref, 2)]
    for k, (nome, *_r) in enumerate(FERR):
        motor.salvar(os.path.join(OP, f'awp_f_{k}.wav'), cena(t, ferrolho(k))); print('ferrolho', k + 1, nome, flush=True)


def usar(kt, kf):
    os.makedirs(ANTES, exist_ok=True)
    for f in glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')) + glob.glob(os.path.join(S, 'awp', 'ferrolho_*.wav')):
        if not os.path.exists(os.path.join(ANTES, os.path.basename(f))): shutil.copy2(f, ANTES)
    x = faz(kt)
    for i in range(4): motor.salvar(os.path.join(S, 'awp', f'tiro_{i}.wav'), variacao(x, i))
    if kf > 0:
        tr, fr = ferrolho(kf)
        for i in range(3):
            motor.salvar(os.path.join(S, 'awp', f'ferrolho_tras_{i}.wav'), variacao(tr, i))
            motor.salvar(os.path.join(S, 'awp', f'ferrolho_frente_{i}.wav'), variacao(fr, i))
    print('AWP tiro =', kt + 1, CAND[kt][0], '| ferrolho =', kf + 1, FERR[kf][0])


if __name__ == '__main__':
    if len(sys.argv) > 3 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1, int(sys.argv[3]) - 1)
    else: main()
    os._exit(0)
