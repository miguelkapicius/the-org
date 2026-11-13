using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Telhado de duas águas da Brasa, desenhado na própria altura do topo do prédio (MapVoxels.Topo): casas em fileira
    /// de lotes de tamanho variado (cortes de 50 a 110 unidades em x e em y), cumeeira no lado comprido do pedaço de
    /// prédio do lote, água descendo pros dois lados até a beirada (rua, divisa de lote ou prédio de altura diferente).
    /// A empena aparece sozinha onde o lote vizinho é mais baixo. Testado no Blender (prévia da Brasa v9.2) antes da Unity.
    /// Só desenho: o 'solid' da Sim (colisão, granada) não muda; o telhado sobe no máximo SUBIDA acima dele.
    /// </summary>
    public static class TelhadoItalia
    {
        /// <summary>Subida por unidade de distância até a beirada (2,6 a cada 5 = ~27 graus) e o teto da subida.</summary>
        const float CAIMENTO = 0.52f;
        const int SUBIDA = 16;
        static int[,] extra, dist;
        static byte[,] eixoX;   // 1 = cumeeira leste-oeste
        static int[] cx, cy;
        static int nx = -1, ny = -1;

        static int[] Cortes(int n, int sem)
        {
            var r = new System.Random(sem); var l = new System.Collections.Generic.List<int> { 0 }; int p = 0;
            int[] passo = { 50, 60, 70, 80, 90, 100, 110 };
            while (p < n) { p += passo[r.Next(passo.Length)]; l.Add(Mathf.Min(p, n)); }
            return l.ToArray();
        }

        static int Faixa(int[] c, int v) { int i = System.Array.BinarySearch(c, v); return i >= 0 ? i : ~i - 1; }
        public static (int, int) Lote(int x, int y) { Prepara(); return (Faixa(cx, x), Faixa(cy, y)); }

        static void Prepara()
        {
            if (nx == MapData.NX && ny == MapData.NY && extra != null) return;
            nx = MapData.NX; ny = MapData.NY;
            cx = Cortes(nx, 5); cy = Cortes(ny, 9);
            extra = new int[nx, ny]; dist = new int[nx, ny]; eixoX = new byte[nx, ny];
            var walk = MapData.walk; var solid = MapData.solid;
            // extensão do prédio em cada lote -> eixo da cumeeira
            int lx = cx.Length, ly = cy.Length;
            var ext = new int[lx, ly, 4];
            for (int i = 0; i < lx; i++) for (int j = 0; j < ly; j++) { ext[i, j, 0] = int.MaxValue; ext[i, j, 1] = -1; ext[i, j, 2] = int.MaxValue; ext[i, j, 3] = -1; }
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                {
                    if (walk[x, y]) continue;
                    int a = Faixa(cx, x), b = Faixa(cy, y);
                    ext[a, b, 0] = Mathf.Min(ext[a, b, 0], x); ext[a, b, 1] = Mathf.Max(ext[a, b, 1], x);
                    ext[a, b, 2] = Mathf.Min(ext[a, b, 2], y); ext[a, b, 3] = Mathf.Max(ext[a, b, 3], y);
                }
            bool Mesmo(int x, int y, int u, int v) => u >= 0 && v >= 0 && u < nx && v < ny && !walk[u, v] && solid[u, v] == solid[x, y]
                                                      && Faixa(cx, u) == Faixa(cx, x) && Faixa(cy, v) == Faixa(cy, y);
            // distância até a beirada só no eixo de través, por corrida (O(N))
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                {
                    if (walk[x, y]) continue;
                    int a = Faixa(cx, x), b = Faixa(cy, y);
                    eixoX[x, y] = (byte)(ext[a, b, 1] - ext[a, b, 0] >= ext[a, b, 3] - ext[a, b, 2] ? 1 : 0);
                }
            // cumeeira em x -> água desce em y: corridas verticais; cumeeira em y -> corridas horizontais
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny;)
                {
                    if (walk[x, y] || eixoX[x, y] == 0) { y++; continue; }
                    int y0 = y; while (y + 1 < ny && eixoX[x, y + 1] == 1 && Mesmo(x, y, x, y + 1)) y++;
                    for (int k = y0; k <= y; k++) dist[x, k] = Mathf.Min(k - y0, y - k);
                    y++;
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx;)
                {
                    if (walk[x, y] || eixoX[x, y] == 1) { x++; continue; }
                    int x0 = x; while (x + 1 < nx && eixoX[x + 1, y] == 0 && Mesmo(x, y, x + 1, y)) x++;
                    for (int k = x0; k <= x; k++) dist[k, y] = Mathf.Min(k - x0, x - k);
                    x++;
                }
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                    if (!walk[x, y]) extra[x, y] = Mathf.Min(SUBIDA, Mathf.RoundToInt(dist[x, y] * CAIMENTO));
        }

        /// <summary>Quanto o telhado sobe acima do topo da Sim nesta coluna (0 na beirada, até SUBIDA na cumeeira).</summary>
        public static int Extra(int x, int y) { Prepara(); return extra[x, y]; }

        /// <summary>Cor da telha nesta coluna: canal de capa-canal ao longo da água (listra a cada 3 unidades) e fiada
        /// mais escura a cada ~8 de distância da beirada; a beirada (2 primeiras unidades) é a telha mais queimada.</summary>
        public static byte Telha(int x, int y)
        {
            Prepara();
            int d = dist[x, y]; int aoLongo = eixoX[x, y] == 1 ? x : y;
            if (d < 2) return 51;
            if (d % 8 == 7) return 51;
            return (byte)((aoLongo / 3) % 2 == 0 ? 50 : 55);
        }
    }
}
