using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Psicólogo do time (ideia 6 de gdd/11-ideias.md; decisão 0039, proposta). O clube contrata um psicólogo do mercado de
    // psicólogos; quanto melhor (1 a 5 estrelas), mais caro e mais forte: amortece as quedas de moral, puxa de volta quem está
    // abaixo do normal, escreve um relatório mais completo de cada jogador e faz as conversas individuais darem mais certo.
    // Sem psicólogo, nada disso existe e a moral anda como na decisão 0019. Números PROVISÓRIOS, como os de `Provisorio`.
    // ============================================================================================================

    public class Psicologo { public string nome; public int qualidade; public int salario; public int contratoSemanas; }

    /// <summary>A emoção do jogador no relatório do psicólogo, da pior para a melhor.</summary>
    public enum Emocao { Desmotivado, Frustrado, Ansioso, Tranquilo, Confiante }

    /// <summary>O que o psicólogo conta de um jogador nesta semana. Psicólogo fraco só diz a emoção e o efeito; o bom diz a causa;
    /// o muito bom prevê o que vem.</summary>
    public class RelatorioJogador
    {
        public Jogador jogador; public Emocao emocao;
        public string causa;      // null quando o psicólogo não é bom o bastante para dizer (qualidade 1)
        public string efeito;     // o efeito da moral na partida, em números
        public string previsao;   // null se não há o que prever ou o psicólogo não chega lá (qualidade abaixo de 4)
    }

    public static class Psicologos
    {
        public const int OPCOES = 4;                                   // psicólogos à venda; o mercado renova a cada temporada
        public static readonly int[] SALARIO = { 0, 400, 800, 1400, 2200, 3200 };   // por semana, pela qualidade (1 a 5)
        public const float PROTECAO_POR_ESTRELA = 0.08f;               // queda de moral: -8 % por estrela (5 estrelas = -40 %)
        public const float RECUPERACAO_POR_ESTRELA = 0.2f;             // por semana, quem está abaixo da moral neutra sobe 0,2 por estrela (5 = +1); menos que a queda do banco, para o banco seguir pesando
        public const float CONVERSA_CHANCE_BASE = 0.4f, CONVERSA_CHANCE_POR_ESTRELA = 0.1f;   // 1 estrela = 50 %, 5 = 90 %
        public const float CONVERSA_GANHO_BASE = 4f;                   // moral ganha numa conversa que dá certo: 4 + qualidade
        public const float CONVERSA_MORAL_MAX = 80f;                   // acima disso, conversar não muda nada
        public const int CONVERSAS_SEMANA = 1, CONVERSAS_GH = 1, NIVEL_GH_SALA = 4;   // a sala do psicólogo (nível 4) dá +1 conversa por semana
        public const int QUALIDADE_CAUSA = 2, QUALIDADE_PREVISAO = 4;  // a partir de quantas estrelas o relatório diz a causa e prevê
        public const float AUMENTO_RENOVACAO = 1.05f;

        static readonly string[] NOMES_F = { "Lívia", "Helena", "Marta", "Joana", "Renata", "Clara", "Bianca", "Teresa" };
        static readonly string[] NOMES_M = { "Caio", "Otávio", "Murilo", "Renan", "Tomás", "Vicente", "Heitor", "Davi" };
        static readonly string[] SOBRENOMES = { "Prado", "Aragão", "Fontes", "Moura", "Bastos", "Lacerda", "Viana", "Coutinho", "Sampaio", "Teles" };

        /// <summary>O mercado de psicólogos: <see cref="OPCOES"/> nomes inventados, com qualidades diferentes entre si.</summary>
        public static List<Psicologo> Gerar(Random rng)
        {
            var qualidades = Enumerable.Range(1, 5).OrderBy(_ => rng.Next()).Take(OPCOES).OrderBy(q => q).ToList();
            var l = new List<Psicologo>();
            foreach (int q in qualidades)
            {
                bool f = rng.Next(2) == 0;
                string nome = (f ? "Dra. " + NOMES_F[rng.Next(NOMES_F.Length)] : "Dr. " + NOMES_M[rng.Next(NOMES_M.Length)]) + " " + SOBRENOMES[rng.Next(SOBRENOMES.Length)];
                int sal = (int)Math.Round(SALARIO[q] * (0.9 + rng.NextDouble() * 0.2) / 50.0) * 50;
                l.Add(new Psicologo { nome = nome, qualidade = q, salario = sal });
            }
            return l;
        }

        /// <summary>Quanto sobra de uma queda de moral com esse psicólogo (1 = nada amortecido).</summary>
        public static float Protecao(int qualidade) => 1f - PROTECAO_POR_ESTRELA * qualidade;
        public static float ChanceConversa(int qualidade) => CONVERSA_CHANCE_BASE + CONVERSA_CHANCE_POR_ESTRELA * qualidade;
        /// <summary>Demitir antes do fim: metade dos salários que faltam, como no contrato dos jogadores (decisão 0035).</summary>
        public static int Multa(Psicologo p) => p == null ? 0 : (int)(p.salario * Math.Max(0, p.contratoSemanas) * Contratos.PARTE_MULTA);
        public static string Estrelas(int q) => new string('★', q) + new string('☆', 5 - q);

        public static string NomeEmocao(Emocao e) => e switch
        {
            Emocao.Confiante => "Confiante", Emocao.Tranquilo => "Tranquilo", Emocao.Ansioso => "Ansioso", Emocao.Frustrado => "Frustrado", _ => "Desmotivado",
        };

        public static Emocao EmocaoDe(float moral) =>
            moral >= 75 ? Emocao.Confiante : moral >= 55 ? Emocao.Tranquilo : moral >= 42 ? Emocao.Ansioso : moral >= Provisorio.MORAL_AVISO ? Emocao.Frustrado : Emocao.Desmotivado;
    }

    public partial class Carreira
    {
        public Psicologo psicologo;                                        // null = o clube não tem psicólogo
        public readonly List<Psicologo> psicologosMercado = new List<Psicologo>();
        public int conversasNaSemana;
        Random rngPsi;                                                     // separado do sorteio da carreira, para não mudar o resto
        Random RngPsi => rngPsi ??= new Random(seed * 977 + numPartida * 13 + Semana);

        public int SalarioPsicologo => psicologo?.salario ?? 0;
        public int ConversasPorSemana => psicologo == null ? 0 : Psicologos.CONVERSAS_SEMANA + (nivelGH >= Psicologos.NIVEL_GH_SALA ? Psicologos.CONVERSAS_GH : 0);
        public int ConversasRestantes => Math.Max(0, ConversasPorSemana - conversasNaSemana);
        /// <summary>O que sobra de cada queda de moral (decisão 0019) com o psicólogo atual: 1 sem psicólogo.</summary>
        public float FatorQuedaMoral => psicologo == null ? 1f : Psicologos.Protecao(psicologo.qualidade);

        /// <summary>Renova o mercado de psicólogos (carreira nova e começo de cada temporada).</summary>
        public void GerarPsicologos() { psicologosMercado.Clear(); psicologosMercado.AddRange(Psicologos.Gerar(new Random(seed * 131 + Temporada * 7))); }

        public string ContratarPsicologo(Psicologo p, int anos = 1)
        {
            if (p == null || !psicologosMercado.Contains(p)) return "Esse psicólogo não está mais disponível.";
            if (psicologo != null) return "O clube já tem um psicólogo. Demita " + psicologo.nome + " antes.";
            if (dinheiro < p.salario * 4) return "O caixa não paga nem um mês de salário dele.";
            anos = Math.Clamp(anos, Contratos.ANOS_MIN, 2);
            p.contratoSemanas = anos * Contratos.SEMANAS_ANO;
            psicologosMercado.Remove(p);
            psicologo = p; conversasNaSemana = 0;
            Anotar("Contratado o psicólogo " + p.nome + " (" + p.qualidade + " estrelas, " + Fmt.Dinheiro(p.salario) + " por semana).");
            return null;
        }

        public string DemitirPsicologo()
        {
            if (psicologo == null) return "O clube não tem psicólogo.";
            int multa = Psicologos.Multa(psicologo);
            if (multa > 0) Lanca("Multa: demissão do psicólogo " + psicologo.nome, -multa);
            Anotar("O psicólogo " + psicologo.nome + " foi demitido" + (multa > 0 ? " (multa de " + Fmt.Dinheiro(multa) + ")." : "."));
            psicologo = null; conversasNaSemana = 0;
            return null;
        }

        public bool PodeRenovarPsicologo => psicologo != null && psicologo.contratoSemanas <= Contratos.SEMANAS_AVISO;

        public string RenovarPsicologo(int anos = 1)
        {
            if (psicologo == null) return "O clube não tem psicólogo.";
            if (!PodeRenovarPsicologo) return "Só dá para renovar nos últimos " + Contratos.SEMANAS_AVISO + " semanas do contrato.";
            psicologo.contratoSemanas += Math.Clamp(anos, Contratos.ANOS_MIN, 2) * Contratos.SEMANAS_ANO;
            psicologo.salario = (int)Math.Round(psicologo.salario * Psicologos.AUMENTO_RENOVACAO / 50.0) * 50;
            Anotar("Renovado o contrato do psicólogo " + psicologo.nome + " (" + Fmt.Dinheiro(psicologo.salario) + " por semana).");
            return null;
        }

        /// <summary>Conversa individual do psicólogo com um jogador. Devolve a mensagem de erro, ou null; <paramref name="funcionou"/> diz se a
        /// moral subiu. A chance e o ganho crescem com a qualidade do psicólogo.</summary>
        public string Conversar(Jogador j, out bool funcionou)
        {
            funcionou = false;
            if (psicologo == null) return "Sem psicólogo, não há conversa individual.";
            if (j == null || !elenco.Contains(j)) return "Esse jogador não é do elenco.";
            if (ConversasRestantes <= 0) return "As conversas desta semana acabaram.";
            if (j.moral >= Psicologos.CONVERSA_MORAL_MAX) return j.nome + " já está bem: a conversa não mudaria nada.";
            conversasNaSemana++;
            int q = psicologo.qualidade;
            funcionou = RngPsi.NextDouble() < Psicologos.ChanceConversa(q);
            if (funcionou) j.moral = Math.Min(100f, j.moral + Psicologos.CONVERSA_GANHO_BASE + q);
            Anotar("Conversa de " + psicologo.nome + " com " + j.nome + ": " + (funcionou ? "ele saiu mais tranquilo." : "não mudou nada desta vez."));
            return null;
        }

        /// <summary>Uma semana de trabalho do psicólogo: puxa de volta quem está abaixo da moral neutra, zera as conversas e conta o contrato.</summary>
        void ProcessarPsicologo(ResumoSemana res)
        {
            conversasNaSemana = 0;
            if (psicologo == null) return;
            float puxa = Psicologos.RECUPERACAO_POR_ESTRELA * psicologo.qualidade;
            foreach (var j in elenco.Where(j => j.moral < Provisorio.MORAL_NEUTRA))
                j.moral = Math.Min(Provisorio.MORAL_NEUTRA, j.moral + puxa);
            psicologo.contratoSemanas--;
            if (psicologo.contratoSemanas == Contratos.SEMANAS_AVISO) res.linhas.Add("O contrato do psicólogo " + psicologo.nome + " termina em " + Contratos.Prazo(psicologo.contratoSemanas) + ".");
            if (psicologo.contratoSemanas <= 0) { res.linhas.Add("O contrato do psicólogo " + psicologo.nome + " terminou e ele deixou o clube."); psicologo = null; }
        }

        // ------------------------------------------------------------------------------------------ o relatório
        /// <summary>O relatório da semana, do jogador mais abalado para o mais confiante. Vazio sem psicólogo.</summary>
        public List<RelatorioJogador> Relatorio() => psicologo == null ? new List<RelatorioJogador>() : elenco.Select(RelatorioDe).OrderBy(r => r.jogador.moral).ToList();

        public RelatorioJogador RelatorioDe(Jogador j)
        {
            int q = psicologo?.qualidade ?? 0;
            var r = new RelatorioJogador { jogador = j, emocao = Psicologos.EmocaoDe(j.moral) };
            float efeito = (j.moral - Provisorio.MORAL_NEUTRA) * Provisorio.MORAL_POR_PONTO * 100f;
            r.efeito = Math.Abs(efeito) < 0.25f ? "Sem efeito na partida." : (efeito > 0 ? "+" : "-") + Math.Abs(efeito).ToString("0.0").Replace('.', ',') + " % de habilidade na partida.";
            if (q >= Psicologos.QUALIDADE_CAUSA) r.causa = Causa(j);
            if (q >= Psicologos.QUALIDADE_PREVISAO) r.previsao = Previsao(j);
            return r;
        }

        string Causa(Jogador j)
        {
            bool t = Titular(j);
            var ult = historico.FirstOrDefault();
            if (j.promessaSemanas > 0 && !t) return "Prometeram a ele uma vaga de titular, e ela ainda não veio.";
            if (!t && j.moral < 55) return j.traco == Traco.Estrela ? "É uma estrela e não aceita ficar no banco." : "Está no banco e quer jogar.";
            if (t && sequencia <= -2) return "O time perdeu " + (-sequencia) + " mapas seguidos.";
            if (t && ult != null && !ult.venceu && Contra(ult.adversario).Rival) return "Ainda sente a derrota no clássico contra " + ult.adversario + ".";
            if (j.contratoSemanas > 0 && j.contratoSemanas <= Contratos.SEMANAS_AVISO) return "Está preocupado com o contrato, que termina em " + Contratos.Prazo(j.contratoSemanas) + ".";
            if (t && ult != null && ult.venceu && ult.Melhor?.nome == j.nome) return "Foi o MVP da última partida.";
            if (t && sequencia >= 3) return "O time vem de " + sequencia + " vitórias seguidas.";
            if (j.entrosamento < 40) return "Ainda está se adaptando ao time.";
            return t ? "Está jogando e se sente parte do time." : "Aceita bem o papel de reserva por enquanto.";
        }

        string Previsao(Jogador j)
        {
            if (!Titular(j))
            {
                float queda = Provisorio.MORAL_RESERVA_SEMANA * (j.traco == Traco.Estrela ? 2f : 1f) * FatorQuedaMoral - Psicologos.RECUPERACAO_POR_ESTRELA * psicologo.qualidade;
                if (queda > 0 && j.moral > Provisorio.MORAL_AVISO)
                {
                    int semanas = (int)Math.Ceiling((j.moral - Provisorio.MORAL_AVISO) / queda);
                    if (semanas <= 12) return "Se seguir no banco, fica desmotivado em cerca de " + semanas + (semanas == 1 ? " semana." : " semanas.");
                }
            }
            if (j.contratoSemanas > 0 && j.contratoSemanas <= Contratos.SEMANAS_AVISO && j.moral < Contratos.MORAL_MIN_RENOVAR + 10)
                return "Com a moral assim, pode recusar a renovação.";
            if (j.moral < Provisorio.MORAL_AVISO) return "Uma conversa individual pode trazê-lo de volta.";
            return null;
        }
    }
}
