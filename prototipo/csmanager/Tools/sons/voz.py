# Vozes (ElevenLabs text-to-speech): locutor (anúncios) e rádio dos jogadores, com efeito de rádio nosso.
# Uso: python voz.py amostra            -> eleven/bruto/voz_amostra/<papel>_<voz>_<fala>.mp3 (pra escolher a voz)
#      python voz.py tudo <locutor> <ct> <tr>   -> todas as falas nas vozes escolhidas (cache: não gera de novo)
import os, sys, json, urllib.request, urllib.error
from eleven import chave, AQUI

BRUTO = os.path.join(AQUI, 'eleven', 'bruto')
MODELO = 'eleven_multilingual_v2'
VOZES = {  # nome curto: (voice_id, descrição)
    'daniel': ('onwK4e9ZLuTAKqWW03F9', 'locutor britânico firme'), 'adam': ('pNInz6obpgDQGcFmaJgB', 'grave, dominante'),
    'brian': ('nPczCjzI2devNBz1zQrb', 'grave, ressonante'), 'sarah': ('EXAVITQu4vr4xnSDxMaL', 'feminina, segura'),
    'harry': ('SOYHLrjzK2X1ezoPC6cr', 'guerreiro, rouco'), 'callum': ('N2lVS1w4EtoT3dr4eOWO', 'rouco, malandro'),
    'charlie': ('IKne3meq5aSn9XLyUdCD', 'grave, confiante (australiano)'),
}
AMOSTRA = {'locutor': (['daniel', 'adam', 'brian', 'sarah'], ['The bomb has been planted.']),
           'radio': (['harry', 'callum', 'charlie', 'adam', 'brian'], ['Go, go, go!', 'Fire in the hole!'])}
# falas clássicas (nome do arquivo: texto). Anúncios = locutor; o resto = rádio de cada time.
ANUNCIOS = {'bomba_plantada': 'The bomb has been planted.', 'bomba_desarmada': 'Bomb has been defused.',
            'vitoria_ct': 'Counter-Terrorists win.', 'vitoria_tr': 'Terrorists win.'}
RADIO = {'gogogo': 'Go, go, go!'}   # usuário: só os clássicos (anúncios + go go go); o resto do rádio não vai ser usado


def falar(voz, texto, destino, gritado=False):
    vid = VOZES[voz][0]
    url = f'https://api.elevenlabs.io/v1/text-to-speech/{vid}?output_format=mp3_44100_128'
    vs = dict(stability=0.35 if gritado else 0.55, similarity_boost=0.8, style=0.45 if gritado else 0.2, use_speaker_boost=True)
    corpo = json.dumps(dict(text=texto, model_id=MODELO, voice_settings=vs)).encode()
    req = urllib.request.Request(url, data=corpo, method='POST', headers={'Content-Type': 'application/json', 'xi-api-key': chave(), 'User-Agent': 'csmanager-sons/1.0'})
    try:
        with urllib.request.urlopen(req, timeout=60) as r: dados = r.read()
        os.makedirs(os.path.dirname(destino), exist_ok=True); open(destino, 'wb').write(dados); return True
    except urllib.error.HTTPError as e:
        print('  ERRO', e.code, e.read()[:200].decode('utf-8', 'replace')); return False


def gritado(t): return t.endswith('!')


def amostra():
    d = os.path.join(BRUTO, 'voz_amostra'); n = 0
    for papel, (vozes, falas) in AMOSTRA.items():
        for v in vozes:
            for i, t in enumerate(falas):
                dest = os.path.join(d, f'{papel}_{v}_{i}.mp3')
                if not os.path.exists(dest) and falar(v, t, dest, gritado(t)): n += 1
    print('amostras geradas:', n)


def tudo(locutor, ct, tr, takes=2):
    n = 0
    for nome, t in ANUNCIOS.items():
        for k in range(takes):
            dest = os.path.join(BRUTO, 'voz', f'anuncio_{nome}_c{k}.mp3')
            if not os.path.exists(dest) and falar(locutor, t, dest): n += 1
    for time, voz in (('ct', ct), ('tr', tr)):
        for nome, t in RADIO.items():
            if (time, nome) in (('ct', 'molotov'), ('tr', 'incendiaria'), ('ct', 'plantando'), ('tr', 'desarmando')): continue
            for k in range(takes):
                dest = os.path.join(BRUTO, 'voz', f'radio_{time}_{nome}_c{k}.mp3')
                if not os.path.exists(dest) and falar(voz, t, dest, gritado(t)): n += 1
    print('falas geradas:', n)


def anuncios_todas():
    """Os 4 anúncios em cada voz de locutor (pra escolher ouvindo o conjunto)."""
    n = 0
    for v in AMOSTRA['locutor'][0]:
        for nome, t in ANUNCIOS.items():
            dest = os.path.join(BRUTO, 'voz_amostra', f'anuncio_{v}_{nome}.mp3')
            if not os.path.exists(dest) and falar(v, t, dest): n += 1
    print('anúncios gerados:', n)


if __name__ == '__main__':
    if sys.argv[1] == 'amostra': amostra()
    elif sys.argv[1] == 'anuncios': anuncios_todas()
    else: tudo(*sys.argv[2:5])
