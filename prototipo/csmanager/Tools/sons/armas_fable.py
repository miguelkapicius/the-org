# Tiro de TODAS as armas no conceito da AK nº 6 (Gabriel, 2026-11-11: "quero a 6, muda o som de tiro de todas as armas
# usando o conceito que você usou nessa"). Conceito (ak_fable.py, rodada 2): várias tomadas novas da IA somadas (textura
# de tiro densa), moldadas pelo envelope tempo x frequência do tiro da MESMA arma no CS2 (terços de oitava, ~5 ms; só
# régua, o áudio do CS2 não entra), cauda média, densidade (crista) da referência. Sem camada sintética.
# Uso: python -u armas_fable.py gera          -> eleven/bruto/<arma>/tiro_g0-3.mp3 (cache)
#      python -u armas_fable.py opcoes        -> saida/_opcoes/fab_<arma>_s.wav (tiro) e fab_<arma>_r.wav (sequência) + medidas
#      python -u armas_fable.py usar a,b|todas -> saida/<arma>/tiro_0-3 e distante_0-3 (antigos em saida/_opcoes/<arma>_antes_fable)
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor, eleven, regua, hibrido
from motor import SR
from hibrido import carregar, aparar_tiro, aceita_tiro, variacao
import ak_fable as A

AQUI = os.path.dirname(os.path.abspath(__file__))
S = A.S; OP = A.OP; BR = os.path.join(AQUI, 'eleven', 'bruto')
ARMAS = [a for a in regua.MAPA]
P = ['one loud {arma} gunshot for a competitive tactical shooter, {car}, sharp bright transient, dry, one shot only',
     'single {arma} shot, first person shooter weapon sound effect, AAA production quality, {car}, crisp and detailed, tight and short, one shot only']
DURS = [1.2, 0.8]
L = int(1.6 * SR)
CAUDA = (0.30, 0.18, 0.9)
# sequência de demonstração: (intervalo s, quantos)
SEQ = {'ak': (0.1, 5), 'm4': (0.09, 5), 'm4s': (0.1, 5), 'galil': (0.09, 5), 'famas': (0.09, 5), 'mp9': (0.07, 6), 'mac10': (0.075, 6),
       'awp': (1.3, 2), 'deagle': (0.35, 3)}


# regua.MAPA aponta a m4s pra m4a1_us_* = UNsilenced (o tiro SEM silenciador). O silenciado do CS2 é m4a1_silencer_01.
REF_CERTA = {'m4s': 'm4a1/m4a1_silencer_01.wav'}


def arq_ref(a): return sorted(glob.glob(os.path.join(regua.REF, REF_CERTA.get(a, regua.MAPA[a][0]))))[0]


def gera():
    from concurrent.futures import ThreadPoolExecutor
    k = eleven.chave(); eleven.URL = eleven.URL.replace('mp3_44100_128', 'mp3_44100_192'); fila = []
    for a in ARMAS:
        if a == 'ak': continue
        arma, car = eleven.ARMAS[a]
        for i, p in enumerate(P):
            for c, dur in enumerate(DURS):
                d = os.path.join(BR, a, f'tiro_g{i * 2 + c}.mp3')
                if not os.path.exists(d): fila.append((p.format(arma=arma, car=car), dur, d))
    gasto = [0]
    def um(it):
        if eleven.gerar_um(k, it[0], it[1], False, it[2]): gasto[0] += round(it[1] * eleven.CRED_SEG)
        else: print('FALHOU', it[2], flush=True)
    with ThreadPoolExecutor(4) as ex: list(ex.map(um, fila))
    print(len(fila), 'pedidos, gasto ~', gasto[0], 'créditos', flush=True)


def ref(a): return A.alinha(carregar(arq_ref(a)), L)


def tomadas(a):
    xs = [aparar_tiro(carregar(f)) for f in sorted(glob.glob(os.path.join(BR, a, 'tiro_g*.mp3')))]
    boas = [x for x in xs if aceita_tiro(x)[0]]
    return (boas if len(boas) >= 2 else xs), len(boas), len(xs)


# Metálico no fim (Gabriel, 2026-11-11: "M4, Glock... som metálico no final"): a tomada da IA traz clack de ferrolho e
# resíduo depois do estouro, e a moldagem AUMENTA esse resto pra preencher a cauda do CS. Agora a tomada só vale no
# estouro (até ~60 ms, some até 130 ms) e a cauda nasce do próprio estouro espalhado em ruído difuso (sem nada tonal);
# a moldagem dá a ela a forma da cauda do CS. Medida: tom_cauda (pico do espectro sobre a vizinhança, 150-700 ms).
RUIDO = {}                                   # silenciado é um sopro de ruído: mais ruído liso no estouro tira o tom de 'laser' da IA
LARGO = {'awp': 3.6}                       # s de referência usados (AWP do CS: cauda de ~3,8 s)
CAUDAS = {'awp': (2.2, 0.5, 3.5)}


def tom_cauda(x):
    from scipy.signal import stft
    pk = int(np.argmax(np.abs(x))); c = x[pk + int(0.15 * SR): pk + int(0.7 * SR)]
    if len(c) < 4096: return 0.0
    f, _t, Z = stft(c, SR, nperseg=2048, noverlap=1536); Pw = (np.abs(Z) ** 2).mean(1); m = (f > 300) & (f < 9000); Pw = Pw[m]
    viz = np.array([np.median(Pw[max(0, i - 25): i + 25]) for i in range(len(Pw))])
    return float(10 * np.log10((Pw / (viz + 1e-20)).max()))


SEM_TOM = {'m4s'}      # armas em que as ressonâncias estreitas da tomada são aplainadas antes de moldar


def sem_tom(x, folga_db=3.0):
    """Aplaina picos estreitos do espectro (ressonância/apito da IA): onde uma frequência passa da mediana da vizinhança
    por mais de folga_db, desce até a folga. O formato largo do espectro não muda."""
    X = np.fft.rfft(x); Pw = np.abs(X) ** 2; k = max(8, len(Pw) // 400)
    from scipy.ndimage import median_filter, uniform_filter1d
    viz = median_filter(uniform_filter1d(Pw, 5), size=2 * k + 1, mode='nearest') + 1e-20
    exc = 10 * np.log10(uniform_filter1d(Pw, 5) / viz + 1e-20) - folga_db
    return np.fft.irfft(X * 10 ** (-np.maximum(exc, 0) / 20), len(x))


def fonte(xs, n, seed, nivel_ruido=0.15):
    """Estouro = as tomadas (até ~60 ms, somem até 130 ms). Cauda e o que faltar em alguma faixa = ruído LISO (branco,
    sem ressonância): espalhar o estouro levava junto as ressonâncias estreitas da IA, que é o que soa metálico."""
    d = sum(A.alinha(A.norm(x), n) for x in xs); t = np.arange(n) / SR
    jan = np.where(t < 0.06, 1.0, np.where(t < 0.13, 0.5 + 0.5 * np.cos(np.pi * (t - 0.06) / 0.07), 0.0))   # o clack de ferrolho da IA vem depois de ~80 ms
    R = np.fft.rfft(np.random.default_rng(seed).standard_normal(n)); f = np.fft.rfftfreq(n, 1 / SR)
    ru = np.fft.irfft(R / np.sqrt(np.maximum(f, 25.0)), n); ru[:A.PRE] = 0      # rosa: tem grave pra moldagem achar o sub
    return d * jan + ru / np.abs(ru).max() * nivel_ruido * np.abs(d).max()


def faz(a):
    if a == 'ak': return A.faz2(5), '4 de 4'
    n = int(LARGO.get(a, 1.6) * SR)
    xs, nb, nt = tomadas(a)
    if a in SEM_TOM: xs = [sem_tom(x) for x in xs]
    r = A.alinha(carregar(arq_ref(a)), n)
    y = A.molda(fonte(xs, n, 7 + len(a), RUIDO.get(a, 0.15)), r, 1.0, 18.0, 27.0)
    y = A.cauda_cs(y, *CAUDAS.get(a, CAUDA))
    i0 = max(0, int(np.argmax(np.abs(y))) - 96); y = y[i0:].copy(); y[:48] *= np.linspace(0, 1, 48)
    y = A.densa(motor.hp(y, 28, 2), A.crista(r) + 0.3, 3.0)
    return motor.acabamento(y, -0.3, 20), f'{nb} de {nt}'


# ---------------- camada DISTANTE (Gabriel, 2026-11-11: "não adianta ficar de perto bom e de longe horrível") -----------
# O distante antigo (hibrido.distante_hibrido) era OUTRO som: estouro abafado + eco sintético do casario. Agora é o
# MESMO tiro novo ouvido de mais longe: menos agudo (ar), e uma cauda difusa curta de ruído liso com o timbre largo do
# próprio tiro (sem repique de eco, sem ressonância). Nada de moldar pelo "distant" do CS2: no jogo isométrico o eco do
# CS soa errado. Só toca fora da tela (Som3D: C_DISTANTE começa em 800 cs e NaTela trava em 450).
def longe(x, seed, corte=4500.0, cauda_db=-11.0, tau=0.28):
    from scipy.ndimage import uniform_filter1d
    n = max(len(x), int(1.5 * SR)); y = np.zeros(n); y[:len(x)] = motor.lp(x, corte, 2)
    X = np.abs(np.fft.rfft(y)); f = np.fft.rfftfreq(n, 1 / SR)
    lf = np.log(np.maximum(f, 20.0)); grade = np.linspace(lf[1], lf[-1], 400)
    liso = np.interp(lf, grade, uniform_filter1d(np.interp(grade, lf, X), 25))          # timbre largo, sem picos estreitos
    R = np.fft.rfft(np.random.default_rng(seed).standard_normal(n))
    ru = np.fft.irfft(R * liso, n); t = np.arange(n) / SR; pre = 0.035
    env = np.where(t < pre, 0.0, np.exp(-(t - pre) / tau)) * np.clip((t - pre) / 0.02, 0, 1)
    ru = ru * env; ru *= 10 ** (cauda_db / 20) * np.abs(y).max() / (np.abs(ru).max() + 1e-12)
    return motor.acabamento(y + ru, -1.0, 120)


def antigos(a):
    d = os.path.join(OP, f'{a}_antes_fable')
    fs = sorted(glob.glob(os.path.join(d, 'tiro_[0-9].wav'))) or sorted(glob.glob(os.path.join(S, a, 'tiro_[0-9].wav')))
    return [sf.read(f)[0] for f in fs]


def seq(a, t):
    dt, n = SEQ.get(a, (0.22, 3)); cab = 1.0 if a != 'awp' else 0.0
    y = np.zeros(int((cab + dt * n + 1.0) * SR) + max(len(v) for v in t))
    if cab: motor.coloca(y, t[0], 0.0, 0.9)
    for i in range(n): motor.coloca(y, t[i % len(t)], cab + i * dt, 0.9)
    return y


def opcoes(lista):
    os.makedirs(OP, exist_ok=True)
    print(' ' * 21 + ' '.join(f'{lo:5d}' for lo, _ in A.FX) + ' | ' + ' '.join(f'{a_:5d}' for a_, _ in A.TX), flush=True)
    for a in lista:
        velha = os.path.join(OP, f'fab_{a}_s.wav'); tv = tom_cauda(sf.read(velha)[0]) if os.path.exists(velha) else 0
        x, nb = faz(a); ant = antigos(a)
        motor.salvar(os.path.join(OP, f'fab_{a}_s.wav'), x); motor.salvar(os.path.join(OP, f'fab_{a}_r.wav'), seq(a, [variacao(x, i) for i in range(4)]))
        motor.salvar(os.path.join(OP, f'fab_{a}_as.wav'), ant[0]); motor.salvar(os.path.join(OP, f'fab_{a}_ar.wav'), seq(a, ant))
        print(A.linha(f'{a} CS2', ref(a))); print(A.linha(f'{a} atual', ant[0])); print(A.linha(f'{a} NOVA ({nb})', x)); print(f'   tom da cauda: CS2 {tom_cauda(ref(a)):.1f}  rodada anterior {tv:.1f}  agora {tom_cauda(x):.1f} dB', flush=True)


def usar(lista):
    for a in lista:
        d = os.path.join(OP, f'{a}_antes_fable'); os.makedirs(d, exist_ok=True)
        for f in glob.glob(os.path.join(S, a, 'tiro_*.wav')) + glob.glob(os.path.join(S, a, 'distante_*.wav')):
            if not os.path.exists(os.path.join(d, os.path.basename(f))): shutil.copy2(f, d)
        x, _nb = faz(a); tem_dist = bool(glob.glob(os.path.join(d, 'distante_*.wav')))
        for i in range(4):
            v = variacao(x, i); motor.salvar(os.path.join(S, a, f'tiro_{i}.wav'), v)
            if tem_dist: motor.salvar(os.path.join(S, a, f'distante_{i}.wav'), longe(v, 300 + i))
        print('aplicada:', a, '(antigos em saida/_opcoes/%s_antes_fable)' % a, flush=True)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    lista = ARMAS if len(sys.argv) < 3 or sys.argv[2] == 'todas' else sys.argv[2].split(',')
    if cmd == 'gera': gera()
    elif cmd == 'opcoes': opcoes(lista)
    elif cmd == 'usar': usar(lista)
    sys.stdout.flush(); os._exit(0)
