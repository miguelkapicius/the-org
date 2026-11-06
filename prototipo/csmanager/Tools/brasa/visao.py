# -*- coding: utf-8 -*-
"""Linha de visão na planta: pra cada casa, o raio mais comprido que o olho alcança (32 direções), em casas.
Bloqueiam: prédio (#) e cobertura (o). 2o andar (k) só enxerga 2o andar/escada/sacada; a sacada (~) e a plataforma (n)
enxergam o chão também (é de onde se segura de cima).

    python visao.py                 -> estatística da Brasa (lê brasa.json) e grava 'visao' no json
    python visao.py inferno <json>  -> mesma estatística no radar da Inferno (tempos.json do scratchpad), em casas da Brasa
"""
import json, math, os, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
DIRS = [(math.cos(a * math.pi / 16), math.sin(a * math.pi / 16)) for a in range(32)]


def raio_grade(g, x, y, dx, dy, W, H):
    c0 = g[y][x]; alto = c0 in 'k'; dentro = False
    s = 0.0
    while True:
        s += 0.25
        u, v = int(x + .5 + dx * s), int(y + .5 + dy * s)
        if not (0 <= u < W and 0 <= v < H): return s
        c = g[v][u]
        if c in '#o': return s
        if alto and c not in 'ks~': return s
        if not alto and c0 not in '~n' and c == 'k': return s
        if c == 'k': dentro = True
        elif dentro and c not in 's~': return s          # da sacada enxerga o apê, mas não atravessa ele


def brasa():
    d = json.load(open(os.path.join(AQUI, 'brasa.json'), encoding='utf-8'))
    g, W, H = d['grade'], d['W'], d['H']
    vis = [[None] * W for _ in range(H)]; linhas = []
    for y in range(H):
        for x in range(W):
            if g[y][x] in '#o': continue
            best = (0, 0)
            for i, (dx, dy) in enumerate(DIRS):
                r = raio_grade(g, x, y, dx, dy, W, H)
                if r > best[0]: best = (r, i)
            vis[y][x] = round(best[0], 1)
            linhas.append((best[0], x, y, best[1]))
    d['visao'] = vis
    json.dump(d, open(os.path.join(AQUI, 'brasa.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    return linhas, d


def inferno(path):
    d = json.load(open(path)); ok, w, h, K = d['ok'], d['w'], d['h'], d['K']
    cel = 80 / (4.9 * K)                     # casas da Brasa por pixel (1 casa = 80 unidades da Inferno)
    linhas = []
    for y in range(0, h, 4):
        for x in range(0, w, 4):
            if ok[y][x] != '1': continue
            best = 0
            for dx, dy in DIRS:
                s = 0
                while True:
                    s += 1
                    u, v = int(x + dx * s), int(y + dy * s)
                    if not (0 <= u < w and 0 <= v < h) or ok[v][u] != '1': break
                best = max(best, s)
            linhas.append((best / cel, x, y, 0))
    return linhas


def resumo(nome, linhas):
    v = sorted(l[0] for l in linhas); n = len(v)
    p = lambda q: v[min(n - 1, int(q * n))]
    print(f"{nome:8s} mediana {p(.5):5.1f}  p75 {p(.75):5.1f}  p90 {p(.9):5.1f}  p99 {p(.99):5.1f}  máx {v[-1]:5.1f} casas"
          f"   (>= 20 casas: {sum(1 for x in v if x >= 20) / n:.0%})")


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'inferno':
        resumo("Inferno", inferno(sys.argv[2]))
    else:
        linhas, d = brasa()
        resumo("Brasa", linhas)
        print("mais compridas:")
        vistos = []
        for r, x, y, i in sorted(linhas, reverse=True):
            if any(abs(x - a) + abs(y - b) < 6 for a, b in vistos): continue
            vistos.append((x, y)); print(f"  {r:5.1f} casas de ({x},{y}) {d['grade'][y][x]} dir {i * 11.25:.0f} graus")
            if len(vistos) >= 10: break
