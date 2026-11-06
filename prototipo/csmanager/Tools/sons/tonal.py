# "Laser/furadeira": som com TOM (pico estreito dominando o espectro) em vez de estouro de ruído. Mede o quanto o
# maior pico do espectro (300-8000 Hz, 0-300 ms) sobe acima da mediana vizinha, em dB. Tiro real é ruidoso (baixo).
import numpy as np
from scipy.signal import stft
def tonalidade(x, sr):
    x = x[: int(0.3 * sr)]
    f, t, Z = stft(x, sr, nperseg=2048, noverlap=1536)
    P = (np.abs(Z) ** 2).mean(1); m = (f > 300) & (f < 8000); P = P[m]
    viz = np.array([np.median(P[max(0, i - 25): i + 25]) for i in range(len(P))])
    return float(10 * np.log10((P / (viz + 1e-20)).max()))
if __name__ == '__main__':
    import sys, glob, soundfile as sf
    for p in sys.argv[1:]:
        for f in sorted(glob.glob(p)):
            x, sr = sf.read(f, always_2d=True); print(f'{tonalidade(x.mean(1), sr):5.1f} dB  {f[-40:]}')
