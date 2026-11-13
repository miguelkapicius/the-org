@echo off
rem CT NOVO 5 (12/11, exe 21): o pacote do CT_NOVO4 + situ4Ligado. Vale pros DOIS times.
rem situ4Ligado: o comeco do "todas as situacoes, todas as opcoes" (catalogo de 92 situacoes em
rem   Tools\pesquisa\SITUACOES_E_OPCOES.md). Toda decisao nova usa a mesma peca: lista as jogadas que da pra fazer,
rem   da valor a cada uma pela crenca do jogador, faz a melhor, fica nela e explica as tres melhores no pensamento.
rem   1) Smoke do inimigo no caminho: esperar abrir, atravessar correndo, atravessar em silencio, atravessar junto,
rem      bangar por cima, spray na saida, HE ou molotov na saida, ficar de lado e pegar quem sair.
rem   2) Segurando um lugar: quieto, jiggle, sair largo, bangar, ajeitar, trocar de angulo ou avancar sai da avaliacao
rem      (maior valor, gosto do jogador, custo de repetir a mesma jogada), nao mais do dado.
rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT_crenca2Ligado_macro2Ligado_retake2Ligado_caminho2Ligado_ctVivoLigado_decisao3Ligado_duelo3Ligado_situ4Ligado
