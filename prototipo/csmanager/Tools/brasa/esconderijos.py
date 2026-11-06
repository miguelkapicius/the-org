# -*- coding: utf-8 -*-
"""Esconderijos do B: pra cada casa do site, de quantas portas (de pé, logo depois de entrar) se vê um jogador agachado
ali. Casa que nenhuma porta vê = esconderijo roubado (pro CT que segura e pro TR do pós-plant)."""
import json, os, sys
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
from angulos import ve, CEL, PISO
PORTAS = {"banana": (15, 10), "pista": (10, 6), "construção": (15, 6)}
def mapa(x0, x1, y0, y1, portas=PORTAS, pose='ag'):
    out = {}
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if CEL[y][x] in '#o' or PISO[y][x] is None: continue
            out[(x, y)] = [n for n, p in portas.items() if ve(p, (x, y), 'pe', pose)]
    return out
if __name__ == '__main__':
    m = mapa(3, 15, 6, 14)
    print('   ' + ''.join(str(x % 10) for x in range(3, 16)))
    for y in range(6, 15):
        lin = ''
        for x in range(3, 16):
            if (x, y) in m: lin += str(len(m[(x, y)]))
            else: lin += '#' if CEL[y][x] == '#' else 'o'
        print(f'{y:2d} ' + lin)
    zero = [k for k, v in m.items() if not v]
    print('escondidas das 3 portas:', zero)
