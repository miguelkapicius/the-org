# HS com capacete: candidatos do EVENTO INTEIRO pra escolha no ouvido (Gabriel, 2026-11-11: o capacete montado em três
# peças por medição ficou "uma merda"). Lição antiga do projeto: som de identidade = gerar ~12 candidatos e ELE escolhe a
# base; régua só no acabamento. Aqui as tomadas ficam cruas (só corte, fade e nível).
# Uso: python -u hscap_fable.py gera | opcoes
import os, sys, glob, numpy as np, soundfile as sf
import motor, eleven
from motor import SR
from hibrido import carregar, aparar
import impactos_fable as I

BR = I.BR; OP = I.OP
P = ['video game headshot through a helmet: a sharp bright metallic ping of a bullet hitting a steel helmet, instantly followed by a crunchy crack and a wet impact, very short',
     'a bullet piercing a combat helmet and hitting the head: high pitched metal tink, then the hard shell cracking open, then a dull wet thud, fast, half a second',
     'first person shooter headshot with helmet feedback sound, bright dink then a crunchy pop, punchy and satisfying, very short',
     'a bullet punches through a thin steel helmet: sharp metallic puncture snap with a tiny high ring, followed by a wet splat, close microphone, very short',
     'foley: a metal spoon struck sharply against a ceramic bowl that cracks apart, with a wet squish right after, very short, dry studio',
     'helmet headshot kill sound effect for a competitive shooter, sharp metallic crack and wet crunch, no long ringing, very short']


def gera():
    from concurrent.futures import ThreadPoolExecutor
    k = eleven.chave(); eleven.URL = eleven.URL.replace('mp3_44100_128', 'mp3_44100_192'); fila = []
    for i, p in enumerate(P):
        for c in range(2):
            d = os.path.join(BR, f'hscap_f{i * 2 + c}.mp3')
            if not os.path.exists(d): fila.append((p + I.SECO, 0.7, d))
    def um(it):
        if not eleven.gerar_um(k, it[0], it[1], False, it[2]): print('FALHOU', it[2], flush=True)
    with ThreadPoolExecutor(4) as ex: list(ex.map(um, fila))
    print(len(fila), 'pedidos, ~', round(len(fila) * 0.7 * eleven.CRED_SEG), 'créditos', flush=True)


def cand(i):
    x = aparar(carregar(os.path.join(BR, f'hscap_f{i}.mp3')), -40, 3)[:int(0.8 * SR)]
    return motor.acabamento(x, -1.0, 30)


def opcoes():
    for f in glob.glob(os.path.join(OP, 'hscap_*.wav')): os.remove(f)
    for i in range(12):
        x = cand(i); motor.salvar(os.path.join(OP, f'hscap_s_{i}.wav'), x); motor.salvar(os.path.join(OP, f'hscap_c_{i}.wav'), I.cena([x], 0.9)[:int(1.6 * SR)])
        print(i + 1, P[i // 2][:60], '%.2f s' % (len(x) / SR), flush=True)


if __name__ == '__main__':
    if sys.argv[1:] == ['gera']: gera()
    elif sys.argv[1:] == ['opcoes']: opcoes()
    sys.stdout.flush(); os._exit(0)
