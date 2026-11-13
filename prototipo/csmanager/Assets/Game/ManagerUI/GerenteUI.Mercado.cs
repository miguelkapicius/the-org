using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Mercado como vitrine (referência: marketplace de cartões): os melhores da semana em destaque, filtros e ordem em chips,
    /// e uma grade de cartões com foto, atributos e o botão de contratar.</summary>
    public partial class GerenteUI
    {
        int ordemMercado;   // 0 geral, 1 mira, 2 reflexo, 3 menor preço, 4 mais jovem
        static readonly string[] ORDENS_MERCADO = { "Geral", "Mira", "Reflexo", "Menor preço", "Mais jovem" };

        void TelaMercado()
        {
            conteudo.Add(SegmentoMercado());
            if (mercadoChat) { TelaNegociacao(); return; }
            // prévia: ao passar o mouse num jogador, mostra o que muda no time se ele for contratado
            previaCaixa = E("previa"); previaTxt = L("", "quebra"); previaCaixa.Add(previaTxt);
            conteudo.Add(previaCaixa);
            AtualizarPrevia(null);

            var cmp = Comparacao(); if (cmp != null) conteudo.Add(cmp);

            // os melhores da semana, como a fileira de "top sellers" da referência
            if (c.mercado.Count > 0)
            {
                conteudo.Add(L("Melhores da semana", "h2", "topo-h2"));
                var tops = E("vit-tops");
                foreach (var j in c.mercado.OrderByDescending(x => x.Overall).Take(8))
                {
                    var jj = j;
                    var it = E("vit-top");
                    it.Add(Retrato(j, "vit-top-foto"));
                    it.Add(L(j.nome, "f15", "negrito", "centro"));
                    it.Add(L(j.Overall.ToString(), "f14", ClasseNota(j.Overall), "centro"));
                    it.RegisterCallback<PointerEnterEvent>(_ => AtualizarPrevia(jj));
                    it.RegisterCallback<PointerLeaveEvent>(_ => AtualizarPrevia(null));
                    tops.Add(it);
                }
                conteudo.Add(tops);
            }

            // filtros (função) à esquerda e a ordem à direita
            var fil = E("vit-filtros");
            var esq = E("tarefas");
            for (int i = 0; i < FUNCOES_NOMES.Length; i++) { int ii = i; esq.Add(BotaoMini(FUNCOES_NOMES[i], () => { filtroFuncao = ii; Mostrar(); }, filtroFuncao == i)); }
            fil.Add(esq);
            var dir = E("tarefas"); dir.AddToClassList("vit-ordem");
            var rot = L("Ordenar por", "f14", "apagado"); rot.style.marginRight = 10; rot.style.marginBottom = 8; dir.Add(rot);
            for (int k = 0; k < ORDENS_MERCADO.Length; k++) { int kk = k; dir.Add(BotaoMini(ORDENS_MERCADO[k], () => { ordemMercado = kk; Mostrar(); }, ordemMercado == k)); }
            fil.Add(dir);
            conteudo.Add(fil);

            bool cheio = c.elenco.Count >= c.ElencoMax;
            var lista = c.mercado.Where(j => filtroFuncao == 0 || j.funcao == FUNCOES_ROLE[filtroFuncao]);
            switch (ordemMercado)
            {
                case 1: lista = lista.OrderByDescending(j => j.Mira); break;
                case 2: lista = lista.OrderByDescending(j => j.Reflexo); break;
                case 3: lista = lista.OrderBy(j => j.Preco); break;
                case 4: lista = lista.OrderBy(j => j.idade); break;
                default: lista = lista.OrderByDescending(j => j.Overall); break;
            }
            var grade = E("vit-grade");
            int n = 0;
            foreach (var j in lista) { grade.Add(CartaoVitrine(j, cheio)); n++; }
            conteudo.Add(grade);
            if (n == 0) conteudo.Add(L("Nenhum jogador nesse filtro.", "f15", "suave"));
            if (cheio) conteudo.Add(L("Elenco cheio (" + c.ElencoMax + "): venda alguém ou suba a Gaming House para ter mais vagas.", "f15", "laranja", "legenda"));
        }

        /// <summary>Cartão de um jogador do mercado: foto, nome, atributos, preço e as duas ações.</summary>
        VisualElement CartaoVitrine(Jogador j, bool cheio)
        {
            var card = E("vit-card"); if (comparar.Contains(j)) card.AddToClassList("comp");
            card.Add(MidiaJogador(j));

            var corpo = E("vit-corpo");
            corpo.Add(NomeComTraco(j, "info-nome", j.potencialRevelado));
            var meta = L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave"); meta.pickingMode = PickingMode.Position;
            ComDica(meta, "Potencial " + (j.potencialRevelado ? j.potencial.ToString() : "desconhecido (um scout revela)") + ". Salário " + Fmt.Dinheiro(j.Salario) + " por semana.");
            corpo.Add(meta);

            // os 5 atributos lado a lado: rótulo pequeno em cima, número colorido embaixo
            var ats = E("vit-attrs");
            for (int k = 1; k <= 5; k++)
            {
                var col = COLS[k]; int v = col.chave(j);
                var cel = E("vit-at"); cel.pickingMode = PickingMode.Position; ComDica(cel, col.dica);
                cel.Add(L(col.rot, "vit-at-rot")); cel.Add(L(v.ToString(), "vit-at-num", ClasseNota(v)));
                ats.Add(cel);
            }
            corpo.Add(ats);

            // ninguém tem preço fixo: o que ele pede hoje (luvas e salário) depende do seu clube (decisão 0026)
            var (pl, ps) = Negociador.Pedido(c, j);
            var preco = E("vit-preco");
            preco.Add(L("PEDE", "dado-rot"));
            var pv = E(); pv.style.alignItems = Align.FlexEnd;
            pv.Add(L(Fmt.Dinheiro(pl), "vit-preco-v", c.dinheiro >= pl ? "t" : "vermelho"));
            pv.Add(L("+ " + Fmt.Dinheiro(ps) + "/sem", "f13", "suave"));
            preco.Add(pv);
            ComDica(preco, "Luvas (pagamento único) e salário por semana que ele pede agora. Mudam com o prestígio do clube, a vaga, a estrutura, as finanças e o clima. Negocie para baixar.");
            corpo.Add(preco);

            var ac = E("vit-acoes");
            bool conversando = c.negociacoes.ContainsKey(j);
            var contratar = BotaoMini(conversando ? "Continuar conversa" : "Negociar", () => IniciarNegociacao(j), false, !cheio); contratar.AddToClassList("grow"); if (!cheio) contratar.AddToClassList("destaque");
            ac.Add(contratar);
            var cmpBtn = BotaoMini("", () => AlternarComparar(j), comparar.Contains(j), true, "swap"); cmpBtn.AddToClassList("so-ic");
            ComDica(cmpBtn, comparar.Contains(j) ? "Tirar da comparação" : "Comparar com outro jogador (do mercado ou do seu elenco)");
            ac.Add(cmpBtn);
            corpo.Add(ac);
            card.Add(corpo);

            card.RegisterCallback<PointerEnterEvent>(_ => AtualizarPrevia(j));
            card.RegisterCallback<PointerLeaveEvent>(_ => AtualizarPrevia(null));
            return card;
        }

        /// <summary>A foto do jogador com a função, o geral e o lado em que ele rende mais (decisão 0024): a mesma mídia do cartão do
        /// elenco e da vitrine do mercado.</summary>
        VisualElement MidiaJogador(Jogador j)
        {
            var midia = E("midia");
            midia.Add(Retrato(j, "retrato-fill"));
            var fn = Selo(Fmt.FuncaoCurta(j.funcao), null); fn.AddToClassList("midia-selo"); ComDica(fn, Fmt.Funcao(j.funcao)); midia.Add(fn);
            var ovr = E("ovr"); ovr.Add(L(j.Overall.ToString(), ClasseNota(j.Overall))); ComDica(ovr, COLS[0].dica); midia.Add(ovr);
            var forte = j.LadoForte;   // a foto mostra o lado em que ele rende mais
            var sl = Selo(forte == null ? "TR · CT" : forte.Value ? "MELHOR NO TR" : "MELHOR NO CT", forte == null ? null : forte.Value ? "laranja" : "azul"); sl.AddToClassList("midia-lado");
            ComDica(sl, "Domínio de lado: TR " + (int)j.ladoTR + " · CT " + (int)j.ladoCT + ". " + (forte == null ? "Rende igual nos dois lados." : "Rende mais " + (forte.Value ? "no ataque (TR)." : "na defesa (CT).")) + "\n\nTreine um lado na tela de Treino.");
            midia.Add(sl);
            return midia;
        }
    }
}
