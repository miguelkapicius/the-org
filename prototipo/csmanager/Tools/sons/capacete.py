# HS com capacete no formato do CS2: camada "dink" (ElevenLabs) + carne (impacto/cabeca aprovado) na proporção 0,6/0,3.
# Escolhe candidato, pitch e mistura pela régua contra o alvo medido (capacete_ref_mix.wav = como o CS2 toca o evento).
import glob, os, numpy as np, soundfile as sf, itertools
from scipy.signal import resample_poly
import ajusta, hibrido, motor
from tonal import tonalidade
REF = 'D:/unity/Tools/ref_sons_cs2/capacete_ref_mix.wav'
A = ajusta.alvo([REF]); x_ref, sr = sf.read(REF); tom_ref = tonalidade(x_ref, sr)
carne = sf.read('saida/impacto/cabeca_0.wav')[0]
def pitch(x, f): return np.interp(np.arange(0, len(x) - 1, f), np.arange(len(x)), x)
CROC = [hibrido.aparar(hibrido.carregar(c)) for c in sorted(glob.glob(os.path.join(hibrido.BRUTO, 'impacto', 'crocante_c*.mp3')))]
def monta(d, f, g, cr=None, gc=0.0, sus=0.18, hpf=0, comp=-12, cr2=None, off2=0.0):
    d = pitch(d / (np.abs(d).max() + 1e-9), f); n = int(0.9 * motor.SR); y = np.zeros(n)
    y[:min(n, len(d))] += 0.6 * d[:n]; y[:len(carne)] += g * carne / (np.abs(carne).max() + 1e-9)
    if cr is not None and gc > 0:   # crocância sustentada até 'sus' s e depois cai (o "cheio" de ~160 ms do CS2)
        c = cr[:n] / (np.abs(cr).max() + 1e-9); t = np.arange(len(c)) / motor.SR
        c = c * np.where(t < sus, 1.0, np.exp(-(t - sus) / 0.06)); y[:len(c)] += gc * c
    if cr2 is not None and off2 > 0:   # segunda trinca (osso quebrando em sequência)
        c2 = cr2 / (np.abs(cr2).max() + 1e-9); i = int(off2 * motor.SR); m = min(len(c2), n - i); y[i:i + m] += gc * 0.8 * c2[:m]
    if hpf: y = motor.hp(y, hpf, 2)
    return motor.acabamento(motor.compressor(y, comp, 4), -1, 20)
res = []
for c in [os.path.join(hibrido.BRUTO, 'impacto', 'dink_c3.mp3')]:
    d = hibrido.aparar(hibrido.carregar(c))
    for f, g, ci, gc, sus, hpf, comp, c2, off2 in itertools.product((0.9,), (0.15,), (3,), (0.5, 0.8), (0.3,), (250,), (-20, -26), (0, 1, 2, 4, 5), (0.05, 0.08, 0.11)):
        y = monta(d, f, g, CROC[ci], gc, sus, hpf, comp, CROC[c2] if c2 != 3 else None, off2); fe = ajusta.feat(y)
        custo = ajusta.custo(fe, A) + max(0.0, tonalidade(y, motor.SR) - tom_ref - 1)
        res.append((custo, os.path.basename(c), f, g, y, ci, gc, sus, hpf, comp, c2, off2))
res.sort(key=lambda r: r[0])
for r in res[:6]: print(f'{r[0]:6.2f}  {r[1]}  pitch {r[2]}  carne {r[3]}  crocante c{r[5]} x{r[6]} sustento {r[7]} s  corte {r[8]} Hz  comp {r[9]}  2a trinca c{r[10]} +{r[11]} s')
melhor = res[0]
for f in glob.glob('saida/impacto/capacete_*.wav'): os.remove(f)
for k in range(4): motor.salvar(f'saida/impacto/capacete_{k}.wav', motor.acabamento(hibrido.variacao(melhor[4], k), -1, 20))
print('capacete: base', melhor[1])
