using System;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO, marco 3 (flag duelo3Ligado) — O DUELO QUE NÃO ESTÁ RENDENDO. O Gabriel (12/11, depois de jogar o CT_NOVO3):
    /// "ficam trocando tiro de longe, de SMG, pinam, acaba o tempo; parece que todo mundo morre de medo: abre, toma um tiro, volta,
    /// abre, toma um tiro, volta, morreu; um pro ganha espaço, chega mais perto, mata; e o CT tem a prioridade de defusar".
    /// Na partida dele (round -170175016) um CT de USP ficou 18 s "em combate" a 550 do alvo sem dar um passo, com a ordem de desarmar;
    /// no round anterior cinco CT de MP9 ficaram 12 s a 200 do C4 sem entrar.
    ///
    /// O cérebro velho, com alvo na mira, só sabia continuar atirando, voltar pra cobertura ou (de 2,5 em 2,5 s, por sorteio) dar um
    /// passo. Aqui, a cada meio segundo de duelo, o jogador compara três coisas e faz a de maior valor (com um pouco de apego ao que já
    /// estava fazendo, pra não trocar de ideia à toa):
    ///   FICAR   — vale a chance de ganhar o duelo DAQUI, que depende da minha arma e da dele nesta distância; cai com o tempo de duelo
    ///             sem tirar vida e com o relógio do objetivo correndo.
    ///   AVANÇAR — vale a chance de ganhar de MAIS PERTO; sobe com colegas do lado (troca), vantagem de vivos e relógio; cai contra AWP
    ///             de longe, com pouca vida e sozinho na mira dele.
    ///   LARGAR  — sair da linha e seguir a ordem; vale quando nem daqui nem de perto a arma resolve, ou com pouca vida.
    /// </summary>
    public static class Duelo3
    {
        public static bool Ligado => TeamBrain.EXPF.Contains("duelo3Ligado");
        public const int FICAR = 0, AVANCAR = 1, LARGAR = 2;   // os mesmos números do duelModo do Brain
        public static readonly string[] NOME = { "brigo daqui", "ganho espaço: chego mais perto", "largo o duelo e sigo a ordem" };

        public struct Situacao
        {
            public string arma, armaDele; public float dist;
            public float tempo;            // segundos de duelo COM ELE NA MIRA desde a última vez que tirei vida dele (fora de vista não conta)
            public bool progresso;         // já tirei vida dele neste duelo
            public int minhaVida, colegas; // colegas a poucos passos
            public int vantagem;           // vivos nossos − vivos deles
            public bool relogio;           // o objetivo tem relógio correndo (bomba plantada pro CT; fim de round pro TR sem plant)
            public bool seguro;            // segurar este lugar É o meu trabalho agora (CT no setup, antes do plant)
            public bool eleMeVe; public int voltas; public float arrisca; public int atual;
        }

        /// <summary>Quanto a arma rende nesta distância (0..1). Vem do jeito que o jogo trata cada arma: pistola e SMG perdem dano e
        /// precisão com a distância, rifle segura até longe, AWP é pior de perto.</summary>
        public static float Rende(string arma, float d)
        {
            switch (TabelaDuelo.Classe(arma))
            {
                case 0: return d < 150 ? 0.55f : 0.95f;                                                     // AWP
                case 1: return d < 450 ? 1f : MathF.Max(0.55f, 1f - (d - 450f) / 900f);                    // rifle
                case 2: return d < 200 ? 1f : d < 420 ? 1f - 0.7f * (d - 200f) / 220f : 0.2f;              // SMG
                default: return d < 160 ? 0.9f : d < 380 ? 0.9f - 0.65f * (d - 160f) / 220f : 0.15f;       // pistola
            }
        }
        static float Chance(string a, string b, float d) { float x = Rende(a, d), y = Rende(b, d); return x / (x + y); }

        public static int Escolhe(in Situacao s, float[] v)
        {
            float aqui = Chance(s.arma, s.armaDele, s.dist), minha = Rende(s.arma, s.dist);
            float dPerto = MathF.Max(90f, s.dist - 170f), perto = Chance(s.arma, s.armaDele, dPerto);
            bool awpLonge = TabelaDuelo.Classe(s.armaDele) == 0 && s.dist > 200;
            v[FICAR] = aqui + (s.progresso ? 0.12f : 0) - MathF.Min(0.45f, 0.07f * MathF.Max(0, s.tempo - 2.5f))
                     - (s.relogio ? 0.25f : 0) + (s.seguro ? 0.2f : 0) - (minha < 0.35f ? 0.15f : 0);
            v[AVANCAR] = s.dist <= 140 || TabelaDuelo.Classe(s.arma) == 0 ? -1f   /* AWP não avança no meio do duelo: andar tira a luneta */
                     : perto + 0.08f * Math.Min(s.colegas, 3) + 0.06f * Math.Clamp(s.vantagem, -2, 3) + (s.relogio ? 0.25f : 0)
                       - (awpLonge ? 0.25f : 0) - (s.minhaVida < 40 ? 0.2f : 0) - (s.eleMeVe && s.colegas == 0 ? 0.08f : 0)
                       + 0.08f * (s.arrisca - 0.5f) - (s.seguro ? 0.2f : 0);
            v[LARGAR] = 0.30f + (s.minhaVida < 40 ? 0.15f : 0) + (minha < 0.35f && perto < 0.4f ? 0.2f : 0) - (s.relogio ? 0.2f : 0)
                     + (s.voltas >= 2 ? 0.1f : 0) - (s.progresso ? 0.15f : 0) - (s.tempo < 1.2f ? 0.3f : 0)
                     - (s.dist < 180 && s.eleMeVe ? 0.4f : 0);   /* com ele na minha cara e me olhando, virar as costas é morrer */
            if (s.atual >= 0 && s.atual < 3) v[s.atual] += 0.08f;   // compromisso: só troca se a outra opção for claramente melhor
            int m = 0; for (int i = 1; i < 3; i++) if (v[i] > v[m]) m = i;
            return m;
        }
    }
}
