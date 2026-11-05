# v17 (2026-11-04): match point com BAM (como o do CS2, no começo do round), AWP com mais estrondo, CT no mármore = IA nº 3.
# Uso: python v17.py           -> candidatos em saida/_opcoes (matchpoint_K, awp_boom_K) + aplica o mármore do CT
#      python v17.py usar MP AWP  (1-based; AWP 1 = a atual, sem mudança)
import os, sys, glob, numpy as np, soundfile as sf
import motor, eleven
from motor import SR
from hibrido import carregar, aparar, variacao

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes')
BRUTO = os.path.join(AQUI, 'eleven', 'bruto')
MP = ['massive cinematic impact hit: a deep booming bass drop with a heavy metallic slam and a short rumble tail, dramatic esports match point stinger',
      'huge trailer boom: one powerful low drum hit layered with a metal clang, tense and dramatic, short tail, no music',
      'epic single orchestral impact, giant bass boom and braam, tournament broadcast stinger, punchy, 2 seconds']


def ler(n): return sf.read(os.path.join(S, n))[0]


def matchpoint():
    k = eleven.chave()
    for i, p in enumerate(MP):
        for c in range(2):
            d = os.path.join(BRUTO, 'hud', f'matchpoint_c{i * 2 + c}.mp3')
            if not os.path.exists(d): eleven.gerar_um(k, p, 2.2, False, d)
    for f in glob.glob(os.path.join(OP, 'matchpoint_*.wav')): os.remove(f)
    for j, f in enumerate(sorted(glob.glob(os.path.join(BRUTO, 'hud', 'matchpoint_c*.mp3')))):
        x = aparar(carregar(f))[:int(2.4 * SR)].copy(); fd = int(0.3 * SR); x[-fd:] *= np.linspace(1, 0, fd)
        motor.salvar(os.path.join(OP, f'matchpoint_{j}.wav'), motor.acabamento(motor.hp(x, 25), -1, 20))
    print('match point: candidatos', j + 1, flush=True)


def boom(x, nivel):
    """Estrondo da AWP: grave de corpo + 'tum' sub logo no estouro (não mexe no eco)."""
    if nivel == 0: return x
    pk = int(np.argmax(np.abs(x[:int(0.05 * SR)])))
    n = int(0.35 * SR); t = np.arange(n) / SR
    f = 42 + 38 * np.exp(-t / 0.04)                                   # 'tum' que cai de 80 pra 42 Hz
    tum = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (0.09 + 0.03 * nivel)) * np.clip(t / 0.002, 0, 1)
    y = motor.prateleira_grave(x, 120, 2.5 * nivel)
    y = y.copy(); m = min(n, len(y) - pk); y[pk:pk + m] += tum[:m] * (0.25 + 0.15 * nivel) * np.abs(x).max()
    y = motor.compressor(y, -12, 2.5, 8.0)
    return y * np.abs(x).max() / (np.abs(y).max() + 1e-12)


def awp():
    for f in glob.glob(os.path.join(OP, 'awp_boom_*.wav')): os.remove(f)
    x = ler('awp/tiro_0.wav')
    for nv in range(4): motor.salvar(os.path.join(OP, f'awp_boom_{nv}.wav'), boom(x, nv))
    print('awp: 4 versões (1 = atual)', flush=True)


def marmore_ct():
    alvo = np.mean([np.sqrt((sf.read(f)[0][:int(0.15 * SR)] ** 2).mean()) for f in sorted(glob.glob(os.path.join(S, 'passo', 'ct_*.wav')))[:4]])
    x = aparar(carregar(os.path.join(BRUTO, 'passo', 'marmore_ct_c2.mp3')))[:int(0.45 * SR)].copy()
    fd = int(0.05 * SR); x[-fd:] *= np.linspace(1, 0, fd); x = motor.hp(x, 40)
    x = np.clip(x * alvo / (np.sqrt((x[:int(0.15 * SR)] ** 2).mean()) + 1e-9), -0.98, 0.98)
    for f in glob.glob(os.path.join(S, 'passo', 'marmore_ct_*.wav')): os.remove(f)
    for i in range(6): motor.salvar(os.path.join(S, 'passo', f'marmore_ct_{i}.wav'), x if i == 0 else variacao(x, i))
    print('mármore CT = IA nº 3', flush=True)


def usar(mp, nv):
    for f in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')): os.remove(f)
    motor.salvar(os.path.join(S, 'hud', 'match_point_0.wav'), sf.read(os.path.join(OP, f'matchpoint_{mp}.wav'))[0])
    if nv > 0:
        for f in sorted(glob.glob(os.path.join(S, 'awp', 'tiro_*.wav'))): motor.salvar(f, boom(sf.read(f)[0], nv))
    print('usado: match point', mp + 1, 'awp', nv + 1)


if __name__ == '__main__':
    if len(sys.argv) > 3 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1, int(sys.argv[3]) - 1)
    else: marmore_ct(); awp(); matchpoint()
    os._exit(0)
