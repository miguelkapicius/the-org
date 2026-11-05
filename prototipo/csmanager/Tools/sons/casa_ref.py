# EQ casado com a referência do CS2 (régua de faixas do raio-x): puxa o balanço sub/grave/médio/agudo/ar do nosso tiro
# pro da arma no CS2, mantendo o pico. Usado na Tec-9 e na Five-SeveN (2026-11-04: saíam "caixa", sem a assinatura).
# Uso: python casa_ref.py arma ref.wav [forca]   -> reescreve saida/<arma>/tiro_*.wav (original guardado em saida/_opcoes/<arma>_antes_K)
import os, sys, glob, shutil, numpy as np, soundfile as sf
from scipy.signal import resample_poly
import motor
from motor import SR

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida')
B = [(20, 150), (150, 500), (500, 2000), (2000, 6000), (6000, 16000)]


def bandas(x, ini=0):
    seg = x[ini:ini + int(0.25 * SR)]; S_ = np.abs(np.fft.rfft(seg * np.hanning(len(seg)))) ** 2; fr = np.fft.rfftfreq(len(seg), 1 / SR)
    e = np.array([S_[(fr >= a) & (fr < b)].sum() for a, b in B]); return e / e.sum()


def partes(x):
    return [motor.lp(x, 150, 4)] + [motor.bp(x, a, b, 4) for a, b in B[1:-1]] + [motor.bp(x, 6000, 16000, 4)]


def casa(x, alvo, forca=0.8, voltas=3):
    pk0 = np.abs(x).max(); y = x
    for _ in range(voltas):
        p = int(np.argmax(np.abs(y))); e = bandas(y, p) + 1e-9
        g = np.clip((alvo / e) ** (0.5 * forca), 0.15, 8)
        y = sum(pi * gi for pi, gi in zip(partes(y), g))
    return y * pk0 / (np.abs(y).max() + 1e-12)


if __name__ == '__main__':
    a, ref = sys.argv[1], sys.argv[2]; forca = float(sys.argv[3]) if len(sys.argv) > 3 else 0.8
    r, sr = sf.read(ref, always_2d=True); r = r.mean(1)
    if sr != SR: r = resample_poly(r, SR, sr)
    alvo = bandas(r, int(np.argmax(np.abs(r))))
    for f in sorted(glob.glob(os.path.join(S, a, 'tiro_[0-9].wav'))):
        k = f.rsplit('_', 1)[1][:-4]; bk = os.path.join(S, '_opcoes', f'{a}_antes_{k}.wav')
        shutil.copy(f, bk); x = sf.read(bk)[0]
        motor.salvar(f, casa(x, alvo, forca))
    print(a, 'alvo', np.round(alvo * 100).astype(int), '-> nosso', np.round(bandas(sf.read(f)[0], int(np.argmax(np.abs(sf.read(f)[0])))) * 100).astype(int), flush=True)
    os._exit(0)
