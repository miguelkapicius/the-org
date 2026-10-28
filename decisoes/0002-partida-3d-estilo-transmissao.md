# 0002 — Partida em 3D com cara de demo/transmissão

- **Status:** proposta
- **Data:** 2026-10-28
- **Autor:** miguelkapicius
- **Seções afetadas:** gdd/00-visao.md, gdd/06-arte.md, gdd/08-ui-ux.md, gdd/09-tecnico.md
- **Substitui:** —

## Contexto
Como apresentar a partida ao jogador.

## Opções consideradas
1. **3D completo estilo demo/GOTV** — imersivo, parece um campeonato de verdade.
2. **2D visto de cima** — mais barato, menos espetacular.
3. **Só texto/estatísticas** — muito mais barato, perde o diferencial.

## Decisão
A partida é renderizada em **3D**, com visual de demo/transmissão: câmera seguindo jogadores, primeira pessoa, vida dos jogadores, minimapa. Texturas e modelos próprios.

## Por quê
Sustenta o pilar "Assistir é o prêmio".

## Consequências
Custo alto de arte 3D (mapas, personagens, armas, animações). Exige animações críveis para os bots parecerem humanos.
