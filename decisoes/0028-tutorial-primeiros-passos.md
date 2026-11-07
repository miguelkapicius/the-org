# 0028 — Primeiros passos (tour guiado)

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O pilar 5 diz que o jogo deve ser "extremamente simples de aprender e entender", mas quem abre o jogo pela primeira vez não recebe nenhuma explicação: são muitas telas e sistemas (treino, mercado por negociação, caixa, séries).

## Decisão
- Um **tour guiado de 6 passos**, sem bloqueio, que leva o jogador pelas telas principais na primeira vez.

## Detalhamento (proposta do agente, a validar)
1. **Os passos:** (1) Painel: o que é o jogo e o cartão da série; (2) Elenco: escalar os 5 titulares e ver o efeito antes de trocar; (3) Treino: foco, sessões, cansaço e a ficha do jogador; (4) Mercado: contratar negociando; (5) Finanças: salários, falência e empréstimo; (6) Série: vetar mapas, estratégia, assistir ou pular. Sem série marcada, o último passo aponta o Calendário.
2. **Como aparece:** um cartão no canto da tela com o passo, o progresso em traços e os botões **Próximo** (leva à tela do passo seguinte), **Ir para a tela** (se o jogador estiver em outra) e **Pular tutorial**. O **ícone da tela do passo pulsa** no menu. Nada trava: o jogador pode usar o jogo normalmente com o cartão aberto. Os avisos temporários sobem para não cobrir o cartão.
3. **Reiniciar:** o botão de ajuda no fim do menu recomeça o tour a qualquer momento.
4. **Salvo na carreira:** o passo atual vai no salvamento; salvamentos antigos não mostram o tour. Uma carreira nova começa com ele.

## Por quê
- Pilar 5: ensina o caminho principal em um minuto, com a explicação no lugar onde a coisa acontece, e sem prender o jogador.

## Consequências
- Os textos são provisórios. O tour é uma visita às telas, não um guia por tarefas (ainda não pede "jogue a primeira série" e verifica).
- Quando entrar uma tela nova importante, o tour precisa de um passo novo.
