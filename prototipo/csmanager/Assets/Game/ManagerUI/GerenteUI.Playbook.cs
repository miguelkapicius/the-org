using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using CSM.Sim;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Playbook (decisão 0022) como um quadro tático: o alternador Ataque/Defesa, a lista de jogadas à esquerda e, à direita, o
    /// mapa com as rotas dos 5 jogadores ANIMADAS, a linha do tempo, os indicadores em anel e os pontos fortes e fracos. Favoritas (o IGL
    /// prefere) e proibidas (o IGL não escolhe) crescem com o nível da Gaming House.</summary>
    public partial class GerenteUI
    {
        string jogadaAberta; int exemploAberto;   // a jogada em foco e qual dos planos dela está no quadro
        bool ladoPlaybookTR = true;
        float rolagemPlaybook;                    // posição da lista de jogadas (não volta ao topo a cada clique)

        static string Mmss(float s) => (int)s / 60 + ":" + ((int)s % 60).ToString("00");

        void TelaPlaybook()
        {
            var intro = L("Marque as jogadas que o IGL deve preferir (favoritas) e as que ele nunca faz (proibidas). Clique numa jogada para ver o desenho no mapa.", "f15", "suave", "quebra");
            ComDica(intro, "O IGL escolhe a cada round uma jogada de um banco de rounds de times profissionais, preferindo o estilo da estratégia. Favorita pesa " +
                           Estrategias.PREFERENCIA_PLAYBOOK.ToString("0") + "x na escolha; proibida nunca é escolhida. Vale para o mapa jogável de hoje; cada mapa novo terá o seu playbook.");
            conteudo.Add(intro);

            bool tr = ladoPlaybookTR;
            var fav = tr ? c.favoritasTR : c.favoritasCT; var proi = tr ? c.proibidasTR : c.proibidasCT;

            // alternador de lado e vagas
            var barra = E("pb-barra");
            var seg = E("pb-seg");
            seg.Add(SegLado("ATAQUE · TR", true)); seg.Add(SegLado("DEFESA · CT", false));
            barra.Add(seg);
            var vg = E("pb-vagas");
            vg.Add(Vagas("FAVORITAS", fav.Count, c.VagasPlaybook, "fav"));
            vg.Add(Vagas("PROIBIDAS", proi.Count, c.VagasPlaybook, "proib"));
            ComDica(vg, "Vagas por lado no nível " + c.nivelGH + " da Gaming House (+1 por nível, até " + Carreira.VAGAS_PLAYBOOK_MAX + ").");
            barra.Add(vg);
            conteudo.Add(barra);

            var jogadas = Estrategias.Jogadas(tr);
            if (jogadas.Count == 0) { conteudo.Add(L("Não há jogadas com dados suficientes nesse lado.", "f15", "suave")); return; }
            if (jogadaAberta == null || !jogadas.Any(x => x.nome == jogadaAberta))
            {
                jogadaAberta = (jogadas.FirstOrDefault(x => c.Marca(x.nome, tr) == Carreira.MarcaJogada.Favorita) ?? jogadas[0]).nome; exemploAberto = 0;
            }
            var atual = jogadas.First(x => x.nome == jogadaAberta);

            var corpo = E("pb-corpo");
            var lista = new ScrollView(ScrollViewMode.Vertical); lista.AddToClassList("pb-lista"); lista.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            foreach (var j in jogadas) lista.Add(LinhaJogada(j, tr, j.nome == jogadaAberta, () => rolagemPlaybook = lista.scrollOffset.y));
            var yr = rolagemPlaybook; lista.schedule.Execute(() => lista.scrollOffset = new Vector2(0, yr)).StartingIn(1);
            corpo.Add(lista);
            corpo.Add(PainelJogada(atual, tr));
            conteudo.Add(corpo);
        }

        VisualElement SegLado(string texto, bool tr)
        {
            var b = E("pb-seg-b"); if (ladoPlaybookTR == tr) b.AddToClassList("ativo");
            var q = E("pb-seg-q", tr ? "lado-tr" : "lado-ct"); b.Add(q);
            b.Add(L(texto));
            Clicavel(b, () => { ladoPlaybookTR = tr; jogadaAberta = null; exemploAberto = 0; rolagemPlaybook = 0; Mostrar(); });
            return b;
        }

        /// <summary>Vagas como pips: um cheio para cada jogada marcada.</summary>
        static VisualElement Vagas(string rotulo, int usadas, int max, string tipo)
        {
            var v = E("pb-vaga");
            v.Add(L(rotulo, "dado-rot"));
            var pips = E("pb-pips");
            for (int i = 0; i < max; i++) { var p = E("pb-pip"); if (i < usadas) p.AddToClassList("on"); p.AddToClassList(tipo); pips.Add(p); }
            v.Add(pips);
            return v;
        }

        VisualElement LinhaJogada(Estrategias.InfoJogada j, bool tr, bool sel, Action antes)
        {
            var jj = j; var marca = c.Marca(j.nome, tr);
            var r = E("pb-linha"); if (sel) r.AddToClassList("sel");
            var esq = E("pb-linha-esq");
            var topo = E("linha-h");
            topo.Add(L(j.nome, "pb-linha-nome"));
            if (marca == Carreira.MarcaJogada.Favorita) { var s = Selo("FAVORITA", "verde"); s.style.marginLeft = 8; topo.Add(s); }
            else if (marca == Carreira.MarcaJogada.Proibida) { var s = Selo("PROIBIDA", "vermelho"); s.style.marginLeft = 8; topo.Add(s); }
            esq.Add(topo);
            esq.Add(L(NomeCategoria(j.estilo) + " · " + j.planos + " planos", "f13", "apagado"));
            r.Add(esq);
            var nums = E("pb-linha-nums");
            nums.Add(L(Mathf.RoundToInt(j.vitoriaPro * 100) + "%", "pb-linha-num", j.vitoriaPro >= 0.55f ? "verde" : j.vitoriaPro < 0.45f ? "vermelho" : "t"));
            nums.Add(L("vitória pro", "pb-linha-rot"));
            r.Add(nums);
            Clicavel(r, () => { antes?.Invoke(); jogadaAberta = jj.nome; exemploAberto = 0; Mostrar(); });
            return r;
        }

        /// <summary>O painel da jogada em foco: título, ações, indicadores, o quadro do mapa animado e o resto.</summary>
        VisualElement PainelJogada(Estrategias.InfoJogada j, bool tr)
        {
            var jj = j; var marca = c.Marca(j.nome, tr);
            var plano = j.exemplos[exemploAberto % j.exemplos.Count];
            var painel = E("pb-painel");

            // título e ações
            var topo = E("pb-topo");
            var tit = E(); tit.style.flexShrink = 1; tit.style.minWidth = 0;
            tit.Add(L(j.nome, "pb-nome"));
            var sel = E("linha-h"); sel.style.marginTop = 6;
            sel.Add(Selo(tr ? "ATAQUE" : "DEFESA", tr ? "laranja" : "azul"));
            var es = Selo(NomeCategoria(j.estilo).ToUpperInvariant(), null); es.style.marginLeft = 6; sel.Add(es);
            if (marca == Carreira.MarcaJogada.Favorita) { var s = Selo("FAVORITA", "verde"); s.style.marginLeft = 6; sel.Add(s); }
            else if (marca == Carreira.MarcaJogada.Proibida) { var s = Selo("PROIBIDA", "vermelho"); s.style.marginLeft = 6; sel.Add(s); }
            tit.Add(sel);
            topo.Add(tit);
            var acoes = E("linha-h");
            var bf = BotaoMini("Favorita", () => Marcar(jj.nome, tr, marca == Carreira.MarcaJogada.Favorita ? Carreira.MarcaJogada.Nenhuma : Carreira.MarcaJogada.Favorita), marca == Carreira.MarcaJogada.Favorita);
            var bp = BotaoMini("Proibir", () => Marcar(jj.nome, tr, marca == Carreira.MarcaJogada.Proibida ? Carreira.MarcaJogada.Nenhuma : Carreira.MarcaJogada.Proibida), marca == Carreira.MarcaJogada.Proibida);
            bf.style.marginRight = 8; ComDica(bf, "O IGL passa a preferir esta jogada (peso " + Estrategias.PREFERENCIA_PLAYBOOK.ToString("0") + "x)."); ComDica(bp, "O IGL nunca escolhe esta jogada.");
            acoes.Add(bf); acoes.Add(bp);
            topo.Add(acoes);
            painel.Add(topo);

            // indicadores
            var st = E("pb-stats");
            st.Add(BlocoAnel(j.vitoriaPro, "Vitória do time pro", Mathf.RoundToInt(j.vitoriaPro * 100) + "%", CorNota(j.vitoriaPro, 0.55f, 0.45f),
                "Quanto o time profissional venceu os rounds com essa jogada."));
            st.Add(BlocoAnel(Mathf.Clamp01(j.forca / 1.6f), "Força na simulação", j.forca.ToString("0.00"), CorNota(j.forca, 1.05f, 0.95f),
                "O quanto a jogada rende na nossa simulação (1,00 é a média)."));
            st.Add(BlocoNumero("Planos no banco", j.planos.ToString(), "Rounds de times profissionais que formam essa jogada."));
            st.Add(tr ? BlocoNumero("Entra no bomb", Mmss(j.horaEntrada), "Hora média em que o time entra no bomb.")
                      : BlocoNumero("Mortes até 25 s", j.mortesCedo.ToString("0.0"), "Mortes médias do time nos primeiros 25 s."));
            // (os indicadores entram na coluna lateral, ao lado do mapa)

            // quadro do mapa e a parte lateral
            var miolo = E("pb-miolo");
            var quadro = E("pb-quadro");
            var mapa = new PlantaJogada(plano, 1.3f);
            quadro.Add(mapa);
            var ctrl = E("pb-ctrl");
            var rep = E("btn-mini", "so-ic"); rep.Add(new Icone("playf", 14)); Clicavel(rep, mapa.Reiniciar); ComDica(rep, "Ver de novo desde o começo.");
            ctrl.Add(rep);
            var tempo = E("pb-tempo"); var fill = E("pb-tempo-fill"); tempo.Add(fill);
            tempo.RegisterCallback<PointerDownEvent>(ev => { var w = tempo.layout.width; if (w > 1f) mapa.Ir(ev.localPosition.x / w); });
            ComDica(tempo, "Clique para ir a um ponto da jogada.");
            ctrl.Add(tempo);
            var rel = L("0:00 / " + Mmss(mapa.Duracao), "pb-rel"); ctrl.Add(rel);
            mapa.AoTempo = t => { fill.style.width = Length.Percent(mapa.Duracao > 0 ? t / mapa.Duracao * 100f : 0f); rel.text = Mmss(t) + " / " + Mmss(mapa.Duracao); };
            mapa.AoTempo(mapa.Tempo);
            quadro.Add(ctrl);
            var rod = E("pb-rod");
            rod.Add(L("Exemplo " + (exemploAberto % j.exemplos.Count + 1) + " de " + j.exemplos.Count, "f14", "suave"));
            var res = Selo(plano.ganhou ? "O TIME PRO VENCEU" : "O TIME PRO PERDEU", plano.ganhou ? "verde" : "vermelho"); res.style.marginLeft = 10; rod.Add(res);
            var fl = E(); fl.style.flexGrow = 1; rod.Add(fl);
            if (j.exemplos.Count > 1) { var b1 = BotaoMini("Outro exemplo", () => { exemploAberto++; Mostrar(); }); rod.Add(b1); }
            quadro.Add(rod);
            quadro.Add(L("Animação ilustrativa: a velocidade é constante; o trajeto e onde cada um para vêm de um round real.", "f13", "apagado", "quebra"));
            miolo.Add(quadro);

            var lateral = E("pb-lateral");
            lateral.Add(st);
            var rt = L("ROTAS", "dado-rot"); rt.style.marginTop = 16; lateral.Add(rt);
            for (int k = 0; k < 5; k++)
            {
                var lin = E("pb-rota");
                var ponto = E("pb-rota-ponto"); ponto.style.backgroundColor = PlantaJogada.CORES[k]; lin.Add(ponto);
                var t = L(Estrategias.ResumoRota(plano, k), "f14", "quebra"); t.style.flexShrink = 1; lin.Add(t);
                lateral.Add(lin);
            }
            Lista(lateral, "PONTOS FORTES", j.fortes, "verde");
            Lista(lateral, "PONTOS FRACOS", j.fracos, "vermelho");
            miolo.Add(lateral);
            painel.Add(miolo);
            return painel;
        }

        static void Lista(VisualElement pai, string titulo, List<string> itens, string cor)
        {
            var h = L(titulo, "dado-rot"); h.style.marginTop = 18; pai.Add(h);
            if (itens.Count == 0) { pai.Add(L("Nada que se destaque nos dados.", "f14", "suave")); return; }
            foreach (var it in itens)
            {
                var lin = E("pb-item");
                var q = E("pb-item-q", cor); lin.Add(q);
                var t = L(it, "f14", "quebra"); t.style.flexShrink = 1; lin.Add(t);
                pai.Add(lin);
            }
        }

        static Color CorNota(float v, float bom, float ruim) => v >= bom ? new Color(0.294f, 0.749f, 0.549f) : v < ruim ? new Color(0.925f, 0.416f, 0.373f) : new Color(0.384f, 0.659f, 0.933f);

        static VisualElement BlocoAnel(float valor, string rotulo, string centro, Color cor, string dica)
        {
            var b = E("pb-bloco"); b.pickingMode = PickingMode.Position;
            b.Add(new Anel(valor, cor, centro));
            var r = L(rotulo, "f13", "suave", "centro", "quebra"); r.style.marginTop = 6; b.Add(r);
            ComDica(b, dica);
            return b;
        }

        static VisualElement BlocoNumero(string rotulo, string valor, string dica)
        {
            var b = E("pb-bloco"); b.pickingMode = PickingMode.Position;
            var mid = E("pb-num-caixa"); mid.Add(L(valor, "pb-num")); b.Add(mid);
            var r = L(rotulo, "f13", "suave", "centro", "quebra"); r.style.marginTop = 6; b.Add(r);
            ComDica(b, dica);
            return b;
        }

        void Marcar(string jogada, bool tr, Carreira.MarcaJogada m)
        {
            var erro = c.MarcarJogada(jogada, tr, m);
            Salvar(); Mostrar();
            if (erro != null) Toast(erro, true);
        }

        static string NomeCategoria(Estrategias.Categoria k) => k switch
        {
            Estrategias.Categoria.Agressiva => "Agressiva", Estrategias.Categoria.Defensiva => "Defensiva",
            Estrategias.Categoria.ExecucoesRapidas => "Exec. rápidas", Estrategias.Categoria.ControleDeMapa => "Controle", _ => "Equilibrada",
        };
    }
}
