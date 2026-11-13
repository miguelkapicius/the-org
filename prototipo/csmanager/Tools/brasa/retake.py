# -*- coding: utf-8 -*-
"""Régua de retake e equilíbrio da Brasa (ou de uma proposta): por bomb, as entradas, quanto o CT leva por cada uma
(só aquela aberta), quanto chão aberto ele cruza dentro do site e quantos cantos do pós-plant veem a porta.

    python Tools/brasa/retake.py                  -> mede brasa.py
    python Tools/brasa/retake.py brasa_proposta   -> mede a proposta
    (gravar json:  ... brasa_proposta --json saida.json)

Uma porta é "a melhor sempre" quando é a mais rápida E a menos exposta. Um bom retake tem 3 portas com troca:
a mais rápida é a mais vigiada, a mais escondida é a mais longa.
"""
import importlib, json, math, os, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
OLHO = {'pe': 22, 'ag': 14}
SITE_ZONAS = {"A": {"Bomb A", "Cemitério", "Poço"}, "B": {"Bomb B", "Caixão"}}


def carregar(nome):
    m = importlib.import_module(nome)
    return m


class Mapa:
    def __init__(self, m):
        self.m = m; self.W, self.H, self.g, self.Z = m.W, m.H, m.g, m.Z
        self.cobt = [[None] * self.W for _ in range(self.H)]
        for a, b, c, d, h, *_ in m.COB:
            p = m.piso_cob(a, c)
            for y in range(c, d + 1):
                for x in range(a, b + 1):
                    self.cobt[y][x] = p + h

    def ve(self, a, b, sa='pe', sb='pe'):
        g, Z = self.g, self.Z
        (xa, ya), (xb, yb) = a, b
        if Z[ya][xa] is None or Z[yb][xb] is None:
            return False
        za = Z[ya][xa] + OLHO[sa]; zb = Z[yb][xb] + OLHO[sb]
        ka = g[ya][xa] == 'k'; kb = g[yb][xb] == 'k'
        if ka != kb and (g[yb][xb] if ka else g[ya][xa]) not in '~s':
            return False
        L = math.hypot(xb - xa, yb - ya); n = max(2, int(L * 5))
        for i in range(1, n):
            t = i / n
            x, y = int(xa + .5 + (xb - xa) * t), int(ya + .5 + (yb - ya) * t)
            if (x, y) in ((xa, ya), (xb, yb)):
                continue
            c = g[y][x]; z = za + (zb - za) * t
            if c == '#':
                return False
            if c == 'o':
                if self.cobt[y][x] is not None and self.cobt[y][x] > z:
                    return False
                continue
            if Z[y][x] is not None and Z[y][x] > z - 1:
                return False
            if (ka or kb) and c not in 'k~s':
                return False
            if not (ka or kb) and c == 'k':
                return False
        return True


def regiao(m, s):
    return {(x, y) for y in range(m.H) for x in range(m.W) if m.ZONA[y][x] in SITE_ZONAS[s] and m.g[y][x] not in '#o'}


def entradas(m, s):
    """Casas fora do site, andáveis, vizinhas (4) de uma casa do site e que dão pra entrar nela; agrupadas por vizinhança."""
    reg = regiao(m, s); bordas = set()
    for (x, y) in reg:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            u, v = x + dx, y + dy
            if (u, v) in reg or not (0 <= u < m.W and 0 <= v < m.H) or not m.anda(m.g[v][u]):
                continue
            if m.pode(m.g[v][u], m.g[y][x]):
                bordas.add((u, v))
    grupos = []
    while bordas:
        p = bordas.pop(); gr = {p}; fila = [p]
        while fila:
            x, y = fila.pop()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = (x + dx, y + dy)
                    if q in bordas:
                        bordas.remove(q); gr.add(q); fila.append(q)
        zonas = {}
        for (x, y) in gr:
            zonas[m.ZONA[y][x]] = zonas.get(m.ZONA[y][x], 0) + 1
        grupos.append((max(zonas, key=zonas.get), sorted(gr)))
    return reg, grupos


def tempos_bloq(m, fontes, bloq):
    """Dijkstra do brasa.tempos com casas bloqueadas (as outras entradas)."""
    salvo = {}
    for (x, y) in bloq:
        salvo[(x, y)] = m.g[y][x]; m.g[y][x] = '#'
    try:
        t, pai = m.tempos(fontes)
    finally:
        for (x, y), c in salvo.items():
            m.g[y][x] = c
    return t, pai


def medir(nome):
    m = carregar(nome); mp = Mapa(m)
    out = {"nome": nome, "sites": {}}
    tT, _ = m.tempos(m.SPAWN_T); tC, _ = m.tempos(m.SPAWN_CT)
    for s in ("A", "B"):
        reg, grupos = entradas(m, s)
        x0, x1, y0, y1 = m.SITES[s]
        plant = [(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if m.anda(m.g[y][x])]
        alvo = min(plant, key=lambda p: math.hypot(p[0] - (x0 + x1) / 2, p[1] - (y0 + y1) / 2))
        # cantos do pós-plant: casas do site (agachado) que veem o plant (de pé) — de onde o TR segura a bomba
        def abrigo(p):
            x, y = p
            if m.g[y][x] == 'n':
                return True
            return any(0 <= x + dx < m.W and 0 <= y + dy < m.H and m.g[y + dy][x + dx] == 'o' for dx in (-1, 0, 1) for dy in (-1, 0, 1))
        cantos = [p for p in reg if m.g[p[1]][p[0]] not in 'o' and abrigo(p) and any(mp.ve(p, q, 'ag', 'pe') for q in plant[::3])]
        linhas = []
        retakers = {"spawn CT": m.SPAWN_CT}
        outro = "B" if s == "A" else "A"
        ox0, ox1, oy0, oy1 = m.SITES[outro]
        retakers["do outro bomb"] = [min(((x, y) for y in range(oy0, oy1 + 1) for x in range(ox0, ox1 + 1) if m.anda(m.g[y][x])),
                                         key=lambda p: math.hypot(p[0] - (ox0 + ox1) / 2, p[1] - (oy0 + oy1) / 2))]
        for zn, cel in grupos:
            outras = [c for z2, c2 in grupos if c2 is not cel for c in c2]
            if all(tT[y][x] is not None and tC[y][x] is not None and tT[y][x] < tC[y][x] - 6 for x, y in cel):
                lado = "TR"
            else:
                lado = "CT"
            ts = {}
            for rn, fontes in retakers.items():
                t, pai = tempos_bloq(m, fontes, outras)
                ts[rn] = round(t[alvo[1]][alvo[0]], 1) if t[alvo[1]][alvo[0]] is not None else None
            # chão aberto: do pé da porta até o plant, quantos segundos visível de algum canto
            t, pai = tempos_bloq(m, m.SPAWN_CT, outras)
            cam = m.caminho(pai, alvo)
            dentro = [p for p in cam if p in reg]
            vistos = [p for p in dentro if any(mp.ve(c, p, 'ag', 'pe') for c in cantos[::2])]
            porta = min(cel, key=lambda p: t[p[1]][p[0]] if t[p[1]][p[0]] is not None else 1e9)
            vigia = [c for c in cantos if any(mp.ve(c, p, 'ag', 'pe') for p in cel)]
            linhas.append({"entrada": zn, "lado": lado, "casas": cel, "t": ts, "aberto_s": round(len(vistos) / m.VEL, 1),
                           "dentro_s": round(len(dentro) / m.VEL, 1), "vigia": round(len(vigia) / max(1, len(cantos)), 2),
                           "porta": porta})
        # quantos cantos do pós-plant veem 2+ portas de CT (um TR segura duas portas sozinho)
        cts = [l for l in linhas if l["lado"] == "CT"]
        duas = 0
        for c in cantos:
            k = sum(1 for l in cts if any(mp.ve(c, p, 'ag', 'pe') for p in l["casas"]))
            duas += k >= 2
        out["sites"][s] = {"entradas": linhas, "cantos": len(cantos), "canto_2portas": round(duas / max(1, len(cantos)), 2)}
    # régua geral
    pts = []
    for (n, x, y) in m.PONTOS:
        mt = m.META.get(n)
        pts.append({"nome": n, "tr": round(tT[y][x], 1), "ct": round(tC[y][x], 1), "meta": mt})
    cA, cB = m.SITES["A"], m.SITES["B"]
    meio = lambda c: ((c[0] + c[1]) // 2, (c[2] + c[3]) // 2)
    gA, _ = m.tempos([meio(cA)]); gB, _ = m.tempos([meio(cB)])
    rot = lambda t_, c: min(t_[y][x] for y in range(c[2], c[3] + 1) for x in range(c[0], c[1] + 1) if t_[y][x] is not None)
    (lx, ly), (mx, my) = m.ROT_TR
    gL, _ = m.tempos([(lx, ly)])
    out["regua"] = {"pontos": pts, "rotAB": round(rot(gA, cB), 1), "rotBA": round(rot(gB, cA), 1), "rotTR": round(gL[my][mx], 1),
                    "pisa": {"A": [round(m.media_site(tT, cA), 1), round(m.media_site(tC, cA), 1)],
                             "B": [round(m.media_site(tT, cB), 1), round(m.media_site(tC, cB), 1)]}}
    return out


def imprimir(o):
    print(f"== {o['nome']}")
    for s, d in o["sites"].items():
        print(f" Bomb {s}: {d['cantos']} cantos de pós-plant; {d['canto_2portas']:.0%} deles seguram 2+ portas do CT")
        for l in sorted(d["entradas"], key=lambda l: l["t"]["spawn CT"] or 99):
            t = l["t"]
            print(f"   {l['entrada']:22s} {l['lado']}  CT spawn {t['spawn CT']}s  outro bomb {t['do outro bomb']}s  "
                  f"dentro {l['dentro_s']}s (visto {l['aberto_s']}s)  vigiada por {l['vigia']:.0%} dos cantos  porta {l['porta']}")
    r = o["regua"]
    print(f" pisa A TR/CT {r['pisa']['A']}  B {r['pisa']['B']}   rotação CT A->B {r['rotAB']} B->A {r['rotBA']}  TR {r['rotTR']}")
    for p in r["pontos"]:
        mt = f"  meta {p['meta'][0]}/{p['meta'][1]}" if p["meta"] else ""
        print(f"   {p['nome']:26s} TR {p['tr']:5.1f} CT {p['ct']:5.1f}{mt}")


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    nome = args[0] if args else 'brasa'
    o = medir(nome)
    imprimir(o)
    if '--json' in sys.argv:
        json.dump(o, open(sys.argv[sys.argv.index('--json') + 1], 'w', encoding='utf-8'), ensure_ascii=False)
