using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// TABELA DE DUELO (PLANO_SIMULACAO.md 4.1, "De onde vêm os números do avaliador") — Headless -tabelaDuelo N [-seed S] [-exp ...] [-saida nome].
    /// Roda partidas inteiras (com a economia de verdade) até dar uns N rounds e COLHE os duelos que acontecem sozinhos: um duelo abre quando dois inimigos têm linha limpa (sem
    /// smoke) e pelo menos um deles já tem o outro como alvo; fecha quando um mata o outro (duelo decidido), quando um terceiro
    /// mata um dos dois, ou quando a linha fica fechada por mais de 0,5 s. Dos decididos guarda, do ponto de vista de cada um,
    /// a foto do INÍCIO: classe de arma, faixa de distância, parado/chegando, cego, vida, colega perto e quem já estava com a
    /// mira no outro. Sai a chance de vitória por fator isolado e por combinação, e um modelo simples (base arma x arma x distância
    /// + efeito de cada fator, em log-chance) já no formato de colar em Assets/Game/Sim/Cerebro/TabelaDuelo.cs.
    /// Só lê o estado (é o juiz, não o bot). Grava Logs/bots/tabela_duelo_&lt;saida&gt;.txt e ..._dados.txt (bloco C#).
    /// </summary>
    static class MedeTabelaDuelo
    {
        const float ALCANCE = 3000f, COLEGA = 220f, JANELA = 1.5f, CHEGOU = 60f, PARADO = 40f, MIRA_GRAUS = 15f;
        const int POUCAS = 50;   // célula com menos amostras que isso sai marcada
        static readonly string[] CLS = { "awp", "rifle", "smg", "pistola" };
        static readonly int NF = TabelaDuelo.FAIXAS.Length + 1;   // faixas de distância: as do jogo (TabelaDuelo.FAIXAS)
        static readonly string[] DIST = Enumerable.Range(0, NF).Select(d => d == 0 ? "<" + TabelaDuelo.FAIXAS[0].ToString("0") : d == NF - 1 ? TabelaDuelo.FAIXAS[d - 1].ToString("0") + "+" : TabelaDuelo.FAIXAS[d - 1].ToString("0") + "-" + TabelaDuelo.FAIXAS[d].ToString("0")).ToArray();
        static readonly string[] MOV = { "parado", "chegando", "andando (de lado/saindo)" };
        static readonly string[] VIDA = { "70+", "30-69", "<30" };

        struct Lado { public int cls, mov, vida; public bool cego, colega, olha, tr; }
        class Reg { public int seed, a, b, dist, venc; public float t, d; public Lado A, B; }   // venc: 0 = A, 1 = B
        class Ep { public Reg r; public float semLos; }

        sealed class Juiz
        {
            readonly Match m; readonly int seed, N; readonly Dictionary<int, Ep> ativos = new Dictionary<int, Ep>();
            readonly List<(float t, V2[] pos)> hist = new List<(float, V2[])>(); float proxHist; int abatesVistos;
            public readonly List<Reg> decididos = new List<Reg>(); public int terceiro, saiu, granada;

            public Juiz(Match m, int seed) { this.m = m; this.seed = seed; N = m.agents.Count; }
            static bool Ve(Agent a, Agent b) => MapData.LosWalk(a.pos, a.z + a.Eye - 18f, b.pos, b.z + b.Eye - 18f, 18f);
            V2 PosHa(int id, float seg) { for (int k = hist.Count - 1; k >= 0; k--) if (hist[k].t <= m.t - seg) return hist[k].pos[id]; return hist.Count > 0 ? hist[0].pos[id] : m.agents[id].pos; }

            Lado Foto(Agent x, Agent o)
            {
                var l = new Lado { cls = TabelaDuelo.Classe(x.gunId), cego = x.Blind(m.t), vida = x.hp >= 70 ? 0 : x.hp >= 30 ? 1 : 2, tr = x.IsT };
                V2 antes = PosHa(x.id, JANELA); float cheguei = (antes - o.pos).Len - (x.pos - o.pos).Len;
                l.mov = cheguei > CHEGOU ? 1 : (x.pos - antes).Len < PARADO ? 0 : 2;
                foreach (var c in m.agents) if (c != x && c.alive && c.team == x.team && (c.pos - x.pos).Len < COLEGA) { l.colega = true; break; }
                l.olha = MathF.Abs(MathX.AngDiff((o.pos - x.pos).Atan2, x.aim)) * 180 / MathF.PI < MIRA_GRAUS;
                return l;
            }

            public void Passo()
            {
                float t = m.t;
                // baixas primeiro: decide os duelos de quem caiu (a foto do início já está guardada)
                for (; abatesVistos < m.abates.Count; abatesVistos++)
                {
                    var ab = m.abates[abatesVistos]; if (ab.v == null) continue;
                    foreach (var k in ativos.Keys.ToList())
                    {
                        var e = ativos[k]; if (e.r.a != ab.v.id && e.r.b != ab.v.id) continue; ativos.Remove(k);
                        int o = e.r.a == ab.v.id ? e.r.b : e.r.a;
                        if (ab.by == null || ab.by.id != o || ab.aliado || ab.suicidio) { terceiro++; continue; }
                        if (ab.arma == null || !WeaponStats.ALL.ContainsKey(ab.arma)) { granada++; continue; }   // HE/fogo não é duelo de mira
                        e.r.venc = o == e.r.a ? 0 : 1; decididos.Add(e.r);
                    }
                }
                for (int i = 0; i < N; i++)
                    for (int j = i + 1; j < N; j++)
                    {
                        var A = m.agents[i]; var B = m.agents[j];
                        if (A.team == B.team || !A.alive || !B.alive) continue;
                        int k = i * N + j; ativos.TryGetValue(k, out var e);
                        bool alvo = A.brain.target == B || B.brain.target == A;
                        if (e == null && !alvo) continue;   // ninguém notou ninguém: nem gasta o raio
                        float d = (A.pos - B.pos).Len;
                        bool los = d < ALCANCE && Ve(A, B) && !m.SmokeBlocks(A.pos, A.z + A.Eye, B.pos, B.z + B.Eye);
                        if (e == null)
                        {
                            if (!los) continue;
                            ativos[k] = new Ep { r = new Reg { seed = seed, a = i, b = j, t = t, d = d, dist = TabelaDuelo.FaixaDist(d), A = Foto(A, B), B = Foto(B, A), venc = -1 } };
                        }
                        else if (los) e.semLos = 0;
                        else { e.semLos += Match.DT; if (e.semLos > 0.5f) { ativos.Remove(k); saiu++; } }
                    }
                // a posição entra na história DEPOIS da foto: "há 1,5 s" nunca é o passo de agora
                if (t >= proxHist) { proxHist = t + 0.25f; hist.Add((t, m.agents.Select(a => a.pos).ToArray())); }
            }
            public int Abertos => ativos.Count;
        }

        // ---------------------------------------------------------------- modelo (logística antissimétrica, sem intercepto)
        // parâmetros: 6 pares de classe (awp-rifle, awp-smg, awp-pistola, rifle-smg, rifle-pistola, smg-pistola) + 24 desvios
        // por faixa de distância (encolhidos pro valor do par quando há pouca amostra) + 6 fatores (o meu menos o dele)
        static readonly int F0 = 6 + 6 * NF, NP = F0 + 6;
        static readonly string[] FAT = { "chegando", "cego", "vida 30-69", "vida <30", "colega perto", "já com a mira no outro" };
        static int Par(int lo, int hi) => lo == 0 ? hi - 1 : lo == 1 ? hi + 1 : 5;
        static double[] X(Lado me, Lado ele, int dist)
        {
            var x = new double[NP];
            if (me.cls != ele.cls) { int p = Par(Math.Min(me.cls, ele.cls), Math.Max(me.cls, ele.cls)); double s = me.cls < ele.cls ? 1 : -1; x[p] = s; x[6 + p * NF + dist] = s; }
            x[F0 + 0] = (me.mov == 1 ? 1 : 0) - (ele.mov == 1 ? 1 : 0);
            x[F0 + 1] = (me.cego ? 1 : 0) - (ele.cego ? 1 : 0);
            x[F0 + 2] = (me.vida == 1 ? 1 : 0) - (ele.vida == 1 ? 1 : 0);
            x[F0 + 3] = (me.vida == 2 ? 1 : 0) - (ele.vida == 2 ? 1 : 0);
            x[F0 + 4] = (me.colega ? 1 : 0) - (ele.colega ? 1 : 0);
            x[F0 + 5] = (me.olha ? 1 : 0) - (ele.olha ? 1 : 0);
            return x;
        }
        static double Sig(double z) => 1.0 / (1.0 + Math.Exp(-z));
        static double Dot(double[] w, double[] x) { double s = 0; for (int i = 0; i < NP; i++) s += w[i] * x[i]; return s; }

        static (double[] w, double[] ep) Ajusta(List<Reg> regs)
        {
            var w = new double[NP]; var lam = new double[NP]; var xs = regs.Select(r => X(r.A, r.B, r.dist)).ToArray();
            for (int i = 0; i < NP; i++) lam[i] = i >= 6 && i < F0 ? 4.0 : 0.05;   // desvio por distância: encolhe (≈ ±0,5 de log-chance a priori)
            double[,] H = null;
            for (int it = 0; it < 30; it++)
            {
                var g = new double[NP]; H = new double[NP, NP];
                for (int i = 0; i < NP; i++) { g[i] = -lam[i] * w[i]; H[i, i] = lam[i]; }
                for (int n = 0; n < regs.Count; n++)
                {
                    var x = xs[n]; double p = Sig(Dot(w, x)), y = regs[n].venc == 0 ? 1 : 0, v = p * (1 - p);
                    for (int i = 0; i < NP; i++) { if (x[i] == 0) continue; g[i] += (y - p) * x[i]; for (int j = 0; j < NP; j++) if (x[j] != 0) H[i, j] += v * x[i] * x[j]; }
                }
                var dw = Resolve(H, g); double mx = 0; for (int i = 0; i < NP; i++) { w[i] += dw[i]; mx = Math.Max(mx, Math.Abs(dw[i])); }
                if (mx < 1e-7) break;
            }
            var ep = new double[NP];   // erro-padrão: diagonal da inversa
            for (int i = 0; i < NP; i++) { var e = new double[NP]; e[i] = 1; ep[i] = Math.Sqrt(Math.Max(0, Resolve(H, e)[i])); }
            return (w, ep);
        }
        static double[] Resolve(double[,] H, double[] b)
        {
            int n = b.Length; var a = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) a[i, j] = H[i, j]; a[i, n] = b[i]; }
            for (int c = 0; c < n; c++)
            {
                int pv = c; for (int r = c + 1; r < n; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[pv, c])) pv = r;
                if (pv != c) for (int j = 0; j <= n; j++) { double tmp = a[c, j]; a[c, j] = a[pv, j]; a[pv, j] = tmp; }
                double dg = a[c, c]; if (Math.Abs(dg) < 1e-12) continue;
                for (int r = 0; r < n; r++) { if (r == c) continue; double f = a[r, c] / dg; if (f == 0) continue; for (int j = c; j <= n; j++) a[r, j] -= f * a[c, j]; }
            }
            var x = new double[n]; for (int i = 0; i < n; i++) x[i] = Math.Abs(a[i, i]) < 1e-12 ? 0 : a[i, n] / a[i, i]; return x;
        }

        // ---------------------------------------------------------------- relatório
        static string Pct(int v, int n) => n == 0 ? "   -  " : $"{100.0 * v / n,5:0.0}%";
        static string Marca(int n) => n < POUCAS ? "  (poucas amostras)" : "";

        public static int Rodar(int n, int seed0, string saida)
        {
            var regs = new List<Reg>(); int terceiro = 0, saiu = 0, granada = 0, abertos = 0, rounds = 0; var trava = new object();
            // partidas inteiras (economia de verdade: pistol, eco, forçada, SMG), não rounds soltos de compra cheia
            int partidas = Math.Max(1, (n + 19) / 20); var compras = new Dictionary<string, int>();
            Parallel.For(0, partidas, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var p = new Partida(seed0 + i); Match atual = null; Juiz j = null;
                p.cadaPasso = m => { if (m != atual) { atual = m; j = new Juiz(m, (seed0 + i) * 100 + p.r + 1); } j.Passo(); };
                p.Jogar(m =>
                {
                    if (j == null) return; var h = p.historico[p.historico.Count - 1];
                    lock (trava)
                    {
                        regs.AddRange(j.decididos); terceiro += j.terceiro; saiu += j.saiu; granada += j.granada; abertos += j.Abertos; rounds++;
                        foreach (string k in new[] { "TR " + h.compraT, "CT " + h.compraCT }) { compras.TryGetValue(k, out int v); compras[k] = v + 1; }
                    }
                    j = null;
                });
            });
            n = rounds;
            regs = regs.OrderBy(r => r.seed).ThenBy(r => r.t).ThenBy(r => r.a).ThenBy(r => r.b).ToList();
            // os dois pontos de vista de cada duelo
            var pov = new List<(Lado me, Lado ele, int dist, bool ganhou, float d)>();
            foreach (var r in regs) { pov.Add((r.A, r.B, r.dist, r.venc == 0, r.d)); pov.Add((r.B, r.A, r.dist, r.venc == 1, r.d)); }

            var sb = new StringBuilder(); var ci = CultureInfo.InvariantCulture;
            sb.AppendLine($"TABELA DE DUELO — {n} rounds em {partidas} partidas inteiras (sementes {seed0}..{seed0 + partidas - 1}), exp \"{TeamBrain.EXP}\"");
            sb.AppendLine("compras: " + string.Join(" · ", compras.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")));
            int total = regs.Count + terceiro + saiu + granada + abertos;
            sb.AppendLine($"duelos abertos (linha limpa + alguém com o outro de alvo): {total} ({100.0 * total / n:0} por 100 rounds)");
            sb.AppendLine($"  DECIDIDOS (um matou o outro com a arma): {regs.Count} ({100.0 * regs.Count / Math.Max(1, total):0.0}%) → {pov.Count} pontos de vista");
            sb.AppendLine($"  um terceiro matou um dos dois: {terceiro} · morto por granada/fogo do outro: {granada} · linha fechou 0,5 s+ sem abate: {saiu} · round acabou no meio: {abertos}");
            sb.AppendLine($"  (as chances abaixo são de GANHAR um duelo que foi decidido; célula com menos de {POUCAS} amostras sai marcada)");

            void Marg(string titulo, Func<(Lado me, Lado ele, int dist, bool ganhou, float d), string> chave)
            {
                sb.AppendLine(); sb.AppendLine(titulo);
                foreach (var g in pov.GroupBy(chave).Where(g => g.Key != null).OrderBy(g => g.Key, StringComparer.Ordinal))
                { int c = g.Count(), v = g.Count(x => x.ganhou); sb.AppendLine($"  {g.Key,-64} {Pct(v, c)}  n={c,6}{Marca(c)}"); }
            }
            sb.AppendLine(); sb.AppendLine("================ EFEITOS MARGINAIS (um fator de cada vez, sem controlar os outros) ================");
            Marg("MOVIMENTO — eu", p => MOV[p.me.mov]);
            Marg("MOVIMENTO — eu x ele", p => p.me.mov == p.ele.mov ? null : $"eu {MOV[p.me.mov]} x ele {MOV[p.ele.mov]}");
            Marg("CEGO — eu x ele", p => $"eu {(p.me.cego ? "CEGO" : "vendo")} x ele {(p.ele.cego ? "CEGO" : "vendo")}");
            Marg("VIDA no início — eu", p => "eu com " + VIDA[p.me.vida]);
            Marg("VIDA no início — eu x ele", p => p.me.vida == p.ele.vida ? null : $"eu {VIDA[p.me.vida]} x ele {VIDA[p.ele.vida]}");
            Marg("COLEGA a menos de 220 — eu x ele", p => $"eu {(p.me.colega ? "com colega" : "sozinho")} x ele {(p.ele.colega ? "com colega" : "sozinho")}");
            Marg("MIRA — ele já me olhando (<15°) x eu já olhando ele", p => $"ele {(p.ele.olha ? "ME OLHANDO" : "olhando outro lado")} · eu {(p.me.olha ? "com a mira nele" : "com a mira fora")}");
            Marg("ARMA — eu (contra qualquer arma)", p => "eu de " + CLS[p.me.cls]);
            Marg("DISTÂNCIA x minha arma (contra qualquer arma)", p => $"{CLS[p.me.cls],-8} a {DIST[p.dist]}");
            Marg("LADO (curiosidade; não entra no modelo)", p => p.me.tr ? "eu TR" : "eu CT");
            Marg("MOVIMENTO x ele me olhando (o caso da quina: chego e ele está pré-mirado)", p => p.me.mov == 1 && p.ele.mov != 1 ? $"eu chegando, ele {(p.ele.olha ? "PRÉ-MIRADO em mim" : "olhando outro lado")}" : null);

            sb.AppendLine(); sb.AppendLine("================ ARMA x ARMA x DISTÂNCIA (medido cru) ================");
            sb.AppendLine($"  {"eu x ele",-20}" + string.Concat(DIST.Select(d => $"{d,22}")) + $"{"todas",22}");
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    var linha = new StringBuilder($"  {CLS[a] + " x " + CLS[b],-20}");
                    for (int d = 0; d <= NF; d++)
                    {
                        var q = pov.Where(p => p.me.cls == a && p.ele.cls == b && (d == NF || p.dist == d)).ToList(); int c = q.Count, v = q.Count(x => x.ganhou);
                        linha.Append($"{Pct(v, c) + " n=" + c + (c < POUCAS ? "*" : ""),22}");
                    }
                    sb.AppendLine(linha.ToString());
                }
            sb.AppendLine("  (* = poucas amostras; a x b e b x a somam 100% porque é o mesmo duelo visto dos dois lados)");

            sb.AppendLine(); sb.AppendLine("================ DISTÂNCIA em que os duelos decididos começam (de 50 em 50; 40 unidades ≈ 2,9 m) ================");
            sb.AppendLine("  distância     duelos    awp x rifle (awp ganha)   parado x chegando (parado ganha)");
            foreach (var g in regs.GroupBy(r => Math.Min(20, (int)(r.d / 50))).OrderBy(g => g.Key))
            {
                var ar = pov.Where(p => Math.Min(20, (int)(p.d / 50)) == g.Key && p.me.cls == 0 && p.ele.cls == 1).ToList(); var pc = pov.Where(p => Math.Min(20, (int)(p.d / 50)) == g.Key && p.me.mov == 0 && p.ele.mov == 1).ToList();
                sb.AppendLine($"  {g.Key * 50,4}{(g.Key == 20 ? "+   " : "-" + (g.Key * 50 + 50).ToString().PadRight(4))} {g.Count(),7}    {Pct(ar.Count(x => x.ganhou), ar.Count)} n={ar.Count,-5}           {Pct(pc.Count(x => x.ganhou), pc.Count)} n={pc.Count}");
            }

            // modelo
            var (w, ep) = Ajusta(regs);
            sb.AppendLine(); sb.AppendLine("================ MODELO (base arma x arma x distância + fatores, em log-chance; o meu menos o dele) ================");
            sb.AppendLine("  fator                        coef    ±erro   vezes na chance   (num duelo 50/50 vira)");
            for (int f = 0; f < FAT.Length; f++) sb.AppendLine($"  {FAT[f],-26} {w[F0 + f],7:0.000}  {ep[F0 + f],6:0.000}   x{Math.Exp(w[F0 + f]),5:0.00}             {100 * Sig(w[F0 + f]):0}%{(Math.Abs(w[F0 + f]) < 2 * ep[F0 + f] ? "   (dentro do ruído)" : "")}");
            sb.AppendLine(); sb.AppendLine("  BASE do modelo (chance de eu ganhar com os outros fatores iguais dos dois lados):");
            sb.AppendLine($"  {"eu x ele",-20}" + string.Concat(DIST.Select(d => $"{d,12}")));
            var bas = new double[4, 4, NF];
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    var linha = new StringBuilder($"  {CLS[a] + " x " + CLS[b],-20}");
                    for (int d = 0; d < NF; d++)
                    {
                        double z = 0; if (a != b) { int p = Par(Math.Min(a, b), Math.Max(a, b)); z = (a < b ? 1 : -1) * (w[p] + w[6 + p * NF + d]); }
                        bas[a, b, d] = Sig(z); linha.Append($"{100 * bas[a, b, d],11:0.0}%");
                    }
                    sb.AppendLine(linha.ToString());
                }
            // calibração: o modelo ajustado agora e a tabela embutida no jogo, contra o que aconteceu nesta amostra
            void Calib(string titulo, Func<(Lado me, Lado ele, int dist, bool ganhou, float d), double> prev)
            {
                sb.AppendLine(); sb.AppendLine(titulo); sb.AppendLine("  previsto      medido      n");
                double erro = 0; int tot = 0;
                foreach (var g in pov.Select(p => (pr: prev(p), p.ganhou)).GroupBy(x => Math.Min(9, (int)(x.pr * 10))).OrderBy(g => g.Key))
                { int c = g.Count(); double pm = g.Average(x => x.pr), ob = g.Count(x => x.ganhou) / (double)c; erro += Math.Abs(pm - ob) * c; tot += c; sb.AppendLine($"  {100 * pm,6:0.0}%    {100 * ob,6:0.0}%   {c,6}{Marca(c)}"); }
                sb.AppendLine($"  erro médio de calibração: {100 * erro / Math.Max(1, tot):0.0} pontos");
            }
            Calib("CALIBRAÇÃO do modelo ajustado nesta amostra (por faixa de chance prevista):", p => Sig(Dot(w, X(p.me, p.ele, p.dist))));
            Calib("CALIBRAÇÃO da tabela EMBUTIDA (CSM.Sim.TabelaDuelo.Chance) contra esta amostra:", p => TabelaDuelo.Chance(CLS[p.me.cls], CLS[p.ele.cls], p.d, p.me.mov == 1, p.ele.mov == 1, p.me.cego, p.ele.cego, new[] { 100, 50, 15 }[p.me.vida], new[] { 100, 50, 15 }[p.ele.vida], p.ele.olha, p.me.colega, p.ele.colega, p.me.olha));

            // consultas de exemplo na tabela embutida
            sb.AppendLine(); sb.AppendLine("================ CONSULTAS DE EXEMPLO (CSM.Sim.TabelaDuelo.Chance, a tabela embutida no jogo) ================");
            void Caso(string nome, float c) => sb.AppendLine($"  {nome,-86} {100 * c,5:0.0}%");
            Caso("rifle x rifle, 150, tudo igual, os dois se olhando", TabelaDuelo.Chance("ak", "m4", 150, false, false, false, false, 100, 100, true));
            Caso("rifle x rifle, 150, EU chegando, ele parado pré-mirado", TabelaDuelo.Chance("ak", "m4", 150, true, false, false, false, 100, 100, true));
            Caso("rifle x rifle, 150, eu parado pré-mirado, ELE chegando sem me olhar", TabelaDuelo.Chance("ak", "m4", 150, false, true, false, false, 100, 100, false));
            Caso("rifle x rifle, 150, eu chegando, ele CEGO", TabelaDuelo.Chance("ak", "m4", 150, true, false, false, true, 100, 100, false));
            Caso("rifle x rifle, 150, EU CEGO", TabelaDuelo.Chance("ak", "m4", 150, false, false, true, false, 100, 100, true));
            Caso("rifle x rifle, 150, eu com 20 de vida, ele com 100", TabelaDuelo.Chance("ak", "m4", 150, false, false, false, false, 20, 100, true));
            Caso("awp x rifle, 350", TabelaDuelo.Chance("awp", "ak", 350, false, false, false, false, 100, 100, true));
            Caso("rifle x awp, 350 (o espelho: soma 100)", TabelaDuelo.Chance("ak", "awp", 350, false, false, false, false, 100, 100, true));
            Caso("awp x rifle, 60", TabelaDuelo.Chance("awp", "ak", 60, false, false, false, false, 100, 100, true));
            Caso("pistola x rifle, 60", TabelaDuelo.Chance("glock", "m4", 60, false, false, false, false, 100, 100, true));
            Caso("pistola x rifle, 350", TabelaDuelo.Chance("glock", "m4", 350, false, false, false, false, 100, 100, true));
            Caso("smg x rifle, 60", TabelaDuelo.Chance("mac10", "m4", 60, false, false, false, false, 100, 100, true));
            Caso("smg x rifle, 350", TabelaDuelo.Chance("mac10", "m4", 350, false, false, false, false, 100, 100, true));
            Caso("rifle x rifle, 150, eu chegando com colega, ele sozinho pré-mirado", TabelaDuelo.Chance("ak", "m4", 150, true, false, false, false, 100, 100, true, true, false));
            Caso("pior caso: pistola cega com 10 de vida chegando num awp pré-mirado a 350 (piso 3%)", TabelaDuelo.Chance("glock", "awp", 350, true, false, true, false, 10, 100, true));

            // células completas
            sb.AppendLine(); sb.AppendLine("================ POR COMBINAÇÃO COMPLETA (ponto de vista de cada um; * = menos de " + POUCAS + " amostras) ================");
            sb.AppendLine("  eu: arma, movimento, cego, vida, colega, mira | ele: idem | distância");
            string D(Lado l) => $"{CLS[l.cls],-7} {(l.mov == 0 ? "parado  " : l.mov == 1 ? "chegando" : "andando ")} {(l.cego ? "CEGO " : "vendo")} vida {VIDA[l.vida],-5} {(l.colega ? "c/colega" : "sozinho ")} {(l.olha ? "mira no outro" : "mira fora    ")}";
            var cel = pov.GroupBy(p => $"{D(p.me)} | {D(p.ele)} | {DIST[p.dist],-8}").Select(g => (k: g.Key, c: g.Count(), v: g.Count(x => x.ganhou))).OrderByDescending(x => x.c).ThenBy(x => x.k, StringComparer.Ordinal).ToList();
            sb.AppendLine($"  {cel.Count} combinações apareceram; {cel.Count(x => x.c >= POUCAS)} com {POUCAS}+ amostras");
            foreach (var x in cel) sb.AppendLine($"  {x.k}  {Pct(x.v, x.c)} n={x.c,5}{(x.c < POUCAS ? " *" : "")}");

            // bloco C# pronto pra colar em Assets/Game/Sim/Cerebro/TabelaDuelo.cs
            var cs = new StringBuilder();
            cs.AppendLine($"        // medido em {n} rounds de {partidas} partidas inteiras (sementes {seed0}..{seed0 + partidas - 1}), {regs.Count} duelos decididos");
            cs.AppendLine("        public static readonly float[] FAIXAS = { " + string.Join(", ", TabelaDuelo.FAIXAS.Select(f => f.ToString("0", ci) + "f")) + " };");
            cs.AppendLine("        static readonly float[] BASE =");
            cs.AppendLine("        {");
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    int cn = regs.Count(r => r.A.cls == a && r.B.cls == b || r.A.cls == b && r.B.cls == a);
                    cs.AppendLine("            " + string.Join(", ", Enumerable.Range(0, NF).Select(d => bas[a, b, d].ToString("0.000", ci) + "f")) + $",   // {CLS[a]} x {CLS[b]} (n={cn}; por faixa: {string.Join("/", Enumerable.Range(0, NF).Select(d => regs.Count(r => r.dist == d && (r.A.cls == a && r.B.cls == b || r.A.cls == b && r.B.cls == a))))})");
                }
            cs.AppendLine("        };");
            string[] nomes = { "K_CHEGANDO", "K_CEGO", "K_VIDA_MEDIA", "K_VIDA_BAIXA", "K_COLEGA", "K_MIRA" };
            cs.AppendLine("        const float " + string.Join(", ", nomes.Select((s, f) => s + " = " + w[F0 + f].ToString("0.000", ci) + "f")) + ";");
            cs.AppendLine("        // erro-padrão: " + string.Join(", ", nomes.Select((s, f) => s + " ±" + ep[F0 + f].ToString("0.000", ci))));
            sb.AppendLine(); sb.AppendLine("================ BLOCO C# (colar em Assets/Game/Sim/Cerebro/TabelaDuelo.cs) ================"); sb.Append(cs);

            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText($"Logs/bots/tabela_duelo_{saida}.txt", sb.ToString());
            File.WriteAllText($"Logs/bots/tabela_duelo_{saida}_dados.txt", cs.ToString());
            string txt = sb.ToString(); int corte = txt.IndexOf("================ POR COMBINAÇÃO COMPLETA", StringComparison.Ordinal), bloco = txt.IndexOf("================ BLOCO C#", StringComparison.Ordinal);
            Console.WriteLine(txt.Substring(0, corte) + $"(combinações completas: só no arquivo Logs/bots/tabela_duelo_{saida}.txt)\n\n" + txt.Substring(bloco));
            return 0;
        }
    }
}
