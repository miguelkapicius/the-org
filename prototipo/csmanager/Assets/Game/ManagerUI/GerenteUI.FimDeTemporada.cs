using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Fim de temporada (decisão 0040): uma tela cheia que conta o ano em etapas animadas (manchete e circuito, campanha,
    /// campeonatos, destaques e dinheiro, o elenco um ano mais velho) e depois o "reset": a lista do que muda para a temporada nova,
    /// item a item. "Pular animação" mostra tudo de uma vez; "Começar a temporada" fecha.</summary>
    public partial class GerenteUI
    {
        VisualElement ftEl;
        bool ftPulou;
        readonly List<Action> ftRevelar = new List<Action>();

        // tempos das etapas, em milissegundos
        const int FT_CIRCUITO = 900, FT_CAMPANHA = 2100, FT_DESTAQUES = 3300, FT_CAMPEONATOS = 4600, FT_ELENCO = 6100, FT_RESET = 8000, FT_PASSO_RESET = 380;

        /// <summary>Abre a sequência do fim de temporada por cima de tudo.</summary>
        void AbrirFimDeTemporada(ResumoTemporada t)
        {
            if (t == null) return;
            ftEl?.RemoveFromHierarchy();
            ftPulou = false; ftRevelar.Clear();
            SomManager.Tocar(Som.Principal);

            var cv = E("ft"); cv.pickingMode = PickingMode.Position; ftEl = cv;
            tela.Add(cv);   // preso na tela antes de agendar as animações: o agendamento de um elemento só corre com ele no painel
            var rol = new ScrollView(ScrollViewMode.Vertical); rol.AddToClassList("grow"); rol.mouseWheelScrollSize = 60; rol.horizontalScrollerVisibility = ScrollerVisibility.Hidden; cv.Add(rol);
            var caixa = E("ft-caixa"); rol.Add(caixa);

            // ---------- topo: o ano e a manchete
            var topo = E("ft-topo");
            var esq = E("ft-topo-esq");
            esq.Add(L("FIM DA TEMPORADA " + t.temporada + "  ·  " + t.ano, "car-peq"));
            esq.Add(L(t.Manchete, "ft-manchete", "quebra"));
            topo.Add(esq);
            var pular = Botao("Pular animação", () => Pular(), true, false, "skip"); pular.style.alignSelf = Align.FlexStart; topo.Add(pular);
            caixa.Add(topo);
            Aparecer(esq, 60);

            // ---------- o circuito e o ranking
            var circ = E("ft-bloco", "ft-circuito");
            var linha = E("linha-h");
            linha.Add(L(Mundo.NomeCircuito(t.Antes), "ft-circ", "suave"));
            var seta = L("→", "ft-seta"); linha.Add(seta);
            linha.Add(L(Mundo.NomeCircuito(t.Depois), "ft-circ"));
            string sel = t.Movimento > 0 ? "PROMOVIDO" : t.Movimento < 0 ? "REBAIXADO" : "MANTIDO";
            var selo = Selo(sel, t.Movimento > 0 ? "verde" : t.Movimento < 0 ? "vermelho" : "azul"); selo.style.marginLeft = 18; linha.Add(selo);
            circ.Add(linha);
            var rk = L("", "ft-num"); circ.Add(Rotulado("Ranking final", rk, "de " + t.times + " times"));
            caixa.Add(circ);
            Aparecer(circ, FT_CIRCUITO, () => SomManager.Tocar(t.Movimento < 0 ? Som.Erro : Som.Sucesso));
            Contar(rk, t.times, t.ranking, FT_CIRCUITO + 150, 1.1f, v => "#" + v);

            var cols = E("ft-cols"); caixa.Add(cols);
            var colA = E("ft-col"); var colB = E("ft-col"); colB.style.marginRight = 0; cols.Add(colA); cols.Add(colB);

            // ---------- a campanha
            var camp = E("ft-bloco");
            camp.Add(L("A campanha", "ft-tit"));
            // duas fileiras de dois (quatro lado a lado não cabem na coluna e a fileira quebrada ficava cortada)
            var tilesA = E("ft-tiles"); var tilesB = E("ft-tiles");
            var vd = L("", "ft-num"); tilesA.Add(Rotulado("Mapas", vd, t.vitorias + " vitórias · " + t.derrotas + " derrotas"));
            var pct = L("", "ft-num"); tilesA.Add(Rotulado("Vitórias", pct, "dos mapas"));
            var ser = L("", "ft-num"); tilesB.Add(Rotulado("Séries vencidas", ser, "de " + t.series));
            var sq = L("", "ft-num"); tilesB.Add(Rotulado("Melhor sequência", sq, "vitórias seguidas"));
            camp.Add(tilesA); camp.Add(tilesB);
            colA.Add(camp);
            Aparecer(camp, FT_CAMPANHA);
            Contar(vd, 0, t.mapas, FT_CAMPANHA + 100, 0.9f, v => v.ToString());
            Contar(pct, 0, t.PctVitorias, FT_CAMPANHA + 100, 0.9f, v => v + " %");
            Contar(ser, 0, t.seriesVencidas, FT_CAMPANHA + 100, 0.9f, v => v.ToString());
            Contar(sq, 0, t.melhorSequencia, FT_CAMPANHA + 100, 0.9f, v => v.ToString());

            // ---------- destaques, dinheiro e fãs
            var dst = E("ft-bloco");
            dst.Add(L("Destaques", "ft-tit"));
            var dts = E("ft-tiles");
            dts.Add(Destaque("MVP da temporada", t.mvp ?? "—", t.mvp != null ? t.mvpVezes + (t.mvpVezes == 1 ? " vez MVP de mapa" : " vezes MVP de mapa") : "nenhum mapa assistido"));
            dts.Add(Destaque("Maior evolução", t.evolucao ?? "—", t.evolucao != null ? "+" + t.evolucaoGanho + " de geral" : "ninguém subiu de geral"));
            dst.Add(dts);
            var din = E("ft-tiles");
            var sd = L("", "ft-num"); din.Add(Rotulado("Caixa", sd, "começou com " + Fmt.Dinheiro(t.dinheiroInicio)));
            var pr = L("", "ft-num"); din.Add(Rotulado("Prêmios", pr, t.campeonatos.Count + " campeonatos"));
            var fs = L("", "ft-num"); din.Add(Rotulado("Fãs", fs, (t.FasGanhos >= 0 ? "+" : "") + Fmt.Fas(t.FasGanhos) + " na temporada"));
            dst.Add(din);
            colA.Add(dst);
            Aparecer(dst, FT_DESTAQUES, () => SomManager.Tocar(Som.Dinheiro));
            Contar(sd, t.dinheiroInicio, t.dinheiroFim, FT_DESTAQUES + 100, 1.2f, Fmt.Dinheiro);
            Contar(pr, 0, t.premios, FT_DESTAQUES + 100, 1.2f, Fmt.Dinheiro);
            Contar(fs, t.fasInicio, t.fasFim, FT_DESTAQUES + 100, 1.2f, Fmt.Fas);

            // ---------- os campeonatos
            var cps = E("ft-bloco");
            cps.Add(L("Campeonatos", "ft-tit"));
            if (t.campeonatos.Count == 0) cps.Add(L("O clube não terminou nenhum campeonato nesta temporada.", "f15", "suave"));
            var grCamp = E("ft-camps"); cps.Add(grCamp);   // faixa de largura inteira, em duas colunas: a lista de 13 campeonatos não estica só a coluna da esquerda
            for (int i = 0; i < t.campeonatos.Count; i++)
            {
                var k = t.campeonatos[i];
                var l = E("ft-linha");
                var pos = E("ft-pos", k.posicao == 1 ? "ouro" : k.posicao <= 3 ? "podio" : "comum");
                if (k.posicao == 1) pos.Add(new Icone("trophy", 18)); else pos.Add(L(Fmt.Ordinal(k.posicao), "f15", "negrito"));
                l.Add(pos);
                var nm = L(k.nome, "f16", "grow"); nm.style.marginLeft = 12; l.Add(nm);
                l.Add(L(k.premio > 0 ? Fmt.Dinheiro(k.premio) : "sem prêmio", "f15", k.premio > 0 ? "verde" : "suave"));
                grCamp.Add(l);
                Aparecer(l, FT_CAMPEONATOS + 200 + i * 110, k.posicao == 1 ? () => SomManager.Tocar(Som.Sucesso) : (Action)null);
            }
            caixa.Add(cps);
            Aparecer(cps, FT_CAMPEONATOS);

            // ---------- o elenco, um ano mais velho
            var el = E("ft-bloco");
            el.Add(L("O elenco", "ft-tit"));
            for (int i = 0; i < t.elenco.Count; i++)
            {
                var e = t.elenco[i];
                var l = E("ft-jog");
                var nome = E("ft-jog-nome");
                nome.Add(L(e.nome, "f16", "negrito"));
                var idade = L((e.idade - 1) + " anos", "f14", "suave"); idade.style.marginLeft = 10; nome.Add(idade);
                if (e.aposentou) { var ap = Selo("APOSENTA", "laranja"); ap.style.marginLeft = 10; nome.Add(ap); }
                else if (!e.titular) { var tt = Selo("RESERVA"); tt.style.marginLeft = 10; nome.Add(tt); }   // titular é o normal: só o reserva ganha selo
                l.Add(nome);
                var barra = E("ft-barra"); var fill = E("ft-fill"); fill.style.width = Length.Percent(Mathf.Clamp(e.overallAntes, 0, 100)); barra.Add(fill); l.Add(barra);
                int dif = e.overallDepois - e.overallAntes;
                var num = L(e.overallAntes.ToString(), "f15", "negrito"); num.style.width = 74; num.style.unityTextAlign = TextAnchor.MiddleRight; l.Add(num);
                el.Add(l);
                int quando = FT_ELENCO + 200 + i * 140;
                var ee = e; var nn = num; var ff = fill; var id = idade;
                Aparecer(l, quando, () =>
                {
                    ff.style.width = Length.Percent(Mathf.Clamp(ee.overallDepois, 0, 100));
                    nn.text = ee.overallDepois + (dif != 0 ? (dif > 0 ? "  +" : "  ") + dif : "");
                    nn.EnableInClassList("verde", dif > 0); nn.EnableInClassList("vermelho", dif < 0);
                    id.text = ee.idade + " anos"; id.AddToClassList("laranja");
                });
            }
            colB.Add(el);
            Aparecer(el, FT_ELENCO);

            // ---------- o reset: o que muda para a temporada nova
            var rs = E("ft-bloco", "ft-reset");
            rs.Add(L("A temporada " + (t.temporada + 1) + " começa", "ft-tit"));
            for (int i = 0; i < t.mudancas.Count; i++)
            {
                var l = E("ft-mud");
                var ck = E("ft-check"); ck.Add(new Icone("check", 16)); l.Add(ck);
                var tx = L(t.mudancas[i], "f15", "quebra", "grow"); tx.style.marginLeft = 10; l.Add(tx);
                rs.Add(l);
                var cc = ck;
                Aparecer(l, FT_RESET + 250 + i * FT_PASSO_RESET, () => { cc.AddToClassList("feito"); SomManager.Tocar(Som.Alternar, 0.6f); });
            }
            colB.Add(rs);
            Aparecer(rs, FT_RESET, () => SomManager.Tocar(Som.Semana));

            // ---------- começar
            // barra fixa embaixo (fora da rolagem): a ação principal fica sempre à vista e dá para começar a qualquer momento
            var rod = E("ft-rodape");
            var rodIn = E("ft-rodape-in"); rod.Add(rodIn);
            rodIn.Add(L("Espaço pula a animação  ·  Enter começa a temporada " + (t.temporada + 1), "f13", "apagado"));
            var comecar = Botao("Começar a temporada " + (t.temporada + 1), () => FecharFimDeTemporada(), true, true, "play", "cheio");
            rodIn.Add(comecar);
            cv.Add(rod);

            cv.schedule.Execute(() => cv.AddToClassList("visivel")).StartingIn(1);
        }

        /// <summary>Faz um bloco aparecer (sobe e acende) depois de <paramref name="ms"/>; <paramref name="depois"/> roda na hora em que ele aparece.
        /// "Pular animação" chama tudo de uma vez.</summary>
        void Aparecer(VisualElement e, int ms, Action depois = null)
        {
            e.AddToClassList("ft-entra");
            bool feito = false;
            Action revela = () => { if (feito) return; feito = true; e.AddToClassList("visivel"); depois?.Invoke(); };
            ftRevelar.Add(revela);
            e.schedule.Execute(() => { if (!ftPulou) revela(); }).StartingIn(Math.Max(1, ms));
        }

        /// <summary>Conta um número de <paramref name="de"/> até <paramref name="ate"/> (desacelerando no fim). <paramref name="final"/> é o texto
        /// definitivo, quando ele não sai do formato (ex.: "12V 9D").</summary>
        void Contar(Label l, int de, int ate, int ms, float seg, Func<int, string> fmt, string final = null)
        {
            string fim = final ?? fmt(ate);
            l.text = fmt(de);
            float t0 = -1f; bool acabou = false;
            Action fixa = () => { acabou = true; l.text = fim; };
            ftRevelar.Add(fixa);
            l.schedule.Execute(() =>
            {
                if (acabou) return;
                if (ftPulou) { fixa(); return; }
                if (t0 < 0f) t0 = Time.realtimeSinceStartup;
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / Mathf.Max(0.05f, seg));
                k = 1f - (1f - k) * (1f - k) * (1f - k);
                if (k >= 1f) fixa(); else l.text = fmt(Mathf.RoundToInt(Mathf.Lerp(de, ate, k)));
            }).StartingIn(Math.Max(1, ms)).Every(16).Until(() => acabou);
        }

        void Pular()
        {
            if (ftPulou) return;
            ftPulou = true;
            foreach (var a in ftRevelar.ToList()) a();
        }

        void FecharFimDeTemporada()
        {
            c.temporadaNaoVista = false;
            Salvar();
            var cv = ftEl; ftEl = null;
            if (cv != null) { cv.RemoveFromClassList("visivel"); cv.schedule.Execute(() => cv.RemoveFromHierarchy()).StartingIn(260); }
            aba = Aba.Painel; Mostrar();
            Toast("Começa a temporada " + c.Temporada + ". O clube joga o circuito " + Mundo.NomeCircuito(c.CircuitoClube) + ".");
        }

        static VisualElement Rotulado(string rotulo, Label numero, string legenda)
        {
            var b = E("ft-tile");
            b.Add(L(rotulo.ToUpperInvariant(), "ft-rot"));
            b.Add(numero);
            b.Add(L(legenda, "f13", "suave", "quebra"));
            return b;
        }

        static VisualElement Destaque(string rotulo, string nome, string legenda)
        {
            var b = E("ft-tile");
            b.Add(L(rotulo.ToUpperInvariant(), "ft-rot"));
            b.Add(L(nome, "ft-nome", "quebra"));
            b.Add(L(legenda, "f13", "suave", "quebra"));
            return b;
        }
    }
}
