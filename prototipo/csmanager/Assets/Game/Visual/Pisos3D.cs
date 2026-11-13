using System.Collections.Generic;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Piso trocado só no visual (a Sim continua com o material dela pra colisão): a plataforma da banca do bomb B
    /// ("Banca" e "Esc_banca", Floor.Stone na Sim, xadrez de pedra) vira deck de tábuas como o do casarão (usuário).
    /// O som dos passos lá também vira madeira (PartidaAoVivo3D.Chao usa Deck).
    /// </summary>
    public static class Pisos3D
    {
        static readonly string[] DECK = { "Banca", "Esc_banca" };
        static bool[,] deck;

        /// <summary>Células do deck de madeira.</summary>
        public static bool[,] Deck()
        {
            if (deck != null) return deck;
            deck = new bool[MapData.NX, MapData.NY];
            var nomes = new HashSet<string>(DECK);
            foreach (var s in MapData.areas)
            {
                if (!nomes.Contains(s.name)) continue;
                for (int x = System.Math.Max(0, s.box.x0); x < System.Math.Min(MapData.NX, s.box.x1); x++)
                    for (int y = System.Math.Max(0, s.box.y0); y < System.Math.Min(MapData.NY, s.box.y1); y++)
                        if (s.has == null || s.has(x, y)) deck[x, y] = true;
            }
            return deck;
        }

        /// <summary>Nicho de cenário (MapData.Nook: recuo com jarros/banca/plantas) ganha Floor.Stone na Sim e desenhava o
        /// xadrez do palácio no meio da areia (usuário: "pisos soltos que viram o piso do palácio"). O piso de cima de cada
        /// casa de nicho copia o da rua mais perto (casa andável fora de cobertura e que não é pedra). Devolve uma cópia.</summary>
        public static byte[] Harmonizar(byte[] G)
        {
            var g = (byte[])G.Clone(); int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, n = 0;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (MapData.coverH[x, y] == 0 || MapData.fmat[x, y] != MapData.Floor.Stone || !MapData.walk[x, y]) continue;
                    int z = MapData.floor[x, y] - 1; if (z < 0 || z >= NZ) continue;
                    // rua mais perto (anel por anel até 8)
                    int fx = -1, fy = -1;
                    for (int r = 1; r <= 8 && fx < 0; r++)
                        for (int dx = -r; dx <= r && fx < 0; dx++)
                            for (int dy = -r; dy <= r && fx < 0; dy++)
                            {
                                if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= NX || yy >= NY) continue;
                                if (MapData.walk[xx, yy] && MapData.coverH[xx, yy] == 0 && MapData.fmat[xx, yy] != MapData.Floor.Stone && MapData.floor[xx, yy] == MapData.floor[x, y]) { fx = xx; fy = yy; }
                            }
                    if (fx < 0) continue;
                    g[(x * NY + y) * NZ + z] = g[(fx * NY + fy) * NZ + z]; n++;
                }
            UnityEngine.Debug.Log($"[Pisos3D] nichos: {n} casas com o piso da rua");
            return g;
        }

        /// <summary>Lasca de parede em cima do telhado (usuário: "essa parede saindo ali tá bugado"): uma tira de prédio com
        /// até 3 de espessura que passa de 4 a 16 acima do telhado vizinho aparecia como uma placa solta. Só no desenho, a
        /// tira desce pra altura do telhado do lado (a mureta de 1 de altura e os muros soltos no chão ficam como são).</summary>
        static void Lascas(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, n = 0;
            var h = new int[NX, NY];
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    int t = 0;
                    if (!MapData.walk[x, y]) for (int z = NZ - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { t = z + 1; break; }
                    h[x, y] = t;
                }
            var corta = new int[NX, NY];
            for (int x = 3; x < NX - 3; x++)
                for (int y = 3; y < NY - 3; y++)
                {
                    if (MapData.walk[x, y] || h[x, y] == 0) continue;
                    int t = h[x, y], alvo = -1;
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        // fina neste eixo: dos dois lados, a até 3 de distância, tem chão ou prédio pelo menos 4 mais baixo
                        int la = -1, lb = -1;
                        for (int d = 1; d <= 3 && la < 0; d++) { int u = x - dx * d, v = y - dy * d; if (MapData.walk[u, v] || h[u, v] <= t - 4) la = MapData.walk[u, v] ? 0 : h[u, v]; }
                        for (int d = 1; d <= 3 && lb < 0; d++) { int u = x + dx * d, v = y + dy * d; if (MapData.walk[u, v] || h[u, v] <= t - 4) lb = MapData.walk[u, v] ? 0 : h[u, v]; }
                        if (la < 0 || lb < 0) continue;
                        int tel = System.Math.Max(la, lb);          // telhado do lado (0 = os dois lados são chão: muro solto, fica)
                        if (tel > 0 && t - tel <= 16) alvo = System.Math.Max(alvo, tel);
                    }
                    corta[x, y] = alvo;
                }
            for (int x = 3; x < NX - 3; x++)
                for (int y = 3; y < NY - 3; y++)
                {
                    int alvo = corta[x, y]; if (alvo <= 0) continue;
                    byte topo = g[(x * NY + y) * NZ + h[x, y] - 1];
                    for (int z = alvo; z < h[x, y]; z++) { g[(x * NY + y) * NZ + z] = 0; n++; }
                    g[(x * NY + y) * NZ + alvo - 1] = topo == 50 || topo == 51 ? topo : (byte)8;
                }
            UnityEngine.Debug.Log($"[Pisos3D] lascas de parede acima do telhado: {n} voxels tirados");
        }

        /// <summary>Terraços da medina no lugar da telha (usuário: telha demais; depois "cara de pastel, bloco de pedra":
        /// cada casa tem que ser diferente, com elementos marroquinos, sem poluir). Cada telhado (pedaço ligado na mesma
        /// altura) sorteia um piso: lajota de barro, lajota areia, cal branca, cimento, pedra ou azulejo verde-água; o
        /// desenho muda de tamanho e de sentido (reto ou em diagonal) por casa; alguns têm barra colorida por dentro da
        /// mureta e, nos maiores, um tapete listrado estendido inteiro. Só no desenho.</summary>
        static void Terracos(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ;
            var id = new int[NX, NY]; var dist = new int[NX, NY];
            var comps = new List<List<(int x, int y)>>();
            var fila = new Queue<(int, int)>();
            for (int x0 = 0; x0 < NX; x0++)
                for (int y0 = 0; y0 < NY; y0++)
                {
                    if (id[x0, y0] != 0 || MapData.walk[x0, y0]) continue;
                    int t = CSM.View.MapVoxels.Topo(x0, y0); if (t < 1 || t > NZ) continue;
                    var c = new List<(int, int)>(); comps.Add(c); int k = comps.Count;
                    id[x0, y0] = k; fila.Enqueue((x0, y0));
                    while (fila.Count > 0)
                    {
                        var (x, y) = fila.Dequeue(); c.Add((x, y));
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int u = x + dx, v = y + dy;
                            if (u < 0 || v < 0 || u >= NX || v >= NY || id[u, v] != 0 || MapData.walk[u, v] || CSM.View.MapVoxels.Topo(u, v) != t) continue;
                            id[u, v] = k; fila.Enqueue((u, v));
                        }
                    }
                }
            // distância até a beira do próprio telhado (até 12)
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (id[x, y] == 0) continue;
                    int d = 12;
                    for (int r = 1; r < 12 && d == 12; r++)
                        for (int dx = -r; dx <= r && d == 12; dx++)
                            for (int dy = -r; dy <= r && d == 12; dy++)
                            {
                                if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                                int u = x + dx, v = y + dy;
                                if (u < 0 || v < 0 || u >= NX || v >= NY || id[u, v] != id[x, y]) d = r;
                            }
                    dist[x, y] = d;
                }
            int n = 0, tapetes = 0; var conta = new int[6];
            for (int k = 1; k <= comps.Count; k++)
            {
                var c = comps[k - 1];
                uint hs = (uint)(c[0].x * 73856093) ^ (uint)(c[0].y * 19349663) ^ (uint)(c.Count * 83492791); hs ^= hs >> 13; hs *= 0x5bd1e995; hs ^= hs >> 15;
                int sorte = (int)(hs % 100), passo = 8 + (int)((hs >> 8) % 3) * 2; bool diag = ((hs >> 12) & 3) == 0;
                // piso: (campo, junta). 0 barro, 1 areia, 2 cal, 3 cimento, 4 pedra, 5 azulejo
                int tipo = sorte < 30 ? 0 : sorte < 54 ? 1 : sorte < 70 ? 2 : sorte < 82 ? 3 : sorte < 92 ? 4 : 5;
                if (tipo == 5 && c.Count < 500) tipo = 1;
                conta[tipo]++;
                byte campo = tipo == 0 ? (byte)63 : tipo == 1 ? (byte)56 : tipo == 2 ? (byte)54 : tipo == 3 ? (byte)60 : tipo == 4 ? (byte)8 : (byte)54;
                byte junta = tipo == 0 ? (byte)12 : tipo == 1 ? (byte)57 : tipo == 2 ? (byte)55 : tipo == 3 ? (byte)61 : tipo == 4 ? (byte)8 : (byte)16;
                // barra por dentro da mureta em ~1/3 das casas grandes: verde-água, azul ou barro
                byte barra = 0; if (c.Count > 350 && ((hs >> 16) % 3) == 0) barra = tipo == 0 ? (byte)56 : ((hs >> 20) & 1) == 0 ? (byte)16 : (byte)63;
                // tapete: só em telhado grande, no ponto mais longe da beira, se couber inteiro (16 x 10)
                int tx = -1, ty = -1; bool tapete = false && c.Count > 700 && ((hs >> 24) % 4) == 0;   // (desligado: no desenho por voxel o tapete saía em tracinhos soltos)
                bool deitado = ((hs >> 27) & 1) == 0;
                if (tapete)
                {
                    int melhor = 0; foreach (var (x, y) in c) if (dist[x, y] > melhor) { melhor = dist[x, y]; tx = x; ty = y; }
                    if (melhor < 11) tapete = false; else tapetes++;
                }
                foreach (var (x, y) in c)
                {
                    int z = CSM.View.MapVoxels.Topo(x, y) - 1; if (z < 0 || z >= NZ) continue;
                    int i = (x * NY + y) * NZ + z; if (g[i] != 50 && g[i] != 51) continue;
                    int u = diag ? x + y : x, v = diag ? x - y : y;
                    int fu = ((u % passo) + passo) % passo, fv = ((v % passo) + passo) % passo;
                    byte m = fu == 0 || fv == 0 ? junta : campo;
                    if (tipo == 5) m = (fu <= 1 && fv <= 1) || (fu == passo / 2 && fv == passo / 2) ? (byte)16 : (fu == 0 || fv == 0 ? (byte)57 : (byte)54);
                    if (barra != 0 && (dist[x, y] == 4 || dist[x, y] == 5)) m = barra;
                    if (tapete)
                    {
                        int rx = deitado ? x - tx : y - ty, ry = deitado ? y - ty : x - tx;      // rx ao longo do comprimento
                        if (System.Math.Abs(rx) <= 8 && System.Math.Abs(ry) <= 5)
                        {
                            int fx = rx + 8;
                            m = System.Math.Abs(ry) == 5 || fx == 0 || fx == 16 ? (byte)8 : (fx / 2) % 4 == 0 ? (byte)5 : (fx / 2) % 4 == 2 ? (byte)9 : (fx % 4 == 1 ? (byte)43 : (byte)8);
                        }
                    }
                    g[i] = m; n++;
                }
            }
            UnityEngine.Debug.Log($"[Pisos3D] terraços: {comps.Count} telhados, {n} voxels; barro {conta[0]}, areia {conta[1]}, cal {conta[2]}, cimento {conta[3]}, pedra {conta[4]}, azulejo {conta[5]}; tapetes {tapetes}");
        }

        /// <summary>Chão de jogo com a mesma mão das paredes novas (usuário: "a textura do chão também podia dar uma mudada",
        /// e "difícil saber onde os bots jogam"): o ladrilho xadrez de voxel vira calçamento de lajes e a terra manchada vira
        /// areia lisa — um material só por tipo, e o desenho (lajes, ondinhas) sai no Maquete.shader. Só o voxel de cima das
        /// casas andáveis; mármore, lajota, cimento, deck e escadas ficam como são.</summary>
        static void Chao(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, nl = 0, na = 0, nc = 0;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (!MapData.walk[x, y]) continue;
                    int z = MapData.floor[x, y] - 1; if (z < 0 || z >= NZ) continue;
                    int i = (x * NY + y) * NZ + z; byte m = g[i];
                    if (m == 0)
                    {   // debaixo da escada de mão do janelão o piso da Sim sobe em rampa mas o chão desenhado fica na rua: pega o topo
                        // de verdade da coluna e põe a laje da rua (usuário, print: "textura bugada do chão embaixo da escada")
                        while (z > 0 && g[(x * NY + y) * NZ + z] == 0) z--;
                        i = (x * NY + y) * NZ + z; m = g[i];
                        if (m == 1 || m == 2 || m == 3 || m == 14 || m == 15 || m == 18 || m == 58 || m == 59) { g[i] = (byte)(MapData.fmat[x, y] == MapData.Floor.Dirt ? Paleta3D.AREIA : Paleta3D.LAJE); nl++; }
                        continue;
                    }
                    if ((m == 46 || m == 47) && MapData.fmat[x, y] == MapData.Floor.Wood) { g[i] = (byte)Paleta3D.LADRILHO; nc++; continue; }   // casarão
                    bool cru = m == 1 || m == 2 || m == 3 || m == 14 || m == 18 || m == 58 || m == 59;
                    if (!cru) continue;
                    if (MapData.fmat[x, y] == MapData.Floor.Tile) { g[i] = (byte)Paleta3D.LAJE; nl++; }
                    else if (MapData.fmat[x, y] == MapData.Floor.Dirt) { g[i] = (byte)Paleta3D.AREIA; na++; }
                }
            UnityEngine.Debug.Log($"[Pisos3D] chão de jogo: {nl} colunas de laje, {na} de areia, {nc} de ladrilho no casarão");
        }

        /// <summary>Janelão no padrão da Vila (usuário: "precisamos mudar o janelão pra ficar com o padrão visual do mapa"): o
        /// bloco de cimento (60-62, pintado em Pedras) vira adobe de um tom só, e o piso onde se anda vira o ladrilho de barro
        /// do casarão. Só troca a cor dos voxels que já eram cimento: forma, mureta e alturas ficam iguais.</summary>
        static void Janelao(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, np = 0, nc = 0; const int TOM = 4;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    int topo = -1; for (int z = NZ - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { topo = z; break; }
                    int tom = TOM;
                    if (!MapData.walk[x, y] && CasaDe != null)
                        for (int r = 1; r <= 2 && tom == TOM; r++)
                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                int u = x + dx * r, v = y + dy * r; if (u < 0 || v < 0 || u >= NX || v >= NY || CasaDe[u, v] < 0) continue;
                                var c = Casas[CasaDe[u, v]]; if (c.palacio || c.cel.Count < 40 || Alt[u, v] < topo) continue;
                                tom = c.tom; break;
                            }
                    for (int z = 0; z <= topo; z++)
                    {
                        int i = (x * NY + y) * NZ + z; if (g[i] < 60 || g[i] > 62) continue;
                        if (z == topo && MapData.walk[x, y]) { g[i] = MapData.floor[x, y] >= MapData.UP - 1 ? (byte)Paleta3D.LADRILHO : (byte)(MapData.fmat[x, y] == MapData.Floor.Dirt ? Paleta3D.AREIA : Paleta3D.LAJE); nc++; }   // (piso baixo = chão debaixo da escada de mão: fica igual à rua)
                        else { g[i] = (byte)(Paleta3D.ADOBE + tom); np++; }
                    }
                }
            UnityEngine.Debug.Log($"[Pisos3D] janelão: {np} voxels de cimento viraram adobe, {nc} colunas de piso viraram ladrilho");
        }

        /// <summary>O que sobrou de reboco antigo (usuário: "arruma a parte de baixo ali do casarão que tá com a textura antiga
        /// ainda"): o Ksar só pinta coluna de prédio; a parede debaixo de piso alto onde se anda (casarão, palácio, plataforma)
        /// e os muros baixos ficavam com o reboco creme descascado (6/7) e o arremate (8). Cada pedaço ligado vira adobe de um
        /// tom só: o da casa em que ele mais encosta, ou o ocre do janelão se não encosta em nenhuma. Só troca a cor.</summary>
        static void Resto(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, nv = 0, npc = 0, nchao = 0;
            var tem = new bool[NX, NY];
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                    for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ; z++) { byte m = g[(x * NY + y) * NZ + z]; if (m == 6 || m == 7 || m == 8) { tem[x, y] = true; break; } }
            // topo de coluna onde se anda nunca vira parede: é chão (laje ou areia, como a rua em volta)
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (!tem[x, y] || !MapData.walk[x, y]) continue;
                    int topo = -1; for (int z = NZ - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { topo = z; break; }
                    if (topo < 0) continue; int it = (x * NY + y) * NZ + topo;
                    if (g[it] == 6 || g[it] == 7 || g[it] == 8) { g[it] = (byte)(MapData.fmat[x, y] == MapData.Floor.Dirt ? Paleta3D.AREIA : Paleta3D.LAJE); nchao++; }
                }
            var visto = new bool[NX, NY]; var fila = new Queue<(int, int)>(); var D4 = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            for (int x0 = 0; x0 < NX; x0++)
                for (int y0 = 0; y0 < NY; y0++)
                {
                    if (!tem[x0, y0] || visto[x0, y0]) continue;
                    var cel = new List<(int, int)>(); var conta = new Dictionary<int, int>();
                    visto[x0, y0] = true; fila.Enqueue((x0, y0));
                    while (fila.Count > 0)
                    {
                        var (x, y) = fila.Dequeue(); cel.Add((x, y));
                        foreach (var (dx, dy) in D4)
                        {
                            int u = x + dx, v = y + dy; if (u < 0 || v < 0 || u >= NX || v >= NY) continue;
                            if (tem[u, v]) { if (!visto[u, v]) { visto[u, v] = true; fila.Enqueue((u, v)); } continue; }
                            int k = CasaDe != null ? CasaDe[u, v] : -1;
                            if (k >= 0 && !Casas[k].palacio && Casas[k].cel.Count >= 40) conta[k] = conta.TryGetValue(k, out int q) ? q + 1 : 1;
                        }
                    }
                    int tom = 4, best = 0; foreach (var kv in conta) if (kv.Value > best) { best = kv.Value; tom = Casas[kv.Key].tom; }
                    byte par = (byte)(Paleta3D.ADOBE + tom); npc++;
                    foreach (var (x, y) in cel)
                        for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ; z++) { int i = (x * NY + y) * NZ + z; if (g[i] == 6 || g[i] == 7 || g[i] == 8) { g[i] = par; nv++; } }
                }
            UnityEngine.Debug.Log($"[Pisos3D] resto de reboco antigo: {nv} voxels em {npc} pedaços viraram adobe; {nchao} colunas de chão corrigidas");
        }

        public class Casa { public List<(int x, int y)> cel = new List<(int, int)>(); public int topo, tom, dono = -1; public bool palacio; public HashSet<int> viz = new HashSet<int>(); }
        /// <summary>Casas do ksar (cada pedaço de prédio ligado e de mesma altura), a casa de cada coluna (-1 = nenhuma) e a
        /// altura desenhada de cada coluna depois da platibanda e das ameias. Pro Tapetes3D.</summary>
        public static readonly List<Casa> Casas = new List<Casa>();
        public static int[,] CasaDe, Alt;
        /// <summary>Casas do quarteirão do palácio (as do corredor de mármore, as coladas nelas e os blocos nivelados).</summary>
        public static readonly HashSet<int> Quarteirao = new HashSet<int>();
        public static bool NoPalacio(int x, int y) => CasaDe != null && x >= 0 && y >= 0 && x < MapData.NX && y < MapData.NY && CasaDe[x, y] >= 0 && Quarteirao.Contains(CasaDe[x, y]);
        public const int PLATIBANDA = 3;

        /// <summary>Vila refeita como ksar marroquino de terra (usuário, com foto de Aït Benhaddou: "refaz o visual das
        /// casinhas, das telhas, das paredes, das texturas... as casas podem mudar um pouco de cor"). Só no desenho; a Sim
        /// não vê nada disto. Cada pedaço de prédio ligado e da mesma altura é uma casa: ganha um dos 8 tons de adobe
        /// (diferente das vizinhas), o telhado de telha vira terraço de terra batida no tom dela, a beira ganha platibanda
        /// de adobe (3 de altura) e as quinas, ameias em degrau; uma parte das casas tem a platibanda toda dentada.</summary>
        static void Ksar(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ;
            var h = new int[NX, NY];
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    int t = 0;
                    if (!MapData.walk[x, y]) for (int z = NZ - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { t = z + 1; break; }
                    h[x, y] = t;
                }
            var id = new int[NX, NY]; for (int x = 0; x < NX; x++) for (int y = 0; y < NY; y++) id[x, y] = -1;
            Casas.Clear(); var fila = new Queue<(int, int)>(); var D4 = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            for (int x0 = 0; x0 < NX; x0++)
                for (int y0 = 0; y0 < NY; y0++)
                {
                    if (id[x0, y0] >= 0 || h[x0, y0] <= MapData.GZ + 2) continue;
                    var c = new Casa(); int k = Casas.Count; Casas.Add(c);
                    id[x0, y0] = k; fila.Enqueue((x0, y0));
                    while (fila.Count > 0)
                    {
                        var (x, y) = fila.Dequeue(); c.cel.Add((x, y));
                        foreach (var (dx, dy) in D4)
                        {
                            int u = x + dx, v = y + dy;
                            if (u < 0 || v < 0 || u >= NX || v >= NY || id[u, v] >= 0 || h[u, v] <= MapData.GZ + 2 || System.Math.Abs(h[u, v] - h[x, y]) > 2) continue;
                            id[u, v] = k; fila.Enqueue((u, v));
                        }
                    }
                    var conta = new Dictionary<int, int>(); int best = 0;
                    foreach (var (x, y) in c.cel) { conta[h[x, y]] = conta.TryGetValue(h[x, y], out int q) ? q + 1 : 1; }
                    foreach (var kv in conta) if (kv.Value > best || (kv.Value == best && kv.Key < c.topo)) { best = kv.Value; c.topo = kv.Key; }
                }
            for (int x = 0; x < NX - 1; x++)
                for (int y = 0; y < NY - 1; y++)
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        int a = id[x, y], b = id[x + dx, y + dy];
                        if (a >= 0 && b >= 0 && a != b) { Casas[a].viz.Add(b); Casas[b].viz.Add(a); }
                    }
            // tom: as casas grandes primeiro, cada uma com um tom que nenhuma vizinha já usa; pedacinho (< 40) copia a vizinha maior
            var ordem = new List<int>(); for (int k = 0; k < Casas.Count; k++) { ordem.Add(k); Casas[k].tom = -1; }
            ordem.Sort((a, b) => Casas[b].cel.Count.CompareTo(Casas[a].cel.Count));
            foreach (int k in ordem)
            {
                var c = Casas[k];
                uint hs = (uint)(c.cel[0].x * 73856093) ^ (uint)(c.cel[0].y * 19349663); hs ^= hs >> 13; hs *= 0x5bd1e995; hs ^= hs >> 15;
                if (c.cel.Count < 40)
                {
                    int maior = -1; foreach (int v in c.viz) if (Casas[v].tom >= 0 && (maior < 0 || Casas[v].cel.Count > Casas[maior].cel.Count)) maior = v;
                    if (maior >= 0) { c.tom = Casas[maior].tom; continue; }
                }
                for (int t = 0; t < Paleta3D.NTONS && c.tom < 0; t++)
                {
                    int tom = (int)((hs + (uint)t) % Paleta3D.NTONS); bool usado = false;
                    foreach (int v in c.viz) if (Casas[v].tom == tom) usado = true;
                    if (!usado) c.tom = tom;
                }
                if (c.tom < 0) c.tom = (int)(hs % Paleta3D.NTONS);
            }
            // palácio: a casa que encosta nos cômodos de mármore (pelo menos 12 colunas a até 3 de um piso do palácio)
            var noPalacio = new bool[NX, NY]; var nomesP = new HashSet<string>(PALACIO);
            foreach (var s in MapData.areas)
            {
                if (!nomesP.Contains(s.name)) continue;
                for (int x = System.Math.Max(0, s.box.x0); x < System.Math.Min(NX, s.box.x1); x++)
                    for (int y = System.Math.Max(0, s.box.y0); y < System.Math.Min(NY, s.box.y1); y++)
                        if (MapData.walk[x, y] && (s.has == null || s.has(x, y))) noPalacio[x, y] = true;
            }
            int npal = 0;
            foreach (var c in Casas)
            {
                int perto = 0;
                foreach (var (x, y) in c.cel)
                {
                    bool p = false;
                    for (int r = 1; r <= 3 && !p; r++)
                        foreach (var (dx, dy) in D4) { int u = x + dx * r, v = y + dy * r; if (u >= 0 && v >= 0 && u < NX && v < NY && noPalacio[u, v]) { p = true; break; } }
                    if (p) perto++;
                }
                c.palacio = perto >= 12; if (c.palacio) npal++;
            }
            // (teste, usuário: "não parece um palácio... umas casas em volta esquisitas") o palácio passa a ser o quarteirão: as
            // casas coladas nas dele entram junto — mesmo muro de cal, mesmas ameias — e o telhado de todas é de telha verde
            // REPROVADO pelo usuário ("ficou uma coisa verde, uma bosta"): o quarteirão inteiro com telhado de telha verde. Ficam
            // só os muros pintados das duas casas do corredor, com terraço de terra como as outras e sem capa verde nas ameias.
            // quarteirão do palácio sem "casas no meio" (usuário, dois prints: "tira essas casas no meio do palácio"): os blocos que
            // ficam em cima ou afundados no telhado das casas do palácio e das coladas nele (pelo menos 70% da beira encostada em
            // casa do quarteirão) são nivelados com o telhado da casa em volta e ficam do mesmo tom. Só no desenho.
            var nucleoP = new HashSet<int>(); for (int k = 0; k < Casas.Count; k++) if (Casas[k].palacio) nucleoP.Add(k);
            var quarteirao = new HashSet<int>();
            for (int k = 0; k < Casas.Count; k++) if (Casas[k].palacio) { quarteirao.Add(k); foreach (int v in Casas[k].viz) quarteirao.Add(v); }
            var porTam = new List<int>(quarteirao); porTam.Sort((a, b) => Casas[b].cel.Count.CompareTo(Casas[a].cel.Count));
            int Raiz(int k) { while (Casas[k].dono >= 0) k = Casas[k].dono; return k; }
            int nniv = 0;
            foreach (int k in porTam)
            {
                var c = Casas[k]; if (c.palacio || c.cel.Count > 6000) continue;
                int tot = 0, dentro = 0; var conta = new Dictionary<int, int>();
                foreach (var (x, y) in c.cel)
                    foreach (var (dx, dy) in D4)
                    {
                        int u = x + dx, v = y + dy; if (u < 0 || v < 0 || u >= NX || v >= NY || id[u, v] == k) continue;
                        tot++; int o = id[u, v]; if (o < 0 || !quarteirao.Contains(o)) continue;
                        o = Raiz(o); if (o == k) continue;
                        dentro++; conta[o] = conta.TryGetValue(o, out int q) ? q + 1 : 1;
                    }
                if (tot == 0 || dentro < 0.7f * tot) continue;
                // dono = a casa com mais beira em comum; a faixa baixa do corredor pesa pouco (senão o bloco afundava até ela)
                float Peso(int o) => conta[o] * (nucleoP.Contains(o) ? 0.3f : 1f);
                int dono = -1; foreach (var kv in conta) if (dono < 0 || Peso(kv.Key) > Peso(dono)) dono = kv.Key;
                if (dono < 0 || Casas[dono].cel.Count < c.cel.Count) continue;
                c.dono = dono; nniv++;
            }
            void Nivela(int x, int y, int T)
            {
                if (T < 1 || T > NZ) return; int cur = h[x, y];
                for (int z = T; z < cur && z < NZ; z++) g[(x * NY + y) * NZ + z] = 0;
                for (int z = System.Math.Max(0, cur - 1); z < T - 1; z++) g[(x * NY + y) * NZ + z] = 6;
                g[(x * NY + y) * NZ + T - 1] = 50; h[x, y] = T;
            }
            for (int k = 0; k < Casas.Count; k++)
            {
                var c = Casas[k];
                if (c.dono >= 0) { var r = Casas[Raiz(k)]; c.topo = r.topo; c.tom = r.tom; c.palacio = r.palacio; foreach (var (x, y) in c.cel) Nivela(x, y, c.topo); }
                else if (quarteirao.Contains(k)) foreach (var (x, y) in c.cel) if (h[x, y] != c.topo) Nivela(x, y, c.topo);   // degrauzinhos de 1-2 dentro da própria casa
                // o arremate creme que contornava os blocos ficava como linha clara no telhado: vira piso (a beira ganha platibanda depois)
                if (quarteirao.Contains(k) || c.dono >= 0) foreach (var (x, y) in c.cel) { int i = (x * NY + y) * NZ + h[x, y] - 1; if (h[x, y] > 0 && g[i] == 8) g[i] = 50; }
            }
            Quarteirao.Clear(); foreach (int k in quarteirao) Quarteirao.Add(k);
            UnityEngine.Debug.Log($"[Pisos3D] palácio: {nniv} blocos nivelados com o telhado em volta");
            // casas coladas (usuário, print: "sempre que tem uma casa colada na outra dá essa bugadinha"): duas casas vizinhas com
            // 3 a 5 de diferença de altura davam um dente de ameia no meio da parede e o friso de losangos desencontrado. Só no
            // desenho: (1) os degrauzinhos de até 3 dentro da própria casa somem; (2) a casa mais baixa sobe até a vizinha quando
            // a diferença é de até 5 e elas dividem pelo menos 8 colunas de parede. Diferença maior continua sendo degrau de verdade.
            bool Grande(Casa c) => c.cel.Count >= 40 && c.topo - MapData.GZ >= 14 && c.topo + PLATIBANDA + 7 < NZ;
            int nemenda = 0, nsolto = 0;
            foreach (var c in Casas)
            {
                if (!Grande(c)) continue;
                foreach (var (x, y) in c.cel)
                {
                    if (h[x, y] > c.topo + 1)
                    {   // pedacinho solto no ar em cima do telhado (ficava escondido dentro do dente de ameia): sai
                        bool vao = false; for (int z = c.topo; z < h[x, y] - 1 && !vao; z++) if (g[(x * NY + y) * NZ + z] == 0) vao = true;
                        if (vao)
                        {
                            for (int z = c.topo; z < NZ; z++) g[(x * NY + y) * NZ + z] = 0;
                            int t = 0; for (int z = c.topo - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { t = z + 1; break; }
                            h[x, y] = t; nsolto++;
                        }
                    }
                    if (h[x, y] != c.topo && System.Math.Abs(h[x, y] - c.topo) <= 3) Nivela(x, y, c.topo);
                }
            }
            var parede = new Dictionary<long, int>();
            for (int x = 0; x < NX - 1; x++)
                for (int y = 0; y < NY - 1; y++)
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        int a = id[x, y], b = id[x + dx, y + dy]; if (a < 0 || b < 0 || a == b) continue;
                        long key = a < b ? (long)a * 100000 + b : (long)b * 100000 + a;
                        parede[key] = parede.TryGetValue(key, out int q) ? q + 1 : 1;
                    }
            var porTopo = new List<int>(); for (int k = 0; k < Casas.Count; k++) porTopo.Add(k);
            porTopo.Sort((a, b) => Casas[b].topo.CompareTo(Casas[a].topo));
            foreach (int k in porTopo)
            {
                var c = Casas[k]; if (!Grande(c)) continue;
                int alvo = c.topo;
                foreach (int v in c.viz)
                {
                    var o = Casas[v]; if (!Grande(o)) continue;
                    long key = k < v ? (long)k * 100000 + v : (long)v * 100000 + k;
                    if (!parede.TryGetValue(key, out int q) || q < 8) continue;
                    int d = o.topo - c.topo; if (d > 0 && d <= 5 && o.topo > alvo) alvo = o.topo;
                }
                if (alvo == c.topo || alvo + PLATIBANDA + 7 >= NZ) continue;
                foreach (var (x, y) in c.cel) if (h[x, y] == c.topo) Nivela(x, y, alvo);
                c.topo = alvo; nemenda++;
            }
            UnityEngine.Debug.Log($"[Pisos3D] casas coladas: {nemenda} subiram até a vizinha, {nsolto} colunas com pedaço solto limpas");
            // pintura: reboco -> adobe do tom; arremate creme do alto -> adobe; telha -> terra batida do tom
            int np = 0;
            for (int k = 0; k < Casas.Count; k++)
            {
                var c = Casas[k]; byte par = c.palacio ? (byte)Paleta3D.PALACIO : (byte)(Paleta3D.ADOBE + c.tom), piso = (byte)(Paleta3D.TERRACO + c.tom);
                foreach (var (x, y) in c.cel)
                    for (int z = 0; z < h[x, y]; z++)
                    {
                        int i = (x * NY + y) * NZ + z; byte m = g[i];
                        if (m == 6 || m == 7) { g[i] = par; np++; }
                        else if (m == 8 && z >= h[x, y] - 3) { g[i] = par; np++; }
                        else if (m == 50 || m == 51) { g[i] = piso; np++; }
                    }
            }
            // platibanda e ameias (casas de pelo menos 40 colunas e 14 acima do chão; beira = até 2 de uma coluna de fora mais baixa)
            int nplat = 0, nam = 0;
            bool Fora(int x, int y, int k, int topo) => x < 0 || y < 0 || x >= NX || y >= NY || (id[x, y] != k && h[x, y] < topo);   // (bloco nivelado tem a mesma altura do dono: não conta como fora)
            // quina de ameia só onde a casa acaba de verdade: outra casa um pouco mais baixa (menos de 8) encostada não é quina
            bool Quina(int x, int y, int k, int topo) => x < 0 || y < 0 || x >= NX || y >= NY || (id[x, y] != k && h[x, y] < topo - 7);
            for (int k = 0; k < Casas.Count; k++)
            {
                var c = Casas[k]; if (c.cel.Count < 40 || c.topo - MapData.GZ < 14 || c.topo + PLATIBANDA + 7 >= NZ) continue;
                byte par = c.palacio ? (byte)Paleta3D.PALACIO : (byte)(Paleta3D.ADOBE + c.tom);
                uint hs = (uint)(c.cel[0].x * 83492791) ^ (uint)(c.cel[0].y * 73856093); hs ^= hs >> 13; hs *= 0x5bd1e995; hs ^= hs >> 15;
                bool dentada = c.palacio || (c.cel.Count >= 150 && c.cel.Count <= 1600 && c.topo - MapData.GZ >= 26 && hs % 3 == 0);
                var sobe = new List<(int x, int y, int ate)>();
                foreach (var (x, y) in c.cel)
                {
                    if (h[x, y] > c.topo + 1) continue;          // já é mais alto (lasca, degrau)
                    int d = 9;
                    for (int r = 1; r <= 2 && d == 9; r++)
                        for (int dx = -r; dx <= r && d == 9; dx++)
                            for (int dy = -r; dy <= r && d == 9; dy++)
                                if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) == r && Fora(x + dx, y + dy, k, c.topo)) d = r;
                    if (d == 9) continue;
                    int ate = c.topo + PLATIBANDA;
                    // quina pra fora: fora nos dois eixos a até 3 de distância -> ameia em degrau (mais alta bem na quina)
                    if (c.cel.Count >= 150 || c.palacio)
                    {
                        int qx = 9, qy = 9;
                        // distância até a beira de verdade andando só por dentro da própria casa: passando por cima de uma casa mais
                        // baixa encostada o dente saía fino (1-2 colunas) e a malha soltava a ponta dele no ar
                        int Ate(int dx, int dy) { for (int r = 1; r <= 5; r++) { int u = x + dx * r, v = y + dy * r; if (Quina(u, v, k, c.topo)) return r; if (id[u, v] != k) return 9; } return 9; }
                        qx = System.Math.Min(Ate(-1, 0), Ate(1, 0)); qy = System.Math.Min(Ate(0, -1), Ate(0, 1));
                        if (qx <= 3 && qy <= 3) ate += 7; else if (qx <= 5 && qy <= 5) ate += 4;
                        else if (dentada && d == 1 && (((x + y) % 9) + 9) % 9 < 4) ate += 3;
                        if (ate > c.topo + PLATIBANDA) nam++;
                    }
                    sobe.Add((x, y, ate));
                }
                // dente de quina fino demais (menos de 2 x 2 colunas na mesma altura) desce um degrau: a malha arredondada comia o
                // meio dele e a ponta ficava solta no ar (cubinho flutuando em cima da quina)
                var nivel = new Dictionary<(int, int), int>(); foreach (var (x, y, ate) in sobe) nivel[(x, y)] = ate;
                bool Bloco(int x, int y, int L)
                {
                    for (int ox = -1; ox <= 0; ox++)
                        for (int oy = -1; oy <= 0; oy++)
                        {
                            bool ok = true;
                            for (int i = 0; i < 2 && ok; i++) for (int j = 0; j < 2 && ok; j++) ok = nivel.TryGetValue((x + ox + i, y + oy + j), out int a2) && a2 >= L;
                            if (ok) return true;
                        }
                    return false;
                }
                int bas = c.topo + PLATIBANDA;
                for (int i = 0; i < sobe.Count; i++)
                {
                    var (x, y, ate) = sobe[i]; if (ate < bas + 4) continue;
                    int novo = ate;
                    if (!Bloco(x, y, novo)) novo = bas;   // (usuário aceitou arrumar: o toco que sobrava "curto e meio pontudo")
                    sobe[i] = (x, y, novo);
                }
                var miolo = new HashSet<(int, int)>(); foreach (var (x, y, ate) in sobe) if (ate >= bas + 7) miolo.Add((x, y));
                for (int i = 0; i < sobe.Count; i++)
                {
                    var (x, y, ate) = sobe[i]; if (ate != bas + 4) continue;
                    bool perto = false;
                    for (int dx = -3; dx <= 3 && !perto; dx++) for (int dy = -3; dy <= 3 && !perto; dy++) perto = miolo.Contains((x + dx, y + dy));
                    if (!perto) sobe[i] = (x, y, bas);
                }
                foreach (var (x, y, ate) in sobe)
                {
                    for (int z = h[x, y] - 1; z < ate && z < NZ; z++) { if (z < 0) continue; g[(x * NY + y) * NZ + z] = par; }
                    h[x, y] = System.Math.Max(h[x, y], ate); nplat++;
                }
            }
            CasaDe = id; Alt = h;
            UnityEngine.Debug.Log($"[Pisos3D] palácio: {npal} casas em volta do mármore"); UnityEngine.Debug.Log($"[Pisos3D] ksar: {Casas.Count} casas, {np} voxels repintados, {nplat} colunas de platibanda ({nam} de ameia)");
        }

        static readonly string[] PALACIO = { "Hall_palacio", "Palacio", "Palacio_sala", "Palacio_corredor", "Sacada_A" };
        static readonly string[] LAJOTA = { "Plataforma" };
        static readonly string[] TRELICA = { "Janelao", "Rato_fundo" };

        /// <summary>Areia com desenho (usuário: "melhora a textura da areia"): ondinhas de vento em trechos (cristas claras,
        /// curvas, de 1 de largura a cada 9) e pedrinhas esparsas, só no chão de terra andável e fora das coberturas.</summary>
        static void Areia(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, n = 0;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (!MapData.walk[x, y] || MapData.fmat[x, y] != MapData.Floor.Dirt || MapData.coverH[x, y] != 0) continue;
                    int z = MapData.floor[x, y] - 1; if (z < 0 || z >= NZ) continue;
                    int i = (x * NY + y) * NZ + z; byte m = g[i];
                    if (m != 1 && m != 2 && m != 3 && m != 18) continue;
                    float trecho = UnityEngine.Mathf.PerlinNoise(x * 0.021f + 7.7f, y * 0.021f + 1.3f);
                    float v = y + 5f * UnityEngine.Mathf.Sin(x * 0.07f + y * 0.013f) + 9f * UnityEngine.Mathf.PerlinNoise(x * 0.03f, y * 0.03f + 4f);
                    float fr = v / 9f - UnityEngine.Mathf.Floor(v / 9f);
                    bool falha = UnityEngine.Mathf.PerlinNoise(x * 0.11f + 2f, y * 0.4f) < 0.3f;
                    uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663); hsh ^= hsh >> 13; hsh *= 0x5bd1e995; hsh ^= hsh >> 15;
                    if (trecho > 0.52f && fr < 0.12f && !falha) { g[i] = 58; n++; }
                    else if (hsh % 260 == 0) { g[i] = 59; n++; }
                }
            UnityEngine.Debug.Log($"[Pisos3D] areia: {n} voxels de ondinha/pedrinha");
        }

        /// <summary>O xadrez de pedra (14/15) era igual em todo lugar (usuário: "bota outro material", "o palácio pode ser chão
        /// de mármore"). Só no desenho: o palácio vira mármore branco em lajes de 14 com junta fina e veios (54/55) e a
        /// plataforma do bomb A vira lajota areia de 10 com junta e um losango verde-água em cada canto (56/57/16).</summary>
        static void Pedras(byte[] g)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, nm = 0, nl = 0;
            var pal = new HashSet<string>(PALACIO); var laj = new HashSet<string>(LAJOTA);
            foreach (var s in MapData.areas)
            {
                bool marmore = pal.Contains(s.name), lajota = laj.Contains(s.name), trelica = System.Array.IndexOf(TRELICA, s.name) >= 0;
                if (!marmore && !lajota && !trelica) continue;
                for (int x = System.Math.Max(0, s.box.x0 - 1); x < System.Math.Min(NX, s.box.x1 + 1); x++)
                    for (int y = System.Math.Max(0, s.box.y0 - 1); y < System.Math.Min(NY, s.box.y1 + 1); y++)
                    {
                        byte m;
                        if (marmore)
                        {
                            bool junta = x % 14 == 0 || y % 14 == 0;
                            float v = UnityEngine.Mathf.PerlinNoise(x * 0.045f + 3.3f, y * 0.045f + 9.1f) + 0.35f * UnityEngine.Mathf.PerlinNoise(x * 0.17f, y * 0.17f + 5f);
                            bool veio = UnityEngine.Mathf.Abs(v * 4f - UnityEngine.Mathf.Round(v * 4f)) < 0.03f;
                            m = (byte)(junta || veio ? 55 : 54);
                        }
                        else if (trelica)
                        {   // janelão (usuário: "uma coisa mais concreto"): piso de cimento em panos de 22 com junta de dilatação,
                            // manchas largas um tom mais escuro e uma ou outra trinca fina
                            bool junta = x % 22 == 0 || y % 22 == 0;
                            float mc = UnityEngine.Mathf.PerlinNoise(x * 0.05f + 1.7f, y * 0.05f + 8.2f);
                            float tr = UnityEngine.Mathf.PerlinNoise(x * 0.035f + 5f, y * 0.035f) * 6f; tr -= UnityEngine.Mathf.Floor(tr);
                            bool trinca = tr < 0.02f && UnityEngine.Mathf.PerlinNoise(x * 0.02f, y * 0.02f + 3f) > 0.55f;
                            m = (byte)(junta ? 61 : (mc > 0.66f ? 62 : 60));   // (sem as trincas: usuário achou o piso "meio quebrado")
                        }
                        else
                        {
                            int fx = ((x % 10) + 10) % 10, fy = ((y % 10) + 10) % 10;
                            int dx = System.Math.Min(fx, 10 - fx), dy = System.Math.Min(fy, 10 - fy);
                            m = (byte)(dx + dy <= 2 ? 16 : (fx == 0 || fy == 0 ? 57 : 56));
                        }
                        for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ && z <= MapData.floor[x, y] + 1; z++)
                        {
                            int i = (x * NY + y) * NZ + z;
                            if (g[i] == 14 || g[i] == 15) { g[i] = m; if (marmore) nm++; else nl++; }
                        }
                    }
            }
            // a mureta em volta do janelão acompanha o piso (usuário: "ter uma coerência visual"): reboco creme vira cimento,
            // com a capa de cima um tom mais escuro. Só muro baixo (até 12 acima do piso), não o prédio alto do lado.
            int nw = 0;
            foreach (var s in MapData.areas)
            {
                if (System.Array.IndexOf(TRELICA, s.name) < 0) continue;
                for (int x = System.Math.Max(0, s.box.x0 - 7); x < System.Math.Min(NX, s.box.x1 + 7); x++)
                    for (int y = System.Math.Max(0, s.box.y0 - 7); y < System.Math.Min(NY, s.box.y1 + 7); y++)
                    {
                        // (usuário: "a parede embaixo dela sem concreto"): o bloco inteiro do janelão é de cimento, do chão da rua
                        // até a mureta — a coluna debaixo do piso e o muro baixo em volta; prédio alto do lado e rua ficam fora
                        bool doComodo = MapData.walk[x, y] && MapData.floor[x, y] >= MapData.UP - 1;
                        if (MapData.walk[x, y] && !doComodo) continue;
                        int alto = 0; for (int z = NZ - 1; z >= 0; z--) if (g[(x * NY + y) * NZ + z] != 0) { alto = z + 1; break; }
                        if (alto > MapData.UP + 12) continue;
                        for (int z = MapData.GZ; z < alto; z++)
                        {
                            int i = (x * NY + y) * NZ + z; if (z < 0) continue;
                            if (g[i] == 6 || g[i] == 7) { g[i] = 60; nw++; } else if (g[i] == 8) { g[i] = 62; nw++; }
                        }
                    }
            }
            UnityEngine.Debug.Log($"[Pisos3D] mármore do palácio: {nm} voxels; lajota da plataforma: {nl}; mureta do janelão em cimento: {nw}");
        }

        /// <summary>Repinta o xadrez de pedra (14/15) do deck como tábuas (46/47, as mesmas do Floor.Wood do MapVoxels),
        /// do chão até o piso de cada casa (pega o degrau da escada também). Devolve uma cópia.</summary>
        public static byte[] Madeira(byte[] G)
        {
            var g = (byte[])G.Clone(); var d = Deck(); int NX = MapData.NX, NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ, n = 0;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    // a casa do deck e até 2 em volta (a borda da escada caía fora da área e ficava com "dentinhos" do xadrez
                    // na lateral dos degraus): topo = o piso mais alto de deck ali perto
                    int alto = -1;
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < NX && yy < NY && d[xx, yy]) alto = System.Math.Max(alto, MapData.floor[xx, yy]);
                        }
                    if (alto < 0) continue;
                    int topo = System.Math.Min(NZ, (d[x, y] ? MapData.floor[x, y] : alto) + 1);
                    for (int z = System.Math.Max(0, MapData.GZ - 1); z < topo; z++)
                    {
                        int i = (x * NY + y) * NZ + z;
                        if (g[i] == 14 || g[i] == 15) { g[i] = (byte)(y % 4 == 0 ? 53 : 52); n++; }   // 52/53 = madeira do deck (Paleta3D): tábua clara de sol, junta discreta
                    }
                }
            // escada do deck: a lateral de cada degrau (reboco 6 e terra 4 abaixo do piso) aparecia em "dentinhos" brancos
            // entre o topo de madeira e a parede; dentro da área da escada ela vira madeira escura
            var esc = new bool[NX, NY];
            foreach (var s in MapData.areas)
            {
                if (s.name != "Esc_banca") continue;
                for (int x = System.Math.Max(0, s.box.x0); x < System.Math.Min(NX, s.box.x1); x++)
                    for (int y = System.Math.Max(0, s.box.y0); y < System.Math.Min(NY, s.box.y1); y++)
                        if (s.has == null || s.has(x, y)) esc[x, y] = true;
            }
            int ne = 0;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (!esc[x, y]) continue;
                    for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ && z < MapData.floor[x, y]; z++)
                    {
                        int i = (x * NY + y) * NZ + z;
                        if (g[i] == 6 || g[i] == 4 || g[i] == 1 || g[i] == 2 || g[i] == 3) { g[i] = 53; ne++; }
                    }
                }
            UnityEngine.Debug.Log($"[Pisos3D] escada do deck: {ne} voxels da lateral viraram madeira");
            // diagnóstico: o que sobrou de material (fora a madeira) na faixa do deck até o piso dele
            var hist = new Dictionary<int, int>();
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    bool perto = false;
                    for (int dx = -2; dx <= 2 && !perto; dx++) for (int dy = -2; dy <= 2 && !perto; dy++) { int xx = x + dx, yy = y + dy; perto = xx >= 0 && yy >= 0 && xx < NX && yy < NY && d[xx, yy]; }
                    if (!perto) continue;
                    for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ && z <= MapData.floor[x, y] + 1; z++) { int m = g[(x * NY + y) * NZ + z]; if (m != 0 && m != 52 && m != 53) hist[m] = hist.TryGetValue(m, out int c) ? c + 1 : 1; }
                }
            var sb = new System.Text.StringBuilder(); foreach (var kv in hist) sb.Append($"{kv.Key}:{kv.Value} ");
            UnityEngine.Debug.Log($"[Pisos3D] deck de madeira: {n} voxels repintados; resto na faixa: {sb}");
            Pedras(g);
            // Terracos(g);   // teste de terraço no lugar da telha: REPROVADO pelo usuário ("ficou muito ruim"), fica desligado
            Areia(g);
            Lascas(g);
            if (MapData.MAPA != "brasa") { Ksar(g); Chao(g); Janelao(g); Resto(g); } else { Casas.Clear(); }
            return g;
        }
    }
}
