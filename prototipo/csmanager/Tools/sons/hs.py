# Headshot: bala entrando e destroçando a cabeça (base) — com capacete = metal perfurado + a MESMA base.
# Régua: sem capacete x headshot_noarmor_02-05 do CS2; com capacete x capacete_ref_mix (como o CS2 toca o evento).
import glob, os, itertools, numpy as np, soundfile as sf
import ajusta, hibrido, motor
from tonal import tonalidade
R = 'D:/unity/Tools/ref_sons_cs2/'
A_cab = ajusta.alvo(sorted(glob.glob(R + 'sounds/player/headshot_noarmor_0[2-5].wav')))
A_cap = ajusta.alvo([R + 'capacete_ref_mix.wav'])
SR = motor.SR
def cands(n): return [(os.path.basename(c), hibrido.aparar(hibrido.carregar(c))) for c in sorted(glob.glob(os.path.join(hibrido.BRUTO, 'impacto', f'{n}_c*.mp3')))]
def norm(x): return x / (np.abs(x).max() + 1e-9)
def pitch(x, f): return np.interp(np.arange(0, len(x) - 1, f), np.arange(len(x)), x)
def base(g, hpf, comp):
    y = norm(g)
    if hpf: y = motor.hp(y, hpf, 2)
    return motor.compressor(y, comp, 4)
def nota(y, A): return ajusta.custo(ajusta.feat(y), A) + max(0.0, tonalidade(y, SR) - 9)
gores, metais = cands('cabeca_gore'), cands('metal_furo')
# 1) base da cabeça
melhor = None
for (gn, g), hpf, comp in itertools.product(gores, (0, 150, 300), (-12, -20)):
    y = motor.acabamento(base(g, hpf, comp), -1, 20); c = nota(y, A_cab)
    if melhor is None or c < melhor[0]: melhor = (c, gn, hpf, comp, y)
c_cab, gn, hpf, comp, y_cab = melhor
print(f'sem capacete: {gn} corte {hpf} Hz comp {comp}  custo {c_cab:.2f}')
b = base(dict(gores)[gn], hpf, comp)
# 2) capacete = metal + a mesma base
melhor = None
for (mn, m), gm, off, fp in itertools.product(metais, (0.4, 0.7, 1.0), (0.0, 0.008, 0.016), (0.9, 1.0, 1.1)):
    mm = pitch(norm(m), fp); n = max(len(mm), len(b) + int(off * SR)) + 1000; y = np.zeros(n)
    y[:len(mm)] += gm * mm; i = int(off * SR); y[i:i + len(b)] += b
    y = motor.acabamento(motor.compressor(y, -14, 3), -1, 20); c = nota(y, A_cap)
    if melhor is None or c < melhor[0]: melhor = (c, mn, gm, off, fp, y)
c_cap, mn, gm, off, fp, y_cap = melhor
print(f'com capacete: {mn} metal x{gm} cabeça +{off*1000:.0f} ms pitch {fp}  custo {c_cap:.2f}')
for nome, y in (('cabeca', y_cab), ('capacete', y_cap)):
    for f in glob.glob(f'saida/impacto/{nome}_*.wav'): os.remove(f)
    for k in range(4): motor.salvar(f'saida/impacto/{nome}_{k}.wav', motor.acabamento(hibrido.variacao(y, k), -1, 20))
