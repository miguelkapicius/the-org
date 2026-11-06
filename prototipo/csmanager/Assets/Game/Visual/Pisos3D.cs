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
                        if (g[i] == 14 || g[i] == 15) { g[i] = (byte)(y % 4 == 0 ? 47 : 46); n++; }
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
                        if (g[i] == 6 || g[i] == 4 || g[i] == 1 || g[i] == 2 || g[i] == 3) { g[i] = 47; ne++; }
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
                    for (int z = System.Math.Max(0, MapData.GZ - 1); z < NZ && z <= MapData.floor[x, y] + 1; z++) { int m = g[(x * NY + y) * NZ + z]; if (m != 0 && m != 46 && m != 47) hist[m] = hist.TryGetValue(m, out int c) ? c + 1 : 1; }
                }
            var sb = new System.Text.StringBuilder(); foreach (var kv in hist) sb.Append($"{kv.Key}:{kv.Value} ");
            UnityEngine.Debug.Log($"[Pisos3D] deck de madeira: {n} voxels repintados; resto na faixa: {sb}");
            return g;
        }
    }
}
