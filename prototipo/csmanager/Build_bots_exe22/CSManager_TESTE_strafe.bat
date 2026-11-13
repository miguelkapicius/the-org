@echo off
rem TESTE do exe 15 + strafadinha de quem segura (apGingaBase040). Fora do TESTE principal: erra menos tiro seguido,
rem mas o TR planta mais (+4,8 pontos).
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
rem --- do exe 13 (duelo): cobFim (volta pra cobertura ate o fim), menorNumero (sozinho contra 2+ da a rajada e sai),
rem   assentaFisica, reacaoConhecido, balaPerto, bangViraCurto (agora tambem abre CORRENDO na propria flash), esperaOlhaPerigo, maoAmeaca090
rem --- NOVO no exe 14 (sessao a fundo, 11/11)
rem   quinaSeguraLigado  = quem segura escolhe o ponto de onde so a cabeca aparece (antes mostrava o corpo inteiro em 87 pct dos duelos):
rem                        1o abate do CT 48,8 -> 52,2 pct (pro 60), TR 52,7 -> 49,4 pct, retake +3,7
rem   quinaPeekLigado    = no par cobertura+peek, prefere o peek que mostra menos corpo
rem   retakeFirmeLigado  = no retake, depois do "entra", o CT nao sai e reabre (retake com vantagem +3,7)
rem   semTRDesarmaLigado = sem TR vivo, quem guardava a arma volta e alguem vai desarmar correndo (o Rocha com kit parado e a bomba explodindo)
rem   fugaEspalhaLigado  = fugindo da bomba / guardando arma, cada um escolhe um canto (colados no mesmo canto: 61 -> 14 pct dos rounds)
rem   validador de jeito humano (200 rounds): total 353 -> 377 cenas/100 rounds, "mira na parede" 29 -> 44, sem uma flag culpada (37-44 tirando
rem                        qualquer uma). O OLHAR segue sendo o ponto fraco: depende da crenca por jogador, que e o proximo item grande.
rem --- NOVO no exe 15 (sessao a fundo, 11/11 tarde)
rem   raioRampaLigado    = o bot achava que escada/rampa subindo era parede e virava a mira pra outro lado; agora o raio segue o relevo
rem   abertoVoltaLigado  = parado encostado na parede com o som/call do outro lado dela: procura a volta inteira pelo lado aberto
rem   armaFimRoundLigado = round acabado, quem sobrou pega a arma mais cara do chao (o TR de MAC-10 com a AWP caida)
rem   "mira na parede" (validador, regua justa sem escada/cego/pezinho): 30,5 -> 23 por 100 rounds. Equilibrio neutro.
rem   CAIXA-PRETA (sempre ligada, com ou sem flags): o jogo grava cada round em Logs\partidas\<data-hora>\ pra sessao dos bots abrir
start "" "%~dp0CSManager.exe" -exp rotaHumanaLigado_apMorteRegiaoLigado_ajudaVistaLigado_ajudaSemRetakeLigado_apCrencaLigado_apTrocaTardeLigado_apTrocaTardeC4Ligado_bxMaoAssentaLigado_dueloVivoLigado_duelo2Ligado_peekPlanoLigado_rastreioLagLigado_bxVisaoCorpoLigado_awpSaiLigado_iscaAwpLigado_bxAbreNaBangLigado_bxIdeiaRoundLigado_bxEstiloLigado_bxRodaCrencaLigado_armaSituLigado_c4DesvioLigado_olhaBombLigado_ppAdultoLigado_apSomAndaLigado_apSomAndaV2Ligado_cobFimLigado_menorNumeroLigado_assentaFisicaLigado_reacaoConhecidoLigado_balaPertoLigado_bangViraCurtoLigado_esperaOlhaPerigoLigado_maoAmeaca090_retakeFirmeLigado_semTRDesarmaLigado_quinaSeguraLigado_fugaEspalhaLigado_quinaPeekLigado_raioRampaLigado_abertoVoltaLigado_armaFimRoundLigado_apGingaBase040
