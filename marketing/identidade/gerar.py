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


# ---------------------------------------------------------------- símbolo e ícone
def simbolo(x, y, s, cor=OSSO, ponto=LARANJA):
    """O símbolo é o O da logo sozinho: um anel (a janela, o radar) com o ponto aparecendo só o necessário por dentro.
    Uma forma geométrica só, que funciona numa cor e em 16 px, como as marcas de jogo que deram certo."""
    cx, cy, R, t = x + 0.5 * s, y + 0.5 * s, 0.44 * s, 0.17 * s
    return (f'<path d="{circulo(cx, cy, R)}{circulo(cx, cy, R - t)}" fill="{cor}" fill-rule="evenodd"/>'
            f'<path d="{lente(cx, cy, R - t, cx + 0.145 * s, 0.21 * s)}" fill="{ponto}"/>')


ICONES = {"laranja": (LARANJA, GRAFITE, OSSO), "grafite": (GRAFITE, OSSO, LARANJA), "osso": (OSSO, GRAFITE, "#d9822b")}
ICONE = "laranja"          # o que vai no app; troque aqui se a dupla escolher outro


def icone(chave, s=512, raio=0.22):
    """Ícone do app: o símbolo sobre um campo de cor chapada."""
    fundo, cor, ponto = ICONES[chave]
    return f'<rect width="{s}" height="{s}" rx="{raio * s:.1f}" fill="{fundo}"/>' + simbolo(0.15 * s, 0.15 * s, 0.7 * s, cor, ponto)


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


SUB = "TACTICAL FPS MANAGER"      # a linha de gênero embaixo do nome
SUBS_TESTE = {"en": SUB, "pt": "MANAGER DE FPS TÁTICO", "frase": "MONTE O TIME. ASSISTA À PARTIDA."}


def logo(nome, tam, x, y, cor=OSSO, sub=None, cor_sub=SUAVE, ponto=LARANJA):
    """A logo: o nome grande e o gênero embaixo. (x, y) = canto de cima. Devolve (svg, largura, altura)."""
    sub = SUB if sub is None else sub
    g, lw = palavra(nome, tam, x, y + tam, cor, ponto)
    alt = tam
    if sub:
        st = 0.19 * tam
        alt += 0.3 * tam + st
        g += f'<path d="{F600.curvas(sub, st, x + 0.01 * tam, y + alt, 0.16)}" fill="{cor_sub}"/>'
    return g, lw, alt


# ---------------------------------------------------------------- o padrão
ANEL_P, PONTO_P, ANEL_V = "#1a2027", "#2b343f", "#3b4551"


def padrao(w, h, passo, ox=0.0, oy=0.0, livre=None):
    """O padrão da marca: o O da logo repetido em malha, cada um com o ponto espiando para um lado.
    Quase todos ficam apagados, em grafite sobre grafite; cinco acendem em laranja e cinco em azul: o 5 contra 5.
    `passo` é a distância entre os centros; (ox, oy) desloca a malha; `livre` = (x0, y0, x1, y1) é a área do texto,
    onde a malha não entra."""
    R, t, r, d = 0.37 * passo, 0.145 * passo, 0.19 * passo, 0.125 * passo
    alt = passo * 0.866
    apag, aceso = [], []
    cols, lins = int(w / passo) + 3, int(h / alt) + 3
    for j in range(-1, lins):
        for i in range(-1, cols):
            cx = ox + i * passo + (passo / 2 if j % 2 else 0)
            cy = oy + j * alt
            if cx < -passo or cx > w + passo or cy < -passo or cy > h + passo:
                continue
            if livre and livre[0] - R < cx < livre[2] + R and livre[1] - R < cy < livre[3] + R:
                continue
            k = (i * 374761393 + j * 668265263) & 0xFFFFFFFF
            k = ((k ^ (k >> 13)) * 1274126177) & 0xFFFFFFFF
            k ^= k >> 16
            ang = (k % 12) * 30
            anel = f'<path d="{circulo(cx, cy, R)}{circulo(cx, cy, R - t)}" fill-rule="evenodd"'
            olho = f'<path d="{lente(cx, cy, R - t, cx + d, r)}" transform="rotate({ang} {cx:.1f} {cy:.1f})"'
            cor = None
            if (k >> 8) % 14 in (0, 7):
                cor = LARANJA if (k >> 8) % 14 == 0 else AZUL
            if cor:
                aceso.append((cx, f'{anel} fill="{ANEL_V}"/>{olho} fill="{cor}"/>'))
            else:
                apag.append(f'{anel} fill="{ANEL_P}"/>{olho} fill="{PONTO_P}"/>')
    return "".join(apag) + "".join(s for _, s in aceso)


# ---------------------------------------------------------------- peças
def documento(w, h, corpo, defs="", fundo=GRAFITE):
    f = f'<rect width="{w}" height="{h}" fill="{fundo}"/>' if fundo else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}"><defs>{defs}</defs>'
            f'<clipPath id="q"><rect width="{w}" height="{h}"/></clipPath><g clip-path="url(#q)">{f}{corpo}</g></svg>')


def veu(w, h, x0, x1, vertical=False):
    """Apaga o padrão do lado onde o texto fica, para ele ler bem."""
    v = 'x1="0" y1="0" x2="0" y2="1"' if vertical else 'x1="0" y1="0" x2="1" y2="0"'
    d = (f'<linearGradient id="veu" {v}><stop offset="{x0}" stop-color="{GRAFITE}" stop-opacity="1"/>'
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
    return f'<rect x="{x - 18 * k:.1f}" y="{y - 26 * k:.1f}" width="{198 * k:.1f}" height="{248 * k:.1f}" rx="{4 * k:.1f}" fill="{GRAFITE}" fill-opacity="0.9"/>' + g


def placar(cx, y, k=1.0):
    """A faixa de placar de uma transmissão: os dois times, os rounds de cada um e o relógio. Números de exemplo."""
    def meio(f, txt, t, x, yy, cor, track=0.0):
        return f'<path d="{f.curvas(txt, t, x - f.largura(txt, t, track) / 2, yy, track)}" fill="{cor}"/>'
    h, lw, mw = 40 * k, 150 * k, 86 * k
    x0 = cx - lw - mw / 2
    g = f'<rect x="{x0:.1f}" y="{y:.1f}" width="{2 * lw + mw:.1f}" height="{h:.1f}" rx="{3 * k:.1f}" fill="{PAINEL}"/>'
    g += f'<rect x="{x0:.1f}" y="{y:.1f}" width="{6 * k:.1f}" height="{h:.1f}" fill="{LARANJA}"/><rect x="{cx + mw / 2 + lw - 6 * k:.1f}" y="{y:.1f}" width="{6 * k:.1f}" height="{h:.1f}" fill="{AZUL}"/>'
    g += f'<rect x="{cx - mw / 2:.1f}" y="{y:.1f}" width="{mw:.1f}" height="{h:.1f}" fill="{GRAFITE}"/>'
    g += f'<path d="{F800.curvas("ORG", 16 * k, x0 + 20 * k, y + 28 * k, 0.03)}" fill="{OSSO}"/>'
    g += meio(F800, "11", 20 * k, cx - mw / 2 - 26 * k, y + 30 * k, LARANJA)
    g += meio(FMONO, "1:24", 13 * k, cx, y + 26.5 * k, OSSO, 0.04)
    g += meio(F800, "9", 20 * k, cx + mw / 2 + 26 * k, y + 30 * k, AZUL)
    t = "RIVAL"
    g += f'<path d="{F800.curvas(t, 16 * k, cx + mw / 2 + lw - 20 * k - F800.largura(t, 16 * k, 0.03), y + 28 * k, 0.03)}" fill="{OSSO}"/>'
    return g


def foto(arq, w, h, fx=0.5, fy=0.5, zoom=1.0, escurece=0.12):
    """Uma captura do jogo cobrindo a peça inteira. (fx, fy) diz que parte da imagem fica à vista quando sobra
    (0 = começo, 1 = fim); `zoom` aproxima. O SVG aponta para o arquivo em capturas/, sem embutir a imagem."""
    from PIL import Image
    iw, ih = Image.open(os.path.join(AQUI, "capturas", arq)).size
    e = max(w / iw, h / ih) * zoom
    dw, dh = iw * e, ih * e
    return (f'<image href="../capturas/{arq}" x="{-(dw - w) * fx:.1f}" y="{-(dh - h) * fy:.1f}" width="{dw:.1f}" height="{dh:.1f}" preserveAspectRatio="none"/>'
            f'<rect width="{w}" height="{h}" fill="{GRAFITE}" fill-opacity="{escurece}"/>')


def arte(nome, w, h, passo, logo_=None, veu_=None, frase=None, desl=(0, 0), captura=None):
    """Uma peça: no fundo, uma captura do jogo (`captura` = (arquivo, fx, fy, zoom)) ou, sem ela, o padrão; por cima,
    (opcional) a logo. `logo_` = (tamanho, x, y); `veu_` escurece o fundo sob o texto."""
    livre, defs = None, ""
    if logo_:
        tam, x, y = logo_
        g, lw, alt = logo(nome, tam, x, y)
        fim = alt + (0.75 * tam + len(frase) * 0.39 * tam if frase else 0)
        livre = (x - 0.3 * tam, y - 0.25 * tam, x + lw + 0.3 * tam, y + fim + 0.2 * tam)
    corpo = foto(captura[0], w, h, *captura[1:]) if captura else padrao(w, h, passo, *desl, livre=livre)
    if veu_:
        defs, r = veu(w, h, *veu_)
        corpo += r
    if logo_:
        corpo += g
        if frase:   # só fora da Steam: lá a cápsula leva arte, nome e subtítulo, mais nada
            ft = 0.26 * tam
            for i, lin in enumerate(frase):
                corpo += f'<path d="{F600.curvas(lin, ft, x, y + alt + 0.75 * tam + i * ft * 1.5, 0.02)}" fill="{OSSO}"/>'
    return documento(w, h, corpo, defs)


def painel(num, linhas, time=None, placar_=None, w=616, h=300, desl=(0, 0), captura=None):
    """Faixa para a descrição da página da Steam ("Sobre este jogo"): o padrão, o número e o título da seção."""
    d, r = veu(w, h, *((0.22, 0.86) if captura else (0.34, 0.8)))
    fundo = foto(captura[0], w, h, *captura[1:]) if captura else padrao(w, h, 78, *desl, livre=(0, h - 124, 28 + max(F800.largura(l, 38, 0.012) for l in linhas) + 14, h))
    corpo = fundo + r
    corpo += simbolo(24, 22, 40) + f'<path d="{FMONO.curvas(num, 11, 74, 47, 0.14)}" fill="{SUAVE}"/>'
    t = 38
    y0 = h - 32 - (len(linhas) - 1) * t * 1.16
    for i, lin in enumerate(linhas):
        corpo += f'<path d="{F800.curvas(lin, t, 28, y0 + i * t * 1.16, 0.012)}" fill="{OSSO}"/>'
    if time:
        corpo += escalacao(*time)
    if placar_:
        corpo += placar(*placar_)
    return documento(w, h, corpo, d)


def gravar(arq, svg):
    os.makedirs(SAIDA, exist_ok=True)
    with open(os.path.join(SAIDA, arq), "w", encoding="utf-8") as f:
        f.write(svg)


NOME = "THE @ORG"


def main():
    nome = sys.argv[1] if len(sys.argv) > 1 else NOME

    # --- logo (texto em curvas)
    for cor, suf, sc, fundo in ((OSSO, "claro", SUAVE, None), (GRAFITE, "escuro", "#5b626c", None), (OSSO, "fundo", SUAVE, GRAFITE)):
        g, lw, lh = logo(nome, 120, 60, 60, cor=cor, cor_sub=sc, ponto=LARANJA if cor == OSSO else "#d9822b")
        gravar(f"logo-{suf}.svg", documento(round(lw + 120), round(lh + 120), g, fundo=fundo))
    for chave, txt in SUBS_TESTE.items():      # as opções de subtítulo, lado a lado, para escolher
        g, lw, lh = logo(nome, 120, 60, 60, sub=txt)
        lw = max(lw, F600.largura(txt, 0.19 * 120, 0.16))
        gravar(f"logo-subtitulo-{chave}.svg", documento(round(lw + 120), round(lh + 120), g))
    # --- ícone do app: o símbolo sobre cor chapada, em três campos; "icone-app" é o que está em uso
    for chave in ICONES:
        gravar(f"icone-{chave}.svg", documento(512, 512, icone(chave), fundo=None))
    gravar("icone-app.svg", documento(512, 512, icone(ICONE), fundo=None))
    gravar("simbolo.svg", documento(512, 512, simbolo(26, 26, 460), fundo=None))
    gravar("simbolo-escuro.svg", documento(512, 512, simbolo(26, 26, 460, GRAFITE, "#d9822b"), fundo=None))
    gravar("steam-icone-comunidade-184x184.svg", documento(184, 184, icone(ICONE, 184, raio=0), fundo=None))
    # --- o padrão solto, para usar de fundo onde precisar (ladrilha na horizontal a cada `passo`)
    gravar("padrao-1920x1080.svg", arte(nome, 1920, 1080, 150))

    # --- Steam: loja. Uma captura do jogo no fundo e o nome grande, sem mais texto.
    D, V = "mapa-deserto.jpg", "mapa-vila.jpg"      # as capturas em capturas/; troque quando houver prints melhores
    gravar("steam-cabecalho-920x430.svg", arte(nome, 920, 430, 0, (96, 44, 140), (0.36, 0.82), captura=(D, 0.62, 0.42, 1.12)))
    gravar("steam-capsula-principal-1232x706.svg", arte(nome, 1232, 706, 0, (128, 64, 246), (0.36, 0.82), captura=(D, 0.6, 0.45, 1.1)))
    gravar("steam-capsula-pequena-462x174.svg", arte(nome, 462, 174, 0, (64, 20, 34), (0.62, 0.98), captura=(D, 0.6, 0.47, 1.0)))
    gravar("steam-capsula-vertical-748x896.svg", arte(nome, 748, 896, 0, (134, 56, 70), (0.24, 0.5, True), captura=(D, 0.52, 0.5, 1.0)))
    gravar("steam-fundo-da-pagina-1438x810.svg", arte(nome, 1438, 810, 0, captura=(V, 0.5, 0.5, 1.0, 0.35)))
    # --- Steam: biblioteca
    gravar("steam-biblioteca-capsula-600x900.svg", arte(nome, 600, 900, 0, (106, 46, 66), (0.22, 0.48, True), captura=(D, 0.52, 0.5, 1.0)))
    gravar("steam-biblioteca-heroi-3840x1240.svg", arte(nome, 3840, 1240, 0, captura=(D, 0.5, 0.45, 1.0, 0.2)))
    g, lw, lh = logo(nome, 250, 0, 0)
    g, lw, lh = logo(nome, 250, (1280 - lw) / 2, (720 - lh) / 2)
    gravar("steam-biblioteca-logo-1280x720.svg", documento(1280, 720, g, fundo=None))
    # --- Steam: faixas da descrição da página
    gravar("steam-painel-1-transmissao-616x300.svg", painel("01", ["ASSISTA COMO", "TRANSMISSÃO"], placar_=(430, 26, 0.78), captura=(V, 0.5, 0.45, 1.0)))
    gravar("steam-painel-2-bots-616x300.svg", painel("02", ["BOTS QUE JOGAM", "COMO PROFISSIONAIS"], captura=(D, 0.55, 0.5, 1.25)))
    gravar("steam-painel-3-gestao-616x300.svg", painel("03", ["SUA GESTÃO", "APARECE NA PARTIDA"], time=(452, 56, 0.8), captura=(D, 0.1, 0.3, 1.1)))
    # --- redes (aqui cabe a frase)
    frase = ["VOCÊ MONTA O TIME.", "ELES JOGAM COMO PROFISSIONAIS."]
    gravar("banner-social-1500x500.svg", arte(nome, 1500, 500, 0, (100, 80, 96), (0.26, 0.72), frase=frase, captura=(V, 0.5, 0.42, 1.0)))
    gravar("quadrado-social-1080x1080.svg", arte(nome, 1080, 1080, 0, (160, 80, 80), (0.24, 0.5, True), captura=(V, 0.6, 0.5, 1.0)))
    print("ok:", len(os.listdir(SAIDA)), "arquivos em", SAIDA, "· nome usado:", limpo(nome))


if __name__ == "__main__":
    main()
