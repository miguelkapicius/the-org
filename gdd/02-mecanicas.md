# Mecânicas

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0003, 0004, 0005, 0007, 0013, 0014, 0017, 0019, 0022, 0024

_Regras e sistemas do jogo, com números quando possível._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Controles

Na partida, o jogador controla a **transmissão**, não os bots (decisão 0001):

- trocar de jogador acompanhado (primeira pessoa);
- **[Sugestão]** câmera livre / câmera de diretor automática;
- **[Sugestão]** velocidade da partida (1x, 2x, pular round).

## Movimento

_Movimento dos bots: ver "IA dos bots" abaixo._

## Sistemas principais

### Partida (simulação)
- Formato 5v5, ataque x defesa, **modo bomba igual ao do CS** (decisão 0005): o ataque planta a bomba em um dos bombsites, a defesa impede ou desarma. Vence o round quem eliminar o outro time, explodir a bomba, desarmar a bomba ou (defesa) segurar até o tempo acabar.
- Economia do round **com os mesmos valores do CS** (decisão 0005): dinheiro por vitória, derrota, abates e objetivo; compra de armas e utilitárias.

### IA dos bots (feature principal, decisão 0003)
Os bots devem jogar como profissionais jogariam. Comportamentos que precisam existir:

- **Abrir pixel / jiggle peek**, segurar ângulo, pré-mirar onde o inimigo vai aparecer.
- **[Sugestão]** Posicionamento por função: entry, suporte, AWPer/sniper, lurker, IGL (líder).
- **[Sugestão]** Uso de utilitárias: smoke, flash, molotov em lugares que fazem sentido taticamente.
- **[Sugestão]** Jogo em equipe: trade kill, rotação, execução de estratégias, retake.
- **[Sugestão]** Erro humano crível: tempo de reação, mira imperfeita, decisões ruins sob pressão.
- **[Sugestão]** Atributos do jogador mudam como o bot joga (mira, reação, posicionamento, utilitária, comunicação).

### Gestão do time
- **Economia do time** (dinheiro do clube, salários, prêmios).
- **Gaming House** do time: o nível dela libera outras melhorias, como treinos e scouts. Detalhes em `03-progressao.md` (decisão 0007).
- **[Sugestão]** Elenco: contratar, vender, treinar jogadores com atributos.

## Regras e fórmulas

_A definir._

## Condições de vitória e derrota

_A definir (de partida e de carreira)._

## Treino, cansaço, afinidade de mapa e estratégia (decisões 0013 e 0014)

O que o manager configura vira **habilidade na partida**: o **preparo** do treino soma, o **cansaço** subtrai, a **afinidade** com o mapa jogado soma ou subtrai, e a **estratégia** escolhida ajusta os pesos da IA do time (agressividade, hora de executar, quanto segurar). Assim tudo aparece no que os bots fazem (pilar 3). Números provisórios.

### Estratégia escolhe os planos de round (decisão 0017)

A IA joga cada round seguindo um **plano de round** tirado de um banco de rounds pro. Cada plano é **classificado** em um dos 5 estilos da estratégia, e o IGL **prefere** os planos do estilo que o manager escolheu (sem proibir os outros, para o time não ficar previsível). Equilibrada não tem preferência.

**[Sugestão]** Como cada plano ganha um estilo (pelo que o time fez no round pro; nota em desvios da média do lado, mínimo 0,6, senão fica Equilibrada):

| Estilo | Ataque (TR) | Defesa (CT) |
|---|---|---|
| Agressiva | muitas mortes nos primeiros 25 s e presença cedo nas áreas disputadas | avança para o lado do TR e troca tiro cedo |
| Defensiva | entra tarde, quase sem contato cedo e sem brigar pelo meio | fica nos bombs, sem avançar |
| Execuções rápidas | entra no bomb cedo, sem trocar tiro antes | **rotação rápida**: jogadores passam de um bomb para o outro antes dos 50 s |
| Controle de mapa | entra tarde depois de muito tempo no meio e nas áreas disputadas | muito tempo no meio nos primeiros 30 s |

Banco atual (1286 planos): TR 116 agressivos, 102 defensivos, 206 rápidos, 185 de controle, 34 equilibrados; CT 139, 236, 87, 105 e 76.

**Medido** (`Headless -estilos`, 18 partidas por estilo, time A com o estilo contra B Equilibrada, ~400 rounds cada):

| Estilo do time | Rounds TR com plano do estilo (antes → depois) | Plant médio do TR (depois) | Rounds CT com plano do estilo (antes → depois) |
|---|---|---|---|
| Equilibrada | — | 64 s | — |
| Agressiva | 22 % → 55 % | 56 s | 22 % → 63 % |
| Defensiva | 16 % → 50 % | 69 s | 38 % → 78 % |
| Execuções rápidas | 37 % → 74 % | 54 s | 10 % → 47 % |
| Controle de mapa | 29 % → 63 % | 68 s | 10 % → 50 % |

Antes da mudança o plant médio ficava entre 56 s e 64 s para todos os estilos. As vitórias por lado não mudaram além do ruído da amostra. **Ponto a olhar:** o CT agressivo usa mais planos agressivos, mas não morreu mais cedo que o Equilibrado (0,52 contra 0,54 mortes antes dos 25 s); pode ser que o cérebro do bot recue antes de chegar ao avanço do plano.

### Playbook do manager (decisão 0022)

**[Sugestão]** Os planos de round do banco são agrupados em **jogadas** com nome legível: no ataque pelo tipo, bomb e rota de entrada (ex.: "Rush A pela Rampa", "Split A (Palácio + Rampa)"); na defesa pela distribuição aos 20 s (ex.: "Setup 2-1-2 com avanço"). Hoje são 21 por lado com 5 ou mais planos. O manager marca jogadas **favoritas** (peso 4 na escolha do IGL) e **proibidas** (o IGL não escolhe; se proibir tudo o que cabe numa compra, ignora a proibição naquele round). Vagas por lado: 1 no nível 1 da Gaming House, +1 por nível, até 5. O playbook combina com a estratégia e continua depois de um timeout. Na tela, cada jogada abre a planta do mapa com as rotas dos 5 jogadores de um exemplo típico e os pontos fortes e fracos tirados dos dados. **Medido** (16 partidas): favorita do ataque de 8,2 % para 22,9 % dos rounds, da defesa de 9,3 % para 25,0 %; proibida de 12,1 % para 0 %.

## Personalidade dos jogadores (decisão 0019)

**[Sugestão]** Além dos 5 atributos, cada jogador tem **moral** e **entrosamento** (0 a 100), e cerca de metade tem um **traço**. Tudo muda a simulação (pilar 3). Números provisórios.

**[Sugestão]** Cada jogador também tem um **domínio de lado** (decisão 0024): um número de 0 a 100 para o TR e outro para o CT (50 é neutro). Na simulação, a mira, o reflexo e o sangue-frio dele mudam até ±8 % a cada 50 pontos de diferença para a média dos dois lados, conforme o lado do round (e o lado troca no half). A média dos dois é a força dele, então o geral e a chance de vitória não mudam: muda quem rende mais em cada metade da partida. Todo jogador nasce com uma especialidade sorteada (até ±20 pontos).

| Traço | Efeito na partida |
|---|---|
| Decisivo | +15 % de sangue-frio em match point, último round do half e prorrogação |
| Esquentado | No round seguinte a um em que morreu primeiro: mira 8 % pior e mais agressivo |
| Líder | Escalado, +5 % de sangue-frio para os outros 4 titulares |
| Estrela | Mira 6 % melhor sempre; troca menos os colegas; a moral cai o dobro no banco |
| Pistoleiro | +12 % de mira e reflexo nos rounds de pistol |
| Embalado | +6 % de reflexo quando o time venceu os 3 últimos rounds |

- **Moral:** +3 por mapa vencido, -3 por mapa perdido (titulares); por semana +1 titular, -2 reserva. Vale de -4,5 % (0) a +3 % (100) na habilidade; neutra em 60.
- **Entrosamento:** por semana +5 titular, +1 reserva, +3 por sessão coletiva (titulares); contratado chega com 20. A média dos titulares muda a chance de um bot **trocar** o colega que morreu (×0,6 a ×1,4, neutra em 50), a distância de onde ele vai trocar (340 a 500) e a força (±2 %). **Medido** (24 partidas por valor): mortes trocadas em até 5 s vão de 21,0 % (entrosamento 10) a 23,2 % (50) e 25,1 % (90); nas vitórias por round o efeito ainda não aparece além do ruído.
- Os traços dos jogadores do mercado só aparecem com scout. O adversário ainda não tem traços (não tem jogadores de verdade).

## Perguntas em aberto

- Quais atributos um jogador tem e como cada um afeta a IA?
