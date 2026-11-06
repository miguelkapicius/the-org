# Passos/aterrissagem no MÁRMORE do palácio, feitos IGUAL aos outros (usuário, 2026-11-04: os candidatos crus da IA
# ficaram "esquisitos"). Parte dos sons JÁ APROVADOS (passo/ct_*, passo/tr_*, corpo/aterrissa_pedra_*) e só muda a superfície:
#   mármore = pisada mais dura e estalada (menos grave/areia, mais ataque 2-6 kHz) + eco curto de salão de pedra lisa.
# Saída: saida/passo/marmore_ct_K, marmore_tr_K, saida/corpo/aterrissa_marmore_K (mesmas variações das originais).
import os, glob, numpy as np, soundfile as sf
import motor
from motor import SR

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida')
IR = motor.ir_ambiente(0.7, 0.75, motor.Rng(77), reflexoes=((0.017, 0.7, 7000), (0.029, 0.5, 6000), (0.044, 0.35, 5000), (0.063, 0.25, 4000)), escuro=7500)


def marmore(x, molhado=0.3):   # eco ~-10 dB da energia do passo
    y = motor.prateleira_grave(x, 180, -4)        # menos baque de terra/areia
    y = motor.pico(y, 3000, 1.1, 4.5)             # estalo do salto na pedra lisa
    y = motor.agudo(y, 2.5, 6000)
    eco = motor.convolve(y, IR)[:len(y) + int(0.45 * SR)]
    z = np.zeros(len(eco)); z[:len(y)] = y
    z = z + eco * molhado * np.sqrt((y ** 2).sum()) / (np.sqrt((eco ** 2).sum()) + 1e-12)
    pk = np.abs(x).max(); z = z * pk / (np.abs(z).max() + 1e-12)   # mesmo pico do original aprovado
    f = int(0.08 * SR); z[-f:] *= np.linspace(1, 0, f)
    return z


if __name__ == '__main__':
    for p in ('passo/marmore_*.wav', 'corpo/aterrissa_marmore_*.wav'):
        for f in glob.glob(os.path.join(S, p)): os.remove(f)
    for orig, novo in (('passo/ct_', 'passo/marmore_ct_'), ('passo/tr_', 'passo/marmore_tr_'), ('corpo/aterrissa_pedra_', 'corpo/aterrissa_marmore_')):
        fs = sorted(glob.glob(os.path.join(S, orig + '[0-9]*.wav')))
        for f in fs:
            k = f.rsplit('_', 1)[1][:-4]
            motor.salvar(os.path.join(S, novo + k + '.wav'), marmore(sf.read(f)[0]))
        print(novo, len(fs), flush=True)
    # página: o aprovado na pedra x o mesmo no mármore (corrida de 8 passos / pulo + aterrissagem)
    def mix(partes, dur):
        out = np.zeros(int(dur * SR))
        for t, x, g in partes:
            i = int(t * SR); m = min(len(x), len(out) - i); out[i:i + m] += x[:m] * g
        return out
    OP = os.path.join(S, '_opcoes')
    for f in glob.glob(os.path.join(OP, 'marmore_*.wav')): os.remove(f)
    pulo = sf.read(os.path.join(S, 'corpo', 'pulo_0.wav'))[0]
    for g, pads in (('ct', ('passo/ct_', 'passo/marmore_ct_')), ('tr', ('passo/tr_', 'passo/marmore_tr_')), ('ater', ('corpo/aterrissa_pedra_', 'corpo/aterrissa_marmore_'))):
        for j, pad in enumerate(pads):
            xs = [sf.read(f)[0] for f in sorted(glob.glob(os.path.join(S, pad + '[0-9]*.wav')))]
            y = mix([(0, pulo, 0.8), (0.55, xs[0], 1.0)], 1.6) if g == 'ater' else mix([(i / 3.6, xs[i % len(xs)], 0.8 if i % 2 else 0.65) for i in range(8)], 8 / 3.6 + 0.6)
            motor.salvar(os.path.join(OP, f'marmore_{g}_{j}.wav'), y)
    os._exit(0)
