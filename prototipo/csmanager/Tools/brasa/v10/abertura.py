# -*- coding: utf-8 -*-
"""Abertura dos bombs: de cada ponto do bomb, que fração do bomb dá pra ver em pé (parede e cobertura >= 22 tapam).
   python -I abertura.py [modulo]   (padrão brasa10; brasa10_antes_posicoes pra comparar)"""
import sys, importlib, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
M = importlib.import_module(sys.argv[1] if len(sys.argv) > 1 else 'brasa10')
S = M.S


def tapa(x, y):
    i, j = int(x * S), int(y * S)
    if M.L[j][i] == '#': return True
    k = M.CB[j][i]
    return k is not None and M.COB[k][2] >= 22


def ve(a, b):
    d = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(1, int(d * S * 1.5))
    return not any(tapa(a[0] + (b[0] - a[0]) * t / n, a[1] + (b[1] - a[1]) * t / n) for t in range(1, n))


for nome, (x0, x1, y0, y1) in M.SITES.items():
    pts = [(x0 + .25 + .5 * i, y0 + .25 + .5 * j) for i in range(int((x1 - x0) * 2)) for j in range(int((y1 - y0) * 2))]
    pts = [p for p in pts if not tapa(*p)]
    fr = [sum(ve(a, b) for b in pts) / len(pts) for a in pts]
    cob = [c for c in M.COB if x0 - 1.5 <= (c[1][0][0] + c[1][2][0]) / 2 <= x1 + 1.5 and y0 - 1.5 <= (c[1][0][1] + c[1][2][1]) / 2 <= y1 + 1.5]
    print(f"{nome}: vê em média {100 * sum(fr) / len(fr):.0f}% do bomb em pé; ponto mais escondido {100 * min(fr):.0f}%; "
          f"coberturas {len(cob)} (altas {sum(1 for c in cob if c[2] >= 22)})")
