# Perguntas em aberto

Lista central de tudo que ainda falta decidir no jogo, por prioridade.
Cada pergunta também aparece em "Perguntas em aberto" da seção indicada.

**Regra:** quando uma pergunta for respondida, registre a resposta na seção (e em `decisoes/` se for relevante) e **remova a pergunta daqui e da seção** no mesmo commit. Perguntas novas entram aqui e na seção.

## 1. Prioridade — destrava o protótipo da IA

- [ ] Quais **atributos** um jogador tem e como cada um afeta a IA? → `gdd/02-mecanicas.md`
- [ ] Validar a decisão **0009** (NavMesh + IA própria, recomendação do agente) → `decisoes/0009-ia-navmesh-mais-ia-propria.md`
- [ ] Quanto dura uma partida? (MR12, MR15, outro formato) → `gdd/01-core-loop.md`
- [ ] Estilo de arte: realista, estilizado ou low-poly? → `gdd/06-arte.md`
- [ ] Versão da Unity e pipeline de render (URP ou HDRP)? → `gdd/09-tecnico.md`

## 2. Lado manager

- [ ] **Elenco:** os jogadores têm contrato (duração, renovação, cláusula)? Quantos jogadores no elenco (titulares + reservas)? → `gdd/03-progressao.md`
- [ ] **Pré-partida:** o manager também configura as compras padrão do time? (veto de mapas e estratégia já decididos, 0014) → `gdd/08-ui-ux.md`
- [ ] **Adversários:** os jogadores dos times adversários têm atributos próprios (como os do mercado) ou o adversário continua sendo só um número de força? → `gdd/10-interface-vs-jogo.md`
- [ ] **Funções:** o manager escolhe a função de CT de cada titular (âncora, rotação, qual bomb) ou ela sai da função de TR? → `gdd/10-interface-vs-jogo.md`

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

## 4. Balanceamento (pode ficar para depois)

- [ ] Quanto pesa cada fator (atributos, Gaming House, momento) na chance de o timeout funcionar? → `gdd/01-core-loop.md`
- [ ] Curva de custo por nível da Gaming House → `gdd/03-progressao.md`
- [ ] Quanto pesa cada fator (treino, experiência, idade) na evolução dos jogadores? → `gdd/03-progressao.md`
- [ ] Calendário: quantos campeonatos por temporada em cada circuito, premiações e pontos do ranking → `gdd/01-core-loop.md`
- [ ] Salários, juros, limite de empréstimo, prazo de falência e manutenção da Gaming House → `gdd/03-progressao.md`

## 5. Para depois

- [ ] Quais estilos o manager pode escolher no timeout além de agressivo e defensivo? → `gdd/01-core-loop.md`
- [ ] O HUD de transmissão é fixo ou configurável? → `gdd/08-ui-ux.md`
- [ ] Mundo e narrativa: o jogo tem história? → `gdd/04-mundo-narrativa.md`
- [ ] Áudio: direção musical, efeitos, voz → `gdd/07-audio.md`

## 6. Validar sugestões do agente

- [ ] Validar as propostas das decisões **0012** (calendário e formatos), **0013** (treino, cansaço e evolução), **0014** (veto e estratégia), **0015** (salários, empréstimo e falência) e **0016** (níveis da Gaming House e scouts): confirmar os detalhes marcados como proposta do agente.

- [ ] Revisar os trechos marcados **[Sugestão]** no GDD (principalmente `00-visao.md` e `02-mecanicas.md`): confirmar (tirar a marca) ou apagar.
