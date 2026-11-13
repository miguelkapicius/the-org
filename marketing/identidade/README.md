# Identidade da marca: logo, ícone e artes de divulgação

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0004, 0025, 0038 · **Escrito em:** 2026-11-10

**Versão visual (canvas):** https://claude.ai/artifact/LPc5mbskMidwz2s1hVgRgF

_O nome THE ORG e a logo foram escolhidos e aprovados pelo autor. O ícone e as artes são proposta do agente, a validar. O porquê de cada escolha, e o que já foi recusado, está na decisão 0038._

## A ideia

O O de ORG é uma janela: o ponto laranja aparece por trás dela, como um bot abrindo pixel. Esse O sozinho é o **símbolo**, vai no **ícone** sobre cor chapada, e repetido em malha vira o **padrão** que fica no fundo de todas as artes.

- **Os banners e as cápsulas:** uma captura do jogo no fundo, escurecida do lado do texto, e o nome grande por cima. As capturas ficam em `capturas/`; as duas de hoje são de exemplo e mostram só o mapa.
- **O padrão:** o O repetido em malha, quase todos apagados e alguns acesos em laranja (TR) e em azul (CT). Fica para onde não há captura.
- **Transmissão e manager:** nos painéis da Steam, o placar de transmissão e a escalação com a função e o geral de cada titular.

## O que se tirou de jogos que deram certo

- **A cápsula tem de ler em miniatura** (120×45): nome grande, simples, em alto contraste.
- **Um assunto dominante e paleta fechada.** O assunto é o jogo, na captura; o nome vai sobre grafite, em osso, com o ponto laranja.
- **O gênero se lê pela imagem** antes do nome. Por isso a captura definitiva precisa mostrar bots e uma jogada, não só o mapa.
- **O símbolo é uma forma geométrica só**, que funciona numa cor e sozinha. Por isso o ícone é o O da logo num campo de cor chapada.

Fontes da pesquisa estão na decisão 0038 e no PR. É resumo de guias, não medição nossa.

## Ícone do app

O símbolo (o O da logo) sobre um campo de cor chapada. **Laranja** está em uso; **grafite** e **osso** ficam como alternativas. Para trocar, mude `ICONE = "laranja"` em `gerar.py`.

## Regras de uso

- **Logo:** nome + subtítulo "TACTICAL FPS MANAGER". O autor recusou "ESPORTS MANAGER"; as alternativas estão em `saida/logo-subtitulo-*`, e o texto em uso é a constante `SUB` em `gerar.py`. Sobre grafite usa osso; sobre fundo claro usa grafite, com o ponto em laranja mais escuro (`#d9822b`).
- **Cores:** grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640`, azul `#62a8ee`. Laranja é o ponto e o TR; azul é o CT.
- **Fontes:** Barlow Condensed ExtraBold no nome e nos títulos, SemiBold no subtítulo, IBM Plex Mono nos números (licença OFL, em `fontes/`).
- **Na Steam** a cápsula leva só a captura, o nome e o subtítulo. A frase fica para as redes.
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
| `padrao-1920x1080` | O padrão solto, para usar de fundo |
| `steam-fundo-da-pagina-1438x810` (`.jpg`) | Fundo da página |
| `steam-painel-1/2/3-…-616x300` | Painéis da descrição: "Assista como transmissão", "Bots que jogam como profissionais", "Sua gestão aparece na partida" |
| `steam-biblioteca-capsula-600x900` | Cápsula da biblioteca |
| `steam-biblioteca-heroi-3840x1240` (`.jpg`) | Herói da biblioteca (sem nome, como a Steam pede) |
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

Para trocar as capturas, ponha os arquivos novos em `capturas/` e mude `D` e `V` em `main()`; cada peça diz que parte da imagem mostra (`fx`, `fy`) e o zoom. O padrão fica em `padrao()`. O `.ico` e o `.jpg` são convertidos dos PNG com o Pillow.

## Perguntas em aberto

- Ícone do app: campo laranja, grafite ou osso?
- O nome THE ORG está livre na Steam, em domínio e em registro de marca?
- Estas artes vão como arte final ou viram referência para um artista refazer a cápsula? São desenho vetorial feito em código pelo agente, não imagem gerada por modelo; mesmo assim o plano de marketing reserva verba para a cápsula.
- Subtítulo: "TACTICAL FPS MANAGER" (em uso), "MANAGER DE FPS TÁTICO" ou "MONTE O TIME. ASSISTA À PARTIDA."?
- Frase e títulos dos painéis: confirmar, e definir a versão em inglês.
- Os números da escalação e do placar são de exemplo.
- Trocar as capturas de exemplo por capturas com bots, HUD da transmissão e uma jogada acontecendo.
- Conferir o mapa do deserto contra a decisão 0004 antes de ir a público.
- Falta o trailer, que depende do jogo.
