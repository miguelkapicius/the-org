using System;

namespace CSM.Manager
{
    // ============================================================================================================
    // Fãs do clube (decisão 0037): uma moeda de progressão. Cada mapa, série e campeonato rende (ou tira) fãs conforme o momento
    // do time; os fãs liberam os níveis da Gaming House (junto com o dinheiro). Fãs NÃO são gastos: são a base conquistada.
    // Sem Unity. Todos os números são PROVISÓRIOS.
    // ============================================================================================================

    public static class Fas
    {
        public const int INICIAIS = 2000;
        public const int BASE_MAPA = 40;                          // fãs de um mapa ganho no Amador contra um rival do mesmo nível
        public static readonly float[] PESO_CIRCUITO = { 1f, 5f, 40f };   // Amador, Pro, Elite
        public const float BONUS_SERIE = 3f;                      // vencer a série vale 3 mapas ganhos
        public const float PERDA_MAPA = 0.5f;                     // perder tira metade do que ganhar renderia
        public const int SEQUENCIA_MAX = 5;                       // a sequência conta até 5; cada mapa dela soma 10%
        public const float POR_SEQUENCIA = 0.10f;
        public const float DIFERENCA = 20f;                       // 20 pontos de força de diferença = +/-100% no ganho
        public static readonly float[] BONUS_TITULO = { 40f, 15f, 6f };    // 1º, 2º e 3º lugar, em múltiplos do mapa ganho

        static float Peso(Circuito c) => PESO_CIRCUITO[(int)c];

        /// <summary>Fãs que um mapa muda: positivo na vitória, negativo na derrota. <paramref name="sequencia"/>: mapas seguidos antes deste.</summary>
        public static int Mapa(bool venceu, int forcaMinha, int forcaAdv, Circuito circ, int sequencia)
        {
            float dif = (forcaAdv - forcaMinha) / DIFERENCA;
            float base_ = BASE_MAPA * Peso(circ);
            if (venceu)
            {
                float adv = Math.Max(0.5f, Math.Min(2f, 1f + dif));
                float mom = 1f + POR_SEQUENCIA * Math.Min(Math.Max(sequencia, 0), SEQUENCIA_MAX);
                return (int)Math.Round(base_ * adv * mom);
            }
            float fraco = Math.Max(0.5f, Math.Min(2f, 1f - dif));   // perder para quem é mais fraco dói mais
            float ruim = 1f + POR_SEQUENCIA * Math.Min(Math.Max(-sequencia, 0), SEQUENCIA_MAX);
            return -(int)Math.Round(base_ * PERDA_MAPA * fraco * ruim);
        }

        public static int Serie(Circuito circ) => (int)Math.Round(BASE_MAPA * Peso(circ) * BONUS_SERIE);

        /// <summary>Fãs do campeonato pela colocação (1 = campeão); 0 fora do pódio.</summary>
        public static int Titulo(int posicao, Circuito circ) => posicao >= 1 && posicao <= 3 ? (int)Math.Round(BASE_MAPA * Peso(circ) * BONUS_TITULO[posicao - 1]) : 0;

        /// <summary>Quanto os fãs mexem no patrocínio (decisões 0038 e 0039): 1,0 com os fãs iniciais, +0,2 a cada vez que a base dobra, entre 0,8 e 3,0.</summary>
        public static float FatorPatrocinio(int fas) => Math.Max(0.8f, Math.Min(3.0f, 0.8f + 0.2f * (float)Math.Log(1.0 + Math.Max(0, fas) / 2000.0, 2.0)));

        /// <summary>Quanto a torcida pesa na atração de um jogador, de -1 (cerca de 600 fãs) a +1 (cerca de 640 mil), com zero em 20 mil.</summary>
        public static float ValorAtracao(int fas) => Math.Max(-1f, Math.Min(1f, (float)Math.Log(Math.Max(1, fas) / 20000.0, 2.0) / 5f));

        /// <summary>O tamanho da torcida em uma palavra.</summary>
        public static string Faixa(int fas) => fas < 8000 ? "Desconhecido" : fas < 60000 ? "Local" : fas < 400000 ? "Regional" : fas < 2000000 ? "Nacional" : "Mundial";

        /// <summary>Fãs mínimos para o nível da Gaming House.</summary>
        public static int ParaNivel(int nivel) => nivel <= 1 ? 0 : GamingHouse.NIVEIS[Math.Min(nivel, GamingHouse.MAX) - 1].fas;

        /// <summary>1234 → "1.234"; 12400 → "12,4 mil"; 1250000 → "1,25 mi". Cabe na barra do topo.</summary>
        public static string Texto(int v)
        {
            if (v >= 1000000) return (v / 1000000.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " mi";
            if (v >= 10000) return (v / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " mil";
            return Math.Abs((long)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        }
    }

    public partial class Carreira
    {
        /// <summary>Muda os fãs, sem passar de 0 para baixo.</summary>
        void MudarFas(int delta) { fas = Math.Max(0, fas + delta); }

        public int FasProximoNivelGH => Fas.ParaNivel(nivelGH + 1);
        public bool FasBastam => nivelGH >= GamingHouse.MAX || fas >= FasProximoNivelGH;
    }
}
