# Match point = só a fala nº 2 ("Maaaatch... POINT!", Brian) com o respiro encurtado (usuário: "reduz o tempo de respirar",
# referência: o "Match point" do Valorant, curto e direto). BAM abandonado pelo usuário.
# A fala 2 tem ~0,42 s de silêncio entre "Maaatch" (até ~0,61 s) e "POINT" (~1,02 s). Gera 3 pausas -> _opcoes/mpcurta_K.
# Uso: python mp_fala.py | python mp_fala.py usar K
import os, sys, glob, shutil, numpy as np, soundfile as sf
import motor
from motor import SR
OP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida', '_opcoes'); S = os.path.dirname(OP)
PAUSAS = (0.08, 0.15, 0.22)


def corta(x, pausa):
    w = int(0.01 * SR); e = np.array([np.sqrt((x[i:i + w] ** 2).mean()) for i in range(0, len(x) - w, w)]); db = 20 * np.log10(e / e.max() + 1e-9)
    mudo = np.where(db < -60)[0]; mudo = mudo[(mudo > 20) & (mudo < len(db) - 20)]          # o buraco do meio
    a, b = mudo[0] * w, mudo[-1] * w
    return np.concatenate([x[:a], np.zeros(int(pausa * SR)), x[b:]])


if __name__ == '__main__':
    x = sf.read(os.path.join(OP, 'mpfala_1.wav'))[0]
    if len(sys.argv) > 2 and sys.argv[1] == 'usar':
        for f in glob.glob(os.path.join(S, 'hud', 'match_point_*.wav')): os.remove(f)
        shutil.copy(os.path.join(OP, f'mpcurta_{int(sys.argv[2]) - 1}.wav'), os.path.join(S, 'hud', 'match_point_0.wav')); print('ok')
    else:
        for f in glob.glob(os.path.join(OP, 'mpcurta_*.wav')): os.remove(f)
        for k, p in enumerate(PAUSAS):
            y = corta(x, p); motor.salvar(os.path.join(OP, f'mpcurta_{k}.wav'), y); print(k + 1, f'pausa {p:.2f} s, total {len(y) / SR:.2f} s', flush=True)
    os._exit(0)
