# Tec-9 e Five-SeveN com o "som específico" delas (usuário, 2026-11-04). Raio-x da referência do CS2 (scratchpad raio_x.py):
#  Five-SeveN: centroide 5,4 kHz, "tum" em 220 Hz + anel metálico ~7,3 kHz, bandas sub/grave/médio/agudo/ar = 39/20/29/6/6.
#  Tec-9: grave enorme pra pistola (78% < 150 Hz, pico 228 Hz) + crepitar chiado 9-10 kHz, centroide 4,4 kHz.
#  Os nossos 1ºs saíram "caixa" (60-67% no médio, centroide 1,4-2,1 kHz). Prompts descrevem essa assinatura.
# Gera tiro_c10.. (o hibrido.ajustar_tiro usa só os índices >= 10 quando existem) e roda o híbrido nas duas.
import os, eleven
AQUI = os.path.dirname(os.path.abspath(__file__)); BRUTO = os.path.join(AQUI, 'eleven', 'bruto')
P = {
    'fiveseven': ['one gunshot from a high-velocity 5.7mm pistol: a short punchy low thump with a very bright sharp cracking ping on top, '
                  'thin metallic ring, snappy, airy and high-pitched, tight short tail',
                  'single shot of a small-caliber high-velocity handgun, bright whip-crack with a crisp metallic ping, light thump underneath, very short',
                  'sharp bright pistol shot with a ringing metallic "tink" and a quick low pop, high frequency snap, dry'],
    'tec9': ['one gunshot from a cheap 9mm machine pistol: a deep heavy bassy boom, far bigger than a normal pistol, with a harsh sizzling crackle on top, loud and short',
             'single pistol shot with a huge low-end thump and a bright crispy crackling snap, booming and gritty, short tail',
             'loud boomy 9mm pistol shot: fat bass punch with a hissing crackle, raw and harsh, close'],
}
SECO = ', dry close-mic recording, no reverb, no echo, no music, isolated sound effect, just one shot, nothing else'

if __name__ == '__main__':
    k = eleven.chave()
    for a, txts in P.items():
        for c in range(8):
            d = os.path.join(BRUTO, a, f'tiro_c{10 + c}.mp3')
            if not os.path.exists(d): eleven.gerar_um(k, txts[c % len(txts)] + SECO, 1.2, False, d)
        print(a, 'ok', flush=True)
    os._exit(0)
