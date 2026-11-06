# C4 v12: bip novo (o usuário não gostou do anterior). Candidatos sintéticos medidos na régua do CS2:
#   c4_beep2 = tom firme ~4,24 kHz, ~110 ms planos e corte rápido; c4_beep3 = dois tons 4,0 + 3,56 kHz, ~130 ms;
#   últimos 10 s (…_10sec) = mais agudo e curto: 5,34 kHz 30 ms / 5,04 -> 4,49 kHz 50 ms.
# Cada candidato = par (bip do meio, bip dos últimos 10 s). Gera:
#   saida/_opcoes/c4bip_K.wav      -> os dois bips lado a lado
#   saida/_opcoes/c4seq_K.wav      -> sequência: teclado, plantou, bip acelerando, chiado (feito do próprio bip), explode
# Uso: python c4_bip.py            (candidatos)
#      python c4_bip.py usar K     (aplica o K escolhido em saida/c4: bip, bip_rapido, final, desarme_inicio)
import os, sys, glob, numpy as np, soundfile as sf
import motor
from motor import SR

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida')
OP = os.path.join(S, '_opcoes')


def ler(n): return sf.read(os.path.join(S, n))[0]


def tom(freqs, dur, quadrado=0.25, ataque=0.004, corte=0.008, glide=0.0, harm=(0.23, 0.12), sobe=0.0, brilho=16000):
    """Bip eletrônico: soma de tons (freqs = [(f, ganho)]), um pouco de onda quadrada (buzzer), corpo plano e corte rápido.
    glide = fração que a frequência cai do começo ao fim; sobe = dB que o volume cresce ao longo do bip (o do CS cresce leve)."""
    n = int(dur * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for f, g in freqs:
        fi = f * (1 + glide * (1 - t / dur))
        fase = 2 * np.pi * np.cumsum(fi) / SR
        s = np.sin(fase) * (1 - quadrado) + np.sign(np.sin(fase)) * quadrado
        s += harm[0] * np.sin(1.82 * fase) + harm[1] * np.sin(3 * fase)
        y += s * g
    env = np.clip(t / ataque, 0, 1) * np.clip((dur - t) / corte, 0, 1) * 10 ** (sobe * (t / dur - 1) / 20)
    y = motor.lp(y * env, brilho)
    return motor.acabamento(y, -3, 6)


# (nome, bip do meio, bip dos últimos 10 s)
CAND = [
    ('CS2: tom firme 4,2 kHz', lambda: tom([(4240, 1)], 0.11, sobe=2), lambda: tom([(5340, 1)], 0.032, corte=0.004)),
    ('CS2: dois tons 4,0 + 3,56 kHz', lambda: tom([(4000, 1), (3563, 0.85)], 0.13, quadrado=0.15, sobe=2),
                                      lambda: tom([(5040, 1), (4490, 0.8)], 0.05, quadrado=0.15, glide=0.06, corte=0.005)),
    ('mais grave e encorpado 3,2 kHz', lambda: tom([(3200, 1)], 0.12, quadrado=0.35, sobe=1), lambda: tom([(4000, 1)], 0.04, quadrado=0.35, corte=0.005)),
    ('piezo seco (buzzer)', lambda: tom([(4240, 1)], 0.085, quadrado=0.6, corte=0.003, brilho=12000), lambda: tom([(5340, 1)], 0.03, quadrado=0.6, corte=0.003, brilho=12000)),
    ('suave e limpo 3,6 kHz', lambda: tom([(3600, 1)], 0.10, quadrado=0.0, harm=(0.05, 0.02), ataque=0.006, corte=0.015),
                              lambda: tom([(4500, 1)], 0.04, quadrado=0.0, harm=(0.05, 0.02), corte=0.008)),
    ('agudo digital 4,8 kHz', lambda: tom([(4800, 1)], 0.09, quadrado=0.3, sobe=1), lambda: tom([(6000, 1)], 0.03, quadrado=0.3, corte=0.004)),
]


def chiado(b, k=0):
    """Último segundo (opção 1 aprovada): o bip acelerando até virar tom contínuo, com chiado por cima."""
    ini, fim_, tomd = 0.11, 0.035, 0.35
    b = b[:int(min(len(b), 0.06 * SR))].copy(); f = int(0.008 * SR); b[-f:] *= np.linspace(1, 0, f)
    n = int(1.4 * SR); y = np.zeros(n); t = 0.0; dt = ini
    while t < 1.0 - tomd * 0.5:
        i = int(t * SR); m = min(len(b), n - i); y[i:i + m] += b[:m] * (0.7 + 0.3 * t)
        t += dt; dt = max(fim_, dt * 0.9)
    a, z = int(0.008 * SR), int(min(len(b) - 0.006 * SR, 0.025 * SR))
    miolo = b[a:z]; tomx = np.tile(miolo, int(tomd * SR / len(miolo)) + 1)[:int(tomd * SR)]
    tomx = tomx * np.linspace(1, 0.8, len(tomx)) + motor.hp(np.random.default_rng(k).standard_normal(len(tomx)), 5000) * 0.05
    i = int(t * SR); y[i:i + len(tomx)] += tomx[:n - i]
    return motor.acabamento(y, -1, 20)


def desarme(bip):
    """Começar a desarmar = dois bips só no início (aprovado)."""
    out = []
    for k, gap in enumerate((0.12, 0.10, 0.14)):
        n = int((gap + len(bip) / SR + 0.05) * SR); y = np.zeros(n)
        y[:len(bip)] += bip * 0.8; i = int(gap * SR); y[i:i + len(bip)] += bip[:n - i] * 0.8
        out.append(y)
    return out


def mix(partes, dur):
    L = int(dur * SR); out = np.zeros(L)
    for t, x, g in partes:
        i = int(t * SR); m = min(len(x), L - i)
        if m > 0: out[i:i + m] += x[:m] * g
    return out


def seq(meio, rap, fin):
    dig = ler('c4/digita_0.wav'); partes = [(0, dig, 0.8)]; t = len(dig) / SR + 0.1
    partes.append((t, ler('c4/plantou_0.wav'), 0.9)); t += 0.6
    for ini, fim in ((40.0, 33.0), (10.0, 1.0)):
        resta = ini
        while resta > fim:
            partes.append((t, rap if resta < 10 else meio, 0.6))
            dt = max(0.15, 0.1 + 0.9 * resta / 40); t += dt; resta -= dt
        t += 0.6 if ini == 40.0 else 0.0
    partes.append((t, fin, 0.9)); t += 1.0
    partes.append((t, ler('c4/explode_0.wav'), 1.0))
    return mix(partes, t + 3.0)


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'c4bip_*.wav')) + glob.glob(os.path.join(OP, 'c4seq_*.wav')): os.remove(f)
    for k, (nome, fm, fr) in enumerate(CAND):
        m, r = fm(), fr()
        par = mix([(0, m, 0.8), (0.5, m, 0.8), (1.0, m, 0.8), (1.6, r, 0.8), (1.8, r, 0.8), (2.0, r, 0.8), (2.2, r, 0.8)], 2.6)
        motor.salvar(os.path.join(OP, f'c4bip_{k}.wav'), par)
        motor.salvar(os.path.join(OP, f'c4seq_{k}.wav'), seq(m, r, chiado(r, k)))
        print(k + 1, nome)


def usar(k):
    nome, fm, fr = CAND[k]; m, r = fm(), fr()
    for p in ('bip_*.wav', 'bip_rapido_*.wav', 'final_*.wav', 'desarme_inicio_*.wav'):
        for f in glob.glob(os.path.join(S, 'c4', p)): os.remove(f)
    motor.salvar(os.path.join(S, 'c4', 'bip_0.wav'), m)
    motor.salvar(os.path.join(S, 'c4', 'bip_rapido_0.wav'), r)
    motor.salvar(os.path.join(S, 'c4', 'final_0.wav'), chiado(r))
    for i, y in enumerate(desarme(m)): motor.salvar(os.path.join(S, 'c4', f'desarme_inicio_{i}.wav'), y)
    print('C4 com o bip', k + 1, nome)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: main()
