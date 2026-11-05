# Ambiente da vila com cara de vila MARROQUINA (usuário, 2026-11-04): baixinho, de fundo, o tempo todo.
# Candidatos ElevenLabs (laços de 30 s) -> saida/_opcoes/ambiente_K.wav (nivelados a -20 dB RMS, emenda do laço suavizada)
# Uso: python ambiente.py          (gera candidatos)
#      python ambiente.py usar K   (vira saida/ambiente/vila_0.wav; o Som3D.Ambiente toca ela no lugar do mercado genérico)
import os, sys, glob, shutil, numpy as np
import motor, eleven
from motor import SR
from hibrido import carregar

AQUI = os.path.dirname(os.path.abspath(__file__))
BRUTO = os.path.join(AQUI, 'eleven', 'bruto', 'ambiente')
OP = os.path.join(AQUI, 'saida', '_opcoes')
PROMPTS = ['Moroccan village ambience: distant souk market murmur with faint Arabic chatter, occasional cart wheels on stone, warm wind, swallows chirping, far away and calm',
           'quiet North African medina street ambience, distant voices echoing in narrow stone alleys, a faint oud playing far away, light warm wind',
           'sunny Moroccan desert town ambience, dry warm wind, distant goats and a rooster, faint market crowd far away, calm and peaceful',
           'Marrakech medina background ambience, very distant bustling market, a faint street musician with hand drums, birds, soft and far, no close sounds']
DUR = 30.0


def nivela(x, alvo_db=-20):
    r = np.sqrt((x ** 2).mean()) + 1e-9
    return x * 10 ** (alvo_db / 20) / r


def gera():
    k = eleven.chave(); n = 0
    for i, p in enumerate(PROMPTS):
        d = os.path.join(BRUTO, f'marrocos_c{i}.mp3')
        if not os.path.exists(d) and eleven.gerar_um(k, p, DUR, True, d): n += 1
    print('gerados', n, flush=True)
    os.makedirs(OP, exist_ok=True)
    for f in glob.glob(os.path.join(OP, 'ambiente_*.wav')): os.remove(f)
    for i in range(len(PROMPTS)):
        d = os.path.join(BRUTO, f'marrocos_c{i}.mp3')
        if not os.path.exists(d): continue
        x = motor.hp(carregar(d), 40)
        motor.salvar(os.path.join(OP, f'ambiente_{i}.wav'), np.clip(nivela(x), -0.98, 0.98)); print(i + 1, f'{len(x) / SR:.1f} s', flush=True)


def usar(k):
    shutil.copy(os.path.join(OP, f'ambiente_{k}.wav'), os.path.join(AQUI, 'saida', 'ambiente', 'vila_0.wav'))
    print('ambiente = marrocos', k + 1)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: gera()
    os._exit(0)
