# Blender: prévia v2 da Brasa (medieval/romana) a partir do pre.npz do pre_brasa.py.
#   blender -b --factory-startup --python brasa_blender2.py -- <json> <pre.npz> <pasta glTF> <saida_prefixo> [vistas]
import bpy, sys, math, os, json
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
JS, NPZ, PACK, SAIDA = a[0], a[1], a[2], a[3]
VISTAS = a[4].split(',') if len(a) > 4 else ['topo', 'ct', 'tr', 'b', 'a', 'meio']
d = json.load(open(JS, encoding='utf-8'))
P = np.load(NPZ)
GZ = d['GZ']; E = 1 / 25.0; CEL = 25.0
H = P['H']; NY, NX = H.shape
bpy.ops.wm.read_factory_settings(use_empty=True)
cena = bpy.context.scene
rng = np.random.default_rng(11)

def lin(c):
    c = np.asarray(c, np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def hx(h): return lin([((h >> 16) & 255) / 255, ((h >> 8) & 255) / 255, (h & 255) / 255])

# texturas v5 (geradas aqui, tileáveis): cantaria do castelo, lajão, pedra rústica, reboco. Valor = multiplicador/2.
TILE = 64.0          # unidades do jogo por repetição
NT = 1024
def borra(a, sig):
    f = np.fft.fftfreq(a.shape[0])[:, None] ** 2 + np.fft.fftfreq(a.shape[1])[None, :] ** 2
    return np.real(np.fft.ifft2(np.fft.fft2(a) * np.exp(-2 * (math.pi ** 2) * (sig ** 2) * f)))
def ruido_t(r, sig):
    a = borra(r.standard_normal((NT, NT)), sig); return a / (a.std() + 1e-9)
def blocos(seed, linhas, cmin, cmax, junta, arg, var, chanfro=0.22, mancha=0.06):
    r = np.random.default_rng(seed); ppu = NT / TILE
    img = np.ones((NT, NT, 3), np.float32)
    xs = (np.arange(NT) + 0.5) / ppu
    y0 = 0.0
    for alt in linhas:
        ya, yb = int(round(y0 * ppu)), int(round((y0 + alt) * ppu)); y0 += alt
        o = r.uniform(0, TILE); cuts = []; x = o
        while x < o + TILE - cmin * 0.6:
            cuts.append(x % TILE); x += r.uniform(cmin, cmax)
        cuts = np.sort(np.array(cuts))
        d = np.abs(xs[:, None] - cuts[None, :]); d = np.minimum(d, TILE - d).min(1) * ppu        # px até a junta vertical
        bid = np.searchsorted(cuts, xs, side='right') % len(cuts)
        tint = 1 + var * r.standard_normal(len(cuts)); hue = 1 + 0.025 * r.standard_normal((len(cuts), 3))
        ys = np.arange(ya, yb); dy = np.minimum(ys - ya + 0.5, yb - ys - 0.5)
        e = np.minimum(d[None, :], dy[:, None])
        sh = 1 - chanfro * np.exp(-np.maximum(e - junta, 0) / 2.5)
        c = (tint[bid][None, :, None] * hue[bid][None, :, :]) * sh[..., None]
        c = np.where((e < junta)[..., None], arg, c)
        img[ya:yb] = c
    img *= (1 + 0.05 * ruido_t(r, 1.2))[..., None]
    img *= (1 + mancha * ruido_t(r, 40))[..., None]
    return img
def linhas_aleat(seed, amin, amax):
    r = np.random.default_rng(seed); L = []; s = 0.0
    while s < TILE - amax: a_ = r.uniform(amin, amax); L.append(a_); s += a_
    L.append(TILE - s); return L
TEXS = []
def registra(nome, img, cod):
    img = img / img.reshape(-1, 3).mean(0)[None, None, :] * 0.5
    im = bpy.data.images.new(nome, NT, NT, alpha=False, float_buffer=True)
    rgba = np.ones((NT, NT, 4), np.float32); rgba[..., :3] = np.clip(img, 0, 1)
    im.pixels.foreach_set(rgba.ravel()); im.update()
    caminho = os.path.join(os.path.dirname(SAIDA), "tex_" + nome + ".png")
    im.filepath_raw = caminho; im.file_format = 'PNG'; im.save()
    im2 = bpy.data.images.load(caminho); im2.colorspace_settings.name = 'Non-Color'
    TEXS.append((cod, im2)); return
    TEXS.append((cod, im))
registra("cantaria", blocos(1, [7, 6, 8, 7, 6, 8, 7, 6, 9], 9, 19, 1.8, 0.48, 0.11, 0.32, 0.09), 0.0)
registra("lajao", blocos(2, [8, 10, 7, 9, 11, 8, 11], 8, 15, 1.5, 0.6, 0.08, 0.18, 0.08), 0.3)
registra("rustica", blocos(3, linhas_aleat(3, 2.6, 4.4), 3, 7.5, 2.4, 1.12, 0.11, 0.3, 0.05), 0.5)
rr_ = np.random.default_rng(4)
reb_ = (1 + 0.07 * ruido_t(rr_, 30) + 0.03 * ruido_t(rr_, 4) + 0.015 * ruido_t(rr_, 0.8))[..., None] * np.ones(3)[None, None, :]
registra("reboco", reb_, 0.75)
print("texturas ok")

def material_vc(nome, rough=0.85):
    mt = bpy.data.materials.new(nome); mt.use_nodes = True
    nt = mt.node_tree; bsdf = nt.nodes["Principled BSDF"]
    ca = nt.nodes.new("ShaderNodeVertexColor"); ca.layer_name = "Col"
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    esc = 1.0 / (TILE * E); mp.inputs['Scale'].default_value = (esc, esc, esc)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    branco = nt.nodes.new("ShaderNodeRGB"); branco.outputs[0].default_value = (1, 1, 1, 1)
    atual = branco.outputs[0]
    for cod, im in TEXS:
        tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = im; tx.projection = 'BOX'; tx.projection_blend = 0.3
        nt.links.new(mp.outputs['Vector'], tx.inputs['Vector'])
        sc = nt.nodes.new("ShaderNodeVectorMath"); sc.operation = 'SCALE'; sc.inputs['Scale'].default_value = 2.0
        nt.links.new(tx.outputs['Color'], sc.inputs[0])
        cm = nt.nodes.new("ShaderNodeMath"); cm.operation = 'COMPARE'; cm.inputs[1].default_value = cod; cm.inputs[2].default_value = 0.06
        nt.links.new(ca.outputs['Alpha'], cm.inputs[0])
        mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = 'RGBA'
        nt.links.new(cm.outputs[0], mx.inputs['Factor']); nt.links.new(atual, mx.inputs[6]); nt.links.new(sc.outputs[0], mx.inputs[7])
        atual = mx.outputs[2]
    mul = nt.nodes.new("ShaderNodeVectorMath"); mul.operation = 'MULTIPLY'
    nt.links.new(ca.outputs['Color'], mul.inputs[0]); nt.links.new(atual, mul.inputs[1])
    nt.links.new(mul.outputs[0], bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = rough
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs['Strength'].default_value = 0.55; bp.inputs['Distance'].default_value = 0.012
    nt.links.new(atual, bp.inputs['Height']); nt.links.new(bp.outputs['Normal'], bsdf.inputs['Normal'])
    return mt

MVC = material_vc("vc")

def malha(nome, co, faces_flat, nlados, cor, dominio):
    """co: (n,3) Blender; faces_flat: índices; nlados: lados por face (fixo); cor: (n,3) por ponto ou por canto."""
    me = bpy.data.meshes.new(nome)
    nv = len(co); nf = len(faces_flat) // nlados
    me.vertices.add(nv); me.vertices.foreach_set("co", co.astype(np.float32).ravel())
    me.loops.add(len(faces_flat)); me.loops.foreach_set("vertex_index", faces_flat.astype(np.int32))
    me.polygons.add(nf); me.polygons.foreach_set("loop_start", np.arange(0, len(faces_flat), nlados, dtype=np.int32))
    me.update(calc_edges=True)
    at = me.color_attributes.new("Col", 'FLOAT_COLOR', dominio)
    rgba = np.ones((len(cor), 4), np.float32); rgba[:, :3] = lin(np.clip(cor[:, :3], 0, 1))
    if cor.shape[1] == 4: rgba[:, 3] = cor[:, 3]
    at.data.foreach_set("color", rgba.ravel())
    me.materials.append(MVC)
    ob = bpy.data.objects.new(nome, me); cena.collection.objects.link(ob)
    return ob

# ---------------------------------------------------------------- chão + telhados (grade de alturas em 1 unidade)
py, px = np.mgrid[0:NY, 0:NX]
co = np.stack([(px + 0.5) * E, -(py + 0.5) * E, (GZ + H) * E], -1).reshape(-1, 3)
vid = np.arange(NY * NX).reshape(NY, NX)
q = np.stack([vid[:-1, :-1], vid[1:, :-1], vid[1:, 1:], vid[:-1, 1:]], -1).reshape(-1)
malha("grade", co, q, 4, np.c_[P['COR'].astype(np.float32).reshape(-1, 3), P['MASK'].astype(np.float32).reshape(-1)], 'POINT')
del co, q
print("grade ok")

# ---------------------------------------------------------------- paredes (contorno liso, faixas de pedra)
WV = P['WV']; n = len(WV)
cw = np.stack([WV[..., 0] * E, -WV[..., 1] * E, (GZ + WV[..., 2]) * E], -1).reshape(-1, 3)
malha("paredes", cw, np.arange(n * 4), 4, P['WC'].astype(np.float32).reshape(-1, 4), 'CORNER')
print("paredes ok", n)

# saia nas bordas do mapa (o fundo dos prédios da borda)
sv, sc = [], []
def saia(xs, ys, zs):
    for k in range(len(xs) - 1):
        z0 = 0.0; z1a, z1b = (GZ + zs[k]) * E, (GZ + zs[k + 1]) * E
        sv.append([(xs[k] * E, -ys[k] * E, z0), (xs[k + 1] * E, -ys[k + 1] * E, z0), (xs[k + 1] * E, -ys[k + 1] * E, z1b), (xs[k] * E, -ys[k] * E, z1a)])
        c = hx(0xE0B57A) if (k // 40) % 3 else hx(0xE6A58A)
        sc.append([hx(0xB7AE9E), hx(0xB7AE9E), c, c])
st = 4
xx = np.arange(0, NX, st); yy = np.arange(0, NY, st)
saia(xx + 0.5, np.full(len(xx), NY - 0.5), H[NY - 1, xx]); saia(np.full(len(yy), NX - 0.5), yy + 0.5, H[yy, NX - 1])
saia(xx + 0.5, np.full(len(xx), 0.5), H[0, xx]); saia(np.full(len(yy), 0.5), yy + 0.5, H[yy, 0])
sv = np.array(sv, np.float32).reshape(-1, 3); sc = np.array(sc, np.float32).reshape(-1, 3)
malha("saia", sv, np.arange(len(sv)), 4, (np.clip(sc, 0, 1) ** (1 / 2.2)), 'CORNER')

# ---------------------------------------------------------------- caixas e peças simples (cor por canto)
BV, BC, BA = [], [], []
ALFA = [1.0]
def caixa(x0, y0, z0, x1, y1, z1, cor, cor_topo=None):
    """caixa em unidades do jogo (y do jogo pra baixo)."""
    X0, X1 = x0 * E, x1 * E; Y0, Y1 = -y1 * E, -y0 * E; Z0, Z1 = (GZ + z0) * E, (GZ + z1) * E
    f = [[(X0, Y0, Z0), (X1, Y0, Z0), (X1, Y0, Z1), (X0, Y0, Z1)], [(X1, Y1, Z0), (X0, Y1, Z0), (X0, Y1, Z1), (X1, Y1, Z1)],
         [(X0, Y1, Z0), (X0, Y0, Z0), (X0, Y0, Z1), (X0, Y1, Z1)], [(X1, Y0, Z0), (X1, Y1, Z0), (X1, Y1, Z1), (X1, Y0, Z1)],
         [(X0, Y0, Z1), (X1, Y0, Z1), (X1, Y1, Z1), (X0, Y1, Z1)]]
    for k, ff in enumerate(f):
        BV.append(ff); BC.append([cor_topo if (k == 4 and cor_topo is not None) else cor] * 4); BA.append(ALFA[0])
def quadv(pts, cor):
    BV.append([(x * E, -y * E, (GZ + z) * E) for x, y, z in pts]); BC.append([cor] * 4 if np.ndim(cor[0]) == 0 else cor); BA.append(ALFA[0])
def caixa_girada(cx, cy, z0, comp, larg, alt, ang, cor):
    ux, uy = math.cos(ang), math.sin(ang); vx, vy = -uy, ux
    p = [(cx + ux * s * comp / 2 + vx * t * larg / 2, cy + uy * s * comp / 2 + vy * t * larg / 2) for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for k in range(4):
        (xa, ya), (xb, yb) = p[k], p[(k + 1) % 4]
        quadv([(xa, ya, z0), (xb, yb, z0), (xb, yb, z0 + alt), (xa, ya, z0 + alt)], cor)
    quadv([(x, y, z0 + alt) for x, y in p], cor)
def cilindro(cx, cy, z0, r, alt, cor, lados=12, deitado=None):
    for k in range(lados):
        a0, a1 = 2 * math.pi * k / lados, 2 * math.pi * (k + 1) / lados
        if deitado is None:
            quadv([(cx + r * math.cos(a0), cy + r * math.sin(a0), z0), (cx + r * math.cos(a1), cy + r * math.sin(a1), z0),
                   (cx + r * math.cos(a1), cy + r * math.sin(a1), z0 + alt), (cx + r * math.cos(a0), cy + r * math.sin(a0), z0 + alt)], cor)
        else:   # eixo em x (comprimento alt)
            quadv([(cx, cy + r * math.cos(a0), z0 + r + r * math.sin(a0)), (cx + alt, cy + r * math.cos(a0), z0 + r + r * math.sin(a0)),
                   (cx + alt, cy + r * math.cos(a1), z0 + r + r * math.sin(a1)), (cx, cy + r * math.cos(a1), z0 + r + r * math.sin(a1))], cor)
    if deitado is None:
        for k in range(lados):
            a0, a1 = 2 * math.pi * k / lados, 2 * math.pi * (k + 1) / lados
            quadv([(cx, cy, z0 + alt), (cx + r * math.cos(a0), cy + r * math.sin(a0), z0 + alt), (cx + r * math.cos(a1), cy + r * math.sin(a1), z0 + alt), (cx, cy, z0 + alt)], [c * 0.9 for c in cor])

REBF_ = [0xF2C2AE, 0xF2D488, 0xAFCFDC, 0xE8A970, 0xF2EDE2, 0xCADBA8, 0xE7B7C4]
def tom(c, f): return np.clip(np.asarray(c) * f, 0, 1)
CINZA = [0x9C8C72, 0x958569, 0xA29277, 0x8F8066]
PEDRA = [0xB7AE9E, 0xA9A08F, 0xC2B8A6, 0x9E978B]
TIJ = [0xB0563C, 0xA24E36, 0xBC6044, 0x9A4A33]
def cor_srgb(h): return np.array([((h >> 16) & 255) / 255, ((h >> 8) & 255) / 255, (h & 255) / 255], np.float32)

def torre_faixas(cx, cy, lado, z0, z1, pal, blocos=6.5, banda=3.6):
    h0 = lado / 2
    cantos = [(cx - h0, cy + h0), (cx + h0, cy + h0), (cx + h0, cy - h0), (cx - h0, cy - h0)]
    nb = int(math.ceil((z1 - z0) / banda))
    for f in range(4):
        (xa, ya), (xb, yb) = cantos[f], cantos[(f + 1) % 4]
        L = math.hypot(xb - xa, yb - ya); nk = max(1, int(round(L / blocos)))
        for b in range(nb):
            za, zb = z0 + b * banda, min(z1, z0 + (b + 1) * banda)
            off = 0.5 if b % 2 else 0.0
            cuts = sorted(set([0.0, 1.0] + [min(1, max(0, (k + off) / nk)) for k in range(nk + 1)]))
            for k in range(len(cuts) - 1):
                t0, t1 = cuts[k], cuts[k + 1]
                if t1 - t0 < 1e-4: continue
                hsh = (int(cx) * 31 + f * 7 + b * 13 + k * 17) % 97
                c = tom(cor_srgb(pal[hsh % 4]), 0.95 + 0.08 * (hsh % 11) / 10) if pal is TIJ else cor_srgb(0x9C8C72)
                quadv([(xa + (xb - xa) * t0, ya + (yb - ya) * t0, za), (xa + (xb - xa) * t1, ya + (yb - ya) * t1, za),
                       (xa + (xb - xa) * t1, ya + (yb - ya) * t1, zb), (xa + (xb - xa) * t0, ya + (yb - ya) * t0, zb)], c)
    return cantos
def ameias(cx, cy, lado, z, pal, alt=5.0, passo=5.0):
    h0 = lado / 2 + 0.6
    cantos = [(cx - h0, cy + h0), (cx + h0, cy + h0), (cx + h0, cy - h0), (cx - h0, cy - h0)]
    c = tom(cor_srgb(pal[0]), 1.05)
    caixa(cx - h0, cy - h0, z - 1.5, cx + h0, cy + h0, z, c)                 # cimalha
    for f in range(4):
        (xa, ya), (xb, yb) = cantos[f], cantos[(f + 1) % 4]
        L = math.hypot(xb - xa, yb - ya); nk = int(L // passo)
        for k in range(0, nk, 2):
            t0, t1 = k / nk, (k + 1) / nk
            xa_, ya_ = xa + (xb - xa) * t0, ya + (yb - ya) * t0; xb_, yb_ = xa + (xb - xa) * t1, ya + (yb - ya) * t1
            caixa(min(xa_, xb_) - 1.2, min(ya_, yb_) - 1.2, z, max(xa_, xb_) + 1.2, max(ya_, yb_) + 1.2, z + alt, c)

ESCURO = cor_srgb(0x2A221C)
for (cx, cy, lado, z0, z1, tipo) in P['torres']:
    tipo = int(tipo)
    ALFA[0] = [0.0, 0.5, 1.0][tipo]
    if tipo == 0:        # castelo: torre quadrada larga com ameias
        torre_faixas(cx, cy, lado, z0, z1, CINZA)
        caixa(cx - lado / 2 + 0.3, cy - lado / 2 + 0.3, z1 - 0.1, cx + lado / 2 - 0.3, cy + lado / 2 - 0.3, z1, cor_srgb(0x8E8A84))
        ameias(cx, cy, lado, z1, CINZA)
        for f in range(4):   # seteiras
            for zz in (z1 - 22, z1 - 40):
                if zz < 10: continue
                dx, dy = [(0, lado / 2 + 0.2), (lado / 2 + 0.2, 0), (0, -lado / 2 - 0.2), (-lado / 2 - 0.2, 0)][f]
                if dx == 0: caixa(cx - 0.8, cy + dy - 0.1, zz, cx + 0.8, cy + dy + 0.1, zz + 7, ESCURO)
                else: caixa(cx + dx - 0.1, cy - 0.8, zz, cx + dx + 0.1, cy + 0.8, zz + 7, ESCURO)
    elif tipo == 1:      # torre medieval esbelta (San Gimignano)
        torre_faixas(cx, cy, lado, z0, z1, PEDRA)
        caixa(cx - lado / 2 - 0.8, cy - lado / 2 - 0.8, z1, cx + lado / 2 + 0.8, cy + lado / 2 + 0.8, z1 + 1.5, cor_srgb(0xC9BFAE))
        ameias(cx, cy, lado, z1 + 1.5, PEDRA, 3.5, 3.5)
        for zz in np.arange(z1 - 14, z1 - 60, -16):
            caixa(cx - 1.0, cy + lado / 2, zz, cx + 1.0, cy + lado / 2 + 0.25, zz + 6, ESCURO)
            caixa(cx + lado / 2, cy - 1.0, zz, cx + lado / 2 + 0.25, cy + 1.0, zz + 6, ESCURO)
    else:                # campanário de tijolo com sineira e telhado em pirâmide
        torre_faixas(cx, cy, lado, z0, z1, TIJ, 4.0, 2.0)
        for f, (dx, dy) in enumerate([(0, 1), (1, 0), (0, -1), (-1, 0)]):
            for w in (-0.25, 0.25):
                ox, oy = cx + dx * (lado / 2 + 0.15) + dy * w * lado, cy + dy * (lado / 2 + 0.15) + dx * w * lado
                if dx == 0: caixa(ox - 3.5, oy - 0.2, z1 - 22, ox + 3.5, oy + 0.2, z1 - 6, ESCURO)
                else: caixa(ox - 0.2, oy - 3.5, z1 - 22, ox + 0.2, oy + 3.5, z1 - 6, ESCURO)
        caixa(cx - lado / 2 - 1, cy - lado / 2 - 1, z1, cx + lado / 2 + 1, cy + lado / 2 + 1, z1 + 2, cor_srgb(0xE7DCC6))
        h0 = lado / 2 + 1; tp = z1 + 2; ap = (cx, cy, tp + 22)
        cs = [(cx - h0, cy - h0), (cx + h0, cy - h0), (cx + h0, cy + h0), (cx - h0, cy + h0)]
        for k in range(4):
            (xa, ya), (xb, yb) = cs[k], cs[(k + 1) % 4]
            quadv([(xa, ya, tp), (xb, yb, tp), ap, ap], tom(cor_srgb(0xC4562E), 0.9 + 0.1 * (k % 2)))

# ---------------------------------------------------------------- arcos sobre a rua (pedra, aduelas alternadas)
ALFA[0] = 1.0
for (x0, y0, x1, y1, fz, zt, tipo) in P['arcos']:
    tipo = int(tipo)
    ALFA[0] = 0.0 if tipo == 0 else 0.5
    u = np.array([x1 - x0, y1 - y0]); L = float(np.linalg.norm(u)); u /= L; v = np.array([-u[1], u[0]])
    ext = 5.0; esp = 7.0 if tipo else 10.0
    mola = 24.0; flecha = max(8.0, min(L / 2, zt - fz - mola - 8))
    topo_ = max(zt, fz + mola + flecha + 8) + (2 if tipo == 0 else 0)
    pal = CINZA if tipo == 0 else PEDRA
    nseg = 22
    def pnt(s, side, z):
        p = np.array([x0, y0]) + u * s + v * side
        return (float(p[0]), float(p[1]), float(z))
    def intr(s):
        if s <= 0 or s >= L: return fz + mola
        t = 2 * s / L - 1
        return fz + mola + flecha * math.sqrt(max(0, 1 - t * t))
    ss = [-ext] + [L * k / nseg for k in range(nseg + 1)] + [L + ext]
    for k in range(len(ss) - 1):
        sa, sb = ss[k], ss[k + 1]
        za, zb = intr(sa), intr(sb)
        c = tom(cor_srgb(pal[k % 4]), 1.0 if k % 2 else 0.86)
        cf = tom(cor_srgb(pal[(k + 1) % 4]), 0.95)
        for side in (-esp / 2, esp / 2):
            quadv([pnt(sa, side, za), pnt(sb, side, zb), pnt(sb, side, zb + 6), pnt(sa, side, za + 6)], c)        # aduelas
            quadv([pnt(sa, side, za + 6), pnt(sb, side, zb + 6), pnt(sb, side, topo_), pnt(sa, side, topo_)], cf)
        quadv([pnt(sa, -esp / 2, za), pnt(sb, -esp / 2, zb), pnt(sb, esp / 2, zb), pnt(sa, esp / 2, za)], tom(c, 0.8))   # intradorso
        quadv([pnt(sa, -esp / 2, topo_), pnt(sb, -esp / 2, topo_), pnt(sb, esp / 2, topo_), pnt(sa, esp / 2, topo_)], cf)
    if tipo == 0:   # portão do castelo com ameias
        for s in np.arange(0, L, 9.0):
            c0 = pnt(s, -esp / 2 - 0.6, 0); c1 = pnt(min(L, s + 4.5), esp / 2 + 0.6, 0)
            caixa(min(c0[0], c1[0]), min(c0[1], c1[1]), topo_, max(c0[0], c1[0]), max(c0[1], c1[1]), topo_ + 5, tom(cor_srgb(CINZA[0]), 1.05))

ALFA[0] = 1.0
# ---------------------------------------------------------------- igreja: frontão romano com colunas viradas pro sagrado
FY = 8 * CEL + 0.7     # fachada sul (y do jogo)
IX0, IX1 = 44.6 * CEL, 53.4 * CEL
TRV = cor_srgb(0xEDE3CE)
zf = 18.0
nc = 6
for k in range(nc):
    cx = IX0 + 14 + (IX1 - IX0 - 28) * k / (nc - 1)
    cilindro(cx, FY + 2.6, zf + 2, 2.3, 44, TRV, 14)
    caixa(cx - 3, FY + 0.2, zf, cx + 3, FY + 5.2, zf + 2, tom(TRV, 0.92))
    caixa(cx - 3, FY + 0.2, zf + 46, cx + 3, FY + 5.2, zf + 48, tom(TRV, 0.92))
caixa(IX0 + 6, FY, zf + 48, IX1 - 6, FY + 5.6, zf + 56, tom(TRV, 0.97))        # entablamento
caixa((IX0 + IX1) / 2 - 9, FY - 0.1, zf, (IX0 + IX1) / 2 + 9, FY + 0.3, zf + 30, tom(cor_srgb(0x5A3A24), 1.0))   # portal
cilindro((IX0 + IX1) / 2, FY + 0.35, zf + 66, 6, 0.4, cor_srgb(0x3A3028), 16)   # (rosácea achatada)

# ---------------------------------------------------------------- mercado (base TR): barracas com toldo listrado, varal de bandeirolas, caixotes
TOLDO = [(0xC23B2E, 0xF1E6CF), (0x3E7D4F, 0xF1E6CF), (0x2F5C8A, 0xF1E6CF), (0xD99A2B, 0xF1E6CF), (0xB0306A, 0xF1E6CF), (0xC23B2E, 0xE8C440)]
FRUTA = [0xE0642A, 0x7FB03A, 0xC8202A, 0xE8C440, 0x8A3A6A, 0xD98A2E]
MAD = cor_srgb(0x7A5034)
def barraca(cx, cy, ang, k, z=0.0):
    ux, uy = math.cos(ang), math.sin(ang); vx, vy = -uy, ux     # u ao longo da parede, v pra frente (pra praça)
    comp, prof = 20.0, 7.0
    caixa_girada(cx, cy, z, comp, prof, 7.5, ang, MAD)
    for a_ in range(-3, 4):
        for b_ in (-1, 1):
            ox, oy = cx + ux * a_ * 2.7 + vx * b_ * 1.6, cy + uy * a_ * 2.7 + vy * b_ * 1.6
            caixa_girada(ox, oy, z + 7.5, 2.4, 3.0, 1.8, ang, tom(cor_srgb(FRUTA[(a_ + b_ * 2 + k) % 6]), 1.0))
    for a_ in (-2, 0, 2):                     # cestos no chão na frente
        ox, oy = cx + ux * a_ * 4 + vx * (prof / 2 + 2.2), cy + uy * a_ * 4 + vy * (prof / 2 + 2.2)
        cilindro(ox, oy, z, 1.9, 2.6, cor_srgb(0xA0784A), 10); cilindro(ox, oy, z + 2.6, 1.6, 0.7, cor_srgb(FRUTA[(a_ + k + 3) % 6]), 10)
    for s_ in (-1, 1):
        for t_ in (-1, 1):
            ox, oy = cx + ux * s_ * (comp / 2 - 0.5) + vx * t_ * (prof / 2 + 1.0), cy + uy * s_ * (comp / 2 - 0.5) + vy * t_ * (prof / 2 + 1.0)
            caixa(ox - 0.5, oy - 0.5, z, ox + 0.5, oy + 0.5, z + 25 - 4 * (t_ > 0), MAD)
    c1, c2 = TOLDO[k % len(TOLDO)]; nlist = 10
    fr, tr_ = -(prof / 2 + 2), prof / 2 + 6.5
    for m in range(nlist):
        s0, s1 = -comp / 2 - 1.5 + (comp + 3) * m / nlist, -comp / 2 - 1.5 + (comp + 3) * (m + 1) / nlist
        pts = [(cx + ux * s0 + vx * fr, cy + uy * s0 + vy * fr, z + 27), (cx + ux * s1 + vx * fr, cy + uy * s1 + vy * fr, z + 27),
               (cx + ux * s1 + vx * tr_, cy + uy * s1 + vy * tr_, z + 21), (cx + ux * s0 + vx * tr_, cy + uy * s0 + vy * tr_, z + 21)]
        quadv(pts, cor_srgb(c1 if m % 2 == 0 else c2))
        quadv([(cx + ux * s0 + vx * tr_, cy + uy * s0 + vy * tr_, z + 21), (cx + ux * s1 + vx * tr_, cy + uy * s1 + vy * tr_, z + 21),
               (cx + ux * s1 + vx * tr_, cy + uy * s1 + vy * tr_, z + 18.5), (cx + ux * s0 + vx * tr_, cy + uy * s0 + vy * tr_, z + 18.5)],
              cor_srgb(c1 if m % 2 == 0 else c2))      # babado do toldo
def monte(cx, cy, ang, h, z=0.0):
    ux, uy = math.cos(ang), math.sin(ang); vx, vy = -uy, ux
    for kk, (a_, b_, dz) in enumerate(((-6, 0, 0), (0, 0, 0), (-3, 0, 6.3), (6, 1, 0))):
        ox, oy = cx + ux * a_ + vx * b_, cy + uy * a_ + vy * b_
        caixa_girada(ox, oy, z + dz, 5.6, 5.6, 6.0, ang, tom(cor_srgb(0xB9875A), 0.88 + 0.06 * kk))
        caixa_girada(ox, oy, z + dz + 6.0, 4.6, 4.6, 0.8, ang, cor_srgb(FRUTA[(kk + h) % 6]))
    for q, (a_, b_) in enumerate(((10, 2), (13.5, 1), (11.5, 4.5))):
        cilindro(cx + ux * a_ + vx * b_, cy + uy * a_ + vy * b_, z, 2.0, 4.5, cor_srgb(0xD8C69A) if q % 2 else cor_srgb(0xC8B48A), 10)
    cilindro(cx - ux * 12 + vx * 1, cy - uy * 12 + vy * 1, z, 2.7, 7.5, cor_srgb(0x7A4A2A), 12)
def guarda_sol(cx, cy, h, z=0.0):
    caixa(cx - 4, cy - 3, z, cx + 4, cy + 3, z + 7, MAD, cor_srgb(0xE9DCC0))
    for q in range(-1, 2):
        cilindro(cx + q * 2.4, cy, z + 7, 1.0, 1.2, cor_srgb(FRUTA[(q + h) % 6]), 8)
    caixa(cx - 0.4, cy - 0.4, z, cx + 0.4, cy + 0.4, z + 26, MAD)
    c1, c2 = TOLDO[h % len(TOLDO)]; R = 11; n_ = 12
    for m in range(n_):
        a0, a1 = 2 * math.pi * m / n_, 2 * math.pi * (m + 1) / n_
        quadv([(cx, cy, z + 29), (cx + R * math.cos(a0), cy + R * math.sin(a0), z + 23), (cx + R * math.cos(a1), cy + R * math.sin(a1), z + 23), (cx, cy, z + 29)],
              cor_srgb(c1 if m % 2 == 0 else c2))
MX0, MX1, MY0, MY1 = 25 * CEL, 39 * CEL, 40 * CEL, 46.5 * CEL
k = 0
B1m_ = P['B1']; TRV_ = cor_srgb(0xE6DBC4)
x = MX0
while x < MX1:                      # loggia (pórtico de colunas com telhado) onde a parede norte é contínua
    if B1m_[int(MY0) - 4, int(x)]:
        x0 = x
        while x < MX1 and B1m_[int(MY0) - 4, int(x)]: x += 1
        x1 = x
        if x1 - x0 > 50:
            nco = int((x1 - x0 - 6) // 22)
            for c_ in range(nco + 1):
                xc = x0 + 4 + (x1 - x0 - 8) * c_ / max(nco, 1)
                cilindro(xc, MY0 + 13, 0, 1.6, 23, TRV_, 12); caixa(xc - 2.2, MY0 + 10.8, 0, xc + 2.2, MY0 + 15.2, 1.2, TRV_)
            caixa(x0, MY0 + 11.2, 23, x1, MY0 + 14.8, 26, tom(TRV_, 0.95))
            for m in range(int((x1 - x0) // 3)):
                xa, xb = x0 + m * 3, min(x1, x0 + (m + 1) * 3)
                quadv([(xa, MY0, 33), (xb, MY0, 33), (xb, MY0 + 16, 26), (xa, MY0 + 16, 26)], tom(cor_srgb(0xC25A34), 1.0 if m % 2 else 0.93))
            for xm in np.arange(x0 + 14, x1 - 10, 26):      # bancas debaixo da loggia
                caixa(xm - 7, MY0 + 2, 0, xm + 7, MY0 + 7, 7, MAD, cor_srgb(0x6E4A2E))
                for q in range(-2, 3):
                    cilindro(xm + q * 2.7, MY0 + 4.5, 7, 1.2, 1.4, cor_srgb(FRUTA[(q + int(xm)) % 6]), 8)
    else:
        x += 1
nb_ = 0
for (x, y, nx_, ny_, fz, h) in P['slots']:
    h = int(h); ang = math.atan2(-nx_, ny_); tp_ = h % 6
    if tp_ <= 3 or tp_ == 5:
        barraca(x + nx_ * 1.0, y + ny_ * 1.0, ang, h % 6, fz); nb_ += 1
        c1, c2 = TOLDO[(h >> 3) % len(TOLDO)]; tx_, ty_ = -ny_, nx_      # tenda grande da parede por cima da barraca
        for m in range(8):
            s0, s1 = -13 + 26 * m / 8, -13 + 26 * (m + 1) / 8
            quadv([(x - nx_ * 5 + tx_ * s0, y - ny_ * 5 + ty_ * s0, fz + 38), (x - nx_ * 5 + tx_ * s1, y - ny_ * 5 + ty_ * s1, fz + 38),
                   (x + nx_ * 20 + tx_ * s1, y + ny_ * 20 + ty_ * s1, fz + 31), (x + nx_ * 20 + tx_ * s0, y + ny_ * 20 + ty_ * s0, fz + 31)],
                  cor_srgb(c1 if m % 2 == 0 else c2))
    elif tp_ == 4:
        monte(x, y, ang, h, fz)
    else:
        guarda_sol(x + nx_ * 6, y + ny_ * 6, h, fz)
# fileira de barracas no meio do mercado (costas com costas), com corredor largo dos dois lados
Hm_ = P['H']; ymid = (MY0 + MY1) / 2; kk = 0
for xm in np.arange(MX0 + 80, MX1 - 60, 46):
    fzm = float(Hm_[int(ymid), int(xm)])
    barraca(xm, ymid - 4.5, 0.0, kk, fzm); barraca(xm, ymid + 4.5, math.pi, kk + 2, fzm); kk += 1; nb_ += 2
    if kk % 2 == 0: monte(xm + 23, ymid, 0.0, kk, fzm)
print("barracas", nb_, "de", len(P['slots']))
BAND = [0xC23B2E, 0xE8C440, 0x3E7D4F, 0xF1E6CF, 0x2F5C8A]
for x in (MX0 + 60, MX0 + 150, MX0 + 250, MX1 - 25):
    for (ya, yb) in ((MY0 + 1, MY1 - 1),):
        n_ = int((yb - ya) // 6)
        for m in range(n_):
            y_ = ya + (yb - ya) * (m + 0.5) / n_; t = (y_ - ya) / (yb - ya)
            z = 36 - 9 * 4 * t * (1 - t)
            quadv([(x - 0.05, y_ - 2, z), (x + 0.05, y_ + 2, z), (x, y_, z - 4.5), (x, y_, z - 4.5)], cor_srgb(BAND[(m + int(x)) % 5]))
# ---------------------------------------------------------------- coberturas com forma (antes eram caixas lisas)
for cb in d['coberturas']:
    x0, x1, y0, y1 = cb['x0'] * CEL, cb['x1'] * CEL, cb['y0'] * CEL, cb['y1'] * CEL
    z = cb['piso']; h = cb['h']; t = cb['tipo']; cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if t == 'barrels':
        r = min(x1 - x0, y1 - y0) / 2 * 0.95
        nb = max(1, int(round(max(x1 - x0, y1 - y0) / (2 * r))))
        for m in range(nb):
            if (x1 - x0) > (y1 - y0): bx, by = x0 + (m + 0.5) * (x1 - x0) / nb, cy
            else: bx, by = cx, y0 + (m + 0.5) * (y1 - y0) / nb
            cilindro(bx, by, z, r, h, cor_srgb(0x7A4A2A), 12)
            cilindro(bx, by, z + h * 0.25, r + 0.15, 1.0, cor_srgb(0x4A4A4A), 12); cilindro(bx, by, z + h * 0.72, r + 0.15, 1.0, cor_srgb(0x4A4A4A), 12)
    elif t in ('fountain', 'well'):
        r = min(x1 - x0, y1 - y0) / 2
        cilindro(cx, cy, z, r, h * 0.55 if t == 'fountain' else h * 0.7, cor_srgb(0xD8D2C4), 18)
        cilindro(cx, cy, z + 0.2, r - 2.2, h * 0.5 if t == 'fountain' else h * 0.65, cor_srgb(0x5E86A0) if t == 'fountain' else ESCURO, 18)
        if t == 'fountain':
            cilindro(cx, cy, z, 2.2, h + 8, cor_srgb(0xE7DCC6), 10); cilindro(cx, cy, z + h + 4, 5, 1.2, cor_srgb(0xE7DCC6), 14)
        else:
            for s in (-1, 1): caixa(cx + s * (r - 1) - 0.6, cy - 0.6, z, cx + s * (r - 1) + 0.6, cy + 0.6, z + h + 14, MAD)
            caixa(cx - r, cy - r * 0.6, z + h + 14, cx + r, cy + r * 0.6, z + h + 15.5, cor_srgb(0xA6452A))
    elif t == 'planter':
        caixa(x0, y0, z, x1, y1, z + h * 0.6, cor_srgb(0xC4683C)); caixa(x0 + 0.6, y0 + 0.6, z + h * 0.6, x1 - 0.6, y1 - 0.6, z + h, cor_srgb(0x4E7A34))
    elif t == 'halfwall':
        caixa(x0, y0, z, x1, y1, z + h, cor_srgb(0xC2B8A6), cor_srgb(0xE7DCC6))
    elif t == 'logs':
        for m in range(3):
            cilindro(x0, y0 + 2 + m * 3.6, z, 1.8, x1 - x0, cor_srgb(0x8A5A34), 8, deitado=True)
        for m in range(2):
            cilindro(x0, y0 + 3.8 + m * 3.6, z + 3.2, 1.8, x1 - x0, cor_srgb(0x7A4E2C), 8, deitado=True)
    elif t == 'ape':
        caixa(x0, y0, z + 2, x1 - 12, y1, z + h, cor_srgb(0x7FA9C9)); caixa(x1 - 12, y0, z + 2, x1, y1, z + h * 0.6, cor_srgb(0x6E655C))
        for wx in (x0 + 4, x1 - 6):
            cilindro(wx, y0, z, 2, 1, ESCURO, 8); cilindro(wx, y1, z, 2, 1, ESCURO, 8)
    elif t == 'cart':
        caixa(x0, y0, z + 5, x1, y1, z + h, cor_srgb(0x9A6A42), cor_srgb(0x6E4A2E))
        for wy in (y0, y1): cilindro(cx, wy, z, 5, 0.8, cor_srgb(0x5A3A24), 10)
    elif t == 'press':
        caixa(x0, y0, z, x1, y1, z + h * 0.5, cor_srgb(0x8A5A34)); cilindro(cx, cy, z + h * 0.5, min(x1 - x0, y1 - y0) / 2 - 2, h * 0.5, cor_srgb(0x6E4A2E), 12)
        caixa(cx - 1, y0, z + h * 0.5, cx + 1, y1, z + h + 10, cor_srgb(0x5A3A24)); caixa(x0, cy - 1, z + h + 8, x1, cy + 1, z + h + 10, cor_srgb(0x5A3A24))
    else:   # crate
        caixa(x0, y0, z, x1, y1, z + h * 0.55, cor_srgb(0xB9875A), cor_srgb(0x9A6A42))
        caixa(x0 + 2, y0 + 2, z + h * 0.55, x1 - 2, y1 - 2, z + h, cor_srgb(0xC9976A), cor_srgb(0x9A6A42))

# ---------------------------------------------------------------- peças do pack: janelas, portas, grades, trepadeiras, chaminés, carroça
CACHE = {}
def tingir(mat, cor, forca=1.0, plano=0.0):
    mat = mat.copy(); nt = mat.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    e = b.inputs['Base Color']
    if e.is_linked:
        src = e.links[0].from_socket
        mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = forca
        nt.links.new(src, mul.inputs[6]); mul.inputs[7].default_value = (*cor, 1)
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.inputs['Factor'].default_value = plano
        nt.links.new(mul.outputs[2], mix.inputs[6]); mix.inputs[7].default_value = (*cor, 1)
        nt.links.new(mix.outputs[2], e)
    else:
        e.default_value = (*cor, 1)
    return mat
TINTA = {"MI_WoodTrim_Wear": (tuple(lin((0.36, 0.62, 0.46))), 1.0, 0.6), "MI_WoodTrim": (tuple(lin((0.85, 0.66, 0.48))), 1.0, 0.0),
         "MI_RockTrim": (tuple(lin((0.98, 0.95, 0.88))), 0.4, 0.0), "MI_Brick": (tuple(lin((0.95, 0.9, 0.82))), 0.5, 0.0)}
MATS = {}
def carrega(nome):
    if nome in CACHE: return CACHE[nome]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(PACK, nome + ".gltf"))
    novos = [o for o in bpy.data.objects if o not in antes]
    for o in novos:
        if o.type == 'MESH':
            for s in o.material_slots:
                if s.material:
                    k = s.material.name.split('.')[0]
                    if k in TINTA:
                        if k not in MATS: MATS[k] = tingir(s.material, *TINTA[k])
                        s.material = MATS[k]
    malhas = [o for o in novos if o.type == 'MESH']
    for o in malhas:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    for o in malhas: o.select_set(True)
    bpy.context.view_layer.objects.active = malhas[0]
    if len(malhas) > 1: bpy.ops.object.join()
    base = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in novos:
        if o != base and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
    base.hide_render = True; base.hide_viewport = True
    CACHE[nome] = base
    return base
METRO = 12.5 * E
def poe(nome, pos, normal_xy, esc=(1, 1, 1), giro=0.0):
    b = carrega(nome)
    o = bpy.data.objects.new(nome, b.data); cena.collection.objects.link(o)
    ang = math.atan2(normal_xy[1], normal_xy[0])
    o.rotation_euler = (0, 0, ang + math.radians(90) + math.radians(giro))
    o.location = pos; o.scale = (esc[0] * METRO, esc[1] * METRO, esc[2] * METRO)
    return o

ZM = d['zonas'].index('Mercato')
PRACAS = [d['zonas'].index(n) for n in ('Piazzetta', 'Belvedere', 'Piazza della Chiesa', 'Corte', 'Cantina', 'Via del forno')]
B1m = P['B1']
VERDE, TERRA = cor_srgb(0x4E7A34), cor_srgb(0xB4683E)
FLOR = [0xD8384A, 0xE87AA0, 0xF2E6E0, 0xE8C440]
nj = 0; varais = []
def raio(x, y, nx_, ny_, maxd=95):
    for t in range(3, maxd):
        xi, yi = int(x + nx_ * t), int(y + ny_ * t)
        if not (0 <= xi < B1m.shape[1] and 0 <= yi < B1m.shape[0]): return None
        if B1m[yi, xi]: return t
    return None
for (cx, cy, nx_, ny_, fz, Hh, zon, cast, ped, igr, h) in P['vagas']:
    h = int(h)
    if igr or Hh < 26: continue
    tx, ty = -ny_, nx_
    nxb, nyb = nx_, -ny_
    pe = Vector((cx * E, -cy * E, (GZ + fz) * E))
    if cast:                    # castelo: seteiras
        for zz in (fz + 16, fz + 38):
            if zz + 7 > fz + Hh - 6: continue
            caixa_girada(cx, cy, zz, 1.6, 0.5, 7, math.atan2(ty, tx), ESCURO)
        continue
    if int(zon) == ZM:
        poe("DoorFrame_Round_Brick", pe, (nxb, nyb)); poe("Door_2_Round", pe, (nxb, nyb))
        if cy > MY0 + 6:        # a parede norte tem a loggia
            c1, c2 = TOLDO[h % 4]
            for m in range(7):
                s0, s1 = -10 + 20 * m / 7, -10 + 20 * (m + 1) / 7
                quadv([(cx + tx * s0, cy + ty * s0, fz + 30), (cx + tx * s1, cy + ty * s1, fz + 30),
                       (cx + tx * s1 + nx_ * 10, cy + ty * s1 + ny_ * 10, fz + 25), (cx + tx * s0 + nx_ * 10, cy + ty * s0 + ny_ * 10, fz + 25)], cor_srgb(c1 if m % 2 == 0 else c2))
        for s in (-7, 7):
            bx, by = cx + tx * s + nx_ * 3, cy + ty * s + ny_ * 3
            cilindro(bx, by, fz, 2.4, 3.2, cor_srgb(0xA0784A), 10); cilindro(bx, by, fz + 3.2, 2.0, 0.8, cor_srgb(FRUTA[(h + s) % 6]), 10)
        continue
    porta = (h % 100) < 38
    linhas = []
    if porta:
        poe("DoorFrame_Round_Brick", pe, (nxb, nyb)); poe("Door_1_Round" if h & 1 else "Door_2_Round", pe, (nxb, nyb))
        for s in (-9, 9):       # vasos na porta
            bx, by = cx + tx * s + nx_ * 3, cy + ty * s + ny_ * 3
            cilindro(bx, by, fz, 1.8, 3.6, TERRA, 10); cilindro(bx, by, fz + 3.6, 2.3, 3.2, tom(VERDE, 0.9 + 0.2 * ((h >> 3) & 1)), 10)
        lx, ly = cx + tx * 7 + nx_ * 1.2, cy + ty * 7 + ny_ * 1.2          # lanterna
        caixa(lx - 0.7, ly - 0.7, fz + 25, lx + 0.7, ly + 0.7, fz + 28, cor_srgb(0x2A2622), cor_srgb(0xF2C86A))
        if Hh >= 44: linhas = [24.0]
    else:
        linhas = [0.0] + ([24.0] if Hh >= 50 else [])
    larga = ((h >> 4) & 3) == 0
    baixa = 0 if Hh >= 40 else -(40 - Hh) * 0.6
    for li, dz in enumerate(linhas):
        pj = pe + Vector((0, 0, (dz + baixa) * E))
        poe("Window_Wide_Round1" if larga else "Window_Thin_Round1", pj, (nxb, nyb)); nj += 1
        fechada = ((h >> (12 + li)) % 10) < 3
        if not ped:
            poe("WindowShutters_%s_Round_%s" % ("Wide" if larga else "Thin", "Closed" if fechada else "Open"), pj, (nxb, nyb))
        if not fechada and ((h >> (16 + li)) % 3) == 0:      # floreira
            zz = fz + dz + baixa + 12.2; w_ = 7.5 if larga else 5
            caixa_girada(cx + nx_ * 1.6, cy + ny_ * 1.6, zz, w_, 2.4, 1.8, math.atan2(ty, tx), TERRA)
            for m in range(5):
                fx, fy = cx + nx_ * 1.6 + tx * (m - 2) * w_ / 5, cy + ny_ * 1.6 + ty * (m - 2) * w_ / 5
                caixa_girada(fx, fy, zz + 1.8, 1.4, 2.0, 1.4, math.atan2(ty, tx), cor_srgb(FLOR[(h + m) % 4]) if m % 2 else VERDE)
    if (h >> 20) % 4 == 0:
        lado = Vector((-nyb, nxb, 0)) * (12 * E)
        poe(["Prop_Vine1", "Prop_Vine4", "Prop_Vine6"][(h >> 24) % 3], pe + lado + Vector((0, 0, 4 * E)), (nxb, nyb), (0.8, 0.8, 0.8))
    if not porta and int(zon) in PRACAS and ((h >> 9) % 4) == 0:      # banco de pedra encostado
        caixa_girada(cx + nx_ * 3, cy + ny_ * 3, fz, 14, 4, 4.2, math.atan2(ty, tx), cor_srgb(0xCFC4B0))
    # varal até a parede da frente (rua estreita)
    if Hh >= 40 and len(varais) < 26 and ((h >> 5) % 3) == 0:
        t = raio(cx, cy, nx_, ny_)
        if t and 24 < t < 90 and all((cx - a_) ** 2 + (cy - b_) ** 2 > 110 ** 2 for a_, b_ in varais):
            varais.append((cx, cy)); z0 = fz + 31
            ex, ey = cx + nx_ * t, cy + ny_ * t
            quadv([(cx, cy, z0), (ex, ey, z0), (ex, ey, z0 + 0.25), (cx, cy, z0 + 0.25)], cor_srgb(0x5A524A))
            nr = int(t // 9)
            for m in range(1, nr):
                if (h >> m) & 1: continue
                px_, py_ = cx + nx_ * t * m / nr, cy + ny_ * t * m / nr
                w_ = 3.2 + 1.2 * ((h >> (m + 3)) & 1)
                quadv([(px_ - nx_ * w_ / 2, py_ - ny_ * w_ / 2, z0 - 5.5), (px_ + nx_ * w_ / 2, py_ + ny_ * w_ / 2, z0 - 5.5),
                       (px_ + nx_ * w_ / 2, py_ + ny_ * w_ / 2, z0), (px_ - nx_ * w_ / 2, py_ - ny_ * w_ / 2, z0)],
                      cor_srgb([0xF4F1EA, 0x7FA9C9, 0xE8C440, 0xD8686A, 0xF4F1EA, 0x9CC08A][(h + m) % 6]))
print("janelas", nj, "varais", len(varais))
# ---------------------------------------------------------------- terraços (vila do sul): caixa d'água, vasos, pérgola, casinha da escada, varal
VASO = cor_srgb(0xB4683E); FOLHA = [cor_srgb(0x5E8A3A), cor_srgb(0x4E7A34), cor_srgb(0x6F9A44)]
nterr = 0
for (cx, cy, w, h, tp, hh, cr) in P["terr"]:
    cr = int(cr)
    hh = int(hh); r_ = np.random.default_rng(hh)
    x0, y0, x1, y1 = cx - w / 2 + 3, cy - h / 2 + 3, cx + w / 2 - 3, cy + h / 2 - 3
    if x1 - x0 < 8 or y1 - y0 < 8: continue
    nterr += 1
    def ponto_():
        return float(r_.uniform(x0 + 2, x1 - 2)), float(r_.uniform(y0 + 2, y1 - 2))
    if r_.random() < 0.35:            # casinha da escada (torrino) com porta
        ww, hh2 = min(12, x1 - x0 - 2), min(10, y1 - y0 - 2)
        bx, by = (x0, y0) if r_.random() < 0.5 else (x1 - ww, y1 - hh2)
        ALFA[0] = 0.75; caixa(bx, by, tp, bx + ww, by + hh2, tp + 12, cor_srgb(REBF_[(cr + 1) % 7]), cor_srgb(0xE9E3D6)); ALFA[0] = 1.0
        caixa(bx + ww / 2 - 2, by - 0.15, tp, bx + ww / 2 + 2, by + 0.2, tp + 8, cor_srgb(0x3E6A8A))
    if r_.random() < 0.4:             # caixa d'água
        tx, ty = ponto_(); cilindro(tx, ty, tp, 3.2, 5.5, cor_srgb(0xD8D4CC), 12)
    for _ in range(int(r_.integers(1, 4))):    # vasos com planta
        tx, ty = ponto_(); cilindro(tx, ty, tp, 1.4, 2.4, VASO, 8); cilindro(tx, ty, tp + 2.4, 2.2, 2.6, FOLHA[int(r_.integers(0, 3))], 8)
    if r_.random() < 0.18 and x1 - x0 > 18 and y1 - y0 > 18:      # pérgola com parreira
        px0, py0, px1, py1 = x0 + 2, y0 + 2, min(x1 - 2, x0 + 22), min(y1 - 2, y0 + 16)
        for (qx, qy) in ((px0, py0), (px1, py0), (px0, py1), (px1, py1)):
            caixa(qx - 0.5, qy - 0.5, tp, qx + 0.5, qy + 0.5, tp + 11, MAD)
        for k in range(int((px1 - px0) // 2.5) + 1):
            xx = px0 + k * 2.5; caixa(xx - 0.3, py0, tp + 11, xx + 0.3, py1, tp + 11.6, MAD)
        for k in range(5):
            tx, ty = float(r_.uniform(px0, px1)), float(r_.uniform(py0, py1))
            caixa(tx - 2.5, ty - 2.5, tp + 11.6, tx + 2.5, ty + 2.5, tp + 12.4, FOLHA[k % 3])
    elif r_.random() < 0.22:          # varal
        ax, ay = x0 + 2, (y0 + y1) / 2; bx_ = x1 - 2
        for qx in (ax, bx_): caixa(qx - 0.3, ay - 0.3, tp, qx + 0.3, ay + 0.3, tp + 9, cor_srgb(0x6A625A))
        n_ = int((bx_ - ax) // 5)
        for m in range(1, n_):
            if r_.random() < 0.3: continue
            xx = ax + (bx_ - ax) * m / n_
            quadv([(xx - 1.8, ay, tp + 4.5), (xx + 1.8, ay, tp + 4.5), (xx + 1.8, ay, tp + 8.8), (xx - 1.8, ay, tp + 8.8)],
                  cor_srgb([0xF4F1EA, 0x7FA9C9, 0xE8C440, 0xD8686A, 0xF4F1EA, 0x9CC08A][(m + hh) % 6]))
print("terraços com coisa", nterr)
# ---------------------------------------------------------------- telhados prontos do pack (Roof_RoundTiles, telha romana)
# o modelo 6x8 tem a cumeeira no eixo y; escala pra pegada da casa (6 m x 8 m nominais) e caimento de ~27°
TELHA_TONS = [(0.80, 0.42, 0.30), (0.74, 0.36, 0.26), (0.86, 0.50, 0.36), (0.70, 0.40, 0.30)]
nt_ = 0
for (cx, cy, w, h, tp, cr, ped) in P['telh']:
    larg, comp = (h, w) if w >= h else (w, h)
    giro = 90.0 if w >= h else 0.0
    b = carrega("Roof_RoundTiles_6x8")
    o = bpy.data.objects.new("telhado", b.data); cena.collection.objects.link(o)
    sobe = 0.46 * larg / 2
    o.location = (cx * E, -cy * E, (GZ + tp) * E)
    o.rotation_euler = (0, 0, math.radians(giro))
    o.scale = (larg * E / 6.0, comp * E / 8.0, sobe * E / 4.89)
    # empenas (triângulo de parede nas pontas da cumeeira)
    ALFA[0] = 0.5 if ped else 0.75
    corp_ = cor_srgb(0xC6B89E) if ped else cor_srgb(REBF_[int(cr) % 4])
    for sgn in (-1, 1):
        if w >= h:
            xe = cx + sgn * w / 2
            quadv([(xe, cy - h / 2, tp), (xe, cy + h / 2, tp), (xe, cy, tp + sobe), (xe, cy, tp + sobe)], corp_)
        else:
            ye = cy + sgn * h / 2
            quadv([(cx - w / 2, ye, tp), (cx + w / 2, ye, tp), (cx, ye, tp + sobe), (cx, ye, tp + sobe)], corp_)
    ALFA[0] = 1.0
    nt_ += 1
print("telhados do pack", nt_)
# ---------------------------------------------------------------- pátios internos: arbustos e flores do pack Quaternius (CC0)
NAT = "D:/unity/CSManager/Assets/Game/Art/Natureza"
def carrega_fbx(nome, cor, alvo):
    antes = set(bpy.data.objects)
    try:
        bpy.ops.import_scene.fbx(filepath=os.path.join(NAT, nome + ".fbx"))
    except Exception:
        bpy.ops.wm.fbx_import(filepath=os.path.join(NAT, nome + ".fbx"))
    novos = [o for o in bpy.data.objects if o not in antes and o.type == 'MESH']
    for o in [o for o in bpy.data.objects if o not in antes and o.type != 'MESH']: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in novos: o.select_set(True)
    bpy.context.view_layer.objects.active = novos[0]
    if len(novos) > 1: bpy.ops.object.join()
    b = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    import mathutils
    cs = [mathutils.Vector(v.co) for v in b.data.vertices]
    mn = Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))); mx = Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
    for v in b.data.vertices: v.co = (v.co - Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))) * (alvo * E / max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z))
    for i_, sl in enumerate(b.material_slots):
        m_ = bpy.data.materials.new(nome + str(i_)); m_.use_nodes = True
        c_ = cor if i_ == 0 or len(b.material_slots) == 1 else [0.85, 0.35, 0.45]
        if sl.material and 'flower' in sl.material.name.lower(): c_ = [0.92, 0.82, 0.86]
        m_.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (*lin(c_), 1); m_.node_tree.nodes["Principled BSDF"].inputs['Roughness'].default_value = 0.9
        sl.material = m_
    b.hide_render = True; b.hide_viewport = True
    return b
try:
    PLANTAS = [carrega_fbx("Bush_Common", [0.30, 0.46, 0.20], 14), carrega_fbx("Bush_Common_Flowers", [0.33, 0.50, 0.22], 13),
               carrega_fbx("Plant_7_Big", [0.28, 0.44, 0.18], 18)]
except Exception as ex:
    print("plantas falharam", ex); PLANTAS = []
rpl = np.random.default_rng(31)
for (x, y, z, dq_) in (P['arv'] if PLANTAS else []):
    b = PLANTAS[int(rpl.integers(0, len(PLANTAS)))]
    o = bpy.data.objects.new("planta", b.data); cena.collection.objects.link(o)
    e_ = float(rpl.uniform(0.8, 1.3)) * min(1.0, dq_ / 8)
    o.location = (x * E, -y * E, (GZ + z) * E); o.rotation_euler = (0, 0, float(rpl.uniform(0, 6.28))); o.scale = (e_, e_, e_)
print("plantas", len(P['arv']))
for (x, y, z) in P['chams']:
    caixa(x - 2.2, y - 2.2, z - 3, x + 2.2, y + 2.2, z + 7, cor_srgb(0xB0563C), cor_srgb(0x3A3028))
    caixa(x - 2.8, y - 2.8, z + 7, x + 2.8, y + 2.8, z + 8.2, cor_srgb(0xC9BFAE))
poe("Prop_Wagon", Vector(((MX1 - 40) * E, -(MY1 - 9) * E, GZ * E)), (0, 1), (1, 1, 1), 90)

# ---------------------------------------------------------------- fundo: castelo atrás do CT (norte), cidade atrás da base TR (sul)
def casa_fundo(x0, y0, x1, y1, z0, z1, cor, telha=0xC25A34):
    ALFA[0] = 0.75; caixa(x0, y0, z0, x1, y1, z1, cor, cor_srgb(0xE9E3D6) if (int(x0 + y0) % 3 == 0) else cor_srgb(0xC98763))
    for (a0, b0, a1, b1) in ((x0, y0, x1, y0 + 1.2), (x0, y1 - 1.2, x1, y1), (x0, y0, x0 + 1.2, y1), (x1 - 1.2, y0, x1, y1)):
        caixa(a0, b0, z1, a1, b1, z1 + 3.5, tom(cor, 1.03))
    ALFA[0] = 1.0
    return
    w, h = x1 - x0, y1 - y0; s = min(w, h) / 2; sub = min(s * 0.5, 18); o = 2.0
    X0, X1, Y0, Y1 = x0 - o, x1 + o, y0 - o, y1 + o
    ct = tom(cor_srgb(telha), 1.0); ct2 = tom(cor_srgb(telha), 0.9)
    if w >= h:
        ra, rb = (x0 + s, (y0 + y1) / 2, z1 + sub), (x1 - s, (y0 + y1) / 2, z1 + sub)
        quadv([(X0, Y0, z1), (X1, Y0, z1), (rb[0], rb[1], rb[2]), (ra[0], ra[1], ra[2])], ct)
        quadv([(X1, Y1, z1), (X0, Y1, z1), (ra[0], ra[1], ra[2]), (rb[0], rb[1], rb[2])], ct2)
        quadv([(X0, Y1, z1), (X0, Y0, z1), ra, ra], ct); quadv([(X1, Y0, z1), (X1, Y1, z1), rb, rb], ct2)
    else:
        ra, rb = ((x0 + x1) / 2, y0 + s, z1 + sub), ((x0 + x1) / 2, y1 - s, z1 + sub)
        quadv([(X0, Y1, z1), (X0, Y0, z1), ra, rb], ct); quadv([(X1, Y0, z1), (X1, Y1, z1), rb, ra], ct2)
        quadv([(X0, Y0, z1), (X1, Y0, z1), ra, ra], ct); quadv([(X1, Y1, z1), (X0, Y1, z1), rb, rb], ct2)
def muro(xa, ya, xb, yb, z0, z1, esp=8.0):
    ALFA[0] = 0.0
    L = math.hypot(xb - xa, yb - ya); ux, uy = (xb - xa) / L, (yb - ya) / L
    cx, cy = (xa + xb) / 2, (ya + yb) / 2
    caixa_girada(cx, cy, z0, L, esp, z1 - z0, math.atan2(uy, ux), tom(cor_srgb(CINZA[0]), 1.0))
    n_ = int(L // 9)
    for k in range(n_):
        px_, py_ = xa + ux * (k + 0.5) * 9, ya + uy * (k + 0.5) * 9
        caixa_girada(px_, py_, z1, 4.5, esp + 1, 5, math.atan2(uy, ux), tom(cor_srgb(CINZA[0]), 1.05))
ZB = -GZ
ALFA[0] = 0.0
# muralha da cidade correndo atrás da borda norte, com torres
muro(-120, -14, NX + 120, -14, ZB, 18 + 40)
for x in np.arange(-100, NX + 120, 260):
    if 600 < x < 1250: continue
    torre_faixas(x, -14, 26, ZB, 18 + 58, CINZA, 9.0, 4.2); ameias(x, -14, 26, 18 + 58, CINZA)
# fortaleza atrás do recinto do castelo: pátio externo, muralha e o mastio (torre de menagem)
CXa, CXb = 30.5 * CEL, 44.5 * CEL
muro(CXa - 40, -14, CXa - 40, -190, ZB, 18 + 52); muro(CXb + 40, -14, CXb + 40, -190, ZB, 18 + 52)
muro(CXa - 40, -190, CXb + 40, -190, ZB, 18 + 52)
for (x, y) in ((CXa - 40, -190), (CXb + 40, -190), (CXa - 40, -14), (CXb + 40, -14)):
    torre_faixas(x, y, 34, ZB, 18 + 74, CINZA, 9.0, 4.2); ameias(x, y, 34, 18 + 74, CINZA)
caixa(CXa - 36, -186, ZB, CXb + 36, -18, 18 + 20, tom(cor_srgb(0x9C968C), 1.0), tom(cor_srgb(0xA29C92), 1.0))   # pátio alto
mx = (CXa + CXb) / 2
torre_faixas(mx, -105, 80, ZB, 18 + 120, CINZA, 9.0, 4.2); ameias(mx, -105, 80, 18 + 120, CINZA, 7, 7)
torre_faixas(mx + 26, -130, 26, 18 + 120, 18 + 150, CINZA, 9.0, 4.2); ameias(mx + 26, -130, 26, 18 + 150, CINZA)
for zz in (60, 85, 110):
    for dx in (-20, 0, 20):
        caixa(mx + dx - 1.2, -105 + 40.1, 18 + zz, mx + dx + 1.2, -105 + 40.4, 18 + zz + 9, ESCURO)
caixa(CXa - 36, -186, ZB, CXb + 36, -18, ZB + 0.1, tom(cor_srgb(0x9C968C), 1.0))
ALFA[0] = 1.0
# cidade atrás da base TR (sul) e nas laterais baixas: quarteirões de casas de telha
rr = np.random.default_rng(21)
REBF = [0xF2C2AE, 0xF2D488, 0xAFCFDC, 0xE8A970, 0xF2EDE2, 0xCADBA8, 0xE7B7C4]
y = NY + 22
while y < NY + 420:
    prof = float(rr.choice([60, 70, 80])); x = -260.0
    while x < NX + 260:
        larg = float(rr.choice([26, 30, 34, 40, 46]))
        for (ya_, yb_) in ((y, y + prof / 2 - 0.5), (y + prof / 2 + 0.5, y + prof)):
            alt = float(rr.choice([34, 40, 46, 52])) + max(0, (y - NY) * 0.05)
            if rr.random() > 0.05:
                casa_fundo(x, ya_, x + larg - 1, yb_, ZB, alt, cor_srgb(int(rr.choice(REBF))))
        x += larg
        if rr.random() < 0.12: x += 24
    y += prof + 30
for (xa, xb) in ((-300, -20), (NX + 20, NX + 300)):
    y = 40.0
    while y < NY + 20:
        prof = float(rr.choice([60, 80, 100])); x = xa
        while x < xb:
            larg = float(rr.choice([28, 34, 40]))
            casa_fundo(x, y, min(xb, x + larg - 1), y + prof - 26, ZB, float(rr.choice([30, 36, 42, 48])), cor_srgb(int(rr.choice(REBF))))
            x += larg + float(rr.choice([0, 0, 0, 20]))
        y += prof

for (x, y, z) in P['parapeito']:
    caixa(x - 1.6, y - 1.6, z, x + 1.6, y + 1.6, z + 9, cor_srgb(0xD3C7B0), cor_srgb(0xE2D8C4))
BV = np.array(BV, np.float32).reshape(-1, 3); BC = np.array(BC, np.float32).reshape(-1, 3)
BA = np.repeat(np.array(BA, np.float32), 4)
malha("enfeites", BV, np.arange(len(BV)), 4, np.c_[BC, BA], 'CORNER')

# chão em volta (campo)
bpy.ops.mesh.primitive_plane_add(size=1, location=(NX * E / 2, -NY * E / 2, 0.0))
pl = bpy.context.object; pl.scale = (NX * E * 4, NY * E * 4, 1)
mpl = bpy.data.materials.new("campo"); mpl.use_nodes = True
mpl.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (*lin((0.56, 0.58, 0.38)), 1); pl.data.materials.append(mpl)

# ---------------------------------------------------------------- luz, céu e câmeras
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(48), math.radians(6), math.radians(-38)))
sol = bpy.context.object.data; sol.energy = 4.0; sol.color = (1.0, 0.93, 0.80); sol.angle = math.radians(1.5)
w = bpy.data.worlds.new("w"); cena.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.70, 0.76, 0.62, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
cena.render.engine = 'BLENDER_EEVEE'
cena.view_settings.view_transform = 'AgX'; cena.view_settings.look = 'AgX - Medium High Contrast'
W, Hm = d['W'], d['H']
CAMS = {
    'topo': ((W / 2 + 26, -Hm / 2 - 34, 62), (W / 2, -Hm / 2, 0), 38, (1800, 1350)),
    'ct': ((45, -19, 17), (37, -4.5, 1.6), 30, (1600, 1000)),        # castello (base CT) + arco
    'tr': ((40, -31, 15), (31.5, -43.5, 0.4), 30, (1600, 1000)),     # mercato (base TR)
    'b': ((22, -30, 20), (9, -12, 0.5), 30, (1600, 1000)),
    'a': ((60, -36, 20), (47, -15, 1), 30, (1600, 1000)),
    'meio': ((44, -42, 18), (30, -24, 1), 28, (1600, 1000)),
}
for v in VISTAS:
    loc, alvo, lente, res = CAMS[v]
    bpy.ops.object.camera_add(location=loc); cam = bpy.context.object; cena.camera = cam; cam.data.lens = lente
    dd = Vector(alvo) - cam.location; cam.rotation_euler = dd.to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_end = 500
    cena.render.resolution_x, cena.render.resolution_y = res
    cena.render.filepath = f"{SAIDA}_{v}.png"
    bpy.ops.render.render(write_still=True)
    print("ok", cena.render.filepath)
