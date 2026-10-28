# 0001 — O manager assiste à partida, não joga

- **Status:** proposta
- **Data:** 2026-10-28
- **Autor:** miguelkapicius
- **Seções afetadas:** gdd/00-visao.md, gdd/01-core-loop.md, gdd/02-mecanicas.md
- **Substitui:** —

## Contexto
Precisamos definir o papel do jogador durante a partida.

## Opções consideradas
1. **Assistir como transmissão** — foco na gestão e na IA; o espetáculo é o time jogando.
2. **Controlar um dos bots** — vira um FPS, compete com o próprio CS e esvazia a IA.
3. **Só resultado em texto** — barato, mas perde o principal diferencial.

## Decisão
O jogador **não controla** os bots com mouse e teclado. Ele assiste à partida como numa transmissão. Se haverá intervenções táticas (timeout, compra, estratégia), fica em aberto.

## Por quê
Sustenta os pilares "Bots que jogam como profissionais" e "Assistir é o prêmio".

## Consequências
A qualidade da IA e da câmera vira o centro do produto. Não precisamos de netcode de FPS nem de sensação de tiro jogável.
