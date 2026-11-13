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
    /// Medidor de jogador sem função (Headless -ocioso N [-exp ...] [-saida nome]): o Gabriel viu o ROCHA com o kit parado no
    /// meio enquanto o time ganhava o retake e ninguém desarmava. Mede, com o C4 plantado, quanto tempo cada CT vivo fica
    /// PARADO LONGE do bomb (e com que ordem/fase), e os rounds em que a bomba explodiu com CT vivo e nenhum TR vivo a
    /// tempo de desarmar. Só lê o estado (oráculo). Grava Logs/bots/ocioso_&lt;saida&gt;.txt.
    /// </summary>
    static class MedeOcioso
    {
        public static int Rodar(int n, int seed0, string saida)
        {
            var cont = new Dictionary<string, double>(); var ex = new Dictionary<string, List<string>>(); var trava = new object(); int plantados = 0, explodiuLimpo = 0, explodiuCtVivo = 0;
            void Soma(string k, double v) { lock (trava) { cont.TryGetValue(k, out double x); cont[k] = x + v; } }
            void Ex(string k, string s) { lock (trava) { if (!ex.TryGetValue(k, out var l)) ex[k] = l = new List<string>(); if (l.Count < 10) l.Add(s); } }
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); int seed = seed0 + i;
                var parado = new float[10]; float tSemTR = -1; bool contou = false; var marcado = new bool[10]; string ultimos = null; int ultN = 0;
var boloT = new float[2]; var boloM = new bool[2]; var juntoT = new float[100]; var juntoM = new bool[100];
                bool fugaMedida = false; var entrou = new float[10]; for (int q = 0; q < 10; q++) entrou[q] = -1; int ctNoPlant = 0, trNoPlant = 0; float tPlant = -1;
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    if (!m.bombPlanted && m.t > 8 && ((int)(m.t / Match.DT)) % 4 == 0)
                    {   // JUNTOS: dois colegas parados a menos de ~3 m um do outro (a foto do Gabriel: RADAR e FALCAO no mesmo ponto do janelão)
                        for (int x = 0; x < m.agents.Count; x++) for (int y = x + 1; y < m.agents.Count; y++)
                        {
                            var A = m.agents[x]; var B = m.agents[y]; int k = x * 10 + y;
                            bool junto = A.alive && B.alive && A.team == B.team && A.Speed < 8 && B.Speed < 8 && (A.pos - B.pos).Len < 42 && A.brain.AlvoAgora == null && B.brain.AlvoAgora == null;
                            if (!junto) { juntoT[k] = 0; juntoM[k] = false; continue; }
                            juntoT[k] += Match.DT * 4;
                            if (juntoT[k] >= 4f)
                            {
                                bool mesmaMira = MathF.Abs(MathX.AngDiff(A.aim, B.aim)) < 0.35f;
                                string lado = A.IsT ? "TR" : "CT";
                                Soma($"JUNTOS parados (s) {lado}: " + (mesmaMira ? "olhando o MESMO lado" : "olhando lados diferentes"), Match.DT * 4);
                                if (!juntoM[k])
                                {
                                    juntoM[k] = true; Soma($"JUNTOS episódios {lado} (2 parados a <3 m por 4 s+)", 1);
                                    string fa = A.brain.fase, fb = B.brain.fase; if (string.CompareOrdinal(fa, fb) > 0) (fa, fb) = (fb, fa);
                                    Soma($"JUNTOS {lado} fases: '{fa}' + '{fb}'{(mesmaMira ? " · mesmo lado" : "")}", 1);
                                    Soma($"JUNTOS {lado} onde: {Places.Region(A.pos)}", 1);
                                    Ex($"JUNTOS {lado}", $"semente {seed} t={m.t:0.0} · {A.name} ({A.gunId}) e {B.name} ({B.gunId}) em {Places.Region(A.pos)} a {(A.pos - B.pos).Len * 2.9f / 40:0.0} m · {(mesmaMira ? "mesmo lado" : "lados diferentes")} · '{A.brain.fase}' ordem '{Corta(A.brain.order.name)}' / '{B.brain.fase}' ordem '{Corta(B.brain.order.name)}' · {A.brain.thought} / {B.brain.thought}");
                                }
                            }
                        }
                    }
                    if (m.t > 12 && ((int)(m.t / Match.DT)) % 8 == 0)
                    {   // BOLO: 3+ do mesmo time parados num raio de ~9 m (a escada do janelão do vídeo), por time e por ordem
                        foreach (bool ladoT in new[] { false, true })
                        {
                            var ps = m.agents.Where(a => a.alive && a.IsT == ladoT && a.Speed < 8 && a.brain.AlvoAgora == null).ToList(); int gi = ladoT ? 1 : 0;
                            List<Agent> g = null;
                            foreach (var c in ps) { var perto = ps.Where(o => (o.pos - c.pos).Len < 125).ToList(); if (perto.Count >= 3 && (g == null || perto.Count > g.Count)) g = perto; }
                            if (g == null) { boloT[gi] = 0; boloM[gi] = false; continue; }
                            boloT[gi] += Match.DT * 8;
                            string sit = (m.bombPlanted ? "C4 plantado" : "antes do plant") + (m.t < 30 ? " · até 30 s" : "");
                            if (boloT[gi] >= 4f) Soma($"BOLO (s) {(ladoT ? "TR" : "CT")} · {sit}", Match.DT * 8);
                            if (boloT[gi] >= 4f && !boloM[gi])
                            {
                                boloM[gi] = true; string ld = ladoT ? "TR" : "CT";
                                Soma($"BOLO episódios {ld} · {sit} · {g.Count} juntos", 1);
                                Soma($"BOLO {ld} onde: {Places.Region(g[0].pos)}", 1);
                                Soma($"BOLO {ld} ordens: {string.Join(" + ", g.Select(a => Corta(a.brain.order.name).Split(':')[0]).OrderBy(s => s, StringComparer.Ordinal))}", 1);
                                float am = g.Average(a => MathF.Cos(a.aim)), bm = g.Average(a => MathF.Sin(a.aim));
                                Soma($"BOLO {ld} olhar: {(MathF.Sqrt(am * am + bm * bm) > 0.85f ? "todos pro MESMO lado" : "lados diferentes")}", 1);
                                Ex($"BOLO {ld}", $"semente {seed} t={m.t:0.0} · {sit} · {Places.Region(g[0].pos)} · " + string.Join(" | ", g.Select(a => $"{a.name} '{a.brain.fase}' ordem '{a.brain.order.name}'")));
                            }
                        }
                    }
                    if (!m.bombPlanted) continue;
                    if (!contou) { contou = true; lock (trava) plantados++; tPlant = m.t; ctNoPlant = m.agents.Count(a => a.alive && !a.IsT); trNoPlant = m.agents.Count(a => a.alive && a.IsT);
                        foreach (var a in m.agents) if (!a.IsT && a.alive && (m.bombPos - a.pos).Len < 140) entrou[a.id] = -2;   /* já estava no bomb no plant: não é "entrada" */ }
                    // ENTRADA no retake: 1ª vez que cada CT chega a menos de ~17 m do C4 (ou morre tentando: conta onde caiu)
                    foreach (var a in m.agents) if (!a.IsT && entrou[a.id] == -1 && a.alive && (m.bombPos - a.pos).Len < 140)
                    {
                        entrou[a.id] = m.t; int ja = entrou.Count(x => x >= 0);
                        if (ja <= 2 && ctNoPlant >= 2) Soma($"ENTRADA: o {ja}º a chegar estava com a ordem '{Corta(a.brain.order.name)}' · fase '{a.brain.fase}'", 1);
                    }
                    int trVivos = m.agents.Count(a => a.alive && a.IsT);
                    if (m.BombLeft < 0.5f && !m.over && !fugaMedida)
                    {   // FUGA: no fim, quantos fogem e quantos pares de colegas fugindo estão colados (o "mesmo cantinho")
                        fugaMedida = true;
                        var gd = m.agents.Where(a => a.alive && a.guardando).ToList(); int pg = 0;
                        for (int x = 0; x < gd.Count; x++) for (int y = x + 1; y < gd.Count; y++) if (gd[x].team == gd[y].team && (gd[x].pos - gd[y].pos).Len < 70) pg++;
                        if (gd.Count >= 2) { Soma("FUGA guardando a arma: rounds com 2+ guardando", 1); Soma("FUGA guardando a arma: pares a menos de 5 m um do outro", pg); if (pg > 0) Soma("FUGA guardando a arma: rounds com colegas colados", 1); }
                        var fg = m.agents.Where(a => a.alive && a.brain.fase == "saindo do bomb").ToList(); int pares = 0;
                        for (int x = 0; x < fg.Count; x++) for (int y = x + 1; y < fg.Count; y++) if (fg[x].team == fg[y].team && (fg[x].pos - fg[y].pos).Len < 55) pares++;
                        if (fg.Count >= 2) { Soma("FUGA da bomba: rounds com 2+ fugindo", 1); Soma("FUGA da bomba: pares de colegas a menos de 4 m um do outro", pares); if (pares > 0) Soma("FUGA da bomba: rounds com colegas colados", 1); }
                    }
                    if (m.BombLeft < 1.2f && !m.over)
                    {   // retrato do último segundo antes de explodir (a explosão mata quem está perto)
                        var vv = m.agents.Where(a => a.alive && !a.IsT).ToList(); ultN = vv.Count;
                        ultimos = string.Join(" | ", vv.Select(a => $"{a.name}{(a.hasKit ? " (kit)" : "")} em {Places.Region(a.pos)} a {(m.bombPos - a.pos).Len * 2.9f / 40:0} m, fase '{a.brain.fase}', ordem '{a.brain.order.name}'"));
                    }
                    if (trVivos == 0 && tSemTR < 0) tSemTR = m.t;
                    foreach (var a in m.agents)
                    {
                        if (!a.alive || a.IsT) { parado[a.id] = 0; continue; }
                        float d = (m.bombPos - a.pos).Len; var b = a.brain;
                        bool ocioso = d > 450 && a.Speed < 8 && b.AlvoAgora == null && b.action == null;
                        if (!ocioso) { parado[a.id] = 0; marcado[a.id] = false; continue; }
                        parado[a.id] += Match.DT;
                        if (parado[a.id] >= 3f)
                        {
                            string k = $"{(a.hasKit ? "COM kit" : "sem kit")} · {(trVivos == 0 ? "nenhum TR vivo" : trVivos + " TR vivo(s)")} · fase '{b.fase}' · ordem '{Corta(b.order.name)}'";
                            Soma("CT parado longe do bomb plantado (s): " + k, Match.DT);
                            if (!marcado[a.id]) { marcado[a.id] = true; Soma("EPISÓDIOS de CT parado 3 s+ longe do bomb plantado", 1); Ex("CT parado 3 s+ longe do bomb plantado", $"semente {seed} t={m.t:0.0} · {a.name} ({a.gunId}{(a.hasKit ? ", kit" : "")}) em {Places.Region(a.pos)} a {d * 2.9f / 40:0} m do bomb · {trVivos} TR vivo(s), bomba em {m.BombLeft:0.0} s · fase '{b.fase}' · ordem '{b.order.name}' · {b.thought}"); }
                        }
                    }
                }
                if (tPlant >= 0 && ctNoPlant >= 2)
                {
                    var ts = entrou.Where(x => x >= 0).OrderBy(x => x).ToList();
                    if (entrou.Any(x => x == -2)) Soma($"RETAKE ({sit0(ctNoPlant, trNoPlant)}): tinha CT dentro do bomb na hora do plant", 1);
                    string sit = ctNoPlant > trNoPlant ? "CT em vantagem" : ctNoPlant == trNoPlant ? "igual" : "CT em desvantagem";
                    string ganhou = m.winner == "CT" ? "CT ganhou" : "TR ganhou";
                    if (ts.Count == 0) Soma($"RETAKE ({sit}): ninguém chegou perto do bomb · {ganhou}", 1);
                    else if (ts.Count == 1) Soma($"RETAKE ({sit}): só UM chegou perto do bomb · {ganhou}", 1);
                    else
                    {
                        float gap = ts[1] - ts[0];
                        Soma($"RETAKE ({sit}): 2º chegou {(gap < 1 ? "a até 1 s do 1º" : gap < 3 ? "b 1-3 s depois" : "c 3 s+ depois")} · {ganhou}", 1);
                    }
                }
                if (m.reason != null && m.reason.StartsWith("bomb explod") && ultN > 0)
                {
                    lock (trava) explodiuCtVivo++;
                    if (tSemTR >= 0)
                    {
                        lock (trava) explodiuLimpo++;
                        Soma("bomba explodiu sem TR vivo: segundos entre o último TR morrer e a explosão: " + (m.t - tSemTR < 5 ? "a <5 s (não dava sem kit)" : m.t - tSemTR < 10 ? "b 5-10 s (só com kit)" : "c 10 s+ (dava de qualquer jeito)"), 1);
                        Ex("bomba explodiu com CT vivo e NENHUM TR vivo", $"semente {seed} · sem TR desde t={tSemTR:0.0}, fim t={m.t:0.0} ({m.t - tSemTR:0.0} s) · {ultimos}");
                    }
                }
            });
            static string sit0(int c, int tr) => c > tr ? "CT em vantagem" : c == tr ? "igual" : "CT em desvantagem";
            static string Corta(string s) { if (s == null) return "?"; int p = s.IndexOf('('); return (p > 0 ? s.Substring(0, p) : s).Trim(); }
            var sb = new StringBuilder();
            sb.AppendLine($"OCIOSO — {n} rounds, sementes {seed0}..{seed0 + n - 1}, exp \"{TeamBrain.EXP}\"");
            sb.AppendLine($"rounds com plant: {plantados} · bomba explodiu com CT vivo: {explodiuCtVivo} · e sem nenhum TR vivo antes de explodir: {explodiuLimpo}");
            foreach (var kv in cont.Where(k => k.Key.StartsWith("RETAKE")).OrderBy(k => k.Key, StringComparer.Ordinal)) sb.AppendLine($"  {kv.Value,8:0}  {kv.Key}");
            foreach (var kv in cont.Where(k => k.Key.StartsWith("ENTRADA")).OrderByDescending(k => k.Value).Take(24)) sb.AppendLine($"  {kv.Value,8:0}  {kv.Key}");
            foreach (var kv in cont.Where(k => k.Key.StartsWith("FUGA")).OrderBy(k => k.Key, StringComparer.Ordinal)) sb.AppendLine($"  {kv.Value,8:0}  {kv.Key}");
            foreach (var kv in cont.Where(k => k.Key.StartsWith("JUNTOS") || k.Key.StartsWith("BOLO")).OrderByDescending(k => k.Value).Take(110)) sb.AppendLine($"  {kv.Value,8:0.0}  {kv.Key}");
            foreach (var kv in cont.Where(k => !k.Key.StartsWith("RETAKE") && !k.Key.StartsWith("ENTRADA") && !k.Key.StartsWith("FUGA") && !k.Key.StartsWith("JUNTOS") && !k.Key.StartsWith("BOLO")).OrderByDescending(k => k.Value).Take(24)) sb.AppendLine($"  {kv.Value,8:0.0}  {kv.Key}");
            foreach (var kv in ex) { sb.AppendLine(); sb.AppendLine("EXEMPLOS — " + kv.Key); foreach (var s in kv.Value) sb.AppendLine("  " + s); }
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/ocioso_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
