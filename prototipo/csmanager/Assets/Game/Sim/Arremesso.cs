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
        /* skybox: acima disso a granada bate e volta. bx: o 165 (≈11 m) estava baixo — 24% das smokes/molotovs/HEs batiam nele de
           PROPÓSITO (o plano usava o quique no céu). -exp bxTetoAltoLigado: 260 (≈18 m, a altura da skybox de um mapa de CS) */
        static float? teto;
        public static float TETO => teto ??= TeamBrain.EXPF.Contains("bxTetoAltoLigado") || ClipNade ? 260f : 165f;
        /* bx: clip da granada nos prédios (MapData.ClipNade) + teto da skybox alto + o plano não usa o céu como tabela.
           -exp bxClipNadeLigado */
        static bool? clipNade;
        public static bool ClipNade => clipNade ??= !TeamBrain.EXPF.Contains("bxClipNadeVelho") && CSM.Map.MapData.MAPA == "vila";   /* o clip é dos lotes da Vila (a Brasa ainda não tem) */
        public static bool Velha => TeamBrain.EXPF.Contains("nadeVelha");

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
            float tt = 0, parado = -1; ultToque = 99; ultTetos = 0; simN = n;
            while (tt < 7)
            {
                float sp = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy + n.vz * n.vz);
                int k = Math.Max(1, (int)MathF.Ceiling(sp * Match.DT / 3f));
                for (int s = 0; s < k; s++) Match.PassoNade(n, Match.DT / k);
                ultTetos = n.tetos; ultParedes = n.paredes;
                tt += Match.DT;
                if (n.tocouChao && ultToque > 90) ultToque = tt;
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
        /// <summary>bx: quantas vezes a última Simula bateu no teto da skybox (validador). Por thread.</summary>
        [ThreadStatic] static int ultTetos, ultParedes; [ThreadStatic] static Match.Nade simN; public static int UltTetos => ultTetos;

        /// <summary>Quando a última Simula tocou o chão pela 1ª vez (99 = não tocou). Por thread.</summary>
        [ThreadStatic] static float ultToque;

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
            // HE no olho: a mão erra ~1-2°; vale a mira que estoura no chão perto dele mesmo errando um pouco (média de 3)
            if (kind == "he" && !TeamBrain.EXPF.Contains("heAlcanceVelho") && !TeamBrain.EXPF.Contains("heSemFolga"))
            {
                var p1 = pl; p1.up -= 1.5f; var p2 = pl; p2.up += 1.5f;
                return (Erro1(de, zChao, pl, kind, alvo, zAlvo) * 2 + Erro1(de, zChao, p1, kind, alvo, zAlvo) + Erro1(de, zChao, p2, kind, alvo, zAlvo)) / 4;
            }
            return Erro1(de, zChao, pl, kind, alvo, zAlvo);
        }
        static float Erro1(V2 de, float zChao, Plano pl, string kind, V2 alvo, float zAlvo)
        {
            var r = Simula(de, zChao, pl, kind);
            float e = (r.p - alvo).Len;
            if (ClipNade && ultTetos > 0) e += 45;   /* o pro não joga lineup no céu: quicar no teto só se não tem outro jeito */
            if (ClipNade && kind == "flash" && ultParedes > 0 && !TeamBrain.EXPF.Contains("bxPontoNade2")) e += 40;   /* flash planejada limpa (pop): a que quica no clip estoura torta, longe */
            if (kind == "flash") e += 0.5f * MathF.Abs(r.z - (zAlvo + 40));
            if (kind == "molotov" && r.ar) e += 200;   // estourou no ar: não pega fogo
            // molotov com folga: o erro da mão de quem joga no olho somava tempo e ela sumia no ar (12%)
            if (kind == "molotov" && !r.ar && r.t > Match.FUSE_MOLOTOV - 0.45f && !TeamBrain.EXPF.Contains("nadeFuseVelho")) e += 60 * (r.t - (Match.FUSE_MOLOTOV - 0.45f)) / 0.45f;
            // HE estoura no chão (quica e explode), não lá no alto: 81% explodiam a 30+ acima do chão, longe do corpo de todos
            if (kind == "he" && !TeamBrain.EXPF.Contains("nadeFuseVelho")) e += 1.2f * MathF.Max(0, r.z - (zAlvo + 6));
            // HE como pro: cai no chão com folga e quica/rola até o pé dele. Arco que só toca o chão no fim do pavio estoura no
            // ar com qualquer erro de mão (32% das HE no ar) — prefere o arremesso baixo que chega cedo
            if (kind == "he" && !TeamBrain.EXPF.Contains("heAlcanceVelho")) e += 50f * MathX.Clamp((MathF.Min(ultToque, Match.FUSE_HE) - (Match.FUSE_HE - 0.45f)) / 0.45f, 0, 1);
            if (kind == "smoke") e += 0.5f * MathF.Max(0, MathF.Abs(r.z - zAlvo) - 6);   // parou em cima de outra coisa
            return e;
        }

        static readonly ConcurrentDictionary<long, Plano> cacheG = new ConcurrentDictionary<long, Plano>();
        // a chave é arredondada (célula de onde + alvo de 6 em 6) mas o plano sai da posição EXATA de quem pediu primeiro:
        // dividido entre rounds paralelos, o round que chegava antes decidia o arremesso dos outros (headless não
        // determinístico). No modo NavGrid.PorRound é da thread e zera a cada round.
        [ThreadStatic] static ConcurrentDictionary<long, Plano> cacheT;
        static ConcurrentDictionary<long, Plano> cache => NavGrid.PorRound ? (cacheT ??= new ConcurrentDictionary<long, Plano>()) : cacheG;
        public static void NovoRound() { if (NavGrid.PorRound) cacheT?.Clear(); }
        public static void ZeraDinamico() { cacheG.Clear(); cacheT?.Clear(); }
        /// <summary>
        /// Acha o arremesso que cai mais perto do alvo, daqui: o jeito (até <paramref name="maxTipo"/>: quem não conhece
        /// a lineup não faz run jump throw), a força e a inclinação; a direção é pro alvo (com um ajuste pequeno, pra
        /// passar de quina). Preferência pelo jeito mais simples quando cai igual. Calculado uma vez por lugar/alvo.
        /// </summary>
        /// <summary>Essa pergunta já está respondida (Resolve sai de graça)? Mesma chave do Resolve.</summary>
        public static bool Pronto(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo = Tipo.CorrePulo)
        {
            long k = ((long)Intel.Idx(de) << 30) | ((long)Math.Clamp((int)(alvo.x / 6), 0, 8191) << 17) | ((long)Math.Clamp((int)(alvo.y / 6), 0, 8191) << 4)
                     | ((long)(kind == "smoke" ? 0 : kind == "molotov" ? 1 : kind == "flash" ? 2 : 3) << 2) | (long)((int)maxTipo & 3);
            if ((int)maxTipo > 3) k ^= 1L << 63;
            if (!CanonVelho) return canon.ContainsKey(k ^ ((long)Math.Clamp((int)MathF.Round(zChao / 4f), 0, 255) << 43));
            return cache.ContainsKey(k);
        }
        public static Plano Resolve(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo = Tipo.CorrePulo)
        {
            long k = ((long)Intel.Idx(de) << 30) | ((long)Math.Clamp((int)(alvo.x / 6), 0, 8191) << 17) | ((long)Math.Clamp((int)(alvo.y / 6), 0, 8191) << 4)
                     | ((long)(kind == "smoke" ? 0 : kind == "molotov" ? 1 : kind == "flash" ? 2 : 3) << 2) | (long)((int)maxTipo & 3);
            if ((int)maxTipo > 3) k ^= 1L << 63;
            if (!CanonVelho)
            {   // canônico: a conta sai do CENTRO da célula (12×12), da altura arredondada e do centro do alvo (6×6) — função só
                // da chave, então vale pra qualquer round/thread/processo e fica guardada de vez (era refeita ~600 trajetórias
                // a cada célula nova de cada bot, a cada round: ~22% do tempo do headless)
                int zq = Math.Clamp((int)MathF.Round(zChao / 4f), 0, 255);
                long kc = k ^ ((long)zq << 43);
                if (canon.TryGetValue(kc, out var pc)) return pc;
                int ix = Intel.Idx(de); V2 dc = new V2((ix / Intel.GY) * Intel.C + Intel.C * 0.5f, (ix % Intel.GY) * Intel.C + Intel.C * 0.5f);
                V2 ac = new V2(Math.Clamp((int)(alvo.x / 6), 0, 8191) * 6 + 3, Math.Clamp((int)(alvo.y / 6), 0, 8191) * 6 + 3);
                var r = Busca(dc, zq * 4f, ac, kind, maxTipo);
                if (canon.Count > 3_000_000) canon.Clear();
                return canon.GetOrAdd(kc, r);
            }
            return cache.GetOrAdd(k, _ => Busca(de, zChao, alvo, kind, maxTipo));
        }
        /// <summary>O arremesso de VERDADE (quem vai soltar a granada agora): conta exata da posição dele, sem cache.</summary>
        public static Plano ResolveExato(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo = Tipo.CorrePulo)
            => CanonVelho ? Resolve(de, zChao, alvo, kind, maxTipo) : Busca(de, zChao, alvo, kind, maxTipo);
        // REPROVOU no fatorial j3 (2026-11-06): CT ganha retake −4,2 (ruído ±4,0) e o ganho de velocidade foi pequeno (~14% da
        // Utilitaria). Fica opt-in (-exp arremessoCanonLigado); o padrão volta ao cache por round da posição exata
        /* bx: refino fino da mira do plano (o pro ajusta contínuo). -exp bxMiraFinaLigado — A/B 2026-11-08 (seed 70000, 200): o erro
           do plano quase não muda (molotov <10: 60,5→64%), lineup errado não cai (1,92→2,08), 1º abate vence −5. O grid não é a
           causa: fica DESLIGADA */
        public static bool MiraFina => TeamBrain.EXPF.Contains("bxMiraFinaLigado");
        public static bool CanonVelho => !TeamBrain.EXPF.Contains("arremessoCanonLigado");
        static readonly ConcurrentDictionary<long, Plano> canon = new ConcurrentDictionary<long, Plano>();

        /* perfil do solver (só mede): quantas buscas, quanto custam e quem pediu (-perf -quemPede) */
        public static long nBusca; public static double msBusca; public static System.Collections.Generic.Dictionary<string, (int n, double ms)> QuemBusca;
        static Plano Busca(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo)
        {
            if (!NavGrid.Medir) return BuscaX(de, zChao, alvo, kind, maxTipo);
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var r = BuscaX(de, zChao, alvo, kind, maxTipo);
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            System.Threading.Interlocked.Increment(ref nBusca);
            var qb = QuemBusca;
            if (qb != null) lock (qb)
            {
                msBusca += ms;
                var fr = new System.Diagnostics.StackTrace(1, false); string q = kind + ": ";
                for (int i = 2; i < Math.Min(6, fr.FrameCount); i++) { string nm = fr.GetFrame(i).GetMethod()?.Name; if (nm == null || nm.Contains("GetOrAdd") || nm.Contains("Resolve") || nm.StartsWith("<")) continue; q += nm + " < "; }
                qb.TryGetValue(q, out var v); qb[q] = (v.n + 1, v.ms + ms);
            }
            return r;
        }
        static Plano BuscaX(V2 de, float zChao, V2 alvo, string kind, Tipo maxTipo)
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
            if (MiraFina && best.erro > 3 && best.erro < 1e8f)
            {   /* bx: o pro ajusta a mira contínuo; o grid (direção de 1,5 em 1,5°) deixava o plano PERFEITO a 10-45 do alvo em 41%
                   das molotovs (auditoria "erro do plano"). Refino fino em volta do melhor: inclinação ±0,6° e direção ±1,2° */
                float bE = best.erro, bUp = best.up, bYaw = best.yaw;
                for (int pass = 0; pass < 2; pass++)
                {
                    float su = pass == 0 ? 0.2f : 0.07f, sy = (pass == 0 ? 0.3f : 0.1f) * MathF.PI / 180; float cu = bUp, cy = bYaw;
                    for (int i = -3; i <= 3; i++)
                        for (int j = -4; j <= 4; j++)
                        {
                            var pl = new Plano { tipo = best.tipo, forca = best.forca, yaw = cy + j * sy, up = cu + i * su };
                            float e = Erro(de, zChao, pl, kind, alvo, zAlvo); if (e < bE - 0.01f) { bE = e; bUp = pl.up; bYaw = pl.yaw; }
                        }
                }
                if (bE < best.erro)
                {
                    var r = Simula(de, zChao, new Plano { tipo = best.tipo, forca = best.forca, yaw = bYaw, up = bUp }, kind);
                    best = new Plano { tipo = best.tipo, forca = best.forca, yaw = bYaw, up = bUp, erro = bE, tempo = r.t };
                }
            }
            return best;
        }
    }
}
