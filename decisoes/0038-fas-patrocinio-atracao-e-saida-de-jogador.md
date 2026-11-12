# 0038 — Fãs mexem no patrocínio e na atração; jogador pede para sair; rescisão na interface

- **Status:** proposta (os números de fãs foram trocados pela decisão 0039)
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md, glossario.md
- **Substitui:** — (completa a 0037 e a 0035: usa os fãs em mais dois lugares e fecha duas pontas soltas dos contratos)

## Contexto
O autor pediu: (1) os fãs também devem mexer no patrocínio e em atrair contratados; (2) completar os contratos com o jogador pedindo para sair e com a rescisão feita pelo manager.

## Opções consideradas
**Fãs no patrocínio**
1. **Multiplicador suave** (0,9 a 1,5) por escala logarítmica. Prós: cresce sempre, mas devagar; sem salto. Contras: mais um número para balancear.
2. **Degraus por faixa de fãs.** Prós: fácil de explicar. Contras: saltos bruscos.

**Jogador pedir para sair**
1. **Pedido com prazo**: jogador infeliz (ou bom parado no banco) pede para sair; o manager vende, paga um bônus de permanência ou ignora (e perde o jogador de graça). Prós: cria decisão com consequência. Contras: um estado a mais no jogador.
2. **Saída automática sem aviso.** Prós: simples. Contras: o manager não pode reagir (contra o pilar 3).

## Decisão
Opção 1 nos dois casos; a rescisão ganha um botão na interface com Desfazer.

## Detalhamento (proposta do agente, a validar; todos os números são provisórios)
- **Patrocínio:** multiplica o patrocínio semanal por `0,9 + 0,1 × log2(1 + fãs/1000)`, entre 0,9 e 1,5. Com os 1.000 fãs iniciais vale 1,0; com 7.000 vale 1,2; com 35.000, cerca de 1,5 (teto). A dica do chip de fãs mostra o valor de agora.
- **Atração:** um novo fator "Torcida" na negociação de contratação (decisão 0026): vale de -1 (cerca de 250 fãs) a +1 (16 mil fãs). Peso 1,4 para estrela, 1,0 para jovem e 0,7 para os outros. Mais fãs, menos luvas e salário pedidos e mais chance de aceitar.
- **Pedir para sair:**
  - Moral abaixo de 25: 6 % por semana. Reserva com geral 68 ou mais e moral abaixo de 45: 4 % por semana. Nunca com 5 jogadores ou menos.
  - O jogador ganha o selo **QUER SAIR** e o painel avisa. O manager tem **4 semanas**: **vender** (rende o valor normal), **pagar o bônus de permanência** (4 salários, clicando no selo; a moral sobe 15 e o pedido cai) ou ignorar.
  - Se ignorar, ele sai **de graça** no fim do prazo e os outros perdem 2 de moral. Com só 5 no elenco ele fica, mas perde 5 pontos de moral.
- **Rescisão pelo manager:** novo botão (ícone de menos, vermelho) em cada cartão do elenco: paga a multa de metade dos salários que faltam, os outros perdem 2 de moral, e o aviso tem **Desfazer** (devolve jogador, dinheiro, extrato e moral). Vender continua melhor na maioria dos casos, porque rende dinheiro em vez de custar.

## Por quê
- Pilar 3: o desempenho (fãs) e o clima do time (moral) têm efeito visível no caixa, nos contratados e no elenco.
- Pilar 5: o patrocínio e a atração aparecem como um número na dica; o pedido de saída é um selo com uma ação.

## Consequências
- Quem rende cresce em dinheiro e em talento atraído; quem rende mal perde patrocínio e não atrai estrelas (espiral para os dois lados: precisa de balanceamento).
- Moral baixa passa a ter custo real (perder jogador), não só desempenho pior.
- Ainda não existe: o jogador do mercado ou do rival pedir para sair do adversário; renegociar salário em vez de bônus; espectadores no HUD usando os fãs (0037).
