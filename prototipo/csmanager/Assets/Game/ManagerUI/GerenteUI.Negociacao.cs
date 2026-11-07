using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Contratação por conversa (decisão 0026; referência: tela de chats): a lista de jogadores do mercado à esquerda, a conversa no
    /// meio com a proposta montada em degraus (luvas e salário), e à direita o que pesa para o jogador na decisão dele.</summary>
    public partial class GerenteUI
    {
        bool mercadoChat; Jogador chatJogador;

        /// <summary>Vitrine ou negociações: o alternador do alto da tela de mercado.</summary>
        VisualElement SegmentoMercado()
        {
            int n = c.negociacoes.Count(kv => c.mercado.Contains(kv.Key) && !kv.Value.fechou);
            var barra = E("pb-barra"); var seg = E("pb-seg");
            VisualElement Aba(string texto, bool chat)
            {
                var b = E("pb-seg-b"); if (mercadoChat == chat) b.AddToClassList("ativo");
                b.Add(L(texto));
                Clicavel(b, () => { mercadoChat = chat; Mostrar(); });
                return b;
            }
            seg.Add(Aba("VITRINE", false)); seg.Add(Aba(n > 0 ? "NEGOCIAÇÕES · " + n : "NEGOCIAÇÕES", true));
            barra.Add(seg);
            var dica = L("Ninguém tem preço fixo: cada jogador decide pelo conjunto e negocia com você.", "f14", "apagado"); dica.style.marginBottom = 14; barra.Add(dica);
            return barra;
        }

        void IniciarNegociacao(Jogador j) { chatJogador = j; mercadoChat = true; Negociador.Abrir(c, j); Mostrar(); }

        // ------------------------------------------------------------------------------------------ a tela
        void TelaNegociacao()
        {
            if (c.mercado.Count == 0) { conteudo.Add(L("O mercado está vazio esta semana.", "f15", "suave")); return; }
            if (chatJogador == null || !c.mercado.Contains(chatJogador))
                chatJogador = c.mercado.Where(x => c.negociacoes.ContainsKey(x)).OrderByDescending(x => x.Overall).FirstOrDefault() ?? c.mercado.OrderByDescending(x => x.Overall).First();
            var j = chatJogador; var n = Negociador.Abrir(c, j);

            var corpo = E("neg-corpo");
            var lista = new ScrollView(ScrollViewMode.Vertical); lista.AddToClassList("neg-lista"); lista.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            foreach (var x in c.mercado.OrderBy(x => c.negociacoes.ContainsKey(x) ? 0 : 1).ThenByDescending(x => x.Overall)) lista.Add(LinhaConversa(x, x == j));
            corpo.Add(lista);
            corpo.Add(Conversa(j, n));
            corpo.Add(PainelFatores(j, n));
            conteudo.Add(corpo);
        }

        VisualElement LinhaConversa(Jogador j, bool sel)
        {
            var jj = j; c.negociacoes.TryGetValue(j, out var n);
            var l = E("neg-linha"); if (sel) l.AddToClassList("sel");
            var foto = Retrato(j, "neg-foto"); l.Add(foto);
            var meio = E("neg-linha-meio");
            var topo = E("neg-linha-topo");
            topo.Add(L(j.nome, "f16", "negrito")); topo.Add(L(n != null && n.msgs.Count > 0 ? "Sem " + n.msgs[n.msgs.Count - 1].semana : "", "f12", "apagado"));
            meio.Add(topo);
            string previa = n == null || n.msgs.Count == 0 ? Fmt.Funcao(j.funcao) + " · geral " + j.Overall + " · clique para conversar" : n.msgs[n.msgs.Count - 1].texto;
            if (previa.Length > 44) previa = previa.Substring(0, 43) + "…";
            meio.Add(L(previa, "f13", "suave"));
            l.Add(meio);
            bool bloq = n != null && n.encerrada && c.Semana < n.bloqueadoAte;
            var ponto = E("neg-ponto"); if (bloq) ponto.AddToClassList("off"); l.Add(ponto);
            Clicavel(l, () => { chatJogador = jj; Mostrar(); });
            return l;
        }

        VisualElement Conversa(Jogador j, Negociacao n)
        {
            var painel = E("neg-conversa");
            bool bloq = n.encerrada && c.Semana < n.bloqueadoAte;
            var f = Negociador.Fatores(c, j, n.promessa); float desejo = Negociador.Desejo(f);

            var topo = E("neg-topo");
            topo.Add(Retrato(j, "neg-foto-g"));
            var tx = E(); tx.style.marginLeft = 14; tx.style.flexGrow = 1;
            tx.Add(L(j.nome, "neg-nome"));
            tx.Add(L(bloq ? "Ocupado · volta na semana " + n.bloqueadoAte : "Ativo agora", "f14", bloq ? "laranja" : "verde"));
            topo.Add(tx);
            string interesse = Negociador.Interesse(desejo);
            var si = Selo("INTERESSE " + interesse, interesse == "ALTO" ? "verde" : interesse == "MÉDIO" ? "laranja" : "vermelho");
            ComDica(si, "O quanto ele quer jogar no seu clube agora. Veja ao lado o que pesa.");
            topo.Add(si);
            painel.Add(topo);

            var msgs = new ScrollView(ScrollViewMode.Vertical); msgs.AddToClassList("neg-msgs"); msgs.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            foreach (var m in n.msgs) msgs.Add(Balao(j, m));
            msgs.schedule.Execute(() => msgs.scrollOffset = new Vector2(0, 100000f)).StartingIn(1);
            painel.Add(msgs);

            painel.Add(CaixaProposta(j, n, bloq));
            return painel;
        }

        VisualElement Balao(Jogador j, MsgNeg m)
        {
            if (m.sistema) { var s = E("neg-sistema"); s.Add(L(m.texto, "f14", "verde", "quebra")); return s; }
            var linha = E("neg-balao-linha"); if (m.nos) linha.AddToClassList("nos");
            if (!m.nos) linha.Add(Retrato(j, "neg-foto-p"));
            var b = E("neg-balao"); if (m.nos) b.AddToClassList("nos");
            var t = L(m.texto, "f15", "quebra"); b.Add(t);
            linha.Add(b);
            linha.Add(L("Sem " + m.semana, "neg-hora"));
            if (m.nos) linha.Add(Brasao(c.clube, "laranja", true));
            return linha;
        }

        VisualElement CaixaProposta(Jogador j, Negociacao n, bool bloq)
        {
            var cx = E("neg-oferta");
            bool cheio = c.elenco.Count >= c.ElencoMax;
            if (n.fechou) { cx.Add(L(j.nome + " já assinou com o seu clube.", "f15", "verde")); return cx; }
            if (bloq) { cx.Add(L(j.nome + " não quer conversar até a semana " + n.bloqueadoAte + ". Depois ele volta a ouvir propostas.", "f15", "suave", "quebra")); return cx; }
            if (cheio) { cx.Add(L("Elenco cheio (" + c.ElencoMax + "): venda alguém ou suba a Gaming House antes de contratar.", "f15", "laranja", "quebra")); return cx; }

            int passoLuvas = Math.Max(500, (int)Math.Round(j.Preco * 0.05 / 100.0) * 100);
            var linha = E("neg-passos");
            linha.Add(Passo("LUVAS", Fmt.Dinheiro(n.ofertaLuvas), () => { n.ofertaLuvas = Math.Max(0, n.ofertaLuvas - passoLuvas); Mostrar(); }, () => { n.ofertaLuvas += passoLuvas; Mostrar(); }, n.ofertaLuvas > 0,
                "Pagamento único na assinatura, tirado do caixa."));
            linha.Add(Passo("SALÁRIO POR SEMANA", Fmt.Dinheiro(n.ofertaSalario), () => { n.ofertaSalario = Math.Max(50, n.ofertaSalario - 50); Mostrar(); }, () => { n.ofertaSalario += 50; Mostrar(); }, n.ofertaSalario > 50,
                "Pago toda semana enquanto ele estiver no elenco."));
            var prom = BotaoMini("Garantir vaga de titular", () => { n.promessa = !n.promessa; Mostrar(); }, n.promessa);
            ComDica(prom, "Ele aceita pagar menos se tiver a vaga garantida, mas se não jogar por " + Negociador.PROMESSA_SEMANAS + " semanas a moral dele despenca.");
            prom.style.alignSelf = Align.FlexEnd; prom.style.marginBottom = 12;
            linha.Add(prom);
            cx.Add(linha);

            // quanto a proposta cobre do que ele pede (a conclusão antes do detalhe)
            float cob = Negociador.Cobertura(c, n, n.ofertaLuvas, n.ofertaSalario, n.promessa);
            var (pl, ps) = Negociador.Pedido(c, j, n.promessa);
            var rodape = E("neg-rodape");
            var res = E(); res.style.flexGrow = 1;
            res.Add(L("Sua proposta cobre " + Mathf.RoundToInt(Mathf.Min(cob, 1.5f) * 100) + "% do que ele pede", "f15", "negrito", cob >= 1f ? "verde" : cob >= 0.8f ? "laranja" : "vermelho"));
            res.Add(L("Ele pede " + Fmt.Dinheiro(pl) + " de luvas e " + Fmt.Dinheiro(ps) + " por semana · paciência " + new string('●', Mathf.Max(0, n.paciencia)) + new string('○', Mathf.Max(0, Negociador.PACIENCIA - n.paciencia)), "f13", "suave"));
            rodape.Add(res);
            var bots = E("neg-botoes");
            bool pode = c.dinheiro >= n.ofertaLuvas;
            if (n.contraPendente && (n.luvasPedida != n.ofertaLuvas || n.salarioPedido != n.ofertaSalario))
            {
                var ac = Botao("Aceitar o pedido", () => Enviar(j, n, n.luvasPedida, n.salarioPedido, n.promessa), c.dinheiro >= n.luvasPedida); ac.style.marginRight = 8; bots.Add(ac);
            }
            var env = Botao("Enviar proposta", () => Enviar(j, n, n.ofertaLuvas, n.ofertaSalario, n.promessa), pode, true, "play");
            if (!pode) ComDica(env, "Dinheiro insuficiente para essas luvas.");
            bots.Add(env);
            rodape.Add(bots);
            cx.Add(rodape);
            return cx;
        }

        VisualElement Passo(string rotulo, string valor, Action menos, Action mais, bool podeMenos, string dica)
        {
            var p = E("neg-passo");
            var r = L(rotulo, "dado-rot"); r.pickingMode = PickingMode.Position; ComDica(r, dica); p.Add(r);
            var l = E("linha-h"); l.style.marginTop = 6;
            l.Add(BotaoIcone("minus", menos, podeMenos));
            l.Add(L(valor, "neg-valor"));
            l.Add(BotaoIcone("plus", mais));
            p.Add(l);
            return p;
        }

        void Enviar(Jogador j, Negociacao n, int luvas, int salario, bool promessa)
        {
            var sai = c.QuemSaiPara(j);   // antes de contratar: quem ele substituiria
            n.promessa = promessa;
            var r = Negociador.Propor(c, n, luvas, salario, promessa);
            if (r.tipo == RespostaTipo.Contra) { n.ofertaLuvas = r.luvas; n.ofertaSalario = r.salario; }
            Salvar();
            if (r.tipo == RespostaTipo.Aceita)
            {
                proximoSomToast = Som.Dinheiro;
                Resposta(null, j.nome + " assinou: " + Fmt.Dinheiro(luvas) + " de luvas e " + Fmt.Dinheiro(salario) + "/sem.", sai != null ? "Escalar agora" : null, () =>
                {
                    c.TornarTitular(j); Salvar(); Mostrar();
                    Toast(j.nome + " agora é titular. Força " + c.Forca + ", chance de vitória " + Pct(c.ChanceVitoria()) + ".");
                });
                return;
            }
            tela.schedule.Execute(() => SomUi.Tocar(r.tipo == RespostaTipo.Recusa || r.tipo == RespostaTipo.Cansado ? Som.Erro : Som.Receber, 0.8f)).StartingIn(260);   // a resposta dele chega um instante depois
            Mostrar();
        }

        // ------------------------------------------------------------------------------------------ o que pesa
        VisualElement PainelFatores(Jogador j, Negociacao n)
        {
            var p = E("neg-fatores");
            var f = Negociador.Fatores(c, j, n.promessa); float d = Negociador.Desejo(f);
            p.Add(L("O QUE PESA PARA ELE", "dado-rot"));
            string it = Negociador.Interesse(d);
            p.Add(L("Interesse " + it.ToLowerInvariant(), "neg-interesse", it == "ALTO" ? "verde" : it == "MÉDIO" ? "laranja" : "vermelho"));
            foreach (var x in f)
            {
                var lin = E("neg-fator"); lin.pickingMode = PickingMode.Position; ComDica(lin, x.texto);
                var topo = E("neg-fator-topo");
                topo.Add(L(x.nome, "f15", "negrito"));
                topo.Add(L(x.valor >= 0.2f ? "a favor" : x.valor <= -0.2f ? "contra" : "neutro", "f12", x.valor >= 0.2f ? "verde" : x.valor <= -0.2f ? "vermelho" : "apagado"));
                lin.Add(topo);
                lin.Add(new BarraFator(x.valor));
                p.Add(lin);
            }
            var rod = E("neg-ficha");
            void Dado(string r, string v) { var l = E("camp-dado"); l.Add(L(r, "f13", "apagado")); l.Add(L(v, "f14", "negrito")); rod.Add(l); }
            Dado("Valor de mercado", Fmt.Dinheiro(j.Preco)); Dado("Salário de tabela", Fmt.Dinheiro(j.SalarioBase) + "/sem");
            Dado("Geral · idade", j.Overall + " · " + j.idade + " anos");
            if (j.traco != CSM.Sim.Traco.Nenhum && j.potencialRevelado) Dado("Traço", Tracos.Nome(j.traco));
            p.Add(rod);
            p.Add(L("Os fatores mudam com o seu clube: suba a Gaming House, escale melhor o time e cuide do caixa.", "f13", "apagado", "quebra"));
            return p;
        }

        /// <summary>Barra de -1 a +1 a partir do meio: verde para a direita (a favor), vermelha para a esquerda (contra).</summary>
        class BarraFator : VisualElement
        {
            readonly float v;
            public BarraFator(float valor)
            {
                v = Mathf.Clamp(valor, -1f, 1f); style.height = 10; style.marginTop = 6; style.alignSelf = Align.Stretch; pickingMode = PickingMode.Ignore;
                generateVisualContent += Desenhar;
            }
            void Desenhar(MeshGenerationContext mgc)
            {
                var p = mgc.painter2D; float w = contentRect.width, h = contentRect.height, meio = w / 2f, y = h / 2f;
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(1f, 1f, 1f, 0.05f); p.lineWidth = 6f; p.BeginPath(); p.MoveTo(new Vector2(3, y)); p.LineTo(new Vector2(w - 3, y)); p.Stroke();
                if (Mathf.Abs(v) > 0.01f)
                {
                    p.strokeColor = v >= 0 ? new Color(0.294f, 0.749f, 0.549f) : new Color(0.925f, 0.416f, 0.373f); p.lineWidth = 6f;
                    p.BeginPath(); p.MoveTo(new Vector2(meio, y)); p.LineTo(new Vector2(meio + v * (meio - 3), y)); p.Stroke();
                }
                p.strokeColor = new Color(1f, 1f, 1f, 0.35f); p.lineWidth = 2f; p.BeginPath(); p.MoveTo(new Vector2(meio, y - 6)); p.LineTo(new Vector2(meio, y + 6)); p.Stroke();
            }
        }
    }
}
