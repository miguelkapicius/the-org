# Pinta à mão (por regra de peça) a textura das armas do Meshy que vieram SEM textura: ferrolho/armação/punho/cano em
# tons diferentes, mantendo o desgaste e as quinas que o arma_16.py assou (o brilho relativo de cada texel). A arma é
# normalizada igual ao jogo (import_gun do agentes.py: coronha em x=0, boca em x=1, eixo do cano em z=0, z pra cima).
#   blender -b --factory-startup --python pinta_armas.py -- Deagle,Tec9,FiveSeven,P250,Famas
# Grava <arma>_3p_albedo.png por cima (o original fica em <arma>_3p_albedo_liso.png na 1ª vez).
import bpy, sys, os, shutil
import numpy as np
NOMES = sys.argv[sys.argv.index("--") + 1].split(",")
sys.argv = [sys.argv[0], "--", os.environ.get("TEMP", "."), "ct"]
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "agentes.py"), encoding="utf-8").read()
src = src.split("# ------------------------------------------------------------------ agentes")[0]
exec(compile(src, "agentes_base", "exec"))

def hexc(h): h = h.lstrip("#"); return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)
# regras: lista de (cor, teste(x, z)) na ordem; a 1ª que bate pinta. x 0..1 (coronha -> boca), z em fração do comprimento
REGRAS = {
    "Deagle":    [("#1c1d1f", lambda x, z: x < 0.37 and z < -0.27),   # punho de borracha
                  ("#5a5e64", lambda x, z: z > -0.15),                  # ferrolho e cano: aço escuro
                  ("#33353a", lambda x, z: True)],                      # armação
    "FiveSeven": [("#1d1e20", lambda x, z: x < 0.32 and z < -0.3),
                  ("#4a4e54", lambda x, z: z > -0.14),
                  ("#28292c", lambda x, z: True)],
    "P250":      [("#1d1e20", lambda x, z: x < 0.34 and z < -0.3),
                  ("#4d5157", lambda x, z: z > -0.13),
                  ("#26272a", lambda x, z: True)],
    "Tec9":      [("#1e1f21", lambda x, z: z < -0.27),                  # punho e pente
                  ("#4d5056", lambda x, z: True)],                      # caixa e capa do cano
    "Famas":     [("#222326", lambda x, z: x > 0.86 and z > -0.06),    # cano e quebra-chama
                  ("#1f2022", lambda x, z: 0.18 < x < 0.37 and z < -0.06),   # pente
                  ("#3a3e42", lambda x, z: True)],                      # corpo de polímero e alça
}

for nome in NOMES:
    pasta = os.path.join(ARMAS, nome + "_3p"); alb = os.path.join(pasta, nome + "_3p_albedo.png")
    liso = os.path.join(pasta, nome + "_3p_albedo_liso.png")
    if not os.path.exists(liso): shutil.copyfile(alb, liso)
    g = import_gun(nome, 1.0)        # normalizada (comprimento 1); a UV do arma_16 segue a mesma
    me = g.data; uv = me.uv_layers.active.data
    img = bpy.data.images.load(liso); W, H = img.size
    px = np.array(img.pixels[:], np.float32).reshape(H, W, 4)
    lum = px[..., :3] @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    out = px.copy(); cont = {}
    regras = [(hexc(c), f) for c, f in REGRAS[nome]]
    for p in me.polygons:
        c = sum((me.vertices[v].co for v in p.vertices), Vector()) / len(p.vertices)
        cor = next(rc for rc, f in regras if f(c.x, c.z)); cont[tuple(cor)] = cont.get(tuple(cor), 0) + 1
        # triângulo(s) da face no UV -> texels (caixa + teste baricêntrico)
        us = [uv[li].uv for li in p.loop_indices]
        for i in range(1, len(us) - 1):
            A, B, C = (np.array([q.x * W, q.y * H]) for q in (us[0], us[i], us[i + 1]))
            x0, x1 = int(max(0, np.floor(min(A[0], B[0], C[0])) - 1)), int(min(W - 1, np.ceil(max(A[0], B[0], C[0])) + 1))
            y0, y1 = int(max(0, np.floor(min(A[1], B[1], C[1])) - 1)), int(min(H - 1, np.ceil(max(A[1], B[1], C[1])) + 1))
            if x1 < x0 or y1 < y0: continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            d = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
            if abs(d) < 1e-9: continue
            l1 = ((B[1] - C[1]) * (xs - C[0]) + (C[0] - B[0]) * (ys - C[1])) / d
            l2 = ((C[1] - A[1]) * (xs - C[0]) + (A[0] - C[0]) * (ys - C[1])) / d
            m = (l1 >= -0.02) & (l2 >= -0.02) & (1 - l1 - l2 >= -0.02)   # borda de 1 texel (sem costura escura)
            yy, xx = ys[m].astype(int), xs[m].astype(int)
            # tom da peça x brilho relativo do texel (desgaste/quina do arma_16), em volta de 1
            k = np.clip(lum[yy, xx] / max(1e-4, float(lum.mean())), 0.55, 1.6)[:, None]
            out[yy, xx, :3] = np.clip(cor[None, :] * k, 0, 1)
    novo = bpy.data.images.new(nome + "_pintada", W, H, alpha=True); novo.pixels.foreach_set(out.ravel())
    novo.filepath_raw = alb; novo.file_format = "PNG"; novo.save()
    print("PINTADA", nome, W, "x", H, "faces por cor", {"#%02x%02x%02x" % tuple(int(v * 255) for v in k): n for k, n in cont.items()})
    bpy.data.objects.remove(g)
