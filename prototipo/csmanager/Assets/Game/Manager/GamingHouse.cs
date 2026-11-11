using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    /// <summary>Um nível da Gaming House (decisão 0016): o que libera e o que custa. Números PROVISÓRIOS.</summary>
    public class NivelGH
    {
        public string nome; public int custo, manutencao, fas;            // custo para chegar a este nível; manutenção por semana
        public int maxSessoes, coletivas, scouts, nivelIntel, elencoMax;
        public float recuperacao, ganhoTreino, patrocinio; public bool estudoMapa, bootcamp;
        public string[] vantagens;
    }

    public static class GamingHouse
    {
        public const int MAX = 5;

        public static readonly NivelGH[] NIVEIS =
        {
            new NivelGH { nome = "Alojamento", custo = 0, manutencao = 500, maxSessoes = 2, coletivas = 0, scouts = 0, nivelIntel = 0, elencoMax = 8, recuperacao = 1f, ganhoTreino = 1f, patrocinio = 1f,
                vantagens = new[] { "Elenco de até 8 jogadores (5 titulares e 3 reservas)", "Treino individual leve: até 2 sessões por semana por jogador" } },
            new NivelGH { nome = "Sala de treino", custo = 100000, fas = 1500, manutencao = 1500, maxSessoes = 4, coletivas = 0, scouts = 0, nivelIntel = 0, elencoMax = 9, recuperacao = 1f, ganhoTreino = 1f, patrocinio = 1f, estudoMapa = true,
                vantagens = new[] { "Libera os TREINOS: foco por atributo, até 4 sessões por semana", "Estudo de mapa: o time ganha afinidade com o mapa escolhido", "+1 vaga de reserva (9 jogadores)" } },
            new NivelGH { nome = "Sala de análise", custo = 250000, fas = 4000, manutencao = 3500, maxSessoes = 4, coletivas = 0, scouts = 1, nivelIntel = 1, elencoMax = 9, recuperacao = 1f, ganhoTreino = 1f, patrocinio = 1.1f, estudoMapa = true,
                vantagens = new[] { "Libera os SCOUTS: 1 scout", "Informação do adversário nível 1: o estilo de jogo dele", "Scout no mercado: revela o potencial dos jogadores", "Patrocínio +10%" } },
            new NivelGH { nome = "Centro de performance", custo = 500000, fas = 12000, manutencao = 7000, maxSessoes = 4, coletivas = 3, scouts = 2, nivelIntel = 2, elencoMax = 10, recuperacao = 1.3f, ganhoTreino = 1f, patrocinio = 1.2f, estudoMapa = true,
                vantagens = new[] { "Fisioterapeuta: recuperação de cansaço +30%", "Sala do psicólogo: +1 conversa individual por semana (com psicólogo contratado)", "Sessões coletivas: entrosamento do time (preparo extra)", "2 scouts", "Informação do adversário nível 2: mapas fortes e fracos", "+1 vaga de reserva (10 jogadores)", "Patrocínio +20%" } },
            new NivelGH { nome = "Complexo de elite", custo = 1000000, fas = 35000, manutencao = 12000, maxSessoes = 5, coletivas = 3, scouts = 3, nivelIntel = 3, elencoMax = 10, recuperacao = 1.3f, ganhoTreino = 1.2f, patrocinio = 1.5f, estudoMapa = true, bootcamp = true,
                vantagens = new[] { "Analista de demos: informação do adversário nível 3 (estratégia detalhada e jogador de destaque)", "3 scouts", "Bootcamp: ganho de treino +20% e uma sessão extra por semana", "Patrocínio +50%" } },
        };

        public static NivelGH De(int nivel) => NIVEIS[Math.Max(1, Math.Min(MAX, nivel)) - 1];
        public static int CustoProximo(int nivel) => nivel >= MAX ? 0 : NIVEIS[nivel].custo;
    }

    public enum Atrib { Mira = 0, Reflexo = 1, Spray = 2, Utilitaria = 3, SangueFrio = 4 }

    /// <summary>
    /// Um jogador do elenco. Os atributos (0..100) são uma LEITURA do <see cref="Skill"/> que a IA dos bots usa: o que o manager vê é
    /// o que muda na partida (pilar 3). Treino, idade, cansaço e preparo (decisão 0013) mexem nesses valores.
    /// </summary>
    public class Jogador
    {
        public string nome; public int idade; public Role funcao; public Skill skill = new Skill();
        public int potencial = 85;                  // teto de evolução (só se vê com scout, no mercado)
        public bool potencialRevelado;
        public float cansaco = 10, preparo;         // 0..100
        public int foco = -1, sessoes;              // plano de treino da semana: atributo em foco (-1 = descanso) e quantas sessões
        public float[] inicioTemporada = new float[5];   // valor dos atributos no começo da temporada (para mostrar a evolução)
        public float ladoTR = 50, ladoCT = 50;      // domínio de cada lado, 0..100 (50 = neutro; decisão 0024). Muda a habilidade só naquele lado
        public int ladoFoco = -1;                   // treino da semana por lado: -1 = os dois, 0 = TR, 1 = CT
        public int mapasNaSemana;                   // partidas jogadas nesta semana (alimenta o cansaço)
        public Traco traco;                         // personalidade (decisão 0019): no mercado só se vê com scout, como o potencial
        public float moral = Provisorio.MORAL_INICIAL, entrosamento = Provisorio.ENTROSAMENTO_INICIAL;   // 0..100
        public int mvps, mvpsSerie;                 // vezes que foi MVP de mapa e de série no clube (decisão 0020)
        public List<string> jogadas = new List<string>();   // últimas jogadas da partida dele, a mais nova primeiro

        public static readonly string[] NOMES = { "Mira", "Reflexo", "Spray", "Utilitária", "Sangue-frio" };
        static readonly float[] PIOR = { 1.4f, 0.30f, 0.5f, 0.4f, 0.4f }, MELHOR = { 0.5f, 0.15f, 0.95f, 0.95f, 0.95f };

        static float Get(Skill s, int a) => a == 0 ? s.aimSigma : a == 1 ? s.reaction : a == 2 ? s.spray : a == 3 ? s.utilitaria : s.composure;
        static void Set(Skill s, int a, float v) { if (a == 0) s.aimSigma = v; else if (a == 1) s.reaction = v; else if (a == 2) s.spray = v; else if (a == 3) s.utilitaria = v; else s.composure = v; }
        static float Para100(Skill s, int a) => Math.Clamp((Get(s, a) - PIOR[a]) / (MELHOR[a] - PIOR[a]), 0f, 1f) * 100f;
        static void De100(Skill s, int a, float v) => Set(s, a, PIOR[a] + (MELHOR[a] - PIOR[a]) * Math.Clamp(v, 0f, 100f) / 100f);

        /// <summary>Valor do atributo, de 0 a 100 (com decimais).</summary>
        public float Valor(int a) => Para100(skill, a);
        public void Definir(int a, float v100) => De100(skill, a, v100);

        public int Mira => (int)Math.Round(Valor(0));
        public int Reflexo => (int)Math.Round(Valor(1));
        public int Spray => (int)Math.Round(Valor(2));
        public int Utilitaria => (int)Math.Round(Valor(3));
        public int SangueFrio => (int)Math.Round(Valor(4));
        public int Agressividade => (int)Math.Round(Math.Clamp(skill.aggression, 0f, 1f) * 100f);   // estilo, não "melhor/pior"

        public static float OverallDe(float mira, float reflexo, float spray, float util, float frio) => (2f * mira + 2f * reflexo + spray + util + frio) / 7f;
        public int Overall => (int)Math.Round(OverallDe(Valor(0), Valor(1), Valor(2), Valor(3), Valor(4)));

        /// <summary>Domínio do lado (0..100): <paramref name="tr"/> true = TR, false = CT.</summary>
        public float Lado(bool tr) => tr ? ladoTR : ladoCT;
        public float LadoMedio => (ladoTR + ladoCT) / 2f;
        /// <summary>O lado em que ele rende mais; "Equilibrado" quando a diferença é pequena (menos de 6 pontos).</summary>
        public bool? LadoForte => Math.Abs(ladoTR - ladoCT) < 6f ? (bool?)null : ladoTR > ladoCT;
        /// <summary>Quanto o lado muda a habilidade (fração, ex.: +0,03 = +3%) em relação à média dos dois lados.</summary>
        public float BonusLado(bool tr) => (Lado(tr) - LadoMedio) / 50f * Provisorio.LADO_MAX;
        /// <summary>Atributo (0..100) como ele rende naquele lado.</summary>
        public float ValorNoLado(int a, bool tr) => Math.Min(100f, Valor(a) * (1f + BonusLado(tr)));
        public int OverallNoLado(bool tr) => (int)Math.Round(OverallEfetivo(1f + BonusLado(tr)));

        /// <summary>O geral que o jogador "rende" com um multiplicador de forma (preparo, cansaço, afinidade...) sobre todos os atributos.</summary>
        public float OverallEfetivo(float mult) =>
            OverallDe(Math.Min(100, Valor(0) * mult), Math.Min(100, Valor(1) * mult), Math.Min(100, Valor(2) * mult), Math.Min(100, Valor(3) * mult), Math.Min(100, Valor(4) * mult));

        /// <summary>A habilidade que vai para a simulação: os atributos multiplicados pela forma do jogador naquele momento.</summary>
        public Skill SkillEfetiva(float mult)
        {
            var s = new Skill { reaction = skill.reaction, aimSigma = skill.aimSigma, flick = skill.flick, spray = skill.spray, crosshair = skill.crosshair, pitchErr = skill.pitchErr, aggression = skill.aggression, composure = skill.composure, utilitaria = skill.utilitaria };
            for (int a = 0; a < 5; a++) De100(s, a, Math.Min(100f, Valor(a) * mult));
            return s;
        }

        // por semana, em pontos de salário/valor (provisório): o salário é pago por semana
        /// <summary>O salário de tabela do jogador (pela habilidade) e o que ficou acertado na negociação (decisão 0026).</summary>
        public int SalarioBase => (2000 + (int)(Overall * Overall * 0.6f)) / 50 * 50 / 4 / 50 * 50;
        public int fotoId;                          // retrato fixo do jogador (1..N; 0 = ainda sem; decisão 0027)
        public int? salarioAcordado; public int promessaSemanas;   // promessa de titular: semanas que faltam para cumprir
        public int Salario => salarioAcordado ?? SalarioBase;
        public int Preco => Salario * 32;                 // contratação
        /// <summary>Contrato (decisão 0035): semanas que faltam (0 = sem contrato, como no mercado) e a cláusula de rescisão que um rival paga para levá-lo.</summary>
        public int contratoSemanas, clausula;
        public int Venda => (int)(Preco / 2 * Contratos.FatorVenda(contratoSemanas));   // contrato curto vale menos para quem compra

        public static string NomeAtrib(int a) => NOMES[a];
    }

    /// <summary>Nome e explicação dos traços de personalidade (decisão 0019), para a interface.</summary>
    public static class Tracos
    {
        public static string Nome(Traco t) => t switch
        {
            Traco.Decisivo => "Decisivo", Traco.Esquentado => "Esquentado", Traco.Lider => "Líder", Traco.Estrela => "Estrela",
            Traco.Pistoleiro => "Pistoleiro", Traco.Embalado => "Embalado", _ => "",
        };

        public static string Descricao(Traco t) => t switch
        {
            Traco.Decisivo => "Cresce quando o round decide: +15% de sangue-frio em match point, último round do half e prorrogação.",
            Traco.Esquentado => "Perde a cabeça: no round depois de morrer primeiro, mira 8% pior e joga mais agressivo.",
            Traco.Lider => "Puxa o time: escalado, dá +5% de sangue-frio aos outros 4 titulares.",
            Traco.Estrela => "Mira 6% melhor sempre, mas joga para si (troca menos os colegas) e não aceita o banco: a moral cai o dobro como reserva.",
            Traco.Pistoleiro => "Especialista de pistola: +12% de mira e reflexo nos rounds de pistol.",
            Traco.Embalado => "Cresce com a sequência: +6% de reflexo quando o time venceu os 3 últimos rounds.",
            _ => "Sem traço marcante.",
        };
    }
}
