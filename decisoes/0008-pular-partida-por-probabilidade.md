# 0008 — Partida pode ser pulada, com resultado por probabilidade

- **Status:** proposta
- **Data:** 2026-10-30
- **Autor:** miguelkapicius
- **Seções afetadas:** gdd/01-core-loop.md, gdd/09-tecnico.md
- **Substitui:** —

## Contexto
Assistir toda partida em 3D toma tempo. Precisávamos decidir se dá para pular.

## Opções consideradas
1. **Pular com resultado calculado por probabilidade** — rápido, permite simular a liga inteira.
2. **Pular rodando a simulação completa sem gráficos** — mais fiel, mas mais lento e pesado.
3. **Não permitir pular** — mais simples, mas cansativo numa carreira longa.

## Decisão
O manager pode **pular** uma partida. O resultado é **calculado por probabilidade**, usando os mesmos fatores da partida assistida (atributos, Gaming House etc.).

## Por quê
Mantém o ritmo da carreira e permite calcular os jogos dos outros times da liga.

## Consequências
A fórmula precisa dar resultados parecidos com os da partida assistida; senão, o jogador vai preferir sempre pular (ou sempre assistir) para ganhar vantagem. Sugestão: calibrar a fórmula com muitas partidas da IA real rodadas sem gráficos.
