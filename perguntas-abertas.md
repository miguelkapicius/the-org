# Perguntas em aberto

Lista central de tudo que ainda falta decidir no jogo, por prioridade.
Cada pergunta também aparece em "Perguntas em aberto" da seção indicada.

**Regra:** quando uma pergunta for respondida, registre a resposta na seção (e em `decisoes/` se for relevante) e **remova a pergunta daqui e da seção** no mesmo commit. Perguntas novas entram aqui e na seção.

## 1. Prioridade — destrava o protótipo da IA

- [ ] Validar a decisão **0009** (NavMesh + IA própria, recomendação do agente) → `decisoes/0009-ia-navmesh-mais-ia-propria.md`
- [ ] Estilo de arte: realista, estilizado ou low-poly? → `gdd/06-arte.md`
- [ ] Versão da Unity e pipeline de render (URP ou HDRP)? → `gdd/09-tecnico.md`
- [ ] **IA no micro:** quanto de eficiência aceitamos perder em troca de variedade (rotas mais longas, jogadas fora do plano)? → `gdd/12-ia-micro.md`
- [ ] **IA no micro:** quanto cada bot pode se afastar do plano do IGL? Igual para todos ou pela função, agressividade e traços? → `gdd/12-ia-micro.md`
- [ ] **Retake:** qual a faixa realista de retakes com alguém vindo pelas costas? (hoje 15 % das entradas, sem flanco planejado) → `gdd/12-ia-micro.md`

## 2. Lado manager

- [ ] **Pré-partida:** o manager também configura as compras padrão do time? (veto de mapas e estratégia já decididos, 0014) → `gdd/08-ui-ux.md`
- [ ] **Funções:** o manager escolhe a função de CT de cada titular (âncora, rotação, qual bomb) ou ela sai da função de TR? → `gdd/10-interface-vs-jogo.md`
- [ ] **Contratos:** pesos da cláusula de rescisão, da multa, da chance de um rival levar o jogador e da recusa de renovar; o jogador pode pedir para sair? (decisão 0035, proposta) → `gdd/03-progressao.md`
- [ ] **Fãs:** pesos de ganho e perda, mínimos de cada nível da Gaming House e o que mais eles liberam (patrocínio, atrair contratados); fãs devem ser gastos como moeda ou só liberar? (decisão 0037, proposta) → `gdd/03-progressao.md`
- [ ] **Espectadores da transmissão:** o HUD da partida mostra quantas pessoas assistem, ditado pelos fãs. Fórmula (fãs, campeonato, momento), posição no HUD e se o adversário também tem o seu? (decisão 0037) → `gdd/08-ui-ux.md`
- [ ] **Psicólogo** (ideia 6 do autor): de quanto em quanto tempo sai o relatório de cada jogador (semanal, quinzenal, mensal)? → `gdd/11-ideias.md`
- [ ] **Psicólogo:** além da moral, quais emoções/estados o relatório mostra e como cada um muda a partida e o time? → `gdd/11-ideias.md`
- [ ] **Psicólogo:** o manager pode agir a partir do relatório (conversa, folga, promessa de vaga)? Com que custo? → `gdd/11-ideias.md`
- [ ] **Psicólogo:** pode ser contratado desde o início ou só com o nível 4 da Gaming House (onde o psicólogo já está)? → `gdd/11-ideias.md`
- [ ] **Psicólogo:** quanto o melhor psicólogo vale a mais que o pior (o tamanho do efeito de cada nível de qualidade) e quanto custa cada nível? → `gdd/11-ideias.md`
- [ ] **Psicólogo:** tem contrato (duração, multa para demitir) como os jogadores? Dá para ter mais de um? → `gdd/11-ideias.md`
- [ ] **Notificações** (ideia 7 do autor): a central substitui a lista "O que merece atenção" do painel ou fica ao lado dela? Guarda histórico e lida/não lida? → `gdd/11-ideias.md`

## 3. Escopo e produto

- [ ] **MVP:** quantos mapas? → `gdd/05-conteudo.md`
- [ ] O mapa jogável de hoje entra no pool com qual nome: "Vila" (código) ou "Forja" (interface)? O "Vila Velha" do pool continua? → `gdd/10-interface-vs-jogo.md`
- [ ] De onde vêm as estratégias de um mapa novo (hoje o banco vem de rounds pro de um mapa da Valve): gerar pela simulação, montar à mão ou outro caminho? → `gdd/10-interface-vs-jogo.md`
- [ ] **MVP:** lista de armas e utilitárias → `gdd/05-conteudo.md`
- [ ] Tamanho do escopo do primeiro lançamento → `gdd/00-visao.md`
- [ ] Haverá replays das partidas? A simulação precisa ser determinística? → `gdd/09-tecnico.md`
- [ ] Single-player apenas, ou terá modo online (ligas entre amigos, ranking)? → `gdd/00-visao.md`
- [ ] Nome do jogo e do "esporte" fictício → `gdd/00-visao.md`
- [ ] Modelo de negócio: pago, free-to-play, early access? → `gdd/00-visao.md`
- [ ] **Marketing:** confirmar a data-alvo (proposta: 24/08–14/09/2027), o Next Fest de junho de 2027 (14 a 21) e não ir ao de fevereiro → `marketing/plano-de-lancamento.md`
- [ ] **Marketing:** inscrever o jogo no BIG Festival (gamescom latam) até 15/01/2027? → `marketing/plano-de-lancamento.md`
- [ ] **Marketing:** qual a verba (anúncios, arte da cápsula, trailer) e quem cuida de redes, criadores e Discord? → `marketing/plano-de-lancamento.md`
- [ ] **Marketing:** página e material em português e inglês desde o início? → `marketing/plano-de-lancamento.md`

## 4. Balanceamento (pode ficar para depois)

- [ ] Quanto pesa cada fator (atributos, Gaming House, momento) na chance de o timeout funcionar? → `gdd/01-core-loop.md`
- [ ] Curva de custo por nível da Gaming House → `gdd/03-progressao.md`
- [ ] Quanto pesa cada fator (treino, experiência, idade) na evolução dos jogadores? → `gdd/03-progressao.md`
- [ ] Quanto deve valer o **domínio de lado** (hoje até ±8 % de habilidade) e quanto custa treinar um lado (hoje 1,8 ponto por sessão, atributo a 60 %)? → `gdd/03-progressao.md`
- [ ] **Negociação:** os pesos dos fatores (prestígio, vaga, estrutura, finanças, clima), o quanto o desejo muda luvas e salário e a paciência do jogador (valores provisórios da decisão 0026) → `gdd/03-progressao.md`
- [ ] Calendário: quantos campeonatos por temporada em cada circuito, premiações e pontos do ranking → `gdd/01-core-loop.md`
- [ ] Salários, juros, limite de empréstimo, prazo de falência e manutenção da Gaming House → `gdd/03-progressao.md`
- [ ] **Viés do lado A:** na medição da decisão 0036, com força igual o time A venceu 67 % de 40 partidas. É ruído, ou o slot A (ou o lado em que começa) tem vantagem na simulação? → `gdd/10-interface-vs-jogo.md`

## 5. Para depois

- [ ] O HUD de transmissão é fixo ou configurável? → `gdd/08-ui-ux.md`
- [ ] **Tutorial:** o tour de hoje é só uma visita às telas. Deve virar um guia por tarefas (por exemplo, "jogue a primeira série") que confere o que o jogador fez? → `gdd/08-ui-ux.md`
- [ ] **Som final:** os sons de interface são sintetizados em código (decisão 0029). Quem faz a versão final (designer, biblioteca licenciada ou manter a síntese)? → `gdd/07-audio.md`
- [ ] **Acessibilidade:** o jogo terá navegação completa por teclado (Tab/foco) e opção de reduzir animações? Dicas só no mouse precisam de alternativa? → `gdd/08-ui-ux.md`
- [ ] Mockup HTML (`prototipo/mockup-ui/`): atualizar para o visual "mesa de transmissão" ou aposentar e aprovar o visual direto pelas capturas da Unity? (muda o fluxo da decisão 0011) → `gdd/08-ui-ux.md`
- [ ] **Retratos:** hoje são 32 retratos de estúdio de jogadores fictícios gerados por IA (decisão 0027). Arte final: manter esse caminho, ilustrar à mão ou usar fotografia de referência? E expressões por estado (cansado, motivado)? → `gdd/06-arte.md`
- [ ] **Tipografia da interface:** hoje Barlow Condensed, Barlow e IBM Plex Mono (livres, OFL). Manter ou definir a identidade tipográfica do jogo junto com o nome e o logo? → `gdd/06-arte.md`
- [ ] **Backlog de arte (precisa gerar imagens, ficou para depois):** brasões dos times (hoje o protótipo usa a inicial no menu, na pré-partida, no ranking e no campeonato) e expressões nos retratos por estado do jogador (cansado, motivado, em alta). → `gdd/06-arte.md`
- [ ] **Base de jogadores** (fase posterior, depois de afinar o comportamento dos bonecos): quantos jogadores, e como será feita (à mão, gerada ou mista)? Os adversários passam a usar jogadores dessa base. → `gdd/10-interface-vs-jogo.md`
- [ ] Mundo e narrativa: o jogo tem história? → `gdd/04-mundo-narrativa.md`
- [ ] Áudio: direção musical, efeitos, voz → `gdd/07-audio.md`

## 6. Validar sugestões do agente

- [ ] Validar as propostas das decisões **0012** (calendário e formatos), **0013** (treino, cansaço e evolução), **0014** (veto e estratégia), **0015** (salários, empréstimo e falência), **0016** (níveis da Gaming House e scouts) **0017** (estratégia escolhe os planos de round pelo estilo) **0018** (timeout do manager), **0019** (traços, moral e entrosamento), **0020** (jogada da partida e MVP), **0021** (rivalidades e notícias), **0022** (playbook do manager), **0024** (força por lado TR e CT), **0025** (direção visual "mesa de transmissão"), **0026** (contratação por negociação) **0027** (retratos individuais), **0028** (primeiros passos) **0029** (sons da interface) e **0034** (atributos e formato MR12), **0035** (contratos realistas), **0036** (partida pulada calibrada) e **0037** (fãs do clube): confirmar os detalhes marcados como proposta do agente.

- [ ] Revisar os trechos marcados **[Sugestão]** no GDD (principalmente `00-visao.md` e `02-mecanicas.md`): confirmar (tirar a marca) ou apagar.
