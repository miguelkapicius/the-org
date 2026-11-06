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

W, H = 44, 38
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
# PLANTA v2 — peças da Inferno adaptadas (nomes como o pessoal chama no Brasil)
#  norte = y 0.  B no noroeste, A no leste, CT no norte entre os dois, TR no sudoeste.
#  Rotas do TR: BANANA (B), MEIO (curta/longo do A), TAPETE (apê no 2o andar -> varanda no poço) e MF (meio falso,
#  liga o meio ao apê CT pela caverna). O CT gira por pista/construção (B), arco e biblioteca (A).
# =====================================================================================================================
# ---- bases
bloco('T', 9, 18, 32, 36); nome("Base TR", 13.5, 34)
bloco('C', 19, 25, 2, 6);  nome("Base CT", 22, 4)

# ---- rampa do TR e meio de baixo (de onde saem banana e meio)
bloco('r', 11, 14, 28, 31); nome("Rampa", 12.5, 29.5)
bloco('r', 8, 16, 25, 27); nome("Meio de baixo", 12.5, 26)

# ---- BANANA: única entrada do TR no B, comprida e em curva; o CT chega antes no alto
bloco('r', 5, 8, 22, 24)                                                   # pé (toras)
bloco('o', 5, 5, 23, 24);  nome("Toras", 3.4, 23.5)
bloco('r', 4, 7, 17, 21);  nome("Banana", 5.5, 19.5)
bloco('o', 4, 4, 18, 18);  nome("Meio muro", 2.6, 18)
bloco('r', 5, 8, 12, 16)                                                   # alto da banana (curva pro leste)
bloco('o', 8, 8, 13, 14);  nome("Carro", 9.8, 13.5)
bloco('r', 5, 8, 11, 11)                                                   # boca da banana (4) -> B

# ---- BOMB B (grande, cheio de canto: caixão no alto, escuro, fonte no meio, tripla)
bloco('b', 2, 12, 2, 10);  nome("Bomb B", 7, 8.6)
bloco('n', 9, 11, 2, 3);   nome("Caixão", 10, 1.4)
bloco('s', 8, 8, 2, 3)
bloco('o', 4, 4, 2, 3);    nome("Escuro", 2.5, 4.2)
bloco('o', 6, 7, 5, 6);    nome("Fonte", 6.5, 4.4)
bloco('o', 3, 4, 7, 8);    nome("Tripla", 3.5, 6.4)
bloco('o', 11, 11, 8, 8);  nome("Cimento", 11, 6.8)
# CT -> B: pista (por cima), construção (porta no leste do B) e linha CT (entra pelo canto de baixo)
bloco('r', 14, 18, 1, 2);  nome("Pista", 16, 0.9)
bloco('r', 14, 17, 3, 7);  nome("Construção", 15.5, 5)
bloco('r', 13, 13, 4, 5)
bloco('r', 17, 18, 5, 10)
bloco('r', 13, 18, 9, 10); nome("Linha CT", 15.5, 9.6)

# ---- MEIO: o TR pega o de baixo, o CT segura o alto (NIP e xuxa, os dois ângulos do alto)
bloco('m', 14, 17, 14, 24); nome("Meio", 15.5, 19)
bloco('o', 14, 14, 18, 18); nome("Banco", 12.6, 18)
bloco('r', 14, 23, 12, 13); nome("Alto do meio", 17, 12.5)
nome("NIP", 14.6, 11.4); nome("Xuxa", 21.2, 11.4)
bloco('r', 22, 23, 7, 11);  nome("Arco", 21, 8.6)                         # base CT <-> alto do meio <-> longo

# ---- A: curta (do alto do meio), longo (do arco), biblioteca (da base CT), varanda (do apê) e poço
bloco('r', 24, 30, 7, 8);   nome("Longo", 27, 7.5)
bloco('r', 24, 30, 12, 13); nome("Curta", 26, 12.5)
bloco('o', 28, 28, 12, 12); nome("Caminhão", 28.6, 11.2)
bloco('r', 26, 30, 3, 4)
bloco('r', 31, 35, 2, 4);   nome("Biblioteca", 33, 3)
bloco('r', 33, 34, 5, 5)
bloco('a', 31, 41, 6, 16);  nome("Bomb A", 36, 11)
bloco('n', 39, 41, 6, 8);   nome("Cemitério", 40, 9.2)
bloco('s', 38, 38, 7, 8)
bloco('o', 35, 36, 10, 10); bloco('o', 33, 33, 14, 14); bloco('o', 39, 39, 14, 15); nome("Areia", 35.5, 9)
bloco('a', 33, 38, 17, 21); nome("Poço", 35.5, 19.5)
bloco('~', 31, 32, 17, 19); nome("Varanda", 31.5, 20.6)

# ---- APÊ CT (2o andar em cima do MF): o CT chega pela caldeira, o TR pelo tapete ou pela escada do MF
bloco('k', 25, 30, 17, 21); nome("Apê CT", 27.5, 19)
bloco('s', 27, 28, 14, 16); nome("Caldeira", 29.8, 15)
# ---- MF (meio falso): rua estreita paralela ao meio; caverna liga ao meio, escada sobe no apê
bloco('r', 21, 22, 18, 28); nome("MF", 21.5, 25)
bloco('r', 18, 20, 22, 23); nome("Caverna", 19, 21.4)
bloco('s', 23, 24, 18, 19); nome("Janela", 23.5, 17)
# ---- TAPETE (apê do TR, 2o andar): sai da base pelo leste, sala, ponte e cai no apê CT
bloco('r', 19, 22, 33, 35)
bloco('s', 23, 24, 33, 34)
bloco('k', 25, 30, 31, 35); nome("Tapete (sala)", 27.5, 33)
bloco('k', 29, 30, 22, 30); nome("Ponte", 31.8, 26)
bloco('r', 23, 26, 28, 28); bloco('s', 27, 28, 28, 28); nome("Beco de trás", 25, 29.6)   # desce da ponte pro MF

SPAWN_T = [(10, 33), (12, 35), (14, 33), (16, 35), (18, 33)]
SPAWN_CT = [(20, 3), (22, 5), (24, 3), (21, 6), (25, 5)]
SITES = {"A": (31, 41, 6, 16), "B": (2, 12, 2, 10)}
PONTOS = [
    ("Pé da banana (toras)", 6, 23), ("Meio da banana", 5, 19), ("Carro (alto da banana)", 7, 13), ("Bomb B", 7, 7),
    ("Meio (centro)", 15, 19), ("Alto do meio", 18, 12), ("Caverna", 19, 22), ("MF (alto)", 21, 19),
    ("Apê CT", 27, 19), ("Varanda", 31, 18), ("Tapete (sala)", 27, 33), ("Curta (entrada do A)", 30, 12),
    ("Bomb A", 36, 11),
]
ROTAS = [("TR banana", 7, 6, 'T'), ("TR meio-curta", 32, 12, 'T'), ("TR tapete", 33, 18, 'T'), ("TR MF", 27, 19, 'T'),
         ("CT B", 7, 12, 'C'), ("CT A", 35, 8, 'C'), ("CT apê", 27, 19, 'C'), ("CT meio", 16, 14, 'C')]
ROT_TR = ((7, 13), (31, 18))            # rotação do TR: alto da banana (carro) -> varanda
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
