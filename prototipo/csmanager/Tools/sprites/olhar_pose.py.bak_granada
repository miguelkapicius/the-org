# Folha de conferência de poses (sem Unity): monta o boneco do agentes.py e renderiza cada pose pedida de 3 ângulos
# (frente, lado direito e a câmera do jogo: isométrica, elevação 50°), lado a lado, com o nome e os problemas que o
# validador acharia ali. Pra olhar rápido o que o validar_anims.py acusou (ou o que o usuário reclamou).
#   blender -b --factory-startup --python olhar_pose.py -- <saida.png> <ct|tr> "anim:t:arma" "anim:t:arma" ...
#   ex.: ... -- D:/tmp/awp.png ct "awp_mirando:0:AWP" "cego:0.3:M4A4" "granada:0.5:M4A4:molotov"
import bpy, sys, os, math
from mathutils import Vector, Euler

ARGV = sys.argv[sys.argv.index("--") + 1:]
SAIDA, TEAM, PEDIDOS = ARGV[0], ARGV[1], ARGV[2:]
AQUI = os.path.dirname(os.path.abspath(__file__))
g = {"__name__": "agentes_lib", "__file__": os.path.join(AQUI, "agentes.py")}
argv0 = sys.argv
sys.argv = [argv0[0], "--", os.path.join(os.path.dirname(SAIDA), "_lixo"), TEAM, "nenhum"]
exec(compile(open(os.path.join(AQUI, "agentes.py"), encoding="utf-8").read(), "agentes.py", "exec"), g)
sys.argv = argv0
A = g["A"]; WEAP = g["WEAP"]
A.root.rotation_euler = (0, 0, 0)
for w in WEAP: A.gun(w)
sc = bpy.context.scene

# cor chapada de cada material (workbench): a mesma conta do exportar3d (nó RGB / emissão / textura = cinza)
for m in bpy.data.materials:
    if not m.node_tree: continue
    rgb = next((n for n in m.node_tree.nodes if n.type == "RGB"), None)
    em = next((n for n in m.node_tree.nodes if n.type == "EMISSION"), None)
    tex = next((n for n in m.node_tree.nodes if n.type == "TEX_IMAGE"), None)
    if rgb is not None: c = rgb.outputs[0].default_value
    elif em is not None: c = em.inputs[0].default_value
    else: c = (0.35, 0.35, 0.37, 1) if tex else (0.8, 0.8, 0.8, 1)
    m.diffuse_color = (c[0], c[1], c[2], 1)
for o in sc.objects:   # sem o contorno (casca invertida) no workbench
    for md in o.modifiers:
        if md.type == "SOLIDIFY": md.show_viewport = False; md.show_render = False

sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "STUDIO"; sh.color_type = "MATERIAL"; sh.show_shadows = True; sh.show_cavity = True
sh.cavity_type = "WORLD"; sh.show_object_outline = True
sc.render.resolution_x = sc.render.resolution_y = 360
sc.render.film_transparent = False
sc.world = sc.world or bpy.data.worlds.new("w")
sc.view_settings.view_transform = "Standard"
# chão
bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0)); chao = bpy.context.active_object
mc = bpy.data.materials.new("chao_olhar"); mc.diffuse_color = (0.55, 0.5, 0.42, 1); chao.data.materials.append(mc)
cam_d = bpy.data.cameras.new("olhar"); cam = bpy.data.objects.new("olhar", cam_d); sc.collection.objects.link(cam)
sc.camera = cam; cam_d.type = "ORTHO"; cam_d.ortho_scale = float(os.environ.get("OLHAR_ESCALA", "1.0"))   # OLHAR_ESCALA=0.45 OLHAR_Z=0.7: só a cabeça

# frente (o boneco olha pra +X), lado direito (-Y) e a câmera do jogo (iso: 50° de elevação, 45° de lado)
VISTAS = [("frente", 0.0, 8.0), ("lado", -90.0, 8.0), ("jogo", -45.0, 50.0), ("costas", 180.0, 15.0)]
quadros = []
for pi, ped in enumerate(PEDIDOS):
    partes = ped.split(":")
    anim, t, arma = partes[0], float(partes[1]), partes[2]
    kw = {}
    for ex in partes[3:]:   # "pitch=60" (graus) ou o nome da granada
        if ex.startswith("pitch="): kw["pitch"] = math.radians(float(ex[6:]))
        else: kw["nade"] = ex
    A.pose(t, anim, arma, **kw)
    bpy.context.view_layer.update()
    for vi, (nm, az, el) in enumerate(VISTAS):
        alvo = Vector((0.05, 0, float(os.environ.get("OLHAR_Z", "0.38"))))
        d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
        cam.location = alvo + d * 4
        cam.rotation_euler = (d * -1).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(os.path.dirname(SAIDA), f"_olhar_{pi}_{vi}.png")
        bpy.ops.render.render(write_still=True)
        quadros.append((pi, vi, sc.render.filepath, f"{anim} t={t} {arma} ({nm})"))

# monta a folha (linha = pose, coluna = vista) com o rótulo
import numpy as np
W = H = 360
folha = np.ones(((H + 18) * len(PEDIDOS), W * len(VISTAS), 4), dtype=np.float32)
for pi, vi, path, rot in quadros:
    im = bpy.data.images.load(path); px = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)
    y0 = (H + 18) * (len(PEDIDOS) - 1 - pi)
    folha[y0:y0 + H, vi * W:(vi + 1) * W] = px
    bpy.data.images.remove(im); os.remove(path)
out = bpy.data.images.new("folha", folha.shape[1], folha.shape[0], alpha=True)
out.pixels.foreach_set(folha.ravel()); out.filepath_raw = SAIDA; out.file_format = "PNG"; out.save()
with open(SAIDA + ".txt", "w", encoding="utf-8") as fh:
    for pi, ped in enumerate(PEDIDOS): fh.write(f"linha {pi + 1}: {ped}\n")
print("FOLHA OK", SAIDA)
