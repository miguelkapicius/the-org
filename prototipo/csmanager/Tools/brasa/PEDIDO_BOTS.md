# Brasa (mapa 2): pedido pra sessão dos bots

Status: **não mandado ainda.** O Gabriel pediu pra esperar a sessão da Sim terminar o A/B. Quando ela estiver livre,
isto vira o pedido.

## O que já existe (Tools/brasa, feito pela sessão do Visual)

- `brasa.py`: a planta (grade de 56 × 54 casas, 1 casa = 25 unidades da Sim, mesma escala da Vila), as alturas, as
  zonas (callouts), as coberturas, a régua de tempos (comparada com a Inferno real) e a linha de visão (`visao.py`).
- `brasa_mapa.json`: tudo que o MapData precisa, já conferido (toda casa andável tem piso e zona; não há degrau acima
  de 10 sem escada/rampa).
- Página: https://claude.ai/artifact/5NBqyW2jnkBJ7qkm5G5dRj (planta, tempos, visão, prévia 3D em blocos).

## O pedido

1. **MapData com mais de um mapa.** Hoje `NX`/`NY` são `const` e as grades são `static readonly` do tamanho da Vila
   (1000 × 875). A Brasa tem 1400 × 1350. O mapa tem que ser escolhido antes de montar (Vila continua o padrão), e
   as grades alocadas pelo tamanho do mapa escolhido. NavGrid, Spots, Places e o que mais guardar tamanho fixo vão
   junto.
2. **Montar a Brasa a partir do `brasa_mapa.json`** (ou de um .cs gerado dele, como preferirem):
   - casa (x, y) cobre as unidades `[x*25, x*25+25) × [y*25, y*25+25)`.
   - `walk` = letra diferente de `#` e `o`.
   - `floor` = `GZ + piso` (piso já em unidades da Sim; arredondar). `stair` = `rampa == '1'`.
   - `fmat` = `material` (`Stone`, `Tile`, `Dirt`, `Wood`, `Cave`; nulo = `Stone`).
   - `solid` das casas `#` = `GZ + topo` (o telhado já leva em conta o piso mais alto ao redor).
   - `~` = sacada: 2º andar de onde só se desce pulando (igual à queda `~` da Vila). `n` = plataforma: sobe só pela
     escada `s`, desce pulando. `k` = 2º andar (piso +30, igual a `UP`).
   - `coberturas`: retângulos em casas (inclusive), altura `h` acima do `piso`, `tipo` = kind do Cover
     (`crate`, `barrel`, `cart`, `logs`, `halfwall`, `fountain`, `sacks`, `bench`). Os barris do carro do banana
     são `barrel` de propósito: na Inferno a Valve trocou o carro por barris que a bala atravessa.
   - `zonas` + `zona`: nome da área de cada casa (índice na lista; -1 = prédio). Viram `areas`/`CALLOUT`.
   - `sites`: retângulos em casas. `spawnT`/`spawnCT`: casas (centro = `x*25+12.5`).
3. **Bots jogando a Brasa:** posições, rotas, estratégias e lineups são de vocês. Referências que já medi na Inferno
   real (radar do CS2 instalado, fuzil, ×1,25 pra escala da Sim): o CT chega antes em todo choke (carro do banana 5,4 s,
   alto do meio 6,7 s, varanda 5,5 s); empate no centro do meio; rotação do CT entre sites ~22 s. A Brasa bate com
   isso a menos de 2 s na maioria dos pontos (tabela na página).
4. **Avisar o Visual** quando o MapData montar a Brasa: o visual 3D (MalhaMapa, Fachada3D, Packs3D, Cenario3D) lê o
   MapData direto, então a maquete sai pelo mesmo processo da Vila. Depois disso a sessão do Visual troca paleta e
   props pro clima da Inferno.

## O que o Visual não mexe

`Sim/`, `Map/`, `View/Effects.cs`, `View/Iso.cs`. Por isso o item 1 e o 2 são de vocês.
