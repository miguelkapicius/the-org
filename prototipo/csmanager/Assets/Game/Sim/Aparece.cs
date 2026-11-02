using System;
using System.IO;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// De que direção o inimigo costuma aparecer pra quem está em cada lugar, em cada momento do round: a experiência do
    /// pro de saber onde deixar a mira. Não olha onde o inimigo está agora; é só o costume, medido em muitos rounds
    /// (Headless -aparece) e salvo em aparece.bin. Quem usa ainda confere se pode ter alguém ali (Ameaca).
    /// </summary>
    public static class Aparece
    {
        public const int BINS = 72, FAIXAS = 5, CEL = 36;
        public const string ARQ = "Assets/Game/Sim/aparece.bin";
        static readonly int NX = MapData.NX / CEL + 1, NY = MapData.NY / CEL + 1;
        static float[] tab; static bool tentou; static readonly object trava = new object();

        static int Faixa(float t, bool plantado) => plantado ? 4 : t < 20 ? 0 : t < 45 ? 1 : t < 80 ? 2 : 3;
        static int Ind(int tm, int f, int cx, int cy) => (((tm * FAIXAS + f) * NX + cx) * NY + cy) * BINS;
        static int Bin(float ang) { int b = (int)((ang + MathF.PI) / (MathF.PI * 2) * BINS); return ((b % BINS) + BINS) % BINS; }
        public static float AngDoBin(int b) => (b + 0.5f) / BINS * MathF.PI * 2 - MathF.PI;

        static bool Carrega()
        {
            if (tab != null) return true;
            if (tentou) return false;
            lock (trava)
            {
                if (tab != null) return true;
                tentou = true;
                if (!File.Exists(ARQ)) return false;
                var b = File.ReadAllBytes(ARQ); var t = new float[b.Length / 4];
                Buffer.BlockCopy(b, 0, t, 0, b.Length);
                tab = t; return true;
            }
        }

        /// <summary>Monta h (soma 1, com o bin vizinho e as células vizinhas pesando menos); devolve quantos contatos entraram.</summary>
        public static float Monta(Team tm, V2 p, float t, bool plantado, float[] h)
        {
            Array.Clear(h, 0, BINS);
            if (!Carrega()) return 0;
            int f = Faixa(t, plantado), cx = (int)(p.x / CEL), cy = (int)(p.y / CEL); float tot = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= NX || y >= NY) continue;
                    float w = dx == 0 && dy == 0 ? 1f : 0.35f; int i0 = Ind((int)tm, f, x, y);
                    for (int b = 0; b < BINS; b++)
                    {
                        float v = tab[i0 + b] * w; if (v <= 0) continue;
                        h[b] += v; h[(b + 1) % BINS] += 0.4f * v; h[(b + BINS - 1) % BINS] += 0.4f * v; tot += v * 1.8f;
                    }
                }
            if (tot > 0) for (int b = 0; b < BINS; b++) h[b] /= tot;
            return tot;
        }

        public static float Parte(float[] h, float ang) => h[Bin(ang)];

        /// <summary>Junta os contatos de um round na contagem (só no aprendizado).</summary>
        public static void Soma(float[] soma, Match m)
        {
            foreach (var (tm, p, t, pl, ang) in m.aparecidos)
                soma[Ind((int)tm, Faixa(t, pl), (int)(p.x / CEL), (int)(p.y / CEL)) + Bin(ang)]++;
        }

        public static float[] Vazio() => new float[2 * FAIXAS * NX * NY * BINS];

        public static void Grava(float[] soma)
        {
            var b = new byte[soma.Length * 4]; Buffer.BlockCopy(soma, 0, b, 0, b.Length);
            File.WriteAllBytes(ARQ, b); tab = null; tentou = false;
        }
    }
}
