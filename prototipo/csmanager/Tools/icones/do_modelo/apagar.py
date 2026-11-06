# Blender: apaga (pinta de branco) retângulos da silhueta. Uso: -- <in.png> <out.png> x0,y0,x1,y1 [...] (pixels, y do topo)
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
img = bpy.data.images.load(a[0]); W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
for r in a[2:]:
    x0, y0, x1, y1 = map(int, r.split(","))
    px[H - y1:H - y0, x0:x1, :3] = 1.0
img.pixels[:] = px.ravel(); img.filepath_raw = a[1]; img.file_format = 'PNG'; img.save()
