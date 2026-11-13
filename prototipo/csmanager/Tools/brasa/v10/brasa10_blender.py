# -*- coding: utf-8 -*-
# Brasa v10 — prévia do visual no Blender, montada com as PEÇAS dos packs (sem esticar telhado, sem casa-cubo).
#   blender -b --factory-startup --python brasa10_blender.py -- <prefixo de saída> [vistas] [escala da resolução]
# Cidade baixa = mercado (base TR) -> sobe o morro -> cidade alta de pedra = igreja + castelo (base CT).
# 1 casa da planta = 25 unidades do jogo = 2 m = 1 módulo do Medieval Village MegaKit (Quaternius, CC0).
# Também grava <prefixo>_pecas.json: a lista de peças (nome, posição, giro, cor) pro porte da Unity.
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import brasa10 as M

arg = sys.argv[sys.argv.index('--') + 1:]
SAIDA = arg[0]
VISTAS = arg[1].split(',') if len(arg) > 1 else ['topo', 'jogo', 'tr', 'ct', 'trans', 'a', 'b', 'meio']
RES = float(arg[2]) if len(arg) > 2 else 1.0
KIT = "D:/unity/import_packs/medieval_village_megakit/Medieval Village MegaKit[Standard]/glTF"
NAT = "D:/unity/import_packs/stylized_nature_megakit/glTF"
PACKS = "D:/unity/CSManager/Assets/Game/Visual/Resources/Packs"
FP = "D:/unity/import_packs/fantasy_props_megakit/Exports/glTF"       # Fantasy Props MegaKit (Quaternius, CC0)

bpy.ops.wm.read_factory_settings(use_empty=True)
cena = bpy.context.scene
rng = np.random.default_rng(10)
U = 1 / 25.0          # unidade do jogo -> Blender (1 casa = 1)
MT = 0.5              # metro do kit -> Blender
AND = 39.0            # altura de um andar do kit (3,12 m) em unidades do jogo
RXY = 0.9             # telhado do kit 10% menor no plano (beiral mais curto), igual pra todas as casas
ZR = 0.5              # telhado do kit mais baixo (caimento ~36°, telhado italiano), igual pra todas as casas
W, H, S = M.W, M.H, M.S
MG = 7                # margem de fundo em volta do mapa (casas)
EW, EH = W + 2 * MG, H + 2 * MG
PECAS = []            # lista pro porte


def lin(c):
    c = np.asarray(c, np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def hx(h, f=1.0):
    return tuple(float(v) for v in np.clip(lin([((h >> 16) & 255) / 255, ((h >> 8) & 255) / 255, (h & 255) / 255]) * f, 0, 1))


def h32(*a):
    h = 2166136261
    for v in a:
        h = ((h ^ (int(v) & 0xffffffff)) * 16777619) & 0xffffffff
    return h


# ====================================================================================================== grades da planta
PECA_DE = {}          # (cx, cy) -> índice da peça que manda na casa
for k, (zona, letra, poli, z, mat) in enumerate(M.P):
    xs = [p[0] for p in poli]; ys = [p[1] for p in poli]
    for cy in range(int(min(ys)), int(math.ceil(max(ys)))):
        for cx in range(int(min(xs)), int(math.ceil(max(xs)))):
            if M.dentro(poli, cx + .5, cy + .5):
                PECA_DE[(cx, cy)] = k


def zpeca(k, x, y):
    z = M.P[k][3]
    return float(z(x, y)) if callable(z) else float(z)


def letra(cx, cy):
    k = PECA_DE.get((cx, cy))
    return M.P[k][1] if k is not None else '#'


def zona(cx, cy):
    k = PECA_DE.get((cx, cy))
    return M.P[k][0] if k is not None else None


def anda(cx, cy):          # chão térreo
    return letra(cx, cy) not in '#k~'


def zchao(cx, cy):
    k = PECA_DE.get((cx, cy))
    return zpeca(k, cx + .5, cy + .5) if k is not None else None


# chão mais perto de cada casa (pra base dos prédios)
_and = [(cx, cy) for (cx, cy) in PECA_DE if anda(cx, cy)]
_ax = np.array(_and, float); _az = np.array([zchao(cx, cy) for cx, cy in _and])


def zperto(cx, cy):
    d = np.abs(_ax[:, 0] - cx) + np.abs(_ax[:, 1] - cy)
    return float(_az[int(np.argmin(d))])


def dist_rua(cx, cy):
    return float(np.max(np.abs(_ax - np.array([cx, cy])), axis=1).min())


# tipo de cada casa da grade estendida
RUA, CASA, CAST, IGR, PAL, MURA, PATIO, RESTO, FORA = range(9)
K = np.full((EH, EW), FORA, np.int8)
for j in range(EH):
    for i in range(EW):
        cx, cy = i - MG, j - MG
        dentro_mapa = 0 <= cx < W and 0 <= cy < H
        if dentro_mapa and anda(cx, cy):
            K[j, i] = RUA; continue
        if dentro_mapa and letra(cx, cy) in 'k~':
            K[j, i] = PAL; continue
        if (30 <= cx < 44 and 0 <= cy < 9) or (34 <= cx < 40 and -5 <= cy < 0) or (26 <= cx < 30 and 6 <= cy < 8) or (cx in (6, 7, 12, 13) and 20 <= cy < 23) or ((2 <= cx < 6 or 14 <= cx < 18) and cy == 21):
            K[j, i] = CAST
        elif 44 <= cx < 55 and 3 <= cy < 8:
            K[j, i] = IGR
        elif -2 <= cy < 1 and -3 <= cx < W + 3:
            K[j, i] = MURA
        elif cy < -2:
            K[j, i] = FORA
        elif -5 <= cx < W + 5 and cy < H + 5:
            K[j, i] = CASA
# miolo de quadra vira pátio com árvore
for j in range(EH):
    for i in range(EW):
        cx, cy = i - MG, j - MG
        if K[j, i] == CASA and 0 <= cx < W and 0 <= cy < H and dist_rua(cx, cy) >= 5:
            K[j, i] = PATIO

# ====================================================================================================== casas (lotes)
LARG = {2: [2, 3, 4], 3: [3, 4, 5, 6, 7], 4: [4, 5, 6, 7]}
livre = K == CASA
LOTE = np.full((EH, EW), -1, np.int32)
CASAS = []
DIR = {'N': (0, -1), 'S': (0, 1), 'O': (-1, 0), 'L': (1, 0)}


def k_em(i, j):
    return K[j, i] if 0 <= i < EW and 0 <= j < EH else FORA


def fundo(i, j, s):
    """quantas casas livres pra dentro da quadra, a partir da casa (i, j) que tem a rua no lado s."""
    dx, dy = DIR[s]; n = 0
    while n < 6 and 0 <= i - dx * n < EW and 0 <= j - dy * n < EH and livre[j - dy * n, i - dx * n]:
        n += 1
    return n


# 1) fileiras de casas viradas pra rua. Uma fileira é uma coisa só: todas as casas do trecho têm a MESMA profundidade e a
#    cumeeira paralela à rua, então os telhados emendam num pano contínuo (só muda a altura do beiral e a cor), em vez de
#    cada casinha com a cumeeira pra um lado (usuário: "essa casa ficou uma merda" — telhados embolados, buraco no meio).
corridas = []
for s, (dx, dy) in DIR.items():
    marc = np.zeros((EH, EW), bool)
    for j in range(EH):
        for i in range(EW):
            marc[j, i] = livre[j, i] and k_em(i + dx, j + dy) == RUA
    vistos = np.zeros((EH, EW), bool)
    for j in range(EH):
        for i in range(EW):
            if not marc[j, i] or vistos[j, i]: continue
            cel = []; a, b = i, j
            while 0 <= a < EW and 0 <= b < EH and marc[b, a]:
                cel.append((a, b)); vistos[b, a] = True
                a, b = (a + 1, b) if s in 'NS' else (a, b + 1)
            corridas.append((s, cel))
corridas.sort(key=lambda c: -len(c[1]))
LMAX = 7


def prof(v):          # profundidade da fileira pelo fundo livre: nunca deixa tira de 1 casa atrás
    return 0 if v < 2 else 2 if v == 2 else 4 if v == 4 else 3


def reparte(n, d):
    """corta n casas de frente em larguras >= d (cumeeira ao longo da rua) e <= LMAX, sem sobrar 1."""
    out = []
    while n > 0:
        ops = [l for l in range(d, LMAX + 1) if l <= n and (n - l == 0 or n - l >= d)]
        if not ops: out.append(n); break
        ps = np.array([3.0 if 3 <= l <= 5 else 1.0 for l in ops]); l = int(ops[int(rng.choice(len(ops), p=ps / ps.sum()))])
        out.append(l); n -= l
    return out


for s, cel in corridas:
    dx, dy = DIR[s]; Lr = len(cel)
    ds = [prof(fundo(a, b, s)) for a, b in cel]
    # trechos de profundidade igual; trecho mais curto que a própria profundidade afina até caber (mínimo 2)
    p = 0
    while p < Lr:
        if ds[p] == 0:
            p += 1; continue
        fim = p
        while fim < Lr and ds[fim] == ds[p]: fim += 1
        n, d = fim - p, ds[p]
        if n < 2:
            p = fim; continue
        d = min(d, max(2, n))
        for l in reparte(n, d):
            cs = [(a - dx * t, b - dy * t) for (a, b) in cel[p:p + l] for t in range(d)]
            i0, j0 = min(c[0] for c in cs), min(c[1] for c in cs)
            w, h = (l, d) if s in 'NS' else (d, l)
            if livre[j0:j0 + h, i0:i0 + w].all():
                LOTE[j0:j0 + h, i0:i0 + w] = len(CASAS); livre[j0:j0 + h, i0:i0 + w] = False
                CASAS.append(dict(i=i0, j=j0, w=w, h=h, frente=s, cume='x' if s in 'NS' else 'y'))
            p += l
nfila = len(CASAS)
# 2) miolo de quadra e a cidade em volta: sempre o maior retângulo que cabe (2..4 de largura, até LMAX de comprimento), com
#    um pouco de sorte no desempate — telhados grandes e calmos, sem retalho
def maior_ret():
    melhor, mv = None, 0.0
    alt = np.zeros((EH, EW), int)
    for j in range(EH - 1, -1, -1):
        for i in range(EW):
            alt[j, i] = (alt[j + 1, i] + 1 if j + 1 < EH else 1) if livre[j, i] else 0
    for j in range(EH):
        for i in range(EW):
            if not livre[j, i]: continue
            hm = 99
            for w in range(1, LMAX + 1):
                if i + w > EW or not livre[j, i + w - 1]: break
                hm = min(hm, alt[j, i + w - 1])
                if w < 2: continue
                for h in ((min(hm, LMAX),) if w <= 4 else (min(hm, 4),)):
                    if h < 2: continue
                    v = w * h * (0.75 + 0.25 * (h32(i, j, w, h) % 1000) / 1000.0)
                    if v > mv: melhor, mv = (i, j, w, h), v
    return melhor


while True:
    m = maior_ret()
    if m is None: break
    i, j, w, h = (int(v) for v in m)
    LOTE[j:j + h, i:i + w] = len(CASAS); livre[j:j + h, i:i + w] = False
    CASAS.append(dict(i=i, j=j, w=w, h=h))
# 3) tira de 1 casa que sobrou: a casa do lado cresce por cima dela (lado inteiro livre), até não ter mais o que crescer
mudou = True
while mudou:
    mudou = False
    for n, c in enumerate(CASAS):
        i, j, w, h = c['i'], c['j'], c['w'], c['h']
        for (di, dj, ti, tj, tw, th) in ((-1, 0, i - 1, j, 1, h), (1, 0, i + w, j, 1, h), (0, -1, i, j - 1, w, 1), (0, 1, i, j + h, w, 1)):
            if ti < 0 or tj < 0 or ti + tw > EW or tj + th > EH or not livre[tj:tj + th, ti:ti + tw].all(): continue
            nw, nh = w + abs(di), h + abs(dj)
            if min(nw, nh) > 5 or max(nw, nh) > LMAX + 1: continue
            c['i'], c['j'], c['w'], c['h'] = min(i, ti), min(j, tj), nw, nh
            LOTE[tj:tj + th, ti:ti + tw] = n; livre[tj:tj + th, ti:ti + tw] = False
            mudou = True; break
for c in CASAS:          # casa de fileira que cresceu pra trás até ficar bem mais funda que larga: cumeeira no lado maior
    if 'cume' in c:
        ao_longo, fundo_ = (c['w'], c['h']) if c['cume'] == 'x' else (c['h'], c['w'])
        if fundo_ > ao_longo: del c['cume']
for j in range(EH):
    for i in range(EW):
        if livre[j, i]: K[j, i] = RESTO; livre[j, i] = False
print("fileiras", nfila)
print("casas", len(CASAS), "restos", int((K == RESTO).sum()), "pátio", int((K == PATIO).sum()))

REBOCO = [0xF0C883, 0xEDB79C, 0xF6EAD0, 0xF4DC9C, 0xF0BE9E, 0xF7F0E4, 0xE6AE84, 0xF0D0C0, 0xEED9AE, 0xF3C9A0]
VENEZ = [0x3F6B4F, 0x2F5A44, 0x4F6F86, 0x7A5034, 0x8A3B2E, 0x3F6B4F]
ZONAS_MERCADO = ("Mercato", "Vicolo", "Via del forno")


for n, c in enumerate(CASAS):
    i, j, w, h = c['i'], c['j'], c['w'], c['h']
    cx, cy = i - MG + w / 2, j - MG + h / 2
    c['cx'], c['cy'] = cx, cy
    r = np.random.default_rng(h32(i, j, 3))
    # lados: quantas casas de rua encostam (N, S, O, L)
    viz = {'N': [(i + t, j - 1) for t in range(w)], 'S': [(i + t, j + h) for t in range(w)],
           'O': [(i - 1, j + t) for t in range(h)], 'L': [(i + w, j + t) for t in range(h)]}
    rua = {s: sum(k_em(a, b) == RUA for a, b in v) for s, v in viz.items()}
    if 'frente' not in c: c['frente'] = max(rua, key=lambda s: rua[s] / len(viz[s])) if max(rua.values()) else None
    zs = [zchao(a - MG, b - MG) for v in viz.values() for a, b in v if k_em(a, b) == RUA]
    c['base'] = float(np.mean(zs)) if zs else zperto(cx, cy)
    c['porta'] = None
    if c['frente']:
        vf = viz[c['frente']]
        ok = [t for t, (a, b) in enumerate(vf) if k_em(a, b) == RUA and M.P[PECA_DE[(a - MG, b - MG)]][1] != 's'
              and M.CB[int((b - MG + .5) * S)][int((a - MG + .5) * S)] is None]
        if ok:
            c['porta'] = min(ok, key=lambda t: abs(t - (len(vf) - 1) / 2))
            a, b = vf[c['porta']]; c['base'] = zchao(a - MG, b - MG)
    c['zmin'] = min(zs) if zs else c['base']
    # dois andares só onde não esconde rua atrás (a câmera do jogo olha do sul pro norte)
    tapa = any(k_em(i + t, j - d) in (RUA, PAL) for t in range(w) for d in (1, 2, 3))
    fora = not (0 <= cx < W and 0 <= cy < H)
    c['tapa'] = tapa
    # sobrado só na cidade de fora do mapa (usuário: "essas casas altas sem sentido, estranhas e feias"); dentro é tudo térreo
    c['andares'] = 2 if (fora and not tapa and r.random() < 0.5) else 1
    perto_cast = (27 <= cx < 47 and cy < 13)
    p_pedra = 1.0 if perto_cast else float(np.clip((21 - cy) / 9, 0, 1)) * 0.85
    c['pedra'] = bool(r.random() < p_pedra)
    c['cor'] = hx(0xEADFC8, 0.92 + 0.12 * r.random()) if c['pedra'] else hx(int(REBOCO[int(r.integers(len(REBOCO)))]), 1.3 + 0.1 * r.random())
    c['venez'] = hx(int(VENEZ[int(r.integers(len(VENEZ)))]))
    tb = 0.74 + 0.36 * r.random(); c['telha'] = (tb, tb * (0.94 + 0.1 * r.random()), tb * (0.86 + 0.16 * r.random()))
    c['loja'] = c['frente'] is not None and any(zona(a - MG, b - MG) in ZONAS_MERCADO for a, b in viz[c['frente']] if k_em(a, b) == RUA)
    # cumeeira ao longo do lado maior; casa quadrada: paralela à rua da frente
    if 'cume' not in c: c['cume'] = 'y' if h > w else 'x' if w > h else ('x' if c['frente'] in ('N', 'S', None) else 'y')
# beiral em alturas diferentes entre vizinhas (faixa de parede a mais embaixo do telhado), pra telhado não emendar em telhado
VIZC = [set() for _ in CASAS]
for j in range(EH):
    for i in range(EW):
        a = LOTE[j, i]
        if a < 0: continue
        for di, dj in ((1, 0), (0, 1)):
            if i + di < EW and j + dj < EH:
                b = LOTE[j + dj, i + di]
                if b >= 0 and b != a: VIZC[a].add(int(b)); VIZC[b].add(int(a))
# casas coladas têm o mesmo número de andares (telhado baixo encostado em parede alta entrava na fachada do vizinho)
_visto = set()
for n0 in range(len(CASAS)):
    if n0 in _visto: continue
    grupo = [n0]; _visto.add(n0); k_ = 0
    while k_ < len(grupo):
        for m_ in VIZC[grupo[k_]]:
            if m_ not in _visto: _visto.add(m_); grupo.append(m_)
        k_ += 1
    alto = (not any(CASAS[m_]['tapa'] for m_ in grupo)) and sum(CASAS[m_]['andares'] for m_ in grupo) >= 1.5 * len(grupo)
    for m_ in grupo: CASAS[m_]['andares'] = 2 if alto else 1
for n in rng.permutation(len(CASAS)):
    c = CASAS[n]
    usados = [CASAS[m]['topo'] for m in VIZC[n] if 'topo' in CASAS[m]]
    melhor, mv = 0.0, -1.0
    for sb in rng.permutation([2.0, 4.5, 7.0]):          # sempre uma faixa: a cornija da parede furava a beira da telha
        t = c['base'] + c['andares'] * AND + sb
        sc = min([abs(t - u) for u in usados], default=99.0) - 0.15 * sb
        if sc > mv: melhor, mv = float(sb), sc
    c['sobe'] = melhor; c['topo'] = c['base'] + c['andares'] * AND + melhor

# ====================================================================================================== materiais
TEXS = {}


def tex(arq, cor=True):
    if arq not in TEXS:
        im = bpy.data.images.load(os.path.join(KIT, arq))
        if not cor: im.colorspace_settings.name = 'Non-Color'
        TEXS[arq] = im
    return TEXS[arq]


def mat_caixa(nome, arq, rep, tinta=(1, 1, 1), calma=0.0, cor_calma=(0.5, 0.5, 0.5), rough=0.9, vcol=True, bump=0.25):
    """material pra malha gerada: textura do kit projetada em caixa (rep = casas por repetição), vezes a cor do vértice."""
    m = bpy.data.materials.new(nome); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs['Roughness'].default_value = rough
    atual = None
    if arq:
        tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs['Scale'].default_value = (1 / rep, 1 / rep, 1 / rep)
        nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
        tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = tex(arq); tx.projection = 'BOX'; tx.projection_blend = 0.15
        nt.links.new(mp.outputs['Vector'], tx.inputs['Vector'])
        mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = calma
        nt.links.new(tx.outputs['Color'], mx.inputs[6]); mx.inputs[7].default_value = (*cor_calma, 1)
        mu = nt.nodes.new("ShaderNodeMix"); mu.data_type = 'RGBA'; mu.blend_type = 'MULTIPLY'; mu.inputs['Factor'].default_value = 1
        nt.links.new(mx.outputs[2], mu.inputs[6]); mu.inputs[7].default_value = (*tinta, 1)
        atual = mu.outputs[2]
        if bump:
            bp = nt.nodes.new("ShaderNodeBump"); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = 0.02
            nt.links.new(tx.outputs['Color'], bp.inputs['Height']); nt.links.new(bp.outputs['Normal'], b.inputs['Normal'])
    if vcol:
        vc = nt.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
        if atual is None:
            atual = vc.outputs['Color']
        else:
            m2 = nt.nodes.new("ShaderNodeMix"); m2.data_type = 'RGBA'; m2.blend_type = 'MULTIPLY'; m2.inputs['Factor'].default_value = 1
            nt.links.new(atual, m2.inputs[6]); nt.links.new(vc.outputs['Color'], m2.inputs[7]); atual = m2.outputs[2]
    nt.links.new(atual, b.inputs['Base Color'])
    return m


QUENTE = hx(0xF2E2C2, 1.75)       # tira o azulado da pedra do kit
MATG = {
    'liso': mat_caixa('liso', None, 1),
    'pedra': mat_caixa('pedra', 'T_Brick_BaseColor.png', 4.0, hx(0xFFD2A2, 2.1), calma=0.5, cor_calma=(0.60, 0.50, 0.36), bump=0.15),                 # cantaria do castelo
    'rustica': mat_caixa('rustica', 'T_UnevenBrick_BaseColor.png', 2.0, QUENTE, calma=0.5, cor_calma=(0.36, 0.33, 0.29), bump=0.05),                  # pedra de muro
    'trav': mat_caixa('trav', 'T_Brick_BaseColor.png', 3.0, hx(0xFFEED2, 2.2), calma=0.72, cor_calma=(0.6, 0.55, 0.45)),       # travertino claro (igreja)
    'gesso': mat_caixa('gesso', 'T_Plaster_BaseColor.png', 2.0),                                   # reboco (empena)
    'rua': mat_caixa('rua', 'T_UnevenBrick_BaseColor.png', 3.0, hx(0xEAD9B8, 1.9), calma=0.68, cor_calma=(0.40, 0.36, 0.30), bump=0.05),
    'mercado': mat_caixa('mercado', 'T_RedBrick_BaseColor.png', 2.0, hx(0xF0C8A8, 1.7), calma=0.6, cor_calma=(0.5, 0.36, 0.3), bump=0.05),
    'piazza': mat_caixa('piazza', 'T_Brick_BaseColor.png', 3.0, hx(0xFFEBC8, 2.3), calma=0.66, cor_calma=(0.5, 0.46, 0.38), bump=0.05),
    'patio_cast': mat_caixa('patio_cast', 'T_UnevenBrick_BaseColor.png', 2.0, hx(0xD6C4A0, 1.5), calma=0.72, cor_calma=(0.34, 0.31, 0.27), bump=0.05),
    'madeira': mat_caixa('madeira', 'T_WoodTrim_BaseColor.png', 1.5, hx(0xE8C8A0, 1.3)),
    'tabua': mat_caixa('tabua', 'T_WoodTrim_BaseColor.png', 3.0, hx(0xF2DDB8, 1.9), calma=0.75, cor_calma=(0.55, 0.42, 0.3), bump=0.08),
    'grama': mat_caixa('grama', None, 1),
    'cotto': mat_caixa('cotto', 'T_RedBrick_BaseColor.png', 1.2, hx(0xF2C4A0, 1.75), calma=0.45, cor_calma=(0.5, 0.36, 0.3), bump=0.05),
    # chão pintado dos bombs: A = tapete de tijolo cor de telha (praça da igreja), B = tijolo cor de vinho (adega)
    'sitioA': mat_caixa('sitioA', 'T_Brick_BaseColor.png', 2.0, hx(0xF6D2A8, 2.1), calma=0.6, cor_calma=(0.5, 0.44, 0.36), bump=0.05),
    'sitioB': mat_caixa('sitioB', 'T_Brick_BaseColor.png', 2.0, hx(0xEEC6A6, 2.0), calma=0.6, cor_calma=(0.5, 0.44, 0.36), bump=0.05),
}
_g = MATG['grama'].node_tree; _b = _g.nodes["Principled BSDF"]; _vc = next(n for n in _g.nodes if n.type == 'VERTEX_COLOR')
_tc = _g.nodes.new("ShaderNodeTexCoord")
_n1 = _g.nodes.new("ShaderNodeTexNoise"); _n1.inputs['Scale'].default_value = 1.3; _n1.inputs['Detail'].default_value = 4
_n2 = _g.nodes.new("ShaderNodeTexNoise"); _n2.inputs['Scale'].default_value = 14.0; _n2.inputs['Detail'].default_value = 2
_g.links.new(_tc.outputs['Object'], _n1.inputs['Vector']); _g.links.new(_tc.outputs['Object'], _n2.inputs['Vector'])
_ad = _g.nodes.new("ShaderNodeMath"); _ad.operation = 'ADD'; _g.links.new(_n1.outputs[0], _ad.inputs[0])
_m2 = _g.nodes.new("ShaderNodeMath"); _m2.operation = 'MULTIPLY'; _m2.inputs[1].default_value = 0.5; _g.links.new(_n2.outputs[0], _m2.inputs[0]); _g.links.new(_m2.outputs[0], _ad.inputs[1])
_rp = _g.nodes.new("ShaderNodeMapRange"); _rp.inputs['From Min'].default_value = 0.45; _rp.inputs['From Max'].default_value = 1.05; _rp.inputs['To Min'].default_value = 0.62; _rp.inputs['To Max'].default_value = 1.25
_g.links.new(_ad.outputs[0], _rp.inputs['Value'])
_mu = _g.nodes.new("ShaderNodeVectorMath"); _mu.operation = 'SCALE'; _g.links.new(_vc.outputs['Color'], _mu.inputs[0]); _g.links.new(_rp.outputs[0], _mu.inputs['Scale'])
_g.links.new(_mu.outputs[0], _b.inputs['Base Color'])
ACC = {k: ([], []) for k in MATG}      # material -> (vértices dos quads, cores)
BR = (1, 1, 1)


def P3(x, y, z):
    return (x, -y, z * U)


def quad(mat, pts, cor=BR):
    v, c = ACC[mat]
    v.append([P3(*p) for p in pts]); c.append(cor)


def caixa(mat, x0, y0, z0, x1, y1, z1, cor=BR, topo=None, cor_topo=None, base=False):
    quad(mat, [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], cor)
    quad(mat, [(x1, y0, z0), (x0, y0, z0), (x0, y0, z1), (x1, y0, z1)], cor)
    quad(mat, [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], cor)
    quad(mat, [(x1, y1, z0), (x1, y0, z0), (x1, y0, z1), (x1, y1, z1)], cor)
    quad(topo or mat, [(x0, y1, z1), (x1, y1, z1), (x1, y0, z1), (x0, y0, z1)], cor_topo or cor)
    if base:
        quad(mat, [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], cor)


def cilindro(mat, cx, cy, z0, r, alt, cor=BR, lados=14, cor_topo=None):
    for k in range(lados):
        a0, a1 = 2 * math.pi * k / lados, 2 * math.pi * (k + 1) / lados
        p0 = (cx + r * math.cos(a0), cy + r * math.sin(a0)); p1 = (cx + r * math.cos(a1), cy + r * math.sin(a1))
        quad(mat, [(*p0, z0), (*p1, z0), (*p1, z0 + alt), (*p0, z0 + alt)], cor)
        quad(mat, [(cx, cy, z0 + alt), (*p0, z0 + alt), (*p1, z0 + alt), (cx, cy, z0 + alt)], cor_topo or cor)


def arco(mat, eixo, a0, a1, c0, c1, zf, mola, flecha, ztopo, cor=BR, n=18):
    """arco de pedra sobre a rua: vão de a0 a a1 ao longo do eixo, espessura de c0 a c1 no outro eixo."""
    def p(a, c, z):
        return (a, c, z) if eixo == 'x' else (c, a, z)

    def zi(a):
        t = 2 * (a - a0) / (a1 - a0) - 1
        return zf + mola + flecha * math.sqrt(max(0.0, 1 - t * t))
    for k in range(n):
        sa, sb = a0 + (a1 - a0) * k / n, a0 + (a1 - a0) * (k + 1) / n
        za, zb = zi(sa), zi(sb)
        cc = tuple(v * (1.0 if k % 2 else 0.86) for v in cor)
        for c in (c0, c1):
            quad(mat, [p(sa, c, za), p(sb, c, zb), p(sb, c, zb + 5), p(sa, c, za + 5)], cc)          # aduelas
            quad(mat, [p(sa, c, za + 5), p(sb, c, zb + 5), p(sb, c, ztopo), p(sa, c, ztopo)], cor)
        quad(mat, [p(sa, c0, za), p(sb, c0, zb), p(sb, c1, zb), p(sa, c1, za)], tuple(v * 0.75 for v in cor))
        quad(mat, [p(sa, c0, ztopo), p(sb, c0, ztopo), p(sb, c1, ztopo), p(sa, c1, ztopo)], cor)
    # ombreiras
    for a in (a0, a1):
        d = 0.18 if a == a0 else -0.18
        aa, ab = min(a, a + d), max(a, a + d)
        if eixo == 'x': caixa(mat, aa, c0 - 0.05, zf, ab, c1 + 0.05, zf + mola, cor)
        else: caixa(mat, c0 - 0.05, aa, zf, c1 + 0.05, ab, zf + mola, cor)


def ameias(mat, x0, y0, x1, y1, z, cor=BR, alt=7.0, passo=0.36, esp=0.22, dentro=(0, 0)):
    """parapeito com ameias numa aresta (x0,y0)-(x1,y1); 'dentro' aponta pro lado do muro."""
    L = math.hypot(x1 - x0, y1 - y0); n = max(1, int(round(L / passo)))
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    ox, oy = dentro[0] * esp, dentro[1] * esp
    xa, xb = sorted((x0, x1 + ox)) if ux == 0 else sorted((x0, x1)); ya, yb = sorted((y0, y1 + oy)) if uy == 0 else sorted((y0, y1))
    if ux == 0: xa, xb = sorted((x0, x0 + ox))
    else: ya, yb = sorted((y0, y0 + oy))
    caixa(mat, xa, ya, z, xb, yb, z + 3.5, cor)
    for k in range(0, n, 2):
        t0, t1 = k / n, (k + 1) / n
        ax, ay = x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0; bx, by = x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1
        if ux == 0: caixa(mat, xa, min(ay, by), z + 3.5, xb, max(ay, by), z + 3.5 + alt, cor)
        else: caixa(mat, min(ax, bx), ya, z + 3.5, max(ax, bx), yb, z + 3.5 + alt, cor)


# materiais do kit: reboco e telha multiplicam a cor do objeto; a madeira das paredes vira cimalha de pedra clara
MATK = {}
CHATO = 0.62          # quanto a textura dos packs vira cor chapada (padrão maquete da Vila)
_MEDIA = {}


def achata(mat, fator=CHATO, relevo=False):
    """mistura a textura com a cor média dela e tira o mapa de relevo: fica com cara de brinquedo, não de foto."""
    if mat.get('chato'): return
    mat['chato'] = 1
    nt = mat.node_tree; b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None: return
    e = b.inputs['Base Color']
    if e.is_linked:
        src = e.links[0].from_socket; no = e.links[0].from_node
        img = no.image if no.type == 'TEX_IMAGE' else next((n.image for n in nt.nodes if n.type == 'TEX_IMAGE' and n.image and 'ase' in n.image.name), None)
        if img is not None:
            if img.name not in _MEDIA:
                px = np.array(img.pixels[:], np.float32).reshape(-1, 4)[::97]
                m = px[px[:, 3] > 0.5][:, :3] if (px[:, 3] > 0.5).any() else px[:, :3]
                _MEDIA[img.name] = tuple(float(v) for v in lin(m).mean(0)) if img.colorspace_settings.name == 'sRGB' else tuple(float(v) for v in m.mean(0))
            mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = fator
            nt.links.new(src, mx.inputs[6]); mx.inputs[7].default_value = (*_MEDIA[img.name], 1); nt.links.new(mx.outputs[2], e)
    if not relevo:
        for l in list(b.inputs['Normal'].links): nt.links.remove(l)
    b.inputs['Roughness'].default_value = 0.8 if relevo else 0.95
    for l in list(b.inputs['Roughness'].links): nt.links.remove(l)
    for nome_e in ('Metallic',):
        for l in list(b.inputs[nome_e].links): nt.links.remove(l)
        b.inputs[nome_e].default_value = 0.0



def no_cor_objeto(nt):
    return nt.nodes.new("ShaderNodeObjectInfo").outputs['Color']


def prepara_kit(mat, base):
    nt = mat.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    e = b.inputs['Base Color']
    src = e.links[0].from_socket if e.is_linked else None

    def mult(cor_fixa=None, objeto=False, plano=0.0):
        nonlocal src
        if src is None: return
        if cor_fixa is not None:
            mu = nt.nodes.new('ShaderNodeMix'); mu.data_type = 'RGBA'; mu.blend_type = 'MULTIPLY'; mu.inputs['Factor'].default_value = 1
            nt.links.new(src, mu.inputs[6]); mu.inputs[7].default_value = (*cor_fixa, 1); src = mu.outputs[2]
        if objeto:
            oc = no_cor_objeto(nt)
            if plano > 0:
                mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = plano
                nt.links.new(src, mx.inputs[6]); nt.links.new(oc, mx.inputs[7]); src = mx.outputs[2]
            else:
                mu = nt.nodes.new('ShaderNodeMix'); mu.data_type = 'RGBA'; mu.blend_type = 'MULTIPLY'; mu.inputs['Factor'].default_value = 1
                nt.links.new(src, mu.inputs[6]); nt.links.new(oc, mu.inputs[7]); src = mu.outputs[2]
        nt.links.new(src, e)
    if base == 'MI_Plaster': mult(objeto=True)
    elif base == 'MI_RoundTiles':
        mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = 0.2
        nt.links.new(src, mx.inputs[6]); mx.inputs[7].default_value = (*hx(0xC88A64), 1); src = mx.outputs[2]
        mult(cor_fixa=hx(0xFFE4CC, 1.4), objeto=True)
    elif base == 'MI_UnevenBrick': mult(cor_fixa=QUENTE, objeto=True)
    elif base in ('MI_Brick', 'MI_RockTrim'): mult(cor_fixa=hx(0xF4E6CC, 1.7))
    elif base == 'MI_WoodTrim_Wear': mult(objeto=True, plano=0.72)
    elif base == 'BEIRADA':          # forro e tábua de beira do telhado: saíam claros e viravam um serrilhado branco embaixo das telhas
        mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = 0.8
        nt.links.new(src, mx.inputs[6]); mx.inputs[7].default_value = (*hx(0x7C4630), 1); nt.links.new(mx.outputs[2], e)
    elif base == 'CORNIJA':
        mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.inputs['Factor'].default_value = 0.88
        nt.links.new(src, mx.inputs[6]); mx.inputs[7].default_value = (*hx(0xE9DFCB), 1); nt.links.new(mx.outputs[2], e)
    return mat


CACHE = {}


def carrega(nome, pasta=KIT, parede=False):
    chave = (nome, pasta)
    if chave in CACHE: return CACHE[chave]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(pasta, nome + ".gltf"))
    novos = [o for o in bpy.data.objects if o not in antes]
    malhas = [o for o in novos if o.type == 'MESH']
    for o in malhas:
        for s in o.material_slots:
            if not s.material: continue
            base = s.material.name.split('.')[0]
            if pasta == FP:
                if s.material.name.startswith('MI_Banner') and not s.material.get('pano'):
                    s.material['pano'] = 1
                    nt_ = s.material.node_tree; bs_ = next(n_ for n_ in nt_.nodes if n_.type == 'BSDF_PRINCIPLED'); e_ = bs_.inputs['Base Color']
                    oi_ = nt_.nodes.new("ShaderNodeObjectInfo")
                    inv_ = nt_.nodes.new("ShaderNodeMath"); inv_.operation = 'SUBTRACT'; inv_.inputs[0].default_value = 1.0; nt_.links.new(oi_.outputs['Alpha'], inv_.inputs[1])
                    mx_ = nt_.nodes.new("ShaderNodeMix"); mx_.data_type = 'RGBA'; nt_.links.new(inv_.outputs[0], mx_.inputs['Factor'])
                    if e_.is_linked: nt_.links.new(e_.links[0].from_socket, mx_.inputs[6])
                    else: mx_.inputs[6].default_value = e_.default_value[:]
                    nt_.links.new(oi_.outputs['Color'], mx_.inputs[7]); nt_.links.new(mx_.outputs[2], e_)
                continue
            if pasta != KIT:
                nome_m = s.material.name.lower()
                if s.material.get('copa') is None and not any(t in nome_m for t in ('bark', 'trunk', 'rock', 'wood', 'stone')):
                    nt_ = s.material.node_tree; bs_ = next((n_ for n_ in nt_.nodes if n_.type == 'BSDF_PRINCIPLED'), None)
                    if bs_ is not None:
                        ge = nt_.nodes.new("ShaderNodeNewGeometry"); oi_ = nt_.nodes.new("ShaderNodeObjectInfo")
                        up = nt_.nodes.new("ShaderNodeVectorMath"); up.operation = 'ADD'; up.inputs[1].default_value = (0, 0, 0.9)
                        nt_.links.new(oi_.outputs['Location'], up.inputs[0])
                        sb = nt_.nodes.new("ShaderNodeVectorMath"); sb.operation = 'SUBTRACT'
                        nt_.links.new(ge.outputs['Position'], sb.inputs[0]); nt_.links.new(up.outputs[0], sb.inputs[1])
                        cima = nt_.nodes.new("ShaderNodeVectorMath"); cima.operation = 'ADD'; cima.inputs[1].default_value = (0, 0, 0.9)
                        nm_ = nt_.nodes.new("ShaderNodeVectorMath"); nm_.operation = 'NORMALIZE'
                        nt_.links.new(sb.outputs[0], nm_.inputs[0]); nt_.links.new(nm_.outputs[0], cima.inputs[0])
                        n2 = nt_.nodes.new("ShaderNodeVectorMath"); n2.operation = 'NORMALIZE'; nt_.links.new(cima.outputs[0], n2.inputs[0])
                        nt_.links.new(n2.outputs[0], bs_.inputs['Normal']); bs_.inputs['Roughness'].default_value = 1.0
                    s.material['copa'] = 1
                continue
            alvo = 'CORNIJA' if (parede and base == 'MI_WoodTrim') else 'BEIRADA' if (nome.startswith('Roof_') and base == 'MI_WoodTrim') else base
            if alvo not in MATK:
                achata(s.material, 0.08 if base == 'MI_RoundTiles' else CHATO, relevo=(base == 'MI_RoundTiles'))
                MATK[alvo] = prepara_kit(s.material.copy() if alvo in ('CORNIJA', 'BEIRADA') else s.material, alvo)
            s.material = MATK[alvo]
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    for o in malhas: o.select_set(True)
    bpy.context.view_layer.objects.active = malhas[0]
    if len(malhas) > 1: bpy.ops.object.join()
    b = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in novos:
        if o != b and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
    b.hide_render = True; b.hide_viewport = True
    CACHE[chave] = b
    return b


MVC = {}


def carrega_fbx(nome):
    """prop dos packs já convertidos no projeto (cor por vértice); origem no meio da base, tamanho normalizado (maior lado = 1)."""
    chave = (nome, 'fbx')
    if chave in CACHE: return CACHE[chave]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(PACKS, nome + ".fbx"))
    novos = [o for o in bpy.data.objects if o not in antes]
    malhas = [o for o in novos if o.type == 'MESH']
    for o in malhas:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    for o in malhas: o.select_set(True)
    bpy.context.view_layer.objects.active = malhas[0]
    if len(malhas) > 1: bpy.ops.object.join()
    b = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    co = np.array([v.co[:] for v in b.data.vertices]); mn, mx = co.min(0), co.max(0)
    f = 1.0 / max(mx[0] - mn[0], mx[1] - mn[1])
    c0 = np.array([(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]])
    for v in b.data.vertices: v.co = (Vector(v.co) - Vector(c0)) * f
    b['alt'] = float((mx[2] - mn[2]) * f); b['dx'] = float((mx[0] - mn[0]) * f); b['dy'] = float((mx[1] - mn[1]) * f)
    ca = b.data.color_attributes
    if len(ca):
        gama = 1.0 if nome in ('barril', 'sacos') else 2.2          # os Quaternius já vêm lineares; Kenney/KayKit em sRGB
        nm = ca[0].name + str(gama)
        if nm not in MVC:
            m = bpy.data.materials.new('vc_' + nm); m.use_nodes = True
            vc = m.node_tree.nodes.new("ShaderNodeVertexColor"); vc.layer_name = ca[0].name
            gm = m.node_tree.nodes.new("ShaderNodeGamma"); gm.inputs['Gamma'].default_value = gama
            hs = m.node_tree.nodes.new("ShaderNodeHueSaturation"); hs.inputs['Saturation'].default_value = 0.88; hs.inputs['Value'].default_value = 0.95
            m.node_tree.links.new(vc.outputs['Color'], gm.inputs['Color']); m.node_tree.links.new(gm.outputs['Color'], hs.inputs['Color'])
            oi = m.node_tree.nodes.new("ShaderNodeObjectInfo")
            inv = m.node_tree.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
            m.node_tree.links.new(oi.outputs['Alpha'], inv.inputs[1])
            mxo = m.node_tree.nodes.new("ShaderNodeMix"); mxo.data_type = 'RGBA'
            m.node_tree.links.new(inv.outputs[0], mxo.inputs['Factor']); m.node_tree.links.new(hs.outputs['Color'], mxo.inputs[6]); m.node_tree.links.new(oi.outputs['Color'], mxo.inputs[7])
            m.node_tree.links.new(mxo.outputs[2], m.node_tree.nodes["Principled BSDF"].inputs['Base Color'])
            m.node_tree.nodes["Principled BSDF"].inputs['Roughness'].default_value = 0.85
            MVC[nm] = m
        b.data.materials.clear(); b.data.materials.append(MVC[nm])
    for o in novos:
        if o != b and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
    b.hide_render = True; b.hide_viewport = True
    CACHE[chave] = b
    return b


def poe(nome, x, y, z, giro=0.0, cor=None, esc=1.0, zesc=1.0, parede=False, pasta=KIT, forca=0.0):
    """peça do kit: (x, y) em casas, z em unidades do jogo, giro em graus (anti-horário visto de cima, no Blender)."""
    b = carrega(nome, pasta, parede)
    o = bpy.data.objects.new(nome, b.data); cena.collection.objects.link(o)
    o.location = (x, -y, z * U); o.rotation_euler = (0, 0, math.radians(giro))
    o.scale = (MT * esc, MT * esc, MT * esc * zesc)
    if cor is not None: o.color = (*cor, 1.0 - forca)
    PECAS.append([nome, round(x, 3), round(y, 3), round(z, 2), round(giro, 1), round(esc, 3), round(zesc, 3), [round(float(v), 3) for v in cor] if cor else None])
    return o


def prop(nome, x, y, z, larg, giro=0.0, alt=None, tinta=None, forca=0.75):
    """prop FBX: larg = maior lado no chão, em casas."""
    b = carrega_fbx(nome)
    o = bpy.data.objects.new(nome, b.data); cena.collection.objects.link(o)
    o.location = (x, -y, z * U); o.rotation_euler = (0, 0, math.radians(giro))
    ez = larg if alt is None else (alt * U) / b['alt']
    o.scale = (larg, larg, ez)
    if tinta is not None: o.color = (*tinta, 1.0 - forca)
    PECAS.append(["fbx:" + nome, round(x, 3), round(y, 3), round(z, 2), round(giro, 1), round(larg, 3), round(ez, 3), None])
    return o


NATINFO = {}
KITM = json.load(open(os.path.join(AQUI, "kit_medidas.json")))
BEIRAL = 0.06          # quanto o telhado passa do lote, em casas (igual pra todas)


def telhado(nome, x, y, zt, giro, cor, larg, comp, zesc=1.0, folga=None):
    """telhado do kit ajustado pra cobrir exatamente larg x comp casas (larg = atravessado à cumeeira), centrado no lote."""
    mn, mx = KITM[nome]["min"], KITM[nome]["max"]
    if folga is None: folga = BEIRAL
    sx = (larg + 2 * folga) / (mx[0] - mn[0]); sy = (comp + 2 * folga) / (mx[1] - mn[1])
    ox, oy = (mn[0] + mx[0]) / 2 * sx, (mn[1] + mx[1]) / 2 * sy
    a = math.radians(giro)
    b = carrega(nome)
    o = bpy.data.objects.new(nome, b.data); cena.collection.objects.link(o)
    o.location = (x - (ox * math.cos(a) - oy * math.sin(a)), -y - (ox * math.sin(a) + oy * math.cos(a)), zt * U)
    o.rotation_euler = (0, 0, a); o.scale = (sx, sy, MT * ZR * zesc); o.color = (*cor, 1)
    PECAS.append([nome, round(x, 3), round(y, 3), round(zt, 2), giro, round(sx, 4), round(sy, 4), [round(float(v), 3) for v in cor], "telhado"])
    return zt + mx[2] * 12.5 * ZR          # altura da cumeeira



MADEIRA_ESC = hx(0x7A5636); PALHA = hx(0xC2A45A); CARVALHO = hx(0x8A6642)


def barril(x, y, z, larg, giro=0.0, alt=None):
    return prop("ksv_barrel", x, y, z, larg, giro, alt=alt, tinta=MADEIRA_ESC, forca=0.8)


def barris(x0, y0, x1, y1, z, alt):
    """barris em pé, na proporção do modelo, enchendo o retângulo; a altura do barril é a da cobertura."""
    b = carrega_fbx("ksv_barrel"); d = min(alt * U / b['alt'], x1 - x0, y1 - y0)
    nx, ny = max(1, int((x1 - x0) / d + 0.15)), max(1, int((y1 - y0) / d + 0.15))
    for a_ in range(nx):
        for b_ in range(ny):
            barril(x0 + (a_ + .5) * (x1 - x0) / nx, y0 + (b_ + .5) * (y1 - y0) / ny, z, d * 0.97, 37 * (a_ + 2 * b_), alt=alt)


def fardo(x, y, z, larg, giro=0.0, alt=None):
    return prop("kgy_hay-bale", x, y, z, larg, giro, alt=alt, tinta=PALHA, forca=0.85)


def caixote(x, y, z, larg, giro, k):
    """caixote pequeno do kit com mercadoria em cima (a cor diz o que é)."""
    e = larg / 0.54
    poe("Prop_Crate", x, y, z, giro, esc=e, zesc=0.55)
    c = hx([0xC8372D, 0x6FA03A, 0xE08A2A, 0xC9A24A, 0x8A4A78][k % 5]); a = larg * 0.36; zt = z + 13.25 * e * 0.55
    quad('liso', [(x - a, y + a, zt + .2), (x + a, y + a, zt + .2), (x + a, y - a, zt + .2), (x - a, y - a, zt + .2)], c)


def estandarte(x, y, z, alt, eixo='x', larg=0.34):
    """pano comprido pendurado na parede (carmim com barra dourada)."""
    def pq(u, zz): return (x + u, y, zz) if eixo == 'x' else (x, y + u, zz)
    quad('liso', [pq(-larg / 2, z), pq(larg / 2, z), pq(larg / 2, z + alt), pq(-larg / 2, z + alt)], hx(0x8E1F2A))
    quad('liso', [pq(-larg / 2, z), pq(larg / 2, z), pq(0, z - alt * 0.14), pq(0, z - alt * 0.14)], hx(0x8E1F2A))
    quad('liso', [pq(-larg / 2, z + alt * 0.12), pq(larg / 2, z + alt * 0.12), pq(larg / 2, z + alt * 0.2), pq(-larg / 2, z + alt * 0.2)], hx(0xD9A93A))


def planta(nome, x, y, z, alt, giro=None):
    """árvore/arbusto do Stylized Nature MegaKit com 'alt' unidades do jogo de altura."""
    b = carrega(nome, NAT)
    if nome not in NATINFO:
        zs = [v.co.z for v in b.data.vertices]; NATINFO[nome] = max(zs) - min(zs)
    e = alt * U / NATINFO[nome]
    o = bpy.data.objects.new(nome, b.data); cena.collection.objects.link(o)
    o.location = (x, -y, z * U); o.rotation_euler = (0, 0, float(rng.uniform(0, 6.28)) if giro is None else math.radians(giro)); o.scale = (e, e, e)
    PECAS.append(["nat:" + nome, round(x, 3), round(y, 3), round(z, 2), 0, round(e, 4), 1, None])
    return o


FRUTAS = ["caixote_tomate", "caixote_alface", "caixote_cenoura", "caixote_batata", "caixote_cebola"]
LONA = [(0xC23B2E, 0xF3E9D2), (0x3E7D4F, 0xF3E9D2), (0xD99A2B, 0xF3E9D2), (0x2F5C8A, 0xF3E9D2), (0xB8402E, 0xE8C440)]


def toldo(x0, x1, yp, sentido, z, k, avanco=0.75, queda=7.0, n=None):
    """lona listrada presa na parede em y = yp, descendo pra frente (sentido = +1 sul / -1 norte)."""
    c1, c2 = LONA[k % len(LONA)]; n = n or max(4, int(round((x1 - x0) / 0.3)))
    yf = yp + sentido * avanco
    for m in range(n):
        xa, xb = x0 + (x1 - x0) * m / n, x0 + (x1 - x0) * (m + 1) / n
        cor = hx(c1 if m % 2 == 0 else c2)
        quad('liso', [(xa, yp, z), (xb, yp, z), (xb, yf, z - queda), (xa, yf, z - queda)], cor)
        quad('liso', [(xa, yf, z - queda), (xb, yf, z - queda), (xb, yf, z - queda - 2.6), (xa, yf, z - queda - 2.6)], cor)


def banca(x0, y0, x1, y1, z, alt, sentido, q):
    yb = y0 if sentido < 0 else y1           # lado de trás (parede)
    yf = y1 if sentido < 0 else y0
    ya, yc = sorted((yb, yb - sentido * 0.0)), None
    caixa('madeira', x0, min(y0, y1), z, x1, max(y0, y1), z + alt * 0.5, (0.85, 0.8, 0.75))
    caixa('madeira', x0 - 0.03, min(y0, y1) - 0.03, z + alt * 0.5, x1 + 0.03, max(y0, y1) + 0.03, z + alt * 0.5 + 1.2, (1.0, 0.95, 0.9))
    frente = yf - sentido * 0.0
    for xpo in (x0 + 0.06, x1 - 0.06):
        caixa('madeira', xpo - 0.05, yb + (0.5 if yf > yb else -0.5) - 0.05, z, xpo + 0.05, yb + (0.5 if yf > yb else -0.5) + 0.05, z + 43, (0.7, 0.65, 0.6))
        yq = yf - sentido * (-0.35)
        caixa('madeira', xpo - 0.05, yq - 0.05, z, xpo + 0.05, yq + 0.05, z + 34, (0.7, 0.65, 0.6))
    sn = 1 if yf > yb else -1
    toldo(x0 - 0.15, x1 + 0.15, yb + sn * 0.5, sn, z + 43, q, avanco=abs(yf - yb) + 0.05, queda=9.0)
    nn = max(2, int((x1 - x0) / 0.55))
    for m in range(nn):
        caixote(x0 + (m + .5) * (x1 - x0) / nn, (y0 + y1) / 2, z + alt * 0.5 + 1.2, 0.44, 3 * m - 4, q + m)
    yg = yf + (0.42 if yf > yb else -0.42)
    for m, t in enumerate((0.2, 0.75)):
        caixote(x0 + t * (x1 - x0), yg, z, 0.42, 8 * m - 6, q + m + 2)

PANO = [hx(0xC23B2E), hx(0x2F7D4F), hx(0xE0A62B), hx(0x2F5C9A), hx(0xF1E6CF), hx(0xB8402E), hx(0x7A3F8A)]
# ====================================================================================================== chão
CHAO_MAT = {'Mercato': 'mercado', 'Vicolo': 'mercado', 'Via del forno': 'mercado', 'Castello': 'patio_cast', 'Arco CT': 'patio_cast',
            'Piazza della Chiesa': 'piazza', 'Sagrato': 'piazza', 'Belvedere': 'piazza', 'Lavatoio': 'piazza', 'Cantina': 'piazza', 'Banchina': 'patio_cast'}
for (cx, cy), k in PECA_DE.items():
    zn, lt = M.P[k][0], M.P[k][1]
    if lt in 'k~': continue
    z00, z10, z11, z01 = zpeca(k, cx, cy), zpeca(k, cx + 1, cy), zpeca(k, cx + 1, cy + 1), zpeca(k, cx, cy + 1)
    mat = CHAO_MAT.get(zn, 'rua')
    cor = (0.93, 0.9, 0.84) if zn == 'Cantina' else BR
    quad(mat, [(cx, cy + 1, z01), (cx + 1, cy + 1, z11), (cx + 1, cy, z10), (cx, cy, z00)], cor)
    # espelho de degrau entre casas vizinhas de alturas diferentes (sagrato, emendas)
    for dx, dy, (a, b) in ((1, 0, ((cx + 1, cy), (cx + 1, cy + 1))), (0, 1, ((cx, cy + 1), (cx + 1, cy + 1)))):
        k2 = PECA_DE.get((cx + dx, cy + dy))
        if k2 is None or M.P[k2][1] in 'k~': continue
        za, zb = zpeca(k, *a), zpeca(k, *b); wa, wb = zpeca(k2, *a), zpeca(k2, *b)
        if abs(za - wa) > 0.4 or abs(zb - wb) > 0.4:
            doca = 'Banchina' in (zn, M.P[k2][0])
            quad('pedra' if doca else 'trav', [(*a, min(za, wa)), (*b, min(zb, wb)), (*b, max(zb, wb)), (*a, max(za, wa))])
            if doca:          # cordão de cantaria na beira da doca
                zt_ = max(za, wa); e_ = 0.14
                caixa('pedra', min(a[0], b[0]) - (e_ if a[0] == b[0] else 0), min(a[1], b[1]) - (e_ if a[1] == b[1] else 0), zt_ - 2.5, max(a[0], b[0]) + (e_ if a[0] == b[0] else 0), max(a[1], b[1]) + (e_ if a[1] == b[1] else 0), zt_ + 0.4, (1.05, 1.04, 1.0))
    # cordonata: faixa de pedra clara atravessando a escadaria a cada casa
    if lt == 's':
        gx = zpeca(k, cx + 1, cy + .5) - zpeca(k, cx, cy + .5); gy = zpeca(k, cx + .5, cy + 1) - zpeca(k, cx + .5, cy)
        zc = zpeca(k, cx + .5, cy + .5)
        if abs(gy) > abs(gx) and abs(gy) > 0.05:
            for f in (0.0, 0.5):
                zz = zpeca(k, cx + .5, cy + f + 0.06)
                caixa('trav', cx, cy + f, zz - 0.5, cx + 1, cy + f + 0.12, zz + 0.7)
        elif abs(gx) > 0.05:
            for f in (0.0, 0.5):
                zz = zpeca(k, cx + f + 0.06, cy + .5)
                caixa('trav', cx + f, cy, zz - 0.5, cx + f + 0.12, cy + 1, zz + 0.7)

# ====================================================================================================== chão pintado dos bombs
def traco(mat, xa, ya, xb, yb, larg, z, cor):
    L = math.hypot(xb - xa, yb - ya); ux, uy = (xb - xa) / L, (yb - ya) / L; vx, vy = -uy * larg / 2, ux * larg / 2
    xa, ya, xb, yb = xa - ux * larg / 2, ya - uy * larg / 2, xb + ux * larg / 2, yb + uy * larg / 2
    quad(mat, [(xa - vx, ya - vy, z), (xb - vx, yb - vy, z), (xb + vx, yb + vy, z), (xa + vx, ya + vy, z)], cor)


LETRA = {'A': [[(-1, 1.5), (0, -1.5), (1, 1.5)], [(-0.55, 0.5), (0.55, 0.5)]],
         'B': [[(-0.75, 1.5), (-0.75, -1.5), (0.25, -1.5), (0.7, -1.1), (0.7, -0.4), (0.25, 0), (-0.75, 0)], [(0.25, 0), (0.8, 0.4), (0.8, 1.1), (0.3, 1.5), (-0.75, 1.5)]]}
CAL = hx(0xF4EAD2)
for nome_s, (sx0, sx1, sy0, sy1) in M.SITES.items():
    zs_ = zchao(int((sx0 + sx1) / 2), int((sy0 + sy1) / 2)) + 0.25
    mat_s = 'sitio' + nome_s
    quad(mat_s, [(sx0, sy1, zs_), (sx1, sy1, zs_), (sx1, sy0, zs_), (sx0, sy0, zs_)])
    b_ = 0.25          # cercadura de calçamento (a mesma pedra das ruas)
    for (a0, b0, a1, b1) in ((sx0 - b_, sy0 - b_, sx1 + b_, sy0), (sx0 - b_, sy1, sx1 + b_, sy1 + b_), (sx0 - b_, sy0, sx0, sy1), (sx1, sy0, sx1 + b_, sy1)):
        quad('rua', [(a0, b1, zs_ + .1), (a1, b1, zs_ + .1), (a1, b0, zs_ + .1), (a0, b0, zs_ + .1)], (0.8, 0.78, 0.74))

# ====================================================================================================== casas com as peças do kit
VIZ4 = {'S': (0, 1), 'N': (0, -1), 'L': (1, 0), 'O': (-1, 0)}
PEDRA_CASA = (0.80, 0.68, 0.54)      # faixa e oitão da casa de pedra no tom da parede do kit (saíam cinza em cima de parede marrom)
GIRO = {'S': 0, 'L': 90, 'N': 180, 'O': 270}
TELHADO = {2: 4, 3: 6, 4: 8}
KIT_COMP = {4: [4, 6, 8], 6: [4, 6, 8, 10, 12, 14], 8: [8, 10, 12, 14]}      # comprimentos de telhado que o kit tem, por largura
CUME_M = {2: 2.95, 3: 4.05, 4: 5.15}      # altura da empena (m) por largura do telhado


def exposto(c, i2, j2, z0):
    """a parede deste andar aparece? (vizinho é rua/pátio/vazio, ou é casa mais baixa)"""
    k = k_em(i2, j2)
    if k in (RUA, PATIO, RESTO, FORA, PAL): return True
    if k == CASA:
        o = CASAS[LOTE[j2, i2]]
        return o['topo'] < z0 + 12
    if k == CAST: return False
    if k == MURA: return z0 > 18 + 50
    if k == IGR: return z0 > 60
    return True


VASOS = ["kenney_pottedPlant", "kenney_plantSmall1", "kenney_plantSmall2", "kenney_plantSmall3"]
nwall = 0
for n, c in enumerate(CASAS):
    i, j, w, h = c['i'], c['j'], c['w'], c['h']
    x0, y0 = i - MG, j - MG; x1, y1 = x0 + w, y0 + h
    r = np.random.default_rng(h32(i, j, 9))
    pre = "Wall_UnevenBrick_" if c['pedra'] else "Wall_Plaster_"
    liso = "Wall_UnevenBrick_Straight" if c['pedra'] else "Wall_Plaster_Straight_Base"
    caixa('rustica', x0 - 0.03, y0 - 0.03, min(c['zmin'], c['base']) - 10, x1 + 0.03, y1 + 0.03, c['base'] + 0.6, (0.9, 0.88, 0.84))
    cantos = {}
    for a in range(c['andares']):
        z = c['base'] + a * AND
        for lado, (dx, dy) in VIZ4.items():
            nseg = w if lado in 'NS' else h
            meio = nseg // 2
            for t in range(nseg):
                ci, cj = (i + t, j + h if lado == 'S' else j - 1) if lado in 'NS' else (i + w if lado == 'L' else i - 1, j + t)
                if not exposto(c, ci, cj, z):
                    continue
                cantos.setdefault((a, lado), set()).add(t)
                px, py = (x0 + t + .5, y1 if lado == 'S' else y0) if lado in 'NS' else (x1 if lado == 'L' else x0, y0 + t + .5)
                g = GIRO[lado]
                rua = k_em(ci, cj) == RUA
                cega = k_em(ci, cj) in (CASA, CAST, MURA, IGR, PAL)       # parede que sobra acima do vizinho: lisa
                u = r.random()
                if a == 0 and lado == c['frente'] and t == c['porta']:
                    poe(pre + "Door_Round", px, py, z, g, c['cor'], parede=True)
                    poe("DoorFrame_Round_Brick", px, py, z, g)
                    # a porta do kit abre pelo canto: desloca meio vão pra centralizar
                    ox, oy = {'S': (-0.265, 0), 'N': (0.265, 0), 'L': (0, 0.265), 'O': (0, -0.265)}[lado]
                    poe(["Door_2_Round", "Door_4_Round", "Door_8_Round", "Door_1_Round"][int(r.integers(4))], px + ox, py + oy, z, g, c['venez'])
                    if c['loja'] and c['andares'] > 1:
                        poe("Banner_1" if r.random() < 0.5 else "Banner_2", px + (0.75 if lado in 'NS' else 0) + dx * 0.05, py + (0.75 if lado in 'OL' else 0) + dy * 0.05, z + 58, g - 90, PANO[int(r.integers(len(PANO)))], pasta=FP, forca=0.8)
                    if c['loja'] and r.random() < 0.6:
                        sx, sy = (0.72, 0) if lado in 'NS' else (0, 0.72)
                        poe(["FarmCrate_Apple", "FarmCrate_Carrot", "Barrel_Apples", "Bag"][int(r.integers(4))], px - sx + dx * 0.22, py - sy + dy * 0.22, zperto(px + dx * .5, py + dy * .5), g + float(r.uniform(-12, 12)), pasta=FP)
                    continue
                ritmo = (t % 2) == ((c['porta'] if (c['porta'] is not None and lado == c['frente']) else nseg // 2) % 2)
                if nseg <= 2: ritmo = (t == 0)
                pj = (0.85 if rua else 0.2) if ritmo else 0.0
                if cega: pj = 0.0
                if lado != c['frente'] and not rua: pj *= 0.6
                if u < pj:
                    larga = r.random() < 0.18
                    tp = "Wide_Round" if larga else "Thin_Round"
                    poe(pre + "Window_" + tp, px, py, z, g, c['cor'], parede=True)
                    poe("Window_" + tp + "1", px, py, z, g, c['venez'])
                    if not c['pedra'] or r.random() < 0.35:
                        poe("WindowShutters_" + tp + ("_Closed" if r.random() < 0.25 else "_Open"), px, py, z, g, c['venez'])
                else:
                    poe(liso, px, py, z, g, c['cor'], parede=True)
                    if rua and not cega and a == c['andares'] - 1 and r.random() < 0.12:
                        poe(["Prop_Vine1", "Prop_Vine2", "Prop_Vine4"][int(r.integers(3))], px + dx * 0.05, py + dy * 0.05, z + 30, g)
                nwall += 1
        # cunhais de pedra nas quinas à mostra
        for (la, lb, qx, qy, g) in (('S', 'O', x0, y1, 0), ('S', 'L', x1, y1, 90), ('N', 'L', x1, y0, 180), ('N', 'O', x0, y0, 270)):
            ta = cantos.get((a, la), set()); tb = cantos.get((a, lb), set())
            ia = (0 if lb == 'O' else w - 1); ib = (h - 1 if la == 'S' else 0)
            if ia in ta and ib in tb:
                # o cunhal do kit saltava ~20 cm pra fora das duas paredes (usuário: "muretas que saltam pra fora"): recua
                # pra dentro da quina até sobrar só um relevo fino
                rc = 0.05
                poe("Corner_Exterior_Brick", qx + (rc if lb == 'O' else -rc), qy + (-rc if la == 'S' else rc), z, g)
    # telhado do kit no tamanho certo da casa
    zt = c['topo']
    if c['sobe'] > 0:
        caixa('rustica' if c['pedra'] else 'gesso', x0 - 0.045, y0 - 0.045, zt - c['sobe'], x1 + 0.045, y1 + 0.045, zt, PEDRA_CASA if c['pedra'] else c['cor'])
    curto, longo = (w, h) if c['cume'] == 'y' else (h, w)
    # telhado comprido vai em trechos na mesma cumeeira (o kit só tem até 8/14 de comprimento: esticar deixava a telha gigante)
    kw = TELHADO[min(curto, 4)]; nt = max(1, int(math.ceil(2 * longo / (max(KIT_COMP[kw]) + 1.0)))); lt = longo / nt
    nome = "Roof_RoundTiles_%dx%d" % (kw, min(KIT_COMP[kw], key=lambda v: abs(v - 2 * lt)))
    # beiral de verdade (usuário: "as casas são maiores que os telhados"): o telhado passa 0,17 casa (~34 cm) de cada lado
    # que dá pra rua/pátio ou pra casa mais baixa, cobrindo a cornija e o cunhal; do lado colado em casa da mesma altura ou
    # mais alta não passa nada, pra não entrar no vizinho
    def _bl(lado):
        nseg_ = w if lado in 'NS' else h
        for t in range(nseg_):
            ci, cj = (i + t, j + h if lado == 'S' else j - 1) if lado in 'NS' else (i + w if lado == 'L' else i - 1, j + t)
            if k_em(ci, cj) == CASA and CASAS[LOTE[cj, ci]]['topo'] > zt - 4: return 0.0
            if k_em(ci, cj) in (CAST, MURA, IGR): return 0.0
        return 0.17
    # no lado da calha a fiada de telha do kit termina em serrilhado ~25 cm pra dentro da borda da peça: ali o beiral é maior,
    # senão a faixa de reboco aparecia entre as pontas das telhas
    ke = (1.9, 1.0) if c['cume'] == 'y' else (1.0, 1.9)
    rx0, rx1, ry0, ry1 = x0 - _bl('O') * ke[0], x1 + _bl('L') * ke[0], y0 - _bl('N') * ke[1], y1 + _bl('S') * ke[1]
    rl = (ry1 - ry0) if c['cume'] == 'y' else (rx1 - rx0); rc_ = (rx1 - rx0) if c['cume'] == 'y' else (ry1 - ry0); lt = rl / nt
    for q in range(nt):
        d_ = (q + 0.5) * lt
        tx, ty = ((rx0 + rx1) / 2, ry0 + d_) if c['cume'] == 'y' else (rx0 + d_, (ry0 + ry1) / 2)
        ap = telhado(nome, tx, ty, zt, 0 if c['cume'] == 'y' else 90, c['telha'], rc_, lt, folga=0.0) - 5.0
    mg = 'rustica' if c['pedra'] else 'gesso'
    cg = PEDRA_CASA if c['pedra'] else c['cor']
    if c['cume'] == 'y':
        for yy in (y0 - 0.047, y1 + 0.047):          # no mesmo plano da faixa de reboco de baixo, pra não marcar emenda
            quad(mg, [(x0, yy, zt - 3), (x1, yy, zt - 3), ((rx0 + rx1) / 2, yy, ap), ((rx0 + rx1) / 2, yy, ap)], cg)
    else:
        for xx in (x0 - 0.047, x1 + 0.047):
            quad(mg, [(xx, y0, zt - 3), (xx, y1, zt - 3), (xx, (ry0 + ry1) / 2, ap), (xx, (ry0 + ry1) / 2, ap)], cg)
    if r.random() < 0.18 and curto >= 3:
        t = float(r.uniform(-0.3, 0.3)) * longo
        qx, qy = ((x0 + x1) / 2 + 0.45, (y0 + y1) / 2 + t) if c['cume'] == 'y' else ((x0 + x1) / 2 + t, (y0 + y1) / 2 + 0.45)
        poe("Prop_Chimney2" if c['pedra'] else "Prop_Chimney", qx, qy, zt + (ap - zt) * 0.35, 0, esc=0.9)
print("paredes", nwall)

# ====================================================================================================== pátios e sobras: jardim murado
ZPAT = {}          # nível único por pátio/jardim (região ligada)
for j in range(EH):
    for i in range(EW):
        if K[j, i] not in (PATIO, RESTO) or (i, j) in ZPAT: continue
        reg = [(i, j)]; ZPAT[(i, j)] = None; k_ = 0
        while k_ < len(reg):
            a_, b_ = reg[k_]; k_ += 1
            for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                v_ = (a_ + da, b_ + db)
                if k_em(*v_) in (PATIO, RESTO) and v_ not in ZPAT: ZPAT[v_] = None; reg.append(v_)
        zr = float(np.median([zperto(a_ - MG + .5, b_ - MG + .5) for a_, b_ in reg]))
        for v_ in reg: ZPAT[v_] = zr
ARV = ["CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5", "Pine_1", "Pine_2", "Pine_3"]
for j in range(EH):
    for i in range(EW):
        if K[j, i] not in (PATIO, RESTO): continue
        cx, cy = i - MG, j - MG
        z = ZPAT[(i, j)]
        if K[j, i] == RESTO and not any(k_em(i + dx, j + dy) in (RUA, FORA, PAL, PATIO, RESTO) for dx, dy in VIZ4.values()):
            # casa solta fechada por casas dos quatro lados: em vez de buraco entre os telhados, uma torrinha (altana) com
            # telhado de quatro águas, um pouco acima dos vizinhos
            vz = [CASAS[LOTE[j + dy, i + dx]] for dx, dy in VIZ4.values() if k_em(i + dx, j + dy) == CASA]
            if vz:
                dono = max(vz, key=lambda o: o['topo']); zt_ = dono['topo'] + 16
                caixa('rustica' if dono['pedra'] else 'gesso', cx - 0.02, cy - 0.02, z - 8, cx + 1.02, cy + 1.02, zt_, PEDRA_CASA if dono['pedra'] else dono['cor'])
                telhado("Roof_Tower_RoundTiles", cx + .5, cy + .5, zt_, 0, dono['telha'], 1.0, 1.0, zesc=0.45)
                continue
        quad('grama', [(cx, cy + 1, z + 0.5), (cx + 1, cy + 1, z + 0.5), (cx + 1, cy, z + 0.5), (cx, cy, z + 0.5)], hx(0x6F9A3E))
        for lado, (dx, dy) in VIZ4.items():
            kv = k_em(i + dx, j + dy)
            if kv in (RUA, FORA, PAL):          # muro de pedra com capa onde o jardim dá pra rua
                e = 0.2
                if lado == 'S': caixa('rustica', cx, cy + 1 - e, z - 8, cx + 1, cy + 1, z + 30, topo='trav')
                if lado == 'N': caixa('rustica', cx, cy, z - 8, cx + 1, cy + e, z + 30, topo='trav')
                if lado == 'L': caixa('rustica', cx + 1 - e, cy, z - 8, cx + 1, cy + 1, z + 30, topo='trav')
                if lado == 'O': caixa('rustica', cx, cy, z - 8, cx + e, cy + 1, z + 30, topo='trav')
        hh = h32(i, j, 5)
        rp_ = np.random.default_rng(hh)
        borda = any(k_em(i + dx, j + dy) not in (PATIO, RESTO) for dx, dy in VIZ4.values())
        if K[j, i] == PATIO:
            if rp_.random() < (0.3 if borda else 0.55):
                planta(ARV[int(rp_.integers(5))], cx + float(rp_.uniform(0.3, 0.7)), cy + float(rp_.uniform(0.3, 0.7)), z, float(rp_.uniform(46, 74)))
        else:
            if hh % 3 == 0: planta(["CommonTree_1", "CommonTree_3", "Pine_3", "CommonTree_2"][(hh >> 4) % 4], cx + .5, cy + .5, z, 50 + (hh >> 8) % 22)
            else: planta(["CommonTree_2", "CommonTree_5"][(hh >> 4) % 2], cx + .5, cy + .5, z, 26 + (hh >> 8) % 10)

# ====================================================================================================== castelo e muralha
ZC = 18.0
ALT = {}          # (i, j) -> topo
for j in range(EH):
    for i in range(EW):
        cx, cy = i - MG, j - MG
        if K[j, i] == CAST:
            ALT[(i, j)] = ZC + 84
            if cy >= 18: ALT[(i, j)] = 10 + (80 if cx in (6, 7, 12, 13) else 52)          # Porta Vecchia: torres e resto da muralha antiga
            if 26 <= cx < 30 and 6 <= cy < 8: ALT[(i, j)] = ZC + 72                         # muralha atrás da sacada
        elif K[j, i] == MURA:
            ALT[(i, j)] = ZC + (58 if cy < 0 else 58)
TORRES = [(30, 7, 32, 9, 136), (36, 7, 38, 9, 136), (30, 0, 32, 2, 140), (42, 0, 44, 2, 140),
          (34, -5, 40, 0, 215),                                    # mastio (torre de menagem)
          (5, -2, 7, 1, 92), (14, -2, 16, 1, 92), (23, -2, 25, 1, 92), (50, -2, 52, 1, 92), (58, -2, 60, 1, 92), (-3, -2, -1, 1, 92), (61, -2, 63, 1, 92)]
TORRE_DE = {}
for t, (a0, b0, a1, b1, alt) in enumerate(TORRES):
    for cy in range(b0, b1):
        for cx in range(a0, a1):
            if K[cy + MG, cx + MG] in (CAST, MURA):
                ALT[(cx + MG, cy + MG)] = ZC + alt; TORRE_DE[(cx + MG, cy + MG)] = t
CP = (1, 1, 1)
for (i, j), zt in ALT.items():
    cx, cy = i - MG, j - MG
    quad('pedra', [(cx, cy + 1, zt), (cx + 1, cy + 1, zt), (cx + 1, cy, zt), (cx, cy, zt)], (0.93, 0.92, 0.9))
    for lado, (dx, dy) in VIZ4.items():
        v = (i + dx, j + dy); kv = k_em(*v)
        if v in ALT: zb = ALT[v]
        elif kv == RUA: zb = zchao(v[0] - MG, v[1] - MG)
        elif kv == CASA: zb = CASAS[LOTE[v[1], v[0]]]['base'] + 20
        else: zb = -10
        if zb >= zt - 0.5: continue
        (ax, ay), (bx, by) = {'S': ((cx, cy + 1), (cx + 1, cy + 1)), 'N': ((cx + 1, cy), (cx, cy)), 'L': ((cx + 1, cy + 1), (cx + 1, cy)), 'O': ((cx, cy), (cx, cy + 1))}[lado]
        quad('pedra', [(ax, ay, zb), (bx, by, zb), (bx, by, zt), (ax, ay, zt)])
        if zt - zb > 14:
            ameias('pedra', ax, ay, bx, by, zt, dentro=(-dx, -dy))
            # cordão de pedra abaixo do parapeito
            e = 0.05
            caixa('pedra', min(ax, bx) - e * abs(dx) * (dx > 0) * 0 - (e if dx < 0 else 0), min(ay, by) - (e if dy < 0 else 0), zt - 9,
                  max(ax, bx) + (e if dx > 0 else 0), max(ay, by) + (e if dy > 0 else 0), zt - 6.5, (1.06, 1.05, 1.02))
            # seteira nas faces altas
            if zt - zb > 60 and (cx + cy) % 2 == 0:
                mx_, my_ = (ax + bx) / 2 + dx * 0.012, (ay + by) / 2 + dy * 0.012
                w_ = 0.045
                pa = (mx_ - w_ * abs(dy), my_ - w_ * abs(dx)); pb = (mx_ + w_ * abs(dy), my_ + w_ * abs(dx))
                zz = zb + (zt - zb) * 0.55
                quad('liso', [(*pa, zz), (*pb, zz), (*pb, zz + 9), (*pa, zz + 9)], hx(0x2A221C))
# portões (arcos finos pra não esconder quem passa)
arco('pedra', 'x', 32, 36, 8.15, 8.85, ZC, 30, 26, ZC + 84); ameias('pedra', 32, 8.85, 36, 8.85, ZC + 84, dentro=(0, -1)); ameias('pedra', 36, 8.15, 32, 8.15, ZC + 84, dentro=(0, 1))
arco('pedra', 'y', 3, 6, 31.2, 31.8, ZC, 28, 22, ZC + 84); ameias('pedra', 31.2, 3, 31.2, 6, ZC + 84, dentro=(1, 0)); ameias('pedra', 31.8, 6, 31.8, 3, ZC + 84, dentro=(-1, 0))
arco('pedra', 'y', 1, 3, 43.2, 43.8, ZC, 26, 16, ZC + 84); ameias('pedra', 43.2, 1, 43.2, 3, ZC + 84, dentro=(1, 0)); ameias('pedra', 43.8, 3, 43.8, 1, ZC + 84, dentro=(-1, 0))
# portas velhas da vila (resto da muralha antiga, pedra rústica): choke do B, entrada do lavatoio, portas do mercado
arco('pedra', 'x', 8, 12, 21.15, 21.85, 10, 28, 22, 10 + 68); ameias('pedra', 8, 21.85, 12, 21.85, 78, dentro=(0, -1)); ameias('pedra', 12, 21.15, 8, 21.15, 78, dentro=(0, 1))
# estandartes nos portões e no mastio
for x in (32.6, 35.4):
    poe("Banner_2_Cloth", x, 8.87, ZC + 60, 0, hx(0xA3202C), esc=1.25, pasta=FP, forca=0.8)
for x in (36.0, 38.0):
    poe("Banner_1_Cloth", x, 0.03, ZC + 170, 0, hx(0xA3202C), esc=2.0, pasta=FP, forca=0.8)

# ====================================================================================================== igreja + campanário
TV = (1, 1, 1)
caixa('trav', 47, 3, ZC - 6, 51, 8, ZC + 62, TV)                                         # nave
poe("Roof_RoundTiles_8x10", 49, 5.5, ZC + 62, 0, (1.0, 0.95, 0.9), esc=RXY, zesc=ZR / RXY)
ap = ZC + 62 + CUME_M[4] * 12.5 * ZR
for yy in (2.98, 8.02):
    quad('trav', [(47, yy, ZC + 61), (51, yy, ZC + 61), (49, yy, ap), (49, yy, ap)], TV)
caixa('trav', 46.9, 7.95, ZC + 58, 51.1, 8.12, ZC + 62, (1.05, 1.04, 1.0))                # cimalha da fachada
for x in (47.1, 48.3, 49.7, 50.9):                                                         # lesenas
    caixa('trav', x - 0.13, 8.0, ZC + 6, x + 0.13, 8.1, ZC + 58, (1.04, 1.03, 1.0))
caixa('liso', 48.45, 8.0, ZC + 6, 49.55, 8.02, ZC + 52, hx(0x3A2A1E))
poe("DoorFrame_Round_Brick", 49, 8.04, ZC + 6, 0, esc=1.5); poe("Door_8_Round", 49 - 0.4, 8.045, ZC + 6, 0, hx(0x6A4A2E), esc=1.5)
poe("Window_Wide_Round1", 49, 8.03, ZC + 44, 0, hx(0x5A3A24), esc=1.2)
for (xa, xb) in ((45, 47), (51, 53)):                                                      # naves laterais
    caixa('trav', xa, 4, ZC - 6, xb, 8, ZC + 36, TV)
    poe("Roof_RoundTiles_4x8", (xa + xb) / 2, 6, ZC + 36, 0, (1.0, 0.95, 0.9), esc=RXY, zesc=ZR / RXY)
    for yy in (3.98, 8.02):
        quad('trav', [(xa, yy, ZC + 35), (xb, yy, ZC + 35), ((xa + xb) / 2, yy, ZC + 36 + CUME_M[2] * 12.5 * ZR), ((xa + xb) / 2, yy, ZC + 36 + CUME_M[2] * 12.5 * ZR)], TV)
    caixa('trav', xa, 3, ZC - 6, xb, 4, ZC + 28, TV)
    poe("Window_Thin_Round1", (xa + xb) / 2, 8.02, ZC + 2, 0, hx(0x5A3A24), esc=1.0)
caixa('rustica', 44, 3, ZC - 6, 45, 8, ZC + 30, topo='trav')                                # muro do jardim da igreja
caixa('trav', 53, 5, ZC - 6, 55, 8, ZC + 34, TV)                                           # sacristia
poe("Roof_RoundTiles_4x6", 54, 6.5, ZC + 34, 0, (1.0, 0.95, 0.9), esc=RXY, zesc=ZR / RXY)
for yy in (4.98, 8.02):
    quad('trav', [(53, yy, ZC + 33), (55, yy, ZC + 33), (54, yy, ZC + 34 + CUME_M[2] * 12.5 * ZR), (54, yy, ZC + 34 + CUME_M[2] * 12.5 * ZR)], TV)
# campanário
CTOP = ZC + 150
caixa('trav', 53, 3, ZC - 6, 55, 5, CTOP, (0.98, 0.95, 0.9))
for zz in (ZC + 52, ZC + 100, CTOP - 3):
    caixa('trav', 52.94, 2.94, zz, 55.06, 5.06, zz + 3, (1.08, 1.06, 1.02))
for (px_, py_, ex, ey) in ((54, 5.01, 0.22, 0), (54, 2.99, 0.22, 0), (55.01, 4, 0, 0.22), (52.99, 4, 0, 0.22)):   # sineira
    for s_ in (-0.4, 0.4):
        qx, qy = px_ + (s_ if ex else 0), py_ + (s_ if ey else 0)
        quad('liso', [(qx - ex, qy - ey, CTOP - 34), (qx + ex, qy + ey, CTOP - 34), (qx + ex, qy + ey, CTOP - 10), (qx - ex, qy - ey, CTOP - 10)], hx(0x231C18))
poe("Roof_Tower_RoundTiles", 54, 4, CTOP + 3, 0, (1.0, 0.95, 0.9), esc=0.86, zesc=0.8)

OCRE = hx(0xF0C883, 1.32)
# ====================================================================================================== palazzo e loggia (2º andar do TR, aberto por cima)
for (cx, cy), k in PECA_DE.items():
    lt = M.P[k][1]
    if lt not in 'k~': continue
    z = zpeca(k, cx + .5, cy + .5)
    cast = M.P[k][0] == "Sacada"          # sacada do capitão: é parte do castelo (era um bloco de reboco amarelo com piso de barro colado na muralha)
    mw, cw, mp = ('pedra', CP, 'pedra') if cast else ('gesso', OCRE, 'cotto')
    quad(mp, [(cx, cy + 1, z), (cx + 1, cy + 1, z), (cx + 1, cy, z), (cx, cy, z)], (0.93, 0.92, 0.9) if cast else BR)
    for lado, (dx, dy) in VIZ4.items():
        v = (cx + dx, cy + dy); lv = letra(*v) if (0 <= v[0] < W and 0 <= v[1] < H) else '#'
        if lv in 'k~': continue
        zb = zchao(*v) if lv != '#' else zperto(cx, cy) - 8
        (ax, ay), (bx, by) = {'S': ((cx, cy + 1), (cx + 1, cy + 1)), 'N': ((cx + 1, cy), (cx, cy)), 'L': ((cx + 1, cy + 1), (cx + 1, cy)), 'O': ((cx, cy), (cx, cy + 1))}[lado]
        quad(mw, [(ax, ay, zb), (bx, by, zb), (bx, by, z), (ax, ay, z)], cw)
        if cast:
            if lv != '#':          # cordão de pedra na altura do piso, igual ao do resto do castelo
                e = 0.05
                caixa('pedra', min(ax, bx) - (e if dx < 0 else 0), min(ay, by) - (e if dy < 0 else 0), z - 2.5,
                      max(ax, bx) + (e if dx > 0 else 0), max(ay, by) + (e if dy > 0 else 0), z, (1.06, 1.05, 1.02))
            continue
        quad('rustica', [(ax + dx * .015, ay + dy * .015, zb), (bx + dx * .015, by + dy * .015, zb), (bx + dx * .015, by + dy * .015, zb + 7), (ax + dx * .015, ay + dy * .015, zb + 7)], (0.9, 0.88, 0.84))
        e = 0.2          # mureta
        if lv == 's' or (lt == '~' and lv != '#'): continue          # entrada pela escada e beira do pulo: abertas
        if lado == 'S': caixa('gesso', cx, cy + 1 - e, z, cx + 1, cy + 1, z + 11, OCRE, topo='trav', cor_topo=(1.04, 1.03, 1.0))
        if lado == 'N': caixa('gesso', cx, cy, z, cx + 1, cy + e, z + 11, OCRE, topo='trav', cor_topo=(1.04, 1.03, 1.0))
        if lado == 'L': caixa('gesso', cx + 1 - e, cy, z, cx + 1, cy + 1, z + 11, OCRE, topo='trav', cor_topo=(1.04, 1.03, 1.0))
        if lado == 'O': caixa('gesso', cx, cy, z, cx + e, cy + 1, z + 11, OCRE, topo='trav', cor_topo=(1.04, 1.03, 1.0))
EC, EB = 1.2, 1.25          # caixote e barril um pouco maiores que o padrão do pack (iguais no mapa inteiro)


def pilha(x0, y0, x1, y1, z, niveis, nome="Crate_Wooden", seed=0):
    """caixotes do pack (0,5 casa cada) enchendo o retângulo; o andar de cima fica incompleto de propósito."""
    nx, ny = max(1, int(round((x1 - x0) / 0.5))), max(1, int(round((y1 - y0) / 0.5)))
    for nv in range(niveis):
        for a_ in range(nx):
            for b_ in range(ny):
                if nv > 0 and nx * ny > 2 and h32(a_, b_, seed) % 4 == 0: continue
                poe(nome if h32(a_, b_, nv, seed) % 5 else "Crate_Wooden", x0 + (a_ + .5) * (x1 - x0) / nx, y0 + (b_ + .5) * (y1 - y0) / ny, z + nv * 11.6 * EC,
                    (h32(a_, b_, nv, seed) % 5 - 2) * 4.0 + (90 if h32(a_, nv, seed) % 2 else 0), esc=EC, pasta=FP)


def barris(x0, y0, x1, y1, z, niveis, seed=0):
    d = 0.7 * EB * MT
    nx, ny = max(1, int(round((x1 - x0) / d))), max(1, int(round((y1 - y0) / d)))
    for nv in range(niveis):
        for a_ in range(nx - nv):
            for b_ in range(ny):
                if nv > 0 and ny > 1 and b_ == ny - 1: continue
                poe("Barrel", x0 + (a_ + .5 + nv * .5) * (x1 - x0) / nx, y0 + (b_ + .5) * (y1 - y0) / ny, z + nv * 11.25 * EB, 40.0 * h32(a_, b_, nv, seed) % 360, esc=EB, pasta=FP)


def banca_pack(x0, y0, x1, y1, z, frente_sul, seed=0):
    """Stall_Empty lado a lado; a frente (balcão) vira pra praça."""
    lg = x1 - x0; n = max(1, int(lg / 1.02 + 0.2)); cy = (y0 + y1) / 2
    for m in range(n):
        xs = x0 + (m + .5) * lg / n
        poe("Stall_Empty", xs, cy, z, 0 if frente_sul else 180, PANO[(seed + m) % 5], esc=1.15, pasta=FP, forca=0.8)
        yf = (y1 + 0.28) if frente_sul else (y0 - 0.28)
        poe(["FarmCrate_Apple", "FarmCrate_Carrot", "FarmCrate_Empty"][h32(m, seed) % 3], xs - 0.22, yf, z, 8.0 * (h32(m, seed) % 5 - 2), pasta=FP)
        if h32(m, seed, 3) % 2: poe("FarmCrate_Carrot", xs + 0.25, yf + (0.05 if frente_sul else -0.05), z, -10, pasta=FP)


for q, (nome, poli, alt, tipo) in enumerate(M.COB):
    xs = [p[0] for p in poli]; ys = [p[1] for p in poli]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys); cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    z = zchao(int(cx), int(cy)) or 0.0
    lg, pf = x1 - x0, y1 - y0
    if tipo == 'barrels':
        barris(x0, y0, x1, y1, z, 2 if alt >= 22 else 1, q)
    elif tipo == 'racks':            # cavaletes de tonel em dois andares, ao longo do lado maior
        n_ = max(1, int(round(max(lg, pf) / 0.68)))
        for m in range(n_):
            for nv in range(2):
                if lg >= pf: poe("Barrel_Holder", x0 + (m + .5) * lg / n_, cy, z + nv * 15.7, 0, pasta=FP)
                else: poe("Barrel_Holder", cx, y0 + (m + .5) * pf / n_, z + nv * 15.7, 90, pasta=FP)
    elif tipo in ('stack', 'stackm'):
        pilha(x0, y0, x1, y1, z, 2, "Crate_Metal" if tipo == 'stackm' else "Crate_Wooden", q)
    elif tipo == 'crate':
        pilha(x0, y0, x1, y1, z, 1, "Crate_Wooden", q)
    elif tipo == 'wagon':
        if lg >= pf: poe("Prop_Wagon", x0 + 0.44, cy, z, 90, CARVALHO)
        else: poe("Prop_Wagon", cx, y1 - 0.44, z, 0, CARVALHO)
    elif tipo == 'stall':
        banca_pack(x0, y0, x1, y1, z, cy < 43.5, q)
    elif tipo == 'kiosk':
        poe("Stall_Empty", cx, cy - 0.27, z, 180, PANO[1], esc=1.15, pasta=FP, forca=0.8); poe("Stall_Empty", cx, cy + 0.27, z, 0, PANO[2], esc=1.15, pasta=FP, forca=0.8)
        poe("FarmCrate_Apple", cx - 0.2, y1 + 0.25, z, 6, pasta=FP); poe("Barrel_Apples", x1 + 0.28, cy + 0.2, z, 0, pasta=FP)
    elif tipo == 'stallcart':
        poe("Stall_Cart_Empty", cx + 0.34, cy, z, 0, PANO[3], esc=1.15, pasta=FP, forca=0.8)
    elif tipo == 'workbench':
        poe("Workbench", cx, cy, z, 0 if lg >= pf else 90, esc=1.25, pasta=FP); poe("Bucket_Wooden_1", x1 + 0.2, cy + 0.1, z, 0, esc=1.25, pasta=FP)
    elif tipo == 'weaponstand':
        poe("WeaponStand", cx, cy, z, 0 if lg >= pf else 90, esc=1.15, pasta=FP)
    elif tipo == 'halfwall':
        caixa('rustica', x0, y0, z, x1, y1, z + alt - 1.5); caixa('trav', x0 - 0.04, y0 - 0.04, z + alt - 1.5, x1 + 0.04, y1 + 0.04, z + alt, (1.04, 1.03, 1.0))
    elif tipo == 'pillar':           # contraforte de cantaria, mesma pedra da muralha
        caixa('pedra', x0, y0, z, x1, y1, z + alt); caixa('pedra', x0 - 0.05, y0 - 0.05, z, x1 + 0.05, y1 + 0.05, z + 5, (0.95, 0.94, 0.92))
        caixa('pedra', x0 - 0.05, y0 - 0.05, z + alt, x1 + 0.05, y1 + 0.05, z + alt + 3, (1.06, 1.05, 1.02))

# ====================================================================================================== enfeites (poucos, todos do pack, nenhum no caminho)
for (x, y, g) in ((25.0, 40.5, 270), (39.0, 40.4, 90), (25.0, 46.5, 270), (39.0, 46.4, 90), (32.0, 8.88, 0), (36.0, 8.88, 0)):       # lampiões de parede
    poe("Lantern_Wall", x, y, (ZC if y < 20 else 0) + 24, g, pasta=FP)
for x in (45.7, 52.3):                                                                           # bancos na frente da igreja
    poe("Bench", x, 8.3, 24, 0, pasta=FP)
# bandeirinhas do mercado: todo fio sai de um mastro de madeira preso na quina de uma casa (usuário: "algumas estão voando e
# não prendem em lugar nenhum") — as duas diagonais e os dois fios de beira, de mastro a mastro
# mastro sempre por fora do cunhal de pedra da quina (no meio da parede ele entrava numa janela)
MASTROS = {'no': (25.26, 41.26), 'ne': (38.74, 41.26), 'so': (25.26, 44.74), 'se': (38.74, 44.74)}          # livre do beiral (0,17)
for (mx_, my_) in MASTROS.values():
    cilindro('trav', mx_, my_, 0, 0.05, 64.5, hx(0x6B4A2E), lados=8)
    cilindro('trav', mx_, my_, 64.5, 0.075, 1.6, hx(0x4A3320), lados=8)
for (ka, kb) in (('no', 'se'), ('so', 'ne'), ('no', 'ne'), ('so', 'se')):
    (xa, ya), (xb, yb) = MASTROS[ka], MASTROS[kb]
    L_ = math.hypot(xb - xa, yb - ya); n_ = int(L_ / 0.42)
    for m in range(n_):
        t = (m + .5) / n_; x_ = xa + (xb - xa) * t; y_ = ya + (yb - ya) * t; z_ = 62 - 26 * t * (1 - t)
        ux, uy = (xb - xa) / L_ * 0.15, (yb - ya) / L_ * 0.15
        quad('liso', [(x_ - ux, y_ - uy, z_), (x_ + ux, y_ + uy, z_), (x_, y_, z_ - 6.5), (x_, y_, z_ - 6.5)], PANO[m % 5])
    for m in range(12):
        t0, t1 = m / 12, (m + 1) / 12
        quad('liso', [(xa + (xb - xa) * t0, ya + (yb - ya) * t0, 62 - 26 * t0 * (1 - t0)), (xa + (xb - xa) * t1, ya + (yb - ya) * t1, 62 - 26 * t1 * (1 - t1)),
                      (xa + (xb - xa) * t1, ya + (yb - ya) * t1, 62.4 - 26 * t1 * (1 - t1)), (xa + (xb - xa) * t0, ya + (yb - ya) * t0, 62.4 - 26 * t0 * (1 - t0))], hx(0x4A423A))
for (x, y) in ((41.5, 8.5), (54.5, 18.5), (41.5, 19.5), (56.3, 11.4)):                           # pinheiros na praça da igreja
    planta("Pine_4", x, y, 18, 58)
for (x, y) in ((24.5, 9.5), (24.5, 13.5), (35.5, 13.5)):                                         # árvores no belvedere
    planta("CommonTree_3", x, y, 18, 44)
poe("Dummy", 26.2, 10.2, ZC, 25, pasta=FP); poe("Dummy", 26.9, 11.3, ZC, -15, pasta=FP)          # bonecos de treino da guarda, no canto do belvedere

# ====================================================================================================== fundo: campo, ciprestes no morro
quad('grama', [(-200, 200, -10.2), (260, 200, -10.2), (260, -200, -10.2), (-200, -200, -10.2)], hx(0x7C9C48))
r2 = np.random.default_rng(77)
for _ in range(150):
    x, y = float(r2.uniform(-9, W + 9)), float(r2.uniform(-13, -2.6))
    if 33 < x < 41 and y > -6.5: continue
    planta(["Pine_1", "Pine_2", "Pine_3", "Pine_5", "Pine_4", "CommonTree_4"][int(r2.integers(6))], x, y, -10, float(r2.uniform(60, 120)))
for _ in range(90):
    lado = int(r2.integers(3))
    x, y = (float(r2.uniform(-12, -5.6)), float(r2.uniform(-2, H + 10))) if lado == 0 else (float(r2.uniform(W + 5.6, W + 12)), float(r2.uniform(-2, H + 10))) if lado == 1 else (float(r2.uniform(-12, W + 12)), float(r2.uniform(H + 5.6, H + 11)))
    planta(["CommonTree_1", "CommonTree_3", "CommonTree_5", "CommonTree_2", "Pine_2"][int(r2.integers(5))], x, y, -10, float(r2.uniform(55, 100)))

# ====================================================================================================== malhas geradas
for nome, (vs, cs) in ACC.items():
    if not vs: continue
    co = np.array(vs, np.float32).reshape(-1, 3); n = len(vs)
    me = bpy.data.meshes.new(nome)
    me.vertices.add(n * 4); me.vertices.foreach_set("co", co.ravel())
    me.loops.add(n * 4); me.loops.foreach_set("vertex_index", np.arange(n * 4, dtype=np.int32))
    me.polygons.add(n); me.polygons.foreach_set("loop_start", np.arange(0, n * 4, 4, dtype=np.int32))
    me.update(calc_edges=True)
    at = me.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    rgba = np.ones((n, 4, 4), np.float32); rgba[:, :, :3] = np.array(cs, np.float32)[:, None, :]
    at.data.foreach_set("color", rgba.ravel())
    me.materials.append(MATG[nome])
    cena.collection.objects.link(bpy.data.objects.new("g_" + nome, me))
print("objetos", len(cena.collection.objects), "peças", len(PECAS))
json.dump({"U": 25, "casa_m": 2, "ZR": ZR, "pecas": PECAS,
           "casas": [{k: (list(v) if isinstance(v, tuple) else v) for k, v in c.items()} for c in CASAS]},
          open(SAIDA + "_pecas.json", "w", encoding="utf-8"), separators=(',', ':'))

# ====================================================================================================== luz, céu, câmeras
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), math.radians(4), math.radians(-32)))
sol = bpy.context.object.data; sol.energy = 3.0; sol.color = (1.0, 0.94, 0.84); sol.angle = math.radians(7.0)
wd = bpy.data.worlds.new("w"); cena.world = wd; wd.use_nodes = True
wd.node_tree.nodes["Background"].inputs[0].default_value = (0.80, 0.84, 1.0, 1); wd.node_tree.nodes["Background"].inputs[1].default_value = 0.78
cena.render.engine = 'BLENDER_EEVEE'
cena.view_settings.view_transform = 'Standard'
try:
    cena.view_settings.look = 'Medium High Contrast'
except Exception:
    pass
for _k, _v in (('use_raytracing', True), ('use_gtao', True), ('use_soft_shadows', True)):
    try: setattr(cena.eevee, _k, _v)
    except Exception: pass
try:
    cena.eevee.use_shadows = True
except Exception:
    pass
CAMS = {
    'topo': ((W / 2 + 24, -H / 2 - 36, 60), (W / 2, -H / 2 + 1, 0), 38, (1800, 1350)),
    'jogo': ((W / 2, -H / 2 - 64, 76.3), (W / 2, -H / 2, 0), 62, (1800, 1500)),      # elevação 50° olhando do sul, como a câmera do jogo
    'tr': ((41.5, -31, 13), (32, -43.6, 0.4), 30, (1600, 1000)),
    'ct': ((47, -23, 21), (37.5, -4, 1.4), 30, (1600, 1000)),
    'trans': ((20, -27, 17), (33, -8, 1.2), 32, (1600, 1000)),                         # da vila pro castelo (transição)
    'a': ((60, -33, 17), (48, -14, 1), 30, (1600, 1000)),
    'b': ((21, -29, 17), (9.5, -13, 0.5), 30, (1600, 1000)),
    'meio': ((43, -44, 17), (30, -24, 0.8), 28, (1600, 1000)),
    'rua': ((33.5, -47.5, 3.2), (30, -36, 1.2), 22, (1600, 1000)),
    'z_poco': ((50.5, -19.5, 5.5), (47.5, -14, 1.2), 35, (1100, 800)),
    'z_casa': ((42.5, -15.5, 6), (38.5, -10, 1.6), 35, (1100, 800)),
    'z_arco': ((27.5, -47.5, 5), (24.5, -43, 1.0), 30, (1100, 800)),
    'z_porta': ((13.5, -27.5, 6), (10, -21.5, 1.4), 30, (1100, 800)),
    'z_cast': ((34, -15, 6), (34, -8, 2.6), 28, (1100, 800)),
    'z_b': ((13.5, -19, 7.5), (8, -12, 0.8), 30, (1100, 800)),
    'z_merc': ((33, -40.5, 6.5), (31.5, -46.6, 0.6), 30, (1100, 800)),
    'z_a': ((52.5, -20.5, 6.5), (48.5, -14.5, 1.2), 30, (1100, 800)),
    'z_terr': ((62, -40, 13), (56, -27, 1.6), 30, (1100, 800)),
    'z_teto': ((27, -40, 9), (22, -33, 1.5), 30, (1100, 800)),
    'z_quad': ((28.5, -47.5, 13), (25, -37.5, 1.6), 35, (1100, 800)),                    # quadra ao lado da escada do mercado (print do usuário)
    'z_quad2': ((20, -46, 15), (25, -37, 1.6), 35, (1100, 800)),
    'z_torre': ((42, -46, 14), (39.5, -37.5, 2.4), 35, (1100, 800)),
    'z_band': ((32, -57, 16), (32, -43.5, 1.2), 30, (1300, 800)),
    'z_band2': ((29, -49.5, 6), (25.2, -41, 1.6), 35, (1100, 800)),
    'z_cunhal': ((29.2, -42.6, 3.2), (27, -40, 0.9), 35, (1100, 800)),
    'z_cunhal2': ((27, -44.5, 6.5), (27, -40, 0.8), 50, (1100, 800)),
    'z_beiral': ((27, -49.5, 7), (22, -39.5, 1.8), 35, (1100, 800)),
    'z_eira': ((27.3, -43.6, 3.6), (24.6, -40.6, 1.9), 40, (1100, 800)),
    'z_eira2': ((20.5, -43.8, 4.4), (22.6, -40.4, 1.9), 40, (1100, 800)),
    'z_sacada': ((31, -16, 9), (28, -8, 2.2), 35, (1100, 800)),
    'z_pedra': ((50, -17, 8), (46.5, -11, 1.8), 35, (1100, 800)),
    'z_belv': ((33.5, -16, 6), (29.5, -10.8, 1.2), 30, (1100, 800)),                     # de perto, saindo do mercado pra scalinata
}
for v in VISTAS:
    loc, alvo, lente, res = CAMS[v]
    bpy.ops.object.camera_add(location=loc); cam = bpy.context.object; cena.camera = cam; cam.data.lens = lente
    dd = Vector(alvo) - cam.location; cam.rotation_euler = dd.to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_end = 600
    cena.render.resolution_x, cena.render.resolution_y = int(res[0] * RES), int(res[1] * RES)
    cena.render.filepath = f"{SAIDA}_{v}.png"
    bpy.ops.render.render(write_still=True)
    print("ok", cena.render.filepath)
