# Refaz só a camada distante dos híbridos a partir dos mesmos candidatos e botões (ajuste_hibrido.json).
import json, glob, os, numpy as np
import hibrido, armas
aj = json.load(open(hibrido.AJ_H, encoding='utf-8'))
for a in armas.ARMAS:
    if a not in aj: continue
    cands = []
    for c in sorted(glob.glob(os.path.join(hibrido.BRUTO, a, 'tiro_c*.mp3'))):
        x = hibrido.aparar_tiro(hibrido.carregar(c))
        if hibrido.aceita_tiro(x)[0]: cands.append(x)
    K = aj[a]['K']; A = None
    for k in range(4):
        _, seco = hibrido.tiro_hibrido(a, cands[k % len(cands)], 100 + k, K)
        hibrido.salvar(os.path.join(hibrido.SAIDA, a, f'distante_{k}.wav'), hibrido.distante_hibrido(a, seco, 100 + k))
    print('ok', a)
