# -*- coding: utf-8 -*-
"""Ângulos de verdade na Brasa: quem vê quem, com altura de chão, altura de cobertura e agachado/em pé.

    python angulos.py            -> tabela de posições (o que cada uma enxerga) e checagem das regras do banana/B

Olho em pé = piso + 22, agachado = piso + 14 (boneco de 25). Um alvo conta como visto se a cabeça dele aparece.
Bloqueia: prédio, cobertura mais alta que a linha, chão mais alto que a linha (rampa/degrau), e a parede entre o 2º andar
(k) e a rua (só a sacada ~ e a escada s ligam os dois).
"""
import json, math, os, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
M = json.load(open(os.path.join(AQUI, 'brasa_mapa.json'), encoding='utf-8'))
W, H, CEL, PISO = M['W'], M['H'], M['celulas'], M['piso']
COBT = [[None] * W for _ in range(H)]
for c in M['coberturas']:
    for y in range(c['y0'], c['y1'] + 1):
        for x in range(c['x0'], c['x1'] + 1):
            COBT[y][x] = c['piso'] + c['h']
OLHO = {'pe': 22, 'ag': 14}


def ve(a, b, sa='pe', sb='pe'):
    (xa, ya), (xb, yb) = a, b
    za = PISO[ya][xa] + OLHO[sa]; zb = PISO[yb][xb] + OLHO[sb]
    ka = CEL[ya][xa] == 'k'; kb = CEL[yb][xb] == 'k'
    if ka != kb and (CEL[yb][xb] if ka else CEL[ya][xa]) not in '~s':
        return False                                   # de dentro de casa só se vê a sacada/escada
    L = math.hypot(xb - xa, yb - ya); n = max(2, int(L * 5))
    for i in range(1, n):
        t = i / n
        x, y = int(xa + .5 + (xb - xa) * t), int(ya + .5 + (yb - ya) * t)
        if (x, y) in ((xa, ya), (xb, yb)):
            continue
        c = CEL[y][x]; z = za + (zb - za) * t
        if c == '#':
            return False
        if c == 'o':
            if COBT[y][x] is not None and COBT[y][x] > z:
                return False
            continue
        if PISO[y][x] is not None and PISO[y][x] > z - 1:
            return False
        if (ka or kb) and c not in 'k~s':
            return False                               # a linha sai de dentro de casa
        if not (ka or kb) and c == 'k':
            return False                               # a linha atravessa uma casa
    return True


def area_vista(p, s='pe', alvo='pe'):
    """Casas andáveis cuja cabeça (em pé) se vê de p."""
    out = []
    for y in range(H):
        for x in range(W):
            if CEL[y][x] in '#o' or PISO[y][x] is None:
                continue
            if ve(p, (x, y), s, alvo):
                out.append((x, y))
    return out


if __name__ == '__main__':
    sys.path.insert(0, AQUI)
    import brasa
    POS = brasa.POSICOES
    nomes = list(POS)
    print("quem vê quem (linha vê coluna; em pé x em pé):")
    for n in nomes:
        (x, y, lado, st, papel) = POS[n]
        vis = [m for m in nomes if m != n and ve((x, y), POS[m][:2], st, POS[m][3])]
        print(f"  {lado} {n:24s} [{st}] vê: {', '.join(vis) if vis else '-'}")
    falhas = brasa.regras_angulos(ve)
    print("REGRAS:", "todas ok" if not falhas else "")
    for f in falhas:
        print("  FALHOU:", f)
