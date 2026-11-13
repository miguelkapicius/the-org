using System;
using System.IO;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Onde cada time COSTUMA estar em cada momento do round (não onde PODE estar): quantos jogadores, em média, numa
    /// célula da grade do Intel, a cada faixa de 5 s. Aprendido rodando muitos rounds (MatchCli.Rotas) — e no futuro
    /// das demos de pro, no mesmo formato. Serve pra mirar no provável: o conector no começo do pistol é possível, mas
    /// ninguém passa ali.
    /// </summary>
    public static class Rotas
    {
        public const float FAIXA = 5f; public const int NF = 32;   // 0..160 s (round + bomb)
        public static readonly string ArqBot = MapaEscolhido.Arq("Assets/Game/Sim/rotas.bin");   // por mapa (Vila no lugar de sempre) — aprendido de bot × bot
        /// <summary>Tirado dos PROS (Headless -rotasPro): o de bot × bot reforçava os vícios dos bots. -exp rotasProLigado usa este.</summary>
        public static readonly string ArqPro = MapaEscolhido.Arq("Assets/Game/Sim/rotas_pro.bin");
        public static string ARQ => !TeamBrain.EXPF.Contains("rotasProVelho") && File.Exists(ArqPro) ? ArqPro : ArqBot;   /* padrão 2026-11-08: fatorial pz no critério; -humano "segurando onde não pode ter ninguém" −53% */
        static float[][][] dens;   // [time][faixa][célula]
        static readonly object trava = new object();

        static bool Carrega()
        {
            if (dens != null) return true;
            lock (trava)
            {
                if (dens != null) return true;
                if (!File.Exists(ARQ)) return false;
                using var br = new BinaryReader(File.OpenRead(ARQ));
                int n = br.ReadInt32(), nf = br.ReadInt32();
                var d = new float[2][][];
                for (int tm = 0; tm < 2; tm++) { d[tm] = new float[nf][]; for (int f = 0; f < nf; f++) { d[tm][f] = new float[n]; for (int i = 0; i < n; i++) d[tm][f][i] = br.ReadSingle(); } }
                dens = d; return true;
            }
        }

        /// <summary>Quantos do time `tm` costumam estar a até `raio` de p no instante t (0 se ainda não tem dados).</summary>
        public static float Perto(Team tm, V2 p, float t, float raio)
        {
            if (!Carrega()) return 0;
            int f = Math.Clamp((int)(t / FAIXA), 0, dens[(int)tm].Length - 1);
            var d = dens[(int)tm][f]; float s = 0; int r = (int)(raio / Intel.C) + 1;
            int cx = (int)(p.x / Intel.C), cy = (int)(p.y / Intel.C);
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= Intel.GX || y >= Intel.GY) continue;
                    float dx = (x - cx) * Intel.C, dy = (y - cy) * Intel.C;
                    if (dx * dx + dy * dy > raio * raio) continue;
                    s += d[x * Intel.GY + y];
                }
            return s;
        }

        /// <summary>Monta a tabela a partir de rounds simulados (posição de cada vivo a cada 0,5 s).</summary>
        public static void Grava(float[][][] soma, int[] amostrasPorFaixa)
        {
            int n = Intel.GX * Intel.GY;
            using var bw = new BinaryWriter(File.Create(ARQ));
            bw.Write(n); bw.Write(NF);
            for (int tm = 0; tm < 2; tm++)
                for (int f = 0; f < NF; f++)
                    for (int i = 0; i < n; i++) bw.Write(amostrasPorFaixa[f] > 0 ? soma[tm][f][i] / amostrasPorFaixa[f] : 0f);
            dens = null;
        }
    }
}
