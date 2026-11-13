"""Vícios de duelo na caixa-preta (o que o Gabriel apontou em 12/11), por pasta de caixas:
 - duelo parado: jogador em 'combate' sem sair do lugar (40 un.) por 6 s+, separado por arma leve (pistola/SMG) e rifle/AWP;
 - abre-toma-tiro-volta: 3 voltas pra cobertura em 12 s pelo mesmo jogador, e quantos desses acabaram mortos no round;
 - cego: tempo de jogador na fase 'cego' parado x andando.
Serve pra achar onde olhar; quem julga é a leitura do round (Tools/pro/ler_round.py).
Uso: python -I Tools/pro/duelo_vicios.py <pasta> [...]"""
import sys, io, os, glob, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
LEVE = {'usp', 'glock', 'p250', 'fiveseven', 'tec9', 'deagle', 'mp9', 'mac10'}
for pasta in sys.argv[1:]:
    n = 0; par6 = collections.Counter(); seg = collections.Counter(); min_ = collections.Counter(); loops = 0; mortos = 0; cego = collections.Counter(); ex = []
    for arq in glob.glob(os.path.join(pasta, '*.tsv')):
        n += 1; t = 0; fase = {}; pos0 = {}; par = {}; vt = collections.defaultdict(list); ja = set(); ult = {}
        for l in io.open(arq, encoding='utf-8'):
            c = l.rstrip('\n').split('\t')
            if c[0] == 'X': break
            if c[0] == 'T': t = float(c[1])
            elif c[0] == 'F':
                f = c[2] if len(c) > 2 else ''; fase[c[1]] = f
                if 'voltando pra cobertura' in f:
                    v = vt[c[1]]; v.append(t)
                    if len(v) >= 3 and v[-1] - v[-3] <= 12 and c[1] not in ja:
                        ja.add(c[1]); loops += 1
                        if len(ex) < 4: ex.append('%s t=%.0f jogador %s' % (os.path.basename(arq)[:-4], v[-3], c[1]))
            elif c[0] == 'A':
                i = c[1]; x, y = float(c[5]), float(c[6]); k = 'leve' if c[4] in LEVE else 'rifle'; min_[k] += 0.25 / 60; ult[i] = t
                f = fase.get(i, '')
                if f == 'cego': cego['parado' if float(c[10]) < 8 else 'andando'] += 0.25
                if f == 'combate':
                    if i not in pos0: pos0[i] = (x, y); par[i] = 0.0
                    if abs(x - pos0[i][0]) + abs(y - pos0[i][1]) < 40: par[i] += 0.25
                    else: pos0[i] = (x, y); par[i] = 0.0
                    if par[i] == 6.0: par6[k] += 1
                    if par[i] >= 6.0: seg[k] += 0.25
                else: pos0.pop(i, None)
        mortos += sum(1 for i in ja if ult.get(i, 0) < t - 1)
    print('%s — %d rounds' % (os.path.basename(os.path.normpath(pasta)), n))
    for k, nomek in (('leve', 'pistola/SMG'), ('rifle', 'rifle/AWP')):
        print('   duelo parado 6 s+ de %s: %d casos em %.0f min de jogador (%.2f por 10 min), %.0f s parado no total' % (nomek, par6[k], min_[k], 10 * par6[k] / max(min_[k], 0.01), seg[k]))
    print('   abre-toma-tiro-volta (3 voltas em 12 s): %d casos (%.2f por round); %d acabaram mortos' % (loops, loops / max(n, 1), mortos))
    tc = cego['parado'] + cego['andando']
    print('   cego: %.0f s no total, %.0f%% parado' % (tc, 100 * cego['parado'] / max(tc, 0.01)))
    if ex: print('   onde olhar:', ' · '.join(ex))
