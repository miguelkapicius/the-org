"""Atalhos do exe 20: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO4.bat
(pacote do CT_NOVO3 + decisao3Ligado + duelo3Ligado).
Uso: python -I Tools/bots/mk_bat20.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_CT_NOVO3.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO 4 (12/11, exe 20): o pacote do CT_NOVO3 + decisao3Ligado + duelo3Ligado. Vale pros DOIS times.',
     'rem decisao3Ligado: como abrir um lugar onde o jogador sabe que tem inimigo. Ele compara bangar e entrar no estouro,',
     'rem   pedir a flash do colega, esperar o colega que esta vindo e abrir junto, ou abrir seco, e faz a de maior chance.',
     'rem   Quem esta atras banga pro colega da frente. Depois da flash entra correndo.',
     'rem duelo3Ligado: o duelo que nao esta rendendo. A cada meio segundo de duelo o jogador escolhe ficar, ganhar espaco',
     'rem   (chegar mais perto; o CT com a bomba plantada vai na direcao do C4) ou largar e seguir a ordem, pelo que a arma',
     'rem   dele rende naquela distancia, colegas do lado, placar de vivos e relogio. Pistola e SMG nao ficam trocando de longe.',
     'rem   No maximo duas voltas pra cobertura em 10 s: acabou o abre, toma tiro, volta em loop.',
     'rem   O TR depois do plant segura (o relogio e dele). Cego: se acredita que vem alguem procura cobertura; senao fica.',
     r'rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_decisao3Ligado_duelo3Ligado']
io.open(os.path.join(dst, 'CSManager_CT_NOVO4.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 2, 'flags')
