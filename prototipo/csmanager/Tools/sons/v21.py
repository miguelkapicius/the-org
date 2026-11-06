# v21 (2026-11-04): match point no molde do CS2.
#  Régua: Music.Match.MatchPoint = sounds/music/cs_stinger (extraído em D:/unity/Tools/ref_sons_cs2/ui, só referência):
#    sobe ~300 ms até o golpe e cai num ronco longo; 87% da energia < 1 kHz, ~1% > 4 kHz (o nosso nº 6 era metálico = agudo).
#  BAM: 3 sintéticos (inchaço reverso -> boom grave -> ronco escuro) + 4 da ElevenLabs escurecidos, todos ~1,8 s.
#  Voz: "Maaatch... (respira) POINT!" — A alongado, respiro entre as palavras, "point" com empolgação. Brian (locutor nº 3).
# Uso: python v21.py                -> _opcoes/bam2_K, mpfala_K (só a fala) e régua impressa
#      python v21.py usar BAM FALA  -> saida/hud/match_point_0 = fala + BAM
import os, sys, glob, json, urllib.request, urllib.error, numpy as np, soundfile as sf
import motor, hibrido, radio, voz, eleven
from motor import SR, Rng
from eleven import chave

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes')
REF = 'D:/unity/Tools/ref_sons_cs2/ui/sounds/music/cs_stinger.wav'
BANDAS = [(20, 80), (80, 250), (250, 1000), (1000, 4000), (4000, 12000)]
FALAS = [('eleven_multilingual_v2', 'Maaatch... point!', 0.3, 0.6), ('eleven_multilingual_v2', 'Maaaatch... POINT!', 0.25, 0.7),
         ('eleven_multilingual_v2', 'Maaatch — point!', 0.35, 0.5),
         ('eleven_v3', '[excited] Maaatch... [inhales] POINT!', 0.5, 0.0), ('eleven_v3', '[dramatic] Maaaatch... [breath] point!', 0.5, 0.0),
         ('eleven_v3', '[excited] Maaatch... point!', 0.5, 0.0)]
PROMPTS_BAM = ['deep dark cinematic boom with a short reverse swell rising into it, then a long low rumble tail, no metal, no high frequencies',
               'sub bass trailer hit: quick reverse whoosh into a huge low boom and dark rumble, warm, no clang']


def bandas(x):
    S_ = np.abs(np.fft.rfft(x)) ** 2; fr = np.fft.rfftfreq(len(x), 1 / SR); e = [S_[(fr >= a) & (fr < b)].sum() for a, b in BANDAS]
    return np.array(e) / sum(e)


def casa_bandas(x, bref, forca=0.85):
    """EQ por bandas puxando o balanço pro do CS2 (régua): divide em 5 bandas, ganha cada uma até a proporção de energia do alvo."""
    partes = [motor.lp(x, 80, 4)] + [motor.bp(x, a, b, 4) for a, b in BANDAS[1:-1]] + [motor.hp(x, 4000, 4)]
    e = np.array([(p ** 2).sum() for p in partes]) + 1e-12; e = e / e.sum()
    g = (bref / e) ** (0.5 * forca); g = np.clip(g, 0.1, 12)
    y = sum(p * gi for p, gi in zip(partes, g))
    return motor.acabamento(motor.compressor(y / np.abs(y).max(), -16, 3, 15.0), -0.5, 80)


def sintetico(k):
    r = Rng(500 + k); dur = 1.8; n = int(dur * SR); t = np.arange(n) / SR; hit = 0.3
    y = np.zeros(n)
    m = int(hit * SR); sw = motor.lp(r.ruido(m), 600 + 300 * k) * (np.arange(m) / m) ** 3     # inchaço reverso até o golpe
    y[:m] += sw * 0.5
    tb = t[m:] - hit; fr = 40 + 70 * np.exp(-tb / 0.08)                                     # boom caindo de ~110 pra 40 Hz
    boom = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tb / (0.5 + 0.2 * k))
    corpo = motor.bp(r.ruido(n - m), 80, 500) * np.exp(-tb / (0.35 + 0.15 * k))
    golpe = motor.lp(r.ruido(n - m), 2500) * np.exp(-tb / 0.03)
    y[m:] += boom * 1.0 + corpo * 0.6 + golpe * 0.35
    ir = motor.ir_ambiente(1.6, 1.4, r, escuro=1800)
    y = y + motor.convolve(motor.lp(y, 1500), ir)[:n] * 0.25 * np.sqrt((y ** 2).sum())
    y = motor.lp(y, 3500); f = int(0.4 * SR); y[-f:] *= np.linspace(1, 0, f) ** 1.5
    return motor.acabamento(motor.compressor(y / np.abs(y).max(), -16, 3, 15.0), -0.5, 80)


def da_ia(f):
    x = hibrido.aparar(hibrido.carregar(f))[:int(1.8 * SR)].copy()
    x = motor.lp(x, 3500, 4)                                           # tira o metal (o CS2 quase não tem nada > 4 kHz)
    fd = int(0.4 * SR); x[-fd:] *= np.linspace(1, 0, fd) ** 1.5
    return motor.acabamento(motor.compressor(x / np.abs(x).max(), -16, 3, 15.0), -0.5, 80)


def falar(modelo, texto, estab, estilo, destino):
    vid = voz.VOZES['brian'][0]
    url = f'https://api.elevenlabs.io/v1/text-to-speech/{vid}?output_format=mp3_44100_128'
    vs = dict(stability=estab, similarity_boost=0.8, use_speaker_boost=True)
    if modelo != 'eleven_v3': vs['style'] = estilo
    corpo = json.dumps(dict(text=texto, model_id=modelo, voice_settings=vs)).encode()
    req = urllib.request.Request(url, data=corpo, method='POST', headers={'Content-Type': 'application/json', 'xi-api-key': chave(), 'User-Agent': 'csmanager-sons/1.0'})
    try:
        with urllib.request.urlopen(req, timeout=90) as r: open(destino, 'wb').write(r.read()); return True
    except urllib.error.HTTPError as e:
        print('  ERRO voz', modelo, e.code, e.read()[:200].decode('utf-8', 'replace')); return False


def gera():
    ref, _ = sf.read(REF, always_2d=True); bref = bandas(ref.mean(1))
    print('régua CS2 bandas %', np.round(bref * 100).astype(int), flush=True)
    k = chave()
    for i, p in enumerate(PROMPTS_BAM):
        for c in range(2):
            d = os.path.join(hibrido.BRUTO, 'hud', f'bam_escuro_c{i * 2 + c}.mp3')
            if not os.path.exists(d): eleven.gerar_um(k, p, 2.0, False, d)
    bams = [sintetico(j) for j in range(3)] + [da_ia(f) for f in sorted(glob.glob(os.path.join(hibrido.BRUTO, 'hud', 'bam_escuro_c*.mp3')))]
    for _ in range(3): bams = [casa_bandas(b, bref) for b in bams]   # todos saíam ~80-90% sub (< 80 Hz): some em caixinha de som
    notas = [float(np.abs(bandas(b) - bref).sum()) for b in bams]
    ordem = np.argsort(notas)[:6]
    for f in glob.glob(os.path.join(OP, 'bam2_*.wav')): os.remove(f)
    for j, i in enumerate(ordem):
        motor.salvar(os.path.join(OP, f'bam2_{j}.wav'), bams[i])
        print(f'BAM {j + 1}: {"sintético" if i < 3 else "IA"} distância das bandas do CS2 {notas[i]:.2f}', np.round(bandas(bams[i]) * 100).astype(int), flush=True)
    for f in glob.glob(os.path.join(OP, 'mpfala_*.wav')): os.remove(f)
    j = 0
    for i, (mod, txt, est, sty) in enumerate(FALAS):
        d = os.path.join(hibrido.BRUTO, 'voz', f'anuncio_match_point_v21_c{i}.mp3')
        if not os.path.exists(d) and not falar(mod, txt, est, sty, d): continue
        v = radio.locutor(hibrido.carregar(d)); motor.salvar(os.path.join(OP, f'mpfala_{j}.wav'), v); print(f'fala {j + 1}: {mod} "{txt}"', flush=True); j += 1


def usar(b, f):
    import v19
    bam = sf.read(os.path.join(OP, f'bam2_{b}.wav'))[0]; v = sf.read(os.path.join(OP, f'mpfala_{f}.wav'))[0]
    fim = v19.fim_real(v); v = v[:fim + int(0.08 * SR)]
    i = len(v) + int(0.15 * SR); y = np.zeros(i + len(bam)); y[:len(v)] += v * 0.75; y[i:i + len(bam)] += bam
    for g in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')): os.remove(g)
    motor.salvar(os.path.join(S, 'hud', 'match_point_0.wav'), motor.acabamento(y, -0.5, 60)); print('match point = fala', f + 1, '+ BAM', b + 1)


if __name__ == '__main__':
    if len(sys.argv) > 3 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1, int(sys.argv[3]) - 1)
    else: gera()
    os._exit(0)
