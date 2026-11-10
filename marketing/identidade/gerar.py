#!/usr/bin/env python3
"""Gera a logo e as artes de divulgação do jogo em SVG (texto já convertido em curvas).

Uso:  python3 marketing/identidade/gerar.py              # gera tudo em marketing/identidade/saida/
      python3 marketing/identidade/gerar.py "OUTRO NOME"  # troca o nome em todas as peças

Depende de: fonttools e uharfbuzz (pip install fonttools uharfbuzz).
Os PNG saem com `node marketing/identidade/render.js` (precisa do Playwright).

Tudo aqui é desenho vetorial feito em código: nada de imagem gerada por modelo.
As cores vêm da interface (decisão 0025) e da maquete do mapa (Paleta3D.cs).
"""
import math
import os
import sys

import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, "saida")

# ---------------------------------------------------------------- cores
GRAFITE = "#0b0d10"   # fundo da interface (0025)
PAINEL = "#14181c"
LINHA = "#262c33"
OSSO = "#eceef1"      # texto da interface
SUAVE = "#a4aab4"
LARANJA = "#f0a640"   # TR e o nosso time
AZUL = "#62a8ee"      # CT e o adversário
# maquete (mapa Vila)
LADRILHO, LADRILHO2, REJUNTE = "#e9c98f", "#e2c084", "#c49a68"
REBOCO, OCRE, CREME = "#f0d9ae", "#c99b6c", "#fbebc8"
TELHA, TELHA2 = "#e0703f", "#c85a34"
MADEIRA, MADEIRA2 = "#c98b4e", "#8f5a32"
PEDRA, VERDE, TURQUESA = "#c9c4b8", "#5db548", "#3fa6a0"
BASE = "#a07450"


def mix(cor, f, alvo="#000000"):
    """Escurece (f<1) a cor em direção ao alvo."""
    a = [int(cor[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(alvo[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02x%02x%02x" % tuple(round(b[i] + (a[i] - b[i]) * f) for i in range(3))


# ---------------------------------------------------------------- texto em curvas
class Fonte:
    def __init__(self, arq):
        caminho = os.path.join(AQUI, "fontes", arq)
        self.tt = TTFont(caminho)
        self.gs = self.tt.getGlyphSet()
        self.upm = self.tt["head"].unitsPerEm
        self.cap = self.tt["OS/2"].sCapHeight
        with open(caminho, "rb") as f:
            self.hb = hb.Font(hb.Face(f.read()))

    def formas(self, txt):
        buf = hb.Buffer()
        buf.add_str(txt)
        buf.guess_segment_properties()
        hb.shape(self.hb, buf, {"kern": True, "liga": False})
        nomes = self.tt.getGlyphOrder()
        return [(nomes[i.codepoint], p.x_advance) for i, p in zip(buf.glyph_infos, buf.glyph_positions)]

    def largura(self, txt, tam, track=0.0):
        e = tam / self.cap
        fs = self.formas(txt)
        return sum(a for _, a in fs) * e + track * tam * (len(fs) - 1)

    def curvas(self, txt, tam, x=0.0, y=0.0, track=0.0, matriz=None):
        """`tam` é a altura da maiúscula em px; (x, y) é o canto esquerdo da linha de base.
        `matriz` (xx, xy, yx, yy, dx, dy) leva o texto para outro plano (ex.: o chão da maquete)."""
        e = tam / self.cap
        pen = SVGPathPen(self.gs, ntos=lambda v: ("%.2f" % v).rstrip("0").rstrip("."))
        cx = 0.0
        for nome, av in self.formas(txt):
            t = (e, 0, 0, -e, x + cx, y)
            if matriz:
                xx, xy, yx, yy, dx, dy = matriz
                gx, gy = x + cx, y
                t = (e * xx, e * xy, -e * yx, -e * yy, xx * gx + yx * gy + dx, xy * gx + yy * gy + dy)
            self.gs[nome].draw(TransformPen(pen, t))
            cx += av * e + track * tam
        return pen.getCommands()


F800 = Fonte("BarlowCondensed-ExtraBold.ttf")
F600 = Fonte("BarlowCondensed-SemiBold.ttf")


# ---------------------------------------------------------------- símbolo e logo
def lente(c1x, cy, r1, c2x, r2):
    """A parte do ponto (centro c2x, raio r2) que aparece dentro da janela (centro c1x, raio r1)."""
    d = c2x - c1x
    x = (d * d + r1 * r1 - r2 * r2) / (2 * d)
    y = math.sqrt(r1 * r1 - x * x)
    return (f'M{c1x + x:.2f} {cy - y:.2f}A{r1:.2f} {r1:.2f} 0 0 1 {c1x + x:.2f} {cy + y:.2f}'
            f'A{r2:.2f} {r2:.2f} 0 0 1 {c1x + x:.2f} {cy - y:.2f}Z')


def arredondar(pts, r):
    """Polígono com os cantos arredondados."""
    d, n = "", len(pts)
    for i in range(n):
        (ax, ay), (bx, by), (cx, cy) = pts[i - 1], pts[i], pts[(i + 1) % n]
        la, lc = math.hypot(ax - bx, ay - by), math.hypot(cx - bx, cy - by)
        p = (bx + (ax - bx) * r / la, by + (ay - by) * r / la)
        q = (bx + (cx - bx) * r / lc, by + (cy - by) * r / lc)
        d += f'{"L" if i else "M"}{p[0]:.2f} {p[1]:.2f}Q{bx:.2f} {by:.2f} {q[0]:.2f} {q[1]:.2f}'
    return d + "Z"


def circulo(cx, cy, r):
    return f'M{cx - r:.2f} {cy:.2f}A{r:.2f} {r:.2f} 0 1 0 {cx + r:.2f} {cy:.2f}A{r:.2f} {r:.2f} 0 1 0 {cx - r:.2f} {cy:.2f}Z'


def simbolo(x, y, s, bloco=OSSO, ponto=LARANJA, borda=None):
    """O brasão da org. Cinco lados (os cinco titulares), a janela redonda (o O de ORG, o olho de quem assiste)
    e o ponto laranja que aparece só o necessário atrás dela (o bot abrindo pixel). Ocupa o quadrado s × s.
    Com `borda`, o brasão ganha contorno e a janela é preenchida: é o ícone solto, que funciona em qualquer fundo."""
    esc = arredondar([(x + a * s, y + b * s) for a, b in ((0.08, 0.05), (0.92, 0.05), (0.92, 0.6), (0.5, 0.97), (0.08, 0.6))], 0.075 * s)
    jx, jy, jr = x + 0.5 * s, y + 0.43 * s, 0.25 * s
    olho = f'<path d="{lente(jx, jy, jr, jx + 0.135 * s, 0.205 * s)}" fill="{ponto}"/>'
    if borda:
        return (f'<path d="{esc}" fill="{bloco}" stroke="{borda}" stroke-width="{0.09 * s:.2f}" stroke-linejoin="round" paint-order="stroke"/>'
                f'<path d="{circulo(jx, jy, jr)}" fill="{borda}"/>{olho}')
    return f'<path d="{esc}{circulo(jx, jy, jr)}" fill="{bloco}" fill-rule="evenodd"/>{olho}'


def palavra(nome, tam, x, y, cor=OSSO, ponto=LARANJA):
    """Nome do jogo em Barlow Condensed ExtraBold. Marcas dentro do nome:
    "@" troca a letra seguinte (um O) pelo anel com o ponto espiando por dentro, igual à janela do brasão;
    "|" põe o ponto meio escondido atrás da letra seguinte; "*" troca a letra seguinte pelo ponto inteiro.
    Devolve (svg, largura)."""
    track = 0.012
    out, cx = [], x
    partes = nome.replace("*", "\0*").replace("|", "\0|").replace("@", "\0@").split("\0")
    ant = ""
    for p in partes:
        cy = y - 0.5 * tam
        if p.startswith("@"):
            p = p[2:]
            R, t = 0.515 * tam, 0.2 * tam
            cx += 0.035 * tam
            out.append(f'<path d="{circulo(cx + R, cy, R)}{circulo(cx + R, cy, R - t)}" fill="{cor}" fill-rule="evenodd"/>')
            out.append(f'<path d="{lente(cx + R, cy, R - t, cx + R + 0.17 * tam, 0.245 * tam)}" fill="{ponto}"/>')
            cx += 2 * R + 0.06 * tam
        elif p.startswith("|"):
            p = p[1:]
            r = 0.2 * tam
            cx += (0.23 if ant[-1:] in "LTFP" else 0.37) * tam   # o vão onde o ponto aparece
            corte = cx - 0.065 * tam              # folga entre o ponto e a letra que o esconde
            c0 = cx + 0.02 * tam
            h = math.sqrt(r * r - (corte - c0) ** 2)
            out.append(f'<path d="M{corte:.2f} {cy - h:.2f}A{r:.2f} {r:.2f} 0 1 0 {corte:.2f} {cy + h:.2f}Z" fill="{ponto}"/>')
        elif p.startswith("*"):
            p = p[2:]
            r = 0.36 * tam
            cx += 0.04 * tam
            out.append(f'<circle cx="{cx + r:.2f}" cy="{cy:.2f}" r="{r:.2f}" fill="{ponto}"/>')
            cx += 2 * r + 0.07 * tam
        if p:
            out.append(f'<path d="{F800.curvas(p, tam, cx, y, track)}" fill="{cor}"/>')
            cx += F800.largura(p, tam, track) + track * tam
            ant = p.strip() or ant
    return "".join(out), cx - x


def limpo(nome):
    return nome.replace("|", "").replace("*", "").replace("@", "")


def subtitulo(txt, tam, x, y, cor=SUAVE):
    return f'<path d="{F600.curvas(txt, tam, x, y, 0.16)}" fill="{cor}"/>', F600.largura(txt, tam, 0.16)


def logo_horizontal(nome, tam, x, y, cor=OSSO, sub="ESPORTS MANAGER", cor_sub=SUAVE, ponto=LARANJA):
    """Brasão + nome numa linha, com o gênero embaixo. (x, y) = canto de cima do brasão. Devolve (svg, largura, altura)."""
    st = 0.19 * tam
    s = tam + (0.3 * tam + st if sub else 0) + 0.3 * tam      # o brasão cobre o nome e o subtítulo
    g = simbolo(x - 0.08 * s, y, s, cor, ponto)
    px, ty = x + 0.84 * s + 0.26 * tam, y + 0.12 * tam
    w, lw = palavra(nome, tam, px, ty + tam, cor, ponto)
    if sub:
        g += subtitulo(sub, st, px + 0.01 * tam, ty + tam + 0.3 * tam + st, cor_sub)[0]
    return g + w, px + lw - x, s


def logo_empilhada(nome, tam, x, y, cor=OSSO, sub="ESPORTS MANAGER", cor_sub=SUAVE, ponto=LARANJA, com_simbolo=False):
    """A logo principal: o nome grande e o gênero embaixo (o brasão em cima é opcional). Devolve (svg, largura, altura)."""
    g, cy = "", y
    if com_simbolo:
        s = 0.9 * tam
        g += simbolo(x - 0.08 * s, cy, s, cor, ponto)
        cy += s + 0.3 * tam
    w, lw = palavra(nome, tam, x, cy + tam, cor, ponto)
    g += w
    cy += tam
    if sub:
        st = 0.19 * tam
        cy += 0.3 * tam + st
        g += subtitulo(sub, st, x + 0.01 * tam, cy, cor_sub)[0]
    return g, lw, cy - y


# ---------------------------------------------------------------- a maquete (isométrica)
class Maquete:
    """A partida vista de cima, do jeito que o jogo mostra: mapa em maquete, bonecos laranja (TR) e azuis (CT),
    cone de visão, rota e fumaça. Coordenadas em quadras; o canto (0,0) fica ao fundo."""

    def __init__(self, u, ox, oy):
        self.u, self.ox, self.oy = u, ox, oy
        self.chao, self.itens, self.defs = [], [], []
        self.n = 0

    def p(self, x, y, z=0.0):
        return (self.ox + (x - y) * 0.866 * self.u, self.oy + (x + y) * 0.5 * self.u - z * self.u)

    def pol(self, pts, cor, extra=""):
        d = " ".join("%.1f,%.1f" % self.p(*q) for q in pts)
        return f'<polygon points="{d}" fill="{cor}" stroke="{cor}" stroke-width="0.7" stroke-linejoin="round"{extra}/>'

    def caixa(self, x0, y0, x1, y1, z0, z1, cor, dividir=True, borda=False):
        xs = [x0] + ([float(i) for i in range(math.floor(x0) + 1, math.ceil(x1))] if dividir else []) + [x1]
        ys = [y0] + ([float(i) for i in range(math.floor(y0) + 1, math.ceil(y1))] if dividir else []) + [y1]
        for a, b in zip(xs, xs[1:]):
            for c, d in zip(ys, ys[1:]):
                e = f' stroke="{mix(cor, 0.5)}" stroke-width="{0.03 * self.u:.2f}"' if borda else ""
                s = self.pol([(a, d, z0), (b, d, z0), (b, d, z1), (a, d, z1)], mix(cor, 0.82))
                s += self.pol([(b, c, z0), (b, d, z0), (b, d, z1), (b, c, z1)], mix(cor, 0.64))
                s += self.pol([(a, c, z1), (b, c, z1), (b, d, z1), (a, d, z1)], cor)
                if borda:
                    s = s.replace('stroke-linejoin="round"', 'stroke-linejoin="round"' + e).replace(
                        f'stroke="{mix(cor, 0.82)}"', "").replace(f'stroke="{mix(cor, 0.64)}"', "").replace(f'stroke="{cor}" ', "")
                self.itens.append(((b + d, z0), s))

    def telhado_x(self, x0, x1, y0, y1, ze, zr, cor=TELHA, parede=REBOCO):
        """Duas águas com a cumeeira ao longo de x."""
        ym, b = (y0 + y1) / 2, 0.25
        xs = [x0] + [float(i) for i in range(math.floor(x0) + 1, math.ceil(x1))] + [x1]
        for a, c in zip(xs, xs[1:]):
            s = self.pol([(a, y0 - b, ze), (c, y0 - b, ze), (c, ym, zr), (a, ym, zr)], mix(cor, 0.88))
            s += self.pol([(c, y0, ze), (c, ym, zr), (c, y1, ze)], mix(parede, 0.64))
            s += self.pol([(a, ym, zr), (c, ym, zr), (c, y1 + b, ze), (a, y1 + b, ze)], cor)
            for t in (0.35, 0.68):
                q0, q1 = self.p(a, ym + (y1 + b - ym) * t, zr + (ze - zr) * t), self.p(c, ym + (y1 + b - ym) * t, zr + (ze - zr) * t)
                s += f'<line x1="{q0[0]:.1f}" y1="{q0[1]:.1f}" x2="{q1[0]:.1f}" y2="{q1[1]:.1f}" stroke="{mix(cor, 0.84)}" stroke-width="{0.035 * self.u:.2f}"/>'
            self.itens.append(((c + y1 + b, ze), s))

    def telhado_y(self, x0, x1, y0, y1, ze, zr, cor=TELHA, parede=OCRE):
        """Duas águas com a cumeeira ao longo de y."""
        xm, b = (x0 + x1) / 2, 0.25
        ys = [y0] + [float(i) for i in range(math.floor(y0) + 1, math.ceil(y1))] + [y1]
        for a, c in zip(ys, ys[1:]):
            s = self.pol([(x0 - b, a, ze), (x0 - b, c, ze), (xm, c, zr), (xm, a, zr)], cor)
            s += self.pol([(x0, c, ze), (xm, c, zr), (x1, c, ze)], mix(parede, 0.82))
            s += self.pol([(xm, a, zr), (xm, c, zr), (x1 + b, c, ze), (x1 + b, a, ze)], mix(cor, 0.74))
            for t in (0.35, 0.68):
                q0, q1 = self.p(xm + (x1 + b - xm) * t, a, zr + (ze - zr) * t), self.p(xm + (x1 + b - xm) * t, c, zr + (ze - zr) * t)
                s += f'<line x1="{q0[0]:.1f}" y1="{q0[1]:.1f}" x2="{q1[0]:.1f}" y2="{q1[1]:.1f}" stroke="{mix(cor, 0.62)}" stroke-width="{0.035 * self.u:.2f}"/>'
            self.itens.append(((x1 + b + c, ze), s))

    def piramide(self, x0, y0, x1, y1, ze, za, cor=TELHA):
        b = 0.25
        xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
        s = self.pol([(x0 - b, y1 + b, ze), (x1 + b, y1 + b, ze), (xm, ym, za)], cor)
        s += self.pol([(x1 + b, y0 - b, ze), (x1 + b, y1 + b, ze), (xm, ym, za)], mix(cor, 0.74))
        self.itens.append(((x1 + y1 + 2 * b, ze), s))

    def janela_y(self, x0, x1, y, z0, z1, cor="#3b4651"):
        """Abertura na face virada para +y (a face clara)."""
        self.itens.append(((math.ceil(x1) + y + 0.001, 50), self.pol([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], cor)))

    def janela_x(self, x, y0, y1, z0, z1, cor="#2c343d"):
        self.itens.append(((x + math.ceil(y1) + 0.001, 50), self.pol([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], cor)))

    def boneco(self, x, y, cor, anel=True):
        u = self.u
        bx, by = self.p(x, y)
        s = f'<ellipse cx="{bx + 0.16 * u:.1f}" cy="{by + 0.05 * u:.1f}" rx="{0.42 * u:.1f}" ry="{0.2 * u:.1f}" fill="#000" opacity="0.22"/>'
        if anel:
            s += f'<ellipse cx="{bx:.1f}" cy="{by:.1f}" rx="{0.5 * u:.1f}" ry="{0.25 * u:.1f}" fill="none" stroke="{cor}" stroke-width="{0.05 * u:.2f}" opacity="0.9"/>'
        rx, ry, n, h = 0.3 * u, 0.15 * u, 0.13 * u, 0.74 * u
        s += (f'<path d="M{bx - rx:.1f} {by:.1f}L{bx - n:.1f} {by - h:.1f}L{bx + n:.1f} {by - h:.1f}L{bx + rx:.1f} {by:.1f}'
              f'A{rx:.1f} {ry:.1f} 0 0 1 {bx - rx:.1f} {by:.1f}Z" fill="{mix(cor, 0.84)}"/>')
        s += (f'<path d="M{bx:.1f} {by - h:.1f}L{bx + n:.1f} {by - h:.1f}L{bx + rx:.1f} {by:.1f}'
              f'A{rx:.1f} {ry:.1f} 0 0 1 {bx:.1f} {by + ry:.1f}Z" fill="{mix(cor, 0.66)}"/>')
        s += f'<circle cx="{bx:.1f}" cy="{by - h - 0.19 * u:.1f}" r="{0.25 * u:.1f}" fill="{cor}"/>'
        s += f'<circle cx="{bx - 0.07 * u:.1f}" cy="{by - h - 0.26 * u:.1f}" r="{0.08 * u:.1f}" fill="#fff" opacity="0.28"/>'
        self.itens.append(((x + y + 0.35, 0), s))

    def cone(self, x, y, graus, comp, cor, abre=24):
        self.n += 1
        pts = [(x, y, 0)]
        for i in range(9):
            a = math.radians(graus - abre + 2 * abre * i / 8)
            pts.append((x + comp * math.cos(a), y + comp * math.sin(a), 0))
        c, t = self.p(x, y), self.p(x + comp * math.cos(math.radians(graus)), y + comp * math.sin(math.radians(graus)))
        self.defs.append(f'<linearGradient id="cone{self.n}" gradientUnits="userSpaceOnUse" x1="{c[0]:.1f}" y1="{c[1]:.1f}" x2="{t[0]:.1f}" y2="{t[1]:.1f}">'
                         f'<stop offset="0" stop-color="{cor}" stop-opacity="0.62"/><stop offset="1" stop-color="{cor}" stop-opacity="0.04"/></linearGradient>')
        d = " ".join("%.1f,%.1f" % self.p(*q) for q in pts)
        self.chao.append(f'<polygon points="{d}" fill="url(#cone{self.n})"/>')

    def rota(self, pts, cor):
        u = self.u
        d = " ".join("%.1f,%.1f" % self.p(x, y) for x, y in pts)
        self.chao.append(f'<polyline points="{d}" fill="none" stroke="{cor}" stroke-width="{0.09 * u:.2f}" stroke-dasharray="{0.28 * u:.1f} {0.2 * u:.1f}" stroke-linejoin="round" opacity="0.95"/>')
        (x0, y0), (x1, y1) = pts[-2], pts[-1]
        a = math.atan2(y1 - y0, x1 - x0)
        tri = [(x1 + 0.42 * math.cos(a), y1 + 0.42 * math.sin(a), 0)] + [
            (x1 + 0.26 * math.cos(a + s * 2.1), y1 + 0.26 * math.sin(a + s * 2.1), 0) for s in (1, -1)]
        self.chao.append(self.pol(tri, cor))

    def fumaca(self, x, y, raio=1.0):
        u = self.u
        cx, cy = self.p(x, y, 0)
        s = f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="{1.25 * raio * u:.1f}" ry="{0.6 * raio * u:.1f}" fill="#000" opacity="0.18"/>'
        for dx, dy, r, cor in ((-0.62, -0.5, 0.62, "#c9ced5"), (0.6, -0.42, 0.66, "#bfc5cd"), (0.0, -0.36, 0.8, "#d5d9de"),
                               (-0.36, -1.0, 0.66, "#e3e6ea"), (0.42, -1.08, 0.6, "#dde0e5"), (0.02, -1.5, 0.56, "#eef0f2")):
            s += f'<circle cx="{cx + dx * raio * u:.1f}" cy="{cy + dy * raio * u:.1f}" r="{r * raio * u:.1f}" fill="{cor}"/>'
        self.itens.append(((x + y + 1.4, 0), s))

    def arvore(self, x, y):
        u = self.u
        self.caixa(x - 0.6, y - 0.6, x + 0.6, y + 0.6, 0, 0.45, PEDRA, dividir=False)
        cx, cy = self.p(x, y, 0.45)
        s = f'<rect x="{cx - 0.07 * u:.1f}" y="{cy - 1.0 * u:.1f}" width="{0.14 * u:.1f}" height="{1.0 * u:.1f}" fill="{MADEIRA2}"/>'
        for dx, dy, r, cor in ((-0.36, -1.25, 0.52, mix(VERDE, 0.78)), (0.34, -1.3, 0.5, mix(VERDE, 0.7)), (0.0, -1.72, 0.6, VERDE), (-0.1, -1.95, 0.3, mix(VERDE, 0.86, "#ffffff"))):
            s += f'<circle cx="{cx + dx * u:.1f}" cy="{cy + dy * u:.1f}" r="{r * u:.1f}" fill="{cor}"/>'
        self.itens.append(((x + y + 1.21, 1), s))

    def montar(self, bombsite="A"):
        u = self.u
        LX, LY = 16, 12
        # --- chão: ladrilho em xadrez bem suave, com a base da maquete por baixo
        g = [self.pol([(0, LY, -0.55), (LX, LY, -0.55), (LX, LY, 0), (0, LY, 0)], mix(BASE, 0.8)),
             self.pol([(LX, 0, -0.55), (LX, LY, -0.55), (LX, LY, 0), (LX, 0, 0)], mix(BASE, 0.58)),
             self.pol([(0, 0, 0), (LX, 0, 0), (LX, LY, 0), (0, LY, 0)], LADRILHO)]
        for i in range(LX):
            for j in range(LY):
                if (i + j) % 2:
                    g.append(self.pol([(i, j, 0), (i + 1, j, 0), (i + 1, j + 1, 0), (i, j + 1, 0)], LADRILHO2))
        # --- marcação do bombsite, pintada no chão
        a, b, c, d = 7.5, 3.2, 11.3, 6.6
        pts = " ".join("%.1f,%.1f" % self.p(x, y) for x, y in ((a, b), (c, b), (c, d), (a, d)))
        g.append(f'<polygon points="{pts}" fill="#fff" fill-opacity="0.16" stroke="#fffaf0" stroke-width="{0.07 * u:.2f}" stroke-dasharray="{0.4 * u:.1f} {0.22 * u:.1f}" stroke-linejoin="round"/>')
        e, f = 0.866 * u, 0.5 * u
        ox, oy = self.p(8.0, 5.45)
        g.append(f'<path d="{F800.curvas(bombsite, 1.7, 0, 0, 0, (e, f, e, -f, ox, oy))}" fill="{REJUNTE}" opacity="0.9"/>')
        self.chao = g + self.chao

        # --- construções ao fundo (não tapam a jogada)
        self.caixa(0, 0, 2.5, 2.5, 0, 4.3, CREME)                      # torre
        self.piramide(0, 0, 2.5, 2.5, 4.3, 5.7)
        self.janela_y(0.85, 1.65, 2.5, 2.6, 3.6)
        self.janela_x(2.5, 0.85, 1.65, 2.6, 3.6)
        self.caixa(2.5, 0, 9, 2.3, 0, 2.7, REBOCO)                     # casa comprida
        self.telhado_x(2.5, 9, 0, 2.3, 2.7, 3.7)
        for x in (3.3, 5.0, 7.6):
            self.janela_y(x, x + 0.8, 2.3, 1.25, 2.1)
        self.janela_y(6.05, 6.95, 2.3, 0, 1.75, MADEIRA2)              # porta
        self.caixa(0, 2.5, 2.3, 11, 0, 2.3, OCRE)                      # galpão lateral
        self.telhado_y(0, 2.3, 2.5, 11, 2.3, 3.2)
        for y in (3.6, 6.0, 8.6):
            self.janela_x(2.3, y, y + 0.9, 1.0, 1.8)
        self.caixa(9, 0, 11.3, 0.6, 0, 1.35, PEDRA)                    # muro, com a passagem do CT
        self.caixa(13.1, 0, 16, 0.6, 0, 1.35, PEDRA)
        # --- cobertura no bombsite
        self.caixa(5.6, 5.0, 6.8, 6.2, 0, 1.15, MADEIRA, dividir=False, borda=True)
        self.caixa(5.75, 5.15, 6.55, 5.95, 1.15, 1.9, mix(MADEIRA, 0.92), dividir=False, borda=True)
        self.caixa(9.4, 7.9, 11.6, 8.8, 0, 0.95, MADEIRA, dividir=False, borda=True)
        self.caixa(12.2, 3.6, 13.3, 4.7, 0, 1.6, TURQUESA, dividir=False, borda=True)
        self.caixa(4.3, 2.3, 5.2, 3.2, 0, 0.8, mix(MADEIRA, 0.9), dividir=False, borda=True)
        self.arvore(14.3, 9.9)

        # --- a jogada: o TR executa o bombsite pela esquerda, fumaça na passagem do CT
        self.rota([(1.2, 12.9), (3.6, 11.0), (5.4, 9.3), (7.4, 8.1)], LARANJA)
        self.rota([(6.0, 13.2), (7.6, 11.2), (9.0, 9.9)], LARANJA)
        self.fumaca(12.2, 1.1, 1.0)
        tr = [(7.9, 7.75, -52), (9.65, 9.65, -64), (5.2, 9.5, -40), (3.3, 11.2, -38), (7.3, 11.6, -55)]
        ct = [(10.4, 4.3, 118), (13.75, 5.2, 150), (7.1, 4.25, 96)]
        for x, y, g_ in tr:
            self.cone(x, y, g_, 3.6, LARANJA)
            self.boneco(x, y, LARANJA)
        for x, y, g_ in ct:
            self.cone(x, y, g_, 3.4, AZUL)
            self.boneco(x, y, AZUL)
        return self

    def svg(self):
        corpo = "".join(self.chao) + "".join(s for _, s in sorted(self.itens, key=lambda t: t[0]))
        sx, sy = self.p(8, 6, -0.55)
        sombra = f'<ellipse cx="{sx:.0f}" cy="{sy + 0.9 * self.u:.0f}" rx="{13.5 * self.u:.0f}" ry="{7.2 * self.u:.0f}" fill="#000" opacity="0.5" filter="url(#borra)"/>'
        return "".join(self.defs), sombra + corpo


# ---------------------------------------------------------------- peças
def grade(w, h, u, ox, oy):
    """Grade tática bem apagada no fundo, no mesmo ângulo da maquete."""
    s, k = [], int((w + h) / u) + 4
    for i in range(-k, k):
        x = ox + i * 2 * 0.866 * u
        s.append(f'<line x1="{x:.0f}" y1="0" x2="{x + h * 1.732:.0f}" y2="{h}"/><line x1="{x:.0f}" y1="0" x2="{x - h * 1.732:.0f}" y2="{h}"/>')
    return f'<g stroke="{OSSO}" stroke-width="1" opacity="0.045">{"".join(s)}</g>'


def documento(w, h, corpo, defs="", fundo=GRAFITE):
    f = f'<rect width="{w}" height="{h}" fill="{fundo}"/>' if fundo else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}">'
            f'<defs><filter id="borra" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="{max(w, h) / 34:.0f}"/></filter>{defs}</defs>'
            f'<clipPath id="q"><rect width="{w}" height="{h}"/></clipPath><g clip-path="url(#q)">{f}{corpo}</g></svg>')


def veu(w, h, x0, x1, vertical=False):
    """Escurece o lado onde a logo fica, para ela ler bem em cima da arte."""
    v = 'x1="0" y1="0" x2="0" y2="1"' if vertical else 'x1="0" y1="0" x2="1" y2="0"'
    d = (f'<linearGradient id="veu" {v}><stop offset="{x0}" stop-color="{GRAFITE}" stop-opacity="0.96"/>'
         f'<stop offset="{x1}" stop-color="{GRAFITE}" stop-opacity="0"/></linearGradient>')
    return d, f'<rect width="{w}" height="{h}" fill="url(#veu)"/>'


def arte(nome, w, h, u, ox, oy, logo=None, veu_=None, sub="ESPORTS MANAGER", frase=None):
    """Uma peça: fundo grafite + grade + maquete + (opcional) logo. `logo` = (tipo, tamanho, x, y)."""
    m = Maquete(u, ox, oy).montar()
    defs, cena = m.svg()
    corpo = grade(w, h, u, ox, oy) + cena
    if veu_:
        d, r = veu(w, h, *veu_)
        defs += d
        corpo += r
    if logo:
        tipo, tam, x, y = logo
        fn = logo_horizontal if tipo == "h" else logo_empilhada
        g, _, alt = fn(nome, tam, x, y, sub=sub)
        corpo += g
        if frase:   # só fora da Steam: lá a cápsula leva arte, nome e subtítulo, mais nada
            ft = 0.26 * tam
            for i, lin in enumerate(frase):
                corpo += f'<path d="{F600.curvas(lin, ft, x, y + alt + 0.75 * tam + i * ft * 1.5, 0.02)}" fill="{OSSO}"/>'
    return documento(w, h, corpo, defs)


def gravar(arq, svg):
    os.makedirs(SAIDA, exist_ok=True)
    with open(os.path.join(SAIDA, arq), "w", encoding="utf-8") as f:
        f.write(svg)


def painel(nome, num, linhas, u, ox, oy, v=(0.5, 0.82), w=616, h=300):
    """Faixa para a descrição da página da Steam ("Sobre este jogo"): um recorte da maquete e o título da seção."""
    m = Maquete(u, ox, oy).montar()
    defs, cena = m.svg()
    d, r = veu(w, h, *v)
    corpo = grade(w, h, u, ox, oy) + cena + r
    corpo += simbolo(26, 26, 40)
    corpo += f'<path d="{F600.curvas(num, 13, 76, 52, 0.16)}" fill="{SUAVE}"/>'
    t = 40
    y0 = h - 34 - (len(linhas) - 1) * t * 1.16
    for i, lin in enumerate(linhas):
        corpo += f'<path d="{F800.curvas(lin, t, 28, y0 + i * t * 1.16, 0.012)}" fill="{OSSO}"/>'
    return documento(w, h, corpo, defs + d)


NOME = "THE @ORG"


def main():
    nome = sys.argv[1] if len(sys.argv) > 1 else NOME

    # --- logo (texto em curvas): a principal é o nome + gênero; "completa" leva o brasão ao lado
    for cor, suf, sc, fundo in ((OSSO, "claro", SUAVE, None), (GRAFITE, "escuro", "#5b626c", None), (OSSO, "fundo", SUAVE, GRAFITE)):
        pt = LARANJA if cor == OSSO else "#d9822b"
        g, lw, lh = logo_empilhada(nome, 120, 60, 60, cor=cor, cor_sub=sc, ponto=pt)
        gravar(f"logo-{suf}.svg", documento(round(lw + 120), round(lh + 120), g, fundo=fundo))
        g, lw, lh = logo_horizontal(nome, 120, 60, 60, cor=cor, cor_sub=sc, ponto=pt)
        gravar(f"logo-completa-{suf}.svg", documento(round(lw + 120), round(lh + 120), g, fundo=fundo))
    # --- brasão e ícones
    gravar("simbolo.svg", documento(512, 512, simbolo(56, 50, 400), fundo=None))
    gravar("simbolo-escuro.svg", documento(512, 512, simbolo(56, 50, 400, bloco=GRAFITE, ponto="#d9822b"), fundo=None))
    gravar("icone-app.svg", documento(512, 512, simbolo(36, 28, 440, borda=GRAFITE), fundo=None))      # formato próprio, fundo transparente
    gravar("icone-quadrado.svg", documento(512, 512, simbolo(76, 70, 360)))                              # para onde o ícone tem de ser quadrado
    gravar("icone-quadrado-claro.svg", documento(512, 512, simbolo(76, 70, 360, bloco=GRAFITE, ponto="#d9822b"), fundo=OSSO))
    gravar("steam-icone-comunidade-184x184.svg", documento(184, 184, simbolo(27, 25, 130)))

    # --- Steam: loja (só arte + nome + subtítulo, como a Steam pede nas cápsulas)
    gravar("steam-cabecalho-920x430.svg", arte(nome, 920, 430, 22, 606, 122, ("e", 80, 40, 44)))
    gravar("steam-capsula-principal-1232x706.svg", arte(nome, 1232, 706, 34, 806, 208, ("e", 122, 64, 56)))
    gravar("steam-capsula-pequena-462x174.svg", arte(nome, 462, 174, 14, 480, 4, ("e", 62, 20, 32), (0.62, 0.84)))
    gravar("steam-capsula-vertical-748x896.svg", arte(nome, 748, 896, 30, 340, 420, ("e", 134, 56, 64)))
    gravar("steam-fundo-da-pagina-1438x810.svg", arte(nome, 1438, 810, 50, 700, 150))
    # --- Steam: biblioteca
    gravar("steam-biblioteca-capsula-600x900.svg", arte(nome, 600, 900, 25, 268, 440, ("e", 106, 46, 60)))
    gravar("steam-biblioteca-heroi-3840x1240.svg", arte(nome, 3840, 1240, 78, 1880, 300))
    g, lw, lh = logo_empilhada(nome, 250, 0, 0)
    g, lw, lh = logo_empilhada(nome, 250, (1280 - lw) / 2, (720 - lh) / 2)
    gravar("steam-biblioteca-logo-1280x720.svg", documento(1280, 720, g, fundo=None))
    # --- Steam: faixas da descrição da página
    gravar("steam-painel-1-transmissao-616x300.svg", painel(nome, "01", ["ASSISTA COMO", "TRANSMISSÃO"], 20, 500, 36))
    gravar("steam-painel-2-bots-616x300.svg", painel(nome, "02", ["BOTS QUE JOGAM", "COMO PROFISSIONAIS"], 40, 330, -150, (0.62, 0.96)))
    gravar("steam-painel-3-gestao-616x300.svg", painel(nome, "03", ["SUA GESTÃO", "APARECE NA PARTIDA"], 30, 700, 96, (0.62, 0.92)))
    # --- redes (aqui cabe a frase)
    frase = ["VOCÊ MONTA O TIME.", "ELES JOGAM COMO PROFISSIONAIS."]
    gravar("banner-social-1500x500.svg", arte(nome, 1500, 500, 27, 1090, 112, ("e", 100, 80, 90), frase=frase))
    gravar("quadrado-social-1080x1080.svg", arte(nome, 1080, 1080, 42, 470, 462, ("e", 160, 80, 70)))
    print("ok:", len(os.listdir(SAIDA)), "arquivos em", SAIDA, "· nome usado:", limpo(nome))


if __name__ == "__main__":
    main()
