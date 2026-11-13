using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// "Os bots são travados": mede duelos, movimento em contato e a distância de luta por arma, passo a passo
    /// (Partida.cadaPasso). Uso: Headless.dll -fluidez N -seed S [-exp ...]. Distâncias em unidades do CS (1 do jogo = 2,9).
    /// </summary>
    public static class Fluidez
    {
        const float CS = 2.9f, DT = Match.DT;
        class Ep { public Agent me, alvo; public float t0, hp0, distIni; public int tiros0; public bool levou, levouCedo, parouLongo; public float parado, andando, paradoSeguido, ultDirT = -9; public V2 ultDir; public int viradas; public bool atirouAntes; public float primeiroTiroT = -1; public int tiros0b; }
        class Acc
        {
            public int eps, matou, morreu, outroMatou, saiu, levouCedo, levouCedoFicou, levouCedoFicouMorreu, levouCedoSaiu, travados, viradas;
            public double dur, durMais2, parado, emContato;
            public int levouSemVer, reagiuAndando;
            public readonly List<float> ttkVer = new List<float>(), primeiroTiro = new List<float>(), viuAtirador = new List<float>(), matouAtirador = new List<float>();
            public readonly Dictionary<string, List<float>> distAbate = new Dictionary<string, List<float>>();
            public readonly Dictionary<string, List<float>> distInicio = new Dictionary<string, List<float>>();
            public void Soma(Acc o)
            {
                ttkVer.AddRange(o.ttkVer); primeiroTiro.AddRange(o.primeiroTiro); viuAtirador.AddRange(o.viuAtirador); matouAtirador.AddRange(o.matouAtirador);
                eps += o.eps; matou += o.matou; morreu += o.morreu; outroMatou += o.outroMatou; saiu += o.saiu; levouCedo += o.levouCedo; levouCedoFicou += o.levouCedoFicou;
                levouCedoFicouMorreu += o.levouCedoFicouMorreu; levouCedoSaiu += o.levouCedoSaiu; travados += o.travados; viradas += o.viradas; dur += o.dur; durMais2 += o.durMais2;
                parado += o.parado; emContato += o.emContato; levouSemVer += o.levouSemVer; reagiuAndando += o.reagiuAndando;
                foreach (var kv in o.distAbate) { if (!distAbate.TryGetValue(kv.Key, out var l)) distAbate[kv.Key] = l = new List<float>(); l.AddRange(kv.Value); }
                foreach (var kv in o.distInicio) { if (!distInicio.TryGetValue(kv.Key, out var l)) distInicio[kv.Key] = l = new List<float>(); l.AddRange(kv.Value); }
            }
        }
        static string Classe(string g) => g == null ? "?" : g == "awp" ? "AWP" : g == "mp9" || g == "mac10" ? "SMG" : WeaponStats.Pistola(g) ? "pistola" : "rifle";
        static void Add(Dictionary<string, List<float>> d, string k, float v) { if (!d.TryGetValue(k, out var l)) d[k] = l = new List<float>(); l.Add(v); }

        public static int Rodar(int n, int seed0)
        {
            new Match(seed0); CSM.Map.Spots.All();
            var tot = new Acc(); var trava = new object();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, i =>
            {
                var acc = new Acc(); var p = new Partida(seed0 + i);
                Match atual = null; Dictionary<Agent, Ep> eps = null; Dictionary<Agent, int> hpAnt = null; Dictionary<Agent, float> semAlvoDesde = null; int abatesVistos = 0;
                Dictionary<Agent, float> levouSemVerEm = null; Dictionary<Agent, Agent> atiradorDe = null; Dictionary<Agent, (Agent q, float t)> sobTiro = null;
                void Fecha(Ep e, string como)
                {
                    float d = atual.t - e.t0; acc.eps++; acc.dur += d; if (d > 2f) acc.durMais2++;
                    acc.parado += e.parado; acc.emContato += e.parado + e.andando; acc.viradas += e.viradas; if (e.parouLongo) acc.travados++;
                    if (como == "matou") acc.ttkVer.Add(d);
                    if (e.primeiroTiroT > 0) acc.primeiroTiro.Add(e.primeiroTiroT - e.t0);
                    switch (como) { case "matou": acc.matou++; break; case "morreu": acc.morreu++; break; case "outro": acc.outroMatou++; break; default: acc.saiu++; break; }
                    if (e.levouCedo) { acc.levouCedo++; if (como == "saiu") acc.levouCedoSaiu++; else { acc.levouCedoFicou++; if (como == "morreu") acc.levouCedoFicouMorreu++; } }
                }
                p.cadaPasso = m =>
                {
                    if (m != atual) { atual = m; eps = new Dictionary<Agent, Ep>(); hpAnt = m.agents.ToDictionary(a => a, a => a.hp); semAlvoDesde = new Dictionary<Agent, float>(); levouSemVerEm = new Dictionary<Agent, float>(); atiradorDe = new Dictionary<Agent, Agent>(); sobTiro = new Dictionary<Agent, (Agent, float)>(); abatesVistos = 0; }
                    // abates novos: distância por arma
                    for (; abatesVistos < m.abates.Count; abatesVistos++)
                    {
                        var ab = m.abates[abatesVistos];
                        if (ab.aliado || ab.by == null || ab.by == ab.v) continue;
                        if (sobTiro.TryGetValue(ab.by, out var st) && st.q == ab.v) { acc.matouAtirador.Add(m.t - st.t); sobTiro.Remove(ab.by); }
                        Add(acc.distAbate, Classe(ab.arma), (ab.by.pos - ab.v.pos).Len * CS);
                    }
                    foreach (var a in m.agents)
                    {
                        int hp0 = hpAnt[a]; bool levouAgora = a.hp < hp0; hpAnt[a] = a.hp;
                        eps.TryGetValue(a, out var e);
                        if (e != null)
                        {
                            if (!a.alive) { Fecha(e, "morreu"); eps.Remove(a); continue; }
                            if (!e.alvo.alive) { Fecha(e, e.alvo.killedBy == a ? "matou" : "outro"); eps.Remove(a); }
                        }
                        if (!a.alive) continue;
                        var alvo = a.brain?.target;
                        if (alvo != null && alvo.alive)
                        {
                            semAlvoDesde.Remove(a);
                            if (e != null && e.alvo != alvo && eps.ContainsKey(a)) { Fecha(e, "saiu"); eps.Remove(a); e = null; }
                            if (e == null || !eps.ContainsKey(a))
                            {
                                e = new Ep { me = a, alvo = alvo, t0 = m.t, hp0 = a.hp, distIni = (alvo.pos - a.pos).Len * CS, tiros0b = a.shots };
                                if (levouSemVerEm.TryGetValue(a, out float tlv) && atiradorDe.TryGetValue(a, out var qa) && qa == alvo) { acc.viuAtirador.Add(m.t - tlv); sobTiro[a] = (alvo, tlv); }
                                eps[a] = e; Add(acc.distInicio, Classe(a.gunId), e.distIni);
                            }
                            if (e.primeiroTiroT < 0 && a.shots > e.tiros0b) e.primeiroTiroT = m.t;
                            if (levouAgora) { e.levou = true; if (m.t - e.t0 < 0.6f && e.hp0 - a.hp >= 25 && e.alvo.alive) e.levouCedo = true; }
                            float v = a.Speed;
                            if (v < 15f) { e.parado += DT; e.paradoSeguido += DT; if (e.paradoSeguido > 1.5f) e.parouLongo = true; }
                            else
                            {
                                e.andando += DT; e.paradoSeguido = 0;
                                var dir = a.vel * (1f / v);
                                if (e.ultDirT > -9 && dir.x * e.ultDir.x + dir.y * e.ultDir.y < -0.5f) e.viradas++;
                                e.ultDir = dir; e.ultDirT = m.t;
                            }
                        }
                        else if (e != null && eps.ContainsKey(a))
                        {
                            if (!semAlvoDesde.TryGetValue(a, out float t0)) semAlvoDesde[a] = t0 = m.t;
                            if (m.t - t0 > 1.0f) { Fecha(e, "saiu"); eps.Remove(a); semAlvoDesde.Remove(a); }
                        }
                        else if (levouAgora && hp0 - a.hp >= 20)
                        {   // levou tiro sem estar vendo ninguém: reage andando (sai da linha) em até 0,5 s?
                            acc.levouSemVer++; levouSemVerEm[a] = m.t; var qm = m.agents.Where(x => x.alive && x.team != a.team && m.t - x.lastShotT < 0.15f).OrderBy(x => (x.pos - a.pos).Len).FirstOrDefault(); if (qm != null) atiradorDe[a] = qm;
                        }
                        if (levouSemVerEm.TryGetValue(a, out float tl) && m.t - tl <= 0.5f && a.Speed * CS > 100f) { acc.reagiuAndando++; levouSemVerEm.Remove(a); }
                        else if (levouSemVerEm.ContainsKey(a) && m.t - levouSemVerEm[a] > 0.5f) levouSemVerEm.Remove(a);
                    }
                };
                p.Jogar();
                lock (trava) tot.Soma(acc);
            });
            var inv = System.Globalization.CultureInfo.InvariantCulture; var sb = new StringBuilder();
            double P(double a, double b) => b > 0 ? 100.0 * a / b : 0;
            sb.AppendLine($"FLUIDEZ {n} partidas (seed {seed0}) {TeamBrain.EXP}");
            sb.AppendLine($"  duelos (eu com alvo): {tot.eps} | terminou: eu matei {P(tot.matou, tot.eps):0.0}% · eu morri {P(tot.morreu, tot.eps):0.0}% · outro matou {P(tot.outroMatou, tot.eps):0.0}% · SAÍ VIVO (os dois vivos) {P(tot.saiu, tot.eps):0.0}%");
            sb.AppendLine($"  duração média {tot.dur / Math.Max(1, tot.eps):0.00} s | duelos de mais de 2 s {P(tot.durMais2, tot.eps):0.0}%");
            sb.AppendLine($"  levei ≥25 de dano nos primeiros 0,6 s sem matar: {tot.levouCedo} | fiquei até o fim {P(tot.levouCedoFicou, tot.levouCedo):0.0}% (desses, morri {P(tot.levouCedoFicouMorreu, tot.levouCedoFicou):0.0}%) · saí {P(tot.levouCedoSaiu, tot.levouCedo):0.0}%");
            sb.AppendLine($"  em contato: parado {P(tot.parado, tot.emContato):0.0}% do tempo | travado (parado >1,5 s seguidos no duelo) {P(tot.travados, tot.eps):0.0}% dos duelos | viradas de direção (ADAD) {tot.viradas / Math.Max(1e-9, tot.emContato):0.00}/s");
            sb.AppendLine($"  levei tiro sem ver ninguém: {tot.levouSemVer} | saí andando em até 0,5 s {P(tot.reagiuAndando, tot.levouSemVer):0.0}%");
            string Q2(List<float> l) => l.Count == 0 ? "-" : string.Format(inv, "n={0} mediana {1:0.00} s · p25 {2:0.00} · p75 {3:0.00} · p90 {4:0.00}", l.Count, Pct(l, .5), Pct(l, .25), Pct(l, .75), Pct(l, .9));
            sb.AppendLine($"  vi o inimigo → 1º tiro: {Q2(tot.primeiroTiro)}");
            sb.AppendLine($"  vi o inimigo → matei (duelos que eu ganhei): {Q2(tot.ttkVer)}");
            sb.AppendLine($"  levei tiro sem ver → achei o atirador: {Q2(tot.viuAtirador)}");
            sb.AppendLine($"  levei tiro sem ver → matei o atirador: {Q2(tot.matouAtirador)}");
            sb.AppendLine("  distância (unidades do CS; perto <500, médio 500–1500, longe >1500):");
            foreach (var k in new[] { "SMG", "rifle", "AWP", "pistola" })
            {
                string Q(List<float> l) => l == null || l.Count == 0 ? "-" : string.Format(inv, "n={0} mediana {1:0} p75 {2:0} p90 {3:0} | longe {4:0.0}%", l.Count, Pct(l, .5), Pct(l, .75), Pct(l, .9), 100.0 * l.Count(x => x > 1500) / l.Count);
                tot.distAbate.TryGetValue(k, out var la); tot.distInicio.TryGetValue(k, out var li);
                sb.AppendLine($"    {k,-8} abate: {Q(la)}");
                sb.AppendLine($"    {"",-8} começou o duelo: {Q(li)}");
            }
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText($"Logs/bots/fluidez_{seed0}.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
        static float Pct(List<float> l, double q) { var s = l.OrderBy(x => x).ToList(); return s[(int)Math.Min(s.Count - 1, q * s.Count)]; }
    }
}
