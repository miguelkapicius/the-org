# Seca a camada PERTO dos tiros (usuário, 2026-11-05: "vejo a troca na minha cara e ela tem eco").
# Medição (eco_tiro.py): os tiros perto do CS2 já trazem a sala dentro (cauda -2..-13 dB) e os nossos copiaram isso —
# P250/Glock/USP/Galil ainda mais molhados que o CS2. Na câmera de cima o lance está "na cara": o eco fica só na camada
# DISTANTE (fora da tela). Aqui:
#  - os primeiros ATAQUE ms depois do pico ficam INTACTOS (o estouro aprovado não muda);
#  - dali pra frente o que está acima de 200 Hz (onde mora o eco/sala) cai mais rápido (exponencial ajustada até a cauda
#    chegar no ALVO); o grave abaixo de 200 Hz (o "estrondo", ex. AWP) só encurta de leve.
# Uso: python seca.py [armas...]  (sem argumento = todas)   -> saida/<arma>/tiro_K.wav; original em saida/_opcoes/<arma>_molhado_K.wav
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor, regua
from motor import SR
from eco_tiro import medir

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida')
ALVO = -16.0          # cauda (120-900 ms vs 0-120 ms) em dB; CS2 vai de -2 a -14, nós queremos mais seco que ele
ATAQUE = 0.07         # s depois do pico intocados
GRAVE_MIN = {'awp': 0.8, 'deagle': 0.3}   # piso da constante do grave (s): o estrondo da AWP/Deagle fica; padrão 0,12
ALVO_ARMA = {'awp': -9.0, 'deagle': -12.0}
TAU_MIN = 0.03        # agudo nunca cai mais rápido que isso (10 ms soava recortado)


def secar(x, tau, grave_tau):
    p = int(np.argmax(np.abs(x))); t = (np.arange(len(x)) - p) / SR - ATAQUE
    g = np.where(t > 0, np.exp(-np.maximum(t, 0) / tau), 1.0)
    gg = np.where(t > 0, np.exp(-np.maximum(t, 0) / grave_tau), 1.0)
    lo = motor.lp(x, 200, 4); hi = x - lo
    y = hi * g + lo * gg
    n = len(y); f = min(int(0.01 * SR), n // 4); y[-f:] *= np.linspace(1, 0, f)
    return y


def ajusta(x, gmin, alvo):
    gt = lambda m: max(gmin, 3 * m)          # o grave decai junto, 3x mais devagar, nunca abaixo do piso
    if medir(x)[0] <= alvo: return x, None
    lo_, hi_ = TAU_MIN, 1.0                  # busca binária no tau dos agudos
    if medir(secar(x, lo_, gt(lo_)))[0] > alvo: return secar(x, lo_, gt(lo_)), lo_
    for _ in range(16):
        m = (lo_ + hi_) / 2
        if medir(secar(x, m, gt(m)))[0] > alvo: hi_ = m
        else: lo_ = m
    return secar(x, lo_, gt(lo_)), lo_


if __name__ == '__main__':
    armas = sys.argv[1:] or list(regua.MAPA)
    os.makedirs(os.path.join(S, '_opcoes'), exist_ok=True)
    for a in armas:
        for f in sorted(glob.glob(os.path.join(S, a, 'tiro_[0-9].wav'))):
            k = f.rsplit('_', 1)[1][:-4]; bk = os.path.join(S, '_opcoes', f'{a}_molhado_{k}.wav')
            if not os.path.exists(bk): shutil.copy(f, bk)      # o original molhado é guardado UMA vez
            x = sf.read(bk)[0]
            if x.ndim > 1: x = x.mean(1)
            antes = medir(x); y, tau = ajusta(x, GRAVE_MIN.get(a, 0.12), ALVO_ARMA.get(a, ALVO)); depois = medir(y)
            pico_antes = np.abs(x[:int(np.argmax(np.abs(x))) + int(ATAQUE * SR)]).max(); pico_depois = np.abs(y).max()
            motor.salvar(f, y * (pico_antes / max(pico_depois, 1e-9)) if pico_depois > pico_antes else y)
            print(f'{a:9s} {k}: cauda {antes[0]:6.1f} -> {depois[0]:6.1f} dB   t40 {antes[1]:.2f} -> {depois[1]:.2f} s   '
                  f'reflexos {antes[2]:.0f} -> {depois[2]:.0f}   tau {"-" if tau is None else f"{tau*1000:.0f} ms"}', flush=True)
    os._exit(0)
