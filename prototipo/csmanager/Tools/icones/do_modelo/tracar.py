# Blender (pelo numpy dele): silhueta renderizada -> contorno SVG (bordas de pixel encadeadas + Douglas-Peucker).
# Uso: blender -b -P tracar.py -- <sil.png> <saida.txt> <largura_viewbox> [tol]
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
img = bpy.data.images.load(a[0]); W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)[::-1]   # linha 0 = topo
m = px[:, :, 0] < 0.5
# reduz pra ~4x a resolução final (suaviza o serrilhado do render) com média
VB = float(a[2]); tol = float(a[3]) if len(a) > 3 else 0.35
ys, xs = np.nonzero(m); x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
m = m[y0:y1, x0:x1]
MARG = 7.0
esc = (VB - 2 * MARG) / m.shape[1]          # unidades de viewbox por pixel do render
alto = m.shape[0] * esc + 2 * MARG
f = max(1, int(round(0.25 / esc)))         # pixel de trabalho ~0,25 unidade
h2, w2 = m.shape[0] // f, m.shape[1] // f
m = m[:h2 * f, :w2 * f].reshape(h2, f, w2, f).mean(axis=(1, 3)) > 0.5
e = esc * f
P = np.pad(m, 1)
# arestas dirigidas com o cheio à esquerda (sentido horário na tela = y pra baixo)
nxt = {}
Hh, Ww = P.shape
for y in range(Hh - 1):
    for x in range(Ww - 1):
        pass
# vetorizado: arestas horizontais e verticais
arestas = []
cima = P[1:, :] & ~P[:-1, :]     # cheio embaixo, vazio em cima: aresta no topo do pixel (y), indo pra direita
baixo = P[:-1, :] & ~P[1:, :]    # cheio em cima, vazio embaixo: aresta na base, indo pra esquerda
esq = P[:, 1:] & ~P[:, :-1]      # cheio à direita: aresta à esquerda do pixel, indo pra cima
dir_ = P[:, :-1] & ~P[:, 1:]     # cheio à esquerda: aresta à direita, indo pra baixo
for y, x in zip(*np.nonzero(cima)): nxt.setdefault((x, y + 1), []).append((x + 1, y + 1))
for y, x in zip(*np.nonzero(baixo)): nxt.setdefault((x + 1, y + 1), []).append((x, y + 1))
for y, x in zip(*np.nonzero(esq)): nxt.setdefault((x + 1, y + 1), []).append((x + 1, y))
for y, x in zip(*np.nonzero(dir_)): nxt.setdefault((x + 1, y), []).append((x + 1, y + 1))
laços = []
while nxt:
    ini = next(iter(nxt)); p = ini; laço = [p]
    while True:
        lst = nxt.get(p)
        if not lst: break
        q = lst.pop()
        if not lst: del nxt[p]
        p = q
        if p == ini: break
        laço.append(p)
    if len(laço) > 8: laços.append(laço)

def dp(pts, t):
    pts = np.asarray(pts, dtype=float)
    if len(pts) < 3: return pts
    a_, b_ = pts[0], pts[-1]; d = b_ - a_; n = np.hypot(*d)
    if n == 0: dist = np.hypot(*(pts - a_).T)
    else: dist = np.abs(d[0] * (pts[:, 1] - a_[1]) - d[1] * (pts[:, 0] - a_[0])) / n
    i = int(np.argmax(dist))
    if dist[i] > t: return np.vstack([dp(pts[:i + 1], t)[:-1], dp(pts[i:], t)])
    return np.vstack([a_, b_])

saida = []
for laço in laços:
    pts = np.array(laço, dtype=float) - 1      # tira o pad
    pts = pts * e + MARG
    # fecha: começa no ponto mais longe do primeiro pra o DP não perder o canto
    k = int(np.argmax(np.hypot(*(pts - pts[0]).T)))
    s = np.vstack([dp(np.vstack([pts[:k + 1]]), tol)[:-1], dp(np.vstack([pts[k:], pts[:1]]), tol)[:-1]])
    if len(s) < 3: continue
    area = 0.5 * np.sum(s[:, 0] * np.roll(s[:, 1], -1) - np.roll(s[:, 0], -1) * s[:, 1])
    if abs(area) < 0.8: continue
    saida.append("M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in s) + " Z")
open(a[1], "w").write(f'{VB:.0f} {alto:.0f}\n' + "\n".join(saida))
open(a[1] + ".T", "w").write(f"{x0} {y0} {esc}")
print("TRACOU", len(saida), "laços", VB, round(alto))
