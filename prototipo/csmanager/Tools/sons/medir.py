# Mede características de um som (ref CS2 ou nosso): usado como régua. Uso: medir.py arq... [--json saida]
import sys, json, numpy as np, soundfile as sf
from scipy.signal import butter, sosfilt
def bandas(x, sr):
    X = np.abs(np.fft.rfft(x * np.hanning(len(x))))**2; f = np.fft.rfftfreq(len(x), 1/sr); tot = X.sum() + 1e-20
    lim = [(0,100),(100,400),(400,2000),(2000,8000),(8000,sr/2)]
    return [float(X[(f>=a)&(f<b)].sum()/tot) for a,b in lim], float((X*f).sum()/tot)
def medir(p):
    x, sr = sf.read(p, always_2d=True); return medir_arr(x.mean(1), sr, p)


def medir_arr(x, sr, p='mem'):
    env = np.sqrt(np.convolve(x**2, np.ones(int(sr*0.005))/int(sr*0.005), 'same')) + 1e-9
    i0 = int(np.argmax(env)); pk = env[i0]; db = 20*np.log10(env/pk)
    ini = int(np.argmax(db > -30))
    def ate(lv):
        j = np.where(db[i0:] < lv)[0]; return float(j[0]/sr) if len(j) else float((len(x)-i0)/sr)
    seg = x[ini:ini+int(0.12*sr)]; b, c = bandas(seg, sr)
    tail = x[ini+int(0.12*sr):ini+int(0.8*sr)]; bt, ct = bandas(tail, sr) if len(tail) > 2048 else ([0]*5, 0)
    # "loudness" simples: RMS máx em janela de 50 ms com pré-ênfase leve (aprox. K)
    sos = butter(2, 60, 'hp', fs=sr, output='sos'); y = sosfilt(sos, x)
    w = int(0.05*sr); r = np.sqrt(np.convolve(y**2, np.ones(w)/w, 'valid')).max() + 1e-9
    return dict(arq=p.split('/')[-1], sr=sr, dur=round(len(x)/sr,3), pico_db=round(20*np.log10(np.abs(x).max()+1e-9),1),
                rms50_db=round(20*np.log10(r),1), ataque_ms=round((i0-ini)/sr*1000,1), d20=round(ate(-20),3), d40=round(ate(-40),3),
                brilho=round(c), bandas=[round(v,3) for v in b], brilho_cauda=round(ct), bandas_cauda=[round(v,3) for v in bt])
if __name__ == '__main__':
    a = [x for x in sys.argv[1:] if not x.startswith('--')]; out = []
    js = sys.argv[sys.argv.index('--json')+1] if '--json' in sys.argv else None
    if js: a.remove(js)
    for p in a:
        try: m = medir(p); out.append(m); print(f"{m['arq'][:34]:34s} {m['dur']:5.2f}s pk{m['pico_db']:6.1f} rms{m['rms50_db']:6.1f} atq{m['ataque_ms']:5.1f} d20 {m['d20']:.3f} d40 {m['d40']:.3f} br{m['brilho']:5d} {m['bandas']} cauda br{m['brilho_cauda']}")
        except Exception as e: print(p, 'ERRO', e)
    if js: json.dump(out, open(js,'w'), indent=1)
