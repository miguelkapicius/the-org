"""Crença do CT x verdade, pela caixa-preta: nos instantes em que 3+ TR vivos estão de verdade numa zona de bomb (A ou B)
antes do plant, quantos o time do CT ACREDITAVA lá (linha C da caixa-preta)? E quando não tem ninguém lá, quantos acreditava?
Uso: python -I Tools/pro/crenca_x_verdade.py <pasta de caixas> [...]"""
import sys, io, os, glob, re, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
it = io.open(os.path.join(RAIZ, 'Tools', 'pro', 'areas_vila.tsv'), encoding='utf-8'); P, _, _ = map(int, next(it).split('\t')); Z = {}
for l in it:
    c = l.rstrip('\n').split('\t'); Z[(int(c[0]), int(c[1]))] = {'A': 0, 'B': 2}.get(c[3], 1)
for pasta in sys.argv[1:]:
    cheio = []; vazio = []; fantasma = 0; n = 0; atraso = []
    for arq in glob.glob(os.path.join(pasta, '*.tsv')):
        cren = [0, 0, 0]; tr = {}; t = 0; plant = False; viu = {}; chegou = {}
        def fecha():
            global fantasma, n
            if plant or t < 15 or not tr: return
            v = [0, 0, 0]
            for z in tr.values(): v[z] += 1
            n += 1
            for z in (0, 2):
                if v[z] >= 3:
                    cheio.append(cren[z] / v[z]); chegou.setdefault(z, t)
                    if cren[z] >= 2 and z not in viu: viu[z] = t; atraso.append(t - chegou[z])
                if v[z] == 0: vazio.append(cren[z]); fantasma += cren[z] >= 1.5
        for l in io.open(arq, encoding='utf-8'):
            c = l.rstrip('\n').split('\t')
            if c[0] == 'T':
                fecha(); t = float(c[1]); plant = c[2] == '1'; tr = {}
            elif c[0] == 'A' and c[3] == 'TR':
                tr[c[1]] = Z.get((int(float(c[5]) // P), int(float(c[6]) // P)), 1)
            elif c[0] == 'C' and c[1] == 'CT':
                mm = re.search(r'eles ([\d.]+)/([\d.]+)/([\d.]+)', c[2])
                if mm: cren = [float(mm.group(i)) for i in (1, 2, 3)]
    md = lambda x: sum(x) / len(x) if x else 0
    atraso.sort()
    print('%s: com 3+ TR de verdade no lado do bomb, o CT acredita em %.0f%% deles (%d instantes) · com 0 TR lá, acredita em %.2f em média, e em 1,5+ (fantasma) em %.1f%% dos instantes · leu "2+ lá" em %d entradas, mediana %.1f s depois de os 3 chegarem'
          % (os.path.basename(pasta.rstrip('/\\')), 100 * md(cheio), len(cheio), md(vazio), 100.0 * fantasma / max(1, len(vazio)), len(atraso), atraso[len(atraso) // 2] if atraso else -1))
