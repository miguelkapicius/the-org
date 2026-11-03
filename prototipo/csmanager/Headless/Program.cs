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
                var p = new Partida(seed0 + i); p.Jogar(m => { foreach (var k in m.ev) if (k.Key.Contains("lembrado") || k.Key.Contains("queimado") || k.Key.StartsWith("FIM") || k.Key.StartsWith("PLANT") || k.Key.Contains("guardou")) { evs.TryGetValue(k.Key, out int v); evs[k.Key] = v + k.Value; } });
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
            for (int t = 0; t < 2; t++) { var D = Partida.Diag; float r = Math.Max(1, D[t, 0]); sb.AppendLine($"  compra cheia {(t == 0 ? "TR" : "CT")}: {D[t, 0]} rounds, AWP com o AWPer {100 * D[t, 1] / r:0}%, primária {D[t, 2] / r:0.0}/5, capacete {D[t, 3] / r:0.0}/5, granadas {D[t, 4] / r:0.0}, kits {D[t, 5] / r:0.0}"); }
            string[] nomes = { "rounds 1-6", "rounds 7-12", "rounds 13-18", "rounds 19+" };
            for (int k = 0; k < 4; k++) sb.AppendLine($"  {nomes[k],-13} A vence {100f * trecho[k, 1] / Math.Max(1, trecho[k, 0]):0.0}% de {trecho[k, 0]}");
            sb.AppendLine("  ponto usado de novo (matou x morreu ali):");
            for (int k = 0; k < 4; k++) sb.AppendLine($"    já matou dali {(k == 3 ? "3+" : k.ToString())} vez(es): {rep[k, 0]} x {rep[k, 1]} = {100f * rep[k, 0] / Math.Max(1, rep[k, 0] + rep[k, 1]):0.0}%");
            foreach (var k in totEv) sb.AppendLine($"  {k.Key,-44} {k.Value / (float)Math.Max(1, rs):0.00}");
            foreach (var kv in planos) if (kv.Value[0] >= 8) sb.AppendLine($"  {kv.Key,-34} {100f * kv.Value[1] / kv.Value[0]:0.0}% de {kv.Value[0]}");
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/{saida}.txt", sb.ToString()); Console.Write(sb);
            return 0;
        }

        static string Exemplos()
        {
            var sb = new StringBuilder("\nEXEMPLOS DE SUSPEITA (semente do round avulso)\n");
            foreach (var (tipo, ex) in Match.Exemplos()) { sb.AppendLine("  " + tipo); foreach (var e in ex) sb.AppendLine("    " + e); }
            return sb.ToString();
        }
        public static int Main(string[] a)
        {
            int n =int.Parse(Arg(a, "-n", "320")), seed0 = int.Parse(Arg(a, "-seed", "900"));
            TeamBrain.EXP = Arg(a, "-exp", ""); Partida.SemLeitura = Array.IndexOf(a, "-semLeitura") >= 0;
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
            {   // força de cada plano do banco (compra cheia): o plano fixo de um lado, o outro escolhe do banco; grava EstrategiasForca.cs
                int R = int.Parse(Arg(a, "-r", "24"));
                var planos = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(Estrategias.Todos, p => p.compra == "full"));
                var vit = new int[planos.Count]; int feitosB = 0; var swB = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, planos.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                {
                    var p = planos[i];
                    TeamBrain.ForcaTR = p.tr ? p.idx + 1 : 0; TeamBrain.ForcaCT = p.tr ? 0 : p.idx + 1;
                    for (int j = 0; j < R; j++)
                    {
                        var m = new Match(seed0 + 7919 * j + 13 * i);
                        while (!m.over && m.t < 200) m.Step();
                        bool trGanhou = m.over && m.winner == "TR";
                        if (trGanhou == p.tr) vit[i]++;
                    }
                    TeamBrain.ForcaTR = TeamBrain.ForcaCT = 0;
                    int f = Interlocked.Increment(ref feitosB); if (f % 50 == 0) Console.WriteLine($"{f}/{planos.Count} planos ({swB.Elapsed.TotalSeconds:0} s)");
                });
                var sb = new StringBuilder();
                foreach (bool tr in new[] { true, false })
                {
                    double soma = 0; int n2 = 0;
                    for (int i = 0; i < planos.Count; i++) if (planos[i].tr == tr) { soma += vit[i]; n2 += R; }
                    double media = soma / Math.Max(1, n2);
                    var lista = new System.Collections.Generic.List<(double wr, int i)>();
                    for (int i = 0; i < planos.Count; i++)
                    {
                        if (planos[i].tr != tr) continue;
                        double wr = (vit[i] + media * 8) / (R + 8);   // encolhe pra média (24 rounds ainda é ruído)
                        lista.Add((wr, i));
                        double peso = Math.Clamp(Math.Exp(4 * (wr - media)), 0.25, 3);
                        sb.Append(FormattableString.Invariant($"            ({planos[i].idx}, {peso:0.###}f),   // {(tr ? "TR" : "CT")} {planos[i].Tipo} bomb {planos[i].site}: venceu {vit[i]}/{R}\n"));
                    }
                    lista.Sort((x, y) => y.wr.CompareTo(x.wr));
                    Console.WriteLine($"{(tr ? "TR" : "CT")}: {lista.Count} planos, vitória média {100 * media:0.0}% · melhores " + string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(lista, 5), x => $"#{planos[x.i].idx} {planos[x.i].Tipo} {100 * x.wr:0}%")) + " · piores " + string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.Reverse(lista), 5), x => $"#{planos[x.i].idx} {planos[x.i].Tipo} {100 * x.wr:0}%")));
                }
                File.WriteAllText("Assets/Game/Sim/EstrategiasForca.cs",
                    "// Força medida de cada plano do banco no headless (Headless -banco -r " + R + "). Gerado; não editar à mão.\n" +
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
                    if (quem != "" && (int)(m.t * 32) % 32 == 0)
                        foreach (var x in m.agents) if (x.alive && x.name == quem) m.log.Add($"{m.t,6:0.00}    >> {x.name} ({x.pos.x:0},{x.pos.y:0}) {Places.Region(x.pos)} v={x.Speed:0} | {x.brain.order.name} → ({x.brain.order.dest.x:0},{x.brain.order.dest.y:0}) | {x.brain.fase} | alvo {x.brain.target?.name ?? "-"} | {x.brain.thought}");
                    if (m.t >= prox) { prox += 5; foreach (var x in m.agents) if (x.alive && x.IsT) m.log.Add($"{m.t,6:0.00}    · {x.name}{(x.hasBomb ? " [C4]" : "")} {Places.Region(x.pos)} v={x.Speed:0} | {x.brain.order.name} | {x.brain.fase} | {x.brain.thought}"); }
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
            File.WriteAllText("Logs/bots/" + Arg(a, "-saida", "headless") + ".txt", $"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia\n" + r + Exemplos());
            Console.WriteLine($"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s = {rs:0.0} rounds/s = {rs * 86400:0} por dia");
            return 0;
        }
    }
}
