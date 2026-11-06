# Ícones do HUD em PNG a partir dos vetores (armas: Assets/Game/Interface/Hud/Icones/armas_svg.json; interface: abaixo).
# Branco sobre transparente, altura fixa em px; o HUD pinta na cor que quiser.
#   blender -b --factory-startup --python icones_svg.py -- <armas_svg.json> <pasta de saída> [altura px]
import bpy, json, sys, os
import numpy as np

ARGV = sys.argv[sys.argv.index("--") + 1:]
armas = {k: v for k, v in json.load(open(ARGV[0], encoding="utf-8")).items() if not k.startswith("_")}
SAIDA = ARGV[1]; ALT = int(ARGV[2]) if len(ARGV) > 2 else 128
os.makedirs(SAIDA, exist_ok=True)
# ícones de interface (viewBox 0 0 24 24); os de traço viram contorno (stroke) desenhado como preenchimento
UI = {
    "capacete": {"w": 24, "h": 24, "d": "M12 2 L20 5.5 L20 11 C20 16.5 16.6 19.9 12 22 C7.4 19.9 4 16.5 4 11 L4 5.5 Z M12 4.4 L6.2 6.9 L6.2 11 C6.2 15.2 8.7 18 12 19.7 C15.3 18 17.8 15.2 17.8 11 L17.8 6.9 Z M7.5 8.3 L16.5 8.3 L16.5 10.4 L7.5 10.4 Z"},
    "caveira": {"w": 24, "h": 24, "d": "M12 2.5 C6.9 2.5 3.5 5.9 3.5 10.4 C3.5 13.1 4.8 15 6.6 16.1 L6.6 20 L9.3 20 L9.3 18 L11 18 L11 20 L13 20 L13 18 L14.7 18 L14.7 20 L17.4 20 L17.4 16.1 C19.2 15 20.5 13.1 20.5 10.4 C20.5 5.9 17.1 2.5 12 2.5 Z M8.6 8.6 C9.9 8.6 10.8 9.6 10.8 10.8 C10.8 12 9.9 13 8.6 13 C7.4 13 6.4 12 6.4 10.8 C6.4 9.6 7.4 8.6 8.6 8.6 Z M15.4 8.6 C16.6 8.6 17.6 9.6 17.6 10.8 C17.6 12 16.6 13 15.4 13 C14.1 13 13.2 12 13.2 10.8 C13.2 9.6 14.1 8.6 15.4 8.6 Z M12 13.4 L13.3 15.6 L10.7 15.6 Z"},
    "bomba": {"w": 24, "h": 24, "d": "M10.5 7 C14.6 7 18 10.4 18 14.5 C18 18.6 14.6 22 10.5 22 C6.4 22 3 18.6 3 14.5 C3 10.4 6.4 7 10.5 7 Z M14.2 6.2 L16.6 3.8 L18.4 5.6 L16 8 Z M18.6 2 L20.2 2 L20.2 3.6 L18.6 3.6 Z M20.6 4.4 L22 4.4 L22 5.8 L20.6 5.8 Z"},
    "alicate": {"w": 24, "h": 24, "d": "M5 3 L7.6 3 L12 9.2 L16.4 3 L19 3 L13.4 11 L13.4 21 L10.6 21 L10.6 11 Z"},
    "relogio": {"w": 24, "h": 24, "d": "M12 2.5 C17.2 2.5 21.5 6.8 21.5 12 C21.5 17.2 17.2 21.5 12 21.5 C6.8 21.5 2.5 17.2 2.5 12 C2.5 6.8 6.8 2.5 12 2.5 Z M12 5 C8.1 5 5 8.1 5 12 C5 15.9 8.1 19 12 19 C15.9 19 19 15.9 19 12 C19 8.1 15.9 5 12 5 Z M10.8 7 L13.2 7 L13.2 11.4 L16.4 13.3 L15.2 15.3 L10.8 12.7 Z"},
    "vida": {"w": 24, "h": 24, "d": "M9 3 L15 3 L15 9 L21 9 L21 15 L15 15 L15 21 L9 21 L9 15 L3 15 L3 9 L9 9 Z"},
    "hs": {"w": 24, "h": 24, "d": "M12 3 C17 3 21 7 21 12 C21 17 17 21 12 21 C7 21 3 17 3 12 C3 7 7 3 12 3 Z M12 5.6 C8.5 5.6 5.6 8.5 5.6 12 C5.6 15.5 8.5 18.4 12 18.4 C15.5 18.4 18.4 15.5 18.4 12 C18.4 8.5 15.5 5.6 12 5.6 Z M12 9.6 C13.3 9.6 14.4 10.7 14.4 12 C14.4 13.3 13.3 14.4 12 14.4 C10.7 14.4 9.6 13.3 9.6 12 C9.6 10.7 10.7 9.6 12 9.6 Z"},
}

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading; sh.light = "FLAT"; sh.color_type = "SINGLE"; sh.single_color = (1, 1, 1)
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d); sc.collection.objects.link(cam); sc.camera = cam
cam_d.type = "ORTHO"

def um(nome, w, h, d, alt):
    for o in list(bpy.data.objects):
        if o.type == "CURVE": bpy.data.objects.remove(o)
    f = os.path.join(SAIDA, f"_{nome}.svg")
    open(f, "w").write(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}"><path d="{d}" fill="#fff" fill-rule="evenodd"/></svg>')
    bpy.ops.import_curve.svg(filepath=f); os.remove(f)
    curvas = [o for o in bpy.data.objects if o.type == "CURVE"]
    for o in curvas: o.data.fill_mode = "BOTH"
    bpy.context.view_layer.update()
    from mathutils import Vector
    pts = [o.matrix_world @ Vector(c) for o in curvas for c in o.bound_box]
    x0 = min(p.x for p in pts); x1 = max(p.x for p in pts); y0 = min(p.y for p in pts); y1 = max(p.y for p in pts)
    larg, altu = (x1 - x0) * 1.06, (y1 - y0) * 1.06
    cam.location = ((x0 + x1) / 2, (y0 + y1) / 2, 1); cam.rotation_euler = (0, 0, 0)
    cam_d.ortho_scale = max(larg, altu)
    rx = max(8, int(round(alt * larg / altu)))
    sc.render.resolution_x = rx if larg >= altu else alt; sc.render.resolution_y = alt if larg >= altu else int(round(alt * altu / larg))
    if larg >= altu: sc.render.resolution_x = rx
    sc.render.filepath = os.path.join(SAIDA, f"{nome}.png")
    bpy.ops.render.render(write_still=True)
    print("ICONE", nome, sc.render.resolution_x, sc.render.resolution_y)

for nome, a in armas.items(): um(nome, a["w"], 64, a["d"], ALT)
for nome, a in UI.items(): um("ui_" + nome, a["w"], a["h"], a["d"], 96)
