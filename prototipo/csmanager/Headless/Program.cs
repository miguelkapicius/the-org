using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Rounds em massa sem o Unity: dotnet run -c Release --project Headless -- -n 2000 -seed 900 [-exp nome]
    /// Grava o relatório do validador em Logs/bots/headless.txt.
    /// </summary>
    public static class Program
    {
        static string Arg(string[] a, string n, string d) { for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return d; }

        /// <summary>Vitórias de c contra b em n rounds de cada lado (mesmas sementes, sementes novas a cada passo).</summary>
        static float Duelo(Pesos c, Pesos b, int n, int s0)
        {
            int ganhou = 0;
            Parallel.For(0, 2 * n, i =>
            {
                bool cTr = i < n; int seed = s0 + (i % n);
                var m = new Match(seed, cTr ? c : b, cTr ? b : c);
                while (!m.over && m.t < 200) m.Step();
                if (m.winner == (cTr ? "TR" : "CT")) Interlocked.Increment(ref ganhou);
            });
            return ganhou / (2f * n);
        }

        /// <summary>
        /// Treino por vitória: a versão nova joga contra a atual dos dois lados e só fica se ganhar acima do ruído, confirmando
        /// em rounds novos. -treino -passos P -n N (N rounds por lado). Salva em Logs/bots/pesos_melhor.txt a cada avanço.
        /// </summary>
        static int Treino(int passos, int n, int seed0)
        {
            var rng = new Rng(seed0); var best = Pesos.Padrao().Clone(); var inicio = best.Clone(); float sigma = 0.15f; int K = Pesos.Campos.Length, s = seed0 + 100000;
            Directory.CreateDirectory("Logs/bots"); var log = new StringBuilder(); var sw = Stopwatch.StartNew();
            void Linha(string t) { Console.WriteLine(t); log.AppendLine(t); File.WriteAllText("Logs/bots/treino_vitoria.txt", log.ToString()); }
            Linha($"TREINO POR VITÓRIA: {passos} passos, {2 * n} rounds por teste");
            for (int it = 0; it < passos; it++)
            {
                var c = best.Clone(); var mexeu = new System.Collections.Generic.List<string>();
                for (int j = 1 + rng.Int(3); j > 0; j--) { int i = rng.Int(K); c.SetN(i, c.GetN(i) + rng.Normal(0, sigma)); mexeu.Add(Pesos.Campos[i].Name); }
                float w = Duelo(c, best, n, s); s += n; bool ok = false;
                if (w > 0.53f) { float w2 = Duelo(c, best, n, s); s += n; ok = (w + w2) / 2 > 0.525f; w = (w + w2) / 2; }
                if (ok) { best = c; sigma = MathF.Min(0.35f, sigma * 1.3f); File.WriteAllText("Logs/bots/pesos_melhor.txt", best.Texto()); } else sigma = MathF.Max(0.05f, sigma * 0.92f);
                Linha($"{it,3} {(ok ? "MELHOROU" : "        ")} {w:0.000}  passo {sigma:0.00}  {string.Join(", ", mexeu)}  [{sw.Elapsed.TotalMinutes:0} min]");
            }
            Linha($"VALIDAÇÃO: treinado x inicial em rounds nunca vistos: {Duelo(best, inicio, 2 * n, s):0.000} (0,500 = igual)");
            return 0;
        }

        /// <summary>Partidas inteiras: A contra B em cada modo de adaptação. Mede vitórias, rounds por trecho e se ponto repetido perde força.</summary>
        static int Partidas(int n, int seed0, string modoA, string modoB, string saida)
        {
            Partida.ModoA = modoA; Partida.ModoB = modoB;
            int vA = 0, roundsT = 0, winsT = 0, rs = 0; var trecho = new int[4, 2]; var rep = new int[4, 2]; var planos = new System.Collections.Generic.SortedDictionary<string, int[]>(); var totEv = new System.Collections.Generic.SortedDictionary<string, int>(); var tr = new object(); var sw = Stopwatch.StartNew();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var evs = new System.Collections.Generic.Dictionary<string, int>();
                var p = new Partida(seed0 + i); p.Jogar(m => { foreach (var k in m.ev) if (k.Key.Contains("lembrado") || k.Key.Contains("queimado")) { evs.TryGetValue(k.Key, out int v); evs[k.Key] = v + k.Value; } });
                lock (tr)
                {
                    if (p.vencedor == p.A) vA++;
                    foreach (var k in evs) { totEv.TryGetValue(k.Key, out int v); totEv[k.Key] = v + k.Value; }
                    foreach (var h in p.historico)
                    {
                        foreach (var (lado, plano, ganhouLado) in new[] { (h.ladoA == "TR" ? "A de TR" : "A de CT", h.ladoA == "TR" ? h.planoT : h.planoCT, h.timeAvenceu), (h.ladoA == "TR" ? "B de CT" : "B de TR", h.ladoA == "TR" ? h.planoCT : h.planoT, !h.timeAvenceu) })
                        {
                            string ch = $"{lado}: {plano}{(h.compraT == "full" && h.compraCT == "full" ? " (cheia x cheia)" : "")}"; if (!planos.TryGetValue(ch, out var v)) planos[ch] = v = new int[2];
                            v[0]++; if (ganhouLado) v[1]++;
                        }
                        rs++; bool tGanhou = (h.ladoA == "TR") == h.timeAvenceu; roundsT += 1; if (tGanhou) winsT++;
                        int k = h.n <= 6 ? 0 : h.n <= 12 ? 1 : h.n <= 18 ? 2 : 3; trecho[k, 0]++; if (h.timeAvenceu) trecho[k, 1]++;
                    }
                    for (int k = 0; k < 4; k++) for (int c = 0; c < 2; c++) rep[k, c] += p.A.leitura.repete[k, c] + p.B.leitura.repete[k, c];
                }
            });
            var sb = new StringBuilder();
            sb.AppendLine($"{n} partidas, {rs} rounds, {sw.Elapsed.TotalMinutes:0.0} min. A [{modoA}] x B [{modoB}]");
            sb.AppendLine($"A ganhou {100f * vA / n:0.0}% das partidas; TR ganhou {100f * winsT / roundsT:0.0}% dos rounds");
            string[] nomes = { "rounds 1-6", "rounds 7-12", "rounds 13-18", "rounds 19+" };
            for (int k = 0; k < 4; k++) sb.AppendLine($"  {nomes[k],-13} A vence {100f * trecho[k, 1] / Math.Max(1, trecho[k, 0]):0.0}% de {trecho[k, 0]}");
            sb.AppendLine("  ponto usado de novo (matou x morreu ali):");
            for (int k = 0; k < 4; k++) sb.AppendLine($"    já matou dali {(k == 3 ? "3+" : k.ToString())} vez(es): {rep[k, 0]} x {rep[k, 1]} = {100f * rep[k, 0] / Math.Max(1, rep[k, 0] + rep[k, 1]):0.0}%");
            foreach (var k in totEv) sb.AppendLine($"  {k.Key,-44} {k.Value / (float)Math.Max(1, rs):0.00}");
            foreach (var kv in planos) if (kv.Value[0] >= 8) sb.AppendLine($"  {kv.Key,-34} {100f * kv.Value[1] / kv.Value[0]:0.0}% de {kv.Value[0]}");
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/{saida}.txt", sb.ToString()); Console.Write(sb);
            return 0;
        }

        public static int Main(string[] a)
        {
            int n = int.Parse(Arg(a, "-n", "320")), seed0 = int.Parse(Arg(a, "-seed", "900"));
            TeamBrain.EXP = Arg(a, "-exp", "");
            if (Array.IndexOf(a, "-auditar") >= 0) { Match.Auditar = true; Brain.Explicar = true; }
            var ini = Stopwatch.StartNew();
            new Match(seed0); Spots.All();
            Console.WriteLine($"mapa pronto em {ini.Elapsed.TotalSeconds:0.0} s");
            if (Array.IndexOf(a, "-los") >= 0)
            {   // de que quadradinhos da rampa o TR enxerga a quina do CT (# = enxerga)
                V2 h = new V2(37.5f * MapData.Q, 11f * MapData.Q);
                for (float y = 15f; y <= 24f; y += 0.5f)
                {
                    var sb = new StringBuilder($"{y,5:0.0} ");
                    for (float x = 30f; x <= 38.5f; x += 0.25f) { var p = new V2(x * MapData.Q, y * MapData.Q); sb.Append(!NavGrid.Movable(p) ? ' ' : MapData.LosWalk(p, NavGrid.ZAt(p), h, NavGrid.ZAt(h), 18f) ? '#' : '.'); }
                    Console.WriteLine(sb);
                }
            }
            if (Array.IndexOf(a, "-tempos") >= 0)
            {
                var alvos = new (string n, V2 p)[] { ("bomb A", Places.PlantA), ("bomb B", Places.PlantB), ("meio", Places.Meio_Norte), ("topo da rampa", Places.A_Rampa), ("tapete", Places.B_Tapete), ("curta", Places.B_Curta), ("conector", Places.A_Conector), ("janelão", Places.Janelao) };
                foreach (var (nome, p) in alvos)
                {
                    float M(V2[] sp) { float s = 0; foreach (var q in sp) s += NavGrid.PathLen(q, p); return s / sp.Length / MapCheck.RUN; }
                    Console.WriteLine($"{nome,-16} TR {M(MapData.SPAWN_T),5:0.0} s   CT {M(MapData.SPAWN_CT),5:0.0} s");
                }
                Console.WriteLine($"A -> B {NavGrid.PathLen(Places.PlantA, Places.PlantB) / MapCheck.RUN:0.0} s");
                return 0;
            }
            if (Array.IndexOf(a, "-partidas") >= 0) return Partidas(int.Parse(Arg(a, "-partidas", "100")), seed0, Arg(a, "-modoA", ""), Arg(a, "-modoB", ""), Arg(a, "-saida", "adapta"));
            if (Array.IndexOf(a, "-treino") >= 0) return Treino(int.Parse(Arg(a, "-passos", "60")), n, seed0);
            var mt = new Metricas(); var trava = new object(); int feitos = 0;
            bool aprende = Array.IndexOf(a, "-aparece") >= 0; float[] somaAp = aprende ? Aparece.Vazio() : null;
            var sw = Stopwatch.StartNew();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var m = new Match(seed0 + i);
                while (!m.over && m.t < 200) m.Step();
                lock (trava) { if (aprende) Aparece.Soma(somaAp, m); mt.Add(m); if (++feitos % 500 == 0) Console.WriteLine($"{feitos} rounds, {feitos / sw.Elapsed.TotalSeconds:0.0}/s"); }
            });
            if (aprende) Aparece.Grava(somaAp);
            double rs = n / sw.Elapsed.TotalSeconds;
            string r = mt.Relatorio();
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/" + Arg(a, "-saida", "headless") + ".txt", $"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia\n" + r);
            Console.WriteLine($"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia");
            return 0;
        }
    }
}
