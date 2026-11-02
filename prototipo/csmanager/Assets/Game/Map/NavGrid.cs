using System;
using System.Collections.Generic;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.Map
{
    /// <summary>
    /// Navegação em grade (célula de 2 unidades) com altura e tamanho do boneco:
    ///  • só fica onde cabe o boneco (raio 4: não raspa em parede, quina nem caixa);
    ///  • ligações: andar (degrau até 3,5), escada de mão (sobe/desce devagar), pular em cima de algo (até 11:
    ///    caixote +10, carroça +20 em dois pulos, tapete +30), cair de um nível (até 34, só pra baixo);
    ///  • campo de distância por destino em cache (Dijkstra a partir do destino);
    ///  • Path(): caminho com pontos e o tipo de cada trecho (pra animação: andar, pular, cair, escada), com os
    ///    cantos cortados onde dá pra andar em linha reta.
    /// </summary>
    public static class NavGrid
    {
        public const int CELL = 2, NGX = NX / CELL, NGY = NY / CELL;
        // EYE = olho de quem está em pé (boneco de 25): caixa simples (16) não esconde, pilha (28) esconde
        // pulo 18 = ~70% da altura do boneco (como o pulo do CS): sobe em caixa (16), barril (12), banca (12),
        // caixote da carroça (10); pilha (28) só por cima da caixa de baixo
        public const float STEP = 3.5f, JUMP = 18f, DROP = 34f, EYE = 18f;
        public static readonly string[] STANDABLE = { "crate", "crate_top", "barrel" };   // dá pra ficar em cima
        public static readonly bool[,] onTop = new bool[NGX, NGY];                         // célula em cima de caixa
        public const float RADIUS = 4f;             // meia largura do boneco (~9)
        public const float LADDER_COST = 2.6f;      // escada de mão: ~2,6x mais lenta que andar
        public static readonly string[] LADDERS = { "Esc_janelao" };

        public static readonly bool[,] move = new bool[NGX, NGY];
        public static readonly float[,] z = new float[NGX, NGY];
        public static readonly bool[,] ladder = new bool[NGX, NGY];

        public enum Link : byte { Walk, Drop, Jump, Ladder }
        static readonly List<(int from, float cost, Link link)>[] incoming;
        static readonly List<(int to, float cost, Link link)>[] outgoing;
        static readonly int[] idx = new int[NGX * NGY];
        static readonly List<(int x, int y)> cells = new List<(int, int)>();
        static readonly Dictionary<long, float[]> cache = new Dictionary<long, float[]>();
        static readonly float[][] layers = new float[4][];   // custo extra por célula (1 = perigo pro TR, 2 = pro CT)
        static readonly (int dx, int dy)[] NB8 = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        static NavGrid()
        {
            // 1) célula andável (piso, sem caixa) e altura
            var open = new bool[NGX, NGY];
            var stand = new bool[NX, NY];   // em cima de caixa/barril dá pra ficar
            foreach (var c in covers)   // pilha alta inteiriça (28) não dá pra subir; caixa dupla sim (16 + 12)
                if (Array.IndexOf(STANDABLE, c.kind) >= 0 && (c.kind != "crate" || c.h <= JUMP || covers.Exists(o => o.kind == "crate_top" && o.r.x0 > c.r.x0 && o.r.x1 < c.r.x1 && o.r.y0 > c.r.y0 && o.r.y1 < c.r.y1)))
                    for (int u = c.r.x0; u < c.r.x1; u++) for (int v = c.r.y0; v < c.r.y1; v++) if (InMap(u, v)) stand[u, v] = true;
            for (int x = 0; x < NGX; x++)
                for (int y = 0; y < NGY; y++)
                {
                    bool ok = true, top = false; int f = 0;
                    for (int a = 0; a < CELL && ok; a++)
                        for (int b = 0; b < CELL && ok; b++)
                        {
                            int u = x * CELL + a, v = y * CELL + b;
                            ok = walk[u, v] && (coverH[u, v] == 0 || stand[u, v]);
                            if (coverH[u, v] > 0) top = true;
                            f = Math.Max(f, floor[u, v] + coverH[u, v]);
                        }
                    open[x, y] = ok; z[x, y] = f; onTop[x, y] = ok && top;
                    if (ok) { var ar = AreaAt(x * CELL + 1, y * CELL + 1); ladder[x, y] = ar != null && Array.IndexOf(LADDERS, ar) >= 0; }
                }
            // 2) folga até parede/caixa (distância em células, 8 vizinhos com custo 1 / 1,41): o boneco cabe onde a
            //    folga >= RADIUS. A escada de mão é estreita de propósito: lá vale meio raio.
            var clear = new float[NGX, NGY];
            for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) clear[x, y] = open[x, y] ? float.MaxValue : 0;
            for (int pass = 0; pass < 2; pass++)
                for (int x = 0; x < NGX; x++)
                    for (int y = 0; y < NGY; y++)
                    {
                        int xx = pass == 0 ? x : NGX - 1 - x, yy = pass == 0 ? y : NGY - 1 - y;
                        if (clear[xx, yy] == 0) continue;
                        float best = clear[xx, yy];
                        foreach (var (dx, dy) in NB8)
                        {
                            int a = xx + dx, b = yy + dy;
                            float c = (dx != 0 && dy != 0) ? 1.414f : 1f;
                            float v = (a < 0 || b < 0 || a >= NGX || b >= NGY) ? 0 : clear[a, b];
                            best = MathF.Min(best, v + c);
                        }
                        clear[xx, yy] = best;
                    }
            for (int x = 0; x < NGX; x++)
                for (int y = 0; y < NGY; y++)
                    move[x, y] = open[x, y] && clear[x, y] * CELL >= (ladder[x, y] ? RADIUS * 0.5f : RADIUS);
            for (int i = 0; i < idx.Length; i++) idx[i] = -1;
            for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) if (move[x, y]) { idx[x * NGY + y] = cells.Count; cells.Add((x, y)); }
            // 3) ligações; colado na folga mínima custa um pouco mais (bot anda pelo meio da rua)
            incoming = new List<(int, float, Link)>[cells.Count];
            outgoing = new List<(int, float, Link)>[cells.Count];
            for (int i = 0; i < cells.Count; i++) { incoming[i] = new List<(int, float, Link)>(8); outgoing[i] = new List<(int, float, Link)>(8); }
            for (int i = 0; i < cells.Count; i++)
            {
                var (cx, cy) = cells[i];
                foreach (var (dx, dy) in NB8)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= NGX || ny >= NGY || !move[nx, ny]) continue;
                    if (dx != 0 && dy != 0 && !(move[cx + dx, cy] && move[cx, cy + dy])) continue;
                    float dh = z[nx, ny] - z[cx, cy];
                    Link link; float extra = 0, mul = 1;
                    if (ladder[cx, cy] || ladder[nx, ny])
                    {
                        if (MathF.Abs(dh) > 8f) continue;    // degrau da escada de mão (3 por unidade x 2)
                        link = MathF.Abs(dh) <= STEP && !(ladder[cx, cy] && ladder[nx, ny]) ? Link.Walk : Link.Ladder;
                        if (link == Link.Ladder) mul = LADDER_COST;
                    }
                    else if (MathF.Abs(dh) <= STEP) link = Link.Walk;
                    else if (dh < 0 && -dh <= DROP) { link = Link.Drop; extra = 4; }
                    else if (dh > 0 && dh <= JUMP) { link = Link.Jump; extra = 8; }
                    else continue;
                    float wallK = clear[nx, ny] * CELL < RADIUS + 3 ? 1.2f : 1f;
                    float cost = MathF.Sqrt(dx * dx + dy * dy) * CELL * wallK * mul + extra;
                    int j = idx[nx * NGY + ny];
                    incoming[j].Add((i, cost, link));
                    outgoing[i].Add((j, cost, link));
                }
            }
        }

        public static int CellCount => cells.Count;
        /// <summary>O ponto está numa escada de mão (sobe degrau a degrau, sem pular).</summary>
        public static bool Escada(V2 p) { int x = (int)(p.x / CELL), y = (int)(p.y / CELL); return x >= 0 && y >= 0 && x < NGX && y < NGY && ladder[x, y]; }
        public static (int x, int y) CellAt(int i) => cells[i];
        public static int IndexOf(int x, int y) => (x < 0 || y < 0 || x >= NGX || y >= NGY) ? -1 : idx[x * NGY + y];
        public static IReadOnlyList<(int to, float cost, Link link)> Out(int i) => outgoing[i];

        public static (int x, int y) CellOf(V2 p) =>
            (Math.Clamp((int)MathF.Floor(p.x / CELL), 0, NGX - 1), Math.Clamp((int)MathF.Floor(p.y / CELL), 0, NGY - 1));

        public static bool Movable(V2 p) { var (x, y) = CellOf(p); return move[x, y]; }
        public static float ZAt(V2 p) { var (x, y) = CellOf(p); return z[x, y]; }
        public static V2 Center(int x, int y) => new V2((x + 0.5f) * CELL, (y + 0.5f) * CELL);

        public static (int x, int y) NearestMovable(V2 p)
        {
            var (cx, cy) = CellOf(p);
            if (move[cx, cy]) return (cx, cy);
            for (int r = 1; r < 16; r++)
            {
                int best = int.MaxValue, bx = 0, by = 0;
                for (int x = cx - r; x <= cx + r; x++)
                    for (int y = cy - r; y <= cy + r; y++)
                    {
                        if (x < 0 || y < 0 || x >= NGX || y >= NGY || !move[x, y]) continue;
                        int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d < best) { best = d; bx = x; by = y; }
                    }
                if (best != int.MaxValue) return (bx, by);
            }
            return (cx, cy);
        }

        /// <summary>Camada de custo extra por célula (índice de célula da navmesh), usada nos caminhos com essa camada.</summary>
        public static void SetLayer(int layer, float[] extra) { lock (cache) { layers[layer] = extra; var old = new List<long>(); foreach (var k in cache.Keys) if (k / (NGX * NGY) == layer) old.Add(k); foreach (var k in old) cache.Remove(k); } }

        /// <summary>Distância pelo nav-grid de cada célula até o destino (inf onde não chega). Com camada, cada célula
        /// custa o extra dela (ex.: perigo de cruzar na mira do inimigo).</summary>
        public static float[] DistField(V2 target, int layer = 0)
        {
            var ex = layers[layer];
            // destino arredondado em 20 unidades: o caminho termina no ponto exato de qualquer jeito (Path)
            var (tx, ty) = NearestMovable(new V2(MathF.Floor(target.x / 20) * 20 + 10, MathF.Floor(target.y / 20) * 20 + 10));
            var (ox, oy) = NearestMovable(target);
            int qa = IndexOf(tx, ty), qb = IndexOf(ox, oy);
            if (qa < 0 || qb < 0 || !Straight(qa, qb)) (tx, ty) = (ox, oy);   // não arredonda pro outro lado de parede
            long key = tx * NGY + ty + (ex != null ? (long)layer * NGX * NGY : 0);
            lock (cache)
                if (cache.TryGetValue(key, out var hit)) return hit;
            // guardado por célula andável (não o mapa inteiro): metade da memória, cabe 3x mais destinos no cache
            var d = new float[cells.Count];
            for (int i = 0; i < d.Length; i++) d[i] = float.PositiveInfinity;
            int src = idx[tx * NGY + ty];
            if (src >= 0)
            {
                // heap binário em arrays (por thread, reaproveitado): a lista de tuplas era o gargalo do Dijkstra
                var hv = heapV ??= new int[1 << 16]; var hk = heapK ??= new float[1 << 16]; int n = 0;
                void Push(int v, float k)
                {
                    if (n == hv.Length) { Array.Resize(ref heapV, n * 2); Array.Resize(ref heapK, n * 2); hv = heapV; hk = heapK; }
                    int i = n++;
                    while (i > 0) { int p = (i - 1) >> 1; if (hk[p] <= k) break; hv[i] = hv[p]; hk[i] = hk[p]; i = p; }
                    hv[i] = v; hk[i] = k;
                }
                d[src] = 0; Push(src, 0);
                while (n > 0)
                {
                    int u = hv[0]; float du = hk[0];
                    int lv = hv[--n]; float lk = hk[n]; int i = 0;
                    while (true) { int c = 2 * i + 1; if (c >= n) break; if (c + 1 < n && hk[c + 1] < hk[c]) c++; if (hk[c] >= lk) break; hv[i] = hv[c]; hk[i] = hk[c]; i = c; }
                    if (n > 0) { hv[i] = lv; hk[i] = lk; }
                    if (du > d[u]) continue;
                    var inc = incoming[u];
                    for (int e = 0; e < inc.Count; e++)
                    {
                        var (v, c, _) = inc[e];
                        float nd = du + c + (ex != null ? ex[v] : 0);
                        if (nd < d[v]) { d[v] = nd; Push(v, nd); }
                    }
                }
            }
            lock (cache) { if (cache.Count >= 1200) cache.Clear(); cache[key] = d; }
            System.Threading.Interlocked.Increment(ref Dijkstras);
            return d;
        }
        public static int Dijkstras;   // quantos campos de distância foram calculados (perfil)
        [ThreadStatic] static int[] heapV; [ThreadStatic] static float[] heapK;

        /// <summary>Distância (campo) na célula de um ponto.</summary>
        static float Em(float[] d, int x, int y) { int i = idx[x * NGY + y]; return i < 0 ? float.PositiveInfinity : d[i]; }

        /// <summary>O ponto, ou o centro da célula andável mais perto dele.</summary>
        public static V2 Snap(V2 p) { if (Movable(p)) return p; var (x, y) = NearestMovable(p); return Center(x, y); }

        /// <summary>Dá pra ir reto de a até b andando (passo curto: peek, ajeitar) sem precisar de caminho.</summary>
        public static bool Direct(V2 a, V2 b)
        {
            var (ax, ay) = CellOf(a); var (bx, by) = CellOf(b);
            int ia = IndexOf(ax, ay), ib = IndexOf(bx, by);
            return ia >= 0 && ib >= 0 && Straight(ia, ib, false);   // passo curto (peek, cover): a largura do corpo só vale no caminho longo
        }

        public static float PathLen(V2 a, V2 b)
        {
            var f = DistField(b);
            var (x, y) = NearestMovable(a);
            return Em(f, x, y);
        }

        public struct Waypoint { public V2 p; public float z; public Link link; }   // link = como se chega neste ponto

        /// <summary>
        /// Caminho de a até b: desce o campo de distância célula a célula e depois corta os cantos — junta trechos
        /// de andar enquanto a linha reta passa só por células andáveis sem degrau grande. Pulos, quedas e escada
        /// viram pontos próprios (o bot sabe quando pular/cair/subir). Vazio se não dá pra chegar.
        /// </summary>
        public static List<Waypoint> Path(V2 a, V2 b, int layer = 0)
        {
            var res = new List<Waypoint>();
            var f = DistField(b, layer);
            var (sx, sy) = NearestMovable(a);
            int cur = idx[sx * NGY + sy];
            if (cur < 0 || float.IsInfinity(f[cur])) return res;
            var raw = new List<(int cell, Link link)> { (cur, Link.Walk) };
            for (int guard = 0; guard < 20000; guard++)
            {
                if (f[cur] <= 0) break;
                int best = -1; float bv = float.PositiveInfinity; Link bl = Link.Walk;
                foreach (var (to, c, l) in outgoing[cur])
                {
                    float v = c + f[to];
                    if (v < bv) { bv = v; best = to; bl = l; }
                }
                if (best < 0) break;
                raw.Add((best, bl)); cur = best;
            }
            // corta cantos: o ponto mais longe que dá pra ir reto (galopa 1,2,4,8... e volta em busca binária —
            // antes testava um por um, quadrático no tamanho do caminho)
            int i0 = 0;
            res.Add(Wp(raw[0].cell, Link.Walk));
            while (i0 < raw.Count - 1)
            {
                int j = i0 + 1;
                if (raw[j].link == Link.Walk)
                {
                    int lastWalk = j;
                    while (lastWalk + 1 < raw.Count && raw[lastWalk + 1].link == Link.Walk) lastWalk++;
                    int ok = j, step = 1;
                    while (ok + step <= lastWalk && Straight(raw[i0].cell, raw[ok + step].cell)) { ok += step; step *= 2; }
                    int lo = ok, hi = Math.Min(ok + step, lastWalk + 1);   // ok passa, hi falha (ou fora)
                    while (hi - lo > 1) { int mid = (lo + hi) / 2; if (Straight(raw[i0].cell, raw[mid].cell)) lo = mid; else hi = mid; }
                    j = lo;
                }
                res.Add(Wp(raw[j].cell, raw[j].link));
                i0 = j;
            }
            var end = res[res.Count - 1]; end.p = b; res[res.Count - 1] = end;
            return res;
        }

        static Waypoint Wp(int cell, Link l) { var (x, y) = cells[cell]; return new Waypoint { p = Center(x, y), z = z[x, y], link = l }; }

        /// <summary>Dá pra andar reto de uma célula a outra (todas andáveis, sem degrau maior que STEP, sem escada).</summary>
        static bool Straight(int ca, int cb, bool largura = true)
        {
            var (ax, ay) = cells[ca]; var (bx, by) = cells[cb];
            int n = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * 2;
            if (n == 0) return true;
            float pz = z[ax, ay];
            float dl = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)), px = -(by - ay) / dl * 0.5f, py = (bx - ax) / dl * 0.5f;
            for (int s = 1; s <= n; s++)
            {
                float t = s / (float)n;
                // checa a célula do centro e as duas laterais do corpo (o boneco tem largura: sem isso o caminho
                // cortava rente à quina e o bot ficava empurrando a parede)
                float fx = ax + 0.5f + (bx - ax) * t, fy = ay + 0.5f + (by - ay) * t;
                int x = (int)fx, y = (int)fy;
                if (x < 0 || y < 0 || x >= NGX || y >= NGY || !move[x, y] || ladder[x, y]) return false;
                int lx = (int)(fx + px), ly = (int)(fy + py), rx = (int)(fx - px), ry = (int)(fy - py);
                if (largura && (lx < 0 || ly < 0 || lx >= NGX || ly >= NGY || !move[lx, ly] || rx < 0 || ry < 0 || rx >= NGX || ry >= NGY || !move[rx, ry])) return false;
                if (MathF.Abs(z[x, y] - pz) > STEP) return false;
                pz = z[x, y];
            }
            return true;
        }

        /// <summary>Próximo ponto do caminho mais curto (alguns passos à frente), respeitando os links.</summary>
        public static V2 Steer(V2 pos, V2 target, int look = 4)
        {
            var path = Path(pos, target);
            if (path.Count < 2) return target;
            return path[1].p;
        }

        /// <summary>Linha de visão de olho a olho (zA/zB = piso de cada um).</summary>
        public static bool Los(V2 a, float zA, V2 b, float zB, float eye = EYE) => LosWalk(a, zA, b, zB, eye);
    }
}
