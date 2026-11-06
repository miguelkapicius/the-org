# AWP seca de sniper (usuário, 2026-11-06): "precisa de um estalo mais seco e forte igual sniper, olha como os caras
# fazem em cinema / jogo grande e replica, não quero eco". Substitui as opções de awp_protag (cauda/trovão = eco).
# Receita de jogo grande / cinema (camadas, cada uma com uma função):
#   ESTALO (transiente, 1-20 ms, 2-9 kHz): a "chicotada" que diz QUANDO atirou — onda em N supersônica + clique seco;
#   CORPO (80-300 Hz, ~100 ms): o peso, do nosso boom nº 3 aprovado, com a cauda CORTADA (sem eco);
#   SUB curto (45-70 Hz, ~150 ms): o soco no peito, curto pra não embolar;
#   MECÂNICO (1-3 kHz, ~60 ms): o "chak" metálico da armação, dá cara de arma de ferrolho;
#   e no começo o estouro é CLIPADO de propósito (saturação/clip só nos primeiros ms) e o estalo vem uns ms antes do
#   corpo — o deslocamento de poucos ms entre camadas muda muito o tamanho e o soco percebido.
# Uso: python awp_seco.py          -> saida/_opcoes/awp_s_K.wav (cena triple kill) e awp_ss_K.wav (só o tiro)
#      python awp_seco.py usar K   -> reescreve saida/awp/tiro_0-3 (guarda os de antes em saida/_opcoes/awp_antes/)
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor
from motor import SR
from awp_protag import cena, limita, S, OP, ANTES, tiros_atuais


def corta_cauda(x, plena=0.13, tau=0.06, fim=0.45):
    """Corpo sem eco: mantém o estouro e o começo do estrondo, depois cai rápido e acaba."""
    t = np.arange(len(x)) / SR
    g = np.where(t < plena, 1.0, np.exp(-(t - plena) / tau)); y = (x * g)[:int(fim * SR)].copy()
    f = int(0.02 * SR); y[-f:] *= np.linspace(1, 0, f); return y


def estalo(rng, nivel, brilho=1.0):
    """Chicotada supersônica: onda em N de 0,5 ms + ruído 2-9 kHz de ~6 ms + clique de 4 kHz."""
    n = int(0.03 * SR); y = np.zeros(n)
    nw = motor.onda_n(0.5); y[:len(nw)] += nw
    r = motor.bp(rng.ruido(n), 2000, 9000, 2) * motor.env_exp(n, 0.010 * brilho, 0.0002); y += 0.8 * r
    c = motor.clique(rng, 4000, 0.004, 1.0); y[:len(c)] += 0.7 * c
    y = motor.hp(y, 1200, 2)
    # denso (clipado) mas CURTO: satura e reaplica o envelope, senão o tanh achata a queda e vira um bloco de chiado
    y = motor.satura(y / (np.abs(y).max() + 1e-12), 3.0) * motor.env_exp(n, 0.007 * brilho, 0.0001)
    return y / (np.abs(y).max() + 1e-12) * nivel


def sub_curto(rng, nivel, dur=0.22):
    s = motor.boom(72, 46, 0.03, 0.07, dur, rng, harm=0.15)
    return s / (np.abs(s).max() + 1e-12) * nivel


def mecanico(rng, nivel):
    m = motor.metal(rng, 1700, 6, 0.035, 0.1, brilho=1.2)
    return motor.hp(m, 700) / (np.abs(m).max() + 1e-12) * nivel


def kclip(x, joelho=0.7):
    """Clipper de joelho suave (o "K-Clip" que os designers de jogo usam no tiro): abaixo do joelho passa limpo, acima
    vira tanh até 1. Sem envelope: não abaixa o corpo que vem depois do estalo, como um limitador com soltura faria."""
    a = np.abs(x); r = 1 - joelho
    return np.where(a < joelho, x, np.sign(x) * (joelho + r * np.tanh((a - joelho) / r)))


def soma(*partes):
    L = max(int(t * SR) + len(x) for t, x in partes); y = np.zeros(L)
    for t, x in partes: motor.coloca(y, x, t)
    return y


# (nome, estalo, mecânico, sub, atraso do corpo ms, clip, cauda: (plena, tau, fim))
CAND = [
    ('atual (boom nº 3, com eco)', None),
    ('seco: só o corpo, cauda cortada', dict(est=0, mec=0, sub=0.35, atr=0, clip=1.5, cauda=(0.13, 0.06, 0.45))),
    ('seco + estalo', dict(est=0.7, mec=0, sub=0.35, atr=3, clip=2.0, cauda=(0.13, 0.06, 0.45))),
    ('seco + estalo + "chak" metálico', dict(est=0.7, mec=0.3, sub=0.35, atr=3, clip=2.0, cauda=(0.13, 0.06, 0.45))),
    ('cinema: estalo forte clipado, corpo 5 ms depois', dict(est=0.95, mec=0.25, sub=0.45, atr=6, clip=3.5, kg=2.8, comp=(-18, 4.0), cauda=(0.11, 0.05, 0.4))),
    ('cinema pesado: nº 5 com mais soco no peito', dict(est=0.95, mec=0.25, sub=0.75, atr=6, clip=3.5, kg=2.8, comp=(-18, 4.0), cauda=(0.11, 0.05, 0.4))),
    ('extremo: chicotada máxima, 0,3 s e acabou', dict(est=1.6, mec=0.3, sub=0.5, atr=8, clip=5.0, kg=2.2, comp=(-20, 5.0), cauda=(0.08, 0.04, 0.3))),
]


def faz(x, k, seed=11):
    p = CAND[k][1]
    if p is None: return x
    rng = motor.Rng(seed + k)
    pk = int(np.argmax(np.abs(x[:int(0.05 * SR)]))); x = x[max(0, pk - int(0.001 * SR)):]
    corpo = corta_cauda(x / (np.abs(x).max() + 1e-12), *p['cauda'])
    # o corpo é clipado/comprimido SOZINHO (bate no teto como no cinema) e o estalo entra por cima depois: se o
    # limitador final pegasse o estalo, a chicotada (1 ms) era a primeira coisa a sumir
    n = int(0.025 * SR); w = np.clip(np.arange(n) / n, 0, 1) ** 4
    corpo[:n] = motor.satura(corpo[:n], p['clip']) * (1 - w) + corpo[:n] * w
    corpo = motor.compressor(corpo, *p.get('comp', (-10, 2.5)), 6.0); corpo = limita(corpo / (np.abs(corpo).max() + 1e-12), 0.95, 0.04, 1.4 if 'comp' in p else 1.0); corpo = corpo / (np.abs(corpo).max() + 1e-12) * 0.65
    partes = [(p['atr'] / 1000, corpo)]
    if p['sub'] > 0: partes.append((p['atr'] / 1000, sub_curto(rng, p['sub'] * 0.75)))
    if p['est'] > 0: partes.append((0.0, estalo(rng, p['est'])))
    if p['mec'] > 0: partes.append((0.001, mecanico(rng, p['mec'])))
    y = soma(*partes)
    y = kclip(y / (np.abs(y).max() + 1e-12) * p.get('kg', 1.7))   # clip de joelho suave amostra a amostra (sem soltura que abafa o corpo depois do estalo)   # cola as camadas sem cortar o estalo (curva suave)
    return motor.acabamento(y, -0.3, 20)


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'awp_s*_*.wav')) + glob.glob(os.path.join(OP, 'awp_p*_*.wav')): os.remove(f)
    base = tiros_atuais()
    for k, (nome, _p) in enumerate(CAND):
        awp = [faz(x, k, 11 + 3 * i) for i, x in enumerate(base[:3])]
        motor.salvar(os.path.join(OP, f'awp_s_{k}.wav'), cena(awp, False))
        motor.salvar(os.path.join(OP, f'awp_ss_{k}.wav'), awp[0])
        print(k + 1, nome, flush=True)


def usar(k):
    os.makedirs(ANTES, exist_ok=True)
    if not glob.glob(os.path.join(ANTES, 'tiro_*.wav')):
        for f in glob.glob(os.path.join(S, 'awp', 'tiro_*.wav')): shutil.copy2(f, ANTES)
    for i, f in enumerate(sorted(glob.glob(os.path.join(ANTES, 'tiro_*.wav')))):
        motor.salvar(os.path.join(S, 'awp', os.path.basename(f)), faz(sf.read(f)[0], k, 11 + 3 * i))
    print('AWP seca =', k + 1, CAND[k][0])


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: main()
    os._exit(0)
