# 0038 — Identidade da marca: o ponto que abre o pixel e a arte em maquete

- **Status:** proposta
- **Data:** 2026-11-10
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/06-arte.md, marketing/identidade/
- **Substitui:** —

## Contexto
O plano de marketing pede nome e logo próprios antes de abrir a página da Steam, e diz que a cápsula é o que mais pesa na conversão de visita em wishlist. O jogo ainda não tem nome nem identidade de marca. As artes do protótipo (soldados encapuzados no hub) lembram a capa de qualquer FPS tático, e a decisão 0004 pede apresentação nossa.

## Opções consideradas
1. **Soldados realistas em pose** — é o padrão do gênero, mas fica igual aos outros, depende de imagem gerada por IA (crítica feita ao concorrente) e não mostra o que o jogo tem de diferente.
2. **Só tipografia** — limpa e barata, mas não diz que existe uma partida em 3D para assistir.
3. **A simulação como arte** — a partida vista de cima em maquete (o visual do próprio jogo), com bonecos laranja e azuis, cone de visão, rota e fumaça, sobre o grafite da interface. Mostra o produto e só nós temos.

## Decisão
Opção 3, proposta do agente:
- **Logo:** o nome em Barlow Condensed ExtraBold (a fonte de título da interface, 0025), com um **ponto laranja meio escondido atrás de uma letra**: um bot abrindo pixel. Subtítulo fixo "ESPORTS MANAGER", que diz o gênero de cara.
- **Símbolo:** um bloco (a parede) e o ponto que aparece atrás dele. É a logo reduzida: ícone, avatar e favicon.
- **Cores:** as do jogo. Grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640` (TR), azul `#62a8ee` (CT) e, nas artes, as cores da maquete.
- **Arte:** maquete isométrica de um bombsite com uma execução do TR em andamento. Vetorial, gerada por `marketing/identidade/gerar.py`, sem imagem gerada por modelo.
- **Na Steam:** a cápsula leva só arte, nome e subtítulo. A frase ("Você monta o time. Eles jogam como profissionais.") fica para redes.
- **Nome:** continua em aberto. Três propostas com a logo pronta: **FULL BUY** (recomendado pelo agente: curto, fala de dinheiro e de jogo), **LIVE ON THREE** e **FRAGHOUSE**.

## Por quê
- Pilar 1 (bots que jogam como profissionais): abrir pixel é o gesto que resume a IA, e vira a marca.
- Pilar 2 (assistir é o prêmio): a arte é a própria partida, vista como o manager a vê.
- Pilar 4 (identidade própria) e decisão 0004: sem soldados, armas ou qualquer forma que lembre a marca de outro jogo.
- Marketing: nome curto e grande na cápsula pequena, gênero escrito no subtítulo, e as mesmas cores e fontes do jogo para a página, o trailer e as capturas parecerem uma coisa só.

## Consequências
- O nome escolhido precisa ser conferido na Steam, em domínio e em registro de marca antes de qualquer material público. O agente só fez uma busca rápida na web: não achou jogo chamado "Full Buy" nem "Fraghouse"; "Live on Three" foi um programa de esports encerrado em 2013.
- Trocar o nome não refaz nada à mão: `gerar.py "NOVO |NOME"` regenera todas as peças.
- Falta decidir se estas artes vão como arte final ou servem de referência para um artista refazer a cápsula (o plano de marketing reserva verba para isso).
- As artes do hub (soldados encapuzados) ficam fora do material público; trocá-las dentro do jogo é outra conversa.
- Os brasões dos times e os retratos seguem pendentes em `gdd/06-arte.md`.
