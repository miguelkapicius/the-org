# AK-47 refeita (2026-11-11, pedido do Gabriel: "não amo o som de algumas armas, testar se sai melhor").
# Raio-x (scratchpad raiox.py) da AK do jogo contra a do CS2, nos primeiros 120 ms, dB em relação à faixa mais forte:
#              30-80  80-150 150-300 300-500 500-1k 1-2k  2-4k  4-8k  8-16k
#   CS2         -1.8    0.0   -5.5    -4.4   -3.5  -8.4  -9.2  -6.4   -9.2
#   nossa       -2.7   -2.2   -8.9   -17.3   -1.3   0.0  -5.4 -12.4  -33.3
# = BURACO no soco (300-500), sobra de "caixa" (500-2k) e nenhum ar (8k+). E o corpo cai cedo (25-60 ms: -4,2 x -2,1).
# Caminho: tomadas novas pedindo corpo + brilho, curtas (0,8-1,2 s: menos espaço pra IA pôr eco), mp3 192;
# tratamento SEM camada sintética (lição da AWP): só a gravação, EQ puxando pro perfil do CS2 com limite, ar por
# excitador (harmônicos da própria gravação), sustentação por clip de joelho, cauda seca.
# Uso: python -u ak_fable.py gera      -> eleven/bruto/ak/tiro_f*.mp3 (cache: não regera)
#      python -u ak_fable.py mede      -> raio-x de cada tomada
#      python -u ak_fable.py opcoes    -> saida/_opcoes/akf_s_K.wav (1 tiro) e akf_r_K.wav (tiro + rajada)
#      python -u ak_fable.py usar K    -> saida/ak/tiro_0-3 (antigos em saida/_opcoes/ak_antes/)
import os, sys, glob, shutil, json, numpy as np, soundfile as sf
import motor, eleven
from motor import SR
from hibrido import carregar, aparar_tiro, aceita_tiro, variacao
from awp_seco import kclip

AQUI = os.path.dirname(os.path.abspath(__file__))
B = os.path.join(AQUI, 'eleven', 'bruto', 'ak'); S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes')
ANTES = os.path.join(OP, 'ak_antes')
REF = sorted(glob.glob('D:/unity/Tools/ref_sons_cs2/sounds/weapons/ak47/ak47_0[124].wav'))

PROMPTS = [
    'AK-47 single gunshot recorded with a close microphone at an outdoor shooting range, full-range high fidelity recording, loud punchy thump with thick low-mid body and a bright crisp crack, heavy steel bolt clack, one shot only',
    'single 7.62x39 rifle shot, first person shooter weapon sound effect, AAA production quality, fat punchy body, aggressive bright crack, tight and short, one shot only',
    'one gunshot from a Kalashnikov rifle from the shooter perspective, weighty barking blast with a chest punch and sizzling bright top end, metallic receiver clank, short decay, one shot only',
    'AKM assault rifle single shot, close studio weapon recording for film sound design, hard hitting, dense and thick, crisp high frequency snap, very short, no tail, one shot only',
    'one loud assault rifle gunshot for a competitive tactical shooter, heavy gritty wooden-stock rifle, powerful bark, sharp bright transient, dry, one shot only',
    'single shot of a 7.62mm rifle fired in an open field, close perspective, big punchy low-mid thud and crackling bright muzzle blast, crisp and detailed, no echo, one shot only',
]
DURS = [0.8, 1.2, 0.8]


def gera():
    k = eleven.chave()
    eleven.URL = eleven.URL.replace('mp3_44100_128', 'mp3_44100_192')
    gasto = 0
    for i, p in enumerate(PROMPTS):
        for c, dur in enumerate(DURS):
            d = os.path.join(B, f'tiro_f{i * 3 + c}.mp3')
            if os.path.exists(d): continue
            ok = eleven.gerar_um(k, p, dur, False, d); gasto += round(dur * eleven.CRED_SEG) if ok else 0
            print(i * 3 + c, 'ok' if ok else 'FALHOU', flush=True)
    print('gasto ~', gasto, 'créditos', flush=True)


FX = [(30, 80), (80, 150), (150, 300), (300, 500), (500, 1000), (1000, 2000), (2000, 4000), (4000, 8000), (8000, 16000)]
TX = [(0, 8), (8, 25), (25, 60), (60, 120), (120, 250), (250, 500)]


def no_pico(x):
    pk = int(np.argmax(np.abs(x))); return x[max(0, pk - 96):]


def faixas(x):
    a = no_pico(x)[:int(0.12 * SR)]; a = np.pad(a, (0, int(0.12 * SR) - len(a)))
    P = np.abs(np.fft.rfft(a * np.hanning(len(a)), 1 << 15)) ** 2; f = np.fft.rfftfreq(1 << 15, 1 / SR)
    return np.array([10 * np.log10(P[(f >= lo) & (f < hi)].sum() + 1e-20) for lo, hi in FX])


def tempo(x):
    e = no_pico(x) ** 2; t0 = e[:int(0.008 * SR)].mean() + 1e-20
    return np.array([10 * np.log10(e[int(a / 1000 * SR):int(b / 1000 * SR)].mean() / t0 + 1e-20) if len(e) > int(a / 1000 * SR) else -99 for a, b in TX])


def crista(x):
    x = no_pico(x); return 20 * np.log10(np.abs(x).max() / (np.sqrt((x[:int(0.05 * SR)] ** 2).mean()) + 1e-12))


def perfil_ref():
    return np.mean([faixas(carregar(r)) - faixas(carregar(r)).max() for r in REF], 0)


def eq_ref(x, forca=0.75, lim=9.0):
    """Puxa as 9 faixas pro perfil do CS2 (fração 'forca' da diferença, no máximo ±lim dB), curva suave em log-frequência."""
    b = faixas(x); d = perfil_ref() - (b - b.max()); d -= np.median(d)
    g = np.clip(d * forca, -lim, lim)
    fc = np.log([np.sqrt(lo * hi) for lo, hi in FX])
    X = np.fft.rfft(x); f = np.fft.rfftfreq(len(x), 1 / SR)
    G = np.interp(np.log(np.maximum(f, 1.0)), fc, g)
    return np.fft.irfft(X * 10 ** (G / 20), len(x)), g


def ar(x, nivel):
    """Ar/brilho tirado da própria gravação: o que há em 2,5 kHz+ saturado gera harmônicos, e só o de 6 kHz+ volta."""
    if nivel <= 0: return x
    h = motor.hp(x, 2500, 2); h = motor.satura(h / (np.abs(h).max() + 1e-12) * 4.0, 2.0); h = motor.hp(h, 6000, 2)
    return x + h * nivel * np.abs(x).max()


def ia(n): return aparar_tiro(carregar(os.path.join(B, f'tiro_f{n}.mp3')))


def norm(x): return x / (np.abs(x).max() + 1e-12)


def junta(a, b):
    a, b = no_pico(a), no_pico(b); y = np.zeros(max(len(a), len(b))); y[:len(a)] += a; y[:len(b)] += b; return y


def densa(x, crista_alvo, kg_max=4.5):
    """Sustentação: empurra no clip de joelho até a crista (pico / RMS dos 50 ms) chegar na do CS2 (~7,7 dB)."""
    x = norm(x); lo, hi = 1.0, kg_max
    if crista(kclip(x * hi)) > crista_alvo: return kclip(x * hi)
    for _ in range(12):
        m = (lo + hi) / 2
        if crista(kclip(x * m)) > crista_alvo: lo = m
        else: hi = m
    return kclip(x * hi)


def trata(x, forca=0.75, brilho=0.12, crista_alvo=8.0, alvo=-15.0, lim=9.0):
    import seca
    x = no_pico(x).copy(); x[:96] *= np.linspace(0, 1, 96) ** 2
    x, g = eq_ref(norm(x), forca, lim)
    x = ar(x, brilho)
    x = motor.hp(x, 28, 2)
    x = densa(x, crista_alvo)                     # o corpo sobe pro teto sem limitador com soltura
    seca.ATAQUE = 0.10                            # AK: o corpo de 60-120 ms é peso, não eco (CS2: -5 dB ali); seca só depois
    x, _tau = seca.ajusta(x, 0.12, alvo)
    return motor.acabamento(x, -0.3, 20)


# (nome, corpo(), parâmetros)
CAND = [
    ('AK atual do jogo (referência)', None, {}),
    ('tomada 14: a mais cheia e densa', lambda: ia(14), {}),
    ('tomada 4: soco em 150-300 Hz, estalo limpo', lambda: ia(4), {}),
    ('tomada 7 com o grave da tomada 10 por baixo', lambda: junta(norm(ia(7)), 0.7 * norm(motor.lp(ia(10), 170, 4))), dict(forca=0.85, brilho=0.03)),
    ('tomada 7: seca e agressiva no médio', lambda: ia(7), dict(forca=0.85)),
    ('tomada 15: estalo forte, grave firme', lambda: ia(15), {}),
    ('tomada 14 com o grave da tomada 10 por baixo (mais peso)', lambda: junta(norm(ia(14)), 0.6 * norm(motor.lp(ia(10), 170, 4))), {}),
    ('tomadas 4 + 14 juntas (soco de uma, densidade da outra)', lambda: junta(norm(ia(4)), 0.8 * norm(ia(14))), {}),
    ('tomada 14 mais brilhante e mais clipada (cara de jogo)', lambda: ia(14), dict(brilho=0.18, crista_alvo=6.5, forca=0.9)),
]


def atuais(): return [sf.read(f)[0] for f in sorted(glob.glob(os.path.join(S, 'ak', 'tiro_[0-9].wav')))]


def faz(k):
    nome, fc, p = CAND[k]
    if fc is None: return atuais()[0]
    return trata(fc(), **p)


def nivela(x, alvo_rms):
    """Mesma força percebida da AK atual (RMS de 50 ms), sem passar do pico."""
    from medir import medir_arr
    d = alvo_rms - medir_arr(x, SR)['rms50_db']
    return x * 10 ** (min(d, 0) / 20)


def rajada(t):
    dur = 1.2 + 0.5 + 2.0; y = np.zeros(int(dur * SR))
    motor.coloca(y, t[0], 0.0, 0.9)
    for i in range(5): motor.coloca(y, t[i % len(t)], 1.2 + i * 0.1, 0.9)
    return y


def linha(nome, x):
    from tonal import tonalidade
    from eco_tiro import medir as eco
    from medir import medir_arr
    b = faixas(x); b -= b.max(); c = eco(x)
    return (f'{nome[:24]:24s} ' + ' '.join(f'{v:5.1f}' for v in b) + ' | ' + ' '.join(f'{v:5.1f}' for v in tempo(x)) +
            f' | tom {tonalidade(x, SR):4.1f} crista {crista(x):4.1f} cauda {c[0]:5.1f} t40 {c[1]:.2f} rms {medir_arr(x, SR)["rms50_db"]:5.1f}')


def opcoes():
    from medir import medir_arr
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'akf_*.wav')): os.remove(f)
    print(' ' * 25 + ' '.join(f'{lo:5d}' for lo, _ in FX) + ' | ' + ' '.join(f'{a:5d}' for a, _ in TX))
    print(linha('CS2 ak47_01', carregar(REF[0])), flush=True)
    alvo = medir_arr(atuais()[0], SR)['rms50_db'] + 1.5
    for k, (nome, fc, _p) in enumerate(CAND):
        x = faz(k) if fc is None else nivela(faz(k), alvo)
        t = atuais() if fc is None else [variacao(x, i) for i in range(4)]
        motor.salvar(os.path.join(OP, f'akf_s_{k}.wav'), x); motor.salvar(os.path.join(OP, f'akf_r_{k}.wav'), rajada(t))
        print(linha(f'{k + 1} {nome}', x), flush=True)


def usar(k):
    import hibrido
    from medir import medir_arr
    os.makedirs(ANTES, exist_ok=True)
    for f in glob.glob(os.path.join(S, 'ak', 'tiro_*.wav')) + glob.glob(os.path.join(S, 'ak', 'distante_*.wav')):
        if not os.path.exists(os.path.join(ANTES, os.path.basename(f))): shutil.copy2(f, ANTES)
    alvo = medir_arr(sf.read(os.path.join(ANTES, 'tiro_0.wav'))[0], SR)['rms50_db'] + 1.5
    x = nivela(faz(k), alvo)
    for i in range(4):
        v = variacao(x, i); motor.salvar(os.path.join(S, 'ak', f'tiro_{i}.wav'), v)
        motor.salvar(os.path.join(S, 'ak', f'distante_{i}.wav'), hibrido.distante_hibrido('ak', v, 100 + i))
    print('AK =', k + 1, CAND[k][0], '(antigos em saida/_opcoes/ak_antes)', flush=True)


# ---------------- rodada 2 (Gabriel: "quero uma AK pra jogo baseada na AK do CS, e que seja som de tiro mesmo") --------
# A rodada 1 só casava 9 faixas dos primeiros 120 ms e clipava: o timbre médio batia, mas a FORMA do tiro (estouro,
# corpo, cauda em cada faixa) continuava a da IA. Aqui a gravação nossa é moldada pelo envelope tempo x frequência da
# AK do CS2 (terços de oitava, ~5 ms): a textura/ruído é da nossa tomada, a forma é a régua. Sem clip, sem excitador.
from scipy.signal import stft, istft
PRE = int(0.005 * SR)


def alinha(x, n):
    pk = int(np.argmax(np.abs(x))); y = x[max(0, pk - PRE):]
    if pk < PRE: y = np.concatenate([np.zeros(PRE - pk), y])
    y = y[:n]; return np.pad(y, (0, n - len(y)))


def _molda(s, r, win, forca, gmax):
    hop = win // 8
    f, _t, S = stft(s, SR, nperseg=win, noverlap=win - hop); _f, _t, R = stft(r, SR, nperseg=win, noverlap=win - hop)
    bordas = [0.0]; fc = 40.0
    while fc < SR / 2: bordas.append(fc); fc *= 2 ** (1 / 3)
    bordas.append(SR / 2 + 1)
    cent, G = [], []
    k = np.ones(3) / 3
    for lo, hi in zip(bordas[:-1], bordas[1:]):
        m = (f >= lo) & (f < hi)
        if not m.any(): continue
        es = np.convolve((np.abs(S[m]) ** 2).sum(0), k, 'same') + 1e-14; er = np.convolve((np.abs(R[m]) ** 2).sum(0), k, 'same') + 1e-14
        g = np.clip(10 * np.log10(er / es) * forca, -30, gmax)
        cent.append(max(f[m].mean(), 1.0)); G.append(g)
    G = np.array(G); lc = np.log(cent); lf = np.log(np.maximum(f, 1.0))
    Gb = np.array([np.interp(lf, lc, G[:, j]) for j in range(G.shape[1])]).T
    return istft(S * 10 ** (Gb / 20), SR, nperseg=win, noverlap=win - hop)[1][:len(s)]


def molda(src, ref, forca=1.0, gmax=15.0, gmax_grave=None):
    """Grave (< 350 Hz) com janela longa (resolução em frequência), resto com janela curta (o estouro não borra)."""
    n = len(ref); s = alinha(src, n) / (np.abs(src).max() + 1e-12)
    lo = _molda(s, ref, 2048, forca, gmax_grave or gmax); hi = _molda(s, ref, 256, forca, gmax)
    y = motor.lp(motor.lp(lo, 350, 2), 350, 2) + motor.hp(motor.hp(hi, 350, 2), 350, 2)
    return y


def cauda_cs(y, plena, tau, fim):
    t = np.arange(len(y)) / SR; g = np.where(t < plena, 1.0, np.exp(-(t - plena) / tau)); y = (y * g)[:int(fim * SR)].copy()
    f = int(0.03 * SR); y[-f:] *= np.linspace(1, 0, f); return y


def ref_cs(): return alinha(carregar(REF[0]), int(1.6 * SR))


def mist(*ns):
    L = int(1.6 * SR); return sum(alinha(norm(ia(n)), L) for n in ns)


CURTA = (0.20, 0.09, 0.55); MEDIA = (0.30, 0.18, 0.9); INTEIRA = (1.2, 0.2, 1.6)
CAND2 = [
    ('AK atual do jogo (referência)', None, None, None),
    ('tomada 14 na forma da AK do CS, cauda curta', lambda: ia(14), 1.0, CURTA),
    ('tomada 4 na forma da AK do CS, cauda curta', lambda: ia(4), 1.0, CURTA),
    ('tomada 15 na forma da AK do CS, cauda curta', lambda: ia(15), 1.0, CURTA),
    ('três tomadas juntas (4+14+15) na forma do CS, cauda curta', lambda: mist(4, 14, 15), 1.0, CURTA),
    ('três tomadas juntas, cauda média', lambda: mist(4, 14, 15, 13), 1.0, MEDIA),
    ('tomadas longas (13+15+10), cauda inteira como no CS', lambda: mist(13, 15, 10), 1.0, INTEIRA),
    ('três tomadas juntas, meio caminho (70% CS, 30% a gravação)', lambda: mist(4, 14, 15), 0.7, CURTA),
]


def faz2(k):
    nome, fc, forca, cd = CAND2[k]
    if fc is None: return atuais()[0]
    y = molda(fc(), ref_cs(), forca)
    y = cauda_cs(y, *cd)
    i0 = max(0, int(np.argmax(np.abs(y))) - 96); y = y[i0:].copy(); y[:48] *= np.linspace(0, 1, 48)
    y = densa(motor.hp(y, 28, 2), 8.0, 3.0)        # densidade do CS (crista ~7,7 dB): nada de nivelar pela AK antiga
    return motor.acabamento(y, -0.3, 20)


def opcoes2():
    from medir import medir_arr
    for f in glob.glob(os.path.join(OP, 'akf_*.wav')): os.remove(f)
    print(' ' * 25 + ' '.join(f'{lo:5d}' for lo, _ in FX) + ' | ' + ' '.join(f'{a:5d}' for a, _ in TX))
    print(linha('CS2 ak47_01', carregar(REF[0])), flush=True)
    alvo = medir_arr(atuais()[0], SR)['rms50_db'] + 2.5
    for k, (nome, fc, *_r) in enumerate(CAND2):
        x = faz2(k)
        t = atuais() if fc is None else [variacao(x, i) for i in range(4)]
        motor.salvar(os.path.join(OP, f'akf_s_{k}.wav'), x); motor.salvar(os.path.join(OP, f'akf_r_{k}.wav'), rajada(t))
        print(linha(f'{k + 1} {nome}', x), flush=True)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    if cmd == 'gera': gera()
    elif cmd == 'opcoes': opcoes()
    elif cmd == 'opcoes2': opcoes2()
    elif cmd == 'usar': usar(int(sys.argv[2]) - 1)
    sys.stdout.flush(); os._exit(0)
