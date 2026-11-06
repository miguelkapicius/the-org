# Galil: contorno do NOSSO modelo 3D (armas_out/Galil) + risquinhos da casa (recortes em máscara), mesmo jeito da Tec9/P250
import os
S = os.path.dirname(os.path.abspath(__file__))
x0, y0, e = open(os.path.join(S, "Galil.txt.T")).read().split(); x0, y0, e = int(x0), int(y0), float(e)
V = lambda px, py: (7 + (px - x0) * e, 7 + (py - y0) * e)
def L(pts, w=2.2): return f'<path d="M{" L".join(f"{x:.1f} {y:.1f}" for x, y in (V(*p) for p in pts))}" stroke-width="{w}"/>'
def bola(px, py, r): x, y = V(px, py); return f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="#000" stroke="none"/>'
det = "".join([
    L([(530, 158), (990, 158)]),                      # tampa da caixa (dust cover)
    L([(1000, 95), (1000, 232)]),                     # emenda caixa / guarda-mão
    L([(1068, 148), (1265, 148)], 2.4), L([(1068, 168), (1265, 168)], 2.4), L([(1068, 186), (1265, 186)], 2.4),   # fendas do guarda-mão
    L([(470, 152), (470, 232)], 2.0),                 # dobradiça da coronha
    L([(842, 262), (858, 400), (915, 560)], 2.0),     # nervura do carregador
    bola(622, 180, 1.6), bola(660, 216, 1.6), bola(745, 206, 1.6), bola(772, 190, 1.6),   # pinos
])
l = open(os.path.join(S, "Galil.txt")).read().split("\n"); w, h = l[0].split()
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}">
  <!-- Galil: contorno tirado do NOSSO modelo 3D (EsportsPoc/Tools/autoral/armas_out/Galil, render de lado em ortográfica,
       traçado automático) + risquinhos da casa à mão. Gerado por scratchpad icones3d/montar_galil.py em 2026-11-06. -->
  <defs>
    <mask id="galil_det" maskUnits="userSpaceOnUse" x="0" y="0" width="{w}" height="{h}">
      <rect width="{w}" height="{h}" fill="#fff"/>
      <g stroke="#000" fill="none" stroke-linecap="round">{det}</g>
    </mask>
  </defs>
  <path fill="#fff" fill-rule="evenodd" mask="url(#galil_det)" d="{" ".join(l[1:])}"/>
</svg>
'''
open(os.path.join(S, "galil_novo.svg"), "w", encoding="utf-8").write(svg)
I = r"D:\unity\CSManager\Tools\icones"
def sv(p, h=150): return open(p, encoding="utf-8").read().replace("<svg ", f'<svg height="{h}" ', 1)
open(os.path.join(S, "prev_galil.html"), "w", encoding="utf-8").write(f"""<html><body style="background:#1b1d22;margin:14px;font:15px sans-serif;color:#aaa">
<div style="display:flex;gap:40px;align-items:center"><span style="width:90px">antiga</span>{sv(os.path.join(I,'galil.svg'))}</div><br>
<div style="display:flex;gap:40px;align-items:center"><span style="width:90px">nova</span>{sv(os.path.join(S,'galil_novo.svg'))}</div><br>
<div style="display:flex;gap:40px;align-items:center"><span style="width:90px">AK / M4 (aprovadas)</span>{sv(os.path.join(I,'ak47.svg'))}{sv(os.path.join(I,'m4a4.svg'))}</div><br>
<div style="display:flex;gap:30px;align-items:center;background:#000a;padding:8px"><span style="width:90px">tamanho HUD</span>{sv(os.path.join(S,'galil_novo.svg'),26)}{sv(os.path.join(I,'ak47.svg'),26)}{sv(os.path.join(I,'galil.svg'),26)}</div>
</body></html>""")
