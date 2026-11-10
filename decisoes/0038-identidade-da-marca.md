# 0038 — Nome THE ORG e identidade da marca: o O que vira radar

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
5. **O radar dentro do O** — o round visto de cima, chapado, dentro do O da logo ampliado. Um assunto só, nome grande, paleta fechada.

**Ícone.** A primeira versão era um brasão de cinco lados. **Recusada pelo autor.** A segunda rodada trouxe o anel, o cone de visão e a formação; o autor não escolheu nenhuma e pediu para seguir o padrão de jogos que deram certo.

**O que se tirou de quem deu certo** (pesquisa rápida do agente em guias de cápsula da Steam e análises de marcas de FPS; a lista é resumo, não medição nossa):
- A cápsula tem de funcionar em miniatura: o nome precisa ler em 120×45.
- Um assunto dominante, paleta comprometida, título simples em alto contraste. Fundo escuro é comum entre os mais vendidos.
- Quem navega procura o gênero antes de ler o nome: cor e assunto carregam isso.
- As marcas de FPS que duram são um símbolo geométrico simples, que funciona numa cor só e sozinho.

## Decisão
- **Nome: THE ORG** (decisão do autor). "Org" é como a cena de esports chama a organização dona do time, que é o papel do manager.
- **Arte: opção 5, o radar dentro do O** (proposta do agente).
- **Logo:** o nome em Barlow Condensed ExtraBold (a fonte de título da interface, 0025). O **O de ORG é um anel com o ponto laranja aparecendo por dentro**: um bot abrindo pixel. Subtítulo fixo "ESPORTS MANAGER", que diz o gênero de cara. **Aprovada pelo autor.**
- **Símbolo:** o O da logo sozinho. **Ícone do app:** o símbolo sobre um campo laranja chapado (grafite e osso ficam como alternativas).
- **Cores:** as da interface. Grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640` (TR) e azul `#62a8ee` (CT). O mapa das artes é em tons de cinza.
- **Arte:** o TR executando o bombsite A, visto de cima e chapado. Dois entram juntos com flash e smoke, um abre o pixel pelo meio, um finge o B; o CT segura o ângulo e rotaciona. Nas cápsulas, isso aparece dentro do O da logo ampliado. O mapa é inventado para a arte. Vetorial, gerada por `marketing/identidade/gerar.py`, sem imagem gerada por modelo.
- **Transmissão e manager:** onde cabe, a arte leva o placar de transmissão e a escalação, com a função e o geral de cada titular.
- **Na Steam:** a cápsula leva só arte, nome e subtítulo, sem etiquetas. As etiquetas do que cada bot faz e a frase ("Você monta o time. Eles jogam como profissionais.") ficam para os painéis, o fundo da página e as redes.

## Por quê
- Pilar 1 (bots que jogam como profissionais): abrir pixel é o gesto que resume a IA e vira a marca; na arte, cada bot aparece fazendo uma coisa que um profissional faz.
- Pilar 2 (assistir é o prêmio): o O da logo vira a janela por onde se assiste, e a arte é um round anotado, como numa transmissão.
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
