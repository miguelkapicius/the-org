@echo off
rem CT NOVO 2 (12/11): o mesmo CSManager.exe (exe 18, com as correcoes da revisao de logica) com o pacote do CT_NOVO + crenca2Ligado + macro2Ligado + retake2Ligado.
rem crenca2Ligado (vale pros dois times):
rem   - o som e de quem fez o som: 4 correndo juntos contam 4 (antes viravam 1) e nao sobra fantasma quando o dono morre
rem   - o time conta vistos + ouvidos por zona; "execucao" e a maioria dos vivos deles num lado
rem   - call atrasada nao apaga som mais novo; nada e limpo atraves de parede; o colado na quina nao some
rem   - depois do plant, quem nao deu noticia esta perto do C4 (o CT nao anda mais de shift na propria base)
rem macro2Ligado: girando, recuando ou juntando pro retake ninguem desvia pra buscar arma nem pra jogar granada no lugar
rem   que esta largando; arma do chao so existe pra quem viu; juntar pro retake e correndo
rem retake2Ligado: guardar ou retomar e UMA decisao do time, pela chance do placar de vivos (regua dos pros);
rem   se o placar melhora, quem guardava volta
rem Leitura dos pros x bots: Tools\pro\MACRO_CT.md. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT_crenca2Ligado_macro2Ligado_retake2Ligado
