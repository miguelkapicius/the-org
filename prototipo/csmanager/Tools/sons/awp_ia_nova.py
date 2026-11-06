# AWP: corpos novos da IA (usuário 2026-11-06: "parece um pote de maionese caindo no chão, até a Deagle tem mais").
# Medido: a AWP atual é um 'tum' em 158 Hz (+18 dB acima do entorno) com BURACO em 150-500 Hz (22 dB abaixo do sub);
# a Deagle tem 150-500 Hz 6 dB mais cheio. Aqui os prompts pedem explosão de boca encorpada e seca, sem eco.
import os, sys
import eleven
AQUI = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(AQUI, 'eleven', 'bruto', 'awp')
P = ['one single close-up sniper rifle gunshot, huge explosive muzzle blast with punchy low-mid body, sharp supersonic crack, completely dry, no reverb, no echo, just one shot',
     'one single .50 caliber anti-materiel rifle shot fired right next to the microphone, violent concussive blast, cracking snap, tight and dry, no tail, no echo',
     'one single cinematic sniper rifle gunshot for an action movie, massive punchy boom with a whip-like crack, aggressive and loud, dry studio, no reverb',
     'one single powerful magnum rifle gunshot, heavy chest-thumping blast and crisp high crack, outdoor close range, short and dry, no echo, nothing else']
k = eleven.chave()
for i, p in enumerate(P):
    for c in range(2):
        d = os.path.join(D, f'tiro_n{i * 2 + c}.mp3')
        if not os.path.exists(d): print(i * 2 + c, 'ok' if eleven.gerar_um(k, p, 1.5, False, d) else 'FALHOU', flush=True)
os._exit(0)
