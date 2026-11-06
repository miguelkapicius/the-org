# Arma caindo (foley de cinema/jogo): opções diretas da IA + opções montadas em camadas (baque do peso + quique + chacoalhar).
import glob, os, numpy as np
import hibrido, motor
SR = motor.SR; O = 'saida/_opcoes'


def carrega(n, ini=0):
    fs = sorted(glob.glob(os.path.join(hibrido.BRUTO, 'arma', f'{n}_c*.mp3')), key=lambda c: int(c.rsplit('_c', 1)[1][:-4]))
    return [hibrido.aparar(hibrido.carregar(f)) for f in fs if int(f.rsplit('_c', 1)[1][:-4]) >= ini]


def pitch(x, f): return np.interp(np.arange(0, len(x) - 1, f), np.arange(len(x)), x)
def norm(x): return x / (np.abs(x).max() + 1e-9)


baques, chocalhos = carrega('camada_baque'), carrega('camada_chacoalho')
for tipo, f_p, quique, g_ch in (('fuzil', 0.9, 0.15, 0.25), ('pistola', 1.3, 0.1, 0.15)):
    for f in glob.glob(os.path.join(O, f'arma_cai_{tipo}_*.wav')): os.remove(f)
    diretos = carrega(f'cai_{tipo}', 40)
    k = 0
    for x in diretos:
        motor.salvar(os.path.join(O, f'arma_cai_{tipo}_D{k}.wav'), motor.acabamento(motor.lp(x, 9000), -2, 20)); k += 1
    for i in range(min(6, len(baques))):
        b = pitch(norm(baques[i]), f_p); c = norm(chocalhos[i % len(chocalhos)])
        n = int(0.9 * SR); y = np.zeros(n)
        y[:min(n, len(b))] += b[:n]                                                            # baque do peso
        j = int(quique * SR); b2 = pitch(b, 1.15)[: int(0.25 * SR)] * np.linspace(1, 0, int(0.25 * SR))[:len(pitch(b, 1.15)[: int(0.25 * SR)])]
        y[j:j + len(b2)] += 0.32 * b2[: n - j]                                                  # quique menor
        j = int(0.015 * SR); c = motor.lp(c, 5000)[: n - j]; y[j:j + len(c)] += g_ch * c       # chacoalhar das peças, abafado
        y = motor.lp(y, 7000)                                                                   # sem agudo "ressoando"
        motor.salvar(os.path.join(O, f'arma_cai_{tipo}_C{i}.wav'), motor.acabamento(y, -2, 30))
    print(tipo, 'diretos', len(diretos), 'camadas', min(6, len(baques)))
