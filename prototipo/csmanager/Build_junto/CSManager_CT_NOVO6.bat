@echo off
rem CT NOVO 6 (12/11, exe 22): o pacote do CT_NOVO5 + semDado5Ligado + crencaJog5Ligado + situ5Ligado. Vale pros DOIS times.
rem semDado5Ligado: as decisoes do jogador que eram "sorteio < chance" (65 lugares) e as escolhas por peso (14) deixam de
rem   jogar dado. A chance vira o valor da jogada naquela situacao; cada jogador tem um ponto fixo por assunto e faz quando o
rem   valor passa do ponto dele. Quem espera insiste (o ponto baixa); reacao a tiro, duelo e cego nao insiste.
rem crencaJog5Ligado: cada jogador guarda o que ELE viu e ouviu (vale na hora, antes da call; dura a memoria dele) e pesa
rem   o que veio so por call pela confianca dele. O mapa de calor continua do time.
rem situ5Ligado: o colega caiu perto = trocar agora, segurar o angulo ou sair da linha, pelo valor de cada uma
rem   (arma do matador, se ele ja mira por onde eu apareco, tempo de chegar, quantos em volta, de quem e o relogio).
rem No CT_NOVO5 e neste: a smoke no caminho ganhou "dar a volta" e parou de alternar entre andar e esperar.
rem O que ainda e sorteado de proposito: o corpo (lado do strafe, pausas, rajada, erro de mira) e a tatica do IGL no comeco do round.
rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT_crenca2Ligado_macro2Ligado_retake2Ligado_caminho2Ligado_ctVivoLigado_decisao3Ligado_duelo3Ligado_situ4Ligado_semDado5Ligado_crencaJog5Ligado_situ5Ligado
