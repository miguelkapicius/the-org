using System;
using System.Collections.Generic;
using System.Text;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO — A PEÇA ÚNICA DE DECISÃO (PLANO_SIMULACAO.md §4.1 "Decisao" e §9.1; o Gabriel, 12/11: "pensa em todas as escolhas em
    /// todas as situações; todas podem ser jogadas de vários jeitos; ensina o bot essas opções, de acordo com crença e juiz"; "ele pode
    /// atravessar sozinho no shift sim, depende do momento").
    ///
    /// Toda situação do catálogo (Tools/pesquisa/SITUACOES_E_OPCOES.md) é decidida do mesmo jeito, por esta classe:
    ///   1. a situação é reconhecida pelo que o jogador viu, ouviu ou recebeu por call (crença, nunca a verdade);
    ///   2. TODAS as jogadas possíveis entram na lista — nenhuma é proibida por regra; a que não dá pra fazer agora (sem a granada, sem
    ///      tempo) simplesmente não entra;
    ///   3. cada uma recebe um VALOR (a chance de sair bem, estimada pelo Avaliador (o juiz) com a crença do jogador, menos o que ela custa);
    ///   4. o jogador faz a de maior valor e FICA nela (apego) até a situação mudar ou outra ficar claramente melhor — sem sorteio;
    ///   5. a escolha é explicada com as três melhores e o valor de cada uma (pensamento, caixa-preta, auditoria).
    /// Um jogador pior erra aqui por causa: crença errada entra no valor, e o ruído de avaliação (quando os atributos entrarem) mexe no
    /// valor antes da comparação — nunca um dado escolhendo a jogada.
    /// </summary>
    public sealed class Escolha
    {
        public struct Opcao { public int id; public string nome; public float valor; public string porque; }

        public string situacao = ""; public int chave = int.MinValue;   // chave: o que identifica ESTA ocorrência (id do inimigo, da smoke, do bomb...)
        public int atual = -1; public float desde = -99;                 // a jogada em que ele está e desde quando
        readonly List<Opcao> ops = new List<Opcao>(12);

        /// <summary>Começa a avaliar. Se a situação ou a chave mudou, o compromisso anterior acaba.</summary>
        public void Comeca(string situacao, int chave)
        {
            if (situacao != this.situacao || chave != this.chave) { this.situacao = situacao; this.chave = chave; atual = -1; }
            ops.Clear();
        }
        /// <summary>Põe uma jogada na lista (só as que dá pra fazer agora).</summary>
        public void Op(int id, string nome, float valor, string porque = null) => ops.Add(new Opcao { id = id, nome = nome, valor = valor, porque = porque });
        public int Quantas => ops.Count;

        /// <summary>Escolhe a de maior valor; a jogada atual ganha <paramref name="apego"/> (compromisso). Devolve o id, ou -1 sem opções.
        /// <paramref name="mudou"/> = trocou de jogada agora (hora de pensar em voz alta e registrar).</summary>
        public int Decide(float t, out bool mudou, float apego = 0.08f)
        {
            mudou = false; if (ops.Count == 0) return -1;
            int m = 0; float mv = float.NegativeInfinity;
            for (int i = 0; i < ops.Count; i++) { float v = ops[i].valor + (ops[i].id == atual ? apego : 0); if (v > mv) { mv = v; m = i; } }
            if (ops[m].id != atual) { atual = ops[m].id; desde = t; mudou = true; }
            return atual;
        }

        /// <summary>A escolha e as duas seguintes, com o valor de cada uma: "bango e entro 0,62 · peço a flash 0,57 · abro seco 0,31".</summary>
        public string Explica()
        {
            var o = new List<Opcao>(ops); o.Sort((a, b) => (b.id == atual ? 1 : 0) != (a.id == atual ? 1 : 0) ? (b.id == atual ? 1 : -1) : b.valor.CompareTo(a.valor));
            var sb = new StringBuilder(96);
            for (int i = 0; i < o.Count && i < 3; i++) { if (i > 0) sb.Append(" · "); sb.Append(o[i].nome).Append(' ').Append(o[i].valor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)); }
            string pq = null; foreach (var x in ops) if (x.id == atual) pq = x.porque;
            return pq != null ? sb.Append(" — ").Append(pq).ToString() : sb.ToString();
        }
        public string NomeAtual { get { foreach (var x in ops) if (x.id == atual) return x.nome; return ""; } }
    }

    /// <summary>
    /// O JUIZ: as contas que toda decisão usa pra dar valor a uma jogada, num lugar só. Os números de duelo vêm da bancada do próprio
    /// jogo (Logs/bots/tabela_duelo_teste300_dados.txt, 1.761 duelos): quem já está com a mira no outro +1,21 (escala logística); colega
    /// do lado +0,50; cego −3,17; chegando andando −0,34. O que a bancada não mede está marcado como estimativa.
    /// </summary>
    public static class Avaliador
    {
        public const float K_MIRA = 1.208f, K_COLEGA = 0.496f, K_CEGO = 3.170f, K_CHEGANDO = 0.336f, K_VIDA_MEDIA = 0.602f, K_VIDA_BAIXA = 1.124f;
        public static float P(float logit) => 1f / (1f + MathF.Exp(-logit));
        public static float Logit(float p) { p = Math.Clamp(p, 0.02f, 0.98f); return MathF.Log(p / (1 - p)); }

        /// <summary>Quanto a arma rende numa distância (0..1) — estimativa pelo jeito que o jogo trata cada arma (Duelo3.Rende).</summary>
        public static float Rende(string arma, float d) => Duelo3.Rende(arma, d);

        /// <summary>Chance de eu ganhar um duelo, pela situação que EU acredito: armas e distância (base), quem está com a mira em quem,
        /// quem está cego, quem chega andando, colega do lado e vida. <paramref name="crencaEleMira"/> 0..1: quanto acredito que ele me
        /// espera com a mira no lugar por onde eu apareço.</summary>
        public static float Duelo(string minhaArma, string armaDele, float dist, float crencaEleMira = 0, bool euMiroNele = false, bool euChegando = false,
                                  bool eleCego = false, bool euCego = false, bool colegaComigo = false, bool colegaComEle = false, int minhaVida = 100, int vidaDele = 100)
        {
            float a = Rende(minhaArma, dist), b = Rende(armaDele ?? "ak", dist);
            float l = Logit(a / (a + b));
            l += -K_MIRA * crencaEleMira + (euMiroNele ? K_MIRA : 0) - (euChegando ? K_CHEGANDO : 0);
            l += (eleCego ? K_CEGO : 0) - (euCego ? K_CEGO : 0) + (colegaComigo ? K_COLEGA : 0) - (colegaComEle ? K_COLEGA : 0);
            l += -(minhaVida < 30 ? K_VIDA_BAIXA : minhaVida < 70 ? K_VIDA_MEDIA : 0) + (vidaDele < 30 ? K_VIDA_BAIXA : vidaDele < 70 ? K_VIDA_MEDIA : 0);
            return P(l);
        }

        /// <summary>Valor de uma jogada que acaba em duelo: a chance de ganhar, mais um pedaço do round que a troca devolve se eu cair com
        /// colega perto (estimativa: 0,30), menos o tempo que ela gasta (0,015 por segundo: o adversário também se mexe).</summary>
        public static float Jogada(float chance, bool temTroca, float segundos = 0) => chance + (temTroca ? 0.30f * (1 - chance) : 0) - 0.015f * segundos;

        /// <summary>Quanto o relógio aperta (0 = folgado, 1 = não dá mais): <paramref name="preciso"/> segundos pra fazer o que falta
        /// contra <paramref name="tenho"/> segundos no relógio do round/da bomba.</summary>
        public static float Aperto(float tenho, float preciso) => Math.Clamp(1f - (tenho - preciso) / 20f, 0f, 1f);
    }
}
