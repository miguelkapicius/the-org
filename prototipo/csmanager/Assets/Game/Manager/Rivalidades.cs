using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Manager
{
    /// <summary>O retrospecto do clube contra um adversário (decisão 0021), tirado do histórico de partidas.</summary>
    public class Retrospecto
    {
        public string adversario;
        public int series, vitorias, derrotas, mapasV, mapasD;
        public float pontos;                 // pontos de rivalidade
        public int sequencia;                // séries seguidas, da mais recente: > 0 vitórias, < 0 derrotas
        public string ultimaEliminacao;      // "Semifinal da Copa X" se ele eliminou o clube na série mais recente entre os dois; senão null

        public bool rival;                   // entre os MAX_RIVAIS de mais pontos, com pelo menos PONTOS_RIVAL
        public bool Rival => rival;
        public string Texto => series == 0 ? "primeiro encontro" :
            vitorias + (vitorias == 1 ? " série vencida" : " séries vencidas") + " e " + derrotas + (derrotas == 1 ? " perdida" : " perdidas") + " (mapas " + mapasV + " x " + mapasD + ")";
    }

    /// <summary>
    /// Rivalidades (decisão 0021): pontos por série contra o mesmo time; rival = os 3 de mais pontos com 10 ou mais. Tudo sai do histórico de
    /// partidas (newest first), que já é salvo. Números PROVISÓRIOS.
    /// </summary>
    public static class Rivalidades
    {
        public const float PONTOS_RIVAL = 10f; public const int MAX_RIVAIS = 3;   // ligas e grupos repetem adversários: sem teto, metade virava rival
        public const float SERIE = 0.5f, MATA_MATA = 2f, FINAL = 4f, ELIMINADO = 3f, ATE_O_FIM = 1f, MAJOR = 2f;

        /// <summary>A fase da série sem o nome do campeonato ("Final", "Semifinal", "rodada 3"...).</summary>
        public static string Fase(Resultado r) => Fmt.Rotulo(r.campeonato, r.rotulo) ?? "";
        public static bool Final(Resultado r) => Fase(r) == "Final";
        public static bool MataMata(Resultado r)
        {
            string f = Fase(r);
            return f == "Final" || f.StartsWith("Semifinal") || f.StartsWith("Quartas") || f.StartsWith("Oitavas") || f.Contains("decisão");
        }
        public static bool Major(Resultado r) => r.campeonato != null && r.campeonato.Contains("Major");
        public static bool AteOFim(Resultado r) => r.serieMd > 1 && Math.Min(r.serieMeus, r.serieDeles) == r.serieMd / 2;
        public static bool Eliminado(Resultado r) => !r.venceuSerie && MataMata(r);

        /// <summary>Pontos de rivalidade de uma série (r = o último mapa dela).</summary>
        public static float PontosDaSerie(Resultado r)
        {
            float p = SERIE;
            if (Final(r)) p += FINAL; else if (MataMata(r)) p += MATA_MATA;
            if (Eliminado(r)) p += ELIMINADO;
            if (AteOFim(r)) p += ATE_O_FIM;
            if (Major(r)) p += MAJOR;
            return p;
        }

        /// <summary>Retrospecto contra cada adversário já enfrentado. <paramref name="historico"/>: mais recente primeiro.</summary>
        public static List<Retrospecto> Todos(IEnumerable<Resultado> historico)
        {
            var porAdv = new Dictionary<string, Retrospecto>();
            var seqFechada = new HashSet<string>();
            foreach (var r in historico)
            {
                if (string.IsNullOrEmpty(r.adversario) || r.adversario == "?") continue;
                if (!porAdv.TryGetValue(r.adversario, out var rt)) porAdv[r.adversario] = rt = new Retrospecto { adversario = r.adversario };
                if (r.venceu) rt.mapasV++; else rt.mapasD++;
                if (!r.serieEncerrada) continue;
                bool primeiraVista = rt.series == 0;   // a série mais recente entre os dois
                rt.series++; if (r.venceuSerie) rt.vitorias++; else rt.derrotas++;
                rt.pontos += PontosDaSerie(r);
                if (primeiraVista && Eliminado(r)) rt.ultimaEliminacao = Fase(r) + " da " + r.campeonato;
                if (!seqFechada.Contains(r.adversario))
                {
                    int sinal = r.venceuSerie ? 1 : -1;
                    if (rt.sequencia == 0 || Math.Sign(rt.sequencia) == sinal) rt.sequencia += sinal; else seqFechada.Add(r.adversario);
                }
            }
            var lista = porAdv.Values.OrderByDescending(x => x.pontos).ThenByDescending(x => x.series).ToList();
            for (int i = 0; i < lista.Count; i++) lista[i].rival = i < MAX_RIVAIS && lista[i].pontos >= PONTOS_RIVAL;
            return lista;
        }
    }
}
