# Mecânicas

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0003, 0004, 0005, 0007, 0013, 0014, 0017

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

## Perguntas em aberto

- Quais atributos um jogador tem e como cada um afeta a IA?
