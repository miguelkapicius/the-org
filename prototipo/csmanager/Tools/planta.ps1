# Planta baixa do mapa em grade de blocos de 20 unidades (40 x 35 = 800 x 700). Cada letra é um tipo de chão:
#   # prédio   T base TR   C base CT   a bomb A   b bomb B   r rua (chão)   m meio (ladrilho)
#   p palácio em cima (+14)   k casarão 2º andar (+10)   w janelão (+14)   P plataforma do A (+8)   n banca do B (+8)
#   s escada   ~ queda (sacada/janela: borda alta de onde se pula)   v carroça (+6: sobe pulando)
# Saída: Tools\planta.txt (texto) e Logs\render\planta.png (desenho com rotas e linhas de visão).
param([string]$Saida = "D:\unity\CSManager\Logs\render\planta.png")
$W = 40; $H = 35
$g = New-Object 'char[,]' $W, $H
for ($x = 0; $x -lt $W; $x++) { for ($y = 0; $y -lt $H; $y++) { $g[$x, $y] = '#' } }
function Bloco($ch, $x0, $x1, $y0, $y1) { for ($x = $x0; $x -le $x1; $x++) { for ($y = $y0; $y -le $y1; $y++) { $g[$x, $y] = $ch } } }

# ---- spawns
Bloco 'C' 19 27 1 4            # base CT (norte, mais perto do A)
Bloco 'T' 13 26 29 33          # base TR (sul)
# ---- bomb A (leste, aberto) com plataforma do CT no fundo
Bloco 'a' 31 38 4 11
Bloco 'P' 34 38 2 3            # plataforma (+8) no fundo do A: o CT chega por cima (rua do CT -> escada)
Bloco 's' 32 33 1 2            # escada da rua do CT pra plataforma
# ---- CT -> A: rua do CT (dobra) e chegada na plataforma
Bloco 'r' 28 31 1 3            # rua do CT (3 de largura, sai do spawn pelo leste)
Bloco 'r' 29 31 3 5            # descida do CT: entra no A pelo noroeste (dobra: o spawn não vê o A)
# ---- TR -> A pela rampa (rua externa com dobra e arco no topo)
Bloco 'r' 27 33 29 31          # saída leste da base (3 de largura)
Bloco 'r' 31 33 19 31          # rampa de baixo (3 de largura: é a rota principal do TR pro A)
Bloco 'r' 31 37 16 18          # dobra
Bloco 'r' 35 37 12 16          # rampa (3 de largura; estreita só no arco do topo, o choke)
# ---- TR -> A pelo palácio (sobe, anda por cima, sacada em cima do A)
Bloco 'r' 25 26 28 28          # entrada do palácio
Bloco 's' 25 26 26 27          # escada
Bloco 'p' 25 26 18 25          # palácio (corredor de cima)
Bloco 'p' 25 29 16 17          # sala
Bloco 'p' 28 29 13 15          # corredor da sacada
Bloco '~' 28 32 12 12          # sacada: borda de onde cai no A
# ---- meio: topo (TR), rua do meio com dobra, praça (CT); janelão olha o meio de cima
Bloco 'r' 21 24 23 28          # topo do meio (entra de lado no meio: o janelão não vê o spawn TR)
Bloco 'm' 17 20 8 24           # rua do meio: larga (80) e comprida, o janelão vê ela inteira
Bloco 'm' 21 24 4 8            # praça (CT), do lado do janelão, liga a base CT no topo do meio
Bloco 's' 21 22 5 6            # escada do janelão (sobe da praça)
Bloco 'w' 16 20 6 7            # janelão (+14): no topo do meio, janela pro sul olhando a rua toda
# ---- caminho de rato: da curta, um corredor estreito (1 de largura) sobe e entra no janelão pelas costas
Bloco 'r' 14 14 8 13           # passagem estreita
Bloco 's' 14 15 6 7            # escadinha que sobe pro nível do janelão
Bloco 'w' 16 16 6 7            # porta dos fundos do janelão
# ---- meio -> A (conector com dobra)
Bloco 'r' 21 26 12 13          # conector: sai do meio pro leste...
Bloco 'r' 25 26 10 11          # ...sobe (dobra)...
Bloco 'r' 25 30 9 10           # ...e entra no A pelo oeste (uma fileira abaixo da praça: não alinha com ela)
# ---- meio -> B (curta com dobra)
Bloco 'r' 12 16 20 21
Bloco 'r' 12 13 14 19
Bloco 'r' 9 13 12 13
# ---- bomb B (oeste, fechado) com banca alta
Bloco 'b' 2 9 6 15
Bloco 'n' 2 4 6 8              # banca (+8)
Bloco 's' 5 5 7 8              # escada da banca
# ---- CT -> B: mercado (com dobra) e viela que chega na banca
Bloco 'r' 12 18 3 4            # mercado (sai do spawn pelo oeste)
Bloco 'r' 9 11 3 5             # descida do mercado (entra no B pelo norte)
Bloco 'r' 11 12 1 2            # passagem pra viela (dobra)
Bloco 'r' 3 10 1 2             # viela (mais alta que o mercado: não alinha)
Bloco 's' 3 4 3 5              # subida da viela pra banca
# ---- TR -> B pelo casarão (2º andar) e tapete
Bloco 'r' 12 16 30 31          # rua do B
Bloco 's' 10 11 30 31          # escada do casarão
Bloco 'k' 4 9 24 31            # casarão (cômodos)
Bloco 'k' 4 5 17 23            # corredor
Bloco '~' 3 9 16 16            # tapete: sacada larga em cima do B (cai em qualquer ponto)
Bloco 'v' 5 6 15 15            # carroça embaixo do tapete (+6): sobe pulando 2x (chão -> carroça -> tapete), igual a van

$lines = for ($y = 0; $y -lt $H; $y++) { -join (0..($W - 1) | ForEach-Object { $g[$_, $y] }) }
$lines | Set-Content -Encoding utf8 "D:\unity\CSManager\Tools\planta.txt"
$lines
