using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Sim
{
    /* =====================================================================================================================
       JOGADORES ESTILO FOOTBALL MANAGER (2026-11-07)
       - Atributos de 1 a 20 (como o FM). 15 = o jogador padrão da sim de hoje (pro de tier 1 médio): uma ficha com tudo 15,
         familiaridade 20 na função e momento neutro dá EXATAMENTE o Skill padrão. Status não cria regra nova: ele mexe nos
         parâmetros que o cérebro já usa (Skill). Os atributos que ainda não têm onde ligar no cérebro ficam guardados e já
         contam na nota da função, na evolução e no treino; cada ligação nova no Brain passa pelo A/B como sempre.
       - Familiaridade de função (1..20): quanto mais joga numa função, mais familiar fica e mais sobem os atributos que ela usa.
         Fora da função ele erra mais o que é da função (mira posicionada no ângulo, granada, calma).
       - Características (as "preferências de jogo" do FM): jeito de jogar que se aprende no treino. Por enquanto guardadas;
         cada uma vira peso de decisão no cérebro com A/B.
       - Momento: do time (clima do elenco + embalo na partida) e individual (moral, forma, confiança na partida). O buff total
         fica limitado a ±2 pontos de atributo (±10% da escala): ajuda, não decide a partida.
       ===================================================================================================================== */

    public enum Atr
    {
        // técnicos (mecânica)
        Mira, Reflexo, Flick, Spray, MiraPosicionada, Movimento, Utilitaria, Awp,   /* pistola usa a Mira; quem é bom de pistola tem a característica BomDePistola */
        // mentais
        Agressividade, Calma, Decisoes, LeituraDeJogo, Posicionamento, TrabalhoEmEquipe, Comunicacao, Lideranca, Concentracao, Determinacao,
        // físico
        Resistencia,
        // ocultos (o jogador não vê o número, só o efeito e o que o olheiro conta)
        Consistencia, JogosGrandes, Profissionalismo, Ambicao, Temperamento, Adaptabilidade,
    }

    /// <summary>Preferências de jogo (como as do FM): jeito de jogar, aprendido e treinável.</summary>
    public enum Caracteristica
    {
        BuscaAberturaDoRound,   // quer o 1º abate, sai na frente
        TentaOClutch,           // no 1vX joga pra ganhar em vez de salvar
        SalvaAArma,             // round perdido: guarda a arma cedo
        LurkaSozinho,           // gosta de ficar longe do time no fim do round
        JogaColadoNoTime,       // anda junto pra trocar
        AwpAgressiva,           // dá peek de AWP, joga de frente
        SeguraAnguloFechado,    // prefere o ângulo curto, perto
        JogaDeLonge,            // prefere a linha longa
        DeagleNoEco,            // no eco compra Deagle e caça HS
        BomDePistola,           // acerta mais de pistola (pistol round, eco, troca de arma): erra 12% menos com ela
        EstudaOJogo,            // vê demo, estuda o adversário: evolui e treina mais rápido o lado "cabeça" (granada, leitura, decisões, posicionamento)
        PreNade,                // joga granada antes de entrar
        JogadaDeRisco,          // ninja, flanco sozinho, peek de informação
        AncoraTeimosa,          // fica no bomb até ter certeza
        RotacionaCedo,          // sai do bomb ao primeiro sinal
        DaDrop,                 // generoso na economia
        PreFire,                // atira no canto comum antes de ver
        SprayNaSmoke,           // atira através da smoke no barulho
        SegueACall,             // faz o que o IGL mandou à risca
        Improvisa,              // muda a jogada no meio
    }

    public static class Atributos
    {
        public const int N = 25;
        public const float PADRAO = 15f;      // o jogador padrão da sim (pro de tier 1 médio)
        public const float BUFF_MAX = 2f;     // momento: no máximo ±2 pontos (±10% da escala)
        public static readonly Atr[] Tecnicos = { Atr.Mira, Atr.Reflexo, Atr.Flick, Atr.Spray, Atr.MiraPosicionada, Atr.Movimento, Atr.Utilitaria, Atr.Awp };
        public static readonly Atr[] Mentais = { Atr.Agressividade, Atr.Calma, Atr.Decisoes, Atr.LeituraDeJogo, Atr.Posicionamento, Atr.TrabalhoEmEquipe, Atr.Comunicacao, Atr.Lideranca, Atr.Concentracao, Atr.Determinacao };
        public static readonly Atr[] Fisicos = { Atr.Resistencia };
        public static readonly Atr[] Ocultos = { Atr.Consistencia, Atr.JogosGrandes, Atr.Profissionalismo, Atr.Ambicao, Atr.Temperamento, Atr.Adaptabilidade };
        /// <summary>Jeito de ser, não qualidade: não entra na "habilidade atual" nem cresce com treino de função.</summary>
        public static bool Personalidade(Atr a) => a == Atr.Agressividade || (int)a >= (int)Atr.Consistencia && a != Atr.JogosGrandes;
        /// <summary>O que a sim já usa hoje (os outros ficam guardados até ganharem ligação com A/B).</summary>
        public static readonly Atr[] LigadosNaSim = { Atr.Mira, Atr.Reflexo, Atr.Flick, Atr.Spray, Atr.MiraPosicionada, Atr.Utilitaria, Atr.Agressividade, Atr.Calma, Atr.Consistencia, Atr.Awp };
        /// <summary>Mecânica pura: cai com a idade (depois dos ~27).</summary>
        public static bool Mecanico(Atr a) => a == Atr.Reflexo || a == Atr.Flick || a == Atr.Movimento || a == Atr.Mira;

        public static string Nome(Atr a) => a switch
        {
            Atr.Mira => "Mira", Atr.Reflexo => "Reflexo", Atr.Flick => "Flick", Atr.Spray => "Controle de spray", Atr.MiraPosicionada => "Mira posicionada",
            Atr.Movimento => "Movimentação", Atr.Utilitaria => "Utilitária", Atr.Awp => "AWP",
            Atr.Agressividade => "Agressividade", Atr.Calma => "Calma", Atr.Decisoes => "Decisões", Atr.LeituraDeJogo => "Leitura de jogo",
            Atr.Posicionamento => "Posicionamento", Atr.TrabalhoEmEquipe => "Trabalho em equipe", Atr.Comunicacao => "Comunicação",
            Atr.Lideranca => "Liderança", Atr.Concentracao => "Concentração", Atr.Determinacao => "Determinação", Atr.Resistencia => "Resistência",
            Atr.Consistencia => "Consistência", Atr.JogosGrandes => "Jogos grandes", Atr.Profissionalismo => "Profissionalismo",
            Atr.Ambicao => "Ambição", Atr.Temperamento => "Temperamento", Atr.Adaptabilidade => "Adaptabilidade", _ => a.ToString(),
        };

        /// <summary>Peso de cada atributo em cada função (é o que faz um cara ser bom de IGL e ruim de lurker). Também diz o
        /// que sobe quando ele joga na função.</summary>
        public static readonly Dictionary<Role, (Atr a, float w)[]> PesoNaFuncao = new Dictionary<Role, (Atr, float)[]>
        {
            [Role.Entry] = new[] { (Atr.Reflexo, 3f), (Atr.Movimento, 3f), (Atr.Flick, 2f), (Atr.Mira, 2f), (Atr.Determinacao, 2f), (Atr.Spray, 1f), (Atr.TrabalhoEmEquipe, 1f), (Atr.Decisoes, 1f) },
            [Role.Suporte] = new[] { (Atr.Utilitaria, 3f), (Atr.TrabalhoEmEquipe, 3f), (Atr.Comunicacao, 2f), (Atr.Posicionamento, 2f), (Atr.Spray, 1f), (Atr.Mira, 1f), (Atr.Decisoes, 1f), (Atr.LeituraDeJogo, 1f) },
            [Role.Lurker] = new[] { (Atr.LeituraDeJogo, 3f), (Atr.Posicionamento, 3f), (Atr.Decisoes, 2f), (Atr.Calma, 2f), (Atr.Concentracao, 2f), (Atr.Mira, 1f), (Atr.Movimento, 1f) },
            [Role.Awper] = new[] { (Atr.Awp, 4f), (Atr.Reflexo, 2f), (Atr.Calma, 2f), (Atr.Posicionamento, 2f), (Atr.Concentracao, 1f), (Atr.Decisoes, 1f) },
            [Role.Igl] = new[] { (Atr.Lideranca, 4f), (Atr.LeituraDeJogo, 3f), (Atr.Decisoes, 3f), (Atr.Comunicacao, 3f), (Atr.Calma, 1f), (Atr.Utilitaria, 1f) },
            [Role.Ancora] = new[] { (Atr.Posicionamento, 3f), (Atr.Calma, 3f), (Atr.Utilitaria, 2f), (Atr.Concentracao, 2f), (Atr.Mira, 1f), (Atr.Spray, 1f), (Atr.Comunicacao, 1f) },
            [Role.Rotacao] = new[] { (Atr.LeituraDeJogo, 3f), (Atr.Movimento, 2f), (Atr.Decisoes, 2f), (Atr.Comunicacao, 2f), (Atr.TrabalhoEmEquipe, 1f), (Atr.Mira, 1f), (Atr.Spray, 1f) },
        };

        /// <summary>O que a função "sabe" e o jogador fora dela erra (ângulos, granadas, tempo): perde até ~4 pontos.</summary>
        /// <summary>O lado "cabeça" do jogo: o que quem estuda (Estuda o jogo, mentor) acelera.</summary>
        public static bool DeCabeca(Atr a) => a == Atr.Utilitaria || a == Atr.LeituraDeJogo || a == Atr.Decisoes || a == Atr.Posicionamento || a == Atr.MiraPosicionada;
        public static bool DaFuncao(Atr a) => a == Atr.MiraPosicionada || a == Atr.Utilitaria || a == Atr.Posicionamento || a == Atr.Decisoes || a == Atr.LeituraDeJogo;
        public const float FORA_DE_POSICAO = 0.2f;   // pontos perdidos por ponto de familiaridade que falta (fam 1 = −3,8)

        public static string Nome(Caracteristica c) => c switch
        {
            Caracteristica.BuscaAberturaDoRound => "Busca o 1º abate", Caracteristica.TentaOClutch => "Tenta o clutch", Caracteristica.SalvaAArma => "Salva a arma",
            Caracteristica.LurkaSozinho => "Lurka sozinho", Caracteristica.JogaColadoNoTime => "Joga colado no time", Caracteristica.AwpAgressiva => "AWP agressiva",
            Caracteristica.SeguraAnguloFechado => "Segura ângulo fechado", Caracteristica.JogaDeLonge => "Joga de longe", Caracteristica.DeagleNoEco => "Deagle no eco", Caracteristica.BomDePistola => "Bom de pistola", Caracteristica.EstudaOJogo => "Estuda o jogo",
            Caracteristica.PreNade => "Granada antes de entrar", Caracteristica.JogadaDeRisco => "Jogada de risco", Caracteristica.AncoraTeimosa => "Âncora teimosa",
            Caracteristica.RotacionaCedo => "Rotaciona cedo", Caracteristica.DaDrop => "Dá drop", Caracteristica.PreFire => "Pre-fire nos cantos",
            Caracteristica.SprayNaSmoke => "Spray na smoke", Caracteristica.SegueACall => "Segue a call", Caracteristica.Improvisa => "Improvisa", _ => c.ToString(),
        };
        /// <summary>Pares que não convivem (aprender uma tira a outra).</summary>
        public static readonly (Caracteristica, Caracteristica)[] Opostas =
        {
            (Caracteristica.TentaOClutch, Caracteristica.SalvaAArma), (Caracteristica.AncoraTeimosa, Caracteristica.RotacionaCedo),
            (Caracteristica.SegueACall, Caracteristica.Improvisa), (Caracteristica.SeguraAnguloFechado, Caracteristica.JogaDeLonge),
            (Caracteristica.LurkaSozinho, Caracteristica.JogaColadoNoTime),
        };
        /// <summary>Características comuns em cada função (o gerador e o treino sugerido usam).</summary>
        public static readonly Dictionary<Role, Caracteristica[]> TipicasDaFuncao = new Dictionary<Role, Caracteristica[]>
        {
            [Role.Entry] = new[] { Caracteristica.BuscaAberturaDoRound, Caracteristica.PreFire, Caracteristica.JogadaDeRisco, Caracteristica.SeguraAnguloFechado, Caracteristica.BomDePistola },
            [Role.Suporte] = new[] { Caracteristica.PreNade, Caracteristica.DaDrop, Caracteristica.JogaColadoNoTime, Caracteristica.SegueACall, Caracteristica.EstudaOJogo },
            [Role.Lurker] = new[] { Caracteristica.LurkaSozinho, Caracteristica.TentaOClutch, Caracteristica.Improvisa, Caracteristica.JogadaDeRisco },
            [Role.Awper] = new[] { Caracteristica.AwpAgressiva, Caracteristica.JogaDeLonge, Caracteristica.SalvaAArma, Caracteristica.DeagleNoEco, Caracteristica.BomDePistola },
            [Role.Igl] = new[] { Caracteristica.Improvisa, Caracteristica.PreNade, Caracteristica.DaDrop, Caracteristica.SalvaAArma, Caracteristica.EstudaOJogo },
            [Role.Ancora] = new[] { Caracteristica.AncoraTeimosa, Caracteristica.SeguraAnguloFechado, Caracteristica.SprayNaSmoke, Caracteristica.SegueACall },
            [Role.Rotacao] = new[] { Caracteristica.RotacionaCedo, Caracteristica.JogaColadoNoTime, Caracteristica.PreFire, Caracteristica.SprayNaSmoke },
        };
    }

    /// <summary>A ficha do jogador (o "perfil" do FM): quem ele é, atributos, funções, características e momento.</summary>
    public class Ficha
    {
        public int id; public string nick, nome, pais; public int idade; public Role funcao, funcaoCT = Role.Ancora; public int tier;   // funcao = a de TR; tier: 1 = elite .. 4 = academia
        public readonly float[] atr = new float[Atributos.N];     // 1..20 (a tela mostra arredondado; cresce aos poucos)
        public readonly float[] teto = new float[Atributos.N];    // oculto: até onde cada atributo pode chegar (o "PA" do FM)
        public readonly float[] fam = new float[7];               // familiaridade por função (índice = Role), 1..20
        public readonly List<Caracteristica> carac = new List<Caracteristica>();
        public readonly Dictionary<Caracteristica, float> treinando = new Dictionary<Caracteristica, float>();   // progresso 0..1
        // momento individual (carreira): −1..1
        public float moral, forma;
        // carreira
        public readonly int[] roundsNaFuncao = new int[7]; public int partidas;

        public float this[Atr a] { get => atr[(int)a]; set => atr[(int)a] = MathF.Max(1f, MathF.Min(20f, value)); }
        public float Fam(Role r) => fam[(int)r];

        /// <summary>Ficha do jogador padrão da sim: tudo 15, familiaridade 20 em todas as funções, consistência 20 (sem dia ruim).</summary>
        public static Ficha Padrao(int id = 0, string nick = null)
        {
            var f = new Ficha { id = id, nick = nick ?? "padrao" + id, nome = "", pais = "", idade = 24, funcao = Role.Entry, tier = 1 };
            for (int i = 0; i < Atributos.N; i++) { f.atr[i] = Atributos.PADRAO; f.teto[i] = Atributos.PADRAO; }
            f[Atr.Consistencia] = 20f; for (int i = 0; i < 7; i++) f.fam[i] = 20f;
            return f;
        }

        /// <summary>O atributo como ele joga neste round: o número + momento (±2) + o dia (consistência) − fora da função.</summary>
        public float Efetivo(Atr a, Role r, float pontosExtra)
        {
            float x = atr[(int)a] + pontosExtra;
            if (Atributos.DaFuncao(a)) x -= (20f - fam[(int)r]) * Atributos.FORA_DE_POSICAO;
            return MathF.Max(1f, MathF.Min(20f, x));
        }

        /// <summary>
        /// Ficha → Skill (o que o cérebro usa). d = (atributo − 15)/5: 20 = +1, 10 = −1, 5 = −2. Com tudo 15, fam 20 e sem extra,
        /// todos os multiplicadores dão 1 e as somas dão 0: o Skill sai idêntico ao padrão (conferido no Headless -fichas eq).
        /// pontosExtra = momento + dia, em pontos de atributo (limitado lá fora).
        /// Agressividade é jeito, não qualidade: o momento não mexe nela.
        /// </summary>
        public Skill ParaSkill(Role r, float pontosExtra = 0f)
        {
            var s = new Skill();
            float D(Atr a) => (Efetivo(a, r, pontosExtra) - Atributos.PADRAO) / 5f;
            float dMira = D(Atr.Mira), dRef = D(Atr.Reflexo), dFlick = D(Atr.Flick), dSpray = D(Atr.Spray), dPos = D(Atr.MiraPosicionada);
            float dUtil = D(Atr.Utilitaria), dCalma = D(Atr.Calma);
            s.aimSigma *= 1f - 0.18f * dMira;                  // 20 → −18% de erro; 10 → +18%
            // AWP e pistola: o erro com essas armas sai do atributo delas (não da mira de rifle)
            s.miraAwp = (1f - 0.18f * D(Atr.Awp)) / (1f - 0.18f * dMira);
            s.miraPistola = Tem(Caracteristica.BomDePistola) ? 0.88f : 1f;   /* pistola = a Mira; bom de pistola erra 12% menos com ela */
            s.reaction *= 1f - 0.12f * dRef;                   // 20 → 0,167 s; 10 → 0,213 s
            s.flick *= 1f + 0.15f * dFlick;
            s.spray = Clamp(s.spray + 0.07f * dSpray, 0.3f, 0.98f);
            s.crosshair = Clamp(s.crosshair + 0.08f * dPos, 0.3f, 0.97f);
            s.pitchErr *= 1f - 0.15f * dPos;
            s.utilitaria = Clamp(s.utilitaria + 0.08f * dUtil, 0.3f, 0.98f);
            s.composure = Clamp(s.composure + 0.08f * dCalma, 0.2f, 0.98f);
            float ag = atr[(int)Atr.Agressividade];             // sem momento: 15 → 0,5; 20 → 0,9; 1 → 0,15 (a faixa dos pros, sem extremo)
            s.aggression = ag >= Atributos.PADRAO ? 0.5f + (ag - Atributos.PADRAO) * 0.08f : 0.5f - (Atributos.PADRAO - ag) * (0.35f / 14f);
            return s;
        }
        static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;

        /// <summary>Nota na função (1..20): média ponderada dos atributos que ela usa, menos o que falta de familiaridade.
        /// É o "estrelas na função" do FM.</summary>
        public float NotaNaFuncao(Role r)
        {
            float s = 0, w = 0;
            foreach (var (a, p) in Atributos.PesoNaFuncao[r]) { s += atr[(int)a] * p; w += p; }
            return MathF.Max(1f, s / w - (20f - fam[(int)r]) * 0.15f);
        }
        public Role MelhorFuncao() => Enumerable.Range(0, 7).Select(i => (Role)i).OrderByDescending(NotaNaFuncao).First();
        /// <summary>Habilidade atual (o "CA" do FM), 1..20: a nota na função natural.</summary>
        public float Habilidade => NotaNaFuncao(funcao);

        public bool Tem(Caracteristica c) => carac.Contains(c);
        public void Aprende(Caracteristica c)
        {
            if (carac.Contains(c)) return;
            foreach (var (x, y) in Atributos.Opostas) { if (x == c) carac.Remove(y); if (y == c) carac.Remove(x); }
            carac.Add(c); treinando.Remove(c);
        }

        public override string ToString() => $"{nick} ({nome}, {pais}, {idade}) {funcao} hab {Habilidade:0.0}";
    }

    /// <summary>
    /// Momento: quanto o jogador rende acima/abaixo do número dele agora. Junta o do time (clima do elenco na carreira +
    /// embalo na partida) com o individual (moral e forma na carreira + confiança na partida). Saída em pontos de atributo,
    /// limitada a ±BUFF_MAX (±2 = ±10% da escala): ajuda, não decide a partida.
    /// </summary>
    public static class Momento
    {
        /// <summary>Embalo do time na partida, −1..1: os últimos rounds dele, o mais recente pesando mais.</summary>
        public static float EmbaloDoTime(IReadOnlyList<bool> venceuRounds)
        {
            float[] w = { 0.4f, 0.3f, 0.2f, 0.1f }; float s = 0;
            for (int k = 0; k < w.Length && k < venceuRounds.Count; k++) s += w[k] * (venceuRounds[venceuRounds.Count - 1 - k] ? 1f : -1f);
            return s;
        }
        /// <summary>Confiança na partida, −1..1: o saldo dele de abates e mortes até aqui.</summary>
        public static float Confianca(int kills, int deaths) => MathF.Tanh((kills - deaths) / 4f);

        public static float Pontos(Ficha f, float climaTime, float embaloTime, float confianca, float amorteceTilt = 1f)
        {
            float time = 0.5f * climaTime + 0.5f * embaloTime;
            float ind = 0.4f * f.moral + 0.3f * f.forma + 0.3f * confianca;
            float m = 0.5f * time + 0.5f * ind;
            // quem tem pavio curto afunda mais no ruim; quem é determinado segura
            if (m < 0) m *= (1f + (10f - f[Atr.Temperamento]) / 20f - (f[Atr.Determinacao] - 10f) / 40f) * amorteceTilt;   /* amorteceTilt: psicólogo/gestão do staff */
            m = MathF.Max(-1f, MathF.Min(1f, m));
            return m * Atributos.BUFF_MAX;
        }

        /// <summary>O dia do jogador na partida (consistência): sorteado uma vez por partida, com o rng dele (não mexe no da
        /// partida). Consistência 20 = sempre igual; 1 = varia ~±1,5 ponto.</summary>
        public static float Dia(Ficha f, int seedPartida)
        {
            float sd = 0.8f * (20f - f[Atr.Consistencia]) / 19f;
            if (sd <= 0f) return 0f;
            var rng = new Rng(seedPartida * 7919 + f.id * 104729 + 11);
            return MathF.Max(-1.5f, MathF.Min(1.5f, rng.Normal(0f, sd)));
        }
    }
}
