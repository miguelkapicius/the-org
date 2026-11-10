# 0038 — Nome THE ORG e identidade da marca: o ponto que abre o pixel e a prancheta tática

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
4. **A prancheta tática** — um round visto de cima em blocos de grafite, como a mesa de quem analisa a partida: bots, cone de visão, rotas, utilitárias e o que cada bot está fazendo. Mostra o produto, fala a língua de quem acompanha esports e só nós temos.

**Ícone.** A primeira versão era um brasão de cinco lados com uma janela redonda. **Recusada pelo autor.** Ficam três opções para escolher (ver Decisão).

## Decisão
- **Nome: THE ORG** (decisão do autor). "Org" é como a cena de esports chama a organização dona do time, que é o papel do manager.
- **Arte: opção 4, a prancheta tática** (proposta do agente, depois de o autor pedir mais cara de FPS tático, formação e manager, e que desse para ver os bots se comportando de verdade).
- **Logo:** o nome em Barlow Condensed ExtraBold (a fonte de título da interface, 0025). O **O de ORG é um anel com o ponto laranja aparecendo por dentro**: um bot abrindo pixel. Subtítulo fixo "ESPORTS MANAGER", que diz o gênero de cara. **Aprovada pelo autor.**
- **Símbolo (ícone do app): em aberto**, entre A (o anel: o O da logo sozinho), B (o cone de visão: o ponto no meio do O, com o cone abrindo um vão no anel, como o marcador de um bot no mapa) e C (a formação: os cinco titulares em cunha, o da ponta laranja). As peças usam o B por enquanto.
- **Cores:** as da interface. Grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640` (TR) e azul `#62a8ee` (CT). O mapa das artes é em tons de grafite.
- **Arte:** o TR executando o bombsite A. Dois entram juntos com flash e smoke, um abre o pixel pelo meio, um finge o B; o CT segura o ângulo e rotaciona. Etiquetas dizem o que cada bot está fazendo. O mapa é inventado para a arte. Vetorial, gerada por `marketing/identidade/gerar.py`, sem imagem gerada por modelo.
- **Lado manager:** onde cabe, a arte leva a escalação, com a função e o geral de cada titular.
- **Na Steam:** a cápsula leva só arte, nome e subtítulo, sem etiquetas. As etiquetas e a frase ("Você monta o time. Eles jogam como profissionais.") ficam para os painéis, o fundo da página e as redes.

## Por quê
- Pilar 1 (bots que jogam como profissionais): abrir pixel é o gesto que resume a IA e vira a marca; na arte, cada bot aparece fazendo uma coisa que um profissional faz.
- Pilar 2 (assistir é o prêmio): a arte é um round anotado, como numa transmissão.
- Pilar 3 (gestão que aparece na partida): o nome fala do clube, a escalação aparece ao lado da jogada.
- Pilar 4 (identidade própria) e decisão 0004: sem soldados, armas ou qualquer forma que lembre a marca de outro jogo.
- Marketing: nome de 6 letras, grande na cápsula pequena, gênero escrito no subtítulo, e as mesmas cores e fontes do jogo para a página, o trailer e as capturas parecerem uma coisa só.

## Consequências
- **THE ORG precisa ser conferido** na Steam, em domínio e em registro de marca antes de qualquer material público. O agente só fez uma busca rápida na web e não achou jogo com esse nome. É uma expressão comum em inglês, então a busca pelo nome sozinho vai trazer outros resultados; o subtítulo "Esports Manager" ajuda.
- A pasta e a build do protótipo ainda se chamam `csmanager`; isso sai antes de qualquer build pública (plano de marketing).
- Trocar o nome não refaz nada à mão: `gerar.py "OUTRO NOME"` regenera todas as peças.
- Falta decidir se estas artes vão como arte final ou servem de referência para um artista refazer a cápsula (o plano de marketing reserva verba para isso).
- As artes do hub (soldados encapuzados) ficam fora do material público; trocá-las dentro do jogo é outra conversa.
- As etiquetas usam palavras da cena ("flash", "smoke", "entry", "lurker"); nenhuma é nome de arma, mapa ou marca. Na escalação, a função de quem usa a arma de precisão aparece como "SNIPER".
- A arte mostra comportamentos (entrar no flash, cobrir a troca, fingir um bombsite, rotacionar) que o jogo precisa mesmo entregar; os primeiros itens de `gdd/12-ia-micro.md` vêm antes de divulgar.
- Os brasões dos times e os retratos seguem pendentes em `gdd/06-arte.md`.
