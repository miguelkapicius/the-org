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
bloco('T', 3, 15, 46, 51); bloco('T', 3, 8, 43, 45); nome("Base TR", 9, 48.5)
bloco('C', 31, 41, 2, 8);  nome("Base CT", 36, 5)

# ---- rampa do TR e meio de baixo (praça de onde saem banana e meio)
faixa('r', [(5.5, 43), (6, 38), (11, 35), (19, 35)], 4); nome("Rampa", 9, 32.6)
bloco('r', 18, 25, 31, 36); nome("Meio de baixo", 21.5, 37.4)
bloco('o', 21, 22, 33, 33)

# ---- BANANA: única entrada do TR no B; sobe em diagonal, com toras no pé, meio muro no meio e o carro no alto
faixa('r', [(20, 32), (17, 27), (16, 23), (14, 19)], 3.6); nome("Banana", 14.4, 26)
bloco('o', 19, 19, 29, 30); nome("Toras", 21.6, 29.2)
bloco('o', 15, 15, 24, 24); nome("Meio muro", 13, 23.8)
bloco('r', 11, 16, 17, 19)                                                  # alto da banana (boca do B)
bloco('o', 16, 16, 18, 19); nome("Carro", 18, 18.6)

# ---- BOMB B (fechado, cheio de canto)
bloco('b', 3, 15, 6, 16);  nome("Bomb B", 8.5, 12.8)
bloco('n', 3, 6, 6, 8);    nome("Caixão", 4.5, 5.2)
bloco('s', 7, 7, 6, 7)
bloco('o', 13, 15, 6, 6);  nome("Escuro", 13.8, 7.6)
bloco('o', 8, 9, 10, 11);  nome("Fonte", 8.5, 9.2)
bloco('o', 4, 5, 13, 14);  nome("Tripla", 4.6, 15.6)
bloco('o', 12, 12, 13, 14); nome("1ª caixa", 12, 12)
bloco('o', 15, 15, 11, 11); nome("Cimento", 16.8, 11.6)
# CT -> B: pista (por cima) e construção (porta no leste do B)
bloco('r', 16, 30, 2, 4);  nome("Pista", 23, 1.4)
bloco('r', 19, 27, 7, 13); nome("Construção", 21, 9)
bloco('o', 22, 23, 10, 10)
bloco('r', 24, 25, 5, 6); bloco('r', 16, 18, 10, 11)

# ---- MEIO: diagonal do meio de baixo ao alto; o CT segura o alto (NIP e xuxa)
faixa('m', [(25, 33), (30, 29.5), (34, 26)], 4); nome("Meio", 27.2, 31.8)
bloco('o', 29, 29, 26, 26); nome("Banco", 27.4, 25.8)
bloco('r', 32, 40, 22, 25); nome("Alto do meio", 36, 23.5)
nome("NIP", 31, 21.2); nome("Xuxa", 37.2, 21.2)
faixa('r', [(40.5, 8), (40.5, 13), (38.5, 17), (38.5, 22)], 3); nome("Arco", 37.4, 11.5)

# ---- A: curta (do alto do meio), longo (do arco), biblioteca (da base CT), varanda (do apê) e poço
bloco('r', 40, 42, 13, 15); nome("Longo", 41, 11.8)
bloco('r', 40, 42, 23, 25); nome("Curta", 41.4, 21.6)
bloco('o', 42, 42, 23, 23); nome("Carroça", 44.4, 22.4)
bloco('r', 42, 47, 3, 5);   nome("Biblioteca", 45, 2.2)
bloco('r', 46, 48, 6, 11)
bloco('a', 43, 53, 12, 27); nome("Bomb A", 48.5, 19)
bloco('n', 50, 53, 12, 14); nome("Cemitério", 51.5, 15.4)
bloco('s', 49, 49, 12, 13)
bloco('o', 46, 47, 17, 17); nome("Areia", 46.5, 16)
bloco('o', 51, 51, 21, 22); bloco('o', 45, 45, 24, 24)
bloco('a', 46, 52, 28, 34); nome("Poço", 49, 31.5)
bloco('~', 43, 45, 29, 31); nome("Varanda", 44, 32.6)

# ---- APÊ CT (2o andar): o CT entra pela caldeira (desce da curta), o TR pelo tapete ou pela janela do MF
bloco('k', 37, 42, 28, 33); nome("Apê CT", 39.5, 30.5)
bloco('s', 40, 41, 26, 27); nome("Caldeira", 42.8, 27)
# ---- MF (meio falso): sai da rua do tapete, sobe paralelo ao meio; caverna liga ao meio; janela sobe no apê
bloco('r', 28, 29, 36, 46); nome("MF", 26.8, 41)
bloco('r', 29, 36, 34, 35)
bloco('s', 35, 36, 32, 33); nome("Janela", 34.4, 31.4)
bloco('r', 27, 28, 31, 35); nome("Caverna", 25.8, 34.8)
# ---- TAPETE (apê do TR, 2o andar): rua saindo da base pro leste, escada, sala, ponte e apê CT
bloco('r', 16, 28, 47, 49)
bloco('s', 29, 30, 47, 48)
bloco('k', 31, 38, 43, 49); nome("Tapete (sala)", 34.5, 46)
bloco('k', 38, 40, 34, 42); nome("Ponte", 42, 38)

SPAWN_T = [(4, 47), (7, 50), (9, 47), (12, 50), (14, 47)]
SPAWN_CT = [(32, 3), (35, 6), (38, 3), (33, 7), (40, 6)]
SITES = {"A": (43, 53, 12, 27), "B": (3, 15, 6, 16)}
PONTOS = [
    ("Pé da banana (toras)", 19, 31), ("Meio da banana", 16, 25), ("Carro (alto da banana)", 14, 18), ("Bomb B", 9, 13),
    ("Meio de baixo", 21, 34), ("Meio (centro)", 29, 30), ("Alto do meio", 36, 23), ("Caverna", 27, 33),
    ("MF (alto)", 33, 34), ("Tapete (sala)", 34, 46), ("Apê CT", 39, 30), ("Varanda", 44, 30), ("Poço", 49, 31),
    ("Construção", 21, 9), ("Bomb A", 48, 19),
]
ROTAS = [("TR banana", 9, 12, 'T'), ("TR meio-curta", 45, 23, 'T'), ("TR tapete", 48, 31, 'T'), ("TR MF", 39, 30, 'T'),
         ("CT B", 14, 18, 'C'), ("CT A", 48, 14, 'C'), ("CT apê", 39, 30, 'C'), ("CT meio", 32, 26, 'C')]
ROT_TR = ((16, 25), (44, 30))            # rotação do TR: meio da banana -> varanda (na Inferno: 11,1 s)
# metas = Inferno medida no radar x 1,25 (TR, CT)
META = {"Pé da banana (toras)": (11.9, 19.0), "Meio da banana": (14.6, 17.0), "Carro (alto da banana)": (19.1, 12.4),
        "Bomb B": (22.3, 12.8), "Meio de baixo": (11.5, 16.3), "Meio (centro)": (14.1, 13.8), "Alto do meio": (18.0, 9.8),
        "Varanda": (20.4, 13.5), "Poço": (24.9, 12.6), "Construção": (24.9, 7.0), "Bomb A": (23.9, 9.6)}
# ---------------------------------------------------------------------------------------------------------------------

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
    with open(os.path.join(AQUI, 'brasa.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(''.join(l) for l in g) + '\n')
    rotas = {}
    for (n, x, y, lado) in ROTAS:
        rotas[n] = caminho(paiT if lado == 'T' else paiC, (x, y))
    dados = {"W": W, "H": H, "grade": [''.join(l) for l in g], "tr": tT, "ct": tC, "nomes": NOMES, "pontos": linhas,
             "rotas": rotas, "spawnT": SPAWN_T, "spawnCT": SPAWN_CT, "sites": SITES,
             "rot": {"AB": round(rotAB, 1), "BA": round(rotBA, 1), "TR": round(rotT, 1)},
             "pisa": {"A": [round(media_site(tT, cA), 1), round(media_site(tC, cA), 1)],
                      "B": [round(media_site(tT, cB), 1), round(media_site(tC, cB), 1)]}}
    with open(os.path.join(AQUI, 'brasa.json'), 'w', encoding='utf-8') as f:
        json.dump(dados, f, ensure_ascii=False)
    for l in g:
        print(''.join(l))




def pagina(saida):
    """Junta brasa.json na pagina.html (artifact da planta)."""
    dados = open(os.path.join(AQUI, 'brasa.json'), encoding='utf-8').read()
    html = open(os.path.join(AQUI, 'pagina.html'), encoding='utf-8').read().replace('/*DADOS*/null', dados)
    open(saida, 'w', encoding='utf-8').write(html)


if __name__ == '__main__':
    main()
    if len(sys.argv) > 2 and sys.argv[1] == 'pagina':
        pagina(sys.argv[2])
