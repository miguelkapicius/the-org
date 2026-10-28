# Técnico

> **Status:** rascunho · **Responsável:** — · **Decisões:** 0002, 0003, 0006

_Engine, arquitetura e restrições técnicas._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Engine e ferramentas

**Unity** (decisão 0006).

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
- Quais pacotes usar para IA e navegação (NavMesh nativo, ML-Agents, solução própria)?
- A simulação precisa ser determinística (mesmo resultado ao rever um round/replay)?
- Haverá replays das partidas?
