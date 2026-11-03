using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using CSM.Map;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Malha 3D do mapa a partir dos MESMOS voxels do visual 2D (MapVoxels.Build, fonte = MapData): ocupação borrada
    /// (gaussiana) + surface nets na iso 0,5 = quinas arredondadas de maquete, piso plano continua plano, degrau de escada
    /// vira rampa lisa. Cor por vértice = material do voxel logo atrás da superfície (Paleta3D). Fora do mapa é vazio,
    /// então as bordas fecham como um bloco de diorama. Em pedaços de CH x CH colunas (malha de 32 bits cada).
    /// Mundo Unity: X = leste, Y = cima, Z = norte (= -y do jogo); 1 unidade Unity = ESC unidades do jogo (1 boneco).
    /// </summary>
    public static class MalhaMapa
    {
        public const float ESC = 1f / 25f;
        public static Vector3 ParaUnity(float x, float y, float z) => new Vector3(x * ESC, z * ESC, -y * ESC);

        public class Pedaco { public Vector3[] v, n; public Color[] c; public int[] t; public int cx, cy; }

        /// <summary>Gera os pedaços (threads). sigma = raio do arredondado em voxels.</summary>
        public static List<Pedaco> Gerar(byte[] G, float sigma = 1.1f, int CH = 100, bool linear = true)
        {
            int nCx = (MapData.NX + CH - 1) / CH, nCy = (MapData.NY + CH - 1) / CH;
            var res = new Pedaco[nCx * nCy];
            Parallel.For(0, res.Length, k =>
            {
                int cx = k % nCx, cy = k / nCx;
                res[k] = Pedacinho(G, cx * CH, Math.Min(MapData.NX, cx * CH + CH), cy * CH, Math.Min(MapData.NY, cy * CH + CH), sigma, linear);
                res[k].cx = cx; res[k].cy = cy;
            });
            var l = new List<Pedaco>();
            foreach (var p in res) if (p.t.Length > 0) l.Add(p);
            return l;
        }

        static Pedaco Pedacinho(byte[] G, int X0, int X1, int Y0, int Y1, float sigma, bool linear)
        {
            int NX = MapData.NX, NY = MapData.NY, NZ = MapVoxels.NZ;
            int R = Math.Max(1, (int)Math.Ceiling(sigma * 2.5f)), P = R + 2;
            int sx = X1 - X0 + 2 * P, sy = Y1 - Y0 + 2 * P, sz = NZ + 2 * P;
            int ox = X0 - P, oy = Y0 - P, oz = -P;   // índice do campo -> voxel do mundo
            byte Vox(int x, int y, int z)
            {
                if (x < 0 || y < 0 || x >= NX || y >= NY) return 0;
                if (z < 0) return (byte)(z >= -2 ? 4 : 0);
                if (z >= NZ) return 0;
                return G[(x * NY + y) * NZ + z];
            }
            int Id(int i, int j, int k) => (i * sy + j) * sz + k;

            // ocupação e topo mais alto (corta o z do trabalho)
            var a = new float[sx * sy * sz];
            int ktop = 0;
            for (int i = 0; i < sx; i++)
                for (int j = 0; j < sy; j++)
                    for (int k = 0; k < sz; k++)
                        if (Vox(i + ox, j + oy, k + oz) != 0) { a[Id(i, j, k)] = 1f; if (k > ktop) ktop = k; }
            int kmax = Math.Min(sz, ktop + P + 1);

            // borrão gaussiano separável
            var w = new float[2 * R + 1]; float ws = 0;
            for (int d = -R; d <= R; d++) { w[d + R] = MathF.Exp(-d * d / (2 * sigma * sigma)); ws += w[d + R]; }
            for (int d = 0; d < w.Length; d++) w[d] /= ws;
            var b = new float[a.Length];
            void Passo(float[] src, float[] dst, int di, int dj, int dk)
            {
                for (int i = 0; i < sx; i++)
                    for (int j = 0; j < sy; j++)
                        for (int k = 0; k < kmax; k++)
                        {
                            float s = 0;
                            for (int d = -R; d <= R; d++)
                            {
                                int ii = Math.Clamp(i + d * di, 0, sx - 1), jj = Math.Clamp(j + d * dj, 0, sy - 1), kk = Math.Clamp(k + d * dk, 0, sz - 1);
                                s += src[Id(ii, jj, kk)] * w[d + R];
                            }
                            dst[Id(i, j, k)] = s;
                        }
            }
            Passo(a, b, 1, 0, 0); Passo(b, a, 0, 1, 0); Passo(a, b, 0, 0, 1);
            var f = b;
            const float ISO = 0.5f;

            var vid = new int[sx * sy * sz];
            Array.Fill(vid, -1);
            var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
            Vector3 corBuf = default;

            // vértice da célula (i..i+1, j..j+1, k..k+1): média dos cruzamentos nas 12 arestas
            int Vert(int i, int j, int k)
            {
                int id = Id(i, j, k);
                if (vid[id] >= 0) return vid[id];
                Span<float> c = stackalloc float[8];
                for (int q = 0; q < 8; q++) c[q] = f[Id(i + (q & 1), j + ((q >> 1) & 1), k + ((q >> 2) & 1))];
                float px = 0, py = 0, pz = 0; int cnt = 0;
                for (int q = 0; q < 8; q++)
                    for (int ax = 0; ax < 3; ax++)
                    {
                        int bit = 1 << ax;
                        if ((q & bit) != 0) continue;
                        int r = q | bit;
                        float fa = c[q], fb = c[r];
                        if ((fa > ISO) == (fb > ISO)) continue;
                        float u = (ISO - fa) / (fb - fa);
                        float qx = q & 1, qy = (q >> 1) & 1, qz = (q >> 2) & 1;
                        if (ax == 0) qx = u; else if (ax == 1) qy = u; else qz = u;
                        px += qx; py += qy; pz += qz; cnt++;
                    }
                px /= cnt; py /= cnt; pz /= cnt;
                // gradiente exato do trilinear no ponto
                float L(float a0, float a1, float t) => a0 + (a1 - a0) * t;
                float gx = L(L(c[1] - c[0], c[3] - c[2], py), L(c[5] - c[4], c[7] - c[6], py), pz);
                float gy = L(L(c[2] - c[0], c[3] - c[1], px), L(c[6] - c[4], c[7] - c[5], px), pz);
                float gz = L(L(c[4] - c[0], c[5] - c[1], px), L(c[6] - c[2], c[7] - c[3], px), py);
                var gn = new Vector3(-gx, -gy, -gz);   // pra fora (campo alto = sólido)
                float gl = gn.magnitude; if (gl > 1e-6f) gn /= gl; else gn = new Vector3(0, 0, 1);
                // posição no mundo do jogo (centro do voxel = +0,5)
                float wx = i + ox + 0.5f + px, wy = j + oy + 0.5f + py, wz = k + oz + 0.5f + pz;
                // material: voxel logo atrás da superfície
                int m = 0;
                for (int s = 1; s <= 4 && m == 0; s++)
                {
                    float d = s * 0.45f;
                    m = Vox((int)MathF.Floor(wx - gn.x * d), (int)MathF.Floor(wy - gn.y * d), (int)MathF.Floor(wz - gn.z * d));
                }
                if (m == 0)
                    for (int q = 0; q < 8 && m == 0; q++) m = Vox(i + ox + (q & 1), j + oy + ((q >> 1) & 1), k + oz + ((q >> 2) & 1));
                if (m == 0) m = 6;
                V.Add(ParaUnity(wx, wy, wz));
                N.Add(new Vector3(gn.x, gn.z, -gn.y));
                C.Add(Tom(Paleta3D.Vertice(m, linear), m, wx, wy, wz, gn, linear));
                vid[id] = V.Count - 1;
                return vid[id];
            }

            void Quad(int a0, int a1, int a2, int a3)
            {
                Vector3 p0 = V[a0], p1 = V[a1], p2 = V[a2];
                var nf = Vector3.Cross(p1 - p0, p2 - p0);
                var nm = N[a0] + N[a1] + N[a2] + N[a3];
                if (Vector3.Dot(nf, nm) < 0) { T.Add(a0); T.Add(a2); T.Add(a1); T.Add(a0); T.Add(a3); T.Add(a2); }
                else { T.Add(a0); T.Add(a1); T.Add(a2); T.Add(a0); T.Add(a2); T.Add(a3); }
            }

            // arestas entre centros de voxel com troca de sinal -> quad das 4 células em volta (só as do pedaço)
            for (int i = P; i < P + (X1 - X0); i++)
                for (int j = P; j < P + (Y1 - Y0); j++)
                    for (int k = 1; k < kmax - 1; k++)
                    {
                        bool s0 = f[Id(i, j, k)] > ISO;
                        if (s0 != f[Id(i + 1, j, k)] > ISO) Quad(Vert(i, j - 1, k - 1), Vert(i, j, k - 1), Vert(i, j, k), Vert(i, j - 1, k));
                        if (s0 != f[Id(i, j + 1, k)] > ISO) Quad(Vert(i - 1, j, k - 1), Vert(i, j, k - 1), Vert(i, j, k), Vert(i - 1, j, k));
                        if (s0 != f[Id(i, j, k + 1)] > ISO) Quad(Vert(i - 1, j - 1, k), Vert(i, j - 1, k), Vert(i, j, k), Vert(i - 1, j, k));
                    }
            // detalhe fino que o borrão apagou (escada de mão, degraus, toldo, roda da carroça, lanterna, varal): o voxel
            // é sólido mas o campo no centro dele ficou abaixo da iso -> vira cubinho com face onde o vizinho é vazio
            Vector3 Uv(float x, float y, float z) => ParaUnity(x, y, z);
            for (int i = P; i < P + (X1 - X0); i++)
                for (int j = P; j < P + (Y1 - Y0); j++)
                    for (int k = 1; k < kmax - 1; k++)
                    {
                        if (f[Id(i, j, k)] >= ISO) continue;
                        int x = i + ox, y = j + oy, z = k + oz;
                        byte mv = Vox(x, y, z); if (mv == 0) continue;
                        // só o que é FINO de verdade (vazio dos dois lados em algum eixo: degrau, trilho, toldo, roda). Quina e
                        // canto de caixa/prédio também caem abaixo da iso no borrão (0,47 e 0,32) mas ali é o arredondado que
                        // vale — virar cubinho deixava fresta sem face e a caixa ficava "vazada"
                        bool fino = (Vox(x - 1, y, z) == 0 && Vox(x + 1, y, z) == 0) || (Vox(x, y - 1, z) == 0 && Vox(x, y + 1, z) == 0)
                                    || (Vox(x, y, z - 1) == 0 && Vox(x, y, z + 1) == 0);
                        if (!fino) continue;
                        var cor = Tom(Paleta3D.Vertice(mv, linear), mv, x, y, z, new Vector3(0, 0, 1), linear);
                        for (int d = 0; d < 6; d++)
                        {
                            int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0, dz = d == 4 ? 1 : d == 5 ? -1 : 0;
                            if (Vox(x + dx, y + dy, z + dz) != 0) continue;
                            // os 4 cantos da face (no mundo do jogo), normal pra fora
                            var nj = new Vector3(dx, dy, dz);
                            Vector3 c0 = new Vector3(x + 0.5f + dx * 0.5f, y + 0.5f + dy * 0.5f, z + 0.5f + dz * 0.5f);
                            Vector3 u1 = dx != 0 ? new Vector3(0, 0.5f, 0) : new Vector3(0.5f, 0, 0);
                            Vector3 u2 = dz != 0 ? new Vector3(0, 0.5f, 0) : new Vector3(0, 0, 0.5f);
                            int b0 = V.Count;
                            foreach (var (s1, s2) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                            {
                                var q = c0 + u1 * s1 + u2 * s2;
                                V.Add(Uv(q.x, q.y, q.z)); N.Add(new Vector3(nj.x, nj.z, -nj.y)); C.Add(cor);
                            }
                            Quad(b0, b0 + 1, b0 + 2, b0 + 3);
                        }
                    }
            return new Pedaco { v = V.ToArray(), n = N.ToArray(), c = C.ToArray(), t = T.ToArray() };
        }

        // ruído de valor 2D determinístico (thread-safe; o Mathf.PerlinNoise é nativo)
        static float H2(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float Ruido2(float x, float y)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y); float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = H2(ix, iy), b = H2(ix + 1, iy), c = H2(ix, iy + 1), d = H2(ix + 1, iy + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Variação de pintura de maquete: telha muda de tom por quarteirão (casas diferentes), parede escurece
        /// perto do chão (sujeira/oclusão de desenho) e clareia no alto, piso com manchas grandes bem leves.</summary>
        static Color Tom(Color c, int m, float x, float y, float z, Vector3 n, bool linear)
        {
            float k = 1;
            bool telha = m == 50 || m == 51, parede = m == 6 || m == 7 || m == 8;
            if (telha)
            {
                float q = Ruido2(x / 70f, y / 70f);
                k = 0.86f + 0.24f * q;
                float hue = (Ruido2(x / 110f + 7, y / 110f + 3) - 0.5f) * 0.05f;
                Color.RGBToHSV(linear ? c.gamma : c, out float h, out float s, out float v);
                var g = Color.HSVToRGB(Mathf.Repeat(h + hue, 1), s, v);
                c = linear ? g.linear : g;
            }
            else if (parede && MathF.Abs(n.z) < 0.6f)
            {
                float alt = z - MapData.GZ;
                k = Mathf.Lerp(0.74f, 1f, Mathf.Clamp01(alt / 16f)) * Mathf.Lerp(1f, 1.05f, Mathf.Clamp01((alt - 20) / 20f));
            }
            else if (n.z > 0.7f && !parede)
                k = 0.95f + 0.1f * Ruido2(x / 45f, y / 45f);
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        /// <summary>Monta os GameObjects dos pedaços embaixo de pai (main thread).</summary>
        public static GameObject Montar(List<Pedaco> ps, Material mat, Transform pai = null)
        {
            var raiz = new GameObject("Mapa 3D");
            if (pai) raiz.transform.SetParent(pai, false);
            foreach (var p in ps)
            {
                var m = new Mesh { name = $"mapa_{p.cx}_{p.cy}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(p.v); m.SetNormals(p.n); m.SetColors(p.c); m.SetTriangles(p.t, 0);
                m.RecalculateBounds();
                var go = new GameObject(m.name);
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                go.isStatic = true;
            }
            return raiz;
        }
    }
}
