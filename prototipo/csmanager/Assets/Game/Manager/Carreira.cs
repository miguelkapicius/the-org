using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Camada de gestão do protótipo (clube, elenco, mercado, finanças, Gaming House).
    //
    // IMPORTANTE: o GDD ainda não decidiu quase nada daqui (ver perguntas-abertas.md, seção 2 "Lado manager").
    // Todos os números e regras abaixo são PROVISÓRIOS, só para a interface ter com o que trabalhar. Ficam juntos em
    // `Provisorio` de propósito: quando o GDD decidir, é um lugar só para trocar. Sem dependência do Unity (só do Sim),
    // então dá para rodar no Headless.
    // ============================================================================================================

    /// <summary>Tudo que é número inventado para o protótipo andar. Nada aqui é decisão de design.</summary>
    public static class Provisorio
    {
        public const int DINHEIRO_INICIAL = 400000;
        public const int TITULARES = 5;            // a partida é 5v5 (decisão 0005); o resto é reserva
        public const int ELENCO_MAX = 8;           // provisório: 5 titulares + 3 reservas
        public const int TAMANHO_MERCADO = 12;
        public const int PREMIO_VITORIA = 50000, PREMIO_DERROTA = 15000;
        public const int NIVEL_GH_MAX = 5;
        /// <summary>Custo para chegar ao nível n (índice n-1). O nível 1 já vem de graça.</summary>
        public static readonly int[] CUSTO_GH = { 0, 100000, 250000, 500000, 1000000 };
        /// <summary>O que a Gaming House libera e em que nível (decisão 0007: treinos e scouts). Ainda sem efeito no jogo.</summary>
        public static readonly (string nome, int nivel)[] LIBERA_GH = { ("Treinos", 2), ("Scouts", 3) };
    }

    public static class Fmt
    {
        /// <summary>1234567 → "$ 1.234.567" (independe da cultura do computador).</summary>
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
    }

    /// <summary>
    /// Um jogador do elenco. Os atributos mostrados na interface (0..100) são uma LEITURA do `Skill` que a IA dos bots já
    /// usa: assim o que o manager vê é exatamente o que muda na partida (pilar 3). Quais atributos o jogo terá de verdade
    /// é pergunta aberta (gdd/02-mecanicas.md).
    /// </summary>
    public class Jogador
    {
        public string nome; public int idade; public Role funcao; public Skill skill = new Skill();

        static int Esc(float v, float pior, float melhor) => (int)Math.Round(Math.Clamp((v - pior) / (melhor - pior), 0f, 1f) * 100f);

        public int Mira => Esc(skill.aimSigma, 1.4f, 0.5f);          // erro de mira menor = melhor
        public int Reflexo => Esc(skill.reaction, 0.30f, 0.15f);       // reação mais rápida = melhor
        public int Spray => Esc(skill.spray, 0.5f, 0.95f);
        public int Utilitaria => Esc(skill.utilitaria, 0.4f, 0.95f);
        public int SangueFrio => Esc(skill.composure, 0.4f, 0.95f);
        public int Agressividade => (int)Math.Round(Math.Clamp(skill.aggression, 0f, 1f) * 100f);   // estilo, não "melhor/pior"

        public int Overall => (int)Math.Round((2f * Mira + 2f * Reflexo + Spray + Utilitaria + SangueFrio) / 7f);

        public int Salario => (2000 + (int)(Overall * Overall * 0.6f)) / 50 * 50;   // por partida da carreira
        public int Valor => Salario * 8;                                              // contratação
        public int Venda => Valor / 2;
    }

    public class Lancamento { public int numPartida; public string descricao; public int valor; }

    public class Resultado
    {
        public int numPartida; public string adversario; public bool venceu, assistida; public int meus, deles;
        public int premio, salarios;
        public string Placar => meus + " x " + deles;
    }

    public class Adversario { public string nome; public int forca; public List<(string nome, Role funcao)> jogadores = new List<(string nome, Role funcao)>(); }

    /// <summary>
    /// O estado da carreira. Fica estático entre cenas (Manager ↔ Partida3D) mas NÃO é salvo em disco ainda
    /// (salvamento é "a definir" em gdd/09-tecnico.md).
    /// </summary>
    public class Carreira
    {
        public static Carreira Atual;

        public string clube = "VULCÃO";
        public int dinheiro, numPartida = 1, nivelGH = 1;   // numPartida: a partida da carreira em que estamos (o calendário ainda é pergunta aberta)
        public readonly List<Jogador> elenco = new List<Jogador>();      // os 5 primeiros são os titulares
        public readonly List<Jogador> mercado = new List<Jogador>();
        public readonly List<Lancamento> extrato = new List<Lancamento>();
        public readonly List<Resultado> historico = new List<Resultado>();
        public Adversario proximo;
        public bool Falido => dinheiro < 0;

        /// <summary>A partida montada e à espera da cena 3D buscar (ver PartidaAoVivo3D). null = nada pendente.</summary>
        public CSM.Sim.Partida pendente;
        public bool emPartida;
        /// <summary>O último resultado que o manager ainda não viu no painel (a interface mostra e limpa).</summary>
        public Resultado naoVisto;

        readonly int seed; Random rng;

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
                c.elenco.Add(new Jogador { nome = nome, idade = 21 + c.rng.Next(0, 8), funcao = role, skill = new Skill() });
            // duas reservas, um pouco abaixo
            for (int i = 0; i < 2; i++) c.elenco.Add(c.Gerar(0.35f, 0.65f));
            c.proximo = new Adversario { nome = "ROCHA", forca = new Jogador { skill = new Skill() }.Overall };
            foreach (var (nome, role, _, _) in Match.CT_TEAM) c.proximo.jogadores.Add((nome, role));
            c.GerarMercado();
            return c;
        }

        Carreira(int seed) { this.seed = seed; rng = new Random(seed); }

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
            return new Jogador { nome = NomeLivre(), idade = 17 + rng.Next(0, 15), funcao = FUNCOES[rng.Next(FUNCOES.Length)], skill = sk };
        }

        string NomeLivre()
        {
            var usados = new HashSet<string>(elenco.Select(j => j.nome).Concat(mercado.Select(j => j.nome)));
            foreach (var (n, _, _, _) in Match.CT_TEAM) usados.Add(n);   // os nomes do adversário também são do Sim
            var livres = NOMES.Where(n => !usados.Contains(n)).ToList();
            return livres.Count > 0 ? livres[rng.Next(livres.Count)] : "JOGADOR" + rng.Next(100, 999);
        }

        void GerarMercado()
        {
            mercado.Clear();
            for (int i = 0; i < Provisorio.TAMANHO_MERCADO; i++) mercado.Add(Gerar(0.2f, 0.95f));
            mercado.Sort((a, b) => b.Overall.CompareTo(a.Overall));
        }

        // ------------------------------------------------------------------------------------------ consultas
        public IEnumerable<Jogador> Titulares => elenco.Take(Provisorio.TITULARES);
        public bool Titular(Jogador j) { int i = elenco.IndexOf(j); return i >= 0 && i < Provisorio.TITULARES; }
        public int SalariosPorPartida => elenco.Sum(j => j.Salario);
        public int Forca => elenco.Count == 0 ? 0 : (int)Math.Round(Titulares.Average(j => j.Overall));
        public int CustoProximoNivelGH => nivelGH >= Provisorio.NIVEL_GH_MAX ? 0 : Provisorio.CUSTO_GH[nivelGH];

        // ------------------------------------------------------------------------------------------ ações (devolvem a mensagem de erro, ou null se deu certo)
        void Lanca(string descricao, int valor) { dinheiro += valor; extrato.Insert(0, new Lancamento { numPartida = numPartida, descricao = descricao, valor = valor }); }

        public string Contratar(Jogador j)
        {
            if (!mercado.Contains(j)) return "Esse jogador não está mais no mercado.";
            if (elenco.Count >= Provisorio.ELENCO_MAX) return "Elenco cheio (" + Provisorio.ELENCO_MAX + "). Venda alguém antes.";
            if (dinheiro < j.Valor) return "Dinheiro insuficiente.";
            Lanca("Contratação de " + j.nome, -j.Valor);
            mercado.Remove(j); elenco.Add(j);
            return null;
        }

        public string Vender(Jogador j)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (elenco.Count <= Provisorio.TITULARES) return "É preciso manter pelo menos " + Provisorio.TITULARES + " jogadores.";
            Lanca("Venda de " + j.nome, j.Venda);
            elenco.Remove(j);
            return null;
        }

        /// <summary>Sobe (delta = -1) ou desce (+1) o jogador na lista; os 5 primeiros são os titulares.</summary>
        public void Mover(Jogador j, int delta)
        {
            int i = elenco.IndexOf(j), k = i + delta;
            if (i < 0 || k < 0 || k >= elenco.Count) return;
            elenco[i] = elenco[k]; elenco[k] = j;
        }

        public string SubirGamingHouse()
        {
            if (nivelGH >= Provisorio.NIVEL_GH_MAX) return "A Gaming House já está no nível máximo.";
            int custo = CustoProximoNivelGH;
            if (dinheiro < custo) return "Dinheiro insuficiente.";
            Lanca("Gaming House: nível " + (nivelGH + 1), -custo);
            nivelGH++;
            return null;
        }

        // ------------------------------------------------------------------------------------------ partida
        /// <summary>
        /// Monta a partida com o elenco do manager no lugar do time A. Cada titular ocupa a vaga da função dele (se houver);
        /// quem sobra preenche as vagas que restam. Nome e habilidade vão para a simulação: é assim que o elenco aparece na partida.
        /// </summary>
        public CSM.Sim.Partida MontarPartida(int seedPartida)
        {
            var pt = new CSM.Sim.Partida(seedPartida, null, null);
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
            for (int v = 0; v < vagas.Length; v++) { vagas[v].nome = porVaga[v].nome; vagas[v].skill = porVaga[v].skill; }
            return pt;
        }

        /// <summary>Prepara uma partida para a cena 3D assistir (a cena pega com <see cref="TomarPendente"/>).</summary>
        public void PrepararAssistir(int seedPartida) { pendente = MontarPartida(seedPartida); emPartida = true; }

        public static CSM.Sim.Partida TomarPendente()
        {
            var c = Atual; if (c == null || c.pendente == null) return null;
            var p = c.pendente; c.pendente = null; return p;
        }

        /// <summary>Pular partida (decisão 0008): resultado por probabilidade. A fórmula é provisória; o GDD pede calibrar
        /// rodando muitas partidas da IA real (gdd/09-tecnico.md).</summary>
        public Resultado Pular()
        {
            double p = 1.0 / (1.0 + Math.Exp(-(Forca - proximo.forca) / 6.0));
            bool venceu = rng.NextDouble() < p;
            int perdedor = rng.Next(3, 12);
            return Registrar(venceu, venceu ? 13 : perdedor, venceu ? perdedor : 13, false);
        }

        /// <summary>Fecha a partida da carreira: paga o prêmio e os salários, guarda o resultado, renova o mercado.</summary>
        public Resultado Registrar(bool venceu, int meus, int deles, bool assistida)
        {
            var r = new Resultado
            {
                numPartida = numPartida, adversario = proximo.nome, venceu = venceu, assistida = assistida, meus = meus, deles = deles,
                premio = venceu ? Provisorio.PREMIO_VITORIA : Provisorio.PREMIO_DERROTA, salarios = SalariosPorPartida,
            };
            Lanca((venceu ? "Prêmio: vitória contra " : "Prêmio: derrota para ") + proximo.nome, r.premio);
            Lanca("Salários", -r.salarios);
            historico.Insert(0, r); naoVisto = r;
            numPartida++; emPartida = false; pendente = null;
            GerarMercado();
            return r;
        }
    }
}
