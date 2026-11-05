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
        /* pulo agachado do CS (~+20% de altura: 22,9 com o boneco de 25): sobe o degrau entre 18 e 22,9 (a carroça do B, +20,
           direto do chão), mais caro que o pulo normal. -exp apPuloAgachadoVelho: só o pulo normal (lido da linha de
           comando: o NavGrid monta antes do TeamBrain) */
        public static readonly float JUMP_MAX = MapData.Velho("apPuloAgachadoVelho") ? JUMP : 22.9f;
        /* PEZINHO (boost): o topo da pilha inteiriça de 28 (sem caixa de cima) vira lugar de ficar — mas só se chega com o
           pezinho do colega (nenhum degrau sobe 28; descer é queda normal). Essas células são soBoost: o chão não tem caminho
           até elas (A* e PathLen respondem "não chega" na hora, sem varrer o mapa) e o NearestMovable não manda ninguém pra lá.
           -exp apBoostVelho: a pilha de 28 volta a ser só obstáculo */
        public static readonly bool BOOST = MapData.Velho("apBoostLigado") && !MapData.Velho("apBoostVelho");   /* opt-in até o A/B sem o AWPer (a sessão dos bots quer como padrão se passar) */
        public static readonly bool[,] soBoost = new bool[NGX, NGY];
        /// <summary>Célula onde o corpo cabe fisicamente (piso livre ou topo de caixa), sem contar a folga da parede.</summary>
        public static readonly bool[,] aberto = new bool[NGX, NGY];
        /// <summary>Distância da célula até a parede/caixa mais perto (unidades): passagem estreita (porta, beco) é folga baixa.</summary>
        public static readonly float[,] folga = new float[NGX, NGY];
        public static bool Andavel(V2 p) { var (x, y) = CellOf(p); return aberto[x, y]; }
        public static bool SobrePilha(V2 p) { var (x, y) = CellOf(p); return soBoost[x, y]; }
        static int[] ilhaDe;   // por célula: 0 = chão; n = topo de pilha n (só se chega lá de boost)
        /// <summary>Bordas que só o pezinho alcança (degrau de 22,9 a ~46 entre células vizinhas): o chão de onde se sobe e
        /// o alto (sacada, janelão, tapete, casarão...). Quem está em pé na cabeça do colega (+25) pula até +22,9 dali.</summary>
        public static readonly List<(V2 chao, V2 alto, float dh)> BordasPezinho = new List<(V2, V2, float)>();
        /// <summary>Topos de pilha (pezinho): id, centro, altura do topo e as células de cima.</summary>
        public static readonly List<(int id, V2 centro, float z, List<V2> pontos)> Ilhas = new List<(int, V2, float, List<V2>)>();
        static bool Ilhado(int s, int g) => ilhaDe != null && ilhaDe[g] != 0 && ilhaDe[s] != ilhaDe[g];
        /// <summary>O topo de pilha (célula soBoost) que dá pra alcançar com o pezinho a partir daqui, ou -1.</summary>
        /// <summary>A célula de chão (nunca topo de pilha) mais perto do ponto.</summary>
        public static (int x, int y) NearestChao(V2 p)
        {
            var (cx, cy) = CellOf(p);
            if (move[cx, cy] && !soBoost[cx, cy]) return (cx, cy);
            for (int r = 1; r < 24; r++)
            {
                int best = int.MaxValue, bx = 0, by = 0;
                for (int x = cx - r; x <= cx + r; x++)
                    for (int y = cy - r; y <= cy + r; y++)
                    {
                        if (x < 0 || y < 0 || x >= NGX || y >= NGY || !move[x, y] || soBoost[x, y]) continue;
                        int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d < best) { best = d; bx = x; by = y; }
                    }
                if (best != int.MaxValue) return (bx, by);
            }
            return (cx, cy);
        }
        public static int IlhaEm(V2 p) { var (x, y) = CellOf(p); int i = IndexOf(x, y); return i < 0 || ilhaDe == null ? 0 : ilhaDe[i]; }
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
        static readonly Dictionary<long, float[]> cacheG = new Dictionary<long, float[]>();
        // ---- modo determinístico (headless): cache dinâmico POR ROUND -----------------------------------------------
        /// <summary>
        /// O headless roda os rounds em paralelo e todos dividiam o cache de campos: o que cada round achava pronto
        /// dependia da ordem das threads e da carga (campo x A* muda empate de caminho e o gasto do orçamento) — a mesma
        /// semente dava resultado diferente. PorRound: cache dinâmico, fila, pares, razões e orçamento são da thread e
        /// zeram no começo de cada round (NovoRound, no construtor do Match); os campos FIXOS (Fixar) seguem divididos
        /// (não dependem de ordem). A fila anda por células, não por tempo. O jogo (um round por vez) não usa.
        /// </summary>
        public static bool PorRound;
        [ThreadStatic] static Dictionary<long, float[]> cacheT; [ThreadStatic] static Queue<long> ordemT;
        static Dictionary<long, float[]> cache => PorRound ? (cacheT ??= new Dictionary<long, float[]>()) : cacheG;
        static Queue<long> ordemCache => PorRound ? (ordemT ??= new Queue<long>()) : ordemG;
        /// <summary>
        /// Zera TODO o estado dinâmico (o cache dividido também; os campos fixos e os marcos ficam): o Match.Aquecer
        /// chama no fim, pra o round descartável não mudar a partida de verdade (nem segurar ~100 MB de campos).
        /// </summary>
        public static void ZeraDinamico()
        {
            lock (cacheG) { cacheG.Clear(); ordemG.Clear(); }
            bool pr = PorRound; PorRound = true; NovoRound(); PorRound = pr;
        }
        /// <summary>Começo de round no modo PorRound: zera tudo que é da thread e muda o resultado.</summary>
        public static void NovoRound()
        {
            if (!PorRound) return;
            cacheT?.Clear(); ordemT?.Clear(); fila?.Clear(); naFila?.Clear(); pares?.Clear(); pedidos?.Clear(); razoes?.Clear();
            razaoSoma = 0; razaoN = 0; nosNoPasso = 0; camposNoPasso = 0; filaPops = 0;
            origemS = -1; origemPasso = -1; origemPedidos = 0; oAtiva = false; passoN = 0;
        }
        static readonly Queue<long> ordemG = new Queue<long>(); const int CACHE_MAX = 400;
        // campos de lugares fixos do mapa (bombs, posições de segurar, centro das áreas): feitos uma vez, nunca saem
        static readonly Dictionary<long, float[]> fixos = new Dictionary<long, float[]>();

        /// <summary>
        /// Calcula de uma vez (na carga) os campos dos lugares fixos do mapa e deixa eles fora do rodízio do cache: o IGL
        /// mede o time inteiro até o bomb, a posição de segurar, a entrada do retake — tudo vira leitura no meio do round.
        /// </summary>
        public static void Fixar(IEnumerable<V2> pontos)
        {
            foreach (var p in pontos)
            {
                CampoNoCache(p, 0, out long k);
                lock (cache) if (fixos.ContainsKey(k)) continue;
                var f = DistField(p);
                lock (cache) fixos[k] = f;
            }
        }
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
            for (int u = 0; u < NX; u++) for (int v = 0; v < NY; v++) if (pisavel[u, v]) stand[u, v] = true;   // chafariz: borda e água
            var boostPix = new bool[NX, NY];
            if (BOOST)
                foreach (var c in covers)
                    if (c.kind == "crate" && c.h > JUMP_MAX && !covers.Exists(o => o.kind == "crate_top" && o.r.x0 > c.r.x0 && o.r.x1 < c.r.x1 && o.r.y0 > c.r.y0 && o.r.y1 < c.r.y1))
                        for (int u = c.r.x0; u < c.r.x1; u++) for (int v = c.r.y0; v < c.r.y1; v++) if (InMap(u, v)) { stand[u, v] = true; boostPix[u, v] = true; }
            for (int x = 0; x < NGX; x++)
                for (int y = 0; y < NGY; y++)
                {
                    bool ok = true, top = false; float f = 0;
                    for (int a = 0; a < CELL && ok; a++)
                        for (int b = 0; b < CELL && ok; b++)
                        {
                            int u = x * CELL + a, v = y * CELL + b;
                            ok = walk[u, v] && (coverH[u, v] == 0 || stand[u, v]);
                            if (coverH[u, v] > 0) top = true;
                            if (boostPix[u, v]) soBoost[x, y] = true;
                            f = MathF.Max(f, floor[u, v] + (coverFina[u, v] > 0 ? coverFina[u, v] : coverH[u, v]));   // chafariz: altura fina do modelo
                        }
                    open[x, y] = ok; z[x, y] = f; onTop[x, y] = ok && top; aberto[x, y] = ok;
                    if (ok) { var ar = AreaAt(x * CELL + 1, y * CELL + 1); ladder[x, y] = ar != null && Array.IndexOf(LADDERS, ar) >= 0; }
                }
            // 2) folga até parede/caixa (distância em células, 8 vizinhos com custo 1 / 1,41): o boneco cabe onde a
            //    folga >= RADIUS. A escada de mão é estreita de propósito: lá vale meio raio.
            var clear = new float[NGX, NGY];
            for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) clear[x, y] = open[x, y] && !soBoost[x, y] ? float.MaxValue : 0;   /* topo de pilha conta como parede pro chão: o chão fica igual ao de antes */
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
            for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) folga[x, y] = clear[x, y] * CELL;
            // topo de pilha (pezinho): folga própria, só dentro do topo (meio raio: em cima da caixa o boneco fica no meio)
            {
                var cb = new float[NGX, NGY];
                for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) cb[x, y] = soBoost[x, y] && open[x, y] ? float.MaxValue : 0;
                for (int pass = 0; pass < 2; pass++)
                    for (int x = 0; x < NGX; x++)
                        for (int y = 0; y < NGY; y++)
                        {
                            int xx = pass == 0 ? x : NGX - 1 - x, yy = pass == 0 ? y : NGY - 1 - y;
                            if (cb[xx, yy] == 0) continue;
                            float best = cb[xx, yy];
                            foreach (var (dx, dy) in NB8)
                            {
                                int a2 = xx + dx, b2 = yy + dy;
                                float c2 = (dx != 0 && dy != 0) ? 1.414f : 1f;
                                float v2 = (a2 < 0 || b2 < 0 || a2 >= NGX || b2 >= NGY) ? 0 : cb[a2, b2];
                                best = MathF.Min(best, v2 + c2);
                            }
                            cb[xx, yy] = best;
                        }
                for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) if (soBoost[x, y]) move[x, y] = cb[x, y] * CELL >= RADIUS * 0.5f;
            }
            for (int i = 0; i < idx.Length; i++) idx[i] = -1;
            for (int x = 0; x < NGX; x++) for (int y = 0; y < NGY; y++) if (move[x, y]) { idx[x * NGY + y] = cells.Count; cells.Add((x, y)); }
            // 2b) topos de pilha (pezinho): cada região contígua de células soBoost é uma ilha
            ilhaDe = new int[cells.Count]; int nIlha = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                var (cx, cy) = cells[i];
                if (!soBoost[cx, cy]) continue;
                if (ilhaDe[i] != 0) continue;
                nIlha++; var fila = new Queue<int>(); fila.Enqueue(i); ilhaDe[i] = nIlha;
                while (fila.Count > 0)
                {
                    var (ux, uy) = cells[fila.Dequeue()];
                    foreach (var (dx, dy) in NB8)
                    {
                        int j = IndexOf(ux + dx, uy + dy);
                        if (j >= 0 && ilhaDe[j] == 0 && soBoost[ux + dx, uy + dy]) { ilhaDe[j] = nIlha; fila.Enqueue(j); }
                    }
                }
            }
            /* (soBoost fica marcado mesmo onde não cabe o corpo: a física usa pra saber que é topo de pilha) */
            if (BOOST)
                for (int x = 0; x < NGX; x += 1)
                    for (int y = 0; y < NGY; y += 1)
                    {
                        if (!move[x, y] || soBoost[x, y] || (x + y) % 3 != 0) continue;
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= NGX || ny >= NGY || !move[nx, ny] || soBoost[nx, ny]) continue;
                            float dh = z[nx, ny] - z[x, y];
                            if (dh > JUMP_MAX && dh <= 25f + JUMP_MAX - 2f) BordasPezinho.Add((Center(x, y), Center(nx, ny), dh));
                        }
                    }
            for (int k = 1; k <= nIlha; k++)
            {
                var pts = new List<V2>(); float zt = 0; V2 soma = V2.Zero;
                for (int i = 0; i < cells.Count; i++) if (ilhaDe[i] == k) { var (cx, cy) = cells[i]; var q = Center(cx, cy); pts.Add(q); soma += q; zt = MathF.Max(zt, z[cx, cy]); }
                if (pts.Count > 0) Ilhas.Add((k, soma / pts.Count, zt, pts));
            }
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
                    else if (dh > 0 && dh <= JUMP_MAX) { link = Link.Jump; extra = 14; }   /* pulo agachado */
                    else continue;
                    float wallK = clear[nx, ny] * CELL < RADIUS + 3 ? 1.2f : 1f;
                    float cost = MathF.Sqrt(dx * dx + dy * dy) * CELL * wallK * mul + extra;
                    int j = idx[nx * NGY + ny];
                    incoming[j].Add((i, cost, link));
                    outgoing[i].Add((j, cost, link));
                }
            }
            // queda do topo da pilha (pezinho) pro chão: a beirada do topo e o chão andável mais perto ficam a ~2 raios
            // (a folga dos dois lados); liga direto, como a queda normal (a física deixa quem está na pilha pisar na folga)
            for (int i = 0; i < cells.Count; i++)
            {
                if (ilhaDe[i] == 0) continue;
                var (cx, cy) = cells[i]; bool beira = false;
                foreach (var (dx, dy) in NB8) { int j = IndexOf(cx + dx, cy + dy); if (j < 0 || ilhaDe[j] != ilhaDe[i]) beira = true; }
                if (!beira) continue;
                int alvo = -1; float da = float.MaxValue;
                for (int ox = -6; ox <= 6; ox++)
                    for (int oy = -6; oy <= 6; oy++)
                    {
                        int j = IndexOf(cx + ox, cy + oy); if (j < 0 || ilhaDe[j] != 0) continue;
                        float d2 = ox * ox + oy * oy; if (d2 < da) { da = d2; alvo = j; }
                    }
                if (alvo < 0) continue;
                float cst = MathF.Sqrt(da) * CELL + 4;
                incoming[alvo].Add((i, cst, Link.Drop)); outgoing[i].Add((alvo, cst, Link.Drop));
            }
            FazMarcos();
        }

        // ---- marcos (heurística ALT) ------------------------------------------------------------------------------
        /// <summary>
        /// A* entre pontos longe (MoveTo/PixelCalc/EsperaOGrupo de um lado do mapa ao outro, com a camada de perigo)
        /// varria 60-120 mil células com a heurística da reta (octil): o pico do passo no Mono era UM A* desses. Com
        /// marcos (ALT): distância andando de cada célula até N_MARCOS lugares espalhados, calculada na carga; a
        /// desigualdade triangular d(u,g) >= d(u,L) - d(g,L) dá uma estimativa ainda admissível e consistente (o caminho
        /// continua o menor, exato) e muito mais justa: o A* vai quase reto pelo corredor certo. -exp altVelho desliga.
        /// </summary>
        public static bool UsaMarcos = true;
        const int N_MARCOS = 16, MARCOS_ATIVOS = 4;
        static float[][] marcos;   // marcos[l][célula] = distância andando da célula até o marco l (sem camada)
        static int[] marcoCel;     // a célula de cada marco
        // os mesmos marcos com o custo de cada camada (o perigo é fixo, calculado uma vez no Perigo.Build): sem isso a
        // estimativa sem camada fica frouxa nos caminhos com perigo e o A* com camada continuava varrendo meio mapa
        static readonly float[][][] marcosCamada = new float[4][][];
        public static double msMarcos;   // perfil: tempo da carga dos marcos

        static void FazMarcos()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int nc = cells.Count; if (nc == 0) return;
            var lista = new List<float[]>(); var cel = new List<int>(); var minD = new float[nc];
            for (int i = 0; i < nc; i++) minD[i] = float.PositiveInfinity;
            // o 1º marco é o mais longe de uma célula qualquer; cada próximo é o mais longe de todos os já escolhidos
            int prox = MaisLonge(AteMarco(nc / 2, null));
            for (int l = 0; l < N_MARCOS && prox >= 0; l++)
            {
                var f = AteMarco(prox, null); lista.Add(f); cel.Add(prox);
                for (int i = 0; i < nc; i++) if (f[i] < minD[i]) minD[i] = f[i];
                prox = MaisLonge(minD);
            }
            marcos = lista.ToArray(); marcoCel = cel.ToArray();
            msMarcos = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
        static int MaisLonge(float[] d)
        {
            int best = -1; float bv = 0;
            for (int i = 0; i < d.Length; i++) if (!float.IsInfinity(d[i]) && d[i] > bv) { bv = d[i]; best = i; }
            return best;
        }
        static void MarcosDaCamada(int layer, float[] ex)
        {
            if (marcoCel == null || ex == null) { marcosCamada[layer] = null; return; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var r = new float[marcoCel.Length][];
            for (int l = 0; l < r.Length; l++) r[l] = AteMarco(marcoCel[l], ex);
            marcosCamada[layer] = r;
            msMarcos += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
        /// <summary>Distância andando de cada célula ATÉ a célula t (Dijkstra pelas ligações de chegada; com camada, o
        /// extra é da célula de onde sai, igual ao A* e ao DistField).</summary>
        static float[] AteMarco(int t, float[] ex)
        {
            int nc = cells.Count; var d = new float[nc];
            for (int i = 0; i < nc; i++) d[i] = float.PositiveInfinity;
            var hv = new int[1 << 14]; var hk = new float[1 << 14]; int n = 1; d[t] = 0; hv[0] = t; hk[0] = 0;
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
                    var (v, c, _) = inc[e]; float nd = du + c + (ex != null ? ex[v] : 0);
                    if (nd >= d[v]) continue;
                    d[v] = nd;
                    if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); }
                    int j = n++;
                    while (j > 0) { int pp = (j - 1) >> 1; if (hk[pp] <= nd) break; hv[j] = hv[pp]; hk[j] = hk[pp]; j = pp; }
                    hv[j] = v; hk[j] = nd;
                }
            }
            return d;
        }

        /// <summary>Teste (headless -testaMarcos): A* com e sem marcos em pares aleatórios, sem e com camada — a distância
        /// tem que ser a mesma; devolve a maior diferença e as células médias de cada um.</summary>
        public static string TestaMarcos(int n, int seed)
        {
            var rnd = new Random(seed); var sb = new System.Text.StringBuilder(); bool antes = UsaMarcos;
            for (int ly = 0; ly < layers.Length; ly++)
            {
                if (ly > 0 && layers[ly] == null) continue;
                float maxDif = 0; long nosCom = 0, nosSem = 0; int infDif = 0;
                for (int i = 0; i < n; i++)
                {
                    int a = rnd.Next(cells.Count), b = rnd.Next(cells.Count);
                    int k0 = NosAEstrela; UsaMarcos = true; float r1 = AEstrela0(a, b, ly > 0 ? layers[ly] : null, false); nosCom += NosAEstrela - k0;
                    k0 = NosAEstrela; UsaMarcos = false; float r2 = AEstrela0(a, b, ly > 0 ? layers[ly] : null, false); nosSem += NosAEstrela - k0;
                    if (float.IsInfinity(r1) != float.IsInfinity(r2)) infDif++;
                    else if (!float.IsInfinity(r1)) maxDif = MathF.Max(maxDif, MathF.Abs(r1 - r2));
                }
                sb.AppendLine($"camada {ly}: {n} pares, maior diferença {maxDif:0.000}, alcance diferente {infDif}, células por A* com marcos {nosCom / n} sem {nosSem / n}");
            }
            UsaMarcos = antes; sb.Append($"células do grid {cells.Count}, marcos {marcos?.Length ?? 0}, carga {msMarcos:0} ms");
            return sb.ToString();
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
                        if (x < 0 || y < 0 || x >= NGX || y >= NGY || !move[x, y] || soBoost[x, y]) continue;
                        int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                        if (d < best) { best = d; bx = x; by = y; }
                    }
                if (best != int.MaxValue) return (bx, by);
            }
            return (cx, cy);
        }

        /// <summary>Camada de custo extra por célula (índice de célula da navmesh), usada nos caminhos com essa camada.</summary>
        public static void SetLayer(int layer, float[] extra) { MarcosDaCamada(layer, extra); lock (cache) { layers[layer] = extra; var old = new List<long>(); foreach (var k in cache.Keys) if (k / (NGX * NGY) == layer) old.Add(k); foreach (var k in old) cache.Remove(k); if (old.Count > 0) { var resto = new Queue<long>(); foreach (var k in ordemCache) if (cache.ContainsKey(k)) resto.Enqueue(k); ordemCache.Clear(); foreach (var k in resto) ordemCache.Enqueue(k); } } }

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
            {
                if (fixos.TryGetValue(key, out var fx)) return fx;
                if (cache.TryGetValue(key, out var hit)) return hit;
            }
            // guardado por célula andável (não o mapa inteiro): metade da memória, cabe 3x mais destinos no cache
            long t0 = Medir ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var d = new float[cells.Count];
            for (int i = 0; i < d.Length; i++) d[i] = float.PositiveInfinity;
            int src = idx[tx * NGY + ty];
            if (src >= 0)
            {
                // heap binário em arrays (por thread, reaproveitado): a lista de tuplas era o gargalo do Dijkstra
                // heap em arrays locais, empilhar inline (função local com captura e [ThreadStatic] no laço são lentos no Mono)
                int[] hv = heapV ??= new int[1 << 16]; float[] hk = heapK ??= new float[1 << 16]; int n = 0;
                d[src] = 0; hv[0] = src; hk[0] = 0; n = 1;
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
                        if (nd >= d[v]) continue;
                        d[v] = nd;
                        if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); heapV = hv; heapK = hk; }
                        int j = n++;
                        while (j > 0) { int pp = (j - 1) >> 1; if (hk[pp] <= nd) break; hv[j] = hv[pp]; hk[j] = hk[pp]; j = pp; }
                        hv[j] = v; hk[j] = nd;
                    }
                }
            }
            Guarda(key, d);
            System.Threading.Interlocked.Increment(ref Dijkstras); camposNoPasso++;
            if (Medir) { msCampo += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; nCampo++; }
            if (QuemPede != null) lock (QuemPede) { var fr = new System.Diagnostics.StackTrace(1, false); string q = "D: "; for (int i = 0; i < Math.Min(3, fr.FrameCount); i++) q += (i > 0 ? " < " : "") + fr.GetFrame(i).GetMethod()?.Name; QuemPede.TryGetValue(q, out int c); QuemPede[q] = c + 1; if (QuemPedePasso != null) { QuemPedePasso.TryGetValue(q, out int cp); QuemPedePasso[q] = cp + 1; } }
            return d;
        }
        public static int Dijkstras;   // quantos campos de distância foram calculados (perfil)
        // tira o mais antigo, um por vez (zerar 1200 campos de ~400 KB de uma vez era meio giga de lixo numa tacada:
        // pausa longa do coletor no jogo)
        static void Guarda(long key, float[] d)
        {
            lock (cache) { if (!cache.ContainsKey(key)) { while (cache.Count >= CACHE_MAX && ordemCache.Count > 0) cache.Remove(ordemCache.Dequeue()); ordemCache.Enqueue(key); } cache[key] = d; }
        }

        // ---- campo calculado em pedaços (um pouco por passo do jogo) -------------------------------------------------
        sealed class CampoEmCurso { public long key; public int layer; public float[] d; public int[] hv = new int[1 << 14]; public float[] hk = new float[1 << 14]; public int n; }
        [ThreadStatic] static List<CampoEmCurso> fila; [ThreadStatic] static HashSet<long> naFila;
        /// <summary>
        /// Tempo por passo do jogo pros campos na fila (ms): no .NET um campo fica pronto em 2-3 passos, no Mono (~37 ms o
        /// campo) em ~6, e nenhum passo estoura. 0 = calcula na hora (antigo).
        /// </summary>
        public static float MS_FILA_POR_PASSO = 6f;
        static int CELULAS_POR_PASSO => MS_FILA_POR_PASSO > 0 ? 1 : 0;
        /// <summary>
        /// Põe o campo do destino na fila em vez de calcular agora: no Mono o campo inteiro custa ~37 ms, e um só dentro
        /// de um passo já era um quadro perdido. Enquanto ele não fica pronto, quem pede caminho usa A* (o mesmo caminho).
        /// </summary>
        static void Enfileira(V2 target, int layer)
        {
            if (CELULAS_POR_PASSO <= 0) { DistField(target, layer); return; }
            var ex = layers[layer];
            var (tx, ty) = NearestMovable(new V2(MathF.Floor(target.x / 20) * 20 + 10, MathF.Floor(target.y / 20) * 20 + 10));
            var (ox, oy) = NearestMovable(target);
            int qa = IndexOf(tx, ty), qb = IndexOf(ox, oy);
            if (qa < 0 || qb < 0 || !Straight(qa, qb)) (tx, ty) = (ox, oy);
            long key = tx * NGY + ty + (ex != null ? (long)layer * NGX * NGY : 0);
            var nf = naFila ??= new HashSet<long>();
            if (nf.Contains(key)) return;
            lock (cache) if (fixos.ContainsKey(key) || cache.ContainsKey(key)) return;
            int src = idx[tx * NGY + ty]; if (src < 0) return;
            var c = new CampoEmCurso { key = key, layer = layer, d = new float[cells.Count] };
            for (int i = 0; i < c.d.Length; i++) c.d[i] = float.PositiveInfinity;
            c.d[src] = 0; c.hv[0] = src; c.hk[0] = 0; c.n = 1;
            (fila ??= new List<CampoEmCurso>()).Add(c); nf.Add(key);
        }
        /// <summary>Avança os campos da fila até o orçamento do passo; o que termina vai pro cache.</summary>
        /// <summary>
        /// Orçamento da fila de campos em CÉLULAS por passo (determinístico), não em ms: por tempo, o headless com a máquina
        /// carregada (vários processos) terminava os campos mais tarde, mais pedido caía em A*/estimativa e o comportamento
        /// dos bots mudava com a carga — e o .NET (rápido) não batia com o Mono do jogo. 24 mil ≈ os 6 ms do Mono
        /// (~37 ms o campo inteiro). Ainda opt-in (-exp filaCelulas) até passar no A/B; o padrão segue por tempo.
        /// </summary>
        public static int FILA_POPS_POR_PASSO = 24000;
        public static bool FilaPorTempo;
        [ThreadStatic] static int filaPops;
        static void AvancaFila()
        {
            var f = fila; if (f == null || f.Count == 0) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            long fim = t0 + (long)(MS_FILA_POR_PASSO * System.Diagnostics.Stopwatch.Frequency / 1000.0);
            bool acabou = false; filaPops = 0;
            while (!acabou && f.Count > 0)
            {
                var c = f[0]; var ex = layers[c.layer]; var d = c.d; int[] hv = c.hv; float[] hk = c.hk; int n = c.n;
                int conta = 0;
                while (n > 0)
                {
                    if (FilaPorTempo && !PorRound ? (++conta & 1023) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() > fim : ++filaPops > FILA_POPS_POR_PASSO) { acabou = true; break; }
                    int u = hv[0]; float du = hk[0];
                    int lv = hv[--n]; float lk = hk[n]; int i = 0;
                    while (true) { int ch = 2 * i + 1; if (ch >= n) break; if (ch + 1 < n && hk[ch + 1] < hk[ch]) ch++; if (hk[ch] >= lk) break; hv[i] = hv[ch]; hk[i] = hk[ch]; i = ch; }
                    if (n > 0) { hv[i] = lv; hk[i] = lk; }
                    if (du > d[u]) continue;
                    var inc = incoming[u];
                    for (int e = 0; e < inc.Count; e++)
                    {
                        var (v, cst, _) = inc[e];
                        float nd = du + cst + (ex != null ? ex[v] : 0);
                        if (nd >= d[v]) continue;
                        d[v] = nd;
                        if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); c.hv = hv; c.hk = hk; }
                        int j = n++;
                        while (j > 0) { int pp = (j - 1) >> 1; if (hk[pp] <= nd) break; hv[j] = hv[pp]; hk[j] = hk[pp]; j = pp; }
                        hv[j] = v; hk[j] = nd;
                    }
                }
                c.n = n;
                if (n == 0)
                {   // pronto: entra no cache
                    Guarda(c.key, d); f.RemoveAt(0); naFila.Remove(c.key);
                    System.Threading.Interlocked.Increment(ref Dijkstras); if (Medir) nCampo++;
                }
            }
            if (Medir) msCampo += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
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

        /// <summary>Distância de p até o alvo de um campo já calculado (DistField(alvo)): pra medir muitos pontos até UM
        /// lugar com um Dijkstra só, em vez de um PathLen (um campo inteiro) por ponto.</summary>
        public static float NoCampo(float[] campo, V2 p) { var (x, y) = NearestMovable(p); return Em(campo, x, y); }

        /// <summary>Volta ao jeito antigo: todo PathLen calcula (ou acha no cache) o campo inteiro até o destino.</summary>
        public static bool PathLenSoCampo;

        /// <summary>
        /// Comprimento do caminho de a até b. Campo do destino já no cache: lê dele. Senão, um A* só entre os dois (o mesmo
        /// menor caminho, o custo de cada ligação nunca é menor que a distância octil) — calcular o campo do mapa inteiro
        /// (~10 ms) pra ler UMA distância era o pico de CPU quando o bot mede vários lugares de uma vez.
        /// </summary>
        public static float PathLen(V2 a, V2 b)
        {
            var (x, y) = NearestMovable(a);
            if (PathLenSoCampo) return Em(DistField(b), x, y);
            var f = CampoNoCache(b, 0, out long chave);
            if (f != null) return Em(f, x, y);
            // destino que volta a ser pedido (o bomb, o ponto do grupo — todo tique, de todo mundo): vale o campo inteiro
            if (ValeCampo(chave)) Enfileira(b, 0);   // campo inteiro em pedaços; agora vai de A*
            var (bx, by) = NearestMovable(b);
            int s = idx[x * NGY + y], g = idx[bx * NGY + by];
            if (s < 0 || g < 0) return float.PositiveInfinity;
            if (s == g) return 0;
            if (Ilhado(s, g)) return float.PositiveInfinity;   // topo de pilha: só de pezinho
            long k = (long)s * cells.Count + g;
            var pc = pares ??= new Dictionary<long, float>();
            if (pc.TryGetValue(k, out float v)) return v;
            if (pc.Count > 200000) pc.Clear();
            if (OrcamentoA) { float vo = DaOrigem(s, g); if (!float.IsNaN(vo)) return pc[k] = vo; }   // mesma origem de novo: exato, sem A*
            if (Estourou) return Estimativa(a, s, g);   // o passo já gastou o orçamento de A*: estimativa (o campo vem depois)
            float r = AEstrela(s, g, null, false);
            if (OrcamentoA) Aprende(s, g, r);
            return pc[k] = r;
        }

        /// <summary>
        /// PathLen pra quem só quer saber se o lugar está a até `limite` andando (ângulo cruzado, quebrar a visão, caixa
        /// pra subir, pixel fora da smoke): exato até o limite; além dele devolve infinito sem procurar o caminho inteiro —
        /// o lugar colado do outro lado da parede era o A* que varria meio mapa pra responder "longe".
        /// </summary>
        public static float PathLenAte(V2 a, V2 b, float limite)
        {
            if (!OrcamentoA || PathLenSoCampo) return PathLen(a, b);
            var (x, y) = NearestMovable(a);
            var f = CampoNoCache(b, 0, out _);
            if (f != null) return Em(f, x, y);
            var (bx, by) = NearestMovable(b);
            int s = idx[x * NGY + y], g = idx[bx * NGY + by];
            if (s < 0 || g < 0) return float.PositiveInfinity;
            if (s == g) return 0;
            if (Ilhado(s, g)) return float.PositiveInfinity;   // topo de pilha: só de pezinho
            long k = (long)s * cells.Count + g;
            var pc = pares ??= new Dictionary<long, float>();
            if (pc.TryGetValue(k, out float v)) return v;
            if (pc.Count > 200000) pc.Clear();
            float vo = DaOrigem(s, g, limite);
            if (!float.IsNaN(vo)) { if (!float.IsInfinity(vo)) pc[k] = vo; return vo; }
            if (Estourou) return Estimativa(a, s, g);
            float r = AEstrela(s, g, null, false, limite);
            if (!float.IsInfinity(r)) { Aprende(s, g, r); pc[k] = r; }
            return r;
        }

        // ---- orçamento de A* por passo -----------------------------------------------------------------------------
        /// <summary>
        /// Liga o orçamento de A* por passo (o Match liga; -exp orcAVelho desliga). Os A* de um passo eram a causa dos
        /// passos lentos: um bot medindo 10-48 lugares (ângulo cruzado, caixa pra subir, quem vem junto) e vários bots
        /// recalculando caminho no mesmo tique. Cada A* custa 4-5 ms no Mono.
        /// </summary>
        public static bool OrcamentoA;
        /// <summary>
        /// Teto no A* de quem só estima (Path com soSeCouber, o pixel da mira): um A* sozinho de 90-100 mil células (alvo do
        /// outro lado do mapa, com a camada de perigo) era o pico do meio do round no Mono. Acima de ORC_NOS_PASSO células
        /// desiste, põe o campo do destino na fila (fica pronto em alguns passos) e adia. DESLIGADO (liga com -exp tetoPixel):
        /// no A/B de 900 rounds o pixel adiado deixou a mira pior (retake do CT 62 -> 52%, TR vence 46,5 -> 52%).
        /// </summary>
        public static bool TetoPixel = false;
        public static int Desistiu;   // perfil: A* que passaram do teto e desistiram
        /// <summary>Células expandidas por A* por passo do jogo antes de negar estimativa (~3 ms no .NET, ~8 no Mono).</summary>
        public static int ORC_NOS_PASSO = 30000;
        [ThreadStatic] static int nosNoPasso;
        public static int Negados, NosAEstrela;   // perfil: pedidos que viraram estimativa; células expandidas no total
        /// <summary>O passo já gastou o orçamento de A*: quem só quer ESTIMAR distância (não andar) usa estimativa ou adia.</summary>
        public static bool Estourou => OrcamentoA && nosNoPasso >= ORC_NOS_PASSO;

        /// <summary>
        /// Distância sem A*, quando o passo estourou: (1) o campo até a ORIGEM, se calculado (a volta é quase a ida);
        /// (2) a razão caminho/reta do último A* entre as mesmas regiões (blocos de 16 unidades) — o caminho de um canto
        /// ao outro quase não muda andando dentro do bloco; (3) reta × a razão média de todos os A* do jogo. Também põe o
        /// campo do destino na fila (ValeCampo já fez) — o próximo pedido lê o campo, exato.
        /// </summary>
        static float Estimativa(V2 a, int s, int g)
        {
            System.Threading.Interlocked.Increment(ref Negados); Conta("EST: ", 2);
            var fo = CampoNoCache(a, 0, out _);
            if (fo != null) return fo[g];
            float reta = Octil(s, g);
            var rz = razoes;
            if (rz != null && rz.TryGetValue(ChaveRegiao(s, g), out float r)) return reta * r;
            return reta * (razaoN > 0 ? (float)(razaoSoma / razaoN) : 1.3f);
        }
        /// <summary>
        /// Muitos destinos da MESMA origem no mesmo passo (o bot medindo 48 lugares em volta do duelo, as caixas perto, os
        /// ângulos): o 1º vai de A*; do 2º em diante, um Dijkstra a partir da origem que continua de onde parou a cada
        /// pedido — a mesma distância exata, e a área perto de mim é varrida uma vez só em vez de um A* por lugar. NaN =
        /// não respondeu (1º pedido, orçamento do passo estourado, ou longe demais): segue o caminho normal.
        /// </summary>
        static float DaOrigem(int s, int g, float limite = float.PositiveInfinity)
        {
            if (origemS != s || origemPasso != passoN) { origemS = s; origemPasso = passoN; origemPedidos = 0; oAtiva = false; }
            if (++origemPedidos < 2) return float.NaN;
            int nc = cells.Count;
            if (!oAtiva)
            {
                if (nosNoPasso >= 2 * ORC_NOS_PASSO) return float.NaN;
                if (oD == null || oD.Length != nc) { oD = new float[nc]; oMk = new int[nc]; oFech = new int[nc]; oGer = 0; oHv = new int[1 << 14]; oHk = new float[1 << 14]; }
                oGer++; oD[s] = 0; oMk[s] = oGer; oHv[0] = s; oHk[0] = 0; oN = 1; oNos = 0; oAtiva = true;
            }
            int ger = oGer;
            if (oFech[g] == ger) return oD[g];
            float[] d = oD; int[] mk = oMk, fe = oFech; int[] hv = oHv; float[] hk = oHk; int n = oN;
            float res = float.PositiveInfinity;   // a fila acabou sem chegar: não alcança
            while (n > 0)
            {
                if (hk[0] > limite) break;   // tudo que falta está além do limite de quem perguntou: infinito (a busca continua no próximo)
                if (oNos >= ORIGEM_MAX_NOS || nosNoPasso >= 2 * ORC_NOS_PASSO) { res = float.NaN; break; }   // exato vale até o dobro do orçamento
                int u = hv[0]; float du = hk[0];
                int lv = hv[--n]; float lk = hk[n]; int i = 0;
                while (true) { int c = 2 * i + 1; if (c >= n) break; if (c + 1 < n && hk[c + 1] < hk[c]) c++; if (hk[c] >= lk) break; hv[i] = hv[c]; hk[i] = hk[c]; i = c; }
                if (n > 0) { hv[i] = lv; hk[i] = lk; }
                if (fe[u] == ger || du > d[u]) continue;
                fe[u] = ger; oNos++; nosNoPasso++; NosAEstrela++;
                var o = outgoing[u];
                for (int e = 0; e < o.Count; e++)
                {
                    var (v, c, _) = o[e];
                    float nd = du + c;   // mesmo custo do A* sem camada
                    if (mk[v] == ger && nd >= d[v]) continue;
                    d[v] = nd; mk[v] = ger;
                    if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); oHv = hv; oHk = hk; }
                    int j = n++;
                    while (j > 0) { int pp = (j - 1) >> 1; if (hk[pp] <= nd) break; hv[j] = hv[pp]; hk[j] = hk[pp]; j = pp; }
                    hv[j] = v; hk[j] = nd;
                }
                if (u == g) { res = du; break; }
            }
            oN = n;
            if (!float.IsNaN(res)) System.Threading.Interlocked.Increment(ref DaOrigemRespondeu);
            return res;
        }
        const int ORIGEM_MAX_NOS = 40000;
        public static int DaOrigemRespondeu;   // perfil: distâncias respondidas pela busca da origem (A* que não precisou)
        [ThreadStatic] static int origemS, origemPasso, origemPedidos, passoN, oGer, oN, oNos; [ThreadStatic] static bool oAtiva;
        [ThreadStatic] static float[] oD, oHk; [ThreadStatic] static int[] oMk, oFech, oHv;

        static void Conta(string pre, int pula)
        {
            if (QuemPede == null) return;
            lock (QuemPede) { var fr = new System.Diagnostics.StackTrace(pula + 1, false); string q = pre; for (int i = 0; i < Math.Min(3, fr.FrameCount); i++) q += (i > 0 ? " < " : "") + fr.GetFrame(i).GetMethod()?.Name; QuemPede.TryGetValue(q, out int c); QuemPede[q] = c + 1; }
        }
        static float Octil(int s, int g)
        {
            var (sx, sy) = cells[s]; var (gx, gy) = cells[g];
            int dx = Math.Abs(sx - gx), dy = Math.Abs(sy - gy);
            return CELL * (Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy));
        }
        static long ChaveRegiao(int s, int g)
        {
            var (sx, sy) = cells[s]; var (gx, gy) = cells[g];
            const int B = 8;   // 8 células = 16 unidades
            return ((long)(sx / B) * (NGY / B + 1) + sy / B) * ((NGX / B + 1) * (NGY / B + 1)) + (gx / B) * (NGY / B + 1) + gy / B;
        }
        [ThreadStatic] static Dictionary<long, float> razoes; [ThreadStatic] static double razaoSoma; [ThreadStatic] static int razaoN;
        static void Aprende(int s, int g, float len)
        {
            float reta = Octil(s, g); if (reta < 1) return;
            var rz = razoes ??= new Dictionary<long, float>();
            if (rz.Count > 100000) rz.Clear();
            float r = float.IsInfinity(len) ? float.PositiveInfinity : len / reta;
            rz[ChaveRegiao(s, g)] = r;
            if (!float.IsInfinity(r) && razaoN < 1000000) { razaoSoma += r; razaoN++; }
        }
        [ThreadStatic] static int[] marcosAtivos; [ThreadStatic] static float[] marcosVal; [ThreadStatic] static Dictionary<long, float> pares; [ThreadStatic] static Dictionary<long, int> pedidos; public static int AEstrelas; public static Dictionary<string, int> QuemPede; public static Dictionary<string, int> QuemPedePasso;   // perfil: quem pediu A*/campo NESTE passo (o -perf limpa a cada Step)
        [ThreadStatic] static float[] gA; [ThreadStatic] static int[] marcaA, paiA; [ThreadStatic] static Link[] linkA; [ThreadStatic] static int geracaoA;

        /// <summary>O campo de distância até alvo, se já calculado (mesma chave do DistField); null se não.</summary>
        static float[] CampoNoCache(V2 target, int layer, out long key)
        {
            var ex = layers[layer];
            var (tx, ty) = NearestMovable(new V2(MathF.Floor(target.x / 20) * 20 + 10, MathF.Floor(target.y / 20) * 20 + 10));
            var (ox, oy) = NearestMovable(target);
            int qa = IndexOf(tx, ty), qb = IndexOf(ox, oy);
            if (qa < 0 || qb < 0 || !Straight(qa, qb)) (tx, ty) = (ox, oy);
            key = tx * NGY + ty + (ex != null ? (long)layer * NGX * NGY : 0);
            lock (cache) return fixos.TryGetValue(key, out var fx) ? fx : cache.TryGetValue(key, out var hit) ? hit : null;
        }

        /// <summary>
        /// Campo inteiro (~10 ms) só pra destino pedido de novo (3ª vez) e só 2 por passo do jogo: o resto vai de A* e o
        /// campo fica pra um passo seguinte — vinte campos no mesmo passo eram o engasgo de meio segundo no jogo ao vivo.
        /// </summary>
        static bool ValeCampo(long chave)
        {
            var pd = pedidos ??= new Dictionary<long, int>();
            pd.TryGetValue(chave, out int vezes); pd[chave] = vezes + 1;
            if (pd.Count > 4000) pd.Clear();
            return vezes >= 2 && (CELULAS_POR_PASSO > 0 || camposNoPasso < CAMPOS_POR_PASSO);
        }
        [ThreadStatic] static int camposNoPasso; const int CAMPOS_POR_PASSO = 1;
        /// <summary>Começo de um passo do jogo: zera o orçamento de campos inteiros.</summary>
        public static void NovoPasso() { camposNoPasso = 0; nosNoPasso = 0; passoN++; AvancaFila(); }

        static int[] celX, celY;   // coordenadas das células em arrays simples (o A* lê muito; tupla em lista é lenta no Mono)
        /// <summary>
        /// A* de s até g (mesmo custo do DistField). Tudo em variáveis locais: no Mono (Unity) cada acesso a [ThreadStatic]
        /// e cada função local com captura custam caro, e aqui isso roda por célula visitada.
        /// </summary>
        /// <summary>Cronômetro do NavGrid (a build do jogo liga): ms e quantidade de campos inteiros e de A*.</summary>
        public static bool Medir; public static double msCampo, msAEstrela; public static int nCampo, nAEstrela;
        static float AEstrela(int s, int g, float[] ex, bool comPais, float limite = float.PositiveInfinity, int maxNos = int.MaxValue)
        {
            if (!Medir) return AEstrela0(s, g, ex, comPais, limite, maxNos);
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            float r = AEstrela0(s, g, ex, comPais, limite, maxNos);
            msAEstrela += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; nAEstrela++;
            return r;
        }
        /// <summary>A* exato; maxNos: passou de tantas células sem chegar, desiste e devolve NaN (quem pediu adia).</summary>
        static float AEstrela0(int s, int g, float[] ex, bool comPais, float limite = float.PositiveInfinity, int maxNos = int.MaxValue)
        {
            if (Ilhado(s, g)) return float.PositiveInfinity;   // topo de pilha: só de pezinho (sem varrer o mapa)
            System.Threading.Interlocked.Increment(ref AEstrelas);
            if (QuemPede != null) lock (QuemPede) { var fr = new System.Diagnostics.StackTrace(2, false); string q = ""; for (int i = 0; i < Math.Min(3, fr.FrameCount); i++) q += (i > 0 ? " < " : "") + fr.GetFrame(i).GetMethod()?.Name; { var (gx0, gy0) = cells[g]; q += " → " + (AreaAt(gx0 * CELL + 1, gy0 * CELL + 1) ?? "?"); } QuemPede.TryGetValue(q, out int c0); QuemPede[q] = c0 + 1; if (QuemPedePasso != null) { int ia = q.IndexOf(" → "); string qp = ia > 0 ? q.Substring(0, ia) : q; QuemPedePasso.TryGetValue(qp, out int cp); QuemPedePasso[qp] = cp + 1; } }
            int nc = cells.Count;
            if (celX == null) { var cx0 = new int[nc]; var cy0 = new int[nc]; for (int k = 0; k < nc; k++) { cx0[k] = cells[k].x; cy0[k] = cells[k].y; } celY = cy0; celX = cx0; }
            if (gA == null || gA.Length != nc) { gA = new float[nc]; marcaA = new int[nc]; paiA = new int[nc]; linkA = new Link[nc]; geracaoA = 0; }
            int ger = ++geracaoA;
            float[] gl = gA; int[] mk = marcaA, pai = paiA; Link[] lnk = linkA; int[] cxs = celX, cys = celY;
            int gx = cxs[g], gy = cys[g];
            int[] hv = heapV ??= new int[1 << 16]; float[] hk = heapK ??= new float[1 << 16]; int n = 0;
            // marcos ativos pra este par: os MARCOS_ATIVOS que dão a maior estimativa na origem (o resto quase não ajuda)
            float[] m0 = null, m1 = null, m2 = null, m3 = null; float g0 = 0, g1 = 0, g2 = 0, g3 = 0;
            float[][] mc = marcos;
            if (ex != null) { mc = null; for (int ly = 1; ly < layers.Length; ly++) if (layers[ly] == ex) { mc = marcosCamada[ly]; break; } }
            if (UsaMarcos && mc != null)
            {
                var ativos = marcosAtivos ??= new int[MARCOS_ATIVOS]; var val = marcosVal ??= new float[MARCOS_ATIVOS]; int na = 0;
                for (int l = 0; l < mc.Length; l++)
                {
                    float dg = mc[l][g], ds = mc[l][s];
                    if (float.IsInfinity(dg)) continue;   // o destino não chega no marco: esse marco não limita nada
                    float v0 = float.IsInfinity(ds) ? float.MaxValue : ds - dg;
                    if (na < MARCOS_ATIVOS) { ativos[na] = l; val[na] = v0; na++; continue; }
                    int pior = 0; for (int q = 1; q < na; q++) if (val[q] < val[pior]) pior = q;
                    if (v0 > val[pior]) { ativos[pior] = l; val[pior] = v0; }
                }
                if (na > 0) { m0 = mc[ativos[0]]; g0 = m0[g]; }
                if (na > 1) { m1 = mc[ativos[1]]; g1 = m1[g]; }
                if (na > 2) { m2 = mc[ativos[2]]; g2 = m2[g]; }
                if (na > 3) { m3 = mc[ativos[3]]; g3 = m3[g]; }
            }
            gl[s] = 0; mk[s] = ger;
            {
                int dx = Math.Abs(cxs[s] - gx), dy = Math.Abs(cys[s] - gy); float h0 = CELL * (Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy));
                if (m0 != null) { h0 = MathF.Max(h0, m0[s] - g0); if (m1 != null) h0 = MathF.Max(h0, m1[s] - g1); if (m2 != null) h0 = MathF.Max(h0, m2[s] - g2); if (m3 != null) h0 = MathF.Max(h0, m3[s] - g3); }
                if (float.IsInfinity(h0)) { nosNoPasso += 1; NosAEstrela += 1; return float.PositiveInfinity; }   // a origem não chega num marco que o destino alcança: não chega no destino
                hv[0] = s; hk[0] = h0; n = 1;
            }
            int nos = 0;
            while (n > 0)
            {
                int u = hv[0]; float fu = hk[0]; nos++;
                if (fu > limite) break;
                if (nos > maxNos) { nosNoPasso += nos; NosAEstrela += nos; System.Threading.Interlocked.Increment(ref Desistiu); return float.NaN; }   // o menor f da fila já passa do limite: o caminho é mais longo que o limite (infinito pra quem perguntou)
                int lv = hv[--n]; float lkk = hk[n]; int i = 0;
                while (true) { int c = 2 * i + 1; if (c >= n) break; if (c + 1 < n && hk[c + 1] < hk[c]) c++; if (hk[c] >= lkk) break; hv[i] = hv[c]; hk[i] = hk[c]; i = c; }
                if (n > 0) { hv[i] = lv; hk[i] = lkk; }
                float gu = gl[u];
                int hdx = Math.Abs(cxs[u] - gx), hdy = Math.Abs(cys[u] - gy);
                float hu = CELL * (Math.Max(hdx, hdy) + 0.4142f * Math.Min(hdx, hdy));
                if (m0 != null) { hu = MathF.Max(hu, m0[u] - g0); if (m1 != null) hu = MathF.Max(hu, m1[u] - g1); if (m2 != null) hu = MathF.Max(hu, m2[u] - g2); if (m3 != null) hu = MathF.Max(hu, m3[u] - g3); }
                if (fu > gu + hu + 1e-3f) continue;   // entrada velha do heap
                if (u == g) { if (comPais) { paiA = pai; linkA = lnk; } nosNoPasso += nos; NosAEstrela += nos; if (Gigantes != null && nos > 40000) Gigante(nos, s, g, ex != null, gu); return gu; }
                var o = outgoing[u]; float extra = ex != null ? ex[u] : 0;
                for (int e = 0; e < o.Count; e++)
                {
                    var (v, c, l) = o[e];
                    float nd = gu + c + extra;   // mesmo custo do DistField (o extra é da célula de onde sai)
                    if (mk[v] == ger && nd >= gl[v]) continue;
                    gl[v] = nd; mk[v] = ger; if (comPais) { pai[v] = u; lnk[v] = l; }
                    int vdx = Math.Abs(cxs[v] - gx), vdy = Math.Abs(cys[v] - gy);
                    float hv0 = CELL * (Math.Max(vdx, vdy) + 0.4142f * Math.Min(vdx, vdy));
                    if (m0 != null) { hv0 = MathF.Max(hv0, m0[v] - g0); if (m1 != null) hv0 = MathF.Max(hv0, m1[v] - g1); if (m2 != null) hv0 = MathF.Max(hv0, m2[v] - g2); if (m3 != null) hv0 = MathF.Max(hv0, m3[v] - g3); }
                    if (float.IsInfinity(hv0)) continue;   // dali não chega no destino
                    float k = nd + hv0;
                    if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); heapV = hv; heapK = hk; }
                    int j2 = n++;
                    while (j2 > 0) { int pp = (j2 - 1) >> 1; if (hk[pp] <= k) break; hv[j2] = hv[pp]; hk[j2] = hk[pp]; j2 = pp; }
                    hv[j2] = v; hk[j2] = k;
                }
            }
            nosNoPasso += nos; NosAEstrela += nos;
            if (Gigantes != null && nos > 40000) Gigante(nos, s, g, ex != null, float.PositiveInfinity);
            return float.PositiveInfinity;
        }
        /// <summary>Perfil (-perf -gigantes): A* que varreu mais de 40k células — quem pediu, de onde pra onde, se chegou.</summary>
        public static List<string> Gigantes;
        static void Gigante(int nos, int s, int g, bool camada, float r)
        {
            var fr = new System.Diagnostics.StackTrace(3, false); string q = "";
            for (int i = 0; i < Math.Min(4, fr.FrameCount); i++) q += (i > 0 ? " < " : "") + fr.GetFrame(i).GetMethod()?.Name;
            var (sx, sy) = cells[s]; var (gx, gy) = cells[g];
            lock (Gigantes) Gigantes.Add($"{nos,6} células {(float.IsInfinity(r) ? "NÃO CHEGA" : $"{r:0}")}{(camada ? " com camada" : "")}  {AreaAt(sx * CELL + 1, sy * CELL + 1) ?? "?"} ({sx * CELL},{sy * CELL}) → {AreaAt(gx * CELL + 1, gy * CELL + 1) ?? "?"} ({gx * CELL},{gy * CELL}) z {z[gx, gy]:0}  ← {q}");
        }

        /// <summary>
        /// Distância (ida, exata) de origem até cada um dos alvos, numa busca só que PARA quando alcançou todos ou passou
        /// do limite: medir 15-24 lugares candidatos pra cada jogador sem calcular o campo do mapa inteiro (no Mono o campo
        /// inteiro por jogador pesava mais que tudo no tique do IGL). Alvo que não alcança: infinito.
        /// </summary>
        public static float[] DistanciasAte(V2 origem, List<V2> alvos, float limite)
        {
            var res = new float[alvos.Count];
            for (int i = 0; i < res.Length; i++) res[i] = float.PositiveInfinity;
            var (sx, sy) = NearestMovable(origem); int s0 = idx[sx * NGY + sy];
            if (s0 < 0 || alvos.Count == 0) return res;
            var alvoDe = new Dictionary<int, List<int>>(); int faltam = 0;
            for (int i = 0; i < alvos.Count; i++)
            {
                var (x, y) = NearestMovable(alvos[i]); int c = idx[x * NGY + y]; if (c < 0) continue;
                if (!alvoDe.TryGetValue(c, out var l)) alvoDe[c] = l = new List<int>();
                l.Add(i); faltam++;
            }
            int nc = cells.Count;
            if (gA == null || gA.Length != nc) { gA = new float[nc]; marcaA = new int[nc]; paiA = new int[nc]; linkA = new Link[nc]; geracaoA = 0; }
            int ger = ++geracaoA; float[] gl = gA; int[] mk = marcaA;
            int[] hv = heapV ??= new int[1 << 16]; float[] hk = heapK ??= new float[1 << 16]; int n = 1;
            gl[s0] = 0; mk[s0] = ger; hv[0] = s0; hk[0] = 0;
            while (n > 0 && faltam > 0)
            {
                int u = hv[0]; float du = hk[0];
                int lv = hv[--n]; float lk = hk[n]; int i = 0;
                while (true) { int c = 2 * i + 1; if (c >= n) break; if (c + 1 < n && hk[c + 1] < hk[c]) c++; if (hk[c] >= lk) break; hv[i] = hv[c]; hk[i] = hk[c]; i = c; }
                if (n > 0) { hv[i] = lv; hk[i] = lk; }
                if (du > gl[u]) continue;
                if (du > limite) break;
                if (alvoDe.TryGetValue(u, out var achou)) { foreach (int k in achou) res[k] = du; faltam -= achou.Count; alvoDe.Remove(u); }
                var o = outgoing[u];
                for (int e = 0; e < o.Count; e++)
                {
                    var (v, c, _) = o[e];
                    float nd = du + c;
                    if (mk[v] == ger && nd >= gl[v]) continue;
                    gl[v] = nd; mk[v] = ger;
                    if (n == hv.Length) { Array.Resize(ref hv, n * 2); Array.Resize(ref hk, n * 2); heapV = hv; heapK = hk; }
                    int j = n++;
                    while (j > 0) { int pp = (j - 1) >> 1; if (hk[pp] <= nd) break; hv[j] = hv[pp]; hk[j] = hk[pp]; j = pp; }
                    hv[j] = v; hk[j] = nd;
                }
            }
            return res;
        }

        public struct Waypoint { public V2 p; public float z; public Link link; }   // link = como se chega neste ponto

        /// <summary>
        /// Caminho de a até b: desce o campo de distância célula a célula e depois corta os cantos — junta trechos
        /// de andar enquanto a linha reta passa só por células andáveis sem degrau grande. Pulos, quedas e escada
        /// viram pontos próprios (o bot sabe quando pular/cair/subir). Vazio se não dá pra chegar.
        /// </summary>
        public static List<Waypoint> Path(V2 a, V2 b, int layer = 0, bool soSeCouber = false)
        {
            var res = new List<Waypoint>();
            var (sx, sy) = NearestMovable(a);
            int cur = idx[sx * NGY + sy];
            long chave = 0; float[] f = PathLenSoCampo ? DistField(b, layer) : CampoNoCache(b, layer, out chave);
            if (f == null && !PathLenSoCampo && ValeCampo(chave)) Enfileira(b, layer);   // fica pronto em alguns passos; agora vai de A*
            if (f == null)
            {   // destino novo: A* entre os dois (o caminho é o mesmo; o campo inteiro só se ele voltar a ser pedido)
                if (cur < 0) return res;
                var (bx, by) = NearestMovable(b); int g = idx[bx * NGY + by];
                if (g < 0) return res;
                // quem só quer o caminho pra estimar (o pixel da mira), com o passo estourado: null = adia pro próximo passo
                if (soSeCouber && g != cur && Estourou) { System.Threading.Interlocked.Increment(ref Negados); Conta("ADIA: ", 1); return null; }
                // pedido que só estima (o pixel) e é longe demais pra um A* caber no passo: o campo desse destino já está
                // sendo calculado em pedaços — adia até ficar pronto (aí sai do campo, de graça) em vez de outro A* gigante
                bool teto = soSeCouber && OrcamentoA && TetoPixel && !PathLenSoCampo && g != cur;
                if (teto && naFila != null && naFila.Contains(chave)) { System.Threading.Interlocked.Increment(ref Negados); Conta("ADIA (campo vindo): ", 1); return null; }
                var cam = new List<(int cell, Link link)>();
                if (g != cur)
                {
                    float rA = AEstrela(cur, g, layers[layer], true, float.PositiveInfinity, teto ? ORC_NOS_PASSO : int.MaxValue);
                    if (float.IsNaN(rA)) { Enfileira(b, layer); System.Threading.Interlocked.Increment(ref Negados); Conta("ADIA (A* grande, campo na fila): ", 1); return null; }
                    if (float.IsInfinity(rA)) return res;
                    for (int c = g, guard = 0; c != cur && guard < 100000; c = paiA[c], guard++) cam.Add((c, linkA[c]));
                }
                cam.Add((cur, Link.Walk)); cam.Reverse();
                return Cantos(cam, b);
            }
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
            return Cantos(raw, b);
        }

        /// <summary>Do caminho célula a célula pros pontos que o bot segue: corta os cantos onde dá pra ir reto.</summary>
        static List<Waypoint> Cantos(List<(int cell, Link link)> raw, V2 b)
        {
            var res = new List<Waypoint>();
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
