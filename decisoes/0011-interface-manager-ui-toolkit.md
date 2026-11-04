# 0011 — Interface do manager em UI Toolkit

- **Status:** proposta
- **Data:** 2026-11-04
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md, gdd/09-tecnico.md
- **Substitui:** —

## Contexto
Até agora o protótipo só tinha a partida (simulação + transmissão 3D). Faltava a interface do manager: elenco, mercado, Gaming House, finanças, escolha de assistir ou pular a partida. Precisamos escolher com que tecnologia de UI da Unity construí-la. O protótipo atual usa IMGUI (`OnGUI`) nos controles da partida e uGUI no HUD da transmissão.

## Opções consideradas
1. **UI Toolkit** (Unity 6, montado em código ou com UXML/USS) — layout flexível (estilo CSS), bom para telas cheias de tabelas e listas, é a direção atual da Unity para UI de runtime. Contras: ecossistema mais novo; precisa de `PanelSettings`.
2. **uGUI (Canvas)** — conhecido, mas layout de telas densas dá mais trabalho e o HUD de transmissão já usa.
3. **IMGUI (`OnGUI`)** — zero configuração e já usado no protótipo, mas não serve para uma interface de jogo: sem layout, difícil de estilizar, ruim para escalar.

## Decisão
A interface do manager (telas fora da partida) usa **UI Toolkit**. A estrutura (elementos e listas) é montada **em código** e o visual (cores, tamanhos, bordas, espaçamento, hover) vem de uma folha de estilos **USS** (`Assets/Resources/ManagerUI/Manager.uss`). O HUD da transmissão (partida) **não** muda por esta decisão.

**Atualização (2026-11-04):** o visual estabilizou, então o estilo saiu do código e foi para o USS. O desenho das telas é aprovado antes, como mockup **HTML/CSS** (`prototipo/mockup-ui/`, fora do jogo), e depois portado para USS com os mesmos nomes de classe. HTML/CSS de verdade dentro do jogo (webview) foi descartado: não é nativo da Unity, exige plugin de terceiros e é frágil.

## Por quê
Pilar "Gestão que aparece na partida": a interface de gestão vai crescer (contratos, treinos, scouts, calendário) e precisa de um sistema de layout que aguente tabelas e telas densas. UI Toolkit é o que a Unity 6 recomenda para isso.

## Consequências
- Código da interface em `prototipo/csmanager/Assets/Game/ManagerUI/`; regras de gestão separadas, em `Assets/Game/Manager/` (sem dependência de UI nem da Unity, roda no Headless).
- Duas cenas: `Manager` (interface) e `Partida3D` (transmissão). O estado da carreira passa entre elas em memória; **salvamento em disco ainda não existe** (`gdd/09-tecnico.md`).
- Fluxo de mudança visual: ajustar o mockup (`prototipo/mockup-ui/`), aprovar, e levar para o `Manager.uss`. Para conferir sem abrir a janela: menu CS Manager > Manager (ou `CenaManager.CapturarBatch` com a variável `CSM_CAPTURA=<pasta>`) salva um PNG de cada tela.
- Ícones são desenhados em código (`Icone.cs`, Painter2D), sem imagens.
- Se a decisão for rejeitada, só a pasta `ManagerUI/` e o `Manager.uss` precisam ser reescritos.
