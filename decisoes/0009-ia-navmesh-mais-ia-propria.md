# 0009 — IA dos bots: NavMesh da Unity + IA própria

- **Status:** proposta
- **Data:** 2026-10-30
- **Autor:** agente (recomendação a pedido de miguelkapicius, falta validar)
- **Seções afetadas:** gdd/09-tecnico.md
- **Substitui:** —

## Contexto
Precisamos escolher as ferramentas para a IA dos bots, que é a feature principal (0003).

## Opções consideradas
1. **ML-Agents (aprendizado por reforço)** — pode gerar comportamentos surpreendentes, mas o treino é lento e caro, o resultado é difícil de entender e ajustar, e é difícil fazer os atributos do jogador (mira, reação) mudarem o comportamento de forma previsível.
2. **NavMesh + IA própria (Utility AI / behavior tree)** — controle total, fácil de ajustar e balancear, os atributos entram direto nas fórmulas, e dá para copiar táticas de profissionais de forma explícita.
3. **Asset pronto da Asset Store** — rápido para começar, mas feito para bots genéricos de tiro, não para jogo tático de alto nível.

## Decisão
Usar o **NavMesh da Unity** para navegação e uma **IA própria** em camadas: time (estratégia), indivíduo (posição, peek, utilitária) e visão/mira (reação e erro). ML-Agents fica de fora no começo.

## Por quê
O pilar 1 exige bots que joguem como profissionais **e** que mudem conforme os atributos e o manager (pilar 3). Isso pede controle fino e previsível, que a IA própria dá e o ML-Agents não.

## Consequências
- Mais trabalho de design: precisamos descrever as táticas e comportamentos à mão.
- Prototipar cedo: um mapa simples, 5v5, só navegação + peek + mira, antes de investir em arte.
- ML-Agents pode ser reavaliado depois para comportamentos específicos (ex.: micro-movimento de peek).
