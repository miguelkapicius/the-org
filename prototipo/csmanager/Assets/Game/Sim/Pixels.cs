using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Mapa de pixels: pra cada célula da grade do Intel, todas as quinas de onde um inimigo pode aparecer pra quem está
    /// ali. Quina = onde a visão pula de perto pra longe; quem está escondido atrás dela aparece exatamente nessa borda.
    /// Calculado uma vez, de todos os pontos do mapa: o bot não acha pixel por regra, ele já sabe todos.
    /// </summary>
    public static class Pixels
    {
        // MAX: até onde procura quina. Com 450 a janela/ângulo longo (janelão, meio, AWP) nunca virava quina — o raio
        // cortado em 450 dos dois lados da abertura não dava o salto — e o bot não tinha como mirar no ângulo longo
        public const int N = 48; public const float STEP = MathF.PI * 2 / N;
        public const float MAX = 450;   // testado 900: não mudou a mira (o ângulo longo do meio é posição, não quina — ver Brain.PosicaoDeles)
        /// <summary>
        /// ponto = pra onde mirar (colado na borda, do lado visível); dp = distância até a quina; lado = por onde abrir
        /// (+1/-1, ângulo pra se afastar da parede); atras = pontos escondidos atrás da quina (onde mora o perigo).
        /// </summary>
        public class Quina { public V2 ponto; public float ang, dp; public int lado; public V2[] atras; }
        static Quina[][] tab; static readonly object trava = new object();
        static readonly Quina[] VAZIO = new Quina[0];

        public static Quina[] Em(V2 p) => tab[Intel.Idx(p)] ?? VAZIO;
        public static int Total { get; private set; }

        public static void Build()
        {
            lock (trava)
            {
                if (tab != null) return;
                var t = new Quina[Intel.GX * Intel.GY][]; int total = 0;
                Parallel.For(0, t.Length, i =>
                {
                    if (!Intel.Pass(i)) return;
                    var l = DaCelula(Intel.CenterOf(i), Intel.Z(i));
                    t[i] = l; System.Threading.Interlocked.Add(ref total, l.Length);
                });
                Total = total; tab = t;
            }
        }

        /// <summary>
        /// A borda exata vista de onde estou: a tabela tem 48 raios (7,5°) a partir do centro da célula; aqui varre de grau em
        /// grau em volta da quina e devolve o primeiro ponto aberto colado na parede (pixel de verdade, não aproximado).
        /// </summary>
        public static V2 Exato(Quina q, V2 pos, float z)
        {
            V2 alvo = q.ponto - pos; if (alvo.Len < 1) return q.ponto;
            float a0 = alvo.Atan2, passo = MathF.PI / 180, ant = MapData.Raio(pos, z, V2.Ang(a0 - 9 * passo), MAX, 18f), melhor = float.MaxValue; V2 res = q.ponto;
            for (int i = -8; i <= 9; i++)
            {
                float a = a0 + i * passo, d = MapData.Raio(pos, z, V2.Ang(a), MAX, 18f);
                if (MathF.Abs(d - ant) >= 45 && MathF.Abs(i) < melhor)
                {   // do raio curto pro longo: mira no longo, encostado na borda
                    float perto = MathF.Min(d, ant), ang = d > ant ? a : a - passo;
                    melhor = MathF.Abs(i); res = pos + V2.Ang(ang) * MathF.Min(MathF.Max(d, ant), perto + 60);
                }
                ant = d;
            }
            return res;
        }

        static Quina[] DaCelula(V2 c, float z)
        {
            V2 cc = NavGrid.Movable(c) ? c : NavGrid.Snap(c);
            var d = new float[N]; var res = new List<Quina>();
            for (int k = 0; k < N; k++) d[k] = MapData.Raio(cc, z, V2.Ang(k * STEP), MAX, 18f);
            for (int k = 0; k < N; k++)
            {
                int k2 = (k + 1) % N; float a = d[k], b = d[k2];
                if (MathF.Abs(a - b) < 60) continue;
                bool pertoK = a < b; int perto = pertoK ? k : k2; float dp = MathF.Min(a, b);
                if (dp < 20) continue;   // parede colada em mim não é quina
                var atras = new List<V2>();
                foreach (float extra in new[] { 70f, 180f })
                    foreach (int kk in new[] { perto, pertoK ? (perto - 1 + N) % N : (perto + 1) % N })
                    {
                        V2 q = cc + V2.Ang(kk * STEP) * (dp + extra);
                        if (NavGrid.Movable(q)) atras.Add(q);
                    }
                if (atras.Count == 0) continue;
                float ang = (pertoK ? k + 0.7f : k + 0.3f) * STEP;
                res.Add(new Quina { ang = ang, dp = dp, lado = pertoK ? 1 : -1, atras = atras.ToArray(), ponto = cc + V2.Ang(ang) * MathF.Min(MathF.Max(a, b), dp + 60) });
            }
            return res.ToArray();
        }
    }
}
