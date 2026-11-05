# Ajustes das notas da página v9 que não precisam de som novo (2026-11-04).
import glob, os, numpy as np, soundfile as sf
import motor
SR = motor.SR
S = 'saida'


def rm(padrao):
    n = 0
    for f in glob.glob(os.path.join(S, padrao)): os.remove(f); n += 1
    return n


def ler(rel): return sf.read(os.path.join(S, rel))[0]


def pitch(x, semitons):
    r = 2 ** (semitons / 12); return np.interp(np.arange(0, len(x) - 1, r), np.arange(len(x)), x)


# 1) removidos pelo usuário ("não tem no CS" / "não precisa")
for p in ('arma/pegar_*.wav', 'c4/pegar_*.wav', 'corpo/equipamento_*.wav', 'granada/acertou_*.wav', 'granada/zumbido_*.wav',
          'hud/pause_coach_*.wav', 'hud/colete_*.wav', 'c4/alerta10_*.wav'):
    print('removido', p, rm(p))

# 2) um bip só (não alterna entre dois sons): o final aprovado; o do meio = ele -1,5 semitom
bip = ler('c4/bip_rapido_0.wav')
rm('c4/bip_rapido_*.wav'); rm('c4/bip_[0-9]*.wav')
motor.salvar(os.path.join(S, 'c4', 'bip_rapido_0.wav'), bip)
motor.salvar(os.path.join(S, 'c4', 'bip_0.wav'), pitch(bip, -1.5) * 0.85)

# 3) teclado: o nº 1 que ele escolheu
dig = ler('c4/digita_0.wav'); rm('c4/digita_*.wav'); motor.salvar(os.path.join(S, 'c4', 'digita_0.wav'), dig)

# 4) cápsula de pistola "na mesma pegada da de fuzil": a de fuzil aprovada, mais aguda e curta
rm('bala/capsula_pistola_*.wav')
for k, f in enumerate(sorted(glob.glob(os.path.join(S, 'bala', 'capsula_fuzil_*.wav')))):
    x = pitch(sf.read(f)[0], 5); x = x[:int(0.35 * SR)].copy(); x[-int(0.08 * SR):] *= np.linspace(1, 0, int(0.08 * SR)) ** 2
    motor.salvar(os.path.join(S, 'bala', f'capsula_pistola_{k}.wav'), x * 0.8)

# 5) match point mais alto (estava -6 dBFS de pico)
for f in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')):
    motor.salvar(f, motor.acabamento(sf.read(f)[0], -0.5, 30))

# 6) faca na parede: sem o chocalho do fim (corta depois do golpe principal)
for f in glob.glob(os.path.join(S, 'faca', 'parede_*.wav')):
    x = sf.read(f)[0]; e = np.abs(x); pk = int(np.argmax(e)); fim = min(len(x), pk + int(0.22 * SR))
    if len(x) <= pk + int(0.23 * SR): continue   # já cortado
    y = x[:fim].copy(); y[-int(0.06 * SR):] *= np.linspace(1, 0, int(0.06 * SR)) ** 2
    motor.salvar(f, y)

# 7) começar a desarmar = dois bips (como o CS2), com o bip aprovado
rm('c4/desarme_inicio_*.wav')
for k, gap in enumerate((0.11, 0.13, 0.12)):
    n = int((gap + len(bip) / SR + 0.05) * SR); y = np.zeros(n)
    y[:len(bip)] += bip * 0.8; i = int(gap * SR); b2 = pitch(bip, 0.5 * k)[:n - i]; y[i:i + len(b2)] += b2 * 0.8
    motor.salvar(os.path.join(S, 'c4', f'desarme_inicio_{k}.wav'), y)

# 8) chiado final = o bip aprovado acelerando até virar tom contínuo (timer acabou), 3 opções de aceleração
os.makedirs(os.path.join(S, '_opcoes'), exist_ok=True)
rm('_opcoes/c4final_*.wav')
b = bip[:int(0.06 * SR)].copy(); b[-int(0.01 * SR):] *= np.linspace(1, 0, int(0.01 * SR))
for k, (ini, fim_, tom) in enumerate(((0.11, 0.035, 0.35), (0.09, 0.03, 0.5), (0.13, 0.045, 0.25))):
    n = int(1.4 * SR); y = np.zeros(n); t = 0.0; dt = ini
    while t < 1.0 - tom * 0.5:
        i = int(t * SR); y[i:i + len(b)] += b[:max(0, n - i)] * (0.7 + 0.3 * t)
        t += dt; dt = max(fim_, dt * 0.9)
    # tom final: o próprio bip esticado (laço do miolo) com chiado por cima
    miolo = b[int(0.01 * SR):int(0.04 * SR)]; tomx = np.tile(miolo, int(tom * SR / len(miolo)) + 1)[:int(tom * SR)]
    tomx = tomx * np.linspace(1, 0.8, len(tomx)) + motor.hp(np.random.default_rng(k).standard_normal(len(tomx)), 5000) * 0.05
    i = int(t * SR); y[i:i + len(tomx)] += tomx[:n - i]
    motor.salvar(os.path.join(S, '_opcoes', f'c4final_{k}.wav'), motor.acabamento(y, -1, 20))
print('ajustes ok')
