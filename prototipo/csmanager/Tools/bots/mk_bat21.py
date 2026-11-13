"""Atalhos do exe 21: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO5.bat
(pacote do CT_NOVO4 + situ4Ligado).
Uso: python -I Tools/bots/mk_bat21.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_CT_NOVO4.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO 5 (12/11, exe 21): o pacote do CT_NOVO4 + situ4Ligado. Vale pros DOIS times.',
     'rem situ4Ligado: o comeco do "todas as situacoes, todas as opcoes" (catalogo de 92 situacoes em',
     r'rem   Tools\pesquisa\SITUACOES_E_OPCOES.md). Toda decisao nova usa a mesma peca: lista as jogadas que da pra fazer,',
     'rem   da valor a cada uma pela crenca do jogador, faz a melhor, fica nela e explica as tres melhores no pensamento.',
     'rem   1) Smoke do inimigo no caminho: esperar abrir, atravessar correndo, atravessar em silencio, atravessar junto,',
     'rem      bangar por cima, spray na saida, HE ou molotov na saida, ficar de lado e pegar quem sair.',
     'rem   2) Segurando um lugar: quieto, jiggle, sair largo, bangar, ajeitar, trocar de angulo ou avancar sai da avaliacao',
     'rem      (maior valor, gosto do jogador, custo de repetir a mesma jogada), nao mais do dado.',
     r'rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_situ4Ligado']
io.open(os.path.join(dst, 'CSManager_CT_NOVO5.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 1, 'flags')
