using System;
using System.Collections.Generic;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.View
{
    /// <summary>
    /// Smoke volumétrica (estilo CS2): grade 3D de 4 unidades que enche o espaço a partir da lata seguindo o terreno —
    /// não atravessa parede, sobe até um teto, e num corredor estreito vai mais longe (o volume é fixo). Tiro abre um
    /// furo que fecha sozinho; HE limpa uma bolha por um tempo. Cresce em ~1,3 s, dura ~15 s e se desfaz no fim.
    /// Escala do jogo: boneco ~25 unidades, então o raio fica ~50 e a altura ~45.
    /// </summary>
    public class SmokeVolume
    {
        public const int C = 4;                       // tamanho da célula
        public const int CELLS = 1250;                // volume total (células): domo de ~raio 34 em área aberta
        public const float GROW = 1.3f, LIFE = 15f, FADE = 2.5f;
        public readonly V3 origin; public float age;
        public readonly int x0, y0, z0, nx, ny, nz;   // caixa da grade em células
        readonly float[] dens;                        // densidade base (0..1) por célula
        readonly int[] order;                         // em que ordem cada célula entra no crescimento (-1 = nunca)
        readonly float[] clear;                       // furos (HE; tiro na smokeVelha): 1 = limpo, volta pra 0 com o tempo
        readonly float[] tiro;                        // túnel de tiro (fecha mais rápido que a HE)
        int filled;

        public SmokeVolume(V3 origin)
        {
            this.origin = origin;
            const int R = 22, HZ = 18;                  // caixa máxima (corredor comprido cabe)
            int cx = (int)(origin.x / C), cy = (int)(origin.y / C), cz = (int)(origin.z / C);
            x0 = cx - R; y0 = cy - R; z0 = Math.Max(0, cz - 1); nx = ny = 2 * R + 1; nz = HZ;
            dens = new float[nx * ny * nz]; order = new int[nx * ny * nz]; clear = new float[nx * ny * nz]; tiro = new float[nx * ny * nz];
            for (int i = 0; i < order.Length; i++) order[i] = -1;
            Fill(cx - x0, cy - y0, cz - z0);
        }

        int Idx(int x, int y, int z) => (z * ny + y) * nx + x;

        /// <summary>Célula livre: anda e está acima do piso, ou é ar acima do telhado; abaixo do teto da smoke.</summary>
        bool Free(int x, int y, int z)
        {
            int wx = (x + x0) * C + C / 2, wy = (y + y0) * C + C / 2, wz = (z + z0) * C + C / 2;
            if (!InMap(wx, wy)) return false;
            // teto em domo: ~1,7 boneco no meio, baixando pra borda (não vira bloco de lados retos)
            float hx = wx - origin.x, hy = wy - origin.y;
            if (wz > origin.z + 44 - (hx * hx + hy * hy) / 50f) return false;
            return wz >= Top(wx, wy);
        }

        /// <summary>Enche por ordem de distância (Dijkstra simples): de lado custa 1, subir 1,6, descer 0,8 (assenta).</summary>
        void Fill(int sx, int sy, int sz)
        {
            var dist = new float[nx * ny * nz];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;
            while (sz < nz - 1 && !Free(sx, sy, sz)) sz++;
            var pq = new MinHeap();
            int s0 = Idx(sx, sy, sz); dist[s0] = 0; pq.Push(s0, 0);
            int n = 0; float dmax = 0;
            // 26 vizinhos com distância de verdade (diagonal = raiz de 2/3): a frente cresce redonda, não em losango.
            // Subir custa um pouco menos (empilha em domo); cada célula tem um "atrito" sorteado (0,85-1,2) que deixa a
            // borda irregular, como fumaça
            var nb = new List<(int dx, int dy, int dz, float c)>();
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        float c = MathF.Sqrt(dx * dx + dy * dy + dz * dz * (dz > 0 ? 0.6f : 0.85f));
                        nb.Add((dx, dy, dz, c));
                    }
            while (pq.Count > 0 && n < CELLS)
            {
                var (i, d) = pq.Pop();
                if (order[i] >= 0 || d > dist[i]) continue;
                order[i] = n++; dmax = d;
                int x = i % nx, y = (i / nx) % ny, z = i / (nx * ny);
                foreach (var (dx, dy, dz, c) in nb)
                {
                    int u = x + dx, v = y + dy, w = z + dz;
                    if (u < 0 || v < 0 || w < 0 || u >= nx || v >= ny || w >= nz) continue;
                    int j = Idx(u, v, w);
                    if (order[j] >= 0 || !Free(u, v, w)) continue;
                    float rough = 0.85f + 0.35f * Noise.Value3((u + x0) * 0.45f, (v + y0) * 0.45f, (w + z0) * 0.45f);
                    float nd = d + c * rough;
                    if (nd < dist[j]) { dist[j] = nd; pq.Push(j, nd); }
                }
            }
            filled = n;
            // borda macia: as últimas células a entrar ficam mais ralas
            for (int i = 0; i < dens.Length; i++)
                if (order[i] >= 0) { float f = dist[i] / Math.Max(dmax, 1); dens[i] = f < 0.7f ? 1f : 1f - (f - 0.7f) / 0.3f * 0.75f; }
            MakePuffs();
        }

        // ------------------------------------------------------------- bolotas (o que se desenha)
        public struct Puff { public V3 c; public float r, t0, ph; }
        public readonly List<Puff> puffs = new List<Puff>();
        readonly List<(V3 a, V3 b, float age)> tunnels = new List<(V3, V3, float)>();

        /// <summary>Espalha bolotas nas células cheias, na ordem em que a fumaça chega nelas: espaçadas ~11, raio até
        /// 17 mas limitado pelo espaço livre (bolota não entra em parede). As da borda são menores.</summary>
        void MakePuffs()
        {
            var byOrder = new int[filled];
            for (int i = 0; i < order.Length; i++) if (order[i] >= 0) byOrder[order[i]] = i;
            var rng = new Rng(x0 * 7 + y0 * 13);
            foreach (int i in byOrder)
            {
                int x = i % nx, y = (i / nx) % ny, z = i / (nx * ny);
                // centro tremido dentro da célula: quebra as fileiras retas da grade
                var c = new V3((x + x0) * C + C / 2f + rng.Uniform(-1.8f, 1.8f), (y + y0) * C + C / 2f + rng.Uniform(-1.8f, 1.8f),
                               (z + z0) * C + C / 2f + rng.Uniform(-1.5f, 1.5f));
                bool near = false;
                foreach (var p in puffs) if ((p.c - c).Len < 7.5f) { near = true; break; }
                if (near) continue;
                float edge = order[i] / (float)filled;
                float r = rng.Uniform(8.5f, 11.5f) * (edge > 0.8f ? 0.75f : 1f);
                // encolhe até caber no espaço livre (testa 6 direções)
                for (; r > 7f; r -= 1.5f)
                {
                    bool ok = true;
                    foreach (var d in new[] { new V3(1, 0, 0), new V3(-1, 0, 0), new V3(0, 1, 0), new V3(0, -1, 0), new V3(0, 0, -1) })
                    {
                        V3 q = c + d * (r * 0.75f);
                        int qx = (int)(q.x / C) - x0, qy = (int)(q.y / C) - y0, qz = (int)(q.z / C) - z0;
                        if (qx < 0 || qy < 0 || qz < 0 || qx >= nx || qy >= ny || qz >= nz || !Free(qx, qy, qz)) { ok = false; break; }
                    }
                    if (ok) break;
                }
                puffs.Add(new Puff { c = c, r = r, t0 = edge * GROW, ph = rng.Uniform(0, 6.28f) });
            }
        }

        /// <summary>Raio atual da bolota (nasce, respira de leve, some no fim — as da borda primeiro).</summary>
        public float PuffR(in Puff p, float t)
        {
            float a = Ease((age - p.t0) / 0.45f);
            if (a <= 0) return 0;
            float endT = LIFE - FADE * (1 - p.t0 / GROW * 0.6f);
            float e = age < endT ? 1 : Math.Max(0, 1 - (age - endT) / (FADE * 0.6f));
            return p.r * a * e * (1 + 0.06f * MathF.Sin(t * 0.9f + p.ph));
        }
        public V3 PuffC(in Puff p, float t)
        {
            // expande: cada partícula sai da lata e voa até o lugar dela (desacelerando), depois flutua de leve
            float u = Ease((age - p.t0 * 0.6f) / 0.7f);
            V3 home = p.c + new V3(0, 0, 1.2f * MathF.Sin(t * 0.6f + p.ph * 1.3f));
            return new V3(origin.x, origin.y, origin.z + 4) + (home - new V3(origin.x, origin.y, origin.z + 4)) * u;
        }

        /// <summary>Ponto dentro de algum túnel de tiro (fica transparente).</summary>
        public bool InTunnel(V3 q)
        {
            foreach (var (a, b, ag) in tunnels)
            {
                float r = 3.5f * MathF.Sqrt(Math.Max(0, 1 - ag / 1.0f));
                if (r < 0.5f) continue;
                V3 ab = b - a; float u = Math.Clamp((q - a).Dot(ab) / Math.Max(ab.Dot(ab), 1e-6f), 0, 1);
                if ((q - (a + ab * u)).Len < r) return true;
            }
            return false;
        }

        public bool Alive => age < LIFE;

        public void Update(float dt)
        {
            age += dt;
            bool velha = TeamBrain.EXP.Contains("smokeVelha");
            float k = dt * (velha ? 1 / 1.4f : HE_K), kt = dt * TIRO_K;
            for (int i = 0; i < clear.Length; i++)
            {
                if (clear[i] > 0) clear[i] = Math.Max(0, clear[i] - k);
                if (tiro[i] > 0) tiro[i] = Math.Max(0, tiro[i] - kt);
            }
            for (int i = tunnels.Count - 1; i >= 0; i--)
            {
                var tn = tunnels[i]; tn.age += dt;
                if (tn.age > 1.0f) tunnels.RemoveAt(i); else tunnels[i] = tn;
            }
        }

        /// <summary>
        /// Tiro atravessando: o mesmo túnel que o visual desenha (smoke por volume), pra o bot ver pelo buraco que o jogador
        /// vê — cone do ponto onde a bala entra na fumaça (densidade > 0,05) até onde sai: raio 2,5 na entrada, +0,11 por
        /// unidade, no máximo 8. Aberto até 0,3 s, fecha da borda pro meio até 1,5 s (flag smokeVelha: cilindro 3,5 por ~1 s).
        /// </summary>
        public void Shot(V3 a, V3 b)
        {
            tunnels.Add((a, b, 0f));
            V3 d = b - a; float L = d.Len; if (L < 1e-3f) return;
            if (TeamBrain.EXP.Contains("smokeVelha")) { for (float s = 0; s <= L; s += C * 0.5f) Clear(a + d * (s / L), 3.5f, 0.7f); return; }
            float ent = -1, sai = -1;
            for (float s = 0; s <= L; s += C * 0.5f) if (Base(a + d * (s / L)) > 0.05f) { if (ent < 0) ent = s; sai = s; }
            if (ent < 0) return;
            for (float s = ent; s <= sai; s += C * 0.5f) Furo(tiro, a + d * (s / L), MathF.Min(8f, 2.5f + 0.11f * (s - ent)), 1f, TIRO_A);
        }
        // furo linear no tempo: valor A, cai k por segundo; limpo (≥1) por (A−1)/k e some em A/k
        const float TIRO_K = 1f / 1.2f, TIRO_A = 1.25f;      // aberto 0,3 s, fechado em 1,5 s
        const float HE_K = 1f / 2.2f, HE_A = 1f + 1f / 2.2f;  // aberto 1,0 s, cheio de novo em 3,2 s

        /// <summary>
        /// HE no meio da smoke: bolha de raio r centrada 6 acima da explosão; pra cima vai o dobro (rompe o topo do domo).
        /// Aberta até 1,0 s e volta a encher até 3,2 s — igual ao visual (flag smokeVelha: esfera, ~1,5 s limpa).
        /// </summary>
        public void HE(V3 p, float r, float velhoAmount)
        {
            if (TeamBrain.EXP.Contains("smokeVelha")) { Clear(p, r, velhoAmount); return; }
            Furo(clear, new V3(p.x, p.y, p.z + 6), r, 2f, HE_A);
        }

        /// <summary>Furo esférico (cima esticado por "subir") no campo dado: o centro abre mais e fecha por último.</summary>
        void Furo(float[] campo, V3 p, float r, float subir, float amount)
        {
            int cx = (int)(p.x / C) - x0, cy = (int)(p.y / C) - y0, cz = (int)(p.z / C) - z0, rc = (int)(r / C) + 1, rcz = (int)(r * subir / C) + 1;
            for (int z = cz - rc; z <= cz + rcz; z++)
                for (int y = cy - rc; y <= cy + rc; y++)
                    for (int x = cx - rc; x <= cx + rc; x++)
                    {
                        if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) continue;
                        float dx = (x + x0) * C + C / 2 - p.x, dy = (y + y0) * C + C / 2 - p.y, dz = (z + z0) * C + C / 2 - p.z;
                        if (dz > 0) dz /= subir;
                        float q = MathF.Sqrt(dx * dx + dy * dy + dz * dz) / r;
                        if (q < 1) { int i = Idx(x, y, z); campo[i] = Math.Max(campo[i], amount * (1 - q * q)); }
                    }
        }

        /// <summary>Densidade crescida sem furo nem fervido (onde a bala "entra" na fumaça).</summary>
        float Base(V3 p)
        {
            int ix = (int)(p.x / C) - x0, iy = (int)(p.y / C) - y0, iz = (int)(p.z / C) - z0;
            if (ix < 0 || iy < 0 || iz < 0 || ix >= nx || iy >= ny || iz >= nz) return 0;
            int i = Idx(ix, iy, iz), grown = (int)(filled * Ease(age / GROW));
            return order[i] < 0 || order[i] > grown ? 0 : dens[i];
        }

        public void Clear(V3 p, float r, float amount)
        {
            int cx = (int)(p.x / C) - x0, cy = (int)(p.y / C) - y0, cz = (int)(p.z / C) - z0, rc = (int)(r / C) + 1;
            for (int z = cz - rc; z <= cz + rc; z++)
                for (int y = cy - rc; y <= cy + rc; y++)
                    for (int x = cx - rc; x <= cx + rc; x++)
                    {
                        if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) continue;
                        float dx = (x + x0) * C + C / 2 - p.x, dy = (y + y0) * C + C / 2 - p.y, dz = (z + z0) * C + C / 2 - p.z;
                        float q = MathF.Sqrt(dx * dx + dy * dy + dz * dz) / r;
                        if (q < 1) { int i = Idx(x, y, z); clear[i] = Math.Max(clear[i], amount * (1 - q * q)); }
                    }
        }

        /// <summary>Densidade no ponto do mundo (0..1), com crescimento, desmanche, furos e um "fervido" animado.</summary>
        public float Sample(V3 p, float t)
        {
            float fx = p.x / C - x0 - 0.5f, fy = p.y / C - y0 - 0.5f, fz = p.z / C - z0 - 0.5f;
            int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy), iz = (int)MathF.Floor(fz);
            if (ix < 0 || iy < 0 || iz < 0 || ix >= nx - 1 || iy >= ny - 1 || iz >= nz - 1) return 0;
            float ax = fx - ix, ay = fy - iy, az = fz - iz;
            int grown = (int)(filled * Ease(age / GROW));
            float acc = 0;
            for (int c = 0; c < 8; c++)
            {
                int x = ix + (c & 1), y = iy + ((c >> 1) & 1), z = iz + ((c >> 2) & 1);
                int i = Idx(x, y, z);
                if (order[i] < 0 || order[i] > grown) continue;
                float w = ((c & 1) != 0 ? ax : 1 - ax) * (((c >> 1) & 1) != 0 ? ay : 1 - ay) * (((c >> 2) & 1) != 0 ? az : 1 - az);
                acc += w * dens[i] * (1 - Math.Min(1f, Math.Max(clear[i], tiro[i])));   // HE passa de 1: fica limpo um tempo antes de voltar
            }
            if (acc <= 0) return 0;
            // fervido: ruído 3D que se mexe devagar dá as bolotas e a borda viva
            float n = Noise.Value3(p.x * 0.07f, p.y * 0.07f, p.z * 0.09f + t * 0.25f) * 0.65f + Noise.Value3(p.x * 0.17f + t * 0.3f, p.y * 0.17f, p.z * 0.2f) * 0.35f;
            float life = age < LIFE - FADE ? 1f : Math.Max(0, (LIFE - age) / FADE);
            return Math.Clamp(acc * (0.55f + 0.9f * n) - (1 - life) * 0.9f, 0, 1);
        }

        static float Ease(float x) { x = Math.Clamp(x, 0, 1); return 1 - (1 - x) * (1 - x); }

        /// <summary>Caixa no mundo (pra cortar o raio).</summary>
        public (V3 mn, V3 mx) Bounds => (new V3(x0 * C, y0 * C, z0 * C), new V3((x0 + nx) * C, (y0 + ny) * C, (z0 + nz) * C));
    }

    /// <summary>
    /// Molotov: fogo que escorre pelo chão a partir de onde a garrafa quebrou — células de 4 unidades no mesmo nível
    /// (degrau pequeno sobe, queda desce), parede segura. Espalha em ~0,5 s, queima ~7 s, apaga no último segundo.
    /// </summary>
    public class FireArea
    {
        public const int C = 4, CELLS = 170;   // ~raio 30 em área aberta (num corredor vai mais longe)
        public const float SPREAD = 0.5f, LIFE = 7f;
        public readonly V3 origin; public float age;
        public readonly List<(int x, int y, int z, int order, float phase)> cells = new List<(int, int, int, int, float)>();

        /// <summary>Espalha por distância de verdade (8 vizinhos, diagonal = raiz de 2) com um "atrito" sorteado por célula:
        /// sai uma mancha redonda de borda irregular — com 4 vizinhos e fila simples saía um losango (quadrado na tela).</summary>
        public FireArea(V3 origin)
        {
            this.origin = origin;
            int sx = (int)origin.x / C, sy = (int)origin.y / C;
            var rng = new Rng(sx * 31 + sy);
            int Id(int x, int y) => x * 4096 + y;
            var dist = new Dictionary<int, float>(); var feito = new HashSet<int>();
            var pq = new MinHeap(); pq.Push(Id(sx, sy), 0); dist[Id(sx, sy)] = 0;
            float semente = rng.Uniform(0, 100);
            bool Anda(int x, int y) { int wx = x * C + C / 2, wy = y * C + C / 2; return InMap(wx, wy) && walk[wx, wy]; }
            int Piso(int x, int y) => floor[x * C + C / 2, y * C + C / 2];
            while (pq.Count > 0 && cells.Count < CELLS)
            {
                var (id, d) = pq.Pop();
                if (!feito.Add(id)) continue;
                int x = id / 4096, y = id % 4096;
                if (!Anda(x, y)) continue;
                int fz = Piso(x, y);
                cells.Add((x, y, fz, cells.Count, rng.Uniform(0, 6.28f)));
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int u = x + dx, v = y + dy, nid = Id(u, v);
                        if (feito.Contains(nid) || !Anda(u, v)) continue;
                        if (dx != 0 && dy != 0 && (!Anda(x + dx, y) || !Anda(x, y + dy))) continue;   // não vaza pela quina
                        if (Piso(u, v) - fz > 4) continue;            // não sobe parede/degrau alto
                        float rough = 0.75f + 0.6f * Noise.Value3(u * 0.45f, v * 0.45f, semente);
                        float nd = d + (dx != 0 && dy != 0 ? 1.4142f : 1f) * rough;
                        if (!dist.TryGetValue(nid, out float od) || nd < od) { dist[nid] = nd; pq.Push(nid, nd); }
                    }
            }
        }

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;
        public bool Alive => age < LIFE;
        public void Update(float dt) => age += dt;
        public int Visible => (int)(cells.Count * Math.Clamp(age / SPREAD, 0, 1));
        public float Strength => age < LIFE - 1 ? 1 : Math.Max(0, LIFE - age);
    }

    /// <summary>Ruído de valor 3D suave (determinístico).</summary>
    public static class Noise
    {
        static float H(int x, int y, int z)
        {
            unchecked { int h = x * 374761393 + y * 668265263 + z * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }
        }
        public static float Value3(float x, float y, float z)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y), iz = (int)MathF.Floor(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float L(float a, float b, float t) => a + (b - a) * t;
            return L(L(L(H(ix, iy, iz), H(ix + 1, iy, iz), fx), L(H(ix, iy + 1, iz), H(ix + 1, iy + 1, iz), fx), fy),
                     L(L(H(ix, iy, iz + 1), H(ix + 1, iy, iz + 1), fx), L(H(ix, iy + 1, iz + 1), H(ix + 1, iy + 1, iz + 1), fx), fy), fz);
        }
    }
}
