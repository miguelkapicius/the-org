using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Camada de gestão do protótipo: clube, elenco, mercado, tempo (semanas), campeonatos, treino, finanças, Gaming House,
    // scouts, veto de mapas e estratégia. Decisões 0007 e 0012 a 0016.
    //
    // IMPORTANTE: os números e regras daqui são PROVISÓRIOS (balanceamento em aberto: ver perguntas-abertas.md). Ficam
    // juntos em `Provisorio` e em GamingHouse.NIVEIS para trocar num lugar só. Sem dependência do Unity (só do Sim).
    // ============================================================================================================

    /// <summary>Tudo que é número inventado para o protótipo andar. Nada aqui é decisão de design.</summary>
    public static class Provisorio
    {
        public const int DINHEIRO_INICIAL = 400000;
        public const int TITULARES = 5;            // a partida é 5v5 (decisão 0005); o resto é reserva
        public const int TAMANHO_MERCADO = 12;
        public const int NIVEL_GH_MAX = GamingHouse.MAX;

        // finanças (decisão 0015)
        public const int SEMANAS_ATE_FALENCIA = 4;                  // semanas com saldo negativo antes da falência
        public const float JURO_EMPRESTIMO = 0.12f;                 // juros totais do empréstimo
        public const int PARCELAS_EMPRESTIMO = 20;                  // parcelas semanais
        public static readonly int[] LIMITE_EMPRESTIMO = { 100000, 250000, 500000 };   // por circuito (Amador, Pro, Elite)
        public static readonly int[] PATROCINIO_BASE = { 1500, 5000, 11000 };           // por semana, por circuito

        // treino e cansaço (decisão 0013)
        public const float GANHO_POR_SESSAO = 0.08f;                // pontos de atributo por sessão por semana (antes de idade e teto)
        // lados TR/CT (decisão 0024): domínio 0..100 por lado; a diferença entre os dois lados vale até LADO_MAX de habilidade a cada 50 pontos
        public const float LADO_MAX = 0.08f, GANHO_LADO = 1.8f, LADO_SORTEIO = 20f, LADO_FOCO_ATRIB = 0.6f;   // ganho por sessão no lado em foco; o atributo rende 60% nessas sessões
        public const float PREPARO_MAX_BONUS = 0.05f;              // até +5% de habilidade com o preparo cheio
        public const float CANSACO_LIMIAR = 35f, CANSACO_MAX_PENALIDADE = 0.12f;   // acima do limiar, até -12% em 100 de cansaço
        public const float AFINIDADE_MAX = 0.03f, ESTRATEGIA_CONTRA = 0.02f;       // efeito de afinidade e de contra-estratégia
        public static readonly int[] INTEL_SEMANAS = { 1, 3, 6 };   // semanas de estudo para os níveis 1, 2 e 3 de informação

        // personalidade (decisão 0019)
        public const float CHANCE_TRACO = 0.5f;                     // metade dos jogadores tem um traço
        public const float MORAL_INICIAL = 65f, MORAL_CONTRATADO = 70f, MORAL_NEUTRA = 60f, MORAL_RENOVACAO = 5f, MORAL_RESCISAO = 2f;
        public const float MORAL_VITORIA = 3f, MORAL_DERROTA = 3f, MORAL_TITULAR_SEMANA = 1f, MORAL_RESERVA_SEMANA = 2f, MORAL_AVISO = 30f;
        public const float MORAL_MAX_BONUS = 0.03f, MORAL_POR_PONTO = MORAL_MAX_BONUS / 40f;   // +3% em 100, -4,5% em 0, neutra em 60
        public const float ENTROSAMENTO_INICIAL = 60f, ENTROSAMENTO_CONTRATADO = 20f;
        public const float ENTROSAMENTO_TITULAR = 5f, ENTROSAMENTO_RESERVA = 1f, ENTROSAMENTO_COLETIVA = 3f, ENTROSAMENTO_ESTRELA = 15f;
        public const float ENTROSAMENTO_FORCA = 0.02f;              // ±2% na força (partida pulada) entre 0 e 100
    }

    public static class Fmt
    {
        /// <summary>1234567 → "$ 1.234.567" (independe da cultura do computador).</summary>
        public static string Fas(int v) => CSM.Manager.Fas.Texto(v);

        public static string Dinheiro(int v)
        {
            string s = Math.Abs((long)v).ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
            return (v < 0 ? "-$ " : "$ ") + s;
        }

        /// <summary>Sigla de 3 letras (etiquetas da pré-partida).</summary>
        public static string FuncaoCurta(Role r) => r switch
        {
            Role.Entry => "ENT", Role.Suporte => "SUP", Role.Lurker => "LUR", Role.Awper => "AWP",
            Role.Igl => "IGL", Role.Ancora => "ANC", Role.Rotacao => "ROT", _ => "---",
        };

        public static string Funcao(Role r) => r switch
        {
            Role.Entry => "Entry", Role.Suporte => "Suporte", Role.Lurker => "Lurker", Role.Awper => "AWPer",
            Role.Igl => "IGL", Role.Ancora => "Âncora", Role.Rotacao => "Rotação", _ => r.ToString(),
        };

        public static string Serie(int md) => "MD" + md;

        /// <summary>O rótulo de um confronto sem repetir o nome do campeonato que ele já traz no começo.</summary>
        public static string Rotulo(string campeonato, string rotulo) =>
            !string.IsNullOrEmpty(rotulo) && !string.IsNullOrEmpty(campeonato) && rotulo.StartsWith(campeonato) ? rotulo.Substring(campeonato.Length).TrimStart(' ', '·') : rotulo;
        public static string Ordinal(int k) => k + "º";
    }

    public class Lancamento { public int semana; public string descricao; public int valor; }

    /// <summary>O que um jogador fez numa partida. Na partida assistida vem da simulação; na pulada é uma ESTIMATIVA (ver Resultado.estimado).</summary>
    public class Desempenho
    {
        public string nome; public Role funcao; public int k, d, hs, dano, fk, rounds;
        public float KD => d == 0 ? k : k / (float)d;
        public int HsPct => k == 0 ? 0 : (int)Math.Round(100f * hs / k);
        public int DanoPorRound => rounds == 0 ? 0 : dano / rounds;
        /// <summary>Nota simples (1,0 = jogo normal) só para destacar o melhor da partida; não é regra do jogo.</summary>
        public float Nota => rounds == 0 ? 0 : 0.5f + (k - 0.6f * d) / rounds * 0.8f + dano / (rounds * 100f) * 0.5f;
    }

    /// <summary>O resultado de um mapa (uma partida) de uma série.</summary>
    public class Resultado
    {
        public int numPartida, semana; public string adversario, campeonato, rotulo, mapa; public bool venceu, assistida; public int meus, deles;
        public int saldoAntes, forcaMinha, forcaDeles;
        public int fas;   // fãs ganhos (ou perdidos) neste mapa e, se a série acabou, o bônus dela (decisão 0037)
        /// <summary>true = os números por jogador foram estimados (partida pulada), não simulados.</summary>
        public bool estimado;
        public List<Desempenho> desempenho = new List<Desempenho>();
        public int serieMeus, serieDeles, serieMd; public bool serieEncerrada, venceuSerie;
        public List<(string mapa, int meus, int deles)> mapasDaSerie = new List<(string mapa, int meus, int deles)>();
        public string Placar => meus + " x " + deles;
        public string PlacarSerie => serieMeus + " x " + serieDeles;
        public Desempenho Melhor => desempenho.Count == 0 ? null : desempenho.OrderByDescending(x => x.Nota).First();
        /// <summary>Jogada da partida (decisão 0020): a descrição do lance de maior nota; null na partida pulada. jogadaNossa: foi de um jogador do clube.</summary>
        public string jogada; public bool jogadaNossa;
        /// <summary>MVP da série (só no último mapa dela).</summary>
        public string mvpSerie;
    }

    /// <summary>Um aviso do painel: o que merece atenção agora e para onde ir resolver.</summary>
    public class Aviso
    {
        public enum Nivel { Dica, Atencao, Critico }
        public Nivel nivel; public string texto, acao; public string destino;   // destino: elenco, mercado, gh, financas, treino, scouts, calendario, serie
    }

    public class Emprestimo { public int principal, parcela, restantes; }
    public class Scout { public int tipo; public int alvo; }   // tipo: 0 livre, 1 procurar no mercado, 2 estudar um adversário (alvo = id do time)
    public class ResultadoCampeonato { public string nome; public int posicao, times, premio; public float pontos; public int semana; }

    /// <summary>O que aconteceu ao passar uma semana (para a interface mostrar).</summary>
    public class ResumoSemana
    {
        public int semana, temporada; public bool virouTemporada;
        public List<string> linhas = new List<string>();
        public List<ResultadoCampeonato> campeonatos = new List<ResultadoCampeonato>();
        public int receitas, despesas;
    }

    /// <summary>O veto de mapas de uma série (decisão 0014): bane e escolhe alternadamente, e o que sobra é o decisivo.</summary>
    public class Veto
    {
        public class Passo { public bool pick; public int quem; public int mapa = -1; }   // quem: 0 = o clube, 1 = o adversário
        public int md; public readonly List<Passo> passos = new List<Passo>(); public readonly List<int> restantes = new List<int>(); public int i;

        public Veto(int md, int primeiro)
        {
            this.md = md;
            for (int m = 0; m < Mapas.TOTAL; m++) restantes.Add(m);
            int a = primeiro, b = 1 - primeiro;
            void P(bool pick, int quem) => passos.Add(new Passo { pick = pick, quem = quem });
            if (md == 1) { P(false, a); P(false, b); P(false, a); P(false, b); P(false, a); P(false, b); }
            else if (md == 3) { P(false, a); P(false, b); P(true, a); P(true, b); P(false, a); P(false, b); }
            else { P(false, a); P(false, b); P(true, a); P(true, b); P(true, a); P(true, b); }
        }
        public Passo Atual => i < passos.Count ? passos[i] : null;
        public bool Completo => i >= passos.Count;
        public void Aplicar(int mapa) { passos[i].mapa = mapa; restantes.Remove(mapa); i++; }
        /// <summary>Os mapas na ordem em que serão jogados: as escolhas e, por último, o que sobrou (o decisivo).</summary>
        public List<int> OrdemDeJogo()
        {
            var l = passos.Where(p => p.pick && p.mapa >= 0).Select(p => p.mapa).ToList();
            if (Completo && restantes.Count == 1) l.Add(restantes[0]);
            return l;
        }
    }

    /// <summary>Uma série em andamento (do veto ao último mapa).</summary>
    public class Serie
    {
        public Campeonato camp; public Confronto conf; public Equipe adv; public Veto veto;
        public List<int> mapas = new List<int>(); public List<(int meus, int deles)> resultados = new List<(int, int)>();
        public int Md => conf.md; public int Alvo => conf.Alvo;
        public int MinhasVit => resultados.Count(r => r.meus > r.deles);
        public int DelesVit => resultados.Count(r => r.deles > r.meus);
        public bool Encerrada => MinhasVit >= Alvo || DelesVit >= Alvo;
        public int MapaAtual => !Encerrada && resultados.Count < mapas.Count ? mapas[resultados.Count] : -1;
    }

    public struct Compromisso
    {
        public Campeonato camp; public Confronto conf; public int semana; public Equipe adv;
        public bool TemJogo => conf != null;
    }

    /// <summary>
    /// O estado da carreira. Fica estático entre cenas (Manager ↔ Partida3D) e é salvo em disco pela camada da interface.
    /// </summary>
    public partial class Carreira
    {
        public static Carreira Atual;

        public string clube = "VULCÃO";
        public const int TUTORIAL_PASSOS = 6;    // primeiros passos (decisão 0028)
        public int tutorial;                      // passo atual do tour; TUTORIAL_PASSOS = concluído ou pulado
        public int dinheiro, numPartida, nivelGH = 1;
        public int fas = Fas.INICIAIS, sequencia;   // fãs do clube (decisão 0037); sequencia: mapas seguidos (+ vitórias, - derrotas)   // numPartida: quantos mapas já foram jogados na carreira
        public readonly List<Jogador> elenco = new List<Jogador>();      // os 5 primeiros são os titulares
        public readonly List<Jogador> mercado = new List<Jogador>();
        public readonly List<Lancamento> extrato = new List<Lancamento>();
        public readonly List<Resultado> historico = new List<Resultado>();
        public readonly Dictionary<Jogador, Negociacao> negociacoes = new Dictionary<Jogador, Negociacao>();   // conversas de contratação em andamento (decisão 0026; não são salvas)
        public readonly List<ResultadoCampeonato> titulos = new List<ResultadoCampeonato>();   // campeonatos já disputados
        public readonly List<string> diario = new List<string>();
        public Mundo mundo;
        public Serie serie;
        public Estilo estilo = Estilo.Equilibrada;       // a estratégia escolhida para a próxima partida
        // playbook do manager (decisão 0022): jogadas favoritas e proibidas, por lado
        public readonly List<string> favoritasTR = new List<string>(), proibidasTR = new List<string>(), favoritasCT = new List<string>(), proibidasCT = new List<string>();
        public int sessoesColetivas; public int estudoMapa = -1;   // plano do time na semana
        public readonly List<Scout> scouts = new List<Scout>();
        public readonly Dictionary<int, float> intel = new Dictionary<int, float>();   // semanas de estudo por time
        public readonly List<Emprestimo> emprestimos = new List<Emprestimo>();
        public int semanasNegativo; public bool falido;
        public bool Falido => falido;

        /// <summary>A última venda, para o "desfazer" (some assim que qualquer outra coisa muda no clube).</summary>
        public Jogador ultimaVenda; int ultimaVendaIndice; Lancamento ultimaVendaLancamento;

        /// <summary>A partida montada e à espera da cena 3D buscar (ver PartidaAoVivo3D). null = nada pendente.</summary>
        public CSM.Sim.Partida pendente;
        public bool emPartida;
        /// <summary>O último resultado que o manager ainda não viu no painel (a interface mostra e limpa).</summary>
        public Resultado naoVisto;

        int seed; Random rng;
        public int Seed => seed;

        // ------------------------------------------------------------------------------------------ criação
        static readonly string[] NOMES =
        {
            "BRASA","NEBLINA","CORVO","LOBO","ARPAO","RAIO","GELO","FERRO","ONIX","JAGUAR","CIPO","PIRITA","MAMBA","TUCANO",
            "ZEFIRO","QUASAR","NIMBO","CRATERA","MARE","CINZA","OBSIDIA","LANCA","PRESA","SERRA","CAPIM","TOCAIA","BREU","AURORA",
            "DUNA","GAVIAO","HELICE","IGUANA","JADE","LIMAO","MIRAGEM","NOVA","ORQUIDEA","PUMA","RUBI","TUFAO",
        };
        static readonly Role[] FUNCOES = { Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };

        public static Carreira Nova(int seed)
        {
            var c = new Carreira(seed) { dinheiro = Provisorio.DINHEIRO_INICIAL };
            // os 5 titulares são os bots que a simulação já tem (mesmos nomes e funções, habilidade padrão)
            foreach (var (nome, role, _, _) in Match.T_TEAM)
            {
                var j = new Jogador { nome = nome, idade = 21 + c.rng.Next(0, 8), funcao = role, skill = new Skill() };
                c.DefinirPotencial(j, 8, 18);
                c.SortearTraco(j); c.SortearLados(j);
                c.elenco.Add(j);
            }
            for (int i = 0; i < 2; i++) c.elenco.Add(c.Gerar(0.35f, 0.65f));   // duas reservas, um pouco abaixo
            foreach (var j in c.elenco) { c.GuardarInicio(j); Contratos.Assinar(j, 0); }   // contrato inicial pelo prazo que cada um prefere (decisão 0035)
            c.mundo =Mundo.Novo(seed, c.clube, c.Forca, c.elenco.Take(Provisorio.TITULARES).Select(j => (j.nome, j.funcao)).ToList());
            c.GerarMercado();
            c.AjustarScouts();
            c.Anotar("Começa a temporada 1. O clube joga o circuito " + Mundo.NomeCircuito(c.mundo.Clube.circuito) + ".");
            return c;
        }

        Carreira(int seed) { this.seed = seed; rng = new Random(seed); }

        /// <summary>Carreira em branco para o salvamento preencher (o estado vem do arquivo). O sorteio recomeça de uma semente derivada.</summary>
        public static Carreira ParaRestaurar(int seed, int numPartida)
        {
            var c = new Carreira(seed);
            c.rng = new Random(seed * 31 + numPartida * 7919);
            return c;
        }

        void DefinirPotencial(Jogador j, int minFolga, int maxFolga)
        {
            int folga = minFolga + rng.Next(0, maxFolga - minFolga + 1) + (j.idade <= 21 ? 6 : j.idade <= 24 ? 3 : 0);
            j.potencial = Math.Min(97, j.Overall + folga);
        }

        void SortearTraco(Jogador j)
        {
            j.traco = rng.NextDouble() < Provisorio.CHANCE_TRACO ? (Traco)rng.Next(1, 7) : Traco.Nenhum;
        }

        /// <summary>Cada jogador tem um lado de preferência: um domina o TR, outro o CT (decisão 0024).</summary>
        void SortearLados(Jogador j)
        {
            float d = ((float)rng.NextDouble() - 0.5f) * 2f * Provisorio.LADO_SORTEIO;   // -20..+20
            float base50 = 50f + ((float)rng.NextDouble() - 0.5f) * 8f;
            j.ladoTR = Math.Clamp(base50 + d, 20f, 95f); j.ladoCT = Math.Clamp(base50 - d, 20f, 95f);
        }

        void GuardarInicio(Jogador j) { for (int a = 0; a < 5; a++) j.inicioTemporada[a] = j.Valor(a); }

        /// <summary>Jogador novo com qualidade entre qMin e qMax (0..1); os atributos variam um pouco em volta.</summary>
        Jogador Gerar(float qMin, float qMax)
        {
            float q = qMin + (qMax - qMin) * (float)rng.NextDouble();
            float V(float pior, float melhor, float ruido = 0.15f) =>
                pior + (melhor - pior) * Math.Clamp(q + ((float)rng.NextDouble() - 0.5f) * 2f * ruido, 0f, 1f);
            var sk = new Skill
            {
                aimSigma = V(1.4f, 0.5f), reaction = V(0.30f, 0.15f), spray = V(0.5f, 0.95f),
                utilitaria = V(0.4f, 0.95f), composure = V(0.4f, 0.95f), crosshair = V(0.5f, 0.95f), flick = V(600f, 1000f),
                aggression = (float)rng.NextDouble(),
            };
            var j = new Jogador { nome = NomeLivre(), idade = 17 + rng.Next(0, 15), funcao = FUNCOES[rng.Next(FUNCOES.Length)], skill = sk };
            DefinirPotencial(j, 3, 24);
            SortearTraco(j); SortearLados(j);
            GuardarInicio(j);
            return j;
        }

        string NomeLivre()
        {
            var usados = new HashSet<string>(elenco.Select(j => j.nome).Concat(mercado.Select(j => j.nome)));
            if (mundo != null) foreach (var e in mundo.equipes) foreach (var (n, _) in e.jogadores) usados.Add(n);
            foreach (var (n, _, _, _) in Match.CT_TEAM) usados.Add(n);
            var livres = NOMES.Where(n => !usados.Contains(n)).ToList();
            return livres.Count > 0 ? livres[rng.Next(livres.Count)] : "JOGADOR" + rng.Next(100, 999);
        }

        void GerarMercado()
        {
            mercado.Clear();
            for (int i = 0; i < Provisorio.TAMANHO_MERCADO; i++) mercado.Add(Gerar(0.2f, 0.95f));
            mercado.Sort((a, b) => b.Overall.CompareTo(a.Overall));
        }

        /// <summary>Alguns jogadores do mercado assinam com outros clubes e entram novos. Devolve quem saiu.</summary>
        List<Jogador> RenovarMercado(int quantos)
        {
            var saiu = new List<Jogador>();
            for (int i = 0; i < quantos && mercado.Count > 0; i++) { int k = rng.Next(mercado.Count); saiu.Add(mercado[k]); mercado.RemoveAt(k); }
            while (mercado.Count < Provisorio.TAMANHO_MERCADO) mercado.Add(Gerar(0.2f, 0.95f));
            mercado.Sort((a, b) => b.Overall.CompareTo(a.Overall));
            return saiu;
        }

        void Anotar(string t) { diario.Insert(0, "Semana " + Semana + ": " + t); if (diario.Count > 40) diario.RemoveAt(diario.Count - 1); }

        // ------------------------------------------------------------------------------------------ consultas básicas
        public int Semana => mundo.semana;
        public int Temporada => mundo.temporada;
        public Equipe EquipeClube => mundo.Clube;
        public Circuito CircuitoClube => mundo.Clube.circuito;
        public int PosicaoRanking => mundo.PosicaoRanking(0);
        public NivelGH GH => GamingHouse.De(nivelGH);
        public int ElencoMax => GH.elencoMax;

        public IEnumerable<Jogador> Titulares => elenco.Take(Provisorio.TITULARES);
        public bool Titular(Jogador j) { int i = elenco.IndexOf(j); return i >= 0 && i < Provisorio.TITULARES; }
        public int SalariosPorSemana => elenco.Sum(j => j.Salario);
        public int Forca => elenco.Count == 0 ? 0 : (int)Math.Round(Titulares.Average(j => j.Overall));
        public int CustoProximoNivelGH => GamingHouse.CustoProximo(nivelGH);
        public int PatrocinioPorSemana => (int)(Provisorio.PATROCINIO_BASE[(int)CircuitoClube] * GH.patrocinio);
        public int DespesaFixaPorSemana => SalariosPorSemana + GH.manutencao + emprestimos.Sum(e => e.parcela);

        /// <summary>Força (média do geral) de uma escalação hipotética: serve para mostrar o efeito de uma troca ANTES de fazê-la.</summary>
        public static int ForcaDe(IEnumerable<Jogador> titulares) { var l = titulares.ToList(); return l.Count == 0 ? 0 : (int)Math.Round(l.Average(j => j.Overall)); }

        /// <summary>A escalação se o jogador <paramref name="entra"/> ficasse no lugar de <paramref name="sai"/> (sai pode ser null = só acrescenta).</summary>
        public int ForcaCom(Jogador entra, Jogador sai)
        {
            var t = Titulares.ToList();
            if (sai != null) { int i = t.IndexOf(sai); if (i >= 0) t[i] = entra; }
            return ForcaDe(t);
        }

        /// <summary>O titular mais fraco que o jogador <paramref name="j"/> superaria (o melhor lugar para ele entrar), ou null.</summary>
        public Jogador QuemSaiPara(Jogador j)
        {
            var pior = Titulares.Where(t => t != j).OrderBy(t => t.Overall).FirstOrDefault();
            return pior != null && j.Overall > pior.Overall ? pior : null;
        }

        static readonly Role[] FUNCOES_DO_TIME = { Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };
        /// <summary>Funções que a escalação titular não cobre (um time equilibrado tem as cinco).</summary>
        public List<Role> FuncoesFaltando() => FUNCOES_DO_TIME.Where(f => !Titulares.Any(j => j.funcao == f)).ToList();

        /// <summary>Quantas semanas o dinheiro paga só das despesas fixas (sem contar prêmios e patrocínio).</summary>
        public double SemanasDeFolego => DespesaFixaPorSemana == 0 ? 99 : Math.Max(0, dinheiro) / (double)DespesaFixaPorSemana;

        // ------------------------------------------------------------------------------------------ campeonatos e compromissos
        public Compromisso Proximo()
        {
            var (camp, conf, sem) = mundo.ProximoCompromisso();
            return new Compromisso { camp = camp, conf = conf, semana = sem, adv = conf != null ? mundo.equipes[conf.a == 0 ? conf.b : conf.a] : null };
        }

        /// <summary>O adversário da série em andamento ou do próximo confronto (null se não há confronto à vista).</summary>
        public Equipe Adversario => serie != null ? serie.adv : Proximo().adv;

        /// <summary>Há um confronto do clube marcado para esta semana (ou atrasado)? Então não dá para só avançar.</summary>
        public bool TemJogoAgora { get { var p = Proximo(); return p.TemJogo && p.semana <= Semana; } }
        public int SemanasAteProximo { get { var p = Proximo(); return p.camp == null ? 99 : Math.Max(0, p.semana - Semana); } }

        public IEnumerable<Campeonato> CampeonatosDoClube => mundo.DoClube.OrderBy(c => c.semanaInicio);
        public IEnumerable<Campeonato> CampeonatosAtivosDoClube => mundo.DoClube.Where(c => c.iniciado && !c.terminado);

        // ------------------------------------------------------------------------------------------ forma, afinidade e chance de vitória
        /// <summary>O quanto a habilidade de um jogador vale agora: 1,0 = o que os atributos dizem. Soma preparo, afinidade com o mapa e
        /// contra-estratégia; subtrai cansaço. É isto que vai para a simulação (pilar 3).</summary>
        public float MultEfetivo(Jogador j, int mapa = -1, Equipe adv = null)
        {
            float preparo = j.preparo / 100f * Provisorio.PREPARO_MAX_BONUS;
            float cansaco = Math.Max(0f, (j.cansaco - Provisorio.CANSACO_LIMIAR) / (100f - Provisorio.CANSACO_LIMIAR)) * Provisorio.CANSACO_MAX_PENALIDADE;
            float afin = mapa >= 0 ? (mundo.Clube.afinidade[mapa] - 50f) / 50f * Provisorio.AFINIDADE_MAX : 0f;
            float rel = adv != null ? Estilos.Relacao(estilo, adv.estilo) * Provisorio.ESTRATEGIA_CONTRA : 0f;
            float moral = (j.moral - Provisorio.MORAL_NEUTRA) * Provisorio.MORAL_POR_PONTO;
            float entros = (EntrosamentoTime - 50f) / 50f * Provisorio.ENTROSAMENTO_FORCA;
            return 1f + preparo - cansaco + afin + rel + moral + entros;
        }

        /// <summary>Entrosamento do time (decisão 0019): a média dos titulares, 0..100.</summary>
        public float EntrosamentoTime => elenco.Count == 0 ? 50f : Titulares.Average(j => j.entrosamento);

        public float ForcaEfetiva(int mapa = -1, Equipe adv = null) => elenco.Count == 0 ? 0 : Titulares.Average(j => j.OverallEfetivo(MultEfetivo(j, mapa, adv)));

        public float ForcaEfetivaAdv(Equipe adv, int mapa = -1)
        {
            float afin = mapa >= 0 ? (adv.afinidade[mapa] - 50f) / 50f * Provisorio.AFINIDADE_MAX : 0f;
            return adv.forca * (1f + afin + Estilos.Relacao(adv.estilo, estilo) * Provisorio.ESTRATEGIA_CONTRA);
        }

        /// <summary>Chance de vencer UM mapa contra <paramref name="adv"/> (mapa -1 = ainda sem mapa definido).
        /// <paramref name="forcaHipotetica"/>: usa essa força de titulares no lugar da real (para pré-visualizar trocas).</summary>
        public double ChanceMapa(Equipe adv, int mapa = -1, int forcaHipotetica = -1)
        {
            if (adv == null) return 0.5;
            double minha = ForcaEfetiva(mapa, adv);
            if (forcaHipotetica >= 0 && Forca > 0) minha *= forcaHipotetica / (double)Forca;
            return Mundo.Chance(minha, ForcaEfetivaAdv(adv, mapa));
        }

        /// <summary>Probabilidade de ganhar uma série (primeiro a N vitórias) com chance <paramref name="p"/> por mapa.</summary>
        public static double ChanceSerie(double p, int md)
        {
            int n = md / 2 + 1; double soma = 0, c = 1;
            for (int k = 0; k < n; k++)
            {
                if (k > 0) c = c * (n - 1 + k) / k;   // C(n-1+k, k)
                soma += c * Math.Pow(p, n) * Math.Pow(1 - p, k);
            }
            return soma;
        }

        /// <summary>Adversário de referência quando não há confronto à vista: o time do circuito mais próximo da nossa força.</summary>
        Equipe AdvReferencia => Adversario ?? mundo.equipes.Where(e => !e.clube && e.circuito == CircuitoClube).OrderBy(e => Math.Abs(e.forca - Forca)).First();
        int MdReferencia { get { var p = Proximo(); return serie != null ? serie.Md : p.TemJogo ? p.conf.md : 1; } }

        /// <summary>Chance de vencer a próxima série (com a escalação, o preparo, o cansaço e a estratégia de agora).</summary>
        public double ChanceVitoria() => ChanceSerie(ChanceMapa(AdvReferencia), MdReferencia);
        /// <summary>O mesmo, com uma força hipotética dos titulares (para mostrar o efeito de uma troca antes de confirmar).</summary>
        public double ChanceVitoria(int forcaHipotetica) => ChanceSerie(ChanceMapa(AdvReferencia, -1, forcaHipotetica), MdReferencia);

        // ------------------------------------------------------------------------------------------ informação sobre o adversário (scouts)
        public int MaxIntel => GH.nivelIntel;
        public float SemanasDeIntel(int idTime) => intel.TryGetValue(idTime, out var s) ? s : 0f;
        /// <summary>Quanto o manager sabe do adversário: 0 nada, 1 o estilo, 2 os mapas, 3 a estratégia e o destaque (limitado pela Gaming House).</summary>
        public int NivelIntel(int idTime)
        {
            float s = SemanasDeIntel(idTime); int n = 0;
            for (int k = 0; k < Provisorio.INTEL_SEMANAS.Length; k++) if (s >= Provisorio.INTEL_SEMANAS[k]) n = k + 1;
            return Math.Min(n, MaxIntel);
        }

        /// <summary>O preset de estratégia que ganha do estilo do adversário (só se sabe com informação nível 1).</summary>
        public Estilo? SugestaoContra(Equipe adv) => adv != null && NivelIntel(adv.id) >= 1 ? Estilos.ContraDe(adv.estilo) : (Estilo?)null;

        void AjustarScouts()
        {
            while (scouts.Count < GH.scouts) scouts.Add(new Scout());
            while (scouts.Count > GH.scouts) scouts.RemoveAt(scouts.Count - 1);
        }

        public string DefinirScout(int i, int tipo, int alvo = -1)
        {
            if (i < 0 || i >= scouts.Count) return "Esse scout não existe (suba a Gaming House).";
            scouts[i].tipo = tipo; scouts[i].alvo = alvo;
            return null;
        }

        // ------------------------------------------------------------------------------------------ avisos
        /// <summary>O que merece atenção agora, do mais grave para o menos.</summary>
        public List<Aviso> Avisos()
        {
            var l = new List<Aviso>();
            void A(Aviso.Nivel n, string t, string a, string d) => l.Add(new Aviso { nivel = n, texto = t, acao = a, destino = d });
            if (Falido) { A(Aviso.Nivel.Critico, "O clube faliu.", "Ver finanças", "financas"); return l; }
            if (dinheiro < 0) A(Aviso.Nivel.Critico, "Saldo NEGATIVO há " + semanasNegativo + " semana(s): na " + Provisorio.SEMANAS_ATE_FALENCIA + "ª o clube vai à falência. Pegue um empréstimo ou venda jogadores.", "Ver finanças", "financas");
            else if (SemanasDeFolego < 1) A(Aviso.Nivel.Critico, "O saldo não paga nem as despesas da próxima semana.", "Ver finanças", "financas");
            else if (SemanasDeFolego < 4) A(Aviso.Nivel.Atencao, "O saldo paga só " + (int)SemanasDeFolego + " semana(s) de despesas (sem prêmios).", "Ver finanças", "financas");
            if (TemJogoAgora) A(Aviso.Nivel.Atencao, "Há uma série marcada para esta semana.", "Preparar série", "serie");
            foreach (var f in FuncoesFaltando()) A(Aviso.Nivel.Atencao, "Nenhum titular na função " + Fmt.Funcao(f) + ".", "Ver mercado", "mercado");
            int cansados = Titulares.Count(j => j.cansaco >= 60);
            if (cansados > 0) A(Aviso.Nivel.Atencao, cansados + " titular(es) com cansaço alto: a habilidade na partida cai. Reduza o treino ou deixe descansar.", "Ver treino", "treino");
            foreach (var r in elenco.Skip(Provisorio.TITULARES))
            {
                var fraco = QuemSaiPara(r);
                if (fraco != null) { A(Aviso.Nivel.Dica, r.nome + " (reserva, " + r.Overall + ") é melhor que o titular " + fraco.nome + " (" + fraco.Overall + ").", "Ajustar escalação", "elenco"); break; }
            }
            foreach (var j in elenco.Where(j => j.contratoSemanas > 0 && j.contratoSemanas <= Contratos.SEMANAS_AVISO).OrderByDescending(j => Titular(j)).Take(2))
                A(Aviso.Nivel.Atencao, j.nome + " termina o contrato em " + Contratos.Prazo(j.contratoSemanas) + ": renove, ou ele sai de graça.", "Ver elenco", "elenco");
            foreach (var j in elenco.Where(j => j.moral < Provisorio.MORAL_AVISO).Take(2))
                A(Aviso.Nivel.Atencao, j.nome + " está desmotivado (moral " + (int)j.moral + "): rende menos na partida." + (Titular(j) ? "" : " Reserva há muito tempo perde ânimo."), "Ver elenco", "elenco");
            if (EntrosamentoTime < 40) A(Aviso.Nivel.Dica, "Time pouco entrosado (" + (int)EntrosamentoTime + "): os bots trocam menos os colegas. Mantenha os titulares e faça sessões coletivas.", "Ver treino", "treino");
            if (nivelGH >= 2 && elenco.All(j => j.sessoes == 0)) A(Aviso.Nivel.Dica, "Ninguém está treinando: os atributos só mudam pela idade.", "Ver treino", "treino");
            if (scouts.Any(s => s.tipo == 0)) A(Aviso.Nivel.Dica, "Há scout sem tarefa: estude o próximo adversário ou procure talentos no mercado.", "Ver scouts", "scouts");
            if (elenco.Count <= Provisorio.TITULARES) A(Aviso.Nivel.Dica, "Sem reservas: se alguém sair, o time fica incompleto.", "Ver mercado", "mercado");
            if (nivelGH < GamingHouse.MAX && FasBastam && dinheiro >= CustoProximoNivelGH + DespesaFixaPorSemana * 8)
                A(Aviso.Nivel.Dica, "Dá para subir a Gaming House sem apertar o caixa.", "Ver Gaming House", "gh");
            return l.OrderByDescending(a => (int)a.nivel).ToList();
        }

        // ------------------------------------------------------------------------------------------ ações de elenco (devolvem a mensagem de erro, ou null se deu certo)
        void Lanca(string descricao, int valor)
        {
            dinheiro += valor;
            extrato.Insert(0, new Lancamento { semana = Semana, descricao = descricao, valor = valor });
            if (extrato.Count > 200) extrato.RemoveAt(extrato.Count - 1);
        }

        /// <summary>Contrata um jogador do mercado. Sem argumentos é o preço de tabela; com <paramref name="luvas"/> e <paramref name="salario"/> é o que foi
        /// acertado na negociação (decisão 0026).</summary>
        public string Contratar(Jogador j, int? luvas = null, int? salario = null, int anos = 0)
        {
            if (!mercado.Contains(j)) return "Esse jogador não está mais no mercado.";
            if (elenco.Count >= ElencoMax) return "Elenco cheio (" + ElencoMax + "). Venda alguém ou suba a Gaming House.";
            int custo = luvas ?? j.Preco;
            if (dinheiro < custo) return "Dinheiro insuficiente.";
            ultimaVenda = null;
            Lanca("Contratação de " + j.nome, -custo);
            if (salario.HasValue) j.salarioAcordado = salario.Value;
            negociacoes.Remove(j);
            mercado.Remove(j); j.potencialRevelado = true; elenco.Add(j); GuardarInicio(j);
            Contratos.Assinar(j, anos);   // decisão 0035: 1 a 3 temporadas, com a cláusula de rescisão
            j.moral = Provisorio.MORAL_CONTRATADO; j.entrosamento = Provisorio.ENTROSAMENTO_CONTRATADO;
            return null;
        }

        public string Vender(Jogador j)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (elenco.Count <= Provisorio.TITULARES) return "É preciso manter pelo menos " + Provisorio.TITULARES + " jogadores.";
            ultimaVendaIndice = elenco.IndexOf(j);
            Lanca("Venda de " + j.nome, j.Venda);
            ultimaVendaLancamento = extrato[0]; ultimaVenda = j;
            elenco.Remove(j);
            return null;
        }

        /// <summary>Desfaz a última venda: o jogador volta para a mesma posição e o dinheiro e o extrato voltam ao que eram.</summary>
        public string DesfazerVenda()
        {
            if (ultimaVenda == null) return "Não há venda para desfazer.";
            if (elenco.Count >= ElencoMax) return "Elenco cheio: não há vaga para o jogador voltar.";
            dinheiro -= ultimaVenda.Venda;
            extrato.Remove(ultimaVendaLancamento);
            elenco.Insert(Math.Min(ultimaVendaIndice, elenco.Count), ultimaVenda);
            ultimaVenda = null;
            return null;
        }

        /// <summary>Sobe (delta = -1) ou desce (+1) o jogador na lista; os 5 primeiros são os titulares.</summary>
        public void Mover(Jogador j, int delta)
        {
            int i = elenco.IndexOf(j), k = i + delta;
            if (i < 0 || k < 0 || k >= elenco.Count) return;
            elenco[i] = elenco[k]; elenco[k] = j; ultimaVenda = null;
        }

        /// <summary>Troca de lugar dois jogadores do elenco (titular com reserva, ou titular com titular).</summary>
        public void Trocar(Jogador a, Jogador b)
        {
            int i = elenco.IndexOf(a), k = elenco.IndexOf(b);
            if (i < 0 || k < 0 || i == k) return;
            elenco[i] = b; elenco[k] = a; ultimaVenda = null;
        }

        /// <summary>Escala o reserva no lugar do titular mais fraco.</summary>
        public void TornarTitular(Jogador j)
        {
            if (Titular(j)) return;
            var sai = Titulares.OrderBy(t => t.Overall).First();
            Trocar(j, sai);
        }

        /// <summary>Manda o titular para as reservas, no lugar do melhor reserva.</summary>
        public void MandarParaReserva(Jogador j)
        {
            if (!Titular(j)) return;
            var entra = elenco.Skip(Provisorio.TITULARES).OrderByDescending(r => r.Overall).FirstOrDefault();
            if (entra != null) Trocar(j, entra);
        }

        public string SubirGamingHouse()
        {
            if (nivelGH >= GamingHouse.MAX) return "A Gaming House já está no nível máximo.";
            int custo = CustoProximoNivelGH;
            if (fas < FasProximoNivelGH) return "Faltam " + Fmt.Fas(FasProximoNivelGH - fas) + " fãs para este nível.";
            if (dinheiro < custo) return "Dinheiro insuficiente.";
            ultimaVenda = null;
            Lanca("Gaming House: " + GamingHouse.De(nivelGH + 1).nome + " (nível " + (nivelGH + 1) + ")", -custo);
            nivelGH++; AjustarScouts();
            Anotar("A Gaming House subiu para o nível " + nivelGH + " (" + GH.nome + ").");
            return null;
        }

        // ------------------------------------------------------------------------------------------ treino (decisão 0013)
        public int MaxSessoes => GH.maxSessoes;
        public int MaxColetivas => GH.coletivas;
        public bool TreinoLiberado => nivelGH >= 2;

        public string DefinirTreino(Jogador j, int foco, int sessoes)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (sessoes > MaxSessoes) return "A Gaming House atual permite no máximo " + MaxSessoes + " sessões por semana.";
            if (sessoes > 0 && nivelGH < 2 && sessoes > 2) return "Suba a Gaming House para treinar mais.";
            j.sessoes = Math.Max(0, sessoes); j.foco = sessoes == 0 ? -1 : foco;
            if (sessoes == 0) j.ladoFoco = -1;
            return null;
        }

        /// <summary>Treino por lado (decisão 0024): -1 = os dois lados, 0 = TR, 1 = CT. Sem sessões, não há o que treinar.</summary>
        public string DefinirLadoTreino(Jogador j, int lado)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (lado >= 0 && j.sessoes == 0) return "Ponha ao menos uma sessão por semana para treinar um lado.";
            j.ladoFoco = Math.Clamp(lado, -1, 1);
            return null;
        }

        /// <summary>Quanto o domínio do lado em foco sobe numa semana (0 se o treino é dos dois lados). Cansaço alto rende menos.</summary>
        public float PreverLado(Jogador j)
        {
            if (j.ladoFoco < 0 || j.sessoes <= 0) return 0f;
            int sess = Math.Min(j.sessoes, MaxSessoes);
            float atual = j.Lado(j.ladoFoco == 0);
            float teto = Math.Clamp((100f - atual) / 40f, 0f, 1f);
            float fadiga = 1f - 0.4f * Math.Clamp((j.cansaco - 60f) / 40f, 0f, 1f);
            return Provisorio.GANHO_LADO * sess * teto * GH.ganhoTreino * fadiga;
        }

        public string DefinirColetivas(int n)
        {
            if (n > MaxColetivas) return MaxColetivas == 0 ? "Sessões coletivas exigem o Centro de performance (nível 4)." : "No máximo " + MaxColetivas + " sessões coletivas.";
            sessoesColetivas = Math.Max(0, n); return null;
        }

        public string DefinirEstudoMapa(int mapa)
        {
            if (mapa >= 0 && !GH.estudoMapa) return "O estudo de mapa exige a Sala de treino (nível 2).";
            estudoMapa = mapa; return null;
        }

        static float FatorIdade(int idade) => idade <= 19 ? 1.5f : idade <= 22 ? 1.2f : idade <= 26 ? 0.9f : idade <= 29 ? 0.6f : 0.4f;

        /// <summary>O que uma semana de treino faria com o jogador (sem aplicar): usado na tela de treino para o manager ver o contrapeso.</summary>
        public (float ganho, float preparoDepois, float cansacoDepois) PreverSemana(Jogador j)
        {
            int sess = Math.Min(j.sessoes, MaxSessoes);
            float carga = (float)Math.Pow(sess, 1.15) * 4.5f + j.mapasNaSemana * 6f + (estudoMapa >= 0 && GH.estudoMapa ? 2f : 0f) + (MaxColetivas > 0 ? Math.Min(sessoesColetivas, MaxColetivas) * 3f : 0f);
            float rec = 18f * GH.recuperacao + (sess == 0 && j.mapasNaSemana == 0 ? 8f : 0f);
            float cansaco = Math.Clamp(j.cansaco + carga - rec, 0f, 100f);
            float coletivas = Math.Min(sessoesColetivas, MaxColetivas);
            float preparo = Math.Min(100f, j.preparo * 0.65f + sess * 9f + coletivas * 6f);
            float ganho = 0f;
            if (j.foco >= 0 && sess > 0)
            {
                float teto = Math.Clamp((j.potencial - j.Valor(j.foco)) / 25f, 0f, 1f);
                float fadiga = 1f - 0.4f * Math.Clamp((cansaco - 60f) / 40f, 0f, 1f);
                ganho = Provisorio.GANHO_POR_SESSAO * sess * FatorIdade(j.idade) * teto * GH.ganhoTreino * fadiga * (j.ladoFoco >= 0 ? Provisorio.LADO_FOCO_ATRIB : 1f);
            }
            return (ganho, preparo, cansaco);
        }

        void ProcessarTreino(ResumoSemana res)
        {
            foreach (var j in elenco)
            {
                var (ganho, preparo, cansaco) = PreverSemana(j);
                if (j.foco >= 0 && ganho > 0) j.Definir(j.foco, Math.Min(j.potencial, j.Valor(j.foco) + ganho));
                float gl = PreverLado(j);
                if (gl > 0) { if (j.ladoFoco == 0) j.ladoTR = Math.Min(100f, j.ladoTR + gl); else j.ladoCT = Math.Min(100f, j.ladoCT + gl); }
                // o lado que ninguém treina esfria devagar para a média (50): especializar tem preço
                j.ladoTR += (50f - j.ladoTR) * 0.01f; j.ladoCT += (50f - j.ladoCT) * 0.01f;
                // evolução natural pela idade: jovens crescem um pouco sozinhos, veteranos declinam (o reflexo cai antes)
                if (j.idade <= 24)
                {
                    for (int a = 0; a < 5; a++) if (j.Valor(a) < j.potencial) j.Definir(a, j.Valor(a) + (25 - j.idade) * 0.008f);
                }
                else if (j.idade >= 28)
                {
                    for (int a = 0; a < 5; a++) j.Definir(a, j.Valor(a) - (j.idade - 27) * (a == (int)Atrib.Reflexo ? 0.012f : 0.006f));
                }
                j.preparo = preparo; j.cansaco = cansaco; j.mapasNaSemana = 0;
            }
            // moral e entrosamento (decisão 0019): titular joga e se integra; reserva perde ânimo
            var tit = new HashSet<Jogador>(Titulares);
            float coletivas = MaxColetivas > 0 ? Math.Min(sessoesColetivas, MaxColetivas) : 0;
            foreach (var j in elenco)
            {
                bool t = tit.Contains(j);
                float queda = Provisorio.MORAL_RESERVA_SEMANA * (j.traco == Traco.Estrela ? 2f : 1f);
                j.moral = Math.Clamp(j.moral + (t ? Provisorio.MORAL_TITULAR_SEMANA : -queda), 0f, 100f);
                if (j.promessaSemanas > 0)   // promessa de titular (decisão 0026): cumprir rende moral, descumprir tira
                {
                    j.promessaSemanas--;
                    if (!t) j.moral = Math.Clamp(j.moral - 6f, 0f, 100f); else if (j.promessaSemanas == 0) j.moral = Math.Clamp(j.moral + 5f, 0f, 100f);
                }
                j.entrosamento = Math.Clamp(j.entrosamento + (t ? Provisorio.ENTROSAMENTO_TITULAR + coletivas * Provisorio.ENTROSAMENTO_COLETIVA : Provisorio.ENTROSAMENTO_RESERVA), 0f, 100f);
            }
            // estudo de mapa: o time ganha afinidade com o mapa escolhido e perde um pouco com os outros
            if (estudoMapa >= 0 && GH.estudoMapa)
            {
                var af = mundo.Clube.afinidade;
                af[estudoMapa] = Math.Min(95, af[estudoMapa] + 3);
                res.linhas.Add("Estudo de mapa: afinidade com " + Mapas.NOMES[estudoMapa] + " " + af[estudoMapa] + ".");
            }
        }

        // ------------------------------------------------------------------------------------------ finanças (decisão 0015)
        public int LimiteEmprestimo => Provisorio.LIMITE_EMPRESTIMO[(int)CircuitoClube];
        public int Devido => emprestimos.Sum(e => e.parcela * e.restantes);
        public int EmprestimoDisponivel => Math.Max(0, LimiteEmprestimo - emprestimos.Sum(e => e.principal));

        public string PegarEmprestimo(int valor)
        {
            if (valor <= 0) return "Escolha um valor.";
            if (valor > EmprestimoDisponivel) return "O banco só empresta até " + Fmt.Dinheiro(EmprestimoDisponivel) + " agora (limite do circuito " + Mundo.NomeCircuito(CircuitoClube) + ").";
            int parcela = (int)Math.Ceiling(valor * (1f + Provisorio.JURO_EMPRESTIMO) / Provisorio.PARCELAS_EMPRESTIMO / 10f) * 10;
            emprestimos.Add(new Emprestimo { principal = valor, parcela = parcela, restantes = Provisorio.PARCELAS_EMPRESTIMO });
            Lanca("Empréstimo recebido", valor);
            Anotar("Empréstimo de " + Fmt.Dinheiro(valor) + " em " + Provisorio.PARCELAS_EMPRESTIMO + " parcelas de " + Fmt.Dinheiro(parcela) + ".");
            return null;
        }

        /// <summary>Quanto custa quitar um empréstimo agora (o principal que falta, sem os juros).</summary>
        public int CustoQuitar(Emprestimo e) => (int)Math.Round(e.parcela * e.restantes / (1f + Provisorio.JURO_EMPRESTIMO));

        public string Quitar(Emprestimo e)
        {
            if (!emprestimos.Contains(e)) return "Esse empréstimo já foi pago.";
            int custo = CustoQuitar(e);
            if (dinheiro < custo) return "Dinheiro insuficiente para quitar (" + Fmt.Dinheiro(custo) + ").";
            Lanca("Empréstimo quitado antes do prazo", -custo);
            emprestimos.Remove(e);
            return null;
        }

        // ------------------------------------------------------------------------------------------ tempo
        /// <summary>Dá para avançar uma semana? Não, se há série marcada para esta semana (jogue, pule ou prepare) ou se o clube faliu.</summary>
        public bool PodeAvancar => !Falido && !TemJogoAgora && serie == null;

        /// <summary>Passa uma semana: treino, cansaço, scouts, finanças, os campeonatos do mundo. Devolve o resumo (ou null se não deu).</summary>
        public ResumoSemana AvancarSemana()
        {
            if (!PodeAvancar) return null;
            ultimaVenda = null;
            var res = new ResumoSemana { semana = Semana };

            ProcessarTreino(res);
            ProcessarScouts(res);
            ProcessarContratos(res);

            // finanças da semana
            int sal = SalariosPorSemana, manut = GH.manutencao, patro = PatrocinioPorSemana, parcelas = 0;
            foreach (var e in emprestimos.ToList()) { parcelas += e.parcela; e.restantes--; if (e.restantes <= 0) emprestimos.Remove(e); }
            Lanca("Salários e manutenção da Gaming House", -(sal + manut));
            Lanca("Patrocínio", patro);
            if (parcelas > 0) Lanca("Parcelas de empréstimo", -parcelas);
            res.receitas = patro; res.despesas = sal + manut + parcelas;

            // o mundo (campeonatos de todos os circuitos)
            EquipeClube.forca = Forca;
            var (terminados, vira) = mundo.AvancarSemana();
            foreach (var camp in terminados) Premiar(camp, res);
            res.semana = Semana; res.temporada = Temporada;
            if (vira) { FimDeTemporada(res); res.virouTemporada = true; }
            foreach (var c in mundo.DoClube.Where(c => c.iniciado && !c.terminado && c.semanaInicio == Semana)) res.linhas.Add("Começou: " + c.nome + ".");
            foreach (var c in mundo.DoClube.Where(c => !c.iniciado && c.semanaInicio == Semana + 2)) res.linhas.Add("Em 2 semanas começa: " + c.nome + ".");

            foreach (var levado in RenovarMercado(2).Where(x => x.Overall >= Contratos.OVERALL_COBICADO))
                res.linhas.Add(levado.nome + " (" + levado.Overall + ") assinou com outro clube e saiu do mercado.");

            // falência: saldo negativo abre um prazo; passado o prazo, game over (decisão 0015)
            if (dinheiro < 0)
            {
                semanasNegativo++;
                if (semanasNegativo >= Provisorio.SEMANAS_ATE_FALENCIA) { falido = true; res.linhas.Add("O clube faliu."); }
                else res.linhas.Add("Saldo negativo há " + semanasNegativo + " semana(s). Faltam " + (Provisorio.SEMANAS_ATE_FALENCIA - semanasNegativo) + " para a falência.");
            }
            else semanasNegativo = 0;
            foreach (var l in res.linhas) Anotar(l);
            return res;
        }

        void Premiar(Campeonato camp, ResumoSemana res)
        {
            int pos = camp.Colocacao(0), premio = camp.PremioPara(pos); float pts = camp.PontosPara(pos);
            if (premio > 0) Lanca("Prêmio: " + camp.nome + " (" + Fmt.Ordinal(pos) + " lugar)", premio);
            int fasTitulo = Fas.Titulo(pos, CircuitoClube);
            if (fasTitulo > 0) { MudarFas(fasTitulo); Anotar(camp.nome + ": " + Fmt.Ordinal(pos) + " lugar traz " + Fmt.Fas(fasTitulo) + " fãs novos."); }
            var rc = new ResultadoCampeonato { nome = camp.nome, posicao = pos, times = camp.times.Count, premio = premio, pontos = pts, semana = Semana };
            titulos.Insert(0, rc); res?.campeonatos.Add(rc);
            res?.linhas.Add(camp.nome + ": " + Fmt.Ordinal(pos) + " lugar, prêmio " + Fmt.Dinheiro(premio) + ", +" + (int)pts + " pontos de ranking.");
        }

        void ProcessarScouts(ResumoSemana res)
        {
            AjustarScouts();
            foreach (var s in scouts)
            {
                if (s.tipo == 2 && s.alvo >= 0)
                {
                    int antes = NivelIntel(s.alvo);
                    intel[s.alvo] = SemanasDeIntel(s.alvo) + 1f;
                    int depois = NivelIntel(s.alvo);
                    if (depois > antes) res.linhas.Add("Scouts: informação nível " + depois + " sobre " + mundo.equipes[s.alvo].nome + ".");
                }
                else if (s.tipo == 1)
                {
                    var alvos = mercado.Where(j => !j.potencialRevelado).OrderByDescending(j => j.Overall).Take(3).ToList();
                    foreach (var j in alvos) j.potencialRevelado = true;
                    if (alvos.Count > 0) res.linhas.Add("Scouts: potencial revelado de " + string.Join(", ", alvos.Select(j => j.nome)) + ".");
                }
            }
        }

        void FimDeTemporada(ResumoSemana res)
        {
            foreach (var j in elenco.ToList())
            {
                j.idade++;
                if (j.idade >= 36 && elenco.Count > Provisorio.TITULARES) { elenco.Remove(j); res.linhas.Add(j.nome + " se aposentou aos " + j.idade + " anos."); }
            }
            foreach (var j in elenco) GuardarInicio(j);
            foreach (var k in intel.Keys.ToList()) intel[k] *= 0.5f;   // a informação envelhece
            GerarMercado();
            res.linhas.Add("Começa a temporada " + Temporada + ". O clube joga o circuito " + Mundo.NomeCircuito(CircuitoClube) + " (ranking " + PosicaoRanking + ").");
        }

        // ------------------------------------------------------------------------------------------ série, veto e estratégia (decisão 0014)
        /// <summary>Abre a série do próximo confronto (se já está na hora). Devolve null se não há série para preparar.</summary>
        public Serie PrepararSerie()
        {
            if (serie != null) return serie;
            var p = Proximo();
            if (!p.TemJogo || p.semana > Semana) return null;
            int primeiro = mundo.PosicaoRanking(0) <= mundo.PosicaoRanking(p.adv.id) ? 0 : 1;   // o melhor colocado veta primeiro
            serie = new Serie { camp = p.camp, conf = p.conf, adv = p.adv, veto = new Veto(p.conf.md, primeiro) };
            AvancarVetoIA();
            NoticiaAntesDaSerie(p.adv);
            return serie;
        }

        // ------------------------------------------------------------------------------------------ rivalidades (decisão 0021)
        public const float CLASSICO_MORAL = 2f;   // no clássico a moral muda o dobro
        public const int MARCO_MVP_1 = 5, MARCO_MVP_2 = 10, MARCO_MVP_3 = 20, IDADE_REVELACAO = 21;

        /// <summary>Retrospecto contra cada adversário já enfrentado, do mais rival para o menos.</summary>
        public List<Retrospecto> Retrospectos => Rivalidades.Todos(historico);
        public Retrospecto Contra(string adv) => Retrospectos.FirstOrDefault(x => x.adversario == adv) ?? new Retrospecto { adversario = adv };
        public List<Retrospecto> Rivais => Retrospectos.Where(x => x.Rival).ToList();
        public bool Classico(Equipe adv) => adv != null && Contra(adv.nome).Rival;

        void NoticiaAntesDaSerie(Equipe adv)
        {
            var rt = Contra(adv.nome);
            if (rt.Rival) Anotar("Clássico contra " + adv.nome + ": " + rt.Texto + ". A moral pesa o dobro nesta série.");
            else if (rt.ultimaEliminacao != null) Anotar("Revanche: " + adv.nome + " eliminou o clube na " + rt.ultimaEliminacao + ".");
        }

        void NoticiasDepoisDaSerie(Retrospecto antes, Retrospecto depois, Resultado r, bool classico)
        {
            string adv = r.adversario;
            if (!antes.Rival && depois.Rival) Anotar(adv + " virou rival do clube (" + depois.Texto + ").");
            if (r.venceuSerie && depois.sequencia >= 3) Anotar(depois.sequencia + "ª série seguida vencida contra " + adv + ".");
            if (r.venceuSerie && antes.sequencia <= -2) Anotar("Fim do tabu: primeira série vencida contra " + adv + " depois de " + (-antes.sequencia) + " derrotas seguidas.");
            if (!r.venceuSerie && Rivalidades.Eliminado(r)) Anotar(adv + " eliminou o clube na " + Rivalidades.Fase(r) + " da " + r.campeonato + ".");
            if (classico && r.venceuSerie)
            {
                Lanca("Bônus de clássico (patrocinadores)", PatrocinioPorSemana);
                Anotar("Vitória no clássico contra " + adv + ": os patrocinadores pagam " + Fmt.Dinheiro(PatrocinioPorSemana) + " de bônus.");
            }
        }

        // ------------------------------------------------------------------------------------------ playbook (decisão 0022)
        public const int VAGAS_PLAYBOOK_MAX = 5;
        /// <summary>Vagas de favorita e de proibida por lado: 1 no nível 1 da Gaming House, +1 por nível, até 5.</summary>
        public int VagasPlaybook => Math.Min(VAGAS_PLAYBOOK_MAX, nivelGH);

        public enum MarcaJogada { Nenhuma, Favorita, Proibida }
        public MarcaJogada Marca(string jogada, bool tr) =>
            (tr ? favoritasTR : favoritasCT).Contains(jogada) ? MarcaJogada.Favorita : (tr ? proibidasTR : proibidasCT).Contains(jogada) ? MarcaJogada.Proibida : MarcaJogada.Nenhuma;

        /// <summary>Marca uma jogada como favorita, proibida ou nenhuma. Devolve a mensagem de erro, ou null se deu certo.</summary>
        public string MarcarJogada(string jogada, bool tr, MarcaJogada marca)
        {
            var fav = tr ? favoritasTR : favoritasCT; var proi = tr ? proibidasTR : proibidasCT;
            if (marca == MarcaJogada.Favorita && !fav.Contains(jogada) && fav.Count >= VagasPlaybook) return "Sem vaga de favorita (" + VagasPlaybook + " no nível " + nivelGH + " da Gaming House). Desmarque outra ou suba a Gaming House.";
            if (marca == MarcaJogada.Proibida && !proi.Contains(jogada) && proi.Count >= VagasPlaybook) return "Sem vaga de proibida (" + VagasPlaybook + " no nível " + nivelGH + " da Gaming House). Desmarque outra ou suba a Gaming House.";
            fav.Remove(jogada); proi.Remove(jogada);
            if (marca == MarcaJogada.Favorita) fav.Add(jogada); else if (marca == MarcaJogada.Proibida) proi.Add(jogada);
            return null;
        }

        int Aff(Equipe e, int m) => e.afinidade[m];

        /// <summary>Quanto o clube sabe da afinidade do adversário com um mapa (sem informação nível 2, assume neutro).</summary>
        public int AfinidadeConhecida(Equipe adv, int mapa) => NivelIntel(adv.id) >= 2 ? adv.afinidade[mapa] : 50;

        int EscolhaIA(int quem, bool pick)
        {
            var v = serie.veto; var adv = serie.adv; var eu = mundo.Clube;
            Func<int, double> nota = m =>
            {
                double minha = quem == 0 ? Aff(eu, m) : Aff(adv, m);
                double deles = quem == 0 ? AfinidadeConhecida(adv, m) : Aff(eu, m);
                double ruido = rng.NextDouble() * 6;
                return pick ? minha - 0.4 * deles + ruido : deles - 0.4 * minha + ruido;   // bane o mapa em que o outro é forte; escolhe o em que eu sou forte
            };
            return v.restantes.OrderByDescending(nota).First();
        }

        /// <summary>Faz as jogadas do adversário até chegar a vez do manager (ou o fim do veto).</summary>
        public void AvancarVetoIA()
        {
            if (serie == null) return;
            var v = serie.veto;
            while (!v.Completo && v.Atual.quem == 1) v.Aplicar(EscolhaIA(1, v.Atual.pick));
            if (v.Completo) serie.mapas = v.OrdemDeJogo();
        }

        /// <summary>O manager veta (bane ou escolhe) um mapa na sua vez.</summary>
        public string Vetar(int mapa)
        {
            if (serie == null) return "Não há série em preparação.";
            var v = serie.veto;
            if (v.Completo) return "O veto já terminou.";
            if (v.Atual.quem != 0) return "Não é a sua vez.";
            if (!v.restantes.Contains(mapa)) return "Esse mapa já foi vetado ou escolhido.";
            v.Aplicar(mapa);
            AvancarVetoIA();
            return null;
        }

        /// <summary>O jogo veta pelo manager (pela afinidade do time e pelo que se sabe do adversário).</summary>
        public void VetoAutomatico()
        {
            if (serie == null) return;
            var v = serie.veto;
            while (!v.Completo) v.Aplicar(EscolhaIA(v.Atual.quem, v.Atual.pick));
            serie.mapas = v.OrdemDeJogo();
        }

        public bool VetoCompleto => serie != null && serie.veto.Completo;

        // ------------------------------------------------------------------------------------------ partida
        /// <summary>
        /// Monta a partida do mapa atual da série com o elenco do manager no lugar do time A, já com a forma de cada jogador
        /// (preparo, cansaço, afinidade com o mapa, contra-estratégia) e a estratégia escolhida, e com o adversário no lugar do B.
        /// Nome e habilidade vão para a simulação: é assim que o que o manager decidiu aparece na partida.
        /// </summary>
        public CSM.Sim.Partida MontarPartida(int seedPartida)
        {
            var adv = Adversario; int mapa = serie != null ? Math.Max(0, serie.MapaAtual) : 0;
            var pesosA = Estilos.Pesos(estilo);
            if (favoritasTR.Count + favoritasCT.Count > 0) pesosA.favoritas = new HashSet<string>(favoritasTR.Concat(favoritasCT));
            if (proibidasTR.Count + proibidasCT.Count > 0) pesosA.proibidas = new HashSet<string>(proibidasTR.Concat(proibidasCT));
            var pt = new CSM.Sim.Partida(seedPartida, pesosA, Estilos.Pesos(adv != null ? adv.estilo : Estilo.Equilibrada));
            pt.A.nome = clube;
            var vagas = pt.A.js;
            var livres = Titulares.ToList();
            var porVaga = new Jogador[vagas.Length];
            for (int v = 0; v < vagas.Length; v++)
            {
                var j = livres.FirstOrDefault(x => x.funcao == vagas[v].roleT);
                if (j != null) { porVaga[v] = j; livres.Remove(j); }
            }
            for (int v = 0; v < vagas.Length; v++)
                if (porVaga[v] == null) { porVaga[v] = livres[0]; livres.RemoveAt(0); }
            for (int v = 0; v < vagas.Length; v++)
            {
                var j = porVaga[v];
                vagas[v].nome = j.nome; vagas[v].traco = j.traco;
                vagas[v].bonusTR = j.BonusLado(true); vagas[v].bonusCT = j.BonusLado(false);
                vagas[v].skill = j.SkillEfetiva(MultEfetivo(j, mapa, adv));
                float e = EntrosamentoTime - (j.traco == Traco.Estrela ? Provisorio.ENTROSAMENTO_ESTRELA : 0f);
                vagas[v].skill.entrosamento = Math.Clamp(e / 100f, 0f, 1f);
            }
            if (adv != null)
            {
                pt.B.nome = adv.nome;
                float v100 = Math.Min(100f, ForcaEfetivaAdv(adv, mapa));
                var r = new Random(seedPartida);
                for (int v = 0; v < pt.B.js.Length; v++)
                {
                    float x = Math.Clamp(v100 + (float)(r.NextDouble() - 0.5) * 8f, 0f, 100f);
                    var jg = new Jogador(); for (int a = 0; a < 5; a++) jg.Definir(a, x);
                    pt.B.js[v].skill = jg.skill;
                    if (v < adv.jogadores.Count) pt.B.js[v].nome = adv.jogadores[v].nome;
                }
            }
            return pt;
        }

        /// <summary>Prepara uma partida para a cena 3D assistir (a cena pega com <see cref="TomarPendente"/>).</summary>
        public void PrepararAssistir(int seedPartida) { pendente = MontarPartida(seedPartida); emPartida = true; }

        public static CSM.Sim.Partida TomarPendente()
        {
            var c = Atual; if (c == null || c.pendente == null) return null;
            var p = c.pendente; c.pendente = null; return p;
        }

        /// <summary>Rounds do perdedor num mapa pulado (o vencedor faz 13), sorteados da distribuição medida na simulação (decisão 0036).
        /// <paramref name="pVencedor"/>: chance que o vencedor tinha; quanto mais folgada, mais o placar é largo.</summary>
        public static int PlacarPerdedor(Random r, double pVencedor)
        {
            // faixas do perdedor: 0-3, 4-6, 7-9, 10-11, 12 (13x12). Equilibrado = medido com d<=15; folgado = medido com d>=30.
            double[] equil = { 0.05, 0.22, 0.35, 0.26, 0.12 }, folga = { 0.28, 0.38, 0.20, 0.14, 0.00 };
            double t = Math.Max(0, Math.Min(1, (pVencedor - 0.75) / 0.2)), x = r.NextDouble(), acc = 0;
            int faixa = 4;
            for (int i = 0; i < 5; i++) { acc += equil[i] * (1 - t) + folga[i] * t; if (x < acc) { faixa = i; break; } }
            int[] lo = { 0, 4, 7, 10, 12 }, hi = { 3, 6, 9, 11, 12 };
            return r.Next(lo[faixa], hi[faixa] + 1);
        }

        /// <summary>Pula o mapa atual: o resultado sai por probabilidade, com a forma de agora (decisão 0008).</summary>
        public Resultado PularMapa()
        {
            if (serie == null && PrepararSerie() == null) return null;
            if (!serie.veto.Completo) VetoAutomatico();
            int mapa = serie.MapaAtual; if (mapa < 0) return null;
            double p = ChanceMapa(serie.adv, mapa);
            bool venceu = rng.NextDouble() < p;
            int perdedor = PlacarPerdedor(rng, venceu ? p : 1 - p);
            return Registrar(venceu, venceu ? 13 : perdedor, venceu ? perdedor : 13, false);
        }

        /// <summary>Pula a série inteira (o veto sai automático) e devolve o resultado do último mapa.</summary>
        public Resultado PularSerie()
        {
            Resultado ultimo = null;
            if (serie == null && PrepararSerie() == null) return null;
            int guarda = 0;
            while (serie != null && guarda++ < 10) ultimo = PularMapa();
            return ultimo;
        }

        /// <summary>Números por jogador de uma partida pulada: ESTIMADOS a partir do geral de cada titular e do placar (não há simulação).</summary>
        List<Desempenho> Estimar(bool venceu, int rounds)
        {
            var l = new List<Desempenho>();
            foreach (var j in Titulares)
            {
                float forma = 0.55f + (j.Overall - 65) / 100f, lado = venceu ? 1.12f : 0.88f;
                float ruido() => 0.8f + (float)rng.NextDouble() * 0.4f;
                int k = Math.Max(0, (int)Math.Round(rounds * forma * lado * ruido()));
                int d = Math.Max(1, (int)Math.Round(rounds * 0.72f * (venceu ? 0.9f : 1.1f) * ruido()));
                l.Add(new Desempenho { nome = j.nome, funcao = j.funcao, k = k, d = Math.Min(d, rounds), hs = (int)Math.Round(k * (0.3f + 0.3f * (float)rng.NextDouble())),
                    dano = (int)Math.Round(k * 82 + rounds * 14 * ruido()), fk = rng.Next(0, Math.Max(1, k / 4 + 1)), rounds = rounds });
            }
            return l;
        }

        /// <summary>Fecha o mapa atual da série: guarda o resultado, cansa e dá experiência aos jogadores, e (se a série acabou) leva o
        /// resultado ao campeonato. <paramref name="desempenho"/>: os números reais da simulação (assistida); null = estimar.</summary>
        public const int JOGADAS_GUARDADAS = 5;   // últimas jogadas da partida no histórico de cada jogador (decisão 0020)

        public Resultado Registrar(bool venceu, int meus, int deles, bool assistida, List<Desempenho> desempenho = null, Destaques.Lance jogada = null)
        {
            ultimaVenda = null;
            if (serie == null) PrepararSerie();
            var s = serie;
            int mapa = s != null ? Math.Max(0, s.MapaAtual) : 0;
            var adv = s?.adv;
            var r = new Resultado
            {
                numPartida = ++numPartida, semana = Semana, adversario = adv != null ? adv.nome : "?", campeonato = s?.camp.nome ?? "", rotulo = s?.conf.rotulo ?? "",
                mapa = Mapas.NOMES[mapa], venceu = venceu, assistida = assistida, meus = meus, deles = deles,
                saldoAntes = dinheiro, forcaMinha = Forca, forcaDeles = adv != null ? adv.Forca : 0,
                estimado = desempenho == null, desempenho = desempenho ?? Estimar(venceu, meus + deles),
            };
            // clássico (decisão 0021): contra um rival a moral muda o dobro; o retrospecto de antes serve para as notícias
            var antes = adv != null ? Contra(adv.nome) : null;
            bool classico = antes != null && antes.Rival;
            float pesoMoral = classico ? CLASSICO_MORAL : 1f;
            // quem jogou fica cansado e ganha um pouco de experiência (jovens aprendem mais)
            foreach (var j in Titulares)
            {
                j.mapasNaSemana++;
                j.moral = Math.Clamp(j.moral + pesoMoral * (venceu ? Provisorio.MORAL_VITORIA : -Provisorio.MORAL_DERROTA), 0f, 100f);
                if (j.idade <= 28) { int a = rng.Next(5); if (j.Valor(a) < j.potencial) j.Definir(a, j.Valor(a) + 0.08f * FatorIdade(j.idade)); }
            }
            var af = mundo.Clube.afinidade; af[mapa] = Math.Min(95, af[mapa] + 1);   // jogar no mapa dá afinidade
            r.fas = Fas.Mapa(venceu, r.forcaMinha, r.forcaDeles, CircuitoClube, sequencia);   // fãs (decisão 0037)
            sequencia = venceu ? Math.Max(sequencia, 0) + 1 : Math.Min(sequencia, 0) - 1;

            // melhores momentos (decisão 0020): MVP do mapa e jogada da partida entram no histórico do jogador e nas notícias
            var mvp = r.Melhor; var jm = mvp != null ? elenco.FirstOrDefault(j => j.nome == mvp.nome) : null;
            if (jm != null)
            {
                jm.mvps++;
                if (jm.mvps == MARCO_MVP_1 || jm.mvps == MARCO_MVP_2 || jm.mvps == MARCO_MVP_3) Anotar(jm.nome + " chega a " + jm.mvps + " MVPs de mapa pelo clube.");
            }
            if (jogada != null)
            {
                r.jogada = jogada.Descricao;
                var autor = Titulares.FirstOrDefault(j => j.nome == jogada.jogador);
                r.jogadaNossa = autor != null;
                if (autor != null)
                {
                    autor.jogadas.Insert(0, "Semana " + Semana + " contra " + r.adversario + ": " + jogada.Descricao);
                    while (autor.jogadas.Count > JOGADAS_GUARDADAS) autor.jogadas.RemoveAt(autor.jogadas.Count - 1);
                    Anotar("Jogada da partida contra " + r.adversario + ": " + jogada.Descricao + ".");
                }
            }

            if (s != null)
            {
                s.resultados.Add((meus, deles));
                r.serieMeus = s.MinhasVit; r.serieDeles = s.DelesVit; r.serieMd = s.Md;
                for (int i = 0; i < s.resultados.Count; i++) r.mapasDaSerie.Add((Mapas.NOMES[s.mapas[i]], s.resultados[i].meus, s.resultados[i].deles));
                if (s.Encerrada)
                {
                    r.serieEncerrada = true; r.venceuSerie = s.MinhasVit > s.DelesVit;
                    if (r.venceuSerie) r.fas += Fas.Serie(CircuitoClube);
                    // MVP da série: quem somou a maior nota nos mapas dela (os anteriores estão no começo do histórico)
                    var mapasSerie = historico.Take(s.resultados.Count - 1).Append(r);
                    var ms = mapasSerie.SelectMany(x => x.desempenho).GroupBy(x => x.nome).OrderByDescending(g => g.Sum(x => x.Nota)).FirstOrDefault();
                    var jms = ms != null ? elenco.FirstOrDefault(j => j.nome == ms.Key) : null;
                    if (jms != null)
                    {
                        r.mvpSerie = jms.nome; jms.mvpsSerie++;
                        if (jms.idade <= IDADE_REVELACAO) Anotar("Revelação: " + jms.nome + ", " + jms.idade + " anos, é o MVP da série contra " + r.adversario + ".");
                        else if (s.Md > 1) Anotar("MVP da série contra " + r.adversario + ": " + jms.nome + ".");
                    }
                    bool souA = s.conf.a == 0;
                    s.camp.RegistrarClube(s.conf, souA ? s.MinhasVit : s.DelesVit, souA ? s.DelesVit : s.MinhasVit);
                    var fim = new ResumoSemana();
                    foreach (var camp in mundo.ApurarFim().Where(c => c.times.Contains(0))) Premiar(camp, fim);
                    foreach (var l in fim.linhas) Anotar(l);
                    r.campeonato = s.camp.nome;
                    serie = null;
                }
            }
            MudarFas(r.fas);
            emPartida = false; pendente = null;
            historico.Insert(0, r); naoVisto = r;
            if (r.serieEncerrada && antes != null) NoticiasDepoisDaSerie(antes, Contra(r.adversario), r, classico);
            return r;
        }
    }
}
