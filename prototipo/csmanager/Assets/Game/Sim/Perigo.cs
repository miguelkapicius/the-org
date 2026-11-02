using System;
using CSM.Map;
using static CSM.Sim.Places;

namespace CSM.Sim
{
    /// <summary>
    /// Onde é perigoso andar à toa, pra cada time: o que dá pra ver das posições onde o inimigo costuma segurar (o AWP
    /// do janelão vê o meio inteiro, o âncora vê a entrada do bomb...). Vira custo extra no caminho (camada da navmesh):
    /// o bot prefere dar a volta a cruzar na mira — não cruza o meio aberto pra ir pra curta —, mas se não tem outro
    /// jeito ele passa. Calculado uma vez (as posições são fixas).
    /// </summary>
    public static class Perigo
    {
        public const int CAMADA_T = 1, CAMADA_CT = 2;
        public const float CUSTO = 2.5f;          // por célula de 2 unidades vista (andar custa ~2): até ~2x mais longo
        static readonly object trava = new object(); static bool pronto;

        public static int Camada(Team t) => t == Team.T ? CAMADA_T : CAMADA_CT;

        public static void Build()
        {
            lock (trava)
            {
                if (pronto) return;
                Intel.Build();
                Pixels.Build();
                NavGrid.SetLayer(CAMADA_T, Custo(new[] { CT_A_Plataforma, CT_A_Caixa, CT_A_Canto, CT_Janelao, CT_Praca, CT_B_Banca, CT_B_Caixa, CT_B_Pilar, CT_B_Viela, CT_A_RuaCT }, CT_Janelao, out vistoT));
                NavGrid.SetLayer(CAMADA_CT, Custo(new[] { T_TopoMeio, T_MeioSul, T_RampaBaixo, T_RampaDobra, T_PalacioSala, T_CasaraoDobra, T_CurtaCima, T_Conector }, null, out vistoCT));
                pronto = true;
            }
        }

        static float[] vistoT, vistoCT;
        /// <summary>Esse ponto fica na mira de onde o inimigo costuma segurar? (0 = não; 1 = rifle; 2 = linha da AWP)</summary>
        public static float Visto(Team meu, V2 p) => (meu == Team.T ? vistoT : vistoCT)[Intel.Idx(p)];

        static float[] Custo(Hold[] holds, Hold awp, out float[] visto)
        {
            var d = new float[Intel.GX * Intel.GY]; visto = d;
            foreach (var h in holds)
                foreach (var l in h.look)
                {
                    float w = h == awp ? 2f : 1f, range = h == awp ? 1000 : 520;
                    V2 p = NavGrid.Snap(h.pos);
                    Intel.Viewshed(p, NavGrid.ZAt(p) + 18, (l - p).Atan2, 25f * MathF.PI / 180, range, null, i => d[i] = MathF.Max(d[i], w));
                }
            var c = new float[NavGrid.CellCount];
            for (int i = 0; i < c.Length; i++) { var (x, y) = NavGrid.CellAt(i); c[i] = CUSTO * d[Intel.Idx(NavGrid.Center(x, y))]; }
            return c;
        }
    }
}
