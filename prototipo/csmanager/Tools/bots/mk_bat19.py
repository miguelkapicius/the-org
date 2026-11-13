"""Atalhos do exe 19: copia todos os .bat do exe anterior e cria o CSManager_CT_NOVO3.bat
(pacote do CT_NOVO2 + caminho2Ligado + ctVivoLigado).
Uso: python -I Tools/bots/mk_bat19.py <pasta_do_exe_anterior> <pasta_do_exe_novo>"""
import io, sys, os, glob, shutil
src, dst = sys.argv[1], sys.argv[2]
for b in glob.glob(os.path.join(src, '*.bat')):
    shutil.copy2(b, os.path.join(dst, os.path.basename(b)))
old = io.open(os.path.join(src, 'CSManager_CT_NOVO2.bat'), encoding='ascii', newline='').read()
start = [l for l in old.replace('\r\n', '\n').split('\n') if l.startswith('start ')][0]
flags = start.split('-exp ', 1)[1].strip()
t = ['@echo off',
     'rem CT NOVO 3 (12/11, exe 19): o pacote do CT_NOVO2 (crenca 2, macro 2, retake 2, ja com as correcoes da revisao de logica)',
     'rem + caminho2Ligado + ctVivoLigado.',
     'rem caminho2Ligado: o caminho e uma escolha. Girando ou juntando pro retake, o CT compara o caminho direto com as voltas',
     'rem   (praca, conector, rato, mercado, base) por tempo + risco que ele acredita ter no caminho + por onde os colegas ja foram',
     'rem   + o gosto fixo dele. O retake ganhou ponto de juntar do outro lado do bomb (A pelo conector, B pelo rato).',
     'rem ctVivoLigado: sem contato o CT nao fica estatua. Cada um no seu relogio: o do meio troca de linha, o apoio vai buscar',
     'rem   informacao no acesso do bomb dele e volta, o ancora troca de angulo. No primeiro contato todo mundo volta pro lugar.',
     'rem Tambem neste exe (flag macro2Ligado): no retake o CT corre ate perto do C4 e so anda de shift no trecho final;',
     'rem   quem esta saindo do raio da bomba nao fica indo e voltando na beira.',
     r'rem Leitura dos pros em palavras: Tools\pro\PROS_EM_PALAVRAS.md. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.',
     f'start "" "%~dp0CSManager.exe" -exp {flags}_caminho2Ligado_ctVivoLigado']
io.open(os.path.join(dst, 'CSManager_CT_NOVO3.bat'), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')
print('ok', len(flags.split('_')) + 2, 'flags')
