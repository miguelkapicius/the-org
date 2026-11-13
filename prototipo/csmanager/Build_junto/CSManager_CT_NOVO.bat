@echo off
rem CT NOVO (cerebro novo, 11/11 noite): o mesmo CSManager.exe (exe 16) com o pacote do TESTE + cerebroNovoCT.
rem So o IGL do CT antes do plant e novo; o TR, o retake e a decisao individual de cada bot ainda sao do cerebro velho.
rem O que muda no CT:
rem   - setup escolhido pelo valor (leitura do adversario, o que vem dando certo, custo de repetir), sem sorteio
rem   - cada jogador tem tarefa com parceiro: ancora, apoio (cruza com o ancora), meio
rem   - colega caiu e nao deu pra trocar: quem estava perto escolhe segurar a linha de quem matou ou recuar pro fundo do bomb,
rem     e joga molotov/HE na entrada se tiver (um so, longe do time)
rem   - giro so por informacao (2+ acreditados ou C4 visto); o ancora do outro lado so larga com o C4 visto
rem   - em vantagem segura; em desvantagem de 2+ recua e joga junto
rem Plano completo: PLANO_SIMULACAO.md na raiz do projeto. A caixa-preta grava a leitura do time (linhas C) em Logs\partidas.
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_cerebroNovoCT
