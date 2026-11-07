# 0035 — Contratos realistas: prazo, multa, cláusula e renovação

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md, glossario.md
- **Substitui:** o detalhamento de contratos da decisão 0034 (a escolha de "contrato enxuto, sem cláusula"). O resto da 0034 (atributos e MR12) continua.

## Contexto
A 0034 propôs um contrato enxuto: prazo de 1 a 3 temporadas, sem cláusula de rescisão. O autor pediu o contrário: o contrato deve ser **mais real, como na vida**, com burocracia de rescisão e multa conforme o que acontece, e **o jogador aceitar ou não** depende de tudo que já foi definido (hype/prestígio do time, estrutura, finanças, clima, vaga).

## Opções consideradas
1. **Enxuto** (0034): prazo e aviso, sem multa nem cláusula. Prós: simples. Contras: contra o pedido do autor; o clube nunca perde um jogador por dinheiro.
2. **Realista médio:** prazo de 1 a 3 temporadas escolhido na negociação, **cláusula de rescisão** paga por um rival, **multa** para o clube rescindir, **renovação** que o jogador pode recusar e **vencimento** que tira o jogador de graça. Prós: cada fato cria uma decisão (renovar, vender antes do fim, proteger quem é cobiçado); usa os fatores da 0026. Contras: um sistema novo para o manager entender.
3. **Completo:** cláusulas negociáveis uma a uma, agentes, direitos de imagem, empréstimo de jogadores, janelas de transferência. Prós: realismo máximo. Contras: peso demais para a fase atual (pilar 5).

## Decisão
Opção 2.

## Detalhamento (proposta do agente, a validar; todos os números são provisórios)
1. **Prazo:** na contratação o manager escolhe 1, 2 ou 3 temporadas. Cada jogador **prefere** um prazo: jovem (até 22) quer 3, veterano (29+) quer 1, o resto quer 2. Cada ano fora do que ele quer baixa 4 % do quanto a proposta cobre do que ele pede.
2. **Cláusula de rescisão:** fixada na assinatura em **preço de tabela × (1 + 0,5 por ano)**. Um rival pode pagar essa cláusula e levar o jogador no meio do contrato; o dinheiro entra no caixa do clube.
   - Só jogador cobiçado (geral 68 ou mais) é levado.
   - A chance por semana é 0,6 %; sobe para 4 % se ele está infeliz (moral abaixo de 40) ou parado no banco sendo bom (reserva com geral 72 ou mais).
   - Nunca leva o elenco abaixo de 5 jogadores.
3. **Multa para rescindir:** o clube que dispensa antes do fim paga **metade dos salários que faltam**; os outros jogadores perdem 2 de moral. Não rescinde sem caixa nem abaixo de 5 jogadores.
4. **Venda:** o preço de venda cai com o fim do contrato (80 % com menos de uma temporada, 50 % com menos de 13 semanas), porque quem compra leva um contrato curto.
5. **Vencimento:** com 13 semanas para o fim, aviso no painel. No fim, o jogador **sai de graça**; se sair deixaria o elenco com menos de 5, ele **fica mais um ano pedindo 15 % de salário a mais**.
6. **Renovação:** não é conversa em balões; é uma resposta calculada. Ele pede salário (o de hoje pelo geral, ajustado pelos mesmos fatores da 0026, mais 4 % por ano fora do prazo que prefere) e **30 % das luvas** de um jogador novo. **Recusa** se a moral está abaixo de 30 ou se o desejo de ficar está abaixo de -0,25 (prestígio, vaga, estrutura, finanças e clima), dizendo o motivo mais pesado. Renovar recomeça o contrato e dá +5 de moral.
7. **Mercado:** de vez em quando um jogador bom do mercado "assina com outro clube" e some; o painel avisa.

## Por quê
- Pilar 3: o contrato muda o que dá para ver (quem joga, quem sai, o caixa).
- Pilar 5: o manager decide pouco por vez (renovar ou não, qual prazo) e a conta vai para a dica.
- Dá uso ao **hype do time** (prestígio, ranking, estrutura): quem é cobiçado só renova se o clube for atraente.

## Consequências
- Novos campos no jogador (`contratoSemanas`, `clausula`) e no salvamento; salvamento antigo ganha 2 temporadas.
- **Na interface:** o prazo aparece na negociação (botão que alterna 1, 2 e 3 temporadas); o selo **RENOVAR** surge no cartão do elenco quando faltam 26 semanas ou menos, e clicar nele renova; a dica do jogador mostra prazo, multa e cláusula. **Não há botão de rescisão**: vender já cobre o caso, com mais dinheiro; a rescisão existe nas regras para uma tela futura.
- Ainda não existe: o jogador **pedir para sair** por conta própria, contrato de reserva diferente do de titular, e o adversário ter contratos.
- Conferido por testes das regras (sem a Unity); a interface não foi compilada nem vista na tela.
