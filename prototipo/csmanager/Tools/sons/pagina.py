# Monta a página de audição (HTML único com os sons em MP3 embutido) + régua do lado + notas (db "notas").
# Uso: python pagina.py <saida.html>
import os, sys, io, glob, json, base64, numpy as np, soundfile as sf
from motor import SR
import cena, regua, foley, armas

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida')
NOMES = {'ak': 'AK-47', 'm4': 'M4A4', 'm4s': 'M4A1-S', 'awp': 'AWP', 'deagle': 'Desert Eagle', 'glock': 'Glock-18',
         'usp': 'USP-S', 'p250': 'P250', 'mp9': 'MP9', 'mac10': 'MAC-10', 'tec9': 'Tec-9', 'fiveseven': 'Five-SeveN', 'galil': 'Galil AR', 'famas': 'FAMAS'}
LADO = {'ak': 'TR', 'm4': 'CT', 'm4s': 'CT', 'awp': 'ambos', 'deagle': 'ambos', 'glock': 'TR', 'usp': 'CT', 'p250': 'ambos', 'mp9': 'CT', 'mac10': 'TR', 'tec9': 'TR', 'fiveseven': 'CT', 'galil': 'TR', 'famas': 'CT'}
RECARGA = {'ak': 2.4, 'm4': 3.1, 'm4s': 3.1, 'awp': 3.7, 'mp9': 2.1, 'mac10': 2.6, 'usp': 2.2, 'glock': 2.3, 'deagle': 2.2, 'p250': 2.2, 'tec9': 2.5, 'fiveseven': 2.2, 'galil': 3.0, 'famas': 3.3}
# mesmas frações do Som3D.PecasRecarga
PECAS = {'awp': [(0.152, 'pente_sai'), (0.667, 'pente_entra'), (0.71, 'pente_bate'), (0.8, 'ferrolho_tras'), (0.88, 'ferrolho_frente')],
         'pist': [(0.17, 'pente_sai'), (0.672, 'pente_entra'), (0.85, 'ferrolho_frente')],
         'fuzil': [(0.157, 'pente_sai'), (0.667, 'pente_entra'), (0.84, 'ferrolho')]}


def ler(rel):
    x, sr = sf.read(os.path.join(S, rel)); return x


def mp3(x):
    x = np.asarray(x, dtype=np.float64)
    pk = np.abs(x).max()
    if pk > 0.98: x = x / pk * 0.98
    b = io.BytesIO(); sf.write(b, x.astype(np.float32), SR, format='MP3', subtype='MPEG_LAYER_III')
    return 'data:audio/mpeg;base64,' + base64.b64encode(b.getvalue()).decode()


def picos(x, n=96):
    m = x.mean(1) if x.ndim == 2 else x
    k = max(1, len(m) // n)
    p = [float(np.abs(m[i * k:(i + 1) * k]).max()) for i in range(n) if i * k < len(m)]
    t = max(p) + 1e-9
    return [round(v / t, 3) for v in p]


def mix(partes, dur):
    L = int(dur * SR); out = np.zeros(L)
    for t, x, g in partes:
        i = int(t * SR); m = min(len(x), L - i)
        if m > 0: out[i:i + m] += x[:m] * g
    return out


def seq_recarga(a):
    pc = PECAS['awp'] if a == 'awp' else PECAS['pist'] if a in ('glock', 'usp', 'p250', 'deagle', 'tec9', 'fiveseven') else PECAS['fuzil']
    d = RECARGA[a]
    pico = lambda x: int(np.argmax(np.abs(x))) / SR
    return mix([(max(0.0, d * u - pico(ler(f'{a}/{p}_0.wav'))), ler(f'{a}/{p}_0.wav'), 0.9) for u, p in pc], d + 0.6)


def seq_c4(fin=0):
    """Teclado (nº 1), plantou, começo do timer, corte, últimos 10 s acelerando, chiado do último segundo, explosão."""
    dig = ler('c4/digita_0.wav'); partes = [(0, dig, 0.8)]; t = len(dig) / SR + 0.1
    partes.append((t, ler('c4/plantou_0.wav'), 0.9)); t += 0.6
    for ini, fim in ((40.0, 34.0), (10.0, 1.0)):
        resta = ini
        while resta > fim:
            partes.append((t, ler('c4/bip_rapido_0.wav' if resta < 10 else 'c4/bip_0.wav'), 0.6))
            dt = max(0.15, 0.1 + 0.9 * resta / 40); t += dt; resta -= dt
        t += 0.5 if ini == 40.0 else 0.0
    partes.append((t, ler('c4/final_0.wav'), 0.9)); t += 1.0
    partes.append((t, ler('c4/explode_0.wav'), 1.0))
    return mix(partes, t + 3.5)


ALERTA = None


def corrida(nome, n=8, passo=1 / 3.6):
    arqs = sorted(glob.glob(os.path.join(S, nome + '_*.wav')))
    partes = [(k * passo, sf.read(arqs[k % len(arqs)])[0], 0.8 if k % 2 else 0.65) for k in range(n)]
    return mix(partes, n * passo + 0.5)


def primeiro(nome, segundos=None):
    x = ler(nome + '_0.wav')
    if segundos: x = x[:int(segundos * SR)].copy(); f = int(0.3 * SR); x[-f:] *= np.linspace(1, 0, f)
    return x


def seq_granada(g):
    pino, arr = ler('granada/pino_0.wav'), ler('granada/arremesso_0.wav')
    q = ler('granada/quique_vidro_0.wav' if g == 'molotov' else 'granada/quique_metal_0.wav')
    q2 = ler('granada/quique_metal_1.wav')
    estouro = {'he': 'granada/he_0.wav', 'flash': 'granada/flash_0.wav', 'smoke': 'granada/smoke_0.wav', 'molotov': 'granada/molotov_0.wav'}[g]
    partes = [(0, pino, 0.6), (0.55, arr, 0.7)]
    if g != 'molotov': partes += [(1.25, q, 0.5), (1.6, q2, 0.3)]
    partes.append((1.15 if g == 'molotov' else 2.1, ler(estouro), 1.0))
    x = mix(partes, 6.0 if g == 'smoke' else 4.6)
    if g == 'molotov':   # fogo queimando depois
        f = ler('granada/fogo_loop_0.wav'); i = int(1.6 * SR); m = min(len(f), len(x) - i); x[i:i + m] += f[:m] * np.linspace(0, 0.7, m)
    return x


def seq_passos():
    partes = []; t = 0
    for k in range(10): partes.append((t, ler(f'passo/pedra_{k % 8}.wav'), 0.8 if k % 2 else 0.65)); t += 1 / 3.6
    return mix(partes, t + 0.4)


def comparacao(add):
    """v12: bip novo da C4 (sequência completa e só os bips) e caixa registradora na compra: escolha no ouvido."""
    nomes_c4 = ['CS2: tom firme 4,2 kHz', 'CS2: dois tons', 'mais grave 3,2 kHz', 'piezo seco', 'suave e limpo', 'agudo digital']
    out = []
    nomes_ui = ['estalo seco', 'vidro', 'tecla de plástico', 'digital suave', 'madeira', 'metal leve']
    for chave, rot, pref in (('ui', 'INTERFACE (botões): passa o mouse 3x, clica, passa e clica — o clique é o mesmo material do estalinho', 'ui_'),
                             ('mpsimples', 'MATCH POINT do zero: mesma voz e tratamento dos anúncios aprovados — 1-2 padrão, 3-4 empolgado', 'mpsimples_'),):
        bts = []
        for f in sorted(glob.glob(os.path.join(S, '_opcoes', pref + '[0-9]*.wav')), key=lambda f: int(f.rsplit('_', 1)[1][:-4])):
            k = int(f.rsplit('_', 1)[1][:-4]); lab = f'{k + 1}' + (f' {nomes_c4[k]}' if chave.startswith('c4') else f' {nomes_ui[k]}' if chave == 'ui' else '')
            add(f'cmp_{chave}_{k}', sf.read(f)[0], f'{rot} {lab}'); bts.append(dict(chave=f'cmp_{chave}_{k}', rotulo=lab))
        if bts: out.append(dict(chave=f'cmp_{chave}', rotulo=rot + ' (escolha)', versoes=bts))
    return out


def main(destino):
    sons, armas_info = {}, []
    def add(chave, x, rotulo):
        sons[chave] = dict(src=mp3(x), picos=picos(x), dur=round(len(x) / SR, 2), rotulo=rotulo)

    for a in NOMES:
        if not os.path.exists(os.path.join(S, a, 'tiro_0.wav')): continue
        for k in range(4): add(f'{a}/tiro_{k}', ler(f'{a}/tiro_{k}.wav'), f'tiro {k + 1}')
        add(f'{a}/distante', ler(f'{a}/distante_0.wav'), 'só a camada distante')
        n = 1 if a == 'awp' else 5 if a in ('deagle', 'usp', 'glock', 'p250') else 8
        add(f'{a}/rajada', cena.rajada(a, n, 150).mean(1) * 1.3, 'rajada' if n > 1 else 'tiro + eco')
        partes = [(i * 2.2, cena.rajada(a, 1 if a == 'awp' else 3, dd, dur=3.0).mean(1) * 1.3, 1) for i, dd in enumerate([150, 700, 1500, 2400])]
        add(f'{a}/distancias', mix(partes, 4 * 2.2 + 2.2), '150 · 700 · 1500 · 2400 u')
        add(f'{a}/recarga', seq_recarga(a), f'recarga ({RECARGA[a]:.1f} s)')
        add(f'{a}/sacar', ler(f'{a}/sacar_0.wav'), 'sacar')
        if a == 'awp' and os.path.exists(os.path.join(S, 'arma', 'zoom_0.wav')):
            add('awp/luneta', mix([(0, ler('arma/zoom_0.wav'), 1), (0.8, ler('arma/zoom_1.wav' if os.path.exists(os.path.join(S, 'arma', 'zoom_1.wav')) else 'arma/zoom_0.wav'), 1)], 1.6), 'luneta: abre e fecha')
        # régua
        pr, dr = regua.MAPA[a]; info = dict(id=a, nome=NOMES[a], lado=LADO[a], rpm=cena.RPM[a], regua={})
        for tipo, pad, ref in (('perto', 'tiro_*.wav', pr), ('distante', 'distante_*.wav', dr)):
            if not ref: continue
            n_ = regua.media(sorted(glob.glob(os.path.join(S, a, pad)))); r_ = regua.media(sorted(glob.glob(os.path.join(regua.REF, ref))))
            erros, db = regua.julgar(n_, r_)
            info['regua'][tipo] = dict(ok=not erros, d20=[round(n_['d20'], 2), round(r_['d20'], 2)], d40=[round(n_['d40'], 2), round(r_['d40'], 2)],
                                       brilho=[int(n_['brilho']), int(r_['brilho'])], ataque=[int(n_['ataque_ms']), int(r_['ataque_ms'])],
                                       bandas=[[round(v, 2) for v in n_['bandas']], [round(v, 2) for v in r_['bandas']]], erros=erros)
        armas_info.append(info)
        print('ok', a, flush=True)

    mundo = []
    def mundo_add(grupo, chave, x, rotulo):
        add(chave, x, rotulo); mundo.append(dict(grupo=grupo, chave=chave, rotulo=rotulo))
    G = 'C4 (refeita)'
    mundo_add(G, 'c4/seq', seq_c4(), 'C4 com o bip 1 aprovado: teclado, plantou, bip, chiado, explode')
    G = 'Passos e corpo'
    for n, r in (('passo/ct', 'CT correndo na pedra (bota)'), ('passo/tr', 'TR correndo na pedra (tênis)'), ('passo/terra', 'correndo na terra'),
                 ('passo/madeira', 'correndo na madeira'), ('passo/metal', 'correndo no metal'), ('passo/telha', 'correndo no telhado')):
        if glob.glob(os.path.join(S, n + '_*.wav')): mundo_add(G, n, corrida(n), r)
    for n, r in (('corpo/pulo', 'pulo'), ('corpo/aterrissa_pedra', 'aterrissar na pedra'), ('corpo/aterrissa_madeira', 'aterrissar na caixa de madeira'),
                 ('corpo/aterrissa_metal', 'aterrissar no metal'), ('corpo/escada', 'degrau de escada'), ('corpo/queda', 'dano de queda'),
                 ('corpo/cai', 'corpo caindo (morte)'), ('corpo/equipamento', 'equipamento chacoalhando')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n), r)
    G = 'Arma e bala'
    for n, r in (('arma/vazio_fuzil', 'pente vazio (fuzil)'), ('arma/vazio_pistola', 'pente vazio (pistola)'), ('arma/quase_vazio', 'pente quase vazio'),
                 ('arma/cai_fuzil', 'fuzil caindo no chão'), ('arma/cai_pistola', 'pistola caindo no chão'), ('arma/pegar', 'pegar arma do chão'),
                 ('bala/capsula_fuzil', 'cápsula de fuzil'), ('bala/capsula_pistola', 'cápsula de pistola'), ('bala/capsula_awp', 'cápsula da AWP'),
                 ('bala/passando', 'bala de fuzil passando perto'), ('bala/passando_sub', 'bala de pistola passando perto'), ('bala/ricochete', 'ricochete')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n), r)
    G = 'Granadas (extras)'
    for n, r in (('granada/sacar', 'sacar granada'), ('granada/isqueiro', 'isqueiro acendendo a molotov'), ('granada/molotov_voando', 'molotov voando (laço)'),
                 ('granada/incendiaria', 'incendiária do CT estourando'), ('granada/molotov_ar', 'molotov estourando no ar'), ('granada/fogo_fim', 'fogo apagando no fim'),
                 ('granada/smoke_fim', 'smoke se desfazendo'), ('granada/acertou', 'granada acertando alguém'), ('granada/zumbido', 'zumbido de quem ficou cego'),
                 ('granada/queimando', 'pisando no fogo')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n, 4 if 'voando' in n or 'zumbido' in n else None), r)
    G = 'Faca'
    for n, r in (('faca/sacar', 'sacar a faca'), ('faca/estocada', 'estocada forte'), ('faca/parede', 'faca batendo na parede')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n), r)
    G = 'Ambiente da Vila (trecho de 12 s)'
    for n, r in (('ambiente/vento', 'vento'), ('ambiente/passaros', 'pássaros'), ('ambiente/toldos', 'toldos e bandeiras'), ('ambiente/mercado', 'mercado ao longe')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n, 12), r)
    G = 'Transmissão'
    for n, r in (('hud/inicio_round', 'início do round'), ('hud/fim_freeze', 'fim do freeze time'), ('hud/compra', 'compra'), ('hud/colete', 'comprar colete'),
                 ('hud/pause_coach', 'pause do coach'), ('hud/match_point', 'match point')):
        if os.path.exists(os.path.join(S, n + '_0.wav')): mundo_add(G, n, primeiro(n), r)
    lance = os.path.join(S, '_cenas', 'lance_round.wav')
    if os.path.exists(lance):
        x = sf.read(lance)[0]; add('lance', x.mean(1) if x.ndim == 2 else x, 'lance de round')

    comparar = comparacao(add)
    novos = [c['chave'] for c in comparar] if False else []
    dados = json.dumps(dict(sons=sons, armas=armas_info, mundo=mundo, comparar=comparar, novos=novos), ensure_ascii=False)
    html = open(os.path.join(AQUI, 'pagina_modelo.html'), encoding='utf-8').read().replace('/*__DADOS__*/null', dados)
    open(destino, 'w', encoding='utf-8').write(html)
    print('página:', destino, round(len(html) / 1e6, 1), 'MB')


if __name__ == '__main__':
    main(sys.argv[1])
