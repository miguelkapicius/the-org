using System;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO — a TABELA DE DUELO do avaliador (PLANO_SIMULACAO.md 4.1, "De onde vêm os números do avaliador").
    /// Chance de ganhar um duelo 1x1, medida no PRÓPRIO jogo: o que o bot espera tem que bater com o que o jogo faz.
    /// (placeholder: ainda sem medição; tudo 50%)
    /// </summary>
    public static class TabelaDuelo
    {
        public const int AWP = 0, RIFLE = 1, SMG = 2, PISTOLA = 3;

        public static readonly float[] FAIXAS = { 100f, 200f, 300f };
        static readonly float[] BASE = new float[16 * 4];
        const float K_CHEGANDO = 0f, K_CEGO = 0f, K_VIDA_MEDIA = 0f, K_VIDA_BAIXA = 0f, K_COLEGA = 0f, K_MIRA = 0f;

        public static int Classe(string arma)
        {
            if (arma == "awp") return AWP;
            if (arma == "pistola" || WeaponStats.Pistola(arma)) return PISTOLA;
            if (arma == "smg" || arma == "mp9" || arma == "mac10") return SMG;
            return RIFLE;
        }
        public static int FaixaDist(float dist) { int f = 0; while (f < FAIXAS.Length && dist >= FAIXAS[f]) f++; return f; }
        static int FaixaVida(int hp) => hp >= 70 ? 0 : hp >= 30 ? 1 : 2;

        public static float Chance(string minhaArma, string armaDele, float dist, bool euChegando, bool eleChegando, bool euCego, bool eleCego,
                                   int minhaVida, int vidaDele, bool eleMeOlhando, bool euComColega = false, bool eleComColega = false, bool euOlhandoEle = true)
        {
            float b = BASE[(Classe(minhaArma) * 4 + Classe(armaDele)) * (FAIXAS.Length + 1) + FaixaDist(dist)]; if (b <= 0f) b = 0.5f;
            float z = (float)Math.Log(b / (1f - b));
            z += K_CHEGANDO * ((euChegando ? 1 : 0) - (eleChegando ? 1 : 0));
            z += K_CEGO * ((euCego ? 1 : 0) - (eleCego ? 1 : 0));
            int vm = FaixaVida(minhaVida), vd = FaixaVida(vidaDele);
            z += K_VIDA_MEDIA * ((vm == 1 ? 1 : 0) - (vd == 1 ? 1 : 0)) + K_VIDA_BAIXA * ((vm == 2 ? 1 : 0) - (vd == 2 ? 1 : 0));
            z += K_COLEGA * ((euComColega ? 1 : 0) - (eleComColega ? 1 : 0));
            z += K_MIRA * ((euOlhandoEle ? 1 : 0) - (eleMeOlhando ? 1 : 0));
            float p = 1f / (1f + (float)Math.Exp(-z));
            return p < 0.03f ? 0.03f : p > 0.97f ? 0.97f : p;
        }
    }
}
