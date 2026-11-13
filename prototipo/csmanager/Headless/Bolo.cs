using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Medidor de "todo mundo junto" em PARTIDA inteira (Headless -bolo N [-exp ...] [-saida nome]): o Gabriel filmou os CT
    /// amontoados na escada do janelão e dois no mesmo ponto. Em round isolado de compra cheia isso quase não aparece; aqui
    /// roda a partida (economia, leitura do IGL, placar) e conta, por compra do round: grupos de 3+ do time parados num raio
    /// de ~9 m por 4 s+ (BOLO), pares parados a menos de 3 m (PAR) e se olham pro mesmo lado. Só lê o estado.
    /// </summary>
    static class MedeBolo
    {
        static string Corta(string s) { if (s == null) return "?"; int p = s.IndexOf('('); s = (p > 0 ? s.Substring(0, p) : s).Trim(); p = s.IndexOf(':'); return p > 0 ? s.Substring(0, p) : s; }

        public static int Rodar(int n, int seed0, string saida)
        {
            var cont = new Dictionary<string, double>(); var ex = new Dictionary<string, List<string>>(); var trava = new object(); int rounds = 0;
            void Soma(string k, double v) { lock (trava) { cont.TryGetValue(k, out double x); cont[k] = x + v; } }
            void Ex(string k, string s) { lock (trava) { if (!ex.TryGetValue(k, out var l)) ex[k] = l = new List<string>(); if (l.Count < 12) l.Add(s); } }
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var p = new Partida(seed0 + i); Match atual = null; int tick = 0;
                var boloT = new float[2]; var boloM = new bool[2]; var parT = new float[100]; var parM = new bool[100];
                var pend = new List<(string k, double v)>(); var pendEx = new List<(string k, string s)>();
                p.cadaPasso = m =>
                {
                    if (m != atual) { atual = m; tick = 0; Array.Clear(boloT, 0, 2); Array.Clear(boloM, 0, 2); Array.Clear(parT, 0, 100); Array.Clear(parM, 0, 100); }
                    if (++tick % 8 != 0 || m.t < 12) return;
                    if (!m.bombPlanted && m.t >= 25 && m.t < 25 + Match.DT * 8 && m.agents.Count(a => a.alive && !a.IsT) == 5)
                    {   // FOTO aos 25 s com os 5 CT vivos: quantos por lado (caminho mais curto até cada bomb decide o lado; meio = longe dos dois)
                        int nA = 0, nB = 0, nM = 0;
                        foreach (var a in m.agents) if (!a.IsT)
                        {
                            float dA = (Places.Plant('A') - a.pos).Len, dB = (Places.Plant('B') - a.pos).Len;
                            if (dA < 330 && dA < dB) nA++; else if (dB < 330) nB++; else nM++;
                        }
                        pend.Add(($"FOTO CT aos 25 s: {nA} no A · {nM} no meio/longe · {nB} no B", 1));
                    }
                    float dt = Match.DT * 8;
                    foreach (bool ladoT in new[] { false, true })
                    {
                        string ld = ladoT ? "TR" : "CT"; int gi = ladoT ? 1 : 0;
                        var ps = m.agents.Where(a => a.alive && a.IsT == ladoT && a.Speed < 8 && a.brain.AlvoAgora == null).ToList();
                        List<Agent> g = null;
                        foreach (var c in ps) { var perto = ps.Where(o => (o.pos - c.pos).Len < 125).ToList(); if (perto.Count >= 3 && (g == null || perto.Count > g.Count)) g = perto; }
                        if (g == null) { boloT[gi] = 0; boloM[gi] = false; }
                        else
                        {
                            boloT[gi] += dt;
                            if (boloT[gi] >= 4f) pend.Add(("BOLO segundos " + ld, dt));
                            if (boloT[gi] >= 4f && !boloM[gi])
                            {
                                boloM[gi] = true;
                                float am = g.Average(a => MathF.Cos(a.aim)), bm = g.Average(a => MathF.Sin(a.aim));
                                bool mesmo = MathF.Sqrt(am * am + bm * bm) > 0.85f;
                                string quando = m.bombPlanted ? "C4 plantado" : m.t < 30 ? "até 30 s" : "30 s+, sem plant";
                                pend.Add(("BOLO episódios " + ld, 1)); pend.Add(("BOLO " + ld + " · " + quando, 1));
                                pend.Add(("BOLO " + ld + " olhar: " + (mesmo ? "todos pro MESMO lado" : "lados diferentes"), 1));
                                pend.Add(("BOLO " + ld + " onde: " + Places.Region(g[0].pos), 1));
                                pend.Add(("BOLO " + ld + " ordens: " + string.Join(" + ", g.Select(a => Corta(a.brain.order.name)).OrderBy(s => s, StringComparer.Ordinal)), 1));
                                pend.Add(("BOLO " + ld + " fases: " + string.Join(" + ", g.Select(a => a.brain.fase).OrderBy(s => s, StringComparer.Ordinal)), 1));
                                pendEx.Add(("BOLO " + ld, $"partida {seed0 + i} round {p.r + 1} t={m.t:0.0} · {Places.Region(g[0].pos)} · {(mesmo ? "mesmo lado" : "lados diferentes")} · " + string.Join(" | ", g.Select(a => $"{a.name} ({a.gunId}) '{a.brain.fase}' ordem '{a.brain.order.name}' — {a.brain.thought}"))));
                            }
                        }
                    }
                    foreach (bool ladoT in new[] { false, true })
                    {   // LARGO: 3+ vivos do time a menos de ~14 m uns dos outros, andando ou não (parte do tempo de round), e quantos miram o mesmo ponto
                        if (m.bombPlanted) break;
                        string ld = ladoT ? "TR" : "CT"; var vs = m.agents.Where(a => a.alive && a.IsT == ladoT).ToList(); if (vs.Count < 3) continue;
                        int maior = 0; Agent centro = null;
                        foreach (var c in vs) { int q = vs.Count(o => (o.pos - c.pos).Len < 200); if (q > maior) { maior = q; centro = c; } }
                        string fx = m.t < 30 ? "a 12-30 s" : m.t < 60 ? "b 30-60 s" : "c 60 s+";
                        pend.Add(($"LARGO tempo {ld} {fx} (s)", dt));
                        if (maior >= 3) { pend.Add(($"LARGO {ld} {fx}: {maior} juntos a <14 m (s)", dt)); pend.Add(($"LARGO {ld} onde (s): {Places.Region(centro.pos)}", dt)); }
                        var porReg = vs.GroupBy(a => Places.Region(a.pos)).OrderByDescending(gr => gr.Count()).First();
                        pend.Add(($"REGIAO {ld} {fx}: maior grupo na mesma região (soma p/ média)", porReg.Count() * dt));
                        if (porReg.Count() >= 3) { pend.Add(($"REGIAO {ld} {fx}: 3+ na mesma região (s)", dt)); pend.Add(($"REGIAO {ld} 3+ onde (s): {porReg.Key} · ordens {string.Join(" + ", porReg.Select(a => Corta(a.brain.order.name)).OrderBy(s => s, StringComparer.Ordinal))}", dt)); }
                        int mesma = 0;
                        foreach (var c in vs) { V2 pc = c.pos + V2.Ang(c.aim) * 300; int q = vs.Count(o => MathF.Abs(MathX.AngDiff((pc - o.pos).Atan2, o.aim)) < 0.26f && (o.pos - c.pos).Len < 350); if (q > mesma) mesma = q; }
                        if (mesma >= 3) pend.Add(($"LARGO {ld} {fx}: {mesma} mirando o MESMO ponto (s)", dt));
                    }
                    for (int x = 0; x < m.agents.Count; x++) for (int y = x + 1; y < m.agents.Count; y++)
                    {
                        var A = m.agents[x]; var B = m.agents[y]; int k = x * 10 + y;
                        bool junto = A.alive && B.alive && A.team == B.team && A.Speed < 8 && B.Speed < 8 && (A.pos - B.pos).Len < 42 && A.brain.AlvoAgora == null && B.brain.AlvoAgora == null;
                        if (!junto) { parT[k] = 0; parM[k] = false; continue; }
                        parT[k] += dt;
                        if (parT[k] >= 4f && !parM[k])
                        {
                            parM[k] = true; string ld = A.IsT ? "TR" : "CT"; bool mesmo = MathF.Abs(MathX.AngDiff(A.aim, B.aim)) < 0.35f;
                            pend.Add(("PAR episódios " + ld, 1)); pend.Add(("PAR " + ld + " olhar: " + (mesmo ? "MESMO lado" : "lados diferentes"), 1));
                            pend.Add(("PAR " + ld + " onde: " + Places.Region(A.pos), 1));
                            string oa = Corta(A.brain.order.name), ob = Corta(B.brain.order.name); if (string.CompareOrdinal(oa, ob) > 0) (oa, ob) = (ob, oa);
                            pend.Add(("PAR " + ld + " ordens: " + oa + " + " + ob, 1));
                            pendEx.Add(("PAR " + ld, $"partida {seed0 + i} round {p.r + 1} t={m.t:0.0} · {Places.Region(A.pos)} a {(A.pos - B.pos).Len * 2.9f / 40:0.0} m · {(mesmo ? "mesmo lado" : "lados diferentes")} · {A.name} ({A.gunId}) '{A.brain.fase}' ordem '{A.brain.order.name}' — {A.brain.thought} | {B.name} ({B.gunId}) '{B.brain.fase}' ordem '{B.brain.order.name}' — {B.brain.thought}"));
                        }
                    }
                };
                p.Jogar(m =>
                {
                    var h = p.historico[p.historico.Count - 1];
                    lock (trava) rounds++;
                    Soma("ROUNDS compra CT " + h.compraCT, 1); Soma("ROUNDS compra TR " + h.compraT, 1);
                    foreach (var (k, v) in pend)
                    {
                        Soma(k, v);
                        if (k.StartsWith("BOLO episódios CT") || k.StartsWith("PAR episódios CT")) Soma(k + " · compra CT " + h.compraCT, v);
                        if (k.StartsWith("BOLO episódios TR") || k.StartsWith("PAR episódios TR")) Soma(k + " · compra TR " + h.compraT, v);
                    }
                    foreach (var (k, s) in pendEx) Ex(k, s + " · compra CT " + h.compraCT + " / TR " + h.compraT);
                    pend.Clear(); pendEx.Clear();
                });
            });
            var sb = new StringBuilder();
            sb.AppendLine($"BOLO — {n} partidas ({rounds} rounds), sementes {seed0}..{seed0 + n - 1}, exp \"{TeamBrain.EXP}\"");
            foreach (var kv in cont.OrderBy(k => k.Key.Split(' ')[0], StringComparer.Ordinal).ThenByDescending(k => k.Value))
                if (kv.Value >= 3 || kv.Key.Contains("episódios") || kv.Key.StartsWith("ROUNDS")) sb.AppendLine($"  {kv.Value,8:0.0}  ({100.0 * kv.Value / Math.Max(1, rounds),5:0.0}/100 rounds)  {kv.Key}");
            foreach (var kv in ex.OrderBy(k => k.Key)) { sb.AppendLine(); sb.AppendLine("EXEMPLOS — " + kv.Key); foreach (var s in kv.Value) sb.AppendLine("  " + s); }
            System.IO.Directory.CreateDirectory("Logs/bots"); System.IO.File.WriteAllText($"Logs/bots/bolo_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
