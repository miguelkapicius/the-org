using System; using System.Linq; using System.Collections.Generic; using System.Threading.Tasks;
using CSM.Sim; using CSM.Map;

namespace CSM.Headless
{
    /// <summary>
    /// Mede o "micro" dos bots (diagnóstico em gdd/12-ia-micro.md): visão tampada por colega, dois colegas no mesmo ângulo,
    /// morrer no mesmo lugar para o mesmo atirador (AWP x outras armas) e quanto a escolha de tática se concentra. Para comparar
    /// antes e depois de cada correção. Uso: dotnet Headless/bin/Release/net9.0/Headless.dll -micro 12 -seed 9100
    /// Relatório em Logs/bots/micro.txt.
    /// </summary>
    public static class MedirMicro
    {
    static float AngDiff(float a, float b) { float d = a - b; while (d > MathF.PI) d -= 2 * MathF.PI; while (d < -MathF.PI) d += 2 * MathF.PI; return MathF.Abs(d); }
    public static int Rodar(int n, int seed0)
    {
        var sb = new System.Text.StringBuilder(); void Out(string s) { sb.AppendLine(s); Console.WriteLine(s); }
        new Match(1); Spots.All();
        var trava = new object();
        long amostrasMov = 0, tampaMov = 0, amostrasPar = 0, tampaPar = 0, pares = 0, mesmoAng = 0;
        var mortes = new Dictionary<string, (int kills, int repete)>();
        var usoT = new Dictionary<int, int>(); var usoC = new Dictionary<int, int>(); int roundsT = 0, roundsC = 0;
        var jogT = new Dictionary<string, int>();
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 }, i =>
        {
            var pt = new Partida(seed0 + i);
            long aM = 0, tM = 0, aP = 0, tP = 0, pr = 0, ma = 0; var mt = new Dictionary<string, (int, int)>();
            var uT = new Dictionary<int, int>(); var uC = new Dictionary<int, int>(); int rT = 0, rC = 0; var jT = new Dictionary<string, int>();
            while (!pt.acabou && pt.r < 60)
            {
                var m = pt.Preparar();
                var posMorte = new Dictionary<Agent, V2>(); int passo = 0;
                while (!m.over && m.t < 200)
                {
                    m.Step(); passo++;
                    foreach (var x in m.agents) if (!x.alive && !posMorte.ContainsKey(x)) posMorte[x] = x.pos;
                    if (passo % 16 != 0 || m.t < 3) continue;   // ~0,5 s
                    foreach (var x in m.agents)
                    {
                        if (!x.alive || x.brain.target != null) continue;
                        bool anda = x.Speed > 20;
                        bool tampado = false;
                        foreach (var y in m.agents)
                        {
                            if (y == x || !y.alive || y.team != x.team) continue;
                            V2 d = y.pos - x.pos; float dl = d.Len;
                            if (dl < 350 && dl > 5 && AngDiff(MathF.Atan2(d.y, d.x), x.aim) < 0.12f) tampado = true;
                            if (y.id > x.id && y.brain.target == null && dl < 160 && x.Speed > 20 && y.Speed > 20) { pr++; if (AngDiff(x.aim, y.aim) < 0.17f) ma++; }
                        }
                        if (anda) { aM++; if (tampado) tM++; } else { aP++; if (tampado) tP++; }
                    }
                }
                // morreu no mesmo lugar para o mesmo atirador em até 6 s
                var abs = m.abates.Where(k => k.by != null && k.v != null && k.by != k.v && !k.aliado && (m.fimEm < 0 || k.t <= m.fimEm + 0.05f)).OrderBy(k => k.t).ToList();
                foreach (var k in abs)
                {
                    string arma = k.arma == "awp" ? "AWP" : "outras armas";
                    bool rep = abs.Any(k2 => k2.t > k.t && k2.t - k.t <= 6 && k2.by == k.by && k2.v.team == k.v.team && posMorte.TryGetValue(k.v, out var p1) && posMorte.TryGetValue(k2.v, out var p2) && (p1 - p2).Len < 250);
                    mt.TryGetValue(arma, out var c); mt[arma] = (c.Item1 + 1, c.Item2 + (rep ? 1 : 0));
                }
                var eT = m.igl[0].Estrategia; var eC = m.igl[1].Estrategia;
                if (eT != null && m.compra[0] == "full") { uT[eT.idx] = uT.GetValueOrDefault(eT.idx) + 1; rT++; jT[eT.jogada] = jT.GetValueOrDefault(eT.jogada) + 1; }
                if (eC != null && m.compra[1] == "full") { uC[eC.idx] = uC.GetValueOrDefault(eC.idx) + 1; rC++; }
                if (!m.over) { foreach (var x in m.agents) x.vivoNoFim = x.alive; m.over = true; m.winner = "CT"; m.reason = "tempo acabou"; }
                for (float q = 0; q < Match.POS_ROUND; q += Match.DT) m.StepPosRound();
                pt.Fechar(m);
            }
            lock (trava)
            {
                amostrasMov += aM; tampaMov += tM; amostrasPar += aP; tampaPar += tP; pares += pr; mesmoAng += ma;
                foreach (var kv in mt) { mortes.TryGetValue(kv.Key, out var c); mortes[kv.Key] = (c.kills + kv.Value.Item1, c.repete + kv.Value.Item2); }
                foreach (var kv in uT) usoT[kv.Key] = usoT.GetValueOrDefault(kv.Key) + kv.Value; foreach (var kv in uC) usoC[kv.Key] = usoC.GetValueOrDefault(kv.Key) + kv.Value;
                foreach (var kv in jT) jogT[kv.Key] = jogT.GetValueOrDefault(kv.Key) + kv.Value;
                roundsT += rT; roundsC += rC;
            }
        });
        Out($"{n} partidas");
        Out($"VISÃO TAMPADA (colega no cone de 7° da mira, até 350): andando {100.0 * tampaMov / amostrasMov:0.0}% · parado {100.0 * tampaPar / amostrasPar:0.0}%");
        Out($"MESMO ÂNGULO (dois colegas andando a menos de 160 um do outro, mira a menos de 10°): {100.0 * mesmoAng / Math.Max(1, pares):0.0}% de {pares} amostras");
        foreach (var kv in mortes) Out($"MORREU NO MESMO LUGAR para o mesmo atirador em até 6 s ({kv.Key}): {100.0 * kv.Value.repete / kv.Value.kills:0.0}% de {kv.Value.kills} abates");
        void Conc(string lado, Dictionary<int, int> u, int r, int pool)
        {
            var o = u.Values.OrderByDescending(x => x).ToList(); int top10 = o.Take(10).Sum(), top20p = o.Take(Math.Max(1, pool / 5)).Sum();
            Out($"TÁTICA {lado} (compra cheia): {r} rounds, {u.Count} planos diferentes de {pool} no banco; os 10 mais usados = {100.0 * top10 / r:0}% dos rounds; mais usado {o[0]}x");
        }
        Conc("TR", usoT, roundsT, Estrategias.Todos.Count(p => p.tr && p.compra == "full")); Conc("CT", usoC, roundsC, Estrategias.Todos.Count(p => !p.tr && p.compra == "full"));
        Out("JOGADAS TR mais usadas: " + string.Join(", ", jogT.OrderByDescending(k => k.Value).Take(6).Select(k => $"{k.Key} {100.0 * k.Value / roundsT:0}%")));
        var fs = Estrategias.Todos.Where(p => p.tr && p.compra == "full").Select(p => Estrategias.Forca(p.idx)).OrderBy(x => x).ToList();
        Out($"FORÇA medida dos planos TR cheios: mín {fs[0]:0.00} · mediana {fs[fs.Count / 2]:0.00} · máx {fs[^1]:0.00} (razão máx/mín {fs[^1] / fs[0]:0.0}x)");
            System.IO.Directory.CreateDirectory("Logs/bots"); System.IO.File.WriteAllText("Logs/bots/micro.txt", sb.ToString());
        return 0;
    }
}
}
