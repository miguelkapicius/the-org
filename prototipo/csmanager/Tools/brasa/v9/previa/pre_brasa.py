# Prévia v2 da Brasa (mais medieval/romana): gera a geometria em 1 unidade do jogo a partir do brasa_mapa.json.
#   python -I pre_brasa.py <json> <saida.npz>
# Saída: grade de alturas (chão + telhado de quatro águas por nível, sem lotes-pirâmide), cores por ponto,
# paredes lisas pelo contorno dos polígonos (com faixas de pedra), e listas de enfeites (torres, arcos, mercado...).
import json, sys, math, numpy as np
from scipy import ndimage
from scipy.spatial import cKDTree

def dentro_poli(poly, X, Y):
    r = np.zeros(X.shape, bool); n = len(poly)
    for k in range(n):
        x0, y0 = poly[k]; x1, y1 = poly[(k + 1) % n]
        cruza = ((y0 > Y) != (y1 > Y)) & (X < (x1 - x0) * (Y - y0) / ((y1 - y0) if y1 != y0 else 1e-9) + x0)
        r ^= cruza
    return r

d = json.load(open(sys.argv[1], encoding='utf-8'))
OUT = sys.argv[2]
GW, GH, QF, GZ = d['GW'], d['GH'], d['QF'], d['GZ']
NX, NY = GW * QF, GH * QF          # 1 px = 1 unidade
CEL = 25.0
cel = np.array([list(r) for r in d['celulas']])
piso = np.array([[np.nan if v is None else v for v in r] for r in d['piso']], float)
topo = np.array([[np.nan if v is None else v for v in r] for r in d['topo']], float)
zona = np.array(d['zona'], int)
matc = np.array([list(r) for r in d['material']])
ZN = d['zonas']
B = cel == '#'
rng = np.random.default_rng(7)

def h32(*a):
    h = 2166136261
    for v in a:
        h = ((h ^ (int(v) & 0xffffffff)) * 16777619) & 0xffffffff
    return h

# ---------------------------------------------------------------- chão e zona preenchidos sob os prédios
_, idx = ndimage.distance_transform_edt(B, return_indices=True)
F = piso[idx[0], idx[1]]
Zs = zona[idx[0], idx[1]]
_, idxb = ndimage.distance_transform_edt(~B, return_indices=True)

# ---------------------------------------------------------------- nível do beiral por lote (cortes variados) e pedaço
def cortes(n, sem):
    r = np.random.default_rng(sem); c = [0]; p = 0
    while p < n:
        p += int(r.choice([10, 12, 14, 16, 18, 20, 22])); c.append(min(p, n))
    return np.array(c)
CXs, CYs = cortes(GW, 5), cortes(GH, 9)
lx = np.searchsorted(CXs, np.arange(GW), side='right') - 1
ly = np.searchsorted(CYs, np.arange(GH), side='right') - 1
LOT = ly[:, None] * 100 + lx[None, :]
Fmax = ndimage.maximum_filter(np.where(B, -1e9, piso), size=21)
Fmax = np.where(Fmax < -1e8, F, Fmax)
T = np.full((GH, GW), np.nan)
for k in np.unique(LOT[B]):
    m = (LOT == k) & B
    lab, n = ndimage.label(m)
    for c in range(1, n + 1):
        mm = lab == c
        base = np.nanmax(Fmax[mm]) + 36
        T[mm] = math.ceil(base / 12) * 12
T = np.where(B, T, T[idxb[0], idxb[1]])

# castelo: um recinto só (retângulo em volta da base CT), muralha com portões; igreja
ZID = {n: i for i, n in enumerate(ZN)}
CX0, CX1, CY1 = 23.5, 44.5, 9.0        # em casas
jj, ii = np.mgrid[0:GH, 0:GW]
CAST = (ii >= int(CX0 * 5)) & (ii < int(CX1 * 5)) & (jj < int(CY1 * 5))
IGREJA = B & (ii >= int(44.6 * 5)) & (ii < int(53.4 * 5)) & (jj >= int(3.3 * 5)) & (jj < 8 * 5)
TCAST = 18 + 60
T = np.where(CAST & B, TCAST, T)
T = np.where(IGREJA, 18 + 60, T)
PEDRA_LOTE = np.zeros((GH, GW), bool)

# ---------------------------------------------------------------- andável em 1 unidade pelos polígonos das peças
xs = np.arange(NX) + 0.5; ys = np.arange(NY) + 0.5
Wk = np.zeros((NY, NX), bool)
for p in d['pecas']:
    poly = np.array(p['poli'], float) * CEL
    x0, y0 = np.floor(poly.min(0)).astype(int); x1, y1 = np.ceil(poly.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, NX), min(y1, NY)
    gx, gy = np.meshgrid(xs[x0:x1], ys[y0:y1])
    Wk[y0:y1, x0:x1] |= dentro_poli(poly, gx, gy)
campo = ndimage.gaussian_filter(Wk.astype(np.float32), 0.8)      # 0 prédio .. 1 rua
B1 = campo < 0.5

def up(a, order=0):
    py, px = np.mgrid[0:NY, 0:NX]
    if order == 0:
        return a[np.clip(py // QF, 0, GH - 1), np.clip(px // QF, 0, GW - 1)]
    cy = (py + 0.5) / QF - 0.5; cx = (px + 0.5) / QF - 0.5
    return ndimage.map_coordinates(a, [cy, cx], order=1, mode='nearest')
F1 = up(F).astype(np.float32)
T1 = up(T).astype(np.float32)
Z1 = up(Zs)
CAST1 = up(CAST); IGR1 = up(IGREJA); PED1 = up(PEDRA_LOTE)
LOT1 = up(LOT)
MAT1 = up(matc)

# ---------------------------------------------------------------- telhado: quatro águas por nível (distância até a beirada)
PITCH, CAP = 0.5, 22.0
R1 = np.where(B1, T1, F1).astype(np.float32)
DIST = np.zeros((NY, NX), np.float32)
SS = np.zeros((NY, NX), np.float32)      # coordenada ao longo do beiral (canal da telha)
for L in np.unique(T1[B1]):
    m = B1 & (T1 == L)
    mp = np.pad(m, 1)
    dd, ind = ndimage.distance_transform_edt(mp, return_indices=True)
    dd = dd[1:-1, 1:-1]; by = ind[0][1:-1, 1:-1] - 1; bx = ind[1][1:-1, 1:-1] - 1
    py, px = np.nonzero(m)
    nxv = px - bx[py, px]; nyv = py - by[py, px]; nn = np.hypot(nxv, nyv) + 1e-6
    SS[py, px] = (px * (-nyv) + py * nxv) / nn
    DIST[m] = dd[m]
dd = DIST
rise = (CAP * (1 - np.exp(-PITCH * np.maximum(dd - 0.5, 0) / CAP))).astype(np.float32)   # sobe a ~27° e vai suavizando, sem platô
casa = B1 & ~CAST1 & ~IGR1
# casas: cada casa é um lote estreito ao longo da rua (semente na fachada), telhado de duas águas com a cumeeira
# paralela à rua, altura e cor por casa -> fileira de casas de vila, não um casarão
dS, indS = ndimage.distance_transform_edt(np.pad(B1, 1), return_indices=True)
dS = dS[1:-1, 1:-1].astype(np.float32); sy_ = indS[0][1:-1, 1:-1] - 1; sx_ = indS[1][1:-1, 1:-1] - 1
cand = np.argwhere(casa & (dS >= 4) & (dS < 5.5))
rng.shuffle(cand)
grade = {}; sementes = []
for y, x in cand:
    r = 46 + 30 * ((x * 7 + y * 13) % 7) / 6.0
    gx, gy = x // 60, y // 60; ok_ = True
    for ax in (gx - 2, gx - 1, gx, gx + 1, gx + 2):
        for ay in (gy - 2, gy - 1, gy, gy + 1, gy + 2):
            for (qx, qy, qr) in grade.get((ax, ay), ()):
                if (qx - x) ** 2 + (qy - y) ** 2 < (0.5 * (r + qr)) ** 2: ok_ = False; break
            if not ok_: break
        if not ok_: break
    if ok_:
        grade.setdefault((gx, gy), []).append((x, y, r)); sementes.append((x, y))
sementes = np.array(sementes)
pyc, pxc = np.nonzero(casa)
_, quem = cKDTree(sementes).query(np.c_[pxc, pyc])
PAR = np.full((NY, NX), -1, np.int32); PAR[pyc, pxc] = quem
NP = len(sementes)
dmax = ndimage.maximum(dS, PAR, index=np.arange(NP)).astype(np.float32)
rp = np.random.default_rng(5)
ALTP = rp.choice([0, 0, 0, 8, 8, 16], NP).astype(np.float32)
CORP = rp.integers(0, 4, NP)
anel = np.array([((sx - CX0 * CEL if sx < CX0 * CEL else 0) ** 2 + 0 if True else 0) for sx in sementes[:, 0]])
dcast = np.hypot(np.maximum(0, np.maximum(CX0 * CEL - sementes[:, 0], sementes[:, 0] - CX1 * CEL)), np.maximum(0, sementes[:, 1] - CY1 * CEL))
PEDP = dcast < 150
TP = T1[sementes[:, 1], sementes[:, 0]] + ALTP
dmp = dmax[np.maximum(PAR, 0)]
dfr = np.minimum(dS, np.maximum(dmp - dS, 0))
riseP = np.minimum(PITCH * np.maximum(dfr - 0.5, 0), 24.0)
R1 = np.where(casa, TP[np.maximum(PAR, 0)] + riseP, R1)
T1 = np.where(casa, TP[np.maximum(PAR, 0)], T1)
DIST = np.where(casa, dfr, DIST); dd = DIST
nxv = pxx0 = None
py_, px_c = np.nonzero(casa)
vx = px_c - sx_[py_, px_c]; vy = py_ - sy_[py_, px_c]; nn = np.hypot(vx, vy) + 1e-6
SS[py_, px_c] = (px_c * (-vy) + py_ * vx) / nn
print("casas", NP)
# igreja: duas águas com cumeeira norte-sul e frontão baixo (templo romano) virado pro sagrado
IX0, IX1 = 44.6 * CEL, 53.4 * CEL
px_ = np.arange(NX)[None, :] + 0.5
gab = 0.3 * np.minimum(px_ - IX0, IX1 - px_)
R1 = np.where(IGR1 & B1, T1 + np.clip(gab, 0, 40), R1)
# castelo: terraço de pedra com ameias na beirada
borda = CAST1 & B1 & (dd <= 3.5)
ameia = ((np.floor(SS / 5).astype(int) % 2) == 0)
R1 = np.where(CAST1 & B1, T1, R1)
R1 = np.where(borda, T1 + np.where(ameia, 8, 4), R1)
H = np.where(B1, R1, F1).astype(np.float32)
# empena: borda de casa mais alta que a vizinha -> pintada de reboco
EMP = np.zeros((NY, NX), bool)
for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
    sp = np.roll(np.roll(PAR, dy, 0), dx, 1); sh = np.roll(np.roll(H, dy, 0), dx, 1)
    EMP |= casa & (sp != PAR) & (sp >= 0) & (H - sh > 1.5)
LOT1 = np.where(casa, PAR + 100000, LOT1)
PED1 = np.zeros((NY, NX), bool); PED1[casa] = PEDP[PAR[casa]]

# ---------------------------------------------------------------- cores por ponto
def hx(h): return np.array([((h >> 16) & 255) / 255, ((h >> 8) & 255) / 255, (h & 255) / 255], np.float32)
COR = np.zeros((NY, NX, 3), np.float32)
pyy, pxx = np.mgrid[0:NY, 0:NX]
# chão calmo: cada lugar tem um piso só, e o piso só muda numa soleira (faixa de pedra clara)
ruido = ndimage.gaussian_filter(rng.random((NY // 8 + 2, NX // 8 + 2)).astype(np.float32), 2.0)
ruido = (ruido - ruido.mean()) / (ruido.std() + 1e-6)
ruido = ndimage.zoom(ruido, 8, order=1)[:NY, :NX]
def lajes(cor, lx, ly, junta=0.9, var=0.035, desloca=True):
    off = ((pyy // ly) % 2) * (lx // 2) if desloca else 0
    ix = (pxx + off) // lx; iy = pyy // ly
    j = ((ix * 73856093) ^ (iy * 19349663)) & 1023
    t = 1.0 + var * ((j % 7) / 3.0 - 1.0) + 0.025 * ruido
    c = hx(cor)[None, None, :] * t[..., None]
    jt = (((pxx + off) % lx) == 0) | ((pyy % ly) == 0)
    c[jt] *= junta
    return c
CL = np.zeros((NY, NX), np.int8)      # 0 rua, 1 castelo, 2 travertino (A), 3 tijolo (mercado), 4 madeira
CL[CAST1 & ~B1] = 1
CL[np.isin(Z1, [ZID['Piazza della Chiesa'], ZID['Sagrato'], ZID['Lavatoio']]) & ~CAST1] = 2
CL[(Z1 == ZID['Mercato'])] = 3
CL[MAT1 == 'W'] = 4
COR[:] = lajes(0xB2A898, 12, 7)
for k, cor_ in ((1, lajes(0xA29C92, 14, 14, 0.88, 0.03, False)), (2, lajes(0xE0D5BE, 16, 10, 0.92, 0.025)),
                (3, lajes(0xB06A52, 6, 3, 0.9, 0.04)), (4, None)):
    m = (CL == k) & ~B1
    if k == 4:
        tab = lajes(0xC4B8A2, 10, 10, 0.9, 0.03, False)
        COR[m] = tab[m]
    else:
        COR[m] = cor_[m]
# soleira: faixa de pedra clara onde um piso encontra outro (no mesmo nível)
viz = np.zeros((NY, NX), bool)
for dy, dx in ((0, 2), (0, -2), (2, 0), (-2, 0), (1, 1), (-1, -1), (1, -1), (-1, 1)):
    sh = np.roll(np.roll(CL, dy, 0), dx, 1); shb = np.roll(np.roll(B1, dy, 0), dx, 1)
    shf = np.roll(np.roll(F1, dy, 0), dx, 1)
    viz |= (sh != CL) & ~shb & (np.abs(shf - F1) < 1.5)
sole = viz & ~B1 & (CL != 4)
COR[sole] = hx(0xD6CCB8)
# degrau/rampa: sem desenho de laje, só a pedra
# telhado: telha lisa com canal discreto, um tom por lote, beiral escuro fino
TEL = hx(0xC25A34)
lt = np.ones((NY, NX), np.float32); lt[casa] = (0.9 + 0.14 * rp.random(NP))[PAR[casa]]
canal = (np.floor(SS / 3.0).astype(int) % 2) == 0
tel = TEL[None, None, :] * lt[..., None] * np.where(canal, 1.0, 0.94)[..., None]
tel[dd < 1.2] = hx(0x9A4428)
COR[casa] = tel[casa]
igr = IGR1 & B1
COR[igr] = tel[igr]
# castelo: terraço de pedra cinza (lajes grandes) e parapeito claro
cb = CAST1 & B1
pc = lajes(0x9C968C, 14, 14, 0.9, 0.03, False)
pc = hx(0xA39C91)[None, None, :] * (1 + 0.02 * ruido)[..., None]
COR[cb] = pc[cb]
COR[borda] = hx(0xB2ABA0)
REBF = np.stack([hx(0xE6B678), hx(0xE3A486), hx(0xEFDDB8), hx(0xEDC77A)])
emp_c = np.where(PED1[..., None], hx(0xC6B89E)[None, None, :], REBF[CORP[np.maximum(PAR, 0)]])
COR[EMP] = emp_c[EMP]

# ---------------------------------------------------------------- paredes: contorno liso do prédio (marching squares)
g = 1.0 - campo
gy_, gx_ = np.gradient(ndimage.gaussian_filter(g, 1.0))
a = g[:-1, :-1]; b = g[:-1, 1:]; c = g[1:, 1:]; e = g[1:, :-1]
caso = (a >= .5) * 1 + (b >= .5) * 2 + (c >= .5) * 4 + (e >= .5) * 8
TAB = {1: [(3, 0)], 2: [(0, 1)], 3: [(3, 1)], 4: [(1, 2)], 5: [(3, 0), (1, 2)], 6: [(0, 2)], 7: [(3, 2)], 8: [(2, 3)],
       9: [(0, 2)], 10: [(0, 1), (2, 3)], 11: [(1, 2)], 12: [(1, 3)], 13: [(0, 1)], 14: [(0, 3)]}
def ponto(ed, Y, X):
    va, vb, vc, ve = a[Y, X], b[Y, X], c[Y, X], e[Y, X]
    def t(p, q): return np.clip((0.5 - p) / np.where(np.abs(q - p) < 1e-6, 1e-6, q - p), 0, 1)
    if ed == 0: return X + t(va, vb), Y + 0.0
    if ed == 1: return X + 1.0, Y + t(vb, vc)
    if ed == 2: return X + 1 - t(vc, ve), Y + 1.0
    return X + 0.0, Y + 1 - t(ve, va)
P0, P1 = [], []
for k, segs in TAB.items():
    Y, X = np.nonzero(caso == k)
    for e0, e1 in segs:
        x0, y0 = ponto(e0, Y, X); x1, y1 = ponto(e1, Y, X)
        P0.append(np.c_[x0, y0]); P1.append(np.c_[x1, y1])
P0 = np.concatenate(P0) + 0.5; P1 = np.concatenate(P1) + 0.5     # centro de pixel -> coordenada em unidades
print("segmentos de parede", len(P0))

def amostra(arr, P, order=1):
    return ndimage.map_coordinates(arr, [P[:, 1] - 0.5, P[:, 0] - 0.5], order=order, mode='nearest')
def normal(P):
    nx = -amostra(gx_, P); ny = -amostra(gy_, P); n = np.hypot(nx, ny) + 1e-9
    return np.c_[nx / n, ny / n]
N0, N1 = normal(P0), normal(P1)
def pega(arr, P):
    xi = np.clip(np.floor(P[:, 0]).astype(int), 0, NX - 1); yi = np.clip(np.floor(P[:, 1]).astype(int), 0, NY - 1)
    return arr[yi, xi]
def info(P, N):
    fora = P + N * 1.8; dentro = P - N * 1.8
    zb = pega(F1, fora); zt = pega(H, dentro)
    return zb, zt, pega(LOT1, dentro), pega(CAST1, dentro), pega(PED1, dentro), pega(IGR1, dentro)
zb0, zt0, lot0, ca0, pe0, ig0 = info(P0, N0)
zb1, zt1, *_ = info(P1, N1)
OFF = 0.7
Q0 = P0 + N0 * OFF; Q1 = P1 + N1 * OFF
mid = (P0 + P1) / 2; tg = P1 - P0; tg /= (np.hypot(tg[:, 0], tg[:, 1])[:, None] + 1e-9)
smid = mid[:, 0] * tg[:, 0] + mid[:, 1] * tg[:, 1]
REB = np.stack([hx(0xE6B678), hx(0xE3A486), hx(0xEFDDB8), hx(0xEDC77A)])
PEDRAS = np.stack([hx(0xB7AE9E), hx(0xA9A08F), hx(0xC2B8A6), hx(0x9E978B)])
CINZA = np.stack([hx(0x9E988E), hx(0x979187), hx(0xA39D93), hx(0x948E84)])
TRAV = np.stack([hx(0xE6DBC4), hx(0xDCCFB4), hx(0xEFE6D2), hx(0xD5C8AD)])
# faixas: cada segmento vira quads de zb..zt; cor por faixa
WV, WC = [], []
def quad(z0a, z0b, z1a, z1b, cor, sel):
    # vértices: Q0@z0a, Q1@z0b, Q1@z1b, Q0@z1a
    n = sel.sum()
    if n == 0: return
    v = np.stack([np.c_[Q0[sel], z0a[sel]], np.c_[Q1[sel], z0b[sel]], np.c_[Q1[sel], z1b[sel]], np.c_[Q0[sel], z1a[sel]]], 1)
    WV.append(v); WC.append(np.repeat(cor[sel][:, None, :], 4, 1))
ok = (zt0 - zb0 > 2) | (zt1 - zb1 > 2)
pedra_toda = (ca0 | pe0 | ig0) & ok
BAND = 4.2
nb = int(np.ceil((np.maximum(zt0 - zb0, zt1 - zb1).max()) / BAND)) + 1
blocoh = lambda k: (np.floor((smid + k * 4.5) / 9.0).astype(np.int64))
for k in range(nb):
    za = zb0 + k * BAND; zb_ = zb1 + k * BAND
    ztop_a = np.minimum(za + BAND, zt0); ztop_b = np.minimum(zb_ + BAND, zt1)
    sel = ok & ((za < zt0) | (zb_ < zt1))
    if not sel.any(): break
    hb = (blocoh(k) * 73856093 + k * 19349663 + lot0 * 83492791) & 0xffff
    jit = (0.97 + 0.05 * (hb % 13) / 12.0).astype(np.float32)[:, None]
    pal = np.where(ca0[:, None], CINZA[hb % 4], np.where(ig0[:, None], TRAV[hb % 4], PEDRAS[hb % 4]))
    pedra = pal * jit
    pedra = np.where(ca0[:, None], hx(0xA9A296)[None, :] * (1.0 if k % 2 else 0.965), pedra)
    pedra = np.where((pe0 & ~ca0)[:, None], hx(0xC6B89E)[None, :] * jit, pedra)
    reb = np.where((lot0 >= 100000)[:, None], REB[CORP[np.clip(lot0 - 100000, 0, NP - 1)]], REB[(lot0 * 2654435761 >> 9) % 4])
    base = (k * BAND) < 10.5                                   # rodapé de pedra em todo prédio
    topo_ = (za + BAND >= zt0 - 0.01) & (k * BAND > 10)        # cimalha de pedra no alto
    cor = np.where((pedra_toda | base)[:, None], pedra, reb)
    cor = np.where((topo_ & ~pedra_toda)[:, None], hx(0xE7DCC6)[None, :] * jit, cor)
    quad(np.minimum(za, zt0), np.minimum(zb_, zt1), ztop_a, ztop_b, cor.astype(np.float32), sel)
# junção castelo x casa: parede de pedra reta na divisa do recinto, do telhado da casa até o alto do castelo
JV, JC = [], []
X0c, X1c, Y1c = int(CX0 * CEL), int(CX1 * CEL), int(CY1 * CEL)
def junta_parede(pa, pb, za, zb_):
    if zb_ - za < 1: return
    JV.append([(pa[0], pa[1], za), (pb[0], pb[1], za), (pb[0], pb[1], zb_), (pa[0], pa[1], zb_)])
    JC.append([hx(0x9A948A)] * 4)
for y in range(0, Y1c):
    for xc, xo in ((X0c, X0c - 1), (X1c - 1, X1c)):
        if 0 <= xo < NX and B1[y, xc] and B1[y, xo]:
            xl = X0c if xc == X0c else X1c
            junta_parede((xl, y), (xl, y + 1), min(H[y, xo], H[y, xc]) - 30, H[y, xc])
for x in range(X0c, X1c):
    if B1[Y1c - 1, x] and B1[Y1c, x]:
        junta_parede((x, Y1c), (x + 1, Y1c), min(H[Y1c, x], H[Y1c - 1, x]) - 30, H[Y1c - 1, x])
if JV:
    WV.append(np.array(JV, np.float32)); WC.append(np.array(JC, np.float32))
print("junções", len(JV))
# degrau entre casas vizinhas de altura diferente: parede vertical na divisa (antes era uma rampa serrilhada)
def cor_alta(yy, xx):
    c = np.empty((len(yy), 3), np.float32)
    pa = PAR[yy, xx]; ca = CAST1[yy, xx]; ig = IGR1[yy, xx]
    c[:] = REB[CORP[np.clip(pa, 0, NP - 1)]]
    c[PED1[yy, xx]] = hx(0xC6B89E)
    c[ca] = hx(0xA9A296); c[ig] = hx(0xE6DBC4)
    return c
DV, DC = [], []
for eixo in (0, 1):
    if eixo == 0:
        A_, B_ = H[:, :-1], H[:, 1:]; ok_ = B1[:, :-1] & B1[:, 1:] & (np.abs(A_ - B_) > 1.5)
        yy, xx = np.nonzero(ok_)
        za, zb_ = A_[yy, xx], B_[yy, xx]
        alto_x = np.where(za > zb_, xx, xx + 1)
        c = cor_alta(yy, alto_x)
        lo, hi = np.minimum(za, zb_), np.maximum(za, zb_)
        X = xx + 1.0
        v = np.stack([np.c_[X, yy, lo], np.c_[X, yy + 1.0, lo], np.c_[X, yy + 1.0, hi], np.c_[X, yy, hi]], 1)
    else:
        A_, B_ = H[:-1, :], H[1:, :]; ok_ = B1[:-1, :] & B1[1:, :] & (np.abs(A_ - B_) > 1.5)
        yy, xx = np.nonzero(ok_)
        za, zb_ = A_[yy, xx], B_[yy, xx]
        alto_y = np.where(za > zb_, yy, yy + 1)
        c = cor_alta(alto_y, xx)
        lo, hi = np.minimum(za, zb_), np.maximum(za, zb_)
        Y = yy + 1.0
        v = np.stack([np.c_[xx, Y, lo], np.c_[xx + 1.0, Y, lo], np.c_[xx + 1.0, Y, hi], np.c_[xx, Y, hi]], 1)
    DV.append(v.astype(np.float32)); DC.append(np.repeat(c[:, None, :], 4, 1))
WV.extend(DV); WC.extend(DC)
print("degraus entre casas", sum(len(x) for x in DV))
WV = np.concatenate(WV).astype(np.float32); WC = np.concatenate(WC).astype(np.float32)
print("quads de parede", len(WV))

# ---------------------------------------------------------------- vagas de fachada (janela/porta/loja), no raster das subcasas
vagas = []
PASSO = 7
for j in range(GH):
    for i in range(GW):
        if not B[j, i]: continue
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            u, v = i + di, j + dj
            if not (0 <= u < GW and 0 <= v < GH) or B[v, u]: continue
            s = j if di else i
            if s % PASSO != 3: continue
            fz = piso[v, u]
            if np.isnan(fz): continue
            okk = True
            for t in range(-3, 4):
                i2, j2 = (i, j + t) if di else (i + t, j)
                u2, v2 = i2 + di, j2 + dj
                if not (0 <= i2 < GW and 0 <= j2 < GH and 0 <= u2 < GW and 0 <= v2 < GH) or not B[j2, i2] or B[v2, u2] or piso[v2, u2] != fz:
                    okk = False; break
            if not okk: continue
            cx = (i + 0.5 + di * 0.5) * QF + di * OFF; cy = (j + 0.5 + dj * 0.5) * QF + dj * OFF
            Hh = float(pega(H, np.array([[cx - di * 3, cy - dj * 3]]))[0]) - fz
            vagas.append((cx, cy, di, dj, float(fz), Hh, int(zona[v, u]), int(CAST[j, i]), int(PEDRA_LOTE[j, i]), int(IGREJA[j, i]), h32(i, j, di, dj)))
vagas = [v for v in vagas if v[7] == 1]          # no castelo continua no raster (seteiras)
# uma vaga por casa: no meio da frente do lote, virada pra rua mais perto
Z1z = Z1
for k_, (x, y) in enumerate(sementes):
    qx, qy = sx_[y, x], sy_[y, x]                 # pixel de rua mais perto
    if not (0 <= qx < NX and 0 <= qy < NY): continue      # frente pra fora do mapa
    nx_, ny_ = qx - x, qy - y; n_ = math.hypot(nx_, ny_) + 1e-9; nx_, ny_ = nx_ / n_, ny_ / n_
    wx, wy = qx + 0.5 - nx_ * 0.5 + nx_ * OFF, qy + 0.5 - ny_ * 0.5 + ny_ * OFF
    fz = float(F1[qy, qx]); Hh = float(TP[k_]) - fz
    vagas.append((wx, wy, nx_, ny_, fz, Hh, int(Z1z[qy, qx]), 0, int(PEDP[k_]), 0, h32(x, y, 77)))
print("vagas", len(vagas))

# ---------------------------------------------------------------- torres: cantos do castelo, portão do arco CT, torres medievais, campanário
def predio_em(x, y):
    xi, yi = int(x), int(y)
    return 0 <= xi < NX and 0 <= yi < NY and B1[yi, xi]
torres = []   # (cx, cy, lado, z0, z1, tipo)
edtB = ndimage.distance_transform_edt(np.pad(B1, 1, constant_values=True))[1:-1, 1:-1]
LADO = 32.0
for (cx, cy) in [(CX0, 0.0), (CX1, 0.0), (CX1, CY1), (CX0, CY1), (33.0, CY1), (35.5, CY1)]:
    x0, y0 = cx * CEL, cy * CEL
    cand = np.argwhere((edtB >= LADO * 0.72) & CAST1)
    dist2 = (cand[:, 1] - x0) ** 2 + (cand[:, 0] - y0) ** 2
    y, x = cand[np.argmin(dist2)]
    if np.sqrt(dist2.min()) < 60 and all((x - t[0]) ** 2 + (y - t[1]) ** 2 > 40 ** 2 for t in torres):
        torres.append((float(x), float(y), LADO, 0.0, float(TCAST) + 30, 'castelo'))
# campanário de tijolo ao lado da igreja
torres.append((54.25 * CEL, 4.6 * CEL, 26.0, 0.0, 18 + 120, 'campanario'))

# arcos sobre a rua: (x0,y0,x1,y1 do vão em unidades, eixo, piso)
arcos = [((32.5 * CEL, CY1 * CEL - 12), (36.0 * CEL, CY1 * CEL - 12), 18.0, 'castelo'),
         ((CX0 * CEL + 6, 3.0 * CEL), (CX0 * CEL + 6, 6.0 * CEL), 18.0, 'castelo'),
         ((CX1 * CEL - 6, 1.2 * CEL), (CX1 * CEL - 6, 3.2 * CEL), 18.0, 'castelo'),
         ((8.5 * CEL, 21.3 * CEL), (12.1 * CEL, 21.3 * CEL), None, 'porta'),
         ((45.5 * CEL, 41.0 * CEL), (45.5 * CEL, 44.5 * CEL), 0.0, 'rua')]
arcos2 = []
for p0, p1, fz, tipo in arcos:
    mx, my = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
    if fz is None: fz = float(F1[int(my), int(mx)])
    u = np.array(p1) - np.array(p0); u /= np.linalg.norm(u)
    qa = np.array(p0) - u * 8; qb = np.array(p1) + u * 8
    zt = float(min(T1[int(qa[1]), int(qa[0])], T1[int(qb[1]), int(qb[0])]))
    arcos2.append((p0[0], p0[1], p1[0], p1[1], fz, zt, tipo))

# chaminés na cumeeira
cum = np.argwhere(casa & (dd > 9) & (dd < 30) & ~CAST1)
rng2 = np.random.default_rng(4); chams = []
for y, x in cum[rng2.choice(len(cum), size=min(400, len(cum)), replace=False)]:
    if all((x - p[0]) ** 2 + (y - p[1]) ** 2 > 90 ** 2 for p in chams):
        chams.append((float(x), float(y), float(H[y, x])))
    if len(chams) >= 45: break

par_ = []
alto = (MAT1 == 'W') & ~B1
for y, x in np.argwhere(alto)[::1]:
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        u, v = x + dx * 2, y + dy * 2
        if 0 <= u < NX and 0 <= v < NY and not B1[v, u] and not alto[v, u] and F1[v, u] < F1[y, x] - 8:
            if (x + y) % 3 == 0: par_.append((float(x), float(y), float(F1[y, x])))
            break
print("parapeito", len(par_))
np.savez_compressed(OUT, parapeito=np.array(par_, np.float64),  H=H, COR=COR.astype(np.float16), WV=WV, WC=WC.astype(np.float16), vagas=np.array(vagas, np.float64),
                    torres=np.array([t[:5] + ({'castelo': 0, 'medieval': 1, 'campanario': 2}[t[5]],) for t in torres], np.float64),
                    arcos=np.array([a[:6] + ({'castelo': 0, 'porta': 1, 'rua': 2}[a[6]],) for a in arcos2], np.float64),
                    chams=np.array(chams, np.float64), B1=B1, merc=(Z1 == ZID['Mercato']) & ~B1)
print("torres", len(torres), "arcos", len(arcos2), "chaminés", len(chams))
