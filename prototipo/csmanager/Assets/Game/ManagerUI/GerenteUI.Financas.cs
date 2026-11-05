using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Finanças com empréstimo e falência (decisão 0015) e os níveis da Gaming House (decisão 0016).</summary>
    public partial class GerenteUI
    {
        // ================================================================================================ finanças
        void TelaFinancas()
        {
            int sal = c.SalariosPorSemana, manut = c.GH.manutencao, parc = c.emprestimos.Sum(e => e.parcela), patro = c.PatrocinioPorSemana;
            int liquido = patro - sal - manut - parc;

            var linha = E("resumos");
            linha.Add(Resumo("Saldo", Fmt.Dinheiro(c.dinheiro), c.dinheiro < 0 ? "vermelho" : "t"));
            linha.Add(Resumo("Fluxo semanal (sem prêmios)", (liquido >= 0 ? "+" : "") + Fmt.Dinheiro(liquido), liquido >= 0 ? "verde" : "vermelho"));
            linha.Add(Resumo("Folga do caixa", (int)c.SemanasDeFolego + " semanas", c.SemanasDeFolego < 4 ? "laranja" : "t"));
            conteudo.Add(linha);

            if (c.dinheiro < 0)
                conteudo.Add(Banner0("Saldo NEGATIVO há " + c.semanasNegativo + " semana(s). Na " + Provisorio.SEMANAS_ATE_FALENCIA + "ª semana negativa o clube vai à falência. Pegue um empréstimo, venda jogadores ou ganhe um prêmio.", true));

            // fluxo da semana
            conteudo.Add(L("O que entra e sai por semana", "h2"));
            var cartao = E("cartao"); cartao.style.paddingTop = 10; cartao.style.paddingBottom = 10;
            void F(string rot, int v, string dica = null)
            {
                var l = E("fluxo"); l.Add(L(rot, "fluxo-rot")); l.Add(L((v >= 0 ? "+" : "") + Fmt.Dinheiro(v), "fluxo-val", v >= 0 ? "verde" : "vermelho"));
                if (dica != null) l.Add(L(dica, "f13", "apagado")); cartao.Add(l);
            }
            F("Patrocínio", patro, "cresce com o circuito (" + Mundo.NomeCircuito(c.CircuitoClube) + ") e o nível da Gaming House");
            F("Salários (" + c.elenco.Count + " jogadores)", -sal);
            F("Manutenção da Gaming House", -manut, c.GH.nome);
            if (parc > 0) F("Parcelas de empréstimo", -parc, c.emprestimos.Count + " empréstimo(s)");
            var tot = E("fluxo"); tot.Add(L("Saldo da semana", "fluxo-rot", "negrito")); tot.Add(L((liquido >= 0 ? "+" : "") + Fmt.Dinheiro(liquido), "fluxo-val", liquido >= 0 ? "verde" : "vermelho")); tot.Add(L("os prêmios dos campeonatos entram quando eles terminam", "f13", "apagado")); cartao.Add(tot);
            conteudo.Add(cartao);

            // empréstimo
            conteudo.Add(L("Empréstimo bancário", "h2"));
            var emp = E("cartao");
            emp.Add(L("LIMITE DO CIRCUITO " + Mundo.NomeCircuito(c.CircuitoClube).ToUpperInvariant() + ": " + Fmt.Dinheiro(c.LimiteEmprestimo) + " · DISPONÍVEL: " + Fmt.Dinheiro(c.EmprestimoDisponivel) + " · A PAGAR: " + Fmt.Dinheiro(c.Devido), "cartao-tit"));
            if (c.emprestimos.Count == 0) emp.Add(L("Nenhum empréstimo. O banco cobra " + (int)(Provisorio.JURO_EMPRESTIMO * 100) + "% de juros, pagos em " + Provisorio.PARCELAS_EMPRESTIMO + " parcelas semanais.", "f15", "suave", "quebra"));
            foreach (var e in c.emprestimos.ToList())
            {
                var l = E("emp-linha");
                l.Add(L("Principal " + Fmt.Dinheiro(e.principal), "f16", "negrito"));
                l.Add(L("parcela " + Fmt.Dinheiro(e.parcela) + " por semana", "f15", "suave"));
                var rest = L(e.restantes + " parcela(s) restante(s)", "f15", "suave"); rest.style.flexGrow = 1; l.Add(rest);
                var ee = e;
                l.Add(Botao("Quitar por " + Fmt.Dinheiro(c.CustoQuitar(e)), () => Resposta(c.Quitar(ee), "Empréstimo quitado."), c.dinheiro >= c.CustoQuitar(e)));
                emp.Add(l);
            }
            var valores = E("valores-emp");
            valores.Add(L("Pegar:", "f15", "suave")); valores.Children().First().style.marginRight = 12;
            foreach (int v in new[] { 25000, 50000, 100000, 250000 })
            {
                int vv = v;
                valores.Add(BotaoMini(Fmt.Dinheiro(v), () => Resposta(c.PegarEmprestimo(vv), "Empréstimo de " + Fmt.Dinheiro(vv) + " recebido. Parcelas de " + Fmt.Dinheiro((int)Math.Ceiling(vv * (1f + Provisorio.JURO_EMPRESTIMO) / Provisorio.PARCELAS_EMPRESTIMO / 10f) * 10) + " por semana."), vv <= c.EmprestimoDisponivel));
            }
            valores.Add(BotaoMini("Tudo o que dá (" + Fmt.Dinheiro(c.EmprestimoDisponivel) + ")", () => { int t = c.EmprestimoDisponivel; Resposta(c.PegarEmprestimo(t), "Empréstimo de " + Fmt.Dinheiro(t) + " recebido."); }, c.EmprestimoDisponivel > 0));
            emp.Add(valores);
            conteudo.Add(emp);

            conteudo.Add(L("Campeonatos disputados", "h2"));
            if (c.titulos.Count == 0) conteudo.Add(L("Nenhum campeonato terminou ainda.", "f15", "suave"));
            foreach (var t in c.titulos.Take(6))
            {
                var r = E("lista-l");
                var rd = L("Semana " + t.semana, "f15", "suave"); rd.style.width = 110; r.Add(rd);
                var nm = L(t.nome, "f16", "grow"); r.Add(nm);
                r.Add(L(Fmt.Ordinal(t.posicao) + " de " + t.times, "f15", "suave")); var pr = L("+" + Fmt.Dinheiro(t.premio), "f16", "negrito", "verde"); pr.style.width = 130; pr.style.marginLeft = 16; r.Add(pr);
                conteudo.Add(r);
            }

            conteudo.Add(L("Extrato", "h2"));
            if (c.extrato.Count == 0) conteudo.Add(L("Sem movimentações ainda.", "f15", "suave"));
            foreach (var l in c.extrato.Take(14))
            {
                var r = E("lista-l");
                var rd = L("Semana " + l.semana, "f15", "suave"); rd.style.width = 110; r.Add(rd);
                var d = L(l.descricao, "f16", "grow"); r.Add(d);
                r.Add(L((l.valor > 0 ? "+" : "") + Fmt.Dinheiro(l.valor), "f16", "negrito", l.valor >= 0 ? "verde" : "vermelho"));
                conteudo.Add(r);
            }
        }

        static VisualElement Banner0(string texto, bool erro)
        {
            var b = E("banner", erro ? "erro" : "ok");
            b.Add(L(texto));
            return b;
        }

        // ================================================================================================ gaming house
        void TelaGamingHouse()
        {
            var resumos = E("resumos");
            resumos.Add(Resumo("Nível atual: " + c.GH.nome, c.nivelGH + " de " + GamingHouse.MAX, "t"));
            resumos.Add(Resumo("Manutenção", Fmt.Dinheiro(c.GH.manutencao) + " por semana", "t"));
            resumos.Add(Resumo("Próximo nível", c.nivelGH >= GamingHouse.MAX ? "Nível máximo" : Fmt.Dinheiro(c.CustoProximoNivelGH), c.nivelGH >= GamingHouse.MAX ? "verde" : "t"));
            conteudo.Add(resumos);

            for (int n = 1; n <= GamingHouse.MAX; n++)
            {
                var nv = GamingHouse.De(n);
                bool atual = n == c.nivelGH, feito = n < c.nivelGH, proximo = n == c.nivelGH + 1;
                var card = E("gh-nivel"); if (atual) card.AddToClassList("atual"); if (feito) card.AddToClassList("feito");
                var topo = E("gh-topo");
                var esq = E("linha-h");
                var num = E("gh-num"); num.Add(L(n.ToString())); esq.Add(num);
                var tx = E(); tx.Add(L(nv.nome, "f20", "negrito")); tx.Add(L("Manutenção " + Fmt.Dinheiro(nv.manutencao) + " por semana" + (n > 1 ? " · custo " + Fmt.Dinheiro(nv.custo) : " · já vem de graça"), "f14", "suave")); esq.Add(tx);
                topo.Add(esq);
                topo.Add(atual ? Selo("NÍVEL ATUAL", "verde") : feito ? Selo("CONQUISTADO") : Selo("BLOQUEADO"));
                card.Add(topo);
                foreach (var vnt in nv.vantagens) { var l = E("gh-vant"); l.Add(E("gh-ponto")); l.Add(L(vnt, "f15", feito || atual ? "t" : "suave")); card.Add(l); }
                if (proximo)
                {
                    var rod = E("gh-rodape");
                    rod.Add(L("Saldo depois: " + Fmt.Dinheiro(c.dinheiro - nv.custo) + " (" + (int)Math.Max(0, (c.dinheiro - nv.custo) / (double)Math.Max(1, c.SalariosPorSemana + nv.manutencao)) + " semanas de despesas)", "f15", "suave"));
                    rod.Add(Botao("Subir de nível", () => Resposta(c.SubirGamingHouse(), "Gaming House no nível " + c.nivelGH + " (" + c.GH.nome + ")."), c.dinheiro >= nv.custo, true));
                    card.Add(rod);
                }
                conteudo.Add(card);
            }
            conteudo.Add(L("Custos, manutenção e efeitos são provisórios (decisões 0015 e 0016). Outras ideias para depois: sala de imprensa (patrocínio), academia de base (jogadores jovens) e laboratório de demos (treinar contra a IA de um adversário).", "legenda"));
        }
    }
}
