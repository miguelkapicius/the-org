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

W, H = 64, 56
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


def nome(n, x, y):
    NOMES.append((n, x, y))


# =====================================================================================================================
# PLANTA v3 — maior (64 x 56, ~1,5x as distâncias da v2) e com espaços mais variados
#  norte = y 0.  B no noroeste, A no leste, CT no norte entre os dois, TR no sul.
#  Rotas do TR: BANANA (B), MEIO (curta/longo do A), TAPETE (apê do TR no 2o andar -> apê CT -> varanda no poço) e MF
#  (meio falso: caverna liga ao meio, janela sobe no apê CT). O CT gira por pista/construção/linha CT (B), arco e
#  biblioteca (A), caldeira (apê).
# =====================================================================================================================
# ---- bases
bloco('T', 15, 30, 47, 53); nome("Base TR", 22.5, 51)
bloco('C', 30, 40, 2, 8);   nome("Base CT", 35, 5)

# ---- rampa do TR e meio de baixo (praça de onde saem banana e meio)
bloco('r', 20, 25, 42, 46); nome("Rampa", 22.5, 44)
bloco('o', 20, 20, 44, 45)
bloco('r', 13, 31, 38, 41); nome("Meio de baixo", 22, 39.5)
bloco('o', 18, 19, 39, 39); nome("Carroça", 18.5, 41.4)

# ---- BANANA: única entrada do TR no B, comprida e em curva; o CT chega antes no alto
bloco('r', 12, 17, 33, 37)
bloco('o', 12, 13, 34, 35); nome("Toras", 10.2, 34.5)
bloco('r', 10, 15, 27, 32); nome("Banana", 12.5, 30.6)
bloco('o', 15, 15, 29, 30); nome("Meio muro", 17.2, 29.5)
bloco('r', 9, 14, 21, 26)
bloco('r', 9, 15, 18, 20)
bloco('o', 14, 15, 19, 20); nome("Carro", 17, 19.5)
bloco('o', 9, 9, 18, 18)

# ---- BOMB B (grande, cheio de canto: caixão no alto, escuro, fonte no meio, tripla, cimento)
bloco('b', 3, 20, 3, 17);   nome("Bomb B", 11, 14.6)
bloco('n', 15, 19, 3, 5);   nome("Caixão", 17, 2.2)
bloco('s', 14, 14, 3, 5)
bloco('o', 6, 6, 3, 5);     nome("Escuro", 4.4, 6.6)
bloco('o', 10, 12, 9, 10);  nome("Fonte", 11, 8.2)
bloco('o', 4, 6, 12, 13);   nome("Tripla", 5, 11)
bloco('o', 16, 17, 13, 14); nome("1ª caixa", 16.5, 12)
bloco('o', 19, 19, 10, 11); nome("Cimento", 18.6, 8.8)
# CT -> B: pista (por cima), construção (igreja em ruína, porta no leste do B) e linha CT (entra pelo canto de baixo)
bloco('r', 21, 29, 2, 4);   nome("Pista", 25, 1.4)
bloco('r', 22, 27, 6, 12);  nome("Construção", 24.5, 8.5)
bloco('o', 24, 25, 9, 9)
bloco('r', 24, 25, 5, 5)
bloco('r', 21, 21, 8, 9)
bloco('r', 28, 31, 9, 10)
bloco('r', 28, 29, 11, 15)
bloco('r', 21, 29, 14, 15); nome("Linha CT", 24.5, 13.2)

# ---- MEIO: o TR pega o de baixo, o CT segura o alto (NIP e xuxa, os dois ângulos do alto)
bloco('m', 26, 30, 20, 37); nome("Meio", 28, 28.5)
bloco('o', 26, 26, 26, 27); nome("Banco", 24.6, 26.5)
bloco('o', 30, 30, 32, 32)
bloco('r', 24, 38, 17, 19); nome("Alto do meio", 30, 18)
nome("NIP", 24.5, 16.4); nome("Xuxa", 36.5, 16.4)
bloco('r', 37, 39, 9, 16);  nome("Arco", 35.4, 12.5)                    # base CT <-> alto do meio <-> longo

# ---- A: curta (do alto do meio), longo (do arco), biblioteca (da base CT), varanda (do apê) e poço
bloco('r', 40, 45, 11, 13); nome("Longo", 42.5, 10.2)
bloco('r', 39, 45, 18, 19); nome("Curta", 42, 20.8)
bloco('o', 43, 43, 18, 18); nome("Caminhão", 43, 16.8)
bloco('r', 41, 44, 4, 5)
bloco('r', 45, 51, 2, 5);   nome("Biblioteca", 48, 3.5)
bloco('r', 49, 50, 6, 7)
bloco('a', 46, 60, 8, 24);  nome("Bomb A", 53, 17)
bloco('n', 57, 60, 8, 11);  nome("Cemitério", 58.5, 12.8)
bloco('s', 56, 56, 9, 10)
bloco('o', 51, 52, 14, 14); nome("Areia", 51.5, 13)
bloco('o', 48, 48, 21, 21); bloco('o', 56, 56, 19, 20); bloco('o', 53, 53, 10, 10)
bloco('a', 50, 57, 25, 31); nome("Poço", 53.5, 28.5)
bloco('~', 47, 49, 25, 28); nome("Varanda", 48, 29.6)

# ---- APÊ CT (2o andar em cima do MF): o CT chega pela caldeira, o TR pelo tapete ou pela janela do MF
bloco('k', 39, 46, 23, 29); nome("Apê CT", 42.5, 26)
bloco('s', 44, 45, 20, 22); nome("Caldeira", 47.4, 21)
# ---- MF (meio falso): rua estreita paralela ao meio; caverna liga ao meio, janela sobe no apê
bloco('r', 34, 35, 25, 41); nome("MF", 34.5, 36)
bloco('r', 31, 33, 33, 34); nome("Caverna", 32, 31.8)
bloco('s', 36, 38, 25, 26); nome("Janela", 37, 23.8)
# ---- TAPETE (apê do TR, 2o andar): sai da base pelo leste, sala, ponte e cai no apê CT
bloco('r', 31, 36, 49, 51)
bloco('s', 37, 38, 49, 50)
bloco('k', 39, 48, 45, 52); nome("Tapete (sala)", 43.5, 48.5)
bloco('k', 45, 46, 30, 44); nome("Ponte", 48.6, 37)
bloco('r', 36, 41, 41, 42); bloco('s', 42, 44, 41, 42); nome("Beco de trás", 39, 43.6)   # desce da ponte pro MF

SPAWN_T = [(17, 49), (20, 51), (23, 49), (26, 51), (29, 49)]
SPAWN_CT = [(31, 3), (34, 6), (37, 3), (32, 7), (39, 6)]
SITES = {"A": (46, 60, 8, 24), "B": (3, 20, 3, 17)}
PONTOS = [
    ("Pé da banana (toras)", 15, 35), ("Meio da banana", 12, 29), ("Carro (alto da banana)", 12, 19), ("Bomb B", 11, 12),
    ("Meio (centro)", 28, 29), ("Alto do meio", 30, 18), ("Caverna", 32, 33), ("MF (alto)", 34, 27),
    ("Apê CT", 42, 26), ("Varanda", 48, 26), ("Tapete (sala)", 43, 48), ("Curta (entrada do A)", 45, 18),
    ("Bomb A", 53, 16),
]
ROTAS = [("TR banana", 11, 11, 'T'), ("TR meio-curta", 48, 18, 'T'), ("TR tapete", 52, 27, 'T'), ("TR MF", 42, 26, 'T'),
         ("CT B", 12, 19, 'C'), ("CT A", 52, 12, 'C'), ("CT apê", 42, 26, 'C'), ("CT meio", 28, 21, 'C')]
ROT_TR = ((12, 19), (48, 26))            # rotação do TR: alto da banana (carro) -> varanda
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
        print(f"{n:28s} {a:6.1f} {b:6.1f}  {quem} por {abs(a - b):.1f}s")
        linhas.append({"nome": n, "x": x, "y": y, "tr": round(a, 1), "ct": round(b, 1)})
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
    print(f"rotação do TR: carro -> varanda {rotT:.1f}s")
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
