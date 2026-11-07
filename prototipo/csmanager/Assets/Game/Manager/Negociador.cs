using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Negociação de contratação (decisão 0026): em vez de um preço fixo, cada jogador do mercado decide pelo conjunto: o prestígio do
    // clube, se ele jogaria, a estrutura (Gaming House), as finanças e o clima do time, além do que é oferecido (luvas e salário).
    // Sem dependência do Unity. Todos os números são PROVISÓRIOS.
    // ============================================================================================================

    public class MsgNeg { public bool nos; public string texto; public int semana; public bool sistema; }

    public class Negociacao
    {
        public Jogador jogador;
        public readonly List<MsgNeg> msgs = new List<MsgNeg>();
        public int paciencia = Negociador.PACIENCIA;     // rodadas ruins que ele aguenta
        public bool encerrada, fechou;                    // encerrada: ele cansou (volta depois); fechou: contrato assinado
        public int bloqueadoAte;                          // semana em que ele volta a conversar
        public int luvasPedida, salarioPedido;            // o que ele pede agora
        public int ofertaLuvas, ofertaSalario, ofertaAnos; public bool promessa;   // a proposta que o manager está montando (ofertaAnos 0 = o prazo que ele prefere, decisão 0035)
        public bool contraPendente;                       // o último pedido dele é uma contraproposta que dá para aceitar
    }

    public enum RespostaTipo { Aceita, Contra, Recusa, Cansado }
    public class RespostaNeg { public RespostaTipo tipo; public string texto; public int luvas, salario; }

    public struct Fator
    {
        public string nome, texto; public float peso, valor;   // valor de -1 (contra) a +1 (a favor)
        public float Efeito => peso * valor;
    }

    public static class Negociador
    {
        public const int PACIENCIA = 3, SEMANAS_BLOQUEIO = 2, PROMESSA_SEMANAS = 4;
        public const float DESEJO_LUVAS = 0.35f, DESEJO_SALARIO = 0.2f;   // o quanto o desejo de jogar aqui abaixa o que ele pede

        /// <summary>Os fatores que pesam para o jogador agora (e o que ele acharia de uma promessa de vaga de titular).</summary>
        public static List<Fator> Fatores(Carreira c, Jogador j, bool prometeVaga)
        {
            var l = new List<Fator>();
            bool jovem = j.idade <= 22, estrela = j.traco == Traco.Estrela;

            // prestígio: circuito e posição no ranking; quem é muito bom quer um clube à altura
            float circ = c.CircuitoClube == Circuito.Elite ? 1f : c.CircuitoClube == Circuito.Pro ? 0.1f : -0.7f;
            float rank = 1f - 2f * (c.PosicaoRanking - 1) / (Mundo.TIMES - 1);
            float exigencia = Math.Clamp((j.Overall - 45) / 45f, 0f, 1f);
            l.Add(new Fator { nome = "Prestígio", peso = 1.2f, valor = Math.Clamp(0.5f * circ + 0.5f * rank - 0.7f * (exigencia - 0.4f), -1f, 1f),
                texto = "Circuito " + Mundo.NomeCircuito(c.CircuitoClube) + ", ranking #" + c.PosicaoRanking + ". Jogador bom quer clube forte." });

            // vaga: ele jogaria? (promessa de titular resolve, mas é uma obrigação)
            bool entraria = c.QuemSaiPara(j) != null;
            float vaga = entraria ? 0.7f : -0.7f; if (prometeVaga) vaga = Math.Max(vaga, 0.55f);
            l.Add(new Fator { nome = "Vaga", peso = estrela ? 1.8f : jovem ? 1.3f : 0.9f, valor = vaga,
                texto = entraria ? "Ele melhoraria o seu time e entraria como titular." : prometeVaga ? "Você prometeu a vaga de titular: se não cumprir, a moral dele cai." : "Hoje você já tem titulares melhores na função dele." + (estrela ? " Estrela não aceita o banco." : "") });

            // estrutura: a Gaming House (treino, recuperação); jovens valorizam mais
            l.Add(new Fator { nome = "Estrutura", peso = jovem ? 1.2f : 0.5f, valor = Math.Clamp((c.nivelGH - 2.5f) / 2.5f, -1f, 1f),
                texto = "Gaming House nível " + c.nivelGH + ". " + (jovem ? "Jovens querem evoluir." : "") });

            // finanças: o caixa cobre quantas semanas de despesa? (salário em dia)
            float semanas = c.dinheiro / (float)Math.Max(1, c.DespesaFixaPorSemana);
            l.Add(new Fator { nome = "Finanças", peso = 0.7f, valor = Math.Clamp(semanas / 15f - 1f, -1f, 1f),
                texto = "O caixa cobre " + (int)semanas + " semanas de despesas." });

            // clima: moral e entrosamento médios do elenco
            float moral = c.elenco.Count > 0 ? c.elenco.Average(x => x.moral) : 50f, entr = c.elenco.Count > 0 ? c.elenco.Average(x => x.entrosamento) : 50f;
            l.Add(new Fator { nome = "Clima", peso = j.traco == Traco.Lider ? 1.0f : 0.6f, valor = Math.Clamp(((moral - 50f) + (entr - 50f)) / 100f, -1f, 1f),
                texto = "Moral média " + (int)moral + ", entrosamento " + (int)entr + "." });
            return l;
        }

        /// <summary>De -1 (não quer nem conversar) a +1 (quer muito jogar aqui): a soma dos fatores pelos pesos.</summary>
        public static float Desejo(List<Fator> f) { float p = f.Sum(x => x.peso); return p <= 0f ? 0f : Math.Clamp(f.Sum(x => x.Efeito) / p, -1f, 1f); }

        public static string Interesse(float desejo) => desejo >= 0.25f ? "ALTO" : desejo >= -0.15f ? "MÉDIO" : "BAIXO";

        /// <summary>O que ele pede agora (luvas e salário por semana): o valor de mercado ajustado pelo desejo de jogar aqui.</summary>
        public static (int luvas, int salario) Pedido(Carreira c, Jogador j, bool prometeVaga = false)
        {
            float d = Desejo(Fatores(c, j, prometeVaga));
            float vet = j.idade >= 28 ? 1.08f : 1f;   // veterano quer mais salário e abre mão de luvas
            int luvas = (int)Math.Round(j.Preco * (1f - DESEJO_LUVAS * d) / (j.idade >= 28 ? 1.1f : 1f) / 100.0) * 100;
            int sal = (int)Math.Round(j.SalarioBase * (1f - DESEJO_SALARIO * d) * vet * (j.traco == Traco.Estrela ? 1.1f : 1f) / 50.0) * 50;
            return (Math.Max(0, luvas), Math.Max(50, sal));
        }

        /// <summary>Quanto a proposta cobre do que ele pede agora (1,0 = aceita). Sem efeito colateral: serve para a tela mostrar antes de enviar.</summary>
        public static float Cobertura(Carreira c, Negociacao n, int luvas, int salario, bool prometeVaga, int anos = 0)
        {
            var (pl, ps) = Pedido(c, n.jogador, prometeVaga);
            return (0.65f * salario / Math.Max(1f, ps) + 0.35f * (pl <= 0 ? 1f : luvas / (float)pl)) * Contratos.FatorPrazo(n.jogador, anos <= 0 ? Contratos.AnosDesejados(n.jogador) : anos);
        }

        public static Negociacao Abrir(Carreira c, Jogador j)
        {
            if (c.negociacoes.TryGetValue(j, out var existente)) return existente;
            var n = new Negociacao { jogador = j };
            var (l, s) = Pedido(c, j);
            n.luvasPedida = l; n.salarioPedido = s; n.ofertaLuvas = l; n.ofertaSalario = s;
            var f = Fatores(c, j, false); float d = Desejo(f);
            string abre = d >= 0.25f ? "Fala, manager! Gostei do seu projeto." : d >= -0.15f ? "Oi, manager. Estou ouvindo propostas." : "Oi. Vou ser direto: ainda não estou convencido do seu clube.";
            n.msgs.Add(new MsgNeg { texto = abre + " Pelo que vejo, quero " + Fmt.Dinheiro(l) + " de luvas e " + Fmt.Dinheiro(s) + " por semana.", semana = c.Semana });
            n.contraPendente = true;
            c.negociacoes[j] = n;
            return n;
        }

        /// <summary>O manager faz uma proposta. Devolve a resposta; se aceitou, já assina o contrato.</summary>
        public static RespostaNeg Propor(Carreira c, Negociacao n, int luvas, int salario, bool prometeVaga, int anos = 0)
        {
            var j = n.jogador;
            n.msgs.Add(new MsgNeg { nos = true, semana = c.Semana, texto = "Proposta: " + Fmt.Dinheiro(luvas) + " de luvas e " + Fmt.Dinheiro(salario) + " por semana" + (prometeVaga ? ", com a vaga de titular garantida." : ".") });
            var f = Fatores(c, j, prometeVaga);
            var (pl, ps) = Pedido(c, j, prometeVaga);
            float r = Cobertura(c, n, luvas, salario, prometeVaga, anos);
            var pior = f.OrderBy(x => x.Efeito).First(); var melhor = f.OrderByDescending(x => x.Efeito).First();

            RespostaNeg resp;
            if (r >= 1f)
            {
                string erro = c.Contratar(j, luvas, salario, anos);
                if (erro != null) { resp = new RespostaNeg { tipo = RespostaTipo.Recusa, texto = erro }; }
                else
                {
                    if (prometeVaga && c.QuemSaiPara(j) == null) { }   // a promessa fica registrada abaixo
                    if (prometeVaga) j.promessaSemanas = PROMESSA_SEMANAS;
                    n.fechou = true; n.contraPendente = false;
                    resp = new RespostaNeg { tipo = RespostaTipo.Aceita, texto = "Fechado! " + Elogio(melhor) + " Vou chegar pronto para jogar.", luvas = luvas, salario = salario };
                }
            }
            else if (r >= 0.8f)
            {
                int cl = Arredonda(luvas + (pl - luvas) * 0.5f, 100), cs = Arredonda(salario + (ps - salario) * 0.5f, 50);
                n.luvasPedida = cl; n.salarioPedido = cs; n.contraPendente = true;
                resp = new RespostaNeg { tipo = RespostaTipo.Contra, luvas = cl, salario = cs, texto = "Estamos perto. Fecho por " + Fmt.Dinheiro(cl) + " de luvas e " + Fmt.Dinheiro(cs) + " por semana." };
            }
            else
            {
                n.paciencia--;
                if (n.paciencia <= 0)
                {
                    n.encerrada = true; n.bloqueadoAte = c.Semana + SEMANAS_BLOQUEIO; n.contraPendente = false;
                    resp = new RespostaNeg { tipo = RespostaTipo.Cansado, texto = "Já deu. Vou ouvir outros clubes. " + Reclamacao(pior) };
                }
                else if (r >= 0.55f)
                {
                    int cl = Arredonda(pl * 0.97f, 100), cs = Arredonda(ps * 0.97f, 50);
                    n.luvasPedida = cl; n.salarioPedido = cs; n.contraPendente = true;
                    resp = new RespostaNeg { tipo = RespostaTipo.Contra, luvas = cl, salario = cs, texto = Reclamacao(pior) + " O mínimo para mim é " + Fmt.Dinheiro(cl) + " de luvas e " + Fmt.Dinheiro(cs) + " por semana." };
                }
                else
                {
                    n.contraPendente = false;
                    resp = new RespostaNeg { tipo = RespostaTipo.Recusa, texto = "Está longe do que eu valho. " + Reclamacao(pior) };
                }
            }
            n.msgs.Add(new MsgNeg { texto = resp.texto, semana = c.Semana });
            if (resp.tipo == RespostaTipo.Aceita) n.msgs.Add(new MsgNeg { sistema = true, semana = c.Semana, texto = j.nome + " assinou: " + Fmt.Dinheiro(luvas) + " de luvas e " + Fmt.Dinheiro(salario) + " por semana." });
            return resp;
        }

        /// <summary>Aceitar a contraproposta que ele fez (equivale a uma proposta igual ao pedido).</summary>
        public static RespostaNeg AceitarContra(Carreira c, Negociacao n) => Propor(c, n, n.luvasPedida, n.salarioPedido, n.promessa, n.ofertaAnos);

        static int Arredonda(float v, int passo) => (int)Math.Round(v / passo) * passo;

        public static string Reclamacao(Fator f) => f.nome switch
        {
            "Prestígio" => "Quero jogar num clube maior; o seu ainda não me convence.",
            "Vaga" => "Quero ser titular, e hoje você já tem gente melhor na minha função.",
            "Estrutura" => "A estrutura da Gaming House é fraca para eu evoluir.",
            "Finanças" => "Vi que o caixa está curto: preciso de garantia de que o salário sai em dia.",
            "Clima" => "O clima no time não está bom, e isso pesa.",
            _ => "O valor está abaixo do que eu quero.",
        };

        static string Elogio(Fator f) => f.nome switch
        {
            "Prestígio" => "Seu clube tem nome.",
            "Vaga" => "Gostei da chance de jogar.",
            "Estrutura" => "A estrutura me atrai.",
            "Finanças" => "Vejo um clube estável.",
            "Clima" => "O time parece unido.",
            _ => "Gostei do projeto.",
        };
    }
}
