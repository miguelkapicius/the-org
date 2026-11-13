# -*- coding: utf-8 -*-
"""Página de aprovação da proposta da Brasa (antes x depois): plantas, retakes e régua.
    python Tools/brasa/pagina_proposta.py <saida.html>
"""
import html, importlib, json, os, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import retake

CORES = {'#': 'var(--m-predio)', 'T': 'var(--m-tr)', 'C': 'var(--m-ct)', 'a': 'var(--m-site)', 'b': 'var(--m-site)',
         'r': 'var(--m-rua)', 'm': 'var(--m-meio)', 'k': 'var(--m-andar)', 'n': 'var(--m-alto)', 's': 'var(--m-escada)',
         '~': 'var(--m-andar)', 'o': 'var(--m-cob)'}
CEL = 9


def planta(m, outro, entradas, rotulos):
    W, H = m.W, m.H
    p = [f'<svg viewBox="0 0 {W * CEL} {H * CEL}" role="img" class="planta">']
    for y in range(H):
        for x in range(W):
            c = m.g[y][x]
            if c == '#':
                continue
            p.append(f'<rect x="{x * CEL}" y="{y * CEL}" width="{CEL}" height="{CEL}" fill="{CORES.get(c, "var(--m-rua)")}"/>')
    if outro is not None:
        for y in range(H):
            for x in range(W):
                if m.g[y][x] != outro.g[y][x]:
                    p.append(f'<rect x="{x * CEL + .75}" y="{y * CEL + .75}" width="{CEL - 1.5}" height="{CEL - 1.5}" class="muda"/>')
    for s, (x0, x1, y0, y1) in m.SITES.items():
        p.append(f'<rect x="{x0 * CEL}" y="{y0 * CEL}" width="{(x1 - x0 + 1) * CEL}" height="{(y1 - y0 + 1) * CEL}" class="plant"/>')
        p.append(f'<text x="{(x0 + x1 + 1) / 2 * CEL}" y="{(y0 + y1 + 1) / 2 * CEL + 7}" class="letra">{s}</text>')
    for i, e in enumerate(entradas):
        x, y = e["porta"]
        p.append(f'<circle cx="{x * CEL + CEL / 2}" cy="{y * CEL + CEL / 2}" r="7" class="porta"/>'
                 f'<text x="{x * CEL + CEL / 2}" y="{y * CEL + CEL / 2 + 3.6}" class="num">{e["n"]}</text>')
    for (n, x, y) in rotulos:
        p.append(f'<text x="{x * CEL}" y="{y * CEL}" class="rot">{html.escape(n)}</text>')
    p.append('</svg>')
    return ''.join(p)


def main(saida):
    va = importlib.import_module('brasa'); vb = importlib.import_module('brasa_proposta')
    ra = retake.medir('brasa'); rb = retake.medir('brasa_proposta')
    ents = {}
    for nomev, r in (("a", ra), ("b", rb)):
        lst = []
        for s in ("A", "B"):
            for e in sorted(r["sites"][s]["entradas"], key=lambda e: e["t"]["spawn CT"] if e["t"]["spawn CT"] is not None else 99):
                lst.append(dict(e, site=s))
        ents[nomev] = lst
    # numera pela proposta (mesmo nome = mesmo número)
    ordem = []
    for e in ents["b"] + ents["a"]:
        k = (e["site"], e["entrada"])
        if k not in ordem:
            ordem.append(k)
    for lst in ents.values():
        for e in lst:
            e["n"] = ordem.index((e["site"], e["entrada"])) + 1
    rot = [("Base CT", 32.5, 1.6), ("Base TR", 4.2, 53.4), ("Banana", 7.6, 24), ("Meio", 26, 28.3), ("Apê", 43.2, 40.4),
           ("Arco", 37.4, 12.6), ("Construção", 23, 15.6), ("Pista", 16, 1.6), ("Biblioteca", 42, 4.2), ("Cemitério", 50, 11.4)]
    svg_a = planta(va, None, ents["a"], rot)
    svg_b = planta(vb, va, ents["b"], rot + [("Sacada", 20.5, 8.6)])

    def tab_retake(s):
        linhas = []
        nomes = [e for e in ents["b"] if e["site"] == s]
        for e in nomes:
            antes = next((x for x in ents["a"] if x["site"] == s and x["entrada"] == e["entrada"]), None)
            ta = antes["t"]["spawn CT"] if antes else None
            tb = e["t"]["spawn CT"]
            fa = "—" if ta is None else f"{ta:.1f} s"
            fb = "—" if tb is None else f"{tb:.1f} s"
            lado = e["lado"]
            linhas.append(f'<tr><td><span class="pin">{e["n"]}</span>{html.escape(e["entrada"])}</td><td class="n">{fa}</td>'
                          f'<td class="n">{fb}</td><td class="n">{e["t"]["do outro bomb"]:.1f} s</td><td class="n">{e["dentro_s"]:.1f} s</td></tr>')
        return ''.join(linhas)

    def tab_regua():
        L = []
        for pa, pb in zip(ra["regua"]["pontos"], rb["regua"]["pontos"]):
            mt = pb["meta"]
            def cel(v, alvo):
                if alvo is None:
                    return f'<td class="n">{v:.1f}</td>'
                d = v - alvo
                cls = "ok" if abs(d) <= 1.5 else ("longe" if abs(d) > 3 else "perto")
                return f'<td class="n {cls}">{v:.1f}</td>'
            muda = abs(pa["tr"] - pb["tr"]) > .05 or abs(pa["ct"] - pb["ct"]) > .05
            L.append(f'<tr class="{"mudou" if muda else ""}"><td>{html.escape(pb["nome"])}</td>'
                     + cel(pa["tr"], mt and mt[0]) + cel(pa["ct"], mt and mt[1])
                     + cel(pb["tr"], mt and mt[0]) + cel(pb["ct"], mt and mt[1])
                     + (f'<td class="n meta">{mt[0]:.1f} / {mt[1]:.1f}</td>' if mt else '<td class="n meta">—</td>') + '</tr>')
        return ''.join(L)

    A, B = ra["regua"], rb["regua"]
    regras = vb.regras_angulos(retake.Mapa(vb).ve)
    dados = {
        "SVG_A": svg_a, "SVG_B": svg_b, "RET_A": tab_retake("A"), "RET_B": tab_retake("B"), "REGUA": tab_regua(),
        "PISA": f'A: TR {A["pisa"]["A"][0]} → {B["pisa"]["A"][0]} s, CT {A["pisa"]["A"][1]} → {B["pisa"]["A"][1]} s · '
                f'B: TR {A["pisa"]["B"][0]} → {B["pisa"]["B"][0]} s, CT {A["pisa"]["B"][1]} → {B["pisa"]["B"][1]} s',
        "ROT": f'CT A→B {A["rotAB"]} → {B["rotAB"]} s, B→A {A["rotBA"]} → {B["rotBA"]} s · TR banana→varanda {A["rotTR"]} → {B["rotTR"]} s',
        "REGRAS": "todas passam" if not regras else "; ".join(regras),
    }
    t = open(os.path.join(AQUI, 'pagina_proposta.html'), encoding='utf-8').read()
    for k, v in dados.items():
        t = t.replace('{{' + k + '}}', v)
    open(saida, 'w', encoding='utf-8').write(t)


if __name__ == '__main__':
    main(sys.argv[1])
