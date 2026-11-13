"""Por que o retake demora: enquanto a ordem do CT é 'juntar pra retomar', quanto tempo ele passa em cada fase e a que velocidade.
Uso: python -I Tools/pro/retake_lento.py <pasta de caixas> [prefixo da ordem]"""
import sys, io, os, glob, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
pre = sys.argv[2] if len(sys.argv) > 2 else 'juntar pra retomar'
tf = collections.Counter(); vf = collections.defaultdict(float); pf = collections.defaultdict(collections.Counter); tot = 0
for arq in glob.glob(os.path.join(sys.argv[1], '*.tsv')):
    fase = {}; ordem = {}; pensa = {}
    for l in io.open(arq, encoding='utf-8'):
        c = l.rstrip('\n').split('\t')
        if c[0] == 'X' or c[0] == 'F' and len(c) > 2 and c[2].startswith('round ganho'): break   # pós-round não conta
        if c[0] == 'F': fase[c[1]] = c[2]
        elif c[0] == 'O': ordem[c[1]] = c[2]
        elif c[0] == 'P': pensa[c[1]] = c[2]
        elif c[0] == 'A' and c[3] == 'CT' and ordem.get(c[1], '').startswith(pre):
            f = fase.get(c[1], '?'); v = float(c[10]); k = f + (' (parado)' if v < 8 else ' (devagar)' if v < 40 else ' (correndo)')
            tf[k] += 0.25; tot += 0.25; pf[k][pensa.get(c[1], '')[:70]] += 1
print('ordem "%s...": %d s de jogador no total' % (pre, tot))
for k, v in tf.most_common(14):
    print('  %4.0f%%  %-42s  ex.: %s' % (100 * v / tot, k, ' | '.join(p for p, _ in pf[k].most_common(2))))
