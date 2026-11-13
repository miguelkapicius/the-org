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
    /// BANCADA DE CENAS DE COMPORTAMENTO (PLANO_SIMULACAO.md, marco 2) — Headless -cenas N [-seed S] [-exp ...] [-saida nome].
    /// Não mede equilíbrio. Roda rounds normais, reconhece SITUAÇÕES em que existe uma jogada inteligente clara e conta o que o bot
    /// fez nela, com exemplos (semente, tempo, quem) pra abrir no replay. "Passou" = fez a coisa inteligente na maioria das vezes
    /// e os exemplos do resto têm motivo. Só lê o estado.
    ///   C1 quina conhecida: o bot anda e dá de cara com um inimigo que o time SABIA estar ali — bangou antes ou abriu seco?
    ///   C2 linha quente: colega morreu perto pra alguém vivo — o que o bot fez nos 6 s seguintes?
    ///   C3 vantagem: CT com 2+ a mais antes do plant — alguém saiu sozinho pra cima de inimigo conhecido?
    ///   C4 flash própria: jogou flash pra si e... entrou correndo ou ficou andando de shift/parado?
    /// </summary>
    static class Cenas
    {
        class Tot
        {
            public readonly Dictionary<string, int> n = new Dictionary<string, int>(); public readonly Dictionary<string, List<string>> ex = new Dictionary<string, List<string>>(); public int rounds;
            public void Soma(string k, string exemplo = null)
            {
                lock (this) { n.TryGetValue(k, out int v); n[k] = v + 1; if (exemplo != null) { if (!ex.TryGetValue(k, out var l)) ex[k] = l = new List<string>(); if (l.Count < 6) l.Add(exemplo); } }
            }
        }

        class Juiz
        {
            readonly Match m; readonly Tot tot; readonly int seed; readonly int N;
            readonly int[] flashes; readonly float[] jogouFlashT, vistoDesde; readonly bool[] via;
            readonly List<(float t, V2[] pos)> hist = new List<(float, V2[])>(); float proxHist;
            readonly List<(int quem, float t0, V2 p0, V2 mPos, int matador, V2 caiu, bool tinhaFlash, bool comColega)> quentes = new List<(int, float, V2, V2, int, V2, bool, bool)>();
            readonly List<(int quem, float t)> flashPropria = new List<(int, float)>();
            int abatesVistos; readonly float[] saiuVantagemT;

            public Juiz(Match m, Tot tot, int seed)
            {
                this.m = m; this.tot = tot; this.seed = seed; N = m.agents.Count;
                flashes = new int[N]; jogouFlashT = new float[N]; vistoDesde = new float[N]; via = new bool[N * N]; saiuVantagemT = new float[N];
                for (int i = 0; i < N; i++) { jogouFlashT[i] = -99; saiuVantagemT[i] = -99; flashes[i] = m.agents[i].nades.Count(x => x == "flash"); }
            }

            string Q(Agent a) => $"semente {seed} t={m.t:0.0} {a.name} ({(a.IsT ? "TR" : "CT")}, {a.gunId}) em {Places.Region(a.pos)} · ordem '{a.brain.order.name}' · {a.brain.thought}";
            V2 PosHa(int id, float seg) { for (int k = hist.Count - 1; k >= 0; k--) if (hist[k].t <= m.t - seg) return hist[k].pos[id]; return hist.Count > 0 ? hist[0].pos[id] : m.agents[id].pos; }
            bool ColegaPerto(Agent a, float d) { foreach (var b in m.agents) if (b != a && b.alive && b.team == a.team && (b.pos - a.pos).Len < d) return true; return false; }
            bool TimeBangou(Agent a, float seg) { foreach (var b in m.agents) if (b.team == a.team && m.t - jogouFlashT[b.id] < seg && (b.pos - a.pos).Len < 700) return true; return false; }

            public void Passo()
            {
                float t = m.t;
                if (t >= proxHist) { proxHist = t + 0.25f; hist.Add((t, m.agents.Select(a => a.pos).ToArray())); }
                // flash jogada (a contagem na mão caiu)
                foreach (var a in m.agents)
                {
                    int f = a.nades.Count(x => x == "flash");
                    if (f < flashes[a.id] && a.alive) { jogouFlashT[a.id] = t; flashPropria.Add((a.id, t)); }
                    flashes[a.id] = f;
                }
                // C4: 1,2 s depois da flash própria (já estourou), nos 1,5 s seguintes ele está entrando?
                for (int k = flashPropria.Count - 1; k >= 0; k--)
                {
                    var (id, t0) = flashPropria[k]; if (t - t0 < 2.7f) continue; flashPropria.RemoveAt(k);
                    var a = m.agents[id]; if (!a.alive || t < 12) continue;
                    float andou = (a.pos - PosHa(id, 1.5f)).Len; bool temOrdemDeIr = a.brain.order.push || a.brain.order.plant || a.brain.order.dest.Len > 1 && a.brain.order.hold == null;
                    if (!temOrdemDeIr) continue;   // flash pra segurar/recuar não é "pra entrar"
                    tot.Soma(andou > 200 ? "C4 flash própria com ordem de ir: ENTROU correndo" : andou > 60 ? "C4 flash própria com ordem de ir: andou devagar (shift)" : "C4 flash própria com ordem de ir: ficou PARADO", andou > 200 ? null : Q(a));
                }
                // baixas: abre a cena C2 pra quem está perto
                for (; abatesVistos < m.abates.Count; abatesVistos++)
                {
                    var ab = m.abates[abatesVistos]; if (ab.aliado || ab.suicidio || ab.v == null || ab.by == null || !ab.by.alive) continue;
                    foreach (var a in m.agents)
                        if (a.alive && a.team == ab.v.team && a != ab.v && (a.pos - ab.vPos).Len < 420 && a.brain.target == null)
                            quentes.Add((a.id, t, a.pos, ab.byPos, ab.by.id, ab.vPos, a.nades.Contains("flash"), ColegaPerto(a, 220)));
                }
                // C2: fecha 6 s depois (ou na morte dele / do matador)
                for (int k = quentes.Count - 1; k >= 0; k--)
                {
                    var q = quentes[k]; var a = m.agents[q.quem]; var mt = m.agents[q.matador];
                    bool fim = t - q.t0 >= 6f || !a.alive || !mt.alive; if (!fim) continue; quentes.RemoveAt(k);
                    float andou = (a.pos - q.p0).Len, chegou = (q.p0 - q.mPos).Len - (a.pos - q.mPos).Len;
                    bool bangou = TimeBangou(a, t - q.t0 + 0.1f), junto = ColegaPerto(a, 220) || q.comColega;
                    string fez = chegou > 70 ? (bangou ? "foi pra linha DEPOIS de flash" : junto ? "foi pra linha junto de colega, sem flash" : q.tinhaFlash ? "foi pra linha SOZINHO e SECO (tinha flash)" : "foi pra linha SOZINHO e SECO (sem flash)")
                               : andou < 40 ? "ficou no lugar" : chegou < -70 ? "saiu da linha (recuou)" : "mudou de ângulo (de lado)";
                    string res = !a.alive ? "morreu" : !mt.alive ? "matador caiu" : "os dois vivos";
                    string lado = a.IsT ? "TR" : "CT";
                    tot.Soma($"C2 linha quente {lado}: {fez}"); tot.Soma($"C2 linha quente {lado}: {fez} → {res}", fez.Contains("SECO") && !a.alive ? Q(a) + $" · colega caiu {t - q.t0:0.0} s antes em {Places.Region(q.caiu)}" : null);
                }
                // C1 e C3: quem passou a VER um inimigo agora
                foreach (var a in m.agents)
                {
                    if (!a.alive) continue; var it = m.intel[(int)a.team];
                    foreach (var e in m.agents)
                    {
                        if (e.team == a.team || !e.alive) continue; int k = a.id * N + e.id;
                        bool ve = a.brain.target == e; if (ve == via[k]) continue; via[k] = ve; if (!ve || t < 12) continue;
                        // sabia dele? contato do time de antes de eu ver (entre 0,7 e 6 s atrás), perto de onde ele está
                        int kc = (int)a.team * N + e.id;   // o que o time sabia dele ATÉ o passo anterior (antes de eu ver)
                        bool sabia = contT[kc] > 0 && t - contT[kc] < 6f && t - contT[kc] > 0.7f && (contP[kc] - e.pos).Len < 200;
                        V2 antes = PosHa(a.id, 1.5f); float cheguei = (antes - e.pos).Len - (a.pos - e.pos).Len, eleVeio = (PosHa(e.id, 1.5f) - a.pos).Len - (e.pos - a.pos).Len;
                        bool euAbri = cheguei > 60 && eleVeio < 40;   // eu que andei pra cima; ele estava no lugar
                        if (sabia && euAbri && (a.pos - e.pos).Len < 900)
                        {
                            bool flashNaMao = a.nades.Contains("flash"), bang = TimeBangou(a, 3.5f), junto = ColegaPerto(a, 220);
                            string fez = bang ? "abriu DEPOIS de flash" : flashNaMao ? (junto ? "abriu seco com flash no bolso (com colega do lado)" : "abriu SECO e SOZINHO com flash no bolso") : junto ? "abriu seco, sem flash, com colega" : "abriu seco, sem flash, sozinho";
                            tot.Soma("C1 quina conhecida: " + fez, fez.Contains("SOZINHO") ? Q(a) + $" · abriu no {e.name} ({e.gunId}) a {(a.pos - e.pos).Len * 2.9f / 40:0} m" : null);
                        }
                        // C3: CT em vantagem de 2+ antes do plant que foi pra cima sozinho
                        if (!a.IsT && !m.bombPlanted && euAbri && m.Alive(Team.CT) >= m.Alive(Team.T) + 2 && t - saiuVantagemT[a.id] > 8)
                        {
                            saiuVantagemT[a.id] = t; bool junto = ColegaPerto(a, 260); bool obrig = a.brain.order.name.Contains("C4") || a.brain.order.defuse;
                            tot.Soma(junto ? "C3 CT em vantagem (2+): foi pra cima com colega" : obrig ? "C3 CT em vantagem (2+): foi pra cima sozinho (C4 caído)" : "C3 CT em vantagem (2+): foi pra cima SOZINHO", junto || obrig ? null : Q(a) + $" · {m.Alive(Team.CT)}x{m.Alive(Team.T)}");
                        }
                    }
                }
                for (int tm = 0; tm < 2; tm++) foreach (var c in m.intel[tm].contacts.Values) { contT[tm * N + c.id] = c.t; contP[tm * N + c.id] = c.pos; }
                if (!m.bombPlanted && t >= 12 && m.Alive(Team.CT) >= m.Alive(Team.T) + 2 && m.Alive(Team.T) > 0 && !vant) { vant = true; tot.Soma("C3 rounds em que o CT ficou com vantagem de 2+ antes do plant"); }
            }
            bool vant; readonly float[] contT = new float[40]; readonly V2[] contP = new V2[40];
        }

        public static int Rodar(int n, int seed0, string saida)
        {
            var tot = new Tot();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); var j = new Juiz(m, tot, seed0 + i);
                while (!m.over && m.t < 200) { m.Step(); j.Passo(); }
                lock (tot) tot.rounds++;
            });
            var sb = new StringBuilder();
            sb.AppendLine($"CENAS DE COMPORTAMENTO — {n} rounds, sementes {seed0}..{seed0 + n - 1}, exp \"{TeamBrain.EXP}\"");
            string grupo = "";
            foreach (var kv in tot.n.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string g = kv.Key.Substring(0, 2); if (g != grupo) { grupo = g; sb.AppendLine(); }
                sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
            }
            foreach (var kv in tot.ex.OrderBy(k => k.Key, StringComparer.Ordinal)) { sb.AppendLine(); sb.AppendLine("EXEMPLOS — " + kv.Key); foreach (var s in kv.Value) sb.AppendLine("  " + s); }
            System.IO.Directory.CreateDirectory("Logs/bots"); System.IO.File.WriteAllText($"Logs/bots/cenas_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return 0;
        }
    }
}
