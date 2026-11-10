#!/usr/bin/env python3
"""Gera a logo e as artes de divulgação do jogo em SVG (texto já convertido em curvas).

Uso:  python3 marketing/identidade/gerar.py              # gera tudo em marketing/identidade/saida/
      python3 marketing/identidade/gerar.py "OUTRO NOME"  # troca o nome em todas as peças

Depende de: fonttools e uharfbuzz (pip install fonttools uharfbuzz).
Os PNG saem com `node marketing/identidade/render.js` (precisa do Playwright).

Tudo aqui é desenho vetorial feito em código: nada de imagem gerada por modelo.
As cores vêm da interface (decisão 0025): grafite, osso, laranja (TR) e azul (CT).
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
                t = (e * xx, e * xy, e * yx, e * yy, xx * gx + yx * gy + dx, xy * gx + yy * gy + dy)
            self.gs[nome].draw(TransformPen(pen, t))
            cx += av * e + track * tam
        return pen.getCommands()


F800 = Fonte("BarlowCondensed-ExtraBold.ttf")
F600 = Fonte("BarlowCondensed-SemiBold.ttf")


FMONO = Fonte("IBMPlexMono-Medium.ttf")


# ---------------------------------------------------------------- formas básicas
def lente(c1x, cy, r1, c2x, r2):
    """A parte do ponto (centro c2x, raio r2) que aparece dentro da janela (centro c1x, raio r1)."""
    d = c2x - c1x
    x = (d * d + r1 * r1 - r2 * r2) / (2 * d)
    y = math.sqrt(r1 * r1 - x * x)
    return (f'M{c1x + x:.2f} {cy - y:.2f}A{r1:.2f} {r1:.2f} 0 0 1 {c1x + x:.2f} {cy + y:.2f}'
            f'A{r2:.2f} {r2:.2f} 0 0 1 {c1x + x:.2f} {cy - y:.2f}Z')


def circulo(cx, cy, r):
    return f'M{cx - r:.2f} {cy:.2f}A{r:.2f} {r:.2f} 0 1 0 {cx + r:.2f} {cy:.2f}A{r:.2f} {r:.2f} 0 1 0 {cx - r:.2f} {cy:.2f}Z'


def polar(cx, cy, r, graus):
    a = math.radians(graus)
    return cx + r * math.cos(a), cy + r * math.sin(a)


# ---------------------------------------------------------------- símbolo (três opções de ícone)
def simbolo_anel(x, y, s, cor=OSSO, ponto=LARANJA):
    """A: o O da logo sozinho. O anel é a janela; o ponto laranja aparece só o necessário atrás dela."""
    cx, cy, R, t = x + 0.5 * s, y + 0.5 * s, 0.42 * s, 0.16 * s
    return (f'<path d="{circulo(cx, cy, R)}{circulo(cx, cy, R - t)}" fill="{cor}" fill-rule="evenodd"/>'
            f'<path d="{lente(cx, cy, R - t, cx + 0.14 * s, 0.2 * s)}" fill="{ponto}"/>')


def simbolo_cone(x, y, s, cor=OSSO, ponto=LARANJA):
    """B: o marcador de um bot no mapa. O ponto no meio, o cone de visão dele abrindo um vão no O."""
    cx, cy, R, t = x + 0.5 * s, y + 0.5 * s, 0.42 * s, 0.14 * s
    a0, a1 = -62, -14                                   # o cone aponta para cima e para a direita
    o0, o1, i0, i1 = polar(cx, cy, R, a0), polar(cx, cy, R, a1), polar(cx, cy, R - t, a0), polar(cx, cy, R - t, a1)
    anel = (f'M{o1[0]:.2f} {o1[1]:.2f}A{R:.2f} {R:.2f} 0 1 1 {o0[0]:.2f} {o0[1]:.2f}L{i0[0]:.2f} {i0[1]:.2f}'
            f'A{R - t:.2f} {R - t:.2f} 0 1 0 {i1[0]:.2f} {i1[1]:.2f}Z')
    f0, f1 = polar(cx, cy, 0.6 * s, a0 + 5), polar(cx, cy, 0.6 * s, a1 - 5)
    cone = f'M{cx:.2f} {cy:.2f}L{f0[0]:.2f} {f0[1]:.2f}A{0.6 * s:.2f} {0.6 * s:.2f} 0 0 1 {f1[0]:.2f} {f1[1]:.2f}Z'
    fm = polar(cx, cy, 0.6 * s, (a0 + a1) / 2)
    gid = f"sc{int(x)}{int(y)}{int(s)}{ponto[1:]}"
    grad = (f'<linearGradient id="{gid}" gradientUnits="userSpaceOnUse" x1="{cx:.1f}" y1="{cy:.1f}" x2="{fm[0]:.1f}" y2="{fm[1]:.1f}">'
            f'<stop offset="0.15" stop-color="{ponto}" stop-opacity="0.95"/><stop offset="1" stop-color="{ponto}" stop-opacity="0.25"/></linearGradient>')
    return (f'{grad}<path d="{cone}" fill="url(#{gid})"/><path d="{anel}" fill="{cor}"/>'
            f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{0.115 * s:.2f}" fill="{ponto}"/>')


def simbolo_formacao(x, y, s, cor=OSSO, ponto=LARANJA):
    """C: os cinco titulares em formação de cunha, avançando. Quem vai na ponta é o ponto laranja."""
    r = 0.098 * s
    pts = [(0.25, 0.3), (0.475, 0.3), (0.7, 0.525), (0.7, 0.75)]
    g = "".join(f'<circle cx="{x + a * s:.2f}" cy="{y + b * s:.2f}" r="{r:.2f}" fill="{cor}"/>' for a, b in pts)
    return g + f'<circle cx="{x + 0.7 * s:.2f}" cy="{y + 0.3 * s:.2f}" r="{r * 1.22:.2f}" fill="{ponto}"/>'


SIMBOLOS = {"a-anel": simbolo_anel, "b-cone": simbolo_cone, "c-formacao": simbolo_formacao}
simbolo = simbolo_cone          # o que vai nas peças; troque aqui se a dupla escolher outro


def icone(fn, s=512, fundo=GRAFITE, cor=OSSO, ponto=LARANJA, raio=0.22):
    """Ícone do app: quadrado de cantos arredondados com o símbolo dentro."""
    return (f'<rect width="{s}" height="{s}" rx="{raio * s:.1f}" fill="{fundo}"/>' + fn(0.14 * s, 0.14 * s, 0.72 * s, cor, ponto))


# ---------------------------------------------------------------- logo
def palavra(nome, tam, x, y, cor=OSSO, ponto=LARANJA):
    """Nome do jogo em Barlow Condensed ExtraBold. Um "@" troca a letra seguinte (um O) pelo anel com o ponto
    espiando por dentro. Devolve (svg, largura)."""
    track = 0.012
    out, cx = [], x
    for p in nome.replace("@", "\0@").split("\0"):
        cy = y - 0.5 * tam
        if p.startswith("@"):
            p = p[2:]
            R, t = 0.515 * tam, 0.2 * tam
            cx += 0.035 * tam
            out.append(f'<path d="{circulo(cx + R, cy, R)}{circulo(cx + R, cy, R - t)}" fill="{cor}" fill-rule="evenodd"/>')
            out.append(f'<path d="{lente(cx + R, cy, R - t, cx + R + 0.17 * tam, 0.245 * tam)}" fill="{ponto}"/>')
            cx += 2 * R + 0.06 * tam
        if p:
            out.append(f'<path d="{F800.curvas(p, tam, cx, y, track)}" fill="{cor}"/>')
            cx += F800.largura(p, tam, track) + track * tam
    return "".join(out), cx - x


def limpo(nome):
    return nome.replace("@", "")


def subtitulo(txt, tam, x, y, cor=SUAVE):
    return f'<path d="{F600.curvas(txt, tam, x, y, 0.16)}" fill="{cor}"/>'


def logo(nome, tam, x, y, cor=OSSO, sub="ESPORTS MANAGER", cor_sub=SUAVE, ponto=LARANJA):
    """A logo: o nome grande e o gênero embaixo. (x, y) = canto de cima. Devolve (svg, largura, altura)."""
    g, lw = palavra(nome, tam, x, y + tam, cor, ponto)
    alt = tam
    if sub:
        st = 0.19 * tam
        alt += 0.3 * tam + st
        g += subtitulo(sub, st, x + 0.01 * tam, y + alt, cor_sub)
    return g, lw, alt


# ---------------------------------------------------------------- a prancheta tática
PISO, PISO_L, PISO_E = "#232b35", "#171c23", "#11151b"
CAIXA = "#4a5563"


class Prancheta:
    """Um round visto de cima, como na mesa de quem analisa a partida: o mapa em blocos de grafite, os bots laranja
    (TR) e azuis (CT) com o cone de visão, o caminho que cada um fez, a rota combinada, as utilitárias e, quando
    `tags` está ligado, o que cada bot está fazendo. Coordenadas em quadras; o canto (0,0) fica ao fundo.
    O mapa é inventado para a arte: não é nenhum mapa do jogo nem de outro jogo."""

    def __init__(self, u, ox, oy, tags=False):
        self.u, self.ox, self.oy, self.tags = u, ox, oy, tags
        self.chao, self.itens, self.topo, self.defs = [], [], [], []
        self.n = 0

    def p(self, x, y, z=0.0):
        return (self.ox + (x - y) * 0.866 * self.u, self.oy + (x + y) * 0.5 * self.u - z * self.u)

    def pts(self, lista):
        return " ".join("%.1f,%.1f" % self.p(*q) for q in lista)

    def pol(self, lista, cor, extra=""):
        return f'<polygon points="{self.pts(lista)}" fill="{cor}" stroke="{cor}" stroke-width="0.7" stroke-linejoin="round"{extra}/>'

    def piso(self, salas, h=0.35):
        lados, tampos, linhas = [], [], []
        for x, y, w, d in salas:      # a beirada clara por fora de tudo; os tampos cobrem o que cai para dentro
            lados.append(f'<polygon points="{self.pts([(x, y, 0), (x + w, y, 0), (x + w, y + d, 0), (x, y + d, 0)])}" fill="none" stroke="{OSSO}" stroke-opacity="0.34" stroke-width="2.2" stroke-linejoin="round"/>')
        for x, y, w, d in salas:
            lados.append(self.pol([(x, y + d, -h), (x + w, y + d, -h), (x + w, y + d, 0), (x, y + d, 0)], PISO_L))
            lados.append(self.pol([(x + w, y, -h), (x + w, y + d, -h), (x + w, y + d, 0), (x + w, y, 0)], PISO_E))
            tampos.append(self.pol([(x, y, 0), (x + w, y, 0), (x + w, y + d, 0), (x, y + d, 0)], PISO))
            for i in range(1, w):
                a, b = self.p(x + i, y), self.p(x + i, y + d)
                linhas.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}"/>')
            for j in range(1, d):
                a, b = self.p(x, y + j), self.p(x + w, y + j)
                linhas.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}"/>')
        self.chao += lados + tampos + [f'<g stroke="{OSSO}" stroke-width="1" opacity="0.07">{"".join(linhas)}</g>']

    def caixa(self, x0, y0, x1, y1, h, cor=CAIXA):
        s = self.pol([(x0, y1, 0), (x1, y1, 0), (x1, y1, h), (x0, y1, h)], mix(cor, 0.74))
        s += self.pol([(x1, y0, 0), (x1, y1, 0), (x1, y1, h), (x1, y0, h)], mix(cor, 0.52))
        s += self.pol([(x0, y0, h), (x1, y0, h), (x1, y1, h), (x0, y1, h)], cor)
        self.itens.append((x1 + y1, s))

    def bombsite(self, x0, y0, x1, y1, letra):
        u = self.u
        self.chao.append(f'<polygon points="{self.pts([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])}" fill="{OSSO}" fill-opacity="0.045" '
                         f'stroke="{OSSO}" stroke-opacity="0.5" stroke-width="{0.055 * u:.2f}" stroke-dasharray="{0.34 * u:.1f} {0.2 * u:.1f}"/>')
        e, f = 0.866 * u, 0.5 * u
        ox, oy = self.p(x0 + 0.5, y1 - 0.55)
        self.chao.append(f'<path d="{F800.curvas(letra, 1.5, 0, 0, 0, (e, f, e, -f, ox, oy))}" fill="{OSSO}" opacity="0.42"/>')

    def cone(self, x, y, graus, comp, cor, abre=24):
        self.n += 1
        lista = [(x, y, 0)] + [(x + comp * math.cos(math.radians(graus - abre + 2 * abre * i / 8)),
                                y + comp * math.sin(math.radians(graus - abre + 2 * abre * i / 8)), 0) for i in range(9)]
        c, t = self.p(x, y), self.p(x + comp * math.cos(math.radians(graus)), y + comp * math.sin(math.radians(graus)))
        self.defs.append(f'<linearGradient id="cone{self.n}" gradientUnits="userSpaceOnUse" x1="{c[0]:.1f}" y1="{c[1]:.1f}" x2="{t[0]:.1f}" y2="{t[1]:.1f}">'
                         f'<stop offset="0" stop-color="{cor}" stop-opacity="0.6"/><stop offset="1" stop-color="{cor}" stop-opacity="0.03"/></linearGradient>')
        self.chao.append(f'<polygon points="{self.pts(lista)}" fill="url(#cone{self.n})"/>')

    def trilha(self, lista, cor):
        """Por onde o bot já passou."""
        self.chao.append(f'<polyline points="{self.pts(lista)}" fill="none" stroke="{cor}" stroke-width="{0.075 * self.u:.2f}" '
                         f'stroke-linejoin="round" stroke-linecap="round" opacity="0.6"/>')

    def rota(self, lista, cor):
        """Para onde ele vai: a jogada combinada."""
        u = self.u
        self.chao.append(f'<polyline points="{self.pts(lista)}" fill="none" stroke="{cor}" stroke-width="{0.085 * u:.2f}" '
                         f'stroke-dasharray="{0.26 * u:.1f} {0.2 * u:.1f}" stroke-linejoin="round" opacity="0.95"/>')
        (x0, y0), (x1, y1) = lista[-2], lista[-1]
        a = math.atan2(y1 - y0, x1 - x0)
        tri = [(x1 + 0.5 * math.cos(a), y1 + 0.5 * math.sin(a), 0)] + [
            (x1 + 0.3 * math.cos(a + s * 2.1), y1 + 0.3 * math.sin(a + s * 2.1), 0) for s in (1, -1)]
        self.chao.append(self.pol(tri, cor))

    def bot(self, x, y, cor, graus, comp=3.4, tag=None, abre=24, lado=1):
        u = self.u
        self.cone(x, y, graus, comp, cor, abre)
        bx, by = self.p(x, y)
        s = f'<ellipse cx="{bx:.1f}" cy="{by + 0.1 * u:.1f}" rx="{0.5 * u:.1f}" ry="{0.25 * u:.1f}" fill="{mix(cor, 0.45)}"/>'
        s += f'<ellipse cx="{bx:.1f}" cy="{by:.1f}" rx="{0.5 * u:.1f}" ry="{0.25 * u:.1f}" fill="{cor}" stroke="{GRAFITE}" stroke-width="{0.06 * u:.2f}"/>'
        self.itens.append((x + y + 0.3, s))
        if tag and self.tags:
            self.etiqueta(bx, by, tag, cor, lado)

    def etiqueta(self, bx, by, txt, cor, lado=1):
        """O que o bot está fazendo, como a transmissão anotaria."""
        u = self.u
        t = max(8.5, 0.34 * u)
        lw = FMONO.largura(txt, t, 0.06)
        w, h = lw + 1.5 * t, 2.3 * t
        ty = by - 1.5 * u
        tx = bx + 0.5 * u if lado > 0 else bx - 0.5 * u - w
        ax = tx if lado > 0 else tx + w
        self.topo.append(f'<path d="M{bx:.1f} {by - 0.22 * u:.1f}L{bx:.1f} {ty + h / 2:.1f}L{ax:.1f} {ty + h / 2:.1f}" fill="none" stroke="{cor}" stroke-width="1.2" opacity="0.9"/>'
                         f'<rect x="{tx:.1f}" y="{ty:.1f}" width="{w:.1f}" height="{h:.1f}" rx="2" fill="{GRAFITE}" stroke="{cor}" stroke-width="1.2"/>'
                         f'<path d="{FMONO.curvas(txt, t, tx + 0.75 * t, ty + h / 2 + t / 2, 0.06)}" fill="{OSSO}"/>')

    def morto(self, x, y, cor):
        u, (bx, by) = self.u, self.p(x, y)
        a, b = 0.3 * u, 0.15 * u
        self.chao.append(f'<path d="M{bx - a:.1f} {by - b:.1f}L{bx + a:.1f} {by + b:.1f}M{bx - a:.1f} {by + b:.1f}L{bx + a:.1f} {by - b:.1f}" '
                         f'stroke="{cor}" stroke-width="{0.1 * u:.2f}" stroke-linecap="round" opacity="0.65"/>')

    def smoke(self, x, y, r=1.5):
        u, (cx, cy) = self.u, self.p(x, y)
        s = f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="{1.22 * r * u:.1f}" ry="{0.61 * r * u:.1f}" fill="{OSSO}" fill-opacity="0.3" stroke="{OSSO}" stroke-opacity="0.7" stroke-width="{0.05 * u:.2f}"/>'
        s += f'<path d="M{cx - 1.22 * r * u:.1f} {cy:.1f}A{1.22 * r * u:.1f} {1.0 * r * u:.1f} 0 0 1 {cx + 1.22 * r * u:.1f} {cy:.1f}" fill="{OSSO}" fill-opacity="0.2" stroke="{OSSO}" stroke-opacity="0.4" stroke-width="{0.04 * u:.2f}"/>'
        self.itens.append((x + y + r, s))

    def flash(self, x, y):
        u, (cx, cy) = self.u, self.p(x, y, 1.2)
        raios = "".join(f'M{polar(cx, cy, 0.22 * u, a)[0]:.1f} {polar(cx, cy, 0.22 * u, a)[1]:.1f}L{polar(cx, cy, 0.55 * u, a)[0]:.1f} {polar(cx, cy, 0.55 * u, a)[1]:.1f}' for a in range(0, 360, 45))
        self.topo.append(f'<path d="{raios}" stroke="{OSSO}" stroke-width="{0.07 * u:.2f}" stroke-linecap="round"/><circle cx="{cx:.1f}" cy="{cy:.1f}" r="{0.13 * u:.1f}" fill="{OSSO}"/>')

    def arco(self, a, b, alto=3.0, z1=0.0):
        """O arremesso de uma utilitária."""
        u = self.u
        p0, p1 = self.p(a[0], a[1], 0.5), self.p(b[0], b[1], z1)
        pm = self.p((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, alto)
        self.topo.append(f'<path d="M{p0[0]:.1f} {p0[1]:.1f}Q{pm[0]:.1f} {pm[1]:.1f} {p1[0]:.1f} {p1[1]:.1f}" fill="none" stroke="{OSSO}" '
                         f'stroke-width="{0.05 * u:.2f}" stroke-dasharray="{0.12 * u:.1f} {0.16 * u:.1f}" opacity="0.75"/>')

    def montar(self):
        # --- o mapa: base do TR à esquerda, do CT à direita, dois bombsites e o meio
        self.piso([(1, 8, 5, 5), (4, 2, 3, 7), (4, 2, 13, 3), (6, 9, 8, 3), (13, 6, 4, 8), (16, 1, 9, 7),
                   (3, 13, 3, 5), (3, 16, 15, 3), (17, 13, 8, 7), (26, 8, 5, 5), (23, 6, 5, 3), (24, 12, 4, 3), (15, 12, 3, 2)])
        self.bombsite(18, 2, 24, 7, "A")
        self.bombsite(18, 14, 24, 19, "B")
        for c in ((19.2, 2.6, 20.4, 3.8, 1.1), (21.4, 4.9, 23.4, 5.9, 0.8), (17.2, 5.6, 18.2, 6.6, 1.4),
                  (20, 15, 21.2, 16.2, 1.1), (22, 17.5, 24, 18.5, 0.8), (14.6, 9.4, 15.6, 10.4, 1.0), (9.5, 2.2, 10.5, 3.0, 0.9)):
            self.caixa(*c)

        # --- o round: o TR executa o A. Dois entram juntos, um abre o pixel pelo meio, um finge o B.
        self.trilha([(3.5, 10), (5.3, 8.2), (5.5, 3.6), (11, 3.4), (15.3, 3.4)], LARANJA)
        self.trilha([(3, 11), (5, 8.6), (5.2, 4.2), (9, 4.0), (13.2, 4.1)], LARANJA)
        self.trilha([(4.5, 11), (7, 10.5), (13.4, 10.6), (14.4, 8.3)], LARANJA)
        self.trilha([(3.5, 12.4), (4.5, 16), (5, 17.6), (9.6, 17.6)], LARANJA)
        self.rota([(15.9, 3.4), (18.3, 3.5), (20.6, 4.6)], LARANJA)
        self.rota([(14.6, 7.6), (15.2, 6.9), (16.6, 6.6)], LARANJA)
        self.smoke(24.6, 7.2, 1.45)
        self.arco((13.2, 4.1), (24.6, 7.2), 4.6)
        self.flash(18.9, 1.9)
        self.arco((15.3, 3.4), (18.9, 1.9), 2.4, 1.2)
        self.bot(15.3, 3.4, LARANJA, 8, 3.6, "ENTRA NO FLASH")
        self.bot(13.2, 4.1, LARANJA, 2, 2.6, "COBRE A TROCA", lado=-1)
        self.bot(14.4, 8.3, LARANJA, -38, 3.4, "ABRE O PIXEL", abre=11, lado=-1)
        self.bot(9.6, 17.6, LARANJA, 0, 3.6, "FINGE O B")
        self.bot(10.6, 3.5, LARANJA, 0, 2.2)
        # --- o CT reage: segura o ângulo, cruza fogo e rotaciona do B
        self.rota([(22.6, 15.2), (25.4, 13.6), (26.6, 10.6), (25.2, 8.2)], AZUL)
        self.bot(21.0, 3.9, AZUL, 186, 3.6, "SEGURA O ÂNGULO")
        self.bot(23.6, 2.0, AZUL, 162, 3.8)
        self.bot(22.0, 15.6, AZUL, -30, 2.2, "ROTACIONA")
        self.bot(19.2, 17.4, AZUL, 180, 3.6)
        self.morto(16.8, 9.2, AZUL)
        return self

    def svg(self):
        corpo = "".join(self.chao) + "".join(s for _, s in sorted(self.itens, key=lambda t: t[0])) + "".join(self.topo)
        return "".join(self.defs), corpo


# ---------------------------------------------------------------- peças
def grade(w, h, u, ox, oy):
    """Grade tática bem apagada no fundo, no mesmo ângulo da prancheta."""
    s, k = [], int((w + h) / u) + 4
    for i in range(-k, k):
        x = ox + i * 2 * 0.866 * u
        s.append(f'<line x1="{x:.0f}" y1="0" x2="{x + h * 1.732:.0f}" y2="{h}"/><line x1="{x:.0f}" y1="0" x2="{x - h * 1.732:.0f}" y2="{h}"/>')
    return f'<g stroke="{OSSO}" stroke-width="1" opacity="0.035">{"".join(s)}</g>'


def documento(w, h, corpo, defs="", fundo=GRAFITE):
    f = f'<rect width="{w}" height="{h}" fill="{fundo}"/>' if fundo else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}"><defs>{defs}</defs>'
            f'<clipPath id="q"><rect width="{w}" height="{h}"/></clipPath><g clip-path="url(#q)">{f}{corpo}</g></svg>')


def veu(w, h, x0, x1, vertical=False):
    """Escurece o lado onde a logo fica, para ela ler bem em cima da arte."""
    v = 'x1="0" y1="0" x2="0" y2="1"' if vertical else 'x1="0" y1="0" x2="1" y2="0"'
    d = (f'<linearGradient id="veu" {v}><stop offset="{x0}" stop-color="{GRAFITE}" stop-opacity="0.97"/>'
         f'<stop offset="{x1}" stop-color="{GRAFITE}" stop-opacity="0"/></linearGradient>')
    return d, f'<rect width="{w}" height="{h}" fill="url(#veu)"/>'


def escalacao(x, y, k=1.0):
    """O lado manager: os cinco titulares, a função de cada um e o geral. Números de exemplo."""
    g = f'<path d="{FMONO.curvas("ESCALAÇÃO", 9 * k, x, y, 0.14)}" fill="{SUAVE}"/>'
    for i, (func, geral, cor) in enumerate((("ENTRY", 84, LARANJA), ("SNIPER", 88, OSSO), ("IGL", 79, OSSO), ("SUPORTE", 81, OSSO), ("LURKER", 86, OSSO))):
        ly = y + (18 + i * 40) * k
        n = str(geral)
        g += f'<rect x="{x:.1f}" y="{ly:.1f}" width="{30 * k:.1f}" height="{30 * k:.1f}" rx="{2 * k:.1f}" fill="{cor}"/>'
        g += f'<path d="{F800.curvas(n, 15 * k, x + (30 * k - F800.largura(n, 15 * k)) / 2, ly + 22.5 * k)}" fill="{GRAFITE}"/>'
        g += f'<path d="{F600.curvas(func, 11.5 * k, x + 42 * k, ly + 13 * k, 0.1)}" fill="{OSSO}"/>'
        g += f'<rect x="{x + 42 * k:.1f}" y="{ly + 21 * k:.1f}" width="{120 * k:.1f}" height="{3.5 * k:.1f}" fill="{LINHA}"/>'
        g += f'<rect x="{x + 42 * k:.1f}" y="{ly + 21 * k:.1f}" width="{120 * k * geral / 100:.1f}" height="{3.5 * k:.1f}" fill="{cor}"/>'
    return g


def arte(nome, w, h, u, foco, em, logo_=None, veu_=None, tags=False, frase=None, time=None):
    """Uma peça: grafite + grade + prancheta + (opcional) logo. `foco` é a quadra do mapa que cai no ponto `em` da tela;
    `logo_` = (tamanho, x, y); `time` = (x, y, escala) põe a escalação."""
    ox = em[0] - (foco[0] - foco[1]) * 0.866 * u
    oy = em[1] - (foco[0] + foco[1]) * 0.5 * u
    defs, cena = Prancheta(u, ox, oy, tags).montar().svg()
    corpo = grade(w, h, u, ox, oy) + cena
    if veu_:
        d, r = veu(w, h, *veu_)
        defs += d
        corpo += r
    if logo_:
        tam, x, y = logo_
        g, _, alt = logo(nome, tam, x, y)
        corpo += g
        if frase:   # só fora da Steam: lá a cápsula leva arte, nome e subtítulo, mais nada
            ft = 0.26 * tam
            for i, lin in enumerate(frase):
                corpo += f'<path d="{F600.curvas(lin, ft, x, y + alt + 0.75 * tam + i * ft * 1.5, 0.02)}" fill="{OSSO}"/>'
    if time:
        corpo += escalacao(*time)
    return documento(w, h, corpo, defs)


def painel(num, linhas, u, foco, em, v=(0.42, 0.8), time=None, w=616, h=300):
    """Faixa para a descrição da página da Steam ("Sobre este jogo"): um recorte da prancheta e o título da seção."""
    ox = em[0] - (foco[0] - foco[1]) * 0.866 * u
    oy = em[1] - (foco[0] + foco[1]) * 0.5 * u
    defs, cena = Prancheta(u, ox, oy, True).montar().svg()
    d, r = veu(w, h, *v)
    corpo = grade(w, h, u, ox, oy) + cena + r
    corpo += simbolo(24, 22, 40) + f'<path d="{FMONO.curvas(num, 11, 74, 47, 0.14)}" fill="{SUAVE}"/>'
    t = 38
    y0 = h - 32 - (len(linhas) - 1) * t * 1.16
    for i, lin in enumerate(linhas):
        corpo += f'<path d="{F800.curvas(lin, t, 28, y0 + i * t * 1.16, 0.012)}" fill="{OSSO}"/>'
    if time:
        corpo += escalacao(*time)
    return documento(w, h, corpo, defs + d)


def gravar(arq, svg):
    os.makedirs(SAIDA, exist_ok=True)
    with open(os.path.join(SAIDA, arq), "w", encoding="utf-8") as f:
        f.write(svg)


NOME = "THE @ORG"
A = (17.5, 4.5)      # a entrada do bombsite A, onde a jogada acontece


def main():
    nome = sys.argv[1] if len(sys.argv) > 1 else NOME

    # --- logo (texto em curvas)
    for cor, suf, sc, fundo in ((OSSO, "claro", SUAVE, None), (GRAFITE, "escuro", "#5b626c", None), (OSSO, "fundo", SUAVE, GRAFITE)):
        g, lw, lh = logo(nome, 120, 60, 60, cor=cor, cor_sub=sc, ponto=LARANJA if cor == OSSO else "#d9822b")
        gravar(f"logo-{suf}.svg", documento(round(lw + 120), round(lh + 120), g, fundo=fundo))
    # --- ícone do app: três opções para escolher; "icone-app" é a que as peças usam
    for chave, fn in SIMBOLOS.items():
        gravar(f"icone-{chave}.svg", documento(512, 512, icone(fn), fundo=None))
        gravar(f"icone-{chave}-claro.svg", documento(512, 512, icone(fn, fundo=OSSO, cor=GRAFITE, ponto="#d9822b"), fundo=None))
    gravar("icone-app.svg", documento(512, 512, icone(simbolo), fundo=None))
    gravar("simbolo.svg", documento(512, 512, simbolo(36, 36, 440), fundo=None))
    gravar("steam-icone-comunidade-184x184.svg", documento(184, 184, icone(simbolo, 184, raio=0)))

    # --- Steam: loja (só arte + nome + subtítulo, como a Steam pede nas cápsulas)
    gravar("steam-cabecalho-920x430.svg", arte(nome, 920, 430, 30, A, (700, 330), (80, 40, 44), (0.2, 0.52)))
    gravar("steam-capsula-principal-1232x706.svg", arte(nome, 1232, 706, 37, A, (800, 420), (122, 64, 56), (0.14, 0.44)))
    gravar("steam-capsula-pequena-462x174.svg", arte(nome, 462, 174, 19, A, (420, 118), (62, 20, 32), (0.52, 0.8)))
    gravar("steam-capsula-vertical-748x896.svg", arte(nome, 748, 896, 38, A, (400, 560), (134, 56, 64), (0.2, 0.42, True)))
    gravar("steam-fundo-da-pagina-1438x810.svg", arte(nome, 1438, 810, 40, (15, 9), (719, 400), tags=True))
    # --- Steam: biblioteca
    gravar("steam-biblioteca-capsula-600x900.svg", arte(nome, 600, 900, 34, A, (320, 570), (106, 46, 60), (0.2, 0.42, True)))
    gravar("steam-biblioteca-heroi-3840x1240.svg", arte(nome, 3840, 1240, 96, (15, 8), (1920, 600)))
    g, lw, lh = logo(nome, 250, 0, 0)
    g, lw, lh = logo(nome, 250, (1280 - lw) / 2, (720 - lh) / 2)
    gravar("steam-biblioteca-logo-1280x720.svg", documento(1280, 720, g, fundo=None))
    # --- Steam: faixas da descrição da página
    gravar("steam-painel-1-transmissao-616x300.svg", painel("01", ["ASSISTA COMO", "TRANSMISSÃO"], 15, (16, 9), (430, 128), (0.36, 0.7)))
    gravar("steam-painel-2-bots-616x300.svg", painel("02", ["BOTS QUE JOGAM", "COMO PROFISSIONAIS"], 30, (17, 4.6), (370, 122), (0.3, 0.62)))
    gravar("steam-painel-3-gestao-616x300.svg", painel("03", ["SUA GESTÃO", "APARECE NA PARTIDA"], 22, (20, 5), (230, 110), (0.3, 0.6), time=(446, 30, 0.8)))
    # --- redes (aqui cabem a frase e as etiquetas)
    frase = ["VOCÊ MONTA O TIME.", "ELES JOGAM COMO PROFISSIONAIS."]
    gravar("banner-social-1500x500.svg", arte(nome, 1500, 500, 32, A, (1130, 270), (100, 80, 90), (0.3, 0.52), tags=True, frase=frase))
    gravar("quadrado-social-1080x1080.svg", arte(nome, 1080, 1080, 48, A, (600, 690), (160, 80, 70), (0.2, 0.4, True), tags=True))
    print("ok:", len(os.listdir(SAIDA)), "arquivos em", SAIDA, "· nome usado:", limpo(nome))


if __name__ == "__main__":
    main()
