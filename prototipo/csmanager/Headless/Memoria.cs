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
    /// Crença, memória e som (sim-apoio, 2026-11-08; pedido do Gabriel: "memória curta, não desconfiam de área em que o colega
    /// morreu, não reagem a todos os sons"). Só mede:
    ///  A) quem matou: o time da vítima tinha visto o matador? há quanto tempo, perto de onde ele matou, e alguém olhou o lugar depois;
    ///  B) quanto tempo um avistamento continua verdade: depois que o contato para de ser atualizado, quanto tempo o inimigo real fica
    ///     a ≤200 do último ponto visto (o cérebro usa cortes fixos de 3-8 s);
    ///  C) sons: quantos de cada tipo por round, quantos chegam num ouvido inimigo e quantos num ouvido amigo (hoje o amigo não ouve).
    /// Uso: Headless -memoria 200 -seed S [-exp ...] [-saida nome] → Logs/bots/memoria_&lt;saida&gt;.txt
    /// </summary>
    static class Memoria
    {
        static float Alcance(string k) => k switch
        {
            "tiro" => 99999, "plantando" => 99999, "desarmando" => 900, "passos" => 280, "granada" => 260, _ => Match.AlcanceSom(k)
        };
        class Ouv { public Agent a; public V2 p, real; public float t0, tVirou = -1; public string k, est, ang, dist, fase0, fonte06 = "", ex = "", sa = "", sa2 = ""; public bool novo, mudou, achou, morreu, quina; }
        static readonly System.Collections.Concurrent.ConcurrentDictionary<Agent, Action<Agent, V2, float, string, bool>> ouvidos = new System.Collections.Concurrent.ConcurrentDictionary<Agent, Action<Agent, V2, float, string, bool>>();
        public static int Rodar(int n, int seed0, string saida)
        {
            Brain.OuviuGancho = (a, p, t, k, ok) => { if (ouvidos.TryGetValue(a, out var f)) f(a, p, t, k, ok); };
            var exE = new List<string>();
            var mortes = new Dictionary<string, int>(); var perm = new Dictionary<string, List<float>>();
            var somN = new Dictionary<string, int>(); var somInim = new Dictionary<string, int>(); var somAmigo = new Dictionary<string, int>();
            var vivoAos = new Dictionary<string, int[]>(); int[] AGES = { 2, 4, 6, 8, 12, 20 };
            int rounds = 0; object trava = new object();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); int N = m.agents.Count;
                var lM = new Dictionary<string, int>(); var lP = new Dictionary<string, List<float>>(); var lV = new Dictionary<string, int[]>();
                var lS = new Dictionary<string, int>(); var lSI = new Dictionary<string, int>(); var lSA = new Dictionary<string, int>();
                void Inc(Dictionary<string, int> d, string k, int v = 1) { d.TryGetValue(k, out int c); d[k] = c + v; }
                // B: avistamento aberto por (time que viu, inimigo): último t do contato, ponto, e se já fechou
                var abertoT = new float[2, N]; var abertoP = new V2[2, N]; var acomp = new List<(int tm, int id, float t0, V2 p, string k)>();
                for (int a = 0; a < 2; a++) for (int b = 0; b < N; b++) abertoT[a, b] = -99;
                int abVistos = 0, somVistos = 0, passo = 0;
                // E: cada som aceito no ouvido -> nos 2 s seguintes a mira virou pra la? a acao mudou? achou ele? ou nada
                var abertos = new Dictionary<Agent, Ouv>(); var lEx = new List<string>();
                Action<Agent, V2, float, string, bool> gancho = (a, p, t, k, ok) =>
                {
                    Inc(lS, "E " + k + " · ouvidos"); if (!ok) { Inc(lS, "E " + k + " · filtrado (som mais perto antes)"); return; }
                    if (abertos.ContainsKey(a)) return;
                    var s = m.sounds[m.sounds.Count - 1]; V2 real = s.p;
                    float d = (real - a.pos).Len, an = MathF.Abs(MathX.AngDiff((real - a.pos).Atan2, a.aim));
                    bool sabido = a.brain.target != null || m.intel[(int)a.team].contacts.Values.Any(c => m.t - c.t < 2f && (c.pos - real).Len < 250);
                    abertos[a] = new Ouv { a = a, p = p, real = real, t0 = t, k = k, novo = !sabido, fase0 = a.brain.fase,
                        est = a.brain.target != null ? "duelo" : a.Speed > a.MaxSpeed * 0.25f ? "andando" : "segurando",
                        ang = an < 0.6f ? "frente" : an < 1.6f ? "lado" : "trás", dist = d < 200 ? "<200" : d < 400 ? "200-400" : "400+" };
                };
                foreach (var ag in m.agents) ouvidos[ag] = gancho;
                while (!m.over && m.t < 200)
                {
                    m.Step(); if (++passo % 2 != 0) continue;
                    // A
                    for (; abVistos < m.abates.Count; abVistos++)
                    {
                        var ab = m.abates[abVistos]; if (ab.by == null || ab.v == null || ab.aliado || ab.suicidio) continue;
                        var it = m.intel[(int)ab.v.team]; string lado = ab.v.IsT ? "T morreu" : "CT morreu";
                        string k;
                        if (!it.contacts.TryGetValue(ab.by.id, out var c)) k = "matador nunca visto/ouvido no round";
                        else
                        {
                            float age = ab.t - c.t, d = (c.pos - ab.by.pos).Len;
                            bool olhou = it.seenAt[Intel.Idx(c.pos)] > c.t + 0.5f;
                            string fx = age < 1 ? "a <1 s" : age < 4 ? "b 1-4 s" : age < 8 ? "c 4-8 s" : age < 15 ? "d 8-15 s" : "e 15+ s";
                            k = $"visto há {fx} · {(d <= 200 ? "matou do mesmo lugar (≤200)" : "matou de outro lugar")}" + (age >= 1 && d <= 200 ? (olhou ? " · alguém olhou o ponto depois" : " · ninguém olhou o ponto depois") : "");
                        }
                        Inc(lM, lado + " | " + k); Inc(lM, lado + " | (total)");
                    }
                    // B
                    for (int tm = 0; tm < 2; tm++)
                        foreach (var c in m.intel[tm].contacts.Values)
                        {
                            if (!c.seen) continue;
                            if (c.t > abertoT[tm, c.id] + 0.01f) { abertoT[tm, c.id] = c.t; abertoP[tm, c.id] = c.pos; }
                        }
                    for (int tm = 0; tm < 2; tm++)
                        for (int id = 0; id < N; id++)
                        {
                            float t0 = abertoT[tm, id]; if (t0 < -50 || m.t - t0 < 0.6f) continue;
                            var e = m.agents[id]; if (!e.alive) { abertoT[tm, id] = -99; continue; }
                            string k = $"{(e.IsT ? "TR visto" : "CT visto")} · {(m.bombPlanted ? "pós-plant" : "antes do plant")}";
                            acomp.Add((tm, id, t0, abertoP[tm, id], k)); abertoT[tm, id] = -99;   // fechou: o time parou de ver
                        }
                    for (int q = acomp.Count - 1; q >= 0; q--)
                    {
                        var (tm, id, t0, p, k) = acomp[q]; var e = m.agents[id]; float age = m.t - t0;
                        bool saiu = !e.alive || (e.pos - p).Len > 200, fim = saiu || age >= 30 || m.over;
                        if (!lV.TryGetValue(k, out var vv)) lV[k] = vv = new int[AGES.Length + 1];
                        if (fim)
                        {
                            if (!lP.TryGetValue(k, out var l)) lP[k] = l = new List<float>(); l.Add(saiu ? age : 30.01f);
                            vv[AGES.Length]++;
                            for (int a = 0; a < AGES.Length; a++) if (age > AGES[a] && (!saiu || age > AGES[a])) { if (!saiu || age > AGES[a]) { } }
                            for (int a = 0; a < AGES.Length; a++) if (age >= AGES[a]) vv[a]++;
                            acomp.RemoveAt(q);
                        }
                    }
                    // D: API ColegaTrocando — quantas vezes devolve algo e se o P (crença) está perto do inimigo real com quem o colega troca
                    if (passo % 10 == 0)
                        foreach (var b in m.agents)
                        {
                            if (!b.alive) continue;
                            foreach (var (col, pp, desde, fonte) in b.brain.ColegaTrocando(m.t))
                            {
                                var alvo = col.brain.target; string k = "D " + fonte;
                                Inc(lS, k + " · amostras"); if (alvo == null) { Inc(lS, k + " · sem alvo"); continue; }
                                float e = (pp - alvo.pos).Len; Inc(lS, k + (e < 200 ? " · P a <200 do inimigo" : e < 500 ? " · P a 200-500" : " · P a 500+"));
                            }
                        }
                    // E
                    foreach (var o in abertos.Values.ToList())
                    {
                        var a = o.a; float dt = m.t - o.t0;
                        if (!a.alive) o.morreu = true;
                        else
                        {
                            if (o.tVirou < 0 && MathF.Abs(MathX.AngDiff((o.p - a.pos).Atan2, a.aim)) < 0.35f) o.tVirou = dt;
                            if (a.brain.fase != o.fase0) o.mudou = true;
                            {   string fm = a.brain.fonteMira;   // a mira foi pra quina por onde o dono do som sai (Pixel) e a mão chegou lá
                                if (!o.quina && (fm == "som" || fm == "som colado" || fm.StartsWith("passo")) && MathF.Abs(MathX.AngDiff(a.aim, a.brain.WantAimDiag)) < 0.35f) o.quina = true; }
                            if (a.brain.target != null && (a.brain.target.pos - o.real).Len < 300) o.achou = true;
                            if (o.fonte06 == "" && dt >= 0.6f && o.est == "andando") o.sa = m.t - a.brain.somAndaDiagT > 0.2f ? "SomAndando não rodou" : a.brain.somAndaDiag;
                            if (o.fonte06 == "" && dt >= 0.6f && o.est == "andando" && o.sa == "virou pro som")
                            {   float aS = (o.p - a.pos).Atan2, w = a.brain.WantAimDiag;
                                float dW = MathF.Abs(MathX.AngDiff(w, aS)) * 57.3f, dA = MathF.Abs(MathX.AngDiff(a.aim, w)) * 57.3f;
                                o.sa2 = $"pedido a {(dW < 20 ? "<20" : dW < 60 ? "20-60" : "60+")}° do som · mão a {(dA < 20 ? "<20" : dA < 60 ? "20-60" : "60+")}° do pedido · mira por '{a.brain.fonteMira}'"; }
                            if (o.fonte06 == "" && dt >= 0.6f) { o.fonte06 = a.brain.fonteMira; o.ex = $"{a.name} {(a.IsT ? "T" : "CT")} t={m.t:0.0} {o.k} {o.dist} {o.ang} · {o.est} · fase {a.brain.fase} · mira por '{a.brain.fonteMira}' · {Places.Region(a.pos)}->{Places.Region(o.real)}"; }
                        }
                        if (dt < 2f && !o.morreu) continue;
                        abertos.Remove(a);
                        string r = o.tVirou >= 0 && o.tVirou < 0.05f ? "já olhava" : o.tVirou >= 0 ? "virou" : o.quina ? "mirou na quina do som" : o.achou ? "achou ele sem virar" : o.mudou ? "só mudou a ação" : o.morreu ? "morreu antes" : "NADA";
                        string kk = o.k == "passos" ? "passo" : o.k;
                        string b = $"E {kk} · {(o.novo ? "novo" : "já sabido")} · {o.est}";
                        Inc(lS, b + " · n"); Inc(lS, b + " · " + r);
                        if (o.novo && o.est != "duelo")
                        {   string c = $"E2 {kk} · {o.dist} · {o.ang} · {o.est}"; Inc(lS, c + " · n"); Inc(lS, c + " · " + r);
                            if (o.est == "andando" && o.dist != "400+" && o.ang != "frente") { Inc(lS, "E5 " + r + " · n"); Inc(lS, "E5 " + r + " · " + (o.sa == "" ? "?" : o.sa)); }
                            if (o.est == "andando" && o.dist != "400+" && o.ang != "frente" && o.sa2 != "") { Inc(lS, "E6 " + r + " · n"); Inc(lS, "E6 " + r + " · " + o.sa2); }
                            if (r == "NADA") { Inc(lS, "E3 nada · mira por " + (o.fonte06 == "" ? "?" : o.fonte06)); Inc(lS, "E3 nada · fase " + o.fase0); Inc(lS, "E3 nada · n");
                                if (o.dist != "400+" && o.ang != "frente" && lEx.Count < 6) lEx.Add(o.ex); }
                            if (r == "virou") Inc(lS, $"E4 {kk} · virou em " + (o.tVirou < 0.3f ? "<0,3 s" : o.tVirou < 0.8f ? "0,3-0,8 s" : "0,8-2 s"));
                        }
                    }
                    // C
                    for (; somVistos < m.sounds.Count; somVistos++)
                    {
                        var s = m.sounds[somVistos]; float r = MathF.Min(Alcance(s.kind), 700);
                        Inc(lS, s.kind);
                        int ini = 0, ami = 0;
                        foreach (var b in m.agents) { if (!b.alive || (b.pos - s.p).Len >= r || (b.pos - s.p).Len < 30) continue; if (b.team == s.who) ami++; else ini++; }
                        if (ini > 0) Inc(lSI, s.kind); if (ami > 0) Inc(lSA, s.kind);
                    }
                }
                foreach (var ag in m.agents) ouvidos.TryRemove(ag, out _);
                lock (trava)
                {
                    rounds++;
                    if (exE.Count < 30) exE.AddRange(lEx.Take(2));
                    foreach (var kv in lM) Inc(mortes, kv.Key, kv.Value);
                    foreach (var kv in lP) { if (!perm.TryGetValue(kv.Key, out var l)) perm[kv.Key] = l = new List<float>(); l.AddRange(kv.Value); }
                    foreach (var kv in lV) { if (!vivoAos.TryGetValue(kv.Key, out var v)) vivoAos[kv.Key] = v = new int[AGES.Length + 1]; for (int a = 0; a <= AGES.Length; a++) v[a] += kv.Value[a]; }
                    foreach (var kv in lS) Inc(somN, kv.Key, kv.Value); foreach (var kv in lSI) Inc(somInim, kv.Key, kv.Value); foreach (var kv in lSA) Inc(somAmigo, kv.Key, kv.Value);
                }
            });
            var sb = new StringBuilder(); sb.AppendLine($"crença, memória e som — {rounds} rounds (seed {seed0}, exp: {TeamBrain.EXP})");
            sb.AppendLine("\nA) QUEM MATOU: o time da vítima já tinha visto/ouvido o matador? (contato do time; % das mortes do lado)");
            foreach (var lado in new[] { "T morreu", "CT morreu" })
            {
                mortes.TryGetValue(lado + " | (total)", out int tot); sb.AppendLine($"  {lado} (n={tot})");
                foreach (var kv in mortes.Where(x => x.Key.StartsWith(lado + " | ") && !x.Key.EndsWith("(total)")).OrderBy(x => x.Key, StringComparer.Ordinal))
                    sb.AppendLine($"    {100.0 * kv.Value / Math.Max(1, tot),5:0.0}%  {kv.Key.Substring(lado.Length + 3)}");
            }
            sb.AppendLine("\nB) QUANTO TEMPO UM AVISTAMENTO CONTINUA VERDADE (inimigo real a ≤200 do último ponto visto, depois que o time parou de ver)");
            foreach (var k in perm.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var l = perm[k].OrderBy(x => x).ToList(); string Q(float p) { var v = l[Math.Min(l.Count - 1, (int)(p * l.Count))]; return v > 30 ? "30+" : v.ToString("0.0"); }
                var vv = vivoAos[k]; int tot = l.Count;
                string aos = string.Join(" · ", AGES.Select((a, j) => $"{a} s {100.0 * l.Count(x => x >= a) / Math.Max(1, tot):0}%"));
                sb.AppendLine($"  {k} (n={tot}): fica p25/p50/p75 [{Q(.25f)}, {Q(.5f)}, {Q(.75f)}] s · ainda lá aos {aos}");
            }
            sb.AppendLine("\nC) SONS por 100 rounds: emitidos · chegam a ≥1 inimigo no alcance · chegam a ≥1 amigo no alcance (hoje o amigo não ouve nada)");
            foreach (var kv in somN.Where(x => !x.Key.StartsWith("E") && !x.Key.StartsWith("D ")).OrderByDescending(x => x.Value))
            {
                somInim.TryGetValue(kv.Key, out int si); somAmigo.TryGetValue(kv.Key, out int sa);
                sb.AppendLine($"  {kv.Key,-16} {100.0 * kv.Value / rounds,8:0} · inimigo {100.0 * si / rounds,7:0} · amigo {100.0 * sa / rounds,7:0}");
            }
            sb.AppendLine("\nE) OUVIU -> FEZ O QUÊ (2 s depois do som aceito no ouvido; 'novo' = o time não tinha contato fresco a <250 e eu não estava em duelo)");
            sb.AppendLine("   resultado: já olhava · virou (a mira chegou a <20° do som) · mirou na quina do som (a mira pedida pelo som, na quina por onde ele sai) · achou ele sem virar · só mudou a ação · morreu antes · NADA");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E ") && x.Key.EndsWith(" · n")).OrderByDescending(x => x.Value))
            {
                string b = kv.Key.Substring(0, kv.Key.Length - 4); var partes = somN.Where(x => x.Key.StartsWith(b + " · ") && x.Key != kv.Key).OrderByDescending(x => x.Value);
                sb.AppendLine($"  {b.Substring(2),-40} n={kv.Value,6} · " + string.Join(" · ", partes.Select(x => $"{x.Key.Substring(b.Length + 3)} {100.0 * x.Value / kv.Value:0}%")));
            }
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E ") && x.Key.EndsWith(" · ouvidos")).OrderByDescending(x => x.Value))
            {   somN.TryGetValue(kv.Key.Replace(" · ouvidos", " · filtrado (som mais perto antes)"), out int f); sb.AppendLine($"  ouvidos {kv.Key.Substring(2).Replace(" · ouvidos", ""),-14} {100.0 * kv.Value / rounds,7:0}/100 rounds · filtrados {100.0 * f / Math.Max(1, kv.Value):0}%"); }
            sb.AppendLine("\nE2) só som NOVO fora de duelo, por distância, ângulo da mira (frente <35°, lado <90°, trás) e estado");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E2 ") && x.Key.EndsWith(" · n")).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                string b = kv.Key.Substring(0, kv.Key.Length - 4); var partes = somN.Where(x => x.Key.StartsWith(b + " · ") && x.Key != kv.Key).OrderByDescending(x => x.Value);
                sb.AppendLine($"  {b.Substring(3),-44} n={kv.Value,6} · " + string.Join(" · ", partes.Select(x => $"{x.Key.Substring(b.Length + 3)} {100.0 * x.Value / kv.Value:0}%")));
            }
            somN.TryGetValue("E3 nada · n", out int nn);
            sb.AppendLine($"\nE3) os NADA (som novo fora de duelo, n={nn}): de onde vinha a mira aos 0,6 s e em que fase estava");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E3 ") && x.Key != "E3 nada · n").OrderByDescending(x => x.Value).Take(40))
                sb.AppendLine($"  {100.0 * kv.Value / Math.Max(1, nn),5:0.0}%  {kv.Key.Substring(10)}");
            sb.AppendLine("\nE4) quanto demora a virar (som novo fora de duelo)");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E4 ")).OrderBy(x => x.Key, StringComparer.Ordinal)) sb.AppendLine($"  {kv.Key.Substring(3),-34} {kv.Value}");
            sb.AppendLine("\nE5) andando, som novo <400 de lado/trás: o que o SomAndando fez aos 0,6 s (só roda com apSomAndaLigado)");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E5 ") && x.Key.EndsWith(" · n")).OrderByDescending(x => x.Value))
            {
                string b = kv.Key.Substring(0, kv.Key.Length - 4); var partes = somN.Where(x => x.Key.StartsWith(b + " · ") && x.Key != kv.Key).OrderByDescending(x => x.Value);
                sb.AppendLine($"  {b.Substring(3),-24} n={kv.Value,6} · " + string.Join(" · ", partes.Select(x => $"{x.Key.Substring(b.Length + 3)} {100.0 * x.Value / kv.Value:0}%")));
            }
            sb.AppendLine("\nE6) andando, SomAndando decidiu virar: pra onde a mira foi PEDIDA (quina do Pixel) e se a mão chegou");
            foreach (var kv in somN.Where(x => x.Key.StartsWith("E6 ") && x.Key.EndsWith(" · n")).OrderByDescending(x => x.Value))
            {
                string b = kv.Key.Substring(0, kv.Key.Length - 4); sb.AppendLine($"  {b.Substring(3)} n={kv.Value}");
                foreach (var x in somN.Where(x => x.Key.StartsWith(b + " · ") && x.Key != kv.Key).OrderByDescending(x => x.Value).Take(8)) sb.AppendLine($"     {100.0 * x.Value / kv.Value,5:0.0}%  {x.Key.Substring(b.Length + 3)}");
            }
            sb.AppendLine("\nexemplos de NADA (<400, lado/trás):"); exE.Sort(string.CompareOrdinal); foreach (var e in exE) sb.AppendLine("  " + e);
            Directory.CreateDirectory("Logs/bots"); File.WriteAllText($"Logs/bots/memoria_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
