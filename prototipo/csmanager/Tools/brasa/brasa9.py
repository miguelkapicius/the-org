# -*- coding: utf-8 -*-
"""Brasa v9 (sobre o motor da v8) — refeita do zero (2026-11-09, Gabriel: "o mapa ficou uma bosta, joga no lixo e faz de novo").

O que mudou no jeito de desenhar:
  * a planta é feita de POLÍGONOS em coordenadas de casa (1 casa = 25 unidades da Sim), não de casas inteiras:
    diagonal é diagonal lisa (o 3D e a navegação trabalham em 1 unidade; a escadinha vinha só da grade de 25);
  * rasterizada numa grade fina de S x S por casa (S = 5 -> 5 unidades) pra régua de tempos e pro MapData;
  * poucas peças, cada uma com função: o banana no espírito do da Inferno (reto no pé, diagonal que sobe e alarga,
    bolsão no alto onde o CT segura, corredor até o B), meio diagonal, apê do TR em 2º andar, A com 3 portas do CT.

    python Tools/brasa/brasa8.py                -> tabela de tempos + checagem
    python Tools/brasa/brasa8.py svg <saida>    -> planta em SVG (polígonos de verdade + rótulos + tempos)
"""
import heapq, json, math, os, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
W, H = 60, 48            # casas
S = 5                    # subdivisões por casa (5 unidades da Sim)
RUN = 62.5 / 25          # casas/s correndo de faca
VEL = RUN * 0.88         # de fuzil

# ---------------------------------------------------------------------------------------------------------------------
# PEÇAS: (zona, letra, polígono [(x,y)...] em casas, piso (número ou função de (x,y)), material)
#   letras: r rua · T base TR · C base CT · a/b bomb · k 2º andar · ~ sacada (só desce) · n plataforma (+12, sobe por
#   escada) · s escada/rampa
# Norte = y 0. TR no sudoeste, CT no nordeste, B no noroeste, A no leste.
# ---------------------------------------------------------------------------------------------------------------------
P = []
ROT = []        # (texto, x, y)


def ret(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def rampa(eixo, a0, z0, a1, z1):
    def f(x, y):
        a = x if eixo == 'x' else y
        t = max(0.0, min(1.0, (a - a0) / (a1 - a0)))
        return z0 + (z1 - z0) * t
    f.rampa = True
    return f


def peca(zona, letra, poli, z, mat='Stone', rot=None):
    P.append((zona, letra, poli, z, mat))
    if rot:
        ROT.append((zona if rot is True else rot, *centro(poli)))


def centro(poli):
    xs = [p[0] for p in poli]; ys = [p[1] for p in poli]
    return (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2


# ---- CONCEITO: vila italiana no morro. O TR vem da cidade baixa (mercado, z 0) e ataca subindo; o CT segura a
#      cidade alta (castelo, z 18). Três rotas separadas por quarteirões grossos, como na Vila:
#        B (oeste)  = vicolo -> salita (subida em diagonal) -> piazzetta -> Porta Vecchia (arco, o choke) -> cantina
#        meio       = a scalinata, a escadaria do centro com dois patamares, que sobe até o belvedere do lado CT
#        A (leste)  = via del forno -> rampa -> lavatoio (o choke coberto) -> piazza della chiesa; e o palazzo (2º
#                     andar do TR) com a loggia que dá pro A
#      O meio dá entrada extra pros dois sites: frantoio (patamar de cima -> B) e sacrestia (belvedere -> A); o
#      sottopassaggio liga o patamar de baixo à rampa do A (rotação do TR). A via alta, atrás dos sites, é a rotação do CT.
# ---- BASE TR: mercado (cidade baixa)
peca("Mercato", 'T', ret(25, 40, 39, 46.5), 0, rot="Mercato (base TR)")

# ---- ROTA DO B
peca("Vicolo", 'r', ret(17.5, 41, 25.05, 44.5), 0, rot=True)
# a subida do B chega na piazzetta pelo LADO (canto leste), não de frente pra Porta Vecchia: de dentro do B não se vê a
# rua, e o CT que quiser olhar a subida tem que sair na piazzetta. Nicho com barris na metade.
peca("Salita", 'r', ret(17.5, 28.95, 22, 41.05), rampa('y', 41, 0, 29, 8), rot=True)
peca("Nicchia", 'r', ret(14.8, 33, 17.55, 36.5), rampa('y', 41, 0, 29, 8), rot=True)
peca("Piazzetta", 'r', ret(6, 24.5, 22, 29.05), 8, rot=True)
peca("Porta Vecchia", 's', ret(8.5, 18, 12.1, 24.55), rampa('y', 24.5, 8, 18, 12), rot=True)
peca("Cantina", 'b', [(3, 6), (14, 6), (14, 9.5), (15.6, 9.5), (15.6, 15.5), (12.2, 15.5), (12.2, 18.05), (3, 18.05)], 12, rot="Cantina (B)")

# ---- MEIO: scalinata (lances e patamares) até o belvedere, que encosta no castelo pelo arco
peca("Scalinata", 's', ret(27.5, 33, 32.5, 40.05), rampa('y', 40, 0, 33, 6), rot=True)
peca("Primo pianerottolo", 'r', [(27, 29), (36, 29), (36, 32.2), (33, 33.05), (27, 33.05)], 6, rot=True)
peca("Scalinata", 's', ret(30.5, 23, 35, 29.05), rampa('y', 29, 6, 23, 12))
peca("Secondo pianerottolo", 'r', [(27.5, 19), (35, 19), (35, 23.05), (28.5, 23.05), (27.5, 22)], 12, rot=True)
peca("Scalinata", 's', ret(26, 14, 30.5, 19.05), rampa('y', 19, 12, 14, 18))
peca("Belvedere", 'r', [(24, 9), (36, 9), (36, 14.05), (24, 14.05)], 18, rot=True)
peca("Arco CT", 'r', ret(32.5, 7, 36, 9.05), 18, rot=True)

# ---- LIGAÇÕES DO MEIO
peca("Frantoio", 's', [(20.5, 5.95), (23, 5.95), (23, 15.6), (15.5, 15.6), (15.5, 13), (20.5, 13)], rampa('y', 6, 18, 13, 12), rot=True)
peca("Sacrestia", 'r', [(35.95, 10.5), (38.4, 10.5), (41.1, 14.4), (41.1, 17.6), (39.2, 17.6), (35.95, 13.6)], 18, rot=True)

# ---- BASE CT (castelo) e via alta (rotação do CT por trás dos sites)
peca("Castello", 'C', ret(32, 1.5, 43, 7.05), 18, rot="Castello (base CT)")
peca("Via alta", 'r', ret(19, 3, 32.05, 6), 18, rot=True)
peca("Via alta", 'r', ret(4, 1.3, 20, 3.3), 18)                                                # beco atrás da cantina
peca("Discesa", 's', ret(3.5, 3.25, 7, 6.05), rampa('y', 3.3, 18, 6, 12), rot=True)
peca("Vicolo della chiesa", 'r', ret(42.95, 1.2, 57, 3.2), 18, rot=True)                      # atrás da igreja: a porta longa do CT no A
peca("Vicolo della chiesa", 'r', ret(55, 3.15, 57, 8.05), 18)
peca("Piazza della Chiesa", 'a', ret(55, 8, 57, 12), 18, 'Tile')

# ---- ROTA DO A
peca("Via del forno", 'r', ret(36.95, 41, 54.5, 44.5), 0, rot=True)
# a rampa do A chega no lavatoio pelo lado (pátio da corte), não de frente pro site
peca("Rampa", 'r', ret(50, 27.95, 54.5, 41.05), rampa('y', 41, 0, 28, 14), rot=True)
peca("Corte", 'r', ret(42.4, 23.95, 54.5, 28.05), rampa('y', 28, 14, 24, 16), rot=True)
peca("Lavatoio", 'r', ret(42.4, 20, 46, 24.05), rampa('y', 24, 16, 20, 18), rot=True)
peca("Piazza della Chiesa", 'a', [(41, 8), (55, 8), (55, 19), (50, 19), (50, 20.05), (41, 20.05)], 18, 'Tile', rot="Piazza (A)")
peca("Sagrato", 'r', ret(45, 8, 53, 10), 24, rot=True)
peca("Scala del palazzo", 's', ret(54.45, 35, 56.05, 38), rampa('x', 54.5, 5, 56, 30))
peca("Palazzo", 'k', ret(55.5, 21, 59.5, 38), 30, 'Wood', rot=True)
peca("Loggia", '~', ret(51, 19.05, 56, 21.5), 30, 'Wood', rot=True)

# ---- COBERTURAS (cada uma com função)
COB = [
    ("Torchio", ret(7.5, 10, 9.5, 12), 12, "press"),            # prensa de vinho no meio da cantina: plant atrás
    ("Botti", ret(3.2, 12.5, 4.6, 16), 20, "barrels"),          # parede de tonéis: o CT segura a Porta Vecchia de lado
    ("Carretto", ret(11, 6.8, 12.6, 8), 14, "cart"),            # CT da via alta segura o fundo
    ("Casse", ret(6.4, 15.8, 7.6, 17), 16, "crate"),          # canto colado no arco: quem entra limpa primeiro
    ("Fontana", ret(29, 10, 31, 12), 10, "fountain"),           # topo da escadaria: o CT agacha e olha a scalinata
    ("Muretto", ret(33.4, 21.6, 34.6, 22.8), 14, "halfwall"),       # patamar de cima: quem disputa o frantoio
    ("Vaso", ret(34.4, 29.6, 35.6, 30.8), 10, "planter"),         # patamar de baixo: o TR agacha antes de subir
    ("Barili", ret(15.2, 34, 16.4, 35.2), 12, "barrels"),       # nicho da salita: o TR agacha fora da linha de quem desce
    ("Legna", ret(51.6, 25, 52.8, 26.4), 12, "logs"),           # rampa: o TR espera a granada antes do lavatoio
    ("Pozzo", ret(46.5, 13, 48.5, 15), 14, "well"),             # poço no meio da piazza: o plant fica atrás dele
    ("Ape", ret(51.5, 15, 53.6, 16.3), 14, "ape"),              # a Ape de três rodas: segura o lavatoio de longe
    ("Fioriera", ret(41.8, 15.6, 43.4, 17), 10, "planter"),     # canto da sacrestia
]

SPAWN_T = [(28, 43), (31, 44.5), (34, 43), (37, 44.5), (32.5, 42)]
SPAWN_CT = [(34, 4), (36.5, 5.5), (39, 4), (41.5, 5.5), (37.5, 3)]
SITES = {"A": (43, 53, 10.5, 18), "B": (5, 12.5, 8, 15.5)}
A_PT, B_PT = (45, 17), (10.5, 14)
ROT_TR = ((14, 27), (44, 22))
PONTOS = [("Piazzetta (TR do B)", 14, 27), ("Porta Vecchia", 10.3, 21), ("Bomb B", 10.5, 14), ("Frantoio", 21.7, 10),
          ("Primo pianerottolo", 30, 31), ("Secondo pianerottolo", 30, 21), ("Belvedere", 30, 13),
          ("Rampa (meio)", 52, 33), ("Lavatoio", 44, 22), ("Bomb A", 45, 17), ("Palazzo", 57.5, 28), ("Sacrestia", 38.5, 13.5)]
# metas: Inferno x 1,25 por função (choke do B = alto do banana; choke do A = varanda; topo do meio = alto do meio)
META = {"Bomb B": (22.3, 12.8), "Bomb A": (23.9, 9.6), "Porta Vecchia": (19.1, 12.4), "Lavatoio": (20.4, 13.5),
        "Secondo pianerottolo": (18.0, 9.8)}
DEFESA = {"B": ["Cantina", "Porta Vecchia"], "A": ["Piazza della Chiesa", "Lavatoio"]}
ROTAS_LANE = {"B": ["Salita", "Nicchia", "Piazzetta"],
              "meio": ["Scalinata", "Primo pianerottolo", "Secondo pianerottolo"],
              "A": ["Rampa", "Corte"]}

# ---------------------------------------------------------------------------------------------------------------------
# RASTER (grade fina)
# ---------------------------------------------------------------------------------------------------------------------
NIVEL = {'k': 2, '~': 2, 'n': 1}
GX, GY = W * S, H * S


def dentro(poli, x, y):
    c = False
    n = len(poli)
    for i in range(n):
        x0, y0 = poli[i]; x1, y1 = poli[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
            c = not c
    return c


def bbox(poli):
    xs = [p[0] for p in poli]; ys = [p[1] for p in poli]
    return max(0, int(min(xs) * S) - 1), min(GX - 1, int(max(xs) * S) + 1), max(0, int(min(ys) * S) - 1), min(GY - 1, int(max(ys) * S) + 1)


L = [['#'] * GX for _ in range(GY)]          # letra
Z = [[None] * GX for _ in range(GY)]         # piso
RP = [[False] * GX for _ in range(GY)]       # rampa
ZN = [[None] * GX for _ in range(GY)]        # zona
MT = [[None] * GX for _ in range(GY)]        # material
CB = [[None] * GX for _ in range(GY)]        # índice da cobertura

for zona, letra, poli, z, mat in P:
    a, b, c, d = bbox(poli)
    for j in range(c, d + 1):
        for i in range(a, b + 1):
            x, y = (i + .5) / S, (j + .5) / S
            if dentro(poli, x, y):
                L[j][i] = letra; ZN[j][i] = zona; MT[j][i] = mat
                Z[j][i] = round(z(x, y), 1) if callable(z) else z
                RP[j][i] = callable(z) and getattr(z, 'rampa', False)
for k, (nome, poli, h, tipo) in enumerate(COB):
    a, b, c, d = bbox(poli)
    for j in range(c, d + 1):
        for i in range(a, b + 1):
            if L[j][i] != '#' and dentro(poli, (i + .5) / S, (j + .5) / S):
                CB[j][i] = k


def anda(i, j):
    return L[j][i] != '#' and CB[j][i] is None


def pode(a, b):
    if a == 's' or b == 's':
        return True
    la, lb = NIVEL.get(a, 0), NIVEL.get(b, 0)
    return la == lb or (la > lb and a in '~n')


def tempos(fontes):
    INF = 1e18
    t = [[INF] * GX for _ in range(GY)]
    fila = []
    for (x, y) in fontes:
        i, j = int(x * S), int(y * S)
        t[j][i] = 0; heapq.heappush(fila, (0, i, j))
    viz = [(dx, dy, math.hypot(dx, dy)) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
    while fila:
        dd, i, j = heapq.heappop(fila)
        if dd > t[j][i]:
            continue
        a = L[j][i]
        for dx, dy, cst in viz:
            u, v = i + dx, j + dy
            if not (0 <= u < GX and 0 <= v < GY) or not anda(u, v) or not pode(a, L[v][u]):
                continue
            if dx and dy and not (anda(u, j) and anda(i, v)):
                continue
            # degrau sem rampa acima de 10 não sobe
            if Z[v][u] - Z[j][i] > 10 and not (RP[v][u] or RP[j][i]):
                continue
            c2 = dd + cst * (1.25 if 's' in (a, L[v][u]) else 1.0)
            if c2 < t[v][u]:
                t[v][u] = c2; heapq.heappush(fila, (c2, u, v))
    return [[(c / S / VEL if c < INF else None) for c in linha] for linha in t]


def em(t, x, y):
    return t[int(y * S)][int(x * S)]


def pisa(t, site):
    x0, x1, y0, y1 = site
    v = [t[j][i] for j in range(int(y0 * S), int(y1 * S)) for i in range(int(x0 * S), int(x1 * S)) if t[j][i] is not None]
    return min(v) if v else None


def checar():
    erros = []
    for nome, poli, h, tipo in COB:
        cx, cy = centro(poli)
        if L[int(cy * S)][int(cx * S)] == '#':
            erros.append(f"cobertura {nome} dentro da parede")
    # degraus altos sem rampa entre vizinhas andáveis
    vistos = set()
    for j in range(GY - 1):
        for i in range(GX - 1):
            if L[j][i] == '#':
                continue
            for u, v in ((i + 1, j), (i, j + 1)):
                if L[v][u] == '#':
                    continue
                dz = abs(Z[v][u] - Z[j][i])
                if dz > 10 and not (RP[j][i] or RP[v][u] or L[j][i] in '~n' or L[v][u] in '~n'):
                    k = (ZN[j][i], ZN[v][u])
                    if k not in vistos:
                        vistos.add(k); erros.append(f"degrau de {dz:.0f} entre {k[0]} e {k[1]} ({i / S:.1f},{j / S:.1f})")
    # tudo alcançável dos dois spawns
    tT = tempos(SPAWN_T)
    sem = {ZN[j][i] for j in range(GY) for i in range(GX) if anda(i, j) and tT[j][i] is None}
    if sem:
        erros.append("o TR não alcança: " + ", ".join(sorted(sem)))
    return erros


def medir():
    tT, tC = tempos(SPAWN_T), tempos(SPAWN_CT)
    out = {"pontos": []}
    for n, x, y in PONTOS:
        a, b = em(tT, x, y), em(tC, x, y)
        if a is None or b is None:
            print("ponto fora do chão:", n, x, y, L[int(y * S)][int(x * S)]); a = a or -1; b = b or -1
        out["pontos"].append({"nome": n, "x": x, "y": y, "tr": round(a, 1), "ct": round(b, 1), "meta": META.get(n)})
    out["pisa"] = {s: [round(pisa(tT, SITES[s]), 1), round(pisa(tC, SITES[s]), 1)] for s in SITES}
    cA, cB = SITES["A"], SITES["B"]
    gA = tempos([A_PT]); gB = tempos([B_PT])
    out["rotAB"] = round(pisa(gA, cB), 1); out["rotBA"] = round(pisa(gB, cA), 1)
    gL = tempos([ROT_TR[0]])
    out["rotTR"] = round(em(gL, *ROT_TR[1]) or -1, 1)
    out["tT"], out["tC"] = tT, tC
    return out


# ---------------------------------------------------------------------------------------------------------------------
# SVG
# ---------------------------------------------------------------------------------------------------------------------
COR = {'T': '--m-tr', 'C': '--m-ct', 'a': '--m-site', 'b': '--m-site', 'r': '--m-rua', 'k': '--m-andar', '~': '--m-andar',
       'n': '--m-alto', 's': '--m-escada'}


def svg(m=None, px=14):
    o = [f'<svg viewBox="0 0 {W * px} {H * px}" class="planta" role="img" aria-label="Planta da Brasa v8">']
    o.append(f'<rect width="{W * px}" height="{H * px}" style="fill:var(--m-predio)"/>')
    for zona, letra, poli, z, mat in P:
        pts = ' '.join(f'{x * px:.1f},{y * px:.1f}' for x, y in poli)
        o.append(f'<polygon points="{pts}" style="fill:var({COR.get(letra, "--m-rua")});stroke:var({COR.get(letra, "--m-rua")});stroke-width:1.2"/>')
    for s, (x0, x1, y0, y1) in SITES.items():
        o.append(f'<rect x="{x0 * px}" y="{y0 * px}" width="{(x1 - x0) * px}" height="{(y1 - y0) * px}" class="plant"/>')
        o.append(f'<text x="{(x0 + x1) / 2 * px}" y="{(y0 + y1) / 2 * px + 9}" class="letra">{s}</text>')
    for nome, poli, h, tipo in COB:
        pts = ' '.join(f'{x * px:.1f},{y * px:.1f}' for x, y in poli)
        o.append(f'<polygon points="{pts}" class="cob"><title>{nome} ({h})</title></polygon>')
    for (x, y) in SPAWN_T:
        o.append(f'<circle cx="{x * px}" cy="{y * px}" r="4" class="spT"/>')
    for (x, y) in SPAWN_CT:
        o.append(f'<circle cx="{x * px}" cy="{y * px}" r="4" class="spC"/>')
    for n, x, y in ROT:
        o.append(f'<text x="{x * px:.1f}" y="{y * px + 4:.1f}" class="rot">{n}</text>')
    for nome, poli, h, tipo in COB:
        x, y = centro(poli)
        o.append(f'<text x="{x * px:.1f}" y="{y * px - 9:.1f}" class="rotc">{nome}</text>')
    o.append('</svg>')
    return ''.join(o)


def main():
    erros = checar()
    for e in erros:
        print("CHECAR:", e)
    m = medir()
    print(f"{'ponto':26s} {'TR':>6s} {'CT':>6s}  primeiro        meta")
    for p in m["pontos"]:
        mt = f"{p['meta'][0]:.1f}/{p['meta'][1]:.1f}" if p["meta"] else ""
        quem = "CT" if p["ct"] < p["tr"] else "TR"
        print(f"{p['nome']:26s} {p['tr']:6.1f} {p['ct']:6.1f}  {quem} por {abs(p['tr'] - p['ct']):4.1f}s   {mt}")
    print("pisa", m["pisa"], " rotação CT A->B", m["rotAB"], "B->A", m["rotBA"], " TR banana->varanda", m["rotTR"])
    return m


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'svg':
        open(sys.argv[2], 'w', encoding='utf-8').write(svg())
    else:
        main()


# ---------------------------------------------------------------------------------------------------------------------
# EXPORTAÇÃO (grade fina) -> Tools/brasa/v9/brasa_mapa.json
#   Mesmo contrato do brasa_mapa.json antigo, mas por SUBCASA de S x S (QF = 25 / S unidades da Sim):
#   subcasa (i, j) cobre [i*QF, i*QF+QF) x [j*QF, j*QF+QF). Letra '#' = prédio (solid = GZ + topo), 'o' = piso com
#   cobertura em cima (como antes), o resto anda (floor = GZ + piso; stair = rampa '1'; fmat = material).
#   Coberturas, sites, spawns e nomes continuam em CASAS (fracionário), como o Places usa.
# ---------------------------------------------------------------------------------------------------------------------
def exportar(saida=None):
    import numpy as np
    saida = saida or os.path.join(AQUI, 'v9', 'brasa_mapa.json')
    os.makedirs(os.path.dirname(saida), exist_ok=True)
    QF = 25 // S
    Zn = np.array([[(-1e9 if v is None else v) for v in l] for l in Z], float)
    # topo dos prédios: piso mais alto num raio de 2 casas + 36/40/44 por lote de 3x3 casas (igual à v7)
    R = 2 * S
    pad = np.pad(Zn, R, constant_values=-1e9)
    mx = np.full_like(Zn, -1e9)
    for dy in range(-R, R + 1, 2):
        for dx in range(-R, R + 1, 2):
            mx = np.maximum(mx, pad[R + dy:R + dy + GY, R + dx:R + dx + GX])
    mx[mx < -1e8] = 0
    topo = [[None] * GX for _ in range(GY)]
    for j in range(GY):
        for i in range(GX):
            if L[j][i] == '#':
                cx, cy = i // S, j // S
                lote = (cx // 3 * 73856093 ^ cy // 3 * 19349663) & 0x7fffffff
                topo[j][i] = int(round(mx[j][i] + (36, 40, 44, 40)[lote % 4]))
    zonas = sorted({z for l in ZN for z in l if z})
    MATC = {'Stone': 'S', 'Tile': 'T', 'Dirt': 'D', 'Wood': 'W', 'Cave': 'C', None: '.'}
    cel = [''.join('o' if CB[j][i] is not None else L[j][i] for i in range(GX)) for j in range(GY)]

    def piso_cob(poli):
        cx, cy = centro(poli)
        best = None
        for r in range(1, 4 * S):
            for dj in range(-r, r + 1):
                for di in range(-r, r + 1):
                    i, j = int(cx * S) + di, int(cy * S) + dj
                    if 0 <= i < GX and 0 <= j < GY and L[j][i] != '#' and CB[j][i] is None and Z[j][i] is not None:
                        return Z[j][i]
        return 0
    d = {
        "nome": "Brasa", "versao": "v9.2", "Q": 25, "S": S, "QF": QF, "W": W, "H": H, "GW": GX, "GH": GY,
        "NX": W * 25, "NY": H * 25, "GZ": 8, "UP": 30,
        "legenda": {"#": "prédio", "T": "base TR", "C": "base CT", "a": "bomb A", "b": "bomb B", "r": "rua",
                    "k": "2o andar", "n": "plataforma (sobe por escada, desce pulando)", "s": "escada/rampa",
                    "~": "sacada (2o andar, só desce)", "o": "cobertura (piso embaixo)"},
        "celulas": cel,
        "piso": [[None if v is None else int(round(v)) for v in l] for l in Z],
        "rampa": [''.join('1' if v else '0' for v in l) for l in RP],
        "material": [''.join(MATC[m] for m in l) for l in MT],
        "topo": topo,
        "zonas": zonas, "zona": [[zonas.index(z) if z else -1 for z in l] for l in ZN],
        "coberturas": [{"nome": n, "tipo": t, "h": h, "poli": [list(p) for p in poli],
                        "x0": min(p[0] for p in poli), "x1": max(p[0] for p in poli),
                        "y0": min(p[1] for p in poli), "y1": max(p[1] for p in poli), "piso": piso_cob(poli)}
                       for n, poli, h, t in COB],
        "sites": {k: {"x0": v[0], "x1": v[1], "y0": v[2], "y1": v[3]} for k, v in SITES.items()},
        "spawnT": SPAWN_T, "spawnCT": SPAWN_CT, "nomes": ROT,
        "pecas": [{"zona": z, "letra": l, "poli": [list(p) for p in poli], "mat": m} for z, l, poli, zf, m in P],
    }
    with open(saida, 'w', encoding='utf-8') as f:
        json.dump(d, f, ensure_ascii=False, separators=(',', ':'))
    return saida


if __name__ == '__main__' and len(sys.argv) > 1 and sys.argv[1] == 'exportar':
    erros = checar()
    for e in erros:
        print("CHECAR:", e)
    print("gravado", exportar())
