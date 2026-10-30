# Técnico

> **Status:** rascunho · **Responsável:** — · **Decisões:** 0002, 0003, 0006, 0008, 0009

_Engine, arquitetura e restrições técnicas._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Engine e ferramentas

**Unity** (decisão 0006).

### IA dos bots (decisão 0009 — recomendação do agente, a validar)

| Camada | O que faz | Ferramenta |
|---|---|---|
| Navegação | Caminhos pelo mapa | **NavMesh** da Unity (pacote AI Navigation) |
| Decisão de time | Estratégia do round, execução, rotação, retake | **IA própria** (Utility AI ou behavior tree) |
| Decisão individual | Posição, peek, abrir pixel, utilitária | **IA própria** |
| Visão e mira | Quem vê quem, tempo de reação, erro de mira | **Sistema próprio** (raycasts + modelo de erro) |

**ML-Agents fica de fora no começo.** Treinar bots por aprendizado de máquina é lento, difícil de controlar e difícil de ligar aos atributos dos jogadores. Pode voltar no futuro para comportamentos específicos.

### Partida pulada (decisão 0008)

O resultado de uma partida pulada é calculado por probabilidade. **[Sugestão]** Para não ficar diferente da partida assistida, calibrar essa fórmula rodando muitas partidas da IA real sem gráficos e comparando os resultados.

## Requisitos de performance

_A definir._

## Salvamento

_A definir._

## Restrições

**[Sugestão]** Ideias de arquitetura para a IA (decisão 0003), a validar:

- **Separar simulação de renderização:** a partida roda como simulação que pode ser assistida em 3D ou rodada rápido sem gráficos (para pular partidas e simular outros jogos da liga).
- **IA em camadas:** uma camada de time (estratégia do round, IGL) e uma individual (posicionamento, peek, mira, utilitária).
- **Modelo de visão e mira** com tempo de reação e erro, em vez de mira perfeita.

## Perguntas em aberto

- Versão da Unity e pipeline de render (URP ou HDRP)?
- A simulação precisa ser determinística (mesmo resultado ao rever um round/replay)?
- Haverá replays das partidas?
