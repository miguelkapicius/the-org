using System;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO, marco 3 (decisão individual por avaliação; flag decisao3Ligado) — a primeira decisão refeita: ABRIR UM LUGAR ONDE
    /// EU SEI QUE TEM INIMIGO (PLANO_SIMULACAO.md §4.1, o exemplo da quina). O cérebro velho sorteava entre micros com pesos, e por
    /// isso saía "vou assim mesmo" com a flash no bolso e o colega do lado. Aqui o jogador lista o que pode fazer, estima a chance de
    /// sair vivo com o abate em cada opção e faz a melhor; a escolha vale até a situação mudar.
    ///
    /// De onde vêm os números: da bancada de duelos do próprio jogo (Logs/bots/tabela_duelo_teste300_dados.txt, 1.761 duelos): quem já
    /// está com a mira no outro ganha +1,21 na escala logística; ter colega do lado +0,50; estar cego −3,17 (poucas amostras); chegar
    /// andando −0,34. Rifle contra rifle parte de 50%. O que a bancada não mede (a flash pegar, o valor da troca) está nas constantes
    /// abaixo, com o motivo de cada uma.
    /// </summary>
    public static class Abertura
    {
        public static bool Ligado => TeamBrain.EXPF.Contains("decisao3Ligado");
        public enum Op { Seco = 0, Bang = 1, PedeBang = 2, EsperaColega = 3 }
        public static readonly string[] NOME = { "abro seco", "bango e entro no estouro", "peço a flash do colega e entro no estouro", "espero o colega e abrimos juntos" };

        public struct Situacao
        {
            public float crenca;          // 0..1: quanto eu acredito que ele está ali (e, estando, que está com a mira na quina)
            public bool flashAlcanca;     // tenho flash e ela chega na cara dele daqui
            public bool colegaComFlash;   // tem colega perto, livre, com flash
            public bool colegaPerto;      // tem colega a poucos passos (pode abrir comigo ou me trocar)
            public bool colegaColado;     // o colega já está do meu lado (abrir junto não custa espera)
            public float tempo;           // segundos que eu posso gastar antes de abrir (relógio do round/da bomba)
            public float arrisca;         // 0..1: jeito fixo do jogador (0 = paciente, 1 = vai)
        }

        const float K_MIRA = 1.208f, K_COLEGA = 0.496f, K_CEGO = 3.170f, K_CHEGANDO = 0.336f;
        const float P_FLASH_MINHA = 0.60f;   // a minha flash cega quem está na quina: nem sempre (ele vira, a flash erra, ele não está lá)
        const float P_FLASH_COLEGA = 0.55f;  // a do colega chega um pouco pior (ele joga de outro ângulo, e pode não atender)
        const float VALE_TROCA = 0.30f;      // se eu caio com colega do lado, a troca devolve parte do round
        const float T_BANG = 2.6f, T_PEDE = 2.9f, T_ESPERA = 3.0f;   // saque + pavio; o colega sacar e jogar; o colega encostar
        const float CUSTO_SEG = 0.015f;      // cada segundo parado custa um pouco (o adversário também se mexe)
        const float CUSTO_FLASH = 0.03f;     // gastar a granada custa pouco perto de morrer na quina

        static float P(float logit) => 1f / (1f + MathF.Exp(-logit));

        /// <summary>Devolve a melhor opção e o valor de cada uma (0 = nem considerar). Sem sorteio.</summary>
        public static Op Escolhe(in Situacao s, float[] v)
        {
            // ele me espera com a mira na quina na medida da crença; eu chego andando
            float seco = -K_MIRA * s.crenca - K_CHEGANDO;
            float junto = seco + K_COLEGA;
            float pSeco = P(s.colegaColado ? junto : seco) + (s.colegaColado ? VALE_TROCA * (1 - P(junto)) : 0);
            v[(int)Op.Seco] = pSeco + 0.06f * (s.arrisca - 0.5f);
            // flash: com chance P_FLASH ele está cego (o duelo vira), senão é o duelo seco de antes
            float cego = seco + K_CEGO + (s.colegaColado ? K_COLEGA : 0);
            v[(int)Op.Bang] = s.flashAlcanca && s.tempo >= T_BANG
                ? P_FLASH_MINHA * P(cego) + (1 - P_FLASH_MINHA) * pSeco - CUSTO_SEG * T_BANG - CUSTO_FLASH : 0;
            v[(int)Op.PedeBang] = s.colegaComFlash && s.tempo >= T_PEDE
                ? P_FLASH_COLEGA * P(cego) + (1 - P_FLASH_COLEGA) * pSeco - CUSTO_SEG * T_PEDE - CUSTO_FLASH   /* quem banga de longe não está do meu lado no duelo: sem bônus de colega */
                  + (s.flashAlcanca ? 0.02f : 0)   /* guardo a minha pra depois */
                : 0;
            v[(int)Op.EsperaColega] = s.colegaPerto && !s.colegaColado && s.tempo >= T_ESPERA
                ? P(junto) + VALE_TROCA * (1 - P(junto)) - CUSTO_SEG * T_ESPERA - 0.06f * (s.arrisca - 0.5f) : 0;
            int m = 0; for (int i = 1; i < 4; i++) if (v[i] > v[m]) m = i;
            return (Op)m;
        }
    }
}
