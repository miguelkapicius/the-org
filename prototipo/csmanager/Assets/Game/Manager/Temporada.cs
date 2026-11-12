using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Fim de temporada (decisão 0040, proposta): na virada do ano a carreira monta o resumo da temporada que acabou (circuito,
    // ranking, campanha, campeonatos, dinheiro, fãs, destaques e o elenco um ano mais velho) e a lista do que muda para a
    // temporada nova. A interface mostra isso numa sequência animada. Só classes simples, para o salvamento guardar.
    // ============================================================================================================

    [Serializable] public class CampanhaTemporada { public string nome; public int posicao, times, premio; }
    [Serializable] public class JogadorTemporada { public string nome; public int idade, overallAntes, overallDepois, mvps; public bool aposentou, titular; }

    [Serializable]
    public class ResumoTemporada
    {
        public int temporada, ano;
        public int circuitoAntes, circuitoDepois, ranking, times;
        public int mapas, vitorias, derrotas, series, seriesVencidas, melhorSequencia;
        public int dinheiroInicio, dinheiroFim, premios, fasInicio, fasFim;
        public string mvp; public int mvpVezes;                    // quem mais foi MVP de mapa na temporada
        public string evolucao; public int evolucaoGanho;          // quem mais subiu de geral
        public List<CampanhaTemporada> campeonatos = new List<CampanhaTemporada>();
        public List<JogadorTemporada> elenco = new List<JogadorTemporada>();
        public List<string> mudancas = new List<string>();         // o que muda para a temporada nova, na ordem da animação

        public Circuito Antes => (Circuito)circuitoAntes;
        public Circuito Depois => (Circuito)circuitoDepois;
        /// <summary>+1 subiu de circuito, -1 caiu, 0 ficou.</summary>
        public int Movimento => Math.Sign(circuitoDepois - circuitoAntes);
        public int Titulos => campeonatos.Count(k => k.posicao == 1);
        public int Saldo => dinheiroFim - dinheiroInicio;
        public int FasGanhos => fasFim - fasInicio;
        public int PctVitorias => mapas == 0 ? 0 : (int)Math.Round(100.0 * vitorias / mapas);

        /// <summary>A manchete da temporada, para o topo da tela.</summary>
        public string Manchete =>
            Movimento > 0 ? "Promovido ao circuito " + Mundo.NomeCircuito(Depois) + "!" :
            Movimento < 0 ? "Rebaixado ao circuito " + Mundo.NomeCircuito(Depois) :
            Titulos > 0 ? (Titulos == 1 ? "Campeão e mantido no " : Titulos + " títulos e mantido no ") + Mundo.NomeCircuito(Depois) :
            "Mantido no circuito " + Mundo.NomeCircuito(Depois);
    }

    public partial class Carreira
    {
        /// <summary>Resumos das temporadas encerradas, a mais nova primeiro.</summary>
        public readonly List<ResumoTemporada> temporadas = new List<ResumoTemporada>();
        /// <summary>A temporada que acabou e o manager ainda não viu (a interface mostra a animação e limpa).</summary>
        public bool temporadaNaoVista;

        // o estado no começo da temporada, para comparar no fim (salvo)
        public int inicioPartida, inicioFas, inicioDinheiro, inicioTitulos, inicioCircuito;
        public bool temInicioTemporada;

        public ResumoTemporada UltimaTemporada => temporadas.FirstOrDefault();

        /// <summary>Guarda como o clube começa a temporada (carreira nova e cada virada).</summary>
        public void MarcarInicioTemporada()
        {
            inicioPartida = numPartida; inicioFas = fas; inicioDinheiro = dinheiro; inicioTitulos = titulos.Count; inicioCircuito = (int)CircuitoClube;
            temInicioTemporada = true;
        }

        /// <summary>Monta o resumo da temporada que acabou. Chamado na virada, depois que o mundo já recalculou os circuitos e antes de
        /// a carreira envelhecer o elenco.</summary>
        ResumoTemporada ResumirTemporada()
        {
            if (!temInicioTemporada) MarcarInicioTemporada();
            int encerrada = Temporada - 1;
            var r = new ResumoTemporada
            {
                temporada = encerrada, ano = Mundo.AnoDaTemporada(encerrada),
                circuitoAntes = inicioCircuito, circuitoDepois = (int)CircuitoClube, ranking = PosicaoRanking, times = mundo.equipes.Count,
                dinheiroInicio = inicioDinheiro, dinheiroFim = dinheiro, fasInicio = inicioFas, fasFim = fas,
            };
            var doAno = historico.Where(x => x.numPartida > inicioPartida).ToList();   // mais novo primeiro
            r.mapas = doAno.Count; r.vitorias = doAno.Count(x => x.venceu); r.derrotas = r.mapas - r.vitorias;
            r.series = doAno.Count(x => x.serieEncerrada); r.seriesVencidas = doAno.Count(x => x.serieEncerrada && x.venceuSerie);
            int seq = 0;
            foreach (var x in Enumerable.Reverse(doAno)) { seq = x.venceu ? seq + 1 : 0; r.melhorSequencia = Math.Max(r.melhorSequencia, seq); }

            foreach (var t in titulos.Take(Math.Max(0, titulos.Count - inicioTitulos)).Reverse())
                r.campeonatos.Add(new CampanhaTemporada { nome = t.nome, posicao = t.posicao, times = t.times, premio = t.premio });
            r.premios = r.campeonatos.Sum(k => k.premio);

            var mvps = doAno.Select(x => x.Melhor?.nome).Where(n => n != null && elenco.Any(j => j.nome == n)).GroupBy(n => n).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (mvps != null) { r.mvp = mvps.Key; r.mvpVezes = mvps.Count(); }

            var tit = new HashSet<Jogador>(Titulares);
            int restam = elenco.Count;
            foreach (var j in elenco)
            {
                bool sai = j.idade + 1 >= IDADE_APOSENTADORIA && restam > Provisorio.TITULARES;   // a mesma regra do fim de temporada
                if (sai) restam--;
                int antes = (int)Math.Round(Jogador.OverallDe(j.inicioTemporada[0], j.inicioTemporada[1], j.inicioTemporada[2], j.inicioTemporada[3], j.inicioTemporada[4]));
                if (antes <= 0) antes = j.Overall;   // sem o começo guardado: sem evolução
                r.elenco.Add(new JogadorTemporada
                {
                    nome = j.nome, idade = j.idade + 1, overallAntes = antes, overallDepois = j.Overall, titular = tit.Contains(j),
                    mvps = doAno.Count(x => x.Melhor?.nome == j.nome), aposentou = sai,
                });
            }
            var melhor = r.elenco.Where(e => !e.aposentou).OrderByDescending(e => e.overallDepois - e.overallAntes).FirstOrDefault();
            if (melhor != null && melhor.overallDepois > melhor.overallAntes) { r.evolucao = melhor.nome; r.evolucaoGanho = melhor.overallDepois - melhor.overallAntes; }
            return r;
        }

        public const int IDADE_APOSENTADORIA = 36;

        /// <summary>A lista do que muda para a temporada nova, na ordem em que a animação mostra.</summary>
        void ListarMudancas(ResumoTemporada r)
        {
            var m = r.mudancas;
            m.Add("Pontos de ranking zerados: metade dos pontos fica de herança.");
            if (r.Movimento != 0) m.Add("O clube joga agora o circuito " + Mundo.NomeCircuito(r.Depois) + ": patrocínio de " + Fmt.Dinheiro(PatrocinioPorSemana) + " por semana.");
            m.Add("Todo o elenco faz aniversário: +1 ano.");
            foreach (var e in r.elenco.Where(e => e.aposentou)) m.Add(e.nome + " se aposenta aos " + e.idade + " anos.");
            m.Add("A evolução dos atributos volta a contar do zero.");
            if (intel.Count > 0) m.Add("A informação dos scouts cai pela metade.");
            m.Add("Mercado de jogadores renovado.");
            m.Add("Mercado de psicólogos renovado.");
            int camps = mundo.DoClube.Count();
            m.Add(camps + " campeonatos no calendário da temporada " + Temporada + ".");
        }
    }
}
