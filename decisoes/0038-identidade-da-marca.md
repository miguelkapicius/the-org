# 0038 — Nome THE ORG e identidade da marca: o O que vira símbolo e padrão

- **Status:** proposta
- **Data:** 2026-11-10
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/00-visao.md, gdd/06-arte.md, marketing/identidade/
- **Substitui:** —

## Contexto
O plano de marketing pede nome e logo próprios antes de abrir a página da Steam, e diz que a cápsula é o que mais pesa na conversão de visita em wishlist. O jogo não tinha nome nem identidade de marca. As artes do protótipo (soldados encapuzados no hub) lembram a capa de qualquer FPS tático, e a decisão 0004 pede apresentação nossa.

## Opções consideradas
**Nome.** O agente propôs FULL BUY, LIVE ON THREE e FRAGHOUSE, com a logo de cada um. O autor preferiu **THE ORG**, o nome que o repositório já tinha.

**Arte.**
1. **Soldados realistas em pose** — é o padrão do gênero, mas fica igual aos outros, depende de imagem gerada por IA (crítica feita ao concorrente) e não mostra o que o jogo tem de diferente.
2. **Só tipografia** — limpa e barata, mas não diz que existe uma partida para assistir.
3. **O mapa em maquete colorida** (o visual do jogo em 3D, com casas e bonecos) — foi a primeira versão. **Recusada pelo autor:** não gostou da "casinha" e pediu mais cara de FPS tático, formação e manager.
4. **A prancheta tática em 3D** (o round em blocos de grafite, em perspectiva) — foi a segunda versão. **Recusada pelo autor:** não gostou do 3D nos banners e pediu para estudar o que deu certo em outros jogos.
5. **O radar dentro do O** (o round visto de cima, chapado, dentro do O da logo ampliado) — foi a terceira versão. **Recusada pelo autor:** ainda não gostou dos banners e pediu outro conceito, como um padrão que combine com o resto.
6. **O padrão** (o O da logo repetido em malha, com alguns acesos em laranja e em azul) — foi a quarta versão. O autor pediu para pôr capturas do jogo nos banners.
7. **Captura do jogo no fundo** — a imagem do jogo rodando, escurecida do lado do texto, com o nome por cima. Mostra o produto de verdade.

**Ícone.** A primeira versão era um brasão de cinco lados. **Recusada pelo autor.** A segunda rodada trouxe o anel, o cone de visão e a formação; o autor não escolheu nenhuma e pediu para seguir o padrão de jogos que deram certo.

**O que se tirou de quem deu certo** (pesquisa rápida do agente em guias de cápsula da Steam e análises de marcas de FPS; a lista é resumo, não medição nossa):
- A cápsula tem de funcionar em miniatura: o nome precisa ler em 120×45.
- Um assunto dominante, paleta comprometida, título simples em alto contraste. Fundo escuro é comum entre os mais vendidos.
- Quem navega procura o gênero antes de ler o nome: cor e assunto carregam isso.
- As marcas de FPS que duram são um símbolo geométrico simples, que funciona numa cor só e sozinho.

## Decisão
- **Nome: THE ORG** (decisão do autor). "Org" é como a cena de esports chama a organização dona do time, que é o papel do manager.
- **Arte: opção 7, captura do jogo no fundo** (pedido do autor). O padrão fica para onde não há captura.
- **Logo:** o nome em Barlow Condensed ExtraBold (a fonte de título da interface, 0025). O **O de ORG é um anel com o ponto laranja aparecendo por dentro**: um bot abrindo pixel. **O nome com o O foi aprovado pelo autor.**
- **Subtítulo:** "TACTICAL FPS MANAGER" (proposta do agente). O autor recusou "ESPORTS MANAGER" e pediu outro texto, sem dizer qual; ficam como alternativas "MANAGER DE FPS TÁTICO" e "MONTE O TIME. ASSISTA À PARTIDA."
- **Símbolo:** o O da logo sozinho. **Ícone do app:** o símbolo sobre um campo laranja chapado (grafite e osso ficam como alternativas).
- **Padrão:** o símbolo repetido em malha, cada um com o ponto virado para um lado. Quase todos apagados, em grafite sobre grafite; alguns acesos em laranja (TR) e em azul (CT). A malha não entra na área do texto.
- **Cores:** as da interface. Grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640` (TR) e azul `#62a8ee` (CT).
- **Transmissão e manager:** nos painéis da Steam, o placar de transmissão e a escalação, com a função e o geral de cada titular.
- **Capturas:** as duas em uso (`mapa-deserto` e `mapa-vila`) são de exemplo, enviadas pelo autor, e mostram só o mapa.
- **Na Steam:** a cápsula leva só a captura, o nome e o subtítulo. A frase ("Você monta o time. Eles jogam como profissionais.") fica para as redes.
- Tudo vetorial, gerado por `marketing/identidade/gerar.py`, sem imagem gerada por modelo.

## Por quê
- Pilar 1 (bots que jogam como profissionais): abrir pixel é o gesto que resume a IA e vira a marca; no padrão, cada anel é um bot olhando para um lado.
- Pilar 2 (assistir é o prêmio): o O da logo é a janela por onde se assiste; o placar de transmissão entra nos painéis.
- Pilar 3 (gestão que aparece na partida): o nome fala do clube, e a escalação entra nos painéis.
- Pilar 4 (identidade própria) e decisão 0004: sem soldados, armas ou qualquer forma que lembre a marca de outro jogo.
- Marketing: nome de 6 letras, grande na cápsula pequena, gênero escrito no subtítulo, e as mesmas cores e fontes do jogo para a página, o trailer e as capturas parecerem uma coisa só.

## Consequências
- **THE ORG precisa ser conferido** na Steam, em domínio e em registro de marca antes de qualquer material público. O agente só fez uma busca rápida na web e não achou jogo com esse nome. É uma expressão comum em inglês, então a busca pelo nome sozinho vai trazer outros resultados; o subtítulo ajuda.
- A pasta e a build do protótipo ainda se chamam `csmanager`; isso sai antes de qualquer build pública (plano de marketing).
- Trocar o nome não refaz nada à mão: `gerar.py "OUTRO NOME"` regenera todas as peças.
- Falta decidir se estas artes vão como arte final ou servem de referência para um artista refazer a cápsula (o plano de marketing reserva verba para isso).
- As artes do hub (soldados encapuzados) ficam fora do material público; trocá-las dentro do jogo é outra conversa.
- A escalação usa palavras da cena ("entry", "lurker", "IGL"); nenhuma é nome de arma, mapa ou marca. A função de quem usa a arma de precisão aparece como "SNIPER".
- As capturas de exemplo mostram o mapa, sem bots nem HUD. As definitivas precisam de uma jogada acontecendo, que é o que diz "FPS tático" de relance.
- Conferir o mapa do deserto contra a decisão 0004 antes de ir a público: visto de cima, o bombsite A com caixotes e muros lembra um mapa de deserto conhecido do gênero.
- Os brasões dos times e os retratos seguem pendentes em `gdd/06-arte.md`.
