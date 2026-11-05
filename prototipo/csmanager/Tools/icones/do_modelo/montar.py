# junta o contorno traçado do NOSSO modelo 3D com os risquinhos da casa (recortes em máscara) -> Tools/icones/<n>.svg
import os
S = os.path.dirname(os.path.abspath(__file__)); I = r"D:\unity\CSManager\Tools\icones"
def T(n):
    x0, y0, e = open(os.path.join(S, n + ".txt.T")).read().split(); x0, y0, e = int(x0), int(y0), float(e)
    return lambda px, py: (7 + (px - x0) * e, 7 + (py - y0) * e)
def L(V, pts, w=2.0): return f'<path d="M{" L".join(f"{x:.1f} {y:.1f}" for x, y in (V(*p) for p in pts))}" stroke-width="{w}"/>'
def serr(V, xa, xb, ya, yb, n, incl=0, w=1.8):
    out = []
    for i in range(n):
        x = xa + (xb - xa) * i / (n - 1); out.append(L(V, [(x + incl, ya), (x, yb)], w))
    return "".join(out)
def bola(V, px, py, r): x, y = V(px, py); return f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="#000" stroke="none"/>'
def anel(V, px, py, r): x, y = V(px, py); return f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" stroke-width="1.6"/>'
def caixa(V, a, b, rx=2, w=1.8):
    (x1, y1), (x2, y2) = V(*a), V(*b); return f'<rect x="{x1:.1f}" y="{y1:.1f}" width="{x2-x1:.1f}" height="{y2-y1:.1f}" rx="{rx}" stroke-width="{w}"/>'
DET = {
    "Tec9": lambda V: "".join([
        L(V, [(70, 250), (1290, 250)], 2.2),                 # tubo do ferrolho / armação
        caixa(V, (625, 112), (1100, 158), 3),                 # fenda do ferrolho
        L(V, [(1250, 90), (1250, 240)], 2.2),                 # emenda do cano perfurado
        anel(V, 1250, 352, 3.0), bola(V, 688, 352, 1.4), bola(V, 840, 352, 1.4),
        L(V, [(945, 1468), (1128, 1468)], 2.0),               # base do carregador
        L(V, [(860, 690), (1170, 690)], 1.8),                 # colar do carregador
    ]),
    "FiveSeven": lambda V: "".join([
        L(V, [(75, 310), (1365, 310)], 2.2),                  # ferrolho / armação
        L(V, [(1365, 355), (1925, 355)], 1.8),
        serr(V, 175, 520, 158, 285, 7, -6),                   # serrilha de trás
        serr(V, 1450, 1730, 200, 312, 5, -6),                 # serrilha da frente
        caixa(V, (855, 128), (1195, 222), 2),                 # janela de ejeção
        L(V, [(440, 752), (322, 1272)], 2.0),                 # painel do punho
        L(V, [(95, 1292), (600, 1292)], 2.2),                 # base do carregador
        bola(V, 852, 465, 1.6), bola(V, 1232, 465, 1.6),
    ]),
    "P250": lambda V: "".join([
        L(V, [(210, 325), (1925, 325)], 2.2),                 # ferrolho / armação
        serr(V, 305, 690, 135, 300, 8, -8),                   # serrilha de trás
        serr(V, 1505, 1755, 238, 318, 5, -6),                 # serrilha da frente
        L(V, [(1430, 455), (1920, 455)], 1.8),                # trilho
        L(V, [(648, 765), (602, 1245)], 2.0),                 # painel do punho
        L(V, [(178, 1265), (630, 1265)], 2.2),                # base do carregador
        caixa(V, (950, 345), (1240, 418), 3, 1.6),            # alavanca de desmontagem
    ]),
}
ARQ = {"Tec9": "tec9", "FiveSeven": "fiveseven", "P250": "p250"}
for n, arq in ARQ.items():
    l = open(os.path.join(S, n + ".txt")).read().split("\n"); w, h = l[0].split(); V = T(n)
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}">
  <!-- {n}: contorno tirado do NOSSO modelo 3D (EsportsPoc/Tools/autoral/armas_out/{n}, render de lado em ortográfica,
       traçado automático) + risquinhos da casa à mão. Gerado por scratchpad icones3d/montar.py em 2026-11-05. -->
  <defs>
    <mask id="c" maskUnits="userSpaceOnUse" x="0" y="0" width="{w}" height="{h}">
      <rect width="{w}" height="{h}" fill="#fff"/>
      <g stroke="#000" fill="none" stroke-linecap="round">{DET[n](V)}</g>
    </mask>
  </defs>
  <path fill="#fff" fill-rule="evenodd" mask="url(#c)" d="{" ".join(l[1:])}"/>
</svg>
'''
    open(os.path.join(S, arq + "_novo.svg"), "w", encoding="utf-8").write(svg)
