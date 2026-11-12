using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Manager
{
    // ============================================================================================================
    // Contratos (decisão 0035): o jogador assina por 1 a 3 temporadas. Dá para vender, rescindir (pagando multa), renovar (se ele
    // quiser) e, com uma cláusula de rescisão, outro clube pode levá-lo. Sem Unity. Todos os números são PROVISÓRIOS.
    // ============================================================================================================

    public static class Contratos
    {
        public const int ANOS_MIN = 1, ANOS_MAX = 3, SEMANAS_ANO = Mundo.SEMANAS_POR_TEMPORADA, SEMANAS_AVISO = 13;
        public const float PARTE_MULTA = 0.5f;            // rescindir: o clube paga metade dos salários que faltam
        public const float CLAUSULA_BASE = 1.0f, CLAUSULA_POR_ANO = 0.5f;   // cláusula = preço de tabela × (1 + 0,5 por ano de contrato)
        public const float AUMENTO_A_CONTRAGOSTO = 1.15f; // sem outro jogador para pôr no lugar, o contrato vence e ele fica por +15 % de salário
        public const float FATOR_PRAZO = 0.04f;           // cada ano fora do que ele quer custa 4 % (cobertura da proposta e salário da renovação)
        public const float LUVAS_RENOVACAO = 0.3f;        // na renovação ele pede 30 % das luvas de um jogador novo
        public const float CORTE_RENOVAR = -0.25f;        // desejo mínimo (de -1 a +1) para aceitar renovar
        public const float MORAL_MIN_RENOVAR = 30f;
        public const int OVERALL_COBICADO = 68;           // abaixo disso nenhum rival paga a cláusula
        public const double CHANCE_RIVAL_NORMAL = 0.006, CHANCE_RIVAL_INFELIZ = 0.04;   // por semana, por jogador cobiçado
        // jogador que pede para sair (decisão 0041)
        public const float MORAL_PEDE_SAIR = 25f;                     // abaixo disso ele pode pedir para sair
        public const double CHANCE_SAIR_INFELIZ = 0.06, CHANCE_SAIR_BANCO = 0.04;   // por semana (banco: reserva bom, parado e com moral < 45)
        public const int PRAZO_SAIDA = 4, SEMANAS_PERMANENCIA = 4;     // semanas até ele ir embora sozinho; bônus de permanência = 4 salários
        public const float MORAL_PERMANENCIA = 15f;

        /// <summary>Quantos anos o jogador prefere assinar: jovem quer segurança (3), veterano quer liberdade (1).</summary>
        public static int AnosDesejados(Jogador j) => j.idade <= 22 ? 3 : j.idade >= 29 ? 1 : 2;

        /// <summary>1,0 se o prazo é o que ele quer; menos a cada ano de diferença.</summary>
        public static float FatorPrazo(Jogador j, int anos) => 1f - FATOR_PRAZO * Math.Abs(Math.Clamp(anos, ANOS_MIN, ANOS_MAX) - AnosDesejados(j));

        /// <summary>O que um rival paga para levar o jogador no meio do contrato.</summary>
        public static int Clausula(Jogador j, int anos) => (int)(Math.Round(j.Preco * (CLAUSULA_BASE + CLAUSULA_POR_ANO * Math.Clamp(anos, ANOS_MIN, ANOS_MAX)) / 100.0) * 100);

        /// <summary>Contrato perto do fim vale menos para quem compra (o jogador sai de graça logo).</summary>
        public static float FatorVenda(int semanasRestantes) => semanasRestantes <= 0 || semanasRestantes >= SEMANAS_ANO ? 1f : semanasRestantes >= SEMANAS_AVISO ? 0.8f : 0.5f;

        /// <summary>A multa que o clube paga para rescindir antes do fim.</summary>
        public static int Multa(Jogador j) => (int)(j.Salario * Math.Max(0, j.contratoSemanas) * PARTE_MULTA);

        public static string Prazo(int semanas)
        {
            if (semanas <= 0) return "sem contrato";
            if (semanas < SEMANAS_ANO) return semanas + (semanas == 1 ? " semana" : " semanas");
            int anos = (semanas + SEMANAS_ANO - 1) / SEMANAS_ANO;
            return anos + (anos == 1 ? " temporada" : " temporadas");
        }

        public static void Assinar(Jogador j, int anos)
        {
            anos = Math.Clamp(anos <= 0 ? AnosDesejados(j) : anos, ANOS_MIN, ANOS_MAX);
            j.contratoSemanas = anos * SEMANAS_ANO; j.clausula = Clausula(j, anos);
        }
    }

    /// <summary>O que o jogador pensa de renovar agora (sem conversa: a resposta já vem calculada).</summary>
    public class PedidoRenovacao { public bool aceita; public int luvas, salario, anos; public string motivo; public float desejo; }

    public partial class Carreira
    {
        /// <summary>O que ele pede para renovar por <paramref name="anos"/> (0 = o prazo que ele prefere) e se aceita. Pesam o prestígio, a estrutura, as
        /// finanças e o clima de sempre, mais a moral dele.</summary>
        public PedidoRenovacao PedirRenovacao(Jogador j, int anos = 0)
        {
            anos = Math.Clamp(anos <= 0 ? Contratos.AnosDesejados(j) : anos, Contratos.ANOS_MIN, Contratos.ANOS_MAX);
            bool titular = Titular(j);
            var f = Negociador.Fatores(this, j, titular);
            float d = Negociador.Desejo(f);
            var (luvas, sal) = Negociador.Pedido(this, j, titular);
            float fora = 1f + Contratos.FATOR_PRAZO * Math.Abs(anos - Contratos.AnosDesejados(j));
            var p = new PedidoRenovacao
            {
                anos = anos, desejo = d,
                luvas = (int)(Math.Round(luvas * Contratos.LUVAS_RENOVACAO / 100.0) * 100),
                salario = (int)(Math.Round(sal * fora / 50.0) * 50),
            };
            if (j.moral < Contratos.MORAL_MIN_RENOVAR) { p.motivo = "Estou desmotivado e não quero ficar."; return p; }
            if (d < Contratos.CORTE_RENOVAR) { p.motivo = Negociador.Reclamacao(f.OrderBy(x => x.Efeito).First()); return p; }
            p.aceita = true; return p;
        }

        /// <summary>Renova o contrato (começa do zero, por <paramref name="anos"/>). Devolve a mensagem de erro, ou null se deu certo.</summary>
        public string Renovar(Jogador j, int anos = 0)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            var p = PedirRenovacao(j, anos);
            if (!p.aceita) return j.nome + " não quer renovar. " + p.motivo;
            if (dinheiro < p.luvas) return "Dinheiro insuficiente para as luvas da renovação.";
            ultimaVenda = null;
            if (p.luvas > 0) Lanca("Renovação de " + j.nome, -p.luvas);
            j.salarioAcordado = p.salario; Contratos.Assinar(j, p.anos);
            j.moral = Math.Min(100f, j.moral + Provisorio.MORAL_RENOVACAO);
            return null;
        }

        /// <summary>Rescinde o contrato antes do fim: paga a multa e o jogador sai do clube.</summary>
        public string Rescindir(Jogador j)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (elenco.Count <= Provisorio.TITULARES) return "É preciso manter pelo menos " + Provisorio.TITULARES + " jogadores.";
            int multa = Contratos.Multa(j);
            if (dinheiro < multa) return "Dinheiro insuficiente para pagar a multa de " + Fmt.Dinheiro(multa) + ".";
            ultimaVenda = null;
            ultimaRescisaoIndice = elenco.IndexOf(j); ultimaRescisaoMulta = multa; ultimaRescisao = j; ultimaRescisaoLancamento = null;
            ultimaRescisaoMoral = elenco.Where(o => o != j).ToDictionary(o => o, o => o.moral);
            if (multa > 0) { Lanca("Rescisão de " + j.nome + " (multa)", -multa); ultimaRescisaoLancamento = extrato[0]; }
            elenco.Remove(j);
            foreach (var o in elenco) o.moral = Math.Max(0f, o.moral - Provisorio.MORAL_RESCISAO);   // dispensar mexe com o grupo
            return null;
        }

        /// <summary>Paga um bônus (4 salários) para um jogador que pediu para sair ficar: a moral sobe e o pedido cai.</summary>
        public string Segurar(Jogador j)
        {
            if (!elenco.Contains(j)) return "Esse jogador não está no elenco.";
            if (j.querSair <= 0) return j.nome + " não pediu para sair.";
            int custo = j.Salario * Contratos.SEMANAS_PERMANENCIA;
            if (dinheiro < custo) return "Dinheiro insuficiente para o bônus de " + Fmt.Dinheiro(custo) + ".";
            ultimaVenda = null;
            Lanca("Bônus de permanência de " + j.nome, -custo);
            j.moral = Math.Min(100f, j.moral + Contratos.MORAL_PERMANENCIA); j.querSair = 0; j.motivoSaida = null;
            Anotar(j.nome + " aceitou ficar com o bônus de permanência.");
            return null;
        }

        // desfazer a última rescisão (o botão Dispensar mostra um aviso com Desfazer, como na venda)
        Jogador ultimaRescisao; int ultimaRescisaoIndice, ultimaRescisaoMulta; Lancamento ultimaRescisaoLancamento; Dictionary<Jogador, float> ultimaRescisaoMoral;

        public string DesfazerRescisao()
        {
            if (ultimaRescisao == null) return "Não há rescisão para desfazer.";
            if (elenco.Count >= ElencoMax) return "Elenco cheio: não há vaga para o jogador voltar.";
            dinheiro += ultimaRescisaoMulta;
            if (ultimaRescisaoLancamento != null) extrato.Remove(ultimaRescisaoLancamento);
            elenco.Insert(Math.Min(ultimaRescisaoIndice, elenco.Count), ultimaRescisao);
            foreach (var kv in ultimaRescisaoMoral) kv.Key.moral = kv.Value;
            ultimaRescisao = null;
            return null;
        }

        /// <summary>Passa uma semana nos contratos: os que vencem saem (ou ficam a contragosto), e um rival pode pagar a cláusula.</summary>
        void ProcessarContratos(ResumoSemana res)
        {
            // quem pediu para sair e foi ignorado vai embora de graça quando o prazo acaba (decisão 0041)
            foreach (var j in elenco.ToList())
            {
                if (j.querSair <= 0) continue;
                if (--j.querSair > 0) continue;
                j.motivoSaida = null;
                if (elenco.Count <= Provisorio.TITULARES)
                {
                    j.moral = Math.Max(0f, j.moral - 5f);
                    res.linhas.Add(j.nome + " queria sair, mas ficou porque você não tem quem pôr no lugar. A moral dele caiu.");
                }
                else
                {
                    elenco.Remove(j);
                    foreach (var o in elenco) o.moral = Math.Max(0f, o.moral - Provisorio.MORAL_RESCISAO);
                    res.linhas.Add(j.nome + " saiu do clube de graça porque o pedido dele foi ignorado. Você poderia ter vendido ou pago o bônus.");
                }
            }
            foreach (var j in elenco.ToList())
            {
                if (j.contratoSemanas <= 0) continue;
                j.contratoSemanas--;
                if (j.contratoSemanas == 0)
                {
                    if (elenco.Count <= Provisorio.TITULARES)
                    {
                        j.salarioAcordado = (int)(Math.Round(j.Salario * Contratos.AUMENTO_A_CONTRAGOSTO / 50.0) * 50);
                        Contratos.Assinar(j, 1);
                        res.linhas.Add(j.nome + " ficou mais um ano porque você não tem quem pôr no lugar, mas passou a ganhar " + Fmt.Dinheiro(j.Salario) + " por semana.");
                    }
                    else { elenco.Remove(j); res.linhas.Add(j.nome + " terminou o contrato e saiu do clube de graça."); }
                }
                else if (j.contratoSemanas == Contratos.SEMANAS_AVISO)
                    res.linhas.Add(j.nome + " termina o contrato em " + Contratos.SEMANAS_AVISO + " semanas: renove, ou ele sai de graça.");
            }
            // um rival paga a cláusula de quem é cobiçado (mais fácil quando o jogador está infeliz ou parado no banco)
            var sorteio = new Random(Seed * 7919 + Temporada * 100 + Semana);
            foreach (var j in elenco.ToList())
            {
                if (elenco.Count <= Provisorio.TITULARES) break;
                if (j.clausula <= 0 || j.Overall < Contratos.OVERALL_COBICADO) continue;
                bool infeliz = j.moral < 40f || (!Titular(j) && j.Overall >= 72);
                if (sorteio.NextDouble() >= (infeliz ? Contratos.CHANCE_RIVAL_INFELIZ : Contratos.CHANCE_RIVAL_NORMAL)) continue;
                Lanca("Cláusula de rescisão de " + j.nome + " (paga por outro clube)", j.clausula);
                res.linhas.Add("Outro clube pagou a cláusula de " + j.nome + " (" + Fmt.Dinheiro(j.clausula) + ") e o levou.");
                elenco.Remove(j);
            }
            // jogador infeliz (ou bom parado no banco) pede para sair; o manager tem PRAZO_SAIDA semanas para vender ou pagar o bônus
            foreach (var j in elenco.ToList())
            {
                if (elenco.Count <= Provisorio.TITULARES) break;
                if (j.querSair > 0) continue;
                bool banco = !Titular(j) && j.Overall >= Contratos.OVERALL_COBICADO && j.moral < 45f;
                double p = j.moral < Contratos.MORAL_PEDE_SAIR ? Contratos.CHANCE_SAIR_INFELIZ : banco ? Contratos.CHANCE_SAIR_BANCO : 0;
                if (p <= 0 || sorteio.NextDouble() >= p) continue;
                j.querSair = Contratos.PRAZO_SAIDA;
                j.motivoSaida = j.moral < Contratos.MORAL_PEDE_SAIR ? "está desmotivado" : "quer jogar e está no banco";
                res.linhas.Add(j.nome + " pediu para sair (" + j.motivoSaida + "). Venda, pague o bônus de " + Fmt.Dinheiro(j.Salario * Contratos.SEMANAS_PERMANENCIA) + " ou ele vai embora de graça em " + Contratos.PRAZO_SAIDA + " semanas.");
            }
        }
    }
}
