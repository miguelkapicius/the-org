# Validador automático das animações dos bonecos 3D (sem Blender e sem Unity): lê Logs/bonecos3d/boneco_<time>.json/.bin
# (o que o exportar3d.py gravou) e mede cada quadro de cada animação. Acusa o que a gente vê como "bugado":
#   ATRAVESSA   braço/mão/arma entrando na cabeça, no tronco ou na perna (além do que já entra na pose de mira)
#   MAO_ARMA    mão que devia segurar a arma longe dela (arma flutuando / mão no ar)
#   CEGO        no cego/meio cego nenhuma mão tapando os olhos (perto das lentes/olhos E na frente do rosto)
#   CHAO        pé ou corpo afundando no chão; em pé sem nenhum pé no chão
#   DESCOLOU    peças que se tocam na pose de mira (ombro-braço, braço-antebraço, antebraço-mão, coxa-canela...) se
#               separando (membro quebrado)
#   SALTO       peça do corpo pulando de um quadro pro outro (pop); em loop, também do último pro primeiro
#   ESCALA      matriz degenerada / NaN
#   PISCA       peça que aparece, some e volta (flicker)
#   ROUPA       peça de baixo vazando pela de cima (tronco aparecendo pelas costas/lados do colete)
#   python validar_anims.py [pasta] [ct|tr|ambos] [filtro]  -> relatório no console + validacao_<time>.txt
# Sai com código 1 se houver erro (pra usar antes de mandar vídeo).
import json, os, sys, re
import numpy as np

ARGV = sys.argv[1:]
PASTA = ARGV[0] if ARGV else r"D:\unity\CSManager\Logs\bonecos3d"
TIMES = ["ct", "tr"] if len(ARGV) < 2 or ARGV[1] == "ambos" else [ARGV[1]]
FILTRO = re.compile(ARGV[2]) if len(ARGV) > 2 else None
ARMAS = ["AK47", "AWP", "Deagle", "Glock", "M4A1S", "M4A4", "MAC10", "MP9", "USP"]
PISTOLAS = {"Deagle", "Glock", "USP"}

# tolerâncias (unidades do Blender: o boneco tem ~0,75 de altura)
NUCLEO = 0.88        # vértice dentro de 88% do raio da outra peça = atravessou de verdade (não só encostou)
FOLGA_PEN = 0.06     # fração de vértices dentro além da pose de referência
MAO_ARMA = 0.045     # superfície da mão até a arma
SALTO = 0.075        # deslocamento máximo de uma peça do corpo entre quadros (30 qps)
DESCOLA = 0.035
NAO_USADAS = r"(comemorar|lamentar)"   # exportadas mas fora do jogo (fim de round sem comemoração)


def carregar(time):
    with open(os.path.join(PASTA, f"boneco_{time}.json"), encoding="utf-8") as fh: h = json.load(fh)
    blob = np.fromfile(os.path.join(PASTA, f"boneco_{time}.bin"), dtype="<f4")
    pecas = h["pecas"]; n = len(pecas)
    loc = {}
    for i, p in enumerate(pecas):
        v = blob[p["v"]:p["v"] + 3 * p["nv"]].reshape(-1, 3)
        if len(v) > 600: v = v[np.linspace(0, len(v) - 1, 600).astype(int)]
        v = np.unique(np.round(v, 5), axis=0)
        loc[p["nome"]] = v
    anims = {}
    for a in h["anims"]:
        q = blob[a["q"]:a["q"] + a["n"] * n * 12].reshape(a["n"], n, 3, 4)
        anims[a["nome"]] = (q, a["loop"])
    return h, [p["nome"] for p in pecas], loc, anims


class Forma:
    """Peça como sólido simples no espaço local: elipsoide (esfera UV) ou caixa (cubo com bevel)."""
    def __init__(s, v):
        s.c = (v.max(0) + v.min(0)) / 2; s.h = np.maximum((v.max(0) - v.min(0)) / 2, 1e-5)
        q = np.abs((v - s.c) / s.h).sum(1).max()
        s.caixa = q > 2.3

    def dentro(s, p, k=NUCLEO):
        """p: pontos no espaço local da peça -> máscara dos que estão dentro do núcleo."""
        q = (p - s.c) / s.h
        if s.caixa: return np.abs(q).max(1) < k
        return (q * q).sum(1) < k * k


def mundo(M, v):
    return v @ M[:, :3].T + M[:, 3]


def local(M, p):
    A = M[:, :3]
    return np.linalg.solve(A, (p - M[:, 3]).T).T


def visivel(M):
    return np.abs(M).sum() > 1e-6


def arma_da(nome):
    for w in sorted(ARMAS, key=len, reverse=True):
        if nome.endswith("_" + w): return w
    return None


def validar(time):
    h, nomes, loc, anims = carregar(time)
    idx = {n: i for i, n in enumerate(nomes)}
    T = lambda k: time + k
    forma = {n: Forma(loc[n]) for n in nomes}
    CABECA = [T("cabeca")] + ([T("mascara")] if time == "ct" else [])
    TRONCO = [T("tronco"), T("colete")]
    BRACOS = {sd: [T("braco" + sd), T("ante" + sd), T("mao" + sd)] + ([T("pulso" + sd)] if T("pulso" + sd) in idx else []) for sd in "LR"}
    PERNAS = [T(k + sd) for k in ("coxa", "canela") for sd in "LR"]
    OLHOS = [T("lenteL"), T("lenteR")] if time == "ct" else [T("olhoL"), T("olhoR")]
    CORPO = [n for n in nomes if n.startswith(time) and not re.search(r"(clarao|estrela|rastro|capsula|pino|bomba|visor|alicate|alca_arma|sm_|fl_|mo_|he_|luneta|chama|fogo|isq)", n)]
    PES = [T("botaL"), T("botaR")]
    ROUPAS = [(T("tronco"), T("colete"))]
    # pares que podem atravessar e onde (a mão direita encostando na coronha etc. já entra na referência)
    PARES = []
    for sd in "LR":
        for b in BRACOS[sd]:
            for c in CABECA + TRONCO: PARES.append((b, c))
    for sd in "LR":   # antebraço/mão atravessando o outro braço
        o = "R" if sd == "L" else "L"
        PARES.append((T("mao" + sd), T("braco" + o))); PARES.append((T("ante" + sd), T("braco" + o)))
        if T("pulso" + sd) in idx: PARES.append((T("pulso" + sd), T("braco" + o)))
    for w in ARMAS:
        for c in CABECA + TRONCO + PERNAS: PARES.append((w, c))
    # pares que se tocam (descolar): medidos pela distância entre vértices mais próximos
    JUNTAS = []
    for sd in "LR":
        JUNTAS += [(T("braco" + sd), T("ante" + sd))] + ([(T("ante" + sd), T("pulso" + sd)), (T("pulso" + sd), T("mao" + sd))] if T("pulso" + sd) in idx else [(T("ante" + sd), T("mao" + sd))])
        JUNTAS += [
                   (T("coxa" + sd), T("canela" + sd)), (T("canela" + sd), T("bota" + sd)),
                   (T("tronco"), T("braco" + sd)), (T("quadril"), T("coxa" + sd))]
    JUNTAS += [(T("tronco"), T("cabeca")), (T("tronco"), T("quadril"))]

    def pen(q, a, b):
        """fração dos vértices de a dentro do núcleo de b no quadro q (0 se alguma escondida)."""
        Ma, Mb = q[idx[a]], q[idx[b]]
        if not (visivel(Ma) and visivel(Mb)): return 0.0
        w = mundo(Ma, loc[a])
        try: l = local(Mb, w)
        except np.linalg.LinAlgError: return 0.0
        return float(forma[b].dentro(l).mean())

    def dist_min(q, a, b):
        Ma, Mb = q[idx[a]], q[idx[b]]
        wa = mundo(Ma, loc[a][::3]); wb = mundo(Mb, loc[b][::3])
        d = ((wa[:, None, :] - wb[None, :, :]) ** 2).sum(2)
        return float(np.sqrt(d.min()))

    # referência: pose de mira de cada arma (o que já encosta/entra "de fábrica")
    ref_pen, ref_junta = {}, {}
    for w in ARMAS:
        q = anims[f"mira_{w}"][0][0]
        ref_pen[w] = {(a, b): pen(q, a, b) for a, b in PARES}
        ref_junta[w] = {(a, b): dist_min(q, a, b) for a, b in JUNTAS}

    erros = []   # (anim, tipo, quadros, detalhe)
    for nome, (Q, loop) in anims.items():
        if FILTRO and not FILTRO.search(nome): continue
        if re.match(NAO_USADAS, nome): continue
        w = arma_da(nome) or ("M4A4" if time == "ct" else "AK47")
        if nome.startswith("awp_"): w = "AWP"
        nq = len(Q)
        morte = nome.startswith("morrer")
        achado = {}
        def acusa(tipo, fi, det):
            k = (tipo, det)
            achado.setdefault(k, []).append(fi)
        for fi in range(nq):
            q = Q[fi]
            # ESCALA
            for n in CORPO:
                M = q[idx[n]]
                if not visivel(M): continue
                if not np.isfinite(M).all() or abs(np.linalg.det(M[:, :3])) < 1e-12: acusa("ESCALA", fi, n)
            # ATRAVESSA (a arma da vez e as peças do corpo)
            for a, b in PARES:
                if a in ARMAS and a != w: continue
                if morte and a in ARMAS: continue    # a arma escapou da mão e cai por conta própria
                base = ref_pen[w].get((a, b), 0.0)
                p = pen(q, a, b)
                # braço (e a pele do braço do TR, "ante" até o cotovelo) nasce no ombro, dentro do tronco
                folga = 0.25 if (("braco" in a or (time == "tr" and "ante" in a)) and b in TRONCO) else FOLGA_PEN
                # de propósito: pistola no coldre da coxa no começo do sacar; coronha da AWP na bochecha na luneta
                if a in PISTOLAS and nome.startswith("sacar") and fi < nq * 0.25 and "coxa" in b: continue
                if a == "AWP" and nome.startswith("awp_") and b in CABECA: folga = 0.2
                if p > base + folga and p > 0.08: acusa("ATRAVESSA", fi, f"{a.replace(time, '')} dentro de {b.replace(time, '')}")
            # ROUPA: peça de baixo vazando pela de cima (tronco furando o colete nas costas/lados = "bola azul")
            for dentro, fora in ROUPAS:
                Mi, Mo = q[idx[dentro]], q[idx[fora]]
                if not (visivel(Mi) and visivel(Mo)): continue
                l = local(Mo, mundo(Mi, loc[dentro])); f = forma[fora]
                qq = (l - f.c) / f.h
                # vértice do tronco na faixa do colete (altura e largura cobertas) mas pra fora da frente/costas
                faixa_ = (np.abs(qq[:, 1]) < 0.85) & (np.abs(qq[:, 2]) < 0.75)
                vaza = faixa_ & (np.abs(qq[:, 0]) > 1.0)
                lado = (np.abs(qq[:, 0]) < 0.85) & (np.abs(qq[:, 2]) < 0.75) & (np.abs(qq[:, 1]) > 1.0)
                n_vaza = int(vaza.sum() + lado.sum())
                if n_vaza > 2: acusa("ROUPA", fi, f"{dentro.replace(time, '')} vazando pelo {fora.replace(time, '')} ({n_vaza} pontos)")
            # CHAO
            vz = min(mundo(q[idx[n]], loc[n])[:, 2].min() for n in CORPO if visivel(q[idx[n]]))
            if vz < -0.03: acusa("CHAO", fi, "corpo abaixo do chão")
            pes = min(mundo(q[idx[n]], loc[n])[:, 2].min() for n in PES)
            no_ar = re.search(r"(no_ar|pular|escada|comemorar)", nome)
            if not no_ar and not morte and pes > 0.03: acusa("CHAO", fi, "nenhum pé no chão")
            # MAO_ARMA
            Mg = q[idx[w]]
            segura = re.match(r"(mira|andar|devagar|agachado|agachar|tiro|no_ar|pitch|apontar|meio_cego|cego|pegar|lamentar|awp_mirando|awp_mirar|tras|lado|pular)", nome)
            if segura and visivel(Mg):
                gv = mundo(Mg, loc[w])
                for sd in "LR":
                    if sd == "L" and re.match(r"(apontar|cego|meio_cego|pegar|lamentar)", nome): continue
                    if sd == "R" and nome.startswith("tiro_AWP"): continue   # puxa o ferrolho
                    hm = q[idx[T("mao" + sd)]][:, 3]
                    raio = float(np.abs(q[idx[T("mao" + sd)]][:, :3]).max())
                    d = float(np.sqrt(((gv - hm) ** 2).sum(1)).min()) - raio
                    if d > MAO_ARMA: acusa("MAO_ARMA", fi, f"mão {sd} a {d * 100:.0f} cm da arma")
            # PEGADA: direita no PUNHO e esquerda no GUARDA-MÃO (pontos de cada arma, do exportar3d); LUNETA: olho na ocular
            pg = h.get("armas", {}).get(w)
            if pg and segura and visivel(Mg):
                def pt(v): return Mg[:, :3] @ np.array(v) + Mg[:, 3]
                L = pg["L"]
                fore = list(pg["fore"])
                if nome.startswith("awp_mira"): fore[0] += 0.03 * L; fore[2] -= 0.01
                alvos = {"R": (pt(pg["grip"]), 0.045), "L": (pt(fore) + (np.array([0, 0.03, -0.01]) if pg["pistola"] else 0), 0.06)}
                for sd, (alvo, tol) in alvos.items():
                    if sd == "L" and re.match(r"(apontar|cego|meio_cego|pegar|lamentar)", nome): continue
                    if sd == "R" and nome.startswith("tiro_AWP"): continue
                    if nome == "awp_mirar" and fi < nq // 2: continue   # subindo pra luneta
                    d = float(np.linalg.norm(q[idx[T("mao" + sd)]][:, 3] - alvo))
                    if d > tol: acusa("PEGADA", fi, f"mão {sd} a {d * 100:.0f} cm do {'punho' if sd == 'R' else 'guarda-mão'}")
                if pg.get("ocular") and nome == "awp_mirando":
                    olho = q[idx[T("lenteR" if time == "ct" else "olhoR")]][:, 3]
                    Mc = q[idx[T("cabeca")]]; frente = Mc[:, 0] / np.linalg.norm(Mc[:, 0])
                    oc = pt(pg["ocular"]); d = float(np.linalg.norm(oc - olho))
                    if d > 0.06: acusa("LUNETA", fi, f"ocular a {d * 100:.0f} cm do olho direito")
                    elif np.dot(oc - olho, frente) < -0.005: acusa("LUNETA", fi, "ocular atrás do olho (dentro da cabeça)")
            # CEGO: mão tapando os olhos, NA FRENTE do rosto
            if re.match(r"(cego|meio_cego)", nome):
                Mc = q[idx[T("cabeca")]]
                frente = Mc[:, 0] / np.linalg.norm(Mc[:, 0]); hc = Mc[:, 3]
                olho = np.mean([q[idx[o]][:, 3] for o in OLHOS], axis=0)
                melhor = 9.0; na_frente = False
                for sd in "LR":
                    hm = q[idx[T("mao" + sd)]][:, 3]
                    d = float(np.linalg.norm(hm - olho)); melhor = min(melhor, d)
                    if d < 0.09 and np.dot(hm - hc, frente) > float(np.dot(olho - hc, frente)) - 0.01: na_frente = True
                if melhor > 0.09: acusa("CEGO", fi, f"mão a {melhor * 100:.0f} cm dos olhos")
                elif not na_frente: acusa("CEGO", fi, "mão atrás da linha dos olhos (dentro do rosto)")
        # DESCOLOU (em todos os quadros, contra a referência)
        for fi in range(0, nq, max(1, nq // 8)):
            q = Q[fi]
            for a, b in JUNTAS:
                if not (visivel(q[idx[a]]) and visivel(q[idx[b]])): continue
                d = dist_min(q, a, b)
                if d > ref_junta[w][(a, b)] + DESCOLA: acusa("DESCOLOU", fi, f"{a.replace(time, '')} - {b.replace(time, '')} {d * 100:.0f} cm")
        # SALTO e PISCA: pulo = passo muito maior que os vizinhos (tranco/pop), não velocidade alta (arremesso é rápido)
        pos = Q[:, :, :, 3]; vis = np.abs(Q).sum((2, 3)) > 1e-6
        corpo_i = [idx[n] for n in CORPO if not re.search(r"(pente|estrela)", n)]
        if nq > 3:
            ordem = list(range(nq)) + ([0, 1] if loop else [])
            for j in corpo_i:
                passos = []
                for a, b in zip(ordem[:-1], ordem[1:]):
                    passos.append(float(np.linalg.norm(pos[b, j] - pos[a, j])) if vis[a, j] and vis[b, j] else 0.0)
                for i in range(len(passos)):
                    viz = [passos[k] for k in (i - 1, i + 1) if 0 <= k < len(passos)]
                    if not loop and i == 0: continue
                    d = passos[i]
                    if d > SALTO * 0.5 and d > 2.5 * max(viz) + 0.025:
                        fi = ordem[i + 1]
                        acusa("SALTO", fi, f"{nomes[j].replace(time, '')} pula {d * 100:.0f} cm" + (" (emenda do loop)" if fi == 0 else ""))
        for j in corpo_i:
            v = vis[:, j]
            for fi in range(1, nq - 1):
                if v[fi - 1] and not v[fi] and v[fi + 1]: acusa("PISCA", fi, nomes[j].replace(time, ""))
        for (tipo, det), qs in achado.items():
            erros.append((nome, tipo, qs, det))
    return erros, anims


def faixa(qs):
    qs = sorted(set(qs)); out = []; a = b = qs[0]
    for x in qs[1:]:
        if x == b + 1: b = x
        else: out.append((a, b)); a = b = x
    out.append((a, b))
    return ",".join(f"{a}" if a == b else f"{a}-{b}" for a, b in out)


if __name__ == "__main__":
    total = 0
    for time in TIMES:
        erros, anims = validar(time)
        linhas = []
        por_anim = {}
        for nome, tipo, qs, det in erros: por_anim.setdefault(nome, []).append((tipo, qs, det))
        for nome in sorted(por_anim):
            nq = len(anims[nome][0])
            linhas.append(f"{nome}  ({nq} quadros)")
            for tipo, qs, det in sorted(por_anim[nome]):
                linhas.append(f"    {tipo:<10} {det}  [quadros {faixa(qs)}]")
        ok = len([a for a in anims if (not FILTRO or FILTRO.search(a)) and not re.match(NAO_USADAS, a) and a not in por_anim])
        cab = f"== {time.upper()}: {len(por_anim)} animações com problema, {ok} ok"
        print(cab); print("\n".join(linhas))
        with open(os.path.join(PASTA, f"validacao_{time}.txt"), "w", encoding="utf-8") as fh:
            fh.write(cab + "\n" + "\n".join(linhas) + "\n")
        total += len(por_anim)
    sys.exit(1 if total else 0)
