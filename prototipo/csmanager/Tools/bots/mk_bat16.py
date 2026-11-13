"""Atalhos do exe 16: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO.bat (pacote do TESTE + cerebroNovoCT).
Uso: python -I Tools/bots/mk_bat16.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_TESTE.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO (cerebro novo, 11/11 noite): o mesmo CSManager.exe (exe 16) com o pacote do TESTE + cerebroNovoCT.',
     'rem So o IGL do CT antes do plant e novo; o TR, o retake e a decisao individual de cada bot ainda sao do cerebro velho.',
     'rem O que muda no CT:',
     'rem   - setup escolhido pelo valor (leitura do adversario, o que vem dando certo, custo de repetir), sem sorteio',
     'rem   - cada jogador tem tarefa com parceiro: ancora, apoio (cruza com o ancora), meio',
     'rem   - colega caiu e nao deu pra trocar: quem estava perto escolhe segurar a linha de quem matou ou recuar pro fundo do bomb,',
     'rem     e joga molotov/HE na entrada se tiver (um so, longe do time)',
     'rem   - giro so por informacao (2+ acreditados ou C4 visto); o ancora do outro lado so larga com o C4 visto',
     'rem   - em vantagem segura; em desvantagem de 2+ recua e joga junto',
     'rem Plano completo: PLANO_SIMULACAO.md na raiz do projeto. A caixa-preta grava a leitura do time (linhas C) em Logs\\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_cerebroNovoCT']
io.open(os.path.join(dst, 'CSManager_CT_NOVO.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 1, 'flags')
