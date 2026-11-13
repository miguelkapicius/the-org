# HS, capacete, corpo, colete e o final da C4 (Gabriel, 2026-11-11: "fazer um som de HS decente", "diferença de quando o
# cara tem capacete: o capacete quebrando, depois a cabeça", "tiro batendo no corpo, corpo com colete", "muda a C4,
# principalmente antes dela explodir"). Mesmo conceito dos tiros (ak_fable.molda): tomadas novas da IA (foley de cinema)
# moldadas pelo envelope tempo x frequência do som correspondente do CS2 (só régua).
# Medido: o HS antigo era um baque grave (tudo < 400 Hz); o do CS2 é um estalo brilhante (400 Hz - 8 kHz). Capacete no
# CS2 = duas peças: headshot_armor_e1 (capacete, médio, 0,4 s) + headshot_armor_flesh. Colete = kevlar_0N + carne 50 ms
# depois (mud_impact_bullet). C4: o aviso antes de explodir no CS2 (ui/arm_bomb) é um alarme pulsado em 2,06 kHz,
# ~10 pulsos/s; o nosso final era um apito contínuo de 4,2 kHz.
# Uso: python -u impactos_fable.py gera    -> eleven/bruto/impacto/<tipo>_f*.mp3
#      python -u impactos_fable.py opcoes  -> saida/_opcoes/imp_<tipo>_K.wav, imp_cena_*.wav, c4f_K.wav, c4f_cena_K.wav
#      python -u impactos_fable.py usar [c4=K] -> saida/impacto/{cabeca,capacete,corpo,colete}_0-2 (+ c4/final_0 se c4=K)
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor, eleven
from motor import SR
from hibrido import carregar, aparar
import ak_fable as A

AQUI = os.path.dirname(os.path.abspath(__file__))
S = A.S; OP = A.OP; BR = os.path.join(AQUI, 'eleven', 'bruto', 'impacto'); R = 'D:/unity/Tools/ref_sons_cs2/sounds'
ANTES = os.path.join(OP, 'impacto_antes_fable')
PROMPTS = {
    'hs': (0.6, ['close-up foley for a film: a watermelon smashed by one hard hit, sharp crack at the start then a wet juicy burst, very short',
                 'foley: a head of cabbage split by a cleaver in one hit, crisp wet crack and splat, very short, dry studio',
                 'a bullet punching through a ripe melon, fast sharp snap then wet splatter, short, close microphone']),
    'capacete': (0.5, ['a bullet striking and cracking a hard polymer combat helmet, sharp hard plastic crack with a short metallic ping, very short, close microphone',
                       'a hard hat hit by a hammer and cracking, loud sharp knock and crack, very short, dry',
                       'a steel helmet struck hard by a small metal object, short bright clank, damped, no long ringing']),
    'dink': (0.5, ['a tiny high-pitched metallic ting, a small steel ball bearing dropped on a thin hard steel plate, bright thin ringing ping, very short',
                   'a coin flicked hard against a thin sheet of hardened steel, very high pitched sharp tink with a short bright ring',
                   'a bullet glancing off a steel helmet, one very high pitched thin ping, short ring, no low thud']),
    'corpo': (0.5, ['a bullet hitting a body, dull wet thud into flesh, short meaty slap, close, dry',
                    'foley: a thick steak slapped hard onto a table, wet meaty thwack, very short']),
    'colete': (0.5, ['a bullet stopped by a kevlar vest, dull hard thump on thick fabric with a short stiff slap, very short, dry',
                     'foley: a baseball bat hitting a heavy canvas punching bag, deep tight thud, very short']),
}
SECO = ', isolated sound effect, no music, no voice, no reverb'


def gera():
    from concurrent.futures import ThreadPoolExecutor
    k = eleven.chave(); eleven.URL = eleven.URL.replace('mp3_44100_128', 'mp3_44100_192'); fila = []
    for tipo, (dur, ps) in PROMPTS.items():
        for i, p in enumerate(ps):
            for c in range(2):
                d = os.path.join(BR, f'{tipo}_f{i * 2 + c}.mp3')
                if not os.path.exists(d): fila.append((p + SECO, dur, d))
    gasto = [0]
    def um(it):
        if eleven.gerar_um(k, it[0], it[1], False, it[2]): gasto[0] += round(it[1] * eleven.CRED_SEG)
        else: print('FALHOU', it[2], flush=True)
    with ThreadPoolExecutor(4) as ex: list(ex.map(um, fila))
    print(len(fila), 'pedidos, gasto ~', gasto[0], 'créditos', flush=True)


def inicio(x, n, piso=-28):
    """Alinha pelo COMEÇO do som (impacto tem pré-ataque que conta), não pelo pico."""
    e = np.sqrt(np.convolve(x ** 2, np.ones(96) / 96, 'same')) + 1e-12; db = 20 * np.log10(e / e.max())
    i0 = max(0, int(np.argmax(db > piso)) - A.PRE); y = x[i0:i0 + n]
    return np.pad(y, (0, n - len(y)))


def tomadas(tipo, quais=None):
    fs = sorted(glob.glob(os.path.join(BR, f'{tipo}_f*.mp3')), key=lambda s: int(s.rsplit('_f', 1)[1][:-4]))
    if quais is not None: fs = [fs[i] for i in quais if i < len(fs)]
    return [carregar(f) for f in fs]


def molda_imp(xs, ref_arq, dur, seed, ruido=0.04, pico_db=-1.5):
    n = int(dur * SR); r = inicio(carregar(os.path.join(R, ref_arq)), n)
    d = sum(inicio(A.norm(x), n) for x in xs)
    Rz = np.fft.rfft(np.random.default_rng(seed).standard_normal(n)); f = np.fft.rfftfreq(n, 1 / SR)
    ru = np.fft.irfft(Rz / np.sqrt(np.maximum(f, 25.0)), n)
    src = d + ru / np.abs(ru).max() * ruido * np.abs(d).max()
    # molda() alinha pelo pico; aqui os dois já estão alinhados pelo começo, então chama as duas bandas direto
    s = src / (np.abs(src).max() + 1e-12)
    lo = A._molda(s, r, 2048, 1.0, 18.0); hi = A._molda(s, r, 256, 1.0, 15.0)
    y = motor.lp(motor.lp(lo, 350, 2), 350, 2) + motor.hp(motor.hp(hi, 350, 2), 350, 2)
    y = aparar(motor.hp(y, 30, 2), -45, 3)
    return motor.acabamento(A.kclip(A.norm(y) * 1.3), pico_db, 25)


def soma(*partes):
    L = max(int(t * SR) + len(x) for t, x, _g in partes); y = np.zeros(L)
    for t, x, g in partes: motor.coloca(y, x, t, g)
    return y


REF_HS = ['player/headshot_noarmor_02.wav', 'player/headshot_noarmor_03.wav', 'player/headshot_armor_flesh.wav']
REF_CORPO = ['physics/surfaces/mud_impact_bullet1.wav', 'physics/surfaces/mud_impact_bullet3.wav', 'physics/surfaces/mud_impact_bullet4.wav']
REF_COLETE = ['player/kevlar_01.wav', 'player/kevlar_03.wav', 'player/kevlar_04.wav']
SUB = [[0, 1, 2, 3, 4, 5], [0, 2, 4, 5], [1, 3, 4, 5]]      # cada variação soma um conjunto de tomadas (textura diferente)


def cabeca(k):
    """HS refinado (Gabriel, 2026-11-11: "só refina o HS que já tinha feito"): mesma base, com (1) ataque imediato (a
    1ª versão levava ~10 ms pra subir; o do CS2 já chega em cheio), (2) mais densidade, como o CS2 (RMS -6 contra -9/-10),
    (3) só referências de estalo molhado (02, 03 e a carne), sem a noarmor_04, que começa com uma batida tonal em 750 Hz."""
    y = molda_imp(tomadas('hs', SUB[k]), REF_HS[k], 0.7, 40 + k, pico_db=-1.0)
    e = np.sqrt(np.convolve(y ** 2, np.ones(48) / 48, 'same')); i0 = max(0, int(np.argmax(e > 0.35 * e.max())) - 72)
    y = y[i0:].copy(); y[:24] *= np.linspace(0, 1, 24)
    return motor.acabamento(A.densa(y, 9.0, 2.5), -1.0, 25)


# Capacete em TRÊS TEMPOS (Gabriel: "parece panela na cabeça"; no CS "dá pra ouvir a bala batendo no capacete, o
# capacete abrindo e pegando na cabeça"). Raio-x do CS2: headshot_armor_01 = toque fino em 9,3 kHz (+7,0 kHz) soando
# ~0,5 s (a bala no capacete); headshot_armor_e1 = pancada média só nos primeiros 70 ms e estalo até 150 ms (o capacete
# abrindo); depois a carne. As tomadas "capacete_f" soavam em 844 Hz por 200 ms (a panela): fora. O estalo usa só
# tomadas sem ressonância forte e ainda passa pelo aplainador.
def pico_tonal(x):
    pk = int(np.argmax(np.abs(x))); a = x[pk:pk + int(0.3 * SR)]; a = np.pad(a, (0, int(0.3 * SR) - len(a)))
    X = np.abs(np.fft.rfft(a * np.hanning(len(a)), 1 << 16)) ** 2; fr = np.fft.rfftfreq(1 << 16, 1 / SR)
    viz = np.array([np.median(X[max(0, i - 200):i + 200:8]) for i in range(0, len(X), 16)]); r = 10 * np.log10(X[::16] / (viz + 1e-20)); f16 = fr[::16]
    m = (f16 > 2000) & (f16 < 13000); j = int(np.argmax(np.where(m, r, -99)))
    return float(f16[j]), float(r[j])


def dink(k, alvo=9300.0):
    """O toque da bala no capacete: a tomada que mais 'canta', subida de tom (reamostragem) até ~9,3 kHz, só o agudo."""
    from scipy.signal import resample
    cands = sorted(((pico_tonal(x)[1] + (20 if pico_tonal(x)[0] > 5000 else 0), i, x) for i, x in enumerate(tomadas('dink'))), key=lambda c: -c[0])   # prefere quem já canta no agudo
    _r, _i, x = cands[k % min(3, len(cands))]; f0, _ = pico_tonal(x)
    pk = int(np.argmax(np.abs(x))); x = x[max(0, pk - 48):pk + int(0.5 * SR)]
    y = resample(x, max(64, int(len(x) * f0 / alvo)))             # mais curto = mais agudo
    y = motor.hp(motor.hp(y, 5500, 2), 5500, 2); t = np.arange(len(y)) / SR
    y = y * np.exp(-t / 0.09); y[:24] *= np.linspace(0, 1, 24)
    return A.norm(y)


FONTES_ESTALO = ['capacete_f0', 'capacete_quebra_c0', 'capacete_quebra_c1', 'capacete_quebra_c2', 'capacete_quebra_c3', 'capacete_quebra_c7', 'capacete_quebra_c11']


def estalo_capacete(k):
    import armas_fable as F
    fs = [FONTES_ESTALO[i] for i in ([0, 1, 2, 3, 4, 5, 6], [0, 2, 4, 6], [1, 3, 5, 6])[k]]
    xs = [F.sem_tom(carregar(os.path.join(BR, f + '.mp3'))) for f in fs]
    y = molda_imp(xs, 'player/headshot_armor_e1.wav', 0.45, 50 + k, ruido=0.08)
    t = np.arange(len(y)) / SR; return y * np.where(t < 0.16, 1.0, np.exp(-(t - 0.16) / 0.05))     # sem o resto grave comprido


def capacete(k):
    """Capacete DESISTIDO de novo (2026-11-11: três tempos = "uma merda", 12 candidatos crus = "horrível"): igual à cabeça."""
    return cabeca(k)


def corpo(k):
    """Baque de carne ESCURO e curto: o chiado molhado depois dos 70 ms deixava o corpo parecido com o HS."""
    y = molda_imp(tomadas('corpo', [[0, 1, 2, 3], [0, 2, 3], [1, 2, 3]][k]), REF_CORPO[k], 0.5, 60 + k, pico_db=-2.0)
    y = motor.lp(y, 2600, 2); t = np.arange(len(y)) / SR; y = (y * np.where(t < 0.09, 1.0, np.exp(-(t - 0.09) / 0.035)))[:int(0.26 * SR)]
    return motor.acabamento(y, -2.0, 20)


def colete(k):
    """O colete segurando (forma do kevlar do CS2) e a carne 50 ms depois, mais baixa (como o CS2 monta)."""
    kv = molda_imp(tomadas('colete', [[0, 1, 2, 3], [0, 2, 3], [1, 2, 3]][k]), REF_COLETE[k], 0.32, 70 + k)
    return motor.acabamento(soma((0.0, kv, 1.0), (0.05, corpo(k), 0.35)), -2.0, 25)


TIPOS = {'cabeca': cabeca, 'capacete': capacete, 'corpo': corpo, 'colete': colete}


def antigos(tipo):
    fs = sorted(glob.glob(os.path.join(ANTES, f'{tipo}_[0-9].wav'))) or sorted(glob.glob(os.path.join(S, 'impacto', f'{tipo}_[0-9].wav')))
    return [sf.read(f)[0] for f in fs]


def tres(xs, dt=0.7):
    y = np.zeros(int((dt * len(xs) + 0.6) * SR))
    for i, x in enumerate(xs): motor.coloca(y, x, i * dt, 1.0)
    return y


def cena(xs, vol):
    """Como no jogo: tiro de AK e o acerto 60 ms depois (volume do Som3D.Impacto: cabeça 0,9, corpo 0,6)."""
    ak = [sf.read(os.path.join(S, 'ak', f'tiro_{i}.wav'))[0] for i in range(3)]
    y = np.zeros(int(3.6 * SR))
    for i in range(3): motor.coloca(y, ak[i], 0.2 + i * 1.1, 0.55); motor.coloca(y, xs[i % len(xs)], 0.26 + i * 1.1, vol)
    return y / max(1.0, np.abs(y).max())


# ---------------- C4: o último segundo ----------------
def tom(f, dur, harm=0.18):
    t = np.arange(int(dur * SR)) / SR; y = np.sin(2 * np.pi * f * t) + harm * np.sin(2 * np.pi * 3 * f * t)
    k = int(0.002 * SR); y[:k] *= np.linspace(0, 1, k); y[-k:] *= np.linspace(1, 0, k); return y


def pulsos(marcas, dur=1.0):
    """marcas = [(t, f, dur_pulso, ganho)]"""
    y = np.zeros(int(dur * SR))
    for t, f, d, g in marcas:
        if t + d <= dur: motor.coloca(y, tom(f, d), t, g)
    return y / (np.abs(y).max() + 1e-12) * 10 ** (-3 / 20)


def c4_final(k):
    if k == 0: return sf.read(os.path.join(S, 'c4', 'final_0.wav'))[0]
    if k == 1:   # alarme pulsado no jeito do CS2: 2,06 kHz, 10 pulsos por segundo, subindo de volume
        return pulsos([(i * 0.1, 2062, 0.05, 0.5 + 0.5 * i / 9) for i in range(10)])
    if k == 2:   # o mesmo bip de sempre (4,2 kHz), só que acelerando até o fim, sem virar apito contínuo
        m, t, i = [], 0.0, 0
        while t < 0.97: m.append((t, 4219, 0.035, 0.7 + 0.3 * t)); t += max(0.05, 0.16 - 0.012 * i); i += 1
        return pulsos(m)
    if k == 3:   # sirene de dois tons (2,06 e 2,6 kHz) alternando cada vez mais rápido
        m, t, i = [], 0.0, 0
        while t < 0.96: d = max(0.035, 0.07 - 0.003 * i); m.append((t, 2062 if i % 2 == 0 else 2600, d * 0.9, 0.6 + 0.4 * t)); t += d; i += 1
        return pulsos(m)
    if k == 4:   # alarme grave do CS2 com o bip agudo marcando por cima, de dois em dois
        m = [(i * 0.1, 2062, 0.05, 0.6 + 0.4 * i / 9) for i in range(10)] + [(i * 0.2, 4219, 0.03, 0.45) for i in range(5)]
        return pulsos(m)


C4_NOMES = ['atual: bips que viram apito contínuo (4,2 kHz)', 'alarme pulsado no jeito do CS2 (2 kHz, 10 por segundo)',
            'o bip de sempre acelerando até o fim, sem apito', 'sirene de dois tons cada vez mais rápida', 'alarme grave com o bip agudo por cima']


def c4_cena(final):
    """Últimos 3,5 s: bips urgentes no ritmo do Som3D, o final (1 s) e a explosão."""
    bip = sf.read(os.path.join(S, 'c4', 'bip_rapido_0.wav'))[0]; exp = sf.read(os.path.join(S, 'c4', 'explode_0.wav'))[0][:int(2.5 * SR)].copy()
    exp[-int(0.3 * SR):] *= np.linspace(1, 0, int(0.3 * SR))
    y = np.zeros(int(6.2 * SR)); rest = 3.5
    while rest > 1.0: motor.coloca(y, bip, 3.5 - rest, 0.5); rest -= max(0.15, 0.1 + 0.9 * rest / 40)
    motor.coloca(y, final, 2.5, 0.55); motor.coloca(y, exp, 3.5, 0.9)
    return y / max(1.0, np.abs(y).max())


def linha(nome, x):
    from medir import medir_arr
    m = medir_arr(x, SR); return f"{nome[:24]:24s} {m['dur']:5.2f}s rms{m['rms50_db']:6.1f} d20 {m['d20']:.3f} br{m['brilho']:5d} {m['bandas']}"


def opcoes():
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'imp_*.wav')) + glob.glob(os.path.join(OP, 'c4f_*.wav')): os.remove(f)
    for tipo, fn in TIPOS.items():
        novos = [fn(k) for k in range(3)]; ant = antigos(tipo); vol = 0.9 if tipo in ('cabeca', 'capacete') else 0.6
        motor.salvar(os.path.join(OP, f'imp_{tipo}_antes.wav'), tres(ant)); motor.salvar(os.path.join(OP, f'imp_{tipo}_novo.wav'), tres(novos))
        motor.salvar(os.path.join(OP, f'imp_cena_{tipo}_antes.wav'), cena(ant, vol)); motor.salvar(os.path.join(OP, f'imp_cena_{tipo}_novo.wav'), cena(novos, vol))
        print(linha(f'{tipo} atual', ant[0])); [print(linha(f'{tipo} novo {k}', x), flush=True) for k, x in enumerate(novos)]
    for k in range(5):
        f = c4_final(k); motor.salvar(os.path.join(OP, f'c4f_{k}.wav'), f); motor.salvar(os.path.join(OP, f'c4f_cena_{k}.wav'), c4_cena(f))
        print('c4 final', k + 1, C4_NOMES[k], flush=True)


def usar(c4=None, so=None):
    os.makedirs(ANTES, exist_ok=True)
    for tipo, fn in TIPOS.items():
        if so and tipo not in so: continue
        for f in glob.glob(os.path.join(S, 'impacto', f'{tipo}_[0-9].wav')):
            if not os.path.exists(os.path.join(ANTES, os.path.basename(f))): shutil.copy2(f, ANTES)
        for k in range(3): motor.salvar(os.path.join(S, 'impacto', f'{tipo}_{k}.wav'), fn(k))
        print('aplicado:', tipo, flush=True)
    if c4:
        d = os.path.join(OP, 'c4_antes_fable'); os.makedirs(d, exist_ok=True)
        if not os.path.exists(os.path.join(d, 'final_0.wav')): shutil.copy2(os.path.join(S, 'c4', 'final_0.wav'), d)
        motor.salvar(os.path.join(S, 'c4', 'final_0.wav'), c4_final(c4)); print('c4 final =', c4 + 1, C4_NOMES[c4], flush=True)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    if cmd == 'gera': gera()
    elif cmd == 'opcoes': opcoes()
    elif cmd == 'usar': usar(next((int(a[3:]) - 1 for a in sys.argv[2:] if a.startswith('c4=')), None), next((a[3:].split(',') for a in sys.argv[2:] if a.startswith('so=')), None))
    sys.stdout.flush(); os._exit(0)
