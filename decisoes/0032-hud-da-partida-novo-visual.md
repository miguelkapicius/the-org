# 0032 — HUD da partida no visual novo, com alternador para o clássico

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O manager ganhou a direção visual "mesa de transmissão" (decisão 0025: grafite neutro, cor só com significado, cantos retos), mas o HUD da partida seguia no visual de antes: tons quentes de marrom e creme, cantos redondos e cards inteiros preenchidos de laranja ou azul. O autor quer o HUD no visual novo, mas **quer validar as duas versões** e poder voltar atrás com facilidade.

## Opções consideradas
1. **Trocar o HUD de vez** — simples, mas sem volta fácil.
2. **Dois HUDs separados** — a volta é trivial, mas dobra o código a manter.
3. **Um só HUD com dois temas** (`HudTema`) — as peças são as mesmas; só as cores e a forma dos cards mudam. A volta é trocar uma opção.

## Decisão
Opção 3. O HUD tem dois temas, **Novo** (padrão) e **Clássico**, escolhidos nas **Opções** (Partida › Visual do HUD da partida) e válidos na próxima partida.

## Detalhamento (proposta do agente, a validar)
1. **Clássico:** exatamente o de antes (marrom, creme, cantos de 10 px, sombra, card preenchido pela vida na cor do time).
2. **Novo:** é a linguagem do menu do manager dentro da partida. Grafite #0b0d10 com branco frio; laranja e azul só para os times; verde e vermelho só para estado; cantos de 3 px e **sem sombra**; a **mesma fonte** do manager (Barlow Condensed SemiBold). O **card do jogador** é grafite e tem o **retrato do jogador** (o mesmo rosto do manager; nos adversários, um rosto do conjunto escolhido pelo nome), a vida em número grande ao lado, o nome e o dinheiro, e a cor viva do time vira uma **linha fina** embaixo do card que encolhe com a vida (vermelha com pouca vida); o jogador morto fica com o retrato escurecido e a caveira. A faixa do placar fica mais fina. O minimapa, o killfeed, o C4 e o placar do fim (botões claros) seguem a mesma paleta.
3. **Como voltar atrás:** nas Opções, escolher Clássico. Para o padrão mudar de vez, trocar `HudNovo` em `Opcoes.cs` (valor padrão 1). Nenhuma peça do HUD foi apagada; as cores vêm de `HudTema.cs`.
4. **Captura para comparar:** `HudCaptura.CapturarBatch -hud_tema novo` ou `-hud_tema classico` gera as imagens em `Logs/hud`.

## Por quê
- O jogador passa a mesma interface do manager para a partida, sem mudar de linguagem no meio.
- Permite ao autor decidir vendo, sem custo de reverter.

## Consequências
- Mais uma opção a testar (duas caras do HUD). Quando uma for escolhida, a outra pode ser removida junto com `HudTema`.
- A troca vale só ao começar uma partida; não há botão para trocar no meio dela.
- O menu da vitrine do protótipo (INICIAR e SAIR) não foi redesenhado.
