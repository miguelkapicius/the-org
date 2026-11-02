using System;
using System.Collections.Generic;
using static CSM.View.Iso;

namespace CSM.View
{
    /// <summary>
    /// Rasteriza um personagem (primitivas analíticas) em pixel art: raio por pixel com 4 sub-amostras (fica a mais
    /// próxima), Lambert do sol quantizado em 4 tons da rampa do material com dithering Bayer 2x2, profundidade e "parte"
    /// pro contorno. Coordenadas de pixel = imagem estática inteira (o padrão do dithering não escorrega com a câmera).
    /// </summary>
    public class CharLayer
    {
        public int x0, y0, w, h;
        public float[] t; public C3[] rgb; public short[] part;

        static readonly (float x, float y)[] SUBS = { (0.25f, 0.25f), (0.75f, 0.25f), (0.25f, 0.75f), (0.75f, 0.75f) };

        public static CharLayer Render(List<Prim> prims, float theta, V3 pos, float tilt = 0, float piv = -1, float white = 0,
                                       int clipX0 = 0, int clipY0 = 0, int clipX1 = BW, int clipY1 = BH)
        {
            int n = prims.Count;
            var pb = new (float x0, float y0, float x1, float y1)[n];
            float xs0 = float.MaxValue, ys0 = float.MaxValue, xs1 = float.MinValue, ys1 = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                var (c, r) = prims[k].Bound();
                var (sx, sy) = Project(Chibi.LocalToWorld(theta, c, pos, tilt, piv));
                float e = r * S + 2;
                pb[k] = (sx - e, sy - e, sx + e, sy + e);
                xs0 = MathF.Min(xs0, sx - e); ys0 = MathF.Min(ys0, sy - e); xs1 = MathF.Max(xs1, sx + e); ys1 = MathF.Max(ys1, sy + e);
            }
            int X0 = Math.Max((int)xs0, clipX0), Y0 = Math.Max((int)ys0, clipY0);
            int X1 = Math.Min((int)xs1 + 1, clipX1), Y1 = Math.Min((int)ys1 + 1, clipY1);
            if (X1 <= X0 || Y1 <= Y0) return null;
            float ct = MathF.Cos(theta), st = MathF.Sin(theta), cp = MathF.Cos(tilt), sp = MathF.Sin(tilt);
            V3 Untilt(V3 q, bool dir)
            {
                float px = dir ? 0 : piv;
                return new V3((q.x - px) * cp + q.z * sp + px, q.y, -(q.x - px) * sp + q.z * cp);
            }
            V3 ToLocal(V3 Ow)
            {
                V3 dd = Ow - pos;
                return Untilt(new V3(ct * dd.x + st * dd.y, -st * dd.x + ct * dd.y, dd.z), false);
            }
            V3 Rot(V3 v) => Untilt(new V3(ct * v.x + st * v.y, -st * v.x + ct * v.y, v.z), true);
            V3 Dl = Rot(DV), Ll = Rot(SUN);
            var L = new CharLayer { x0 = X0, y0 = Y0, w = X1 - X0, h = Y1 - Y0 };
            int N = L.w * L.h;
            L.t = new float[N]; L.rgb = new C3[N]; L.part = new short[N];
            var cand = new List<int>(n);
            for (int jj = Y0; jj < Y1; jj++)
                for (int ii = X0; ii < X1; ii++)
                {
                    int k0 = (jj - Y0) * L.w + (ii - X0);
                    L.t[k0] = float.PositiveInfinity;
                    cand.Clear();
                    for (int k = 0; k < n; k++)
                        if (ii + 1 >= pb[k].x0 && ii <= pb[k].x1 && jj + 1 >= pb[k].y0 && jj <= pb[k].y1) cand.Add(k);
                    if (cand.Count == 0) continue;
                    float bestT = float.PositiveInfinity; int bestK = -1; V3 bestO = default;
                    foreach (var (ox, oy) in SUBS)
                    {
                        V3 Ol = ToLocal(StaticMap.PixelRayOrigin(ii + ox, jj + oy));
                        foreach (int k in cand)
                        {
                            float t = prims[k].Hit(Ol, Dl);
                            if (t < bestT) { bestT = t; bestK = k; bestO = Ol; }
                        }
                    }
                    if (bestK < 0) continue;
                    var pr = prims[bestK];
                    V3 P = bestO + Dl * bestT;
                    float lam = pr.Normal(P).Dot(Ll);
                    int band = BandOf(lam + BAY[jj % 2, ii % 2]);
                    C3 c = RAMP[pr.mat, band];
                    if (white > 0) c = C3.Lerp(c, new C3(255, 250, 240), white);
                    L.t[k0] = bestT; L.rgb[k0] = c; L.part[k0] = (short)pr.part;
                }
            return L;
        }
    }
}
