using System;
using System.Collections.Concurrent;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Arremesso de granada como no CS2: não existe "jogar com a força que precisa". A força é a do botão (clique
    /// esquerdo 675, os dois ~438, direito ~202), a velocidade do jogador entra com peso 1,25 (andando, correndo, pulando)
    /// e o que o jogador escolhe é a mira (direção e inclinação) e o jeito. Por isso existe lineup: um lugar, uma mira e
    /// um jeito que caem num pixel. Fonte: weapon_basecsgrenade (CS:GO) — velocidade 750×0,9, inclinação +10°×(90−|p|)/90,
    /// gravidade da granada 0,4×800 = 320. Escala do nosso mapa: horizontal ÷4 (correr 250 = 62,5), altura ÷2,9 (boneco
    /// 72 = 25). Em cima disso um teto invisível (a skybox), senão meia força cruzava o mapa por cima de tudo.
    /// </summary>
    public static class Arremesso
    {
        public enum Tipo { Parado, Andando, Correndo, Pulo, CorrePulo }
        public struct Plano
        {
            public Tipo tipo; public float forca, yaw, up;   // up em graus (positivo = pra cima), yaw em radianos
            public float erro, tempo;                        // onde cai x alvo; quanto leva até estourar
            public override string ToString() => $"{Nome(tipo)}{(forca >= 1 ? "" : forca >= 0.5f ? ", meia força" : ", fraca")}, mira {up:0.#}° pra cima";
        }
        public static string Nome(Tipo t) => t switch { Tipo.Parado => "parado", Tipo.Andando => "andando", Tipo.Correndo => "correndo", Tipo.Pulo => "jump throw", _ => "run jump throw" };

        public const float H = 0.25f, V = 1f / 2.9f;      // CS → nosso (horizontal, vertical)
        public const float GRAV = 320f * V;              // ~110
        public const float TETO = 165f;                  // skybox: acima disso a granada bate e volta
        public static bool Velha => TeamBrain.EXP.Contains("nadeVelha");

        /// <summary>Velocidade de saída (nossas unidades/s) — com o tipo, a força, a direção e a inclinação.</summary>
        public static (float vx, float vy, float vz) Velocidade(Tipo tipo, float forca, float yaw, float up, float ruidoMov = 1f, float ruidoPulo = 1f)
        {
            float p = up + 10f * (90f - MathF.Abs(up)) / 90f;   // o CS levanta a mira até 10°
            float pr = p * MathF.PI / 180f;
            float vel = 675f * (0.3f + 0.7f * MathX.Clamp(forca, 0, 1));
            float cx = MathF.Cos(pr) * MathF.Cos(yaw), cy = MathF.Cos(pr) * MathF.Sin(yaw), cz = MathF.Sin(pr);
            float mov = tipo switch { Tipo.Andando => 130f, Tipo.Correndo or Tipo.CorrePulo => 245f, _ => 0f } * ruidoMov;
            float pulo = tipo is Tipo.Pulo or Tipo.CorrePulo ? 301.99f * ruidoPulo : 0f;
            float vx = cx * vel + MathF.Cos(yaw) * mov * 1.25f, vy = cy * vel + MathF.Sin(yaw) * mov * 1.25f, vz = cz * vel + pulo * 1.25f;
            return (vx * H, vy * H, vz * V);
        }

        /// <summary>Onde a granada estoura (smoke: onde para; molotov: onde toca o chão; flash/HE: onde está no tempo).</summary>
        public static (V2 p, float z, float t, bool ar) Simula(V2 de, float zChao, Plano pl, string kind)
        {
            var n = new Match.Nade { l = new Places.Lineup("teste", kind, de, de) };
            Lanca(n, de, zChao, pl, 1f, 1f);
            float tt = 0, parado = -1;
            while (tt < 7)
            {
                float sp = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy + n.vz * n.vz);
                int k = Math.Max(1, (int)MathF.Ceiling(sp * Match.DT / 3f));
                for (int s = 0; s < k; s++) Match.PassoNade(n, Match.DT / k);
                tt += Match.DT;
                if (kind == "molotov" && n.tocouChao) return (n.P, n.z, tt, false);
                if (kind == "molotov" && tt >= Match.FUSE_MOLOTOV) return (n.P, n.z, tt, true);
                if (kind == "flash" && tt >= Match.FUSE_FLASH || kind == "he" && tt >= Match.FUSE_HE) return (n.P, n.z, tt, !n.noChao);
                if (kind == "smoke")
                {
                    float vh = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy);
                    if (n.noChao && vh < 4) { if (parado < 0) parado = tt; } else parado = -1;
                    if (parado >= 0 && tt - parado >= 0.15f && tt > 0.6f) return (n.P, n.z, tt, false);
                }
            }
            return (n.P, n.z, tt, !n.noChao);
        }

        /// <summary>Põe a granada na mão de quem joga: sai do olho, um pouco à frente.</summary>
        public static void Lanca(Match.Nade n, V2 de, float zChao, Plano pl, float ruidoMov, float ruidoPulo)
        {
            var (vx, vy, vz) = Velocidade(pl.tipo, pl.forca, pl.yaw, pl.up, ruidoMov, ruidoPulo);
            V2 f = V2.Ang(pl.yaw) * 5.5f;
            V2 sai = NavGrid.Movable(de + f) ? de + f : de;
            n.x = sai.x; n.y = sai.y; n.z = zChao + 22; n.vx = vx; n.vy = vy; n.vz = vz;
        }

        /// <summary>Erro do plano: distância de onde estoura até o alvo (flash: o alvo é no ar, uns 40 acima).</summary>
        static float Erro(V2 de, float zChao, Plano pl, string kind, V2 alvo, float zAlvo)
        {
            var r = Simula(de, zChao, pl, kind);
            float e = (r.p - alvo).Len;
            if (kind == "flash") e += 0.5f * MathF.Abs(r.z - (zAlvo + 40));
            if (kind == "molotov" && r.ar) e += 200;   // estourou no ar: não pega fogo
            if (kind == "smoke") e += 0.5f * MathF.Max(0, MathF.Abs(r.z - zAlvo) - 6);   // parou em cima de outra coisa
            return e;
        }

        static readonly ConcurrentDictionary<long, Plano> cache = new ConcurrentDictionary<long, Plano>();
        /// <summary>
        /// Acha o arremesso que cai mais perto do alvo, daqui: o jeito (até <paramref name="maxTipo"/>: quem não conhece
        /// a lineup não faz run jump throw), a força e a inclinação; a direção é pro alvo (com um ajuste pequeno, pra
        /// passar de quina). Preferência pelo jeito mais simples quando cai igual. Calculado uma vez por lugar/alvo.
        /// </summary>
        public static Plano Resolve(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo = Tipo.CorrePulo)
        {
            long k = ((long)Intel.Idx(de) << 30) | ((long)Math.Clamp((int)(alvo.x / 6), 0, 8191) << 17) | ((long)Math.Clamp((int)(alvo.y / 6), 0, 8191) << 4)
                     | ((long)(kind == "smoke" ? 0 : kind == "molotov" ? 1 : kind == "flash" ? 2 : 3) << 2) | (long)((int)maxTipo & 3);
            if ((int)maxTipo > 3) k ^= 1L << 63;
            return cache.GetOrAdd(k, _ => Busca(de, zChao, alvo, kind, maxTipo));
        }

        static Plano Busca(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo)
        {
            float zAlvo = MapData.Top((int)Math.Clamp(alvo.x, 0, MapData.NX - 1), (int)Math.Clamp(alvo.y, 0, MapData.NY - 1));
            float yaw0 = (alvo - de).Atan2;
            var best = new Plano { erro = float.MaxValue };
            float[] forcas = { 1f, 0.5f, 0f };
            for (int ti = 0; ti <= (int)maxTipo; ti++)
                foreach (float fo in forcas)
                {
                    // grosso: de 4 em 4 graus; fino: em volta do melhor, de 0,5 em 0,5 (e um ajuste de direção)
                    float bUp = 0, bE = float.MaxValue;
                    for (float up = -20; up <= 80; up += 4)
                    {
                        var pl = new Plano { tipo = (Tipo)ti, forca = fo, yaw = yaw0, up = up };
                        float e = Erro(de, zChao, pl, kind, alvo, zAlvo); if (e < bE) { bE = e; bUp = up; }
                    }
                    if (bE > best.erro + 60) continue;
                    float bYaw = yaw0;
                    for (float up = bUp - 3.5f; up <= bUp + 3.5f; up += 0.5f)
                        for (float dy = -3; dy <= 3; dy += 1.5f)
                        {
                            float yw = yaw0 + dy * MathF.PI / 180;
                            var pl = new Plano { tipo = (Tipo)ti, forca = fo, yaw = yw, up = up };
                            float e = Erro(de, zChao, pl, kind, alvo, zAlvo); if (e < bE - 0.01f) { bE = e; bUp = up; bYaw = yw; }
                        }
                    // o jeito mais simples ganha se cai quase igual (ninguém pula pra jogar o que dá parado)
                    if (bE < best.erro - (best.erro < float.MaxValue ? 4 : 0))
                    {
                        var r = Simula(de, zChao, new Plano { tipo = (Tipo)ti, forca = fo, yaw = bYaw, up = bUp }, kind);
                        best = new Plano { tipo = (Tipo)ti, forca = fo, yaw = bYaw, up = bUp, erro = bE, tempo = r.t };
                    }
                }
            return best;
        }
    }
}
