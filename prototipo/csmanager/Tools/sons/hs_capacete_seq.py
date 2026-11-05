# HS com capacete = capacete quebrando -> cabeça aprovada (sem capacete) emendada; total <= 0,8 s (ideia do usuário).
import glob, os, numpy as np, soundfile as sf
import hibrido, motor
SR = motor.SR; MAX = 0.8
cabecas = [sf.read(f'saida/impacto/cabeca_{k}.wav')[0] for k in range(3)]
def env(x):
    w = int(0.005 * SR); return np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')) + 1e-9
def emenda(h, c):
    h = h / (np.abs(h).max() + 1e-9) * np.abs(c).max()            # capacete no mesmo pico da cabeça
    e = env(h); pk = int(np.argmax(e)); db = 20 * np.log10(e / e[pk])
    queda = np.where(db[pk:] < -12)[0]
    off = int(np.clip((pk + (queda[0] if len(queda) else len(db))) / SR, 0.05, 0.15) * SR)
    n = int(MAX * SR); y = np.zeros(n)
    hh = h[:n].copy(); f = int(0.04 * SR)
    if len(hh) > off: hh[off:off + f] *= np.linspace(1, 0, len(hh[off:off + f])); hh[off + f:] = 0   # capacete sai em 40 ms
    y[:len(hh)] += hh
    m = min(len(c), n - off); y[off:off + m] += c[:m]
    fim = int(0.06 * SR); y[-fim:] *= np.linspace(1, 0, fim) ** 2
    return y / (np.abs(y).max() + 1e-9) * np.abs(c).max(), off / SR
d = 'saida/_opcoes'; os.makedirs(d, exist_ok=True)
for f in glob.glob(os.path.join(d, 'hsc_*.wav')): os.remove(f)
crus = sorted(glob.glob(os.path.join(hibrido.BRUTO, 'impacto', 'capacete_quebra_c*.mp3')), key=lambda c: int(c.rsplit('_c', 1)[1][:-4]))
for i, c in enumerate(crus):
    y, off = emenda(hibrido.aparar(hibrido.carregar(c)), cabecas[i % 3])
    motor.salvar(os.path.join(d, f'hsc_{i}.wav'), y); print(f'{i + 1:2d}: cabeça {i % 3 + 1} entra em {off * 1000:.0f} ms, total {len(y) / SR:.2f} s')
