using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Confere a API RotaHumana pelo C# (sim-apoio, 2026-11-08): sorteia N rotas TR (do spawn, 4 regiões ou até o bomb, até
    /// 50 s) com Proximas(anterior) + SorteiaPermanencia e imprime as 10 mais comuns. Os números do pro estão em
    /// `python -I Tools/rotas_humanas/gera.py valida`. Uso: Headless -rotaHumana 1000 [-mapa brasa]
    /// </summary>
    static class RotaHumanaTeste
    {
        public static int Rodar(int n, int seed)
        {
            var rnd = new Random(seed); var cont = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string rg = "TR", ant = null; float t = 0; var rota = new List<string>();
                while (rota.Count < 4)
                {
                    t += RotaHumana.SorteiaPermanencia(Team.T, rg, (float)rnd.NextDouble());
                    if (t >= 50) break;
                    string v = RotaHumana.SorteiaProxima(Team.T, rg, t, false, ant, (float)rnd.NextDouble());
                    if (v == null) break;
                    ant = rg; rg = v; rota.Add(rg);
                    if (rg == "A" || rg == "B") break;
                }
                string k = string.Join(" > ", rota); cont.TryGetValue(k, out int c); cont[k] = c + 1;
            }
            Console.WriteLine($"RotaHumana ({MapData.MAPA}): {n} rotas TR, 10 mais comuns");
            foreach (var kv in cont.OrderByDescending(x => x.Value).Take(10)) Console.WriteLine($"  {100.0 * kv.Value / n,5:0.0}%  {kv.Key}");
            var (a, b, c2) = RotaHumana.Permanencia(Team.T, "TR"); Console.WriteLine($"permanência TR no spawn: p25 {a:0.0} p50 {b:0.0} p75 {c2:0.0} s");
            Console.WriteLine("próximas do TR saindo do spawn aos 5 s: " + string.Join(", ", RotaHumana.Proximas(Team.T, "TR", 5, false).Select(x => $"{x.regiao} {x.peso:0.00}")));
            int semReg = 0; foreach (var s in MapData.areas) if (RotaHumana.RegiaoDaArea(s.name) == null) semReg++;
            Console.WriteLine($"áreas do mapa: {MapData.areas.Count}, sem região: {semReg}");
            return 0;
        }
    }
}

namespace CSM.Headless
{
    /// <summary>Confere o PlanoRegioes contra a régua do pro: Headless -planoRegioes 400 [-mapa brasa]</summary>
    static class PlanoRegioesTeste
    {
        public static int Rodar(int n, int seed)
        {
            foreach (bool indep in new[] { true, false })
            {
                PlanoRegioes.SoIndependente = indep;
                foreach (var lado in new[] { Team.T, Team.CT })
                {
                    var sd = new double[3]; var s3 = new double[3]; var na = new int[3]; var rng = new Rng(seed); var alvoOk = 0; int alvoN = 0;
                    var rotas = new Dictionary<string, int>();
                    for (int r = 0; r < n; r++)
                    {
                        string sp = lado == Team.T ? "TR" : "CT"; var at = new[] { sp, sp, sp, sp, sp };
                        int[] f = lado == Team.T ? new[] { (int)Role.Entry, (int)Role.Suporte, (int)Role.Suporte, (int)Role.Lurker, (int)Role.Awper }
                                                 : new[] { (int)Role.Ancora, (int)Role.Ancora, (int)Role.Rotacao, (int)Role.Awper, (int)Role.Suporte };
                        string alvo = lado == Team.T ? (rng.Rand() < 0.5f ? "A" : "B") : null;
                        var p = PlanoRegioes.DistribuirComTempos(lado, 0, at, f, rng, alvo);
                        for (int i = 0; i < 5; i++) { string k = string.Join(">", p[i].Skip(1).Take(3).Select(x => x.regiao)); rotas.TryGetValue(k, out int c); rotas[k] = c + 1;
                            if (alvo != null && f[i] != (int)Role.Lurker) { alvoN++; if (p[i].Any(x => x.regiao == alvo)) alvoOk++; } }
                        for (float t = 5; t < 90; t += 0.5f)
                        {
                            var cont = new Dictionary<string, int>();
                            foreach (var rr in p) { string s = null; foreach (var (rg, ch) in rr) { if (ch > t) break; s = rg; } if (s != null) { cont.TryGetValue(s, out int c); cont[s] = c + 1; } }
                            int viv = cont.Values.Sum(); if (viv < 3) continue; if (lado == Team.T && (cont.TryGetValue("A", out int qa) ? qa : 0) + (cont.TryGetValue("B", out int qb) ? qb : 0) >= 2) break;
                            int fa = PlanoRegioes.Fase(t); sd[fa] += (double)cont.Count / viv; s3[fa] += cont.Values.Max() >= 3 ? 1 : 0; na[fa]++;
                        }
                    }
                    Console.WriteLine($"{MapData.MAPA} {lado} {(indep ? "independente" : "PlanoRegioes")}:" + (alvoN > 0 ? $"  chega no bomb do plano {100.0 * alvoOk / alvoN:0}% (não-lurker)" : "")
                        + $"  rotas distintas (3 primeiras regiões) {rotas.Count} em {5 * n}");
                    for (int fa = 0; fa < 3; fa++) if (na[fa] > 0)
                    {   var (rd, r3) = PlanoRegioes.Regua(lado, fa);
                        Console.WriteLine($"   {new[] { "cedo", "meio", "tarde" }[fa],-5}  distintas/vivo {sd[fa] / na[fa]:0.000} (pro {rd:0.000})   3+ na mesma {s3[fa] / na[fa]:0.000} (pro {r3:0.000})");
                    }
                }
            }
            return 0;
        }
    }
}
