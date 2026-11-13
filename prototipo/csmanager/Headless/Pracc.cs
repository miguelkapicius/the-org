using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Treino de táticas (Headless -pracc K -np N [-leitura stackA|stackB|meio|cedo|passivo] [-ct]): o IGL tem K ideias
    /// (Taticas.Ideias), cada uma joga N rounds contra a jogada de onde veio, nas mesmas sementes (pareado). Entra no
    /// repertório a que não perde pra original além do ruído e não estoura o tempo. Grava Logs/bots/pracc_SEED.txt.
    /// </summary>
    static class Pracc
    {
        struct R { public bool ganhou, plantou, tempo; }

        public static int Rodar(int k, int np, int seed0, string leitura, bool ct)
        {
            bool tr = !ct;
            var L = Leitura(leitura);
            var rng = new Rng(seed0);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ideias = Taticas.Ideias(tr, "full", L, 0.8f, k, rng);
            var sb = new StringBuilder();
            sb.AppendLine($"pracc {(tr ? "TR" : "CT")} · leitura '{(leitura == "" ? "nenhuma" : leitura)}' · {ideias.Count} ideias · {np} rounds cada contra a original (mesmas sementes {seed0}+)");
            sb.AppendLine();
            var rep = new Repertorio();
            int ok = 0;
            foreach (var id in ideias)
            {
                var pai = Estrategias.Todos[id.pai];
                var ri = Jogar(id, tr, np, seed0); var rp = Jogar(pai, tr, np, seed0);
                var d = Enumerable.Range(0, np).Select(i => (tr ? (ri[i].ganhou ? 1 : 0) : (ri[i].ganhou ? 0 : 1)) - (double)(tr ? (rp[i].ganhou ? 1 : 0) : (rp[i].ganhou ? 0 : 1))).ToArray();
                double md = d.Average(), se = Math.Sqrt(d.Select(x => (x - md) * (x - md)).Sum() / Math.Max(1, np - 1) / np);
                double wi = ri.Count(r => r.ganhou == tr) * 100.0 / np, wp = rp.Count(r => r.ganhou == tr) * 100.0 / np;
                double pi = ri.Count(r => r.plantou) * 100.0 / np, pp = rp.Count(r => r.plantou) * 100.0 / np;
                double ti = ri.Count(r => r.tempo) * 100.0 / np, tp = rp.Count(r => r.tempo) * 100.0 / np;
                bool entra = md >= -se && ti <= tp + 5;
                if (entra) { ok++; rep.Adiciona(id, (float)Math.Clamp(0.5 + 2 * md, 0.2, 0.9)); }
                sb.AppendLine($"{(entra ? "ENTRA" : "fora ")}  {id.Nome}");
                sb.AppendLine($"       vitória {wi:0}% x original #{pai.idx} {wp:0}%  (dif {100 * md:+0;-0} ± {100 * se:0}) · plant {pi:0}% x {pp:0}% · tempo acabou {ti:0}% x {tp:0}%");
                sb.Append(Taticas.Resumo(id));
                sb.AppendLine();
                Console.WriteLine($"{(entra ? "ENTRA" : "fora ")} dif {100 * md:+0;-0}±{100 * se:0}  {id.Nome}  ({sw.Elapsed.TotalSeconds:0} s)");
            }
            sb.Insert(0, $"{ok} de {ideias.Count} entraram no repertório ({sw.Elapsed.TotalSeconds:0} s)\n");
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText($"Logs/bots/pracc_{seed0}.txt", sb.ToString());
            Console.WriteLine($"{ok} de {ideias.Count} entraram · Logs/bots/pracc_{seed0}.txt");
            return 0;
        }

        static R[] Jogar(Estrategias.Plano p, bool tr, int np, int seed0)
        {
            var r = new R[np];
            Parallel.For(0, np, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
            {
                if (tr) TeamBrain.PlanoTR = p; else TeamBrain.PlanoCT = p;
                try
                {
                    var m = new Match(seed0 + i);
                    while (!m.over && m.t < 200) m.Step();
                    r[i] = new R { ganhou = m.over && m.winner == "TR", plantou = m.bombPlanted, tempo = m.reason == "tempo acabou" };
                }
                finally { TeamBrain.PlanoTR = null; TeamBrain.PlanoCT = null; }
            });
            return r;
        }

        /// <summary>Leitura de mentira pro treino: o que o analista diz que o adversário faz.</summary>
        static Leitura Leitura(string s)
        {
            if (s == "") return null;
            var L = new Leitura();
            switch (s)
            {
                case "stackA": L.ctRounds = 6; L.ctStackA = 4; L.ctVisto = 6; L.ctEmA = 3f; L.ctEmB = 1f; L.intencao.lado = -0.4f; break;
                case "stackB": L.ctRounds = 6; L.ctStackB = 4; L.ctVisto = 6; L.ctEmA = 1f; L.ctEmB = 3f; L.intencao.lado = 0.4f; break;
                case "cedo": L.ctRounds = 6; L.ctAgressivo = 4; L.intencao.ritmo = 0.3f; break;
                case "passivo": L.ctRounds = 6; L.ctAgressivo = 0; break;
                case "meio":
                    {
                        string mid = CSM.Map.MapData.areas.Select(a => a.name).FirstOrDefault(a => Estrategias.ZonaDaArea(a) == "Mid" && Estrategias.Todos.Any(p => p.tr && p.js.Any(j => j.passos.Any(x => x.area == a))));
                        if (mid != null) { var c = Estrategias.Centro(mid); L.rodada = 6; for (int i = 0; i < 4; i++) L.mortes.Add((c, Team.T, 3 + i)); }
                        break;
                    }
                case "A": L.trRounds = 6; L.trA = 5; L.trB = 1; break;
                case "B": L.trRounds = 6; L.trA = 1; L.trB = 5; break;
                case "lento": L.trRounds = 6; L.trLento = 4; L.trA = 3; L.trB = 3; break;
            }
            return L;
        }
    }
}
