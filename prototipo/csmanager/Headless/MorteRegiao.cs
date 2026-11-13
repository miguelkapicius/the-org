using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// "Morreu na região do colega" medido como no pro (sim-apoio, 2026-11-08; Tools/rotas_humanas/morte_regiao.py): pra cada morte
    /// e cada colega vivo a 0-2 regiões dela, o que ele faz nos 6 s seguintes — entra na região (quando; sozinho ou com outro),
    /// fica, recua, vai por outra região, ou morre sem sair. Mesmas regiões (RotaHumana) e mesmo grafo (transições do pro).
    /// Uso: Headless -morteRegiao 200 -seed S [-exp ...] [-saida nome] → Logs/bots/morteregiao_&lt;saida&gt;.txt
    /// </summary>
    static class MorteRegiao
    {
        const float JAN = 6f;
        static string Fz(Agent a, float t) { var b = a.brain; string f = b.fase ?? ""; if (f.Length == 0) f = "(" + (b.order?.name ?? "?") + ")"; int i = f.IndexOfAny(new[] { ',', '·', ':' }); if (i > 0) f = f.Substring(0, i); return f.Trim() + (b.tradeUntil > t ? " [troca]" : ""); }
        public static int Rodar(int n, int seed0, string saida)
        {
            var res = new Dictionary<string, Dictionary<string, int>>(); var tempos = new Dictionary<string, List<(float t, bool junto)>>();
            object trava = new object(); var fases = new Dictionary<string, Dictionary<string, int>>();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); int N = m.agents.Count;
                var tl = new List<(string r, float a)>[N]; for (int k = 0; k < N; k++) tl[k] = new List<(string, float)>(); var fz = new List<(float a, string f)>[N]; for (int k = 0; k < N; k++) fz[k] = new List<(float, string)>();
                var morte = new float[N]; Array.Fill(morte, -1); var rMorte = new string[N]; float tPlant = 1e9f; int passo = 0;
                while (!m.over && m.t < 200)
                {
                    m.Step(); if (++passo % 2 != 0) continue;
                    if (m.bombPlanted && tPlant > 1e8f) tPlant = m.t;
                    foreach (var a in m.agents)
                    {
                        if (!a.alive) { if (morte[a.id] < 0) { morte[a.id] = m.t; rMorte[a.id] = tl[a.id].Count > 0 ? tl[a.id][^1].r : RotaHumana.Regiao(a.pos); } continue; }
                        string r = RotaHumana.Regiao(a.pos); if (r == null) continue;
                        if (tl[a.id].Count == 0 || tl[a.id][^1].r != r) tl[a.id].Add((r, m.t));
                        string ff = Fz(a, m.t); if (fz[a.id].Count == 0 || fz[a.id][^1].f != ff) fz[a.id].Add((m.t, ff));
                    }
                }
                bool mesmoMat(Agent x, Agent y) { Agent kx = null, ky = null; foreach (var ab in m.abates) { if (ab.v == x) kx = ab.by; if (ab.v == y) ky = ab.by; } return kx != null && kx == ky; }
                string FzEm(int id, float t) { string s = "?"; foreach (var (a, f) in fz[id]) { if (a > t) break; s = f; } return s; }
                string Onde(int id, float t) { string s = null; foreach (var (r, a) in tl[id]) { if (a > t) break; s = r; } return s; }
                var loc = new List<(string k, string fim, float? te, bool junto, bool morreuLa)>();
                foreach (var x in m.agents)
                {
                    float dm = morte[x.id]; if (dm < 0 || rMorte[x.id] == null) continue; string rm = rMorte[x.id];
                    string plant = dm >= tPlant ? "pós-plant" : "antes do plant";
                    foreach (var y in m.agents)
                    {
                        if (y == x || y.team != x.team || (morte[y.id] >= 0 && morte[y.id] <= dm)) continue;
                        string ry = Onde(y.id, dm); if (ry == null) continue;
                        int dd = RotaHumana.Distancia(ry, rm); if (dd > 2) continue;
                        string antes = Onde(y.id, dm - 3); bool indo = antes != null && RotaHumana.Distancia(antes, rm) > dd;
                        string fim = "fica"; float? te = null; string fase = null; bool morreu = morte[y.id] >= 0 && morte[y.id] <= dm + JAN;
                        foreach (var (r, a) in tl[y.id])
                        {
                            if (a <= dm || a > dm + JAN) continue;
                            if (morreu && morte[y.id] < a) break;
                            if (r == ry) continue;
                            if (r == rm) { fim = "entra em R"; te = a - dm; fase = FzEm(y.id, a - 0.3f); }
                            else if (RotaHumana.Distancia(r, rm) > dd) fim = "recua";
                            else fim = "outra região (mesma dist. ou mais perto)";
                            break;
                        }
                        if (fim == "fica" && morreu) { fim = "morre (sem sair)"; fase = "morreu: " + FzEm(y.id, morte[y.id] - 0.3f) + (mesmoMat(x, y) ? " · mesmo matador" : ""); }
                        if (dd == 0 && (fim == "fica" || fim == "entra em R")) fim = "fica em R";
                        bool junto = false, morreuLa = false;
                        if (te is float tt)
                        {
                            foreach (var z in m.agents) if (z != x && z != y && z.team == x.team && tl[z.id].Any(q => q.r == rm && MathF.Abs(q.a - (dm + tt)) <= 1.5f)) { junto = true; break; }
                            morreuLa = morte[y.id] >= 0 && morte[y.id] <= dm + tt + JAN;
                        }
                        string kk = $"{(x.IsT ? "T" : "CT")} | {plant} | {dd} | {(indo ? "já ia pra lá" : "não ia")}"; loc.Add((kk, fim, te, junto, morreuLa));
                        if (fase != null) lock (trava) { if (!fases.TryGetValue(kk, out var fd)) fases[kk] = fd = new Dictionary<string, int>(); fd.TryGetValue(fase, out int fc); fd[fase] = fc + 1; }
                    }
                }
                lock (trava)
                    foreach (var (k, fim, te, junto, morreuLa) in loc)
                    {
                        if (!res.TryGetValue(k, out var d)) res[k] = d = new Dictionary<string, int>();
                        d.TryGetValue(fim, out int c); d[fim] = c + 1;
                        if (morreuLa) { d.TryGetValue("…e morreu em R", out int c2); d["…e morreu em R"] = c2 + 1; }
                        if (te is float tt) { if (!tempos.TryGetValue(k, out var l)) tempos[k] = l = new List<(float, bool)>(); l.Add((tt, junto)); }
                    }
            });
            var sb = new StringBuilder(); sb.AppendLine($"morreu na região do colega — {n} rounds (seed {seed0}, exp: {TeamBrain.EXP})");
            foreach (var k in res.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var c = res[k]; int tot = c.Where(kv => !kv.Key.StartsWith("…")).Sum(kv => kv.Value); if (tot < 30) continue;
                var tv = tempos.TryGetValue(k, out var l) ? l.Select(q => q.t).OrderBy(q => q).ToList() : new List<float>();
                int jt = l?.Count(q => q.junto) ?? 0;
                string Q(float p) => tv.Count > 0 ? tv[Math.Min(tv.Count - 1, (int)(p * tv.Count))].ToString("0.0") : "-";
                sb.AppendLine($"{k}  (n={tot})");
                sb.AppendLine("    " + string.Join(" · ", c.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {100.0 * kv.Value / tot:0.0}%")));
                if (fases.TryGetValue(k, out var fd2)) sb.AppendLine("    fases: " + string.Join(" · ", fd2.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key} {kv.Value}")));
                sb.AppendLine($"    entrou <1,5 s {100.0 * tv.Count(t => t < 1.5f) / tot:0.0}% · 1,5-6 s {100.0 * tv.Count(t => t >= 1.5f) / tot:0.0}% · tempo p25/p50/p75 [{Q(.25f)}, {Q(.5f)}, {Q(.75f)}] · junto {100.0 * jt / Math.Max(1, tv.Count):0.0}%");
            }
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/morteregiao_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
