# Tec-9 e Five-SeveN "muito potentes, mas são pistolas" (usuário, 2026-11-04): menos grave e nível de pistola.
# Medido (rms 150 ms, já com o volume do jogo): Glock -18,8 / P250 -25,3 / Deagle -15,6 / AK -13,4; Tec-9 estava -16,0 (nível de
# Deagle/AK, grave mais alto de todas), Five-SeveN -23,6. Corta o grave (prateleira < 150 Hz) e põe a Tec-9 no nível da Glock.
# Guarda o anterior em _opcoes/<arma>_forte_K.
import os, glob, shutil, numpy as np, soundfile as sf
import motor
from motor import SR
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida')
AJ = {'tec9': (-6.0, -3.0), 'fiveseven': (-5.0, -1.0)}   # (grave dB, volume dB)
for a, (gdb, vdb) in AJ.items():
    for f in sorted(glob.glob(os.path.join(S, a, 'tiro_[0-9].wav'))):
        k = f.rsplit('_', 1)[1][:-4]; bk = os.path.join(S, '_opcoes', f'{a}_forte_{k}.wav')
        if not os.path.exists(bk): shutil.copy(f, bk)
        x = sf.read(bk)[0]; y = motor.prateleira_grave(x, 150, gdb)
        y = y * np.abs(x).max() / (np.abs(y).max() + 1e-12) * 10 ** (vdb / 20)
        motor.salvar(f, y)
    x = sf.read(os.path.join(S, a, 'tiro_0.wav'))[0]; pk = np.argmax(abs(x)); r = np.sqrt((x[pk:pk + int(0.15 * SR)] ** 2).mean())
    print(a, f'rms150 {20 * np.log10(r):.1f} dB', flush=True)
os._exit(0)
