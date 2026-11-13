@echo off
rem CT NOVO 3 (12/11, exe 19): o pacote do CT_NOVO2 (crenca 2, macro 2, retake 2, ja com as correcoes da revisao de logica)
rem + caminho2Ligado + ctVivoLigado.
rem caminho2Ligado: o caminho e uma escolha. Girando ou juntando pro retake, o CT compara o caminho direto com as voltas
rem   (praca, conector, rato, mercado, base) por tempo + risco que ele acredita ter no caminho + por onde os colegas ja foram
rem   + o gosto fixo dele. O retake ganhou ponto de juntar do outro lado do bomb (A pelo conector, B pelo rato).
rem ctVivoLigado: sem contato o CT nao fica estatua. Cada um no seu relogio: o do meio troca de linha, o apoio vai buscar
rem   informacao no acesso do bomb dele e volta, o ancora troca de angulo. No primeiro contato todo mundo volta pro lugar.
rem Tambem neste exe (flag macro2Ligado): no retake o CT corre ate perto do C4 e so anda de shift no trecho final;
rem   quem esta saindo do raio da bomba nao fica indo e voltando na beira.
rem Leitura dos pros em palavras: Tools\pro\PROS_EM_PALAVRAS.md. Plano: PLANO_SIMULACAO.md. Caixa-preta em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT_crenca2Ligado_macro2Ligado_retake2Ligado_caminho2Ligado_ctVivoLigado
