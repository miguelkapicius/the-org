# Envelope em dB (relativo ao pico, janela 5 ms) em instantes depois do início. Uso: envelope.py arq...
import sys, numpy as np, soundfile as sf
T = [0, 2, 5, 10, 20, 40, 80, 160, 320, 640, 1280]
def env_db(p):
    x, sr = sf.read(p, always_2d=True); x = x.mean(1)
    w = int(0.005 * sr); e = np.sqrt(np.convolve(x**2, np.ones(w)/w, 'same')) + 1e-9
    db = 20*np.log10(e/e.max()); ini = int(np.argmax(db > -30))
    return [round(float(db[min(len(db)-1, ini + int(ms*sr/1000))]), 1) for ms in T]
if __name__ == '__main__':
    print(' '*28 + ''.join(f'{t:>7d}' for t in T))
    for p in sys.argv[1:]: print(f'{p.replace(chr(92),"/").split("/")[-2][:10]+"/"+p.split("/")[-1][:16]:28s}' + ''.join(f'{v:7.1f}' for v in env_db(p)))
