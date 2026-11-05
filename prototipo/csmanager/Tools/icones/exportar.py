"""Exporta os ícones SVG (Tools/icones) pra PNG recortado em Assets/Game/Interface/Resources/Icones.

Renderiza cada SVG no Edge headless com fundo transparente, recorta pela caixa do que não é transparente (com uma
folguinha) e grava RGBA. Sem dependências (PNG lido/escrito na mão com zlib). Uso: python exportar.py [nome ...]
"""
import os, struct, subprocess, sys, zlib, tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "Game", "Interface", "Resources", "Icones"))
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
ALTURA = 256          # altura da página de render (viewBox inteiro); o recorte fica menor
FOLGA = 3

# svg -> nome no Resources (armas pelo Armas3D.DoJogo; o resto com prefixo)
NOMES = {
    "ak47": "AK47", "m4a4": "M4A4", "m4a1s": "M4A1S", "awp": "AWP", "mp9": "MP9", "mac10": "MAC10",
    "usp": "USP", "glock": "Glock", "deagle": "Deagle", "faca": "faca",
    "tec9": "Tec9", "fiveseven": "FiveSeven", "p250": "P250", "galil": "Galil", "famas": "Famas",
    "flash": "nade_flash", "smoke": "nade_smoke", "he": "nade_he", "molotov": "nade_molotov", "incendiaria": "nade_incendiaria",
    "c4": "ui_bomba", "kit": "ui_alicate", "capacete": "ui_capacete", "colete": "ui_colete", "colete_capacete": "ui_colete_capacete", "caveira": "ui_caveira",
    "hs": "ui_hs", "cego": "kf_cego", "semluneta": "kf_semluneta", "smokekill": "kf_smoke", "noar": "kf_noar", "parede": "kf_parede",
}


def ler_png(caminho):
    d = open(caminho, "rb").read()
    assert d[:8] == b"\x89PNG\r\n\x1a\n"
    i, idat, w = 8, b"", 0
    while i < len(d):
        n = struct.unpack(">I", d[i:i + 4])[0]; tipo = d[i + 4:i + 8]; corpo = d[i + 8:i + 8 + n]; i += 12 + n
        if tipo == b"IHDR":
            w, h, prof, cor = struct.unpack(">IIBB", corpo[:10])
            assert prof == 8 and cor in (2, 6), (prof, cor)
            bpp = 4 if cor == 6 else 3
        elif tipo == b"IDAT": idat += corpo
    raw = zlib.decompress(idat); passo = w * bpp; linhas = []; ant = bytearray(passo); p = 0
    for _ in range(h):
        f = raw[p]; lin = bytearray(raw[p + 1:p + 1 + passo]); p += 1 + passo
        for x in range(passo):
            a = lin[x - bpp] if x >= bpp else 0; b = ant[x]; c = ant[x - bpp] if x >= bpp else 0
            if f == 1: lin[x] = (lin[x] + a) & 255
            elif f == 2: lin[x] = (lin[x] + b) & 255
            elif f == 3: lin[x] = (lin[x] + (a + b) // 2) & 255
            elif f == 4:
                pp = a + b - c; pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                lin[x] = (lin[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        linhas.append(lin); ant = lin
    if bpp == 3: raise SystemExit("screenshot sem alfa: o Edge não deixou o fundo transparente")
    return w, h, linhas


def gravar_png(caminho, w, h, linhas):
    raw = b"".join(b"\x00" + bytes(l) for l in linhas)
    def chunk(t, c): return struct.pack(">I", len(c)) + t + c + struct.pack(">I", zlib.crc32(t + c) & 0xFFFFFFFF)
    open(caminho, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                              + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def view_box(svg):
    t = open(svg, encoding="utf-8").read(); i = t.index('viewBox="') + 9
    _, _, vw, vh = map(float, t[i:t.index('"', i)].split()); return vw, vh


def exportar(nome):
    svg = os.path.join(AQUI, nome + ".svg"); vw, vh = view_box(svg)
    W, H = int(round(ALTURA * vw / vh)), ALTURA
    tmp = tempfile.mkdtemp()
    html = os.path.join(tmp, "p.html"); png = os.path.join(tmp, "p.png")
    url = "file:///" + svg.replace("\\", "/")
    open(html, "w", encoding="utf-8").write(
        f'<!doctype html><html><head><style>html,body{{margin:0;background:transparent}}img{{display:block}}</style></head>'
        f'<body><img src="{url}" width="{W}" height="{H}"></body></html>')
    # perfil próprio (não briga com outro Edge aberto) e até 3 tentativas: o headless às vezes trava sem gravar
    for tentativa in range(3):
        try:
            subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                            f"--user-data-dir={os.path.join(tmp, 'perfil%d' % tentativa)}", "--no-first-run",
                            f"--window-size={W},{H}", f"--screenshot={png}", "file:///" + html.replace("\\", "/")],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
            if os.path.exists(png): break
        except subprocess.TimeoutExpired:
            pass
    else:
        raise SystemExit(f"{nome}: o Edge não gerou o print em 3 tentativas")
    w, h, L = ler_png(png)
    xs = [x for y in range(h) for x in range(w) if L[y][4 * x + 3] > 8]
    ys = [y for y in range(h) if any(L[y][4 * x + 3] > 8 for x in range(w))]
    x0, x1 = max(0, min(xs) - FOLGA), min(w - 1, max(xs) + FOLGA)
    y0, y1 = max(0, ys[0] - FOLGA), min(h - 1, ys[-1] + FOLGA)
    corte = [L[y][4 * x0:4 * (x1 + 1)] for y in range(y0, y1 + 1)]
    # branco puro com alfa (o Unity pinta pela cor do RawImage)
    for lin in corte:
        for x in range(0, len(lin), 4): lin[x] = lin[x + 1] = lin[x + 2] = 255
    saida = os.path.join(DESTINO, NOMES[nome] + ".png")
    gravar_png(saida, x1 - x0 + 1, y1 - y0 + 1, corte)
    print(f"{nome:12s} -> {NOMES[nome]}.png  {x1 - x0 + 1}x{y1 - y0 + 1}")


if __name__ == "__main__":
    for n in (sys.argv[1:] or list(NOMES)): exportar(n)
