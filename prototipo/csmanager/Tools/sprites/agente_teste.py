# Prova dos agentes em sprite: CT e TR chibi com rosto, AK autoral na mão (pente separado), luz/contorno de desenho,
# câmera igual à do jogo (elevação 50°, olhando de sudeste). Renderiza: 8 direções mirando, recarga e andar.
#   blender -b --factory-startup --python agente_teste.py -- <saida> [ct|tr|ambos]
# Coordenadas Blender: X = leste do jogo, Y = norte (= -y do jogo), Z cima. Boneco local: X frente, Y esquerda.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Quaternion

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGV[0] if ARGV else r"D:\unity\CSManager\Logs\sprites"
TEAMS = ["ct", "tr"] if len(ARGV) < 2 or ARGV[1] == "ambos" else [ARGV[1]]
ARMAS = r"D:\unity\EsportsPoc\Tools\autoral\armas_out"
RES = 192            # tela do sprite
PX_PER_BU = 140      # boneco de 1,0 de altura fica ~95 px na tela
os.makedirs(OUT, exist_ok=True)

# ------------------------------------------------------------------ cena
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        sc.render.engine = eng
        break
    except TypeError:
        pass
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = True
sc.render.filter_size = 0.6
sc.view_settings.view_transform = "Standard"
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGBA"

def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)

# câmera ortográfica do jogo
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = RES / PX_PER_BU
el = math.radians(50)
d = Vector((math.cos(el) / math.sqrt(2), -math.cos(el) / math.sqrt(2), math.sin(el)))
FOCO = Vector((0.08, 0, 0.5))
cam.location = FOCO + d * 10
cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
cam.data.clip_end = 40

# sol do jogo (Iso.SUN no sistema do jogo -> Blender: y troca de sinal) + céu fraco
sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", "SUN"))
sc.collection.objects.link(sun)
sd = Vector((-0.55, 0.28, 0.79)).normalized()
sun.rotation_euler = (-sd).to_track_quat("-Z", "Y").to_euler()
sun.data.energy = 3.2
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.52, 0.5, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

# ------------------------------------------------------------------ materiais de desenho (3 tons + contorno)
def toon(name, col, tex=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    el_ = ramp.color_ramp.elements
    el_[0].position = 0.0; el_[0].color = (0.58, 0.55, 0.6, 1)
    el_[1].position = 0.22; el_[1].color = (0.82, 0.8, 0.82, 1)
    e3 = el_.new(0.62); e3.color = (1, 1, 1, 1)
    mul = nt.nodes.new("ShaderNodeMixRGB"); mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(dif.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], mul.inputs[1])
    if tex:
        ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = bpy.data.images.load(tex)
        nt.links.new(ti.outputs[0], mul.inputs[2])
    else:
        mul.inputs[2].default_value = srgb(col)
    nt.links.new(mul.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    return m

OUTLINE = bpy.data.materials.new("contorno"); OUTLINE.use_nodes = True
OUTLINE.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = srgb("#1c1416")
OUTLINE.use_backface_culling = True
try:
    OUTLINE.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#1c1416")
except KeyError:
    pass

def outline(o, th=0.009):
    o.data.materials.append(OUTLINE)
    md = o.modifiers.new("contorno", "SOLIDIFY")
    md.thickness = th; md.offset = 1; md.use_flip_normals = True; md.use_rim = False
    md.material_offset = len(o.data.materials) - 1
    o["th"] = th

def fix_outline(o):
    """Contorno com espessura constante no mundo, apesar da escala da peça."""
    md = o.modifiers.get("contorno")
    if md:
        s = (abs(o.scale[0]) + abs(o.scale[1]) + abs(o.scale[2])) / 3
        md.thickness = o.get("th", 0.009) / max(s, 1e-4)
PARTS = []   # (obj, função de pose)
def smooth(o):
    for p in o.data.polygons: p.use_smooth = True

def ell(name, mat, r=(1, 1, 1), seg=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1)
    o = bpy.context.active_object; o.name = name; smooth(o)
    o.data.materials.append(mat); outline(o)
    o["r"] = r
    return o

def box(name, mat, half, bevel=0.01):
    bpy.ops.mesh.primitive_cube_add(size=2)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(mat)
    bv = o.modifiers.new("bv", "BEVEL"); bv.width = bevel / max(min(half), 1e-3); bv.segments = 2
    outline(o)
    o["r"] = half
    return o

# ------------------------------------------------------------------ arma autoral (3ª pessoa) com pente separado
def import_gun(name, L):
    antes = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ARMAS, name + "_3p", name + "_3p.fbx"))
    novos = [o for o in bpy.context.scene.objects if o not in antes]
    objs = [o for o in novos if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    g = bpy.context.active_object
    mw = g.matrix_world.copy(); g.parent = None; g.matrix_world = mw
    for o in novos:
        if o.type != "MESH" and o.name in bpy.data.objects: bpy.data.objects.remove(o)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = g.data
    vs = [v.co.copy() for v in me.vertices]
    mn = Vector([min(v[i] for v in vs) for i in range(3)]); mx = Vector([max(v[i] for v in vs) for i in range(3)])
    ext = mx - mn
    ax = max(range(3), key=lambda i: ext[i])                       # cano = eixo mais comprido
    rest = [i for i in range(3) if i != ax]
    up = max(rest, key=lambda i: ext[i])                           # altura = segundo maior (pente/punho)
    side = [i for i in rest if i != up][0]
    def slab(t0, t1):
        return [v for v in vs if t0 <= (v[ax] - mn[ax]) / ext[ax] <= t1]
    a, b = slab(0, 0.06), slab(0.94, 1)
    va = max(v[up] for v in a) - min(v[up] for v in a); vb = max(v[up] for v in b) - min(v[up] for v in b)
    sgn_ax = 1 if vb < va else -1                                  # boca = ponta mais fina
    muz = b if sgn_ax > 0 else a
    zc = sum(v[up] for v in muz) / len(muz)
    sgn_up = 1 if (mx[up] - zc) < (zc - mn[up]) else -1           # pra baixo é onde a arma desce mais (pente)
    # matriz: eixo da arma -> X, cima -> Z, coronha na origem, cano na altura 0
    M = Matrix.Identity(4)
    rows = []
    for tgt in range(3):
        src = [ax, side, up][tgt]; s = [sgn_ax, 1, sgn_up][tgt]
        row = [0, 0, 0]; row[src] = s; rows.append(row)
    R = Matrix(rows).to_4x4()
    me.transform(R)
    vs = [v.co.copy() for v in me.vertices]
    x0 = min(v.x for v in vs); x1 = max(v.x for v in vs)
    muz = [v for v in vs if v.x > x1 - 0.06 * (x1 - x0)]
    zc = sum(v.z for v in muz) / len(muz); yc = sum(v.y for v in vs) / len(vs)
    me.transform(Matrix.Translation((-x0, -yc, -zc)))
    k = L / (x1 - x0)
    me.transform(Matrix.Scale(k, 4))
    if R.to_3x3().determinant() < 0:   # espelhou: conserta normais
        me.flip_normals()
    g.name = name
    # material de desenho com a textura própria
    tex = os.path.join(ARMAS, name + "_3p", name + "_3p_albedo.png")
    g.data.materials.clear(); g.data.materials.append(toon(name, "#333333", tex)); outline(g, 0.006)
    return g

def split_mag(g, L, x0f, x1f):
    """Separa o pente: vértices na faixa x0f..x1f (fração do comprimento) abaixo da caixa da culatra."""
    me = g.data
    hand = [v.co.z for v in me.vertices if 0.62 * L <= v.co.x <= 0.70 * L]
    zcut = min(hand) - 0.004
    bm = bmesh.new(); bm.from_mesh(me)
    sel = set(v.index for v in bm.verts if x0f * L <= v.co.x <= x1f * L and v.co.z < zcut)
    for f in bm.faces: f.select = all(v.index in sel for v in f.verts)
    bm.to_mesh(me); bm.free()
    bpy.ops.object.select_all(action="DESELECT"); g.select_set(True); bpy.context.view_layer.objects.active = g
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
    mag = [o for o in bpy.context.selected_objects if o != g][0]; mag.name = g.name + "_pente"
    c = sum((v.co for v in mag.data.vertices), Vector()) / len(mag.data.vertices)
    top = max(v.co.z for v in mag.data.vertices)
    piv = Vector((c.x, 0, top))                                    # pivô na boca do pente
    mag.data.transform(Matrix.Translation(-piv)); mag["piv"] = piv
    return mag, zcut

# ------------------------------------------------------------------ boneco
def ik(s, h, l1, l2, pole):
    v = h - s; dd = min(v.length, l1 + l2 - 1e-3); u = v.normalized()
    a = (l1 * l1 - l2 * l2 + dd * dd) / (2 * dd); hh = math.sqrt(max(l1 * l1 - a * a, 0))
    p = pole - u * pole.dot(u); p = p.normalized() if p.length > 1e-6 else Vector((0, 0, -1))
    return s + u * a + p * hh, s + u * dd

def place_seg(o, a, b, r):
    """Elipsoide esticado entre a e b (membro)."""
    d = b - a; L = d.length
    o.location = (a + b) / 2
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.to_track_quat("Z", "Y")
    o.scale = (r, r, L / 2 + r * 0.6)
    fix_outline(o)

def place(o, c, r=None, q=None):
    o.location = c
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = q or Quaternion()
    rr = r or o["r"]; o.scale = rr
    fix_outline(o)

class Agent:
    def __init__(s, team):
        s.team = team; ct = team == "ct"
        C = {
            "skin": "#e2ad86" if ct else "#c68a62", "eye": "#1a1414", "white": "#ffffff", "brow": "#3a2a22",
            "suit": "#2f3c58" if ct else "#6c6e72", "pants": "#2f3c58" if ct else "#a8976e",
            "vest": "#5e6640" if ct else "#36373b", "glove": "#1c1c1f" if ct else "#5a3a22",
            "boot": "#1d1d20" if ct else "#4a3020", "helm": "#3b465c", "hair": "#2a2320", "black": "#1e1e22",
            "lens": "#5fc0d4", "brass": "#c9a24a", "leather": "#5c3b22", "bandana": "#5c2a26", "stub": "#8a5c40",
        }
        M = {k: toon(team + "_" + k, v) for k, v in C.items()}
        s.root = bpy.data.objects.new(team, None); sc.collection.objects.link(s.root)
        P = {}
        def add(o, key):
            o.parent = s.root; P[key] = o
        # pernas, quadril, cinto
        for sd in ("L", "R"):
            add(ell("coxa" + sd, M["pants"]), "coxa" + sd); add(ell("canela" + sd, M["pants"]), "canela" + sd)
            add(ell("bota" + sd, M["boot"]), "bota" + sd)
            if ct: add(ell("joelheira" + sd, M["black"]), "joelheira" + sd)
        add(ell("quadril", M["pants"]), "quadril"); add(ell("cinto", M["leather"] if not ct else M["black"]), "cinto")
        add(box("coldre", M["black"] if ct else M["leather"], (0.03, 0.02, 0.045)), "coldre")
        # tronco e colete
        add(ell("tronco", M["suit"]), "tronco")
        add(box("colete", M["vest"], (0.085, 0.12, 0.085), 0.02), "colete")
        if ct:
            for i in range(3): add(box("bolso%d" % i, M["vest"], (0.022, 0.03, 0.035), 0.008), "bolso%d" % i)
            add(box("radio", M["black"], (0.02, 0.018, 0.032), 0.006), "radio"); add(ell("antena", M["black"]), "antena")
            for sd in ("L", "R"): add(ell("ombreira" + sd, M["vest"]), "ombreira" + sd)
        else:
            for i in range(2): add(box("bolso%d" % i, M["vest"], (0.02, 0.028, 0.03), 0.008), "bolso%d" % i)
            add(ell("bandoleira", M["leather"]), "bandoleira")
            for i in range(6): add(ell("bala%d" % i, M["brass"]), "bala%d" % i)
            add(ell("faca", M["black"]), "faca")
            add(ell("bandana", M["bandana"]), "bandana")
        # cabeça
        add(ell("cabeca", M["skin"], seg=32), "cabeca")
        for sd in ("L", "R"):
            add(ell("olho" + sd, M["eye"]), "olho" + sd); add(ell("brilho" + sd, M["white"]), "brilho" + sd)
            add(box("sobrancelha" + sd, M["brow"], (0.008, 0.026, 0.006), 0.003), "sobr" + sd)
        if ct:
            add(ell("capacete", M["helm"], seg=32), "capacete")
            add(box("faixa", M["black"], (0.012, 0.085, 0.012), 0.004), "faixa")
            for sd in ("L", "R"):
                add(ell("oculos" + sd, M["black"]), "oculos" + sd); add(ell("lente" + sd, M["lens"]), "lente" + sd)
                add(ell("fone" + sd, M["black"]), "fone" + sd)
        else:
            add(ell("cabelo", M["hair"], seg=32), "cabelo"); add(ell("barba", M["stub"]), "barba")
            add(box("boca", M["eye"], (0.004, 0.022, 0.004), 0.002), "boca")
        # braços: manga, antebraço (TR pele), luva
        for sd in ("L", "R"):
            add(ell("braco" + sd, M["suit"]), "braco" + sd)
            add(ell("ante" + sd, M["suit"] if ct else M["skin"]), "ante" + sd)
            add(ell("mao" + sd, M["glove"]), "mao" + sd)
        s.P = P
        # arma
        s.L = 0.70
        s.gun = import_gun("AK47", s.L); s.gun.parent = s.root
        s.mag, _ = split_mag(s.gun, s.L, 0.425, 0.625); s.mag.parent = s.root
        s.mag.data.materials.clear(); s.mag.data.materials.append(s.gun.data.materials[0]); outline(s.mag, 0.006)
        s.mag2 = s.mag.copy(); s.mag2.data = s.mag.data; sc.collection.objects.link(s.mag2); s.mag2.parent = s.root
        vs = [v.co for v in s.gun.data.vertices]
        def mid(a, b):
            sl = [v for v in vs if a * s.L <= v.x <= b * s.L]
            return Vector((((a + b) / 2) * s.L, 0, (min(v.z for v in sl) + max(v.z for v in sl)) / 2))
        s.grip = mid(0.26, 0.33) + Vector((0, 0, -0.01))
        s.fore = mid(0.50, 0.57)
        s.bolt = Vector((0.40 * s.L, -0.03, 0.012))

    # ---------------------------------------------------------- pose: dicionário de pontos no espaço do boneco
    def pose(s, t, anim):
        P = s.P; ct = s.team == "ct"
        bob = 0; step = None
        if anim == "andar":
            ph = t * 2 * math.pi; bob = 0.012 * abs(math.sin(ph)); step = ph
        hipz = 0.30 + bob
        # pernas
        for sd, sg in (("L", 1), ("R", -1)):
            hip = Vector((0, sg * 0.06, hipz)); ank = Vector((0.0, sg * 0.07, 0.06))
            if step is not None:
                p = step + (0 if sg > 0 else math.pi)
                ank.x += 0.13 * math.sin(p); ank.z += max(0, math.cos(p)) * 0.07
            knee, ank = ik(hip, ank, 0.13, 0.13, Vector((1, 0, 0.2)))
            place_seg(P["coxa" + sd], hip, knee, 0.052); place_seg(P["canela" + sd], knee, ank, 0.045)
            place(P["bota" + sd], ank + Vector((0.03, 0, -0.03)), (0.075, 0.048, 0.04))
            if ct: place(P["joelheira" + sd], knee + Vector((0.035, 0, 0)), (0.025, 0.04, 0.038))
        place(P["quadril"], Vector((0, 0, hipz + 0.01)), (0.085, 0.115, 0.06))
        place(P["cinto"], Vector((0, 0, hipz + 0.045)), (0.095, 0.122, 0.022))
        place(P["coldre"], Vector((0.0, -0.125, hipz - 0.01)))
        ch = Vector((0, 0, hipz + 0.15))
        place(P["tronco"], ch, (0.1, 0.13, 0.13))
        place(P["colete"], ch + Vector((0.012, 0, 0.005)))
        if ct:
            for i in range(3): place(P["bolso%d" % i], ch + Vector((0.1, (i - 1) * 0.07, -0.04)))
            place(P["radio"], ch + Vector((0.07, 0.09, 0.07)))
            place_seg(P["antena"], ch + Vector((0.07, 0.1, 0.1)), ch + Vector((0.06, 0.11, 0.2)), 0.006)
            for sd, sg in (("L", 1), ("R", -1)): place(P["ombreira" + sd], ch + Vector((0, sg * 0.12, 0.085)), (0.06, 0.045, 0.03))
        else:
            for i in range(2): place(P["bolso%d" % i], ch + Vector((0.095, (i - 0.5) * 0.11, -0.05)))
            b0, b1 = ch + Vector((0.1, 0.1, 0.08)), ch + Vector((0.11, -0.1, -0.09))
            place_seg(P["bandoleira"], b0, b1, 0.016)
            for i in range(6):
                c = b0.lerp(b1, (i + 1) / 7) + Vector((0.016, 0, 0))
                place(P["bala%d" % i], c, (0.008, 0.009, 0.014))
            place_seg(P["faca"], ch + Vector((0.1, 0.07, 0.03)), ch + Vector((0.1, 0.07, -0.05)), 0.01)
            place(P["bandana"], ch + Vector((0, 0, 0.125)), (0.075, 0.095, 0.03))
        # cabeça
        hc = ch + Vector((0.01, 0, 0.27))
        place(P["cabeca"], hc, (0.18, 0.185, 0.17))
        for sd, sg in (("L", 1), ("R", -1)):
            place(P["olho" + sd], hc + Vector((0.165, sg * 0.062, -0.005)), (0.012, 0.024, 0.036))
            place(P["brilho" + sd], hc + Vector((0.176, sg * 0.055, 0.012)), (0.006, 0.008, 0.01))
            place(P["sobr" + sd], hc + Vector((0.158, sg * 0.068, 0.045)))
        if ct:
            place(P["capacete"], hc + Vector((-0.02, 0, 0.06)), (0.19, 0.195, 0.15))
            place(P["faixa"], hc + Vector((0.155, 0, 0.085)))
            for sd, sg in (("L", 1), ("R", -1)):
                place(P["oculos" + sd], hc + Vector((0.15, sg * 0.06, 0.105)), (0.025, 0.05, 0.034))
                place(P["lente" + sd], hc + Vector((0.165, sg * 0.06, 0.105)), (0.016, 0.04, 0.026))
                place(P["fone" + sd], hc + Vector((0, sg * 0.185, -0.005)), (0.045, 0.03, 0.055))
        else:
            place(P["cabelo"], hc + Vector((-0.02, 0, 0.04)), (0.18, 0.188, 0.15))
            place(P["barba"], hc + Vector((0.05, 0, -0.07)), (0.14, 0.16, 0.09))
            place(P["boca"], hc + Vector((0.168, 0, -0.07)))
        # arma: coronha no ombro direito, cano pra frente
        gpos = ch + Vector((-0.09, -0.06, 0.03)); gq = Quaternion()
        mag_on = True; mag_pos = Vector((9, 9, -9)); mag_q = Quaternion(); show2 = False; lh_t = None
        if anim == "recarga":
            f = t   # 0..1
            def k(a, b): return min(max((f - a) / (b - a), 0), 1)
            tilt = math.radians(22) * (k(0.0, 0.12) - k(0.85, 1.0))
            gq = Quaternion((1, 0, 0), -tilt) @ Quaternion((0, 1, 0), -tilt * 0.6)
            gpos = gpos + Vector((0, 0, -0.03)) * (k(0.0, 0.12) - k(0.85, 1.0))
        GM = Matrix.Translation(gpos) @ gq.to_matrix().to_4x4()
        def W(p): return GM @ p
        rh_t = W(s.grip); lh_t = W(s.fore)
        piv = Vector(s.mag["piv"])
        mag_home = W(piv)
        if anim == "recarga":
            f = t
            pouch = ch + Vector((0.07, 0.1, -0.11))
            if f < 0.14: lh_t = W(s.fore).lerp(mag_home + Vector((0, 0, -0.06)), k(0.02, 0.14))
            elif f < 0.30:   # tira o pente e solta: cai girando
                lh_t = mag_home + Vector((0, 0, -0.06)) + Vector((0.02, 0.04, -0.06)) * k(0.14, 0.24)
                mag_on = False
                u = k(0.18, 0.30)
                mag_pos = (mag_home + Vector((0.02, 0.04, -0.06)) * k(0.14, 0.24)) + Vector((0.03, 0.05, -0.45)) * (u * u)
                mag_q = gq @ Quaternion((1, 0, 0), u * 1.8)
            elif f < 0.46:   # mão vai no bolso
                mag_on = False; mag_pos = Vector((9, 9, -9)); mag_q = Quaternion()
                lh_t = (mag_home + Vector((0.02, 0.04, -0.12))).lerp(pouch, k(0.30, 0.42))
                show2 = f > 0.40
            elif f < 0.66:   # traz o novo e encaixa
                mag_on = False
                mag_pos_new = pouch.lerp(mag_home + Vector((0, 0, -0.035)), k(0.46, 0.58)).lerp(mag_home, k(0.58, 0.66))
                show2 = True; lh_t = mag_pos_new + Vector((0, 0, -0.06))
                s.mag2_pos = mag_pos_new
            elif f < 0.86:   # puxa o ferrolho (mão esquerda por cima)
                bo = W(s.bolt)
                lh_t = (mag_home + Vector((0, 0, -0.06))).lerp(bo + Vector((0.02, -0.03, 0.02)), k(0.66, 0.74))
                lh_t = lh_t + Vector((-0.05, 0, 0)) * (k(0.74, 0.79) - k(0.79, 0.84))
            else:
                lh_t = (W(s.bolt) + Vector((0.02, -0.03, 0.02))).lerp(W(s.fore), k(0.86, 0.98))
        # braços
        for sd, sg, tgt in (("L", 1, lh_t), ("R", -1, rh_t)):
            sh = ch + Vector((0, sg * 0.125, 0.07))
            el, hd = ik(sh, tgt, 0.19, 0.19, Vector((0, sg * 0.4, -1)))
            mid = sh.lerp(el, 0.65)
            place_seg(P["braco" + sd], sh, mid if not ct else el, 0.042)
            place_seg(P["ante" + sd], mid if not ct else el, hd, 0.036)
            place(P["mao" + sd], hd, (0.034, 0.032, 0.032))
            if not ct:   # manga curta: de cima até 65% do braço, resto pele
                place_seg(P["ante" + sd], mid, hd, 0.036)
        # arma e pentes
        s.gun.location = gpos; s.gun.rotation_mode = "QUATERNION"; s.gun.rotation_quaternion = gq
        if mag_on:
            s.mag.location = mag_home; s.mag.rotation_mode = "QUATERNION"; s.mag.rotation_quaternion = gq
        else:
            s.mag.location = mag_pos; s.mag.rotation_mode = "QUATERNION"; s.mag.rotation_quaternion = mag_q
        if show2 and anim == "recarga" and t >= 0.46:
            s.mag2.location = s.mag2_pos; s.mag2.rotation_mode = "QUATERNION"; s.mag2.rotation_quaternion = gq
        elif show2:
            s.mag2.location = lh_t + Vector((0, 0, 0.05)); s.mag2.rotation_mode = "QUATERNION"; s.mag2.rotation_quaternion = gq
        else:
            s.mag2.location = Vector((9, 9, -9))

def render(path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

DIRS = ["L", "SE", "S", "SO", "O", "NO", "N", "NE"]
for team in TEAMS:
    for o in list(sc.objects):
        if o.type in ("MESH", "EMPTY"): bpy.data.objects.remove(o)
    A = Agent(team)
    # 8 direções mirando (ângulo do jogo: 0 = leste, cresce pro sul)
    for i, dn in enumerate(DIRS):
        A.root.rotation_euler = (0, 0, -i * math.pi / 4)
        A.pose(0, "mira"); render(os.path.join(OUT, f"{team}_mira_{i}_{dn}.png"))
    # recarga e andar de 3/4 (virado pro sudeste, de frente pra câmera)
    A.root.rotation_euler = (0, 0, -7 * math.pi / 4)   # de perfil pra direita da tela (NE)
    for fi in range(16):
        A.pose(fi / 15, "recarga"); render(os.path.join(OUT, f"{team}_recarga_{fi:02d}.png"))
    for fi in range(8):
        A.pose(fi / 8, "andar"); render(os.path.join(OUT, f"{team}_andar_{fi:02d}.png"))
print("SPRITES OK")
