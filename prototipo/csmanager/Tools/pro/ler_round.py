"""Lê um round da caixa-preta em linha do tempo, de N em N segundos, pra um time: distância ao C4 (depois do plant), velocidade,
fase, ordem e pensamento de cada jogador vivo. Uso: python -I Tools/pro/ler_round.py <arquivo.tsv> [CT|TR] [passo_s] [desde_s]"""
import sys, io, math
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
arq = sys.argv[1]; lado = sys.argv[2] if len(sys.argv) > 2 else 'CT'; passo = float(sys.argv[3]) if len(sys.argv) > 3 else 2.5; desde = float(sys.argv[4]) if len(sys.argv) > 4 else -1
t = 0; plant = None; bomb = None; fase = {}; ordem = {}; pensa = {}; last = {}; nome = {}; time = {}; ult = {}
for l in io.open(arq, encoding='utf-8'):
    c = l.rstrip('\n').split('\t')
    if c[0] == 'T':
        t = float(c[1])
        if c[2] == '1' and plant is None: plant = t; bomb = (float(c[3]), float(c[4])); print('--- PLANT aos %.1f s em (%.0f,%.0f)' % (t, bomb[0], bomb[1]))
    elif c[0] == 'X': print('--- FIM DO ROUND aos', c[1])
    elif c[0] == 'FIM': print('--- venceu', ' · '.join(c[1:]))
    elif c[0] == 'F': fase[c[1]] = c[2] if len(c) > 2 else ''
    elif c[0] == 'O': ordem[c[1]] = c[2] if len(c) > 2 else ''
    elif c[0] == 'P': pensa[c[1]] = c[2] if len(c) > 2 else ''
    elif c[0] == 'C' and c[1] == lado.replace('TR', 'TR') and (desde < 0 and plant is not None or desde >= 0 and t >= desde): print('  %5.1f TIME: %s' % (t, c[2][:120]))
    elif c[0] == 'A':
        nome[c[1]] = c[2]; time[c[1]] = c[3]; ult[c[1]] = t
        ok = (desde < 0 and plant is not None) or (desde >= 0 and t >= desde)
        if ok and c[3] == lado and t - last.get(c[1], -99) >= passo:
            last[c[1]] = t; d = math.hypot(float(c[5]) - bomb[0], float(c[6]) - bomb[1]) if bomb else -1
            print('%5.1f %-7s %-5s hp%3s d=%4.0f v=%4.1f | %-28s | %-30s | %s' % (t, c[2], c[4], c[9], d, float(c[10]), fase.get(c[1], '')[:28], ordem.get(c[1], '')[:30], pensa.get(c[1], '')[:78]))
print('último quadro:', ' '.join('%s(%s)%.0f' % (nome[i], time[i], u) for i, u in sorted(ult.items(), key=lambda x: x[1])))
