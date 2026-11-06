# Ferrolho novo da AWP (usuário 2026-11-06: "o ferrolho não está bom"). Peças separadas (trás / frente) pra casar com a
# animação; pedidos de ferrolho de sniper pesado, seco, perto, sem eco.
import os, eleven
AQUI = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(AQUI, 'eleven', 'bruto', 'awp')
P = {'ferrolho_tras': ['heavy bolt-action sniper rifle bolt handle lifted and pulled back hard, crisp solid metallic clack, close up, dry, no echo, nothing else',
                      'sniper rifle bolt unlock and pull back, two quick tight metal clicks, precise and heavy, close microphone, dry'],
     'ferrolho_frente': ['heavy bolt-action sniper rifle bolt pushed forward and locked down, solid satisfying metallic chunk, close up, dry, no echo, nothing else',
                         'sniper rifle bolt slammed forward and handle locked, sharp tight metal clack, precise and heavy, close microphone, dry']}
k = eleven.chave()
for peca, ps in P.items():
    for i, p in enumerate(ps):
        for c in range(2):
            d = os.path.join(D, f'{peca}_n{i * 2 + c}.mp3')
            if not os.path.exists(d): print(peca, i * 2 + c, 'ok' if eleven.gerar_um(k, p, 0.8, False, d) else 'FALHOU', flush=True)
os._exit(0)
