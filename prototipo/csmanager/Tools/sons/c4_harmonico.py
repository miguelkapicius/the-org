# C4 v20 (usuário, 2026-11-05): o bip dos últimos 10 s "muda muito" do bip normal, e o apito final antes de explodir
# "praticamente não existe". Quer tudo HARMÔNICO com o bip normal (o bip 1 aprovado, 4,24 kHz 110 ms, fica igual).
# Medido: o bip urgente era outro som (5,34 kHz e 32 ms, timbre mais fino); o apito final era o bip urgente acelerando +
# tom contínuo no FIM do clipe -> o tom caía em cima da explosão e sumia (e em 2x/4x o último segundo do jogo encolhe).
# Aqui cada opção = (bip urgente, apito final), feitos com o MESMO timbre do bip normal (tom() do c4_bip), em intervalos
# musicais da nota dele. O apito final dura APITO s e termina exatamente onde a explosão entra (o Som3D dispara ele
# pelo tempo REAL que falta, não pelo do jogo, e corta ele quando explode).
# Uso: python c4_harmonico.py         -> saida/_opcoes/c4h_K.wav (últimos 13 s: bip normal, urgentes, apito, explosão)
#      python c4_harmonico.py usar K  -> saida/c4/bip_rapido_0.wav e final_0.wav (bip_0 e desarme não mudam)
import os, sys, glob, numpy as np, soundfile as sf
import motor
from motor import SR
from c4_bip import tom, mix, ler, S, OP

F0 = 4240.0                       # nota do bip normal aprovado
APITO = 1.0                       # s do apito final (termina na explosão)
URG = 0.075                       # s do bip urgente (o normal tem 110 ms; o de antes 32 ms soava outro som)


def bip(freqs, dur): return tom([(f, g) for f, g in freqs], dur, sobe=2)


def apito(freqs_bip, freqs_tom, glide=0.0):
    """Bips urgentes acelerando (0 -> ~55%) e entra o tom contínuo (acorde) crescendo até o fim; acaba seco no fim."""
    n = int(APITO * SR); y = np.zeros(n)
    b = bip(freqs_bip, 0.06); t, dt = 0.0, 0.11
    while t < APITO * 0.52:
        i = int(t * SR); m = min(len(b), n - i); y[i:i + m] += b[:m] * (0.75 + 0.4 * t); t += dt; dt = max(0.045, dt * 0.85)
    i0 = int(t * SR); L = n - i0; tt = np.arange(L) / SR
    tomx = np.zeros(L)
    for f, g in freqs_tom:
        fi = f * (1 + glide * (tt / max(tt[-1], 1e-6)))
        fase = 2 * np.pi * np.cumsum(fi) / SR
        tomx += g * (0.75 * np.sin(fase) + 0.25 * np.sign(np.sin(fase)) + 0.23 * np.sin(1.82 * fase) + 0.12 * np.sin(3 * fase))
    env = np.clip(tt / 0.01, 0, 1) * 10 ** ((tt / tt[-1] - 1) * 4 / 20)   # cresce 4 dB até a explosão
    tomx = motor.lp(tomx * env, 16000); y[i0:] += tomx * 0.9
    f = int(0.004 * SR); y[-f:] *= np.linspace(1, 0, f)
    return motor.acabamento(y, -1, 4)


st = lambda s: F0 * 2 ** (s / 12)      # semitons acima da nota do bip normal
# (nome, bip urgente, apito final)
CAND = [
    ('mesma nota, só acelera; apito = nota + oitava',    lambda: bip([(F0, 1)], URG), lambda: apito([(F0, 1)], [(F0, 1), (2 * F0, 0.35)])),
    ('quinta acima; apito = nota + quinta (acorde)',     lambda: bip([(st(7), 1)], URG), lambda: apito([(st(7), 1)], [(F0, 0.8), (st(7), 1)])),
    ('quarta acima; apito = nota + quarta',              lambda: bip([(st(5), 1)], URG), lambda: apito([(st(5), 1)], [(F0, 0.8), (st(5), 1)])),
    ('terça maior acima; apito = acorde maior',          lambda: bip([(st(4), 1)], URG), lambda: apito([(st(4), 1)], [(F0, 0.7), (st(4), 0.8), (st(7), 0.8)])),
    ('oitava acima; apito = oitava + nota',              lambda: bip([(2 * F0, 1)], 0.065), lambda: apito([(2 * F0, 1)], [(F0, 0.8), (2 * F0, 1)])),
    ('mesma nota; apito sobe uma quinta até explodir',   lambda: bip([(F0, 1)], URG), lambda: apito([(F0, 1)], [(F0, 1)], glide=0.5)),
]


def seq(meio, urg, fin):
    """Últimos 13 s como no jogo (ritmo do Som3D) e o apito terminando na explosão."""
    partes, t, resta = [], 0.2, 13.0
    while resta > APITO:
        partes.append((t, urg if resta < 10 else meio, 0.6))
        dt = max(0.15, 0.1 + 0.9 * resta / 40); t += dt; resta -= dt
    t += resta - APITO if resta > APITO else 0
    partes.append((t, fin, 0.75)); t += APITO
    partes.append((t, ler('c4/explode_0.wav'), 1.0))
    return mix(partes, t + 3.0)


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'c4h_*.wav')): os.remove(f)
    meio = ler('c4/bip_0.wav')
    for k, (nome, fu, ff) in enumerate(CAND):
        motor.salvar(os.path.join(OP, f'c4h_{k}.wav'), seq(meio, fu(), ff())); print(k + 1, nome, flush=True)


def usar(k):
    nome, fu, ff = CAND[k]
    motor.salvar(os.path.join(S, 'c4', 'bip_rapido_0.wav'), fu())
    motor.salvar(os.path.join(S, 'c4', 'final_0.wav'), ff())
    print('C4 urgente + apito =', k + 1, nome)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: main()
    os._exit(0)
