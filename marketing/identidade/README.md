# Identidade da marca: logo e artes de divulgação

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0004, 0025, 0038 · **Escrito em:** 2026-11-10

**Versão visual (canvas):** https://claude.ai/artifact/LPc5mbskMidwz2s1hVgRgF (página privada do Miguel; compartilhar pelo menu Share para o Gabriel abrir).

_Proposta do agente, a validar. O porquê de cada escolha está na decisão 0038._

## A ideia

A marca é a própria simulação. O **ponto laranja** é um bot; ele aparece só o necessário atrás de uma letra, como quem abre pixel num ângulo. A arte é a partida vista de cima em **maquete**, sobre o grafite da interface.

## Os 3 nomes propostos

| | Nome | Por quê | Contra |
|---|---|---|---|
| A | **FULL BUY** (recomendado) | A rodada em que o time compra tudo: fala de dinheiro e de jogo, que é o trabalho do manager. 7 letras, o mais legível na cápsula pequena | Quem não é da cena não pega a referência de primeira (o subtítulo resolve) |
| B | **LIVE ON THREE** | O aviso de que a partida vai começar valendo; o ponto vira a luz de "no ar" | Longo; foi nome de um programa de esports encerrado em 2013 |
| C | **FRAGHOUSE** | A Gaming House do time; palavra inventada, fácil de achar na busca | Fala mais do clube do que da partida |

Nenhum usa "CS", "Counter", "Strike" ou "Major". **Antes de fechar:** conferir Steam, domínio e registro de marca. O agente só fez uma busca rápida na web.

## Regras de uso

- **Logo principal:** nome + subtítulo "ESPORTS MANAGER". Sobre grafite usa osso; sobre fundo claro usa grafite, com o ponto em laranja mais escuro (`#d9822b`).
- **Símbolo:** bloco + ponto. Para ícone, avatar e onde o nome não cabe.
- **Cores:** grafite `#0b0d10`, osso `#eceef1`, laranja `#f0a640`, azul `#62a8ee`. O laranja é sempre o ponto (e o TR); o azul só aparece na arte (o CT).
- **Fontes:** Barlow Condensed ExtraBold no nome, SemiBold no subtítulo (licença OFL, em `fontes/`).
- **Na Steam** a cápsula leva só arte, nome e subtítulo. A frase fica para redes.
- **Não fazer:** soldado realista, arma, mira, ou qualquer forma que lembre a capa de outro FPS (decisão 0004); degradê, brilho ou outra cor de destaque.

## Peças (em `saida/`, SVG e PNG)

| Arquivo | Uso |
|---|---|
| `logo-<nome>-claro` / `-escuro` / `-fundo` / `-completa` | Logo de cada um dos 3 nomes: para fundo escuro, para fundo claro, já sobre grafite, e com o símbolo ao lado |
| `simbolo`, `icone`, `icone-claro` | Símbolo solto e ícone 512×512 |
| `steam-cabecalho-920x430` | Cabeçalho da página |
| `steam-capsula-principal-1232x706` | Cápsula principal |
| `steam-capsula-pequena-462x174` | Cápsula pequena (listas e busca) |
| `steam-capsula-vertical-748x896` | Cápsula vertical |
| `steam-biblioteca-heroi-3840x1240` | Herói da biblioteca (sem nome, como a Steam pede) |
| `banner-social-1500x500`, `quadrado-social-1080x1080` | Redes |

As artes saem com o nome **A**. Conferir os tamanhos no Steamworks na hora de subir: a Steam muda a lista de vez em quando.

## Regerar ou trocar o nome

```bash
pip install fonttools uharfbuzz
```

```bash
python3 marketing/identidade/gerar.py "FRAG|HOUSE"
```

```bash
node marketing/identidade/render.js
```

No nome, `|` marca onde o ponto abre o pixel (atrás da letra seguinte) e `*` troca a letra seguinte pelo ponto inteiro (`"LIVE *ON THREE"`). Sem argumento, usa FULL BUY.

## Perguntas em aberto

- Qual dos 3 nomes (ou outro)? Conferir Steam, domínio e registro de marca.
- Estas artes vão como arte final ou viram referência para um artista refazer a cápsula? São desenho vetorial feito em código pelo agente, não imagem gerada por modelo; mesmo assim o plano de marketing reserva verba para a cápsula.
- Subtítulo "ESPORTS MANAGER" e a frase "Você monta o time. Eles jogam como profissionais.": confirmar, e definir a frase em inglês.
- Faltam: logo da biblioteca (1280×720 transparente), fundo da página e as capturas e o trailer, que dependem do jogo.
