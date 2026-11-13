"""Atalhos do exe 17: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO2.bat
(pacote do CT_NOVO + crenca2Ligado + macro2Ligado + retake2Ligado).
Uso: python -I Tools/bots/mk_bat17.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_CT_NOVO.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO 2 (12/11): o mesmo CSManager.exe (exe 17) com o pacote do CT_NOVO + crenca2Ligado + macro2Ligado + retake2Ligado.',
     'rem crenca2Ligado (vale pros dois times):',
     'rem   - o som e de quem fez o som: 4 correndo juntos contam 4 (antes viravam 1) e nao sobra fantasma quando o dono morre',
     'rem   - o time conta vistos + ouvidos por zona; "execucao" e a maioria dos vivos deles num lado',
     'rem   - call atrasada nao apaga som mais novo; nada e limpo atraves de parede; o colado na quina nao some',
     'rem   - depois do plant, quem nao deu noticia esta perto do C4 (o CT nao anda mais de shift na propria base)',
     'rem macro2Ligado: girando, recuando ou juntando pro retake ninguem desvia pra buscar arma nem pra jogar granada no lugar',
     'rem   que esta largando; arma do chao so existe pra quem viu; juntar pro retake e correndo',
     'rem retake2Ligado: guardar ou retomar e UMA decisao do time, pela chance do placar de vivos (regua dos pros);',
     'rem   se o placar melhora, quem guardava volta',
     r'rem Leitura dos pros x bots: Tools\pro\MACRO_CT.md. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_crenca2Ligado_macro2Ligado_retake2Ligado']
io.open(os.path.join(dst, 'CSManager_CT_NOVO2.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 3, 'flags')
