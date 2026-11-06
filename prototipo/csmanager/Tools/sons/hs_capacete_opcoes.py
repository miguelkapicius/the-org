# 3 opções montadas de HS com capacete: metal furado (melhor pela régua) + cada uma das 3 cabeças aprovadas.
import glob, os, numpy as np, soundfile as sf
import ajusta, hibrido, motor
from tonal import tonalidade
SR = motor.SR
A = ajusta.alvo(['D:/unity/Tools/ref_sons_cs2/capacete_ref_mix.wav'])
cabecas = [sf.read(f'saida/impacto/cabeca_{k}.wav')[0] for k in range(3)]
def monta(m, c, gm=0.55, off=0.01):
    m = m / (np.abs(m).max() + 1e-9); i = int(off * SR); n = max(len(m), i + len(c)) + 500; y = np.zeros(n)
    y[:len(m)] += gm * m * np.abs(c).max(); y[i:i + len(c)] += c
    return y / (np.abs(y).max() + 1e-9) * 0.89
melhor = min(((np.mean([ajusta.custo(ajusta.feat(monta(m, c)), A) + max(0, tonalidade(monta(m, c), SR) - 9) for c in cabecas]), f)
              for f in sorted(glob.glob(os.path.join(hibrido.BRUTO, 'impacto', 'metal_furo_c*.mp3')))
              for m in [hibrido.aparar(hibrido.carregar(f))]), key=lambda t: t[0])
m = hibrido.aparar(hibrido.carregar(melhor[1])); os.makedirs('saida/_opcoes', exist_ok=True)
for k, c in enumerate(cabecas): motor.salvar(f'saida/_opcoes/capacete_montado_{k}.wav', monta(m, c))
print('metal escolhido:', os.path.basename(melhor[1]), round(melhor[0], 2))
