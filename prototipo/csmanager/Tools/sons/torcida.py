# Trata as tomadas de torcida (torcida_ia.py) pro jogo e monta as opções de audição da página.
# Murmúrio: laço sem emenda (a cauda cruza com o começo) e TODOS no mesmo nível pela mediana do RMS de 1 s (os gritinhos
# no meio continuam acima da cama, como numa arena). Reações: aparadas no começo, cauda com fade, niveladas por categoria
# (usuário: "tem que ser mais alto") e com teto de pico de joelho suave. Tudo em ESTÉREO (largura de arena).
# Uso: python torcida.py         -> saida/torcida/*.wav (novos) + saida/_opcoes/pl*_K.wav (página)
import os, glob, numpy as np, soundfile as sf
from scipy.signal import resample_poly
import motor
from motor import SR
from torcida_ia import MURMURIO, REACOES
from awp_seco import kclip

AQUI = os.path.dirname(os.path.abspath(__file__))
B = os.path.join(AQUI, 'eleven', 'bruto', 'torcida')
S = os.path.join(AQUI, 'saida'); T = os.path.join(S, 'torcida'); OP = os.path.join(S, '_opcoes')
# nível de cada categoria: RMS do trecho de 400 ms mais alto (dBFS). Os antigos clutch/ace estavam em -11.
ALVO = dict(inicio=-10, fim=-9.5, round=-12, multikill=-10, jogada=-11.5, quase=-13, clutch=-10, ace=-9.5)
MURM_DB = -22.0          # mediana do RMS de 1 s da cama
# reações novas de clutch/ace entram DEPOIS das já aprovadas (clutch_0-3, ace_0-4)
INI = dict(clutch=4, ace=5)


def ler(n):
    x, sr = sf.read(os.path.join(B, n + '.mp3'), always_2d=True)
    if x.shape[1] == 1: x = np.repeat(x, 2, 1)
    return resample_poly(x[:, :2], SR, sr, axis=0)


def hp(x, f): return np.stack([motor.hp(x[:, c], f) for c in range(2)], 1)
def db(v): return 20 * np.log10(v + 1e-12)
def janelas(x, w):
    m = (x ** 2).mean(1); n = int(w * SR)
    return np.array([np.sqrt(m[i:i + n].mean()) for i in range(0, max(1, len(m) - n), n // 2)])


def laco(x, cf=2.0):
    n = int(cf * SR); y = x[n:].copy(); t = np.linspace(0, np.pi / 2, n)[:, None]
    y[-n:] = y[-n:] * np.cos(t) + x[:n] * np.sin(t)          # a cauda vira o começo: emenda some ao repetir
    return y


def teto(x, pk=-1.0):
    g = 10 ** (pk / 20); return kclip(x / g, 0.8) * g


def murmurio(i):
    x = hp(ler(f'murmurio_n{i}'), 45)
    x = x * 10 ** ((MURM_DB - db(np.median(janelas(x, 1.0)))) / 20)
    # gritinho no meio da cama pode, explosão não: em laço ela fica reconhecível. Segura o RMS de 400 ms em MURM_DB + 8.
    # (antes do laço: assim a emenda cruza dois trechos já domados e continua contínua)
    n = int(0.4 * SR); e = np.sqrt(media(((x ** 2).mean(1)), n)) + 1e-9
    g = media(np.minimum(1.0, 10 ** ((MURM_DB + 8 - db(e)) / 20)), n)
    return teto(laco(x * g[:, None]), -3.0)


def media(v, n):
    """Média móvel centrada por soma acumulada (np.convolve com janela de 19 mil amostras levava minutos)."""
    c = np.concatenate(([0.0], np.cumsum(v))); i = np.arange(len(v))
    a = np.clip(i - n // 2, 0, len(v)); b = np.clip(i + n // 2 + 1, 0, len(v))
    return (c[b] - c[a]) / (b - a)


def reacao(nome, i):
    x = hp(ler(f'{nome}_n{i}'), 50)
    e = np.sqrt((x ** 2).mean(1)); e = np.convolve(e, np.ones(240) / 240, 'same')
    vivo = np.where(e > e.max() * 10 ** (-40 / 20))[0]
    x = x[max(0, vivo[0] - int(0.01 * SR)):]                  # tira o silêncio do começo
    f = min(len(x) // 4, int(0.7 * SR)); x[-f:] *= (np.linspace(1, 0, f) ** 1.5)[:, None]   # sai sem corte seco
    x = x * 10 ** ((ALVO[nome] - db(janelas(x, 0.4).max())) / 20)
    return teto(x, -1.0)


def salvar(p, x): os.makedirs(os.path.dirname(p), exist_ok=True); sf.write(p, x.astype(np.float32), SR, subtype='PCM_16')


def coloca(dest, x, t, g=1.0):
    i = int(t * SR); m = min(len(x), len(dest) - i)
    if m > 0: dest[i:i + m] += x[:m] * g


def cena(m, r):
    """45 s de transmissão: cama (2 camadas cruzando) + tiroteio de fundo + início, round, multi-kill, jogada, clutch."""
    L = int(45 * SR); y = np.zeros((L, 2))
    a, b = m[3], m[10]
    for k, (cam, g) in enumerate(((a, 1.0), (b, 0.7))):
        z = np.tile(cam, (int(np.ceil(L / len(cam))) + 1, 1))[k * 7 * SR:][:L]; y += z * g * 0.75
    ak = sf.read(os.path.join(S, 'ak', 'distante_0.wav'))[0]; ak = np.stack([ak, ak], 1) if ak.ndim == 1 else ak
    for t in [9, 9.1, 9.2, 15, 15.12, 21, 21.1, 21.2, 21.3, 30, 30.1, 37, 37.1, 37.2]: coloca(y, ak, t, 0.5)
    for t, x in ((0.5, r['inicio'][0]), (12, r['round'][0]), (22, r['multikill'][0]), (31, r['jogada'][0]), (38.5, r['clutch'][0])):
        coloca(y, x, t, 1.0)
    return y / (np.abs(y).max() + 1e-12) * 10 ** (-1 / 20)


def main():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'pl*_*.wav')): os.remove(f)
    m = []
    for i in range(len(MURMURIO)):
        x = murmurio(i); m.append(x); salvar(os.path.join(T, f'murmurio_{i}.wav'), x)
        salvar(os.path.join(OP, f'plm_{i}.wav'), x[:int(10 * SR)])   # página: 10 s de cada (o jogo usa ~20 s em laço)
        print('murmurio', i, round(len(x) / SR, 1), 's', flush=True)
    r = {}
    for nome, _t, _d, n in REACOES:
        r[nome] = []
        for i in range(n):
            x = reacao(nome, i); r[nome].append(x)
            salvar(os.path.join(T, f'{nome}_{INI.get(nome, 0) + i}.wav'), x)
            salvar(os.path.join(OP, f'pl{nome}_{i}.wav'), x)
        print(nome, n, flush=True)
    salvar(os.path.join(OP, 'plcena_0.wav'), cena(m, r))
    print('cena ok', flush=True)


if __name__ == '__main__':
    main()
    os._exit(0)
