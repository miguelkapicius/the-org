using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Validador de humanidade (Headless -humano N [-exp ...] [-saida nome]): assiste cada round e marca as cenas que um pro
    /// nunca faria — as que o Gabriel viu no exe ("olhando pra parede", "pinou e puxou granada", "entrou no bomb e não
    /// plantou", "pegou arma no meio da troca", "travado pensando", "colados", "um de cada vez", "lento na base"). Cada cena
    /// vira episódios por 100 rounds + exemplos (semente, segundo, jogador, região, fase, o que pensava) pra conferir no jogo.
    /// Só lê o estado (oráculo é permitido aqui: é o juiz, não o bot). Grava Logs/bots/humano_&lt;saida&gt;.txt.
    /// </summary>
    static class Humano
    {
        const int AMOSTRA = 8;   // a cada 8 passos = 0,25 s
        const float DT_A = AMOSTRA * Match.DT;

        public static readonly string[] CENAS =
        {
            "mira na parede lisa parado (≥1,5 s, sem alvo; quina não conta)",
            "segurando onde não pode ter ninguém (≥3 s)",
            "C4 no bomb sem plantar (≥3 s, ninguém vendo)",
            "granada com inimigo na cara (sabia dele)",
            "granada exposto (inimigo vendo, não sabia)",
            "pegou arma do chão no meio da troca",
            "travado: ordem de andar e parado (≥3 s)",
            "trocando de ideia toda hora (≥6 fases em 2 s)",
            "colados (dois a <0,5 m por ≥2 s)",
            "um de cada vez (entrou 1,5–6 s depois onde o colega morreu, matador vivo)",
            "troca perdida (foi trocar em <1,5 s e morreu pro mesmo)",
            "foi pegar arma/item do chão com troca acontecendo (≥0,5 s)",
            "morreu indo pegar arma/item do chão",
        };

        class Ex { public int seed; public float t; public string quem, onde, fase, pensa; }
        class Total { public Dictionary<string, int>[] fase = CENAS.Select(_ => new Dictionary<string, int>()).ToArray(); public int[] n = new int[CENAS.Length]; public List<Ex>[] ex = CENAS.Select(_ => new List<Ex>()).ToArray(); public int rounds; public Dictionary<string, float> tempoTR = new Dictionary<string, float>(), baseFase = new Dictionary<string, float>(); public int roundsTR; public Dictionary<string, (double soma, int n)> time = new Dictionary<string, (double, int)>(); public int seqLonga, seqCurta; }

        /// <summary>Estado de um round (um Match).</summary>
        class Juiz
        {
            readonly Match m; readonly Total tot; readonly int seed;
            readonly float[,] desde = new float[10, CENAS.Length];
            readonly bool[,] marcado = new bool[10, CENAS.Length];
            readonly string[] acaoAnt = new string[10], armaAnt = new string[10], primAnt = new string[10];
            readonly Queue<(float t, string f)>[] fases = Enumerable.Range(0, 10).Select(_ => new Queue<(float, string)>()).ToArray();
            readonly float[,] colado = new float[10, 10];
            readonly string[] regAt = new string[10]; readonly float[] regDesde = new float[10];   /* plantado: região atual e desde quando */
            readonly float[] pegandoAnt = { -9, -9, -9, -9, -9, -9, -9, -9, -9, -9 }; readonly bool[] morreuVisto = new bool[10];
            int abatesVistos;
            public readonly Dictionary<string, float> tempoTR = new Dictionary<string, float>(), baseFase = new Dictionary<string, float>();

            public Juiz(Match m, Total tot, int seed)
            {
                this.m = m; this.tot = tot; this.seed = seed;
                for (int i = 0; i < 10; i++) for (int c = 0; c < CENAS.Length; c++) desde[i, c] = -1;
            }

            static readonly bool ReguaRampa = Array.IndexOf(Environment.GetCommandLineArgs(), "-reguaRampa") >= 0;
            readonly List<(float t, V2[] pos)> hist = new List<(float, V2[])>();
            static string Corta9(string s) { if (s == null) return "?"; int p = s.IndexOf('('); s = (p > 0 ? s.Substring(0, p) : s).Trim(); p = s.IndexOf(':'); return p > 0 ? s.Substring(0, p) : s; }
            void Soma(string k, double v) { lock (tot) { tot.time.TryGetValue(k, out var x); tot.time[k] = (x.soma + v, x.n + 1); } }
            readonly Dictionary<int, (float t, string r)> mortoEm = new Dictionary<int, (float, string)>();
            void Marca(int c, Agent a, string extra = "")
            {
                lock (tot)
                {
                    tot.n[c]++; { string fk = a.brain.fase + (c == 6 && extra.Contains("[pós-morte]") ? " [pós-morte]" : "") + (c == 0 && extra != "" ? " | " + extra.Split(" ## ")[0] : ""); tot.fase[c].TryGetValue(fk, out int q0); tot.fase[c][fk] = q0 + 1; }
                    if (tot.ex[c].Count < (c == 0 ? 40 : 8))
                        tot.ex[c].Add(new Ex { seed = seed, t = m.t, quem = $"{a.name} ({(a.IsT ? "TR" : "CT")}, {a.gunId})", onde = Places.Region(a.pos), fase = a.brain.fase + (extra != "" ? " · " + extra : ""), pensa = a.brain.thought });
                }
            }

            /// <summary>Condição contínua: vira episódio quando dura `min` s (conta uma vez por episódio).</summary>
            void Dura(int c, Agent a, bool cond, float min, string extra = "")
            {
                int i = a.id;
                if (!cond) { desde[i, c] = -1; marcado[i, c] = false; return; }
                if (desde[i, c] < 0) desde[i, c] = m.t;
                if (!marcado[i, c] && m.t - desde[i, c] >= min) { marcado[i, c] = true; Marca(c, a, extra); }
            }

            static bool Ve(Agent a, Agent b) => MapData.LosWalk(a.pos, a.z + a.Eye - 18f, b.pos, b.z + b.Eye - 18f, 18f);

            Agent InimigoVendo(Agent a, float raio)
            {
                foreach (var e in m.agents) if (e.alive && e.team != a.team && (e.pos - a.pos).Len < raio && Ve(e, a)) return e;
                return null;
            }

            public void Passo()
            {
                float t = m.t;
                if (hist.Count == 0 || t - hist[hist.Count - 1].t >= 0.25f) { hist.Add((t, m.agents.Select(x => x.pos).ToArray())); if (hist.Count > 40) hist.RemoveAt(0); }
                foreach (var a in m.agents)
                {
                    int i = a.id;
                    if (!a.alive) { if (!morreuVisto[i]) { morreuVisto[i] = true; if (t - pegandoAnt[i] < 0.6f) Marca(12, a); } for (int c = 0; c < CENAS.Length; c++) desde[i, c] = -1; continue; }
                    var b = a.brain; bool livre = b.target == null && b.action == null && m.plantando != a;
                    // 0 mira na parede
                    float d = 6; V2 dir = V2.Ang(a.aim);
                    if (ReguaRampa) d = MapData.RaioHorizonte(a.pos, a.z, dir, 60, a.Eye);   /* régua que segue o relevo: olhar escada acima não é parede */
                    else while (d < 40 && MapData.LosWalk(a.pos, a.z, a.pos + dir * d, a.z, a.Eye)) d += 6;
                    // quina (pre-aim na borda, coisa de pro) não conta: só parede lisa — os dois lados (±10°) também fecham perto
                    bool lisa = false;
                    if (d < 40)
                    {
                        float l1 = ReguaRampa ? MapData.RaioHorizonte(a.pos, a.z, V2.Ang(a.aim + 0.17f), 200, a.Eye) : MapData.Raio(a.pos, a.z, V2.Ang(a.aim + 0.17f), 200, a.Eye), l2 = ReguaRampa ? MapData.RaioHorizonte(a.pos, a.z, V2.Ang(a.aim - 0.17f), 200, a.Eye) : MapData.Raio(a.pos, a.z, V2.Ang(a.aim - 0.17f), 200, a.Eye);
                        lisa = l1 < 60 && l2 < 60;
                    }
                    string dg0 = "";
                    if (lisa && livre && a.Speed < 8)
                    {
                        if (b.alvoMira is V2 am0)
                        {
                            float da = MathF.Abs(MathX.AngDiff((am0 - a.pos).Atan2, a.aim)) * 57.3f;
                            bool ve0 = MapData.LosWalk(a.pos, a.z, am0, NavGrid.ZAt(NavGrid.Snap(am0)), a.Eye);
                            if (da < 8 && !ve0) Soma($"DIAG parede: olho {a.Eye:0} · raio a 18 {(MapData.Raio(a.pos, a.z, V2.Ang(a.aim), 450, 18f) < 40 ? "<40" : "40+")} · raio no olho {(MapData.Raio(a.pos, a.z, V2.Ang(a.aim), 450, a.Eye) < 40 ? "<40" : "40+")} · alvoMira a {((am0 - a.pos).Len < 45 ? "<45" : (am0 - a.pos).Len < 120 ? "45-120" : "120+")} · micro {b.Situacao}", 1);
                            dg0 = (da < 8 ? "mira NO alvoMira" : da < 30 ? "mira perto do alvoMira" : "mira LONGE do alvoMira") + (ve0 ? ", que eu vejo" : ", que NÃO vejo") + $" · fonte '{b.fonteMira}'";
                            float a0 = (am0 - a.pos).Atan2;
                            dg0 += $" ## pos {a.pos.x:0},{a.pos.y:0} z {a.z:0} alvo {am0.x:0},{am0.y:0} zAlvo {NavGrid.ZAt(NavGrid.Snap(am0)):0} dist {(am0 - a.pos).Len:0} · raios(18) no alvo {MapData.Raio(a.pos, a.z, V2.Ang(a0), 450, 18f):0} +10° {MapData.Raio(a.pos, a.z, V2.Ang(a0 + 0.17f), 200, 18f):0} -10° {MapData.Raio(a.pos, a.z, V2.Ang(a0 - 0.17f), 200, 18f):0} · na mira {MapData.Raio(a.pos, a.z, V2.Ang(a.aim), 450, 18f):0} · da {da:0.0}";
                        }
                        else dg0 = $"sem alvoMira · fonte '{b.fonteMira}'";
                    }
                    Dura(0, a, livre && a.Speed < 8 && lisa && t > 3 && !(ReguaRampa && (b.fase == "cego" || b.fase == "dando pezinho")), 1.5f, dg0);   /* régua nova: cego virado pro canto e quem dá pezinho de cara pra parede não são erro */
                    // 1 segurando onde não pode ter ninguém: a crença do time pro lado da mira ~0, e outro lado com gente
                    var it = m.intel[(int)a.team];
                    if (livre && a.Speed < 8 && t > 20 && b.order.hold != null)
                    {
                        float alc = MathF.Min(MapData.Raio(a.pos, a.z, dir, 600, a.Eye), 600);
                        float aqui = it.Esperados(a.pos + dir * MathF.Max(60, alc * 0.8f), 160), melhor = 0;
                        for (int k = 0; k < 8; k++) { V2 dk = V2.Ang(k * 0.785f); float ak = MathF.Min(MapData.Raio(a.pos, a.z, dk, 600, a.Eye), 600); if (ak > 80) melhor = MathF.Max(melhor, it.Esperados(a.pos + dk * ak * 0.8f, 160)); }
                        Dura(1, a, aqui < 0.05f && melhor > 0.6f, 3f, $"crença na mira {aqui:0.00} x {melhor:0.00} noutro lado");
                    }
                    else Dura(1, a, false, 3f);
                    // 2 C4 no bomb sem plantar
                    bool noBomb = false;
                    if (a.hasBomb && !m.bombPlanted) foreach (var s in MapData.SITES.Values) if ((s.Center - a.pos).Len < 150) noBomb = true;
                    Dura(2, a, noBomb && m.plantando != a && b.target == null && InimigoVendo(a, 700) == null, 3f);
                    // 3/4 granada: começou agora
                    if (b.action == "granada" && acaoAnt[i] != "granada")
                    {
                        var e = InimigoVendo(a, 900);
                        string k = b.actionNade?.kind ?? "?";
                        bool sabia = t - b.lastSeenT < 1.5f && (b.lastSeenPos - a.pos).Len < 600;
                        if (e != null && sabia && (e.pos - a.pos).Len < 600) Marca(3, a, $"{k}, {e.name} a {(e.pos - a.pos).Len * 2.9f / 40:0} m");
                        else if (e != null) Marca(4, a, $"{k}, {e.name} a {(e.pos - a.pos).Len * 2.9f / 40:0} m");
                    }
                    acaoAnt[i] = b.action;
                    // 5 pegou arma do chão no meio da troca
                    if (armaAnt[i] != null && a.gunId != armaAnt[i])
                    {
                        bool pegou = m.drops.Any(x => x.pegouPor == a && t - x.pegouT < DT_A + 0.01f);
                        bool troca = t - b.lastSeenT < 3f && (b.lastSeenPos - a.pos).Len < 600 || InimigoVendo(a, 600) != null;
                        if (pegou && troca) Marca(5, a, $"{armaAnt[i]} → {a.gunId}{(primAnt[i] == null ? " (só tinha pistola)" : "")}");
                    }
                    armaAnt[i] = a.gunId; primAnt[i] = a.Primaria;
                    // 6 travado: tem pra onde ir e está parado
                    bool temQueIr = b.order.hold == null && (b.order.dest - a.pos).Len > 60 && !b.order.paciente && !b.order.waitSmoke && t > b.order.notBefore;
                    bool posMorte = m.abates.Any(x => !x.aliado && x.v != null && x.v.team == a.team && t - x.t < 8f && (x.v.pos - a.pos).Len < 1100);   /* colega caiu perto há pouco: o travado vem de não trocar? */
                    Dura(6, a, livre && temQueIr && a.Speed < 5 && t > 3, 3f, $"ordem '{b.order.name}'" + (posMorte ? " [pós-morte]" : ""));
                    // 7 trocando de ideia
                    /* o duelo é uma decisão só: atirar, levar tiro, segurar onde ele sumiu, voltar pra cobertura e reabrir são o
                       próprio duelo (o pro faz isso em 2 s). "Trocar de ideia" é mudar de plano fora dele (principal, 2026-11-09,
                       lendo exemplos: base e teste tinham "combate › levei tiro › combate" contado como indecisão) */
                    string fz = b.fase == "combate" || b.fase == "levei tiro" || b.fase == "segurando onde o inimigo sumiu" || b.fase == "voltando pra cobertura"
                                || b.fase == "saindo do duelo" || b.fase.StartsWith("duelo:") ? "duelando" : b.fase;
                    var q = fases[i]; if (q.Count == 0 || q.Last().f != fz) q.Enqueue((t, fz));
                    while (q.Count > 0 && t - q.Peek().t > 2f) q.Dequeue();
                    Dura(7, a, q.Count >= 6, 0f, string.Join(" › ", q.Select(x => x.f)));
                    // 11/12 indo pegar coisa do chão com troca acontecendo (o que o oráculo sabe: inimigo vivo a < 650 visto pelo time há < 4 s, ou tiro levado há < 3 s)
                    bool pegando = b.fase == "pegando arma" || b.fase == "pegando de passagem" || b.fase == "pegando o drop" || b.fase == "pegando granada";
                    bool emTroca = b.target != null || t - a.apanhouEm < 3f;
                    if (!emTroca) foreach (var c in it.contacts.Values) if (m.agents[c.id].alive && t - c.t < 4f && (c.pos - a.pos).Len < 650) { emTroca = true; break; }
                    Dura(11, a, pegando && emTroca, 0.5f);
                    pegandoAnt[i] = pegando ? t : pegandoAnt[i];
                    // TR: tempo por área nos primeiros 40 s (comparado com o pro)
                    if (a.IsT && t < 40 && !m.bombPlanted)
                    {
                        string ar = MapData.AreaAt(a.pos.x, a.pos.y);
                        if (ar != null) { tempoTR.TryGetValue(ar, out float v); tempoTR[ar] = v + DT_A; }
                        if (ar == "TR" && t > 10) { string fk = b.fase + " | " + b.order.name; baseFase.TryGetValue(fk, out float w); baseFase[fk] = w + DT_A; }
                    }
                }
                // plantado (sim-apoio 2026-11-09): há quanto tempo cada vivo está na região atual; pro (de_mirage) 4 s+ 0,69/0,67 · 8 s+ 0,49/0,47 · 15 s+ 0,29/0,30
                foreach (var a in m.agents)
                {
                    if (!a.alive) continue; var r = RegiaoPro(a.pos); if (r == null) continue;
                    if (regAt[a.id] != r) { regAt[a.id] = r; regDesde[a.id] = t; }
                }
                if (((int)(t / DT_A)) % 2 == 0 && t >= 5 && t < 90 && !m.bombPlanted)
                    foreach (var a in m.agents)
                    {
                        if (!a.alive || regAt[a.id] == null) continue; string L = a.IsT ? "T" : "CT"; float ida = t - regDesde[a.id];
                        Soma($"{L}_na_regiao_ha_4s_ou_mais", ida >= 4 ? 1 : 0); Soma($"{L}_na_regiao_ha_8s_ou_mais", ida >= 8 ? 1 : 0); Soma($"{L}_na_regiao_ha_15s_ou_mais", ida >= 15 ? 1 : 0);
                    }
                // régua do pro: espalhamento do time por região (antes do plant, 5–90 s, ≥3 vivos)
                if (((int)(t / DT_A)) % 2 == 0 && t >= 5 && t < 90 && !m.bombPlanted)
                    foreach (var lado in new[] { Team.T, Team.CT })
                    {
                        var regs = new List<string>(); foreach (var a in m.agents) if (a.alive && a.team == lado) { var r = RegiaoPro(a.pos); if (r != null) regs.Add(r); }
                        if (regs.Count < 3) continue;
                        var cnt = regs.GroupBy(x => x).Select(g => g.Count()).ToList(); int mg = cnt.Max();
                        string fase = t < 25 ? "cedo" : t < 50 ? "meio" : "tarde", L = lado == Team.T ? "T" : "CT";
                        Soma($"{L}_regioes_distintas_por_vivo_{fase}", (double)cnt.Count / regs.Count); Soma($"{L}_maior_grupo_{fase}", mg); Soma($"{L}_3mais_mesma_regiao_{fase}", mg >= 3 ? 1 : 0);
                    }
                // 8 colados
                foreach (var a in m.agents)
                    foreach (var o in m.agents)
                    {
                        if (o.id <= a.id || !a.alive || !o.alive || a.team != o.team) { colado[a.id, o.id] = -1; continue; }
                        bool c = (a.pos - o.pos).Len < 7 && t > 15;
                        if (!c) { colado[a.id, o.id] = -1; continue; }
                        if (colado[a.id, o.id] < 0) colado[a.id, o.id] = t;
                        else if (colado[a.id, o.id] < 9000 && t - colado[a.id, o.id] >= 2f) { colado[a.id, o.id] = 9999; Marca(8, a, $"com {o.name}"); }
                    }
                foreach (var a in m.agents)
                    if (!a.alive && !mortoEm.ContainsKey(a.id))
                    {
                        string r = RegiaoPro(a.pos); mortoEm[a.id] = (t, r);
                        float melhor = -1; foreach (var kv in mortoEm) { if (kv.Key == a.id || m.agents[kv.Key].team != a.team) continue; if (kv.Value.r == r && t - kv.Value.t <= 6 && kv.Value.t > melhor) melhor = kv.Value.t; }
                        if (melhor >= 0) lock (tot) { if (t - melhor < 1.5f) tot.seqCurta++; else tot.seqLonga++; }
                    }
                // 9 um de cada vez: morte nova perto de onde um colega morreu há <6 s, e quem matou o colega segue vivo (sem troca)
                for (; abatesVistos < m.abates.Count; abatesVistos++)
                {
                    var ab = m.abates[abatesVistos]; if (ab.aliado || ab.suicidio || ab.v == null) continue;
                    for (int j = abatesVistos - 1; j >= 0 && ab.by != null; j--)
                    {   // 2º duelo: o matador de um colega morre 1,5-6 s depois pra alguém do time do morto = troca tardia GANHA
                        var an = m.abates[j]; if (ab.t - an.t > 6f) break;
                        if (an.v == null || an.aliado || an.by != ab.v || ab.by.team != an.v.team) continue;
                        float gap = ab.t - an.t; string arm = ab.by.gunId == "awp" ? "awp" : "outra";
                        Soma($"UM5 2º duelo {(ab.by.IsT ? "TR" : "CT")} troca {(gap < 1.5f ? "rápida (<1,5 s)" : "tardia (1,5-6 s)")}: GANHOU", 1);
                        if (gap >= 1.5f) Soma($"UM6 tardia GANHOU · matador tinha {an.by.hp} hp? · dist {((ab.by.pos - ab.v.pos).Len < 300 ? "<300" : (ab.by.pos - ab.v.pos).Len < 700 ? "300-700" : "700+")}", 1);
                        break;
                    }
                    for (int j = abatesVistos - 1; j >= 0; j--)
                    {
                        var an = m.abates[j]; if (ab.t - an.t > 6f) break;
                        if (an.v == null || an.aliado || an.v.team != ab.v.team || an.by == null) continue;
                        if ((an.v.pos - ab.v.pos).Len < 300 && an.by.alive && (ab.by == an.by || (ab.by != null && (ab.by.pos - an.by.pos).Len < 200)))
                        {
                            Marca(ab.t - an.t < 1.5f ? 10 : 9, ab.v, $"{an.v.name} caiu {ab.t - an.t:0.0} s antes; {an.by.name} vivo");
                            Soma($"UM5 2º duelo {(ab.v.IsT ? "TR" : "CT")} troca {(ab.t - an.t < 1.5f ? "rápida (<1,5 s)" : "tardia (1,5-6 s)")}: PERDEU", 1);
                            if (ab.t - an.t >= 1.5f && ab.by != null) Soma($"UM6 tardia PERDEU · dist {((ab.by.pos - ab.v.pos).Len < 300 ? "<300" : (ab.by.pos - ab.v.pos).Len < 700 ? "300-700" : "700+")} · matador {(ab.by == an.by ? "o MESMO" : "outro")} com {(ab.by.hp >= 90 ? "90+ hp" : ab.by.hp >= 50 ? "50-89 hp" : "<50 hp")}", 1);
                            if (ab.t - an.t >= 1.5f)
                            {   // placar de vivos na hora (o morto já não conta): meu time tinha vantagem e mesmo assim foi no 1x1?
                                int meus = m.agents.Count(x => x.alive && x.team == ab.v.team) + 1, deles = m.agents.Count(x => x.alive && x.team != ab.v.team);
                                string vt = meus > deles ? "meu time em VANTAGEM" : meus == deles ? "igual" : "em desvantagem";
                                string rel = ab.v.IsT ? (m.bombPlanted ? "C4 plantado" : m.RoundLeft > 40 ? "com tempo (40 s+)" : "sem tempo") : (m.bombPlanted ? "C4 plantado" : "antes do plant");
                                Soma($"UM7 {(ab.v.IsT ? "TR" : "CT")} perdeu a tardia · {vt} · {rel}", 1);
                                Soma($"UM8 {(ab.v.IsT ? "TR" : "CT")} perdeu a tardia · {vt} · {rel} · {meus}x{deles}", 1);
                            }
                            if (ab.t - an.t >= 1.5f)
                            {   // o que o 2º fez entre a morte do colega e a dele: ficou, foi pra cima do matador, ou recuou
                                var h0 = hist.LastOrDefault(h => h.t <= an.t + 0.01f); if (h0.pos == null) h0 = hist[0];
                                V2 p0 = h0.pos[ab.v.id], k0 = h0.pos[an.by.id];
                                float andou = (ab.v.pos - p0).Len, chegou = (p0 - k0).Len - (ab.v.pos - an.by.pos).Len;
                                string oq = andou < 40 ? "a ficou no lugar (<3 m)" : chegou > 55 ? "b FOI PRA CIMA do matador (4 m+ mais perto)" : chegou < -55 ? "c recuou" : "d andou de lado";
                                Soma($"UM DE CADA VEZ {(ab.v.IsT ? "TR" : "CT")}: {oq} · estava a {((p0 - an.v.pos).Len < 200 ? "<15 m do colega" : "15 m+ do colega")} · antes do duelo: {ab.v.brain.Situacao} · ordem {Corta9(ab.v.brain.order.name)}", 1);
                                Soma($"UM DE CADA VEZ {(ab.v.IsT ? "TR" : "CT")}: {oq}", 1);
                                if (ab.by != null)
                                {
                                    float dm = MathF.Abs(MathX.AngDiff((ab.by.pos - ab.v.pos).Atan2, ab.v.aim)) * 57.3f;
                                    string mir = dm < 12 ? "mirava NELE" : dm < 50 ? "mirava perto (12-50°)" : "mirava LONGE (50°+)";
                                    string kv = (ab.by.pos - h0.pos[ab.by.id]).Len > 40 ? "matador VEIO" : "matador parado";
                                    Soma($"UM2 {(ab.v.IsT ? "TR" : "CT")}: {oq.Split(' ')[0]} · {mir} · {kv}", 1);
                                    Soma($"UM3 {(ab.v.IsT ? "TR" : "CT")}: {oq.Split(' ')[0]} · {mir} · {kv} · fase {ab.v.brain.fase} · {Corta9(ab.v.brain.order.name)}", 1);
                                    Soma($"UM4 geral: {mir} · {kv}", 1);
                                }
                                if (ab.v.brain.order.name.StartsWith("pegar o bomb"))
                                    Console.Error.WriteLine($"C4DIAG seed {seed} t={m.t:0.0} {ab.v.name} {oq} · ordem '{ab.v.brain.order.name}' ({ab.v.brain.order.why}) notBefore {ab.v.brain.order.notBefore:0.0} waitSmoke {ab.v.brain.order.waitSmoke} · fase '{ab.v.brain.fase}' · colega {an.v.name} caiu {ab.t - an.t:0.0}s antes, levava C4? dist colega-C4 {(an.v.pos - m.droppedBomb).Len:0} · eu-C4 {(ab.v.pos - m.droppedBomb).Len:0} (era {(p0 - m.droppedBomb).Len:0}) · matador {ab.by?.name} a {(ab.v.pos - (ab.by?.pos ?? ab.v.pos)).Len:0} · {ab.v.brain.thought}");
                            }
                            break;
                        }
                    }
                }
            }
        }


        // ---- régua do pro (Tools/pro/regua.py): o time medido nas MESMAS regiões do pro (as 23 áreas da Vila traduzidas da Mirage)
        static readonly string[] REG_PRO = { "TR", "CT", "Rampa_baixo", "Hall_palacio", "Palacio_sala", "Sacada_A", "A", "Esc_plataforma", "Conector_fim", "Conector",
            "Meio", "Topo_meio", "Topo_entrada", "Rato", "Esc_rato", "Janelao", "Curta_sobe", "Mercado", "Rua_B", "Casarao", "Casarao_corredor", "B", "Viela" };
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> regDe = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        static string RegiaoPro(V2 p)
        {
            string a = MapData.AreaAt(p.x, p.y); if (a == null) return null;
            return regDe.GetOrAdd(a, x =>
            {
                if (Array.IndexOf(REG_PRO, x) >= 0) return x;
                V2 c = Estrategias.Centro(x); string best = null; float bd = float.MaxValue;
                foreach (var r in REG_PRO) { float dd = (Estrategias.Centro(r) - c).Len; if (dd < bd) { bd = dd; best = r; } }
                return best;
            });
        }
        public static int Rodar(int n, int seed0, string saida)
        {
            var tot = new Total(); var sw = System.Diagnostics.Stopwatch.StartNew(); int feitos = 0;
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, i =>
            {
                var m = new Match(seed0 + i); var j = new Juiz(m, tot, seed0 + i); int passo = 0;
                while (!m.over && m.t < 200) { m.Step(); if (++passo % AMOSTRA == 0) j.Passo(); }
                lock (tot)
                {
                    tot.rounds++; tot.roundsTR++;
                    foreach (var kv in j.tempoTR) { tot.tempoTR.TryGetValue(kv.Key, out float v); tot.tempoTR[kv.Key] = v + kv.Value; }
                    foreach (var kv in j.baseFase) { tot.baseFase.TryGetValue(kv.Key, out float v); tot.baseFase[kv.Key] = v + kv.Value; }
                }
                int f = Interlocked.Increment(ref feitos); if (f % 50 == 0) Console.WriteLine($"{f}/{n} rounds ({sw.Elapsed.TotalSeconds:0} s)");
            });
            var sb = new StringBuilder();
            sb.AppendLine($"HUMANO {n} rounds (seed {seed0}) {TeamBrain.EXP} — cenas que um pro não faz, por 100 rounds");
            double soma = 0;
            for (int c = 0; c < CENAS.Length; c++) { double r = 100.0 * tot.n[c] / Math.Max(1, tot.rounds); soma += r; sb.AppendLine($"  {r,7:0.0}  {CENAS[c]}"); }
            sb.AppendLine($"  {soma,7:0.0}  TOTAL (cenas por 100 rounds)");
            // TR lento na base: tempo médio do time por área nos 40 s, contra o pro (banco)
            sb.AppendLine(); sb.AppendLine("RÉGUA DO PRO (Tools/pro/regua.json, de_mirage, compra cheia) — nós x pro");
            Dictionary<string, double> regua = new Dictionary<string, double>();
            try { var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText("Tools/pro/regua.json")); foreach (var pr in doc.RootElement.GetProperty("de_mirage").EnumerateObject()) if (pr.Value.ValueKind == System.Text.Json.JsonValueKind.Number) regua[pr.Name] = pr.Value.GetDouble(); } catch { }
            foreach (var kv in tot.time.OrderBy(k => k.Key)) { regua.TryGetValue(kv.Key, out double pv); sb.AppendLine($"  {kv.Key,-36} nós {kv.Value.soma / Math.Max(1, kv.Value.n),6:0.000} (n {kv.Value.n}) · pro {pv,6:0.000}"); }
            regua.TryGetValue("seq_mesma_regiao_1.5-6_por100", out double pl); regua.TryGetValue("seq_mesma_regiao_lt1.5_por100", out double pc);
            sb.AppendLine($"  morreu na região do colega 1,5–6 s depois  nós {100.0 * tot.seqLonga / Math.Max(1, tot.rounds),6:0.0}/100 rounds · pro {pl,6:0.0}");
            sb.AppendLine($"  morreu na região do colega <1,5 s depois   nós {100.0 * tot.seqCurta / Math.Max(1, tot.rounds),6:0.0}/100 rounds · pro {pc,6:0.0}");
            sb.AppendLine(); sb.AppendLine("TR: segundos do time por área nos primeiros 40 s — nós x pro (banco, compra cheia)");
            var pro = TempoProTR();
            foreach (var kv in tot.tempoTR.OrderByDescending(kv => kv.Value).Take(14))
            {
                double nos = kv.Value / Math.Max(1, tot.roundsTR); pro.TryGetValue(kv.Key, out double p);
                sb.AppendLine($"  {Estrategias.Callout(kv.Key),-24} nós {nos,5:0.0} s · pro {p,5:0.0} s{(nos > p * 1.6 + 2 ? "   << demais" : "")}");
            }
            sb.AppendLine(); sb.AppendLine("TR na base depois dos 10 s: segundos por round, por fase | ordem");
            foreach (var kv in tot.baseFase.OrderByDescending(kv => kv.Value).Take(12)) sb.AppendLine($"  {kv.Value / Math.Max(1, tot.roundsTR),5:0.0} s  {kv.Key}");
            sb.AppendLine();
            for (int c = 0; c < CENAS.Length; c++)
            {
                if (tot.ex[c].Count == 0) continue;
                sb.AppendLine($"== {CENAS[c]}");
                sb.AppendLine("   por fase: " + string.Join(" · ", tot.fase[c].OrderByDescending(kv => kv.Value).Take(60).Select(kv => $"{kv.Key} {100.0 * kv.Value / Math.Max(1, tot.n[c]):0}%")));
                if (c == 6) { int pm = tot.fase[c].Where(kv => kv.Key.EndsWith("[pós-morte]")).Sum(kv => kv.Value); sb.AppendLine($"   pós-morte (colega caiu a <1100 há <8 s): {100.0 * pm / Math.Max(1, tot.n[c]):0.0}% = {100.0 * pm / Math.Max(1, tot.rounds):0.0}/100 rounds"); }
                foreach (var e in tot.ex[c]) sb.AppendLine($"   seed {e.seed} {e.t,5:0.0}s  {e.quem} em {e.onde} · fase '{e.fase}' · pensava: {e.pensa}");
            }
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText($"Logs/bots/humano_{saida}.txt", sb.ToString());
            Console.WriteLine(sb.ToString().Split('\n').Take(CENAS.Length + 2).Aggregate((x, y) => x + "\n" + y));
            Console.WriteLine($"gravado Logs/bots/humano_{saida}.txt ({sw.Elapsed.TotalSeconds:0} s)");
            return 0;
        }

        /// <summary>Tempo médio do time TR em cada área nos primeiros 40 s, pelas rotas pro do banco (compra cheia).</summary>
        static Dictionary<string, double> TempoProTR()
        {
            var d = new Dictionary<string, double>(); int n = 0;
            foreach (var p in Estrategias.Todos)
            {
                if (!p.tr || p.compra != "full") continue; n++;
                for (int k = 0; k < 5; k++) foreach (var ps in p.js[k].passos) { float s = MathF.Min(ps.ate, 40) - MathF.Max(ps.de, 0); if (s > 0) { d.TryGetValue(ps.area, out double v); d[ps.area] = v + s; } }
            }
            foreach (var k in d.Keys.ToList()) d[k] /= Math.Max(1, n);
            return d;
        }
    }
}
