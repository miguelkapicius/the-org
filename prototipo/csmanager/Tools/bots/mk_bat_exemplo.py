"""Atalhos de teste do exe 15 a partir do atalho do exe 12. Uso: python mk_bat.py <pasta_do_exe12> <pasta_do_exe13>"""
import io, sys, os
src, dst = sys.argv[1], sys.argv[2]
old = io.open(os.path.join(src, 'CSManager_TESTE.bat'), encoding='ascii', newline='').read()
linhas = old.replace('\r\n', '\n').rstrip('\n').split('\n')
start = [l for l in linhas if l.startswith('start ')][0]
flags12 = start.split('-exp ', 1)[1].strip()
novas13 = 'cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090'
novas14 = 'retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado'
novas15 = 'raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado'
rems = [l for l in linhas if not l.startswith('start ') and not l.startswith('@echo') and not l.startswith('rem Exe de TESTE') and not l.startswith('rem O exe normal')]

def grava(nome, cab, extra, flags):
    t = ['@echo off'] + cab + rems + extra + [f'start "" "%~dp0CSManager.exe" -exp {flags}']
    io.open(os.path.join(dst, nome), 'w', encoding='ascii', newline='').write('\r\n'.join(t) + '\r\n')


R13 = ['rem --- do exe 13 (duelo): cobFim (volta pra cobertura ate o fim), menorNumero (sozinho contra 2+ da a rajada e sai),',
       'rem   assentaFisica, reacaoConhecido, balaPerto, bangViraCurto (agora tambem abre CORRENDO na propria flash), esperaOlhaPerigo, maoAmeaca090']
R14 = ['rem --- NOVO no exe 14 (sessao a fundo, 11/11)',
       'rem   quinaSeguraLigado  = quem segura escolhe o ponto de onde so a cabeca aparece (antes mostrava o corpo inteiro em 87 pct dos duelos):',
       'rem                        1o abate do CT 48,8 -> 52,2 pct (pro 60), TR 52,7 -> 49,4 pct, retake +3,7',
       'rem   quinaPeekLigado    = no par cobertura+peek, prefere o peek que mostra menos corpo',
       'rem   retakeFirmeLigado  = no retake, depois do "entra", o CT nao sai e reabre (retake com vantagem +3,7)',
       'rem   semTRDesarmaLigado = sem TR vivo, quem guardava a arma volta e alguem vai desarmar correndo (o Rocha com kit parado e a bomba explodindo)',
       'rem   fugaEspalhaLigado  = fugindo da bomba / guardando arma, cada um escolhe um canto (colados no mesmo canto: 61 -> 14 pct dos rounds)',
       'rem   validador de jeito humano (200 rounds): total 353 -> 377 cenas/100 rounds, "mira na parede" 29 -> 44, sem uma flag culpada (37-44 tirando',
       'rem                        qualquer uma). O OLHAR segue sendo o ponto fraco: depende da crenca por jogador, que e o proximo item grande.']

R15 = ['rem --- NOVO no exe 15 (sessao a fundo, 11/11 tarde)',
       'rem   raioRampaLigado    = o bot achava que escada/rampa subindo era parede e virava a mira pra outro lado; agora o raio segue o relevo',
       'rem   abertoVoltaLigado  = parado encostado na parede com o som/call do outro lado dela: procura a volta inteira pelo lado aberto',
       'rem   armaFimRoundLigado = round acabado, quem sobrou pega a arma mais cara do chao (o TR de MAC-10 com a AWP caida)',
       'rem   "mira na parede" (validador, regua justa sem escada/cego/pezinho): 30,5 -> 23 por 100 rounds. Equilibrio neutro.',
       'rem   CAIXA-PRETA (sempre ligada, com ou sem flags): o jogo grava cada round em Logs\partidas\<data-hora>\ pra sessao dos bots abrir']
grava('CSManager_TESTE.bat',
      ['rem Exe de TESTE: o mesmo CSManager.exe (exe 15), com as mudancas ainda em validacao ligadas.',
       'rem O exe normal (CSManager.exe direto) roda SEM nenhuma destas.'],
      R13 + R14 + R15, flags12 + '_' + novas13 + '_' + novas14 + '_' + novas15)
grava('CSManager_TESTE_exe14.bat',
      ['rem Comparacao: o exe 15 com as flags de teste do exe 14 (sem as tres novas do exe 15).'], R13 + R14, flags12 + '_' + novas13 + '_' + novas14)
grava('CSManager_TESTE_strafe.bat',
      ['rem TESTE do exe 15 + strafadinha de quem segura (apGingaBase040). Fora do TESTE principal: erra menos tiro seguido,',
       'rem mas o TR planta mais (+4,8 pontos).'],
      R13 + R14 + R15, flags12 + '_' + novas13 + '_' + novas14 + '_' + novas15 + '_apGingaBase040')
grava('CSManager_TESTE_duelo_exe12.bat',
      ['rem Comparacao: o exe 15 com as flags de teste do exe 12, SEM nada do exe 13, 14 ou 15.'], [], flags12)
print('ok', len((flags12 + '_' + novas13 + '_' + novas14 + '_' + novas15).split('_')))
