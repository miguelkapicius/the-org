# Identidade da marca: logo, ícone e artes de divulgação

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0004, 0025, 0038 · **Escrito em:** 2026-11-10

**Versão visual (canvas):** https://claude.ai/artifact/LPc5mbskMidwz2s1hVgRgF (página privada do Miguel; compartilhar pelo menu Share para o Gabriel abrir).

_O nome THE ORG foi escolhido pelo autor. O resto é proposta do agente, a validar. O porquê de cada escolha está na decisão 0038._

## A ideia

A marca é a própria simulação.

- **O brasão** é a org: todo time de esports tem o seu. É o ícone do app, com formato próprio.
- **Cinco lados:** os cinco titulares.
- **A janela redonda:** o O de ORG e o olho do manager, que assiste.
- **O ponto laranja:** um bot abrindo pixel. Aparece só o necessário atrás da janela.
- **A arte** é a partida vista de cima em maquete, sobre o grafite da interface.

## Regras de uso

- **Logo principal:** nome + subtítulo "ESPORTS MANAGER". A versão "completa" leva o brasão ao lado.
- **Sobre grafite** usa osso; **sobre fundo claro** usa grafite, com o ponto em laranja mais escuro (`#d9822b`).
- **Ícone do app:** o brasão com contorno grafite, fundo transparente. Funciona em qualquer cor de fundo. Onde o ícone tem de ser quadrado (Steam), usar o quadrado.
- **Cores:** grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640`, azul `#62a8ee`. O laranja é sempre o ponto (e o TR); o azul só aparece na arte (o CT).
- **Fontes:** Barlow Condensed ExtraBold no nome, SemiBold no subtítulo (licença OFL, em `fontes/`).
- **Na Steam** a cápsula leva só arte, nome e subtítulo. A frase fica para redes.
- **Não fazer:** soldado realista, arma, mira, ou qualquer forma que lembre a capa de outro FPS (decisão 0004); degradê, brilho ou outra cor de destaque.

## Peças (em `saida/`, SVG e PNG)

| Arquivo | Uso |
|---|---|
| `logo-claro` / `-escuro` / `-fundo` | Logo principal: para fundo escuro, para fundo claro e já sobre grafite |
| `logo-completa-claro` / `-escuro` / `-fundo` | Logo com o brasão ao lado |
| `simbolo`, `simbolo-escuro` | O brasão solto |
| `icone-app` (`.svg`, `.png`, `.ico`) | Ícone do app, formato próprio; o `.ico` tem de 16 a 256 px |
| `icone-quadrado`, `icone-quadrado-claro` | Ícone quadrado 512×512 |
| `steam-icone-comunidade-184x184` (`.jpg`) | Ícone da comunidade |
| `steam-cabecalho-920x430` | Cabeçalho da página |
| `steam-capsula-principal-1232x706` | Cápsula principal |
| `steam-capsula-pequena-462x174` | Cápsula pequena (listas e busca) |
| `steam-capsula-vertical-748x896` | Cápsula vertical |
| `steam-fundo-da-pagina-1438x810` | Fundo da página |
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

Para testar outro nome, passe-o como argumento (`gerar.py "OUTRO @ONOME"`). No nome, `@` troca a letra seguinte (um O) pelo anel com o ponto por dentro. O `.ico` e o `.jpg` são convertidos dos PNG com o Pillow.

## Perguntas em aberto

- O nome THE ORG está livre na Steam, em domínio e em registro de marca?
- Estas artes vão como arte final ou viram referência para um artista refazer a cápsula? São desenho vetorial feito em código pelo agente, não imagem gerada por modelo; mesmo assim o plano de marketing reserva verba para a cápsula.
- Subtítulo "ESPORTS MANAGER", a frase "Você monta o time. Eles jogam como profissionais." e os títulos dos três painéis: confirmar, e definir a versão em inglês.
- Faltam as capturas e o trailer, que dependem do jogo.
