# Perfil das armas normalizadas (comprimento 1, cano no z=0, coronha em x=0): fundo da arma por fatia ao longo do
# cano. Serve pra achar onde fica o pente (o "vale" mais fundo que não é o punho).
#   blender -b --factory-startup --python perfil_armas.py
import bpy, sys, os
sys.argv = [sys.argv[0], "--", os.environ.get("TEMP", "."), "ct"]
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "agentes.py"), encoding="utf-8").read()
src = src.split("# ------------------------------------------------------------------ agentes")[0]   # só cena + armas
exec(compile(src, "agentes_base", "exec"))
for name in WEAP:
    g = import_gun(name, 1.0)
    vs = [v.co for v in g.data.vertices]
    linha = []
    for i in range(40):
        a, b = i / 40, (i + 1) / 40
        sl = [v.z for v in vs if a <= v.x < b]
        linha.append("%5.2f" % (min(sl) if sl else 0))
    print("PERFIL", name, " ".join(linha))
    bpy.data.objects.remove(g)
