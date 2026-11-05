# v18 (2026-11-04):
#  1) Match point = locutor (Brian, o nº 3 aprovado) falando "Match point." e logo depois o BAM aprovado (nº 6) -> hud/match_point_0
#  2) AK no PADRÃO da M4A4 (usuário: "AK com muito eco, faz igual a M4 só que com som de AK"): mesma base da IA da AK,
#     botões do híbrido da M4 (mais corpo da IA, sub forte, agudo neutro, secador). 3 opções em _opcoes/ak_K (1 = atual).
# Uso: python v18.py            -> candidatos
#      python v18.py usar AK    -> AK escolhida (1-based) vira saida/ak/tiro_*, distante_*
import os, sys, glob, json, numpy as np, soundfile as sf
import motor, hibrido, voz, radio
from motor import SR

AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, 'saida'); OP = os.path.join(S, '_opcoes')
AJ = json.load(open(os.path.join(AQUI, 'ajuste_hibrido.json'), encoding='utf-8'))


def mix(partes, dur):
    out = np.zeros(int(dur * SR))
    for t, x, g in partes:
        i = int(t * SR); m = min(len(x), len(out) - i); out[i:i + m] += x[:m] * g
    return out


def match_point():
    for k in range(2):
        d = os.path.join(hibrido.BRUTO, 'voz', f'anuncio_match_point_c{k}.mp3')
        if not os.path.exists(d): voz.falar('brian', 'Match point.', d)
    bam = sf.read(os.path.join(OP, 'matchpoint_5.wav'))[0]
    for f in glob.glob(os.path.join(OP, 'mpvoz_*.wav')): os.remove(f)
    for k in range(2):
        v = radio.locutor(hibrido.carregar(os.path.join(hibrido.BRUTO, 'voz', f'anuncio_match_point_c{k}.mp3')))
        y = mix([(0, v, 0.8), (len(v) / SR - 0.05, bam, 1.0)], len(v) / SR + len(bam) / SR)
        motor.salvar(os.path.join(OP, f'mpvoz_{k}.wav'), motor.acabamento(y, -1, 30))
    print('match point com voz: 2 takes', flush=True)


def base_ak():
    """A mesma base que o ajuste da AK escolheu (melhor candidato da IA na régua, botões neutros)."""
    cands = sorted(glob.glob(os.path.join(hibrido.BRUTO, 'ak', 'tiro_c*.mp3')))
    novos = [c for c in cands if int(os.path.basename(c).rsplit('_c', 1)[1][:-4]) >= 10]
    if novos: cands = novos
    ias = [x for x in (hibrido.aparar_tiro(hibrido.carregar(c)) for c in cands) if hibrido.aceita_tiro(x)[0]]
    import ajusta, regua
    refs = sorted(glob.glob(os.path.join(hibrido.REF, regua.MAPA['ak'][0])))
    A = ajusta.alvo(refs); A['eco'] = float(np.mean([hibrido.eco(hibrido.carregar(r)) for r in refs]))
    notas = [hibrido.custo_h(('ak', [ia], dict(hibrido.NEU_H), A)) for ia in ias]
    return ias[int(np.argmin(notas))]


def opcoes_ak():
    KM = dict(AJ['m4']['K'])
    menos_eco = dict(KM, envio=KM['envio'] * 0.6, t60=KM['t60'] * 0.7, seca=KM['seca'] * 0.8)
    return [None, KM, menos_eco]


def faz_ak(K, base):
    tiros, dist = [], []
    for k in range(4):
        ia = hibrido.variacao(base, k); x, seco = hibrido.tiro_hibrido('ak', ia, 100 + k, K)
        tiros.append(x); dist.append(hibrido.distante_hibrido('ak', seco, 100 + k))
    return tiros, dist


def candidatos():
    match_point()
    base = base_ak()
    for f in glob.glob(os.path.join(OP, 'ak_*.wav')): os.remove(f)
    for j, K in enumerate(opcoes_ak()):
        tiros = [sf.read(f)[0] for f in sorted(glob.glob(os.path.join(S, 'ak', 'tiro_[0-9].wav')))] if K is None else faz_ak(K, base)[0]
        y = mix([(0, tiros[0], 0.9)] + [(1.2 + i * 0.1, tiros[i % 4], 0.9) for i in range(5)], 1.2 + 0.5 + 1.4)   # 1 tiro + rajada de 5 (600 rpm)
        motor.salvar(os.path.join(OP, f'ak_{j}.wav'), y)
    print('AK: 3 opções (1 = atual, 2 = padrão da M4, 3 = padrão da M4 com menos eco)', flush=True)


def usar(j):
    if j == 0: print('AK fica a atual'); return
    tiros, dist = faz_ak(opcoes_ak()[j], base_ak())
    for k in range(4):
        motor.salvar(os.path.join(S, 'ak', f'tiro_{k}.wav'), tiros[k]); motor.salvar(os.path.join(S, 'ak', f'distante_{k}.wav'), dist[k])
    print('AK nova:', j + 1)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'usar': usar(int(sys.argv[2]) - 1)
    else: candidatos()
    os._exit(0)
