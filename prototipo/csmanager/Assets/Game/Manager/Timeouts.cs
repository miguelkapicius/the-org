using System;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    /// <summary>
    /// Timeout do manager na partida assistida (decisão 0018): 3 por partida; o time tem uma CHANCE de absorver a conversa e
    /// passar a jogar no estilo escolhido, com foco (mais sangue-frio) por alguns rounds. Números PROVISÓRIOS.
    /// </summary>
    public static class Timeouts
    {
        public const float BASE = 0.50f;
        public const float SANGUE_FRIO = 0.15f;       // de -15 % (sangue-frio 0) a +15 % (100), pela média dos 5 em campo
        public const float POR_NIVEL_GH = 0.05f;      // por nível da Gaming House acima do 1
        public const float POR_DERROTA = 0.04f, MAX_DERROTAS = 0.12f;   // rounds perdidos em sequência: o time ouve mais
        public const int EMBALO_VANTAGEM = 4; public const float EMBALO = 0.10f;   // vencendo por 4+: não quer mudar
        public const float MIN = 0.20f, MAX = 0.90f;
        public const float FOCO = 0.10f; public const int ROUNDS_FOCO = 3;

        /// <summary>Cada parte da chance, para a tela mostrar antes de confirmar.</summary>
        public class Chance
        {
            public float sangueFrio, gamingHouse, momento, total;
            public int derrotasSeguidas, vantagem; public float sangueFrioMedio;
        }

        public static Chance Calcular(Partida pt, bool timeA, int nivelGH)
        {
            var t = timeA ? pt.A : pt.B; var outro = timeA ? pt.B : pt.A;
            float sf = (float)t.js.Average(j => new Jogador { skill = j.skill }.Valor((int)Atrib.SangueFrio));
            int derrotas = 0;
            for (int i = pt.historico.Count - 1; i >= 0 && pt.historico[i].timeAvenceu != timeA; i--) derrotas++;
            int vant = t.placar - outro.placar;
            var c = new Chance { sangueFrioMedio = sf, derrotasSeguidas = derrotas, vantagem = vant };
            c.sangueFrio = (sf - 50f) / 50f * SANGUE_FRIO;
            c.gamingHouse = Math.Max(0, nivelGH - 1) * POR_NIVEL_GH;
            c.momento = Math.Min(MAX_DERROTAS, derrotas * POR_DERROTA) - (vant >= EMBALO_VANTAGEM ? EMBALO : 0f);
            c.total = Math.Clamp(BASE + c.sangueFrio + c.gamingHouse + c.momento, MIN, MAX);
            return c;
        }

        /// <summary>
        /// Número de 0 a 1 a partir do momento da partida, bem espalhado (SplitMix64). Um new Random com sementes vizinhas dava
        /// primeiros sorteios parecidos: com chance de 70 % o timeout funcionava em metade das vezes.
        /// </summary>
        public static double Sorteio(params int[] partes)
        {
            ulong z = 0x9E3779B97F4A7C15UL;
            foreach (int p in partes)
            {
                z += (ulong)(uint)p + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; z ^= z >> 31;
            }
            return (z >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>O estilo em que o time joga agora (pelos pesos da partida).</summary>
        public static Estilo EstiloAtual(Partida pt, bool timeA)
        {
            var p = (timeA ? pt.A : pt.B).pesos;
            return p == null ? Estilo.Equilibrada : (Estilo)(int)p.categoria;
        }

        /// <summary>Pede o timeout: sorteia pela chance e aplica na partida. Devolve null se não havia timeout para pedir.</summary>
        public static Partida.PedidoTimeout Pedir(Partida pt, bool timeA, Estilo estilo, int nivelGH)
        {
            var t = timeA ? pt.A : pt.B;
            if (pt.acabou || t.timeouts <= 0) return null;
            var c = Calcular(pt, timeA, nivelGH);
            // sorteio próprio (não mexe no sorteio da partida, que decide compras e jogadas)
            int abates = 0; foreach (var j in t.js) abates += j.k;
            bool ok = Sorteio(pt.r, pt.A.placar, pt.B.placar, t.timeouts, (int)estilo, abates) < c.total;
            return pt.PedirTimeout(t, Estilos.Pesos(estilo), Estilos.Nome(estilo), ok, c.total, ROUNDS_FOCO, FOCO) ? pt.timeoutsPedidos[pt.timeoutsPedidos.Count - 1] : null;
        }
    }
}
