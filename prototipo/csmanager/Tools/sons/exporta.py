# Copia os sons prontos (saida/<grupo>/<nome>_K.wav) pra Unity: Assets/Game/Audio/Resources/Sons/... (Som3D carrega por nome).
import os, glob, shutil
AQUI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(AQUI, '..', '..', 'Assets', 'Game', 'Audio', 'Resources', 'Sons')
n = 0
for f in glob.glob(os.path.join(AQUI, 'saida', '*', '*.wav')):
    g = os.path.basename(os.path.dirname(f))
    if g.startswith('_'): continue
    d = os.path.join(DEST, g); os.makedirs(d, exist_ok=True)
    alvo = os.path.join(d, os.path.basename(f))
    if not os.path.exists(alvo) or os.path.getmtime(alvo) < os.path.getmtime(f): shutil.copy2(f, alvo); n += 1
rem = 0
for f in glob.glob(os.path.join(DEST, '*', '*.wav')):
    src = os.path.join(AQUI, 'saida', os.path.basename(os.path.dirname(f)), os.path.basename(f))
    if not os.path.exists(src):
        os.remove(f); rem += 1
        if os.path.exists(f + '.meta'): os.remove(f + '.meta')
print(n, 'arquivos copiados,', rem, 'removidos em', os.path.normpath(DEST))
