# 0036 — Partida pulada calibrada com a simulação

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/10-interface-vs-jogo.md, gdd/09-tecnico.md
- **Substitui:** — (cumpre a sugestão da decisão 0008: calibrar a fórmula rodando muitas partidas)

## Contexto
A partida pulada (decisão 0008) usava uma curva logística de escala 6,0 inventada e um placar sorteado de 13 x 3 a 13 x 11, sem nunca dar 13 x 12 nem prorrogação. O autor pediu calibrar isso com uma simulação.

## Opções consideradas
1. **Manter os números inventados.** Prós: nada a fazer. Contras: pular uma partida dá resultados diferentes de assistir, contra o pilar 3.
2. **Medir com partidas inteiras da IA e ajustar** a escala da curva e a distribuição de placares. Prós: pular e assistir ficam parecidos. Contras: a medição é lenta (cerca de 1 minuto por partida) e vale para a força e a IA de hoje; muda quando os bonecos ou a base de jogadores mudarem.

## Decisão
Opção 2.

## Detalhamento (proposta do agente, a validar)
Medição com `Headless -chance 40` (40 partidas por diferença, força base 65, MR12 com prorrogação; ferramenta `MedirChance.cs`):
- **Escala da curva:** medida em cerca de **9,7**; passa de 6,0 para **10,0** (`Mundo.ESCALA_CHANCE`). Com 10 pontos de diferença o favorito vence ~73 % dos mapas. Vale também para os campeonatos entre adversários.
- **Placar:** o perdedor faz em média ~8 rounds (antes ~7). Faixas do perdedor em jogo equilibrado: 0-3 (5 %), 4-6 (22 %), 7-9 (35 %), 10-11 (26 %), **12 (13 x 12, 12 %)**. Com favorito muito folgado (chance acima de 95 %): 28 %, 38 %, 20 %, 14 % e 0 %. Entre os dois, mistura proporcional. Prorrogação é registrada como 13 x 12.
- **Limites da medição:** 40 partidas por ponto têm erro de vários pontos percentuais. Com diferença 0 o time A venceu 67 % (o esperado seria 50 %), sinal de um viés de lado ou de ruído; fica para investigar. A escala 10 é uma média, não um valor firme.

## Por quê
- Pilar 3: pular e assistir precisam contar a mesma história.
- Os números saem de dados, não de palpite, e a ferramenta fica para refazer quando a IA mudar.

## Consequências
- `Carreira.PlacarPerdedor` sorteia o placar; `Mundo.Chance` usa a nova escala.
- Rodar `-chance` de novo depois de mudar a IA ou o balanço de armas.
- Nova pergunta: por que o time A ganha mais com diferença 0 (viés de lado do slot)?
