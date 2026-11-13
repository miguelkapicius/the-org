"""Resumo de pastas de caixa-preta lado a lado: plant, quem vence, ordens e posturas do CT antes do plant, retake.
Serve pra ver se uma mudança QUEBROU algo. Uso: python -I Tools/pro/resumo_cx.py <pasta> [...]"""
import io, glob, os, sys, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
for pasta in sys.argv[1:]:
    n = pl = ct = 0; o = collections.Counter(); tp = []; cv = []; rk = [0, 0]
    for arq in glob.glob(os.path.join(pasta, '*.tsv')):
        plant = False; fim = None; t = 0; cts = set(); ords = collections.Counter(); vivos = 0
        for l in io.open(arq, encoding='utf-8'):
            c = l.rstrip('\n').split('\t')
            if c[0] == 'T':
                t = float(c[1])
                if c[2] == '1' and not plant: plant = True; tp.append(t); vivos = len(cts)
                cts = set()
            elif c[0] == 'A' and c[3] == 'CT': cts.add(c[1])
            elif c[0] == 'O' and not plant and int(c[1]) >= 5: ords[c[2].split(':')[0].split(' ')[0]] += 1
            elif c[0] == 'FIM': fim = c[1]
        if not fim: continue
        n += 1; pl += plant; ct += fim == 'CT'; o.update(ords)
        if plant: cv.append(vivos); rk[0] += 1; rk[1] += fim == 'CT'
    print('%-8s %d rounds · plant %.0f%% · CT vence %.0f%% · plant aos %.0f s com %.1f CT vivos · CT vence depois do plant %.0f%%' % (os.path.basename(os.path.normpath(pasta)), n, 100 * pl / n, 100 * ct / n, sum(tp) / max(1, len(tp)), sum(cv) / max(1, len(cv)), 100 * rk[1] / max(1, rk[0])))
    print('         ordens do CT antes do plant, por round: ' + ' · '.join('%s %.2f' % (k, v / n) for k, v in o.most_common(8)))
