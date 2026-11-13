# Brasa v9.2 no jogo: pedido pra Sim (sim-apoio e sim-util)

A planta nova da Brasa foi aprovada pelo Gabriel em 2026-11-09 às 17h57. Ela foi refeita do zero e a Brasa v7 sai de cena.
Página com planta, régua e motivo de cada lugar: https://claude.ai/artifact/5x4UTYZa4ivom7aoJAQ9Pb

**Combinado:** nada disto entra antes do exe 11 (2026-11-09). O exe 11 sai com a Brasa v7.

## O que já está pronto (sessão do Visual, só Python)

- `Tools/brasa/brasa9.py`: planta em polígonos, régua de tempos, checagem e exportação. Cópia da versão aprovada em
  `Tools/brasa/brasa_v9_2_aprovada.py`.
  - `python -I Tools/brasa/brasa9.py`: tabela de tempos.
  - `python -I Tools/brasa/brasa9.py exportar`: grava `Tools/brasa/v9/brasa_mapa.json`.
- `Tools/brasa/v9/brasa_mapa.json` (gerado e conferido): toda subcasa andável tem piso e zona, todo prédio tem topo, e
  a checagem de degrau e de alcance não acusa nada.
- O `Tools/brasa/brasa_mapa.json` antigo (v7) e o `gera_cs.py` continuam como estão até alguém trocar de propósito.

## O que muda no contrato

Antes, cada casa de 25 unidades era um bloco. Agora a grade é **fina**:

| campo | antes (v7) | agora (v9) |
|---|---|---|
| resolução | casa de 25 unidades (`Q`) | subcasa de `QF = 5` unidades (`S = 5` por casa) |
| tamanho | 56 × 54 casas | `W × H` = 60 × 48 casas; `GW × GH` = 300 × 240 subcasas; `NX × NY` = 1500 × 1200 |
| `celulas`, `rampa`, `material`, `piso`, `topo`, `zona` | por casa | por subcasa (linhas de `GW` caracteres ou números) |
| coberturas | retângulo em casas inteiras | `x0..x1`/`y0..y1` em casas **fracionárias** (+ `poli`) |
| sites, spawns, nomes | casas inteiras | casas fracionárias |

As regras de montagem são as mesmas do `MontaBrasa` de hoje, trocando `Q` por `QF` na hora de encher a grade:

- A subcasa (i, j) cobre `[i*QF, i*QF+QF) × [j*QF, j*QF+QF)`.
- `walk` vale para letra diferente de `#`. `floor` = `GZ + piso`. `stair` = `rampa == '1'`. `fmat` vem do material,
  na mesma tabela de antes.
- Prédio `#`: `solid` = `GZ + topo`.
- A letra `o` é piso com cobertura em cima, igual a antes.
- A cobertura vira `AddCover` pelo retângulo (casas fracionárias × 25, arredondar). `piso` e `h` vêm prontos.

**É por causa disso que a diagonal sai lisa:** o 3D e a navegação já trabalham em 1 unidade, e a escadinha vinha só do
bloco de 25.

## Nomes das zonas (mudou tudo)

O `MontaBrasa` hoje traduz `"Bomb A" → "A"`, `"Bomb B" → "B"`, `"Base TR" → "TR"` e `"Base CT" → "CT"`. Na v9:

- `Piazza della Chiesa` → `A` (bomb A)
- `Cantina` → `B` (bomb B)
- `Mercato` → `TR` (base TR)
- `Castello` → `CT` (base CT)

As outras zonas (callouts): Arco CT, Belvedere, Corte, Discesa, Frantoio, Lavatoio, Loggia, Nicchia, Palazzo,
Piazzetta, Porta Vecchia, Primo pianerottolo, Rampa, Sacrestia, Sagrato, Salita, Scala del palazzo, Scalinata,
Secondo pianerottolo, Via alta, Via del forno, Vicolo, Vicolo della chiesa.

## O que a sim-util refaz

- **`Places.cs` da Brasa:** nenhuma posição antiga vale. A página explica o papel de cada lugar e cada cobertura (onde
  o CT segura, onde o TR espera a granada).
- **Banco de estratégias (`converter_brasa.py`):** precisa reconverter com as zonas novas. Pela função do lugar, o
  equivalente da Inferno é:
  - Porta Vecchia ≈ alto do banana;
  - lavatoio ≈ varanda/curta;
  - scalinata/belvedere ≈ meio/alto do meio;
  - palazzo/loggia ≈ apartamento/varanda.
- **Régua no Sim:** medir de novo pisa, retake e rotação. As metas estão na página e no `brasa9.py` (`medir()`).

Até isso tudo estar no lugar, ninguém roda partida na Brasa.
