# Identidade da marca: logo, ícone e artes de divulgação

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0004, 0025, 0038 · **Escrito em:** 2026-11-10

**Versão visual (canvas):** https://claude.ai/artifact/LPc5mbskMidwz2s1hVgRgF

_O nome THE ORG e a logo foram escolhidos e aprovados pelo autor. O ícone e as artes são proposta do agente, a validar. O porquê de cada escolha, e o que já foi recusado, está na decisão 0038._

## A ideia

O O de ORG é uma janela. Na logo, o ponto laranja aparece por trás dela (um bot abrindo pixel). Nas artes, o O cresce e vira o radar da partida.

- **A arte:** um round visto de cima, chapado. O mapa em cinza, os bots laranja (TR) e azuis (CT), o cone de visão de cada um, o caminho que fizeram, a rota combinada, smoke e flash, e etiquetas do que cada bot está fazendo.
- **Transmissão e manager:** onde cabe, o placar de transmissão e a escalação com a função e o geral de cada titular.

## O que se tirou de jogos que deram certo

- **A cápsula tem de ler em miniatura** (120×45): nome grande, simples, em alto contraste.
- **Um assunto dominante e paleta fechada.** Aqui o assunto é o radar dentro do O; as cores são grafite, osso, laranja e azul.
- **O gênero se lê pela imagem** antes do nome: o radar com bots e rotas diz "FPS tático"; o subtítulo diz "manager".
- **O símbolo é uma forma geométrica só**, que funciona numa cor e sozinha. Por isso o ícone é o O da logo num campo de cor chapada.

Fontes da pesquisa estão na decisão 0038 e no PR. É resumo de guias, não medição nossa.

## Ícone do app

O símbolo (o O da logo) sobre um campo de cor chapada. **Laranja** está em uso; **grafite** e **osso** ficam como alternativas. Para trocar, mude `ICONE = "laranja"` em `gerar.py`.

## Regras de uso

- **Logo:** nome + subtítulo "ESPORTS MANAGER". Sobre grafite usa osso; sobre fundo claro usa grafite, com o ponto em laranja mais escuro (`#d9822b`).
- **Cores:** grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640`, azul `#62a8ee`. Laranja é o ponto e o TR; azul é o CT.
- **Fontes:** Barlow Condensed ExtraBold no nome e nos títulos, SemiBold no subtítulo, IBM Plex Mono nas etiquetas (licença OFL, em `fontes/`).
- **Na Steam** a cápsula leva só arte, nome e subtítulo, sem etiquetas. As etiquetas e a frase ficam para os painéis, o fundo da página e as redes.
- **Não fazer:** soldado realista, arma, mira, nome de arma ou de mapa real, ou qualquer forma que lembre a capa de outro FPS (decisão 0004); degradê de fundo, brilho ou outra cor de destaque.

## Peças (em `saida/`, SVG e PNG)

| Arquivo | Uso |
|---|---|
| `logo-claro` / `-escuro` / `-fundo` | Logo: para fundo escuro, para fundo claro e já sobre grafite |
| `icone-laranja`, `icone-grafite`, `icone-osso` | O ícone nos três campos de cor, 512×512 |
| `icone-app` (`.svg`, `.png`, `.ico`), `simbolo`, `simbolo-escuro` | O ícone em uso (hoje o laranja) e o símbolo solto; o `.ico` tem de 16 a 256 px |
| `steam-icone-comunidade-184x184` (`.jpg`) | Ícone da comunidade |
| `steam-cabecalho-920x430` | Cabeçalho da página |
| `steam-capsula-principal-1232x706` | Cápsula principal |
| `steam-capsula-pequena-462x174` | Cápsula pequena (listas e busca) |
| `steam-capsula-vertical-748x896` | Cápsula vertical |
| `steam-fundo-da-pagina-1438x810` | Fundo da página, com as etiquetas |
| `steam-painel-1/2/3-…-616x300` | Painéis da descrição: "Assista como transmissão", "Bots que jogam como profissionais", "Sua gestão aparece na partida" |
| `steam-biblioteca-capsula-600x900` | Cápsula da biblioteca |
| `steam-biblioteca-heroi-3840x1240` | Herói da biblioteca (sem nome, como a Steam pede) |
| `steam-biblioteca-logo-1280x720` | Logo da biblioteca, fundo transparente |
| `banner-social-1500x500`, `quadrado-social-1080x1080` | Redes |

Conferir os tamanhos no Steamworks na hora de subir: a Steam muda a lista de vez em quando.

## Regerar

```bash
pip install fonttools uharfbuzz
```

```bash
python3 marketing/identidade/gerar.py
```

```bash
node marketing/identidade/render.js
```

A jogada desenhada fica em `Radar.montar()`: o mapa (inventado para a arte), onde está cada bot, para onde olha e a etiqueta dele. O `.ico` e o `.jpg` são convertidos dos PNG com o Pillow.

## Perguntas em aberto

- Ícone do app: campo laranja, grafite ou osso?
- O nome THE ORG está livre na Steam, em domínio e em registro de marca?
- Estas artes vão como arte final ou viram referência para um artista refazer a cápsula? São desenho vetorial feito em código pelo agente, não imagem gerada por modelo; mesmo assim o plano de marketing reserva verba para a cápsula.
- Subtítulo, frase, títulos dos painéis e etiquetas dos bots: confirmar, e definir a versão em inglês.
- Os números da escalação e do placar são de exemplo.
- Faltam as capturas e o trailer, que dependem do jogo.
