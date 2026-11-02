using System;
using System.Threading.Tasks;
using static CSM.View.Iso;

namespace CSM.View
{
    /// <summary>
    /// Imagem estática do mapa inteiro (BW x BH): raio por pixel na grade de voxels (DDA), sombreamento por face (topo
    /// claro, faces +x/+y mais escuras), bisel dos ladrilhos, pintas, realce de quina e sombra do sol por raymarch.
    /// Guarda também a profundidade (T), o objeto (OBJ) e a máscara de piso pra compor os personagens por cima.
    /// </summary>
    public class StaticMap
    {
        public readonly byte[] G;
        public readonly C3[] RGB = new C3[BW * BH];
        public readonly float[] T = new float[BW * BH];
        public readonly short[] OBJ = new short[BW * BH];
        public readonly bool[] FLOOR = new bool[BW * BH];
        const int NX = Map.MapData.NX, NY = Map.MapData.NY, NZ = MapVoxels.NZ;

        public StaticMap()
        {
            G = MapVoxels.Build();
            var F = new byte[BW * BH];
            var Cc = new byte[BW * BH];
            Parallel.For(0, BH, jj =>
            {
                for (int ii = 0; ii < BW; ii++)
                {
                    Cast(ii + 0.5f, jj + 0.5f, out float t, out byte f, out byte c);
                    int k = jj * BW + ii;
                    T[k] = t; F[k] = f; Cc[k] = c;
                }
            });
            var NF = new[] { new V3(1, 0, 0), new V3(0, 1, 0), new V3(0, 0, 1) };
            Parallel.For(0, BH, jj =>
            {
                for (int ii = 0; ii < BW; ii++)
                {
                    int k = jj * BW + ii;
                    byte c = Cc[k];
                    if (c == 0) { T[k] = float.PositiveInfinity; continue; }
                    byte f = F[k];
                    int band = f == 0 ? 0 : (f == 1 ? 1 : 2);
                    C3 rgb = RAMP[c, band];
                    V3 pos = PixelRayOrigin(ii + 0.5f, jj + 0.5f) + DV * T[k];
                    long hsh = ((ii * 73856093L) ^ (jj * 19349663L)) % 101;
                    bool tiled = c == 1 || c == 2 || c == 18 || c == 5 || c == 14;
                    bool floor = (tiled || c == 3 || c == 15 || (c >= 46 && c <= 49)) && f == 2;
                    FLOOR[k] = floor;
                    if (floor && tiled)
                    {
                        float u = Mod(pos.x, TL), v = Mod(pos.y, TL);
                        bool bev = ((u >= 1 && u < 2.2f) || (v >= 1 && v < 2.2f)) && c != 5;
                        if (bev && (ii + jj) % 2 == 0) rgb = RAMP[c, 3];
                        if (hsh == 0) rgb = RAMP[c, 1];
                        if (hsh == 1) rgb = RAMP[c, 3];
                    }
                    if (c == 6 && hsh < 3) rgb = RAMP[c, 1];
                    short obj = OBJ_TAB[c];
                    OBJ[k] = obj;
                    // realce da quina: topo com uma face lateral do mesmo objeto logo abaixo
                    if (f == 2 && jj + 1 < BH)
                    {
                        int kb = k + BW;
                        if (Cc[kb] != 0 && F[kb] != 2 && MathF.Abs(T[kb] - T[k]) < 3f && OBJ_TAB[Cc[kb]] == obj) rgb = RAMP[c, 3];
                    }
                    // sombra do sol (só nos topos)
                    if (f == 2)
                    {
                        V3 p = pos + NF[f] * 0.05f + SUN * 0.2f;
                        for (int s = 0; s < 100; s++)
                        {
                            V3 q = p + SUN * (s * 0.5f);
                            int qx = (int)MathF.Floor(q.x), qy = (int)MathF.Floor(q.y), qz = (int)MathF.Floor(q.z);
                            if (qx < 0 || qy < 0 || qz < 0 || qx >= NX || qy >= NY || qz >= NZ) continue;
                            if (G[(qx * NY + qy) * NZ + qz] > 0) { rgb = rgb * new C3(0.62f, 0.60f, 0.74f); break; }
                        }
                    }
                    RGB[k] = rgb;
                }
            });
        }

        static float Mod(float a, float m) { float r = a % m; return r < 0 ? r + m : r; }

        /// <summary>Origem do raio do pixel (sx, sy) da imagem estática (antes de andar na direção DV).</summary>
        public static V3 PixelRayOrigin(float sx, float sy)
        {
            float sxp = (sx - CXs) / S, syp = (CYs - sy) / S;
            return RV * sxp + UV * syp - DV * LRAY;
        }

        void Cast(float sx, float sy, out float tHit, out byte face, out byte col)
        {
            tHit = float.PositiveInfinity; face = 0; col = 0;
            V3 O = PixelRayOrigin(sx, sy);
            float[] o = { O.x, O.y, O.z }, d = { DV.x, DV.y, DV.z }, nxyz = { NX, NY, NZ };
            float tIn = float.NegativeInfinity, tEx = float.PositiveInfinity; int axIn = 0;
            for (int a = 0; a < 3; a++)
            {
                float tu = (nxyz[a] - o[a]) / d[a], tl = (0 - o[a]) / d[a];
                if (tu > tIn) { tIn = tu; axIn = a; }
                if (tl < tEx) tEx = tl;
            }
            if (!(tIn < tEx - 1e-9f)) return;
            float curT = tIn + 1e-4f;
            int curF = axIn;
            int[] ijk = new int[3];
            float[] tmax = new float[3], tdel = new float[3];
            for (int a = 0; a < 3; a++)
            {
                float p = o[a] + d[a] * curT;
                ijk[a] = Math.Clamp((int)MathF.Floor(p), 0, (int)nxyz[a] - 1);
                tmax[a] = (ijk[a] - o[a]) / d[a];
                tdel[a] = 1f / MathF.Abs(d[a]);
            }
            for (int it = 0; it < NX + NY + NZ + 4; it++)
            {
                byte cv = G[(ijk[0] * NY + ijk[1]) * NZ + ijk[2]];
                if (cv > 0) { tHit = curT; face = (byte)curF; col = cv; return; }
                int ax = tmax[0] < tmax[1] ? (tmax[0] < tmax[2] ? 0 : 2) : (tmax[1] < tmax[2] ? 1 : 2);
                curT = tmax[ax]; curF = ax;
                tmax[ax] += tdel[ax];
                ijk[ax] -= 1;
                if (ijk[ax] < 0) return;
            }
        }
    }
}
