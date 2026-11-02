using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSM.Map;
using CSM.Sim;
using CSM.View;
using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Bots jogando:
    ///   MatchCli.Round  -seed N   → vídeo de um round em visão tática (mapa inteiro, bots, caminhos, tiros, smokes,
    ///                               fogo) + painel com plano/calls do IGL, killfeed, o pensamento de cada bot e os
    ///                               mapas de calor (o que cada time sabe). Logs/bots/round_N.mp4 e round_N.txt.
    ///   MatchCli.Stats  -n N      → N rounds sem vídeo e um relatório: vitórias por lado e por plano, plants,
    ///                               kills/HS/precisão por função, first kill, trocas, onde morrem, e o que parece
    ///                               errado (bot travado, morreu sem ver, nunca atirou...). Logs/bots/stats.txt.
    /// </summary>
    public static class MatchCli
    {
        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        static Pesos PesosArg()
        {
            var f = Arg("-pesos", null);
            return f != null && File.Exists(f) ? Pesos.De(File.ReadAllText(f)) : null;
        }

        /// <summary>
        /// Roda n rounds em paralelo (um por núcleo; a simulação não usa nada da Unity). Conta bot travado (quer andar
        /// e não sai do lugar por 3 s) no log do round.
        /// </summary>
        static readonly List<string> Erros = new List<string>();   // round que quebrou: registra e segue (não derruba o treino)
        public static readonly List<string> TravadoEx = new List<string>();   // exemplos de bot travado (seed, tempo, onde)

        static Match[] Rodar(int n, int seed0, Pesos pT, Pesos pCT, Dictionary<string, int> stuck = null)
        {
            new Match(seed0, pT?.Clone(), pCT?.Clone()); Spots.All();   // aquece mapa, navmesh, spots e calor na thread principal
            var res = new Match[n];
            System.Threading.Tasks.Parallel.For(0, n, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, r =>
            {
                var m = new Match(seed0 + r, pT?.Clone(), pCT?.Clone());
                var stillFor = new float[m.agents.Count];
                var ref5 = m.agents.Select(a => a.pos).ToArray(); var semSair = new float[m.agents.Count];
                while (!m.over && m.t < 200)
                {
                    try { m.Step(); }
                    catch (Exception e) { m.Log("ERRO NA SIMULAÇÃO: " + e.GetType().Name + " " + e.Message + " @ " + e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); lock (Erros) Erros.Add($"seed {seed0 + r}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); break; }
                    // travado de verdade: QUERENDO andar (fase andando, sem segurar/ação/duelo) há 4 s seguidos e não saiu do
                    // lugar (antes media a cada 5 s e contava quem só tinha acabado de receber ordem de andar)
                    foreach (var a in m.agents)
                    {
                        bool quer = a.alive && a.brain.order.hold == null && !a.guardando && a.brain.action == null && a.brain.target == null
                                    && (a.brain.fase == "andando" || a.brain.fase == "ordem") && !(m.bombPlanted && a.IsT);
                        if (!quer || (a.pos - ref5[a.id]).Len >= 6) { semSair[a.id] = 0; ref5[a.id] = a.pos; continue; }
                        semSair[a.id] += Match.DT;
                        if (semSair[a.id] >= 4)
                        {
                            semSair[a.id] = 0;
                            {
                                m.Log($"SEM PROGRESSO: {a.name} fase={a.brain.fase} ordem='{a.brain.order.name}' pensa='{a.brain.thought}' quer={a.brain.wantVel.Len:0} em {Places.Region(a.pos)} ({a.pos})");
                                string k = $"\n     sem progresso @ {Places.Region(a.pos)} / '{a.brain.order.name}' / {a.brain.thought}";
                                if (stuck != null) lock (stuck) { stuck[k] = stuck.TryGetValue(k, out var c) ? c + 1 : 1; }
                                lock (TravadoEx) if (TravadoEx.Count < 25) TravadoEx.Add($"seed {seed0 + r} t {m.t:0} {a.name} z {a.z:0} (chão {NavGrid.ZAt(a.pos):0}) noAr {a.noAr} em {Places.Region(a.pos)} {a.pos} quer {a.brain.wantVel.Len:0} '{a.brain.order.name}' pensa '{a.brain.thought}'");
                            }
                        }
                    }
                    foreach (var a in m.agents)
                    {
                        if (!a.alive) continue;
                        if (a.brain.wantVel.Len > 10 && a.Speed < 3) stillFor[a.id] += Match.DT; else stillFor[a.id] = 0;
                        if (stillFor[a.id] > 3f)
                        {
                            stillFor[a.id] = -999; m.Log($"TRAVADO: {a.name} ({a.brain.thought}) em {Places.Region(a.pos)}");
                            string k = $"\n     empurrando parede @ {Places.Region(a.pos)} / fase {a.brain.fase} / {a.brain.thought.Split('—')[0].Trim()}";
                            if (stuck != null) lock (stuck) { stuck[k] = stuck.TryGetValue(k, out var c) ? c + 1 : 1; }
                            lock (TravadoEx) if (TravadoEx.Count < 40) TravadoEx.Add($"PAREDE seed {seed0 + r} t {m.t:0} {a.name} z {a.z:0} em {Places.Region(a.pos)} {a.pos} quer {a.brain.wantVel.Len:0} fase {a.brain.fase} '{a.brain.thought}'");
                        }
                    }
                }
                res[r] = m;
            });
            return res;
        }

        // ================================================================ estatística de muitos rounds
        public static void Stats()
        {
            var sb = new StringBuilder();
            try
            {
                int n = int.Parse(Arg("-n", "40")), seed0 = int.Parse(Arg("-seed", "1"));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var res = new List<(Match m, string tPlan, string ctPlan)>();
                var stuck = new Dictionary<string, int>(); int deathsBlind = 0, deathsUnseen = 0, totalDeaths = 0;
                var thoughtKinds = new Dictionary<string, int>();
                var p = PesosArg();
                var mt = new Metricas();
                Match.Perfil = Array.IndexOf(Environment.GetCommandLineArgs(), "-perfil") >= 0;
                // calibração do tiro (experimento): -escala = imprecisão da arma, -mira = erro de mira do jogador (graus)
                Combat.ESCALA_ARMA = float.Parse(Arg("-escala", Combat.ESCALA_ARMA.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture);
                Skill.MIRA_PADRAO = float.Parse(Arg("-mira", Skill.MIRA_PADRAO.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture);
                sb.AppendLine($"tiro: escala da arma {Combat.ESCALA_ARMA}, erro de mira {Skill.MIRA_PADRAO}°");
                Match.ArmaCT = Arg("-armaCT", null); if (Match.ArmaCT != null) sb.AppendLine($"EXPERIMENTO: rifle do CT = {Match.ArmaCT}");
                TeamBrain.EXP = Arg("-exp", ""); if (TeamBrain.EXP != "") sb.AppendLine($"EXPERIMENTO: {TeamBrain.EXP}");
                var rodados = Rodar(n, seed0, p, p, stuck);
                if (Match.Perfil)
                {
                    double tot = Match.Tempo[0] + Match.Tempo[4] + Match.Tempo[5] + Match.Tempo[6], f = 1.0 / System.Diagnostics.Stopwatch.Frequency;
                    sb.AppendLine($"PERFIL (tempo de CPU somado nas threads, {tot * f:0} s):");
                    for (int i = 0; i < 7; i++) sb.AppendLine($"  {Match.TEMPO_NOME[i],-26} {Match.Tempo[i] * f,7:0.0} s  {100 * Match.Tempo[i] / tot,5:0}%");
                    // dentro dos cérebros
                    foreach (var kv in Brain.Perfil.OrderByDescending(k => k.Value)) sb.AppendLine($"    brain {kv.Key,-22} {kv.Value * f,7:0.0} s");
                    sb.AppendLine($"    campos de distância calculados (Dijkstra do mapa): {NavGrid.Dijkstras} ({NavGrid.Dijkstras / (float)n:0.0} por round)");
                }
                foreach (var m in rodados)
                {
                    mt.Add(m);
                    foreach (var a in m.agents)
                    {
                        if (!a.alive || a.diedAt > 0)
                        {
                            totalDeaths++;
                            if (a.killedBy != null)
                            {
                                bool saw = a.brain.log.Any(l => l.t > a.diedAt - 2f && l.t <= a.diedAt && l.s.StartsWith("vi " + a.killedBy.name));
                                if (!saw) deathsUnseen++;
                                if (a.blindUntil > a.diedAt) deathsBlind++;
                            }
                        }
                        foreach (var (t, s) in a.brain.log)
                        {
                            string k = s.Split(new[] { ' ', '(' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                            if (s.StartsWith("pre-fire")) k = "pre-fire"; else if (s.StartsWith("checando")) k = "checando"; else if (s.StartsWith("segurando apertado")) k = "quieto"; else if (s.StartsWith("segurando")) k = "segurando";
                            thoughtKinds[k] = thoughtKinds.TryGetValue(k, out var c) ? c + 1 : 1;
                        }
                    }
                    res.Add((m, m.igl[0].plan, m.igl[1].plan));
                    Directory.CreateDirectory("Logs/bots/rounds");
                    File.WriteAllText($"Logs/bots/rounds/round_{res.Count - 1 + seed0}.txt", RoundLog(m));
                }
                sb.AppendLine($"{n} rounds em {sw.Elapsed.TotalSeconds:0.0} s ({Environment.ProcessorCount - 2} em paralelo)");
                sb.AppendLine(mt.Relatorio());
                if (Erros.Count > 0) { sb.AppendLine($"ROUNDS QUE QUEBRARAM: {Erros.Count}"); foreach (var e in Erros.Take(10)) sb.AppendLine("  " + e); }
                int tw = res.Count(x => x.m.winner == "TR");
                sb.AppendLine($"VITÓRIAS: TR {tw} ({100f * tw / n:0}%)  CT {n - tw} ({100f * (n - tw) / n:0}%)   (CS pro costuma dar ~45-50% TR)");
                foreach (var g in res.GroupBy(x => x.m.reason)) sb.AppendLine($"  {g.Key}: {g.Count()}");
                sb.AppendLine($"duração média: {res.Average(x => x.m.t):0}s;  plants: {res.Count(x => x.m.bombPlanted)} ({100f * res.Count(x => x.m.bombPlanted) / n:0}%) — A {res.Count(x => x.m.bombPlanted && x.m.planSite == 'A')}, B {res.Count(x => x.m.bombPlanted && x.m.planSite == 'B')}");
                sb.AppendLine("PLANOS TR (vitória do TR):");
                foreach (var g in res.GroupBy(x => x.tPlan)) sb.AppendLine($"  {g.Key,-18} {g.Count(x => x.m.winner == "TR")}/{g.Count()}");
                sb.AppendLine("SETUPS CT (vitória do CT):");
                foreach (var g in res.GroupBy(x => x.ctPlan)) sb.AppendLine($"  {g.Key,-18} {g.Count(x => x.m.winner == "CT")}/{g.Count()}");
                sb.AppendLine("POR JOGADOR (kills, mortes, HS%, precisão, dano/round, first kill/death, trocas feitas):");
                var all = res.SelectMany(x => x.m.agents).GroupBy(a => a.name);
                foreach (var g in all)
                {
                    var a0 = g.First(); int k = g.Sum(a => a.kills), d = g.Sum(a => a.deaths), hs = g.Sum(a => a.hs), sh = g.Sum(a => a.shots), hi = g.Sum(a => a.hits);
                    sb.AppendLine($"  {a0.name,-7} {(a0.team == Team.T ? "TR" : "CT")} {a0.role,-8} {a0.gunId,-4} K {k,3} D {d,3}  HS {(k > 0 ? 100f * hs / k : 0),3:0}%  prec {(sh > 0 ? 100f * hi / sh : 0),3:0}%  ADR {g.Sum(a => a.damage) / (float)n,5:0}  FK {g.Sum(a => a.firstKills),2} FD {g.Sum(a => a.firstDeaths),2}  trocas {g.Sum(a => a.tradeKills),2}");
                }
                sb.AppendLine("ONDE MORREM:");
                foreach (var g in res.SelectMany(x => x.m.agents.Where(a => a.diedAt > 0)).GroupBy(a => Places.Region(a.pos)).OrderByDescending(g => g.Count()).Take(12))
                    sb.AppendLine($"  {g.Key,-22} {g.Count()}");
                // duelo por lugar: de onde o matador atira em quem (antes do plant = o CT defendendo, depois = retake)
                foreach (var plant in new[] { false, true })
                {
                    var ks = res.SelectMany(x => x.m.acertos).Where(x => x.matou && x.plantado == plant).ToList();
                    sb.AppendLine($"DUELO POR LUGAR {(plant ? "depois do plant" : "antes do plant")} (matador @ lugar -> vítima @ lugar; TR mata/CT mata):");
                    foreach (var g in ks.GroupBy(x => Places.Region(x.posAtirador) + " x " + Places.Region(x.posVitima)).OrderByDescending(g => g.Count()).Take(14))
                        sb.AppendLine($"  {g.Key,-46} TR {g.Count(x => x.atiradorTR),3}  CT {g.Count(x => !x.atiradorTR),3}  volta {ks.Count(x => Places.Region(x.posVitima) + " x " + Places.Region(x.posAtirador) == g.Key),3}   vítima viu {100f * g.Count(x => x.vitimaViu) / g.Count(),3:0}%  de lado/costas {100f * g.Count(x => x.foraDaVisao) / g.Count(),3:0}%  cega {100f * g.Count(x => x.cego) / g.Count(),3:0}%  dist {g.Average(x => x.dist),3:0}  t {g.Average(x => x.t),3:0}");
                    sb.AppendLine("   (detalhe dos 6 maiores: vítima viu antes % · vítima atirou % · vítima andando % · tempo do matador vendo / da vítima vendo · AWP %)");
                    foreach (var g in ks.GroupBy(x => Places.Region(x.posAtirador) + " x " + Places.Region(x.posVitima)).OrderByDescending(g => g.Count()).Take(10))
                    {
                        var v = g.Where(x => x.vitViuHa >= 0 && x.matViuHa >= 0).ToList();
                        sb.AppendLine($"   {g.Key,-44} antes {(v.Count > 0 ? 100f * v.Count(x => x.vitViuHa > x.matViuHa + 0.02f) / v.Count : 0),3:0}%  atirou {100f * g.Count(x => x.vitimaAtirou) / g.Count(),3:0}%  andando {100f * g.Count(x => x.vitAndando) / g.Count(),3:0}%  vendo {(v.Count > 0 ? v.Average(x => x.matViuHa) : 0):0.00}/{(v.Count > 0 ? v.Average(x => x.vitViuHa) : 0):0.00} s  AWP {100f * g.Count(x => x.arma == "awp") / g.Count(),3:0}%  ex: {string.Join(", ", g.Take(3).Select(x => $"seed {x.seed} t{x.t:0} {x.matador}>{x.vitima}"))}");
                    }
                }
                sb.AppendLine("SINAIS DE PROBLEMA:");
                sb.AppendLine($"  bot travado (quer andar e não sai do lugar por 3 s): {stuck.Values.Sum()} vezes {string.Join(", ", stuck.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value))}");
                sb.AppendLine("  exemplos de travado:\n" + string.Join("\n", TravadoEx.Select(x => "     " + x)));
                sb.AppendLine($"  morreu sem ver quem matou: {deathsUnseen}/{totalDeaths} ({(totalDeaths > 0 ? 100f * deathsUnseen / totalDeaths : 0):0}%) — alto demais = bot de costas ou sem limpar ângulo");
                sb.AppendLine($"  morreu cego: {deathsBlind}");
                int noShot = res.SelectMany(x => x.m.agents).Count(a => a.diedAt > 0 && a.shots == 0);
                sb.AppendLine($"  morreu sem dar nenhum tiro: {noShot}");
                sb.AppendLine("PENSAMENTOS MAIS COMUNS:");
                foreach (var kv in thoughtKinds.OrderByDescending(kv => kv.Value).Take(18)) sb.AppendLine($"  {kv.Key,-14} {kv.Value}");
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/stats.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>Valida o mapa depois de mexer: MapCheck + NavCheck (0 reprovações) e os tempos. Logs/bots/mapa_valida.txt</summary>
        public static void MapaValida()
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine(MapCheck.Report()); sb.AppendLine(NavCheck.Report());
                float vel = MapCheck.RUN;
                string T(V2 x, V2 y) => $"{NavGrid.PathLen(x, y) / vel,5:0.0}s";
                V2 sT = MapData.SPAWN_T[2], sCT = MapData.SPAWN_CT[2];
                sb.AppendLine($"TEMPOS (faca): TR→A {T(sT, Places.PlantA)}  TR→B {T(sT, Places.PlantB)}  TR→tapete {T(sT, Places.B_Tapete)}  TR→curta {T(sT, Places.B_Curta)}  TR→rampa {T(sT, Places.A_Rampa)}");
                sb.AppendLine($"               CT→A {T(sCT, Places.PlantA)}  CT→B {T(sCT, Places.PlantB)}  rotação A→B {T(Places.PlantA, Places.PlantB)}");
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/mapa_valida.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        // ================================================================ diagnóstico de caminho
        /// <summary>Caminho de (-x,-y) até o plant do A (ou -tx -ty): cada ponto com altura, tipo, e se dá pra andar
        /// reto até o próximo. Logs/bots/diag.txt</summary>
        public static void Diag()
        {
            var sb = new StringBuilder();
            try
            {
                new Match(1);
                var a = new V2(float.Parse(Arg("-x", "893"), System.Globalization.CultureInfo.InvariantCulture), float.Parse(Arg("-y", "310.6"), System.Globalization.CultureInfo.InvariantCulture));
                var b = Arg("-tx", null) != null ? new V2(float.Parse(Arg("-tx", "0")), float.Parse(Arg("-ty", "0"))) : Places.PlantA;
                // tempos do mapa (correndo de faca, 62,5 u/s), do spawn e entre os bombs
                float vel = MapCheck.RUN;
                string T(V2 x, V2 y) => $"{NavGrid.PathLen(x, y) / vel,5:0.0}s";
                V2 sT = MapData.SPAWN_T[0], sCT = MapData.SPAWN_CT[0];
                sb.AppendLine($"TEMPOS (faca): TR→A {T(sT, Places.PlantA)}  TR→B {T(sT, Places.PlantB)}  TR→meio {T(sT, Places.Meio_Sul)}");
                sb.AppendLine($"               CT→A {T(sCT, Places.PlantA)}  CT→B {T(sCT, Places.PlantB)}  CT→janelão {T(sCT, Places.CT_Janelao.pos)}");
                sb.AppendLine($"               rotação CT A→B {T(Places.PlantA, Places.PlantB)}  plataforma→banca {T(Places.CT_A_Plataforma.pos, Places.CT_B_Banca.pos)}");
                sb.AppendLine($"               TR entradas: rampa {T(sT, Places.A_Rampa)}  curta {T(sT, Places.B_Curta)}  tapete {T(sT, Places.B_Tapete)}");
                sb.AppendLine("TR da posição de espera até o plant (correndo):");
                foreach (var h in new[] { Places.T_RampaDobra, Places.T_RampaBaixo, Places.T_PalacioSala, Places.T_Conector, Places.T_MeioSul })
                    sb.AppendLine($"   {h.name,-18} → A {T(h.pos, Places.PlantA)}   → B {T(h.pos, Places.PlantB)}");
                foreach (var h in new[] { Places.T_CasaraoDobra, Places.T_CurtaCima })
                    sb.AppendLine($"   {h.name,-18} → A {T(h.pos, Places.PlantA)}   → B {T(h.pos, Places.PlantB)}");
                sb.AppendLine("CT de cada posição até cada plant (rotação):");
                foreach (var h in new[] { Places.CT_A_Plataforma, Places.CT_A_Caixa, Places.CT_A_Canto, Places.CT_A_RuaCT, Places.CT_Janelao, Places.CT_Praca, Places.CT_B_Viela, Places.CT_B_Pilar, Places.CT_B_Banca, Places.CT_B_Caixa })
                    sb.AppendLine($"   {h.name,-18} → A {T(h.pos, Places.PlantA)}   → B {T(h.pos, Places.PlantB)}");
                sb.AppendLine($"de {a} (andável {NavGrid.Movable(a)}, z {NavGrid.ZAt(a)}) até {b} (andável {NavGrid.Movable(b)}, z {NavGrid.ZAt(b)})");
                foreach (int layer in new[] { 0, 1 })
                {
                    var p = NavGrid.Path(a, b, layer);
                    sb.AppendLine($"camada {layer}: {p.Count} pontos");
                    for (int i = 0; i < p.Count; i++)
                        sb.AppendLine($"  {i,2} {p[i].p} z {p[i].z,5:0.0} {p[i].link,-6} {(i + 1 < p.Count ? (NavGrid.Direct(p[i].p, p[i + 1].p) ? "reto ok" : "RETO NÃO") : "")}");
                }
                // um bot sozinho ali indo pro destino: posição, ponto do caminho e velocidade a cada 0,1 s
                var m = new Match(5); var ag = m.agents[1];
                foreach (var o in m.agents) if (o != ag) { o.alive = false; o.hp = 0; }
                ag.pos = a; ag.z = NavGrid.ZAt(a); ag.hasBomb = true; m.planSite = 'A';
                ag.brain.SetOrder(new Order { name = "teste", dest = b, plant = true, push = true });
                sb.AppendLine("trilha:");
                for (int k = 0; k < 40; k++)
                {
                    for (int s = 0; s < 3; s++) { ag.brain.Tick(Match.DT); ag.brain.TurnAim(Match.DT); typeof(Match).GetMethod("Integrate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(m, new object[] { ag }); m.t += Match.DT; }
                    var pth = ag.brain.path; int w = (int)typeof(Brain).GetField("wp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ag.brain);
                    sb.AppendLine($"  t {m.t:0.00} pos {ag.pos} vel {ag.vel} quer {ag.brain.wantVel} wp {w}/{pth?.Count} alvo {(pth != null && w < pth.Count ? pth[w].p.ToString() : "-")} fase {ag.brain.fase} '{ag.brain.thought}'");
                }
                sb.AppendLine("vizinhança (andável/z) passo 2:");
                for (int dy = -8; dy <= 8; dy += 2)
                {
                    var l = new StringBuilder();
                    for (int dx = -8; dx <= 8; dx += 2) { var q = a + new V2(dx, dy); l.Append(NavGrid.Movable(q) ? $"{NavGrid.ZAt(q),4:0}" : "   #"); }
                    sb.AppendLine(l.ToString());
                }
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/diag.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>Estado de um bot passo a passo num round: -seed S -nome X -de t1 -ate t2 → Logs/bots/trace.txt</summary>
        public static void Trace()
        {
            var sb = new StringBuilder();
            try
            {
                int seed = int.Parse(Arg("-seed", "1")); string nome = Arg("-nome", "MIRA");
                float de = float.Parse(Arg("-de", "30"), System.Globalization.CultureInfo.InvariantCulture), ate = float.Parse(Arg("-ate", "45"), System.Globalization.CultureInfo.InvariantCulture);
                var m = new Match(seed); var ag = m.agents.First(a => a.name == nome);
                var wpF = typeof(Brain).GetField("wp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float prox = de;
                while (!m.over && m.t < ate)
                {
                    m.Step();
                    if (m.t >= prox) {
                        prox += 0.1f; var pth = ag.brain.path; int w = (int)wpF.GetValue(ag.brain);
                        sb.AppendLine($"{m.t,6:0.00} pos {ag.pos} vel {ag.vel} ({ag.Speed:0}) quer {ag.brain.wantVel} max {ag.MaxSpeed:0} scoped {ag.scoped} faca {ag.faca} wp {w}/{pth?.Count} alvo {(pth != null && w < pth.Count ? pth[w].p.ToString() : "-")} fase {ag.brain.fase} micro {ag.brain.micro} '{ag.brain.thought}'");
                    }
                }
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/trace.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Tempo perdido em lugar seguro: nos primeiros 20 s, quem está parado (ou strafando) onde nenhum inimigo pode
        /// estar (calor ~0 em volta) — por time, função, fase, micro e pensamento. Logs/bots/base.txt
        /// </summary>
        public static void Base()
        {
            var sb = new StringBuilder();
            try
            {
                int n = int.Parse(Arg("-n", "64")), seed0 = int.Parse(Arg("-seed", "1"));
                var por = new Dictionary<string, float>(); float total = 0, vivo = 0;
                for (int s = 0; s < n; s++)
                {
                    var m = new Match(seed0 + s);
                    while (!m.over && m.t < 20)
                    {
                        m.Step();
                        foreach (var a in m.agents)
                        {
                            if (!a.alive) continue;
                            vivo += Match.DT;
                            if (m.intel[(int)a.team].HeatIn(a.pos, 350) > 0.05f) continue;   // pode ter inimigo perto: parar é normal
                            bool parado = a.Speed < 25, strafe = a.Speed >= 25 && a.brain.wantVel.Len > 1 && MathF.Abs(MathX.AngDiff(a.vel.Atan2, a.aim)) > 1.2f && a.Speed < a.MaxSpeed * 0.45f;
                            if (!parado && !strafe) continue;
                            total += Match.DT;
                            string th = a.brain.thought ?? ""; int c = th.IndexOfAny(new[] { ':', '—', '(' }); if (c > 0) th = th.Substring(0, c);
                            string k = $"{(a.IsT ? "TR" : "CT")} {a.role,-8} {(parado ? "parado " : "strafe ")} {a.brain.fase,-28} {a.brain.micro,-8} {th.Trim()}";
                            por[k] = por.TryGetValue(k, out var v) ? v + Match.DT : Match.DT;
                        }
                    }
                }
                sb.AppendLine($"{n} rounds, primeiros 20 s: parado/strafando em lugar seguro = {total / n:0.0} s por round ({100 * total / Math.Max(1, vivo):0.0}% do tempo vivo)");
                foreach (var kv in por.OrderByDescending(x => x.Value).Take(40)) sb.AppendLine($"  {kv.Value / n,5:0.00} s/round  {kv.Key}");
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/base.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Lineups: cada granada da biblioteca cai onde devia (arco sem prédio, ponto andável)? E cada smoke esconde
        /// mesmo as linhas pra que ela serve (smoke crescida, olho a olho)? Logs/bots/lineups.txt
        /// </summary>
        /// <summary>
        /// Aprende onde cada time costuma estar a cada 5 s do round (Rotas): roda -n rounds e grava
        /// Assets/Game/Sim/rotas.bin. Logs/bots/rotas.txt resume os lugares mais frequentados por faixa.
        /// </summary>
        public static void Rotas()
        {
            var sb = new StringBuilder();
            try
            {
                int n = int.Parse(Arg("-n", "400")), seed0 = int.Parse(Arg("-seed", "3000"));
                new Match(seed0); Spots.All();
                int nc = Intel.GX * Intel.GY;
                var soma = new float[2][][]; for (int tm = 0; tm < 2; tm++) { soma[tm] = new float[Sim.Rotas.NF][]; for (int f = 0; f < Sim.Rotas.NF; f++) soma[tm][f] = new float[nc]; }
                var amostras = new int[Sim.Rotas.NF]; var trava = new object();
                System.Threading.Tasks.Parallel.For(0, n, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 14 }, r =>
                {
                    var m = new Match(seed0 + r);
                    var loc = new float[2][][]; for (int tm = 0; tm < 2; tm++) { loc[tm] = new float[Sim.Rotas.NF][]; for (int f = 0; f < Sim.Rotas.NF; f++) loc[tm][f] = new float[nc]; }
                    var am = new int[Sim.Rotas.NF]; float prox = 0;
                    while (!m.over && m.t < 160)
                    {
                        m.Step();
                        if (m.t < prox) continue; prox = m.t + 0.5f;
                        int f = Math.Min(Sim.Rotas.NF - 1, (int)(m.t / Sim.Rotas.FAIXA)); am[f]++;
                        foreach (var a in m.agents) if (a.alive) loc[(int)a.team][f][Intel.Idx(a.pos)] += 1;
                    }
                    lock (trava) { for (int tm = 0; tm < 2; tm++) for (int f = 0; f < Sim.Rotas.NF; f++) for (int i = 0; i < nc; i++) soma[tm][f][i] += loc[tm][f][i]; for (int f = 0; f < Sim.Rotas.NF; f++) amostras[f] += am[f]; }
                });
                Sim.Rotas.Grava(soma, amostras);
                sb.AppendLine($"rotas: {n} rounds → {Sim.Rotas.ARQ}");
                foreach (int f in new[] { 1, 3, 6, 10, 14 })
                    foreach (int tm in new[] { 0, 1 })
                    {
                        var top = Enumerable.Range(0, nc).OrderByDescending(i => soma[tm][f][i]).Take(5)
                                            .Select(i => $"{Places.Region(new V2((i / Intel.GY + 0.5f) * Intel.C, (i % Intel.GY + 0.5f) * Intel.C))} {soma[tm][f][i] / Math.Max(1, amostras[f]):0.00}");
                        sb.AppendLine($"  {f * 5,3}-{f * 5 + 5,3}s {(tm == 0 ? "TR" : "CT")}: {string.Join(" · ", top)}");
                    }
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/rotas.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Física de pulo e queda: um bot sozinho sobe em cada lugar "em cima de caixa" a partir do chão do lado (pulo)
        /// e desce de volta (queda), com a física de verdade (Match.Integrate). Chegou? Quantas vezes ficou no ar?
        /// Logs/bots/fisica.txt
        /// </summary>
        public static void Fisica()
        {
            var sb = new StringBuilder();
            try
            {
                new Match(1);
                var integra = typeof(Match).GetMethod("Integrate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool Anda(V2 de, V2 para, out string info)
                {
                    var m = new Match(5); var ag = m.agents[1];
                    foreach (var o in m.agents) if (o != ag) { o.alive = false; o.hp = 0; }
                    ag.pos = de; ag.z = NavGrid.ZAt(de); ag.faca = false;
                    ag.brain.SetOrder(new Order { name = "teste", dest = para, push = true });
                    int pulos = 0; bool antes = false; float zMax = ag.z; var trilha = new StringBuilder(); float prox = 0;
                    var wpF = typeof(Brain).GetField("wp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    while (m.t < 12)
                    {
                        ag.brain.Tick(Match.DT); ag.brain.TurnAim(Match.DT); integra.Invoke(m, new object[] { ag }); m.t += Match.DT;
                        if (ag.noAr && !antes) pulos++; antes = ag.noAr; zMax = MathF.Max(zMax, ag.z);
                        if (m.t >= prox && m.t < 6) { prox += 0.2f; var pth = ag.brain.path; int w = (int)wpF.GetValue(ag.brain); trilha.Append($"\n        t {m.t:0.0} {ag.pos} z {ag.z:0.0} ar {ag.noAr} quer {ag.brain.wantVel} wp {w} {(pth != null && w < pth.Count ? pth[w].p + " z" + pth[w].z.ToString("0") : "")}"); }
                        if ((ag.pos - para).Len < 14 && MathF.Abs(ag.z - NavGrid.ZAt(para)) < 3 && !ag.noAr) { info = $"{m.t:0.0}s, {pulos}x no ar" + (m.t > 4 ? " (DEMOROU)" + trilha : ""); return m.t <= 6; }
                    }
                    info = $"parou em {Places.Region(ag.pos)} z {ag.z:0} (alvo z {NavGrid.ZAt(para):0}), {pulos}x no ar, fase {ag.brain.fase}" + trilha;
                    return false;
                }
                var cima = Spots.All().Where(s => s.kind == Spots.Kind.EmCima).ToList();
                int ok = 0, ruim = 0;
                // rotas fixas com degraus seguidos (caixote → carroça → tapete) e as que travaram em partida
                foreach (var (nome, de, para) in new[] { ("chão do B -> tapete pela carroça", new V2(143, 398), new V2(139.4f, 467.8f)), ("tapete -> chão do B", new V2(139.4f, 467.8f), new V2(143, 398)),
                                                         ("caixote -> tapete", new V2(149.5f, 392.5f), new V2(139.4f, 467.8f)), ("bomb B -> caixote", new V2(170, 380), new V2(149.5f, 392.5f)) })
                {
                    bool c = Anda(de, para, out string info);
                    if (c) ok++; else ruim++;
                    sb.AppendLine($"  {(c ? "ok  " : "RUIM")} {nome}: {info}");
                }
                foreach (var s in cima)
                {
                    float zc = NavGrid.ZAt(s.p); V2? chao = null;
                    for (float r = 30; r <= 90 && chao == null; r += 10)
                        for (int k = 0; k < 16; k++)
                        {
                            V2 q = s.p + V2.Ang(k * 0.3927f) * r;
                            if (NavGrid.Movable(q) && NavGrid.ZAt(q) < zc - 6 && NavGrid.Path(q, s.p).Count > 0) { chao = q; break; }
                        }
                    if (chao == null) continue;
                    foreach (var (de, para, tipo) in new[] { (chao.Value, s.p, "sobe"), (s.p, chao.Value, "desce") })
                    {
                        bool c = Anda(de, para, out string info);
                        if (c) ok++; else ruim++;
                        sb.AppendLine($"  {(c ? "ok  " : "RUIM")} {tipo,-5} {Places.Region(s.p),-22} (+{zc - NavGrid.ZAt(chao.Value):0}) {info}");
                    }
                }
                sb.Insert(0, $"FÍSICA (pulo/queda): {ok} ok, {ruim} ruins\n");
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/fisica.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Por que o mapa é do TR + o time anda colado? Rounds avulsos: (1) agrupamento — distância do aliado mais
        /// perto por time/fase e onde/por que ficam colados; (2) execução — quantos TR x CT no bomb quando o TR decide
        /// e quando planta, (3) first kill → vitória, como cada lado ganha. Logs/bots/analise.txt
        /// </summary>
        public static void Analise()
        {
            var sb = new StringBuilder();
            try
            {
                int n = int.Parse(Arg("-n", "256")), seed0 = int.Parse(Arg("-seed", "100"));
                TeamBrain.EXP = Arg("-exp", ""); if (TeamBrain.EXP != "") sb.AppendLine($"EXPERIMENTO: {TeamBrain.EXP}");
                new Match(seed0); Spots.All();
                var trava = new object();
                var amostra = new Dictionary<string, (int n, int colado, int perto, int grupo3)>();
                var coladoPor = new Dictionary<string, int>();
                var rounds = new List<(string win, string fk, int tExec, int ctExec, int ctNoSiteExec, int tPlant, int ctPlant, int ctNoSitePlant, bool plant, char site, float tPl, string reason, string plano, string setup)>();
                System.Threading.Tasks.Parallel.For(0, n, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 14 }, i =>
                {
                    var m = new Match(seed0 + i);
                    var loc = new Dictionary<string, (int n, int colado, int perto, int grupo3)>(); var locC = new Dictionary<string, int>();
                    int tE = -1, cE = -1, cSE = -1, tP = -1, cP = -1, cSP = -1; float prox = 0;
                    int NoSite(char s) { V2 c = Places.Plant(s); return m.agents.Count(a => a.alive && !a.IsT && (a.pos - c).Len < 420); }
                    var mortoVisto = new HashSet<int>();
                    while (!m.over)
                    {
                        m.Step();
                        foreach (var a in m.agents)
                            if (!a.alive && mortoVisto.Add(a.id) && !m.igl[0].Executou && !m.bombPlanted)
                            {   // morte na fase de controle de mapa (antes do TR decidir): de quem, onde, fazendo o quê
                                string k = $"   MORTE {(a.IsT ? "TR" : "CT")} {Places.Region(a.pos),-20} ordem: {a.brain.order.name,-34} matador {(a.killedBy == null ? "-" : Places.Region(a.killedBy.pos) + (a.killedBy.gunId == "awp" ? " (AWP)" : ""))}";
                                locC[k] = locC.TryGetValue(k, out var c1) ? c1 + 1 : 1;
                            }
                        if (tE < 0 && m.igl[0].Executou)
                        {
                            tE = m.Alive(Team.T); cE = m.Alive(Team.CT); cSE = NoSite(m.planSite);
                            if (cSE == 0)
                            {   // bomb vazio na hora da execução: cadê o âncora dele?
                                var anc = m.agents.FirstOrDefault(a => !a.IsT && a.role == Role.Ancora && a.brain.homeSite == m.planSite);
                                string k = anc == null ? "sem âncora" : !anc.alive ? $"âncora morto ({(anc.killedBy != null ? Places.Region(anc.pos) : "?")})" : $"âncora vivo em {Places.Region(anc.pos)} — {anc.brain.order.name}";
                                locC["   VAZIO: " + k] = locC.TryGetValue("   VAZIO: " + k, out var c0) ? c0 + 1 : 1;
                            }
                        }
                        if (tP < 0 && m.bombPlanted) { tP = m.Alive(Team.T); cP = m.Alive(Team.CT); cSP = NoSite(m.planSite); }
                        if (m.t < prox) continue;
                        prox = m.t + 0.5f;
                        foreach (var a in m.agents)
                        {
                            if (!a.alive) continue;
                            var it = m.intel[(int)a.team];
                            // CT andando pra cima de 2+ inimigos que ele acha que estão ali
                            if (!a.IsT && !m.bombPlanted && a.Speed > 20 && a.brain.target == null)
                            {
                                V2 dirv = a.vel / a.Speed; float eFrente = 0;
                                foreach (var c in it.contacts.Values) if (m.t - c.t < 4 && m.agents[c.id].alive && (c.pos - a.pos).Len < 450 && (c.pos - a.pos).Dot(dirv) > 0.5f * (c.pos - a.pos).Len) eFrente++;
                                if (eFrente >= 2) { string k = $"   PRACIMA {a.brain.fase,-24} ordem: {a.brain.order.name}"; locC[k] = locC.TryGetValue(k, out var c2) ? c2 + 1 : 1; }
                            }
                            // TR indo pegar o C4 caído que um CT conhecido está vendo
                            if (a.IsT && m.bombDropped && a.brain.order.name.StartsWith("pegar o bomb") && a.Speed > 10)
                            {
                                bool visto = it.contacts.Values.Any(c => m.t - c.t < 8 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 650 && MapData.LosWalk(c.pos, c.z, m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f));
                                string k = $"   C4CAIDO {(visto ? "VIGIADO" : "livre")}"; locC[k] = locC.TryGetValue(k, out var c3) ? c3 + 1 : 1;
                            }
                            // dois do mesmo time parados, perto, olhando pro mesmo lado
                            // o ponto que cada um vigia (onde a linha da mira bate) — dois vigiando o mesmo ponto = ângulo dobrado
                            V2 Vigia(Agent q) { V2 d = V2.Ang(q.aim); V2 last = q.pos; for (float s = 20; s < 700; s += 20) { V2 p = q.pos + d * s; if (!NavGrid.Movable(p) || !MapData.LosWalk(q.pos, q.z, p, NavGrid.ZAt(p), 18f)) break; last = p; } return last; }
                            if (a.Speed < 8 && a.brain.target == null)
                                foreach (var b in m.agents)
                                    if (b.id > a.id && b.alive && b.team == a.team && b.Speed < 8 && b.brain.target == null && (b.pos - a.pos).Len < 250 && MathF.Abs(MathX.AngDiff(a.aim, b.aim)) < 0.35f && (Vigia(a) - Vigia(b)).Len < 70)
                                    { string k = $"   MESMOANG {(a.IsT ? "TR" : "CT")} {Places.Region(a.pos),-20} {a.brain.order.name} + {b.brain.order.name}"; locC[k] = locC.TryGetValue(k, out var c4) ? c4 + 1 : 1; }
                            var mates = m.agents.Where(x => x != a && x.alive && x.team == a.team).ToList(); if (mates.Count == 0) continue;
                            float d = mates.Min(x => (x.pos - a.pos).Len); int g = mates.Count(x => (x.pos - a.pos).Len < 150);
                            string fase = a.IsT ? (m.bombPlanted ? "TR pós-plant" : m.igl[0].Executou ? "TR execução" : "TR antes de decidir") : (m.bombPlanted ? "CT retomada" : "CT antes do plant");
                            loc.TryGetValue(fase, out var v); loc[fase] = (v.n + 1, v.colado + (d < 60 ? 1 : 0), v.perto + (d < 120 ? 1 : 0), v.grupo3 + (g >= 2 ? 1 : 0));
                            if (d < 60 && !m.bombPlanted && !(a.IsT && m.igl[0].Executou))
                            {
                                string k = $"{fase,-20} {Places.Region(a.pos),-22} {a.brain.fase,-26} ordem: {a.brain.order.name}";
                                locC[k] = locC.TryGetValue(k, out var c) ? c + 1 : 1;
                            }
                        }
                    }
                    lock (trava)
                    {
                        foreach (var kv in loc) { amostra.TryGetValue(kv.Key, out var v); amostra[kv.Key] = (v.n + kv.Value.n, v.colado + kv.Value.colado, v.perto + kv.Value.perto, v.grupo3 + kv.Value.grupo3); }
                        foreach (var kv in locC) coladoPor[kv.Key] = coladoPor.TryGetValue(kv.Key, out var c) ? c + kv.Value : kv.Value;
                        rounds.Add((m.winner, m.firstKill == null ? "-" : m.firstKill.IsT ? "TR" : "CT", tE, cE, cSE, tP, cP, cSP, m.bombPlanted, m.planSite, m.plantedAt, m.reason, m.igl[0].plan, m.igl[1].plan));
                    }
                });
                sb.AppendLine($"{n} rounds avulsos (compra cheia)");
                sb.AppendLine("AGRUPAMENTO (aliado mais perto: colado <60, perto <120; grupo = 2+ aliados a <150):");
                foreach (var kv in amostra.OrderBy(x => x.Key))
                    sb.AppendLine($"  {kv.Key,-20} colado {100f * kv.Value.colado / kv.Value.n,4:0}%   perto {100f * kv.Value.perto / kv.Value.n,4:0}%   em grupo de 3+ {100f * kv.Value.grupo3 / kv.Value.n,4:0}%");
                sb.AppendLine("  onde ficam colados (fora da execução/pós-plant; amostras de 0,5 s por jogador):");
                int mCT = coladoPor.Where(x => x.Key.Contains("MORTE CT")).Sum(x => x.Value), mTR = coladoPor.Where(x => x.Key.Contains("MORTE TR")).Sum(x => x.Value);
                sb.AppendLine($"MORTES NO CONTROLE DE MAPA (antes do TR decidir o bomb): CT {mCT / (float)n:0.00}/round · TR {mTR / (float)n:0.00}/round");
                foreach (var kv in coladoPor.Where(x => x.Key.Contains("MORTE")).OrderByDescending(x => x.Value).Take(22)) sb.AppendLine($"   {kv.Value,4}  {kv.Key.Replace("   MORTE ", "")}");
                foreach (var kv in coladoPor.Where(x => x.Key.StartsWith("   ") == false).OrderByDescending(x => x.Value).Take(25)) sb.AppendLine($"   {kv.Value / (float)n,5:0.00}/round  {kv.Key}");
                foreach (var (tag, titulo) in new[] { ("PRACIMA", "CT ANDANDO PRA CIMA DE 2+ INIMIGOS VISTOS (amostras de 0,5 s)"), ("C4CAIDO", "TR INDO PEGAR O C4 CAÍDO (amostras)"), ("MESMOANG", "DOIS PARADOS PERTO OLHANDO PRO MESMO LADO (amostras)") })
                {
                    sb.AppendLine($"{titulo}: {coladoPor.Where(x => x.Key.Contains(tag)).Sum(x => x.Value) / (float)n:0.00}/round");
                    foreach (var kv in coladoPor.Where(x => x.Key.Contains(tag)).OrderByDescending(x => x.Value).Take(10)) sb.AppendLine($"   {kv.Value,5}  {kv.Key.Replace("   " + tag + " ", "")}");
                }
                sb.AppendLine("BOMB ATACADO VAZIO NA HORA DA EXECUÇÃO — cadê o âncora (rounds):");
                foreach (var kv in coladoPor.Where(x => x.Key.Contains("VAZIO")).OrderByDescending(x => x.Value).Take(20)) sb.AppendLine($"   {kv.Value,4}  {kv.Key.Replace("   VAZIO: ", "")}");
                var R = rounds;
                sb.AppendLine($"COMO GANHAM: TR {R.Count(r => r.win == "TR")} ({100f * R.Count(r => r.win == "TR") / R.Count:0}%)  — " + string.Join(", ", R.GroupBy(r => $"{r.win}: {r.reason}").OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
                foreach (var fk in new[] { "TR", "CT" }) { var g = R.Where(r => r.fk == fk).ToList(); if (g.Count > 0) sb.AppendLine($"  first kill do {fk}: {g.Count} rounds, {fk} ganhou {100f * g.Count(r => r.win == fk) / g.Count:0}%  (pro: ~70-75%)"); }
                var ex = R.Where(r => r.tExec >= 0).ToList();
                sb.AppendLine($"EXECUÇÃO (TR decidiu o bomb): {ex.Count} rounds · vivos TR {ex.Average(r => r.tExec):0.0} x CT {ex.Average(r => r.ctExec):0.0} · CT no bomb atacado (<420 do plant) {ex.Average(r => r.ctNoSiteExec):0.0}");
                foreach (int k in new[] { 0, 1, 2, 3 })
                {
                    var g = ex.Where(r => Math.Min(r.ctNoSiteExec, 3) == k).ToList(); if (g.Count == 0) continue;
                    sb.AppendLine($"  {(k == 3 ? "3+" : k.ToString())} CT no bomb na hora: {g.Count,3} rounds · planta {100f * g.Count(r => r.plant) / g.Count,3:0}% · TR ganha {100f * g.Count(r => r.win == "TR") / g.Count,3:0}%");
                }
                var pl = R.Where(r => r.plant).ToList();
                if (pl.Count > 0)
                {
                    sb.AppendLine($"NO PLANT: {pl.Count} · vivos TR {pl.Average(r => r.tPlant):0.0} x CT {pl.Average(r => r.ctPlant):0.0} · CT perto do bomb {pl.Average(r => r.ctNoSitePlant):0.0} · TR ganha {100f * pl.Count(r => r.win == "TR") / pl.Count:0}% (pro ~70-75%)");
                    foreach (var g in pl.GroupBy(r => (r.tPlant - r.ctPlant)).OrderBy(g => g.Key))
                        sb.AppendLine($"  vantagem TR {g.Key,2}: {g.Count(),3} plants · TR ganha {100f * g.Count(r => r.win == "TR") / g.Count(),3:0}%");
                    foreach (var g in pl.GroupBy(r => r.site)) sb.AppendLine($"  bomb {g.Key}: {g.Count()} plants, TR ganha {100f * g.Count(r => r.win == "TR") / g.Count():0}%");
                }
                var np = R.Where(r => !r.plant).ToList();
                sb.AppendLine($"SEM PLANT: {np.Count} · TR ganha {100f * np.Count(r => r.win == "TR") / Math.Max(1, np.Count):0}% (sem plant o TR só ganha matando todo mundo)");
                sb.AppendLine("SETUP CT x vitória CT: " + string.Join(", ", R.GroupBy(r => r.setup).Select(g => $"{g.Key} {100f * g.Count(r => r.win == "CT") / g.Count():0}% ({g.Count()})")));
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/analise.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>Quem olha não fica parado no ponto exato (ajeita, peek): testa a origem e uns passos em volta.</summary>
        static IEnumerable<(V2 de, V2[] para)> ComFolga((V2 de, V2[] para)[] linhas)
        {
            foreach (var (de, para) in linhas)
                foreach (var o in new[] { new V2(0, 0), new V2(14, 0), new V2(-14, 0), new V2(0, 14), new V2(0, -14) })
                    if (NavGrid.Movable(de + o)) yield return (de + o, para);
        }

        public static void Lineups()
        {
            var sb = new StringBuilder(); int ruins = 0, fisicaLonge = 0;
            try
            {
                new Match(1);
                foreach (var l in Places.TREINADAS.OrderBy(x => x.kind).ThenBy(x => x.name))
                {
                    var cai = Match.Arco(l.from, NavGrid.ZAt(l.from), l.to);
                    bool ok = NavGrid.Movable(l.from) && NavGrid.Movable(l.to) && (cai - l.to).Len < 20;
                    string linha = $"{(ok ? "ok  " : "RUIM")} {l.kind,-8} {l.name,-34} de {Places.Region(l.from)} em {Places.Region(l.to)}";
                    if (!NavGrid.Movable(l.from)) linha += " | de: fora da navmesh";
                    if (!NavGrid.Movable(l.to)) linha += " | alvo fora da navmesh";
                    if ((cai - l.to).Len >= 20) linha += $" | bate e cai a {(cai - l.to).Len:0} do alvo ({Places.Region(cai)})";
                    // física de verdade (quica, rola, fusível): onde estoura
                    var fi = Match.Simula(l.from, NavGrid.ZAt(l.from), l.kind == "smoke" ? Match.MiraSmoke(l.from, NavGrid.ZAt(l.from), l.to) : l.to, l.kind);
                    float desvio = (fi.p - l.to).Len; if (desvio > 35) fisicaLonge++;
                    linha += $" | física: estoura a {desvio:0} do pixel em {fi.t:0.0}s, {fi.quiques} quique(s){(fi.paredes > 0 ? $", {fi.paredes} parede" : "")}{(fi.pop ? ", POP no ar" : "")}{(fi.noAr ? ", estourou no ar" : "")} (altura {fi.z - NavGrid.ZAt(fi.p):0})";
                    if (l.kind == "smoke" && Places.CORTA.TryGetValue(l, out var linhas))
                    {
                        var s = new CSM.View.SmokeVolume(new V3(l.to.x, l.to.y, NavGrid.ZAt(l.to))); s.age = 4;
                        int via = 0, cortou = 0; var furos = new List<string>();
                        foreach (var (de, para) in ComFolga(linhas))
                            foreach (var p in para)
                            {
                                float za = NavGrid.ZAt(de) + 18, zb = NavGrid.ZAt(p) + 18;
                                if (!MapData.LosWalk(de, NavGrid.ZAt(de), p, NavGrid.ZAt(p), 18f)) continue;
                                via++;
                                bool c = false; float L = (p - de).Len; int n = (int)(L / 6) + 1;
                                for (int i = 1; i < n && !c; i++) { float u = i / (float)n; V2 q = de + (p - de) * u; if (s.Sample(new V3(q.x, q.y, za + (zb - za) * u), 4) > 0.35f) c = true; }
                                if (c) cortou++; else furos.Add($"{Places.Region(de)}→{Places.Region(p)}");
                            }
                        linha += $" | corta {cortou}/{via} linhas" + (furos.Count > 0 ? " (vaza: " + string.Join(", ", furos) + ")" : "");
                        if (via == 0)
                            foreach (var (de, para) in linhas)
                            {   // ninguém dali vê o alvo: onde perto de 'de' dá pra ver (pra corrigir a intenção)
                                V2 ach = de; float bd = 1e9f;
                                for (float dx = -6; dx <= 6; dx += 0.5f) for (float dy = -6; dy <= 6; dy += 0.5f)
                                {
                                    V2 q = de + new V2(dx * MapData.Q, dy * MapData.Q);
                                    if (NavGrid.Movable(q) && MapData.LosWalk(q, NavGrid.ZAt(q), para[0], NavGrid.ZAt(para[0]), 18f) && (q - de).Len < bd) { bd = (q - de).Len; ach = q; }
                                }
                                linha += $" | de {Places.Region(de)} ninguém vê {Places.Region(para[0])}; mais perto que vê: Cell({ach.x / MapData.Q - 0.5f:0.##}, {ach.y / MapData.Q - 0.5f:0.##}) {Places.Region(ach)}";
                            }
                        if (via == 0 || cortou < via) ok = false;
                    }
                    if (!ok) { ruins++; if (linha.StartsWith("ok")) linha = "RUIM" + linha.Substring(4); }
                    sb.AppendLine(linha);
                    if (!ok)
                    {   // procura o pixel bom mais perto (alvo andável, arco chega, smoke corta o máximo)
                        float bv = float.MinValue; V2 bestTo = l.to; string bestDesc = "nenhum";
                        for (float dx = -3; dx <= 3; dx += 0.25f)
                            for (float dy = -3; dy <= 3; dy += 0.25f)
                            {
                                V2 q = l.to + new V2(dx * MapData.Q, dy * MapData.Q);
                                if (!NavGrid.Movable(q) || (Match.Arco(l.from, NavGrid.ZAt(l.from), q) - q).Len >= 20) continue;
                                float sc = -(dx * dx + dy * dy) * 0.5f; int cort = 0, via = 0;
                                if (l.kind == "smoke" && Places.CORTA.TryGetValue(l, out var ls2))
                                {
                                    var s = new CSM.View.SmokeVolume(new V3(q.x, q.y, NavGrid.ZAt(q))); s.age = 4;
                                    foreach (var (de, para) in ComFolga(ls2))
                                        foreach (var p in para)
                                        {
                                            if (!MapData.LosWalk(de, NavGrid.ZAt(de), p, NavGrid.ZAt(p), 18f)) continue;
                                            via++; float za = NavGrid.ZAt(de) + 18, zb = NavGrid.ZAt(p) + 18, L = (p - de).Len; int n = (int)(L / 6) + 1;
                                            for (int i = 1; i < n; i++) { float u = i / (float)n; V2 w = de + (p - de) * u; if (s.Sample(new V3(w.x, w.y, za + (zb - za) * u), 4) > 0.35f) { cort++; break; } }
                                        }
                                    sc += 20 * cort;
                                }
                                if (sc > bv) { bv = sc; bestTo = q; bestDesc = $"Cell({bestTo.x / MapData.Q - 0.5f:0.##}, {bestTo.y / MapData.Q - 0.5f:0.##}) em {Places.Region(q)}" + (via > 0 ? $" corta {cort}/{via}" : ""); }
                            }
                        sb.AppendLine($"       sugestão de alvo: {bestDesc}");
                    }
                }
                sb.Insert(0, $"LINEUPS: {Places.TREINADAS.Count}, ruins {ruins}, estouram a >35 do pixel com a física: {fisicaLonge}\n");
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/bots/lineups.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        // ================================================================ partidas inteiras
        /// <summary>
        /// Partidas completas (MR12, prorrogação, economia, compra, save): -n partidas (em paralelo), -seed.
        /// Logs/bots/partidas.txt = resumo; Logs/bots/partida_SEED.txt = round a round da primeira.
        /// </summary>
        public static void Partida()
        {
            var sb = new StringBuilder();
            try
            {
                int n = int.Parse(Arg("-n", "28")), seed0 = int.Parse(Arg("-seed", "1"));
                TeamBrain.EXP = Arg("-exp", ""); if (TeamBrain.EXP != "") sb.AppendLine($"EXPERIMENTO: {TeamBrain.EXP}");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var p = PesosArg();
                new Match(seed0); Spots.All();
                var ps = new Sim.Partida[n];
                Sim.Partida.SemLeitura = Array.IndexOf(Environment.GetCommandLineArgs(), "-semleitura") >= 0;
                if (Sim.Partida.SemLeitura) sb.AppendLine("EXPERIMENTO: sem leitura do adversário");
                var mt = new Metricas();
                int explodiuSemTr = 0, smokeDef = 0, cacadas = 0, cacaSaida = 0, awpDrop = 0, medoDefuse = 0, mortosExplosao = 0, pegouChao = 0;
                System.Threading.Tasks.Parallel.For(0, n, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, i =>
                {
                    var pt = new Sim.Partida(seed0 + i, p, p);
                    pt.Jogar(m =>
                    {
                        lock (mt)
                        {
                            mt.Add(m);
                            if (m.reason == "bomb explodiu" && m.Alive(Team.T) == 0) explodiuSemTr++;
                            foreach (var a in m.agents) foreach (var (t, s) in a.brain.log)
                            {
                                if (s.StartsWith("smoke defuse")) smokeDef++;
                                else if (s.StartsWith("pegando a AWP pra dropar")) awpDrop++;
                                else if (s.Contains("mas não vou arriscar")) medoDefuse++;
                            }
                            foreach (var l in m.log) { if (l.Contains("vão caçar") || l.Contains("vai caçar")) cacadas++; if (l.Contains("caçar quem sai")) cacaSaida++; if (l.Contains("morreu na explosão")) mortosExplosao++; if (l.Contains("do chão")) pegouChao++; }
                        }
                    });
                    ps[i] = pt;
                });
                sb.AppendLine($"{n} partidas em {sw.Elapsed.TotalSeconds:0} s");
                var pl = ps.Select(x => $"{Math.Max(x.A.placar, x.B.placar)}x{Math.Min(x.A.placar, x.B.placar)}").GroupBy(s => s).OrderByDescending(g => g.Count());
                sb.AppendLine($"PLACARES: {string.Join(", ", pl.Select(g => $"{g.Key} ({g.Count()})"))}");
                sb.AppendLine($"rounds por partida: {ps.Average(x => x.historico.Count):0.0}   prorrogação: {ps.Count(x => x.historico.Count > 24)}/{n}   VULCÃO venceu {ps.Count(x => x.vencedor == x.A)}/{n}");
                var rs = ps.SelectMany(x => x.historico).ToList();
                sb.AppendLine($"TR vence {100f * rs.Count(x => (x.ladoA == "TR") == x.timeAvenceu) / rs.Count:0}% dos rounds");
                // economia
                sb.AppendLine("COMPRA (vitória de quem comprou assim, contra o que o outro comprou):");
                foreach (var g in rs.SelectMany(x => new[] { (eu: x.compraT, ele: x.compraCT, ganhei: (x.ladoA == "TR") == x.timeAvenceu), (eu: x.compraCT, ele: x.compraT, ganhei: (x.ladoA == "TR") != x.timeAvenceu) })
                                     .GroupBy(x => $"{x.eu} x {x.ele}").OrderByDescending(g => g.Count()))
                    sb.AppendLine($"  {g.Key,-18} {g.Count(),4} rounds   venceu {100f * g.Count(x => x.ganhei) / g.Count(),3:0}%");
                foreach (int t in new[] { 0, 1 })
                {
                    float n0 = Math.Max(1, Sim.Partida.Diag[t, 0]);
                    sb.AppendLine($"COMPRA CHEIA {(t == 0 ? "TR" : "CT")}: {Sim.Partida.Diag[t, 0]} rounds · AWPer com AWP {100 * Sim.Partida.Diag[t, 1] / n0:0}% · com primária {Sim.Partida.Diag[t, 2] / n0:0.0}/5 · capacete {Sim.Partida.Diag[t, 3] / n0:0.0}/5 · granadas {Sim.Partida.Diag[t, 4] / n0:0.0} · kits {Sim.Partida.Diag[t, 5] / n0:0.0}");
                }
                sb.AppendLine("TR VENCE POR COMPRA (TR x CT):");
                foreach (var g in rs.GroupBy(x => $"{x.compraT} x {x.compraCT}").OrderByDescending(g => g.Count()))
                    sb.AppendLine($"  {g.Key,-18} {g.Count(),4} rounds   TR venceu {100f * g.Count(x => (x.ladoA == "TR") == x.timeAvenceu) / g.Count(),3:0}%");
                sb.AppendLine("PLANOS TR NA PARTIDA (vitória do TR):");
                foreach (var g in ps.SelectMany(x => x.historico).GroupBy(x => x.planoT).OrderByDescending(g => g.Count()))
                    sb.AppendLine($"  {g.Key,-18} {g.Count(),4}   {100f * g.Count(x => (x.ladoA == "TR") == x.timeAvenceu) / g.Count(),3:0}%");
                // pistol → 2º round
                int pistolW = 0, conv = 0;
                var porCompra = new Dictionary<string, (int n, int c)>();
                foreach (var x in ps) foreach (int k in new[] { 0, 12 }) if (x.historico.Count > k + 1)
                {
                    pistolW++; var h1 = x.historico[k + 1];
                    bool c = x.historico[k].timeAvenceu == h1.timeAvenceu; if (c) conv++;
                    bool perdedorA = !x.historico[k].timeAvenceu, aTR = h1.ladoA == "TR";
                    string compraPerdedor = perdedorA == aTR ? h1.compraT : h1.compraCT;
                    porCompra.TryGetValue(compraPerdedor, out var v); porCompra[compraPerdedor] = (v.n + 1, v.c + (c ? 1 : 0));
                }
                sb.AppendLine($"quem ganha o pistol ganha o 2o round: {100f * conv / Math.Max(1, pistolW):0}% (pro: ~75-85%) — perdedor do pistol: " +
                              string.Join(", ", porCompra.Select(kv => $"{kv.Key} {kv.Value.n}x (perdeu {100f * kv.Value.c / kv.Value.n:0}%)")));
                sb.AppendLine($"GUARDAR ARMA: {rs.Sum(x => x.guardaram)} decisões ({rs.Sum(x => x.guardadasVivas)} armas salvas) em {rs.Count} rounds");
                sb.AppendLine($"  CT foi caçar quem sai do bomb: {cacaSaida}   TR foi caçar o save do CT: {cacadas}");
                sb.AppendLine($"  bomb explodiu com todo TR morto (CT sem tempo): {explodiuSemTr}   morreram na explosão: {mortosExplosao}");
                sb.AppendLine($"  smoke defuse: {smokeDef}   TR foi parar defuse que não dava tempo (medo): {medoDefuse}   AWP pega pra dropar pro AWPer: {awpDrop}   pegou arma do chão: {pegouChao}");
                sb.AppendLine(); sb.AppendLine(mt.Relatorio());
                sb.AppendLine("JOGADORES (todas as partidas):");
                foreach (var j in ps.SelectMany(x => x.A.js.Concat(x.B.js)).GroupBy(j => j.nome))
                {
                    int k = j.Sum(x => x.k), d = j.Sum(x => x.d), ro = j.Sum(x => x.rounds);
                    sb.AppendLine($"  {j.Key,-7} K/D {k}/{d} ({(d > 0 ? (float)k / d : k):0.00})  ADR {(ro > 0 ? j.Sum(x => x.dano) / (float)ro : 0):0}  HS {(k > 0 ? 100f * j.Sum(x => x.hs) / k : 0):0}%  guardou {j.Sum(x => x.guardou)}");
                }
                Directory.CreateDirectory("Logs/bots");
                File.WriteAllText($"Logs/bots/partida_{seed0}.txt", string.Join("\n", ps[0].log));
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            if (Erros.Count > 0) { sb.AppendLine($"ROUNDS QUE QUEBRARAM: {Erros.Count}"); foreach (var e in Erros.Take(10)) sb.AppendLine("  " + e); }
            File.WriteAllText("Logs/bots/partidas.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        // ================================================================ treino
        /// <summary>
        /// Treino dos pesos: (1+1)-ES — mexe em 1 a 3 pesos (no espaço 0..1 de cada faixa), roda os mesmos rounds (mesmas
        /// sementes, pra comparar justo), fica com o novo se a nota do validador melhorou. O passo cresce quando acerta e
        /// diminui quando erra. No fim valida em sementes que o treino nunca viu (pra ver se não "decorou" os rounds).
        ///   -iter N (60)  -rounds R por avaliação (48)  -seed S  -pesos arquivo de partida
        /// Saída: Logs/bots/treino.txt (cada passo) e Logs/bots/pesos_melhor.txt (copiar pra Assets/Game/Sim/pesos.txt
        /// quando aprovado).
        /// </summary>
        public static void Treino()
        {
            var sb = new StringBuilder(); const string OUT = "Logs/bots/treino.txt";
            Directory.CreateDirectory("Logs/bots");
            try
            {
                int iters = int.Parse(Arg("-iter", "60")), n = int.Parse(Arg("-rounds", "48")), seed0 = int.Parse(Arg("-seed", "1000"));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Metricas Aval(Pesos p, int s0, int nn) { var mt = new Metricas(); foreach (var m in Rodar(nn, s0, p, p)) mt.Add(m); return mt; }
                var inicio = PesosArg() ?? Pesos.Padrao();
                var best = inicio.Clone();
                var v0 = Aval(inicio, seed0 + 50000, n * 2);
                sb.AppendLine($"TREINO: {iters} passos; cada passo compara o atual e o novo nos mesmos {n} rounds novos (sementes nunca repetem)");
                sb.AppendLine("ANTES (validação):"); sb.AppendLine(v0.Relatorio());
                File.WriteAllText(OUT, sb.ToString());
                var rng = new Rng(seed0); float sigma = 0.15f; int K = Pesos.Campos.Length;
                for (int it = 0; it < iters; it++)
                {
                    int s0 = seed0 + 1000 + it * n;   // rounds novos a cada passo: ninguém "decora" sementes
                    var c = best.Clone(); var mexeu = new List<string>();
                    int nm = 1 + rng.Int(3);
                    for (int j = 0; j < nm; j++)
                    {
                        int i = rng.Int(K); float a = c.GetN(i); c.SetN(i, a + rng.Normal(0, sigma));
                        mexeu.Add($"{Pesos.Campos[i].Name} {Pesos.FaixaDe(Pesos.Campos[i]).min + a * (Pesos.FaixaDe(Pesos.Campos[i]).max - Pesos.FaixaDe(Pesos.Campos[i]).min):0.##}→{(float)Pesos.Campos[i].GetValue(c):0.##}");
                    }
                    float lc = Aval(c, s0, n).Perda(), li = Aval(best, s0, n).Perda();
                    bool ok = lc < li;
                    if (ok) { best = c; sigma = MathF.Min(0.4f, sigma * 1.4f); } else sigma = MathF.Max(0.03f, sigma * 0.9f);
                    sb.AppendLine($"{it,3} {(ok ? "MELHOROU" : "        ")} novo {lc,7:0.000} x atual {li,7:0.000}  passo {sigma:0.00}  {string.Join(", ", mexeu)}   [{sw.Elapsed.TotalMinutes:0.0} min]");
                    File.WriteAllText(OUT, sb.ToString());
                    File.WriteAllText("Logs/bots/pesos_melhor.txt", best.Texto());
                }
                // validação: o antes e o depois no mesmo conjunto de rounds que o treino nunca usou
                var v1 = Aval(best, seed0 + 50000, n * 2);
                if (v1.Perda() >= v0.Perda())
                {   // o ruído enganou o treino: não entrega pesos piores que os de partida
                    sb.AppendLine($"REJEITADO: validação piorou ({v0.Perda():0.000} → {v1.Perda():0.000}); pesos_melhor.txt = os de partida");
                    File.WriteAllText("Logs/bots/pesos_melhor.txt", inicio.Texto());
                }
                sb.AppendLine($"VALIDAÇÃO em {n * 2} rounds nunca vistos: antes {v0.Perda():0.000} → depois {v1.Perda():0.000}");
                sb.AppendLine(v1.Relatorio());
                sb.AppendLine("PESOS:"); sb.AppendLine(best.Texto());
                sb.AppendLine($"tempo total {sw.Elapsed.TotalMinutes:0.0} min");
                sb.AppendLine($"rounds que quebraram: {Erros.Count}"); foreach (var e in Erros.Take(10)) sb.AppendLine("  " + e);
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            File.WriteAllText(OUT, sb.ToString());
            EditorApplication.Exit(0);
        }

        static string RoundLog(Match m)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TR: {m.igl[0].plan}   CT: {m.igl[1].plan}   vencedor: {m.winner} ({m.reason})");
            var lines = new List<(float t, string s)>();
            foreach (var l in m.log) lines.Add((float.Parse(l.Substring(0, 6).Trim(), System.Globalization.CultureInfo.CurrentCulture), "  " + l.Substring(8)));
            foreach (var a in m.agents) foreach (var (t, s) in a.brain.log) lines.Add((t, $"     {a.name,-7}: {s}"));
            foreach (var (t, s) in lines.OrderBy(x => x.t)) sb.AppendLine($"{t,6:0.00}{s}");
            return sb.ToString();
        }

        // ================================================================ vídeo de um round
        const int MW = Iso.BW / 2, MH = Iso.BH / 2, PW = 640, FW = MW + PW, FH = MH;
        static C3[] baseMap;

        /// <summary>O mapa estático em meia resolução (fundo do vídeo e do replay).</summary>
        static void MontaBase()
        {
            if (baseMap != null) return;
            var sm = new StaticMap();
            baseMap = new C3[MW * MH];
            for (int y = 0; y < MH; y++)
                for (int x = 0; x < MW; x++)
                {
                    C3 s = new C3(0, 0, 0); int bg = 0;
                    for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) { int i = (2 * y + b) * Iso.BW + 2 * x + a; if (sm.OBJ[i] == 0) bg++; s = s + sm.RGB[i] * 0.25f; }
                    baseMap[y * MW + x] = bg >= 3 ? new C3(26, 20, 22) : s * 0.78f;
                }
        }

        // ================================================================ replay (página com anotação)
        static readonly string[] ARMAS = { "ak", "m4", "m4s", "awp", "mp9", "mac10", "usp", "glock", "faca", "deagle", "p250" };   // índice vai no json: só acrescentar no fim
        static string Js(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";
        static string F1(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        static int I(float v) => (int)MathF.Round(v);

        /// <summary>
        /// Replay de uma partida inteira pra página de anotação: cada round em quadros de 0,1 s (posição e mira de cada
        /// um já na imagem do mapa, vida, estado, arma), smokes, fogo, flashes, tiros, bomb, pensamentos e log.
        ///   -seed S → Logs/bots/replay/partida_S.json e Logs/bots/replay/mapa.png
        /// </summary>
        public static void Replay()
        {
            string res;
            try
            {
                int seed = int.Parse(Arg("-seed", "1"));
                Brain.Explicar = true;
                MontaBase();
                Directory.CreateDirectory("Logs/bots/replay");
                var tex = new Texture2D(MW, MH, TextureFormat.RGB24, false); var px = new Color32[MW * MH];
                for (int y = 0; y < MH; y++) for (int x = 0; x < MW; x++) { C3 c = baseMap[y * MW + x]; px[(MH - 1 - y) * MW + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255); }
                tex.SetPixels32(px); File.WriteAllBytes("Logs/bots/replay/mapa.png", tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                var p = PesosArg();
                var pt = new Sim.Partida(seed, p, p);
                var sb = new StringBuilder();
                sb.Append($"{{\"seed\":{seed},\"mapa\":[{MW},{MH}],\"times\":[{Js(pt.A.nome)},{Js(pt.B.nome)}],\"armas\":[{string.Join(",", ARMAS.Select(Js))}],\"rounds\":[");
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
                    sb.Append(",\"jog\":[" + string.Join(",", m.agents.Select(a => $"{{\"n\":{Js(a.name)},\"tr\":{(a.IsT ? 1 : 0)},\"r\":{Js(a.role.ToString())}}}")) + "]");
                    sb.Append(",\"f\":[" + fr + "]");
                    sb.Append(",\"pens\":[" + string.Join(",", m.agents.SelectMany(a => a.brain.decisoes.Select(l => (l.t, a.id, l.s, l.porque, l.ctx))).OrderBy(x => x.t).Select(x => $"[{F1(x.t)},{x.id},{Js(x.s)},{Js(x.porque)},{Js(x.ctx)}]")) + "]");
                    sb.Append(",\"susp\":[" + string.Join(",", m.suspeitas.Select(x => $"[{F1(x.t)},{x.id},{Js(x.tipo)},{Js(x.texto)}]")) + "]");
                    sb.Append(",\"log\":[" + string.Join(",", m.log.Select(l => $"[{l.Substring(0, 6).Trim().Replace(',', '.')},{Js(l.Substring(8))}]")) + "]}");
                }
                sb.Append("]}");
                File.WriteAllText($"Logs/bots/replay/partida_{seed}.json", sb.ToString());
                res = $"ok {pt.historico.Count} rounds, {pt.A.nome} {pt.A.placar} x {pt.B.placar} {pt.B.nome}, {sb.Length / 1024} KB";
            }
            catch (Exception e) { res = "ERRO " + e; }
            File.WriteAllText("Logs/bots/replay_cli.txt", res);
            EditorApplication.Exit(0);
        }

        /// <summary>[t, jogadores[[x,y,mx,my,hp,estado,arma]...], smokes[[x,y,r]], fogo[[x,y,r]], flashes[[x,y]], tiros[[x0,y0,x1,y1,acertou]], bomb[x,y,estado]]</summary>
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
            for (int i = 0; i < m.hes.Count; i++) { var (x, y) = P(m.hes[i].p, m.hes[i].z); if (i > 0 || m.flashes.Count > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},1]"); }   // 3º campo 1 = HE
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
            // granadas voando (posição da física: dá pra ver quicar e rolar): [x,y,tipo 0 smoke 1 flash 2 molotov 3 HE]
            sb.Append(",[");
            for (int i = 0; i < m.flying.Count; i++)
            {
                var n = m.flying[i]; var (x, y) = P(n.P, n.z);
                if (i > 0) sb.Append(','); sb.Append($"[{I(x)},{I(y)},{(n.l.kind == "smoke" ? 0 : n.l.kind == "flash" ? 1 : n.l.kind == "molotov" ? 2 : 3)}]");
            }
            sb.Append("]]");
        }

        public static void Round()
        {
            string log = "";
            try
            {
                int seed = int.Parse(Arg("-seed", "1"));
                float fps = 10, every = 1f / fps;   // um quadro a cada 0,1 s de jogo (o vídeo toca a 20 qps = 2x)
                MontaBase();
                var pp = PesosArg();
                Match m;
                int pSeed = int.Parse(Arg("-partida", "-1"));
                if (pSeed >= 0)
                {   // um round de uma partida: joga os anteriores sem vídeo (economia, armas guardadas) e grava este
                    int rk = int.Parse(Arg("-round", "1"));
                    var pt = new Sim.Partida(pSeed, pp, pp);
                    while (pt.r < rk - 1 && !pt.acabou) { var mm = pt.Preparar(); while (!mm.over && mm.t < 200) mm.Step(); pt.Fechar(mm); }
                    m = pt.Preparar(); seed = pSeed * 100 + rk;
                    File.WriteAllText($"Logs/bots/round_{seed}_contexto.txt", string.Join("\n", pt.log) + $"\n\nROUND {rk}: TR {m.compra[0]} x CT {m.compra[1]}\n" +
                        string.Join("\n", m.agents.Select(a => $"  {a.name,-7} {(a.IsT ? "TR" : "CT")} {a.role,-8} {a.gunId,-5} colete {(a.armor > 0 ? (a.helmet ? "+capacete" : "sim") : "não")} {(a.hasKit ? "kit " : "")}granadas: {string.Join(",", a.nades)}")));
                }
                else m = new Match(seed, pp?.Clone(), pp?.Clone());
                string dir = $"Logs/bots/round_{seed}"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
                var tex = new Texture2D(FW, FH, TextureFormat.RGB24, false); var px = new Color32[FW * FH];
                var buf = new C3[FW * FH];
                int f = 0; float next = 0;
                while (true)
                {
                    if (m.t >= next || m.over)
                    {
                        next += every;
                        Draw(m, buf);
                        for (int y = 0; y < FH; y++) for (int x = 0; x < FW; x++) { C3 c = buf[y * FW + x]; px[(FH - 1 - y) * FW + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255); }
                        tex.SetPixels32(px); File.WriteAllBytes($"{dir}/q_{f++:D5}.png", tex.EncodeToPNG());
                        if (m.over) { for (int k = 0; k < 20; k++) File.Copy($"{dir}/q_{f - 1:D5}.png", $"{dir}/q_{f++:D5}.png"); break; }
                    }
                    m.Step();
                    if (m.t > 200) break;
                }
                UnityEngine.Object.DestroyImmediate(tex);
                File.WriteAllText($"Logs/bots/round_{seed}.txt", RoundLog(m));
                log = $"ok {f} quadros, {m.winner} ({m.reason}) em {m.t:0}s\n";
            }
            catch (Exception e) { log = "ERRO " + e; }
            File.WriteAllText("Logs/bots/round_cli.txt", log);
            EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- desenho
        static (float x, float y) P(V2 p, float z) { var (x, y) = Iso.Project(new V3(p.x, p.y, z)); return (x / 2, y / 2); }
        static void Put(C3[] b, int x, int y, C3 c, float a = 1) { if (x < 0 || y < 0 || x >= FW || y >= FH) return; int i = y * FW + x; b[i] = a >= 1 ? c : C3.Lerp(b[i], c, a); }
        static void Disc(C3[] b, float cx, float cy, float r, C3 c, float a = 1) { for (int y = (int)(cy - r); y <= (int)(cy + r); y++) for (int x = (int)(cx - r); x <= (int)(cx + r); x++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Put(b, x, y, c, a); }
        static void Line(C3[] b, float x0, float y0, float x1, float y1, C3 c, float a = 1, int th = 0)
        {
            int n = (int)MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= n; i++) { int x = (int)(x0 + (x1 - x0) * i / n), y = (int)(y0 + (y1 - y0) * i / n); for (int d = -th; d <= th; d++) { Put(b, x + d, y, c, a); Put(b, x, y + d, c, a); } }
        }
        static readonly C3[] tmpTxt = new C3[700 * 12];
        static void Text(C3[] b, int x, int y, string s, C3 c, int sc = 2)
        {
            s = s.ToUpperInvariant(); int w = Math.Min(700, s.Length * 6 + 4);
            var magic = new C3(1, 2, 3);
            for (int i = 0; i < tmpTxt.Length; i++) tmpTxt[i] = magic;
            PixelFont.Draw(tmpTxt, 700, 12, 2, 2, s, c);
            for (int yy = 0; yy < 12; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    var p = tmpTxt[yy * 700 + xx]; if (p.r == 1 && p.g == 2 && p.b == 3) continue;
                    for (int a = 0; a < sc; a++) for (int bb = 0; bb < sc; bb++) Put(b, x + xx * sc + a, y + yy * sc + bb, p);
                }
        }

        static readonly C3 TC = new C3(255, 150, 50), CTC = new C3(90, 170, 255), K = new C3(18, 14, 14);

        static void Draw(Match m, C3[] b)
        {
            for (int y = 0; y < FH; y++) Array.Copy(baseMap, y * MW, b, y * FW, MW);
            for (int y = 0; y < FH; y++) for (int x = MW; x < FW; x++) b[y * FW + x] = new C3(30, 24, 24);
            // smokes, fogo, flash
            foreach (var s in m.smokes) foreach (var pf in s.puffs) { float r = s.PuffR(pf, m.t); if (r < 0.5f) continue; var c = s.PuffC(pf, m.t); var (x, y) = P(new V2(c.x, c.y), c.z); Disc(b, x, y, r, new C3(200, 200, 194), 0.85f); }
            foreach (var fa in m.fires) foreach (var c in fa.cells) if (c.order < fa.Visible) { var (x, y) = P(new V2(c.x * 4 + 2, c.y * 4 + 2), c.z); Disc(b, x, y, 2, new C3(255, 120, 30), 0.8f * fa.Strength); }
            foreach (var fl in m.flashes) { var (x, y) = P(fl.p, fl.z); Disc(b, x, y, 16 * (1 - (m.t - fl.t) / 0.5f) + 2, new C3(255, 255, 230), 0.8f); }
            foreach (var n in m.flying) { var (x, y) = P(n.P, n.z); Disc(b, x, y, 3, n.l.kind == "smoke" ? new C3(230, 230, 230) : n.l.kind == "molotov" ? new C3(255, 120, 30) : n.l.kind == "he" ? new C3(90, 140, 60) : new C3(255, 255, 160)); }
            foreach (var h in m.hes) { var (x, y) = P(h.p, h.z); Disc(b, x, y, 22 * (m.t - h.t) / 0.6f + 4, new C3(255, 170, 60), 0.6f * (1 - (m.t - h.t) / 0.6f)); }
            // caminhos
            foreach (var a in m.agents)
            {
                if (!a.alive || a.brain.path == null) continue;
                var p = a.brain.path; C3 col = a.IsT ? TC : CTC;
                for (int i = 1; i < p.Count; i++) { var (x0, y0) = P(p[i - 1].p, p[i - 1].z); var (x1, y1) = P(p[i].p, p[i].z); Line(b, x0, y0, x1, y1, col, 0.35f); }
            }
            // tiros
            foreach (var s in m.shots) { var (x0, y0) = P(s.a, s.za); var (x1, y1) = P(s.b, s.zb); Line(b, x0, y0, x1, y1, s.hit ? new C3(255, 80, 60) : new C3(255, 240, 160), 0.9f); }
            // bomb
            if (m.bombPlanted) { var (x, y) = P(m.bombPos, m.bombZ); if (((int)(m.t * 4)) % 2 == 0) Disc(b, x, y, 6, new C3(255, 30, 30)); Text(b, (int)x + 8, (int)y - 6, $"{m.BombLeft:0}", new C3(255, 80, 80)); }
            else if (m.bombDropped) { var (x, y) = P(m.droppedBomb, NavGrid.ZAt(m.droppedBomb)); Disc(b, x, y, 4, new C3(255, 220, 40)); }
            // armas no chão
            foreach (var d in m.drops)
            {
                var (x, y) = P(d.pos, d.z);
                Line(b, x - 4, y + 3, x + 4, y + 3, new C3(30, 30, 30), 1, 1); Line(b, x - 3, y + 3, x + 3, y + 3, d.gun == "awp" ? new C3(90, 200, 90) : new C3(200, 200, 200));
                Text(b, (int)x - d.gun.Length * 3, (int)y + 6, d.gun, new C3(220, 220, 200), 1);
            }
            // jogadores
            foreach (var a in m.agents)
            {
                var (x, y) = P(a.pos, a.z); C3 col = a.IsT ? TC : CTC;
                if (!a.alive) { Line(b, x - 5, y - 5, x + 5, y + 5, new C3(120, 110, 110), 1, 1); Line(b, x - 5, y + 5, x + 5, y - 5, new C3(120, 110, 110), 1, 1); continue; }
                var (fx, fy) = P(a.pos + V2.Ang(a.aim) * 22, a.z);
                Line(b, x, y, fx, fy, new C3(255, 255, 255), 0.9f);
                Disc(b, x, y, 7.5f, K); Disc(b, x, y, 6, col);
                if (a.Blind(m.t)) Disc(b, x, y, 4, new C3(255, 255, 255));
                if (a.hasBomb) Disc(b, x + 6, y - 6, 3, new C3(255, 220, 40));
                Text(b, (int)x - a.name.Length * 3, (int)y - 22, a.name, col, 1);
                for (int i = 0; i < 20; i++) Put(b, (int)x - 10 + i, (int)y + 10, i < a.hp / 5 ? new C3(80, 230, 90) : new C3(90, 30, 30));
            }
            // painel
            int px = MW + 12, py = 10;
            Text(b, px, py, $"{(int)(m.RoundLeft / 60)}:{(int)Math.Max(0, m.RoundLeft % 60):00}  TR {m.Alive(Team.T)} x {m.Alive(Team.CT)} CT", new C3(255, 255, 255)); py += 24;
            if (m.over) { Text(b, px, py, $"{m.winner} GANHOU: {m.reason}", new C3(255, 230, 80)); py += 22; }
            Text(b, px, py, $"TR: {m.igl[0].plan} ({m.igl[0].stage})", TC); py += 20;
            Text(b, px, py, $"CT: {m.igl[1].plan} ({m.igl[1].stage})", CTC); py += 24;
            foreach (int tm in new[] { 0, 1 })
            {
                var cl = m.igl[tm].calls; for (int i = Math.Max(0, cl.Count - 2); i < cl.Count; i++) { Text(b, px, py, Cut($"IGL: {cl[i].s}", 50), tm == 0 ? TC : CTC); py += 18; }
            }
            py += 6;
            foreach (var (t, s) in m.feed.Skip(Math.Max(0, m.feed.Count - 4))) { Text(b, px, py, Cut(s, 50), new C3(240, 240, 240)); py += 18; }
            py += 8;
            foreach (var a in m.agents)
            {
                C3 col = a.IsT ? TC : CTC;
                Text(b, px, py, $"{a.name} {a.role} {a.gunId} {(a.alive ? a.hp + "HP" : "MORTO")}", a.alive ? col : new C3(110, 100, 100)); py += 16;
                Text(b, px + 12, py, Cut(a.brain.thought, 49), a.alive ? new C3(225, 220, 210) : new C3(100, 95, 95)); py += 19;
            }
            // mapas de calor: o que cada time acha que existe de inimigo
            HeatMini(m, b, Team.T, MW + 10, FH - 222, "TR ACHA QUE CT ESTÁ:");
            HeatMini(m, b, Team.CT, MW + 330, FH - 222, "CT ACHA QUE TR ESTÁ:");
        }

        static string Cut(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        static void HeatMini(Match m, C3[] b, Team team, int ox, int oy, string title)
        {
            const float k = 300f / Iso.BW;
            Text(b, ox, oy - 14, title, team == Team.T ? TC : CTC, 1);
            for (int y = 0; y < 210; y++) for (int x = 0; x < 300; x++) { int sx = (int)(x / k / 2), sy = (int)(y / k / 2); if (sx < MW && sy < MH) Put(b, ox + x, oy + y, baseMap[sy * MW + sx] * 0.6f); }
            var it = m.intel[(int)team];
            for (int i = 0; i < it.heat.Length; i++)
            {
                if (!Intel.Pass(i) || it.heat[i] < 0.05f) continue;
                var c = Intel.CenterOf(i); var (x, y) = Iso.Project(new V3(c.x, c.y, Intel.Z(i)));
                int cx = ox + (int)(x * k), cy = oy + (int)(y * k);
                C3 hc = team == Team.T ? new C3(255, 60, 60) : new C3(255, 160, 40);
                for (int a = -1; a <= 1; a++) for (int bb = -1; bb <= 1; bb++) if (cx + a >= ox && cy + bb >= oy && cx + a < ox + 300 && cy + bb < oy + 210) Put(b, cx + a, cy + bb, hc, MathF.Min(0.9f, it.heat[i]));
            }
            foreach (var a in m.agents) if (a.alive && a.team == team) { var (x, y) = Iso.Project(new V3(a.pos.x, a.pos.y, a.z)); Disc(b, ox + x * k, oy + y * k, 2.5f, team == Team.T ? TC : CTC); }
        }
    }
}
