# Agentes em sprite a partir dos modelos 3D autorais (Meshy TR2/CT2, mesma origem das armas): monta esqueleto nas
# juntas, liga a malha com pesos automáticos, posa com IK (mãos na arma, pés no chão) e renderiza no estilo desenho
# (contorno escuro + 3 tons de luz, igual nas armas) com a câmera do jogo.
#   blender -b --factory-startup --python personagens.py -- <saida> [ct|tr|ambos]
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Quaternion

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGV[0] if ARGV else r"D:\unity\CSManager\Logs\sprites"
TEAMS = ["ct", "tr"] if len(ARGV) < 2 or ARGV[1] == "ambos" else [ARGV[1]]
SRC = r"D:\unity\EsportsPoc\Assets\EsportsPoc\Resources\Autoral\Src"
ARMAS = r"D:\unity\EsportsPoc\Tools\autoral\armas_out"
RES, PX_PER_BU = 192, 150
os.makedirs(OUT, exist_ok=True)

# juntas no modelo original (x = esquerda do boneco, z = cima; frente = -y), lidas da vista de frente em A
JUNTAS = {
    "tr": dict(quadril=(0, -0.018), coluna=(0, 0.114), peito=(0, 0.216), pescoco=(0, 0.318), topo=(0, 0.47),
               ombro=(0.104, 0.237), cotovelo=(0.212, 0.118), pulso=(0.298, 0.049), dedos=(0.318, -0.004),
               anca=(0.047, -0.018), joelho=(0.05, -0.212), tornozelo=(0.061, -0.392)),
    "ct": dict(quadril=(0, -0.004), coluna=(0, 0.12), peito=(0, 0.22), pescoco=(0, 0.308), topo=(0, 0.47),
               ombro=(0.094, 0.247), cotovelo=(0.186, 0.131), pulso=(0.267, 0.037), dedos=(0.288, -0.024),
               anca=(0.045, -0.004), joelho=(0.05, -0.182), tornozelo=(0.06, -0.406)),
}
NOME = {"tr": "TR2", "ct": "CT2"}

WEAP = {   # comprimento no boneco de altura 1 (fuzil ~0,5 da altura, um pouco exagerado pra ler)
    "AK47":  dict(L=0.56, grip=(0.26, 0.33), fore=(0.50, 0.57), kind="rifle", mag=(0.425, 0.625)),
    "M4A4":  dict(L=0.54, grip=(0.28, 0.35), fore=(0.50, 0.58), kind="rifle"),
    "M4A1S": dict(L=0.63, grip=(0.24, 0.30), fore=(0.44, 0.51), kind="rifle"),
    "AWP":   dict(L=0.72, grip=(0.28, 0.34), fore=(0.40, 0.46), kind="rifle"),
    "MP9":   dict(L=0.36, grip=(0.30, 0.40), fore=(0.55, 0.65), kind="smg"),
    "MAC10": dict(L=0.30, grip=(0.30, 0.42), fore=(0.55, 0.70), kind="smg"),
    "USP":   dict(L=0.27, grip=(0.06, 0.24), fore=(0.06, 0.24), kind="pistol"),
    "Glock": dict(L=0.20, grip=(0.06, 0.30), fore=(0.06, 0.30), kind="pistol"),
}

# ------------------------------------------------------------------ cena, câmera do jogo, luz
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        sc.render.engine = eng; break
    except TypeError:
        pass
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = True
sc.render.filter_size = 0.6
sc.view_settings.view_transform = "Standard"
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"

def srgb(h):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = RES / PX_PER_BU
el = math.radians(50)
dcam = Vector((math.cos(el) / math.sqrt(2), -math.cos(el) / math.sqrt(2), math.sin(el)))
cam.location = Vector((0.06, 0, 0.5)) + dcam * 10
cam.rotation_euler = (-dcam).to_track_quat("-Z", "Y").to_euler(); cam.data.clip_end = 40
sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", "SUN")); sc.collection.objects.link(sun)
sdir = Vector((-0.55, 0.28, 0.79)).normalized()
sun.rotation_euler = (-sdir).to_track_quat("-Z", "Y").to_euler(); sun.data.energy = 3.2
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.52, 0.5, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

def toon(name, tex, gain=1.0):
    """Desenho: luz em 3 degraus vezes a cor da textura (clareada um pouco: as texturas do Meshy são escuras)."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB"); ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"; e = ramp.color_ramp.elements
    e[0].position = 0.0; e[0].color = (0.58, 0.55, 0.6, 1)
    e[1].position = 0.22; e[1].color = (0.82, 0.8, 0.82, 1)
    e3 = e.new(0.62); e3.color = (1, 1, 1, 1)
    nt.links.new(dif.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs[0], ramp.inputs[0])
    ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = bpy.data.images.load(tex)
    br = nt.nodes.new("ShaderNodeBrightContrast"); br.inputs["Bright"].default_value = 0.0
    hsv = nt.nodes.new("ShaderNodeHueSaturation"); hsv.inputs["Value"].default_value = gain; hsv.inputs["Saturation"].default_value = 1.15
    nt.links.new(ti.outputs[0], hsv.inputs["Color"])
    mul = nt.nodes.new("ShaderNodeMixRGB"); mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1
    nt.links.new(ramp.outputs[0], mul.inputs[1]); nt.links.new(hsv.outputs[0], mul.inputs[2])
    em = nt.nodes.new("ShaderNodeEmission"); nt.links.new(mul.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    return m

OUTLINE = bpy.data.materials.new("contorno"); OUTLINE.use_nodes = True
OUTLINE.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = srgb("#1c1416")
OUTLINE.use_backface_culling = True

def outline(o, th):
    o.data.materials.append(OUTLINE)
    md = o.modifiers.new("contorno", "SOLIDIFY")
    md.thickness = th; md.offset = 1; md.use_flip_normals = True; md.use_rim = False
    md.material_offset = len(o.data.materials) - 1

def import_obj(path):
    antes = set(sc.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    novos = [o for o in sc.objects if o not in antes]
    ms = [o for o in novos if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in ms: o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1: bpy.ops.object.join()
    g = bpy.context.active_object
    mw = g.matrix_world.copy(); g.parent = None; g.matrix_world = mw
    for o in novos:
        if o.type != "MESH" and o.name in bpy.data.objects: bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT"); g.select_set(True); bpy.context.view_layer.objects.active = g
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return g

# ------------------------------------------------------------------ arma (mesmo tratamento de antes)
def import_gun(name, L):
    g = import_obj(os.path.join(ARMAS, name + "_3p", name + "_3p.fbx")); me = g.data
    vs = [v.co.copy() for v in me.vertices]
    mn = Vector([min(v[i] for v in vs) for i in range(3)]); mx = Vector([max(v[i] for v in vs) for i in range(3)])
    ext = mx - mn; ax = max(range(3), key=lambda i: ext[i])
    rest = [i for i in range(3) if i != ax]; up = max(rest, key=lambda i: ext[i]); side = [i for i in rest if i != up][0]
    def slab(t0, t1): return [v for v in vs if t0 <= (v[ax] - mn[ax]) / ext[ax] <= t1]
    a, b = slab(0, 0.06), slab(0.94, 1)
    va = max(v[up] for v in a) - min(v[up] for v in a); vb = max(v[up] for v in b) - min(v[up] for v in b)
    sgn_ax = 1 if vb < va else -1
    muz = b if sgn_ax > 0 else a; zc = sum(v[up] for v in muz) / len(muz)
    sgn_up = 1 if (mx[up] - zc) < (zc - mn[up]) else -1
    rows = []
    for tgt in range(3):
        src = [ax, side, up][tgt]; s_ = [sgn_ax, 1, sgn_up][tgt]; row = [0, 0, 0]; row[src] = s_; rows.append(row)
    R = Matrix(rows)
    if R.determinant() < 0: rows[1] = [-x for x in rows[1]]; R = Matrix(rows)
    me.transform(R.to_4x4())
    vs = [v.co.copy() for v in me.vertices]
    x0 = min(v.x for v in vs); x1 = max(v.x for v in vs)
    muz = [v for v in vs if v.x > x1 - 0.06 * (x1 - x0)]
    zc = sum(v.z for v in muz) / len(muz); yc = sum(v.y for v in vs) / len(vs)
    me.transform(Matrix.Translation((-x0, -yc, -zc))); me.transform(Matrix.Scale(L / (x1 - x0), 4))
    g.name = name
    g.data.materials.clear(); g.data.materials.append(toon(name, os.path.join(ARMAS, name + "_3p", name + "_3p_albedo.png"), 1.25))
    outline(g, 0.005)
    return g

def split_mag(g, L, x0f, x1f):
    me = g.data
    zcut = min(v.co.z for v in me.vertices if 0.62 * L <= v.co.x <= 0.70 * L) - 0.003
    bm = bmesh.new(); bm.from_mesh(me)
    sel = set(v.index for v in bm.verts if x0f * L <= v.co.x <= x1f * L and v.co.z < zcut)
    for f in bm.faces: f.select = all(v.index in sel for v in f.verts)
    bm.to_mesh(me); bm.free()
    bpy.ops.object.select_all(action="DESELECT"); g.select_set(True); bpy.context.view_layer.objects.active = g
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
    mag = [o for o in bpy.context.selected_objects if o != g][0]; mag.name = g.name + "_pente"
    c = sum((v.co for v in mag.data.vertices), Vector()) / len(mag.data.vertices)
    piv = Vector((c.x, 0, max(v.co.z for v in mag.data.vertices)))
    mag.data.transform(Matrix.Translation(-piv)); mag["piv"] = piv
    return mag

class Gun:
    def __init__(s, name):
        cfg = WEAP[name]; s.cfg = cfg; s.L = cfg["L"]
        s.obj = import_gun(name, s.L); s.mag = s.mag2 = None
        if "mag" in cfg:
            s.mag = split_mag(s.obj, s.L, *cfg["mag"])
            s.mag2 = s.mag.copy(); s.mag2.data = s.mag.data; sc.collection.objects.link(s.mag2)
        vs = [v.co for v in s.obj.data.vertices]
        def mid(a, b, low=False):
            sl = [v for v in vs if a * s.L <= v.x <= b * s.L] or vs
            z0 = min(v.z for v in sl); z1 = max(v.z for v in sl)
            return Vector((((a + b) / 2) * s.L, 0, (z0 * 0.65 + z1 * 0.35) if low else (z0 + z1) / 2))
        s.grip = mid(*cfg["grip"], low=True); s.fore = mid(*cfg["fore"]); s.bolt = Vector((0.40 * s.L, -0.025, 0.01))
        for o in (s.obj, s.mag, s.mag2):
            if o: o.location = (50, 50, -50)

# ------------------------------------------------------------------ personagem: malha + esqueleto + IK
class Agent:
    def __init__(s, team):
        s.team = team
        body = import_obj(os.path.join(SRC, NOME[team], NOME[team] + ".fbx"))
        me = body.data
        zmin = min(v.co.z for v in me.vertices); zmax = max(v.co.z for v in me.vertices)
        k = 1.0 / (zmax - zmin)
        # normaliza: altura 1, pé no chão, frente = +X (o modelo olha pra -Y), esquerda = +Y
        N = Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Scale(k, 4) @ Matrix.Translation((0, 0, -zmin))
        me.transform(N)
        body.data.materials.clear()
        body.data.materials.append(toon(team, os.path.join(SRC, NOME[team], NOME[team] + "_albedo.png"), 1.3))
        J = JUNTAS[team]
        vs = [v.co.copy() for v in me.vertices]
        def P(name, side=0):
            x, z = J[name]
            p = N @ Vector((x * side if side else x, 0, z))
            # centro de verdade na profundidade: média dos vértices perto da junta (só a coordenada da frente)
            near = [v for v in vs if abs(v.y - p.y) < 0.035 and abs(v.z - p.z) < 0.035]
            if near: p.x = sum(v.x for v in near) / len(near)
            return p
        # esqueleto
        arm = bpy.data.armatures.new(team + "_esq"); rig = bpy.data.objects.new(team + "_rig", arm)
        sc.collection.objects.link(rig); bpy.context.view_layer.objects.active = rig
        bpy.ops.object.mode_set(mode="EDIT")
        def bone(n, h, t, parent=None):
            b = arm.edit_bones.new(n); b.head = h; b.tail = t
            if parent: b.parent = arm.edit_bones[parent]; b.use_connect = False
            return b
        bone("quadril", P("quadril"), P("coluna"))
        bone("coluna", P("coluna"), P("peito"), "quadril")
        bone("peito", P("peito"), P("pescoco"), "coluna")
        bone("cabeca", P("pescoco"), P("topo"), "peito")
        for sd, sg in (("L", 1), ("R", -1)):
            bone("braco" + sd, P("ombro", sg), P("cotovelo", sg), "peito")
            bone("ante" + sd, P("cotovelo", sg), P("pulso", sg), "braco" + sd)
            bone("mao" + sd, P("pulso", sg), P("dedos", sg), "ante" + sd)
            bone("coxa" + sd, P("anca", sg), P("joelho", sg), "quadril")
            bone("canela" + sd, P("joelho", sg), P("tornozelo", sg), "coxa" + sd)
            ank = P("tornozelo", sg); bone("pe" + sd, ank, ank + Vector((0.08, 0, -0.04)), "canela" + sd)
        bpy.ops.object.mode_set(mode="OBJECT")
        s.rest = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.bones}
        # liga a malha com pesos automáticos (calor dos ossos); se falhar, usa envelope
        bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        try:
            bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        except Exception as ex:
            print("PESO AUTO FALHOU", ex); bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
        # vértice que ficou sem peso (peça solta do Meshy) vai inteiro pro osso mais perto, senão fica parado no ar
        segs = [(b.name, b.head_local.copy(), b.tail_local.copy()) for b in arm.bones]
        def seg_d(p, a, b):
            ab = b - a; u = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.dot(ab), 1e-9))); return (p - (a + ab * u)).length
        sem = [v for v in me.vertices if not v.groups or sum(g.weight for g in v.groups) < 1e-3]
        for v in sem:
            nb = min(segs, key=lambda s_: seg_d(v.co, s_[1], s_[2]))[0]
            vg = body.vertex_groups.get(nb) or body.vertex_groups.new(name=nb)
            vg.add([v.index], 1.0, "REPLACE")
        print("GRUPOS", len(body.vertex_groups), "VERTS SEM PESO (consertados)", len(sem))
        outline(body, 0.006)
        s.root = bpy.data.objects.new(team + "_raiz", None); sc.collection.objects.link(s.root)
        rig.parent = s.root
        s.rig = rig; s.body = body
        # alvos de IK (no espaço da raiz)
        s.tg = {}
        for n in ("maoL", "maoR", "poloL", "poloR", "peL", "peR", "joeL", "joeR"):
            e = bpy.data.objects.new(team + "_" + n, None); sc.collection.objects.link(e); e.parent = s.root; s.tg[n] = e
        for sd in ("L", "R"):
            c = rig.pose.bones["ante" + sd].constraints.new("IK")
            c.target = s.tg["mao" + sd]; c.pole_target = s.tg["polo" + sd]; c.chain_count = 2; c.pole_angle = math.radians(-90)
            c2 = rig.pose.bones["canela" + sd].constraints.new("IK")
            c2.target = s.tg["pe" + sd]; c2.pole_target = s.tg["joe" + sd]; c2.chain_count = 2; c2.pole_angle = math.radians(-90)
        s.guns = {}

    def gun(s, name):
        if name not in s.guns:
            g = Gun(name)
            for o in (g.obj, g.mag, g.mag2):
                if o: o.parent = s.root
            s.guns[name] = g
        return s.guns[name]

    def pose(s, t, anim, gname):
        for g in s.guns.values():
            for o in (g.obj, g.mag, g.mag2):
                if o: o.location = (50, 50, -50)
        G = s.gun(gname); kind = G.cfg["kind"]; R = s.rest; rig = s.rig
        # corpo: balanço do passo
        bob = 0; step = None
        if anim == "andar": ph = t * 2 * math.pi; bob = 0.012 * abs(math.sin(ph)); step = ph
        rig.location = (0, 0, bob - 0.006)
        # pés
        for sd, sg in (("L", 1), ("R", -1)):
            ank = R["canela" + sd][1].copy()
            if step is not None:
                p = step + (0 if sg > 0 else math.pi)
                ank.x += 0.11 * math.sin(p); ank.z += max(0, math.cos(p)) * 0.05
            ank.z -= bob
            s.tg["pe" + sd].location = ank
            s.tg["joe" + sd].location = R["canela" + sd][0] + Vector((0.4, 0, 0))
        # arma no ombro direito
        shR = R["bracoR"][0]; shL = R["bracoL"][0]
        gq = Quaternion()
        if kind == "pistol": gpos = shR + Vector((0.23, 0.07, 0.02))
        elif kind == "smg": gpos = shR + Vector((0.02, 0.04, 0.03))
        else: gpos = shR + Vector((-0.05, 0.045, 0.05))   # cano na altura do olho, coronha no ombro
        def k(a, b): return min(max((t - a) / (b - a), 0), 1)
        if anim == "recarga":
            tilt = math.radians(22) * (k(0.0, 0.12) - k(0.85, 1.0))
            gq = Quaternion((1, 0, 0), -tilt) @ Quaternion((0, 1, 0), -tilt * 0.6)
            gpos = gpos + Vector((0, 0, -0.03)) * (k(0.0, 0.12) - k(0.85, 1.0))
        GM = Matrix.Translation(gpos) @ gq.to_matrix().to_4x4(); W = lambda p: GM @ p
        rh = W(G.grip); lh = W(G.fore) + (Vector((0, 0.02, -0.008)) if kind == "pistol" else Vector())
        mag_home = W(Vector(G.mag["piv"])) if G.mag else None
        mag_pos, mag_q, mag2_pos = mag_home, gq, None
        if anim == "recarga" and G.mag:
            chest = R["peito"][0]; pouch = chest + Vector((0.08, 0.05, -0.02)); under = mag_home + Vector((0, 0, -0.05))
            if t < 0.14: lh = W(G.fore).lerp(under, k(0.02, 0.14))
            elif t < 0.30:
                drop = Vector((0.02, 0.03, -0.05)) * k(0.14, 0.24); lh = under + drop; u = k(0.18, 0.30)
                mag_pos = mag_home + drop + Vector((0.03, 0.05, -0.45)) * (u * u); mag_q = gq @ Quaternion((1, 0, 0), u * 1.8)
            elif t < 0.46:
                mag_pos = None; lh = (under + Vector((0.02, 0.03, -0.05))).lerp(pouch, k(0.30, 0.42))
                if t > 0.40: mag2_pos = lh + Vector((0, 0, 0.04))
            elif t < 0.66:
                mag_pos = None
                mag2_pos = pouch.lerp(mag_home + Vector((0, 0, -0.03)), k(0.46, 0.58)).lerp(mag_home, k(0.58, 0.66))
                lh = mag2_pos + Vector((0, 0, -0.05))
            elif t < 0.86:
                bo = W(G.bolt) + Vector((0.015, -0.025, 0.015))
                lh = under.lerp(bo, k(0.66, 0.74)) + Vector((-0.04, 0, 0)) * (k(0.74, 0.79) - k(0.79, 0.84))
            else:
                lh = (W(G.bolt) + Vector((0.015, -0.025, 0.015))).lerp(W(G.fore), k(0.86, 0.98))
        s.tg["maoR"].location = rh; s.tg["maoL"].location = lh
        s.tg["poloR"].location = R["anteR"][0] + Vector((-0.1, -0.3, -0.3))
        s.tg["poloL"].location = R["anteL"][0] + Vector((-0.1, 0.3, -0.3))
        G.obj.location = gpos; G.obj.rotation_mode = "QUATERNION"; G.obj.rotation_quaternion = gq
        if G.mag:
            if mag_pos is not None:
                G.mag.location = mag_pos; G.mag.rotation_mode = "QUATERNION"; G.mag.rotation_quaternion = mag_q
            if mag2_pos is not None:
                G.mag2.location = mag2_pos; G.mag2.rotation_mode = "QUATERNION"; G.mag2.rotation_quaternion = gq
        bpy.context.view_layer.update()

def render(path):
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)

DIRS = ["L", "SE", "S", "SO", "O", "NO", "N", "NE"]
for team in TEAMS:
    for o in list(sc.objects):
        if o.type in ("MESH", "EMPTY", "ARMATURE"): bpy.data.objects.remove(o)
    A = Agent(team)
    padrao = "M4A4" if team == "ct" else "AK47"
    for i, dn in enumerate(DIRS):
        A.root.rotation_euler = (0, 0, -i * math.pi / 4)
        A.pose(0, "mira", padrao); render(os.path.join(OUT, f"{team}_mira_{i}_{dn}.png"))
    A.root.rotation_euler = (0, 0, -7 * math.pi / 4)
    for w in WEAP:
        A.pose(0, "mira", w); render(os.path.join(OUT, f"{team}_arma_{w}.png"))
    for fi in range(16):
        A.pose(fi / 15, "recarga", "AK47"); render(os.path.join(OUT, f"{team}_recarga_{fi:02d}.png"))
    for fi in range(8):
        A.pose(fi / 8, "andar", padrao); render(os.path.join(OUT, f"{team}_andar_{fi:02d}.png"))
print("SPRITES OK")
