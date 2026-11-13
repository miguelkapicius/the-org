# Entrada dos times no palco do campeonato (cutscene) com os jogadores "da vida real" no traço dos bonecos do jogo:
# mesmas proporções de chibi do agentes.py (perna 0,13+0,13, quadril 0,30, cabeça 0,17), mesmo desenho em 3 tons +
# contorno escuro, mas de camisa do time, calça, tênis, cabelo, barba, óculos e fone.
# Móveis do palco = Kenney Furniture Kit (CC0, Art/Kenney/furniture_zip): cadeira gamer, monitor, teclado, mouse.
#
#   blender -b --factory-startup --python Tools/jogadores3d/entrada.py -- folha              -> Logs/entrada/folha.png
#   blender -b --factory-startup --python Tools/jogadores3d/entrada.py -- quadros DE ATE [PCT] [PASSO]
#   blender -b --factory-startup --python Tools/jogadores3d/entrada.py -- video              -> Logs/entrada/entrada.mp4
#   blender -b --factory-startup --python Tools/jogadores3d/entrada.py -- blend              -> Logs/entrada/entrada.blend
# Coordenadas: Z cima; plateia em -Y, telão em +Y. Boneco local: X frente, Y esquerda (igual ao agentes.py).
import bpy, math, sys, os, random
from mathutils import Vector, Matrix, Quaternion

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODO = ARGV[0] if ARGV else "folha"
RAIZ = r"D:\unity\CSManager"
SAIDA = os.path.join(RAIZ, "Logs", "entrada")
QUADROS = os.path.join(SAIDA, "q")
KEN = os.path.join(RAIZ, r"Art\Kenney\furniture_zip\Models\GLTF format")
FONTE = r"C:\Windows\Fonts\bahnschrift.ttf"
FPS = 30
DUR = 32.0
os.makedirs(QUADROS, exist_ok=True)

# ------------------------------------------------------------------ cena
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        sc.render.engine = eng
        break
    except TypeError:
        pass
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
sc.render.fps = FPS
sc.render.filter_size = 0.8
sc.view_settings.view_transform = "Standard"
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
ee = sc.eevee
for k, v in (("taa_render_samples", int(os.environ.get("AMOSTRAS", "16"))),("volumetric_tile_size", "4"), ("volumetric_samples", 48),
             ("use_volumetric_shadows", True), ("volumetric_end", 30.0), ("volumetric_start", 0.5),
             ("use_gtao", True), ("gtao_distance", 0.4)):
    try: setattr(ee, k, v)
    except Exception as e: print("eevee", k, e)

def srgb(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (a,)

def gama(h, a=1.0):
    """Cor em sRGB sem linearizar (o texto do sequenciador já trabalha em gama)."""
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)) + (a,)

def mistura(h1, h2, f):
    a = srgb(h1); b = srgb(h2)
    c = [a[i] * (1 - f) + b[i] * f for i in range(3)]
    def g(x): return 12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055
    return "#" + "".join("%02x" % int(max(0, min(1, g(x))) * 255 + 0.5) for x in c)

# ------------------------------------------------------------------ material de desenho (3 tons + contorno)
# Igual ao toon() do agentes.py (Diffuse -> ShaderToRGB -> rampa constante -> x cor), com duas coisas a mais pro palco:
# a cor da luz tinge o tom (holofote laranja/azul aparece no boneco) e a sombra é mais funda (palco escuro).
MATS = {}
def toon(nome, cor, escuro=0.3, brilho=0.0, tinge=0.6):
    chave = (cor, escuro, brilho, tinge)
    if chave in MATS: return MATS[chave]
    m = bpy.data.materials.new(nome); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear(); N = nt.nodes.new; L = nt.links.new
    out = N("ShaderNodeOutputMaterial"); dif = N("ShaderNodeBsdfDiffuse"); s2r = N("ShaderNodeShaderToRGB")
    L(dif.outputs[0], s2r.inputs[0])
    bw = N("ShaderNodeRGBToBW"); L(s2r.outputs[0], bw.inputs[0])
    ramp = N("ShaderNodeValToRGB"); ramp.color_ramp.interpolation = "CONSTANT"
    e = ramp.color_ramp.elements
    e[0].position = 0.0; e[0].color = (escuro, escuro, escuro * 1.08, 1)
    e[1].position = 0.1; e[1].color = (0.62, 0.6, 0.63, 1)
    e3 = e.new(0.42); e3.color = (1, 1, 1, 1)
    e4 = e.new(0.95); e4.color = (1.22, 1.2, 1.18, 1)
    L(bw.outputs[0], ramp.inputs[0])
    # tinta da luz: cor / claridade (só o matiz), meio a meio com branco
    mx = N("ShaderNodeMath"); mx.operation = "MAXIMUM"; mx.inputs[1].default_value = 0.03; L(bw.outputs[0], mx.inputs[0])
    dv = N("ShaderNodeMixRGB"); dv.blend_type = "DIVIDE"; dv.inputs[0].default_value = 1
    L(s2r.outputs[0], dv.inputs[1]); L(mx.outputs[0], dv.inputs[2])
    tn = N("ShaderNodeMixRGB"); tn.blend_type = "MIX"; tn.inputs[0].default_value = tinge; tn.use_clamp = False
    tn.inputs[1].default_value = (1, 1, 1, 1); L(dv.outputs[0], tn.inputs[2])
    rgb = N("ShaderNodeRGB"); rgb.outputs[0].default_value = srgb(cor)
    m1 = N("ShaderNodeMixRGB"); m1.blend_type = "MULTIPLY"; m1.inputs[0].default_value = 1
    L(ramp.outputs[0], m1.inputs[1]); L(rgb.outputs[0], m1.inputs[2])
    m2 = N("ShaderNodeMixRGB"); m2.blend_type = "MULTIPLY"; m2.inputs[0].default_value = 1
    L(m1.outputs[0], m2.inputs[1]); L(tn.outputs[0], m2.inputs[2])
    fim = m2.outputs[0]
    if brilho:
        ad = N("ShaderNodeMixRGB"); ad.blend_type = "ADD"; ad.inputs[0].default_value = brilho
        L(fim, ad.inputs[1]); L(rgb.outputs[0], ad.inputs[2]); fim = ad.outputs[0]
    em = N("ShaderNodeEmission"); L(fim, em.inputs[0]); L(em.outputs[0], out.inputs[0])
    MATS[chave] = m
    return m

def luz_mat(nome, cor, forca=1.0):
    """Emissão pura (LED, tela, lâmpada): não depende da luz da cena."""
    m = bpy.data.materials.new(nome); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = srgb(cor) if isinstance(cor, str) else cor
    em.inputs[1].default_value = forca
    out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], out.inputs[0])
    return m

CONTORNO = luz_mat("contorno", "#1c1416", 1.0); CONTORNO.use_backface_culling = True

def contorno(o, th=0.009):
    o.data.materials.append(CONTORNO)
    md = o.modifiers.new("contorno", "SOLIDIFY")
    md.thickness = th; md.offset = 1; md.use_flip_normals = True; md.use_rim = False
    md.material_offset = len(o.data.materials) - 1
    o["th"] = th

def acerta_contorno(o):
    md = o.modifiers.get("contorno")
    if md:
        s = (abs(o.scale[0]) + abs(o.scale[1]) + abs(o.scale[2])) / 3
        md.thickness = o.get("th", 0.009) / max(s, 1e-4)

def _esfera_base(seg):
    nome = "esfera%d" % seg
    if nome in bpy.data.meshes: return bpy.data.meshes[nome]
    import bmesh
    me = bpy.data.meshes.new(nome); bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=1)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    return me

def _cubo_base():
    if "cubo" in bpy.data.meshes: return bpy.data.meshes["cubo"]
    import bmesh
    me = bpy.data.meshes.new("cubo"); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=2); bm.to_mesh(me); bm.free()
    return me

COLE = sc.collection
def novo(nome, me, pai=None):
    o = bpy.data.objects.new(nome, me); COLE.objects.link(o)
    if pai: o.parent = pai
    return o

def ell(nome, mat, seg=24, pai=None, th=0.009, linha=True):
    me = _esfera_base(seg).copy(); me.materials.append(mat)
    o = novo(nome, me, pai)
    if linha: contorno(o, th)
    return o

def box(nome, mat, meia, bisel=0.008, pai=None, linha=True, th=0.009):
    me = _cubo_base().copy(); me.materials.append(mat)
    o = novo(nome, me, pai)
    if bisel > 0:
        bv = o.modifiers.new("bv", "BEVEL"); bv.width = bisel / max(min(meia), 1e-3); bv.segments = 2
    if linha: contorno(o, th)
    o["r"] = meia; o.scale = meia
    return o

def torus(nome, mat, R, r, pai=None):
    import bmesh
    me = bpy.data.meshes.new(nome); bm = bmesh.new()
    seg, sseg = 24, 8
    vs = []
    for i in range(seg):
        a = 2 * math.pi * i / seg; ring = []
        for j in range(sseg):
            b = 2 * math.pi * j / sseg
            ring.append(bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
        vs.append(ring)
    for i in range(seg):
        for j in range(sseg):
            bm.faces.new((vs[i][j], vs[(i + 1) % seg][j], vs[(i + 1) % seg][(j + 1) % sseg], vs[i][(j + 1) % sseg]))
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    me.materials.append(mat)
    return novo(nome, me, pai)

def place_seg(o, a, b, r):
    d = b - a; L = d.length
    o.location = (a + b) / 2
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.to_track_quat("Z", "Y")
    o.scale = (r, r, L / 2 + r * 0.6); acerta_contorno(o)

def place(o, c, r=None, q=None):
    o.location = c
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = q or Quaternion()
    o.scale = r if r is not None else tuple(o["r"]); acerta_contorno(o)

def ik(s, h, l1, l2, pole):
    v = h - s; dd = min(v.length, l1 + l2 - 1e-3); u = v.normalized()
    a = (l1 * l1 - l2 * l2 + dd * dd) / (2 * dd); hh = math.sqrt(max(l1 * l1 - a * a, 0))
    p = pole - u * pole.dot(u); p = p.normalized() if p.length > 1e-6 else Vector((0, 0, -1))
    return s + u * a + p * hh, s + u * dd

def sm(x): x = min(max(x, 0.0), 1.0); return x * x * (3 - 2 * x)
def k(t, a, b): return min(max((t - a) / (b - a), 0.0), 1.0)
def qz(a): return Quaternion((0, 0, 1), a)
def lerp(a, b, u): return a + (b - a) * u

# ------------------------------------------------------------------ os jogadores (times padrão do jogo: Match.T_TEAM/CT_TEAM)
TIMES = {
    "VULCÃO": dict(camisa="#d8462a", detalhe="#1f1c22", faixa="#ffab2e", calca="#24232a", sola="#d8462a", luz="#ff7a2a",
                   fone="#1c1b20", foneCor="#ff8a2a", cadeira="#c9402a"),
    "ROCHA":  dict(camisa="#2c4f86", detalhe="#d7dde4", faixa="#7fb6ee", calca="#2a3140", sola="#2c4f86", luz="#4aa8ff",
                   fone="#1c1d22", foneCor="#5ab4ff", cadeira="#2c4f86"),
}
# função, pele, cabelo (cor), estilo do cabelo, rosto (barba/óculos), altura, calça própria (None = do time)
FICHAS = [
    dict(nick="VULCAO", time="VULCÃO", funcao="Entry",   pele="#c68a62", cabelo="#1a1414", estilo="degrade", rosto=["rala"], alt=1.0),
    dict(nick="FAISCA", time="VULCÃO", funcao="Suporte", pele="#f0c6a2", cabelo="#e6c873", estilo="topete", rosto=[], alt=0.97),
    dict(nick="SOMBRA", time="VULCÃO", funcao="Lurker",  pele="#8a5a3c", cabelo="#161214", estilo="bone", rosto=[], alt=1.02, bone="#1f1c22"),
    dict(nick="MIRA",   time="VULCÃO", funcao="AWP",     pele="#e2ad86", cabelo="#4a2e1c", estilo="rabo", rosto=["oculos"], alt=0.99),
    dict(nick="TROVAO", time="VULCÃO", funcao="IGL",     pele="#a8714c", cabelo="#2a1a12", estilo="cacheado", rosto=["barba"], alt=1.04),
    dict(nick="ROCHA",  time="ROCHA",  funcao="Âncora",  pele="#5e3b28", cabelo="#141012", estilo="raspado", rosto=["barba"], alt=1.05),
    dict(nick="ESCUDO", time="ROCHA",  funcao="Rotação", pele="#e8b896", cabelo="#2b1d14", estilo="franja", rosto=["oculos"], alt=0.98),
    dict(nick="FALCAO", time="ROCHA",  funcao="AWP",     pele="#b77a52", cabelo="#1d1a20", estilo="moicano", rosto=[], alt=1.01, tinta="#4fb0ff"),
    dict(nick="ANCORA", time="ROCHA",  funcao="Âncora",  pele="#d39b72", cabelo="#7a3a1e", estilo="coque", rosto=["barba"], alt=1.03),
    dict(nick="RADAR",  time="ROCHA",  funcao="Suporte", pele="#9c6644", cabelo="#171314", estilo="afro", rosto=[], alt=0.98),
]
NOME_VISIVEL = {"VULCAO": "VULCÃO", "TROVAO": "TROVÃO", "FAISCA": "FAÍSCA", "FALCAO": "FALCÃO", "ANCORA": "ÂNCORA"}

HR = Vector((0.17, 0.175, 0.165))          # raio da cabeça (agentes.py)
def na_cabeca(d, k=1.0):
    """Ponto da superfície da cabeça na direção d (coordenadas da cabeça, centro na origem)."""
    e = math.sqrt((d.x / HR.x) ** 2 + (d.y / HR.y) ** 2 + (d.z / HR.z) ** 2)
    return d / max(e, 1e-4) * k

def normal_cabeca(p):
    return Vector((p.x / HR.x ** 2, p.y / HR.y ** 2, p.z / HR.z ** 2)).normalized()

class Jogador:
    def __init__(s, f):
        s.f = f; T = TIMES[f["time"]]; s.T = T
        nm = f["nick"]
        s.root = bpy.data.objects.new(nm, None); COLE.objects.link(s.root)
        s.P = {}; s.cab = []   # peças da cabeça: (chave, centro, raios, giro local) em coordenadas da cabeça
        mt = lambda c, **kw: toon(nm + c, c, **kw)
        pele, cab = mt(f["pele"]), mt(f["cabelo"])
        cam_, det, fx = mt(T["camisa"]), mt(T["detalhe"]), mt(T["faixa"])
        s.mat = dict(pele=pele, cabelo=cab)
        def E(chave, mat, seg=24, th=0.009): o = ell(nm + chave, mat, seg, s.root, th); s.P[chave] = o; return o
        def B(chave, mat, meia, bis=0.008): o = box(nm + chave, mat, meia, bis, s.root); s.P[chave] = o; return o
        calca = mt(f.get("calca", T["calca"]))
        for sd in ("L", "R"):
            E("coxa" + sd, calca); E("canela" + sd, calca)
            E("tenis" + sd, mt("#f2efe8")); E("sola" + sd, mt(T["sola"])); E("cadarco" + sd, mt("#f2efe8"))
        E("quadril", calca); E("barra", cam_)
        E("tronco", cam_); E("faixa", fx); E("gola", det); E("ombroL", det); E("ombroR", det)
        for sd in ("L", "R"):
            E("braco" + sd, cam_); E("punhoCam" + sd, det); E("ante" + sd, pele); E("mao" + sd, pele)
        # cabeça: pele, orelhas, nariz, olhos (branco, pupila, brilho), sobrancelhas, boca
        E("cabeca", pele, 32); E("orelhaL", pele); E("orelhaR", pele); E("nariz", pele, 16, 0.006)
        s.cab += [("cabeca", Vector((0, 0, 0)), HR, None)]
        for sd, sg in (("L", 1), ("R", -1)):
            s.cab.append(("orelha" + sd, Vector((-0.004, sg * 0.168, -0.004)), Vector((0.03, 0.02, 0.044)), None))
        s.cab.append(("nariz", na_cabeca(Vector((1, 0, -0.08))) + Vector((0.004, 0, 0)), Vector((0.018, 0.02, 0.016)), None))
        for sd in ("L", "R"):
            o = ell(nm + "branco" + sd, mt("#f4efe8"), 16, s.root, 0.004); s.P["branco" + sd] = o
            o = ell(nm + "olho" + sd, mt("#19140f"), 16, s.root, 0.0, False); s.P["olho" + sd] = o
            o = ell(nm + "brilho" + sd, luz_mat(nm + "br", "#ffffff"), 12, s.root, 0, False); s.P["brilho" + sd] = o
            o = box(nm + "sobr" + sd, mt(mistura(f["cabelo"], "#000000", 0.25)), (0.004, 0.02, 0.0065), 0.003, s.root, False); s.P["sobr" + sd] = o
        o = ell(nm + "boca", mt("#6a2a26"), 16, s.root, 0, False); s.P["boca"] = o
        s.decal = []   # (chave, direção na cabeça, larg, alt, afasta, giro em X)
        for sd, sg in (("L", 1), ("R", -1)):
            s.decal += [("branco" + sd, Vector((0.15, sg * 0.056, 0.035)), 0.026, 0.021, 0.001, 0),
                        ("olho" + sd, Vector((0.15, sg * 0.053, 0.032)), 0.0135, 0.017, 0.004, 0),
                        ("brilho" + sd, Vector((0.15, sg * 0.048, 0.04)), 0.0045, 0.0045, 0.007, 0),
                        ("sobr" + sd, Vector((0.15, sg * 0.058, 0.077)), 0.02, 0.0065, 0.004, sg * math.radians(-8))]
        s.decal.append(("boca", Vector((0.15, 0, -0.068)), 0.022, 0.0075, 0.002, 0))
        s.topo = 0.17   # altura do topo (pro arco do fone)
        s.lado = 0.18
        s._cabelo(f)
        for r in f["rosto"]:
            if r == "barba":
                o = E("barba", cab)
                s.cab.append(("barba", Vector((0.055, 0, -0.07)), Vector((0.122, 0.158, 0.098)), None))
                s.decal = [d for d in s.decal if d[0] != "boca"] + [("boca", Vector((0.15, 0, -0.068)), 0.022, 0.0075, 0.03, 0)]
            if r == "rala":
                o = E("barba", mt(mistura(f["pele"], f["cabelo"], 0.3)))
                s.cab.append(("barba", Vector((0.03, 0, -0.045)), Vector((0.148, 0.17, 0.122)), None))
            if r == "oculos":
                aro = mt("#1e1c22")
                for sd, sg in (("L", 1), ("R", -1)):
                    o = torus(nm + "aro" + sd, aro, 1.0, 0.16, s.root); contorno(o, 0.003); s.P["aro" + sd] = o
                    s.cab.append(("aro" + sd, na_cabeca(Vector((0.15, sg * 0.056, 0.036))) + Vector((0.016, 0, 0)),
                                  Vector((0.034, 0.034, 0.034)), Quaternion((0, 1, 0), math.pi / 2)))
                    o = E("haste" + sd, aro, 12, 0.003)
                    s.cab.append(("haste" + sd, Vector((0.06, sg * 0.168, 0.04)), Vector((0.1, 0.006, 0.006)), None))
                o = E("ponte", aro, 12, 0.003)
                s.cab.append(("ponte", Vector((0.188, 0, 0.042)), Vector((0.006, 0.02, 0.006)), None))
        # fone (no pescoço até a hora de jogar)
        fone, fc = mt(T["fone"]), mt(T["foneCor"])
        for sd in ("L", "R"):
            E("concha" + sd, fone); E("tampa" + sd, fc); E("almofada" + sd, mt("#2e2c33"))
        for i in range(6): E("arco%d" % i, fone, 12, 0.005)
        E("mic", fone, 12, 0.005); E("micPonta", fc, 12, 0.005)

    def _cabelo(s, f):
        nm = f["nick"]; est = f["estilo"]; P = s.P
        cab = toon(nm + "cab", f["cabelo"])
        def C(chave, centro, raios, q=None, mat=cab, seg=24):
            o = ell(nm + chave, mat, seg, s.root); P[chave] = o
            s.cab.append((chave, Vector(centro), Vector(raios), q))
        calota = ((-0.03, 0, 0.05), (0.165, 0.18, 0.155))
        if est == "degrade":
            C("calota", (-0.02, 0, 0.035), (0.168, 0.179, 0.157))
            C("topo", (0.01, 0, 0.115), (0.135, 0.13, 0.07))
            s.topo = 0.19
        elif est == "raspado":
            C("calota", (-0.006, 0, 0.012), (0.172, 0.178, 0.164), mat=toon(nm + "rasp", mistura(f["pele"], f["cabelo"], 0.7)))
        elif est == "topete":
            C("calota", *calota)
            C("topete", (0.075, 0, 0.15), (0.11, 0.125, 0.06), Quaternion((0, 1, 0), math.radians(-22)))
            C("topete2", (0.02, 0, 0.17), (0.1, 0.11, 0.05))
            s.topo = 0.215
        elif est == "bone":
            bone = toon(nm + "bone", f.get("bone", "#222"))
            C("calota", (-0.025, 0, 0.03), (0.17, 0.181, 0.15))
            C("domo", (-0.012, 0, 0.065), (0.176, 0.184, 0.145), mat=bone)
            C("aba", (-0.2, 0, 0.085), (0.085, 0.1, 0.014), Quaternion((0, 1, 0), math.radians(-14)), mat=bone)
            C("botao", (-0.01, 0, 0.21), (0.016, 0.016, 0.01), mat=toon(nm + "bfx", s.T["faixa"]))
            # boné pra trás: na testa aparece o vão do regulador (cabelo) com a tira por cima
            C("vao", (0.14, 0, 0.115), (0.03, 0.05, 0.03), mat=cab)
            C("tira", (0.146, 0, 0.098), (0.02, 0.055, 0.008), mat=toon(nm + "bfx2", s.T["faixa"]))
            s.topo = 0.215
        elif est == "rabo":
            C("calota", (-0.03, 0, 0.045), (0.167, 0.181, 0.158))
            C("risca", (0.06, 0.03, 0.16), (0.09, 0.07, 0.035), Quaternion((0, 1, 0), math.radians(-15)))
            C("prende", (-0.17, 0, 0.03), (0.03, 0.032, 0.03), mat=toon(nm + "elast", s.T["faixa"]))
            C("rabo1", (-0.21, 0, -0.03), (0.042, 0.04, 0.07), Quaternion((0, 1, 0), math.radians(25)))
            C("rabo2", (-0.235, 0, -0.11), (0.032, 0.03, 0.05), Quaternion((0, 1, 0), math.radians(15)))
            s.topo = 0.2
        elif est == "cacheado":
            C("calota", (-0.035, 0, 0.06), (0.175, 0.19, 0.165))
            for i, (x, y, z) in enumerate([(0.06, 0.07, 0.17), (0.06, -0.07, 0.17), (-0.03, 0.0, 0.21), (-0.1, 0.1, 0.15),
                                           (-0.1, -0.1, 0.15), (0.02, 0.13, 0.12), (0.02, -0.13, 0.12), (-0.16, 0.0, 0.1),
                                           (0.09, 0.0, 0.18), (-0.06, 0.08, 0.2), (-0.06, -0.08, 0.2)]):
                C("cacho%d" % i, (x, y, z), (0.06, 0.06, 0.055), seg=16)
            s.topo = 0.255; s.lado = 0.2
        elif est == "franja":
            C("calota", (-0.025, 0, 0.045), (0.17, 0.183, 0.16))
            C("franja", (0.118, 0, 0.1), (0.065, 0.155, 0.05), Quaternion((0, 1, 0), math.radians(20)))
            C("franjaL", (0.1, 0.11, 0.06), (0.05, 0.05, 0.06))
            C("franjaR", (0.1, -0.11, 0.06), (0.05, 0.05, 0.06))
            s.topo = 0.2
        elif est == "moicano":
            C("calota", (-0.006, 0, 0.014), (0.172, 0.178, 0.164), mat=toon(nm + "rasp", mistura(f["pele"], f["cabelo"], 0.65)))
            tinta = toon(nm + "tinta", f.get("tinta", f["cabelo"]))
            C("crista", (-0.01, 0, 0.165), (0.17, 0.05, 0.065), mat=tinta)
            C("crista2", (0.11, 0, 0.15), (0.06, 0.045, 0.05), Quaternion((0, 1, 0), math.radians(-30)), mat=tinta)
            s.topo = 0.235
        elif est == "coque":
            C("calota", (-0.03, 0, 0.045), (0.167, 0.181, 0.158))
            C("coque", (-0.11, 0, 0.175), (0.06, 0.06, 0.055))
            C("prende", (-0.085, 0, 0.15), (0.03, 0.045, 0.03), mat=toon(nm + "elast", "#1a1a1a"))
            s.topo = 0.23
        elif est == "afro":
            C("calota", (-0.07, 0, 0.09), (0.21, 0.235, 0.2))
            for i, (x, y, z) in enumerate([(0.03, 0.12, 0.2), (0.03, -0.12, 0.2), (-0.08, 0, 0.27), (-0.18, 0.12, 0.14),
                                           (-0.18, -0.12, 0.14), (0.05, 0, 0.24), (-0.05, 0.2, 0.1), (-0.05, -0.2, 0.1)]):
                C("bolha%d" % i, (x, y, z), (0.075, 0.075, 0.07), seg=16)
            s.topo = 0.3; s.lado = 0.245
        else:
            C("calota", *calota)

    # -------------------------------------------------------------- pose
    def pose(s, st):
        """st: andar (fase em ciclos, peso), recuar, sentar 0..1, pes (pés levantados no giro), acenar, soco,
        fone 0..1, digitar, olhar (yaw da cabeça relativo ao corpo), respirar (t), teclado/mouse (pontos locais)."""
        P = s.P; t = st.get("t", 0.0)
        andar = st.get("andar", 0.0); fase = st.get("fase", 0.0); sent = sm(st.get("sentar", 0.0))
        passo = 0.12
        hipz = 0.30 - 0.025 * andar
        lean = 0.0
        pes = {"L": Vector((0.0, 0.07, 0.06)), "R": Vector((0.0, -0.07, 0.06))}
        for sd, sg in (("L", 1), ("R", -1)):
            if andar > 0:
                u = (fase + (0 if sg > 0 else 0.5)) % 1.0
                if u < 0.5: dx = passo * (1 - 4 * u); up = 0.0
                else:
                    v = (u - 0.5) / 0.5; dx = -passo + 2 * passo * sm(v); up = math.sin(math.pi * v) * 0.07
                pes[sd] = pes[sd] + Vector((dx * andar, 0, up * andar))
        bob = 0.012 * abs(math.sin(2 * math.pi * fase)) * andar
        hipz += bob
        quad = Vector((0, 0, hipz))
        # sentar: o quadril vai pra trás e desce até o assento; pés vão pra frente
        if sent > 0:
            alvo = Vector((-0.12, 0, 0.26))
            # desce com o tronco pra frente (contrapeso) e endireita no fim
            quad = quad.lerp(alvo, sent)
            lean += 0.35 * math.sin(math.pi * min(sent * 1.1, 1.0)) - 0.06 * sent
            for sd, sg in (("L", 1), ("R", -1)):
                pes[sd] = pes[sd].lerp(Vector((0.05, sg * 0.085, 0.06)), sent)
        lev = st.get("pes", 0.0)
        if lev > 0:
            for sd, sg in (("L", 1), ("R", -1)):
                pes[sd] = pes[sd].lerp(Vector((0.1, sg * 0.09, 0.15 + 0.02 * sg)), sm(lev))
        respira = 0.004 * math.sin(2 * math.pi * t / 3.2)
        for sd, sg in (("L", 1), ("R", -1)):
            q = quad + Vector((0, sg * 0.06, 0))
            pole = Vector((1, sg * 0.15, 0.25))
            joelho, tor = ik(q, pes[sd], 0.13, 0.13, pole)
            place_seg(P["coxa" + sd], q, joelho, 0.055); place_seg(P["canela" + sd], joelho, tor, 0.047)
            place(P["tenis" + sd], tor + Vector((0.03, 0, -0.024)), (0.08, 0.05, 0.04))
            place(P["sola" + sd], tor + Vector((0.03, 0, -0.046)), (0.083, 0.053, 0.016))
            place(P["cadarco" + sd], tor + Vector((0.06, 0, -0.004)), (0.035, 0.03, 0.014))
        place(P["quadril"], quad + Vector((0, 0, 0.01)), (0.088, 0.117, 0.062))
        # tronco: inclina (lean) em volta do quadril e gira um pouco (torce) pra olhar a plateia
        torce = st.get("torce", 0.0)
        Qt = qz(torce) @ Quaternion((0, 1, 0), lean)
        ch = quad + Qt @ Vector((0, 0, 0.15 + respira))
        place(P["barra"], quad + Qt @ Vector((0, 0, 0.05)), (0.097, 0.126, 0.045), Qt)
        place(P["tronco"], ch, (0.1, 0.13, 0.135), Qt)
        place(P["faixa"], ch + Qt @ Vector((0.0, 0, 0.035)), (0.103, 0.133, 0.028), Qt)
        place(P["gola"], ch + Qt @ Vector((0, 0, 0.118)), (0.068, 0.088, 0.032), Qt)
        # cabeça: gira no pescoço (olhar = yaw relativo ao tronco, pitch pra baixo quando joga)
        olhar = st.get("olhar", 0.0); pitch = st.get("pitch", 0.0)
        Qh = Qt @ qz(olhar - torce) @ Quaternion((0, 1, 0), pitch)
        pesc = ch + Qt @ Vector((0.01, 0, 0.12))
        hc = pesc + Qh @ Vector((0, 0, 0.15))
        for chave, c, r, q in s.cab:
            o = P[chave]
            place(o, hc + Qh @ c, tuple(r), Qh @ (q or Quaternion()))
        # pisca (cada um no seu tempo)
        fase_p = (t + 0.73 * len(s.f["nick"]) + 0.31 * ord(s.f["nick"][0])) % 3.9
        pisca = 0.12 if fase_p < 0.11 else 1.0
        for chave, d, w, h, afasta, gx in s.decal:
            if chave[:-1] in ("branco", "olho", "brilho"): h = h * pisca
            p = na_cabeca(d); n = normal_cabeca(p)
            qn = n.to_track_quat("X", "Z") @ Quaternion((1, 0, 0), gx)
            place(P[chave], hc + Qh @ (p + n * afasta), (0.003, w, h), Qh @ qn)
        # braços
        ombro = {}
        for sd, sg in (("L", 1), ("R", -1)):
            ombro[sd] = ch + Qt @ Vector((0, sg * 0.136, 0.07))
            place(P["ombro" + sd], ombro[sd] + Qt @ Vector((0, -sg * 0.01, 0.02)), (0.05, 0.05, 0.035), Qt)
        LB = 0.195
        alvo = {}
        balanco = 0.085 * andar
        for sd, sg in (("L", 1), ("R", -1)):
            sw = -balanco * math.sin(2 * math.pi * (fase + (0.5 if sg > 0 else 0.0)))
            alvo[sd] = ombro[sd] + Qt @ Vector((0.02 + sw, sg * 0.05, -0.33))
            if sent > 0:   # apoia a mão no assento/braço da cadeira
                alvo[sd] = alvo[sd].lerp(quad + Vector((0.05, sg * 0.17, 0.08)), sent)
        polo = {sd: Vector((-1, sg * 0.4, -0.2)) for sd, sg in (("L", 1), ("R", -1))}
        dig = sm(st.get("digitar", 0.0))
        if dig > 0:
            tk, ms = st["teclado"], st["mouse"]
            j = 0.006 * math.sin(2 * math.pi * 6.3 * t) * (1 if (int(t * 3.1) % 3) else 0.3)
            jm = Vector((0.008 * math.sin(2 * math.pi * 0.9 * t), 0.012 * math.sin(2 * math.pi * 1.3 * t + 1.0), 0))
            alvo["L"] = alvo["L"].lerp(tk + Vector((0, 0, 0.03 + max(0, j))), dig)
            alvo["R"] = alvo["R"].lerp(ms + jm + Vector((0, 0, 0.03)), dig)
            for sd, sg in (("L", 1), ("R", -1)): polo[sd] = polo[sd].lerp(Vector((-0.3, sg * 1.0, -0.6)), dig)
        fone = st.get("fone", 0.0)
        fmao = math.sin(math.pi * min(max(fone, 0), 1)) if 0 < fone < 1 else 0.0
        ac = sm(st.get("acenar", 0.0))
        if ac > 0:   # direita acena alto, pro lado da plateia
            w = math.sin(2 * math.pi * 2.6 * t)
            a = ombro["R"] + Qt @ (Vector((0.1, -0.2, 0.2)) + Vector((0.02 * w, -0.06 * w, 0.015 * abs(w))))
            alvo["R"] = alvo["R"].lerp(a, ac); polo["R"] = polo["R"].lerp(Vector((0.0, -1, -0.4)), ac)
        so = sm(st.get("soco", 0.0))
        if so > 0:   # soco no ar (o capitão) com a esquerda
            pump = 0.5 + 0.5 * math.sin(2 * math.pi * 2.2 * t)
            a = ombro["L"] + Qt @ Vector((0.09, 0.17, 0.12 + 0.13 * pump))
            alvo["L"] = alvo["L"].lerp(a, so); polo["L"] = polo["L"].lerp(Vector((0.2, 1, -0.3)), so)
        # fone: no pescoço (0) -> nas orelhas (1); as mãos levam as conchas no meio do caminho
        fu = sm(fone)
        for sd, sg in (("L", 1), ("R", -1)):
            pesc_c = ch + Qt @ Vector((0.03, sg * 0.105, 0.105))
            orel = hc + Qh @ Vector((-0.004, sg * (s.lado + 0.012 if s.lado > 0.19 else 0.19), -0.004))
            arco_meio = pesc_c.lerp(orel, 0.5) + Qh @ Vector((0.05, sg * 0.05, 0.03))
            c = (pesc_c.lerp(arco_meio, fu * 2) if fu < 0.5 else arco_meio.lerp(orel, fu * 2 - 1))
            # concha deitada no pescoço (virada pra fora e pra cima), em pé na orelha
            q0 = Qt @ Quaternion((1, 0, 0), sg * math.radians(-60)); q1 = Qh
            qc = q0.slerp(q1, fu)
            place(P["concha" + sd], c, (0.044, 0.03, 0.054), qc)
            place(P["tampa" + sd], c + qc @ Vector((0, sg * 0.024, 0)), (0.034, 0.012, 0.042), qc)
            place(P["almofada" + sd], c - qc @ Vector((0, sg * 0.02, 0)), (0.04, 0.014, 0.048), qc)
            s._concha = getattr(s, "_concha", {}); s._concha[sd] = (c, qc)
            if fmao > 0:
                alvo[sd] = alvo[sd].lerp(c + qc @ Vector((-0.01, sg * 0.05, -0.01)), fmao)
                polo[sd] = polo[sd].lerp(Vector((-0.2, sg * 1, -0.4)), fmao)
        cL, qL = s._concha["L"]; cR, qR = s._concha["R"]
        # arco (faixa fina): por cima da cabeça (fone posto) ou por trás do pescoço
        pts = []
        for i in range(7):
            u = i / 6.0
            ang = math.pi * u
            em_cima = hc + Qh @ Vector((-0.01, math.cos(ang) * (s.lado + 0.012), math.sin(ang) * (s.topo + 0.022)))
            atras = ch + Qt @ Vector((-0.06 - 0.05 * math.sin(ang), math.cos(ang) * 0.11, 0.135 + 0.01 * math.sin(ang)))
            pts.append(atras.lerp(em_cima, fu))
        pts[0] = cL.lerp(pts[0], 0.3); pts[6] = cR.lerp(pts[6], 0.3)
        for i in range(6): place_seg(P["arco%d" % i], pts[i], pts[i + 1], 0.012)
        mic0 = cL + qL @ Vector((0.02, 0.01, -0.02)); mic1 = cL + qL @ Vector((0.11, -0.03, -0.07))
        place_seg(P["mic"], mic0, mic1, 0.008); place(P["micPonta"], mic1, (0.016, 0.014, 0.014))
        for sd, sg in (("L", 1), ("R", -1)):
            cot, mao = ik(ombro[sd], alvo[sd], LB, LB, polo[sd])
            meio = ombro[sd].lerp(cot, 0.62)
            place_seg(P["braco" + sd], ombro[sd], meio, 0.047)
            place_seg(P["punhoCam" + sd], meio.lerp(ombro[sd], 0.08), meio, 0.05)
            place_seg(P["ante" + sd], meio, mao, 0.037)
            place(P["mao" + sd], mao, (0.036, 0.034, 0.034))

    def no_mundo(s, pos, yaw, esc=None):
        s.root.location = pos
        s.root.rotation_mode = "XYZ"; s.root.rotation_euler = (0, 0, yaw)
        e = s.f["alt"] if esc is None else esc
        s.root.scale = (e, e, e)

    def local(s, p):
        """Ponto do mundo -> coordenadas do boneco (pra mão ir no teclado)."""
        return s.root.matrix_world.inverted() @ p


# ------------------------------------------------------------------ Kenney
def importa_kenney(nome):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(KEN, nome + ".glb"))
    novos = [o for o in bpy.data.objects if o not in antes]
    for o in novos:
        if o.type == "MESH":
            for i, m in enumerate(o.data.materials):
                bsdf = m.node_tree.nodes.get("Principled BSDF") if m and m.use_nodes else None
                c = bsdf.inputs["Base Color"].default_value if bsdf else (0.5, 0.5, 0.5, 1)
                def g(x): return 12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055
                hexa = "#" + "".join("%02x" % int(max(0, min(1, g(c[j]))) * 255) for j in range(3))
                o.data.materials[i] = toon("k_" + m.name, hexa)
    return novos

MODELOS = {}
def modelo(nome):
    """Uma cópia de cada peça do Kenney (fica escondida; as instâncias usam a mesma malha)."""
    if nome not in MODELOS:
        todos = importa_kenney(nome)
        bpy.context.view_layer.update()
        objs = [o for o in todos if o.type == "MESH"]
        mw = {o.name: o.matrix_world.copy() for o in objs}
        for o in objs: o.data.transform(mw[o.name])
        for o in objs:
            o.parent = None; o.matrix_world = Matrix()
            o.hide_render = True; o.hide_viewport = True
        for o in todos:
            if o.type != "MESH": bpy.data.objects.remove(o)
        MODELOS[nome] = objs
    return MODELOS[nome]

def instancia(nome, pos, yaw=0.0, esc=1.0, pai=None, pecas=None, cor=None, linha=True, troca=None):
    """Instância do modelo: um vazio com as peças (malha compartilhada) dentro. cor: troca o material 'carpet' (estofado);
    troca: {pedaço do nome do material: cor} (monitor preto de palco em vez do cinza do Kenney)."""
    tr = dict(troca or {})
    if cor: tr["carpet"] = cor
    raiz = bpy.data.objects.new(nome, None); COLE.objects.link(raiz)
    raiz.location = pos; raiz.rotation_euler = (0, 0, yaw); raiz.scale = (esc, esc, esc)
    if pai: raiz.parent = pai
    filhos = {}
    for o in modelo(nome.split("#")[0]):
        if pecas and o.name.split(".")[0] not in pecas: continue
        me = o.data
        if tr:
            me = me.copy()
            for i, m in enumerate(me.materials):
                for pedaco, c in tr.items():
                    if m and pedaco in m.name and m is not CONTORNO: me.materials[i] = toon("troca" + c, c)
        n = bpy.data.objects.new(nome + "_" + o.name, me); COLE.objects.link(n); n.parent = raiz
        if linha:
            md = n.modifiers.new("contorno", "SOLIDIFY"); md.thickness = 0.012 / esc; md.offset = 1
            md.use_flip_normals = True; md.use_rim = False
            if CONTORNO.name not in [m.name for m in me.materials if m]: me.materials.append(CONTORNO)
            md.material_offset = len(me.materials) - 1
        filhos[o.name.split(".")[0]] = n
    return raiz, filhos

_TELA = None
def tela_monitor():
    """Face da tela do computerScreen do Kenney (polígonos do material escuro virados pra frente ou pra trás em Y, o
    lado de maior área): centro, meias medidas e normal, nas coordenadas do modelo."""
    global _TELA
    if _TELA: return _TELA
    o = modelo("computerScreen")[0]; me = o.data
    lados = {1: [], -1: []}
    for p in me.polygons:
        m = me.materials[p.material_index]
        if m and "metalDark" in m.name and abs(p.normal.y) > 0.9:
            lados[1 if p.normal.y > 0 else -1].append(p)
    sg = max(lados, key=lambda k_: sum(p.area for p in lados[k_]))
    vs = [me.vertices[i].co for p in lados[sg] for i in p.vertices]
    mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    _TELA = ((mn + mx) / 2, (mx - mn) / 2, Vector((0, sg, 0)))
    print("[entrada] tela do monitor", tuple(round(x, 3) for x in _TELA[0]), tuple(round(x, 3) for x in _TELA[1]), sg)
    return _TELA

def caixa_mundo(nome, mat, c, meia, bisel=0.01, linha=True):
    o = box(nome, mat, meia, bisel, None, linha); o.location = c; return o

# ------------------------------------------------------------------ texto
_FONTE = None
def fonte():
    global _FONTE
    if _FONTE is None:
        try: _FONTE = bpy.data.fonts.load(FONTE)
        except Exception: _FONTE = None
    return _FONTE

def texto(nome, corpo, mat, tam, pos=(0, 0, 0), rot=(math.pi / 2, 0, 0), alinha="CENTER", ext=0.0, pai=None):
    cu = bpy.data.curves.new(nome, "FONT"); cu.body = corpo; cu.size = tam; cu.align_x = alinha; cu.align_y = "CENTER"
    if fonte(): cu.font = fonte()
    cu.extrude = ext
    o = bpy.data.objects.new(nome, cu); COLE.objects.link(o)
    o.data.materials.append(mat); o.location = pos; o.rotation_euler = rot
    if pai: o.parent = pai
    return o

# ================================================================== FOLHA (os 10 jogadores)
def montar_folha():
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]; bg.inputs[0].default_value = srgb("#c9bfae"); bg.inputs[1].default_value = 1.0
    sol = bpy.data.objects.new("sol", bpy.data.lights.new("sol", "SUN")); COLE.objects.link(sol)
    d = Vector((-0.45, -0.75, 0.75)).normalized(); sol.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sol.data.energy = 3.4
    chao = caixa_mundo("chao", toon("chao", "#d8cdb8", escuro=0.55), (10, 0, -0.05), (18, 4, 0.05), 0, False)
    js = []
    for i, f in enumerate(FICHAS):
        j = Jogador(f); js.append(j)
        lin = 0 if i < 5 else 1; col = i % 5
        x = (col - 2) * 0.62 + lin * 20.0; y = 0.0
        j.no_mundo(Vector((x, y, 0)), math.radians(-90 + (col - 2) * 6))
        st = dict(t=0.4 * i, olhar=0.0, fone=0.0)
        if f["nick"] == "TROVAO": st.update(soco=1.0, t=0.11)
        if f["nick"] in ("FAISCA", "FALCAO"): st.update(acenar=1.0, t=0.09)
        j.pose(st)
        cor = TIMES[f["time"]]["camisa"]
        nome = NOME_VISIVEL.get(f["nick"], f["nick"])
        texto("n" + f["nick"], nome, toon("txt" + cor, cor, escuro=1.0), 0.085, (x, y - 0.28, 0.002), (0, 0, 0))
        texto("f" + f["nick"], f["funcao"].upper(), toon("txtf", "#5a5148", escuro=1.0), 0.045, (x, y - 0.37, 0.002), (0, 0, 0))
    cam =bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); COLE.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
    for lado, dx in (("vulcao", 0.0), ("rocha", 20.0)):
        tm = "VULCÃO" if lado == "vulcao" else "ROCHA"
        texto("titulo" + lado, tm, toon("tit" + lado, TIMES[tm]["camisa"], escuro=1.0), 0.2, (dx, 0.95, 1.02), ext=0.01)
        alvo = Vector((dx, 0.0, 0.38)); pos = alvo + Vector((0.0, -4.4, 1.5))
        cam.location = pos; cam.rotation_euler = (alvo - pos).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(SAIDA, "folha_%s.png" % lado)
        bpy.ops.render.render(write_still=True)
    # de perto: cada um em close (rosto), 5 + 5
    cam.data.lens = 85
    for lado, idx in (("vulcao", range(5)), ("rocha", range(5, 10))):
        a = Vector((sum(js[i].root.location.x for i in idx) / 5, js[idx[0]].root.location.y, 0.62))
        p = a + Vector((0.0, -3.1, 0.9))
        cam.location = p; cam.rotation_euler = (a - p).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(SAIDA, "rostos_%s.png" % lado)
        bpy.ops.render.render(write_still=True)

# ================================================================== PALCO + CUTSCENE
CHAIR_Y = 0.6            # centro do assento
SEAT_DX = 0.47           # distância entre cadeiras
ESC_CAD = 0.66           # Kenney chairDesk -> assento na altura do quadril sentado
DESK_TOPO = 0.37
TUNEL_Y = 3.0

def lugares(time):
    sg = -1 if time == "VULCÃO" else 1
    return [Vector((sg * (0.95 + SEAT_DX * k), CHAIR_Y, 0)) for k in range(5)]   # k=0 perto do meio

class Palco:
    def __init__(s):
        s.luzes = []; s.spots = []; s.objs = {}
        s.mundo(); s.estrutura(); s.telao(); s.tunel(); s.bancadas(); s.plateia(); s.iluminacao(); s.confete()

    def mundo(s):
        sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
        bg = sc.world.node_tree.nodes["Background"]; bg.inputs[0].default_value = srgb("#0b0d16"); bg.inputs[1].default_value = 1.0
        # neblina do ginásio (faz os fachos dos holofotes aparecerem)
        me = _cubo_base().copy(); o = novo("neblina", me); o.scale = (14, 14, 5); o.location = (0, -2, 4.6)
        m = bpy.data.materials.new("neblina"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
        vol = nt.nodes.new("ShaderNodeVolumePrincipled"); vol.inputs["Density"].default_value = 0.07
        try: vol.inputs["Anisotropy"].default_value = 0.35
        except Exception: pass
        out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(vol.outputs[0], out.inputs["Volume"])
        me.materials.append(m)
        s.neblina = vol

    def estrutura(s):
        piso = toon("piso", "#23252e", escuro=0.35)
        caixa_mundo("palco", piso, (0, 1.2, -0.18), (3.7, 2.2, 0.18), 0.02)
        # degrau de LED na frente do palco (laranja na esquerda, azul na direita)
        for i in range(24):
            x = -3.6 + (i + 0.5) * 7.2 / 24
            cor = mistura(TIMES["VULCÃO"]["luz"], TIMES["ROCHA"]["luz"], i / 23)
            caixa_mundo("led%d" % i, luz_mat("led%d" % i, cor, 2.2), (x, -1.0, -0.06), (7.2 / 48 - 0.004, 0.012, 0.03), 0, False)
        # piso da arena (mais baixo) e grade da frente
        caixa_mundo("arena", toon("arena", "#15161c", escuro=0.4), (0, -4.5, -0.42), (12, 3.5, 0.06), 0, False)
        # linhas finas no piso do palco (marcam as bancadas)
        for sg in (-1, 1):
            caixa_mundo("tapete%d" % sg, toon("tapete" + str(sg), "#2e3240", escuro=0.35), (sg * 2.0, 0.55, 0.002), (1.45, 0.62, 0.004), 0.004, False)
        # treliça (truss) em cima do palco
        metal = toon("metal", "#55585f", escuro=0.3)
        for y in (-0.6, 1.6):
            caixa_mundo("trel_x%s" % y, metal, (0, y, 3.1), (4.2, 0.05, 0.05), 0.01)
        for x in (-4.2, 4.2):
            caixa_mundo("trel_y%s" % x, metal, (x, 0.5, 3.1), (0.05, 1.15, 0.05), 0.01)
            caixa_mundo("torre%s" % x, metal, (x, 0.5, 1.5), (0.06, 0.06, 1.6), 0.01)
        # torres de LED dos lados do telão
        for sg in (-1, 1):
            s.objs["torreLed%d" % sg] = caixa_mundo("torreLed%d" % sg, luz_mat("torreLed%d" % sg, "#ffffff", 0.0), (sg * 3.35, 2.85, 1.25), (0.18, 0.05, 1.25), 0, False)
            caixa_mundo("torreMoldura%d" % sg, toon("moldura", "#101116"), (sg * 3.35, 2.92, 1.25), (0.22, 0.05, 1.3), 0.01)

    def telao(s):
        # telão gigante em cima do túnel
        moldura = toon("moldura", "#101116")
        caixa_mundo("telaoMoldura", moldura, (0, TUNEL_Y + 0.08, 1.55), (2.98, 0.06, 0.86), 0.02)
        s.telaoMat = bpy.data.materials.new("telao"); s.telaoMat.use_nodes = True
        nt = s.telaoMat.node_tree; nt.nodes.clear(); N = nt.nodes.new; L = nt.links.new
        tc = N("ShaderNodeTexCoord"); sx = N("ShaderNodeSeparateXYZ"); L(tc.outputs["Generated"], sx.inputs[0])
        ramp = N("ShaderNodeValToRGB"); L(sx.outputs[0], ramp.inputs[0])
        ramp.color_ramp.elements[0].color = srgb(TIMES["VULCÃO"]["luz"]); ramp.color_ramp.elements[1].color = srgb(TIMES["ROCHA"]["luz"])
        e = ramp.color_ramp.elements.new(0.5); e.color = srgb("#2a1a3a")
        # faixas diagonais animadas (valor "fase" mexido por quadro)
        mp = N("ShaderNodeMath"); mp.operation = "MULTIPLY_ADD"; mp.inputs[1].default_value = 9.0
        L(sx.outputs[0], mp.inputs[0])
        s.telaoFase = N("ShaderNodeValue"); s.telaoFase.outputs[0].default_value = 0.0
        ad = N("ShaderNodeMath"); ad.operation = "ADD"; L(mp.outputs[0], ad.inputs[0]); L(sx.outputs[1], ad.inputs[1])
        ad2 = N("ShaderNodeMath"); ad2.operation = "ADD"; L(ad.outputs[0], ad2.inputs[0]); L(s.telaoFase.outputs[0], ad2.inputs[1])
        sn = N("ShaderNodeMath"); sn.operation = "PINGPONG"; sn.inputs[1].default_value = 0.5; L(ad2.outputs[0], sn.inputs[0])
        lim = N("ShaderNodeMath"); lim.operation = "GREATER_THAN"; lim.inputs[1].default_value = 0.4; L(sn.outputs[0], lim.inputs[0])
        lm = N("ShaderNodeMath"); lm.operation = "MULTIPLY_ADD"; lm.inputs[1].default_value = 0.25; lm.inputs[2].default_value = 0.55; L(lim.outputs[0], lm.inputs[0])
        mul = N("ShaderNodeMixRGB"); mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1; L(ramp.outputs[0], mul.inputs[1])
        L(lm.outputs[0], mul.inputs[2])
        em = N("ShaderNodeEmission"); L(mul.outputs[0], em.inputs[0])
        s.telaoForca = em.inputs[1]; s.telaoForca.default_value = 1.0
        out = N("ShaderNodeOutputMaterial"); L(em.outputs[0], out.inputs[0])
        tela = caixa_mundo("telao", s.telaoMat, (0, TUNEL_Y + 0.01, 1.58), (2.85, 0.01, 0.78), 0, False)
        # textos do telão
        branco = luz_mat("txtTelao", "#ffffff", 1.4)
        s.txtCopa = texto("txtCopa", "COPA DA VILA", branco, 0.42, (0, TUNEL_Y - 0.02, 1.72), ext=0.004)
        s.txtFinal = texto("txtFinal", "GRANDE FINAL", luz_mat("txtFinal", "#ffd27a", 1.3), 0.17, (0, TUNEL_Y - 0.02, 1.3), ext=0.003)
        s.txtVs = texto("txtVs", "VULCÃO  x  ROCHA", branco, 0.36, (0, TUNEL_Y - 0.02, 1.62), ext=0.004)
        s.txtVs.hide_render = True

    def tunel(s):
        parede = toon("tunel", "#14151b", escuro=0.3)
        # vão de entrada embaixo do telão (largura 1,2, altura 0,74) e corredor pra trás
        caixa_mundo("tunelTeto", parede, (0, TUNEL_Y + 0.7, 0.8), (0.7, 0.7, 0.05), 0.01, False)
        for sg in (-1, 1):
            caixa_mundo("tunelParede%d" % sg, parede, (sg * 0.66, TUNEL_Y + 0.7, 0.38), (0.06, 0.7, 0.38), 0.01, False)
            caixa_mundo("tunelPorta%d" % sg, toon("porta", "#101116"), (sg * 1.75, TUNEL_Y + 0.04, 0.38), (1.1, 0.06, 0.38), 0.01)
            # filete de LED em volta do vão
            s.objs["fiotunel%d" % sg] = caixa_mundo("fioTunel%d" % sg, luz_mat("fioTunel%d" % sg, "#ffffff", 2.0), (sg * 0.61, TUNEL_Y - 0.02, 0.38), (0.012, 0.012, 0.38), 0, False)
        caixa_mundo("tunelFundo", parede, (0, TUNEL_Y + 1.45, 0.4), (0.7, 0.05, 0.45), 0, False)
        caixa_mundo("tunelChao", toon("tunelChao", "#1b1c22"), (0, TUNEL_Y + 0.7, -0.01), (0.66, 0.7, 0.012), 0, False)
        # fumaça na boca do túnel (mais densa) e a luz de trás
        me = _cubo_base().copy(); o = novo("fumacaTunel", me); o.scale = (0.62, 0.75, 0.42); o.location = (0, TUNEL_Y + 0.35, 0.4)
        m = bpy.data.materials.new("fumacaTunel"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
        vol = nt.nodes.new("ShaderNodeVolumePrincipled"); vol.inputs["Density"].default_value = 1.1
        nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 3.0
        s.fumacaW = nt.nodes.new("ShaderNodeValue")
        tc = nt.nodes.new("ShaderNodeTexCoord"); vm = nt.nodes.new("ShaderNodeVectorMath"); vm.operation = "ADD"
        nt.links.new(tc.outputs["Object"], vm.inputs[0]); cmb = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(s.fumacaW.outputs[0], cmb.inputs[2]); nt.links.new(cmb.outputs[0], vm.inputs[1]); nt.links.new(vm.outputs[0], nz.inputs["Vector"])
        mm = nt.nodes.new("ShaderNodeMath"); mm.operation = "MULTIPLY"; mm.inputs[1].default_value = 1.6
        nt.links.new(nz.outputs["Fac"], mm.inputs[0]); nt.links.new(mm.outputs[0], vol.inputs["Density"])
        s.fumacaDens = mm.inputs[1]
        out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(vol.outputs[0], out.inputs["Volume"]); me.materials.append(m)
        s.luzTunel = s.luz("luzTunel", "POINT", (0, TUNEL_Y + 1.25, 0.45), "#ffffff", 0)

    def bancadas(s):
        s.cadeiras = {}; s.teclados = {}; s.mouses = {}
        tampo = toon("tampo", "#18191f", escuro=0.35)
        for time in ("VULCÃO", "ROCHA"):
            T = TIMES[time]; sg = -1 if time == "VULCÃO" else 1
            xs = [p.x for p in lugares(time)]
            x0 = (min(xs) + max(xs)) / 2; meia = (max(xs) - min(xs)) / 2 + 0.34
            yd = CHAIR_Y - 0.31   # beirada da mesa do lado do jogador
            caixa_mundo("mesa" + time, tampo, (x0, yd - 0.12, DESK_TOPO - 0.015), (meia, 0.16, 0.015), 0.006)
            # painel da frente com a faixa de LED e o nome do time
            caixa_mundo("painel" + time, toon("painel" + time, "#101116"), (x0, yd - 0.27, DESK_TOPO / 2), (meia, 0.012, DESK_TOPO / 2 - 0.01), 0.006)
            s.objs["ledMesa" + time] = caixa_mundo("ledMesa" + time, luz_mat("ledMesa" + time, T["luz"], 0.3), (x0, yd - 0.285, DESK_TOPO - 0.05), (meia - 0.02, 0.004, 0.012), 0, False)
            s.objs["nomeMesa" + time] = texto("nomeMesa" + time, time, luz_mat("nomeMesa" + time, T["luz"], 0.3), 0.15, (x0, yd - 0.29, 0.15), ext=0.002)
            for i, p in enumerate(lugares(time)):
                # monitor virado pro jogador, teclado e mouse
                # (a face da tela do Kenney olha pra +Y do modelo: sem girar, ela fica virada pro jogador)
                base_ = Vector((p.x - 0.13, yd - 0.219, DESK_TOPO))
                instancia("computerScreen#%s%d" % (time, i), base_, 0.0, 0.66, troca={"metal": "#26282f"})
                # tela acesa do monitor: placa de emissão em cima da face da tela do Kenney (achada na malha)
                tc_, th_, tn_ = tela_monitor()
                cen = base_ + tc_ * 0.66 + tn_ * 0.003
                caixa_mundo("telaMon%s%d" % (time, i), luz_mat("telaMon" + time, mistura(T["luz"], "#9fd6ff", 0.6), 1.2),
                            cen, (th_.x * 0.66 * 0.94, 0.0015, th_.z * 0.66 * 0.9), 0, False)
                instancia("computerKeyboard#%s%d" % (time, i), Vector((p.x + 0.093 + 0.03, yd + 0.0, DESK_TOPO)), math.radians(180), 0.66,
                          troca={"metal": "#2c2e36"})
                instancia("computerMouse#%s%d" % (time, i), Vector((p.x - 0.13, yd + 0.01, DESK_TOPO)), math.radians(180), 0.9,
                          troca={"metal": TIMES[time]["camisa"]})
                s.teclados[(time, i)] = Vector((p.x + 0.04, yd - 0.035, DESK_TOPO + 0.02))
                s.mouses[(time, i)] = Vector((p.x - 0.13, yd - 0.03, DESK_TOPO + 0.02))
                # cadeira: a parte de cima (assento + encosto) gira; o pé fica
                raiz, f = instancia("chairDesk#%s%d" % (time, i), p.copy(), 0.0, ESC_CAD, cor=T["cadeira"])
                s.cadeiras[(time, i)] = (raiz, f)
            # caixa de som só na ponta de fora (a de dentro tapava a passagem do túnel)
            xx = (min(xs) - 0.5) if sg < 0 else (max(xs) + 0.5)
            instancia("speaker#%s" % time, Vector((xx - 0.07, CHAIR_Y - 0.15, 0)), 0.0, 0.9, troca={"wood": "#23242b", "metal": "#3a3c44"})

    def plateia(s):
        s.fas = []
        random.seed(7)
        cores_neutras = ["#2a2c36", "#33303a", "#2c3330", "#3a3230", "#252a36"]
        pele = ["#e2ad86", "#c68a62", "#8a5a3c", "#f0c6a2", "#5e3b28", "#b77a52"]
        for fila in range(9):
            y = -1.75 - fila * 0.48; z = -0.36 + fila * 0.16
            caixa_mundo("arquibancada%d" % fila, toon("arq", "#1a1b22", escuro=0.4), (0, y - 0.1, z - 0.06), (6.5, 0.26, 0.06), 0, False)
            n = 28
            for c in range(n):
                x = -6.0 + (c + random.uniform(0.15, 0.85)) * 12.0 / n
                lado = "VULCÃO" if x < 0 else "ROCHA"
                torcedor = random.random() < 0.55
                cor = TIMES[lado]["camisa"] if torcedor else random.choice(cores_neutras)
                corpo = ell("fa%d_%d" % (fila, c), toon("fa" + cor, cor, escuro=0.3), 12, None, 0.012)
                cabeca = ell("fac%d_%d" % (fila, c), toon("fap" + str(c % 6), pele[c % 6], escuro=0.3), 12, None, 0.012)
                cab2 = ell("fah%d_%d" % (fila, c), toon("fah", "#1a1414", escuro=0.3), 12, None, 0.0, False)
                fa = dict(x=x, y=y + random.uniform(-0.05, 0.05), z=z, corpo=corpo, cab=cabeca, cab2=cab2,
                          fase=random.random(), pula=random.random() < 0.35, alt=random.uniform(0.9, 1.08), lado=lado, bracos=None, cel=None)
                if random.random() < 0.4:   # braços pro alto
                    br = [ell("fab%d_%d_%d" % (fila, c, b), toon("fa" + cor, cor, escuro=0.3), 8, None, 0.0, False) for b in range(2)]
                    fa["bracos"] = br
                if random.random() < 0.12:   # celular com a luz
                    fa["cel"] = box("facel%d_%d" % (fila, c), luz_mat("cel", "#dfeaff", 3.0), (0.012, 0.004, 0.02), 0, None, False)
                s.fas.append(fa)
        # placas
        for i, (x, txt, lado) in enumerate([(-3.2, "VAI VULCÃO!", "VULCÃO"), (2.6, "ROCHA <3", "ROCHA"), (-1.0, "TROVÃO MITO", "VULCÃO"), (4.6, "FALCÃO", "ROCHA")]):
            fila = [2, 3, 5, 1][i]; y = -1.75 - fila * 0.48 + 0.12; z = -0.36 + fila * 0.16 + 0.48
            caixa_mundo("placa%d" % i, toon("placa", "#f2efe8", escuro=0.4), (x, y, z), (0.32, 0.01, 0.11), 0.01)
            # o texto vira pro palco (é pra lá que a placa está apontada)
            texto("placaTxt%d" % i, txt, toon("placaTxt" + lado, TIMES[lado]["camisa"], escuro=0.6), 0.09, (x, y + 0.015, z), (math.pi / 2, 0, math.pi), ext=0.0)

    def luz(s, nome, tipo, pos, cor, energia, alvo=None, tam=None):
        o = bpy.data.objects.new(nome, bpy.data.lights.new(nome, tipo)); COLE.objects.link(o)
        o.location = pos; o.data.color = srgb(cor)[:3]; o.data.energy = energia
        if alvo is not None: o.rotation_euler = (Vector(alvo) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        if tam is not None and tipo == "SPOT": o.data.spot_size = math.radians(tam); o.data.spot_blend = 0.25
        # sombra dura: penumbra sorteada por amostra vira granulado no desenho de 3 tons (rampa constante)
        try: o.data.shadow_soft_size = 0.0
        except Exception: pass
        if tipo == "AREA": o.data.use_shadow = False
        s.luzes.append(o)
        return o

    def iluminacao(s):
        # holofotes móveis na treliça (fachos na neblina)
        cores = ["#ff7a2a", "#ffffff", "#4aa8ff", "#ff7a2a", "#ffffff", "#4aa8ff", "#ff7a2a", "#4aa8ff"]
        for i in range(8):
            x = -3.6 + i * 7.2 / 7; y = -0.6 if i % 2 == 0 else 1.6
            # a luz fica embaixo da carcaça (dentro dela a própria caixa fazia sombra e o facho sumia)
            sp = s.luz("spot%d" % i, "SPOT", (x, y, 2.86), cores[i], 0, (x * 0.6, 0.8, 0), 16)
            try: sp.data.volume_factor = 6.0
            except Exception: pass
            corpo = box("holofote%d" % i, toon("holo", "#2a2b30"), (0.07, 0.07, 0.09), 0.02, None, True)
            corpo.location = (x, y, 3.0)
            s.spots.append((sp, x, y, i))
        # luz de cada bancada (acende quando o time senta) e luz de frente (rosto)
        s.luzBancada = {}
        for time in ("VULCÃO", "ROCHA"):
            sg = -1 if time == "VULCÃO" else 1
            o = s.luz("bancada" + time, "AREA", (sg * 1.9, 0.55, 1.9), "#fff4e6", 0, (sg * 1.9, 0.55, 0))
            o.data.size = 2.6; o.data.shape = "RECTANGLE"; o.data.size_y = 1.1
            try: o.data.volume_factor = 0.0
            except Exception: pass
            s.luzBancada[time] = o
            rim = s.luz("contra" + time, "SPOT", (sg * 1.9, 2.4, 1.7), TIMES[time]["luz"], 0, (sg * 1.9, 0.6, 0.5), 50)
            try: rim.data.volume_factor = 0.3
            except Exception: pass
            s.luzBancada["contra" + time] = rim
        s.frente = s.luz("frente", "AREA", (0, -3.2, 2.2), "#cfd8ff", 0, (0, 1.0, 0.4))
        s.frente.data.size = 6.0
        try: s.frente.data.volume_factor = 0.0
        except Exception: pass
        s.geral = s.luz("geral", "SUN", (0, 0, 5), "#8090c0", 0.0, (0.3, 0.6, 0))
        try: s.geral.data.volume_factor = 0.0
        except Exception: pass
        s.palcoLuz = s.luz("palcoLuz", "AREA", (0, 1.4, 3.0), "#ffffff", 0, (0, 1.4, 0))
        s.palcoLuz.data.size = 7.0
        try: s.palcoLuz.data.volume_factor = 0.0
        except Exception: pass

    def confete(s):
        random.seed(11); s.conf = []
        cores = [TIMES["VULCÃO"]["camisa"], TIMES["VULCÃO"]["faixa"], "#ffffff", TIMES["ROCHA"]["faixa"], "#ffd27a"]
        for i in range(260):
            c = cores[i % len(cores)]
            o = box("conf%d" % i, luz_mat("conf" + c, c, 1.0), (0.018, 0.012, 0.0015), 0, None, False)
            o.location = (0, 0, -20)
            s.conf.append(dict(o=o, x=random.uniform(-3.8, 3.8), y=random.uniform(-1.6, 2.2), z0=random.uniform(3.2, 5.0),
                               v=random.uniform(0.35, 0.6), f=random.uniform(0, 6.28), w=random.uniform(2, 6)))
        # faíscas (chafariz de pirotecnia na frente do palco)
        s.faisca = []
        for b in (-3.0, -1.2, 1.2, 3.0):
            for i in range(26):
                o = ell("faisca%s_%d" % (b, i), luz_mat("faisca", "#ffd27a", 6.0), 8, None, 0, False)
                o.scale = (0.012, 0.012, 0.012); o.location = (0, 0, -20)
                s.faisca.append(dict(o=o, x=b, a=random.uniform(0, 6.28), v=random.uniform(1.6, 2.4), f=random.uniform(0, 1), r=random.uniform(0.05, 0.3)))
        s.luzPiro = [s.luz("piro%s" % b, "POINT", (b, -0.85, 0.5), "#ffcf6a", 0) for b in (-3.0, -1.2, 1.2, 3.0)]


# ------------------------------------------------------------------ roteiro: quem anda por onde e quando
class Roteiro:
    """Caminho e ações de um jogador ao longo do tempo (determinístico)."""
    def __init__(s, j, time, k, t0, assento, vel=0.8):
        s.j = j; s.time = time; s.k = k; s.t0 = t0; s.assento = assento
        s.VEL = vel   # u/s (anda: 4 x passo por ciclo)
        sg = -1 if time == "VULCÃO" else 1
        yfila = CHAIR_Y + 0.34
        s.pts = [Vector((0, TUNEL_Y + 0.95, 0)), Vector((0, 1.95, 0)), Vector((sg * 0.55, yfila, 0)), Vector((assento.x, yfila, 0))]
        s.L = [0.0]
        for a, b in zip(s.pts[:-1], s.pts[1:]): s.L.append(s.L[-1] + (b - a).length)
        s.total = s.L[-1]
        s.t_chega = t0 + s.total / s.VEL + 0.35       # (+ freada)
        s.t_virar = s.t_chega; s.d_virar = 0.55
        s.t_recuar = s.t_virar + s.d_virar; s.d_recuar = 0.55
        s.t_sentar = s.t_recuar + s.d_recuar; s.d_sentar = 0.8
        s.t_girar = s.t_sentar + s.d_sentar + 0.25 + 0.15 * ((k * 7) % 3); s.d_girar = 1.05
        s.t_puxar = s.t_girar + s.d_girar; s.d_puxar = 0.45
        s.t_fone = s.t_puxar + 0.35 + 0.25 * (k % 3); s.d_fone = 1.1
        s.t_jogar = s.t_fone + s.d_fone + 0.1
        s.yaw_sentar = math.radians(90)              # de costas pra cadeira (que está virada pro túnel)
        s.yaw_final = math.radians(-90)              # de frente pra mesa / plateia
        s.giro_sg = 1 if time == "VULCÃO" else -1    # um time gira pra cada lado
        # aceno: quando passa pela frente do palco
        s.t_acena = t0 + (s.L[1] - 0.2) / s.VEL; s.d_acena = 1.7

    def dist(s, t):
        """Distância andada (acelera no começo e freia no fim)."""
        if t <= s.t0: return 0.0
        dt = t - s.t0; a = 0.35
        d = s.VEL * max(0.0, dt - a / 2) if dt > a else s.VEL * dt * dt / (2 * a)
        fim = s.total
        # freada: a última parte em ease-out
        t_lin = s.t0 + fim / s.VEL
        if t >= t_lin - 0.15:
            u = min(1.0, (t - (t_lin - 0.15)) / 0.5)
            d0 = s.VEL * max(0.0, (t_lin - 0.15 - s.t0) - a / 2)
            d = d0 + (fim - d0) * (1 - (1 - u) ** 2)
        return min(d, fim)

    def ponto(s, d):
        for i in range(len(s.pts) - 1):
            if d <= s.L[i + 1] or i == len(s.pts) - 2:
                u = (d - s.L[i]) / max(s.L[i + 1] - s.L[i], 1e-6)
                return s.pts[i].lerp(s.pts[i + 1], min(max(u, 0), 1))

    def estado(s, t, palco):
        """(posição, yaw, pose, yaw da cadeira, deslocamento da cadeira)"""
        st = dict(t=t)
        cad_yaw = math.radians(90); cad_dy = 0.0
        visivel = t >= s.t0 - 0.1
        if t < s.t_chega:
            d = s.dist(t); p = s.ponto(d)
            frente = s.ponto(min(d + 0.25, s.total)) - p
            if frente.length < 1e-4: frente = s.pts[-1] - s.pts[-2]
            yaw = math.atan2(frente.y, frente.x)
            vel = (s.dist(t + 0.02) - s.dist(t - 0.02)) / 0.04
            st["andar"] = min(1.0, vel / s.VEL); st["fase"] = d / (4 * 0.12)
            # olha pra plateia (-Y) quando dá; acena
            olhar_plat = math.atan2(-1, 0) - yaw
            olhar_plat = (olhar_plat + math.pi) % (2 * math.pi) - math.pi
            ac = sm(k(t, s.t_acena, s.t_acena + 0.3)) * (1 - sm(k(t, s.t_acena + s.d_acena - 0.35, s.t_acena + s.d_acena)))
            st["olhar"] = max(-1.1, min(1.1, olhar_plat)) * (0.35 + 0.65 * ac)
            st["torce"] = max(-0.4, min(0.4, olhar_plat)) * 0.4 * ac
            if s.j.f["funcao"] == "IGL": st["soco"] = ac
            elif s.k % 2 == 0 or s.j.f["nick"] in ("FAISCA", "FALCAO"): st["acenar"] = ac
            return visivel, p, yaw, st, cad_yaw, cad_dy
        fim = s.pts[-1]; dir_fila = math.atan2((s.pts[-1] - s.pts[-2]).y, (s.pts[-1] - s.pts[-2]).x)
        if t < s.t_recuar:   # vira de costas pra cadeira (passinhos no lugar)
            u = sm(k(t, s.t_virar, s.t_virar + s.d_virar))
            dif = (s.yaw_sentar - dir_fila + math.pi) % (2 * math.pi) - math.pi
            yaw = dir_fila + dif * u
            st["andar"] = 0.35 * math.sin(math.pi * u); st["fase"] = u * 0.9
            st["olhar"] = -0.4 * math.sin(math.pi * u)
            return visivel, fim, yaw, st, cad_yaw, cad_dy
        if t < s.t_sentar:   # dois passinhos pra trás até a cadeira
            u = sm(k(t, s.t_recuar, s.t_recuar + s.d_recuar))
            alvo = Vector((s.assento.x, CHAIR_Y + 0.12, 0))
            p = fim.lerp(alvo, u)
            st["andar"] = 0.6 * math.sin(math.pi * u); st["fase"] = -u * 0.85
            st["olhar"] = 0.5 * math.sin(math.pi * u)   # olha por cima do ombro
            return visivel, p, s.yaw_sentar, st, cad_yaw, cad_dy
        # sentado: daqui pra frente o boneco vai junto com a cadeira
        u_s = k(t, s.t_sentar, s.t_sentar + s.d_sentar)
        st["sentar"] = u_s
        u_g = sm(k(t, s.t_girar, s.t_girar + s.d_girar))
        cad_yaw = s.yaw_sentar + s.giro_sg * math.pi * u_g
        st["pes"] = math.sin(math.pi * k(t, s.t_girar - 0.1, s.t_girar + s.d_girar + 0.1))
        u_p = sm(k(t, s.t_puxar, s.t_puxar + s.d_puxar))
        cad_dy = -0.08 * u_p
        st["fone"] = k(t, s.t_fone, s.t_fone + s.d_fone)
        st["digitar"] = k(t, s.t_jogar, s.t_jogar + 0.45)
        st["pitch"] = 0.12 * sm(k(t, s.t_jogar, s.t_jogar + 0.5))
        if u_g > 0 and u_g < 1: st["olhar"] = -0.3 * s.giro_sg * math.sin(math.pi * u_g)
        piv = Vector((s.assento.x, CHAIR_Y + cad_dy, 0))
        p = piv + Vector((math.cos(cad_yaw), math.sin(cad_yaw), 0)) * 0.12
        if t >= s.t_jogar:   # pontos da mesa no espaço do boneco (sem esperar o Blender recalcular a matriz)
            R = Matrix.Rotation(-cad_yaw, 3, "Z"); e = s.j.f["alt"]
            st["teclado"] = (R @ (palco.teclados[(s.time, s.k)] - p)) / e
            st["mouse"] = (R @ (palco.mouses[(s.time, s.k)] - p)) / e
        return visivel, p, cad_yaw, st, cad_yaw, cad_dy


# ------------------------------------------------------------------ câmera (planos) e eventos
class Diretor:
    def __init__(s, cam, palco, rot):
        s.cam = cam; s.palco = palco; s.rot = rot
        A = [r for r in rot if r.time == "VULCÃO"]; B = [r for r in rot if r.time == "ROCHA"]
        s.A, s.B = A, B
        igl = next(r for r in A if r.j.f["funcao"] == "IGL"); awp = next(r for r in B if r.j.f["nick"] == "FALCAO")
        s.igl, s.awp = igl, awp
        tA = A[0].t0; tB = B[0].t0
        t_trav = tB + 3.1
        c_igl = max(t_trav + 2.6, igl.t_girar - 1.4)
        t_travB = c_igl + 3.6
        c_awp = max(t_travB + 2.2, awp.t_fone - 1.0)
        s.planos = [
            (0.0, "abertura"), (tA - 0.6, "tunelA"), (tB - 0.3, "tunelB"), (t_trav, "travelling"), (c_igl, "closeIGL"),
            (t_travB, "travellingB"), (c_awp, "closeAWP"), (c_awp + 3.6, "orbita"), (c_awp + 7.0, "final"),
        ]
        s.fim = s.planos[-1][0] + 5.4
        s.legendas = []

    def plano(s, t):
        atual = s.planos[0]
        for p in s.planos:
            if t >= p[0]: atual = p
        return atual

    def camera(s, t):
        t0, nome = s.plano(t); u = t - t0
        ruido = Vector((0.006 * math.sin(1.7 * t) + 0.004 * math.sin(3.1 * t + 1), 0.0, 0.005 * math.sin(2.3 * t + 2)))
        lente = 35; foco = None; fstop = 4.0
        if nome == "abertura":   # grua: de cima da plateia descendo pro palco
            d = (t0 + 0) and 0
            v = sm(u / 6.5)
            pos = Vector((-2.6, -7.2, 3.6)).lerp(Vector((0.4, -4.3, 1.25)), v)
            alvo = Vector((0, 2.2, 1.4)).lerp(Vector((0, 2.0, 0.9)), v)
            lente = 24
        elif nome == "tunelA":   # baixo, de frente pro túnel; eles saem da fumaça
            pos = Vector((0.5, 0.95, 0.4)) + Vector((-0.12 * u / 4, -0.25 * u / 4, 0))
            alvo = Vector((0, TUNEL_Y, 0.48)); lente = 32
        elif nome == "travelling":   # alto, da plateia: os dois times chegando nas bancadas
            v = sm(u / 3.2)
            pos = Vector((-1.2, -2.5, 1.75)).lerp(Vector((0.9, -2.3, 1.55)), v); alvo = Vector((-0.3, 1.0, 0.25)).lerp(Vector((0.3, 0.9, 0.25)), v)
            lente = 30
        elif nome == "tunelB":   # contra-plongée do lado direito, ROCHA sai na luz azul
            pos = Vector((1.9, 1.2, 0.18)).lerp(Vector((1.6, 0.6, 0.3)), sm(u / 5))
            alvo = Vector((0.35, 2.2, 0.55)).lerp(Vector((1.2, CHAIR_Y + 0.4, 0.45)), sm(u / 5)); lente = 28
        elif nome == "travellingB":   # do alto, por trás da bancada do ROCHA: eles sentam e giram as cadeiras
            v = sm(u / 3.0)
            pos = Vector((3.2, 2.1, 1.5)).lerp(Vector((2.9, 1.8, 1.35)), v); alvo = Vector((1.8, 0.45, 0.32)); lente = 30
        elif nome == "closeIGL":   # o capitão senta e gira a cadeira pra câmera
            r = s.igl; c = Vector((r.assento.x, CHAIR_Y, 0))
            pos = c + Vector((0.55, -1.25, 0.98)).lerp(Vector((0.45, -1.1, 0.92)), sm(u / 3.6)); alvo = c + Vector((0, 0.02, 0.55))
            lente = 42; foco = (alvo - pos).length; fstop = 2.8
        elif nome == "closeAWP":   # o AWP do ROCHA põe o fone
            r = s.awp; c = Vector((r.assento.x, CHAIR_Y - 0.08, 0))
            pos = c + Vector((-0.6, -1.15, 1.0)).lerp(Vector((-0.5, -1.0, 0.95)), sm(u / 3.6)); alvo = c + Vector((0, 0, 0.66)); lente = 45
            foco = (alvo - pos).length; fstop = 2.8
        elif nome == "orbita":   # gira por trás da bancada do VULCÃO (telas acesas)
            c = Vector((-1.9, CHAIR_Y, 0.45)); a = math.radians(115) - u * math.radians(16)
            pos = c + Vector((math.cos(a) * 1.4, math.sin(a) * 1.1, 0.95)); alvo = c + Vector((0, -0.2, 0.05)); lente = 27
        else:   # final: abre pro palco inteiro, telão com VULCÃO x ROCHA, confete
            v = sm(u / 5)
            pos = Vector((0.0, -2.2, 0.9)).lerp(Vector((0.0, -5.6, 2.1)), v); alvo = Vector((0, 1.3, 0.7)).lerp(Vector((0, 1.6, 1.05)), v)
            lente = 26
        pos = pos + ruido
        s.cam.location = pos; s.cam.rotation_euler = (alvo - pos).to_track_quat("-Z", "Y").to_euler()
        s.cam.data.lens = lente
        s.cam.data.dof.use_dof = foco is not None
        if foco is not None: s.cam.data.dof.focus_distance = foco; s.cam.data.dof.aperture_fstop = fstop
        return nome, u


def montar_cutscene():
    palco = Palco()
    js = {f["nick"]: Jogador(f) for f in FICHAS}
    rot = []
    # ordem de saída do túnel: o 1º vai pra cadeira mais longe (ninguém passa por cima de ninguém)
    ordemA = ["VULCAO", "FAISCA", "SOMBRA", "MIRA", "TROVAO"]       # o capitão fecha a fila
    ordemB = ["ROCHA", "ESCUDO", "FALCAO", "ANCORA", "RADAR"]
    T_A, ESP = 4.4, 0.8
    VELS = [0.82, 0.78, 0.8, 0.77, 0.81]
    def fila(time, ordem, t0):
        lug = lugares(time)
        for n, nick in enumerate(ordem):
            kk = 4 - n
            rot.append(Roteiro(js[nick], time, kk, t0 + n * ESP + 0.12 * ((n * 5) % 3), lug[kk], VELS[n]))
    T_B = T_A + 3.9   # o ROCHA sai logo depois do último do VULCÃO
    fila("VULCÃO", ordemA, T_A)
    fila("ROCHA", ordemB, T_B)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); COLE.objects.link(cam); sc.camera = cam
    cam.data.clip_start = 0.05; cam.data.clip_end = 60
    dire = Diretor(cam, palco, rot)
    # legendas (nome + função): entram por cima do vídeo na montagem (dentro da cena o desfoque do close apagava)
    import json
    leg = []
    for nome_p, r in (("closeIGL", dire.igl), ("closeAWP", dire.awp)):
        f = r.j.f; T = TIMES[f["time"]]
        t0p = next(a for a, b in dire.planos if b == nome_p)
        leg.append(dict(de=t0p + 0.5, ate=t0p + 3.3, nome=NOME_VISIVEL.get(f["nick"], f["nick"]),
                        sub="%s  ·  %s" % (f["funcao"].upper(), f["time"]), cor=T["camisa"], faixa=T["faixa"]))
    with open(os.path.join(SAIDA, "legendas.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(legendas=leg, fim=dire.fim), fh, ensure_ascii=False, indent=1)
    tA = rot[0].t0
    def atualizar(fr):
        t = fr / FPS
        nome, u = dire.camera(t)
        # jogadores e cadeiras
        for r in rot:
            vis, p, yaw, st, cyaw, cdy = r.estado(t, palco)
            r.j.root.hide_render = not vis
            for o in r.j.root.children: o.hide_render = not vis
            r.j.no_mundo(p, yaw)
            r.j.pose(st)
        _ajusta_cadeiras(palco, rot, t)
        # telão
        palco.telaoFase.outputs[0].default_value = t * 0.35
        vs = t >= dire.planos[-1][0]
        palco.txtVs.hide_render = not vs; palco.txtCopa.hide_render = vs; palco.txtFinal.hide_render = vs
        # fumaça do túnel mexendo
        palco.fumacaW.outputs[0].default_value = t * 0.25
        # luz do túnel: laranja quando o VULCÃO sai, azul com o ROCHA; pisca na saída
        tB = rot[5].t0
        if t < tA - 1.0: cor, e = "#ffffff", 40
        elif t < tB - 0.8: cor, e = TIMES["VULCÃO"]["luz"], 220 + 160 * max(0, math.sin(2 * math.pi * 3 * t)) * (1 - k(t, tA, tA + 1.5))
        elif t < tB + 6: cor, e = TIMES["ROCHA"]["luz"], 220 + 160 * max(0, math.sin(2 * math.pi * 3 * t)) * (1 - k(t, tB, tB + 1.5))
        else: cor, e = "#ffffff", 60
        palco.luzTunel.data.color = srgb(cor)[:3]; palco.luzTunel.data.energy = e
        for sg in (-1, 1):
            m = palco.objs["fiotunel%d" % sg].data.materials[0]
            m.node_tree.nodes["Emission"].inputs[0].default_value = srgb(cor)
        # holofotes: varrem, com a cor do time que está entrando
        for sp, x, y, i in palco.spots:
            a = t * (0.55 + 0.07 * i) + i
            alvo = Vector((x * 0.5 + 1.4 * math.sin(a), 0.6 + 1.3 * math.cos(a * 0.8), 0))
            if dire.plano(t)[1] == "final": alvo = Vector((x * 0.25, -2.0 + 0.8 * math.sin(a), 0.0))
            sp.rotation_euler = (alvo - Vector((x, y, 2.86))).to_track_quat("-Z", "Y").to_euler()
            base = 1400 if t > 1.0 else 1400 * t
            if tA - 0.6 < t < tB - 0.6 and i % 3 != 1: sp.data.color = srgb(TIMES["VULCÃO"]["luz"])[:3]
            elif tB - 0.6 <= t < tB + 5 and i % 3 != 1: sp.data.color = srgb(TIMES["ROCHA"]["luz"])[:3]
            else: sp.data.color = srgb(["#ff7a2a", "#ffffff", "#4aa8ff"][i % 3])[:3]
            sp.data.energy = base
        # luzes das bancadas acendem quando o último do time senta
        for time, grupo in (("VULCÃO", dire.A), ("ROCHA", dire.B)):
            ts = min(r.t_sentar for r in grupo); tj = max(r.t_jogar for r in grupo)
            palco.luzBancada[time].data.energy = 25 + 110 * sm(k(t, ts, ts + 2.0))
            palco.luzBancada["contra" + time].data.energy = 300 * sm(k(t, ts - 1, ts + 1))
            m = palco.objs["ledMesa" + time].data.materials[0]
            m.node_tree.nodes["Emission"].inputs[1].default_value = 0.3 + 2.4 * sm(k(t, tj - 1.0, tj))
            m = palco.objs["nomeMesa" + time].data.materials[0]
            m.node_tree.nodes["Emission"].inputs[1].default_value = 0.4 + 1.8 * sm(k(t, tj - 1.0, tj))
        palco.frente.data.energy = 140 + 160 * sm(k(t, dire.planos[-1][0], dire.planos[-1][0] + 1.0))
        palco.palcoLuz.data.energy = 60
        for sg in (-1, 1):
            m = palco.objs["torreLed%d" % sg].data.materials[0]
            n = m.node_tree.nodes["Emission"]
            n.inputs[0].default_value = srgb(TIMES["VULCÃO" if sg < 0 else "ROCHA"]["luz"])
            n.inputs[1].default_value = 0.8 + 0.6 * max(0, math.sin(2 * math.pi * 0.8 * t + sg))
        # plateia pulando
        for fa in palco.fas:
            anima = 1.0 if (fa["lado"] == "VULCÃO" and tA - 0.5 < t < tA + 5) or (fa["lado"] == "ROCHA" and rot[5].t0 - 0.5 < t < rot[5].t0 + 5) or t > dire.planos[-1][0] else 0.35
            pulo = abs(math.sin(2 * math.pi * (1.6 * t + fa["fase"]))) * (0.06 if fa["pula"] else 0.015) * anima
            h = fa["alt"]; z = fa["z"] + pulo
            place(fa["corpo"], Vector((fa["x"], fa["y"], z + 0.16 * h)), (0.11, 0.1, 0.16 * h))
            place(fa["cab"], Vector((fa["x"], fa["y"], z + 0.42 * h)), (0.11, 0.11, 0.105))
            place(fa["cab2"], Vector((fa["x"] + 0.01, fa["y"] + 0.02, z + 0.47 * h)), (0.1, 0.1, 0.07))
            if fa["bracos"]:
                for b, sg in zip(fa["bracos"], (-1, 1)):
                    oscila = 0.05 * math.sin(2 * math.pi * (1.3 * t + fa["fase"]) + sg)
                    place_seg(b, Vector((fa["x"] + sg * 0.1, fa["y"], z + 0.26 * h)), Vector((fa["x"] + sg * (0.14 + oscila), fa["y"] - 0.02, z + 0.6 * h)), 0.03)
            if fa["cel"]:
                place(fa["cel"], Vector((fa["x"] + 0.06, fa["y"] + 0.12, z + 0.62 * h)), (0.014, 0.004, 0.024))
        # confete e faíscas no final
        tf = dire.planos[-1][0] + 0.4
        for c in palco.conf:
            if t < tf: c["o"].location = (0, 0, -20); continue
            dt = t - tf
            z = c["z0"] - c["v"] * dt
            c["o"].location = (c["x"] + 0.1 * math.sin(c["w"] * dt * 0.5 + c["f"]), c["y"], z)
            c["o"].rotation_euler = (c["w"] * dt + c["f"], c["f"] + dt * 2, 0)
        for fz in palco.faisca:
            dt = (t - tf + fz["f"] * 0.9)
            if t < tf or dt < 0: fz["o"].location = (0, 0, -20); continue
            dt = dt % 0.9
            vx = math.cos(fz["a"]) * fz["r"]; vy = math.sin(fz["a"]) * fz["r"] * 0.5
            fz["o"].location = (fz["x"] + vx * dt, -0.85 + vy * dt, -0.02 + fz["v"] * dt - 4.9 * dt * dt * 0.5)
        for L_ in palco.luzPiro:
            L_.data.energy = 0 if t < tf else 60 * (0.7 + 0.3 * math.sin(40 * t + L_.location.x))
        return t
    global CAD_YAW0
    CAD_YAW0 = math.radians(90)
    # orienta as cadeiras: no começo estão viradas pro túnel (+Y)
    _orienta_cadeiras(palco)
    return palco, rot, dire, atualizar

CAD_YAW0 = 0.0
TOPO_CADEIRA = "chair"
TEMPOS = {}
def _orienta_cadeiras(palco):
    """Descobre pra onde o chairDesk do Kenney olha (encosto = vértices altos; eixo = centro das rodinhas) e gira a
    instância inteira pra que o assento olhe pra +Y; o topo depois gira por cima disso."""
    objs = modelo("chairDesk")
    vs = [v.co for o in objs for v in o.data.vertices]
    baixo = [v for v in vs if v.z < 0.12]; alto = [v for v in vs if v.z > 0.45]
    c = sum(baixo, Vector()) / max(len(baixo), 1); ca = sum(alto, Vector()) / max(len(alto), 1)
    tras = Vector((ca.x - c.x, ca.y - c.y, 0)).normalized()
    frente_yaw = math.atan2(-tras.y, -tras.x)
    global CHAIR_C, TOPO_CADEIRA
    TOPO_CADEIRA = max(objs, key=lambda o: max(v.co.z for v in o.data.vertices)).name.split(".")[0]
    assento = [v.co.z for o in objs if o.name.split(".")[0] == TOPO_CADEIRA for v in o.data.vertices]
    print("[entrada] chairDesk frente local yaw %.1f°, eixo %s, topo '%s' z %.3f..%.3f, peças %s" % (
        math.degrees(frente_yaw), tuple(round(x, 3) for x in c), TOPO_CADEIRA, min(assento), max(assento), [o.name for o in objs]))
    CHAIR_C = Vector((c.x, c.y, 0))
    for (time, i), (raiz, f) in palco.cadeiras.items():
        # gira a instância em volta do centro do assento: frente -> +Y
        rz = math.radians(90) - frente_yaw
        raiz.rotation_euler = (0, 0, rz)
        raiz["rz"] = rz

def render_quadros(de, ate, pct=100, passo=1, sufixo=""):
    palco, rot, dire, atualizar = montar_cutscene()
    sc.render.resolution_percentage = pct
    for fr in range(de, ate + 1, passo):
        alvo = os.path.join(QUADROS, "q%s_%04d.png" % (sufixo, fr))
        if os.path.exists(alvo) and not sufixo: continue
        sc.frame_set(fr)
        _ajusta_cadeiras(palco, rot, fr / FPS)
        atualizar(fr)
        _ajusta_cadeiras(palco, rot, fr / FPS)
        sc.render.filepath = alvo
        import time as _t; t0 = _t.time()
        bpy.ops.render.render(write_still=True)
        print("[entrada] quadro %d %.1fs" % (fr, _t.time() - t0), flush=True)

def _ajusta_cadeiras(palco, rot, t):
    """Cadeira: a instância fica parada (só anda pra mesa); o topo gira em volta do centro do assento."""
    for r in rot:
        vis, p, yaw, st, cyaw, cdy = r.estado(t, palco)
        raiz, f = palco.cadeiras[(r.time, r.k)]
        rz = raiz.get("rz", 0.0)
        raiz.rotation_euler = (0, 0, rz)
        # centro do assento no mundo = assento; a instância é girada em volta do centro do assento
        c = CHAIR_C * ESC_CAD
        off = Matrix.Rotation(rz, 3, "Z") @ c
        raiz.location = Vector((r.assento.x, CHAIR_Y + cdy, 0)) - off
        topo = f.get(TOPO_CADEIRA)
        if topo is not None:
            R = Matrix.Rotation(cyaw - math.radians(90), 4, "Z")
            cc = Vector((CHAIR_C.x, CHAIR_C.y, 0))
            topo.matrix_parent_inverse = Matrix()
            topo.matrix_basis = Matrix.Translation(cc) @ R @ Matrix.Translation(-cc)

CHAIR_C = Vector((0, 0, 0))

def montar_video():
    """Junta os quadros num mp4 (sequenciador do Blender)."""
    import glob
    qs = sorted(glob.glob(os.path.join(QUADROS, "q_*.png")))
    if not qs: print("sem quadros"); return
    import json
    s2 = bpy.data.scenes.new("video")
    s2.render.fps = FPS
    # o tamanho do vídeo é o dos quadros (o 1º quadro manda)
    img = bpy.data.images.load(qs[0]); W, H = img.size; bpy.data.images.remove(img)
    W, H = W // 2 * 2, H // 2 * 2  # H264 exige lados pares
    s2.render.resolution_x, s2.render.resolution_y = W, H
    s2.render.resolution_percentage = 100
    ed = s2.sequence_editor_create()
    cole = ed.strips if hasattr(ed, "strips") else ed.sequences  # coleção vazia é falsa
    st = cole.new_image("q", qs[0], 1, 1)
    for q in qs[1:]: st.elements.append(os.path.basename(q))
    s2.frame_start = 1; s2.frame_end = len(qs)
    # legendas (nome do jogador numa caixa da cor do time + função embaixo), com fade
    try:
        dados = json.load(open(os.path.join(SAIDA, "legendas.json"), encoding="utf-8"))
    except Exception as e:
        dados = {"legendas": []}; print("sem legendas", e)
    def faixa_txt(nome, corpo, canal, f0, f1, tam, loc, cor, caixa=None):
        try: s = cole.new_effect(nome, "TEXT", canal, f0, length=f1 - f0)
        except TypeError: s = cole.new_effect(nome, "TEXT", canal, frame_start=f0, frame_end=f1)
        s.text = corpo; s.font_size = tam * H / 720; s.location = loc; s.color = cor
        for a, v in (("anchor_x", "LEFT"), ("anchor_y", "BOTTOM"), ("align_x", "LEFT"), ("use_bold", True)):
            try: setattr(s, a, v)
            except Exception: pass
        if fonte(): s.font = fonte()
        if caixa:
            s.use_box = True; s.box_color = caixa
            try: s.box_margin = 0.012
            except Exception: pass
        try: s.blend_type = "ALPHA_OVER"
        except Exception: pass
        for fr_, a_ in ((f0, 0.0), (f0 + 7, 1.0), (f1 - 7, 1.0), (f1, 0.0)):
            s.blend_alpha = a_; s.keyframe_insert("blend_alpha", frame=fr_)
        return s
    for i, L_ in enumerate(dados["legendas"]):
        f0 = int(L_["de"] * FPS) + 1; f1 = int(L_["ate"] * FPS) + 1
        if f0 > len(qs): continue
        faixa_txt("nome%d" % i, " %s " % L_["nome"], 2, f0, f1, 46, (0.055, 0.15), (1, 1, 1, 1), gama(L_["cor"]))
        faixa_txt("sub%d" % i, L_["sub"], 3, f0, f1, 22, (0.058, 0.1), gama(L_["faixa"]))
    if hasattr(s2.render.image_settings, "media_type"): s2.render.image_settings.media_type = "VIDEO"
    s2.render.image_settings.file_format = "FFMPEG"
    s2.render.ffmpeg.format = "MPEG4"; s2.render.ffmpeg.codec = "H264"
    s2.render.ffmpeg.constant_rate_factor = "HIGH"
    s2.render.filepath = os.path.join(SAIDA, "entrada.mp4")
    bpy.ops.render.render(animation=True, scene=s2.name)

if MODO == "folha":
    montar_folha()
elif MODO == "quadros":
    de = int(ARGV[1]); ate = int(ARGV[2]); pct = int(ARGV[3]) if len(ARGV) > 3 else 100; passo = int(ARGV[4]) if len(ARGV) > 4 else 1
    suf = ARGV[5] if len(ARGV) > 5 else ""
    render_quadros(de, ate, pct, passo, suf)
elif MODO == "video":
    montar_video()
elif MODO == "blend":
    palco, rot, dire, atualizar = montar_cutscene()
    sc.frame_start = 0; sc.frame_end = int(dire.fim * FPS)
    atualizar(0)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SAIDA, "entrada.blend"))
elif MODO == "teste":   # teste "2,5.5,9" [PCT]: alguns instantes em Logs/entrada/teste_<s>.png
    palco, rot, dire, atualizar = montar_cutscene()
    sc.render.resolution_percentage = int(ARGV[2]) if len(ARGV) > 2 else 50
    print("[entrada] planos", [(round(a, 2), b) for a, b in dire.planos], "fim", round(dire.fim, 2))
    for sx in ARGV[1].split(","):
        fr = int(round(float(sx) * FPS)); sc.frame_set(fr); atualizar(fr)
        sc.render.filepath = os.path.join(SAIDA, "teste_%05.1f.png" % float(sx))
        bpy.ops.render.render(write_still=True)
elif MODO == "duracao":
    palco, rot, dire, atualizar = montar_cutscene()
    print("[entrada] planos", [(round(a, 2), b) for a, b in dire.planos], "fim", round(dire.fim, 2), "quadros", int(dire.fim * FPS))
    for r in rot: print("[entrada]", r.j.f["nick"], "sai", round(r.t0, 2), "senta", round(r.t_sentar, 2), "joga", round(r.t_jogar, 2))
