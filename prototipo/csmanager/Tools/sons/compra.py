# Compra no freeze = o clássico "ka-ching" de gastar dinheiro (usuário, 2026-11-04): "ka" (clunk da caixa/gaveta)
# + "ching" (sino), em dois tempos, levemente mais rápido que uma registradora normal.
# Candidatos ElevenLabs -> limpa -> aproxima levemente os dois tempos (compra_rapida.rapida) -> saida/_opcoes/compra_K.wav
# Uso: python compra.py         (gera candidatos)
#      python compra.py usar K  (vira saida/hud/compra_0.wav)
import os, sys, glob, shutil, numpy as np
import motor, eleven, compra_rapida
from motor import SR
from hibrido import carregar, aparar

AQUI = os.path.dirname(os.path.abspath(__file__))
BRUTO = os.path.join(AQUI, 'eleven', 'bruto', 'hud')
OP = os.path.join(AQUI, 'saida', '_opcoes')
PROMPTS = ['the classic "ka-ching" money sound effect: a quick cash register clunk immediately followed by a bright bell ding, spending money',
           'iconic cha-ching sound effect, cash register bell ring with a coin rattle, money spent, short and crisp',
           'ka-ching! cash register sound effect played when you buy something in a video game, quick clunk then ding',
           'vintage cash register cha-ching: drawer clunk and one bright bell ring, very short, close, no reverb']
TAKES = 3


def gera():
    k = eleven.chave(); n = 0
    for i, p in enumerate(PROMPTS):
        for c in range(TAKES):
            d = os.path.join(BRUTO, f'kaching_c{i * TAKES + c}.mp3')
            if not os.path.exists(d) and eleven.gerar_um(k, p, 0.8, False, d): n += 1
    print('gerados', n, flush=True)
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'compra_*.wav')): os.remove(f)
    arqs = sorted(glob.glob(os.path.join(BRUTO, 'kaching_c*.mp3')), key=lambda f: int(f.rsplit('_c', 1)[1][:-4]))
    for j, f in enumerate(arqs):
        x = aparar(carregar(f))[:int(1.2 * SR)].copy()
        compra_rapida.CORTE = 0.25          # "levemente mais rápida"
        y, modo = compra_rapida.rapida(x)
        motor.salvar(os.path.join(OP, f'compra_{j}.wav'), y); print(j + 1, f'{len(y) / SR:.2f} s', modo, flush=True)


def usar(k):
    for f in glob.glob(os.path.join(AQUI, 'saida', 'hud', 'compra_*.wav')): os.remove(f)
    shutil.copy(os.path.join(OP, f'compra_{k}.wav'), os.path.join(AQUI, 'saida', 'hud', 'compra_0.wav'))
    print('compra = ka-ching', k + 1)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: gera()
    os._exit(0)
