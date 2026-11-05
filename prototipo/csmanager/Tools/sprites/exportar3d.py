# Bonecos 3D pro visual de maquete (Unity): os MESMOS agentes do agentes.py (peças, armas autorais, poses
# procedurais), mas em vez de renderizar sprite grava a malha de cada peça (cor por vértice) e, por animação, a matriz
# de cada peça em cada quadro (30 qps, duração real do CS2 x 0,9). O Unity monta o boneco com uma peça por Transform e
# toca as matrizes interpolando (Assets/Game/Visual/Boneco3D.cs).
#   blender -b --factory-startup --python exportar3d.py -- <saida> <ct|tr>
# Saída: <saida>/boneco_<time>.json (cabeçalho) + boneco_<time>.bin (float32: malhas e quadros).
# Coordenadas gravadas como no Blender (X frente, Y esquerda, Z cima); a troca pro Unity é no C#.
import bpy, sys, os, math, json, struct
import numpy as np

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGV[0] if ARGV else r"D:\unity\CSManager\Logs\bonecos3d"
TEAM = ARGV[1] if len(ARGV) > 1 else "ct"
FPS3 = 30
AQUI = os.path.dirname(os.path.abspath(__file__))
os.makedirs(OUT, exist_ok=True)

# roda o agentes.py sem nenhum grupo de render: só monta o boneco do time (classe Agent, armas, poses)
g = {"__name__": "agentes_lib", "__file__": os.path.join(AQUI, "agentes.py")}
argv0 = sys.argv
sys.argv = [argv0[0], "--", os.path.join(OUT, "_lixo"), TEAM, "nenhum"]
with open(os.path.join(AQUI, "agentes.py"), encoding="utf-8") as fh:
    fonte = fh.read()
# andar devagar (shift) no 3D: passada maior que a dos sprites (com 0,075 o ciclo andava só 7,5 unidades e a perna
# girava ~4x por segundo na velocidade de shift do jogo); 0,12 = 12 unidades por ciclo, ~2,5 passos/s
ANTES = '"devagar": (0.075, 0.035, 0.006)'
assert ANTES in fonte, "GAITS do agentes.py mudou: revisar o patch do devagar"
fonte = fonte.replace(ANTES, '"devagar": (0.12, 0.045, 0.008)')
exec(compile(fonte, "agentes.py", "exec"), g)
sys.argv = argv0
A = g["A"]; WEAP = g["WEAP"]; RELOAD_S = g["RELOAD_S"]; SHOT_S = g["SHOT_S"]
A.root.rotation_euler = (0, 0, 0)
for w in WEAP: A.gun(w)          # todas as armas existem (o boneco pode pegar qualquer uma do chão)
bpy.context.view_layer.update()

# ------------------------------------------------------------------ peças: tudo que é malha embaixo do boneco
def descende(o, raiz):
    while o is not None:
        if o == raiz: return True
        o = o.parent
    return False

PECAS = [o for o in bpy.data.objects if o.type == "MESH" and descende(o, A.root)]
PECAS.sort(key=lambda o: o.name)
print("PECAS", len(PECAS))

_img = {}
def pixels(im):
    if im.name not in _img:
        w, h = im.size
        a = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(a)
        a = a.reshape(h, w, 4)[:, :, :3]
        if im.colorspace_settings.name.lower().startswith("srgb"):
            a = np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)
        _img[im.name] = a
    return _img[im.name]

def info_mat(m):
    """(cor linear fixa, imagem, brilha?) do material de desenho/brilho do agentes.py."""
    if m is None or not m.node_tree: return (0.8, 0.8, 0.8), None, False
    nt = m.node_tree
    im = next((n.image for n in nt.nodes if n.type == "TEX_IMAGE" and n.image), None)
    rgb = next((n for n in nt.nodes if n.type == "RGB"), None)
    em = next((n for n in nt.nodes if n.type == "EMISSION"), None)
    brilha = rgb is None and im is None and em is not None
    if rgb is not None: c = tuple(rgb.outputs[0].default_value[:3])
    elif brilha: c = tuple(em.inputs[0].default_value[:3])
    else: c = (0.8, 0.8, 0.8)
    # pintura por cima da textura (AWP verde, ferrolho da Glock): tom da pintura
    pint = next((n for n in nt.nodes if n.type == "MIX_RGB" and n.blend_type == "MULTIPLY" and not n.inputs[1].is_linked), None)
    if im is not None and pint is not None:
        # região pintada (coordenadas do objeto): x < xmax, zmin < z < zmax, como no toon(paint=)
        lt = [n.inputs[1].default_value for n in nt.nodes if n.type == "MATH" and n.operation == "LESS_THAN"]
        gt = [n.inputs[1].default_value for n in nt.nodes if n.type == "MATH" and n.operation == "GREATER_THAN"]
        reg = (lt[0] if lt else 99, lt[1] if len(lt) > 1 else 99, gt[0] if gt else -99)
        c = tuple(pint.inputs[1].default_value[:3]); im = ("pint", im, reg)
    return c, im, brilha

def malha(o):
    """Malha avaliada (com bevel, sem o contorno) em coordenadas locais da peça, cor por canto de face."""
    mods = [md for md in o.modifiers if md.type == "SOLIDIFY"]
    for md in mods: md.show_viewport = False
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data if me.uv_layers.active else None
    mats = [info_mat(s.material) for s in o.material_slots] or [info_mat(None)]
    try: ln = [l.normal.copy() for l in me.loops]   # 4.1+: normais de canto prontas
    except Exception:
        me.calc_normals_split(); ln = [l.normal.copy() for l in me.loops]
    V, N, C, T = [], [], [], []
    brilho = False
    for tri in me.loop_triangles:
        mi = min(tri.material_index, len(mats) - 1)
        c0, im, br = mats[mi]
        if mi < len(o.material_slots) and o.material_slots[mi].material and o.material_slots[mi].material.name == "contorno":
            continue
        brilho = brilho or br
        for li in tri.loops:
            vi = me.loops[li].vertex_index
            co = me.vertices[vi].co
            c = c0
            if im is not None and uv is not None:
                pint = isinstance(im, tuple); img = im[1] if pint else im
                px = pixels(img); h, w = px.shape[:2]
                u, v = uv[li].uv
                s = px[min(h - 1, max(0, int((v % 1.0) * h))), min(w - 1, max(0, int((u % 1.0) * w)))]
                if pint:   # mesma conta do toon(paint=): claridade x pintura
                    bw = float(0.2126 * s[0] + 0.7152 * s[1] + 0.0722 * s[2])
                    k = min(max(bw * 1.8 + 0.42, 0), 1)
                    xm, zm, zn = im[2]
                    c = tuple(ci * k for ci in c0) if (co.x < xm and zn < co.z < zm) else tuple(float(x) for x in s)
                else: c = tuple(float(x) for x in s)
            T.append(len(V)); V.append(tuple(co)); N.append(tuple(ln[li])); C.append(tuple(c))
    ev.to_mesh_clear()
    for md in mods: md.show_viewport = True
    return V, N, C, T, brilho

# ------------------------------------------------------------------ animações
padrao = "M4A4" if TEAM == "ct" else "AK47"
def nq(seg, minimo=2): return max(minimo, round(seg * FPS3))
ANIMS = []   # (nome, anim, arma, n, loop, kw)
for w in WEAP:
    ANIMS += [(f"mira_{w}", "mira", w, 1, True, {}),
              (f"andar_{w}", "andar", w, 24, True, {}),
              (f"devagar_{w}", "devagar", w, 30, True, {}),
              (f"agachado_{w}", "agachado", w, 30, True, {}),
              (f"tiro_{w}", "tiro", w, nq(SHOT_S[w], 4) if w != "AWP" else nq(1.1), False, {}),
              (f"recarga_{w}", "recarga", w, nq(RELOAD_S[w]), False, {}),
              (f"sacar_{w}", "sacar", w, 12, False, {}),
              *([(f"sacar_costas_{w}", "sacar_costas", w, 20, False, {}),
                 # referência da primária pendurada nas costas (o Unity prende no tronco quando ele está de pistola)
                 (f"costas_{w}", "faca_parado", w, 1, True, {})] if g["WEAP"][w]["kind"] != "pistol" else []),
              (f"no_ar_sobe_{w}", "no_ar_sobe", w, 1, True, {}),
              (f"no_ar_cai_{w}", "no_ar_cai", w, 1, True, {}),
              (f"agachar_{w}", "agachar", w, 1, True, {}),
              # mira pra cima/baixo (25°): o Unity soma a diferença pra mira reta no tronco/braços/arma/cabeça
              (f"pitch_cima_{w}", "mira", w, 1, True, {"pitch": math.radians(60)}),
              (f"pitch_baixo_{w}", "mira", w, 1, True, {"pitch": math.radians(-60)}),
              # pegar/largar do chão, call apontando, comemorar e lamentar o fim do round (com a arma da vez)
              (f"pegar_{w}", "pegar", w, 20, False, {}),
              (f"apontar_{w}", "apontar", w, 24, False, {}),
              (f"comemorar_{w}", "comemorar", w, 24, True, {}),
              (f"lamentar_{w}", "lamentar", w, 1, True, {}),
              # cego (arma caída, mão nos olhos, estrelinhas) e meio cego (aba na testa, arma na frente) com a arma da vez
              (f"cego_{w}", "cego", w, 24, True, {}),
              (f"meio_cego_{w}", "meio_cego", w, 24, True, {})]
ANIMS += [("awp_mirar", "awp_mirar", "AWP", 8, False, {}), ("awp_mirando", "awp_mirando", "AWP", 1, True, {}),
          ("tras", "tras", padrao, 24, True, {}), ("lado_esq", "lado_esq", padrao, 24, True, {}),
          ("lado_dir", "lado_dir", padrao, 24, True, {}), ("pular", "pular", padrao, 18, False, {}),
          ("escada", "escada", padrao, 24, True, {}),
          ("faca_parado", "faca_parado", padrao, 1, True, {}), ("faca_andar", "faca_andar", padrao, 24, True, {}),
          ("faca_m1", "faca_m1", padrao, 12, False, {}), ("faca_m2", "faca_m2", padrao, 18, False, {}),
          ("sacar_faca", "sacar_faca", padrao, 12, False, {}),
          ("faca_no_ar_sobe", "faca_no_ar_sobe", padrao, 1, True, {}), ("faca_no_ar_cai", "faca_no_ar_cai", padrao, 1, True, {}),
          ("cego", "cego", padrao, 24, True, {}), ("morrer", "morrer", padrao, 30, False, {}),
          ("morrer_frente", "morrer_frente", padrao, 30, False, {}),
          ("plantar", "plantar", padrao, 60, False, {}), ("plantar_pe", "plantar_pe", padrao, 60, False, {}),
          ("desarmar", "desarmar", padrao, 30, True, {}), ("desarmar_pe", "desarmar_pe", padrao, 30, True, {})]
# jeito do CS2 (força do Sim): por cima (botão esquerdo, nome sem sufixo), meia força ("_meio") e por baixo ("_baixo")
for jt, suf in (("cima", ""), ("meio", "_meio"), ("baixo", "_baixo")):
    for nd in ("smoke", "flash", "molotov", "he"):
        ANIMS.append((f"granada_{nd}{suf}", "granada", padrao, 30, False, {"nade": nd, "jeito": jt}))
    ANIMS.append((f"granada_fim{suf}", "granada_fim", padrao, 12, False, {"jeito": jt}))   # continuação depois de soltar (o Sim solta em u = 1)

# só algumas animações (iterar rápido com o validar_anims.py): ANIM_FILTRO=regex; a mira de cada arma vai junto (referência)
if os.environ.get("ANIM_FILTRO"):
    import re
    _f = re.compile(os.environ["ANIM_FILTRO"])
    ANIMS = [a for a in ANIMS if _f.search(a[0]) or a[0].startswith("mira_")]

blob = bytearray()
def put(arr):
    off = len(blob) // 4
    blob.extend(struct.pack("<%df" % len(arr), *arr))
    return off

head = {"time": TEAM, "fps": FPS3, "pecas": [], "anims": [],
        # pontos de pegada de cada arma no espaço da peça (pro validador: mão direita no punho, esquerda no guarda-mão,
        # olho na ocular da luneta)
        "armas": {w: {"grip": list(A.gun(w).grip), "fore": list(A.gun(w).fore), "L": A.gun(w).L,
                      "pistola": WEAP[w]["kind"] == "pistol",
                      "ocular": list(A.gun(w).ocular) if A.gun(w).ocular is not None else None} for w in WEAP}}
for o in PECAS:
    V, N, C, T, br = malha(o)
    flat = [x for p in V for x in p]; nf = [x for p in N for x in p]; cf = [x for p in C for x in p]
    head["pecas"].append({"nome": o.name, "nv": len(V), "v": put(flat), "n": put(nf), "c": put(cf), "brilho": br})
print("MALHAS OK", len(blob) // 4)

LONGE = 20.0
for nome, anim, arma, n, loop, kw in ANIMS:
    try:
        quadros = []
        for fi in range(n):
            t = fi / n if loop else (fi / (n - 1) if n > 1 else 0.0)
            A.pose(t, anim, arma, **kw)
            bpy.context.view_layer.update()
            fr = []
            for o in PECAS:
                m = o.matrix_world
                if m.translation.length > LONGE: fr += [0.0] * 12        # escondida
                else: fr += [m[r][c] for r in range(3) for c in range(4)]
            quadros.append(put(fr))
        head["anims"].append({"nome": nome, "n": n, "loop": loop, "q": quadros[0] if quadros else 0})
        print("ANIM", nome, n)
    except Exception as e:
        print("FALHOU", nome, e)

with open(os.path.join(OUT, f"boneco_{TEAM}.bin"), "wb") as fh: fh.write(blob)
with open(os.path.join(OUT, f"boneco_{TEAM}.json"), "w", encoding="utf-8") as fh: json.dump(head, fh)
print("EXPORT OK", len(PECAS), "pecas", len(head["anims"]), "anims", len(blob) // 1024, "KB")
