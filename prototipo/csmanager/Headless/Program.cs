using System;
using System.Collections.Generic;
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
        // -cpu: tempo do Step em ciclos da thread (não conta o tempo em que o SO tirou a thread da CPU: com outros headless
        // rodando, o relógio de parede dava passos de 400-800 ms com 0 A* — era a máquina ocupada, não o passo)
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryThreadCycleTime(IntPtr h, out ulong c);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
        static bool CPU; static double CICLOS_MS = 1;
        static void CalibraCpu()
        {
            double melhor = 0; var sw = new Stopwatch();
            for (int k = 0; k < 8; k++)
            {
                QueryThreadCycleTime(GetCurrentThread(), out ulong c0); sw.Restart(); while (sw.Elapsed.TotalMilliseconds < 40) { }
                QueryThreadCycleTime(GetCurrentThread(), out ulong c1); melhor = Math.Max(melhor, (c1 - c0) / sw.Elapsed.TotalMilliseconds);
            }
            // o contador de ciclos da thread anda na frequência nominal (TSC): com a máquina ocupada a calibração pelo relógio
            // sai baixa (a thread não roda o tempo todo) — usa a frequência do registro quando tem
            double mhz = 0; try { mhz = Convert.ToDouble(Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz", 0)); } catch { }
            CICLOS_MS = mhz > 0 ? mhz * 1000 : melhor; Console.WriteLine($"-cpu: {CICLOS_MS / 1000:0} mil ciclos por ms (calibrado {melhor / 1000:0})");
        }
        static double LIMIAR_DETALHE = 100;   // -perf: Step acima disso (ms) sai detalhado (-detalhe N)

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
                var p = new Partida(seed0 + i); p.Jogar(m => { foreach (var k in m.ev) if (k.Key.Contains("lembrado") || k.Key.Contains("queimado") || k.Key.StartsWith("FIM") || k.Key.StartsWith("PLANT") || k.Key.Contains("guardou")) { evs.TryGetValue(k.Key, out int v); evs[k.Key] = v + k.Value; }
                    void Soma(string c, int x) { evs.TryGetValue(c, out int v); evs[c] = v + x; }   // sinais do visual: compras no freeze e clique seco
                    Soma("SINAL rounds", 1); foreach (var dr in m.drops) if (dr.para != null && dr.de != null && dr.de.team == dr.para.team && dr.t < 15) Soma("ECO drop da compra que ninguém pegou", 1); foreach (var dr in m.drops) if (dr.t < 20 && !WeaponStats.Pistola(dr.gun) && ((dr.pos - CSM.Map.MapData.SPAWN_T[2]).Len < 320 || (dr.pos - CSM.Map.MapData.SPAWN_CT[2]).Len < 320)) Soma("ECO arma primária esquecida na base (" + (dr.para != null ? "drop pra alguém" : dr.de != null ? "largada por " + dr.de.brain.order.name.Split(':')[0] : "sem dono") + ")", 1); Soma("SINAL compras", m.compras.Count); Soma("SINAL cliques secos", m.cliques.Count);
                    foreach (var g in System.Linq.Enumerable.GroupBy(m.compras, c => c.quem)) { var ts = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(g, c => c.t)); for (int q = 1; q < ts.Count; q++) if (ts[q] - ts[q - 1] < 0.25f) Soma("SINAL compra colada (<0,25 s)", 1); if (ts.Count > 0 && (ts[0] < -Match.FREEZE || ts[^1] > 0)) Soma("SINAL compra fora do freeze", 1); }
                });
                lock (tr)
                {
                    if (p.vencedor == p.A) vA++;
                    foreach (var e in p.ev) { totEv.TryGetValue(e, out int ve); totEv[e] = ve + 1; }   // decisões de economia
                    foreach (var h in p.historico)
                    {   // compra x vitória por lado (o que a economia rende)
                        bool aT = h.ladoA == "TR"; bool tGanhou = aT == h.timeAvenceu;
                        if (h.compraT == "full" && h.compraCT == "full") { totEv.TryGetValue("ECO cheia x cheia", out int c1); totEv["ECO cheia x cheia"] = c1 + 1; if (tGanhou) { totEv.TryGetValue("ECO cheia x cheia: TR venceu", out int c2); totEv["ECO cheia x cheia: TR venceu"] = c2 + 1; } }
                        foreach (var (k, g) in new[] { ("ECO compra TR " + h.compraT, tGanhou), ("ECO compra CT " + h.compraCT, !tGanhou) })
                        { totEv.TryGetValue(k, out int v1); totEv[k] = v1 + 1; if (g) { totEv.TryGetValue(k + " venceu", out int v2); totEv[k + " venceu"] = v2 + 1; } }
                    }
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
            for (int t = 0; t < 2; t++) { var D = Partida.Diag; float r = Math.Max(1, D[t, 0]); sb.AppendLine($"  compra cheia {(t == 0 ? "TR" : "CT")}: {D[t, 0]} rounds, AWP com o AWPer {100 * D[t, 1] / r:0}%, primária {D[t, 2] / r:0.0}/5, capacete {D[t, 3] / r:0.0}/5, granadas {D[t, 4] / r:0.0}, kits {D[t, 5] / r:0.0}"); }
            string[] nomes = { "rounds 1-6", "rounds 7-12", "rounds 13-18", "rounds 19+" };
            for (int k = 0; k < 4; k++) sb.AppendLine($"  {nomes[k],-13} A vence {100f * trecho[k, 1] / Math.Max(1, trecho[k, 0]):0.0}% de {trecho[k, 0]}");
            sb.AppendLine("  ponto usado de novo (matou x morreu ali):");
            for (int k = 0; k < 4; k++) sb.AppendLine($"    já matou dali {(k == 3 ? "3+" : k.ToString())} vez(es): {rep[k, 0]} x {rep[k, 1]} = {100f * rep[k, 0] / Math.Max(1, rep[k, 0] + rep[k, 1]):0.0}%");
            foreach (var k in totEv) sb.AppendLine($"  {k.Key,-44} {k.Value / (float)Math.Max(1, rs):0.00}");
            for (int sd = 0; sd < 2; sd++) { var D = Partida.Diag; float n0 = Math.Max(1, D[sd, 0]); sb.AppendLine($"  COMPRA CHEIA {(sd == 0 ? "TR" : "CT")}: rounds {D[sd, 0]} | AWP no AWPer {D[sd, 1] / n0:0.00} | com primária {D[sd, 2] / n0:0.00}/5 | capacete {D[sd, 3] / n0:0.00}/5 | granadas {D[sd, 4] / n0:0.00} | kits {D[sd, 5] / n0:0.00}"); }
            foreach (var kv in planos) if (kv.Value[0] >= 8) sb.AppendLine($"  {kv.Key,-34} {100f * kv.Value[1] / kv.Value[0]:0.0}% de {kv.Value[0]}");
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/{saida}.txt", sb.ToString()); Console.Write(sb);
            return 0;
        }

        static string Exemplos()
        {
            var sb = new StringBuilder("\nEXEMPLOS DE SUSPEITA (semente do round avulso)\n");
            // em ordem (a ordem de chegada dependia das threads)
            var lista = Match.Exemplos(); lista.Sort((x, y) => string.CompareOrdinal(x.tipo, y.tipo));
            foreach (var (tipo, ex) in lista) { sb.AppendLine("  " + tipo); var o = new List<string>(ex); o.Sort(string.CompareOrdinal); foreach (var e in o) sb.AppendLine("    " + e); }
            return sb.ToString();
        }
        public static int Main(string[] a)
        {
            int n =int.Parse(Arg(a, "-n", "320")), seed0 = int.Parse(Arg(a, "-seed", "900"));
            TeamBrain.EXP = Arg(a, "-exp", ""); NavGrid.PathLenSoCampo = TeamBrain.EXP.Contains("pathLenCampo"); Partida.SemLeitura = Array.IndexOf(a, "-semLeitura") >= 0;
            NavGrid.PorRound = !TeamBrain.EXP.Contains("cacheDivididoVelho");
            if (Arg(a, "-armaCT", "") != "") Match.ArmaCT = Arg(a, "-armaCT", "");   // teste de arma: todo CT (menos o AWPer) com ela   // determinístico: cache de campos, fila e orçamento por round
            if (Array.IndexOf(a, "-auditar") >= 0) { Match.Auditar = true; Brain.Explicar = true; }
            if (Array.IndexOf(a, "-soAuditar") >= 0) Match.Auditar = true;     // teste: só a auditoria (sem os porquês do cérebro)
            if (Array.IndexOf(a, "-soExplicar") >= 0) Brain.Explicar = true;   // teste: só os porquês
            var ini = Stopwatch.StartNew();
            new Match(seed0); Spots.All();
            if (Array.IndexOf(a, "-aquecer") >= 0) Console.WriteLine(Match.Aquecer());
            Console.WriteLine($"mapa pronto em {ini.Elapsed.TotalSeconds:0.0} s (marcos do A* {NavGrid.msMarcos:0} ms)");
            if (Array.IndexOf(a, "-degraus") >= 0)
            {   /* degraus entre células vizinhas andáveis: quantos o pulo normal (18) não sobe e o pulo agachado do CS (~22,9) subiria */
                var faixa = new SortedDictionary<int, int>(); var onde = new Dictionary<string, int>(); var bordas = new Dictionary<string, int>();
                for (int x = 0; x < NavGrid.NGX - 1; x++)
                    for (int y = 0; y < NavGrid.NGY - 1; y++)
                    {
                        if (!NavGrid.move[x, y]) continue;
                        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                        {
                            int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || !NavGrid.move[nx, ny]) continue;
                            float dh = NavGrid.z[nx, ny] - NavGrid.z[x, y]; if (dh <= 3.5f) continue;
                            int b = (int)MathF.Floor(dh / 2) * 2; faixa.TryGetValue(b, out int c0); faixa[b] = c0 + 1;
                            if (dh > 23f && dh <= 48f) { string ar2 = (MapData.AreaAt(x * NavGrid.CELL + 1, y * NavGrid.CELL + 1) ?? "?") + "→" + (MapData.AreaAt(nx * NavGrid.CELL + 1, ny * NavGrid.CELL + 1) ?? "?") + $" (+{(int)dh})"; bordas.TryGetValue(ar2, out int c2); bordas[ar2] = c2 + 1; }
                            if (dh > 18f && dh <= 23f) { string ar = MapData.AreaAt(x * NavGrid.CELL + 1, y * NavGrid.CELL + 1) ?? "?"; onde.TryGetValue(ar, out int c1); onde[ar] = c1 + 1; }
                        }
                    }
                foreach (var kv in faixa) Console.WriteLine($"  sobe {kv.Key}-{kv.Key + 2}: {kv.Value} bordas");
                Console.WriteLine("  só de pezinho (23-48), por área: " + string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderByDescending(bordas, kv => kv.Value), kv => $"{kv.Key} {kv.Value}")));
                { int nb = 0; var ilhas = new HashSet<int>(); for (int x = 0; x < NavGrid.NGX; x++) for (int y = 0; y < NavGrid.NGY; y++) if (NavGrid.soBoost[x, y]) { nb++; ilhas.Add(NavGrid.IlhaEm(NavGrid.Center(x, y))); } Console.WriteLine($"  pezinho: {nb} células em {ilhas.Count} topos de pilha"); }
                Console.WriteLine("  18-23 (só pulo agachado) por área: " + string.Join(", ", System.Linq.Enumerable.Select(onde, kv => $"{kv.Key} {kv.Value}")));
                return 0;
            }
            if (Array.IndexOf(a, "-abates") >= 0)
            {   // killfeed estruturado (Match.abates) de alguns rounds, pra conferir assistência, arma e flags
                for (int ra = 0; ra < n; ra++)
                {
                    var mk = new Match(seed0 + ra); while (!mk.over && mk.t < 200) mk.Step();
                    Console.WriteLine($"== semente {seed0 + ra}");
                    foreach (var k in mk.abates)
                        Console.WriteLine($"  {k.t,6:0.0}  {(k.by?.name ?? "-"),-8} [{k.arma}] {k.v.name,-8}{(k.hs ? " HS" : "")}{(k.assist != null ? $" + {k.assist.name}{(k.flashAssist ? " (flash)" : "")} [{k.v.danoDe[k.assist.id]}]" : "")}{(k.cegoQuemMatou ? " cego" : "")}{(k.semLuneta ? " sem luneta" : "")}{(k.atravesSmoke ? " pela smoke" : "")}{(k.noAr ? " no ar" : "")}{(k.aliado ? " ALIADO" : "")}{(k.suicidio ? " suicídio" : "")}");
                }
                return 0;
            }
            if (Array.IndexOf(a, "-vazamento") >= 0)
            {   // que estado estático muda de um round pro outro (o que vaza entre rounds na mesma thread)
                var campos = new List<System.Reflection.FieldInfo>();
                foreach (var tp in typeof(Match).Assembly.GetTypes())
                {
                    if (tp.Namespace != "CSM.Sim" && tp.Namespace != "CSM.Map" || tp.ContainsGenericParameters) continue;
                    foreach (var f in tp.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        if (!f.IsLiteral) campos.Add(f);
                }
                Dictionary<string, string> Foto()
                {
                    var r = new Dictionary<string, string>();
                    foreach (var f in campos) { object v; try { v = f.GetValue(null); } catch { continue; } r[f.DeclaringType.Name + "." + f.Name] = Resumo(v, 0); }
                    return r;
                }
                string Resumo(object v, int d)
                {
                    if (v == null) return "null";
                    if (v is string str) return str.GetHashCode().ToString();
                    if (v.GetType().IsPrimitive || v is V2) return v.ToString();
                    if (v is System.Collections.IEnumerable en && d < 2)
                    {
                        long h = 17; int c = 0;
                        foreach (var x in en) { unchecked { h = h * 31 + (x == null ? 0 : (x.GetType().IsValueType || x is string ? x.GetHashCode() : Resumo(x, d + 1).GetHashCode())); } c++; if (c > 200000) break; }
                        return $"n={c} h={h}";
                    }
                    if (d < 2 && !v.GetType().IsValueType)
                    {   // objeto: campos de instância rasos
                        long h = 17;
                        foreach (var f in v.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        { object w; try { w = f.GetValue(v); } catch { continue; } unchecked { h = h * 31 + Resumo(w, d + 1).GetHashCode(); } }
                        return "obj h=" + h;
                    }
                    return v.GetHashCode().ToString();
                }
                var f0 = Foto();
                var m0 = new Match(seed0); while (!m0.over && m0.t < 200) m0.Step();
                var f1 = Foto();
                Console.WriteLine("== mudou durante o round 1 (inclui o que é criado na 1ª vez):");
                foreach (var kv in f1) if (!f0.TryGetValue(kv.Key, out var o) || o != kv.Value) Console.WriteLine($"  {kv.Key}: {f0.GetValueOrDefault(kv.Key)} -> {kv.Value}");
                var m1 = new Match(seed0 + 1);
                var f2 = Foto();
                Console.WriteLine("== mudou entre o fim de um round e o começo do outro (depois do construtor):");
                foreach (var kv in f2) if (!f1.TryGetValue(kv.Key, out var o) || o != kv.Value) Console.WriteLine($"  {kv.Key}: {(f1.ContainsKey(kv.Key) ? f1[kv.Key] : "?")} -> {kv.Value}");
                while (!m1.over && m1.t < 200) m1.Step();
                var f3 = Foto();
                Console.WriteLine("== mudou durante o round 2:");
                foreach (var kv in f3) if (!f2.TryGetValue(kv.Key, out var o) || o != kv.Value) Console.WriteLine($"  {kv.Key}: {f2.GetValueOrDefault(kv.Key)} -> {kv.Value}");
                return 0;
            }
            if (Array.IndexOf(a, "-testaMarcos") >= 0) { Perigo.Build(); Console.WriteLine(NavGrid.TestaMarcos(3000, 1)); return 0; }
            if (Array.IndexOf(a, "-chafariz") >= 0)
            {   // navmesh em volta do chafariz: altura acima do chão por célula ('.' chão, dígito = +z, '#' não anda) e ligações
                foreach (var c in MapData.covers) if (c.kind == "fountain")
                {
                    var (x0, y0) = NavGrid.CellOf(new V2(c.r.x0 - 8, c.r.y0 - 8)); var (x1, y1) = NavGrid.CellOf(new V2(c.r.x1 + 8, c.r.y1 + 8));
                    for (int y = y0; y <= y1; y++)
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int x = x0; x <= x1; x++) { float dz = NavGrid.z[x, y] - MapData.GZ; sb.Append(!NavGrid.move[x, y] ? '#' : dz < 0.5f ? '.' : (char)('0' + Math.Min(9, (int)MathF.Round(dz)))); }
                        Console.WriteLine(sb);
                    }
                    var meio = new V2((c.r.x0 + c.r.x1) / 2f, (c.r.y0 + c.r.y1) / 2f);
                    foreach (var q in new[] { new V2(meio.x + 10.5f, meio.y), new V2(meio.x + 13, meio.y), new V2(meio.x + 16, meio.y), new V2(meio.x, meio.y - 14) })
                        Console.WriteLine($"ponto +({q.x - meio.x:0},{q.y - meio.y:0}) anda {NavGrid.Movable(q)} z {NavGrid.ZAt(q) - MapData.GZ:0.0} caminho do spawn {NavGrid.PathLen(MapData.SPAWN_T[2], q):0} (reta {(q - MapData.SPAWN_T[2]).Len:0})");
                }
                return 0;
            }
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
            if (Array.IndexOf(a, "-lineups") >= 0)
            {   // cada lineup do mapa com o arremesso do CS2: cai onde? de que jeito? quanto voa?
                foreach (var l in Places.TREINADAS)
                {
                    var pl = Arremesso.Resolve(l.from, NavGrid.ZAt(l.from), l.to, l.kind);
                    var ol = Arremesso.Resolve(l.from, NavGrid.ZAt(l.from), l.to, l.kind, Arremesso.Tipo.Andando);
                    Console.WriteLine($"{l.name,-38} {l.kind,-8} dist {(l.to - l.from).Len,4:0} | sabendo: erro {pl.erro,5:0.0} {pl} voa {pl.tempo:0.0}s | no olho: erro {ol.erro,5:0.0} {ol}");
                }
                return 0;
            }
            if (Array.IndexOf(a, "-banco") >= 0)
            {   // força de cada plano do banco (compra cheia): rounds normais, os dois lados escolhem do banco (sem a força
                // ainda, pra não viciar) e cada round conta pro plano do TR e pro do CT. Acumula em Logs/banco_forca.csv
                // (cada rodada soma às anteriores) e grava EstrategiasForca.cs
                var planos = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(Estrategias.Todos, p => p.compra == "full"));
                var pos = new System.Collections.Generic.Dictionary<int, int>(); for (int i = 0; i < planos.Count; i++) pos[planos[i].idx] = i;
                var vit = new int[planos.Count]; var jog = new int[planos.Count];
                const string CSV = "Logs/banco_forca.csv";
                if (File.Exists(CSV)) foreach (var l in File.ReadAllLines(CSV)) { var c = l.Split(';'); if (c.Length == 3 && int.TryParse(c[0], out int ix) && pos.TryGetValue(ix, out int pi)) { vit[pi] += int.Parse(c[1]); jog[pi] += int.Parse(c[2]); } }
                int feitosB = 0; var swB = System.Diagnostics.Stopwatch.StartNew(); var lk = new object();
                TeamBrain.EXP = (TeamBrain.EXP + ",forcaIgual").Trim(',');
                Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                {
                    var m = new Match(seed0 + i);
                    while (!m.over && m.t < 200) m.Step();
                    bool trGanhou = m.over && m.winner == "TR";
                    var pt = m.igl[(int)Team.T].Estrategia; var pc = m.igl[(int)Team.CT].Estrategia;
                    lock (lk)
                    {
                        if (pt != null && pos.TryGetValue(pt.idx, out int a1)) { jog[a1]++; if (trGanhou) vit[a1]++; }
                        if (pc != null && pos.TryGetValue(pc.idx, out int a2)) { jog[a2]++; if (!trGanhou) vit[a2]++; }
                    }
                    int f = Interlocked.Increment(ref feitosB); if (f % 500 == 0) Console.WriteLine($"{f}/{n} rounds ({swB.Elapsed.TotalSeconds:0} s)");
                });
                File.WriteAllLines(CSV, System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, planos.Count), i => $"{planos[i].idx};{vit[i]};{jog[i]}"));
                var sb = new StringBuilder();
                foreach (bool tr in new[] { true, false })
                {
                    double soma = 0; int n2 = 0;
                    for (int i = 0; i < planos.Count; i++) if (planos[i].tr == tr) { soma += vit[i]; n2 += jog[i]; }
                    double media = soma / Math.Max(1, n2);
                    var lista = new System.Collections.Generic.List<(double wr, int i)>();
                    for (int i = 0; i < planos.Count; i++)
                    {
                        if (planos[i].tr != tr) continue;
                        double wr = (vit[i] + media * 8) / (jog[i] + 8);   // encolhe pra média (poucos rounds ainda é ruído)
                        lista.Add((wr, i));
                        double peso = Math.Clamp(Math.Exp(4 * (wr - media)), 0.25, 3);
                        sb.Append(FormattableString.Invariant($"            ({planos[i].idx}, {peso:0.###}f),   // {(tr ? "TR" : "CT")} {planos[i].Tipo} bomb {planos[i].site}: venceu {vit[i]}/{jog[i]}\n"));
                    }
                    lista.Sort((x, y) => y.wr.CompareTo(x.wr));
                    Console.WriteLine($"{(tr ? "TR" : "CT")}: {lista.Count} planos, vitória média {100 * media:0.0}% · melhores " + string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(lista, 5), x => $"#{planos[x.i].idx} {planos[x.i].Tipo} {100 * x.wr:0}%")) + " · piores " + string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.Reverse(lista), 5), x => $"#{planos[x.i].idx} {planos[x.i].Tipo} {100 * x.wr:0}%")));
                }
                File.WriteAllText("Assets/Game/Sim/EstrategiasForca.cs",
                    "// Força medida de cada plano do banco no headless (Headless -banco; soma em Logs/banco_forca.csv). Gerado; não editar à mão.\n" +
                    "// Peso de escolha = exp(4 × (vitória encolhida − média do lado)), entre 0,25 e 3.\nusing System.Collections.Generic;\n\nnamespace CSM.Sim\n{\n    public static partial class Estrategias\n    {\n        static readonly (int idx, float forca)[] FORCA =\n        {\n" + sb +
                    "        };\n        static Dictionary<int, float> forcaMap;\n        /// <summary>Peso de escolha pela força medida (1 = sem medida).</summary>\n        public static float Forca(int idx)\n        {\n            if (forcaMap == null) { var d = new Dictionary<int, float>(); foreach (var (i, f) in FORCA) d[i] = f; forcaMap = d; }\n            return forcaMap.TryGetValue(idx, out var v) ? v : 1f;\n        }\n    }\n}\n");
                Console.WriteLine($"gravado Assets/Game/Sim/EstrategiasForca.cs ({swB.Elapsed.TotalSeconds:0} s)");
                return 0;
            }
            if (Array.IndexOf(a, "-regioes") >= 0)
            {   // cada região do mapa: centro, distância de caminho até cada bomb e base (pra classificar como as zonas da Mirage)
                var vistas = new System.Collections.Generic.HashSet<string>();
                foreach (var s in MapData.areas)
                {
                    if (!MapData.CALLOUT.TryGetValue(s.name, out var c) || !vistas.Add(c)) continue;
                    V2 p = NavGrid.Snap(new V2((s.box.x0 + s.box.x1) / 2f, (s.box.y0 + s.box.y1) / 2f));
                    float da = NavGrid.PathLen(p, Places.PlantA), db = NavGrid.PathLen(p, Places.PlantB), dt = NavGrid.PathLen(p, MapData.SPAWN_T[0]), dc = NavGrid.PathLen(p, MapData.SPAWN_CT[0]);
                    Console.WriteLine($"{c};{s.name};{p.x:0};{p.y:0};A {da:0};B {db:0};baseTR {dt:0};baseCT {dc:0};razão {da / (da + db):0.00}");
                }
                return 0;
            }
            if (Array.IndexOf(a, "-teoria") >= 0)
            {   // a mesma régua dos 643 rounds pro de Mirage (scratchpad teoria_mirage.py): formação por zona, ritmo, rotas no bomb, lurk
                var zcache = new System.Collections.Generic.Dictionary<int, string>();
                string Zona(V2 p)
                {
                    int k = Intel.Idx(p); if (zcache.TryGetValue(k, out var z)) return z;
                    z = Places.Zona(p); zcache[k] = z; return z;
                }
                string Form(Match m, bool t)
                {
                    int ca = 0, cm = 0, cb = 0, cs = 0;
                    foreach (var x in m.agents) if (x.alive && x.IsT == t) { var z = Zona(x.pos); if (z == "A") ca++; else if (z == "B") cb++; else if (z == "Mid") cm++; else cs++; }
                    return $"A{ca}-M{cm}-B{cb}" + (cs > 0 ? $"-base{cs}" : "");
                }
                var form = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>();
                void Conta(string ch, string v) { if (!form.TryGetValue(ch, out var d)) form[ch] = d = new System.Collections.Generic.Dictionary<string, int>(); d.TryGetValue(v, out int c); d[v] = c + 1; }
                int nr = n, plants = 0, ganhouT = 0; var tPlant = new System.Collections.Generic.List<float>(); var comp = new System.Collections.Generic.List<float>();
                for (int rr = 0; rr < nr; rr++)
                {
                    var m = new Match(seed0 + rr); float compT = -1; var rotas = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<(float t, string reg)>>();
                    bool agrCT = false;
                    while (!m.over && m.t < 200)
                    {
                        m.Step();
                        int ti = (int)MathF.Round(m.t * 32);
                        if (ti % 32 != 0) continue;
                        int s = ti / 32;
                        if (s == 15 || s == 25 || s == 35 || s == 45) Conta("T " + s, Form(m, true));
                        if (s == 20) Conta("CT 20", Form(m, false));
                        foreach (var x in m.agents)
                        {
                            if (!x.alive) continue;
                            if (x.IsT) { if (!rotas.TryGetValue(x.id, out var l)) rotas[x.id] = l = new System.Collections.Generic.List<(float, string)>(); l.Add((m.t, Places.Region(x.pos))); }
                            else if (s < 20 && Places.Region(x.pos) is "topo do meio" or "entrada do topo" or "meio" or "curta" or "rampa de baixo" or "saída da rampa" or "palácio" or "rua do casarão" or "casarão" or "corredor do casarão") agrCT = true;
                        }
                        if (!m.bombPlanted && compT < 0)
                        {
                            int no = 0;
                            foreach (var x in m.agents) if (x.alive && x.IsT && Zona(x.pos) == m.planSite.ToString()) no++;
                            if (no >= 3) compT = m.t;
                        }
                    }
                    Conta("CT agressão cedo", agrCT ? "sim" : "não");
                    if (m.winner == "T" || m.winner == "TR") ganhouT++;
                    if (m.bombPlanted)
                    {
                        plants++; tPlant.Add(m.plantedAt); if (compT >= 0) comp.Add(compT);
                        string site = Zona(m.bombPos); var regs = new System.Collections.Generic.HashSet<string>();
                        foreach (var l in rotas.Values) foreach (var (tt, reg) in l) if (tt > m.plantedAt - 12 && tt < m.plantedAt && !reg.StartsWith("bomb")) regs.Add(reg);
                        Conta("rotas no bomb", regs.Count >= 7 ? "7+" : regs.Count.ToString());
                        int outro = 0; foreach (var x in m.agents) if (x.alive && x.IsT && Zona(x.pos) != site && Zona(x.pos) != "Mid" && Zona(x.pos) != "Spawn") outro++;
                        Conta("TR no outro lado no plant", outro.ToString());
                        Conta("tipo", compT >= 0 && compT < 25 ? "rush" : compT >= 0 && compT < 50 ? "execute" : "default/tarde");
                    }
                    else Conta("tipo", "sem plant");
                }
                float Q(System.Collections.Generic.List<float> l, float q) { if (l.Count == 0) return 0; l.Sort(); return l[(int)(q * (l.Count - 1))]; }
                Console.WriteLine($"rounds {nr} · vitória TR {100f * ganhouT / nr:0.0}% · plant {100f * plants / nr:0.0}% · plant p25/50/75 {Q(tPlant, .25f):0}/{Q(tPlant, .5f):0}/{Q(tPlant, .75f):0} s · comprometeu p25/50/75 {Q(comp, .25f):0}/{Q(comp, .5f):0}/{Q(comp, .75f):0} s");
                foreach (var kv in form)
                {
                    int tot = 0; foreach (var v in kv.Value.Values) tot += v;
                    var top = System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(kv.Value, x => x.Value), 8);
                    Console.WriteLine($"  {kv.Key}: " + string.Join(", ", System.Linq.Enumerable.Select(top, x => $"{x.Key} {100f * x.Value / tot:0.0}%")));
                }
                return 0;
            }
            if (Array.IndexOf(a, "-folga") >= 0)
            {   // quanto o centro do boneco chega da parede de verdade (pixel), andando: o corpo tem meia largura ~4,5
                int nr = int.Parse(Arg(a, "-rounds", "30")); var hist = new int[6]; int tot = 0; var pior = new System.Collections.Generic.Dictionary<string, int>();
                for (int rr = 0; rr < nr; rr++)
                {
                    var m = new Match(seed0 + rr);
                    while (!m.over && m.t < 200)
                    {
                        m.Step();
                        foreach (var x in m.agents)
                        {
                            if (!x.alive || x.Speed < 5 || x.noAr) continue;
                            int cx = (int)x.pos.x, cy = (int)x.pos.y; float d = 9;
                            for (int u = cx - 7; u <= cx + 7; u++)
                                for (int v = cy - 7; v <= cy + 7; v++)
                                {
                                    bool bloq = u < 0 || v < 0 || u >= MapData.NX || v >= MapData.NY || !MapData.walk[u, v] || MapData.coverH[u, v] > 0 && x.z < MapData.floor[u, v] + MapData.coverH[u, v] - 2;
                                    if (!bloq) continue;
                                    float dx = MathF.Max(0, MathF.Max(u - x.pos.x, x.pos.x - (u + 1))), dy = MathF.Max(0, MathF.Max(v - x.pos.y, x.pos.y - (v + 1)));
                                    d = MathF.Min(d, MathF.Sqrt(dx * dx + dy * dy));
                                }
                            tot++; hist[Math.Min(5, (int)d)]++;
                            if (d < 2.5f) { string k = Places.Region(x.pos); pior.TryGetValue(k, out int pk); pior[k] = pk + 1; }
                        }
                    }
                }
                Console.WriteLine($"ticks andando: {tot}");
                for (int i = 0; i < 6; i++) Console.WriteLine($"  centro a {i}{(i < 5 ? $"-{i + 1}" : "+")} da parede: {100f * hist[i] / tot:0.00}%");
                foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(pior, k => k.Value), 10)) Console.WriteLine($"    < 2,5 em {kv.Key}: {kv.Value}");
                return 0;
            }
            if (Array.IndexOf(a, "-replay") >= 0) return Replay(seed0);
            if (Array.IndexOf(a, "-perf") >= 0) { LIMIAR_DETALHE = double.Parse(Arg(a, "-detalhe", "100"), System.Globalization.CultureInfo.InvariantCulture); CPU = Array.IndexOf(a, "-cpu") >= 0; if (CPU) CalibraCpu(); }
            if (Array.IndexOf(a, "-perf") >= 0)
            {   // tempo de CPU de cada m.Step (o jogo ao vivo roda um por quadro): distribuição e os piores, pra achar o pico
                Match.Perf = true; NavGrid.Medir = true; if (Array.IndexOf(a, "-gigantes") >= 0) NavGrid.Gigantes = new System.Collections.Generic.List<string>(); Match.Perfil = true; NavGrid.QuemPede = Array.IndexOf(a, "-quemPede") >= 0 ? new System.Collections.Generic.Dictionary<string, int>() : null; var detal = new System.Collections.Generic.List<(double ms, string s)>(); var swP = new System.Diagnostics.Stopwatch(); var piores = new System.Collections.Generic.List<(double ms, int seed, float t)>(); var hist = new int[6]; long tot = 0; double soma = 0;
                var iniT = new Dictionary<string, double>(); var iniP = new Dictionary<string, long>(); int iniN = 0;   // o 1º passo de cada round, por parte
                for (int rr = 0; rr < n; rr++)
                {
                    var m = new Match(seed0 + rr);
                    while (!m.over && m.t < 200)
                    {
                        int dj0 = NavGrid.Dijkstras, ae0 = NavGrid.AEstrelas, gc0 = GC.CollectionCount(2), gc1 = GC.CollectionCount(0); if (NavGrid.QuemPede != null) (NavGrid.QuemPedePasso ??= new System.Collections.Generic.Dictionary<string, int>()).Clear(); ulong cy0 = 0; if (CPU) QueryThreadCycleTime(GetCurrentThread(), out cy0); bool prim = m.t < 0.001f; Dictionary<string, double> tA = prim ? new Dictionary<string, double>(Match.Tempos) : null; Dictionary<string, long> pA = prim ? new Dictionary<string, long>(Brain.Perfil) : null; swP.Restart(); m.Step(); if (prim) { foreach (var kv in Match.Tempos) { tA.TryGetValue(kv.Key, out double v0); double dv = kv.Value - v0; if (dv > 0) { iniT.TryGetValue(kv.Key, out double w); iniT[kv.Key] = w + dv; } } foreach (var kv in Brain.Perfil) { pA.TryGetValue(kv.Key, out long v0); long dv = kv.Value - v0; if (dv > 0) { iniP.TryGetValue(kv.Key, out long w); iniP[kv.Key] = w + dv; } } iniN++; } double ms = swP.Elapsed.TotalMilliseconds; if (CPU) { QueryThreadCycleTime(GetCurrentThread(), out ulong cy1); ms = (cy1 - cy0) / CICLOS_MS; } soma += ms; tot++;
                        hist[ms < 1 ? 0 : ms < 4 ? 1 : ms < 16 ? 2 : ms < 33 ? 3 : ms < 100 ? 4 : 5]++;
                        if (ms > 16) piores.Add((ms, seed0 + rr, m.t));
                        if (ms > LIMIAR_DETALHE)
                        {
                            int ag = 0; for (int k = 1; k < 10; k++) if (Match.perfAgente[k] > Match.perfAgente[ag]) ag = k;
                            detal.Add((ms, $"semente {seed0 + rr} t={m.t:0.00}: intel {Match.perfMs[0]:0} · IGL {Match.perfMs[1]:0} · cérebros {Match.perfMs[2]:0} (pior {m.agents[ag].name} {Match.perfAgente[ag]:0}, {m.agents[ag].brain.fase}, {m.agents[ag].brain.order.name}) · física {Match.perfMs[3]:0} · resto {Match.perfMs[4]:0} · detector {Match.perfMs[5]:0} · Dijkstras {NavGrid.Dijkstras - dj0} · A* {NavGrid.AEstrelas - ae0} · GC gen2 {GC.CollectionCount(2) - gc0} gen0 {GC.CollectionCount(0) - gc1}" + (NavGrid.QuemPedePasso != null ? "\n            " + string.Join("\n            ", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderByDescending(NavGrid.QuemPedePasso, q => q.Value), q => $"{q.Value,3}× {q.Key}")) : "")));
                        }
                    }
                }
                Console.WriteLine($"{tot} passos, média {soma / tot:0.00} ms · células do grid {NavGrid.CellCount} · GC gen2 total {GC.CollectionCount(2)}");
                string[] nomes = { "<1", "1-4", "4-16", "16-33", "33-100", "100+" };
                for (int i = 0; i < 6; i++) Console.WriteLine($"  {nomes[i],6} ms: {hist[i]} ({100.0 * hist[i] / tot:0.000}%)");
                foreach (var p in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(piores, q => q.ms), 15)) Console.WriteLine($"  {p.ms,7:0.0} ms  semente {p.seed} t={p.t:0.00}");
                foreach (var d in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(detal, q => q.ms), 15)) Console.WriteLine($"  {d.ms,7:0} ms  {d.s}");
                double totT = 0; foreach (var kv in Match.Tempos) if (!kv.Key.StartsWith("cérebro · ")) totT += kv.Value;
                Console.WriteLine($"  por parte (ms por passo): total {totT / tot:0.000}");
                foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Match.Tempos, q => q.Value), 22)) Console.WriteLine($"    {kv.Value / tot,7:0.000}  {kv.Key}");
                foreach (var kv in System.Linq.Enumerable.OrderByDescending(Brain.Perfil, q => q.Value)) Console.WriteLine($"    {kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / tot,7:0.000}  (dentro do cérebro) {kv.Key}");
                Console.WriteLine($"    NavGrid: campos inteiros {NavGrid.nCampo} ({NavGrid.msCampo / Math.Max(1, NavGrid.nCampo):0.0} ms cada, {NavGrid.msCampo / tot:0.000} ms/passo) · A* {NavGrid.nAEstrela} ({NavGrid.msAEstrela / Math.Max(1, NavGrid.nAEstrela):0.00} ms cada, {NavGrid.msAEstrela / tot:0.000} ms/passo, {NavGrid.NosAEstrela / Math.Max(1, NavGrid.nAEstrela)} células cada, {NavGrid.NosAEstrela / Math.Max(0.001, NavGrid.msAEstrela):0} células/ms) · estimativas no lugar de A* (orçamento estourado) {NavGrid.Negados} · respondidas pela busca da origem {NavGrid.DaOrigemRespondeu} · A* que passaram do teto (pixel) {NavGrid.Desistiu}");
                Console.WriteLine($"  1º passo do round ({iniN} rounds), ms por round:");
                foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(iniT, q => q.Value), 25)) Console.WriteLine($"    {kv.Value / Math.Max(1, iniN),8:0.00}  {kv.Key}");
                foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(iniP, q => q.Value), 15)) Console.WriteLine($"    {kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, iniN),8:0.00}  (dentro do cérebro) {kv.Key}");
                if (NavGrid.Gigantes != null) { Console.WriteLine($"A* gigantes (>40k células): {NavGrid.Gigantes.Count}"); foreach (var gq in NavGrid.Gigantes) Console.WriteLine("  " + gq); }
                if (NavGrid.QuemPede != null) foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(NavGrid.QuemPede, q => q.Value), 600)) Console.WriteLine($"  A* {kv.Value,7}  {kv.Key}");
                return 0;
            }
            if (Array.IndexOf(a, "-log") >= 0)
            {   // um round só, com o log do jogo e o que cada TR estava fazendo a cada 5 s (pra investigar um exemplo)
                Match.LogContatos = Array.IndexOf(a, "-contatos") >= 0;
                Match m;
                int rodada = int.Parse(Arg(a, "-round", "0"));
                if (rodada > 0)
                {   // o round N da partida, pulando os anteriores igual o vídeo do Visual3D (PularAte: sem pós-round)
                    var pt = new Partida(seed0, null, null); m = pt.Preparar();
                    while (pt.r + 1 < rodada && !pt.acabou) { while (!m.over && m.t < 200) m.Step(); pt.Fechar(m); if (!pt.acabou) m = pt.Preparar(); }
                }
                else m = new Match(seed0);
                float prox = 0;
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    string quem = Arg(a, "-quem", "");
                    int passoQuem = int.Parse(Arg(a, "-quemPasso", "32"));
                    if (quem != "" && (int)(m.t * 32) % passoQuem == 0)
                        foreach (var x in m.agents) if (x.alive && x.name == quem) m.log.Add($"{m.t,6:0.00}    >> {x.name} ({x.pos.x:0},{x.pos.y:0}) {Places.Region(x.pos)} v={x.Speed:0} quer={x.brain.wantVel.Len:0} mov={x.move} | {x.brain.order.name} → ({(x.brain.order.hold?.pos ?? x.brain.order.dest).x:0},{(x.brain.order.hold?.pos ?? x.brain.order.dest).y:0}) | {x.brain.fase} | alvo {x.brain.target?.name ?? "-"} | {x.brain.thought}");
                    if (m.t >= prox) { prox += 5; foreach (var x in m.agents) if (x.alive && x.IsT) m.log.Add($"{m.t,6:0.00}    · {x.name}{(x.hasBomb ? " [C4]" : "")} {Places.Region(x.pos)} ({x.pos.x:0},{x.pos.y:0}) mira {x.aim * 57.3f:0}° v={x.Speed:0} | {x.brain.order.name} | {x.brain.fase} | {x.brain.thought}"); }
                    foreach (var x in m.agents)
                        if (x.alive && x.brain.target is Agent e && m.t - x.brain.alvoDesde > 8 && (int)(m.t * 10) % 20 == 0)
                            m.log.Add($"{m.t,6:0.00}    !! {x.name} ({x.pos.x:0},{x.pos.y:0} z{x.z:0} agach {x.crouch}) duelando {e.name} ({e.pos.x:0},{e.pos.y:0} z{e.z:0} agach {e.crouch}) há {m.t - x.brain.alvoDesde:0} s · dist {(e.pos - x.pos).Len:0} · LoS {MapData.LosWalk(x.pos, x.z, e.pos, e.z, 18f)} · smoke {m.SmokeBlocks(x.pos, x.z + 18, e.pos, e.z + 18)} · hp {x.hp}/{e.hp} · bala {x.gun.inMag}");
                }
                foreach (var l in m.log) Console.WriteLine(l);
                return 0;
            }
            if (Array.IndexOf(a, "-partidas") >= 0) return Partidas(int.Parse(Arg(a, "-partidas", "100")), seed0, Arg(a, "-modoA", ""), Arg(a, "-modoB", ""), Arg(a, "-saida", "adapta"));
            if (Array.IndexOf(a, "-treino") >= 0) return Treino(int.Parse(Arg(a, "-passos", "60")), n, seed0);
            var mt = new Metricas(); var trava = new object(); int feitos = 0;
            bool aprende = Array.IndexOf(a, "-aparece") >= 0; float[] somaAp = aprende ? Aparece.Vazio() : null;
            var sw = Stopwatch.StartNew();
            var digest = Array.IndexOf(a, "-digest") >= 0 ? new List<string>() : null;   // impressão digital de cada round (testa determinismo)
            var pendentes = new Dictionary<int, Match>(); int proximo = 0;
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = int.Parse(Arg(a, "-hilos", Environment.ProcessorCount.ToString())) }, i =>
            {
                var m = new Match(seed0 + i);
                while (!m.over && m.t < 200) m.Step();
                lock (trava)
                {   // agrega em ordem de semente (quem termina antes espera a vez): a ordem das threads mudava empates e
                    // somas do relatório
                    pendentes[i] = m;
                    while (pendentes.TryGetValue(proximo, out var mo))
                    {
                        pendentes.Remove(proximo); int io = proximo++;
                        if (digest != null) digest.Add($"{seed0 + io} {mo.winner} t={mo.t:0.000} vivos={System.Linq.Enumerable.Count(mo.agents, x => x.alive)} pos0={mo.agents[0].pos.x:0.00},{mo.agents[0].pos.y:0.00}");
                        if (aprende) Aparece.Soma(somaAp, mo); mt.Add(mo); if (++feitos % 500 == 0) Console.WriteLine($"{feitos} rounds, {feitos / sw.Elapsed.TotalSeconds:0.0}/s");
                    }
                }
            });
            if (aprende) Aparece.Grava(somaAp);
            if (digest != null) { digest.Sort(string.CompareOrdinal); Directory.CreateDirectory("Logs/bots"); File.WriteAllLines("Logs/bots/" + Arg(a, "-saida", "headless") + ".digest", digest); }
            double rs = n / sw.Elapsed.TotalSeconds;
            string r = mt.Relatorio();
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/" + Arg(a, "-saida", "headless") + ".txt", $"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia\n" + r + Exemplos());
            Console.WriteLine($"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia");
            return 0;
        }
            // ================================================================ replay (mesmo json do MatchCli.Replay, sem Unity)
        static readonly string[] ARMAS = { "ak", "m4", "m4s", "awp", "mp9", "mac10", "usp", "glock", "faca", "deagle", "p250" };   // índice vai no json: só acrescentar no fim
        static string Js(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";
        static string F1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        static int I(float v) => (int)MathF.Round(v);
        static (float x, float y) P(V2 p, float z) { var (x, y) = CSM.View.Iso.Project(new CSM.View.V3(p.x, p.y, z)); return (x / 2, y / 2); }

        /// <summary>
        /// Replay de uma partida inteira pra página "Replay da Vila" (quadros de 0,1 s, pensamentos, suspeitas, log):
        /// -replay -seed S → Logs/bots/replay/partida_S.json. O fundo (mapa.png) vem do MatchCli (Unity).
        /// </summary>
        static int Replay(int seed)
        {
            Brain.Explicar = true;
            Directory.CreateDirectory("Logs/bots/replay");
            var pt = new Partida(seed, null, null);
            var sb = new StringBuilder();
            sb.Append($"{{\"seed\":{seed},\"mapa\":[{CSM.View.Iso.BW / 2},{CSM.View.Iso.BH / 2}],\"times\":[{Js(pt.A.nome)},{Js(pt.B.nome)}],\"armas\":[{string.Join(",", System.Linq.Enumerable.Select(ARMAS, Js))}],\"rounds\":[");
            bool first = true;
            while (!pt.acabou && pt.r < 60)
            {
                var m = pt.Preparar(); int rn = pt.r + 1;
                var fr = new StringBuilder(); float next = 0; int nf = 0;
                while (true)
                {
                    if (m.t >= next || m.over) { next += 0.1f; if (nf++ > 0) fr.Append(','); Quadro(m, fr); }
                    if (m.over || m.t > 200) break;
                    try { m.Step(); } catch (Exception e) { m.over = true; m.winner = "CT"; m.reason = "erro: " + e.Message; }
                }
                var compraT = m.compra[0]; var compraCT = m.compra[1];
                pt.Fechar(m);
                var info = pt.historico[pt.historico.Count - 1];
                sb.Append(first ? "" : ","); first = false;
                sb.Append($"{{\"n\":{rn},\"placar\":[{info.placarA},{info.placarB}],\"ladoA\":{Js(info.ladoA)},\"compra\":[{Js(compraT)},{Js(compraCT)}],\"vencedor\":{Js(info.vencedor)},\"motivo\":{Js(info.motivo)},\"planos\":[{Js(m.igl[0].plan)},{Js(m.igl[1].plan)}]");
                sb.Append(",\"jog\":[" + string.Join(",", System.Linq.Enumerable.Select(m.agents, a => $"{{\"n\":{Js(a.name)},\"tr\":{(a.IsT ? 1 : 0)},\"r\":{Js(a.role.ToString())}}}")) + "]");
                sb.Append(",\"f\":[" + fr + "]");
                var pens = new System.Collections.Generic.List<(float t, int id, string s, string porque, string ctx)>();
                foreach (var a in m.agents) foreach (var l in a.brain.decisoes) pens.Add((l.t, a.id, l.s, l.porque, l.ctx));
                pens.Sort((x, y) => x.t.CompareTo(y.t));
                sb.Append(",\"pens\":[" + string.Join(",", System.Linq.Enumerable.Select(pens, x => $"[{F1(x.t)},{x.id},{Js(x.s)},{Js(x.porque)},{Js(x.ctx)}]")) + "]");
                sb.Append(",\"susp\":[" + string.Join(",", System.Linq.Enumerable.Select(m.suspeitas, x => $"[{F1(x.t)},{x.id},{Js(x.tipo)},{Js(x.texto)}]")) + "]");
                sb.Append(",\"log\":[" + string.Join(",", System.Linq.Enumerable.Select(m.log, l => $"[{l.Substring(0, 6).Trim().Replace(',', '.')},{Js(l.Substring(8))}]")) + "]}");
                Console.Error.Write($"\rround {rn}");
            }
            sb.Append("]}");
            File.WriteAllText($"Logs/bots/replay/partida_{seed}.json", sb.ToString());
            Console.WriteLine($"\nok {pt.historico.Count} rounds, {pt.A.nome} {pt.A.placar} x {pt.B.placar} {pt.B.nome}, {sb.Length / 1024} KB");
            return 0;
        }

        /// <summary>[t, jogadores[[x,y,mx,my,hp,estado,arma]...], smokes[[x,y,r]], fogo[[x,y,r]], flashes[[x,y]], tiros[[x0,y0,x1,y1,acertou]], bomb[x,y,estado], voando]</summary>
        static void Quadro(Match m, StringBuilder sb)
        {
            sb.Append('[').Append(F1(m.t)).Append(",[");
            for (int i = 0; i < m.agents.Count; i++)
            {
                var a = m.agents[i]; var (x, y) = P(a.pos, a.z); var (fx, fy) = P(a.pos + V2.Ang(a.aim) * 22, a.z);
                int st = (a.alive ? 1 : 0) | (a.Blind(m.t) ? 2 : 0) | (a.MeioCego(m.t) ? 4 : 0) | (a.hasBomb ? 8 : 0) | (a.scoped ? 16 : 0) | (a.guardando ? 32 : 0) | (a.brain.action == "desarmar" ? 64 : 0) | (a.brain.action == "plantar" ? 128 : 0);
                if (i > 0) sb.Append(',');
                sb.Append($"[{I(x)},{I(y)},{I(fx - x)},{I(fy - y)},{Math.Max(0, a.hp)},{st},{(a.faca ? 8 : Array.IndexOf(ARMAS, a.gunId))}]");
            }
            sb.Append("],[");
            for (int i = 0; i < m.smokes.Count; i++)
            {
                var s = m.smokes[i]; var (mn, mx) = s.Bounds; var (x, y) = P(new V2(s.origin.x, s.origin.y), s.origin.z + 10);
                if (i > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},{I(MathF.Max(mx.x - mn.x, mx.y - mn.y) * 0.5f)}]");
            }
            sb.Append("],[");
            for (int i = 0; i < m.fires.Count; i++)
            {
                var f = m.fires[i]; var (x, y) = P(new V2(f.origin.x, f.origin.y), f.origin.z);
                if (i > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},{I(18 * f.Strength + 4)}]");
            }
            sb.Append("],[");
            for (int i = 0; i < m.flashes.Count; i++) { var (x, y) = P(m.flashes[i].p, m.flashes[i].z); if (i > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)}]"); }
            for (int i = 0; i < m.hes.Count; i++) { var (x, y) = P(m.hes[i].p, m.hes[i].z); if (i > 0 || m.flashes.Count > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},1]"); }
            sb.Append("],[");
            for (int i = 0; i < m.shots.Count; i++)
            {
                var s = m.shots[i]; var (x0, y0) = P(s.a, s.za); var (x1, y1) = P(s.b, s.zb);
                if (i > 0) sb.Append(','); sb.Append($"[{I(x0)},{I(y0)},{I(x1)},{I(y1)},{(s.hit ? 1 : 0)}]");
            }
            sb.Append("],");
            if (m.bombPlanted) { var (x, y) = P(m.bombPos, m.bombZ); sb.Append($"[{I(x)},{I(y)},{(m.defused ? 3 : m.exploded ? 4 : 2)},{F1(m.BombLeft)}]"); }
            else if (m.bombDropped) { var (x, y) = P(m.droppedBomb, NavGrid.ZAt(m.droppedBomb)); sb.Append($"[{I(x)},{I(y)},1,0]"); }
            else sb.Append("[]");
            sb.Append(",[");
            for (int i = 0; i < m.flying.Count; i++)
            {
                var n = m.flying[i]; var (x, y) = P(n.P, n.z);
                if (i > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},{(n.l.kind == "smoke" ? 0 : n.l.kind == "flash" ? 1 : n.l.kind == "molotov" ? 2 : 3)}]");
            }
            sb.Append("]]");
        }
    }
}
