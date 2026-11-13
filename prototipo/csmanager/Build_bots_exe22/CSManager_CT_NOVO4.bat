@echo off
rem CT NOVO 4 (12/11, exe 20): o pacote do CT_NOVO3 + decisao3Ligado + duelo3Ligado. Vale pros DOIS times.
rem decisao3Ligado: como abrir um lugar onde o jogador sabe que tem inimigo. Ele compara bangar e entrar no estouro,
rem   pedir a flash do colega, esperar o colega que esta vindo e abrir junto, ou abrir seco, e faz a de maior chance.
rem   Quem esta atras banga pro colega da frente. Depois da flash entra correndo.
rem duelo3Ligado: o duelo que nao esta rendendo. A cada meio segundo de duelo o jogador escolhe ficar, ganhar espaco
rem   (chegar mais perto; o CT com a bomba plantada vai na direcao do C4) ou largar e seguir a ordem, pelo que a arma
rem   dele rende naquela distancia, colegas do lado, placar de vivos e relogio. Pistola e SMG nao ficam trocando de longe.
rem   No maximo duas voltas pra cobertura em 10 s: acabou o abre, toma tiro, volta em loop.
rem   O TR depois do plant segura (o relogio e dele). Cego: se acredita que vem alguem procura cobertura; senao fica.
rem Falas do Gabriel: Tools\pesquisa\gabriel_falas_10a12nov.txt. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT_crenca2Ligado_macro2Ligado_retake2Ligado_caminho2Ligado_ctVivoLigado_decisao3Ligado_duelo3Ligado
