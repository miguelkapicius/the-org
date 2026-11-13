# 0042 — Fãs em escala de milhões (mais difíceis, mais valiosos) e a folha de contrato

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md, glossario.md
- **Substitui:** os números de fãs das decisões 0037 e 0041 (início, ganhos, mínimos da Gaming House e curva do patrocínio). A estrutura (fãs não se gastam, liberam a Gaming House, mexem no patrocínio e na atração) continua.

## Contexto
O autor quer que os fãs vão além de 35 mil: alguns **milhões** no topo, mais **difíceis de conquistar**, mas com **mais benefícios**. Também quer que o contrato do jogador apareça na interface **como uma folha de contrato de verdade**.

## Opções consideradas
**Escala dos fãs**
1. **Mesma fórmula, números maiores** (circuito pesa muito mais; os mínimos sobem). Prós: simples, previsível. Contras: para chegar a milhões só joga no Elite por muitas temporadas.
2. **Crescimento composto** (ganho proporcional à base atual). Prós: milhões saem naturalmente. Contras: quem já é grande cresce sem esforço e quem cai não volta; difícil de balancear.
3. **Só mudar o texto** (mostrar "2,4 mi" sem mudar a conta). Contras: o número não significaria nada.

**Contrato na interface**
1. **Dica de texto** (como estava). Contras: não parece um contrato.
2. **Folha em papel por cima da tela**, com cláusulas numeradas em linguagem simples, carimbo (MINUTA, ASSINADO, EM VIGOR) e assinaturas. Prós: dá peso ao ato de contratar; reúne prazo, salário, vaga, cláusula e multa num lugar.

## Decisão
Fãs: opção 1. Contrato: opção 2.

## Detalhamento (proposta do agente, a validar; todos os números são provisórios)
- **Fãs, ganhos:** começa com 2.000. Mapa ganho vale 40 fãs × peso do circuito, que agora é **Amador 1, Pro 5, Elite 40** (era 1, 2,5 e 6). Fatores de adversário, sequência, série e campeonato (campeão 40 mapas, vice 15, terceiro 6) ficam como estavam. Um título do Elite rende cerca de 64 mil fãs; um do Pro, 8 mil; um do Amador, 1,6 mil.
- **Gaming House:** os níveis pedem **8 mil, 60 mil, 400 mil e 2 milhões** de fãs (eram 1,5 mil, 4 mil, 12 mil e 35 mil).
- **Patrocínio:** o fator vai de **0,8 a 3,0**, com 1,0 nos 2.000 fãs iniciais e +0,2 a cada vez que a base dobra (cerca de 1,5 com 12 mil, 1,9 com 100 mil, 2,6 com 1 milhão e o teto de 3,0 perto de 5 milhões).
- **Atração de contratados:** o fator "Torcida" vai de -1 (cerca de 600 fãs) a +1 (cerca de 640 mil), com zero em 20 mil.
- **Faixas da torcida** (só um nome na dica): Desconhecido, Local (8 mil), Regional (60 mil), Nacional (400 mil) e Mundial (2 milhões).
- **Folha de contrato:** clicar no nome/salário do jogador no Elenco abre o contrato em vigor, com **Renovar** e **Rescindir** (e **Pagar bônus** se ele pediu para sair). Na negociação, **Ver contrato** abre a **minuta** com a proposta atual e dá para enviar dali. Ao fechar a contratação, a folha **ASSINADO** aparece com **Escalar agora**. As cláusulas: 1 objeto (função e vaga), 2 vigência (de que semana a que semana), 3 remuneração (salário e luvas), 4 cláusula de rescisão, 5 multa, 6 fim do contrato e, se for o caso, 7 pedido de saída.

## Por quê
- Pilar 3: a torcida cresce com os resultados e aparece no caixa, na Gaming House e em quem aceita vir.
- Pilar 5: a folha reúne regras espalhadas em dicas num lugar só, em linguagem simples, e o botão certo fica ao lado da cláusula certa.

## Consequências
- Os mínimos pedem muitas temporadas; é preciso conferir por simulação quanto tempo leva chegar a cada faixa (ver a trajetória em `ManagerTest`).
- O patrocínio de até 3x pode desequilibrar a economia: precisa ser balanceado junto com salários.
- Salvamentos antigos têm os fãs recalculados (2.000 + 60 por mapa jogado, e pelo menos o que o nível da Gaming House pede).
- Ainda não existe: espectadores no HUD (0037), assinatura animada, versão imprimível do contrato.
