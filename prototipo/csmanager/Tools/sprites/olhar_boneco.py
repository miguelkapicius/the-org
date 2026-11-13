# Folha do boneco com as CORES DO EXPORT (cor por canto: trama, sombreado), sem Unity.
#   blender -b --factory-startup --python olhar_boneco.py -- <saida.png> <ct|tr> [agentes.py alternativo]
import bpy, sys, os, math
import numpy as np
from mathutils import Vector
ARGV = sys.argv[sys.argv.index("--") + 1:]
SAIDA, TEAM = ARGV[0], ARGV[1]
AQUI = os.path.dirname(os.path.abspath(__file__))
FONTE = ARGV[2] if len(ARGV) > 2 else os.path.join(AQUI, "agentes.py")
g = {"__name__": "agentes_lib", "__file__": os.path.join(AQUI, "agentes.py")}
argv0 = sys.argv; sys.argv = [argv0[0], "--", os.path.join(os.path.dirname(SAIDA), "_lixo"), TEAM, "nenhum"]
exec(compile(open(FONTE, encoding="utf-8").read(), "agentes.py", "exec"), g); sys.argv = argv0
A = g["A"]; A.root.rotation_euler = (0, 0, 0)
arma = "M4A4" if TEAM == "ct" else "AK47"
A.gun(arma); A.pose(0.0, "mira", arma); bpy.context.view_layer.update()
sc = bpy.context.scene

def desenho(o, c):
    """desenho na cor da peça (o export só leva cor): tricô em carreiras (trama) ou em gomos (tramaV), xadrez da camisa."""
    f = 1.0; t = int(o.get("trama", 0)); tv = int(o.get("tramaV", 0)); xa = int(o.get("xadrez", 0))
    m = float(o.get("mancha", 0)); cm = int(o.get("camu", 0))
    def h_(a, b, c_): return (math.sin(a * 127.1 + b * 311.7 + c_ * 74.7) * 43758.5453) % 1.0
    if m: f *= 1 + m * (h_(round(c[0] * 9), round(c[1] * 9), round(c[2] * 9)) * 2 - 1)
    if cm:
        v = h_(round(c[0] * 2.2 + 0.3), round(c[1] * 2.2), round(c[2] * 2.6))
        f *= 0.82 if v < 0.3 else 1.12 if v > 0.72 else 1.0
    if not (t or tv or xa): return f
    r = math.sqrt(sum(v * v for v in c)) or 1.0
    lat = math.acos(max(-1.0, min(1.0, c[2] / r))) / math.pi; lon = (math.atan2(c[1], c[0]) + math.pi) / (2 * math.pi)
    if t: f *= 1.09 if int(lat * t) % 2 else 0.92
    if tv: f *= 1.055 if int(lon * tv) % 2 else 0.945
    if xa:
        a = int(lat * xa) % 2; b = int(lon * xa * 2) % 2
        f *= 0.72 if (a and b) else 0.88 if (a or b) else 1.07
    return f

mat = bpy.data.materials.new("vc"); mat.use_nodes = True
vc = mat.node_tree.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Cor"
bs = mat.node_tree.nodes["Principled BSDF"]; mat.node_tree.links.new(vc.outputs[0], bs.inputs["Base Color"]); bs.inputs["Roughness"].default_value = 0.9
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    for md in list(o.modifiers):
        if md.type == "SOLIDIFY": o.modifiers.remove(md)
    cores = []
    for sl in o.material_slots:
        m = sl.material; c = (0.5, 0.5, 0.5)
        if m and m.node_tree:
            rgb = next((n for n in m.node_tree.nodes if n.type == "RGB"), None)
            em = next((n for n in m.node_tree.nodes if n.type == "EMISSION"), None)
            if rgb is not None: c = tuple(rgb.outputs[0].default_value[:3])
            elif em is not None: c = tuple(em.inputs[0].default_value[:3])
        cores.append(c)
    me = o.data
    if not len(me.vertices): continue
    zs = [v.co.z for v in me.vertices]; z0, za = min(zs), max(max(zs) - min(zs), 1e-6); trama = int(o.get("trama", 0))
    at = me.color_attributes.new("Cor", "FLOAT_COLOR", "CORNER")
    for p in me.polygons:
        c0 = cores[min(p.material_index, len(cores) - 1)] if cores else (0.5, 0.5, 0.5)
        fd = desenho(o, [sum(me.vertices[v].co[i_] for v in p.vertices) / len(p.vertices) for i_ in range(3)])
        for li in p.loop_indices:
            f = (0.92 + 0.08 * (me.vertices[me.loops[li].vertex_index].co.z - z0) / za) * fd
            at.data[li].color = (c0[0] * f, c0[1] * f, c0[2] * f, 1)
    me.materials.clear(); me.materials.append(mat)
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0)); ch = bpy.context.active_object
mc = bpy.data.materials.new("chao"); mc.use_nodes = True; mc.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0.55, 0.5, 0.42, 1); ch.data.materials.append(mc)
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(120))); bpy.context.object.data.energy = 3.0; bpy.context.object.data.angle = math.radians(8)
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.85, 0.88, 1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sc.render.engine = "BLENDER_EEVEE"; sc.view_settings.view_transform = "Standard"
cam_d = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_d); sc.collection.objects.link(cam); sc.camera = cam; cam_d.type = "ORTHO"
W = H = 440; sc.render.resolution_x = W; sc.render.resolution_y = H
VISTAS = [(0.5, 0.74, 18, 12), (0.5, 0.74, -55, 14), (0.85, 0.37, 25, 12), (0.85, 0.37, -60, 14), (0.85, 0.37, 160, 16), (1.0, 0.45, -45, 50)]
folha = np.ones((H, W * len(VISTAS), 4), np.float32)
for k, (esc, zalvo, az, el) in enumerate(VISTAS):
    cam_d.ortho_scale = esc
    d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
    cam.location = Vector((0.05, 0, zalvo)) + d * 4; cam.rotation_euler = (d * -1).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = SAIDA + "_%d.png" % k; bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(sc.render.filepath); folha[:, k * W:(k + 1) * W] = np.array(im.pixels[:], np.float32).reshape(H, W, 4)
    bpy.data.images.remove(im); os.remove(sc.render.filepath)
out = bpy.data.images.new("folha", folha.shape[1], H, alpha=True); out.pixels.foreach_set(folha.ravel()); out.filepath_raw = SAIDA; out.file_format = "PNG"; out.save()
print("FOLHA OK", SAIDA)
