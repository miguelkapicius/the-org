import json, os
from multiprocessing import Pool
import hibrido, armas
if __name__ == '__main__':
    with Pool(10) as pool: res = pool.starmap(hibrido.ajustar_dist, [(a, 30) for a in armas.ARMAS])
    aj = {}
    for a, r in res:
        if r: aj[a] = r[0]; print(a, 'custo', r[1])
    json.dump(aj, open(hibrido.AJ_D, 'w', encoding='utf-8'), indent=1)
