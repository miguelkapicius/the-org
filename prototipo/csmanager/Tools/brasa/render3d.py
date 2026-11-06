# -*- coding: utf-8 -*-
"""Imagem 3D (isométrica, em blocos) da Brasa a partir do brasa_mapa.json, sem Unity: chão na altura certa, rampas,
2º andar, coberturas e prédios cortados baixos (corte de maquete) pra dar pra ler o mapa.

    python render3d.py <saida.png> [se|no]     se = câmera do sudeste (padrão), no = do noroeste
"""
import json, os, struct, sys, zlib
import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
M = json.load(open(os.path.join(AQUI, 'brasa_mapa.json'), encoding='utf-8'))
W, H, CEL, PISO = M['W'], M['H'], M['celulas'], M['piso']
A, B, C = 15.0, 7.5, 0.62            # px por casa (x, y) e por unidade de altura

COR = {'T': (231, 185, 122), 'C': (150, 188, 228), 'a': (240, 217, 166), 'b': (240, 217, 166), 'r': (214, 194, 154),
       'm': (222, 176, 120), 'k': (160, 112, 72), '~': (196, 87, 58), 'n': (185, 160, 125), 's': (150, 140, 135)}
TELHA, REBOCO = (190, 86, 46), (236, 211, 160)
MAT = {'Dirt': (196, 160, 110), 'Cave': (130, 115, 100), 'Wood': (170, 120, 78)}


def png(path, img):
    h, w, _ = img.shape
    raw = b''.join(b'\0' + img[y].tobytes() for y in range(h))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
                           + ch(b'IDAT', zlib.compress(raw, 6)) + ch(b'IEND', b''))


def main(saida, lado='se'):
    gira = lado == 'no'
    caixas = []                       # (x0, y0, x1, y1, z0, z1, cor do topo, cor do lado)
    for y in range(H):
        for x in range(W):
            c = CEL[y][x]
            if c == '#':
                viz = [PISO[v][u] for u in range(x - 1, x + 2) for v in range(y - 1, y + 2)
                       if 0 <= u < W and 0 <= v < H and PISO[v][u] is not None and CEL[v][u] not in '#o']
                if not viz:
                    continue
                caixas.append((x, y, x + 1, y + 1, 0, max(viz) + 16, TELHA, REBOCO))
                continue
            z = PISO[y][x] or 0
            base = MAT.get(M['material'][y][x] or '', COR.get(c, (214, 194, 154))) if c in 'rm' else COR.get(c, (214, 194, 154))
            if c == 'o':
                base = (214, 194, 154)
            caixas.append((x, y, x + 1, y + 1, 0, max(1.5, z), base, tuple(int(v * 0.85) for v in base)))
    for cb in M['coberturas']:
        cor = (122, 75, 42) if cb['tipo'] == 'barrel' else (110, 92, 78) if cb['tipo'] in ('halfwall', 'wall') else (150, 112, 70)
        for y in range(cb['y0'], cb['y1'] + 1):
            for x in range(cb['x0'], cb['x1'] + 1):
                caixas.append((x + .12, y + .12, x + .88, y + .88, cb['piso'], cb['piso'] + cb['h'], cor, cor))

    def proj(x, y, z):
        if gira:
            x, y = W - x, H - y
        return (x - y) * A, (x + y) * B - z * C
    xs, ys = [], []
    for (x0, y0, x1, y1, z0, z1, *_ ) in caixas:
        for (px, py) in (proj(x0, y0, z1), proj(x1, y1, 0), proj(x1, y0, 0), proj(x0, y1, 0)):
            xs.append(px); ys.append(py)
    ox, oy = -min(xs) + 20, -min(ys) + 20
    Wi, Hi = int(max(xs) + ox + 20), int(max(ys) + oy + 20)
    img = np.zeros((Hi, Wi, 3), np.uint8); img[:] = (239, 233, 223)

    def quad(pts, cor):
        p = np.array([(px + ox, py + oy) for px, py in pts])
        x0, y0 = np.floor(p.min(0)).astype(int); x1, y1 = np.ceil(p.max(0)).astype(int)
        if x1 <= x0 or y1 <= y0:
            return
        gy, gx = np.mgrid[y0:y1, x0:x1] + 0.5
        dentro = np.ones(gx.shape, bool); s = None
        for i in range(4):
            ax, ay = p[i]; bx, by = p[(i + 1) % 4]
            cr = (bx - ax) * (gy - ay) - (by - ay) * (gx - ax)
            dentro &= cr >= -1e-6 if True else cr <= 0
        if not dentro.any():
            dentro = np.ones(gx.shape, bool)
            for i in range(4):
                ax, ay = p[i]; bx, by = p[(i + 1) % 4]
                dentro &= ((bx - ax) * (gy - ay) - (by - ay) * (gx - ax)) <= 1e-6
        img[y0:y1, x0:x1][dentro] = cor

    def chave(cx):
        x0, y0, x1, y1, z0, z1 = cx[:6]
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        if gira:
            mx, my = W - mx, H - my
        return (mx + my, z0)
    for (x0, y0, x1, y1, z0, z1, top, lado_c) in sorted(caixas, key=chave):
        if gira:
            # vista do noroeste: lados visíveis são o norte (y0) e o oeste (x0)
            sul = [proj(x1, y0, z1), proj(x0, y0, z1), proj(x0, y0, z0), proj(x1, y0, z0)]
            les = [proj(x0, y0, z1), proj(x0, y1, z1), proj(x0, y1, z0), proj(x0, y0, z0)]
        else:
            sul = [proj(x0, y1, z1), proj(x1, y1, z1), proj(x1, y1, z0), proj(x0, y1, z0)]
            les = [proj(x1, y1, z1), proj(x1, y0, z1), proj(x1, y0, z0), proj(x1, y1, z0)]
        topo = [proj(x0, y0, z1), proj(x1, y0, z1), proj(x1, y1, z1), proj(x0, y1, z1)]
        quad(sul, tuple(int(v * 0.78) for v in lado_c))
        quad(les, tuple(int(v * 0.62) for v in lado_c))
        quad(topo, top)
    png(saida, img)
    rot = []
    for (n, x, y) in M['nomes']:
        zz = None
        for dx in (0, 1, -1):
            for dy in (0, 1, -1):
                u, v = int(x) + dx, int(y) + dy
                if 0 <= u < W and 0 <= v < H and PISO[v][u] is not None:
                    zz = PISO[v][u]; break
            if zz is not None:
                break
        px, py = proj(x + .5, y + .5, (zz or 0) + 4)
        rot.append({"n": n, "x": round((px + ox) / Wi * 100, 2), "y": round((py + oy) / Hi * 100, 2)})
    json.dump({"w": Wi, "h": Hi, "rotulos": rot}, open(saida[:-4] + '.json', 'w', encoding='utf-8'), ensure_ascii=False)
    print(saida, img.shape)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else 'se')
