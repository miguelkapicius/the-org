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

- [ ] **Elenco:** como funcionam contratações, vendas, salários e contratos? → `gdd/03-progressao.md`
- [ ] Times e jogadores fictícios gerados pelo jogo ou criados à mão? → `gdd/05-conteudo.md`
- [ ] **Carreira:** formato dos campeonatos (MD1/MD3, grupos, mata-mata) e calendário → `gdd/01-core-loop.md`
- [ ] **Falência:** o jogador recebe avisos antes? Existe empréstimo ou outra saída antes do game over? → `gdd/03-progressao.md`
- [ ] **Evolução:** existe idade de pico e declínio? → `gdd/03-progressao.md`
- [ ] A Gaming House tem custo de manutenção mensal? → `gdd/03-progressao.md`

## 3. Escopo e produto

- [ ] **MVP:** quantos mapas? → `gdd/05-conteudo.md`
- [ ] **MVP:** lista de armas e utilitárias → `gdd/05-conteudo.md`
- [ ] Tamanho do escopo do primeiro lançamento → `gdd/00-visao.md`
- [ ] Haverá replays das partidas? A simulação precisa ser determinística? → `gdd/09-tecnico.md`
- [ ] Single-player apenas, ou terá modo online (ligas entre amigos, ranking)? → `gdd/00-visao.md`
- [ ] Nome do jogo e do "esporte" fictício → `gdd/00-visao.md`
- [ ] Modelo de negócio: pago, free-to-play, early access? → `gdd/00-visao.md`

## 4. Balanceamento (pode ficar para depois)

- [ ] Quanto pesa cada fator (atributos, Gaming House, momento) na chance de o timeout funcionar? → `gdd/01-core-loop.md`
- [ ] Quantos níveis a Gaming House tem e o que cada nível libera? → `gdd/03-progressao.md`
- [ ] Curva de custo por nível da Gaming House → `gdd/03-progressao.md`
- [ ] Quanto pesa cada fator (treino, experiência, idade) na evolução dos jogadores? → `gdd/03-progressao.md`

## 5. Para depois

- [ ] Quais estilos o manager pode escolher no timeout além de agressivo e defensivo? → `gdd/01-core-loop.md`
- [ ] O HUD de transmissão é fixo ou configurável? → `gdd/08-ui-ux.md`
- [ ] Mundo e narrativa: o jogo tem história? → `gdd/04-mundo-narrativa.md`
- [ ] Áudio: direção musical, efeitos, voz → `gdd/07-audio.md`

## 6. Validar sugestões do agente

- [ ] Revisar os trechos marcados **[Sugestão]** no GDD (principalmente `00-visao.md` e `02-mecanicas.md`): confirmar (tirar a marca) ou apagar.
