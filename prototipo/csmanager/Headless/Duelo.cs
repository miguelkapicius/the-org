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
    /// Medidor de duelo (Headless -duelo N [-exp ...] [-saida nome]): assiste cada round e mede cada vez que dois inimigos se
    /// veem e trocam tiro — quanto dura, como acaba (abate rápido, abate demorado, alguém sai da linha), se os dois ficaram no
    /// mesmo lugar, e se quem saiu reabriu do mesmo pixel ou de outro. É a régua do "ficam parados trocando tiro até alguém
    /// morrer" que o Gabriel vê no exe. Só lê o estado (oráculo: é o juiz, não o bot). Grava Logs/bots/duelo_&lt;saida&gt;.txt.
    /// </summary>
    static class MedeDuelo
    {
        const float MESMO_LUGAR = 30f;   // ~2 m: ADAD em volta do mesmo ponto ainda é "o mesmo lugar"
        class Ep
        {
            public int a, b; public float ini, ult, semLos, dist0; public V2 pa0, pb0; public float maxA, maxB; public int tirosA, tirosB, hpA0, hpB0;
            public float primDanoA = -1, primDanoB = -1;   // quando cada um levou o 1º dano dentro do duelo
            public readonly Dictionary<string, int> estado = new Dictionary<string, int>();   // passos de cada lado por "o que estava fazendo"
        }
        class Tot
        {
            public int rounds, duelos, viramSemTiro;
            public readonly Dictionary<string, int> n = new Dictionary<string, int>();
            public readonly Dictionary<string, List<string>> ex = new Dictionary<string, List<string>>();
            public void Soma(string k, int v = 1) { n.TryGetValue(k, out int x); n[k] = x + v; }
            public void Exemplo(string k, string s) { if (!ex.TryGetValue(k, out var l)) ex[k] = l = new List<string>(); if (l.Count < 10) l.Add(s); }
        }

        static bool Ve(Agent a, Agent b) => MapData.LosWalk(a.pos, a.z + a.Eye - 18f, b.pos, b.z + b.Eye - 18f, 18f);

        sealed class Juiz
        {
            readonly Match m; readonly Tot tot; readonly int seed;
            readonly Dictionary<int, Ep> ativos = new Dictionary<int, Ep>();
            readonly float[] tiroAnt = new float[10]; readonly bool[] vivoAnt = new bool[10]; readonly int[] hpAnt = new int[10];
            readonly Dictionary<int, (float t, int quem, V2 pos)> saiu = new Dictionary<int, (float, int, V2)>();   // par → quem quebrou a linha e de onde
            readonly V2[] posHa = new V2[10]; float posHaT = -9;   // posição de ~0,5 s atrás (quem se mexeu pra quebrar a linha)
            public Gravador g; readonly List<(int a, int b, float ini, float ult, string fim, string tag, float dist)> pend = new List<(int, int, float, float, string, string, float)>();
            public Juiz(Match m, Tot tot, int seed) { this.m = m; this.tot = tot; this.seed = seed; for (int i = 0; i < 10; i++) { tiroAnt[i] = m.agents[i].lastShotT; vivoAnt[i] = true; hpAnt[i] = m.agents[i].hp; } }

            string Quem(Agent x) => $"{x.name} ({(x.IsT ? "TR" : "CT")}, {x.gunId})";
            static string Banda(float d) => d < 0.5f ? "a <0,5 s" : d < 1f ? "b 0,5-1 s" : d < 2.5f ? "c 1-2,5 s" : d < 5f ? "d 2,5-5 s" : "e 5 s+";
            static string Dist(float d) => d < 150 ? "perto" : d < 350 ? "médio" : "longe";

            void Fecha(Ep e, string fim, Agent matou = null)
            {
                ativos.Remove(e.a * 10 + e.b);
                var A = m.agents[e.a]; var B = m.agents[e.b];
                float dur = e.ult - e.ini;
                lock (tot)
                {
                    if (e.tirosA + e.tirosB == 0) { tot.viramSemTiro++; return; }
                    tot.duelos++;
                    bool parados = e.maxA < MESMO_LUGAR && e.maxB < MESMO_LUGAR;
                    if (g != null && dur >= 1f)
                    {
                        string tg = dur >= 2.5f && e.tirosA >= 2 && e.tirosB >= 2 && parados ? "pinando (2,5 s+, os dois atirando do mesmo lugar)"
                                  : dur >= 2.5f ? "duelo longo (2,5 s+)" : "duelo comum (1-2,5 s)";
                        uint h = (uint)(seed * 7919 + e.a * 131 + e.b * 17 + (int)(e.ini * 10)); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                        if (tg.StartsWith("pinando") ? h % 3 == 0 : tg.StartsWith("duelo longo") ? h % 6 == 0 : h % 25 == 0) pend.Add((e.a, e.b, e.ini, e.ult, fim, tg, e.dist0));
                    }
                    tot.Soma("fim: " + fim);
                    tot.Soma("duração: " + Banda(dur));
                    tot.Soma("duração por distância: " + Dist(e.dist0) + " · " + Banda(dur));
                    if (dur >= 2.5f) foreach (var kv in e.estado) tot.Soma("duelo longo (2,5 s+), passos em: " + kv.Key, kv.Value);
                    if (parados) tot.Soma("os dois no mesmo lugar o duelo todo");
                    if (parados && dur >= 1f) tot.Soma("os dois no mesmo lugar o duelo todo (duelos de 1 s+)");
                    if (dur >= 1f) tot.Soma("duelos de 1 s+");
                    // CENA: trocação parada
                    if (dur >= 2.5f && e.tirosA >= 2 && e.tirosB >= 2 && parados)
                    {
                        tot.Soma("CENA trocação parada (2,5 s+, os dois atirando, os dois no mesmo lugar)");
                        tot.Exemplo("CENA trocação parada (2,5 s+, os dois atirando, os dois no mesmo lugar)", $"semente {seed} t={e.ini:0.0}-{e.ult:0.0} · {Quem(A)} x {Quem(B)} · {Places.Region(A.pos)} x {Places.Region(B.pos)} · {Dist(e.dist0)} · tiros {e.tirosA}/{e.tirosB} · {fim} · fases '{A.brain.fase}' / '{B.brain.fase}'");
                    }
                    if (matou != null)
                    {
                        // POR ARMA: quem ganhou o duelo, por classe de arma dos dois e distância em que começou
                        static string Cls(Agent x) => x.gunId == "awp" ? "awp" : WeaponStats.Pistola(x.gunId) ? "pistola" : (x.gunId == "mp9" || x.gunId == "mac10") ? "smg" : "rifle";
                        var perdeu = matou == A ? B : A; string cm = Cls(matou), cp = Cls(perdeu), dbx = e.dist0 < 150 ? "perto" : e.dist0 < 350 ? "médio" : "longe";
                        if (cm != cp) tot.Soma($"ARMA x ARMA ({dbx}): {cm} matou {cp}");
                        tot.Soma($"ARMA: duelos decididos de {cm} começaram {dbx}"); tot.Soma($"ARMA: duelos decididos de {cp} começaram {dbx}");
                        var v = matou == A ? B : A; float maxV = matou == A ? e.maxB : e.maxA; float pd = matou == A ? e.primDanoB : e.primDanoA;
                        // CENA: levou tiro, não saiu do lugar e morreu ali (1 s+ depois do 1º dano)
                        if (pd >= 0 && e.ult - pd >= 1f)
                        {
                            tot.Soma("morreu 1 s+ depois do 1º dano no duelo");
                            if (maxV < MESMO_LUGAR)
                            {
                                tot.Soma("CENA levou tiro, ficou no lugar e morreu ali (1 s+ depois do 1º dano)");
                                tot.Exemplo("CENA levou tiro, ficou no lugar e morreu ali (1 s+ depois do 1º dano)", $"semente {seed} t={pd:0.0}→{e.ult:0.0} · {Quem(v)} em {Places.Region(v.pos)} morto por {Quem(matou)} · {Dist(e.dist0)} · fase '{v.brain.fase}' · pensava: {v.brain.thought}");
                            }
                        }
                    }
                    if (fim.StartsWith("alguém saiu"))
                    {
                        // quem quebrou a linha: quem mais se mexeu no último meio segundo
                        float mA = (A.pos - posHa[e.a]).Len, mB = (B.pos - posHa[e.b]).Len; int q = mA >= mB ? e.a : e.b;
                        saiu[e.a * 10 + e.b] = (e.ult, q, m.agents[q].pos);
                        int hpQ = m.agents[q].hp, hp0 = q == e.a ? e.hpA0 : e.hpB0;
                        tot.Soma("saiu da linha: " + (hpQ < hp0 ? "depois de levar dano" : "sem ter levado dano"));
                    }
                }
            }

            public void Passo()
            {
                float t = m.t; g?.Quadro();
                if (t - posHaT >= 0.5f) { posHaT = t; for (int i = 0; i < 10; i++) posHa[i] = m.agents[i].pos; }
                // mortes: fecha os duelos de quem morreu
                for (int i = 0; i < 10; i++)
                {
                    var v = m.agents[i];
                    if (vivoAnt[i] && !v.alive)
                    {
                        vivoAnt[i] = false;
                        foreach (var e in ativos.Values.Where(x => x.a == i || x.b == i).ToList())
                        {
                            var o = m.agents[e.a == i ? e.b : e.a];
                            if (v.killedBy == o) { e.ult = t; Fecha(e, "abate", o); }
                            else Fecha(e, "morto por um terceiro");
                        }
                    }
                }
                for (int i = 0; i < 10; i++)
                    for (int j = i + 1; j < 10; j++)
                    {
                        var A = m.agents[i]; var B = m.agents[j];
                        if (A.team == B.team || !A.alive || !B.alive) continue;
                        int k = i * 10 + j; ativos.TryGetValue(k, out var e);
                        bool los = (A.pos - B.pos).Len < 1000 && Ve(A, B) && !A.Blind(t) && !B.Blind(t) && !m.SmokeBlocks(A.pos, A.z + A.Eye, B.pos, B.z + B.Eye);
                        if (los)
                        {
                            if (e == null)
                            {
                                ativos[k] = e = new Ep { a = i, b = j, ini = t, pa0 = A.pos, pb0 = B.pos, hpA0 = A.hp, hpB0 = B.hp, dist0 = (A.pos - B.pos).Len };
                                // OLHAR: no instante em que a linha abre, pra onde cada um estava olhando (e fazendo o quê)
                                foreach (var (x, o) in new[] { (A, B), (B, A) })
                                {
                                    float ang = MathF.Abs(MathX.AngDiff((o.pos - x.pos).Atan2, x.aim)) * 180 / MathF.PI;
                                    string fx = ang < 15 ? "a na mira (<15°)" : ang < 55 ? "b no campo de visão (15-55°)" : "c FORA do campo de visão (55°+)";
                                    string mov = x.Speed < 8 ? "parado" : "andando";
                                    lock (tot)
                                    {
                                        tot.Soma("OLHAR quando a linha abre: " + fx);
                                        if (ang >= 55) { tot.Soma("OLHAR fora do campo, fazendo: " + mov + " · " + x.brain.fase); }
                                        if (ang >= 55 && x.Speed >= 8)
                                        {   // andando: o inimigo apareceu na frente, do lado ou atrás do rumo? o time sabia dele? quem mandava na mira?
                                            float rumo = MathF.Abs(MathX.AngDiff((o.pos - x.pos).Atan2, x.vel.Atan2)) * 180 / MathF.PI;
                                            bool sabia = m.intel[(int)x.team].contacts.TryGetValue(o.id, out var cc) && t - cc.t < 4f;
                                            float dd = (o.pos - x.pos).Len;
                                            tot.Soma($"OLHAR andando fora do campo: inimigo {(rumo < 60 ? "a NA FRENTE do rumo" : rumo < 120 ? "b do lado" : "c atrás")} · {(sabia ? "time sabia dele (<4 s)" : "ninguém sabia")} · {(dd < 250 ? "perto" : dd < 600 ? "médio" : "longe")}");
                                            tot.Soma($"OLHAR andando fora do campo, mira por: {x.brain.fonteMira} · {(rumo < 60 ? "na frente" : rumo < 120 ? "do lado" : "atrás")}");
                                            if (rumo < 60) tot.Exemplo("OLHAR andando: inimigo apareceu NA FRENTE do rumo e a mira estava noutro lado", $"semente {seed} t={t:0.0} · {Quem(x)} em {Places.Region(x.pos)} [{x.pos.x:0},{x.pos.y:0}] mira a {ang:0}° do {o.name} ({Places.Region(o.pos)} [{o.pos.x:0},{o.pos.y:0}], {dd * 2.9f / 40:0} m) · mira por '{x.brain.fonteMira}' · fase '{x.brain.fase}' · {x.brain.thought}");
                                        }
                                        if (ang >= 55 && x.Speed < 8) tot.Exemplo("OLHAR parado de lado/de costas pra onde o inimigo apareceu", $"semente {seed} t={t:0.0} · {Quem(x)} em {Places.Region(x.pos)} a {ang:0}° do {o.name} ({Places.Region(o.pos)}) · fase '{x.brain.fase}' · {x.brain.thought}");
                                    }
                                }
                                if (saiu.TryGetValue(k, out var s) && t - s.t < 6f)
                                {   // reabriu: de onde?
                                    float andou = (m.agents[s.quem].pos - s.pos).Len;
                                    lock (tot) tot.Soma("reabriu em até 6 s: " + (andou > 40 ? "de outro lugar (2,5 m+)" : "do mesmo lugar"));
                                    saiu.Remove(k);
                                }
                            }
                            e.ult = t; e.semLos = 0;
                            e.maxA = MathF.Max(e.maxA, (A.pos - e.pa0).Len); e.maxB = MathF.Max(e.maxB, (B.pos - e.pb0).Len);
                            if (A.lastShotT != tiroAnt[i] && A.brain.AlvoAgora == B) { if (e.tirosA == 0) Posicao(A, B); e.tirosA++; }
                            if (B.lastShotT != tiroAnt[j] && B.brain.AlvoAgora == A) { if (e.tirosB == 0) Posicao(B, A); e.tirosB++; }
                            Estado(e, A, B, A.lastShotT != tiroAnt[i]); Estado(e, B, A, B.lastShotT != tiroAnt[j]);
                            if (A.hp < hpAnt[i] && e.primDanoA < 0) e.primDanoA = t;
                            if (B.hp < hpAnt[j] && e.primDanoB < 0) e.primDanoB = t;
                        }
                        else if (e != null) { e.semLos += Match.DT; if (e.semLos > 0.5f) Fecha(e, "alguém saiu da linha"); }
                    }
                for (int i = 0; i < 10; i++) { tiroAnt[i] = m.agents[i].lastShotT; hpAnt[i] = m.agents[i].hp; }
            }
            // POSIÇÃO no 1º tiro: quanto do corpo de x o inimigo vê (5 pontos: cabeça, ombros, pernas) e se havia, a um passo,
            // um ponto que ainda vê a cabeça dele mostrando menos (a quina)
            static readonly (float h, float l)[] CORPO = { (21f, 0f), (15f, 4.6f), (15f, -4.6f), (5f, 3f), (5f, -3f) };
            static int Exposto(V2 p, float z, float duck, Agent o)
            {
                V2 d = p - o.pos; float dl = MathF.Max(1, d.Len); V2 lat = new V2(-d.y / dl, d.x / dl); float k = 1f - 0.28f * duck; int n = 0;
                foreach (var (h, l) in CORPO) if (MapData.LosWalk(o.pos, o.z + o.Eye, p + lat * l, z + h * k, 0f)) n++;
                return n;
            }
            void Posicao(Agent x, Agent o)
            {
                int ex = Exposto(x.pos, x.z, x.duck, o); bool quina = false; float dist = (x.pos - o.pos).Len;
                if (ex >= 3)
                    foreach (float r in new[] { 8f, 16f, 26f })
                    {
                        for (int k = 0; k < 16 && !quina; k++)
                        {
                            V2 q = x.pos + V2.Ang(k * 0.3927f) * r;
                            if (!NavGrid.Movable(q) || !NavGrid.Direct(x.pos, q)) continue;
                            float zq = NavGrid.ZAt(q);
                            if (!MapData.LosWalk(q, zq + x.Eye, o.pos, o.z + 21f, 0f)) continue;
                            if (Exposto(q, zq, x.duck, o) <= 2) quina = true;
                        }
                        if (quina) break;
                    }
                string arma = x.gunId == "awp" ? "awp" : WeaponStats.Pistola(x.gunId) ? "pistola" : (x.gunId == "mp9" || x.gunId == "mac10") ? "smg" : "rifle";
                string db = dist < 150 ? "perto" : dist < 350 ? "médio" : "longe";
                string sit = ex <= 2 ? "a na quina (mostra até 2 de 5)" : quina ? "c corpo à mostra COM quina a um passo" : "b corpo à mostra, sem quina perto";
                lock (tot) { tot.Soma("POSIÇÃO no 1º tiro: " + sit); tot.Soma($"POSIÇÃO no 1º tiro por arma/distância: {arma} {db} · {sit.Substring(0, 1)}"); tot.Soma("POSIÇÃO no 1º tiro, quem segura x quem vem: " + (x.brain.Segurando ? "segurando" : "vindo") + " · " + sit.Substring(0, 1));
                    if (x.brain.Segurando) tot.Soma($"POSIÇÃO de quem segura, pelo que fazia antes de ver: {x.brain.lastMicroOuFase} · {sit.Substring(0, 1)}"); }
            }
            static void Estado(Ep e, Agent x, Agent o, bool atirou)
            {
                var b = x.brain; string k;
                if (atirou) k = "a atirou";
                else if (b.AlvoAgora == null)
                {
                    float ang = MathF.Abs(MathX.AngDiff((o.pos - x.pos).Atan2, x.aim)) * 180 / MathF.PI;
                    k = "b sem alvo · " + (b.LargouAgora(o) ? "larguei ele de propósito" : ang > 55 ? "ele fora do meu campo de visão" : x.scoped && ang > 20 ? "fora da luneta" : "na minha frente e não vejo") + " · " + b.fase;
                }
                else if (b.AlvoAgora != o) k = "c mirando em outro";
                else if (b.travaAgora != null) k = "d " + b.travaAgora;
                else k = "e com alvo, sem atirar · " + (x.gun.Reloading ? "recarregando" : x.gun.inMag == 0 ? "pente vazio" : "entre rajadas") + " · " + b.fase;
                e.estado.TryGetValue(k, out int v); e.estado[k] = v + 1;
            }
            public void Fim() { FimX(); if (g != null) foreach (var p in pend) g.Clipe(p.a, p.b, p.ini, p.ult, p.fim, p.tag, p.dist); }
            void FimX() { foreach (var e in ativos.Values.ToList()) Fecha(e, "round acabou"); foreach (var s in saiu) lock (tot) tot.Soma("saiu e não reabriu em 6 s"); }
        }

        public static int Rodar(int n, int seed0, string saida, int clipes = 0)
        {
            var tot = new Tot(); var todos = new List<(string tag, int chave, string json, string txt)>();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); var j = new Juiz(m, tot, seed0 + i);
                if (clipes > 0) j.g = new Gravador(m, seed0 + i);
                while (!m.over && m.t < 200) { m.Step(); j.Passo(); }
                j.Fim(); lock (tot) { tot.rounds++; if (j.g != null) for (int c = 0; c < j.g.json.Count; c++) todos.Add((j.g.meta[c].tag, j.g.meta[c].chave, j.g.json[c], j.g.texto[c])); }
            });
            var sb = new StringBuilder();
            sb.AppendLine($"DUELO — {n} rounds, sementes {seed0}..{seed0 + n - 1}, exp \"{TeamBrain.EXP}\"");
            sb.AppendLine($"duelos com tiro: {tot.duelos} ({100.0 * tot.duelos / n:0} por 100 rounds) · se viram sem tiro: {tot.viramSemTiro}");
            foreach (var kv in tot.n.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string pct = kv.Key.StartsWith("CENA") || kv.Key.StartsWith("reabriu") || kv.Key.StartsWith("saiu") ? "" : $" = {100.0 * kv.Value / Math.Max(1, tot.duelos):0.0}% dos duelos";
                sb.AppendLine($"  {kv.Key,-78} {kv.Value,6}  ({100.0 * kv.Value / n:0.0}/100 rounds){pct}");
            }
            tot.n.TryGetValue("os dois no mesmo lugar o duelo todo (duelos de 1 s+)", out int p1); tot.n.TryGetValue("duelos de 1 s+", out int d1);
            sb.AppendLine($"  RESUMO: dos duelos de 1 s+, {100.0 * p1 / Math.Max(1, d1):0.0}% com os dois no mesmo lugar o tempo todo");
            foreach (var kv in tot.ex) { sb.AppendLine(); sb.AppendLine("EXEMPLOS — " + kv.Key); foreach (var s in kv.Value) sb.AppendLine("  " + s); }
            if (clipes > 0)
            {   // até `clipes` de cada tipo, em ordem fixa (semente, par)
                var esc = todos.GroupBy(x => x.tag).OrderBy(gp => gp.Key, StringComparer.Ordinal).SelectMany(gp => gp.OrderBy(x => x.chave).Take(clipes)).ToList();
                Directory.CreateDirectory("Logs/duelos");
                File.WriteAllText($"Logs/duelos/{saida}.txt", $"CLIPES DE DUELO — {saida} — exp \"{TeamBrain.EXP}\"\n\n" + string.Join("\n", esc.Select(x => x.txt)));
                string dados = "{\"titulo\":\"" + saida + "\",\"exp\":\"" + TeamBrain.EXP + "\",\"clipes\":[" + string.Join(",", esc.Select(x => x.json)) + "]}";
                File.WriteAllText($"Logs/duelos/{saida}.json", dados);
                if (File.Exists("Tools/duelos/viewer.html")) File.WriteAllText($"Logs/duelos/{saida}.html", File.ReadAllText("Tools/duelos/viewer.html").Replace("/*DADOS*/null", dados));
                sb.AppendLine(); sb.AppendLine($"CLIPES: {esc.Count} em Logs/duelos/{saida}.txt / .html");
            }
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText($"Logs/bots/duelo_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
