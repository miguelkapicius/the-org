# Ícones de arma do HUD: silhueta branca de lado (cano pra direita) renderizada dos MESMOS modelos das armas do jogo
# (agentes.py), em alta resolução, fundo transparente, recortada no contorno. Sai um PNG por arma.
#   blender -b --factory-startup --python icones_armas.py -- <pasta de saída> [altura px]
import bpy, sys, os
from mathutils import Vector

ARGV = sys.argv[sys.argv.index("--") + 1:]
SAIDA = ARGV[0]; ALT = int(ARGV[1]) if len(ARGV) > 1 else 256
AQUI = os.path.dirname(os.path.abspath(__file__))
os.makedirs(SAIDA, exist_ok=True)
g = {"__name__": "agentes_lib", "__file__": os.path.join(AQUI, "agentes.py")}
argv0 = sys.argv
sys.argv = [argv0[0], "--", os.path.join(SAIDA, "_lixo"), "ct", "nenhum"]
exec(compile(open(os.path.join(AQUI, "agentes.py"), encoding="utf-8").read(), "agentes.py", "exec"), g)
sys.argv = argv0
A = g["A"]; WEAP = g["WEAP"]
sc = bpy.context.scene

# tudo some; workbench chapado branco, fundo transparente
for o in sc.objects:
    o.hide_render = True
    for md in o.modifiers:
        if md.type == "SOLIDIFY": md.show_render = False
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "FLAT"; sh.color_type = "SINGLE"; sh.single_color = (1, 1, 1); sh.show_object_outline = False
sh.show_cavity = False; sh.show_shadows = False
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
cam_d = bpy.data.cameras.new("icone"); cam = bpy.data.objects.new("icone", cam_d); sc.collection.objects.link(cam)
sc.camera = cam; cam_d.type = "ORTHO"

import numpy as np
for w in WEAP:
    G = A.gun(w)
    pecas = [G.obj] + ([G.mag] if G.mag else [])
    G.obj.location = (0, 0, 0); G.obj.rotation_mode = "QUATERNION"; G.obj.rotation_quaternion = (1, 0, 0, 0); G.obj.scale = (1, 1, 1)
    if G.mag:
        G.mag.location = Vector(G.mag["piv"]); G.mag.rotation_mode = "QUATERNION"; G.mag.rotation_quaternion = (1, 0, 0, 0); G.mag.scale = (1, 1, 1)
    for o in pecas: o.hide_render = False
    bpy.context.view_layer.update()
    # caixa da arma (no plano x-z), câmera do lado (-y olhando +y)
    pts = [o.matrix_world @ Vector(c) for o in pecas for c in o.bound_box]
    x0 = min(p.x for p in pts); x1 = max(p.x for p in pts); z0 = min(p.z for p in pts); z1 = max(p.z for p in pts)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2; larg = (x1 - x0) * 1.04; alt = (z1 - z0) * 1.1
    cam.location = (cx, -5, cz); cam.rotation_euler = (1.5707963, 0, 0)
    cam_d.ortho_scale = max(larg, alt)
    res_x = int(ALT * max(larg, alt) / alt) if larg > alt else ALT
    sc.render.resolution_x = res_x; sc.render.resolution_y = int(res_x * alt / larg) if larg > alt else ALT
    sc.render.resolution_percentage = 100
    tmp = os.path.join(SAIDA, f"_{w}.png"); sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    # recorta no contorno (alfa) com 2 px de folga
    im = bpy.data.images.load(tmp); W, H = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)
    ys, xs = np.where(px[:, :, 3] > 0.02)
    if len(xs):
        a, b = max(0, xs.min() - 2), min(W, xs.max() + 3); c, d = max(0, ys.min() - 2), min(H, ys.max() + 3)
        crop = px[c:d, a:b].copy(); crop[:, :, :3] = 1.0
        out = bpy.data.images.new(w, b - a, d - c, alpha=True)
        out.pixels.foreach_set(crop.ravel()); out.filepath_raw = os.path.join(SAIDA, f"{w}.png"); out.file_format = "PNG"; out.save()
        print("ICONE", w, b - a, d - c)
    bpy.data.images.remove(im); os.remove(tmp)
    for o in pecas: o.hide_render = True
