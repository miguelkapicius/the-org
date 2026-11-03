# Agentes em sprite (estilo desenho: contorno escuro + 3 tons de luz, igual em todas as peças, armas inclusive).
# CT: operador de máscara de gás, farda azul-marinho, colete oliva com bolsos, joelheira, bota preta.
# TR: mercenário de balaclava (olhos e boca aparecendo), camiseta cinza, colete grafite, calça cáqui manchada.
# Personagens originais: só o tipo vem da referência (sem logo nem emblema de ninguém).
# Armas: as 8 autorais 3ª pessoa (EsportsPoc/Tools/autoral/armas_out/<arma>_3p), pegada por tipo.
#   blender -b --factory-startup --python agentes.py -- <saida> [ct|tr|ambos]
# Coordenadas Blender: X = leste do jogo, Y = norte (= -y do jogo), Z cima. Boneco local: X frente, Y esquerda.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Quaternion

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGV[0] if ARGV else r"D:\unity\CSManager\Logs\sprites"
TEAMS = ["ct", "tr"] if len(ARGV) < 2 or ARGV[1] == "ambos" else [ARGV[1]]
ARMAS = r"D:\unity\EsportsPoc\Tools\autoral\armas_out"
RES = 192
PX_PER_BU = 140
os.makedirs(OUT, exist_ok=True)

# arma: comprimento no boneco, faixa do punho e da mão de apoio (fração do comprimento), tipo, pente (faixa ao longo do
# cano e corte de altura em fração do comprimento; None = fundo do guarda-mão). Medido com perfil_armas.py.
# tempos do CS2 (recarga s, cadência RPM) x 0,9 = nossos; mesma fonte do Assets/Game/Sim/WeaponStats.cs
CS2 = {"AK47": (2.4, 600), "M4A4": (3.1, 666.67), "M4A1S": (3.1, 600), "AWP": (3.7, 41.24),
       "MP9": (2.1, 857.14), "MAC10": (2.6, 800), "USP": (2.2, 352.94), "Glock": (2.3, 400), "Deagle": (2.2, 266.67)}
RELOAD_S = {w: r * 0.9 for w, (r, _) in CS2.items()}
SHOT_S = {w: 60.0 / rpm * 0.9 for w, (_, rpm) in CS2.items()}
FPS = 12   # quadros por segundo das animações renderizadas
WEAP = {
    "AK47":  dict(L=0.70, grip=(0.26, 0.33), fore=(0.50, 0.57), kind="rifle", mag=(0.425, 0.625, None)),
    "M4A4":  dict(L=0.68, grip=(0.24, 0.32), fore=(0.58, 0.66), kind="rifle", mag=(0.39, 0.555, None)),
    "M4A1S": dict(L=0.80, grip=(0.23, 0.30), fore=(0.50, 0.58), kind="rifle", mag=(0.34, 0.455, None)),
    "AWP":   dict(L=0.92, grip=(0.16, 0.24), fore=(0.44, 0.52), kind="rifle", mag=(0.32, 0.43, -0.06), bolt=True),
    "MP9":   dict(L=0.44, grip=(0.53, 0.62), fore=(0.83, 0.92), kind="smg", mag=(0.52, 0.66, -0.21)),
    "MAC10": dict(L=0.36, grip=(0.50, 0.58), fore=(0.74, 0.84), kind="smg", mag=(0.56, 0.68, -0.27)),
    "USP":   dict(L=0.34, grip=(0.03, 0.18), fore=(0.03, 0.18), kind="pistol", mag=(0.0, 0.2, -0.30)),
    "Glock": dict(L=0.24, grip=(0.05, 0.28), fore=(0.05, 0.28), kind="pistol", mag=(0.0, 0.3, -0.48)),
    "Deagle": dict(L=0.27, grip=(0.04, 0.28), fore=(0.04, 0.28), kind="pistol", mag=(-0.25, 0.35, -0.4)),   # procedural (build_deagle)
}

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

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = RES / PX_PER_BU
el = math.radians(50)
dcam = Vector((math.cos(el) / math.sqrt(2), -math.cos(el) / math.sqrt(2), math.sin(el)))
FOCO = Vector((0.08, 0, 0.5))
cam.location = FOCO + dcam * 10
cam.rotation_euler = (-dcam).to_track_quat("-Z", "Y").to_euler()
cam.data.clip_end = 40

sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", "SUN"))
sc.collection.objects.link(sun)
sdir = Vector((-0.55, 0.28, 0.79)).normalized()
sun.rotation_euler = (-sdir).to_track_quat("-Z", "Y").to_euler()
sun.data.energy = 3.2
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.52, 0.5, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

# ------------------------------------------------------------------ material de desenho: 3 tons + contorno
# pintura das armas (cor, até onde vai ao longo do cano e até que altura, em fração do comprimento): AWP verde no corpo
# e coronha (luneta e cano ficam pretos), Glock de polímero claro
PINTURA = {"AWP": ("#5f7d3a", 0.55, 0.03, -9.0), "Glock": ("#c2b08a", 9.0, 9.0, -0.07)}   # Glock: só o ferrolho (em cima), armação preta

def toon(name, col, tex=None, stains=False, knit=False, checker=None, mottle=False, molle=False, paint=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    e = ramp.color_ramp.elements
    e[0].position = 0.0; e[0].color = (0.58, 0.55, 0.6, 1)
    e[1].position = 0.22; e[1].color = (0.82, 0.8, 0.82, 1)
    e3 = e.new(0.62); e3.color = (1, 1, 1, 1)
    nt.links.new(dif.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs[0], ramp.inputs[0])
    mul = nt.nodes.new("ShaderNodeMixRGB"); mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1
    nt.links.new(ramp.outputs[0], mul.inputs[1])
    if tex:
        ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = bpy.data.images.load(tex)
        base = ti.outputs[0]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB"); rgb.outputs[0].default_value = srgb(col)
        base = rgb.outputs[0]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    def mul_by(sock_factor_color):
        mm = nt.nodes.new("ShaderNodeMixRGB"); mm.blend_type = "MULTIPLY"; mm.inputs[0].default_value = 1
        nt.links.new(base_holder[0], mm.inputs[1]); nt.links.new(sock_factor_color, mm.inputs[2])
        base_holder[0] = mm.outputs[0]
    base_holder = [base]
    if paint:   # pinta pela claridade da textura (mantém os detalhes) só na região da arma pedida
        pcol, xmax, zmax, zmin = paint
        bw = nt.nodes.new("ShaderNodeRGBToBW"); nt.links.new(base, bw.inputs[0])
        ma = nt.nodes.new("ShaderNodeMath"); ma.operation = "MULTIPLY_ADD"; ma.use_clamp = True
        ma.inputs[1].default_value = 1.8; ma.inputs[2].default_value = 0.42; nt.links.new(bw.outputs[0], ma.inputs[0])
        pr = nt.nodes.new("ShaderNodeMixRGB"); pr.blend_type = "MULTIPLY"; pr.inputs[0].default_value = 1
        pr.inputs[1].default_value = srgb(pcol); nt.links.new(ma.outputs[0], pr.inputs[2])
        sx = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sx.inputs[0])
        lx = nt.nodes.new("ShaderNodeMath"); lx.operation = "LESS_THAN"; lx.inputs[1].default_value = xmax; nt.links.new(sx.outputs[0], lx.inputs[0])
        lz = nt.nodes.new("ShaderNodeMath"); lz.operation = "LESS_THAN"; lz.inputs[1].default_value = zmax; nt.links.new(sx.outputs[2], lz.inputs[0])
        gz = nt.nodes.new("ShaderNodeMath"); gz.operation = "GREATER_THAN"; gz.inputs[1].default_value = zmin; nt.links.new(sx.outputs[2], gz.inputs[0])
        m0 = nt.nodes.new("ShaderNodeMath"); m0.operation = "MULTIPLY"; nt.links.new(lx.outputs[0], m0.inputs[0]); nt.links.new(lz.outputs[0], m0.inputs[1])
        mk = nt.nodes.new("ShaderNodeMath"); mk.operation = "MULTIPLY"; nt.links.new(m0.outputs[0], mk.inputs[0]); nt.links.new(gz.outputs[0], mk.inputs[1])
        mx = nt.nodes.new("ShaderNodeMixRGB"); mx.blend_type = "MIX"; nt.links.new(mk.outputs[0], mx.inputs[0])
        nt.links.new(base, mx.inputs[1]); nt.links.new(pr.outputs[0], mx.inputs[2])
        base_holder[0] = mx.outputs[0]
    if checker:   # xadrez (camisa, lenço): duas cores em quadradinhos
        ck = nt.nodes.new("ShaderNodeTexChecker"); ck.inputs["Scale"].default_value = checker[1]
        nt.links.new(tc.outputs["Object"], ck.inputs["Vector"])
        nt.links.new(base, ck.inputs["Color1"]); ck.inputs["Color2"].default_value = srgb(checker[0])
        base_holder[0] = ck.outputs["Color"]
    if stains:   # manchas escuras (sujeira) em degrau, sem degradê
        nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 22; nz.inputs["Detail"].default_value = 3
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
        r2 = nt.nodes.new("ShaderNodeValToRGB"); r2.color_ramp.interpolation = "CONSTANT"
        r2.color_ramp.elements[0].color = (1, 1, 1, 1); r2.color_ramp.elements[1].position = 0.64
        r2.color_ramp.elements[1].color = (0.5, 0.44, 0.38, 1)
        nt.links.new(nz.outputs["Fac"], r2.inputs[0]); mul_by(r2.outputs[0])
    if mottle:   # trama de tecido: manchinhas de 2 tons, bem leves (sem degradê, combina com o desenho)
        nm = nt.nodes.new("ShaderNodeTexNoise"); nm.inputs["Scale"].default_value = 7; nm.inputs["Detail"].default_value = 8
        nt.links.new(tc.outputs["Object"], nm.inputs["Vector"])
        rm = nt.nodes.new("ShaderNodeValToRGB"); rm.color_ramp.interpolation = "CONSTANT"
        rm.color_ramp.elements[0].color = (0.8, 0.8, 0.8, 1); rm.color_ramp.elements[1].position = 0.5
        rm.color_ramp.elements[1].color = (1, 1, 1, 1)
        nt.links.new(nm.outputs["Fac"], rm.inputs[0]); mul_by(rm.outputs[0])
    if molle:    # colete com fitas horizontais costuradas (linhas escuras finas)
        wm = nt.nodes.new("ShaderNodeTexWave"); wm.inputs["Scale"].default_value = 3.5
        try: wm.bands_direction = "Z"
        except Exception: pass
        nt.links.new(tc.outputs["Object"], wm.inputs["Vector"])
        rw = nt.nodes.new("ShaderNodeValToRGB"); rw.color_ramp.interpolation = "CONSTANT"
        rw.color_ramp.elements[0].color = (1, 1, 1, 1); rw.color_ramp.elements[1].position = 0.74
        rw.color_ramp.elements[1].color = (0.55, 0.55, 0.55, 1)
        nt.links.new(wm.outputs["Fac"], rw.inputs[0]); mul_by(rw.outputs[0])
    if knit:     # tricô: listras verticais finas
        wv = nt.nodes.new("ShaderNodeTexWave"); wv.inputs["Scale"].default_value = 9
        try: wv.bands_direction = "X"
        except Exception: pass
        nt.links.new(tc.outputs["Object"], wv.inputs["Vector"])
        r3 = nt.nodes.new("ShaderNodeValToRGB"); r3.color_ramp.interpolation = "CONSTANT"
        r3.color_ramp.elements[0].color = (0.8, 0.8, 0.8, 1); r3.color_ramp.elements[1].position = 0.5
        r3.color_ramp.elements[1].color = (1, 1, 1, 1)
        nt.links.new(wv.outputs["Fac"], r3.inputs[0]); mul_by(r3.outputs[0])
    nt.links.new(base_holder[0], mul.inputs[2])
    # BRANCO: clareia tudo até o branco (boneco cegado pela flash), controlado por set_branco()
    wv = nt.nodes.new("ShaderNodeValue"); wv.name = "BRANCO"; wv.outputs[0].default_value = 0.0
    mw = nt.nodes.new("ShaderNodeMixRGB"); mw.blend_type = "MIX"; mw.inputs[2].default_value = (1, 1, 1, 1)
    nt.links.new(wv.outputs[0], mw.inputs[0]); nt.links.new(mul.outputs[0], mw.inputs[1])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mw.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    return m

OUTLINE = bpy.data.materials.new("contorno"); OUTLINE.use_nodes = True
OUTLINE.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = srgb("#1c1416")
OUTLINE.use_backface_culling = True

def outline(o, th=0.009):
    o.data.materials.append(OUTLINE)
    md = o.modifiers.new("contorno", "SOLIDIFY")
    md.thickness = th; md.offset = 1; md.use_flip_normals = True; md.use_rim = False
    md.material_offset = len(o.data.materials) - 1
    o["th"] = th

def fix_outline(o):
    md = o.modifiers.get("contorno")
    if md:
        s = (abs(o.scale[0]) + abs(o.scale[1]) + abs(o.scale[2])) / 3
        md.thickness = o.get("th", 0.009) / max(s, 1e-4)

def ell(name, mat, seg=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1)
    o = bpy.context.active_object; o.name = name
    for p in o.data.polygons: p.use_smooth = True
    o.data.materials.append(mat); outline(o)
    return o

def box(name, mat, half, bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(size=2)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(mat)
    bv = o.modifiers.new("bv", "BEVEL"); bv.width = bevel / max(min(half), 1e-3); bv.segments = 2
    outline(o); o["r"] = half
    return o

def place_seg(o, a, b, r):
    d = b - a; L = d.length
    o.location = (a + b) / 2
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.to_track_quat("Z", "Y")
    o.scale = (r, r, L / 2 + r * 0.6); fix_outline(o)

def place(o, c, r=None, q=None):
    o.location = c
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = q or Quaternion()
    o.scale = r if r is not None else tuple(o["r"]); fix_outline(o)

def hide(o):
    o.location = (50, 50, -50)

def ik(s, h, l1, l2, pole):
    v = h - s; dd = min(v.length, l1 + l2 - 1e-3); u = v.normalized()
    a = (l1 * l1 - l2 * l2 + dd * dd) / (2 * dd); hh = math.sqrt(max(l1 * l1 - a * a, 0))
    p = pole - u * pole.dot(u); p = p.normalized() if p.length > 1e-6 else Vector((0, 0, -1))
    return s + u * a + p * hh, s + u * dd

def trilha(t, chaves):
    """Posição em t por pontos-chave [(t, Vector)] com smoothstep entre eles (antes do 1º = 1º; depois do último = último)."""
    if t <= chaves[0][0]: return chaves[0][1].copy()
    for (ta, a), (tb, b) in zip(chaves[:-1], chaves[1:]):
        if t <= tb:
            u = (t - ta) / max(tb - ta, 1e-6); u = u * u * (3 - 2 * u)
            return a.lerp(b, u)
    return chaves[-1][1].copy()

def _norma(q, pot):
    if pot == 2: return q.length
    return (abs(q.x) ** pot + abs(q.y) ** pot + abs(q.z) ** pot) ** (1.0 / pot)

def _fundo(p, r, obst):
    """Quanto o ponto p (esfera de raio r) entra nos obstáculos [(centro, raios, expoente)] (2 = elipsoide, 4 = caixa
    arredondada)."""
    d = 0.0
    for c, R, pot in obst:
        q = Vector(((p.x - c.x) / (R.x + r), (p.y - c.y) / (R.y + r), (p.z - c.z) / (R.z + r)))
        e = _norma(q, pot)
        if e < 1: d += 1 - e
    return d

COTOVELO_ANT = {}   # ângulo do cotovelo no quadro anterior da MESMA animação (por braço): continuidade entre quadros

def ik_desvia(s, h, l1, l2, pole, obst_mao, obst_braco, meio=None, chave=None):
    """IK do braço que não atravessa o corpo: a mão sai de dentro da cabeça/tronco (vai pra superfície) e o cotovelo
    gira em volta do eixo ombro-mão até o braço passar por fora (preferindo o polo pedido; custo contínuo pra não
    trocar de lado de um quadro pro outro)."""
    for _ in range(2):
        for c, R, pot in obst_mao:
            # a mão inteira fica fora da cabeça/máscara; no tronco/colete só o centro (encosta: a mão no punho da arma
            # perto do peito é a pose de mira aprovada)
            mg = 0.03 if R.x > 0.15 else 0.015
            q = Vector(((h.x - c.x) / (R.x + mg), (h.y - c.y) / (R.y + mg), (h.z - c.z) / (R.z + mg)))
            e = _norma(q, pot)
            if 1e-4 < e < 1:
                h = c + Vector((q.x / e * (R.x + mg), q.y / e * (R.y + mg), q.z / e * (R.z + mg)))
    v = h - s; dd = min(v.length, l1 + l2 - 1e-3); u = v.normalized()
    a = (l1 * l1 - l2 * l2 + dd * dd) / (2 * dd); hh = math.sqrt(max(l1 * l1 - a * a, 0))
    p0 = pole - u * pole.dot(u); p0 = p0.normalized() if p0.length > 1e-6 else Vector((0, 0, -1))
    p1 = u.cross(p0)
    hd = s + u * dd
    def custo(ang):
        elb = s + u * a + (p0 * math.cos(ang) + p1 * math.sin(ang)) * hh
        c = 0.0
        for i in range(3, 9): c += _fundo(s.lerp(elb, i / 8), 0.036, obst_braco)
        for i in range(0, 9): c += _fundo(elb.lerp(hd, i / 8), 0.032, obst_braco)
        if meio is not None:   # antebraço que nasce no meio do braço (TR): a peça corta o cotovelo
            m = s.lerp(elb, meio)
            for i in range(0, 9): c += _fundo(m.lerp(hd, i / 8), 0.034, obst_braco)
        return c * 6 + 0.02 * ang * ang + (0.3 * (ang - ant) ** 2 if ant is not None else 0.0), elb
    # desce a ladeira a partir do polo pedido (não pula pro outro lado do corpo de um quadro pro outro): passo de 8°,
    # depois 2°, depois 0,5°
    ant = COTOVELO_ANT.get(chave) if chave else None
    ang = ant if ant is not None else 0.0
    c0, elb = custo(ang)
    if c0 > 1e-6:
        for passo in (math.radians(8), math.radians(2), math.radians(0.5)):
            while abs(ang) < math.radians(160):
                cm, em = custo(ang - passo); cp, ep = custo(ang + passo)
                if min(cm, cp) >= c0: break
                if cm < cp: ang -= passo; c0, elb = cm, em
                else: ang += passo; c0, elb = cp, ep
    if chave: COTOVELO_ANT[chave] = ang
    return elb, hd

# ------------------------------------------------------------------ armas autorais
def build_deagle(L):
    """Desert Eagle autoral (procedural): ferrolho grande de aço escovado com sulcos atrás, cano triangular com
    trilho em cima, armação escura, guarda-mato, cão, cabo inclinado preto com a base do pente. Eixo X = cano (0 atrás,
    L na boca), Z = cima, boca do cano em z=0 (igual às armas importadas)."""
    me = bpy.data.meshes.new("Deagle"); bm = bmesh.new()
    def cube(x0, x1, z0, z1, hy, mi, shear=0.0):
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r["verts"]:
            x = x0 + (v.co.x + 0.5) * (x1 - x0); z = z0 + (v.co.z + 0.5) * (z1 - z0)
            x += shear * (z1 - z)        # inclina pra trás embaixo (cabo)
            v.co = Vector((x, v.co.y * 2 * hy, z))
        for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = mi
    # 0 aço do ferrolho, 1 armação escura, 2 cabo preto, 3 detalhe (sulcos/boca)
    # pistola grossa e curta (pouco maior que a Glock): ferrolho alto e largo, cabo encorpado
    cube(0.02 * L, L, -0.02, 0.03, 0.018, 0)                         # ferrolho
    cube(0.18 * L, L * 0.995, 0.03, 0.038, 0.009, 0)                 # trilho do cano
    for i in range(4): cube((0.05 + 0.045 * i) * L, (0.07 + 0.045 * i) * L, -0.01, 0.026, 0.0185, 3)   # sulcos
    cube(L * 0.98, L * 1.0, -0.012, 0.016, 0.0185, 3)                # boca
    cube(0.06 * L, 0.8 * L, -0.04, -0.02, 0.016, 1)                  # armação
    cube(0.3 * L, 0.6 * L, -0.08, -0.07, 0.011, 1)                   # guarda-mato embaixo
    cube(0.56 * L, 0.61 * L, -0.08, -0.04, 0.011, 1)                 # guarda-mato frente
    cube(0.4 * L, 0.44 * L, -0.066, -0.04, 0.005, 3)                 # gatilho
    cube(-0.04 * L, 0.04 * L, 0.0, 0.04, 0.008, 1)                   # cão
    cube(0.0 * L, 0.3 * L, -0.11, -0.04, 0.0165, 2, shear=0.28)      # cabo
    cube(-0.02 * L, 0.29 * L, -0.124, -0.111, 0.0175, 1, shear=0.28) # base do pente (sai na recarga)
    bm.to_mesh(me); bm.free()
    g = bpy.data.objects.new("Deagle", me); sc.collection.objects.link(g)
    for nm, col in (("Deagle_aco", "#9aa0a6"), ("Deagle_armacao", "#3a3c40"), ("Deagle_cabo", "#1f2023"), ("Deagle_detalhe", "#26282b")):
        g.data.materials.append(toon(nm, col))
    bv = g.modifiers.new("bv", "BEVEL"); bv.width = 0.002; bv.segments = 1
    outline(g, 0.005)
    return g

def import_gun(name, L):
    if name == "Deagle": return build_deagle(L)
    antes = set(sc.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ARMAS, name + "_3p", name + "_3p.fbx"))
    novos = [o for o in sc.objects if o not in antes]
    objs = [o for o in novos if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    g = bpy.context.active_object
    mw = g.matrix_world.copy(); g.parent = None; g.matrix_world = mw
    for o in novos:
        if o.type != "MESH" and o.name in bpy.data.objects: bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT"); g.select_set(True); bpy.context.view_layer.objects.active = g
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = g.data
    vs = [v.co.copy() for v in me.vertices]
    mn = Vector([min(v[i] for v in vs) for i in range(3)]); mx = Vector([max(v[i] for v in vs) for i in range(3)])
    ext = mx - mn
    ax = max(range(3), key=lambda i: ext[i])
    rest = [i for i in range(3) if i != ax]
    up = max(rest, key=lambda i: ext[i])
    side = [i for i in rest if i != up][0]
    def slab(t0, t1): return [v for v in vs if t0 <= (v[ax] - mn[ax]) / ext[ax] <= t1]
    # boca: na ponta mais fina; embaixo: lado onde a arma desce mais a partir da linha do cano
    a, b = slab(0, 0.06), slab(0.94, 1)
    va = max(v[up] for v in a) - min(v[up] for v in a); vb = max(v[up] for v in b) - min(v[up] for v in b)
    sgn_ax = 1 if vb < va else -1
    muz = b if sgn_ax > 0 else a
    zc = sum(v[up] for v in muz) / len(muz)
    sgn_up = 1 if (mx[up] - zc) < (zc - mn[up]) else -1
    rows = []
    for tgt in range(3):
        src = [ax, side, up][tgt]; s_ = [sgn_ax, 1, sgn_up][tgt]
        row = [0, 0, 0]; row[src] = s_; rows.append(row)
    R = Matrix(rows)
    if R.determinant() < 0:
        rows[1] = [-x for x in rows[1]]; R = Matrix(rows)         # vira o lado em vez de espelhar
    me.transform(R.to_4x4())
    vs = [v.co.copy() for v in me.vertices]
    x0 = min(v.x for v in vs); x1 = max(v.x for v in vs)
    muz = [v for v in vs if v.x > x1 - 0.06 * (x1 - x0)]
    zc = sum(v.z for v in muz) / len(muz); yc = sum(v.y for v in vs) / len(vs)
    me.transform(Matrix.Translation((-x0, -yc, -zc)))
    me.transform(Matrix.Scale(L / (x1 - x0), 4))
    g.name = name
    tex = os.path.join(ARMAS, name + "_3p", name + "_3p_albedo.png")
    pt = PINTURA.get(name)
    g.data.materials.clear(); g.data.materials.append(toon(name, "#333333", tex, paint=(pt[0], pt[1] * L, pt[2] * L, pt[3] * L) if pt else None))
    outline(g, 0.006)
    return g

def split_mag(g, L, x0f, x1f, zf=None):
    me = g.data
    if zf is None:
        hand = [v.co.z for v in me.vertices if 0.62 * L <= v.co.x <= 0.70 * L]
        zcut = min(hand) - 0.004
    else:
        zcut = zf * L
    bm = bmesh.new(); bm.from_mesh(me)
    sel = set(v.index for v in bm.verts if x0f * L <= v.co.x <= x1f * L and v.co.z < zcut)
    for f in bm.faces: f.select = all(v.index in sel for v in f.verts)
    bm.to_mesh(me); bm.free()
    bpy.ops.object.select_all(action="DESELECT"); g.select_set(True); bpy.context.view_layer.objects.active = g
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
    mag = [o for o in bpy.context.selected_objects if o != g][0]; mag.name = g.name + "_pente"
    c = sum((v.co for v in mag.data.vertices), Vector()) / len(mag.data.vertices)
    top = max(v.co.z for v in mag.data.vertices)
    piv = Vector((c.x, 0, top))
    mag.data.transform(Matrix.Translation(-piv)); mag["piv"] = piv
    return mag

def mag_proxy(name, hx, hy, h):
    """Pente inteiro: corpo preto (topo na origem, desce até -h) com placa de base mais larga e clara."""
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    def cube(cx, cz, ax, ay, az, mi):
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r["verts"]: v.co = Vector((cx + v.co.x * 2 * ax, v.co.y * 2 * ay, cz + v.co.z * 2 * az))
        for f in {f for v in r["verts"] for f in v.link_faces}: f.material_index = mi
    cube(0, -h / 2, hx, hy, h / 2, 0)
    cube(0, -h - 0.004, hx * 1.25, hy * 1.2, 0.006, 1)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    o.data.materials.append(toon(name + "_corpo", "#2a2b2e")); o.data.materials.append(toon(name + "_base", "#5a5d62"))
    bv = o.modifiers.new("bv", "BEVEL"); bv.width = 0.003; bv.segments = 2
    outline(o, 0.004)
    o.location = (50, 50, -50)
    return o

class Gun:
    def __init__(s, name):
        cfg = WEAP[name]; s.name = name; s.cfg = cfg; s.L = cfg["L"]
        s.obj = import_gun(name, s.L)
        s.mag = s.mag2 = None
        s.magp = s.magp2 = None
        if "mag" in cfg:
            s.mag = split_mag(s.obj, s.L, *cfg["mag"])
            # pente com a textura crua (sem a pintura da arma: o da AWP é preto)
            s.mag.data.materials[0] = toon(name + "_pente", "#333333", None if name == "Deagle" else os.path.join(ARMAS, name + "_3p", name + "_3p_albedo.png"))
            s.mag2 = s.mag.copy(); s.mag2.data = s.mag.data; sc.collection.objects.link(s.mag2)
            if cfg["kind"] in ("smg", "pistol") or name in ("M4A4", "M4A1S"):
                # fora da arma o pente aparece inteiro (dentro do punho só o pé dele fica à mostra): bloco preto
                # com placa de base, do tamanho do pente de verdade
                vs_ = [v.co for v in s.mag.data.vertices]
                hx = max((max(v.x for v in vs_) - min(v.x for v in vs_)) * 0.42, 0.012)
                hy = max((max(v.y for v in vs_) - min(v.y for v in vs_)) * 0.45, 0.011)
                if cfg["kind"] == "pistol":   # pente de pistola: um pouco menor que o punho (não pelo comprimento: a USP tem silenciador)
                    h, hx, hy = 0.058, min(hx, 0.011), min(hy, 0.008)
                elif cfg["kind"] == "rifle":   # M4: pente reto de fuzil (o recortado do modelo saía quebrado)
                    h, hx, hy = 0.21 * s.L, 0.016, 0.009
                else:
                    h = 0.4 * s.L
                s.magp = mag_proxy(name + "_pente_inteiro", hx, hy, h)
                s.magp2 = s.magp.copy(); s.magp2.data = s.magp.data; sc.collection.objects.link(s.magp2)
        vs = [v.co for v in s.obj.data.vertices]
        def mid(a, b, low=False):
            sl = [v for v in vs if a * s.L <= v.x <= b * s.L] or vs
            z0 = min(v.z for v in sl); z1 = max(v.z for v in sl)
            return Vector((((a + b) / 2) * s.L, 0, (z0 * 0.65 + z1 * 0.35) if low else (z0 + z1) / 2))
        s.grip = mid(*cfg["grip"], low=True)
        s.fore = mid(*cfg["fore"])
        if cfg["kind"] == "rifle":   # mão esquerda POR BAIXO do guarda-mão (palma apoiando), não de lado no meio dele
            sl = [v for v in vs if cfg["fore"][0] * s.L <= v.x <= cfg["fore"][1] * s.L] or vs
            s.fore = Vector((s.fore.x, 0.004, min(v.z for v in sl) - 0.016))
        s.bolt = Vector((0.40 * s.L, -0.03, 0.012))
        s.ocular = Vector((0.215 * s.L, 0, 0.042)) if cfg.get("bolt") else None
        for o in (s.obj, s.mag, s.mag2, s.magp, s.magp2):
            if o: hide(o)

# ------------------------------------------------------------------ agentes
CORES = {
    "ct": dict(pele="#e2ad86", capuz="#2c3446", borracha="#4a4e52", aro="#b3b9c0", vidro="#5e9fb2", brilho="#ffffff", valvula="#25272b",
               filtro="#62676d", farda="#3a4562", colete="#5c6644", preto="#1f2024", luva="#2b2c30", bota="#1d1d20"),
    # TR mercenário de balaclava: camiseta cinza, colete grafite com placa, calça cáqui manchada, coldre de couro
    "tr": dict(pele="#c68a62", balaclava="#3a2a22", olho="#1a1414", brilho="#ffffff", labio="#a0574a", branco="#f2ede6",
               camiseta="#6f7175", colete="#3a3b40", placa="#46474c", couro="#4e3220", fivela="#b8923e",
               calca="#b4a68a", luva="#1e1e22", bota="#4a3020"),
}
XADREZ = {}

# granadas na mão: peças (elipsoides) em volta do centro da granada: deslocamento e raios
NADE_PARTS = {"smoke": ["sm_corpo", "sm_faixa", "sm_topo"], "flash": ["fl_corpo", "fl_faixa", "fl_topo"],
              "molotov": ["mo_garrafa", "mo_gargalo", "mo_pano"], "he": ["he_corpo", "he_topo"]}
NADE_GEOM = {
    "sm_corpo": ((0, 0, 0), (0.03, 0.03, 0.05)), "sm_faixa": ((0, 0, 0.012), (0.032, 0.032, 0.01)), "sm_topo": ((0, 0, 0.056), (0.015, 0.015, 0.012)),
    "fl_corpo": ((0, 0, 0), (0.025, 0.025, 0.048)), "fl_faixa": ((0, 0, -0.012), (0.027, 0.027, 0.008)), "fl_topo": ((0, 0, 0.052), (0.013, 0.013, 0.012)),
    "mo_garrafa": ((0, 0, 0), (0.032, 0.032, 0.045)), "mo_gargalo": ((0, 0, 0.055), (0.012, 0.012, 0.025)), "mo_pano": ((0, 0, 0.086), (0.017, 0.017, 0.02)),
    "he_corpo": ((0, 0, 0), (0.035, 0.035, 0.038)), "he_topo": ((0, 0, 0.042), (0.012, 0.012, 0.012)),
}
C4_K = 2.0   # tamanho da C4 (usuário: maiorzinha, não gigante)
# C4 (CS): tijolo de explosivo embrulhado em papel pardo, duas fitas pretas, teclado com tela verde e botões, fio vermelho
C4_PECAS = {"bomba": ((0.058, 0.042, 0.024), "#a8875a"), "bombaF0": ((0.009, 0.0445, 0.0255), "#2b2b2b"),
            "bombaF1": ((0.009, 0.0445, 0.0255), "#2b2b2b"), "bombaT": ((0.03, 0.028, 0.006), "#3a3d40"),
            "bombaL": ((0.011, 0.02, 0.002), "#8fdc6a"), **{"bombaK%d" % i: ((0.0048, 0.0048, 0.002), "#c9ccd0") for i in range(4)}}
PROP_COR = {"sm_corpo": "#7a7d80", "sm_faixa": "#d8d8d0", "sm_topo": "#2a2b2e", "fl_corpo": "#c9cbc6", "fl_faixa": "#2a2b2e",
            "fl_topo": "#3a3b3e", "mo_garrafa": "#4f8a4a", "mo_gargalo": "#4f8a4a", "mo_pano": "#e07a2a", "he_corpo": "#5a6638",
            "he_topo": "#3a3b3e", "capsula": "#c9a24a", "pino": "#9aa0a6", "bomba": "#4a5530", "alicate": "#8a2a20",
            "alca_arma": "#2a2420", "isqueiro": "#b9bec4", "isq_tampa": "#8d9298"}
# peças da cabeça (giram juntas no "cego")
HEAD_KEYS = ["cabeca", "mascara", "focinho", "filtro", "tampa", "aroL", "aroR", "lenteL", "lenteR", "reflexoL", "reflexoR",
             *["mascaraV%d" % i for i in range(3)], "furoL", "furoR", "boca", "labio", "brancoL", "brancoR", "olhoL", "olhoR", "brilhoL", "brilhoR"]
PROP_BRILHO = {"clarao": "#ffd25a", "clarao2": "#fff6d8", "estrela0": "#ffe36a", "estrela1": "#ffe36a", "estrela2": "#ffe36a",
               "visor": "#ff3a2a", "luneta": "#9fe6ff", **{"rastro%d" % i: "#f6f4ea" for i in range(10)},
               "chama_isq": "#ffb43a", "fogo0": "#ff7a1a", "fogo1": "#ffb43a", "fogo2": "#ffe08a"}


def build_knife(team, parent):
    """Baioneta militar autoral (no estilo de uma M9), clara pra ler de longe: lâmina comprida de aço claro com ponta
    recortada, fio brilhante embaixo, serrilha no lombo e a fenda oval do corta-arame perto da guarda; guarda cruzada
    com anel em cima; cabo comprido cinza-claro com sulcos e pomo arredondado. Eixo X = ponta, Z = lombo; origem no
    meio do cabo."""
    root = bpy.data.objects.new(team + "_faca", None); sc.collection.objects.link(root); root.parent = parent
    aco = toon(team + "_aco", "#dfe4e8"); fio = toon(team + "_fio", "#ffffff"); serra = toon(team + "_serra", "#b4bcc4")
    fenda = toon(team + "_fenda", "#7c848c"); metal = toon(team + "_guarda", "#a9b1b8")
    cabo = toon(team + "_cabo", "#9a9f97"); sulco = toon(team + "_sulco", "#767b74")
    def prism(name, pts, half_y, mat, th=0.003):
        me = bpy.data.meshes.new(name); bm = bmesh.new()
        f = bm.faces.new([bm.verts.new((x, -half_y, z)) for x, z in pts])
        r = bmesh.ops.extrude_face_region(bm, geom=[f])
        for v in [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]: v.co.y += 2 * half_y
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new(name, me); sc.collection.objects.link(o); o.parent = root
        o.data.materials.append(mat); outline(o, th)
        return o
    x0, L = 0.045, 0.17   # começo e comprimento da lâmina
    # corpo da lâmina (aço claro) com ponta recortada; fio branco embaixo
    prism("lamina", [(x0, -0.006), (x0, 0.017), (x0 + 0.62 * L, 0.017), (x0 + 0.8 * L, 0.012), (x0 + L, -0.001),
                     (x0 + 0.82 * L, -0.006)], 0.0035, aco)
    prism("fio", [(x0, -0.006), (x0 + 0.82 * L, -0.006), (x0 + L, -0.001), (x0 + 0.85 * L, -0.013), (x0 + 0.5 * L, -0.019),
                  (x0 + 0.08 * L, -0.02), (x0, -0.02)], 0.0028, fio)
    # serrilha no lombo: dentes em zigue-zague
    pts = [(x0 + 0.06 * L, 0.017)]
    for i in range(9):
        xa = x0 + (0.06 + i * 0.055) * L
        pts += [(xa + 0.0275 * L, 0.024), (xa + 0.055 * L, 0.017)]
    pts += [(x0 + 0.06 * L, 0.0165)]
    prism("serra", pts, 0.003, serra)
    # fenda oval do corta-arame (perto da guarda, no meio da lâmina)
    prism("fenda", [(x0 + 0.08 * L + 0.022 * math.cos(a), 0.004 + 0.006 * math.sin(a)) for a in [i * math.pi / 8 for i in range(16)]],
          0.0042, fenda, 0.0015)
    # guarda cruzada com anel em cima
    prism("guarda", [(x0 - 0.009, -0.03), (x0 - 0.009, 0.026), (x0, 0.026), (x0, -0.03)], 0.009, metal)
    anel = [(x0 - 0.0045 + 0.012 * math.cos(a), 0.038 + 0.012 * math.sin(a)) for a in [i * math.pi / 8 for i in range(16)]]
    prism("anel", anel, 0.006, metal)
    prism("anel_furo", [(x0 - 0.0045 + 0.006 * math.cos(a), 0.038 + 0.006 * math.sin(a)) for a in [i * math.pi / 8 for i in range(16)]],
          0.0065, sulco, 0.0012)
    # cabo comprido com sulcos e pomo
    prism("cabo", [(-0.068, -0.014), (-0.068, 0.012), (x0 - 0.009, 0.013), (x0 - 0.009, -0.016)], 0.0095, cabo)
    for i in range(6):
        xa = -0.06 + i * 0.017
        prism("sulco%d" % i, [(xa, -0.0155), (xa, 0.0135), (xa + 0.004, 0.0135), (xa + 0.004, -0.0155)], 0.0102, sulco, 0.001)
    prism("pomo", [(-0.084, -0.011), (-0.082, 0.009), (-0.068, 0.014), (-0.068, -0.015), (-0.08, -0.014)], 0.011, metal)
    return root

def glow(name, col):
    """Material que brilha (fogo, clarão): cor chapada, sem sombra."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = srgb(col)
    out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], out.inputs[0])
    return m

# textura das roupas: trama de tecido (mottle) e fitas costuradas no colete (molle)
TECIDO = {"farda", "camiseta", "calca", "balaclava", "capuz"}
FITAS = {"colete", "placa"}

class Agent:
    def __init__(s, team):
        s.team = team; ct = team == "ct"; C = CORES[team]
        M = {k: toon(team + "_" + k, v, stains=False, checker=XADREZ.get(k), mottle=k in TECIDO, molle=k in FITAS)
             for k, v in C.items()}
        s.root = bpy.data.objects.new(team, None); sc.collection.objects.link(s.root)
        s.P = {}
        def E(key, mat, seg=24): o = ell(team + key, M[mat], seg); o.parent = s.root; s.P[key] = o
        def B(key, mat, half, bev=0.008): o = box(team + key, M[mat], half, bev); o.parent = s.root; s.P[key] = o
        legs = "farda" if ct else "calca"
        for sd in ("L", "R"):
            E("coxa" + sd, legs); E("canela" + sd, legs); E("bota" + sd, "bota")
            if ct: E("joelheira" + sd, "preto")
        E("quadril", legs)
        E("cinto", "preto" if ct else "couro")
        if ct:
            B("coldre", "preto", (0.034, 0.02, 0.07), 0.012); E("perneira", "preto"); E("perneira2", "preto")
            B("coldreAba", "preto", (0.037, 0.024, 0.013), 0.008); B("coldreCabo", "luva", (0.015, 0.012, 0.028), 0.006); E("coldreAlca", "preto")
            E("tronco", "farda"); B("colete", "colete", (0.106, 0.134, 0.094), 0.018)
            for i in range(3): B("pente%d" % i, "colete", (0.022, 0.026, 0.042), 0.006)
            for i in range(2): B("bolsinho%d" % i, "colete", (0.018, 0.03, 0.02), 0.005)
            B("radio", "preto", (0.02, 0.018, 0.034), 0.005); E("antena", "preto")
            for sd in ("L", "R"): B("alca" + sd, "colete", (0.07, 0.03, 0.012), 0.005)
            E("gola", "farda")
            E("cabeca", "capuz", 32); E("mascara", "borracha", 32); B("focinho", "borracha", (0.022, 0.044, 0.034), 0.014); E("filtro", "filtro"); E("tampa", "aro")
            for i in range(3): B("mascaraV%d" % i, "valvula", (0.004, 0.024, 0.004), 0.002)   # grade da válvula de fala   # tiras da máscara por cima do capuz (aparecem de cima)
            for sd in ("L", "R"):
                E("aro" + sd, "aro"); E("lente" + sd, "vidro"); E("reflexo" + sd, "brilho")

        else:
            B("bolsa", "couro", (0.03, 0.02, 0.035)); B("fivela", "fivela", (0.006, 0.02, 0.014), 0.003)
            B("coldre", "couro", (0.034, 0.02, 0.07), 0.012); E("perneira", "couro"); E("perneira2", "couro")
            B("coldreAba", "couro", (0.037, 0.024, 0.013), 0.008); B("coldreCabo", "luva", (0.015, 0.012, 0.028), 0.006); E("coldreAlca", "couro")
            E("tronco", "camiseta"); B("colete", "colete", (0.104, 0.134, 0.092), 0.016)
            B("placa", "placa", (0.012, 0.085, 0.07), 0.01)
            for i in range(2): B("pente%d" % i, "colete", (0.02, 0.028, 0.03), 0.006)
            for sd in ("L", "R"): B("alca" + sd, "colete", (0.07, 0.03, 0.012), 0.005)
            E("gola", "camiseta")
            E("cabeca", "balaclava", 32); E("furoL", "pele"); E("furoR", "pele"); E("boca", "pele"); E("labio", "labio")
            for sd in ("L", "R"): E("branco" + sd, "branco"); E("olho" + sd, "olho"); E("brilho" + sd, "brilho")
        for sd in ("L", "R"):
            E("braco" + sd, "farda" if ct else "camiseta")
            E("ante" + sd, "farda" if ct else "pele")
            if not ct: E("pulso" + sd, "pele")   # TR (manga curta): pele do braço até o cotovelo + antebraço
            E("mao" + sd, "luva")
        s.guns = {}
        # objetos de cena que o boneco usa (ficam escondidos até a animação pedir)
        s.knife = build_knife(team, s.root)
        s.props = {}
        for nm, col in PROP_COR.items():
            if nm == "bomba": continue   # (a C4 sai do C4_PECAS, logo abaixo)
            elif nm in ("isqueiro", "isq_tampa"): o = box(team + nm, toon(team + nm, col), (0.016, 0.01, 0.024 if nm == "isqueiro" else 0.01), 0.004)
            else: o = ell(team + nm, toon(team + nm, col))
            o.parent = s.root; s.props[nm] = o
        for nm, (half, col) in C4_PECAS.items():
            half = tuple(h * C4_K for h in half)
            o = box(team + nm, toon(team + nm, col), half, min(half) * 0.5); o.parent = s.root; s.props[nm] = o
        o = ell(team + "bombaFio", toon(team + "bombaFio", "#c0392b")); o.parent = s.root; s.props["bombaFio"] = o
        for nm, col in PROP_BRILHO.items():
            if nm == "visor": o = box(team + nm, glow(team + nm, col), (0.005, 0.005, 0.004), 0.002)   # LED vermelho
            else: o = ell(team + nm, glow(team + nm, col))
            o.parent = s.root; s.props[nm] = o

    def gun(s, name):
        if name not in s.guns:
            g = Gun(name)
            for o in (g.obj, g.mag, g.mag2, g.magp, g.magp2):
                if o: o.parent = s.root
            s.guns[name] = g
        return s.guns[name]

    def pose(s, t, anim, gname, nade="smoke", pitch=0.0, from_scope=False):
        """Pose no instante t (0..1) da animação. Movimento: mira, andar, devagar, correr, agachar, agachado,
        pular, escada. Ação: tiro, recarga, granada, cego, plantar, desarmar, morrer."""
        P = s.P; ct = s.team == "ct"
        _id = (anim, gname, nade, round(pitch, 4), from_scope)
        if getattr(s, "_pose_id", None) != _id or t < getattr(s, "_pose_t", -1): COTOVELO_ANT.clear()
        s._pose_id, s._pose_t = _id, t
        for g in s.guns.values():
            for o in (g.obj, g.mag, g.mag2, g.magp, g.magp2):
                if o: hide(o)
        for o in s.props.values(): hide(o)
        hide(s.knife)
        s.root.location = (0, 0, 0)
        G = s.gun(gname); kind = G.cfg["kind"]
        def k(a, b): return min(max((t - a) / (b - a), 0), 1)
        def sm(x): return x * x * (3 - 2 * x)
        # ---------------------------------------------------------- pernas e altura do quadril
        hipz = 0.30; lean = 0.0; twist = 0.0; stride = lift = bob = 0.0; step = None
        feet = {"L": Vector((0.0, 0.07, 0.06)), "R": Vector((0.0, -0.07, 0.06))}
        kneel = False
        # andar como no CS: normal, devagar (shift), de costas, de lado (AD) e com a faca (mais rápido);
        # walk_dir = direção do passo no corpo (a mira continua pra frente)
        walk_dir = Vector((1, 0, 0))
        # passada longa (perninha de chibi com passo curto quase não mostrava a perna; e ciclo lento lê melhor no vídeo)
        GAITS = {"andar": (0.17, 0.1, 0.02), "devagar": (0.075, 0.035, 0.006), "agachado": (0.1, 0.05, 0.006),
                 "tras": (0.1, 0.06, 0.01), "lado_esq": (0.085, 0.06, 0.01), "lado_dir": (0.085, 0.06, 0.01),
                 "faca_andar": (0.18, 0.11, 0.025)}
        if anim in GAITS:
            stride, lift, bob = GAITS[anim]
            step = t * 2 * math.pi
            walk_dir = {"tras": Vector((-1, 0, 0)), "lado_esq": Vector((0, 1, 0)), "lado_dir": Vector((0, -1, 0))}.get(anim, walk_dir)
        if anim == "devagar": hipz -= 0.02
        # a perna (0,13 + 0,13) só alcança a passada se o quadril baixar um pouco (joelho dobrado de quem corre);
        # senão o IK estica a perna no limite, o pé anda menos que o corpo e o boneco patina
        if anim == "andar": hipz -= 0.065    # alcança 0,17: vertical <= raiz(0,26² - 0,17²) = 0,197 (com o balanço)
        if anim == "faca_andar": hipz -= 0.085
        if anim == "faca_andar": lean = 0.08
        if anim in ("lado_esq", "lado_dir"): lean = 0.03
        if anim in ("agachar", "agachado"):
            hipz = 0.2; lean = 0.1
            if anim == "agachar": feet["L"] = Vector((0.07, 0.08, 0.06)); feet["R"] = Vector((-0.07, -0.08, 0.06))
        if anim == "pular":
            dip = 0.06 * (sm(k(0.0, 0.12)) - sm(k(0.12, 0.22))) + 0.05 * (sm(k(0.74, 0.84)) - sm(k(0.86, 1.0)))
            hipz -= dip
            if 0.2 < t < 0.8: s.root.location.z = 0.16 * math.sin(math.pi * (t - 0.2) / 0.6)
            tuck = sm(k(0.22, 0.35)) - sm(k(0.62, 0.76))
            for sd in feet: feet[sd] = feet[sd] + Vector((-0.04, 0, 0.09)) * tuck
        # no ar (o jogo move a altura; aqui só a pose): subindo = pernas encolhidas; caindo = pernas descendo pro chão,
        # joelho meio dobrado pra aterrissar. Com arma na mão ou de faca (faca_no_ar_*)
        if anim.endswith("no_ar_sobe"):
            for sd in feet: feet[sd] = feet[sd] + Vector((-0.035, 0, 0.12))
            lean = 0.05
        if anim.endswith("no_ar_cai"):
            feet["L"] = feet["L"] + Vector((0.05, 0.01, 0.03)); feet["R"] = feet["R"] + Vector((-0.03, -0.01, 0.05))
            lean = -0.03
        if anim == "escada":
            # sobe a escada de mão (na frente): pés nos degraus, alternando
            hipz = 0.30
            for sd, ph in (("L", 0.0), ("R", math.pi)):
                u = 0.5 + 0.5 * math.sin(2 * math.pi * t + ph)
                feet[sd] = Vector((0.1, 0.06 if sd == "L" else -0.06, 0.06 + 0.16 * u))
        # bomba: agachado (pés afastados, tronco pra frente) ou em pé (debruçado), como no CS
        if anim in ("plantar", "desarmar"):
            # ajoelhado no joelho direito, pé esquerdo à frente (antes pés abertos com os joelhos caindo pra dentro)
            hipz = 0.17; lean = 0.36; kneel = True
            feet["L"] = Vector((0.1, 0.085, 0.06)); feet["R"] = Vector((-0.11, -0.065, 0.06))
        # pegar do chão / largar (arma, granada, C4): agacha e debruça; comemorar: pulinhos; lamentar: ombro caído
        if anim == "pegar":
            dobra = sm(k(0.0, 0.38)) - sm(k(0.62, 1.0))
            hipz -= 0.1 * dobra; lean = 0.42 * dobra
            for sd, sg in (("L", 1), ("R", -1)): feet[sd] = feet[sd].lerp(Vector((sg * 0.07, sg * 0.08, 0.06)), dobra)
        if anim == "comemorar":
            pulo = abs(math.sin(2 * math.pi * t)); s.root.location.z = 0.07 * pulo
        if anim == "lamentar":
            lean = -0.04; hipz -= 0.01
        if anim == "morrer_frente":   # pernas esticadas (tomba de cara)
            feet["L"] = Vector((0.0, 0.075, 0.045)); feet["R"] = Vector((0.0, -0.075, 0.045))
        if anim in ("plantar_pe", "desarmar_pe"):
            lean = 0.62
            feet["L"] = Vector((0.03, 0.08, 0.06)); feet["R"] = Vector((-0.03, -0.08, 0.06))
        if anim == "morrer":   # pernas esticadas (deitado não fica de joelho dobrado)
            feet["L"] = Vector((0.02, 0.075, 0.045)); feet["R"] = Vector((0.02, -0.075, 0.045))
        # corrida (andar normal e faca): cada pé fica no chão só uma fração do ciclo (CORRIDA = fração) e o corpo voa
        # entre os passos — o ciclo cobre mais chão sem o pé patinar (perna curta de chibi andando com pé sempre no
        # chão precisava de 7 passos por segundo). Quadril mais baixo no meio do apoio, mais alto no voo
        CORRIDA = {"andar": 0.35, "faca_andar": 0.32}
        st_frac = CORRIDA.get(anim)
        if step is not None and st_frac:
            ub = t % 1.0; meio_voo = (st_frac + 0.5) / 2
            bob = bob * math.cos(4 * math.pi * (ub - meio_voo))
        elif step is not None: bob = bob * abs(math.sin(step))
        hipz += bob
        LOWER = []
        for sd, sg in (("L", 1), ("R", -1)):
            hip = Vector((0, sg * 0.06, hipz)); ank = feet[sd].copy()
            if step is not None:
                p = step + (0 if sg > 0 else math.pi)
                if walk_dir.y != 0:   # passo de lado: pés abrem e fecham sem cruzar
                    ank.y += walk_dir.y * stride * (0.5 + 0.5 * math.sin(p)) * (1 if (walk_dir.y > 0) == (sg > 0) else -0.35)
                    ank.z += max(0, math.cos(p)) * lift
                else:
                    # passo de verdade: meio ciclo com o pé PLANTADO, indo pra trás em velocidade constante (= a do
                    # corpo: o corpo anda 4 x stride por ciclo), meio ciclo no ar voltando pra frente
                    u = (p / (2 * math.pi)) % 1.0
                    stf = st_frac or 0.5     # andando: meio ciclo no chão; correndo: só a fração de apoio
                    if u < stf: dx = stride * (1 - 2 * u / stf); up = 0.0
                    else:
                        v = (u - stf) / (1 - stf); dx = -stride + 2 * stride * sm(v); up = math.sin(math.pi * v) * lift
                    ank.x += walk_dir.x * dx; ank.z += up
            pole = Vector((1, sg * 0.3, 0.2)) if kneel else Vector((1, 0, 0.2))   # ajoelhado: joelho da frente pra fora
            if kneel and sd == "R": pole = Vector((0.2, 0, -1))   # joelho direito no chão
            knee, ank = ik(hip, ank, 0.13, 0.13, pole)
            if kneel and sd == "R": knee = Vector((knee.x, knee.y, max(knee.z, 0.05)))
            place_seg(P["coxa" + sd], hip, knee, 0.054); place_seg(P["canela" + sd], knee, ank, 0.046)
            place(P["bota" + sd], ank + Vector((0.03, 0, -0.03)), (0.078, 0.05, 0.042))
            LOWER += [P["coxa" + sd], P["canela" + sd], P["bota" + sd]]
            if ct: place(P["joelheira" + sd], knee + Vector((0.038, 0, 0)), (0.026, 0.042, 0.04)); LOWER.append(P["joelheira" + sd])
            if sd == "R":   # coldre de perna (os dois times): segue a coxa, duas faixas em volta dela, alça no cinto
                up = (hip - knee).normalized(); fora = Vector((0, -1, 0)); fora = (fora - up * fora.dot(up)).normalized()
                qc = Matrix((fora.cross(up), fora, up)).transposed().to_quaternion()   # X frente, Y fora, Z sobe a coxa
                cc = hip.lerp(knee, 0.6) + fora * 0.07
                coldre_em = (cc, up, fora)   # a pistola guardada entra aqui (back_pos da pistola)
                place(P["coldre"], cc, None, qc)
                place(P["coldreAba"], cc + up * 0.067 + fora * 0.002, None, qc)
                # cabo da pistola saindo do coldre (só quando a pistola não é a arma: aí ela mesma vai pro coldre)
                if kind != "pistol":
                    place(P["coldreCabo"], cc + up * 0.089 - fora.cross(up) * 0.012, None, qc @ Quaternion((0, 1, 0), math.radians(-18)))
                    LOWER.append(P["coldreCabo"])
                else: hide(P["coldreCabo"])
                for nm, f in (("perneira", 0.42), ("perneira2", 0.72)):   # faixa = disco fino atravessando a coxa
                    place(P[nm], hip.lerp(knee, f), (0.06, 0.06, 0.008), up.to_track_quat("Z", "Y"))
                place_seg(P["coldreAlca"], Vector((0.0, -0.105, hipz + 0.04)), cc + up * 0.075 + fora * 0.004, 0.009)
                LOWER += [P["coldre"], P["coldreAba"], P["perneira"], P["perneira2"], P["coldreAlca"]]
        place(P["quadril"], Vector((0, 0, hipz + 0.01)), (0.085, 0.115, 0.06))
        place(P["cinto"], Vector((0, 0, hipz + 0.045)), (0.095, 0.122, 0.022))
        ch = Vector((0, 0, hipz + 0.15))
        place(P["tronco"], ch, (0.1, 0.13, 0.13))
        place(P["colete"], ch + Vector((0.0, 0, 0.005)))   # centrado no tronco: cobre frente E costas
        for sd, sg in (("L", 1), ("R", -1)): place(P["alca" + sd], ch + Vector((0, sg * 0.085, 0.1)))
        if ct:
            for i in range(3): place(P["pente%d" % i], ch + Vector((0.1, (i - 1) * 0.06, -0.035)))
            for i in range(2): place(P["bolsinho%d" % i], ch + Vector((0.098, (i - 0.5) * 0.08, 0.05)))
            place(P["radio"], ch + Vector((0.07, 0.095, 0.075)))
            place_seg(P["antena"], ch + Vector((0.07, 0.1, 0.11)), ch + Vector((0.055, 0.11, 0.21)), 0.006)
            place(P["gola"], ch + Vector((0, 0, 0.12)), (0.07, 0.09, 0.035))
        else:
            place(P["placa"], ch + Vector((0.104, 0, 0.012)))
            for i in range(2): place(P["pente%d" % i], ch + Vector((0.098, (i - 0.5) * 0.1, -0.06)))
            place(P["bolsa"], Vector((0.03, -0.12, hipz + 0.02))); place(P["fivela"], Vector((0.098, 0, hipz + 0.045)))
            place(P["gola"], ch + Vector((0, 0, 0.12)), (0.068, 0.088, 0.032))
        # cabeça
        hc = ch + Vector((0.01, 0, 0.27)); hr = Vector((0.17, 0.175, 0.165))
        place(P["cabeca"], hc, tuple(hr))
        def surf(p, k=1.02):
            q = p - hc; e = math.sqrt((q.x / hr.x) ** 2 + (q.y / hr.y) ** 2 + (q.z / hr.z) ** 2)
            return hc + q / max(e, 1e-4) * k
        if ct:
            # máscara de gás: peça cinza no rosto, lentes grandes com aro claro e reflexo, válvula no meio,
            # filtro curto na bochecha esquerda (traços no alto do rosto: a câmera vem de cima)
            # borracha cinza (contrasta com o capuz azul) e aro de metal claro nas lentes
            place(P["mascara"], hc + Vector((0.09, 0, -0.015)), (0.105, 0.148, 0.13))
            for sd, sg in (("L", 1), ("R", -1)):
                place(P["aro" + sd], hc + Vector((0.166, sg * 0.06, 0.04)), (0.02, 0.058, 0.058))
                place(P["lente" + sd], hc + Vector((0.178, sg * 0.06, 0.04)), (0.012, 0.045, 0.045))
                place(P["reflexo" + sd], hc + Vector((0.188, sg * 0.048, 0.06)), (0.005, 0.015, 0.012))
            # máscara S10/FM12 (CT SAS do CS): na frente só a válvula de exalação baixa e larga, colada na borracha, com
            # a grade de fala; o filtro é uma lata redonda rosqueada de lado, na bochecha esquerda, apontando pra baixo
            qv = Quaternion((0, 1, 0), math.radians(28))   # a frente da válvula olha pra frente e pra baixo
            vf = hc + Vector((0.186, 0, -0.066))
            place(P["focinho"], vf, (0.022, 0.044, 0.034), qv)
            for i in range(3): place(P["mascaraV%d" % i], vf + qv @ Vector((0.022, 0, 0.016 - i * 0.016)), (0.004, 0.026 - abs(i - 1) * 0.005, 0.004), qv)
            eixo = Vector((0.62, 0.62, -0.48)).normalized()   # pra frente, pra fora e pra baixo
            base = hc + Vector((0.15, 0.095, -0.088))
            place(P["filtro"], base + eixo * 0.026, (0.042, 0.042, 0.034), eixo.to_track_quat("Z", "X"))
            place(P["tampa"], base + eixo * 0.058, (0.034, 0.034, 0.007), eixo.to_track_quat("Z", "X"))
        else:
            # balaclava lisa: furos dos olhos e da boca são "adesivos" achatados deitados na superfície da cabeça
            # (nada sai pra fora), com o olho por cima em camadas finas: pele, branco, pupila, brilho
            def decal(o, d, w, h, off, thick=0.003):
                p = surf(hc + d, 1.0); q = p - hc
                n = Vector((q.x / hr.x ** 2, q.y / hr.y ** 2, q.z / hr.z ** 2)).normalized()
                place(o, p + n * off, (thick, w, h), n.to_track_quat("X", "Z"))
            for sd, sg in (("L", 1), ("R", -1)):
                decal(P["furo" + sd], Vector((0.15, sg * 0.054, 0.05)), 0.036, 0.024, 0.0)
                decal(P["branco" + sd], Vector((0.15, sg * 0.054, 0.05)), 0.024, 0.016, 0.002)
                decal(P["olho" + sd], Vector((0.15, sg * 0.05, 0.05)), 0.012, 0.014, 0.004)
                decal(P["brilho" + sd], Vector((0.15, sg * 0.046, 0.056)), 0.004, 0.004, 0.006)
            decal(P["boca"], Vector((0.15, 0, -0.06)), 0.026, 0.014, 0.0)
            decal(P["labio"], Vector((0.15, 0, -0.06)), 0.017, 0.005, 0.002)
        # ---------------------------------------------------------- arma e mãos
        PR = s.props
        def c4(c, q=None):
            """Monta a C4 com centro em c (q: giro; padrão alinhado com o boneco, teclado em cima)."""
            q = q or Quaternion(); R = q.to_matrix()
            for nm, (half, _) in C4_PECAS.items():
                off = {"bomba": (0, 0, 0), "bombaF0": (0.032, 0, 0), "bombaF1": (-0.032, 0, 0), "bombaT": (-0.006, 0, 0.029),
                       "bombaL": (0.006, 0, 0.036)}.get(nm)
                if off is None:   # botões 2x2 atrás da tela
                    i = int(nm[-1]); off = (-0.014 - 0.012 * (i // 2), (i % 2 - 0.5) * 0.016, 0.036)
                place(PR[nm], c + R @ (Vector(off) * C4_K), None, q)
            place(PR["visor"], c + R @ (Vector((0.006, 0.026, 0.036)) * C4_K), None, q)
            place_seg(PR["bombaFio"], c + R @ (Vector((-0.03, -0.024, 0.03)) * C4_K), c + R @ (Vector((-0.05, -0.04, 0.012)) * C4_K), 0.0045 * C4_K)
        gq = Quaternion()
        if kind == "pistol": gpos = ch + Vector((0.2, -0.02, 0.06))
        elif kind == "smg": gpos = ch + Vector((0.0, -0.06, 0.03))
        else:   # fuzil do lado direito, rente por FORA do colete (antes no meio do peito: a coronha entrava 9 cm no
            # colete e o braço direito cruzava o peito), apontando um tiquinho pro centro
            gpos = ch + Vector((0.0, -0.145, -0.01)); gq = Quaternion((0, 0, 1), math.radians(5))
        aim_pos, aim_q = gpos.copy(), gq.copy()
        # arma baixa (correndo / segurando com a esquerda do lado) e nas costas (escada, bomba)
        low_q = Quaternion((0, 1, 0), math.radians(32)) @ Quaternion((0, 0, 1), math.radians(20))
        low_pos = ch + Vector((0.02, -0.08, -0.08))
        side_q = Quaternion((0, 1, 0), math.radians(70)); side_pos = ch + Vector((0.02, 0.15, -0.06))   # na mão esquerda, cano pra baixo
        # nas costas, pendurada na bandoleira (jeito clássico): coronha atrás do quadril esquerdo, cano subindo na
        # diagonal até passar do ombro direito, lado de cima da arma virado pra fora (pra trás). Pistola: coldre.
        dx = Vector((-0.06, -0.3, 0.95)).normalized(); dz = Vector((-1, 0, 0)); dz = (dz - dx * dz.dot(dx)).normalized()
        back_q = Matrix((dx, dz.cross(dx), dz)).transposed().to_quaternion()
        back_pos = ch + Vector((-0.17, 0.1, -0.2))   # (2 cm mais pra trás: o punho não fica dentro do colete)
        if kind == "pistol":   # dentro do coldre da coxa: cano pra baixo, cabo pra trás saindo por cima
            cc_, up_, fora_ = coldre_em; fr_ = fora_.cross(up_)
            back_q = Matrix((-up_, fr_.cross(up_), -fr_)).transposed().to_quaternion()
            back_pos = cc_ + up_ * 0.088 + fr_ * 0.008
        def blend(p0, q0, p1, q1, u): return p0.lerp(p1, u), q0.slerp(q1, u)
        free_hands = False      # mãos não seguem a arma
        if anim == "correr" and kind != "pistol":
            gpos, gq = low_pos, low_q
        # AWP na luneta (referência: AWPer do CS2 em 3ª pessoa): ocular da luneta colada na lente do olho direito
        # (lente em hc + (0,176; -0,06; 0,038); luneta ~0,045 acima do cano, ocular a ~0,28 do fim da coronha), fuzil
        # nivelado na altura do olho, coronha entrando na bochecha (cheek weld; o chibi tem a cabeça grande)
        # (com a cabeça já inclinada/virada pra luneta: head_turn 0,1/-0,12 leva a lente pra ~(0,203; -0,082; 0,289))
        # o olho direito com a cabeça na pose da luneta (HT_LUNETA) + 3,5 cm de alívio; a arma vai onde a ocular cai ali
        HT_LUNETA = (0.2, -0.12)   # cabeça desce pra luneta (olho mais perto da arma e do braço curto)
        _n = ch + Vector((0, 0, 0.13))
        _Mh = Matrix.Translation(_n) @ Matrix.Rotation(HT_LUNETA[1], 4, "Z") @ Matrix.Rotation(HT_LUNETA[0], 4, "Y") @ Matrix.Translation(-_n)
        olho_d = _Mh @ (ch + Vector((0.01, 0, 0.27)) + Vector((0.176, -0.06, 0.038 if ct else 0.05)))
        SCOPED = (olho_d + _Mh.to_3x3() @ Vector((0.035, 0, 0)) - G.ocular) if G.ocular is not None else ch + Vector((-0.065, -0.08, 0.245))
        scope_u = 0.0
        if anim == "recarga":
            if from_scope:   # sai da luneta pra posição normal antes de recarregar
                scope_u = 1 - sm(k(0.0, 0.12)); gpos = aim_pos.lerp(SCOPED, scope_u)
            tilt = math.radians(22) * (k(0.0, 0.12) - k(0.85, 1.0))
            gq = Quaternion((1, 0, 0), -tilt) @ Quaternion((0, 1, 0), -tilt * 0.6)
            gpos = gpos + Vector((0, 0, -0.03)) * (k(0.0, 0.12) - k(0.85, 1.0))
        if anim == "awp_tiro_luneta":
            # atira na luneta: coice forte, a arma desce um pouco do olho pra puxar o ferrolho e volta pra luneta
            scope_u = 1 - 0.45 * (sm(k(0.18, 0.32)) - sm(k(0.76, 0.95)))
            kick = sm(k(0.0, 0.04)) - sm(k(0.04, 0.25))
            gpos = aim_pos.lerp(SCOPED, scope_u) + Vector((-0.03, 0, 0.016)) * kick
            gq = Quaternion((0, 1, 0), -math.radians(8) * kick)
        kick = 0.0
        if anim == "tiro":   # AWP: coice forte + ferrolho (as outras usam "coice")
            kick = sm(k(0.0, 0.06)) - sm(k(0.06, 0.5))
            gpos = gpos + Vector((-0.035, 0, 0.012)) * kick
            gq = Quaternion((0, 1, 0), -math.radians(9) * kick)
        if anim == "coice":
            # coice curto de arma automática/pistola: t = força (1 = quadro do disparo, 0,5 = voltando);
            # pequeno pra não virar tremedeira no automático
            a = t * (1.6 if kind == "pistol" else 1.0)
            gpos = gpos + Vector((-0.013, 0, 0.004)) * a
            gq = Quaternion((0, 1, 0), -math.radians(2.5) * a)
        if anim in ("escada", "plantar", "desarmar", "plantar_pe", "desarmar_pe"):
            gpos, gq = back_pos, back_q; free_hands = True
            if kind != "pistol":   # alça da bandoleira atravessando o peito
                place_seg(s.props["alca_arma"], ch + Vector((0.09, -0.085, 0.1)), ch + Vector((0.1, 0.09, -0.1)), 0.01)
        if anim in ("granada", "granada_fim"):   # troca de arma como no CS: o fuzil vai pras costas (bandoleira), a
            # pistola pro coldre; depois de soltar volta pra mira
            u = sm(k(0.0, 0.16)) if anim == "granada" else 1 - sm(k(0.4, 1.0))
            fora = ch + Vector((0.02, -0.24, -0.06)) if kind != "pistol" else ch + Vector((0.06, -0.2, -0.1))
            fq = Quaternion((0, 1, 0), math.radians(75))
            if u < 0.5: gpos, gq = blend(aim_pos, aim_q, fora, fq, sm(u * 2))
            else: gpos, gq = blend(fora, fq, back_pos, back_q, sm(u * 2 - 1))
            if kind != "pistol" and u > 0.5: place_seg(s.props["alca_arma"], ch + Vector((0.09, -0.085, 0.1)), ch + Vector((0.1, 0.09, -0.1)), 0.01)
        if anim in ("awp_mirar", "awp_mirando"):
            # entra na luneta: sobe a arma até o olho, encosta a coronha bem no ombro; na luneta fica parado
            scope_u = sm(k(0.0, 0.7)) if anim == "awp_mirar" else 1.0
            # luneta na altura do olho direito (olhos ~0,32 acima do peito; luneta ~0,08 acima do cano), coronha
            # encostada na bochecha (entra um pouco na lateral da cabeça grande do chibi)
            gpos = aim_pos.lerp(SCOPED, scope_u); gq = aim_q
            lean = 0.06 * scope_u + (0.004 * math.sin(t * 2 * math.pi) if anim == "awp_mirando" else 0)
        if anim == "cego":   # arma caída, quase apontando pro chão
            # (de lado e pra fora da coxa direita: antes o cano cruzava pra dentro e entrava na perna)
            gpos, gq = low_pos + Vector((0.05, -0.07, 0.0)), Quaternion((0, 1, 0), math.radians(50)) @ Quaternion((0, 0, 1), math.radians(-18))
        if anim == "meio_cego":   # meio cego: a arma desce um pouco, segue mais ou menos na frente
            gpos, gq = blend(aim_pos, aim_q, low_pos, low_q, 0.35)
        if anim == "pegar":   # arma desce pro lado de FORA enquanto a esquerda pega no chão (antes entrava na coxa)
            dobra = sm(k(0.0, 0.38)) - sm(k(0.62, 1.0))
            fora_pos = ch + Vector((0.06, -0.16, -0.03)); fora_q = Quaternion((0, 1, 0), math.radians(42)) @ Quaternion((0, 0, 1), math.radians(-15))
            gpos, gq = blend(aim_pos, aim_q, fora_pos, fora_q, dobra)
        if anim == "comemorar":   # arma pro alto, cano pro céu, sobe e desce com o pulo
            gpos = ch + Vector((0.08, -0.1, 0.3 + 0.04 * abs(math.sin(2 * math.pi * t))))
            gq = Quaternion((0, 1, 0), -math.radians(70))
        if anim == "lamentar":
            gpos, gq = low_pos + Vector((0, 0, -0.04)), low_q
        draw_u = None
        if anim == "sacar":
            # saca: fuzil vem de baixo (cano pro chão, do lado direito) até a mira, passando um pouco; pistola sai do
            # coldre da perna
            if kind == "pistol": p0, q0 = back_pos, back_q
            else: p0, q0 = ch + Vector((0.0, -0.13, -0.18)), Quaternion((0, 1, 0), math.radians(75)) @ Quaternion((0, 0, 1), math.radians(-10))
            u = sm(k(0.0, 0.55)); over = 0.06 * math.sin(math.pi * k(0.45, 0.75))
            gpos, gq = blend(p0, q0, aim_pos, aim_q, u)
            gq = Quaternion((0, 1, 0), -over) @ gq
            draw_u = u
        if anim == "sacar_costas" and kind != "pistol":
            # puxa das costas por cima do ombro direito (depois de plantar, escada, faca): fica nas costas até a mão
            # chegar no guarda-mão, passa por cima do ombro com o cano pro alto e desce pra mira
            u = sm(k(0.36, 0.8)); over = 0.05 * math.sin(math.pi * k(0.66, 0.92))
            # o PUNHO faz o caminho (a mão segura nele): sai do meio das costas, contorna por fora do lado direito na altura
            # do ombro (o braço nunca sobe do lado da cabeça grande) e chega na mira; o cano vai subindo por cima do
            # ombro no meio do caminho e desce pra frente no fim
            gb = back_pos + back_q @ G.grip; ga = aim_pos + aim_q @ G.grip
            mid_q = Quaternion((0, 1, 0), math.radians(-60))
            gp = trilha(u, [(0.0, gb), (0.35, ch + Vector((-0.14, -0.3, 0.0))), (0.65, ch + Vector((0.03, -0.3, 0.04))), (1.0, ga)])
            gq = back_q.slerp(mid_q, sm(min(1.0, u / 0.55))) if u < 0.55 else mid_q.slerp(aim_q, sm((u - 0.55) / 0.45))
            gpos = gp - gq @ G.grip
            gq = Quaternion((0, 1, 0), -over) @ gq
            draw_u = u
        knife = anim.startswith("faca") or anim == "sacar_faca"
        if knife:   # com a faca o fuzil vai pras costas (pistola no coldre)
            gpos, gq = back_pos, back_q; free_hands = True
            if kind != "pistol": place_seg(s.props["alca_arma"], ch + Vector((0.09, -0.085, 0.1)), ch + Vector((0.1, 0.09, -0.1)), 0.01)
        if pitch and not (free_hands or knife):
            # olhando pra cima/baixo: a arma gira em volta do ombro (positivo = pra cima); a cabeça acompanha
            # até 60°: 70% na arma (gira no ombro) e 30% no tronco (inclina pra trás olhando pra cima, pra frente pra baixo)
            piv = ch + Vector((0, -0.05, 0.08)); Rp = Quaternion((0, 1, 0), -0.7 * pitch)
            gpos = piv + Rp @ (gpos - piv); gq = Rp @ gq
            if pitch < 0:   # mirando baixo: arma sai do peito (mais longe quanto mais baixo: a 60° o punho entrava no colete)
                f_ = -pitch / math.radians(25)
                gpos = gpos + Vector((0.045, -0.01, 0.0)) * min(1.0, f_) + Vector((0.05, -0.012, 0.01)) * max(0.0, f_ - 1.0)   # mirando baixo: arma sai do peito
        GM = Matrix.Translation(gpos) @ gq.to_matrix().to_4x4()
        W = lambda p: GM @ p
        L = G.L
        rh_t = W(G.grip); lh_t = W(G.fore) + (Vector((0, 0.03, -0.01)) if kind == "pistol" else Vector())
        mag_home = W(Vector(G.mag["piv"])) if G.mag else None
        mag_pos, mag_q, mag2_pos = mag_home, gq, None
        bolt = W(Vector((0.4 * L, -0.03, 0.012))) if kind != "pistol" else W(Vector((0.06 * L, 0, 0.05 * L)))
        if anim == "recarga" and G.mag:
            pouch = ch + Vector((0.135, 0.06, -0.04))   # na frente do bolso do colete (a mão encosta, não entra)
            under = mag_home + Vector((0, 0, -0.06))
            # pente velho: sai na mão, cai girando e fica deitado no chão até o fim
            if t >= 0.14:
                # sai na mão, solta e cai (gravidade) até o chão perto dos pés, quica um pouquinho e fica deitado
                p0 = mag_home + Vector((0.02, 0.04, -0.06)) * k(0.14, 0.22)
                land = Vector((0.17, -0.09, 0.022))   # na frente e do lado da câmera (não some atrás das pernas)
                u = k(0.22, 0.4)
                if t < 0.22: mag_pos = p0; mag_q = gq
                elif u < 1:
                    mag_pos = p0.lerp(land, u); mag_pos.z = p0.z + (land.z - p0.z) * u * u
                    mag_q = gq @ Quaternion((1, 0, 0), u * 2.4) @ Quaternion((0, 1, 0), u * 0.8)
                else:
                    b = k(0.4, 0.48); hop = 0.025 * math.sin(math.pi * b)
                    mag_pos = land + Vector((0.015 * b, 0.01 * b, hop))
                    mag_q = Quaternion((0, 0, 1), 0.6) @ Quaternion((1, 0, 0), math.pi / 2 - 0.5 * hop / 0.025)
            if t < 0.14: lh_t = W(G.fore).lerp(under, k(0.02, 0.14))
            elif t < 0.30:
                lh_t = under + Vector((0.02, 0.04, -0.06)) * k(0.14, 0.24)
            elif t < 0.46:
                lh_t = (under + Vector((0.02, 0.04, -0.06))).lerp(pouch, k(0.30, 0.42))
                if t > 0.40: mag2_pos = lh_t + Vector((0, 0, 0.05))
            elif t < 0.66:
                mag2_pos = pouch.lerp(mag_home + Vector((0, 0, -0.035)), k(0.46, 0.58)).lerp(mag_home, k(0.58, 0.66))
                lh_t = mag2_pos + Vector((0, 0, -0.06))
            if t >= 0.66: mag2_pos = mag_home   # pente novo fica encaixado até o fim
            if t < 0.66: pass
            elif t < 0.86:
                if G.cfg.get("bolt"):   # AWP: o ferrolho é com a direita; a esquerda volta pra frente
                    lh_t = under.lerp(W(G.fore), k(0.66, 0.74))
                    rh_t = W(G.grip).lerp(bolt + Vector((0, -0.03, 0.02)), k(0.66, 0.72)) + Vector((-0.06, 0, 0)) * (k(0.72, 0.78) - k(0.78, 0.84))
                else:
                    bo = bolt + Vector((0.02, -0.03, 0.02))
                    lh_t = under.lerp(bo, k(0.66, 0.74)) + Vector((-0.05, 0, 0)) * (k(0.74, 0.79) - k(0.79, 0.84))
            else:
                lh_t = W(G.fore) if G.cfg.get("bolt") else (bolt + Vector((0.02, -0.03, 0.02))).lerp(W(G.fore), k(0.86, 0.98))
                if G.cfg.get("bolt"): rh_t = (bolt + Vector((0, -0.03, 0.02))).lerp(W(G.grip), k(0.86, 0.98))
        if anim == "tiro":
            muz = W(Vector((L, 0, 0)))
            if t < 0.16:   # clarão na boca
                q = gq @ Quaternion((1, 0, 0), t * 7)
                place(PR["clarao"], muz + gq @ Vector((0.05, 0, 0)), (0.07, 0.03, 0.03), q)
                place(PR["clarao2"], muz + gq @ Vector((0.03, 0, 0)), (0.03, 0.06, 0.06), q)
            if G.cfg.get("bolt") and t > 0.4:   # AWP: puxa o ferrolho depois do tiro
                rh_t = W(G.grip).lerp(bolt + Vector((0, -0.03, 0.02)), k(0.4, 0.5)) + Vector((-0.06, 0, 0)) * (k(0.5, 0.6) - k(0.62, 0.72))
                rh_t = rh_t.lerp(W(G.grip), k(0.76, 0.9))
        if anim == "awp_tiro_luneta":
            if t < 0.07:
                muz = W(Vector((L, 0, 0))); q = gq @ Quaternion((1, 0, 0), 0.5)
                place(PR["clarao"], muz + gq @ Vector((0.06, 0, 0)), (0.08, 0.035, 0.035), q)
                place(PR["clarao2"], muz + gq @ Vector((0.03, 0, 0)), (0.035, 0.07, 0.07), q)
            # ferrolho com a direita enquanto a arma está meio baixa: levanta, puxa pra trás, empurra, abaixa
            rh_t = W(G.grip).lerp(bolt + Vector((0, -0.03, 0.02)), k(0.32, 0.42)) + Vector((-0.06, 0, 0)) * (k(0.42, 0.52) - k(0.55, 0.65))
            rh_t = rh_t.lerp(W(G.grip), k(0.66, 0.78))
        if anim == "coice" and t > 0.9:   # clarão só no quadro do disparo
            muz = W(Vector((L, 0, 0))); q = gq @ Quaternion((1, 0, 0), 0.6)
            place(PR["clarao"], muz + gq @ Vector((0.05, 0, 0)), (0.07, 0.032, 0.032), q)
            place(PR["clarao2"], muz + gq @ Vector((0.03, 0, 0)), (0.03, 0.06, 0.06), q)
        # pontos pra quem compõe o vídeo/jogo: boca do cano e janela de ejeção (espaço do boneco)
        s.muzzle_local = W(Vector((L, 0, 0))); s.eject_local = W(Vector((0.42 * L if kind != "pistol" else 0.35 * L, -0.03, 0.02)))
        if anim in ("granada", "granada_fim"):
            mol = nade == "molotov"
            chest = ch + Vector((0.16, -0.05, 0.02)); pouch = ch + Vector((0.12, -0.07, -0.06))
            orelha = ch + Vector((-0.01, -0.25, 0.22))     # mão armada: do lado da cabeça, não atrás
            solta = ch + Vector((0.22, -0.25, 0.33))        # soltura: na frente e acima do ombro (por fora do rosto)
            segue = ch + Vector((0.25, 0.04, -0.04))        # continuação: desce cruzando na frente do corpo
            aponta = ch + Vector((0.3, 0.12, 0.2))          # esquerda apontando pro alvo
            if anim == "granada":
                rh_t = trilha(t, [(0.0, W(G.grip)), (0.05, ch + Vector((0.14, -0.12, -0.04))), (0.12, pouch), (0.2, chest), (0.4, chest), (0.58, orelha + Vector((0.03, 0.02, -0.06))),
                                  (0.72, orelha), (1.0, solta)])
                if mol:   # a esquerda pega o isqueiro na cintura, acende o pano e vai pra frente
                    isq = ch + Vector((0.1, 0.12, -0.1)); acende = chest + Vector((0.0, 0.07, 0.12))
                    lh_t = trilha(t, [(0.0, W(G.fore)), (0.08, ch + Vector((0.12, 0.13, -0.04))), (0.18, isq), (0.28, acende), (0.38, acende), (0.5, ch + Vector((0.12, 0.2, 0.02))),
                                      (0.72, aponta), (1.0, aponta + Vector((-0.08, 0.04, -0.12)))])
                else:     # a esquerda tira o pino e vai pra frente apontando
                    pino = chest + Vector((0.02, 0.04, 0.07))
                    lh_t = trilha(t, [(0.0, W(G.fore)), (0.1, ch + Vector((0.12, 0.13, -0.04))), (0.24, pino), (0.3, pino), (0.38, pino + Vector((0.0, 0.1, 0.04))),
                                      (0.5, ch + Vector((0.12, 0.2, 0.06))), (0.72, aponta), (1.0, aponta + Vector((-0.08, 0.04, -0.12)))])
                twist = -0.4 * (sm(k(0.42, 0.72)) - sm(k(0.72, 1.0))) + 0.25 * sm(k(0.82, 1.0))
                lean = -0.06 * (sm(k(0.42, 0.72)) - sm(k(0.72, 0.9))) + 0.1 * sm(k(0.85, 1.0))
                nade_pos = rh_t + Vector((0, 0, 0.04))
                if 0.06 < t: s.nade(nade, nade_pos, spin=0.0)
                if not mol and 0.3 < t < 0.5:   # pino na mão esquerda
                    place(PR["pino"], lh_t + Vector((0.0, 0.0, 0.03)), (0.014, 0.014, 0.005))
                if mol:
                    if 0.14 < t < 0.44:   # isqueiro na esquerda; a chama aparece quando chega no pano
                        place(PR["isqueiro"], lh_t + Vector((0.0, 0.0, 0.026)))
                        place(PR["isq_tampa"], lh_t + Vector((-0.02, 0.0, 0.056)), q=Quaternion((0, 1, 0), -0.9))
                        if t > 0.24:
                            f = 0.8 + 0.25 * math.sin(t * 90)
                            place(PR["chama_isq"], lh_t + Vector((0.0, 0.0, 0.058)), (0.014 * f, 0.014 * f, 0.026 * f))
                    if t > 0.3:   # pano pegando fogo e crescendo
                        cresce = sm(k(0.3, 0.45))
                        for fi in range(3):
                            ph = t * (70 + 17 * fi) + fi * 2.1
                            r = (0.032 + 0.01 * math.sin(ph)) * cresce * (1.0 - 0.25 * fi)
                            place(PR["fogo%d" % fi], nade_pos + Vector((0.006 * math.sin(ph), 0.006 * math.cos(ph * 1.3), 0.1 + 0.03 * fi * cresce)),
                                  (r, r, r * 1.8))
            else:   # granada_fim: continuação depois de soltar (mão vazia) e volta pra arma
                rh_t = trilha(t, [(0.0, solta), (0.35, segue), (0.55, segue), (0.8, ch + Vector((0.14, -0.12, -0.04)))])
                lh_t = trilha(t, [(0.0, aponta + Vector((-0.08, 0.04, -0.12))), (0.35, ch + Vector((0.02, 0.2, -0.04))), (0.6, ch + Vector((0.12, 0.13, -0.04)))])
                v = sm(k(0.75, 1.0)); rh_t = rh_t.lerp(W(G.grip), v); lh_t = lh_t.lerp(W(G.fore), v)
                twist = 0.25 * (1 - sm(k(0.3, 1.0))); lean = 0.1 * (1 - sm(k(0.3, 1.0)))
        head_turn = None
        if scope_u > 0:   # bochecha na coronha, olho na luneta (cabeça inclina pro lado da arma)
            head_turn = (HT_LUNETA[0] * scope_u, HT_LUNETA[1] * scope_u)
        if pitch:   # olha pra cima/baixo junto com a arma
            ht = head_turn or (0.0, 0.0); head_turn = (ht[0] - 0.45 * pitch, ht[1])
            lean += -0.3 * pitch
        if anim in ("cego", "meio_cego"):
            # cegado: vira o rosto pra baixo e de lado, mão esquerda tapando os olhos, arma caída na direita, cambaleia
            # pra trás, estrelinhas girando em volta da cabeça. Meio cego (flash de lado): vira o rosto pro outro lado
            # e faz aba com a mão na testa, a arma continua na frente (direita).
            # O alvo da mão sai do olho JÁ com a cabeça virada (antes a cabeça girava depois e o rosto saía da mão).
            forte = anim == "cego"
            # cabeça baixa e de lado (encolhe da luz); a mão esquerda vai pra FRENTE das lentes/olhos (o resolvedor
            # do braço passa o antebraço por fora da cabeça grande: antes o braço atravessava o rosto)
            # (braço curto de chibi + cabeça grande: a mão chega pelo lado ESQUERDO e tapa os olhos com a palma; o rosto
            # vira um pouco pra mão)
            head_turn = (0.22, 0.3) if forte else (0.16, 0.22)
            tilt, yaw = head_turn; neck = ch + Vector((0, 0, 0.13))
            Mh = Matrix.Translation(neck) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y") @ Matrix.Translation(-neck)
            olhos = Mh @ (hc + Vector((0.176, 0.03, 0.045)))
            frente = Mh.to_3x3() @ Vector((1, 0, 0))
            treme = 0.005 * math.sin(t * 2 * math.pi)
            lh_t = olhos + frente * 0.05 + Vector((0.0, 0.0, treme))
            if forte:
                s.root.location = (-0.03 * sm(k(0.0, 0.3)) + 0.01 * math.sin(t * 2 * math.pi), 0.012 * math.sin(t * 4 * math.pi), 0)
                lean = -0.06 + 0.025 * math.sin(t * 2 * math.pi)
                for i in range(3):   # estrelinhas
                    ang = t * 2 * math.pi + i * 2.094
                    place(PR["estrela%d" % i], Mh @ (hc + Vector((0.15 * math.cos(ang), 0.15 * math.sin(ang), 0.21 + 0.02 * math.sin(ang * 2)))), (0.022, 0.022, 0.022))
            else:
                lean = -0.03
        if scope_u > 0 and anim != "recarga":   # na luneta: esquerda lá na frente, no guarda-mão (como no CS2)
            lh_t = lh_t.lerp(W(G.fore + Vector((0.03 * L, 0, -0.01))), scope_u)
        fixed = anim in ("plantar", "desarmar", "plantar_pe", "desarmar_pe", "escada")   # mãos em coisa parada no mundo
        Mlean = Matrix.Translation((0, 0, hipz)) @ Matrix.Rotation(lean, 4, "Y") @ Matrix.Translation((0, 0, -hipz))
        if anim in ("plantar", "desarmar", "plantar_pe", "desarmar_pe"):
            bomb = Vector((0.29, 0.0, 0.037 * C4_K)) if anim in ("plantar", "desarmar") else Vector((0.32, 0.0, 0.037 * C4_K))
            if anim.startswith("plantar"):
                u = sm(k(0.0, 0.25)); bpos = (Mlean @ (ch + Vector((0.2, 0, -0.08)))).lerp(bomb, u)   # na frente do colete
            else:
                bpos = bomb
            qc4 = Quaternion((0, 0, 1), math.pi / 2)   # deitada atravessada: o comprido de lado, teclado pra cima
            c4(bpos, qc4)
            tap = 0.02 * abs(math.sin(t * 6 * math.pi))
            if anim.startswith("plantar"):
                v = sm(k(0.2, 0.32)); tap *= v
                # segura pelos lados com as duas mãos; no chão a esquerda firma em cima e a direita digita no teclado
                lado = 0.058 * C4_K + 0.016; topo = 0.036 * C4_K
                lh_t = (bpos + Vector((0.0, lado, 0.0))).lerp(bpos + Vector((0.03, 0.03, topo + 0.02 + tap)), v)
                rh_t = (bpos + Vector((0.0, -lado, 0.0))).lerp(bpos + Vector((-0.02, -0.01, topo + 0.03 + 0.02 * v * abs(math.sin(t * 6 * math.pi + 1.5)))), v)
            else:
                lh_t = bpos + Vector((0.02, 0.04, 0.036 * C4_K + 0.02))
                rh_t = bpos + Vector((0.03, -0.02, 0.036 * C4_K + 0.04 + tap)); place(PR["alicate"], rh_t + Vector((0.025, 0, -0.02)), (0.03, 0.008, 0.008))
        if anim == "pegar":
            dobra = sm(k(0.0, 0.38)) - sm(k(0.62, 1.0)); pega = sm(k(0.3, 0.42)) - sm(k(0.58, 0.7))
            Ml = Matrix.Translation((0, 0, hipz)) @ Matrix.Rotation(lean, 4, "Y") @ Matrix.Translation((0, 0, -hipz))
            alvo = Ml.inverted() @ Vector((0.26, 0.07, 0.05))   # ponto do chão, antes de inclinar o tronco
            lh_t = W(G.fore).lerp(alvo, max(pega, dobra * 0.85))
        if anim == "comemorar":   # punho esquerdo pro alto batendo no ritmo do pulo
            lh_t = ch + Vector((0.06, 0.16, 0.36 + 0.1 * abs(math.sin(2 * math.pi * t))))
        if anim == "lamentar":
            lh_t = W(G.fore) + Vector((0, 0.02, -0.02)); head_turn = (0.35, 0.0)   # cabeça baixa
        if anim == "apontar":   # call no rádio: aponta com a esquerda pra frente, a direita segura a arma
            u = sm(k(0.0, 0.25)) - sm(k(0.7, 1.0)); ht = head_turn or (0.0, 0.0)
            lh_t = W(G.fore).lerp(ch + Vector((0.34, 0.1, 0.2)), u); head_turn = (ht[0] - 0.08 * u, ht[1] + 0.12 * u)
        if anim == "escada":
            for sd, ph, sg in (("L", math.pi, 1), ("R", 0.0, -1)):
                u = 0.5 + 0.5 * math.sin(2 * math.pi * t + ph)
                tgt = Vector((0.17, sg * 0.1, ch.z + 0.08 + 0.2 * u))
                if sd == "L": lh_t = tgt
                else: rh_t = tgt
            lean = 0.06
        if anim == "sacar_costas" and kind != "pistol":
            # direita sobe por cima do ombro e pega o guarda-mão nas costas, puxa e escorrega pro punho; a esquerda
            # vem do lado e encaixa no guarda-mão quando a arma chega na frente
            rest_r = ch + Vector((0.05, -0.17, -0.12)); rest_l = ch + Vector((0.03, 0.165, -0.2))
            # a mão vai por trás das costas até o punho do fuzil (lá em cima o cano passa atrás da cabeça grande: o braço
            # atravessava ela) e segura nele o resto do saque: o fuzil sobe pelo lado de fora do ombro e vem pra frente
            if t < 0.36: rh_t = trilha(t, [(0.0, rest_r), (0.13, ch + Vector((-0.1, -0.28, -0.02))), (0.25, ch + Vector((-0.17, -0.12, -0.02))), (0.36, W(G.grip))])
            else: rh_t = W(G.grip)
            lh_t = rest_l.lerp(W(G.fore), sm(k(0.66, 0.9)))
        if anim == "sacar":
            # direita no punho o tempo todo; esquerda vem do lado e pega a arma no fim (fuzil: puxa o ferrolho)
            side = ch + Vector((0.02, 0.16, -0.14))
            lh_t = side.lerp(W(G.fore) + (Vector((0, 0.03, -0.01)) if kind == "pistol" else Vector()), sm(k(0.35, 0.6)))
            if kind != "pistol":
                if G.cfg.get("bolt"):
                    rh_t = W(G.grip).lerp(bolt + Vector((0, -0.03, 0.02)), sm(k(0.56, 0.72))) + Vector((-0.06, 0, 0)) * (sm(k(0.72, 0.8)) - sm(k(0.8, 0.88)))
                    rh_t = rh_t.lerp(W(G.grip), k(0.86, 0.96))
                else:
                    bo = bolt + Vector((0.02, -0.03, 0.02))
                    lh_t = lh_t.lerp(bo, sm(k(0.56, 0.72))) + Vector((-0.05, 0, 0)) * (sm(k(0.72, 0.78)) - sm(k(0.78, 0.84)))
                    lh_t = lh_t.lerp(W(G.fore), k(0.84, 0.96))
        KD_FACA = Vector((0.2, 1, -0.05)).normalized()   # parado/andando: faca deitada, atravessada pra dentro (CS2)
        kd = KD_FACA   # direção da lâmina
        if knife:
            # como no CS2: cotovelo do lado do corpo, antebraço pra frente e a faca na horizontal, atravessada
            hold_r = ch + Vector((0.2, -0.12, -0.1)); hold_l = ch + Vector((0.03, 0.165, -0.25))
            rh_t, lh_t = hold_r, hold_l
            if anim == "faca_andar":   # braços balançam com o passo
                sw = math.sin(t * 2 * math.pi)
                rh_t = hold_r + Vector((0.07 * sw, 0, 0.02 * abs(sw))); lh_t = hold_l + Vector((-0.07 * sw, 0, 0.02 * abs(sw)))
            def bz(a, b, c, u): return a.lerp(b, u).lerp(b.lerp(c, u), u)
            trail = []   # pontos do rastro (do mais novo pro mais velho)
            if anim == "faca_m1":
                # M1: corte rápido na diagonal, do alto à direita pra baixo à esquerda, na frente do corpo, e volta
                a0 = ch + Vector((0.16, -0.2, 0.2)); a1 = ch + Vector((0.34, -0.01, 0.04)); a2 = ch + Vector((0.26, 0.12, -0.1))
                d0 = Vector((0.4, -0.6, 0.7)); d1 = Vector((1, 0, 0)); d2 = Vector((0.4, 0.6, -0.6))
                u = sm(k(0.18, 0.48))
                if t < 0.18: rh_t = hold_r.lerp(a0, sm(k(0.0, 0.18))); kd = KD_FACA.lerp(d0, sm(k(0.0, 0.18)))
                elif t < 0.55: rh_t = bz(a0, a1, a2, u); kd = d0.lerp(d1, min(1, u * 2)) if u < 0.5 else d1.lerp(d2, (u - 0.5) * 2)
                else: rh_t = a2.lerp(hold_r, sm(k(0.55, 1.0))); kd = d2.lerp(KD_FACA, sm(k(0.55, 1.0)))
                lean = 0.1 * (sm(k(0.15, 0.4)) - sm(k(0.55, 1.0)))
                if 0.2 < t < 0.62:   # arco contínuo atrás da ponta da lâmina
                    for i in range(10):
                        uu = u - i * 0.045
                        if 0 < uu: trail.append(bz(a0, a1, a2, uu) + (d0.lerp(d2, uu)).normalized() * 0.13)
            if anim == "faca_m2":
                # M2: estocada pesada — puxa a faca pra trás (corpo recua), enfia pra frente com o peso, segura, volta
                back = ch + Vector((-0.04, -0.17, 0.02)); thrust = ch + Vector((0.36, -0.03, 0.05))
                if t < 0.3: rh_t = hold_r.lerp(back, sm(k(0.0, 0.3))); kd = KD_FACA.lerp(Vector((1, 0, 0.05)), sm(k(0.0, 0.3))); lean = -0.08 * sm(k(0.0, 0.3))
                elif t < 0.5: rh_t = back.lerp(thrust, sm(k(0.3, 0.5))); kd = Vector((1, 0, 0.05)); lean = -0.08 + 0.28 * sm(k(0.3, 0.5))
                elif t < 0.7: rh_t = thrust; kd = Vector((1, 0, 0.05)); lean = 0.2
                else: rh_t = thrust.lerp(hold_r, sm(k(0.7, 1.0))); kd = Vector((1, 0, 0.05)).lerp(KD_FACA, sm(k(0.7, 1.0))); lean = 0.2 * (1 - sm(k(0.7, 1.0)))
                lh_t = hold_l + Vector((-0.08, 0.03, 0.03)) * (sm(k(0.35, 0.5)) - sm(k(0.7, 1.0)))   # braço esquerdo pra trás (equilíbrio)
                if 0.38 < t < 0.62:
                    for i in range(6): trail.append(rh_t + Vector((0.12 - i * 0.045, 0, 0.005)))
            if anim == "sacar_faca":   # tira da bainha na cintura direita, gira a lâmina pra frente
                sheath = ch + Vector((0.02, -0.14, -0.13)); u = sm(k(0.0, 0.6))
                rh_t = sheath.lerp(hold_r, u); kd = Vector((0, 0, -1)).lerp(KD_FACA, sm(k(0.2, 0.7)))
        if fixed and abs(lean) > 1e-4:   # o alvo está parado no mundo: leva pro tronco antes de inclinar
            Mlean = Matrix.Translation((0, 0, hipz)) @ Matrix.Rotation(lean, 4, "Y") @ Matrix.Translation((0, 0, -hipz))
            Mi = Mlean.inverted(); lh_t = Mi @ lh_t; rh_t = Mi @ rh_t
        # braços: o corpo (cabeça já virada, com a máscara/rosto; tronco com o colete) é obstáculo pro braço e pra mão
        hcF, frF = hc.copy(), Vector((1, 0, 0))
        if head_turn:
            neck = ch + Vector((0, 0, 0.13)); tilt, yaw = head_turn
            Mh = Matrix.Translation(neck) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y") @ Matrix.Translation(-neck)
            hcF = Mh @ hc; frF = Mh.to_3x3() @ frF
        cab = (hcF + frF * 0.02, Vector((0.185, 0.175, 0.165)), 2)
        tor = (ch + Vector((0.0, 0, -0.005)), Vector((0.1, 0.13, 0.13)), 2)
        cole = (ch + Vector((0.0, 0, 0.005)), Vector((0.108, 0.136, 0.098)), 4)
        corpo = [cab, tor, cole]
        if ct:   # máscara de gás: sai do rosto pra frente e pra baixo
            corpo.append(((Mh @ (hc + Vector((0.09, 0, -0.015)))) if head_turn else hc + Vector((0.09, 0, -0.015)), Vector((0.105, 0.148, 0.13)), 2))
        # mãos no fuzil (não nas costas, na faca, na granada, na bomba...): cotovelos fechados e alcance do guarda-mão
        maos_fuzil = kind == "rifle" and not knife
        for sd, sg, tgt in (("L", 1, lh_t), ("R", -1, rh_t)):
            sh = ch + Vector((0, sg * 0.125, 0.07))
            pole = Vector((0, sg * 0.4, -1))
            if maos_fuzil:   # fuzil: cotovelos pra baixo, perto do corpo (usuário: braço aberto demais)
                pole = Vector((-0.3, -0.12, -1)) if sd == "R" else Vector((0.1, 0.22, -1))
            if sd == "R" and knife: pole = Vector((-0.3, -0.8, -0.6))
            if sd == "R" and anim == "sacar_costas": pole = Vector((-0.8, -0.6, -0.1))   # cotovelo pra trás e pra fora   # faca: cotovelo pra fora e pra trás (na estocada subia na cabeça)
            if sd == "R" and scope_u > 0:   # cotovelo direito na luneta: pra baixo e um pouco pra fora, junto do corpo
                # (antes levantado de lado, "asa de galinha": usuário achou o braço pra fora demais)
                pole = pole.lerp(Vector((0.6, -0.45, -0.65)), sm(min(1.0, scope_u / 0.6)))
            # braço esquerdo estica um pouco (desenho animado) pra alcançar o guarda-mão da AWP na luneta
            lb = 0.19 + (0.075 * scope_u if sd == "L" and anim != "recarga" else 0.0)
            if sd == "L" and maos_fuzil: lb += 0.05
            if sd == "R" and maos_fuzil and scope_u <= 0: lb -= 0.015   # braço direito dobra menos: cotovelo não abre   # o guarda-mão do fuzil (do lado direito) fica longe da mão esquerda
            if sd == "L" and abs(pitch) > math.radians(25):   # mira muito pra cima/baixo: o braço esquerdo estica pro guarda-mão
                lb += 0.06 * min(1.0, (abs(pitch) - math.radians(25)) / math.radians(35))
            # na luneta o braço direito encosta no peito (cotovelo pra frente e pra baixo): só a cabeça empurra o cotovelo;
            # com o colete como obstáculo ele fugia pro lado, na altura do ombro
            # (mistura contínua das duas soluções pelo scope_u: trocar de uma vez dava salto ao sair da luneta)
            elb, hd = ik_desvia(sh, tgt, lb, lb, pole, corpo, corpo, chave=sd)
            wl = sm(min(1.0, max(0.0, (scope_u - 0.2) / 0.6))) if sd == "R" else 0.0
            if wl > 0:
                elb2, _ = ik_desvia(sh, tgt, lb, lb, pole, corpo, [cab], chave="R_luneta")
                u_ = (hd - sh).normalized(); dd = (hd - sh).length
                a_ = dd / 2; r_ = math.sqrt(max(lb * lb - a_ * a_, 0)); c_ = sh + u_ * a_
                e_ = elb.lerp(elb2, wl) - c_; e_ -= u_ * e_.dot(u_)   # volta pro círculo de cotovelos válidos
                if e_.length > 1e-6: elb = c_ + e_.normalized() * r_
            mid = sh.lerp(elb, 0.6)
            if ct:
                place_seg(P["braco" + sd], sh, elb, 0.044); place_seg(P["ante" + sd], elb, hd, 0.038)
            else:
                # manga até 60% do braço, pele até o cotovelo e antebraço do cotovelo à mão (antes uma peça só do meio do
                # braço à mão cortava o cotovelo e entrava no colete)
                place_seg(P["braco" + sd], sh, mid, 0.046); place_seg(P["ante" + sd], mid, elb, 0.037); place_seg(P["pulso" + sd], elb, hd, 0.036)
            place(P["mao" + sd], hd, (0.034, 0.032, 0.032))
            if knife and sd == "R":   # faca na mão direita: o centro do cabo fica no punho, lâmina ao longo de kd
                kn = kd.normalized()
                s.knife.location = hd - kn * 0.01; s.knife.rotation_mode = "QUATERNION"
                # parado/correndo: lâmina deitada com a face pra cima (CS); nos golpes, o fio pra baixo
                s.knife.rotation_quaternion = kn.to_track_quat("X", "Y" if anim in ("faca_parado", "faca_andar") else "Z")
        if knife:
            for i, p in enumerate(trail[:10]):   # rastro do golpe: faixa clara que afina
                s_ = 0.024 * (1 - i * 0.08)
                place(PR["rastro%d" % i], p, (s_, s_, s_ * 0.45))
        G.obj.location = gpos; G.obj.rotation_mode = "QUATERNION"; G.obj.rotation_quaternion = gq
        # pistola no coldre fica menor (chibi: a de verdade é mais comprida que a coxa) e cresce saindo dele no sacar
        e_ = 1.0
        if kind == "pistol":
            if anim == "sacar": e_ = 0.62 + 0.38 * sm(k(0.0, 0.3))
            elif (gpos - back_pos).length < 1e-6: e_ = 0.62
        G.obj.scale = (e_, e_, e_)
        gun_objs = [G.obj] + ([s.knife] if knife else [])
        if G.mag:
            # fora da arma (caindo, no chão, na mão) SMG e pistola mostram o pente inteiro
            out1 = mag_pos is not None and mag_pos is not mag_home
            out2 = mag2_pos is not None and anim == "recarga" and t < 0.6
            if mag_pos is not None:
                o = G.magp if (out1 and G.magp) else G.mag
                o.location = mag_pos; o.rotation_mode = "QUATERNION"; o.rotation_quaternion = mag_q
                o.scale = (1, 1, 1)
                if not out1 and e_ != 1.0:   # encaixado na pistola encolhida (coldre): encolhe junto em volta dela
                    o.location = gpos + (mag_pos - gpos) * e_; o.scale = (e_, e_, e_)
                if not out1: gun_objs.append(o)   # só o encaixado acompanha a arma; o que caiu fica parado no chão
            if mag2_pos is not None:
                o = G.magp2 if (out2 and G.magp2) else G.mag2
                o.location = mag2_pos; o.rotation_mode = "QUATERNION"; o.rotation_quaternion = gq; gun_objs.append(o)
        # inclinação do tronco (correr, agachar, lançar): gira tudo de cima em volta do quadril
        if head_turn:   # gira a cabeça (e tudo do rosto) em volta do pescoço
            neck = ch + Vector((0, 0, 0.13)); tilt, yaw = head_turn
            Mh = Matrix.Translation(neck) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y") @ Matrix.Translation(-neck)
            for nm in HEAD_KEYS:
                if nm in P: P[nm].matrix_basis = Mh @ P[nm].matrix_basis
        upper = [o for o in P.values() if o not in LOWER] + gun_objs + [o for o in PR.values() if o.location.z > -40 and o.name.find("bomba") < 0 and o.name.find("visor") < 0]
        if abs(lean) > 1e-4 or abs(twist) > 1e-4:
            M = Matrix.Translation((0, 0, hipz)) @ Matrix.Rotation(twist, 4, "Z") @ Matrix.Rotation(lean, 4, "Y") @ Matrix.Translation((0, 0, -hipz))
            for o in upper: o.matrix_basis = M @ o.matrix_basis
        if anim == "morrer":
            # cai de costas girando nos pés; a arma escapa da mão e cai na frente
            a = -1.45 * sm(k(0.0, 0.7)); bounce = 0.02 * math.sin(math.pi * k(0.7, 0.85))
            M = Matrix.Translation((-0.04, 0, bounce)) @ Matrix.Rotation(a, 4, "Y") @ Matrix.Translation((0.04, 0, 0))
            for o in list(P.values()): o.matrix_basis = M @ o.matrix_basis
            u = sm(k(0.05, 0.6))
            G.obj.location = gpos.lerp(Vector((0.25, -0.12, 0.03)), u)
            G.obj.rotation_quaternion = gq.slerp(Quaternion((1, 0, 0), math.radians(90)) @ Quaternion((0, 0, 1), 0.6), u)
            if G.mag:   # o pente continua encaixado na arma que caiu
                bpy.context.view_layer.update()
                G.mag.location = G.obj.matrix_basis @ Vector(G.mag["piv"]); G.mag.rotation_quaternion = G.obj.rotation_quaternion
        if anim == "morrer_frente":
            # tomba de cara pra frente girando no bico do pé (tiro pelas costas); a arma escapa pra frente
            a = 1.4 * sm(k(0.0, 0.7)); bounce = 0.02 * math.sin(math.pi * k(0.7, 0.85))
            M = Matrix.Translation((0.08, 0, bounce)) @ Matrix.Rotation(a, 4, "Y") @ Matrix.Translation((-0.08, 0, 0))
            for o in list(P.values()): o.matrix_basis = M @ o.matrix_basis
            u = sm(k(0.05, 0.6))
            G.obj.location = gpos.lerp(Vector((0.45, 0.1, 0.03)), u)
            G.obj.rotation_quaternion = gq.slerp(Quaternion((1, 0, 0), math.radians(-90)) @ Quaternion((0, 0, 1), -0.5), u)
            if G.mag:
                bpy.context.view_layer.update()
                G.mag.location = G.obj.matrix_basis @ Vector(G.mag["piv"]); G.mag.rotation_quaternion = G.obj.rotation_quaternion
        s.chao()

    def chao(s):
        """Peça do corpo (elipsoide/caixa) abaixo de z=0 -> sobe tudo o que está no boneco."""
        bpy.context.view_layer.update()
        menor = 9.0
        for o in s.P.values():
            M = o.matrix_world
            if M.translation.length > 20: continue
            caixa = "r" in o.keys()
            ext = sum(abs(M[2][j]) for j in range(3)) if caixa else math.sqrt(sum(M[2][j] ** 2 for j in range(3)))
            menor = min(menor, M.translation.z - ext)
        if menor < -0.004:
            for o in s.root.children:
                if o.matrix_world.translation.length < 20: o.location.z -= menor
            bpy.context.view_layer.update()

    def nade(s, kind, pos, spin=0.0):
        q = Quaternion((0, 1, 0), spin)
        for nm in NADE_PARTS[kind]:
            o = s.props[nm]; off, r = NADE_GEOM[nm]
            place(o, pos + q @ Vector(off), r, q)

def set_branco(w):
    for m in bpy.data.materials:
        if m.node_tree and "BRANCO" in m.node_tree.nodes: m.node_tree.nodes["BRANCO"].outputs[0].default_value = w

def render(path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

def ladder(root):
    """Escada de mão de madeira na frente do boneco (só pra mostrar a animação de subir; o mapa tem a dele)."""
    mat = toon("escada", "#7a5232")
    objs = []
    for sg in (1, -1):
        o = box("trilho", mat, (0.012, 0.012, 0.6), 0.004); o.parent = root; place(o, Vector((0.21, sg * 0.13, 0.6))); objs.append(o)
    for i in range(7):
        o = box("degrau", mat, (0.01, 0.13, 0.01), 0.003); o.parent = root; place(o, Vector((0.21, 0, 0.1 + i * 0.16))); objs.append(o)
    return objs

GRUPOS = ARGV[2].split(",") if len(ARGV) > 2 else ["mov", "armas", "sacar", "faca", "granadas", "outros"]
DIRS = ["L", "SE", "S", "SO", "O", "NO", "N", "NE"]
for team in TEAMS:
    for o in list(sc.objects):
        if o.type in ("MESH", "EMPTY"): bpy.data.objects.remove(o)
    A = Agent(team)
    padrao = "M4A4" if team == "ct" else "AK47"
    linhas = []   # folha de conferência: rótulo + arquivos
    def seq(label, anim, n, gun=None, loop=True, branco=False, **kw):
        fs = []
        for fi in range(n):
            tt = fi / n if loop else fi / (n - 1)
            set_branco(0.85 * (1 - tt) ** 1.5 if branco else 0.0)
            A.pose(tt, anim, gun or padrao, **kw)
            p = os.path.join(OUT, f"{team}_{label.replace(' ', '_')}_{fi:02d}.png"); render(p); fs.append(p)
        linhas.append((label, fs))
    if "mov" in GRUPOS:
        fs = []
        for i, dn in enumerate(DIRS):
            A.root.rotation_euler = (0, 0, -i * math.pi / 4)
            A.pose(0, "mira", padrao); p = os.path.join(OUT, f"{team}_mira_{i}_{dn}.png"); render(p); fs.append(p)
        linhas.append(("mirando 8 direcoes", fs))
    A.root.rotation_euler = (0, 0, -7 * math.pi / 4)   # perfil pra direita da tela
    if "teste" in GRUPOS:
        for tt in (0.89, 1.0):
            A.pose(tt, "morrer", padrao)
            print("TESTE", tt, tuple(round(x, 3) for x in A.P["cabeca"].matrix_world.translation), tuple(round(x, 3) for x in A.P["cabeca"].scale))
    if "mov" in GRUPOS:
        seq("andar", "andar", 8); seq("andar devagar", "devagar", 8); seq("andar de costas", "tras", 8)
        seq("andar de lado esq (A)", "lado_esq", 8); seq("andar de lado dir (D)", "lado_dir", 8)
        seq("agachado andando", "agachado", 8); seq("pular", "pular", 10, loop=False)
        A.root.rotation_euler = (0, 0, -5 * math.pi / 4)   # de costas pra câmera, escada de frente
        lad = ladder(A.root); seq("escada de mao", "escada", 8)
        A.root.rotation_euler = (0, 0, -7 * math.pi / 4)
        for o in lad: bpy.data.objects.remove(o)
    if "armas" in GRUPOS:
        fs = []
        for w in WEAP:
            A.pose(0, "mira", w); p = os.path.join(OUT, f"{team}_arma_{w}.png"); render(p); fs.append(p)
        linhas.append(("armas", fs))
        seq("tiro fuzil", "tiro", 6, padrao, loop=False); seq("tiro AWP", "tiro", 10, "AWP", loop=False)
        seq("AWP entrando na luneta", "awp_mirar", 8, "AWP", loop=False); seq("AWP na luneta", "awp_mirando", 4, "AWP")
        seq("tiro pistola", "tiro", 5, "Glock" if team == "tr" else "USP", loop=False)
        for w in WEAP: seq("recarga " + w, "recarga", 12, w, loop=False)
    if "ad8" in GRUPOS:   # AD/AD (e de costas) nas 8 direções
        for i, dn in enumerate(DIRS):
            A.root.rotation_euler = (0, 0, -i * math.pi / 4)
            seq(f"A (esq) virado {dn}", "lado_esq", 8); seq(f"D (dir) virado {dn}", "lado_dir", 8)
        A.root.rotation_euler = (0, 0, -7 * math.pi / 4)
    if "sacar" in GRUPOS:
        seq("sacar fuzil", "sacar", 10, padrao, loop=False); seq("sacar AWP", "sacar", 10, "AWP", loop=False)
        seq("sacar pistola", "sacar", 8, "Glock" if team == "tr" else "USP", loop=False)
    if "recarga_video" in GRUPOS:   # recarga com 24 quadros por arma (vídeo)
        for w in WEAP: seq("rv " + w, "recarga", max(8, round(RELOAD_S[w] * FPS)), w, loop=False)   # duração real
    if "awp2" in GRUPOS:
        seq("AWP atirando na luneta (com ferrolho)", "awp_tiro_luneta", max(8, round(SHOT_S["AWP"] * FPS)), "AWP", loop=False)
        seq("AWP recarga saindo da luneta", "recarga", max(8, round(RELOAD_S["AWP"] * FPS)), "AWP", loop=False, from_scope=True)
        for w, lab in ((padrao, "fuzil"), ("AWP", "AWP"), ("Glock" if team == "tr" else "USP", "pistola")):
            fs = []
            for pd in (25, 12, 0, -12, -25):
                A.pose(0, "mira", w, pitch=math.radians(pd)); p = os.path.join(OUT, f"{team}_pitch_{w}_{pd + 25:02d}.png"); render(p); fs.append(p)
            linhas.append((f"{lab}: olhando pra cima 25, 12, reto, pra baixo 12, 25", fs))
        fs = []
        for i, dn in enumerate(DIRS):   # pra baixo nas 8 direções (descendo rampa / mirando quem está embaixo)
            A.root.rotation_euler = (0, 0, -i * math.pi / 4)
            A.pose(0, "mira", padrao, pitch=math.radians(-20)); p = os.path.join(OUT, f"{team}_pitchbaixo_{i}.png"); render(p); fs.append(p)
        linhas.append(("olhando pra baixo nas 8 direcoes", fs))
        fs = []
        for i, dn in enumerate(DIRS):
            A.root.rotation_euler = (0, 0, -i * math.pi / 4)
            A.pose(0, "mira", padrao, pitch=math.radians(20)); p = os.path.join(OUT, f"{team}_pitchcima_{i}.png"); render(p); fs.append(p)
        linhas.append(("olhando pra cima nas 8 direcoes", fs))
        A.root.rotation_euler = (0, 0, -7 * math.pi / 4)
    if "coice" in GRUPOS:
        # 3 quadros por arma (parado, voltando, disparo com clarão) + AWP com o ciclo do ferrolho; e onde ficam, na
        # imagem, a janela de ejeção, a boca do cano e o pé (pra cápsula cair no chão certo)
        from bpy_extras.object_utils import world_to_camera_view
        import json
        def px(p_local):
            v = world_to_camera_view(sc, cam, A.root.matrix_world @ p_local)
            return [round(v.x * sc.render.resolution_x, 1), round((1 - v.y) * sc.render.resolution_y, 1)]
        info = {}
        for w in WEAP:
            if WEAP[w].get("bolt"):
                for fi in range(16):
                    A.pose(fi / 15, "tiro", w); render(os.path.join(OUT, f"{team}_awp_{fi:02d}.png"))
            for i, a in enumerate((0.0, 0.5, 1.0)):
                A.pose(a, "coice", w); render(os.path.join(OUT, f"{team}_coice_{w}_{i}.png"))
            A.pose(0.0, "coice", w)
            info[w] = {"ejecao": px(A.eject_local), "boca": px(A.muzzle_local), "pe": px(Vector((0.05, -0.06, 0.0)))}
        with open(os.path.join(OUT, f"{team}_pontos.json"), "w") as fh: json.dump(info, fh)
    if "cadencia" in GRUPOS:   # um ciclo de tiro por arma (8 quadros; o jogo toca no intervalo da arma)
        for w in WEAP: seq("ciclo " + w, "tiro", 8, w, loop=False)
    if "lista" in GRUPOS:
        fs = []
        for w in WEAP:
            A.pose(0, "mira", w); p = os.path.join(OUT, f"{team}_arma_{w}.png"); render(p); fs.append(p)
        linhas.append(("armas", fs))
    if "awp" in GRUPOS:
        seq("AWP parado", "mira", 1, "AWP"); seq("AWP entrando na luneta", "awp_mirar", 8, "AWP", loop=False)
        seq("AWP na luneta", "awp_mirando", 4, "AWP")
    if "faca" in GRUPOS:
        seq("faca parado", "faca_parado", 1); seq("faca andando (mais rapido)", "faca_andar", 8)
        seq("faca M1 (corte rapido)", "faca_m1", 8, loop=False); seq("faca M2 (estocada)", "faca_m2", 10, loop=False)
        seq("sacar faca", "sacar_faca", 8, loop=False)
        fs = []   # faca de perto (só ela, grande) pra ver o modelo
        if "close" not in A.__dict__:
            A.pose(0, "faca_parado", padrao)
            sc.render.resolution_x = sc.render.resolution_y = 192; cam.data.ortho_scale = 0.45
            cam.location = A.knife.matrix_world.translation + dcam * 10
            p = os.path.join(OUT, f"{team}_faca_close.png"); render(p); fs.append(p)
            cam.data.ortho_scale = RES / PX_PER_BU; cam.location = Vector((0.08, 0, 0.5)) + dcam * 10
            linhas.append(("faca de perto", fs))
    if "granadas" in GRUPOS:
        for nd in ("smoke", "flash", "molotov", "he"): seq("granada " + nd, "granada", 14, loop=False, nade=nd)
    if "outros" in GRUPOS:
        seq("cego (flash)", "cego", 8)
        seq("plantar agachado", "plantar", 10, loop=False); seq("plantar em pe", "plantar_pe", 10, loop=False)
        seq("desarmar agachado", "desarmar", 8); seq("desarmar em pe", "desarmar_pe", 8)
        sc.render.resolution_x = sc.render.resolution_y = 288; cam.data.ortho_scale = 288 / PX_PER_BU   # cai pra trás: quadro maior
        seq("morrer", "morrer", 10, loop=False)
        sc.render.resolution_x = sc.render.resolution_y = RES; cam.data.ortho_scale = RES / PX_PER_BU
    if "jogo" in GRUPOS:
        # o conjunto que o jogo usa, nas 8 direções: {team}_{anim}_{arma}_{dir}_{quadro}.png
        #   por arma: mira (parado), andar, tiro, recarga; faca: faca_parado, faca_andar; com o fuzil padrão:
        #   agachado, granada, plantar (TR) / desarmar (CT), cego, morrer (quadro maior, 288)
        armas = (["AK47", "AWP", "Glock", "MAC10", "Deagle"] if team == "tr" else ["M4A4", "M4A1S", "AWP", "USP", "MP9", "Deagle"])
        # arma pedida na linha de comando vale mesmo que seja do outro time (pegou do chão: CT de AK, TR de M4)
        if len(ARGV) > 3: armas = [a for a in WEAP if a in ARGV[3].split(",")]
        TIRO_N = {"rifle": 6, "smg": 6, "pistol": 5}
        # quadro de 288 (o de 192 cortava o cano da AWP/M4A1-S e o fuzil no cego/granada em algumas direções)
        sc.render.resolution_x = sc.render.resolution_y = 288; cam.data.ortho_scale = 288 / PX_PER_BU
        # direções (env NDIR, padrão 32 = 11,25°): com 8 o boneco virava aos saltos de 45°; o usuário quer olhar 360
        NDIR = int(os.environ.get("NDIR", "32"))
        def jseq(anim_key, anim, n, gun, loop=True, **kw):
            for i in range(NDIR):
                A.root.rotation_euler = (0, 0, -i * 2 * math.pi / NDIR)
                for fi in range(n):
                    tt = fi / n if loop else (fi / (n - 1) if n > 1 else 0)
                    A.pose(tt, anim, gun, **kw)
                    render(os.path.join(OUT, f"{team}_{anim_key}_{gun}_{i}_{fi:02d}.png"))
            linhas.append((f"{anim_key} {gun} ({NDIR} dir x {n})", []))
        so_passo = len(ARGV) > 3 and ("so_passo" in ARGV[3].split(",") or "so_ar" in ARGV[3].split(","))
        so_ar = len(ARGV) > 3 and "so_ar" in ARGV[3].split(",")
        if so_ar:
            # só as poses no ar (pulo e queda), por arma e de faca
            todas = (["AK47", "AWP", "Glock", "MAC10", "Deagle"] if team == "tr" else ["M4A4", "M4A1S", "AWP", "USP", "MP9", "Deagle"])
            for w in (armas or todas):
                jseq("no_ar_sobe", "no_ar_sobe", 1, w); jseq("no_ar_cai", "no_ar_cai", 1, w)
            if "extras" in ARGV[3].split(","):
                jseq("faca_no_ar_sobe", "faca_no_ar_sobe", 1, padrao); jseq("faca_no_ar_cai", "faca_no_ar_cai", 1, padrao)
            armas = []
        if so_passo and not armas and not so_ar: armas = (["AK47", "AWP", "Glock", "MAC10", "Deagle"] if team == "tr" else ["M4A4", "M4A1S", "AWP", "USP", "MP9", "Deagle"])
        # passos: 12 quadros por ciclo (com 8, no vídeo a perna pulava quadro e parecia mais rápida que o corpo)
        PASSO_N = 12
        for w in armas:
            if not so_passo: jseq("mira", "mira", 1, w)
            jseq("andar", "andar", PASSO_N, w)
            if so_passo: continue
            jseq("tiro", "tiro", 10 if w == "AWP" else TIRO_N[WEAP[w]["kind"]], w, loop=False)
            jseq("recarga", "recarga", 12, w, loop=False)
        if not so_ar and (len(ARGV) <= 3 or "extras" in ARGV[3].split(",")):
            jseq("agachado", "agachado", PASSO_N, padrao)
            if not so_passo: jseq("faca_parado", "faca_parado", 1, padrao)
            jseq("faca_andar", "faca_andar", PASSO_N, padrao)
        # quanto o corpo anda num ciclo (pé plantado: 4 x passada), em unidades do jogo (1 BU = 25): o jogo toca o ciclo
        # na velocidade do boneco e o pé não patina
        with open(os.path.join(OUT, "passo.txt"), "w") as fh:
            # pé no chão por uma fração f do ciclo varrendo 2 x passada: corpo anda 2 x passada / f por ciclo (andando f = 0,5)
            for an, s_, f_ in (("andar", 0.17, 0.35), ("agachado", 0.1, 0.5), ("faca_andar", 0.18, 0.32)): fh.write(f"{an} {2 * s_ / f_ * 25:.2f}\n")
        if not so_passo and (len(ARGV) <= 3 or "extras" in ARGV[3].split(",")):
            jseq("granada", "granada", 14, padrao, loop=False, nade="smoke")
            jseq("plantar" if team == "tr" else "desarmar", "plantar" if team == "tr" else "desarmar", 10 if team == "tr" else 8, padrao, loop=team != "tr")
            jseq("cego", "cego", 8, padrao)
            jseq("morrer", "morrer", 10, padrao, loop=False)
        sc.render.resolution_x = sc.render.resolution_y = RES; cam.data.ortho_scale = RES / PX_PER_BU
        A.root.rotation_euler = (0, 0, -7 * math.pi / 4)
    with open(os.path.join(OUT, f"manifest_{team}.txt"), "w", encoding="utf-8") as fh:
        for label, fs in linhas: fh.write(label + "|" + "|".join(fs) + "\n")
print("SPRITES OK")
