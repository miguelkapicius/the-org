using System;
using System.Collections.Generic;

namespace CSM.View
{
    /// <summary>Primitiva analítica do personagem (elipsoide, caixa ou cápsula), com material da paleta e "parte"
    /// (as linhas internas do contorno saem entre partes diferentes).</summary>
    public abstract class Prim
    {
        public int mat, part;
        public abstract float Hit(V3 O, V3 D);
        public abstract V3 Normal(V3 P);
        public abstract (V3 c, float r) Bound();
    }

    public class Ell : Prim
    {
        public V3 c, r;
        public Ell(V3 c, V3 r, int mat, int part) { this.c = c; this.r = r; this.mat = mat; this.part = part; }
        public override float Hit(V3 O, V3 D)
        {
            V3 oc = new V3((O.x - c.x) / r.x, (O.y - c.y) / r.y, (O.z - c.z) / r.z);
            V3 dc = new V3(D.x / r.x, D.y / r.y, D.z / r.z);
            float a = dc.Dot(dc), b = oc.Dot(dc), cc = oc.Dot(oc) - 1f;
            float disc = b * b - a * cc;
            return disc > 0 ? (-b - MathF.Sqrt(disc)) / a : float.PositiveInfinity;
        }
        public override V3 Normal(V3 P) => new V3((P.x - c.x) / (r.x * r.x), (P.y - c.y) / (r.y * r.y), (P.z - c.z) / (r.z * r.z)).Norm;
        public override (V3, float) Bound() => (c, MathF.Max(r.x, MathF.Max(r.y, r.z)));
    }

    public class Box : Prim
    {
        public V3 c, h;
        public Box(V3 c, V3 h, int mat, int part) { this.c = c; this.h = h; this.mat = mat; this.part = part; }
        public override float Hit(V3 O, V3 D)
        {
            float tn = float.NegativeInfinity, tf = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                float o = a == 0 ? O.x : a == 1 ? O.y : O.z, d = a == 0 ? D.x : a == 1 ? D.y : D.z;
                float cc = a == 0 ? c.x : a == 1 ? c.y : c.z, hh = a == 0 ? h.x : a == 1 ? h.y : h.z;
                if (MathF.Abs(d) < 1e-9f) d = 1e-9f;
                float t1 = (cc - hh - o) / d, t2 = (cc + hh - o) / d;
                tn = MathF.Max(tn, MathF.Min(t1, t2)); tf = MathF.Min(tf, MathF.Max(t1, t2));
            }
            return tn < tf ? tn : float.PositiveInfinity;
        }
        public override V3 Normal(V3 P)
        {
            V3 d = new V3((P.x - c.x) / h.x, (P.y - c.y) / h.y, (P.z - c.z) / h.z);
            float ax = MathF.Abs(d.x), ay = MathF.Abs(d.y), az = MathF.Abs(d.z);
            if (ax >= ay && ax >= az) return new V3(MathF.Sign(d.x), 0, 0);
            if (ay >= az) return new V3(0, MathF.Sign(d.y), 0);
            return new V3(0, 0, MathF.Sign(d.z));
        }
        public override (V3, float) Bound() => (c, h.Len);
    }

    public class Cap : Prim
    {
        public V3 a, b; public float r;
        public Cap(V3 a, V3 b, float r, int mat, int part) { this.a = a; this.b = b; this.r = r; this.mat = mat; this.part = part; }
        public override float Hit(V3 O, V3 D)
        {
            V3 ba = b - a, oa = O - a;
            float baba = ba.Dot(ba), bard = ba.Dot(D), baoa = oa.Dot(ba), rdoa = oa.Dot(D), oaoa = oa.Dot(oa);
            float A = baba - bard * bard, B = baba * rdoa - baoa * bard, C = baba * oaoa - baoa * baoa - r * r * baba;
            float h = B * B - A * C, t = float.PositiveInfinity;
            if (A > 1e-9f && h >= 0)
            {
                float tc = (-B - MathF.Sqrt(h)) / A, y = baoa + tc * bard;
                if (y > 0 && y < baba) t = tc;
            }
            foreach (V3 e in new[] { a, b })
            {
                V3 oc = O - e;
                float bb = oc.Dot(D), cc = oc.Dot(oc) - r * r, hh = bb * bb - cc;
                if (hh >= 0) t = MathF.Min(t, -bb - MathF.Sqrt(hh));
            }
            return t;
        }
        public override V3 Normal(V3 P)
        {
            V3 ba = b - a;
            float k = Math.Clamp((P - a).Dot(ba) / ba.Dot(ba), 0, 1);
            return (P - (a + ba * k)).Norm;
        }
        public override (V3, float) Bound() => ((a + b) / 2, (b - a).Len / 2 + r);
    }

    /// <summary>Pose do personagem (pontos das mãos e da arma no espaço local: x frente, y esquerda, z cima).</summary>
    public class Pose
    {
        public V3 gA, gB, rh, lh;
        public float crouch, rec; public float? walk, run;
        public bool can, awp, flash;
        public string gun, nade = "smoke";   // arma (Weapons.ALL) e granada na mão quando can
        public Pose Clone() => (Pose)MemberwiseClone();
    }

    /// <summary>Personagem chibi procedural (cabeça grande, capacete, colete, arma) + poses e primitivas extras.</summary>
    public static class Chibi
    {
        public static V3 P3(float x, float y, float z) => new V3(x, y, z);
        public static readonly (V3 gA, V3 gB, V3 rh, V3 lh) AIM = (P3(1.2f, -1f, 11.2f), P3(11.5f, -1f, 11f), P3(3.4f, -1f, 10.4f), P3(6f, -0.8f, 10.8f));
        public static readonly (V3 gA, V3 gB, V3 rh, V3 lh) RUNG = (P3(1.6f, -1.6f, 10.4f), P3(10.4f, -0.2f, 7.4f), P3(3.4f, -1.6f, 9.8f), P3(6.6f, -0.9f, 8.6f));
        public static readonly (V3 gA, V3 gB, V3 rh, V3 lh) PISTOL = (P3(2.4f, -1f, 11.6f), P3(6.8f, -1f, 11.6f), P3(3.4f, -1f, 11f), P3(4.6f, -0.4f, 11f));
        public static readonly (V3 gA, V3 gB, V3 rh, V3 lh) AWPP = (P3(1.2f, -1f, 11.4f), P3(16f, -1f, 11.2f), P3(3.4f, -1f, 10.6f), P3(7f, -0.8f, 10.9f));
        public static readonly (V3 gA, V3 gB, V3 lh) LOW = (P3(0.8f, 3f, 9f), P3(9.5f, 3f, 7f), P3(4f, 3.2f, 8.6f));
        public static readonly (V3 gA, V3 gB) SLUNG = (P3(-3.8f, 3.4f, 15f), P3(-3.6f, -3.2f, 7.5f));
        public static readonly (V3 rh, V3 lh) PLANT_H = (P3(4.4f, -1.6f, 7.2f), P3(4.4f, 1.6f, 7.2f));
        static readonly V3[] RK = { P3(2.5f, -4.2f, 8.4f), P3(1.6f, -4.8f, 10.6f), P3(-3.6f, -5f, 15.4f), P3(4.4f, -4.6f, 18f), P3(7.4f, -4f, 15.6f), P3(5.4f, -4f, 11.6f) };

        public static Pose Aim() => new Pose { gA = AIM.gA, gB = AIM.gB, rh = AIM.rh, lh = AIM.lh };

        static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

        static V3 Keyed(float[] times, V3[] vals, float f)
        {
            if (f <= times[0]) return vals[0];
            for (int i = 0; i < times.Length - 1; i++)
                if (f <= times[i + 1]) return V3.Lerp(vals[i], vals[i + 1], Smooth((f - times[i]) / (times[i + 1] - times[i])));
            return vals[vals.Length - 1];
        }

        /// <summary>Arremesso de granada (f em quadros de 0 a 16): braço direito sobe, arma desce, lata na mão.</summary>
        public static void ThrowPose(float f, Pose pose)
        {
            f = MathF.Min(f, 16);
            pose.rh = Keyed(new float[] { 0, 3, 5, 8, 10, 11, 13, 16 }, new[] { AIM.rh, RK[0], RK[1], RK[2], RK[3], RK[4], RK[5], AIM.rh }, f);
            float u = f <= 3 ? Smooth(f / 3f) : (f <= 13 ? 1f : 1f - Smooth((f - 13) / 3f));
            pose.gA = V3.Lerp(AIM.gA, LOW.gA, u); pose.gB = V3.Lerp(AIM.gB, LOW.gB, u); pose.lh = V3.Lerp(AIM.lh, LOW.lh, u);
            pose.can = 4 <= f && f < 11;
        }

        static (V3 knee, V3 end) Ik3(V3 s, V3 h, V3 pole, float l1 = 4.2f, float l2 = 4.2f)
        {
            V3 v = h - s;
            float d = MathF.Max(v.Len, 1e-6f);
            V3 u = v / d;
            float dd = MathF.Min(d, l1 + l2 - 0.05f);
            float a = (l1 * l1 - l2 * l2 + dd * dd) / (2 * dd);
            float hh = MathF.Sqrt(MathF.Max(l1 * l1 - a * a, 0));
            V3 mid = s + u * a;
            V3 p = pole - u * pole.Dot(u);
            float pn = p.Len;
            p = pn > 1e-6f ? p / pn : new V3(0, 0, -1);
            return (mid + p * hh, s + u * dd);
        }

        /// <summary>Primitivas do chibi na pose (espaço local) e a boca do cano (gB).</summary>
        public static (List<Prim> prims, V3 muzzle) Build(Pose pose, TeamMats M)
        {
            var P = new List<Prim>(48);
            float c = pose.crouch;
            float bz = -2.8f * c, lean = 0, sway = 0;
            if (pose.walk.HasValue) bz += 0.5f * MathF.Abs(MathF.Sin(pose.walk.Value));
            if (pose.run.HasValue)
            {
                float rph = pose.run.Value;
                bz += -0.5f + 1.3f * (1f - MathF.Abs(MathF.Sin(rph)));
                lean = 0.2f; sway = 0.35f * MathF.Sin(rph);
            }
            float hipz = 7f + bz, tl = MathF.Tan(lean);
            V3 L(V3 v) { float z = v.z + bz; return new V3(v.x + tl * (z - hipz), v.y + sway, z); }
            void E(V3 c_, V3 r, int m, int p) => P.Add(new Ell(L(c_), r, m, p));
            void B(V3 c_, V3 h, int m, int p) => P.Add(new Box(L(c_), h, m, p));
            void C(V3 a, V3 b, float r, int m, int p) => P.Add(new Cap(a, b, r, m, p));

            foreach (var (sd, part) in new[] { (1, 7), (-1, 8) })
            {
                V3 hip = P3(0, sd * 1.8f, hipz);
                V3 ank = P3(sd > 0 ? 0.4f : -0.4f, sd * 2f, 1.5f);
                V3 pole = P3(1, 0, 0.2f);
                if (pose.walk.HasValue)
                {
                    float ph = pose.walk.Value + (sd > 0 ? 0 : MathF.PI);
                    ank.x += 3f * MathF.Sin(ph);
                    ank.z += MathF.Max(0, MathF.Cos(ph)) * 1.6f;
                }
                if (pose.run.HasValue)
                {
                    float ph = pose.run.Value + (sd > 0 ? 0 : MathF.PI);
                    ank.x = 0.6f + 3.8f * MathF.Sin(ph);
                    float lift = MathF.Max(0, MathF.Cos(ph));
                    ank.z = 1.5f + 3.6f * lift;
                    ank.x -= 1.6f * lift * MathF.Max(0, -MathF.Sin(ph));
                    pole = P3(1, 0, 0.6f);
                }
                var (knee, ank2) = Ik3(hip, ank, pole, 3f, 3f);
                C(hip, knee, 1.8f, M.pants, part);
                C(knee, ank2, 1.6f, M.pants, part);
                if (M.ct) P.Add(new Ell(knee + P3(0.9f, 0, 0), P3(0.9f, 1.25f, 1.2f), M.mask, part));   // joelheira
                P.Add(new Ell(P3(ank2.x + 0.8f, ank2.y, ank2.z - 0.3f), P3(2.2f, 1.6f, 1.15f), M.boot, part + 2));
            }
            E(P3(0, 0, 7.6f), P3(2.4f, 3.3f, 1.8f), M.pants, 9);
            E(P3(0, 0, 8.7f), P3(2.7f, 3.6f, 0.7f), M.belt, 10);
            B(P3(0.4f, -3.3f, 8.0f), P3(0.9f, 0.5f, 1.1f), M.ct ? M.mask : M.belt, 10);   // coldre/bolsa na cintura
            E(P3(0, 0, 13.9f), P3(1.9f, 2.7f, 0.9f), M.ct ? M.sleeve : 45, 11);              // gola (TR: bandana no pescoço)
            E(P3(0, 0, 11.2f), P3(2.9f, 3.8f, 3.1f), M.vest, 4);
            if (M.ct)
            {
                // CT: colete oliva com 3 porta-carregadores, rádio no ombro esquerdo com antena curta, ombreiras
                // (sem mochila); joelheira e coldre de perna ficam nas pernas/cintura
                for (int k = -1; k <= 1; k++) B(P3(2.75f, k * 1.45f, 10.4f), P3(0.5f, 0.62f, 0.95f), M.plate, 5);
                B(P3(2.5f, 0, 12.6f), P3(0.45f, 2.3f, 0.7f), M.plate, 5);
                B(P3(2.2f, 2.5f, 13.1f), P3(0.5f, 0.45f, 0.75f), M.mask, 24);                  // rádio
                C(P3(2.2f, 2.8f, 13.8f), P3(2.0f, 3.0f, 15.6f), 0.15f, M.mask, 24);           // antena
                foreach (int sd in new[] { 1, -1 }) E(P3(0, sd * 3.5f, 13.6f), P3(1.7f, 1.05f, 0.95f), M.plate, 24);   // ombreira
            }
            else
            {
                // TR: colete grafite liso com dois bolsos, bandoleira de munição atravessada no peito, faca no colete
                B(P3(2.35f, 0, 11.7f), P3(0.6f, 2.3f, 1.9f), M.plate, 5);
                for (int k = -1; k <= 1; k += 2) B(P3(2.8f, k * 1.25f, 9.9f), P3(0.42f, 0.6f, 0.7f), M.plate, 5);
                V3 b0 = P3(2.95f, 2.9f, 13.4f), b1 = P3(3.15f, -2.7f, 8.9f);
                C(b0, b1, 0.42f, 60, 24);                                                        // bandoleira
                for (int k = 1; k <= 6; k++) E(V3.Lerp(b0, b1, k / 7f) + P3(0.35f, 0, 0), P3(0.28f, 0.3f, 0.42f), 30, 24);   // cartuchos
                C(P3(-2.9f, -2.9f, 13.4f), P3(-3.0f, 2.7f, 8.9f), 0.42f, 60, 24);              // bandoleira nas costas
                C(P3(2.95f, 1.9f, 12.6f), P3(2.95f, 1.9f, 11.0f), 0.3f, 54, 24);               // cabo da faca
                C(P3(2.95f, 1.9f, 11.0f), P3(2.95f, 1.9f, 9.6f), 0.36f, 60, 24);               // bainha
            }
            // cabeça chibi com rosto: pele, olhos com brilho, sobrancelhas
            E(P3(0.5f, 0, 18.4f), P3(4.6f, 4.7f, 4.3f), M.skin, 2);
            foreach (int sd in new[] { 1, -1 })
            {
                E(P3(4.85f, sd * 1.6f, 18.4f), P3(0.5f, 0.62f, 1.0f), M.eye, 21);
                E(P3(5.2f, sd * 1.42f, 18.85f), P3(0.2f, 0.24f, 0.3f), 33, 21);
                E(P3(4.75f, sd * 1.75f, 19.85f), P3(0.3f, 0.8f, 0.22f), 45, 21);   // sobrancelha
            }
            if (M.ct)
            {
                // CT: capacete tático, óculos de proteção apoiados no capacete, fone nas orelhas
                E(P3(-0.3f, 0, 20.3f), P3(5f, 5.15f, 3.9f), M.helm, 1);
                B(P3(3.9f, 0, 21.0f), P3(0.4f, 2.6f, 0.3f), M.mask, 3);
                foreach (int sd in new[] { 1, -1 })
                {
                    E(P3(3.95f, sd * 1.7f, 21.6f), P3(0.65f, 1.35f, 0.95f), M.mask, 3);
                    E(P3(4.3f, sd * 1.7f, 21.65f), P3(0.5f, 1.05f, 0.72f), M.lens, 3);
                    E(P3(0.1f, sd * 4.95f, 18.8f), P3(1.1f, 0.8f, 1.4f), M.mask, 1);
                }
            }
            else
            {
                // TR: cabelo curto escuro, barba por fazer e bandana no pescoço
                E(P3(-0.4f, 0, 20.0f), P3(4.75f, 4.85f, 3.7f), M.helm, 1);
                E(P3(1.4f, 0, 16.5f), P3(3.6f, 4.0f, 2.0f), 60, 22);
                E(P3(4.75f, 0, 17.1f), P3(0.35f, 0.9f, 0.28f), 54, 21);   // boca
            }
            V3 SL = L(P3(0, 3.6f, 13.4f)), SR = L(P3(0, -3.6f, 13.4f));
            V3 rsh = P3(-pose.rec, 0, 0);
            if (pose.run.HasValue) rsh += P3(0, 0, 0.55f * MathF.Sin(2 * pose.run.Value));
            V3 rh = L(pose.rh + rsh), lh = L(pose.lh + rsh);
            // braço: manga em cima (TR: manga curta, resto pele), antebraço, luva
            void Arm(V3 S, V3 e, V3 h, int pt)
            {
                V3 mid = V3.Lerp(S, e, 0.6f);
                C(S, mid, 1.55f, M.sleeve, pt);
                C(mid, e, 1.45f, M.ct ? M.sleeve : M.fore, pt);
                C(e, h, 1.35f, M.fore, pt);
            }
            var (eL, hL) = Ik3(SL, lh, P3(0, 0.6f, -1));
            Arm(SL, eL, hL, 12);
            P.Add(new Ell(hL, P3(1.45f, 1.4f, 1.4f), M.glove, 15));
            var (eR, hR) = Ik3(SR, rh, P3(0, -0.6f, -1));
            Arm(SR, eR, hR, 13);
            if (pose.can) Weapons.Nade(pose.nade, hR, P, M);
            P.Add(new Ell(hR, P3(1.45f, 1.4f, 1.4f), M.glove, 16));
            if (pose.gun == "nade") return (P, hR);   // só a granada na mão
            V3 gA = L(pose.gA + rsh), gB = L(pose.gB + rsh);
            V3 d = gB - gA;
            var wd = Weapons.Get(pose.gun);
            if (wd != null)
            {
                V3 fdir = d / MathF.Max(d.Len, 1e-6f);
                Weapons.Build(wd, gA, fdir, P, M);
                return (P, gA + fdir * wd.len * Weapons.SC);
            }
            C(gA, gA + d * 0.8f, 1f, M.gun, 14);
            C(gA, gA + d * 0.22f, 1.3f, M.gundk, 14);
            C(gA + d * 0.45f, gA + d * 0.8f, 1.2f, M.gundk, 14);
            C(gA + d * 0.8f, gB, 0.65f, M.gundk, 14);
            V3 m0 = gA + d * 0.45f;
            V3 dn = MathF.Abs(d.z) < 6 ? P3(0.4f, 0, -3f) : P3(2.6f, 0, 0);
            C(m0, m0 + dn, 0.8f, M.gundk, 14);
            if (pose.awp)
            {
                V3 upv = P3(0, 0, 1.6f);
                C(gA + d * 0.28f + upv, gA + d * 0.56f + upv, 0.75f, M.gundk, 14);
                C(gA + d * 0.28f + upv, gA + d * 0.31f + upv, 0.95f, M.accent, 14);
                C(gA + d * 0.42f + upv * 0.4f, gA + d * 0.42f + upv * 0.9f, 0.45f, M.gundk, 14);
                C(gA - d * 0.12f, gA + d * 0.05f, 1.45f, M.gun, 14);
            }
            return (P, gB);
        }

        public static List<Prim> BombPrims() => new List<Prim>
        {
            new Box(P3(0, 0, 1.3f), P3(2.4f, 1.7f, 1.3f), 40, 1),
            new Box(P3(0.5f, 0, 2.7f), P3(1.1f, 1f, 0.18f), 34, 2),
            new Cap(P3(-1.8f, -1.7f, 1f), P3(-1.8f, 1.7f, 1f), 0.75f, 41, 3),
            new Cap(P3(-0.6f, -1.7f, 1f), P3(-0.6f, 1.7f, 1f), 0.75f, 41, 3),
            new Cap(P3(1.6f, 1.8f, 2.4f), P3(-1.2f, 1.8f, 2.6f), 0.25f, 30, 4),
        };

        /// <summary>O que voa da cabeça no HS: o capacete do CT com os óculos; o TR não tem capacete (lista vazia).</summary>
        public static List<Prim> HelmPrims(TeamMats t) => !t.ct ? new List<Prim>() : new List<Prim>
        {
            new Ell(P3(0, 0, 0), P3(5f, 5.15f, 3.9f), t.helm, 1),
            new Ell(P3(4.25f, 1.7f, 1.3f), P3(0.65f, 1.35f, 0.95f), t.mask, 3),
            new Ell(P3(4.25f, -1.7f, 1.3f), P3(0.65f, 1.35f, 0.95f), t.mask, 3),
        };

        /// <summary>Local -> mundo: tomba (morto) em volta de x = piv e gira theta em volta de z.</summary>
        public static V3 LocalToWorld(float theta, V3 v, V3 pos, float tilt = 0, float piv = -1)
        {
            if (tilt != 0)
            {
                float ct = MathF.Cos(tilt), st = MathF.Sin(tilt);
                v = new V3((v.x - piv) * ct - v.z * st + piv, v.y, (v.x - piv) * st + v.z * ct);
            }
            float c = MathF.Cos(theta), s = MathF.Sin(theta);
            return new V3(pos.x + c * v.x - s * v.y, pos.y + s * v.x + c * v.y, pos.z + v.z);
        }
    }
}
