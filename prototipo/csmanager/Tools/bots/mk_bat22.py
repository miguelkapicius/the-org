"""Atalhos do exe 22: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO6.bat
(pacote do CT_NOVO5 + semDado5Ligado + crencaJog5Ligado + situ5Ligado).
Uso: python -I Tools/bots/mk_bat22.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_CT_NOVO5.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO 6 (12/11, exe 22): o pacote do CT_NOVO5 + semDado5Ligado + crencaJog5Ligado + situ5Ligado. Vale pros DOIS times.',
     'rem semDado5Ligado: as decisoes do jogador que eram "sorteio < chance" (65 lugares) e as escolhas por peso (14) deixam de',
     'rem   jogar dado. A chance vira o valor da jogada naquela situacao; cada jogador tem um ponto fixo por assunto e faz quando o',
     'rem   valor passa do ponto dele. Quem espera insiste (o ponto baixa); reacao a tiro, duelo e cego nao insiste.',
     'rem crencaJog5Ligado: cada jogador guarda o que ELE viu e ouviu (vale na hora, antes da call; dura a memoria dele) e pesa',
     'rem   o que veio so por call pela confianca dele. O mapa de calor continua do time.',
     'rem situ5Ligado: o colega caiu perto = trocar agora, segurar o angulo ou sair da linha, pelo valor de cada uma',
     'rem   (arma do matador, se ele ja mira por onde eu apareco, tempo de chegar, quantos em volta, de quem e o relogio).',
     'rem No CT_NOVO5 e neste: a smoke no caminho ganhou "dar a volta" e parou de alternar entre andar e esperar.',
     'rem O que ainda e sorteado de proposito: o corpo (lado do strafe, pausas, rajada, erro de mira) e a tatica do IGL no comeco do round.',
     r'rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_semDado5Ligado_crencaJog5Ligado_situ5Ligado']
io.open(os.path.join(dst, 'CSManager_CT_NOVO6.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 3, 'flags')
