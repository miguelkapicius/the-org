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
    /// Diagnóstico da cena "C4 no bomb sem plantar" do -humano (sim-apoio, 2026-11-08): mesmo detector (portador a menos de
    /// 150 do centro de um bomb, sem plantar, sem alvo, ninguém vendo, ≥3 s), mas conta CADA episódio pelo estado do portador:
    /// ordem, se a ordem é de plantar, bomb do plano x bomb onde está, distância até o ponto de plant, fase, se anda.
    /// Uso: Headless -diagC4 N -seed S [-exp ...] [-saida nome] → Logs/bots/diagc4_&lt;saida&gt;.txt
    /// </summary>
    static class DiagC4
    {
        static bool Ve(Agent a, Agent b) => MapData.LosWalk(a.pos, a.z + a.Eye - 18f, b.pos, b.z + b.Eye - 18f, 18f);

        public static int Rodar(int n, int seed0, string saida, int hilos)
        {
            var cont = new Dictionary<string, Dictionary<string, int>>(); int eps = 0, rounds = 0; var ex = new List<string>();
            void Soma(string sec, string k) { if (!cont.TryGetValue(sec, out var d)) cont[sec] = d = new Dictionary<string, int>(); d.TryGetValue(k, out int v); d[k] = v + 1; }
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = hilos }, i =>
            {
                var m = new Match(seed0 + i); var desde = new float[10]; var marcado = new bool[10]; Array.Fill(desde, -1);
                int passo = 0;
                while (!m.over && m.t < 200)
                {
                    m.Step(); if (++passo % 8 != 0) continue;
                    foreach (var a in m.agents)
                    {
                        int id = a.id;
                        bool cond = false; string siteEm = "-";
                        if (a.alive && a.hasBomb && !m.bombPlanted)
                            foreach (var kv in MapData.SITES) if ((kv.Value.Center - a.pos).Len < 150) { cond = true; siteEm = kv.Key; }
                        var b = a.brain;
                        if (cond) cond = m.plantando != a && b.target == null && !m.agents.Any(e => e.alive && e.team != a.team && (e.pos - a.pos).Len < 700 && Ve(e, a));
                        if (!cond) { desde[id] = -1; marcado[id] = false; continue; }
                        if (desde[id] < 0) desde[id] = m.t;
                        if (marcado[id] || m.t - desde[id] < 3f) continue;
                        marcado[id] = true;
                        var o = b.order; float dps = (m.PlantSpot - a.pos).Len;
                        bool dentroPlano = MapData.SITES[m.planSite.ToString()].Contains(a.pos.x, a.pos.y), dentroEm = MapData.SITES[siteEm].Contains(a.pos.x, a.pos.y);
                        int meus = m.Alive(a.team), deles = m.Alive(a.IsT ? Team.CT : Team.T);
                        lock (cont)
                        {
                            eps++;
                            Soma("ordem de plantar?", o != null && o.plant ? "sim" : "não");
                            Soma("bomb onde está = bomb do plano?", siteEm == m.planSite.ToString() ? "sim" : "não (está no outro)");
                            Soma("dentro da área de plantar (SITES)?", dentroEm ? "sim" : "não (a <150 do centro, fora do retângulo)");
                            Soma("dist até o ponto de plant", dps < 10 ? "<10 (no ponto)" : dps < 60 ? "10-60" : dps < 150 ? "60-150" : dps < 400 ? "150-400" : "400+ (outro bomb)");
                            Soma("anda?", a.Speed > 8 ? "andando" : "parado");
                            float pl = NavGrid.PathLen(a.pos, m.PlantSpot);
                            Soma("caminho até o ponto ÷ reta", float.IsInfinity(pl) ? "sem caminho" : pl < dps * 1.3f + 10 ? "direto (<1,3x)" : pl < dps * 2.5f + 10 ? "volta média" : "volta grande (2,5x+)");
                            float dCol = m.agents.Where(x => x != a && x.alive && x.team == a.team).Select(x => (x.pos - m.PlantSpot).Len).DefaultIfEmpty(9999).Min();
                            Soma("colega mais perto do ponto de plant", dCol < 12 ? "<12 (em cima do ponto)" : dCol < 25 ? "12-25" : dCol < 60 ? "25-60" : "60+");
                            Soma("velocidade real x pedida", a.Speed > 40 ? "corre" : a.Speed > 8 ? "anda" : "parado");
                            Soma("ponto de plant andável?", NavGrid.Movable(m.PlantSpot) ? "sim" : $"não (snap a {(NavGrid.Snap(m.PlantSpot) - m.PlantSpot).Len:0})");
                            Soma("ordem", o?.name ?? "-");
                            Soma("fase", b.fase ?? "-");
                            Soma("ação", b.action ?? "-");
                            Soma("vivos nós x eles", $"{meus}x{deles}");
                            Soma("hora", m.t < 40 ? "<40" : m.t < 70 ? "40-70" : m.t < 100 ? "70-100" : "100+");
                            Soma("pensava (começo)", (b.thought ?? "-").Split('—', ':')[0].Trim());
                            if (ex.Count < 30) ex.Add($"seed {seed0 + i} {m.t:0.0}s {a.name} em {Places.Region(a.pos)} (site {siteEm}, plano {m.planSite}, dentro {dentroEm}) · ordem '{o?.name}' plant={o?.plant} · ps a {dps:0} (caminho {NavGrid.PathLen(a.pos, m.PlantSpot):0}, pos {a.pos.x:0},{a.pos.y:0}) · fase '{b.fase}' · ação {b.action ?? "-"} · colega no ponto a {m.agents.Where(x => x != a && x.alive && x.team == a.team).Select(x => (x.pos - m.PlantSpot).Len).DefaultIfEmpty(9999).Min():0} · {a.Speed:0} u/s · pensa: {b.thought}");
                        }
                    }
                }
                lock (cont) rounds++;
            });
            var sb = new StringBuilder();
            foreach (char st in "AB") { V2 q = Places.Plant(st); sb.AppendLine($"ponto de plant {st}: {q.x:0},{q.y:0} andável {NavGrid.Movable(q)} snap a {(NavGrid.Snap(q) - q).Len:0}"); }
            sb.AppendLine($"C4 no bomb sem plantar — {eps} episódios em {rounds} rounds = {100.0 * eps / Math.Max(1, rounds):0.0} por 100 rounds (exp: {TeamBrain.EXP})");
            foreach (var kv in cont)
            {
                sb.AppendLine($"  {kv.Key}");
                foreach (var x in kv.Value.OrderByDescending(q => q.Value).Take(14)) sb.AppendLine($"     {x.Key,-60} {100.0 * x.Value / Math.Max(1, eps),5:0.0}%  n={x.Value}");
            }
            sb.AppendLine("  exemplos"); foreach (var e in ex) sb.AppendLine("     " + e);
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/diagc4_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
