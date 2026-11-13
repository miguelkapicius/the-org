@echo off
rem Comparacao: o exe 15 com as flags de teste do exe 12, SEM nada do exe 13, 14 ou 15.
rem --- rotas e coletivo
rem   rotaHumanaLigado      = cada bot escolhe a rota pela crenca dos pros (menos os 4 pelo mesmo caminho)
rem   apMorteRegiaoLigado   = nao entra em fila na regiao onde o colega acabou de morrer
rem   ajudaVistaLigado      = ajuda o colega em troca se ve, ouve o tiro ou sente que ele levou tiro
rem   ajudaSemRetakeLigado  = no retake o CT nao para pra ajudar duelo de colega (a ajuda derrubava o retake)
rem   apCrencaLigado        = crenca v2 (sim-apoio): memoria da posicao do inimigo dura mais (passou no A/B)
rem   apTrocaTardeLigado    = troca tardia (sim-apoio): um de cada vez cai bastante; reprova no plant (+4,5)
rem   apTrocaTardeC4Ligado  = (sim-apoio) conserto do tempo acabou: TR busca o C4 caido a tempo (11,8 -> 9,7%)
rem --- duelo e micro
rem   bxMaoAssentaLigado    = mao assenta antes de atirar (passou)
rem   dueloVivoLigado       = atira se mexendo de lado devagar e segue o passo na reacao
rem   duelo2Ligado          = ADAD de verdade no duelo, tiro na virada
rem   peekPlanoLigado       = peek com plano: dar uma bala e voltar, reabrir de outro angulo/agachado ou esperar ele vir
rem   rastreioLagLigado     = a mira acompanha o alvo com ~0,1 s de atraso (quem inverte o lado engana a mira)
rem   bxVisaoCorpoLigado    = mira no pedaco do corpo que aparece (sim-util)
rem --- AWP
rem   awpSaiLigado          = AWP que matou sai e segura de outro angulo
rem   iscaAwpLigado         = mostra o ombro pro AWP gastar o tiro e abre na recarga
rem --- tatica (sim-util)
rem   bxAbreNaBangLigado    = quem banga espera atras da quina e abre no estouro (passou)
rem   bxIdeiaRoundLigado    = IGL escolhe a ideia do round (rush, fake, default, controle, info), menos topo meio
rem   bxEstiloLigado        = IGL escolhe agressivo/equilibrado/defensivo lendo o inimigo
rem   bxRodaCrencaLigado    = CT roda pela crenca do que ouviu/viu
rem --- NOVO no exe 12 (pedidos do Gabriel de 10/11)
rem   armaSituLigado        = pega arma melhor do chao pela situacao (pistola no eco -> rifle/AWP do lado)
rem   c4DesvioLigado        = TR com o C4/execucao nao troca de bomb pela base CT quando acredita que tem CT la
rem   (bxC4BaseLigado ficou FORA deste: derrubava o retake do CT 29 -> 24%. Ele esta no CSManager_TESTE_C4base.bat)
rem   olhaBombLigado        = quem entra no bomb olha pra frente, nao pras costas sem motivo
rem   ppAdultoLigado        = pos-plant adulto: com vantagem joga passivo, nao vai brigar na base CT; CT pune TR sozinho em dupla
rem   (ppEscondidoLigado ficou FORA: derrubava o retake do CT de 26 pra 20%)
rem   apSomAndaLigado       = (sim-apoio) bot andando reage ao passo/tiro de flanco ou de tras (antes 25-47% nao reagia)
rem   apSomAndaV2Ligado     = (sim-apoio) o "nao ouvi" nao gruda nos passos seguintes do mesmo inimigo: 1-7% sem reacao
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado
