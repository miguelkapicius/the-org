using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.Map
{
    /// <summary>
    /// Confere a navmesh: (1) toda área do mapa é alcançável a partir das duas bases (e dá pra voltar);
    /// (2) pedaços andáveis que ninguém alcança (lugar inacessível); (3) rotas-chave usam a ligação certa (escada de
    /// mão, pulo na carroça, queda da sacada...). Também guarda o alcance pra desenhar.
    /// </summary>
    public static class NavCheck
    {
        public static bool[] reachT, reachCT, backT, backCT;   // por célula (índice da NavGrid)

        static bool[] Forward(V2 from)
        {
            var r = new bool[NavGrid.CellCount];
            var (x, y) = NavGrid.NearestMovable(from); int s = NavGrid.IndexOf(x, y);
            if (s < 0) return r;
            var q = new Queue<int>(); q.Enqueue(s); r[s] = true;
            while (q.Count > 0) { int u = q.Dequeue(); foreach (var (v, _, _) in NavGrid.Out(u)) if (!r[v]) { r[v] = true; q.Enqueue(v); } }
            return r;
        }

        static bool[] Back(V2 to)
        {
            var f = NavGrid.DistField(to); var r = new bool[NavGrid.CellCount];
            for (int i = 0; i < r.Length; i++) r[i] = !float.IsInfinity(f[i]);   // o campo é por célula andável
            return r;
        }

        static V2 C(string area) { foreach (var s in areas) if (s.name == area) return s.box.Center; throw new Exception("área " + area); }

        public static string Report()
        {
            var sb = new StringBuilder();
            V2 t = SPAWN_T[0], ct = SPAWN_CT[0];
            reachT = Forward(t); reachCT = Forward(ct); backT = Back(t); backCT = Back(ct);
            int n = NavGrid.CellCount;
            sb.AppendLine($"NAVMESH: {n} células de {NavGrid.CELL}x{NavGrid.CELL}, raio do boneco {NavGrid.RADIUS}");
            int falhas = 0;
            // 1) áreas
            sb.AppendLine("ÁREAS (alcança da base TR / da base CT / volta pras duas)");
            var porArea = new Dictionary<string, (int tot, int t, int ct, int volta)>();
            for (int i = 0; i < n; i++)
            {
                var (x, y) = NavGrid.CellAt(i);
                string a = AreaAt(x * NavGrid.CELL + 1, y * NavGrid.CELL + 1) ?? "(nicho)";
                porArea.TryGetValue(a, out var v);
                porArea[a] = (v.tot + 1, v.t + (reachT[i] ? 1 : 0), v.ct + (reachCT[i] ? 1 : 0), v.volta + (backT[i] && backCT[i] ? 1 : 0));
            }
            foreach (var s in areas)
            {
                if (!porArea.TryGetValue(s.name, out var v)) { sb.AppendLine($"  XX  {CALLOUT[s.name]}: nenhuma célula andável (boneco não cabe?)"); falhas++; continue; }
                bool ok = v.t == v.tot && v.ct == v.tot && v.volta == v.tot;
                if (!ok) falhas++;
                sb.AppendLine($"  {(ok ? "ok" : "XX")}  {CALLOUT[s.name]}: {v.tot} células, TR {100f * v.t / v.tot:0}%, CT {100f * v.ct / v.tot:0}%, volta {100f * v.volta / v.tot:0}%");
            }
            // 2) pedaços que ninguém alcança
            var seen = new bool[n]; int ilhas = 0;
            for (int i = 0; i < n; i++)
            {
                if (seen[i] || reachT[i] || reachCT[i]) continue;
                var comp = new List<int>(); var q = new Queue<int>(); q.Enqueue(i); seen[i] = true;
                while (q.Count > 0)
                {
                    int u = q.Dequeue(); comp.Add(u);
                    var (ux, uy) = NavGrid.CellAt(u);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int v = NavGrid.IndexOf(ux + dx, uy + dy);
                            if (v >= 0 && !seen[v] && !reachT[v] && !reachCT[v]) { seen[v] = true; q.Enqueue(v); }
                        }
                }
                ilhas++;
                var (cx, cy) = NavGrid.CellAt(comp[comp.Count / 2]);
                string nome = AreaAt(cx * NavGrid.CELL + 1, cy * NavGrid.CELL + 1);
                sb.AppendLine($"  XX  inacessível: {comp.Count} células perto de ({cx * NavGrid.CELL}, {cy * NavGrid.CELL}) {(nome != null ? CALLOUT[nome] : "")}");
                falhas++;
            }
            if (ilhas == 0) sb.AppendLine("  ok  nenhum pedaço inacessível");
            // 3) rotas-chave: tem que chegar e usar a ligação esperada
            sb.AppendLine("ROTAS (comprimento, tempo correndo, ligações usadas)");
            var rotas = new (string nome, V2 a, V2 b, NavGrid.Link? precisa)[]
            {
                ("base TR -> bomb A", t, SITES["A"].Center, null),
                ("base TR -> bomb B", t, SITES["B"].Center, null),
                ("base CT -> bomb A", ct, SITES["A"].Center, null),
                ("base CT -> bomb B", ct, SITES["B"].Center, null),
                ("praça -> janelão (escada de mão)", C("Praca") + new V2(0, 30), C("Janelao"), NavGrid.Link.Ladder),
                ("janelão -> praça (desce a escada de mão)", C("Janelao"), C("Praca"), null),
                ("curta -> caminho de rato -> fundos do janelão", C("Curta_fim"), C("Rato_fundo"), null),
                ("chão do B -> tapete (caixote + carroça)", SITES["B"].Center, C("Tapete"), NavGrid.Link.Jump),
                ("tapete -> casarão", C("Tapete"), C("Casarao"), null),
                ("sacada do palácio -> bomb A (queda)", C("Sacada_A"), SITES["A"].Center, NavGrid.Link.Drop),
                ("bomb A -> plataforma (escada)", SITES["A"].Center, C("Plataforma"), null),
                ("bomb B -> banca (escada)", SITES["B"].Center, C("Banca"), null),
                ("base TR -> palácio", t, C("Palacio"), null),
                ("base TR -> casarão", t, C("Casarao"), null),
            };
            foreach (var (nome, a, b, precisa) in rotas)
            {
                var p = NavGrid.Path(a, b);
                if (p.Count == 0) { sb.AppendLine($"  XX  {nome}: não chega"); falhas++; continue; }
                float L = 0; for (int i = 1; i < p.Count; i++) L += (p[i].p - p[i - 1].p).Len;
                var links = p.Skip(1).GroupBy(w => w.link).Where(g => g.Key != NavGrid.Link.Walk).Select(g => $"{g.Key} x{g.Count()}");
                bool ok = precisa == null || p.Any(w => w.link == precisa);
                if (!ok) falhas++;
                sb.AppendLine($"  {(ok ? "ok" : "XX")}  {nome}: {L:0} un, {L / MapCheck.RUN:0.0} s, {p.Count} pontos {(links.Any() ? "[" + string.Join(", ", links) + "]" : "")}");
            }
            // 4) posições (cantinhos, atrás de caixa, em cima de caixa) e se dá pra chegar em todas
            var spots = Spots.All(i => reachT[i] && reachCT[i]);
            int nCanto = spots.FindAll(s => s.kind == Spots.Kind.Canto).Count, nAtras = spots.FindAll(s => s.kind == Spots.Kind.AtrasCaixa).Count,
                nCima = spots.FindAll(s => s.kind == Spots.Kind.EmCima).Count;
            sb.AppendLine($"POSIÇÕES: {spots.Count} (cantinhos {nCanto}, atrás de caixa {nAtras}, em cima de caixa {nCima})");
            int semCaminho = 0;
            foreach (var s in spots) if (NavGrid.Path(t, s.p).Count == 0 || NavGrid.Path(ct, s.p).Count == 0) semCaminho++;
            if (semCaminho > 0) { sb.AppendLine($"  XX  {semCaminho} posições sem caminho"); falhas++; }
            else sb.AppendLine("  ok  todas as posições têm caminho das duas bases");
            sb.Insert(0, $"FALHAS: {falhas}\n");
            return sb.ToString();
        }
    }
}
