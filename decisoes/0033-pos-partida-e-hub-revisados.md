# 0033 — Pós-partida e hub do Painel revisados

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O pós-partida e o hub do Painel eram as telas mais antigas da interface. O pós-partida tinha a tabela de desempenho encolhida no lado esquerdo (metade da largura vazia), o MVP e a jogada espalhados em linhas soltas, sem destaque, e os botões no fim, que com 5 jogadores obrigavam a rolar. No hub, quando não havia série marcada, o cartão principal repetia a mesma ilustração do Calendário, e as notícias repetiam "Semana N:" em toda linha.

## Opções consideradas
1. **Só ajustar espaçamento** — não resolve a tabela vazia nem os destaques perdidos.
2. **Reorganizar as duas telas na linguagem dos cartões já usada em Finanças, Gaming House e Scouts** — faixa de resultado, cartões de destaque e tabela na largura toda.
3. **Pós-partida com gráficos por jogador** — mais enfeite que informação; fica para depois, se fizer falta.

## Decisão
Opção 2.

## Detalhamento (proposta do agente, a validar)
**Pós-partida**
1. **Faixa do resultado** com uma barra lateral verde (vitória) ou vermelha (derrota): campeonato, mapa e se foi assistida ou pulada; o placar grande no meio; e à direita a série (placar, o que vem depois, os mapas já jogados como selos e a força dos dois times).
2. **Dois cartões de destaque:** **MVP do mapa** (nome, função, abates, K/D e dano por round; mais o MVP da série quando acaba) e **Jogada da partida** (só na assistida). Num mapa pulado o MVP fica sozinho na metade esquerda.
3. **Tabela de desempenho na largura toda**, com a linha do MVP tingida de verde, a coluna "1º abate" renomeada para **Aberturas** e uma coluna nova, **Nota** (a mesma nota que já decide o MVP, agora visível). Cabeçalhos com dica ao passar o mouse.
4. As linhas ficam mais baixas, para a tela e os botões (Próximo mapa, Pular o resto da série, Ver finanças) caberem sem rolar.

**Hub do Painel**
5. O cartão principal usa **sempre a arte da série**, nunca a do Calendário.
6. **Selo no canto** de Treino, Calendário e Mercado com quantos avisos pedem atenção naquela tela (o detalhe continua na lista "O que merece atenção").
7. Títulos longos (nome de campeonato) usam fonte menor, em vez de quebrar em três linhas.
8. **Notícias:** "Semana 12:" vira uma etiqueta "S12" cinza, e o texto fica sozinho.

## Por quê
- Pilar 5: a informação certa em destaque, sem procurar. A nota que decide o MVP deixa de ser invisível.
- Consistência com as telas já revisadas (decisão 0025).

## Consequências
- Só muda a apresentação: nenhuma regra de jogo muda.
- Conferido só por capturas em 1600×900; falta olhar em 1024×768 e com o mouse.
