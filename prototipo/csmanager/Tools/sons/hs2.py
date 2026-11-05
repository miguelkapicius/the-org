# HS escolhido pelo usuário no ouvido: candidatos 2, 6 e 10 = as 3 variações da cabeça (sem mexer no caráter).
# Com capacete = metal furado + a MESMA cabeça; régua só escolhe qual metal, volume e atraso (média nas 3).
import glob, os, itertools, numpy as np, soundfile as sf
import ajusta, hibrido, motor
from tonal import tonalidade
SR = motor.SR; B = hibrido.BRUTO
ESCOLHIDOS = [2, 6, 10]   # numeração da página (1..12)
def rms(x): return float(np.sqrt(np.mean(x[:int(0.15 * SR)] ** 2)) + 1e-12)
cabecas = [hibrido.aparar(hibrido.carregar(os.path.join(B, 'impacto', f'hs_c{n - 1}.mp3'))) for n in ESCOLHIDOS]
alvo = np.median([rms(c / np.abs(c).max()) for c in cabecas])
cabecas = [c / np.abs(c).max() * alvo / rms(c / np.abs(c).max()) for c in cabecas]   # mesmo volume percebido
pk = max(np.abs(c).max() for c in cabecas); cabecas = [c / pk * 0.89 for c in cabecas]
for f in glob.glob('saida/impacto/cabeca_*.wav'): os.remove(f)
for k, c in enumerate(cabecas): motor.salvar(f'saida/impacto/cabeca_{k}.wav', motor.acabamento(c, -1 - 20 * np.log10(0.89 / (np.abs(c).max() + 1e-9)) * 0, 20) if False else c)
A = ajusta.alvo(['D:/unity/Tools/ref_sons_cs2/capacete_ref_mix.wav'])
metais = [(os.path.basename(m), hibrido.aparar(hibrido.carregar(m))) for m in sorted(glob.glob(os.path.join(B, 'impacto', 'metal_furo_c*.mp3')))]
def monta(m, c, gm, off):
    m = m / (np.abs(m).max() + 1e-9); i = int(off * SR); n = max(len(m), i + len(c)) + 500; y = np.zeros(n)
    y[:len(m)] += gm * m * np.abs(c).max(); y[i:i + len(c)] += c
    return y / (np.abs(y).max() + 1e-9) * 0.89
res = []
for (mn, m), gm, off in itertools.product(metais, (0.35, 0.55, 0.8), (0.004, 0.01, 0.018)):
    ys = [monta(m, c, gm, off) for c in cabecas]
    custo = np.mean([ajusta.custo(ajusta.feat(y), A) + max(0.0, tonalidade(y, SR) - 9) for y in ys])
    res.append((custo, mn, gm, off, ys))
res.sort(key=lambda r: r[0])
for r in res[:3]: print(f'{r[0]:6.2f} {r[1]} metal x{r[2]} cabeça +{r[3]*1000:.0f} ms')
for f in glob.glob('saida/impacto/capacete_*.wav'): os.remove(f)
for k, y in enumerate(res[0][4]): motor.salvar(f'saida/impacto/capacete_{k}.wav', y)
print('ok: cabeca 0-2 = candidatos', ESCOLHIDOS, '| capacete 0-2 com', res[0][1])
