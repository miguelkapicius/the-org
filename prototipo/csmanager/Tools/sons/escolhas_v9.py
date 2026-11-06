# Candidatos v9 (índice >= 20) -> saida/_opcoes/<grupo>_<nome>_<k>.wav pra escolha no ouvido (não vai pro jogo).
import glob, os, json, numpy as np, soundfile as sf
import hibrido, motor
from tonal import tonalidade
SR = motor.SR
ITENS = [('arma', 'cai_fuzil'), ('arma', 'cai_pistola'), ('bala', 'passando'), ('bala', 'passando_sub'), ('bala', 'ricochete'),
         ('c4', 'largar'), ('c4', 'sacar'), ('faca', 'sacar'), ('granada', 'molotov_ar'), ('granada', 'queimando'),
         ('granada', 'sacar'), ('hud', 'compra'), ('hud', 'inicio_round')]
bip = sf.read('saida/c4/bip_rapido_0.wav')[0]
d = os.path.join('saida', '_opcoes'); os.makedirs(d, exist_ok=True)
rel = {}
for g, n in ITENS:
    for f in glob.glob(os.path.join(d, f'{g}_{n}_*.wav')): os.remove(f)
    crus = sorted(glob.glob(os.path.join(hibrido.BRUTO, g, f'{n}_c*.mp3')), key=lambda c: int(c.rsplit('_c', 1)[1][:-4]))
    crus = [c for c in crus if int(c.rsplit('_c', 1)[1][:-4]) >= 20]
    k = 0; fora = 0
    for c in crus:
        x = hibrido.aparar(hibrido.carregar(c))
        if g == 'bala' and n.startswith('passando') and tonalidade(x, SR) > hibrido.TOM_MAX: fora += 1; continue   # "laser"
        if (g, n) == ('c4', 'sacar'):   # como no CS: pega a C4 e ela dá um bip
            y = np.zeros(len(x) + int(0.35 * SR)); y[:len(x)] += x; i = len(x) - int(0.05 * SR); y[i:i + len(bip)] += bip[:len(y) - i] * 0.5; x = y
        x = motor.acabamento(x, hibrido.nivel(g, n), 20)
        motor.salvar(os.path.join(d, f'{g}_{n}_{k}.wav'), x); k += 1
    rel[f'{g}/{n}'] = dict(opcoes=k, laser_descartados=fora)
    print(f'{g}/{n}: {k} opções' + (f' ({fora} com tom de laser descartados)' if fora else ''))
json.dump(rel, open(os.path.join(d, 'escolhas_v9.json'), 'w'), indent=1)
