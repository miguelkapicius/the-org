# Efeitos das granadas em sprite, no mesmo estilo dos bonecos (contorno + 3 tons; fogo e clarão chapados, brilhando)
# e na mesma escala do jogo (PX_PER_BU dos agentes):
#   smoke: nuvem de bolotas que cresce da lata, segura e some; molotov: começo (espalha) + fogo em loop na mancha
#   queimada; flash: estouro de luz com raios; he: bola de fogo que vira fumaça escura, com cacos e mancha no chão.
#   blender -b --factory-startup --python efeitos.py -- <saida>
import bpy, sys, os, math, random
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUTE = ARGS[0] if ARGS else r"D:\unity\CSManager\Logs\sprites"
sys.argv = [sys.argv[0], "--", OUTE, "ct"]
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "agentes.py"), encoding="utf-8").read()
exec(compile(src.split("class Agent:")[0], "agentes_base", "exec"))
from mathutils import Vector, Quaternion

def sm(x): x = min(max(x, 0.0), 1.0); return x * x * (3 - 2 * x)
def k(t, a, b): return min(max((t - a) / (b - a), 0.0), 1.0)

def glow_ell(name, col):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=1)
    o = bpy.context.active_object; o.name = name
    for p in o.data.polygons: p.use_smooth = True
    o.data.materials.append(glow(name, col)); return o

def clear():
    for o in list(sc.objects):
        if o.type in ("MESH", "EMPTY"): bpy.data.objects.remove(o)

def frame(res, foco=Vector((0, 0, 0.35))):
    sc.render.resolution_x = sc.render.resolution_y = res; cam.data.ortho_scale = res / PX_PER_BU
    cam.location = foco + dcam * 10

linhas = []
def render_seq(nome, n, fn, loop=False):
    fs = []
    for i in range(n):
        t = i / n if loop else i / (n - 1)
        fn(t); bpy.context.view_layer.update()
        p = os.path.join(OUTE, f"efeito_{nome}_{i:02d}.png"); sc.render.filepath = p
        bpy.ops.render.render(write_still=True); fs.append(p)
    linhas.append((nome, fs))

# ------------------------------------------------------------------ smoke
clear(); frame(512, Vector((0, 0, 0.4)))
rnd = random.Random(3); R = 1.5
cinza = toon("fumaca", "#dcdcd6", mottle=True)
puffs = []
for i in range(46):
    a = rnd.uniform(0, 2 * math.pi); r = R * math.sqrt(rnd.random())
    z = 0.15 + 0.85 * (1 - (r / R) ** 2) * rnd.uniform(0.3, 1.0)
    rad = rnd.uniform(0.28, 0.45)
    o = ell("puff%d" % i, cinza); o["th"] = 0.012
    puffs.append((o, Vector((r * math.cos(a), r * math.sin(a), z)), rad, (r / R) * 0.32 + rnd.uniform(0, 0.06), rnd.uniform(0, 6.28)))
lata = ell("lata", toon("lata", "#7a7d80"))
def smoke(t):
    place(lata, Vector((0.05, 0, 0.03)), (0.05, 0.03, 0.03), Quaternion((0, 1, 0), math.pi / 2))
    for o, p, rad, dl, ph in puffs:
        g = sm(k(t, dl, dl + 0.22)) * (1 - sm(k(t, 0.82, 1.0)))
        if g < 0.02: hide(o); continue
        pos = Vector((0, 0, 0.05)).lerp(p, sm(k(t, dl, dl + 0.18))) + Vector((0, 0, 0.03 * math.sin(6.28 * t + ph)))
        place(o, pos, (rad * g, rad * g, rad * g * 0.85))
render_seq("smoke", 16, smoke)

# ------------------------------------------------------------------ molotov
clear(); frame(448, Vector((0, 0, 0.2)))
rnd = random.Random(7)
# mancha queimada irregular (várias elipses) e chamas em gota: base vermelho-laranja, meio laranja, miolo amarelo
queim = toon("queimado", "#4a3428")
manchas = []
for i in range(7):
    a = i * 2 * math.pi / 7 + rnd.uniform(-0.3, 0.3)
    o = ell("mancha%d" % i, queim); o["th"] = 0.003
    manchas.append((o, Vector((0.45 * math.cos(a), 0.45 * math.sin(a), 0.003 + i * 0.0004)), rnd.uniform(0.55, 0.75)))
chamas = []
for i in range(40):
    a = rnd.uniform(0, 2 * math.pi); r = 1.0 * rnd.random() ** 0.7
    fora = glow_ell("chama%d" % i, "#ff5a1a"); meio = glow_ell("meio%d" % i, "#ff9a2a"); dentro = glow_ell("miolo%d" % i, "#ffe26a")
    chamas.append((fora, meio, dentro, Vector((r * math.cos(a), r * math.sin(a), 0)), rnd.uniform(0.22, 0.42) * (1.15 - 0.5 * r), rnd.uniform(0, 6.28), r))
brasas = [glow_ell("brasa%d" % i, "#ffb040") for i in range(10)]
def molotov(t, inicio):
    spread = sm(k(t, 0, 1)) if inicio else 1.0
    for o, c, rr in manchas:
        place(o, c * spread, (rr * spread + 0.03, rr * 0.8 * spread + 0.03, 0.005))
    for fora, meio, dentro, p, h0, ph, r in chamas:
        if r > 1.0 * spread + 0.05: hide(fora); hide(meio); hide(dentro); continue
        h = h0 * (0.6 + 0.4 * math.sin(2 * math.pi * 2 * t + ph)) * (0.6 + 0.4 * spread)
        sway = Vector((0.03 * math.sin(2 * math.pi * 2 * t + ph * 1.3), 0, 0))
        place(fora, p + Vector((0, 0, h * 0.7)), (0.1, 0.1, h * 0.8))
        place_seg(meio, p + Vector((0.02, 0.02, h * 0.3)), p + sway + Vector((0.02, 0.02, h * 1.35)), 0.06)
        place(dentro, p + Vector((0.035, 0.035, h * 0.45)), (0.04, 0.04, h * 0.45))
    for i, b in enumerate(brasas):
        u = (t * 2 + i / 10) % 1.0; a = i * 2.4
        place(b, Vector((0.6 * math.cos(a) * spread, 0.6 * math.sin(a) * spread, 0.1 + 0.6 * u)), (0.012, 0.012, 0.012))
render_seq("molotov_inicio", 6, lambda t: molotov(t, True))
render_seq("molotov_loop", 8, lambda t: molotov(t, False), loop=True)

# ------------------------------------------------------------------ flash
clear(); frame(320, Vector((0, 0, 0.6)))
nucleo = glow_ell("nucleo", "#ffffff"); halo = glow_ell("halo", "#fff3b0")
raios = [glow_ell("raio%d" % i, "#fffbe0") for i in range(12)]
def flash(t):
    g = sm(k(t, 0, 0.25)) * (1 - sm(k(t, 0.45, 1.0)))
    c = Vector((0, 0, 0.6))
    place(nucleo, c, (0.3 * g + 0.01,) * 3); place(halo, c + Vector((-0.01, 0.01, -0.01)), (0.45 * g + 0.01,) * 3)
    for i, o in enumerate(raios):
        a = i * 2 * math.pi / 12; el_ = 0.5 * math.sin(i * 1.7)
        d = Vector((math.cos(a) * math.cos(el_), math.sin(a) * math.cos(el_), math.sin(el_)))
        L = (0.4 + 0.5 * (i % 3) / 2) * g
        place_seg(o, c + d * 0.2 * g, c + d * (0.2 * g + L), 0.02 * g + 0.002)
render_seq("flash", 7, flash)

# ------------------------------------------------------------------ HE
clear(); frame(448, Vector((0, 0, 0.5)))
rnd = random.Random(11)
fogo = [(glow_ell("fogo%d" % i, ("#ff5a1a", "#ff9a2a", "#ffe26a")[i % 3]),
         Vector((rnd.uniform(-0.55, 0.55), rnd.uniform(-0.55, 0.55), rnd.uniform(0.1, 0.8))), rnd.uniform(0.26, 0.44)) for i in range(20)]
escuro = toon("fumaca_escura", "#4a4642", mottle=True)
fum = []
for i in range(14):
    o = ell("fum%d" % i, escuro); o["th"] = 0.012
    fum.append((o, Vector((rnd.uniform(-0.45, 0.45), rnd.uniform(-0.45, 0.45), rnd.uniform(0.2, 0.7))), rnd.uniform(0.2, 0.34)))
cacos = [(ell("caco%d" % i, toon("caco%d" % i, "#3a3330")), Vector((math.cos(i * 0.9), math.sin(i * 0.9), 1.2 + 0.4 * (i % 3))).normalized())
         for i in range(10)]
marca = ell("marca", toon("marca", "#4a3428")); marca["th"] = 0.003
def he(t):
    place(marca, Vector((0, 0, 0.004)), (0.45 * sm(k(t, 0, 0.15)) + 0.01, 0.4 * sm(k(t, 0, 0.15)) + 0.01, 0.005))
    gf = sm(k(t, 0, 0.12)) * (1 - sm(k(t, 0.25, 0.45)))
    for o, p, r in fogo:
        if gf < 0.02: hide(o); continue
        place(o, p * (0.5 + 0.7 * sm(k(t, 0, 0.3))), (r * gf,) * 3)
    gs = sm(k(t, 0.18, 0.4)) * (1 - sm(k(t, 0.75, 1.0)))
    for o, p, r in fum:
        if gs < 0.02: hide(o); continue
        place(o, p + Vector((0, 0, 0.5 * k(t, 0.2, 1.0))), (r * (0.6 + 0.6 * k(t, 0.2, 1.0)) * gs,) * 3)
    for o, d in cacos:
        u = k(t, 0.02, 0.6)
        if u <= 0 or u >= 1: hide(o); continue
        p = Vector((d.x * 1.2 * u, d.y * 1.2 * u, 0.1 + d.z * 0.9 * u - 1.6 * u * u))
        place(o, p, (0.025, 0.018, 0.015), Quaternion((1, 0, 0), u * 15))
render_seq("he", 10, he)

with open(os.path.join(OUTE, "manifest_efeitos.txt"), "w", encoding="utf-8") as fh:
    for nome, fs in linhas: fh.write(nome + "|" + "|".join(fs) + "\n")
print("EFEITOS OK")
