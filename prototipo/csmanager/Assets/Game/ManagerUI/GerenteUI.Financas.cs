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
        /// <summary>Uma peça de resumo: o rótulo pequeno, o número grande e, se houver, uma frase de apoio.</summary>
        static VisualElement PecaFin(string rotulo, string valor, string cor, string apoio = null)
        {
            var p = E("fin-peca");
            p.Add(L(rotulo, "dado-rot"));
            p.Add(L(valor, "fin-valor", cor));
            if (apoio != null) p.Add(L(apoio, "f13", "apagado", "quebra"));
            return p;
        }

        /// <summary>Uma linha do fluxo da semana: o nome, uma barra do tamanho do valor (a maior linha enche) e o valor.</summary>
        static VisualElement LinhaFluxo(string rotulo, int v, int maior, string dica = null, bool total = false)
        {
            var l = E("fin-fluxo"); if (total) l.AddToClassList("total");
            l.Add(L(rotulo, "fin-rot", total ? "negrito" : "t"));
            var trilho = E("fin-trilho");
            if (!total)
            {
                var b = E("fin-barra", v >= 0 ? "pos" : "neg"); b.style.width = Length.Percent(Mathf.Clamp(Mathf.Abs(v) / (float)Mathf.Max(1, maior) * 100f, 3f, 100f)); trilho.Add(b);
            }
            l.Add(trilho);
            l.Add(L((v >= 0 ? "+" : "") + Fmt.Dinheiro(v), "fin-val", v >= 0 ? "verde" : "vermelho"));
            if (dica != null) ComDica(l, dica);
            return l;
        }

        void TelaFinancas()
        {
            int sal = c.SalariosPorSemana, manut = c.GH.manutencao, parc = c.emprestimos.Sum(e => e.parcela), patro = c.PatrocinioPorSemana;
            int liquido = patro - sal - manut - parc;

            // o essencial numa fileira: onde estamos, por quanto tempo aguenta e para onde vai a semana
            var pecas = E("fin-pecas");
            pecas.Add(PecaFin("SALDO", Fmt.Dinheiro(c.dinheiro), c.dinheiro < 0 ? "vermelho" : "t"));
            pecas.Add(PecaFin("FOLGA DO CAIXA", (int)c.SemanasDeFolego + " semanas", c.SemanasDeFolego < 4 ? "laranja" : "t", "quanto o caixa aguenta sem prêmios"));
            pecas.Add(PecaFin("SALDO DA SEMANA", (liquido >= 0 ? "+" : "") + Fmt.Dinheiro(liquido), liquido >= 0 ? "verde" : "vermelho", liquido >= 0 ? "o caixa cresce sem prêmios" : "o caixa encolhe sem prêmios"));
            pecas.Add(PecaFin("DÍVIDA", Fmt.Dinheiro(c.Devido), c.Devido > 0 ? "laranja" : "t", c.Devido > 0 ? c.emprestimos.Count + " empréstimo(s) em aberto" : "nenhum empréstimo"));
            conteudo.Add(pecas);

            if (c.dinheiro < 0)
                conteudo.Add(Banner0("Saldo NEGATIVO há " + c.semanasNegativo + " semana(s). Na " + Provisorio.SEMANAS_ATE_FALENCIA + "ª semana negativa o clube vai à falência. Pegue um empréstimo, venda jogadores ou ganhe um prêmio.", true));

            var linha1 = E("cols2");

            // ---- por semana
            var cartao = E("cartao", "fin-cartao");
            cartao.Add(L("POR SEMANA", "cartao-tit"));
            int maior = Mathf.Max(patro, sal, manut, parc);
            cartao.Add(LinhaFluxo("Patrocínio", patro, maior, "Cresce com o circuito (" + Mundo.NomeCircuito(c.CircuitoClube) + ") e o nível da Gaming House."));
            cartao.Add(LinhaFluxo("Salários (" + c.elenco.Count + " jogadores)", -sal, maior));
            cartao.Add(LinhaFluxo("Gaming House", -manut, maior, c.GH.nome + ": manutenção semanal."));
            if (parc > 0) cartao.Add(LinhaFluxo("Parcelas do empréstimo", -parc, maior, c.emprestimos.Count + " empréstimo(s) em aberto."));
            cartao.Add(LinhaFluxo("Saldo da semana", liquido, maior, "Os prêmios dos campeonatos entram quando eles terminam.", true));
            linha1.Add(cartao);

            // ---- empréstimo
            var emp = E("cartao", "fin-cartao");
            emp.Add(L("EMPRÉSTIMO", "cartao-tit"));
            var empTit = L("Você pode pegar até " + Fmt.Dinheiro(c.EmprestimoDisponivel), "f18", "negrito");
            empTit.pickingMode = PickingMode.Position;
            ComDica(empTit, "Limite do circuito " + Mundo.NomeCircuito(c.CircuitoClube) + ": " + Fmt.Dinheiro(c.LimiteEmprestimo) + ". O banco cobra " + (int)(Provisorio.JURO_EMPRESTIMO * 100) + "% de juros, pagos em " + Provisorio.PARCELAS_EMPRESTIMO + " parcelas semanais.");
            emp.Add(empTit);
            var devia = L(c.Devido > 0 ? "Você deve " + Fmt.Dinheiro(c.Devido) + " ao banco." : "Você não deve nada ao banco.", "f14", "suave"); devia.style.marginBottom = 8; emp.Add(devia);
            foreach (var e in c.emprestimos.ToList())
            {
                var l = E("fin-emp");
                var info = E(); info.style.flexGrow = 1;
                info.Add(L("Principal " + Fmt.Dinheiro(e.principal), "f15", "negrito"));
                info.Add(L(Fmt.Dinheiro(e.parcela) + "/sem · faltam " + e.restantes + " parcelas", "f13", "apagado"));
                l.Add(info);
                var ee = e;
                l.Add(Botao("Quitar por " + Fmt.Dinheiro(c.CustoQuitar(e)), () => Resposta(c.Quitar(ee), "Empréstimo quitado."), c.dinheiro >= c.CustoQuitar(e)));
                emp.Add(l);
            }
            emp.Add(L("PEGAR MAIS", "dado-rot", "fin-pegar"));
            var valores = E("valores-emp");
            foreach (int v in new[] { 25000, 50000, 100000, 250000 })
            {
                int vv = v;
                valores.Add(BotaoMini(Fmt.Dinheiro(v), () => Resposta(c.PegarEmprestimo(vv), "Empréstimo de " + Fmt.Dinheiro(vv) + " recebido. Parcelas de " + Fmt.Dinheiro((int)Math.Ceiling(vv * (1f + Provisorio.JURO_EMPRESTIMO) / Provisorio.PARCELAS_EMPRESTIMO / 10f) * 10) + " por semana."), false, vv <= c.EmprestimoDisponivel));
            }
            valores.Add(BotaoMini("Tudo", () => { int t = c.EmprestimoDisponivel; Resposta(c.PegarEmprestimo(t), "Empréstimo de " + Fmt.Dinheiro(t) + " recebido."); }, false, c.EmprestimoDisponivel > 0));
            emp.Add(valores);
            linha1.Add(emp);
            conteudo.Add(linha1);

            // ---- histórico: campeonatos e movimentações lado a lado
            var linha2 = E("cols2");
            var camps = E("cartao", "fin-cartao");
            camps.Add(L("CAMPEONATOS DISPUTADOS", "cartao-tit"));
            if (c.titulos.Count == 0) camps.Add(L("Nenhum campeonato terminou ainda. Os prêmios aparecem aqui.", "f15", "suave", "quebra"));
            foreach (var t in c.titulos.Take(6))
            {
                var r = E("fin-lin");
                var rd = L("Sem. " + t.semana, "f14", "apagado"); rd.style.width = 62; r.Add(rd);
                r.Add(L(t.nome, "f15", "grow"));
                r.Add(L(Fmt.Ordinal(t.posicao) + " de " + t.times, "f14", "suave"));
                var pr = L("+" + Fmt.Dinheiro(t.premio), "f15", "negrito", "verde"); pr.style.width = 100; pr.style.marginLeft = 12; pr.style.unityTextAlign = TextAnchor.MiddleRight; r.Add(pr);
                camps.Add(r);
            }
            linha2.Add(camps);
            var movs = E("cartao", "fin-cartao");
            movs.Add(L("ÚLTIMAS MOVIMENTAÇÕES", "cartao-tit"));
            if (c.extrato.Count == 0) movs.Add(L("Sem movimentações ainda.", "f15", "suave"));
            foreach (var l in c.extrato.Take(8))
            {
                var r = E("fin-lin");
                var rd = L("Sem. " + l.semana, "f14", "apagado"); rd.style.width = 62; r.Add(rd);
                r.Add(L(l.descricao, "f15", "grow"));
                var vl = L((l.valor > 0 ? "+" : "") + Fmt.Dinheiro(l.valor), "f15", "negrito", l.valor >= 0 ? "verde" : "vermelho"); vl.style.width = 100; vl.style.unityTextAlign = TextAnchor.MiddleRight; r.Add(vl);
                movs.Add(r);
            }
            linha2.Add(movs);
            conteudo.Add(linha2);
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
            // a trilha dos 5 níveis: onde estamos e o que falta (o que cada nível dá fica na dica do passo)
            var trilha = E("gh-trilha");
            for (int n = 1; n <= GamingHouse.MAX; n++)
            {
                var nv = GamingHouse.De(n);
                bool atual = n == c.nivelGH, feito = n < c.nivelGH;
                var passo = E("gh-passo"); if (n == GamingHouse.MAX) passo.AddToClassList("ultimo");
                if (atual) passo.AddToClassList("atual"); else if (feito) passo.AddToClassList("feito");
                var topo = E("gh-passo-topo");
                var num = E("gh-num"); if (feito) num.Add(new Icone("check", 18)); else num.Add(L(n.ToString())); topo.Add(num);
                if (n < GamingHouse.MAX) topo.Add(E("gh-liga"));
                passo.Add(topo);
                passo.Add(L(nv.nome, "gh-passo-nome"));
                ComDica(passo, "Nível " + n + ": " + nv.nome + (n > 1 ? " · pede " + Fmt.Fas(nv.fas) + " fãs e custa " + Fmt.Dinheiro(nv.custo) : "") + " · manutenção " + Fmt.Dinheiro(nv.manutencao) + "/sem\n" + string.Join("\n", nv.vantagens.Select(v => "• " + v)));
                trilha.Add(passo);
            }
            conteudo.Add(trilha);

            var cols = E("cols2");

            // ---- o que já está valendo (o nível atual em destaque e, mais apagado, o que veio dos anteriores)
            var agora = E("cartao", "fin-cartao");
            agora.Add(L("EM VIGOR · NÍVEL " + c.nivelGH, "cartao-tit"));
            agora.Add(L(c.GH.nome, "f22", "negrito"));
            var man = L("manutenção " + Fmt.Dinheiro(c.GH.manutencao) + " por semana", "f14", "suave"); man.style.marginBottom = 10; agora.Add(man);
            foreach (var vnt in c.GH.vantagens) { var l = E("gh-vant"); l.Add(E("gh-ponto")); l.Add(L(vnt, "f15", "t", "quebra")); agora.Add(l); }
            if (c.nivelGH > 1)
            {
                var antes = Enumerable.Range(1, c.nivelGH - 1).SelectMany(n => GamingHouse.De(n).vantagens).ToList();
                var resto = L("+ " + antes.Count + " vantagens dos níveis anteriores", "f14", "apagado"); resto.style.marginTop = 8; resto.pickingMode = PickingMode.Position;
                ComDica(resto, string.Join("\n", antes.Select(v => "• " + v)));
                agora.Add(resto);
            }
            cols.Add(agora);

            // ---- o próximo nível: só o que se ganha, o preço e o botão
            var prox = E("cartao", "fin-cartao");
            if (c.nivelGH >= GamingHouse.MAX)
            {
                prox.Add(L("NÍVEL MÁXIMO", "cartao-tit"));
                prox.Add(L("Sua Gaming House já é o " + c.GH.nome + ". Não há mais o que melhorar.", "f16", "suave", "quebra"));
            }
            else
            {
                var nv = GamingHouse.De(c.nivelGH + 1);
                prox.Add(L("PRÓXIMO · NÍVEL " + (c.nivelGH + 1), "cartao-tit"));
                prox.Add(L(nv.nome, "f22", "negrito"));
                int dif = nv.manutencao - c.GH.manutencao;
                var custo = L("pede " + Fmt.Fas(nv.fas) + " fãs e custa " + Fmt.Dinheiro(nv.custo) + " · a manutenção sobe " + Fmt.Dinheiro(dif) + " por semana (para " + Fmt.Dinheiro(nv.manutencao) + ")", "f14", "suave", "quebra"); custo.style.marginBottom = 10; prox.Add(custo);
                prox.Add(L("O QUE VOCÊ GANHA", "dado-rot", "fin-pegar"));
                foreach (var vnt in nv.vantagens) { var l = E("gh-vant"); l.Add(E("gh-ponto")); l.Add(L(vnt, "f15", "t", "quebra")); prox.Add(l); }
                var rod = E("gh-rodape");
                var res = E(); res.style.flexGrow = 1; res.style.flexShrink = 1; res.style.marginRight = 14;
                if (!c.FasBastam) res.Add(L("Faltam " + Fmt.Fas(c.FasProximoNivelGH - c.fas) + " fãs (você tem " + Fmt.Fas(c.fas) + ")" + (c.dinheiro < nv.custo ? " e " + Fmt.Dinheiro(nv.custo - c.dinheiro) : ""), "f15", "laranja", "quebra"));
                else if (c.dinheiro < nv.custo) res.Add(L("Faltam " + Fmt.Dinheiro(nv.custo - c.dinheiro), "f15", "laranja"));
                else res.Add(L("Sobram " + Fmt.Dinheiro(c.dinheiro - nv.custo) + " · " + (int)Math.Max(0, (c.dinheiro - nv.custo) / (double)Math.Max(1, c.SalariosPorSemana + nv.manutencao)) + " semanas de despesas", "f14", "suave", "quebra"));
                rod.Add(res);
                rod.Add(Botao("Subir de nível", () => Resposta(c.SubirGamingHouse(), "Gaming House no nível " + c.nivelGH + " (" + c.GH.nome + ")."), c.dinheiro >= nv.custo && c.FasBastam, true));
                prox.Add(rod);
            }
            cols.Add(prox);
            conteudo.Add(cols);
        }
    }
}
