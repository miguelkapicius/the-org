# Blender: arma de lado em ortográfica. Saída: <nome>_sil.png (silhueta preta no branco) e <nome>_luz.png (sombreado
# com cavidade e contorno pra ver os detalhes). Uso: blender -b -P lado.py -- <fbx> <saida_dir> <nome>
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index("--") + 1:]
fbx, out, nome = a[0], a[1], a[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
pts = [o.matrix_world @ mathutils.Vector(c) for o in objs for c in o.bound_box]
mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
cen = (mn + mx) / 2; tam = mx - mn
print("TAMANHO", nome, tuple(round(v, 4) for v in tam))
# eixo mais fino = espessura (olhamos ao longo dele); o mais longo = comprimento (horizontal na imagem)
eix = sorted(range(3), key=lambda i: tam[i])
fino, alto, longo = eix[0], eix[1], eix[2]
cam_d = bpy.data.cameras.new("c"); cam_d.type = 'ORTHO'
cam = bpy.data.objects.new("c", cam_d); bpy.context.scene.collection.objects.link(cam)
cam_d.ortho_scale = tam[longo] * 1.04
d = mathutils.Vector((0, 0, 0)); d[fino] = 1
up = mathutils.Vector((0, 0, 0)); up[alto] = 1
cam.location = cen + d * (tam[fino] * 4 + 1)
rot = (-d).to_track_quat('-Z', 'Y')
# alinhar o "up" da câmera com o eixo da altura
m = mathutils.Matrix.Identity(3)
z = d.normalized(); y = up.normalized(); x = y.cross(z)
m.col[0] = x; m.col[1] = y; m.col[2] = z
cam.rotation_euler = m.to_euler()
cam_d.clip_end = 1000
sc = bpy.context.scene; sc.camera = cam
px = 2000
sc.render.resolution_x = px; sc.render.resolution_y = int(px * tam[alto] / tam[longo] * 1.04 / 1.04) + 40
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new("w")
# silhueta
sh.light = 'FLAT'; sh.color_type = 'SINGLE'; sh.single_color = (0, 0, 0)
sh.background_type = 'VIEWPORT'; sh.background_color = (1, 1, 1)
sc.view_settings.view_transform = 'Standard'
sh.show_cavity = False; sh.show_object_outline = False
sc.render.filepath = f"{out}/{nome}_sil.png"; bpy.ops.render.render(write_still=True)
# sombreado com cavidade (detalhes)
sh.light = 'STUDIO'; sh.single_color = (0.75, 0.75, 0.75); sh.show_cavity = True; sh.cavity_type = 'BOTH'
sh.cavity_ridge_factor = 2; sh.cavity_valley_factor = 2; sh.curvature_ridge_factor = 2; sh.curvature_valley_factor = 2
sh.background_color = (0.2, 0.2, 0.25)
sc.render.filepath = f"{out}/{nome}_luz.png"; bpy.ops.render.render(write_still=True)
# do outro lado também (pra saber qual lado o CS2 mostra: cano pra direita)
cam.location = cen - d * (tam[fino] * 4 + 1)
m.col[0] = -x; m.col[2] = -z; cam.rotation_euler = m.to_euler()
sc.render.filepath = f"{out}/{nome}_luz2.png"; bpy.ops.render.render(write_still=True)
