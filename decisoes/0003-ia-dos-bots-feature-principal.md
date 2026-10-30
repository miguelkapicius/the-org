# 0003 — IA dos bots é a feature principal

- **Status:** proposta
- **Data:** 2026-10-28
- **Autor:** miguelkapicius
- **Seções afetadas:** gdd/00-visao.md, gdd/02-mecanicas.md, gdd/09-tecnico.md
- **Substitui:** —

## Contexto
Bots de jogos de tiro costumam parecer robóticos. Queremos que o time pareça profissional.

## Opções consideradas
1. **IA que joga como pro** — peek, ângulos, utilitária, jogo em equipe, erro humano.
2. **IA simples + resultado calculado por estatística** — fácil, mas o que se vê não convence.

## Decisão
A IA dos bots é a **feature principal** do jogo: os bots devem abrir pixel, segurar ângulos e jogar como profissionais jogariam.

## Por quê
É o pilar 1 da visão e o que diferencia o jogo de outros managers.

## Consequências
A maior parte do risco técnico está aqui. Precisamos prototipar a IA cedo, antes de investir pesado em arte e em sistemas de manager.
