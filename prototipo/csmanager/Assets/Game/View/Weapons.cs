using System;
using System.Collections.Generic;

namespace CSM.View
{
    /// <summary>Caixa orientada (eixos próprios): peças de arma que acompanham a inclinação do cano.</summary>
    public class OBox : Prim
    {
        public V3 c, a0, a1, a2, h;
        public OBox(V3 c, V3 a0, V3 a1, V3 a2, V3 h, int mat, int part) { this.c = c; this.a0 = a0; this.a1 = a1; this.a2 = a2; this.h = h; this.mat = mat; this.part = part; }
        public override float Hit(V3 O, V3 D)
        {
            V3 o = O - c;
            float tn = float.NegativeInfinity, tf = float.PositiveInfinity;
            for (int k = 0; k < 3; k++)
            {
                V3 ax = k == 0 ? a0 : k == 1 ? a1 : a2;
                float hh = k == 0 ? h.x : k == 1 ? h.y : h.z;
                float oo = o.Dot(ax), d = D.Dot(ax);
                if (MathF.Abs(d) < 1e-9f) d = 1e-9f;
                float t1 = (-hh - oo) / d, t2 = (hh - oo) / d;
                tn = MathF.Max(tn, MathF.Min(t1, t2)); tf = MathF.Min(tf, MathF.Max(t1, t2));
            }
            return tn < tf ? tn : float.PositiveInfinity;
        }
        public override V3 Normal(V3 P)
        {
            V3 o = P - c;
            float x = o.Dot(a0) / h.x, y = o.Dot(a1) / h.y, z = o.Dot(a2) / h.z;
            float ax = MathF.Abs(x), ay = MathF.Abs(y), az = MathF.Abs(z);
            if (ax >= ay && ax >= az) return a0 * MathF.Sign(x);
            if (ay >= az) return a1 * MathF.Sign(y);
            return a2 * MathF.Sign(z);
        }
        public override (V3, float) Bound() => (c, h.Len);
    }

    /// <summary>
    /// Armas do jogo como peças (coronha, ferrolho, carregador, luneta, silenciador) no sistema da arma: a = ao longo
    /// do cano a partir da coronha, s = esquerda, v = cima. Medidas na escala do chibi (fuzil ~10,5). Modelos
    /// autorais de leitura rápida: a silhueta é o que diferencia cada arma no tamanho do boneco.
    /// </summary>
    public static class Weapons
    {
        public enum Kind { Rifle, Sniper, Smg, Pistol }
        public const float SC = 1.25f;   // armas um pouco maiores que o real (estilo chibi), pra silhueta ler

        public class Def
        {
            public string id, name; public Kind kind;
            public float len, grip, fore;   // comprimento, onde fica a mão direita e a esquerda (ao longo do cano)
        }

        public static readonly Dictionary<string, Def> ALL = new Dictionary<string, Def>
        {
            ["ak"] = new Def { id = "ak", name = "AK-47", kind = Kind.Rifle, len = 10.6f, grip = 3.4f, fore = 7.0f },
            ["m4"] = new Def { id = "m4", name = "M4A4", kind = Kind.Rifle, len = 10.4f, grip = 3.5f, fore = 6.8f },
            ["m4s"] = new Def { id = "m4s", name = "M4A1-S", kind = Kind.Rifle, len = 12.0f, grip = 3.5f, fore = 6.6f },
            ["awp"] = new Def { id = "awp", name = "AWP", kind = Kind.Sniper, len = 15.0f, grip = 3.6f, fore = 7.2f },
            ["mp9"] = new Def { id = "mp9", name = "MP9", kind = Kind.Smg, len = 7.6f, grip = 2.8f, fore = 4.7f },
            ["mac10"] = new Def { id = "mac10", name = "MAC-10", kind = Kind.Smg, len = 5.8f, grip = 2.3f, fore = 4.2f },
            ["usp"] = new Def { id = "usp", name = "USP-S", kind = Kind.Pistol, len = 7.4f, grip = 0.9f, fore = 1.1f },
            ["glock"] = new Def { id = "glock", name = "Glock-18", kind = Kind.Pistol, len = 4.0f, grip = 0.9f, fore = 1.1f },
            ["deagle"] = new Def { id = "deagle", name = "Desert Eagle", kind = Kind.Pistol, len = 5.0f, grip = 0.9f, fore = 1.1f },
            ["p250"] = new Def { id = "p250", name = "P250", kind = Kind.Pistol, len = 4.2f, grip = 0.9f, fore = 1.1f },
        };

        public static Def Get(string id) => id != null && ALL.TryGetValue(id, out var d) ? d : null;

        /// <summary>Onde a arma fica no corpo (espaço local do boneco: x frente, y esquerda, z cima) em cada postura.</summary>
        public static (V3 gA, V3 f) Mount(Def d, bool lowered)
        {
            V3 f = new V3(1, 0, -0.02f).Norm;
            V3 gA = d.kind switch
            {
                Kind.Pistol => new V3(3.0f, -0.6f, 11.7f),
                Kind.Smg => d.id == "mac10" ? new V3(2.2f, -0.8f, 11.4f) : new V3(0.6f, -1f, 11.3f),
                Kind.Sniper => new V3(0f, -1f, 11.4f),
                _ => new V3(0f, -1f, 11.2f),
            };
            if (lowered && d.kind != Kind.Pistol) { f = new V3(0.93f, 0.12f, -0.34f).Norm; gA = gA + new V3(0.6f, -0.5f, -0.7f); }
            return (gA, f);
        }

        static V3 Up(V3 f) { V3 z = new V3(0, 0, 1); V3 u = z - f * z.Dot(f); return u.Len < 1e-4f ? new V3(1, 0, 0) : u.Norm; }
        static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        /// <summary>Mãos na arma (direita no punho, esquerda na frente).</summary>
        public static (V3 rh, V3 lh) Hands(Def d, V3 gA, V3 f)
        {
            V3 u = Up(f), l = Cross(u, f);
            V3 rh = gA + f * (d.grip * SC) - u * 1.1f;
            V3 lh = d.kind == Kind.Pistol ? gA + f * (d.fore * SC) - u * 1.2f + l * 0.9f : gA + f * (d.fore * SC) - u * 0.8f;
            return (rh, lh);
        }

        /// <summary>Peças da arma a partir de gA na direção f.</summary>
        public static void Build(Def d, V3 gA, V3 f, List<Prim> P, TeamMats M, int part = 14)
        {
            V3 u = Up(f), l = Cross(u, f);
            int blk = M.gundk, dkg = M.gun, met = M.metal, wood = M.wood, grn = M.green;
            V3 G(float a, float s, float v) => gA + (f * a + l * s + u * v) * SC;
            // caixa reta no sistema da arma
            void Bx(float a0, float a1, float v0, float v1, float hw, int m) =>
                P.Add(new OBox(G((a0 + a1) / 2, 0, (v0 + v1) / 2), f, l, u, new V3((a1 - a0) / 2, hw, (v1 - v0) / 2) * SC, m, part));
            // caixa inclinada no plano da arma (ang em rad a partir do cano, positivo = pra cima)
            void Bt(float a, float v, float ang, float hl, float hh, float hw, int m)
            {
                V3 ax = f * MathF.Cos(ang) + u * MathF.Sin(ang), az = -f * MathF.Sin(ang) + u * MathF.Cos(ang);
                P.Add(new OBox(G(a, 0, v), ax, l, az, new V3(hl, hw, hh) * SC, m, part));
            }
            void Cy(float a0, float v0, float a1, float v1, float r, int m, float s = 0) => P.Add(new Cap(G(a0, s, v0), G(a1, s, v1), r * SC, m, part));
            switch (d.id)
            {
                case "ak":
                    Bt(1.5f, -0.35f, -0.12f, 1.6f, 0.62f, 0.45f, wood);            // coronha de madeira
                    Bx(0f, 0.25f, -1.05f, 0.35f, 0.5f, blk);                       // soleira
                    Bx(3.0f, 6.0f, -0.55f, 0.6f, 0.5f, dkg);                       // caixa da culatra
                    Cy(3.1f, 0.7f, 5.9f, 0.7f, 0.33f, blk);                        // tampa
                    Cy(3.6f, -0.5f, 3.1f, -2.1f, 0.42f, wood);                     // punho
                    Cy(4.6f, -0.6f, 4.9f, -2.0f, 0.5f, blk); Cy(4.9f, -2.0f, 5.7f, -3.1f, 0.5f, blk);   // carregador curvo
                    Bx(6.0f, 8.2f, -0.5f, 0.45f, 0.48f, wood);                     // guarda-mão
                    Cy(6.0f, 0.75f, 8.0f, 0.75f, 0.33f, wood);                     // tubo de gás coberto
                    Cy(8.2f, 0.05f, 10.6f, 0.05f, 0.22f, blk);                     // cano
                    Cy(9.9f, 0.1f, 9.9f, 0.9f, 0.18f, blk);                        // massa de mira
                    Cy(10.15f, 0.05f, 10.6f, 0.05f, 0.3f, blk);                    // quebra-chamas
                    break;
                case "m4":
                case "m4s":
                    Bx(0f, 2.2f, -0.9f, 0.5f, 0.45f, blk);                         // coronha telescópica
                    Cy(1.5f, 0.2f, 3.0f, 0.2f, 0.32f, blk);                        // tubo
                    Bx(2.9f, 5.4f, -0.6f, 0.3f, 0.45f, dkg);                       // receptor de baixo
                    Bx(2.9f, 5.6f, 0.3f, 0.85f, 0.42f, dkg);                       // receptor de cima
                    Bx(3.0f, 5.4f, 0.85f, 1.05f, 0.3f, blk);                       // trilho
                    Cy(3.6f, -0.6f, 3.1f, -2.0f, 0.4f, blk);                       // punho
                    Bt(4.6f, -1.8f, -1.42f, 1.25f, 0.55f, 0.36f, blk);             // carregador reto
                    if (d.id == "m4")
                    {
                        Bx(5.6f, 8.4f, -0.5f, 0.75f, 0.5f, dkg);                   // guarda-mão com trilhos
                        Bx(5.6f, 8.4f, 0.75f, 0.9f, 0.25f, blk);
                        Bx(8.0f, 8.3f, 0.75f, 1.6f, 0.15f, blk);                   // massa de mira
                        Cy(8.4f, 0.15f, 10.0f, 0.15f, 0.22f, blk);                 // cano
                        Cy(9.9f, 0.15f, 10.4f, 0.15f, 0.32f, blk);                 // quebra-chamas
                    }
                    else
                    {
                        Cy(5.6f, 0.15f, 8.0f, 0.15f, 0.6f, dkg);                   // guarda-mão fino
                        Cy(8.0f, 0.15f, 12.0f, 0.15f, 0.5f, met);                  // silenciador comprido
                        Cy(11.75f, 0.15f, 12.0f, 0.15f, 0.55f, blk);
                    }
                    break;
                case "awp":
                    Bx(0f, 3.2f, -1.3f, 0.55f, 0.55f, grn);                        // coronha grossa
                    Bx(0.6f, 2.6f, 0.55f, 0.9f, 0.45f, grn);                       // apoio de rosto
                    Cy(3.6f, -0.4f, 3.2f, -1.9f, 0.45f, grn);                      // punho
                    Bx(3.2f, 8.0f, -0.7f, 0.55f, 0.55f, grn);                      // corpo
                    Bx(4.6f, 5.6f, -1.6f, -0.7f, 0.4f, blk);                       // carregador
                    Cy(4.0f, 0.4f, 4.0f, 0.1f, 0.2f, blk, -0.6f); P.Add(new Ell(G(4.0f, -1.4f, 0.1f), new V3(0.4f, 0.4f, 0.4f), blk, part));   // ferrolho
                    Cy(3.4f, 1.7f, 8.0f, 1.7f, 0.62f, blk);                        // luneta
                    Cy(7.2f, 1.7f, 8.0f, 1.7f, 0.85f, blk); Cy(3.2f, 1.7f, 3.8f, 1.7f, 0.75f, blk);
                    Bx(4.6f, 4.9f, 0.55f, 1.3f, 0.25f, blk); Bx(6.4f, 6.7f, 0.55f, 1.3f, 0.25f, blk);
                    P.Add(new Ell(G(8.05f, 0, 1.7f), new V3(0.15f, 0.6f, 0.6f), M.lens, part));   // lente da luneta
                    Cy(8.0f, 0.1f, 15.0f, 0.1f, 0.32f, blk);                       // cano longo
                    Cy(14.3f, 0.1f, 15.0f, 0.1f, 0.45f, blk);                      // freio de boca
                    break;
                case "mp9":
                    Cy(0f, 0.2f, 1.6f, 0.2f, 0.2f, blk); Cy(0f, 0.2f, 0f, -1f, 0.2f, blk); Cy(0f, -1f, 1.4f, -0.6f, 0.18f, blk);   // coronha dobrável
                    Bx(1.4f, 5.6f, -0.5f, 0.7f, 0.5f, blk);                        // corpo de polímero
                    Bx(1.6f, 5.4f, 0.7f, 0.85f, 0.3f, dkg);                        // trilho
                    Bt(2.8f, -2.0f, -1.6f, 1.5f, 0.5f, 0.4f, blk);                 // punho com carregador
                    Cy(4.7f, -0.5f, 4.7f, -1.8f, 0.35f, blk);                      // empunhadura vertical
                    Cy(5.6f, 0.2f, 7.6f, 0.2f, 0.3f, blk);                         // cano
                    break;
                case "mac10":
                    Bx(0.8f, 4.6f, -0.6f, 0.8f, 0.55f, dkg);                       // caixa quadrada
                    Bt(2.3f, -2.1f, -1.62f, 1.6f, 0.5f, 0.4f, blk);                // punho com carregador comprido
                    Cy(4.6f, 0.4f, 5.8f, 0.4f, 0.25f, blk);                        // cano curto
                    Cy(0.8f, 0.6f, -0.6f, 0.6f, 0.15f, blk, 0.5f); Cy(0.8f, 0.6f, -0.6f, 0.6f, 0.15f, blk, -0.5f);   // coronha de arame
                    P.Add(new Cap(G(-0.6f, 0.5f, 0.6f), G(-0.6f, -0.5f, 0.6f), 0.15f, blk, part));
                    break;
                case "usp":
                    Bx(0f, 4.2f, 0f, 0.75f, 0.42f, blk);                           // ferrolho
                    Bx(0.3f, 3.8f, -0.45f, 0f, 0.38f, dkg);                        // armação
                    Bt(0.85f, -1.4f, -1.85f, 1.15f, 0.5f, 0.4f, blk);              // empunhadura
                    Cy(4.2f, 0.35f, 7.4f, 0.35f, 0.48f, met);                      // silenciador
                    break;
                case "deagle":
                    Bx(0f, 5.0f, 0f, 0.95f, 0.5f, met);                            // ferrolho grande e cinza
                    Bx(0.3f, 4.6f, -0.5f, 0f, 0.45f, met);                         // armação
                    Bt(0.9f, -1.6f, -1.9f, 1.25f, 0.6f, 0.45f, blk);               // empunhadura
                    break;
                case "p250":
                case "glock":
                    Bx(0f, 4.0f, 0f, 0.8f, 0.42f, met);                            // ferrolho cinza
                    Bx(0.3f, 3.8f, -0.5f, 0f, 0.4f, blk);                          // armação
                    Bt(0.9f, -1.5f, -1.75f, 1.2f, 0.55f, 0.42f, blk);              // empunhadura
                    break;
            }
        }

        /// <summary>Granada na mão direita (smoke, flash, molotov, he).</summary>
        public static void Nade(string kind, V3 hR, List<Prim> P, TeamMats M, int part = 17)
        {
            const float K = 1.5f;   // granada exagerada pra ler no tamanho do boneco
            V3 z(float k) => hR + new V3(0, 0, k * K);
            switch (kind)
            {
                case "flash":
                    P.Add(new Cap(z(-0.9f), z(2.0f), 1.275f, 33, part));
                    P.Add(new Cap(z(0.5f), z(0.9f), 1.38f, 54, part));
                    P.Add(new Cap(z(2.0f), z(2.5f), 0.75f, 62, part));
                    break;
                case "molotov":
                    P.Add(new Cap(z(-0.7f), z(1.2f), 1.575f, 26, part));       // garrafa
                    P.Add(new Cap(z(1.2f), z(2.6f), 0.63f, 26, part));        // gargalo
                    P.Add(new Ell(z(2.9f), new V3(0.9f, 0.9f, 0.8f), 30, part));   // pano aceso
                    break;
                case "he":
                    P.Add(new Ell(z(0.4f), new V3(1.8f, 1.8f, 1.95f), M.ct ? 53 : 42, part));
                    P.Add(new Cap(z(1.6f), z(2.1f), 0.525f, 62, part));
                    break;
                default:   // smoke
                    P.Add(new Cap(z(-0.9f), z(2.1f), 1.575f, 62, part));
                    P.Add(new Cap(z(0.7f), z(1.1f), 1.68f, 33, part));
                    P.Add(new Cap(z(2.1f), z(2.6f), 0.75f, 54, part));
                    break;
            }
        }
    }
}
