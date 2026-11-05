# v19 (2026-11-04): match point refeito (usuário: "precisa de mais entonação e emoção, e o bam tá cortando a fala"):
#   locutor Brian (nº 3) mais expressivo (estabilidade baixa, estilo alto), 4 takes; o BAM nº 6 entra só DEPOIS do fim real
#   da fala (-35 dB) + 0,2 s, e mais baixo que a voz.
# Uso: python v19.py          -> _opcoes/mpvoz_K
#      python v19.py usar K   -> saida/hud/match_point_0.wav
import os, sys, glob, json, shutil, urllib.request, numpy as np, soundfile as sf
import motor, hibrido, radio, voz
from motor import SR
from eleven import chave

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes')
# v20: o usuário achou as empolgadas "com cara de IA" — quer o tom seco e firme dos anúncios aprovados ("The bomb has been planted.")
TAKES = [('Match point.', 0.55, 0.2), ('Match point.', 0.55, 0.2), ('Match point.', 0.7, 0.1), ('Match point.', 0.7, 0.1)]


def falar(texto, estab, estilo, destino):
    vid = voz.VOZES['brian'][0]
    url = f'https://api.elevenlabs.io/v1/text-to-speech/{vid}?output_format=mp3_44100_128'
    vs = dict(stability=estab, similarity_boost=0.8, style=estilo, use_speaker_boost=True)
    corpo = json.dumps(dict(text=texto, model_id=voz.MODELO, voice_settings=vs)).encode()
    req = urllib.request.Request(url, data=corpo, method='POST', headers={'Content-Type': 'application/json', 'xi-api-key': chave(), 'User-Agent': 'csmanager-sons/1.0'})
    with urllib.request.urlopen(req, timeout=60) as r: open(destino, 'wb').write(r.read())


def fim_real(x, lim_db=-35):
    w = int(0.01 * SR); e = np.sqrt(np.convolve(x ** 2, np.ones(w) / w, 'same')); vivo = np.where(e > e.max() * 10 ** (lim_db / 20))[0]
    return vivo[-1] if len(vivo) else len(x)


def gera():
    bam = sf.read(os.path.join(OP, 'bam_1.wav'))[0]   # BAM nº 6 mais alto e com tum grave (bam.py)
    for f in glob.glob(os.path.join(OP, 'mpvoz_*.wav')): os.remove(f)
    for k, (t, est, sty) in enumerate(TAKES):
        d = os.path.join(hibrido.BRUTO, 'voz', f'anuncio_match_point_v20_c{k}.mp3')
        if not os.path.exists(d): falar(t, est, sty, d)
        v = radio.locutor(hibrido.carregar(d)); fim = fim_real(v); v = v[:fim + int(0.08 * SR)]
        i = len(v) + int(0.2 * SR); y = np.zeros(i + len(bam))
        y[:len(v)] += v * 0.75; y[i:i + len(bam)] += bam * 1.0   # BAM alto (usuário: 'muito baixo')
        motor.salvar(os.path.join(OP, f'mpvoz_{k}.wav'), motor.acabamento(y, -1, 30))
    print('match point: 4 takes', flush=True)


def usar(k):
    for f in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')): os.remove(f)
    shutil.copy(os.path.join(OP, f'mpvoz_{k}.wav'), os.path.join(S, 'hud', 'match_point_0.wav')); print('match point', k + 1)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: gera()
    os._exit(0)
