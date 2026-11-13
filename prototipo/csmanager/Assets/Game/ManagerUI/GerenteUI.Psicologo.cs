using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Psicólogo do time (decisão 0039): contratar um do mercado de psicólogos, ler o relatório da semana de cada jogador e
    /// usar as conversas individuais em quem está abalado. Sem psicólogo, a tela mostra o mercado e o que ele faria.</summary>
    public partial class GerenteUI
    {
        void TelaPsicologo()
        {
            var p = c.psicologo;
            if (p == null) { TelaSemPsicologo(); return; }

            // quem trabalha no clube
            var card = E("scout-card");
            var topo = E("scout-topo");
            var nome = E("linha-h"); nome.Add(L(p.nome, "f20", "negrito"));
            var est = L(Psicologos.Estrelas(p.qualidade), "f16", "laranja"); est.style.marginLeft = 12; est.pickingMode = PickingMode.Position;
            ComDica(est, QueFazAQualidade(p.qualidade));
            nome.Add(est);
            topo.Add(nome);
            var contrato = L(Fmt.Dinheiro(p.salario) + " por semana · contrato " + Contratos.Prazo(p.contratoSemanas), "f14", p.contratoSemanas <= Contratos.SEMANAS_AVISO ? "laranja" : "suave");
            contrato.style.marginLeft = 12; topo.Add(contrato);
            card.Add(topo);
            card.Add(L("Quedas de moral " + (int)Math.Round((1f - c.FatorQuedaMoral) * 100f) + " % menores. Quem está abaixo do normal sobe " + (Psicologos.RECUPERACAO_POR_ESTRELA * p.qualidade).ToString("0.0").Replace('.', ',') + " por semana.", "f15", "suave", "quebra"));
            var acoes = E("tarefas");
            if (c.PodeRenovarPsicologo) acoes.Add(BotaoMini("Renovar por 1 temporada", () => Resposta(c.RenovarPsicologo(1), "Contrato renovado. Salário agora " + Fmt.Dinheiro(c.psicologo?.salario ?? 0) + " por semana."), false, true, "check"));
            int multa = Psicologos.Multa(p);
            var dem = BotaoMini("Demitir" + (multa > 0 ? " (multa " + Fmt.Dinheiro(multa) + ")" : ""), () => Resposta(c.DemitirPsicologo(), "Psicólogo demitido."));
            ComDica(dem, "Demitir antes do fim paga metade dos salários que faltam. O relatório e as conversas param na hora.");
            acoes.Add(dem);
            card.Add(acoes);
            conteudo.Add(card);

            // o relatório da semana
            var tit = L("Relatório da semana", "h2", "topo-h2"); tit.pickingMode = PickingMode.Position;
            ComDica(tit, "A moral (0 a 100) muda a habilidade na partida: neutra em 60, até +3 % em 100 e -4,5 % em 0. Um psicólogo de 2 estrelas ou mais diz a causa; de 4 ou mais, prevê o que vem.");
            conteudo.Add(tit);
            int livres = c.ConversasRestantes;
            conteudo.Add(L("Conversas individuais nesta semana: " + livres + " de " + c.ConversasPorSemana + ". Chance de dar certo: " + (int)Math.Round(Psicologos.ChanceConversa(p.qualidade) * 100f) + " %.", "f15", livres > 0 ? "verde" : "suave"));
            foreach (var r in c.Relatorio()) conteudo.Add(CartaoRelatorio(r, livres > 0));
        }

        VisualElement CartaoRelatorio(RelatorioJogador r, bool podeConversar)
        {
            var j = r.jogador;
            var card = E("intel-card");
            var topo = E("intel-topo");
            var nome = E("linha-h");
            nome.Add(L(j.nome, "f18", "negrito"));
            var papel = L((c.Titular(j) ? "Titular" : "Reserva") + " · " + Fmt.Funcao(j.funcao), "f14", "suave"); papel.style.marginLeft = 12; nome.Add(papel);
            var selo = Selo(Psicologos.NomeEmocao(r.emocao).ToUpperInvariant(), CorEmocao(r.emocao)); selo.style.marginLeft = 12; nome.Add(selo);
            topo.Add(nome);
            var mor = E("linha-h");
            mor.Add(L("Moral " + (int)j.moral, "f14", "suave"));
            var barra = Barra((int)j.moral, true); barra.style.marginLeft = 10; barra.style.width = 120; mor.Add(barra);
            topo.Add(mor);
            card.Add(topo);

            card.Add(L(r.efeito, "f15", j.moral >= Provisorio.MORAL_NEUTRA ? "verde" : "laranja"));
            if (r.causa != null) card.Add(L(r.causa, "f15", "quebra"));
            else card.Add(L("Um psicólogo de 2 estrelas ou mais diria o porquê.", "oculto-txt"));
            if (r.previsao != null) { var pv = L(r.previsao, "f15", "laranja", "quebra"); pv.style.marginTop = 4; card.Add(pv); }

            if (j.moral < Psicologos.CONVERSA_MORAL_MAX)
            {
                var jj = j;
                var b = BotaoMini("Conversa individual", () =>
                {
                    string erro = c.Conversar(jj, out bool deu);
                    Resposta(erro, deu ? jj.nome + " saiu mais tranquilo da conversa (moral " + (int)jj.moral + ")." : "A conversa com " + jj.nome + " não mudou nada desta vez.");
                }, false, podeConversar, "help");
                b.style.alignSelf = Align.FlexStart; b.style.marginTop = 10;
                ComDica(b, podeConversar ? "Gasta uma conversa da semana. Se der certo, a moral sobe " + (Psicologos.CONVERSA_GANHO_BASE + c.psicologo.qualidade).ToString("0") + "." : "As conversas desta semana acabaram. Voltam na semana que vem.");
                card.Add(b);
            }
            return card;
        }

        void TelaSemPsicologo()
        {
            var cartao = E("cartao");
            cartao.Add(L("O CLUBE NÃO TEM PSICÓLOGO", "cartao-tit"));
            cartao.Add(L("Um psicólogo amortece as quedas de moral, puxa de volta quem está abalado, conversa com um jogador por semana e escreve um relatório de cada um. Quanto melhor, mais caro: é escolher entre gastar no mental ou em jogador.", "f16", "quebra"));
            if (c.nivelGH < Psicologos.NIVEL_GH_SALA) cartao.Add(L("A sala do psicólogo (Gaming House nível " + Psicologos.NIVEL_GH_SALA + ") dá uma conversa a mais por semana.", "f15", "suave", "quebra"));
            conteudo.Add(cartao);

            conteudo.Add(L("Psicólogos disponíveis", "h2"));
            if (c.psicologosMercado.Count == 0) { conteudo.Add(L("Nenhum disponível agora. O mercado se renova na próxima temporada.", "f15", "suave")); return; }
            var fileira = E("cols2");
            foreach (var p in c.psicologosMercado)
            {
                var pp = p;
                var card = E("scout-card");
                var topo = E("scout-topo");
                topo.Add(L(p.nome, "f18", "negrito"));
                var est = L(Psicologos.Estrelas(p.qualidade), "f16", "laranja"); est.style.marginLeft = 12; topo.Add(est);
                card.Add(topo);
                int despesa = c.DespesaFixaPorSemana;
                string peso = despesa > 0 ? " (+" + (int)Math.Round(100.0 * p.salario / despesa) + " % nas despesas da semana)" : "";
                card.Add(L(Fmt.Dinheiro(p.salario) + " por semana" + peso, "f15", "suave"));
                card.Add(L(QueFazAQualidade(p.qualidade), "f14", "quebra"));
                var acoes = E("tarefas");
                acoes.Add(BotaoMini("Contratar por 1 temporada", () => Resposta(c.ContratarPsicologo(pp, 1), pp.nome + " é o psicólogo do clube."), false, true, "plus"));
                acoes.Add(BotaoMini("Por 2 temporadas", () => Resposta(c.ContratarPsicologo(pp, 2), pp.nome + " é o psicólogo do clube por 2 temporadas.")));
                card.Add(acoes);
                fileira.Add(card);
            }
            conteudo.Add(fileira);
            conteudo.Add(L("O salário entra nas despesas de toda semana. Demitir antes do fim paga metade dos salários que faltam.", "legenda"));
        }

        static string QueFazAQualidade(int q)
        {
            string s = "Quedas de moral " + (int)Math.Round(Psicologos.PROTECAO_POR_ESTRELA * q * 100f) + " % menores; conversa dá certo " + (int)Math.Round(Psicologos.ChanceConversa(q) * 100f) + " % das vezes.";
            if (q >= Psicologos.QUALIDADE_PREVISAO) s += " O relatório diz a causa e prevê o que vem.";
            else if (q >= Psicologos.QUALIDADE_CAUSA) s += " O relatório diz a causa.";
            else s += " O relatório diz só a emoção.";
            return s;
        }

        static string CorEmocao(Emocao e) => e == Emocao.Confiante || e == Emocao.Tranquilo ? "verde" : e == Emocao.Ansioso ? "laranja" : "vermelho";
    }
}
