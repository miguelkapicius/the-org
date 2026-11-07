using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Scouts (decisão 0016): cada scout recebe uma tarefa por semana (estudar um adversário ou procurar talentos no mercado) e o
    /// que se descobre aparece aqui. Com informação, o manager escolhe melhor o veto de mapas e a estratégia.</summary>
    public partial class GerenteUI
    {
        int scoutAberto = -1;   // scout com a lista de "outro time" aberta (-1 = nenhum)

        /// <summary>Times que vale a pena estudar: o próximo adversário primeiro e depois os rivais do circuito.</summary>
        List<Equipe> AlvosDeEstudo()
        {
            var lista = new List<Equipe>();
            var adv = c.Adversario; if (adv != null) lista.Add(adv);
            foreach (var e in c.mundo.Ranking().Where(e => !e.clube && e.circuito == c.CircuitoClube)) if (!lista.Contains(e)) lista.Add(e);
            return lista.Take(7).ToList();
        }

        void TelaScouts()
        {
            if (c.scouts.Count == 0)
            {
                var cartao = E("cartao");
                cartao.Add(L("OS SCOUTS CHEGAM COM A SALA DE ANÁLISE (NÍVEL 3)", "cartao-tit"));
                cartao.Add(L("Scouts descobrem como o adversário joga (estilo, mapas fortes e fracos, estratégia) e o potencial dos jogadores do mercado.", "f16", "quebra"));
                foreach (var v in GamingHouse.NIVEIS.Skip(2).Take(3).SelectMany(n => n.vantagens.Where(x => x.Contains("scout") || x.Contains("Scout") || x.Contains("Informação")))) { var l = E("gh-vant"); l.Add(E("gh-ponto")); l.Add(L(v, "f15", "suave")); cartao.Add(l); }
                var b = Botao("Ver a Gaming House", () => Ir(Aba.GamingHouse), true, true); b.style.alignSelf = Align.FlexStart; b.style.marginTop = 14; cartao.Add(b);
                conteudo.Add(cartao);
                conteudo.Add(L("Sem scouts, o adversário é um mistério: você não vê o estilo nem os mapas dele, e o potencial dos jogadores do mercado aparece como \"?\".", "legenda"));
                return;
            }

            var tsc = L("Seus scouts", "h2", "topo-h2"); tsc.pickingMode = PickingMode.Position;
            ComDica(tsc, "A informação sobe com as semanas de estudo (1 semana: estilo; 3: mapas; 6: estratégia e destaque) e é limitada pela Gaming House (nível atual: até " + c.MaxIntel + "). Ela envelhece pela metade a cada temporada.");
            conteudo.Add(tsc);
            var alvos = AlvosDeEstudo();
            for (int i = 0; i < c.scouts.Count; i++)
            {
                int k = i; var s = c.scouts[i];
                var card = E("scout-card");
                var topo = E("scout-topo");
                topo.Add(L("Scout " + (i + 1), "f18", "negrito"));
                string estado = s.tipo == 0 ? "Sem tarefa" : s.tipo == 1 ? "Procurando talentos no mercado" : "Estudando " + c.mundo.equipes[s.alvo].nome + " (" + (int)c.SemanasDeIntel(s.alvo) + " semana(s))";
                topo.Add(L(estado, "f15", s.tipo == 0 ? "laranja" : "verde"));
                card.Add(topo);
                var tarefas = E("tarefas");
                tarefas.Add(BotaoMini("Livre", () => { c.DefinirScout(k, 0); Salvar(); Mostrar(); }, s.tipo == 0));
                tarefas.Add(BotaoMini("Procurar no mercado", () => { c.DefinirScout(k, 1); Salvar(); Mostrar(); }, s.tipo == 1, true, "search"));
                bool abertoAqui = scoutAberto == k;
                foreach (var a in alvos)
                {
                    var aa = a; bool prox = c.Adversario == a, escolhido = s.tipo == 2 && s.alvo == a.id;
                    if (!prox && !escolhido && !abertoAqui) continue;   // só o próximo adversário e o time escolhido à vista
                    string rot = (prox ? "Próximo adversário: " : "") + a.nome;
                    tarefas.Add(BotaoMini(rot, () => { c.DefinirScout(k, 2, aa.id); scoutAberto = -1; Salvar(); Mostrar(); }, escolhido, true, "eye"));
                }
                tarefas.Add(BotaoMini(abertoAqui ? "Menos" : "Outro time…", () => { scoutAberto = abertoAqui ? -1 : k; Mostrar(); }));
                card.Add(tarefas);
                conteudo.Add(card);
            }

            conteudo.Add(L("O que já sabemos", "h2"));
            var conhecidos = c.mundo.equipes.Where(e => !e.clube && (c.SemanasDeIntel(e.id) > 0 || e == c.Adversario)).OrderByDescending(e => e == c.Adversario).ThenByDescending(e => c.SemanasDeIntel(e.id)).Take(6).ToList();
            if (conhecidos.Count == 0) conteudo.Add(L("Nada ainda. Ponha um scout para estudar um adversário.", "f15", "suave"));
            foreach (var e in conhecidos) conteudo.Add(CartaoInformacao(e));
        }

        VisualElement CartaoInformacao(Equipe e)
        {
            int n = c.NivelIntel(e.id);
            var card = E("intel-card");
            var topo = E("intel-topo");
            var nome = E("linha-h"); nome.Add(L(e.nome, "f20", "negrito"));
            if (e == c.Adversario) { var s = Selo("PRÓXIMO ADVERSÁRIO", "laranja"); s.style.marginLeft = 12; nome.Add(s); }
            var circ = L(Mundo.NomeCircuito(e.circuito) + " · ranking #" + c.mundo.PosicaoRanking(e.id), "f14", "suave"); circ.style.marginLeft = 12; nome.Add(circ);
            topo.Add(nome);
            var pips = E("intel-nivel");
            for (int i = 0; i < 3; i++) pips.Add(E("pip", i < n ? "on" : "off"));
            var nv = E("linha-h"); nv.Add(L("Informação " + n + "/3", "f14", "suave")); nv.Add(pips); topo.Add(nv);
            card.Add(topo);

            if (n == 0) { card.Add(L("Sem informação: 1 semana de estudo revela o estilo de jogo.", "oculto-txt")); return card; }

            var est = L("Força aproximada " + e.Forca + "  ·  Estilo " + Estilos.Nome(e.estilo), "f17", "negrito"); est.pickingMode = PickingMode.Position;
            ComDica(est, Estilos.Descricao(e.estilo)); card.Add(est);
            var contra = Estilos.ContraDe(e.estilo);
            if (e.estilo != Estilo.Equilibrada) card.Add(L("Estratégia que joga bem contra esse estilo: " + Estilos.Nome(contra) + ".", "f15", "verde", "quebra"));

            if (n >= 2)
            {
                var t = L("Mapas (afinidade do time deles / a nossa)", "f14", "negrito", "suave"); t.style.marginTop = 12; card.Add(t);
                int melhor = Enumerable.Range(0, Mapas.TOTAL).OrderByDescending(m => e.afinidade[m]).First();
                for (int m = 0; m < Mapas.TOTAL; m++)
                {
                    var l = E("mb-linha");
                    l.Add(L(Mapas.NOMES[m], "mb-nome"));
                    l.Add(Barra(e.afinidade[m], true));
                    l.Add(L(e.afinidade[m].ToString(), "mb-num", "azul"));
                    var nos = L("nós " + c.EquipeClube.afinidade[m], "f13", "laranja"); nos.style.marginLeft = 14; nos.style.width = 60; l.Add(nos);
                    if (m == melhor) l.Add(Selo("MAPA FORTE DELES", "azul"));
                    card.Add(l);
                }
            }
            else card.Add(L("Com 3 semanas de estudo: os mapas fortes e fracos.", "oculto-txt"));

            if (n >= 3)
            {
                var d = e.jogadores[(e.id + c.Temporada) % e.jogadores.Count];
                var t = L("Jogador de destaque: " + d.nome + " (" + Fmt.Funcao(d.funcao) + ")", "f15", "negrito"); t.style.marginTop = 10; card.Add(t);
            }
            else if (n >= 2) { var t = L("Com 6 semanas: a estratégia detalhada e o jogador de destaque.", "oculto-txt"); t.style.marginTop = 6; card.Add(t); }
            return card;
        }
    }
}
