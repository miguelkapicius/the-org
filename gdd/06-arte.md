# Direção de arte

> **Status:** rascunho · **Responsável:** — · **Decisões:** 0002, 0004, 0024, 0025, 0027, 0038

_Estilo visual e referências._

## Estilo

Tudo modelado em **3D**. A partida deve parecer uma **demo/transmissão de CS**, mas com texturas e visual próprios (decisões 0002 e 0004).

## Paleta

**[Sugestão]** A marca usa as cores que a interface já tem (decisão 0038, proposta): grafite `#0b0d10` e osso `#eceef1` (0025), laranja `#f0a640` (TR e o nosso time) e azul `#62a8ee` (CT e o adversário). A paleta completa do jogo em 3D continua a definir.

## Identidade da marca

**[Sugestão]** Proposta do agente em `marketing/identidade/` (decisão 0038), a validar:

- **Nome:** THE ORG (escolhido pelo autor).
- **Logo:** o nome em Barlow Condensed ExtraBold. O O de ORG é um anel com o ponto laranja aparecendo por dentro: um bot abrindo pixel. Subtítulo "ESPORTS MANAGER".
- **Símbolo (ícone do app):** o O da logo sozinho, sobre um campo de cor chapada (laranja em uso; grafite e osso como alternativas). Segue o padrão das marcas de jogo que deram certo: uma forma geométrica só, que funciona numa cor e em 16 px. O autor recusou o brasão e não escolheu entre o cone de visão e a formação.
- **Arte:** o padrão. O O da logo repetido em malha, cada um com o ponto virado para um lado; quase todos apagados, em grafite sobre grafite, e alguns acesos em laranja (TR) e em azul (CT). O nome vai grande por cima, numa área limpa. Nos painéis da Steam entram o placar de transmissão e a escalação. O autor recusou as três versões anteriores, todas com o mapa desenhado: a maquete colorida, a prancheta tática em 3D e o radar dentro do O. Nada de soldado realista nem de arma (0004).
- **Peças prontas:** ícones, cápsulas, cabeçalho, fundo e painéis da página da Steam, logo, cápsula e herói da biblioteca, banner e post de redes. Saem de `marketing/identidade/gerar.py`.

## Referências

- Demos/GOTV e transmissões de campeonatos de CS (enquadramento, HUD de transmissão).

## Lista de assets

_A definir: mapas, modelos de jogadores, armas, efeitos._

## Pendências de arte (dependem de gerar imagens)

Ficaram para depois, a pedido do autor (2026-11-07), por exigirem gerar imagens:

- **Brasões dos times:** hoje o protótipo usa a inicial do time no menu, na pré-partida, no ranking e nas telas de campeonato. Faltam 48 brasões (e o do clube do jogador, que pode ser personalizável).
- **Expressões nos retratos:** cada jogador tem um rosto fixo (decisão 0027). Falta uma variação por estado (cansado, motivado, em alta, frustrado), mantendo o mesmo rosto.

## Perguntas em aberto

- Estilo realista, estilizado ou low-poly?
- Retratos dos jogadores: hoje são 32 retratos de estúdio de jogadores fictícios gerados por IA, em três faixas de idade (decisão 0027). Arte final: manter esse caminho, fazer retratos ilustrados à mão ou contratar fotografia de referência? Faltam expressões por estado (cansado, motivado) e mais rostos se o elenco visível passar de 32.
- O nome THE ORG está livre na Steam, em domínio e em registro de marca?
- Ícone do app: campo laranja (em uso), grafite ou osso?
- Artes (decisão 0038): validar o padrão de anéis. As artes feitas em código servem como arte final ou viram referência para um artista refazer a cápsula?
- Subtítulo "ESPORTS MANAGER", frase "Você monta o time. Eles jogam como profissionais." e títulos dos painéis da Steam: confirmar, e definir a versão em inglês.
- Tipografia da interface: hoje Barlow Condensed, Barlow e IBM Plex Mono (livres, OFL, decisão 0025). Manter ou definir a identidade tipográfica do jogo junto com o nome e o logo?
