using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // O mundo do jogo (decisões 0012 e 0014): mapas, estilos de jogo, os 48 times, o ranking, os circuitos e o calendário
    // de campeonatos da temporada. Sem dependência do Unity. Números PROVISÓRIOS.
    // ============================================================================================================

    /// <summary>Pool de mapas (decisão 0014). Só o primeiro existe em 3D hoje; os outros entram como nome, afinidade e veto.</summary>
    public static class Mapas
    {
        public const int TOTAL = 7;
        public static readonly string[] NOMES = { "Forja", "Terraço", "Estaleiro", "Mina", "Vila Velha", "Observatório", "Duna" };
        public static bool Tem3D(int mapa) => mapa == 0;
    }

    /// <summary>Estratégias (decisão 0014). Cada uma ajusta os pesos da IA do time; há uma relação de contra entre elas.</summary>
    public enum Estilo { Equilibrada, Agressiva, Defensiva, ExecucoesRapidas, ControleDeMapa }

    public static class Estilos
    {
        public static string Nome(Estilo e) => e switch
        {
            Estilo.Agressiva => "Agressiva", Estilo.Defensiva => "Defensiva", Estilo.ExecucoesRapidas => "Execuções rápidas",
            Estilo.ControleDeMapa => "Controle de mapa", _ => "Equilibrada",
        };

        public static string Descricao(Estilo e) => e switch
        {
            Estilo.Agressiva => "Pressiona cedo, abre ângulos largos e arrisca duelos. Forte contra quem fica parado; sofre contra defesa organizada.",
            Estilo.Defensiva => "Segura posições, espera o erro e troca de ângulo com calma. Forte contra rush; sofre contra execuções rápidas.",
            Estilo.ExecucoesRapidas => "Decide o bomb cedo, com execuções rápidas e coordenadas. Forte contra defesa passiva; sofre contra controle de mapa.",
            Estilo.ControleDeMapa => "Toma espaço aos poucos e só executa tarde, com informação. Forte contra execuções rápidas; sofre contra agressão.",
            _ => "Sem ajustes: o jogo padrão da IA, sem pontos fortes nem fracos contra os outros estilos.",
        };

        /// <summary>Quem ganha de quem (um ciclo): Agressiva &lt; Defensiva &lt; Execuções rápidas &lt; Controle de mapa &lt; Agressiva.</summary>
        public static Estilo ContraDe(Estilo e) => e switch
        {
            Estilo.Agressiva => Estilo.Defensiva, Estilo.Defensiva => Estilo.ExecucoesRapidas,
            Estilo.ExecucoesRapidas => Estilo.ControleDeMapa, Estilo.ControleDeMapa => Estilo.Agressiva, _ => Estilo.Equilibrada,
        };

        /// <summary>+1 se <paramref name="meu"/> é o contra do estilo do adversário, -1 se é o contrário, 0 se é neutro.</summary>
        public static int Relacao(Estilo meu, Estilo deles)
        {
            if (meu == Estilo.Equilibrada || deles == Estilo.Equilibrada || meu == deles) return 0;
            if (ContraDe(deles) == meu) return 1;
            if (ContraDe(meu) == deles) return -1;
            return 0;
        }

        /// <summary>Copia os pesos base e aplica a estratégia (os campos usados estão dentro das faixas do treino da IA).</summary>
        public static Pesos Pesos(Estilo e)
        {
            var p = CSM.Sim.Pesos.Padrao().Clone();
            switch (e)
            {
                case Estilo.Agressiva: p.estilo = 0.6f; p.ctAgressao = 0.6f; p.tExec = 36; p.tExecDefault = 50; break;
                case Estilo.Defensiva: p.estilo = -0.6f; p.ctAgressao = 0.25f; p.tExec = 52; p.tExecDefault = 66; p.quieto = 0.9f; break;
                case Estilo.ExecucoesRapidas: p.estilo = 0.2f; p.tExec = 30; p.tExecDefault = 44; p.tFake = 46; break;
                case Estilo.ControleDeMapa: p.tExec = 52; p.tExecDefault = 70; p.avancar = 0.9f; break;
            }
            return p;
        }
    }

    /// <summary>O mundo: times, ranking, circuitos e o calendário de campeonatos da temporada.</summary>
    public class Mundo
    {
        public readonly List<Equipe> equipes = new List<Equipe>();
        public readonly List<Campeonato> campeonatos = new List<Campeonato>();   // todos os da temporada (de todos os circuitos)
        public int semana = 1, temporada = 1; int proxId = 1; Random rng; int seed;
        public const int SEMANAS_POR_TEMPORADA = 52;
        public const int TIMES = 48, POR_CIRCUITO = 16;

        public Equipe Clube => equipes[0];
        public Equipe PorId(int id) => equipes[id];

        // ------------------------------------------------------------------ calendário (modelo de uma temporada, repetido a cada ano)
        public class Slot { public int semana; public Formato formato; public string nome; public bool major, liga; }
        public static readonly Slot[] CALENDARIO =
        {
            new Slot { semana = 2,  formato = Formato.GruposGslMataMata, nome = "Copa Inaugural" },
            new Slot { semana = 5,  formato = Formato.Liga, nome = "Liga Nacional · 1º split", liga = true },
            new Slot { semana = 13, formato = Formato.MataMata, nome = "Copa Aberta de Outono" },
            new Slot { semana = 16, formato = Formato.GruposMataMata, nome = "Copa Meridional" },
            new Slot { semana = 20, formato = Formato.SuicoMataMata, nome = "Major de Outono", major = true },
            new Slot { semana = 23, formato = Formato.GruposGslMataMata, nome = "Copa Atlântica" },
            new Slot { semana = 26, formato = Formato.MataMata, nome = "Copa Aberta de Verão" },
            new Slot { semana = 29, formato = Formato.Liga, nome = "Liga Nacional · 2º split", liga = true },
            new Slot { semana = 37, formato = Formato.MataMata, nome = "Copa Aberta de Primavera" },
            new Slot { semana = 39, formato = Formato.GruposMataMata, nome = "Copa do Sul" },
            new Slot { semana = 42, formato = Formato.SuicoMataMata, nome = "Major de Inverno", major = true },
            new Slot { semana = 45, formato = Formato.GruposGslMataMata, nome = "Grande Final do Circuito" },
            new Slot { semana = 48, formato = Formato.GruposMataMata, nome = "Taça de Fim de Ano" },
        };

        static int Premio(Circuito c, Slot s)
        {
            int basePool = c == Circuito.Elite ? 600000 : c == Circuito.Pro ? 250000 : 80000;
            float mult = s.major ? 2f : s.liga ? 0.5f : s.formato == Formato.MataMata ? 0.5f : 1f;
            return (int)(basePool * mult);
        }
        static float Peso(Circuito c, Slot s) => (c == Circuito.Elite ? 600f : c == Circuito.Pro ? 250f : 100f) * (s.major ? 2f : s.liga ? 0.5f : s.formato == Formato.MataMata ? 0.5f : 1f);

        public static string NomeCircuito(Circuito c) => c == Circuito.Elite ? "Elite" : c == Circuito.Pro ? "Pro" : "Amador";

        // ------------------------------------------------------------------ criação
        static readonly string[] NOMES_TIMES =
        {
            "Fênix Rubra", "Lobos do Norte", "Titãs", "Corvos Negros", "Tigres de Aço", "Jaguares", "Serpentes", "Falcões", "Dragões Azuis", "Guardiões",
            "Mamutes", "Escorpiões", "Relâmpago", "Tempestade", "Vendaval", "Bússola", "Atlas", "Ônix", "Quasar", "Zênite",
            "Eclipse", "Vórtice", "Pulsar", "Nébula", "Cometa", "Meteoro", "Aurora Boreal", "Maré Alta", "Rochedo", "Cânion",
            "Planalto", "Savana", "Tundra", "Delta", "Estuário", "Recife", "Farol", "Âncora Velha", "Caravela", "Bandeirantes",
            "Sentinelas", "Vigias", "Patrulha", "Legião", "Cavaleiros", "Arqueiros", "Gladiadores",
        };
        static readonly string[] SILABAS = { "BA", "KO", "RI", "TU", "ZE", "MA", "LO", "NI", "SA", "VE", "DU", "FA", "GO", "HI", "JU", "PE", "QU", "XO", "YA", "WE", "TA", "RO", "MI", "KA" };
        static readonly Role[] FUNCOES = { Role.Igl, Role.Entry, Role.Awper, Role.Lurker, Role.Suporte };

        public static Mundo Novo(int seed, string nomeClube, int forcaClube, List<(string nome, Role funcao)> jogadoresClube)
        {
            var m = new Mundo { seed = seed, rng = new Random(seed * 13 + 5) };
            var clube = new Equipe { id = 0, nome = nomeClube, forca = forcaClube, clube = true, estilo = Estilo.Equilibrada, jogadores = jogadoresClube.ToList() };
            for (int k = 0; k < Mapas.TOTAL; k++) clube.afinidade[k] = 50;
            m.equipes.Add(clube);
            var usados = new HashSet<string>(jogadoresClube.Select(j => j.nome));
            string Nick() { string n; do { n = SILABAS[m.rng.Next(SILABAS.Length)] + SILABAS[m.rng.Next(SILABAS.Length)] + (m.rng.Next(3) == 0 ? SILABAS[m.rng.Next(SILABAS.Length)] : ""); } while (!usados.Add(n)); return n; }
            // 47 times: 16 de elite (78 a 90), 16 pro (79 a 66), 15 amadores (66 a 54)
            for (int i = 0; i < TIMES - 1; i++)
            {
                float f = i < 16 ? 90f - 12f * i / 15f : i < 32 ? 79f - 13f * (i - 16) / 15f : 66f - 12f * (i - 32) / 14f;
                var e = new Equipe { id = i + 1, nome = NOMES_TIMES[i], forca = f + (float)(m.rng.NextDouble() - 0.5) * 1.2f, estilo = (Estilo)m.rng.Next(5) };
                for (int k = 0; k < Mapas.TOTAL; k++) e.afinidade[k] = 30 + m.rng.Next(51);
                e.afinidade[m.rng.Next(Mapas.TOTAL)] = 85 + m.rng.Next(10);   // o mapa preferido
                foreach (var fn in FUNCOES) e.jogadores.Add((Nick(), fn));
                m.equipes.Add(e);
            }
            foreach (var e in m.equipes) e.pontosAnt = (e.forca - 50f) * 12f;
            m.RecalcularCircuitos();
            m.CriarTemporada();
            return m;
        }

        // ------------------------------------------------------------------ ranking e circuitos
        public List<Equipe> Ranking() => equipes.OrderByDescending(e => e.PontosRanking).ThenByDescending(e => e.forca).ToList();
        public int PosicaoRanking(int id) => Ranking().FindIndex(e => e.id == id) + 1;

        public void RecalcularCircuitos()
        {
            var r = Ranking();
            for (int i = 0; i < r.Count; i++) r[i].circuito = i < POR_CIRCUITO ? Circuito.Elite : i < 2 * POR_CIRCUITO ? Circuito.Pro : Circuito.Amador;
        }

        // ------------------------------------------------------------------ temporada
        public Func<int, int, double> ChanceMapaIA => (a, b) => Chance(equipes[a].forca, equipes[b].forca);
        public static double Chance(double fa, double fb) => 1.0 / (1.0 + Math.Exp(-(fa - fb) / 6.0));

        void CriarTemporada()
        {
            campeonatos.Clear();
            var porCirc = new Dictionary<Circuito, List<int>>();
            foreach (Circuito c in Enum.GetValues(typeof(Circuito)))
                porCirc[c] = Ranking().Where(e => e.circuito == c).Select(e => e.id).ToList();   // ordenados por ranking (cabeças de chave)
            foreach (var slot in CALENDARIO)
                foreach (Circuito c in new[] { Circuito.Elite, Circuito.Pro, Circuito.Amador })
                {
                    var tt = porCirc[c];
                    string nome = (slot.major && c != Circuito.Elite ? slot.nome.Replace("Major", "Finais") : slot.nome) + " " + NomeCircuito(c);
                    if (slot.liga)
                    {
                        var a = tt.Where((x, i) => i % 2 == 0).ToList(); var b = tt.Where((x, i) => i % 2 == 1).ToList();
                        Adicionar(new Campeonato(proxId++, nome + " (A)", slot.formato, c, slot.semana, a, Premio(c, slot), Peso(c, slot), seed * 131 + proxId));
                        Adicionar(new Campeonato(proxId++, nome + " (B)", slot.formato, c, slot.semana, b, Premio(c, slot), Peso(c, slot), seed * 131 + proxId));
                    }
                    else Adicionar(new Campeonato(proxId++, nome, slot.formato, c, slot.semana, tt, Premio(c, slot), Peso(c, slot), seed * 131 + proxId, slot.major && c == Circuito.Elite));
                }
        }

        public void Adicionar(Campeonato c) { c.ChanceMapa = ChanceMapaIA; campeonatos.Add(c); }

        /// <summary>Mundo em branco para o salvamento preencher (times, campeonatos e estado vêm do arquivo).</summary>
        public static Mundo ParaRestaurar(int seed, int temporada)
        {
            return new Mundo { seed = seed, rng = new Random(seed * 13 + 5 + temporada * 977) };
        }
        public int ProxId { get => proxId; set => proxId = value; }
        public int Seed => seed;

        /// <summary>Campeonatos (iniciados ou não) em que o clube joga nesta temporada.</summary>
        public IEnumerable<Campeonato> DoClube => campeonatos.Where(c => c.times.Contains(0));

        /// <summary>Hora de abrir os campeonatos da semana: os do clube geram a 1ª rodada; os outros já saem resolvidos.</summary>
        void IniciarDaSemana(Action<Campeonato> aoTerminarDoClube)
        {
            foreach (var c in campeonatos.Where(c => c.semanaInicio == semana && !c.iniciado))
            {
                c.iniciado = true;
                c.Iniciar(c.times.Contains(0) ? 0 : -1);
                if (c.terminado && !c.times.Contains(0)) { /* os pontos entram quando o campeonato "acaba" no calendário (ver ApurarFim) */ }
            }
        }

        /// <summary>Pontos e prêmio de quem acabou (os dos outros times, no fim do campeonato; os do clube, devolvidos para o chamador).</summary>
        public List<Campeonato> ApurarFim()
        {
            var clubeTerminou = new List<Campeonato>();
            foreach (var c in campeonatos.Where(c => c.terminado && !c.apurado && (semana >= c.SemanaFim || c.times.Contains(0))))
            {
                c.apurado = true;
                for (int k = 0; k < c.colocacao.Count; k++) equipes[c.colocacao[k]].pontos += c.PontosPara(k + 1);
                if (c.times.Contains(0)) clubeTerminou.Add(c);
            }
            return clubeTerminou;
        }

        /// <summary>Passa uma semana. Devolve os campeonatos do clube que terminaram e se a temporada virou.</summary>
        public (List<Campeonato> terminados, bool viroTemporada) AvancarSemana()
        {
            semana++;
            bool vira = false;
            if (semana > SEMANAS_POR_TEMPORADA) { FimDeTemporada(); vira = true; }
            IniciarDaSemana(null);
            var term = ApurarFim();
            return (term, vira);
        }

        void FimDeTemporada()
        {
            foreach (var c in campeonatos.Where(c => c.iniciado && !c.terminado)) { /* campeonato que ficou pendurado: o clube perdeu o fim; resolve o que falta */ c.FinalizarForcado(); }
            ApurarFim();
            foreach (var e in equipes) { e.pontosAnt = e.pontos + 0.5f * e.pontosAnt; e.pontos = 0; }
            foreach (var e in equipes.Where(e => !e.clube)) e.forca = Math.Max(48f, Math.Min(93f, e.forca + (float)(rng.NextDouble() - 0.5) * 4f - (e.forca - 72f) * 0.03f));   // os times oscilam ao longo dos anos
            semana = 1; temporada++;
            RecalcularCircuitos();
            CriarTemporada();
        }

        // ------------------------------------------------------------------ próximo compromisso
        /// <summary>O próximo confronto pendente do clube, o mais cedo por data; ou o próximo campeonato do clube que ainda não começou.</summary>
        public (Campeonato camp, Confronto confronto, int semana) ProximoCompromisso()
        {
            var pendentes = campeonatos.Where(c => c.iniciado && !c.terminado && c.PendenteDoClube != null)
                .Select(c => (c, f: c.PendenteDoClube)).OrderBy(x => x.f.semana * 10 + x.f.dia).ToList();
            if (pendentes.Count > 0) return (pendentes[0].c, pendentes[0].f, pendentes[0].f.semana);
            var futuro = DoClube.Where(c => !c.iniciado && c.semanaInicio >= semana).OrderBy(c => c.semanaInicio).FirstOrDefault();
            return futuro != null ? (futuro, null, futuro.semanaInicio) : (null, null, semana);
        }
    }
}
