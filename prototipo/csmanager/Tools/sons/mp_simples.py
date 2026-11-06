# Match point do zero (usuário, 2026-11-04: "pega a mesma voz das outras falas, com empolgação, só falando match point").
# Mesmo caminho dos anúncios aprovados: voz.falar('brian', ...) (multilingual v2, configurações do voz.py) + radio.locutor.
# Sem truque de texto/pausa. 4 takes -> _opcoes/mpsimples_K. Uso: python mp_simples.py | python mp_simples.py usar K
import os, sys, glob, shutil
import motor, hibrido, radio, voz
OP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida', '_opcoes'); S = os.path.dirname(OP)
TAKES = [False, False, True, True]   # gritado = configuração "empolgada" que o voz.py já usa pro rádio

if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar':
        for f in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')): os.remove(f)
        shutil.copy(os.path.join(OP, f'mpsimples_{int(sys.argv[2]) - 1}.wav'), os.path.join(S, 'hud', 'match_point_0.wav')); print('ok')
    else:
        for f in glob.glob(os.path.join(OP, 'mpsimples_*.wav')): os.remove(f)
        for k, g in enumerate(TAKES):
            d = os.path.join(hibrido.BRUTO, 'voz', f'anuncio_match_point_simples_c{k}.mp3')
            if not os.path.exists(d): voz.falar('brian', 'Match point!', d, g)
            motor.salvar(os.path.join(OP, f'mpsimples_{k}.wav'), radio.locutor(hibrido.carregar(d))); print(k + 1, 'empolgado' if g else 'padrão', flush=True)
    os._exit(0)
