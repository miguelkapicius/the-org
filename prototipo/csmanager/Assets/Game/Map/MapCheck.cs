using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.Map
{
    /// <summary>
    /// Régua do mapa (equilíbrio): tempos correndo spawn -> bombs, vantagem do CT em cada bomb, entradas de cada
    /// bomb, largura das ruas e linhas de visão compridas (rua reta e longa favorece o CT/AWP). Alvos: CT chega 3-6 s
    /// antes do TR; TR leva 10+ s até um bomb, CT 5+ s; rua 32-46; linha de visão: segurar bomb/lado CT até 340,
    /// rua/meio até 260 (o janelão é exceção de propósito).
    /// </summary>
    public static class MapCheck
    {
        public const float RUN = 62.5f;   // boneco de 25 correndo ~2,5 alturas/s (mapa na escala de 25 por quadradinho)   // unidades/s (~250 do CS com 1 unidade ~ 5)
        static readonly HashSet<string> T_SIDE = new HashSet<string> { "TR", "Rua_B", "Beco_casarao", "Rampa_saida", "Rampa_baixo", "Rampa_dobra", "Topo_meio", "Hall_palacio", "Esc_palacio", "Esc_casarao", "Casarao", "Casarao_corredor", "Casarao_dobra", "Palacio", "Palacio_sala", "Palacio_corredor" };
        static readonly HashSet<string> CT_SIDE = new HashSet<string> { "CT", "Mercado", "Descida_mercado", "Rua_CT", "Descida_CT", "Esc_plataforma", "Praca", "Esc_janelao", "Janelao", "Viela", "Passagem_viela", "Esc_viela", "Esc_rato" };

        static V2 Mean(V2[] ps) { V2 s = V2.Zero; foreach (var p in ps) s = s + p; return s / ps.Length; }

        public struct Sight { public float d, limit; public string a, b; public V2 p, q; public bool sniper; }

        static bool Sniper(string a) => a == "Janela" || a == "Janelao";
        // a rua do meio é a linha de AWP de propósito: janelão e praça (CT) x meio e entrada do topo (TR)
        static readonly HashSet<string> MID_LANE = new HashSet<string> { "Meio", "Praca", "Esc_janelao", "Topo_entrada", "Curta", "Conector" };
        // aceitos de propósito (como na Mirage): o lado CT vê os próprios bombs; do casarão/tapete (alto) se vê o B até a entrada do CT
        static bool Aceito(string a, string b) =>
            (CT_SIDE.Contains(a) && SiteOf(b) != null) || (CT_SIDE.Contains(b) && SiteOf(a) != null) ||
            ((a.StartsWith("Casarao") || a == "Tapete") && (CT_SIDE.Contains(b) || SiteOf(b) == "B" || b == "Rato")) ||
            ((b.StartsWith("Casarao") || b == "Tapete") && (CT_SIDE.Contains(a) || SiteOf(a) == "B" || a == "Rato"));   // tapete (TR) vigia a boca do rato, que é flanco do próprio TR

        /// <summary>Bomb a que a área pertence (plataforma/banca e suas escadas são parte do bomb), ou null.</summary>
        public static string SiteOf(string a) => a == "A" || a == "Plataforma" ? "A" : a == "B" || a == "Banca" || a == "Esc_banca" || a == "Carroca" || a == "Carroca_degrau" ? "B" : null;

        /// <summary>Linhas de visão acima do limite (amostra do chão a cada "step"), a mais longa de cada par de áreas.</summary>
        public static List<Sight> Violations(int step = 10)
        {
            var pts = new List<(V2 p, float z, string a)>();
            for (int x = step / 2; x < NX; x += step + 2)
                for (int y = step / 2; y < NY; y += step + 2)
                    if (walk[x, y] && coverH[x, y] == 0) pts.Add((new V2(x, y), floor[x, y], AreaAt(x, y)));
            var found = new List<Sight>();
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                {
                    var (p, zp, ap) = pts[i]; var (q, zq, aq) = pts[j];
                    if ((ap == aq && (ap == "TR" || ap == "CT" || ap == "Praca" || ap == "Meio")) || (SiteOf(ap) != null && SiteOf(ap) == SiteOf(aq))) continue;
                    // linha dentro do próprio lado (spawn e rotas internas de um time) não decide duelo entre times
                    if ((T_SIDE.Contains(ap) && T_SIDE.Contains(aq)) || (CT_SIDE.Contains(ap) && CT_SIDE.Contains(aq))) continue;
                    float d = V2.Dist(p, q);
                    if (d < 250 || d > 525) continue;
                    bool sniper = Sniper(ap) || Sniper(aq) || (MID_LANE.Contains(ap) && MID_LANE.Contains(aq)) || Aceito(ap, aq);
                    bool hold = SiteOf(ap) != null || SiteOf(aq) != null || CT_SIDE.Contains(ap) || CT_SIDE.Contains(aq);
                    float limit = sniper ? 999 : hold ? 425 : 325;   // 340/260 na escala antiga (x1,25)
                    if (d <= limit && !sniper) continue;
                    if (LosWalk(p, zp, q, zq, 18)) found.Add(new Sight { d = d, limit = limit, a = ap, b = aq, p = p, q = q, sniper = sniper });
                }
            return found.GroupBy(l => string.CompareOrdinal(l.a, l.b) < 0 ? l.a + "|" + l.b : l.b + "|" + l.a)
                        .Select(g => g.OrderByDescending(l => l.d).First()).OrderByDescending(l => l.d).ToList();
        }

        /// <summary>
        /// Conserta linhas de visão sozinho: pega a pior, acha um ponto dela dentro de uma rua (não bomb/praça/escada)
        /// e põe uma casa avançando da calçada até cobrir esse ponto, sem deixar a rua com menos de 32. Devolve as
        /// chamadas House(...) pra fixar no MapData.
        /// </summary>
        public static List<string> AutoChicanes(int maxIter, StringBuilder log)
        {
            var placed = new List<(V2 c, int side, string lane)>();
            var code = new List<string>();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var skip = new HashSet<string>();
            for (int it = 0; it < maxIter; it++)
            {
                var v = Violations(12).Where(s => !s.sniper && !skip.Contains(s.a + "|" + s.b)).ToList();
                if (v.Count == 0) break;
                var s0 = v[0];
                bool done = false;
                foreach (float t in new[] { 0.5f, 0.45f, 0.55f, 0.4f, 0.6f, 0.35f, 0.65f, 0.3f, 0.7f, 0.25f, 0.75f, 0.2f, 0.8f, 0.15f, 0.85f })
                {
                    V2 q = s0.p + (s0.q - s0.p) * t;
                    string an = AreaAt(q.x, q.y);
                    var sh = an == null ? null : areas.Find(a => a.name == an && a.width > 0 && a.pts != null);
                    if (sh == null || sh.stairs) continue;
                    // ponto mais perto na polilinha da rua: centro, direção, normal
                    float best = float.MaxValue; V2 c = default, dir = default;
                    for (int i = 1; i < sh.pts.Length; i++)
                    {
                        V2 a = sh.pts[i - 1], b = sh.pts[i], ab = b - a;
                        float u = Math.Clamp((q - a).Dot(ab) / MathF.Max(ab.Dot(ab), 1e-6f), 0, 1);
                        V2 pp = a + ab * u;
                        float dd = V2.Dist(q, pp);
                        if (dd < best) { best = dd; c = pp; dir = ab / ab.Len; }
                    }
                    V2 n = new V2(-dir.y, dir.x);
                    float o = (q - c).Dot(n), r = sh.width / 2f;
                    int side = o >= 0 ? 1 : -1;
                    float deep = r - MathF.Abs(o) + 4;
                    if (deep > 2 * r - 32 || deep < 4) continue;
                    V2 hc = c + n * side * (r - deep / 2 + 1);
                    if (placed.Any(pl => pl.lane == an && pl.side != side && V2.Dist(pl.c, hc) < 26)) continue;
                    House(hc.x, hc.y, dir.x, dir.y, 12, deep + 2);
                    placed.Add((hc, side, an));
                    code.Add(string.Format(ci, "House({0:F0}, {1:F0}, {2:F2}f, {3:F2}f, 12, {4:F0});   // {5} x {6}", hc.x, hc.y, dir.x, dir.y, deep + 2, CALLOUT[s0.a], CALLOUT[s0.b]));
                    log?.AppendLine($"  casa na {CALLOUT[an]} pra cortar {CALLOUT[s0.a]} <-> {CALLOUT[s0.b]} ({s0.d:F0})");
                    done = true;
                    break;
                }
                if (!done) { skip.Add(s0.a + "|" + s0.b); log?.AppendLine($"  sem rua pra cortar {CALLOUT[s0.a]} <-> {CALLOUT[s0.b]} ({s0.d:F0})"); }
            }
            return code;
        }

        const float VAO_MIN = 30;
        static readonly HashSet<string> UM_BONECO = new HashSet<string> { "Rato", "Esc_rato", "Rato_fundo" };

        /// <summary>Vão livre de cada área, do mais estreito pro mais largo.</summary>
        public static List<(string nome, float w)> Vaos()
        {
            int W = NavGrid.NGX, H = NavGrid.NGY;
            var mv = new bool[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) mv[x, y] = NavGrid.Movable(NavGrid.Center(x, y));
            var rx = new int[W, H]; var ry = new int[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W;) { if (!mv[x, y]) { x++; continue; } int e = x; while (e < W && mv[e, y]) e++; for (int i = x; i < e; i++) rx[i, y] = e - x; x = e; }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H;) { if (!mv[x, y]) { y++; continue; } int e = y; while (e < H && mv[x, e]) e++; for (int i = y; i < e; i++) ry[x, i] = e - y; y = e; }
            var por = new Dictionary<string, List<float>>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!mv[x, y]) continue;
                    V2 c = NavGrid.Center(x, y);
                    string a = AreaAt(c.x, c.y);
                    if (a == null) continue;
                    if (!por.TryGetValue(a, out var l)) por[a] = l = new List<float>();
                    l.Add(Math.Min(rx[x, y], ry[x, y]) * NavGrid.CELL + 2 * NavGrid.RADIUS);
                }
            var r = new List<(string, float)>();
            foreach (var kv in por) { kv.Value.Sort(); r.Add((kv.Key, kv.Value[(int)(kv.Value.Count * 0.1f)])); }
            return r.OrderBy(v => v.Item2).ToList();
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            V2 t = Mean(SPAWN_T), ct = Mean(SPAWN_CT);
            int bad = 0;
            void Line(bool ok, string s) { if (!ok) bad++; sb.AppendLine((ok ? "  ok  " : "  XX  ") + s); }
            sb.AppendLine("TEMPOS (correndo, segundos)");
            foreach (var kv in SITES)
            {
                float tt = NavGrid.PathLen(t, kv.Value.Center) / RUN, tc = NavGrid.PathLen(ct, kv.Value.Center) / RUN;
                Line(tt >= 10 && tc >= 5 && tt - tc >= 3 && tt - tc <= 6.5f,
                     $"bomb {kv.Key}: TR {tt:F1} s, CT {tc:F1} s -> CT chega {tt - tc:F1} s antes (alvo 3-6; TR >= 10, CT >= 5)");
            }
            float rot = NavGrid.PathLen(SITES["A"].Center, SITES["B"].Center) / RUN;
            sb.AppendLine($"  ..  rotação A <-> B: {rot:F1} s");
            sb.AppendLine("ENTRADAS DOS BOMBS");
            foreach (var kv in SITES)
            {
                string site = kv.Key;
                var ent = new HashSet<string>();
                var tr = new HashSet<string>(); var ctr = new HashSet<string>();
                foreach (var sh in areas.Where(a => SiteOf(a.name) == site))
                    for (int x = Math.Max(sh.box.x0, 0); x < Math.Min(sh.box.x1, NX); x++)
                        for (int y = Math.Max(sh.box.y0, 0); y < Math.Min(sh.box.y1, NY); y++)
                        {
                            if (!walk[x, y] || SiteOf(AreaAt(x, y)) != site) continue;
                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                int u = x + dx, v = y + dy;
                                if (!InMap(u, v) || !walk[u, v]) continue;
                                string a = AreaAt(u, v);
                                if (a == null || SiteOf(a) == site) continue;
                                string rota = a == "Esc_viela" || a == "Passagem_viela" ? "Viela" : a == "Esc_plataforma" ? "Rua_CT" : a;
                                bool lt = T_SIDE.Contains(a) || a == "Rampa" || a == "Sacada_A" || a == "Tapete", lc = CT_SIDE.Contains(a);
                                ent.Add(CALLOUT[rota] + (lt ? " [TR]" : lc ? " [CT]" : " [meio]"));
                                if (!lc) tr.Add(rota);
                                if (!lt) ctr.Add(rota);
                            }
                        }
                // regra da Mirage: cada ponto estratégico com ~2 caminhos do lado TR e 3 do lado CT (o do meio conta pros dois)
                Line(tr.Count >= 2 && tr.Count <= 3 && ctr.Count >= 3 && ctr.Count >= tr.Count,
                     $"bomb {site}: caminhos TR {tr.Count}, CT {ctr.Count} (alvo TR 2-3, CT 3+) -> {string.Join(", ", ent)}");
            }
            sb.AppendLine("LARGURA DAS RUAS");
            foreach (var sh in areas.Where(a => a.width > 0))
                Line(sh.width >= 32 && sh.width <= 46, $"{CALLOUT[sh.name]}: {sh.width} (alvo 32-46; casas avançando tiram até 10 de um lado)");
            // vão livre de verdade (navmesh): em cada ponto, a menor das duas corridas livres (x e y); o 10% mais
            // estreito da área tem que caber o boneco (~18) com folga. Só o caminho de rato é de 1 boneco de propósito.
            sb.AppendLine("VÃO LIVRE (10% mais estreito da área; boneco ~18, mínimo 30)");
            foreach (var (nome, w) in Vaos())
                Line(w >= VAO_MIN || UM_BONECO.Contains(nome), $"{CALLOUT[nome]}: {w:F0}{(UM_BONECO.Contains(nome) ? " (1 boneco de propósito)" : "")}");
            sb.AppendLine("LINHAS DE VISÃO ACIMA DO LIMITE (segurar bomb/lado CT: 425; rua/meio: 325)");
            foreach (var g in Violations().Take(40))
                Line(g.sniper, $"{CALLOUT[g.a]} <-> {CALLOUT[g.b]}: {g.d:F0} {g.p}->{g.q}{(g.sniper ? " (de propósito)" : "")}");
            sb.Insert(0, $"REPROVAÇÕES: {bad}\n");
            return sb.ToString();
        }
    }
}
