using System;
using System.Collections.Generic;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.Map
{
    /// <summary>
    /// Posições que um jogador usa pra segurar ou se esconder, achadas na navmesh: cantinho (parede em dois lados
    /// perto), atrás de caixa/pilar/barril (cobertura a um passo), em cima de caixa (boost). Espalhadas (uma a cada
    /// ~22 unidades por tipo) e só onde dá pra chegar. É o cardápio de lugares pros bots.
    /// </summary>
    public static class Spots
    {
        public enum Kind { Canto, AtrasCaixa, EmCima }
        public struct Spot { public V2 p; public float z; public Kind kind; public string area; }

        static List<Spot> cache;

        static bool Blocked(int x, int y, float z0) =>
            x < 0 || y < 0 || x >= NavGrid.NGX || y >= NavGrid.NGY || !NavGrid.move[x, y] || NavGrid.z[x, y] > z0 + NavGrid.STEP;

        public static List<Spot> All(Func<int, bool> reachable = null)
        {
            if (cache != null && reachable == null) return cache;
            var cand = new List<Spot>();
            for (int i = 0; i < NavGrid.CellCount; i++)
            {
                if (reachable != null && !reachable(i)) continue;
                var (x, y) = NavGrid.CellAt(i);
                if ((x + y) % 2 != 0) continue;
                float z0 = NavGrid.z[x, y];
                Kind? k = null;
                if (NavGrid.soBoost[x, y]) continue;   /* topo de pilha (pezinho): não é lugar de rotina */
                if (NavGrid.onTop[x, y]) k = Kind.EmCima;
                else
                {
                    // cobertura (caixa/pilar alto) a até 4 células: atrás de caixa
                    bool cover = false;
                    for (int dx = -4; dx <= 4 && !cover; dx++)
                        for (int dy = -4; dy <= 4 && !cover; dy++)
                        {
                            int u = (x + dx) * NavGrid.CELL + 1, v = (y + dy) * NavGrid.CELL + 1;
                            cover = InMap(u, v) && walk[u, v] && coverH[u, v] >= 12;
                        }
                    if (cover) k = Kind.AtrasCaixa;
                    else
                    {
                        // cantinho: bloqueado logo ali em um eixo x E em um eixo y (até 3 células)
                        bool bx = false, by = false;
                        for (int d = 1; d <= 3; d++)
                        {
                            bx |= Blocked(x + d, y, z0) || Blocked(x - d, y, z0);
                            by |= Blocked(x, y + d, z0) || Blocked(x, y - d, z0);
                        }
                        if (bx && by) k = Kind.Canto;
                    }
                }
                if (k == null) continue;
                var c = NavGrid.Center(x, y);
                cand.Add(new Spot { p = c, z = z0, kind = k.Value, area = AreaAt(c.x, c.y) });
            }
            // espalha: no máximo um por tipo a cada 22 unidades
            var res = new List<Spot>();
            foreach (var s in cand)
            {
                bool near = false;
                foreach (var r in res) if (r.kind == s.kind && (r.p - s.p).Len < (s.kind == Kind.EmCima ? 10f : 22f)) { near = true; break; }
                if (!near) res.Add(s);
            }
            if (reachable == null) cache = res;
            return res;
        }
    }
}
