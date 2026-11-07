# 0025 — Direção visual "mesa de transmissão"

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/06-arte.md, gdd/08-ui-ux.md
- **Substitui:** a direção visual "vidro noturno" descrita em gdd/08-ui-ux.md (2026-11-06). Mexe também na regra da decisão 0023 que dizia "ação principal em índigo": agora é o botão sólido claro.

## Contexto
Depois do "vidro noturno" e das telas enxutas (0023), o autor ainda achou a interface com "muita cara de IA" e pediu componentes mais limpos, com cara de interface moderna e não genérica. Olhando as capturas, o que denunciava isso era: fundo azul-roxo com estrelas e brilhos; índigo como única cor de destaque; tudo dentro de caixas com borda; menu em cartão com barra colorida na lateral; títulos de cartão em cima do rosto da arte; uma única fonte; avatares coloridos sem função.

## Opções consideradas
1. **Ajustar o vidro noturno** (menos brilho, menos borda) — barato, mas mantém o céu azul-roxo e o índigo, que são justamente o ar de "gerado".
2. **Grafite neutro com tipografia em três vozes** — tira a cor decorativa, usa claro sobre escuro para a ação e deixa a cor só para o que tem significado no jogo. Mais trabalho (fontes, estilos), resultado mais próprio.
3. **Tema claro** — foge do clima de transmissão e das artes escuras do jogo.

## Decisão
Opção 2, chamada **mesa de transmissão** (a interface parece a mesa de operação de uma transmissão de campeonato):
- **Neutros:** fundo grafite (#0b0d10) sem degradê, estrelas ou brilho; superfícies são **preenchimento suave sem borda**; borda só aparece em algo escolhido ou em foco; divisores de uma linha em vez de caixas dentro de caixas.
- **Uma cor de ação:** a ação principal de cada tela é o **botão sólido claro** (texto escuro); as demais são contorno. Nada de barra colorida na lateral de itens ou cartões: o item escolhido do menu é só um fundo mais claro.
- **Cor só com significado:** laranja = TR e o nosso time, azul = CT e o adversário; verde, laranja e vermelho = estado e nota. Avatares neutros.
- **Três vozes tipográficas** (todas livres, licença OFL): **Barlow Condensed** nos títulos, nomes e números grandes (cara de placar de transmissão); **Barlow** no texto; **IBM Plex Mono** nos números das tabelas (colunas alinhadas).
- **Formas:** cantos de 2 a 4 px, barras finas (3 a 4 px), selos pequenos e retos; o número geral do jogador vira um selo quadrado, não um círculo.
- **Artes** (hub e retratos TR/CT, decisões 0023 e 0024): saturação reduzida para combinar com o grafite; o título do cartão do hub é curto, para não cair em cima de rostos.
- **Ficha do jogador** (0024): a foto sangra à direita e some para o grafite à esquerda; o nome em condensado grande é a única ênfase da tela.

## Por quê
- Pilar 5 (complexo por dentro, simples de entender): menos caixas e menos cor decorativa deixam a conclusão e o número em destaque.
- Pilar 2 (assistir é o prêmio) e 4 (identidade própria): o visual de mesa de transmissão conversa com a partida em 3D, e a dupla TR (laranja) e CT (azul) é do nosso assunto, não de um modelo genérico.
- Referências de design usadas: as diretrizes de "evitar design gerado por IA" (neutros escolhidos, tipografia pareada, cartão só onde o papel pede, estrutura que carrega informação) e as imagens de referência dos autores (G.Take, lobby de jogo, menu de CS:GO, app de estudos).

## Consequências
- `Manager.uss` ganha uma camada de refino no fim do arquivo; as cores com transparência foram **convertidas para a mistura linear da Unity** (branco a 4 % aparecia como cinza de 18 %); quem criar cor nova com transparência deve conferir na captura.
- As fontes ficam em `Assets/Resources/ManagerUI/Fontes/` com os textos das licenças OFL.
- O fundo estrelado gerado em código foi removido.
- O mockup HTML (`prototipo/mockup-ui/`) continua no visual antigo (pergunta já aberta).
- Os cartões do hub, o painel e as outras telas foram conferidos só por captura de tela, não com o mouse.
