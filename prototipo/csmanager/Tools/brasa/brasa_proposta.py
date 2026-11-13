# -*- coding: utf-8 -*-
"""Planta do 2o mapa ("Brasa", vila italiana no estilo da Inferno) em grade de 40 x 35 quadradinhos (Q = 25 da Sim,
mesma escala da Vila) + medidor de tempos (quem chega primeiro em cada casa, correndo de fuzil).

    python Tools/brasa/brasa.py            -> Tools/brasa/brasa.txt, brasa.json e a tabela de tempos
    python Tools/brasa/brasa.py vila       -> mesma medição na planta da Vila (calibragem da régua)

Letras:  # prédio   T base TR   C base CT   a bomb A   b bomb B   r rua   m meio (ladrilho)
         k 2o andar (casarão)   n plataforma (+12, sobe só por escada, desce pulando)   s escada
         ~ sacada (2o andar: daqui pula pra baixo, não sobe)   o cobertura (caixote/carroça: não passa)
"""
import heapq, json, math, os, sys

W, H = 56, 54
AQUI = os.path.dirname(os.path.abspath(__file__))
RUN = 62.5 / 25          # quadradinhos/s correndo de faca (MapCheck.RUN / Q)
FUZIL = 0.88             # AK 0,86 / M4 0,9
VEL = RUN * FUZIL

g = [['#'] * W for _ in range(H)]
NOMES = []               # (nome, x, y) pro desenho


def bloco(ch, x0, x1, y0, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            g[y][x] = ch


def faixa(ch, pts, larg):
    """Corredor em linha quebrada (pode ser diagonal): marca as casas cujo centro fica a até larg/2 da linha."""
    r = larg / 2
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        for y in range(max(0, int(min(y0, y1) - r) - 1), min(H, int(max(y0, y1) + r) + 2)):
            for x in range(max(0, int(min(x0, x1) - r) - 1), min(W, int(max(x0, x1) + r) + 2)):
                px, py = x + .5, y + .5; dx, dy = x1 - x0, y1 - y0
                t = max(0, min(1, ((px - x0) * dx + (py - y0) * dy) / max(1e-9, dx * dx + dy * dy)))
                if math.hypot(px - x0 - t * dx, py - y0 - t * dy) <= r:
                    g[y][x] = ch


def nome(n, x, y):
    NOMES.append((n, x, y))


# =====================================================================================================================
# PLANTA v4 — refeita do zero a partir das medidas da Inferno (radar e tempos do jogo instalado), escala 1,25 x:
#  o boneco da Sim é maior que o do CS em relação ao chão, então as distâncias crescem 25% pra não ficar apertado.
#  Norte = y 0. TR no sudoeste, CT no norte, B no noroeste, A no leste (a Inferno girada: o desenho é nosso).
#  Metas (Inferno x 1,25): TR pisa no B ~22 s e no A ~24 s; CT ~13 e ~10. Banana: o TR domina o pé, o CT o carro,
#  e se encontram a 2/3 da subida. Meio: empate no meio, CT 8 s antes no alto. Apê: o CT chega antes na varanda.
# =====================================================================================================================
# ---- bases
bloco('T', 3, 15, 48, 52); bloco('T', 3, 8, 45, 47); nome("Base TR", 9, 50.5)
bloco('C', 31, 41, 2, 8);  nome("Base CT", 36, 5)

# ---- rampa do TR e meio de baixo (praça de onde saem banana e meio)
faixa('r', [(5.5, 45), (8, 40.5), (14, 40), (17.5, 36)], 4); nome("Rampa", 10.6, 43.4)
bloco('r', 15, 22, 30, 36); nome("Meio de baixo", 18.5, 37.6)
bloco('o', 19, 21, 34, 34); nome("Carroça", 18.4, 32.6)

# ---- BANANA (no formato da Inferno, conferido com a linha de visão do radar dela): reta no pé, diagonal subindo,
#      patamar no alto e um corredor que sobe colado na parede do B e entra pela porta lá em cima, então de dentro do
#      banana não se vê o site. Cada cobertura tem função:
#      toras = o TR agacha no pé, protegido de quem olha lá de cima; meio muro = o TR agacha no topo da diagonal e
#      levanta pra pegar os barris; barris (o "carro", como a Valve trocou) = quina do CT de onde se vê o banana até as
#      toras, por isso o TR sobe com molotov; caixa = o CT agacha do outro lado do patamar e pega quem chega perto;
#      topo do corredor = o CT segura a saída do patamar sem ver o banana.
bloco('r', 10, 12, 28, 32); bloco('r', 13, 14, 31, 32); nome("Pé do banana", 8, 31)
bloco('o', 12, 12, 29, 29); nome("Toras", 13.6, 29.4)
faixa('r', [(11.5, 28.5), (17.5, 19.5)], 3.6); nome("Banana", 12, 23.6)
bloco('r', 14, 21, 16, 19); nome("Alto do banana", 12, 18)
bloco('o', 16, 18, 20, 20); nome("Meio muro", 20.2, 21.4)
bloco('r', 14, 14, 20, 20)                                                   # passagem de 2 do lado do meio muro
bloco('o', 20, 20, 17, 18); nome("Barris (carro)", 23, 17.6)
bloco('o', 15, 15, 17, 17); nome("Caixa", 13, 16.6)
bloco('r', 17, 19, 9, 15); nome("Corredor", 21.2, 13.4)
bloco('r', 16, 16, 9, 10)                                                    # porta do banana no B, no alto do corredor
# v7: sacada da construção — o CT sobe a escada na construção, atravessa a salinha e PULA no corredor (não tem volta:
#     o TR não sobe por ali). É a 3a porta do retake do B (por trás de quem plantou) e um jeito do CT voltar pro banana.
bloco('s', 22, 22, 13, 13); bloco('k', 21, 21, 12, 13); bloco('~', 20, 20, 12, 12); nome("Sacada", 21.5, 11.4)   # escada no fundo da construção

# ---- BOMB B (fechado, cheio de canto: quem entra pela porta do banana tem que limpar escuro, 1ª caixa, fonte, tripla
#      e o caixão lá em cima, um de cada vez)
bloco('b', 3, 15, 6, 14);  nome("Bomb B", 8.5, 15.4)
bloco('n', 3, 6, 6, 8);    nome("Caixão", 4.5, 5.2)
bloco('s', 7, 7, 6, 7)
bloco('o', 13, 13, 8, 9);  nome("Escuro", 13.8, 5.4)                         # mais pra dentro da quina, longe da borda do plant
bloco('o', 8, 9, 10, 11);  nome("Fonte", 8.5, 9.2)
bloco('o', 4, 5, 12, 13);  nome("Tripla", 3.6, 14.6)
bloco('o', 13, 14, 13, 14); nome("1ª caixa", 12, 15.4)                      # encostada na parede de baixo, idem
bloco('o', 4, 4, 9, 9);   nome("Cimento", 4, 10.2)                           # saiu da quina do plant (com o escuro fazia um esconderijo)
# CT -> B: pista (por cima, porta no norte do B), construção (porta no leste, na quina do escuro)
bloco('r', 8, 27, 2, 4); bloco('r', 26, 30, 4, 6);  nome("Pista", 21, 1.4)       # passagem larga (3) da base CT pra pista
bloco('r', 9, 11, 5, 5); nome("Porta da pista", 10, 1.2)
bloco('o', 19, 20, 4, 4); bloco('o', 14, 14, 2, 2)                            # barris e caixote: quebram a linha da base CT até o B
bloco('r', 23, 27, 6, 13); nome("Construção", 25, 9.4)
bloco('o', 24, 25, 10, 10)
bloco('o', 27, 27, 6, 6)                                                     # barris: quebram a linha da biblioteca até o B pela passagem
bloco('r', 24, 25, 5, 5); bloco('r', 16, 22, 6, 7)

# ---- MEIO: rua reta e comprida do meio de baixo até o alto do meio, do lado do A. O banana fica de um lado, o MF e
#      o apê do outro (como na Inferno). O CT segura o alto pelos dois ângulos (NIP e xuxa).
bloco('m', 23, 36, 29, 33); nome("Meio", 29.5, 31)
bloco('o', 28, 28, 29, 30); nome("Carroça do meio", 28, 27.8)                  # meia cobertura: esconde o TR da xuxa, não do NIP
bloco('o', 33, 33, 33, 33)
bloco('r', 35, 41, 25, 33); nome("Alto do meio", 38.5, 34.6)
bloco('o', 22, 22, 30, 32); nome("Barris", 22.6, 29.4)
bloco('o', 39, 39, 27, 28)
nome("NIP", 40.6, 26.2); nome("Xuxa", 42.6, 33.6)                          # NIP atrás do caixote, xuxa na quina de baixo: cruzam o meio
faixa('r', [(40.5, 8), (40.5, 13), (36, 17), (36, 24.5)], 3); nome("Arco", 37.6, 11)

# ---- A: curta (do alto do meio), longo (do arco), biblioteca (da base CT), varanda (do apê) e poço
bloco('r', 38, 42, 18, 20); nome("Longo", 42, 17)                          # v7: liga no arco (era beco sem saída)
bloco('r', 42, 42, 22, 26); nome("Curta", 44, 21.2)
bloco('o', 42, 42, 22, 22); nome("Carroça", 40, 21.4)
bloco('r', 42, 52, 5, 7);   nome("Biblioteca", 45, 4.2)
bloco('s', 51, 52, 8, 8); bloco('n', 51, 52, 9, 11)        # v7: desce no cemitério (alto do A), não no meio do site
bloco('a', 43, 53, 12, 27); nome("Bomb A", 48.5, 19)
bloco('n', 50, 53, 12, 14); nome("Cemitério", 51.5, 15.4)
bloco('s', 49, 49, 12, 13)
bloco('o', 46, 47, 17, 17); nome("Areia", 46.5, 16)
bloco('o', 49, 50, 22, 22); bloco('o', 45, 45, 24, 24); bloco('o', 52, 52, 18, 19)
bloco('a', 49, 53, 28, 35); nome("Poço", 51, 36.4)
bloco('~', 47, 48, 28, 31); nome("Varanda", 47.6, 32.6)                         # dá pro A e pro poço: o site e a biblioteca veem a varanda

# ---- APÊ CT (2o andar, do lado do alto do meio): o CT entra pela caldeira, o TR pela ponte (tapete) ou pela janela (MF)
bloco('k', 43, 46, 29, 38); nome("Apê CT", 44.5, 33.5)
bloco('s', 42, 42, 29, 30); nome("Caldeira", 40.2, 29.6)
# ---- MF (meio falso): rua estreita paralela ao meio, do lado de baixo; caverna liga ao meio; janela sobe no apê.
#      A base TR tem uma 2a saída direto no MF (como a Valve abriu na Inferno).
bloco('r', 24, 38, 37, 38); nome("MF", 30.5, 39.6)
bloco('r', 29, 30, 34, 36); nome("Caverna", 32.4, 35)
bloco('s', 39, 42, 37, 38); nome("Janela", 40.5, 36.2)
bloco('r', 24, 25, 39, 48)
# ---- TAPETE (apê do TR, 2o andar): rua saindo da base pro leste, escada, sala, ponte e apê CT
bloco('r', 16, 28, 49, 51)
bloco('s', 29, 30, 49, 50)
bloco('k', 31, 38, 42, 49); nome("Tapete (sala)", 34.5, 45.5)
bloco('k', 39, 44, 41, 42); bloco('k', 43, 44, 39, 40); nome("Ponte", 41.5, 43.6)

SPAWN_T = [(4, 49), (7, 51), (9, 49), (12, 51), (14, 49)]
SPAWN_CT = [(32, 3), (35, 6), (38, 3), (33, 7), (40, 6)]
SITES = {"A": (46, 51, 16, 22), "B": (6, 11, 8, 13)}         # área de plantar (tamanho da Inferno: ~5 a 6 casas de lado), não o site todo
PONTOS = [
    ("Pé da banana (toras)", 11, 31), ("Meio da banana", 14, 24), ("Carro (alto da banana)", 18, 17), ("Bomb B", 9, 13),
    ("Meio de baixo", 17, 32), ("Meio (centro)", 30, 31), ("Alto do meio", 39, 29), ("Caverna", 29, 35),
    ("MF (alto)", 36, 37), ("Tapete (sala)", 34, 46), ("Apê CT", 44, 33), ("Varanda", 47, 30), ("Poço", 51, 32),
    ("Construção", 25, 8), ("Bomb A", 48, 19),
]
# posições de jogo (x, y, lado, agachado/em pé, pra que serve) — angulos.py confere quem vê quem
POSICOES = {
    "Toras (TR)": (11, 30, 'T', 'ag', "TR agachado atrás das toras, no pé do banana"),
    "Meio muro (TR)": (17, 21, 'T', 'ag', "TR agacha no topo da diagonal; levanta e pega os barris"),
    "TR no patamar": (17, 18, 'T', 'pe', "TR que subiu o banana"),
    "TR na porta do B": (15, 10, 'T', 'pe', "TR que acabou de entrar no B"),
    "Barris (CT)": (21, 17, 'C', 'pe', "CT na quina dos barris (o carro): vê o banana até as toras"),
    "Caixa do alto (CT)": (15, 16, 'C', 'ag', "CT agachado atrás da caixa, pega quem chega no patamar"),
    "Topo do corredor (CT)": (18, 9, 'C', 'pe', "CT no alto do corredor: segura quem sai do patamar"),
    "1ª caixa (CT)": (15, 13, 'C', 'ag', "abaixo da porta, pega quem entra de lado"),
    "Escuro (CT)": (14, 7, 'C', 'pe', "quina escura acima da porta, cruza com a 1ª caixa"),
    "Fonte (CT)": (9, 12, 'C', 'pe', "meio do site, atrás da fonte"),
    "Tripla (CT)": (6, 13, 'C', 'ag', "atrás da pilha de caixotes, ângulo comprido na porta"),
    "Caixão (CT)": (5, 7, 'C', 'pe', "no alto do caixão, ângulo de cabeça"),
}


def regras_angulos(ve):
    """Regras tiradas da Inferno real (linha de visão medida no radar dela): o canto dos barris vê o banana até as
    toras; o corredor que leva ao B não vê o banana; o TR tem cobertura agachado nas toras e no meio muro; o B se limpa
    um ângulo por vez, na porta."""
    P = POSICOES
    p = lambda n: P[n][:2]
    st = lambda n: P[n][3]
    falhas = []
    if not ve(p("Barris (CT)"), (14, 24), 'pe', 'pe'):
        falhas.append("dos barris o CT não vê o meio do banana (na Inferno o canto do carro vê até as toras)")
    for t in ("Toras (TR)", "Meio muro (TR)"):
        for ct in ("Barris (CT)", "Caixa do alto (CT)", "Topo do corredor (CT)"):
            if ve(p(ct), p(t), st(ct), 'ag'):
                falhas.append(f"{ct} vê o TR agachado em {t} (a cobertura não protege)")
    if not ve(p("Meio muro (TR)"), p("Barris (CT)"), 'pe', 'pe'):
        falhas.append("de pé no meio muro o TR não pega os barris (o meio muro não mira em nada)")
    for ct in ("Caixa do alto (CT)", "Topo do corredor (CT)"):
        if ve(p(ct), (14, 24), st(ct), 'pe') or ve(p(ct), (11, 31), st(ct), 'pe'):
            falhas.append(f"{ct} vê o meio/pé do banana (o corredor tinha que ficar de lado)")
    for ct in ("Barris (CT)", "Caixa do alto (CT)", "Topo do corredor (CT)"):
        if not ve(p(ct), p("TR no patamar"), st(ct), 'pe'):
            falhas.append(f"{ct} não pega o TR que chega no patamar")
    sitio = ["1ª caixa (CT)", "Escuro (CT)", "Fonte (CT)", "Tripla (CT)", "Caixão (CT)"]
    na_porta = [n for n in sitio if ve(p(n), p("TR na porta do B"), st(n), 'pe')]
    if len(na_porta) < 4:
        falhas.append(f"só {len(na_porta)} posições do B pegam quem entra ({', '.join(na_porta)})")
    do_corredor = [n for n in sitio if ve((18, 12), p(n), 'pe', st(n)) or ve((17, 11), p(n), 'pe', st(n))]
    if len(do_corredor) > 1:
        falhas.append(f"antes da porta o TR já vê {', '.join(do_corredor)} (tinha que limpar um por vez)")
    if any(ve(p("TR no patamar"), p(n), 'pe', st(n)) for n in sitio):
        falhas.append("do patamar já se vê dentro do B")
    return falhas


ROTAS = [("TR banana", 9, 12, 'T'), ("TR meio-curta", 45, 24, 'T'), ("TR tapete", 51, 32, 'T'), ("TR MF", 44, 33, 'T'),
         ("CT B", 18, 17, 'C'), ("CT A", 48, 14, 'C'), ("CT apê", 44, 33, 'C'), ("CT meio", 30, 31, 'C')]
ROT_TR = ((14, 24), (47, 30))            # rotação do TR: meio da banana -> varanda (na Inferno: 11,1 s)
# metas = Inferno medida no radar x 1,25 (TR, CT)
META = {"Pé da banana (toras)": (11.9, 19.0), "Meio da banana": (14.6, 17.0), "Carro (alto da banana)": (19.1, 12.4),
        "Bomb B": (22.3, 12.8), "Meio de baixo": (11.5, 16.3), "Meio (centro)": (14.1, 13.8), "Alto do meio": (18.0, 9.8),
        "Varanda": (20.4, 13.5), "Poço": (24.9, 12.6), "Construção": (24.9, 7.0), "Bomb A": (23.9, 9.6)}
# ---------------------------------------------------------------------------------------------------------------------
# ALTURAS, ZONAS E COBERTURAS (o que o MapData e a maquete 3D precisam além da planta)
#  z = piso acima do chão (MapData.GZ), em unidades da Sim (boneco = 25). O 2o andar fica a +30 (igual à Vila: UP).
#  O lado TR é baixo (0), o meio fica a +6, o B e o lado CT a +18: o banana e o arco são as subidas.
# ---------------------------------------------------------------------------------------------------------------------
Z = [[None] * W for _ in range(H)]          # piso
RAMPA = [[False] * W for _ in range(H)]     # piso inclinado (escada/rampa)
ZONA = [[None] * W for _ in range(H)]       # nome da área (callout)
MAT = [[None] * W for _ in range(H)]        # material do chão (MapData.Floor)


def R(x0, x1, y0, y1):
    return lambda x, y: x0 <= x <= x1 and y0 <= y <= y1


def F(pts, larg):
    r = larg / 2

    def tem(x, y):
        px, py = x + .5, y + .5
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            t = max(0, min(1, ((px - x0) * dx + (py - y0) * dy) / max(1e-9, dx * dx + dy * dy)))
            if math.hypot(px - x0 - t * dx, py - y0 - t * dy) <= r:
                return True
        return False
    return tem


def lin(eixo, a0, z0, a1, z1):
    """Rampa: z vai de z0 a z1 enquanto o eixo ('x' ou 'y') vai de a0 a a1 (centro da casa)."""
    def f(x, y):
        a = (x if eixo == 'x' else y) + .5
        t = max(0.0, min(1.0, (a - a0) / (a1 - a0)))
        return z0 + (z1 - z0) * t
    f.rampa = True
    return f


def area(nome_, mascaras, z, mat='Stone'):
    if not isinstance(mascaras, list):
        mascaras = [mascaras]
    for y in range(H):
        for x in range(W):
            if g[y][x] in '#o' or not any(m(x, y) for m in mascaras):
                continue
            Z[y][x] = round(z(x, y), 1) if callable(z) else z
            RAMPA[y][x] = callable(z) and getattr(z, 'rampa', False)
            ZONA[y][x] = nome_; MAT[y][x] = mat


area("Base TR", R(3, 15, 45, 52), 0)
area("Rampa", F([(5.5, 45), (8, 40.5), (14, 40), (17.5, 36)], 4), lin('y', 44, 0, 36, 6))
area("Pé do banana", [R(10, 12, 28, 32), R(13, 14, 31, 32)], 6, 'Dirt')
area("Banana", F([(11.5, 28.5), (17.5, 19.5)], 3.6), lin('y', 28.5, 6, 19.5, 18), 'Dirt')
area("Alto do banana", R(14, 21, 16, 19), 18, 'Dirt')
area("Banana", R(14, 14, 20, 20), lin('y', 28.5, 6, 19.5, 18), 'Dirt')
area("Corredor do banana", [R(17, 19, 9, 15), R(16, 16, 9, 10)], 18)
area("Sacada", R(22, 22, 13, 13), 24)
RAMPA[13][22] = True
area("Sacada", [R(21, 21, 12, 13), R(20, 20, 12, 12)], 30, 'Wood')
area("Meio de baixo", R(15, 22, 30, 36), 6)
area("Bomb B", R(3, 15, 6, 14), 18, 'Tile')
area("Caixão", R(3, 6, 6, 8), 30, 'Wood')
area("Caixão", R(7, 7, 6, 7), 24)
for x, y in ((7, 6), (7, 7)):
    RAMPA[y][x] = True
area("Pista", [R(8, 27, 2, 4), R(26, 30, 4, 6), R(9, 11, 5, 5)], 18)
area("Construção", [R(23, 27, 6, 13), R(24, 25, 5, 5), R(16, 22, 6, 7)], 18, 'Dirt')
area("Base CT", R(31, 41, 2, 8), 18)
area("Arco", F([(40.5, 8), (40.5, 13), (36, 17), (36, 24.5)], 3), lin('y', 9, 18, 24, 6))
area("Longo", R(38, 42, 18, 20), lin('x', 38, 10.4, 43, 6))
area("Meio", R(23, 36, 29, 33), 6, 'Tile')
area("Alto do meio", R(35, 41, 25, 33), 6, 'Tile')
area("Curta", R(42, 42, 22, 26), 6)
area("Bomb A", R(43, 53, 12, 27), 6, 'Tile')
area("Cemitério", R(50, 53, 12, 14), 18, 'Dirt')
area("Cemitério", R(49, 49, 12, 13), lin('x', 48.5, 6, 50.5, 18))
area("Biblioteca", R(42, 52, 5, 7), 18, 'Wood')
area("Biblioteca", R(51, 52, 8, 11), 18, 'Wood')
area("Poço", R(49, 53, 28, 35), 0, 'Dirt')
area("Poço", R(49, 53, 28, 29), lin('y', 27.5, 6, 30, 0), 'Dirt')
area("Varanda", R(47, 48, 28, 31), 30, 'Wood')
area("Apê CT", R(43, 46, 29, 38), 30, 'Wood')
area("Caldeira", R(42, 42, 29, 30), 18)
for y in (29, 30):
    RAMPA[y][42] = True
area("MF", [R(24, 38, 37, 38), R(24, 25, 39, 48)], 0)
area("Caverna", R(29, 30, 34, 36), lin('y', 33.5, 6, 36.5, 0), 'Cave')
area("Janela", R(39, 42, 37, 38), lin('x', 38.5, 0, 42.5, 30))
area("Rua do tapete", R(16, 28, 49, 51), 0)
area("Tapete", R(29, 30, 49, 50), lin('x', 28.5, 0, 30.5, 30))
area("Tapete", R(31, 38, 42, 49), 30, 'Wood')
area("Ponte", [R(39, 44, 41, 42), R(43, 44, 39, 40)], 30, 'Wood')

# coberturas: (x0, x1, y0, y1, altura acima do piso, tipo, nome). Na Inferno a Valve trocou o carro do banana por barris
# que a bala atravessa: aqui também são barris.
COB = [(19, 21, 34, 34, 14, "cart", "Carroça"), (12, 12, 29, 29, 10, "logs", "Toras"), (16, 18, 20, 20, 16, "halfwall", "Meio muro"),
       (20, 20, 17, 18, 15, "barrel", "Barris (carro)"), (15, 15, 17, 17, 16, "crate", "Caixa do alto"), (13, 13, 8, 9, 26, "wall", "Escuro"),
       (8, 9, 10, 11, 10, "fountain", "Fonte"), (4, 5, 12, 13, 20, "crate", "Tripla"), (13, 14, 13, 14, 16, "crate", "1ª caixa"),
       (4, 4, 9, 9, 10, "sacks", "Cimento"), (19, 20, 4, 4, 13, "barrel", "Pista"), (14, 14, 2, 2, 12, "crate", "Pista"), (24, 25, 10, 10, 12, "crate", "Construção"), (27, 27, 6, 6, 16, "barrel", "Construção"), (28, 28, 29, 30, 16, "cart", "Carroça do meio"),
       (33, 33, 33, 33, 12, "barrel", "Meio"), (22, 22, 30, 32, 16, "barrel", "Barris"), (39, 39, 27, 28, 16, "crate", "NIP"),
       (42, 42, 22, 22, 14, "cart", "Carroça da curta"), (46, 47, 17, 17, 8, "sacks", "Areia"), (49, 50, 22, 22, 12, "crate", "Bomb A"),
       (45, 45, 24, 24, 12, "crate", "Bomb A"), (52, 52, 18, 19, 12, "crate", "Bomb A")]


def piso_cob(x, y):
    """Piso embaixo de uma cobertura = o piso de uma casa vizinha andável."""
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
        u, v = x + dx, y + dy
        if 0 <= u < W and 0 <= v < H and Z[v][u] is not None:
            return Z[v][u]
    return 0


def topo_predio(x, y):
    """Altura do telhado (acima de GZ) de uma casa de prédio: o piso mais alto ao redor + 36 a 44 (lote)."""
    m = 0
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            u, v = x + dx, y + dy
            if 0 <= u < W and 0 <= v < H and Z[v][u] is not None:
                m = max(m, Z[v][u])
    lote = (x // 3 * 73856093 ^ y // 3 * 19349663) & 0x7fffffff
    return m + (36, 40, 44, 40)[lote % 4]

NIVEL = {'k': 2, '~': 2, 'n': 1}


def carregar_vila():
    """Planta da Vila (Tools/planta.txt) com as alturas dela: p/w palácio/janelão +14, k casarão, P/n plataforma +8."""
    global g, SPAWN_T, SPAWN_CT, PONTOS, SITES, NOMES, W, H
    W, H = 40, 35
    linhas = [l.rstrip('\n').lstrip('﻿') for l in open(os.path.join(AQUI, '..', 'planta.txt'), encoding='utf-8')]
    linhas = [l for l in linhas if len(l) >= W]
    g = [list(l[:W]) for l in linhas[:H]]
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            g[y][x] = {'p': 'k', 'w': 'k', 'P': 'n', 'v': 's'}.get(c, c)
    SPAWN_T = [(15, 31), (17, 32), (20, 31), (22, 32), (24, 31)]
    SPAWN_CT = [(20, 2), (22, 3), (24, 2), (26, 3), (21, 4)]
    SITES = {"A": (31, 38, 4, 11), "B": (2, 9, 6, 15)}
    NOMES = []
    PONTOS = [("Bomb A", 34, 8), ("Bomb B", 6, 11), ("Meio", 18, 16), ("Rampa (topo)", 36, 13), ("Curta", 12, 16)]


def nivel(c):
    return NIVEL.get(c, 0)


def anda(c):
    return c not in '#o'


def pode(a, b):
    if a == 's' or b == 's':
        return True
    la, lb = nivel(a), nivel(b)
    if la == lb:
        return True
    return la > lb and a in '~n'          # da sacada/plataforma pula pra baixo; subir só por escada


def tempos(fontes):
    INF = 1e9
    t = [[INF] * W for _ in range(H)]
    pai = {}
    fila = []
    for (x, y) in fontes:
        t[y][x] = 0
        heapq.heappush(fila, (0, x, y))
    while fila:
        d, x, y = heapq.heappop(fila)
        if d > t[y][x]:
            continue
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if not dx and not dy:
                    continue
                u, v = x + dx, y + dy
                if not (0 <= u < W and 0 <= v < H) or not anda(g[v][u]) or not pode(g[y][x], g[v][u]):
                    continue
                if dx and dy and not (anda(g[y][u]) and anda(g[v][x]) and pode(g[y][x], g[y][u]) and pode(g[y][x], g[v][x])):
                    continue          # não corta quina
                custo = math.hypot(dx, dy) * (1.25 if 's' in (g[y][x], g[v][u]) else 1.0)
                if d + custo < t[v][u]:
                    t[v][u] = d + custo
                    pai[(u, v)] = (x, y)
                    heapq.heappush(fila, (d + custo, u, v))
    return [[(c / VEL if c < INF else None) for c in linha] for linha in t], pai


def caminho(pai, alvo):
    p = [alvo]
    while p[-1] in pai:
        p.append(pai[p[-1]])
    return p[::-1]


def media_site(t, s):
    x0, x1, y0, y1 = s
    v = [t[y][x] for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if t[y][x] is not None]
    return min(v)


def main():
    vila = len(sys.argv) > 1 and sys.argv[1] == 'vila'
    if vila:
        carregar_vila()
    tT, paiT = tempos(SPAWN_T)
    tC, paiC = tempos(SPAWN_CT)
    print(f"{'ponto':28s} {'TR':>6s} {'CT':>6s}  primeiro")
    linhas = []
    for (n, x, y) in PONTOS:
        a, b = tT[y][x], tC[y][x]
        quem = "CT" if b < a else "TR"
        m = globals().get("META", {}).get(n)
        print(f"{n:28s} {a:6.1f} {b:6.1f}  {quem} por {abs(a - b):.1f}s" + (f"   meta {m[0]:.1f}/{m[1]:.1f}" if m else ""))
        m = globals().get("META", {}).get(n)
        linhas.append({"nome": n, "x": x, "y": y, "tr": round(a, 1), "ct": round(b, 1), "meta": m})
    # rotações: do site ao outro site (entrada mais perto) pra cada lado
    def rot(t_de, s):
        x0, x1, y0, y1 = s
        return min(t_de[y][x] for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if t_de[y][x] is not None)
    cA = SITES["A"]; cB = SITES["B"]
    meioA = ((cA[0] + cA[1]) // 2, (cA[2] + cA[3]) // 2); meioB = ((cB[0] + cB[1]) // 2, (cB[2] + cB[3]) // 2)
    gA, _ = tempos([meioA]); gB, _ = tempos([meioB])
    rotAB = rot(gA, cB); rotBA = rot(gB, cA)
    print(f"rotação A->B {rotAB:.1f}s   B->A {rotBA:.1f}s  (do centro de um site até pisar no outro)")
    (lx, ly), (mx, my) = ROT_TR
    gL, _ = tempos([(lx, ly)])
    rotT = gL[my][mx]
    print(f"rotação do TR: meio da banana -> varanda {rotT:.1f}s")
    print(f"primeiro a pisar: A  TR {media_site(tT, cA):.1f}s CT {media_site(tC, cA):.1f}s   "
          f"B  TR {media_site(tT, cB):.1f}s CT {media_site(tC, cB):.1f}s")
    if vila:
        return
    with open(os.path.join(AQUI, 'proposta', 'brasa.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(''.join(l) for l in g) + '\n')
    rotas = {}
    for (n, x, y, lado) in ROTAS:
        rotas[n] = caminho(paiT if lado == 'T' else paiC, (x, y))
    dados = {"W": W, "H": H, "grade": [''.join(l) for l in g], "tr": tT, "ct": tC, "nomes": NOMES, "pontos": linhas,
             "rotas": rotas, "spawnT": SPAWN_T, "spawnCT": SPAWN_CT, "sites": SITES,
             "rot": {"AB": round(rotAB, 1), "BA": round(rotBA, 1), "TR": round(rotT, 1)},
             "pisa": {"A": [round(media_site(tT, cA), 1), round(media_site(tC, cA), 1)],
                      "B": [round(media_site(tT, cB), 1), round(media_site(tC, cB), 1)]}}
    mapa = exportar()
    dados["z"] = mapa["piso"]; dados["topo"] = mapa["topo"]; dados["cob"] = mapa["coberturas"]
    with open(os.path.join(AQUI, 'proposta', 'brasa.json'), 'w', encoding='utf-8') as f:
        json.dump(dados, f, ensure_ascii=False)
    import angulos
    dados["posicoes"] = [{"nome": n, "x": v[0], "y": v[1], "lado": v[2], "pose": v[3], "papel": v[4],
                          "ve": [m for m, w in POSICOES.items() if m != n and angulos.ve(v[:2], w[:2], v[3], w[3])]}
                         for n, v in POSICOES.items()]
    with open(os.path.join(AQUI, 'proposta', 'brasa.json'), 'w', encoding='utf-8') as f:
        json.dump(dados, f, ensure_ascii=False)
    import visao
    visao.brasa()
    for l in g:
        print(''.join(l))


def checar():
    """Toda casa andável tem piso e zona; toda casa de cobertura está numa cobertura da lista; nada de degrau alto sem
    escada (o boneco sobe sozinho até 10, igual ao MapData.JUMP_STEP; acima disso só por escada/rampa ou descendo)."""
    erros = []
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            if c == 'o' and not any(a <= x <= b and cc <= y <= d for a, b, cc, d, *_ in COB):
                erros.append(f"cobertura sem ficha em ({x},{y})")
            if c in '#o':
                continue
            if Z[y][x] is None:
                erros.append(f"casa sem altura em ({x},{y}) {c}")
                continue
            for dx, dy in ((1, 0), (0, 1)):
                u, v = x + dx, y + dy
                if u < W and v < H and Z[v][u] is not None and g[v][u] not in '#o':
                    dz = abs(Z[v][u] - Z[y][x])
                    if dz > 10 and not (RAMPA[y][x] or RAMPA[v][u] or g[y][x] in '~n' or g[v][u] in '~n'):
                        erros.append(f"degrau de {dz:.0f} entre ({x},{y}) {c} e ({u},{v}) {g[v][u]}")
    for y in range(H - 1):
        for x in range(W - 1):
            a, b, c, d = g[y][x], g[y][x + 1], g[y + 1][x], g[y + 1][x + 1]
            if (a != '#' and d != '#' and b == '#' and c == '#') or (b != '#' and c != '#' and a == '#' and d == '#'):
                erros.append(f"dois caminhos se encostam só pela quina em ({x},{y})-({x + 1},{y + 1})")
    return erros


def exportar():
    """brasa_mapa.json: tudo que o MapData precisa pra montar a Brasa (casas de 25 unidades da Sim)."""
    topo = [[None if g[y][x] != '#' else topo_predio(x, y) for x in range(W)] for y in range(H)]
    zonas = sorted({ZONA[y][x] for y in range(H) for x in range(W) if ZONA[y][x]})
    mapa = {
        "nome": "Brasa", "Q": 25, "W": W, "H": H, "NX": W * 25, "NY": H * 25, "GZ": 8, "UP": 30,
        "legenda": {"#": "prédio", "T": "base TR", "C": "base CT", "a": "bomb A", "b": "bomb B", "r": "rua", "m": "meio (ladrilho)",
                    "k": "2o andar", "n": "plataforma (sobe por escada, desce pulando)", "s": "escada",
                    "~": "sacada (2o andar, só desce)", "o": "cobertura"},
        "celulas": [''.join(l) for l in g],
        "piso": Z, "rampa": [''.join('1' if v else '0' for v in l) for l in RAMPA],
        "material": MAT, "topo": topo,
        "zonas": zonas, "zona": [[zonas.index(z) if z else -1 for z in l] for l in ZONA],
        "coberturas": [{"x0": a, "x1": b, "y0": c, "y1": d, "h": h, "tipo": t, "nome": n, "piso": piso_cob(a, c)}
                       for a, b, c, d, h, t, n in COB],
        "sites": {k: {"x0": v[0], "x1": v[1], "y0": v[2], "y1": v[3]} for k, v in SITES.items()},
        "spawnT": SPAWN_T, "spawnCT": SPAWN_CT,
        "nomes": NOMES,
    }
    with open(os.path.join(AQUI, 'proposta', 'brasa_mapa.json'), 'w', encoding='utf-8') as f:
        json.dump(mapa, f, ensure_ascii=False)
    for e in checar():
        print("CHECAR:", e)
    return mapa




def pagina(saida):
    """Junta brasa.json (e a imagem 3D) na pagina.html (artifact da planta)."""
    import base64, render3d
    img = os.path.join(AQUI, '..', '..', 'Logs', 'render', 'brasa_3d_se.png')
    render3d.main(img, 'se')
    d = json.load(open(os.path.join(AQUI, 'proposta', 'brasa.json'), encoding='utf-8'))
    d["img3d"] = "data:image/png;base64," + base64.b64encode(open(img, 'rb').read()).decode()
    d["rot3d"] = json.load(open(img[:-4] + '.json', encoding='utf-8'))
    dados = json.dumps(d, ensure_ascii=False)
    html = open(os.path.join(AQUI, 'pagina.html'), encoding='utf-8').read().replace('/*DADOS*/null', dados)
    open(saida, 'w', encoding='utf-8').write(html)


if __name__ == '__main__':
    main()
    if len(sys.argv) > 2 and sys.argv[1] == 'pagina':
        pagina(sys.argv[2])
