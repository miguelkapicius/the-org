# Torcida de campeonato LAN (usuário 2026-11-07): "som ambiente de torcida, multidão assistindo... vários, 15 muvucas, pra não
# ficar perceptível que repete"; gritos no começo e no fim da partida, no clutch, ace, multi-kill e jogadas; "tem que ser mais
# alto". Tomadas cruas da ElevenLabs em eleven/bruto/torcida/<nome>_n<i>.mp3 (cache: não gera de novo o que já existe).
# Diferente das armas, aqui NÃO vai o sufixo "seco": torcida precisa da sala (arena). Pede sem música e sem locutor.
# Uso: python torcida_ia.py [--seco]
import os, sys
from concurrent.futures import ThreadPoolExecutor
from eleven import chave, gerar_um, CRED_SEG, BRUTO

ARENA = ', large indoor esports arena, live audience recording, no music, no announcer, no clear words'

MURMURIO = [   # a cama: 15 trechos em laço; o jogo sorteia a ordem pela semente e toca 2 camadas cruzando
    'calm audience murmur between rounds, thousands of people chatting softly',
    'tense focused crowd hum, low murmur, an occasional cough and seat creak',
    'excited crowd chatter rising and falling, scattered claps',
    'crowd murmur with distant rhythmic clapping that starts and fades out',
    'lively audience, scattered shouts and whistles far away over a constant murmur',
    'anticipation in the crowd, people whispering, nervous energy, soft murmur',
    'big arena room tone with a crowd hum and feet shuffling on bleachers',
    'crowd murmur with sporadic small cheers from groups of fans',
    'distant wordless chanting oh oh oh from a fan section over the crowd murmur',
    'inflatable thundersticks banging rhythmically in the distance over a crowd murmur',
    'restless crowd, a ripple of applause passing through, murmur',
    'quiet tense arena, low hum, just a few voices',
    'crowd buzzing after a big play and slowly settling down',
    'audience murmur with an occasional collective ooh wave',
    'large crowd ambience, steady energetic low roar of thousands of fans',
]
REACOES = [   # (nome, textos, duração, quantas)
    ('inicio', ['crowd roaring and applauding as the match begins, thundersticks, whistles, building excitement',
                'arena audience cheering loudly for the teams walking on stage, applause and shouting'], 8.0, 4),
    ('fim', ['huge crowd ovation at the end of a grand final, roaring, long applause, whistles, ecstatic',
             'thousands of fans erupting and applauding the winning team, standing ovation, sustained roar'], 10.0, 4),
    ('round', ['crowd cheering and clapping after a round win, short medium cheer',
               'fans applauding and shouting briefly after a good round'], 4.0, 6),
    ('multikill', ['crowd roar rising fast after a triple kill, excited screaming fans',
                   'audience exploding into a loud cheer for an amazing multi kill'], 4.0, 6),
    ('jogada', ['crowd gasping ooooh in surprise then cheering at an incredible play',
                'audience collective wow followed by a burst of applause'], 3.5, 6),
    ('quase', ['crowd collective ohhh groan of disappointment after a near miss',
               'audience sighing aww together, letdown, quick groan'], 3.0, 4),
    ('clutch', ['esports arena crowd erupting in cheers after a clutch play, big rising roar and applause',
                'live audience exploding into screams and clapping after a one versus three clutch'], 5.0, 4),
    ('ace', ['massive arena crowd going wild for an ace, screaming roar, stomping and applause',
             'huge crowd explosion of cheering, ecstatic roar, everyone on their feet'], 6.0, 3),
]


def fila():
    D = os.path.join(BRUTO, 'torcida')
    for i, t in enumerate(MURMURIO):
        d = os.path.join(D, f'murmurio_n{i}.mp3')
        if not os.path.exists(d): yield (t + ARENA, 22.0, True, d)
    for nome, txts, dur, n in REACOES:
        for i in range(n):
            d = os.path.join(D, f'{nome}_n{i}.mp3')
            if not os.path.exists(d): yield (txts[i % len(txts)] + ARENA, dur, False, d)


def main():
    f = list(fila()); custo = sum(round(x[1] * CRED_SEG) for x in f)
    print(f'{len(f)} tomadas a gerar, ~{custo} créditos', flush=True)
    if '--seco' in sys.argv or not f: return
    k = chave()
    if not k: print('sem chave'); sys.exit(2)
    gasto = [0]
    def um(x):
        t, dur, laco, d = x
        if gerar_um(k, t, dur, laco, d): gasto[0] += round(dur * CRED_SEG); print('  ', os.path.basename(d), gasto[0], flush=True)
    with ThreadPoolExecutor(4) as ex: list(ex.map(um, f))
    print('gasto ~', gasto[0], flush=True)


if __name__ == '__main__':
    main()
    os._exit(0)
