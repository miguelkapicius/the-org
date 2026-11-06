# Régua dos sons: compara cada arma nossa com as medidas do CS2 (só referência) e acusa o que está fora.
# Uso: python regua.py [arma,...]   (lê Tools/sons/saida/<arma>/*.wav e D:\unity\Tools\ref_sons_cs2)
import sys, os, glob, json, numpy as np
from medir import medir

AQUI = os.path.dirname(os.path.abspath(__file__))
REF = r'D:/unity/Tools/ref_sons_cs2/sounds/weapons'
MAPA = {  # nossa arma -> (arquivos de tiro perto, arquivos distantes) do CS2
    'ak': ('ak47/ak47_0[124].wav', 'ak47/ak47_distant.wav'),
    'm4': ('m4a1/m4a1_0[1-4].wav', 'm4a1/m4a1_distant_0*.wav'),
    'm4s': ('m4a1/m4a1_us_0[1-4].wav', 'm4a1/m4a1_us_distant*.wav'),
    'awp': ('awp/awp_01.wav', 'awp/awp_distant.wav'),
    'deagle': ('deagle/deagle_0[12].wav', 'deagle/deagle*distant*.wav'),
    'glock': ('glock18/glock_0[12].wav', 'glock18/glock18-1-distant.wav'),
    'usp': ('usp/usp_0[1-3].wav', None),
    'p250': ('p250/p250_01.wav', 'p250/p250_distant_01.wav'),
    'mp9': ('mp9/mp9_0[1-4].wav', 'mp9/mp9-1-distant.wav'),
    'mac10': ('mac10/mac10_0[12].wav', 'mac10/mac10-1-distant.wav'),
    'tec9': ('tec9/tec9_02.wav', 'tec9/tec9_distant_01.wav'),
    'fiveseven': ('fiveseven/fiveseven_01.wav', 'fiveseven/fiveseven-1-distant.wav'),
    'galil': ('galilar/galil_0[1-4].wav', 'galilar/galil_distant.wav'),
    'famas': ('famas/famas_0[1-3].wav', 'famas/famas_distant_01.wav'),
}
TOL = dict(d20=1.4, d40=1.4, brilho=1.4)  # razão máxima (pra cima ou pra baixo)
BANDAS_MAX = 0.35                         # soma |diferença| das 5 faixas (sub, grave, médio, agudo, ar)
ATAQUE_MAX_MS = 30                        # resposta: o estouro tem que chegar já (jogo de reação)


def media(arqs):
    ms = [medir(a) for a in arqs]
    out = {k: float(np.mean([m[k] for m in ms])) for k in ('d20', 'd40', 'brilho', 'ataque_ms', 'rms50_db', 'dur')}
    out['bandas'] = list(np.mean([m['bandas'] for m in ms], 0))
    return out


def julgar(nos, ref):
    erros = []
    for k, t in TOL.items():
        r = nos[k] / max(ref[k], 1e-6)
        if r > t or r < 1 / t: erros.append(f'{k} {nos[k]:.3g} x {ref[k]:.3g}')
    db = float(np.abs(np.array(nos['bandas']) - np.array(ref['bandas'])).sum())
    if db > BANDAS_MAX: erros.append(f'bandas {db:.2f} ' + ' '.join(f'{a:.2f}/{b:.2f}' for a, b in zip(nos['bandas'], ref['bandas'])))
    if nos['ataque_ms'] > max(ATAQUE_MAX_MS, ref['ataque_ms'] * 1.3): erros.append(f"ataque {nos['ataque_ms']:.0f} ms")
    return erros, db


def vetor(m):
    return np.array([np.log(m['d20']), np.log(m['d40']), np.log(m['brilho']) * 1.5] + [b * 3 for b in m['bandas']])


def main(lista):
    total = 0; res = {}
    for a in lista:
        pr, dr = MAPA[a]
        for tipo, padrao, ref_pad in (('tiro', 'tiro_*.wav', pr), ('distante', 'distante_*.wav', dr)):
            nos_arq = sorted(glob.glob(os.path.join(AQUI, 'saida', a, padrao)))
            if not nos_arq or not ref_pad: continue
            ref_arq = sorted(glob.glob(os.path.join(REF, ref_pad)))
            if not ref_arq: print(a, tipo, 'sem referência'); continue
            n, r = media(nos_arq), media(ref_arq)
            erros, db = julgar(n, r); total += len(erros)
            res[(a, tipo)] = (n, r)
            print(f"{'OK ' if not erros else 'XX '} {a:7s} {tipo:8s} d20 {n['d20']:.2f}/{r['d20']:.2f} d40 {n['d40']:.2f}/{r['d40']:.2f} "
                  f"br {n['brilho']:5.0f}/{r['brilho']:5.0f} atq {n['ataque_ms']:4.0f}/{r['ataque_ms']:4.0f} bandas {db:.2f}" + ('  -> ' + '; '.join(erros) if erros else ''))
    # distinção: duas armas nossas não podem ficar bem mais parecidas entre si do que no CS2
    tiros = [a for a in lista if (a, 'tiro') in res]
    for i in range(len(tiros)):
        for k in range(i + 1, len(tiros)):
            a, b = tiros[i], tiros[k]
            dn = np.linalg.norm(vetor(res[(a, 'tiro')][0]) - vetor(res[(b, 'tiro')][0]))
            dr = np.linalg.norm(vetor(res[(a, 'tiro')][1]) - vetor(res[(b, 'tiro')][1]))
            if dn < 0.5 * dr and dr > 0.6:
                total += 1; print(f'XX  distinção {a} x {b}: {dn:.2f} (CS2 {dr:.2f}) — parecidas demais')
    print(f'\nREPROVAÇÕES: {total}')
    return total


if __name__ == '__main__':
    lista = sys.argv[1].split(',') if len(sys.argv) > 1 else list(MAPA)
    sys.exit(min(main(lista), 100))
