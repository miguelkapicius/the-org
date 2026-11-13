# -*- coding: utf-8 -*-
"""Brasa v8 — refeita do zero (2026-11-09, Gabriel: "o mapa ficou uma bosta, joga no lixo e faz de novo").

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
W, H = 48, 62            # casas
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


# ---- BASE TR (sudoeste) ----------------------------------------------------------------------------------------------
peca("Base TR", 'T', [(2, 54), (9.6, 54), (9.6, 53.2), (14, 53.2), (14, 60.5), (2, 60.5)], 0, rot=True)

# ---- BANANA: sai reto da base, tem o nicho das toras no pé, sobe em diagonal alargando, chega num bolsão (onde o CT
#      segura atrás do carretto) e vira um corredor que entra no B pela quina de baixo. Largura 4 a 5 casas: dá pra
#      jogar granada e entrar em dupla; o TR tem cobertura nas toras e no meio muro, o CT no carretto e na caixa.
peca("Pé do banana", 'r', ret(4.5, 35, 8.8, 41.2), 0, 'Dirt', rot="Pé do banana")
peca("Rampa TR", 'r', ret(3.5, 47.4, 9.6, 54.1), 0, rot=True)
peca("Rampa TR", 'r', ret(4.5, 41.1, 8.8, 47.5), 0)
peca("Pé do banana", 'r', ret(2.6, 35.2, 4.6, 38.6), 0, 'Dirt')                             # nicho das toras
peca("Banana", 'r', [(4.5, 35.05), (8.8, 35.05), (14.2, 21.4), (9.4, 21.4)], rampa('y', 35, 0, 21.4, 12), 'Dirt', rot=True)
peca("Alto do banana", 'r', [(9.4, 21.5), (9.4, 17), (16.4, 17), (16.4, 22.6), (14.4, 22.6), (14.2, 21.5)], 12, 'Dirt', rot=True)
peca("Corredor", 'r', ret(12.2, 11.6, 15.6, 17.05), 12, rot=True)
peca("Corredor", 'r', ret(12.8, 10.9, 15, 11.7), 12)                                        # porta do B (estreita)

# ---- BOMB B (noroeste): praça fechada com a fontana no meio; caixões no alto (nordeste). Portas: corredor do banana
#      (quina sudeste), construção (leste, embaixo) e escadaria dos caixões (leste, em cima, do lado CT).
peca("Bomb B", 'b', [(3, 2.5), (13, 2.5), (13, 6), (16.6, 6), (16.6, 10.95), (11, 10.95), (11, 12.3), (7, 12.3), (7, 10.95), (3, 10.95)], 12, rot=True)
peca("Caixões", 'n', ret(13.05, 2.5, 17.2, 5.95), 24, 'Wood', rot=True)
peca("Escadaria", 's', ret(17.2, 2.5, 19.6, 5), rampa('x', 19.6, 12, 17.2, 24), rot=True)
peca("Construção", 'r', [(16.55, 7), (24.5, 7), (25.5, 6.2), (30.1, 6.2), (30.1, 9.6), (25.6, 9.6), (24.6, 10.2), (16.55, 10.2)], 12, 'Dirt', rot=True)
peca("Obra", 'r', ret(19.8, 10.15, 23.2, 12.6), 12, 'Dirt', rot=True)                                  # bolsão no fim da construção: CT segura a porta do B
peca("Rua dos caixões", 'r', ret(19.55, 2.5, 30.1, 4.6), 12, rot="Rua CT")

# ---- BASE CT (nordeste)
peca("Base CT", 'C', ret(30, 1.5, 41, 7.4), 12, rot=True)

# ---- MEIO: do meio de baixo sobe em diagonal até o alto do meio; o arco liga o alto ao lado CT (construção/base);
#      o segundo meio liga o banana ao meio (rotação curta do TR).
peca("Meio de baixo", 'r', ret(13.9, 40, 19.6, 57), 0, rot=True)
peca("Meio", 'r', [(15, 40.05), (19.6, 40.05), (22.4, 31.6), (21.7, 30.4), (25.6, 22.4), (21, 22.4), (18.9, 29.6), (18.1, 31.4)], rampa('y', 40, 0, 22.4, 6), 'Tile', rot=True)
peca("Segundo meio", 'r', [(11.1, 26.6), (20.6, 26.6), (19.6, 28.8), (10, 28.8)], 3, rot=True)
peca("Alto do meio", 'r', [(20, 16), (29.6, 16), (29.6, 22.5), (21, 22.5), (20, 21)], 6, 'Tile', rot=True)
peca("Arco", 's', [(25.6, 9.55), (29.2, 9.55), (29.2, 11.5), (28.4, 11.5), (28.4, 13.6), (29.2, 13.6), (29.2, 16.05), (25.6, 16.05), (25.6, 13.6), (26.4, 13.6), (26.4, 11.5), (25.6, 11.5)], rampa('y', 9.6, 12, 16, 6), rot=True)
peca("Curta", 'r', [(29.5, 18), (34.1, 21.6), (34.1, 25), (29.5, 21.4)], 6, rot=True)

# ---- BOMB A (leste): praça grande mas quebrada (cemitério em cima no nordeste, feno, areia). Portas do CT:
#      biblioteca (norte, a mais rápida e a mais vigiada), longo (diagonal do nordeste, mais escondida) e curta (oeste,
#      pra quem vem do B pelo arco). Do TR: curta, varanda (pula do apê) e poço.
peca("Bomb A", 'a', [(34, 15), (41, 15), (41, 18.6), (45, 18.6), (45, 28.2), (34, 28.2)], 6, 'Tile', rot=True)
peca("Cemitério", 'n', ret(41.05, 15, 45, 18.55), 18, 'Dirt', rot=True)
peca("Biblioteca", 's', ret(35, 7.35, 38.6, 15.05), rampa('y', 7.4, 12, 15, 6), 'Wood', rot=True)
peca("Longo", 'r', [(41, 2.4), (44.8, 2.4), (44.8, 4), (44.5, 13.5), (41.05, 13.5), (41.05, 7)], rampa('y', 5, 12, 13.5, 16), rot=True)
peca("Longo", 's', ret(41.05, 13.45, 44.5, 15.05), rampa('y', 13.5, 16, 15, 18))                   # degraus pro cemitério
peca("Cemitério", 's', ret(40.2, 16, 41.1, 18.5), rampa('x', 40.2, 6, 41.1, 18))                  # escadinha do A pro cemitério
peca("Poço", 'r', ret(41, 28.15, 45, 33.5), rampa('y', 28.2, 6, 29.5, 2), 'Dirt', rot=True)

# ---- APÊ (2º andar do TR): rua do apê sai da base pro leste, escada, sala, ponte em diagonal, apê CT e varanda que
#      dá pro A. O CT entra no apê pela caldeira (do poço).
peca("Rua do apê", 'r', ret(14, 56.9, 29.4, 59), 0, rot=True)
peca("Rua do apê", 'r', ret(27, 52.95, 29.4, 57), 0)
peca("Escada do apê", 's', ret(27, 44.4, 29.4, 53), rampa('y', 52.8, 0, 44.4, 30), rot=True)
peca("Sala", 'k', ret(24.2, 40, 30.4, 44.45), 30, 'Wood', rot="Sala (apê TR)")
peca("Corredor do apê", 'k', ret(28, 35, 30.4, 40.05), 30, 'Wood', rot=True)
peca("Ponte", 'k', [(28, 35.05), (30.4, 35.05), (36.4, 32.95), (34, 32.95)], 30, 'Wood', rot=True)
peca("Apê CT", 'k', ret(34, 29.5, 40.2, 33), 30, 'Wood', rot=True)
peca("Varanda", '~', ret(36, 28.15, 39.4, 29.55), 30, 'Wood', rot=True)
peca("Caldeira", 's', ret(40.15, 30, 41.05, 32.6), rampa('x', 41, 2, 40.2, 30), rot=True)

# ---- COBERTURAS (poucas, cada uma com função): (nome, polígono, altura acima do piso, tipo)
COB = [
    ("Toras", ret(2.7, 36.4, 3.7, 38.3), 10, "logs"),                    # TR agacha no nicho, protegido do alto
    ("Meio muro", [(10.9, 24.4), (12.2, 24.4), (11.6, 25.6), (10.3, 25.6)], 12, "halfwall"),   # TR agacha no meio da subida
    ("Carretto", ret(14.6, 19.6, 16.2, 22.2), 14, "cart"),              # o "carro": CT segura o banana daqui
    ("Caixa do alto", ret(9.6, 17.2, 10.8, 18.4), 16, "crate"),          # CT agachado do outro lado do bolsão
    ("Fontana", ret(7.6, 5.6, 9.6, 7.6), 10, "fountain"),
    ("1ª caixa", ret(11.2, 9.6, 12.4, 10.8), 16, "crate"),
    ("Tripla", ret(3.2, 8.6, 4.8, 10.6), 20, "crate"),
    ("Andaime", ret(21, 8.4, 22.2, 10), 16, "crate"),
    ("Carroça", ret(23.4, 16.4, 25.2, 17.4), 14, "cart"),               # NIP do alto do meio
    ("Feno", ret(37, 18.6, 38.6, 19.8), 14, "hay"),
    ("Areia", ret(35, 24, 36.4, 25), 8, "sacks"),
    ("Caixote do A", ret(42.6, 23.6, 43.8, 24.8), 16, "crate"),
]

SPAWN_T = [(4.5, 56.5), (6.5, 58.5), (8.5, 56.5), (10.5, 58.5), (12, 56.5)]
SPAWN_CT = [(32.5, 3.5), (34.5, 5.5), (36.5, 3.5), (38.5, 5.5), (35.5, 6.5)]
SITES = {"A": (36.5, 42, 19, 25), "B": (5.5, 11, 3.5, 9)}   # área de plantar

# pontos da régua (casas) e metas = Inferno medida no radar x 1,25 (TR, CT)
PONTOS = [("Pé da banana (toras)", 6.5, 38), ("Meio da banana", 10, 26.5), ("Carro (alto da banana)", 13, 19.5),
          ("Bomb B", 10.5, 8), ("Meio de baixo", 16, 43), ("Meio (centro)", 20.5, 31), ("Alto do meio", 25, 19),
          ("Varanda", 37.5, 28.8), ("Poço", 43, 31), ("Construção", 19, 8.5), ("Bomb A", 39, 22), ("Apê CT", 37, 31),
          ("Sala (apê TR)", 26, 42)]
META = {"Pé da banana (toras)": (11.9, 19.0), "Meio da banana": (14.6, 17.0), "Carro (alto da banana)": (19.1, 12.4),
        "Bomb B": (22.3, 12.8), "Meio de baixo": (11.5, 16.3), "Meio (centro)": (14.1, 13.8), "Alto do meio": (18.0, 9.8),
        "Varanda": (20.4, 13.5), "Poço": (24.9, 12.6), "Construção": (24.9, 7.0), "Bomb A": (23.9, 9.6)}

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
    gA = tempos([(39, 22)]); gB = tempos([(10.5, 8)])
    out["rotAB"] = round(pisa(gA, cB), 1); out["rotBA"] = round(pisa(gB, cA), 1)
    gL = tempos([(10, 26.5)])
    out["rotTR"] = round(em(gL, 37.5, 28.8) or -1, 1)
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
