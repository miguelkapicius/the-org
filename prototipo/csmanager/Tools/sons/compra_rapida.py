# Caixa registradora "em dois tempos, mas levemente mais rápida" (usuário, 2026-11-04).
# Para cada candidato: acha os dois tempos (alavanca/gaveta e sino) pelo fluxo espectral e aproxima o 2º do 1º
# (tira ~35% do intervalo, do fim da cauda do 1º, com crossfade) — o timbre e o tom ficam iguais. Cauda final encurtada.
# Sem segundo tempo claro: acelera 12% inteiro (reamostragem, sobe ~2 semitons, ainda soa natural).
# Entrada: eleven/bruto/hud/compra_caixa_cK.mp3 -> saida/_opcoes/compra_K.wav (o original fica em _opcoes/compra_lento_K.wav)
import os, glob, shutil, numpy as np, soundfile as sf
from scipy.signal import resample_poly
import motor
from motor import SR

OP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida', '_opcoes')
CORTE = 0.35


def fluxo(x, w=240):
    n = len(x) // w; fr = x[:n * w].reshape(n, w) * np.hanning(w)
    S = np.abs(np.fft.rfft(fr, axis=1)); d = np.maximum(0, np.diff(S, axis=0)).sum(1)
    return np.concatenate([[0], d]), w


def tempos(x):
    f, w = fluxo(motor.hp(x, 800)); f = f / (f.max() + 1e-9)
    on = [i for i in range(1, len(f) - 1) if f[i] > 0.25 and f[i] >= f[i - 1] and f[i] >= f[i + 1]]
    picos = []
    for i in on:
        if not picos or (i - picos[-1]) * w / SR > 0.08: picos.append(i)
        elif f[i] > f[picos[-1]]: picos[-1] = i
    return [p * w for p in picos]


def rapida(x):
    t = tempos(x)
    if len(t) >= 2:
        a, b = t[0], t[1]                      # 1º e 2º tempo (amostras)
        tira = int((b - a) * CORTE); xf = int(0.006 * SR)
        ini = b - int(0.004 * SR) - tira       # o trecho tirado acaba 4 ms antes do 2º tempo
        y = np.concatenate([x[:ini], np.zeros(len(x) - ini - tira)])
        resto = x[ini + tira:]
        y[ini - xf:ini] *= np.linspace(1, 0, xf); resto = resto.copy(); resto[:xf] *= np.linspace(0, 1, xf)
        y[ini - xf:ini - xf + len(resto)] += resto
        y = y[:len(x) - tira + xf]
        modo = f'2 tempos: {a / SR * 1000:.0f} e {b / SR * 1000:.0f} ms -> intervalo {(b - a - tira) / SR * 1000:.0f} ms'
    else:
        y = resample_poly(x, 25, 28); modo = 'sem 2º tempo claro: 12% mais rápida'
    # cauda mais curta: some depois de -35 dB
    e = np.abs(y); lim = e.max() * 10 ** (-35 / 20); vivo = np.where(e > lim)[0]; fim = min(len(y), vivo[-1] + int(0.03 * SR))
    y = y[:fim].copy(); f = int(0.06 * SR); y[-f:] *= np.linspace(1, 0, f)
    return motor.acabamento(y, -3, 4), modo


if __name__ == '__main__':
    for f in sorted(glob.glob(os.path.join(OP, 'compra_[0-9]*.wav'))):
        k = f.rsplit('_', 1)[1][:-4]; lento = os.path.join(OP, f'compra_lento_{k}.wav')
        if not os.path.exists(lento): shutil.copy(f, lento)
        x = sf.read(lento)[0]; y, modo = rapida(x)
        motor.salvar(f, y); print(int(k) + 1, f'{len(x) / SR:.2f} -> {len(y) / SR:.2f} s', modo, flush=True)
    os._exit(0)
