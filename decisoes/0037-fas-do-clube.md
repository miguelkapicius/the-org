# 0037 — Fãs do clube: moeda de progressão que libera a Gaming House

- **Status:** proposta (os números de fãs foram trocados pela decisão 0042)
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md, glossario.md
- **Substitui:** — (complementa 0007 e 0016: o nível da Gaming House passa a pedir fãs além de dinheiro)

## Contexto
O autor quer um sistema de fãs: cada jogo e cada resultado do clube, conforme o momento, ganha (ou perde) fãs. É um parâmetro de progressão que desbloqueia coisas, como os upgrades da Gaming House, e aparece na interface principal ao lado do dinheiro, como uma moeda.

## Opções consideradas
1. **Fãs como requisito (limiar), não gastos.** A base conquistada libera níveis; o dinheiro continua pagando. Prós: o jogador não "perde fãs" ao comprar, o que seria estranho; premia consistência. Contras: não é uma moeda de troca no sentido estrito.
2. **Fãs gastos como moeda** (cada upgrade consome fãs). Prós: moeda de verdade. Contras: gastar a torcida soa contraintuitivo; um mau momento depois de comprar pode quebrar o clube.
3. **Fãs só alimentam o patrocínio** (sem desbloquear). Prós: simples. Contras: não é progressão, contra o pedido do autor.

## Decisão
Opção 1, mostrada como uma segunda "carteira" ao lado do saldo. Se o autor quiser gastar fãs, trocar para a opção 2 é uma mudança pequena (`SubirGamingHouse` desconta em vez de só conferir).

## Detalhamento (proposta do agente, a validar; todos os números são provisórios)
- **Início:** 1.000 fãs.
- **Mapa ganho:** 40 fãs × peso do circuito (Amador 1, Pro 2,5, Elite 6) × fator do adversário (de 0,5 a 2: cada 20 pontos de força a mais do rival dobra) × momento (+10 % por mapa de sequência de vitórias, até 5).
- **Mapa perdido:** tira metade disso, e mais (até o dobro) se o rival é mais fraco; sequência de derrotas aumenta a perda em até 50 %.
- **Série vencida:** + 3 mapas ganhos. **Campeonato:** campeão 40, vice 15 e terceiro 6 mapas ganhos; fora do pódio nada.
- **Nunca abaixo de 0.** Vale também nos mapas pulados.
- **Gaming House:** nível 2 pede 1.500 fãs, nível 3 pede 4.000, nível 4 pede 12.000 e nível 5 pede 35.000, além do custo em dinheiro. Os fãs não são gastos.
- **Interface:** chip "FÃS" ao lado do saldo, com dica (como ganhar e o que falta para o próximo nível); a Gaming House mostra o requisito e quanto falta; o pós-partida mostra "+N fãs".
- **Salvamento:** guarda fãs e sequência; salvamento antigo estima 1.000 + 40 por mapa jogado (e pelo menos o que o nível atual pede).

## Por quê
- Pilar 3: vencer na partida aparece no progresso do clube.
- Pilar 5: um número só, com a conta na dica.
- Dá ligação entre desempenho esportivo e crescimento do clube, sem depender só de dinheiro.

## Consequências
- Quem joga mal fica preso nos níveis baixos mesmo com caixa cheio: precisa render para crescer.
- Não faz nada ainda: fãs afetando patrocínio, preço de ingresso, atração de contratados (o prestígio da 0026 pode passar a usar fãs).
- **Espectadores no HUD (pedido do autor, ainda não implementado):** o número de pessoas assistindo à transmissão, mostrado no HUD da partida, é ditado pelos fãs do clube (e varia com o momento e o campeonato). Fórmula, posição no HUD e se vale para o adversário ficam em aberto.
- Os números precisam de balanceamento com partidas longas (quantas temporadas até o nível 5).
