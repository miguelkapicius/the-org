using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CSM.Manager;
using CSM.Sim;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>
    /// A interface do manager: onde o jogador cuida do clube entre as partidas (elenco, mercado, Gaming House, finanças)
    /// e, na pré-partida, escolhe assistir ou pular a próxima partida.
    ///
    /// UI Toolkit montado em código (sem UXML/USS), estilo inline; peças em Ui.cs. As regras vêm de CSM.Manager.Carreira;
    /// aqui só desenho e clique. Todo número que a tela mostra (salário, preço, custo da Gaming House) é provisório.
    /// A cena é criada pelo menu CS Manager > Manager > Recriar cena do manager.
    /// </summary>
    public class GerenteUI : MonoBehaviour
    {
        public int seed = 7;
        public const string CENA_PARTIDA = "Partida3D";

        enum Aba { Painel, Elenco, Mercado, GamingHouse, Financas, PreMatch }
        Aba aba = Aba.Painel;
        Carreira c;
        VisualElement janela, conteudo;
        string aviso; bool avisoErro;
        int filtroElenco;    // 0 todos, 1 titulares, 2 reservas
        int filtroFuncao;    // 0 todas, depois uma por função
        static readonly string[] FUNCOES_NOMES = { "Todas", "Entry", "Suporte", "Lurker", "AWPer", "IGL" };
        static readonly Role[] FUNCOES_ROLE = { Role.Entry, Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };   // [0] não é usado

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            if (Carreira.Atual == null) Carreira.Atual = Carreira.Nova(seed);
            c = Carreira.Atual;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
#if UNITY_EDITOR
            // se o projeto tiver um tema de runtime, usa (a interface não depende dele: tudo é estilo inline)
            foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:ThemeStyleSheet"))
            {
                var t = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (t != null) { ps.themeStyleSheet = t; break; }
            }
#endif
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            yield return null;   // o UIDocument cria a raiz no quadro seguinte

            var tela = Box(FlexDirection.Column, Fundo, 28);   // fundo escuro com a "janela" arredondada por cima
            tela.style.position = Position.Absolute;
            tela.style.left = 0; tela.style.top = 0; tela.style.right = 0; tela.style.bottom = 0;
            doc.rootVisualElement.Add(tela);

            janela = Box(FlexDirection.Row, Janela, 0, 24);
            Borda(janela, Linha);
            janela.style.flexGrow = 1; janela.style.overflow = Overflow.Hidden;
            tela.Add(janela);

            Mostrar();
        }

        // ================================================================================================ estrutura
        void Mostrar()
        {
            janela.Clear();
            if (aba == Aba.PreMatch) { TelaPreMatch(); aviso = null; return; }

            janela.Add(MontarMenu());
            var principal = Box(FlexDirection.Column); principal.style.flexGrow = 1;
            janela.Add(principal);

            string titulo, sub;
            switch (aba)
            {
                case Aba.Elenco: titulo = "Elenco"; sub = "Os " + Provisorio.TITULARES + " primeiros da lista são os titulares da próxima partida."; break;
                case Aba.Mercado: titulo = "Mercado"; sub = "Jogadores livres para contratar. A lista se renova a cada partida."; break;
                case Aba.GamingHouse: titulo = "Gaming House"; sub = "Subir o nível libera outras melhorias (decisão 0007)."; break;
                case Aba.Financas: titulo = "Finanças"; sub = "Prêmios e vendas entram; salários, contratações e melhorias saem."; break;
                default: titulo = "Painel do clube"; sub = "Partida " + c.numPartida + " da carreira"; break;
            }
            principal.Add(Cabecalho(titulo, sub));

            conteudo = Box(FlexDirection.Column); conteudo.style.flexGrow = 1; Pad(conteudo, 32, 26);
            principal.Add(conteudo);
            if (aviso != null)
            {
                conteudo.Add(Banner(aviso, avisoErro ? Vermelho : Verde));
                aviso = null;
            }
            switch (aba)
            {
                case Aba.Elenco: TelaElenco(); break;
                case Aba.Mercado: TelaMercado(); break;
                case Aba.GamingHouse: TelaGamingHouse(); break;
                case Aba.Financas: TelaFinancas(); break;
                default: TelaPainel(); break;
            }
        }

        VisualElement Banner(string texto, Color cor)
        {
            var b = Box(FlexDirection.Row, new Color(cor.r, cor.g, cor.b, 0.12f), 0, 12); Pad(b, 20, 13);
            b.style.borderLeftWidth = 4; b.style.borderLeftColor = cor; b.style.marginBottom = 20;
            b.Add(Txt(texto, 16, cor, true));
            return b;
        }

        VisualElement MontarMenu()
        {
            var m = Box(FlexDirection.Column, Lateral); m.style.width = 280; m.style.flexShrink = 0; Pad(m, 18, 22);
            m.style.borderRightWidth = 1; m.style.borderRightColor = Linha;

            var h = Linha_(); h.style.marginBottom = 26; h.style.paddingLeft = 6;
            h.Add(Brasao(c.clube, 44, Laranja));
            var t = Box(); t.style.marginLeft = 14;
            t.Add(Txt(c.clube, 18, Texto, true));
            t.Add(Txt("Gaming House nv. " + c.nivelGH, 13, Suave));
            h.Add(t);
            m.Add(h);

            Item(m, "Painel", Aba.Painel, null);
            Item(m, "Elenco", Aba.Elenco, c.elenco.Count + "/" + Provisorio.ELENCO_MAX);
            Item(m, "Mercado", Aba.Mercado, c.mercado.Count.ToString());
            var div = Box(); div.style.height = 1; div.style.backgroundColor = Linha; div.style.marginTop = 10; div.style.marginBottom = 14; m.Add(div);
            Item(m, "Finanças", Aba.Financas, null);
            Item(m, "Gaming House", Aba.GamingHouse, "nv. " + c.nivelGH);

            var folga = Box(); folga.style.flexGrow = 1; m.Add(folga);
            m.Add(Paragrafo("Protótipo. Salários, preços, prêmios e custos da Gaming House são valores provisórios: o GDD ainda não decidiu como funcionam.", 12, Apagado));
            return m;
        }

        void Item(VisualElement menu, string texto, Aba a, string contagem)
        {
            bool atual = aba == a;
            var it = Linha_(); it.style.height = 46; Pad(it, 14, 0); Raio(it, 10); it.style.marginBottom = 4; it.style.justifyContent = Justify.SpaceBetween;
            it.style.backgroundColor = atual ? Painel2 : Transparente;
            it.Add(Txt(texto, 17, atual ? Texto : Suave, atual));
            if (contagem != null) it.Add(Contagem(contagem));
            it.RegisterCallback<PointerEnterEvent>(_ => { if (aba != a) it.style.backgroundColor = new Color(1, 1, 1, 0.04f); });
            it.RegisterCallback<PointerLeaveEvent>(_ => { if (aba != a) it.style.backgroundColor = Transparente; });
            it.RegisterCallback<ClickEvent>(_ => { aba = a; Mostrar(); });
            menu.Add(it);
        }

        VisualElement Cabecalho(string titulo, string sub)
        {
            var h = Linha_(); h.style.height = 92; h.style.flexShrink = 0; Pad(h, 32, 0); h.style.justifyContent = Justify.SpaceBetween;
            h.style.borderBottomWidth = 1; h.style.borderBottomColor = Linha;
            var esq = Box();
            esq.Add(Txt(titulo, 30, Texto, true));
            esq.Add(Txt(sub, 14, Suave));
            h.Add(esq);

            var dir = Linha_();
            var sal = Box(); sal.style.alignItems = Align.FlexEnd; sal.style.marginRight = 18;
            sal.Add(Txt("Salários por partida", 12, Suave));
            sal.Add(Txt(Fmt.Dinheiro(c.SalariosPorPartida), 15, Texto, true));
            dir.Add(sal);
            var saldo = Box(FlexDirection.Column, Painel, 0, 14); Pad(saldo, 18, 8); Borda(saldo, Linha);
            saldo.Add(Txt("Saldo do clube", 12, Suave));
            saldo.Add(Txt(Fmt.Dinheiro(c.dinheiro), 21, c.dinheiro < 0 ? Vermelho : Texto, true));
            dir.Add(saldo);
            h.Add(dir);
            return h;
        }

        void Resposta(string erro, string ok)
        {
            if (erro != null) { aviso = erro; avisoErro = true; } else { aviso = ok; avisoErro = false; }
            Mostrar();
        }

        void Ir(Aba a) { aba = a; Mostrar(); }

        // ================================================================================================ painel
        void TelaPainel()
        {
            if (c.naoVisto != null)
            {
                var r = c.naoVisto; c.naoVisto = null;
                conteudo.Add(Banner((r.venceu ? "Vitória " : "Derrota ") + r.Placar + " contra " + r.adversario + (r.assistida ? "" : " (partida pulada)")
                    + "   ·   prêmio " + Fmt.Dinheiro(r.premio) + "   ·   salários " + Fmt.Dinheiro(-r.salarios), r.venceu ? Verde : Vermelho));
            }
            if (c.Falido)
            {
                Cartao("Falência", out var fc);
                fc.Add(Txt("O dinheiro do clube acabou.", 22, Vermelho, true));
                var p = Paragrafo("O GDD define que o jogo acaba aqui. Ainda está em aberto se haverá aviso antes ou saída como empréstimo.", 15); p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.numPartida); c = Carreira.Atual; aba = Aba.Painel; Mostrar(); }, true, Lima, 260));
                conteudo.Add(fc.parent);
                return;
            }

            // cartões de ação
            var tiles = Box(FlexDirection.Row); tiles.style.marginBottom = 30;
            tiles.Add(Tile(">", "Pré-partida", "Partida " + c.numPartida + " · contra " + c.proximo.nome, () => Ir(Aba.PreMatch), true));
            tiles.Add(Tile(">>", "Pular partida", "Só o resultado, por probabilidade", PularPartida));
            tiles.Add(Tile("+", "Contratar", c.mercado.Count + " jogadores livres", () => Ir(Aba.Mercado)));
            tiles.Add(Tile("^", "Gaming House", c.nivelGH >= Provisorio.NIVEL_GH_MAX ? "Nível máximo" : "Subir por " + Fmt.Dinheiro(c.CustoProximoNivelGH), () => Ir(Aba.GamingHouse), false, true));
            conteudo.Add(tiles);

            // titulares
            var th = Linha_(); th.style.justifyContent = Justify.SpaceBetween; th.style.marginBottom = 14;
            th.Add(Txt("Elenco titular", 20, Texto, true));
            th.Add(Botao("Ver elenco", () => Ir(Aba.Elenco)));
            conteudo.Add(th);
            var cards = Box(FlexDirection.Row); cards.style.marginBottom = 30;
            int k = 0, total = c.Titulares.Count();
            foreach (var j in c.Titulares) { cards.Add(CartaoJogador(j, ++k == total)); }
            conteudo.Add(cards);

            // histórico
            conteudo.Add(Txt("Últimas partidas", 20, Texto, true));
            conteudo.Children().Last().style.marginBottom = 14;
            Cartao(null, out var ch, 8);
            var cartaoHist = ch.parent;
            if (c.historico.Count == 0) { var v = Paragrafo("Nenhuma partida jogada ainda.", 15); Pad(v, 14); ch.Add(v); }
            foreach (var r in c.historico.Take(4))
            {
                var l = Linha_(); l.style.height = 40; Pad(l, 14, 0);
                var rod = Txt("Partida " + r.numPartida, 14, Suave); rod.style.width = 120; l.Add(rod);
                l.Add(Selo(r.venceu ? "VITÓRIA" : "DERROTA", r.venceu ? Verde : Vermelho));
                var pl = Txt(r.Placar, 16, Texto, true); pl.style.width = 100; pl.style.marginLeft = 18; l.Add(pl);
                l.Add(Txt("contra " + r.adversario + (r.assistida ? "" : " · pulada"), 15, Suave));
                ch.Add(l);
            }
            conteudo.Add(cartaoHist);
        }

        VisualElement CartaoJogador(Jogador j, bool ultimo)
        {
            var cj = Box(FlexDirection.Column, Painel, 18, 16); Borda(cj, Linha);
            cj.style.flexGrow = 1; cj.style.flexBasis = 0; if (!ultimo) cj.style.marginRight = 14;
            var topo = Linha_(); topo.style.marginBottom = 14;
            topo.Add(Avatar(j.nome, 46));
            var nomes = Box(); nomes.style.marginLeft = 12; nomes.style.flexGrow = 1;
            nomes.Add(Txt(j.nome, 18, Texto, true));
            nomes.Add(Txt(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", 13, Suave));
            topo.Add(nomes);
            topo.Add(Txt(j.Overall.ToString(), 26, CorNota(j.Overall), true));
            cj.Add(topo);
            cj.Add(AtributoLinha("Mira", j.Mira));
            cj.Add(AtributoLinha("Reflexo", j.Reflexo));
            cj.Add(AtributoLinha("Spray", j.Spray));
            return cj;
        }

        void PularPartida()
        {
            c.Pular();
            aba = Aba.Painel;
            Mostrar();   // o painel mostra o resultado (naoVisto)
        }

        void Assistir()
        {
            if (!Application.CanStreamedLevelBeLoaded(CENA_PARTIDA))
            {
                Resposta("A cena " + CENA_PARTIDA + " não está no Build Settings. Rode o menu CS Manager > Manager > Recriar cena do manager.", null);
                return;
            }
            c.PrepararAssistir(1000 + c.numPartida * 17);
            SceneManager.LoadScene(CENA_PARTIDA);
        }

        // ================================================================================================ pré-partida
        /// <summary>Tela cheia, os dois times frente a frente; o que o manager configura aqui ainda é pergunta aberta,
        /// por isso mapas e plano de jogo aparecem desligados.</summary>
        void TelaPreMatch()
        {
            var raiz = Box(FlexDirection.Column); raiz.style.flexGrow = 1; Pad(raiz, 40, 28); raiz.style.alignItems = Align.Center;
            janela.Add(raiz);

            var topo = Linha_(); topo.style.justifyContent = Justify.SpaceBetween; topo.style.alignSelf = Align.Stretch;
            topo.Add(Selo("PRÉ-PARTIDA", Suave));
            topo.Add(Txt("Partida " + c.numPartida + " da carreira", 14, Suave));
            raiz.Add(topo);
            if (aviso != null) { var b = Banner(aviso, avisoErro ? Vermelho : Verde); b.style.marginTop = 14; raiz.Add(b); aviso = null; }

            var meio = Linha_(); meio.style.flexGrow = 1; meio.style.justifyContent = Justify.Center;
            meio.Add(LadoPreMatch(c.clube, c.Forca, Laranja, c.Titulares.Select(j => (j.nome, j.funcao)).ToList()));
            var vs = Txt("VS", 28, Apagado, true); vs.style.width = 160; vs.style.unityTextAlign = TextAnchor.MiddleCenter; meio.Add(vs);
            meio.Add(LadoPreMatch(c.proximo.nome, c.proximo.forca, Azul, c.proximo.jogadores));
            raiz.Add(meio);

            var barra = Linha_(); barra.style.backgroundColor = Lateral; Raio(barra, 18); Borda(barra, Linha); Pad(barra, 10);
            barra.style.width = 1180;
            var voltar = Botao("Voltar", () => Ir(Aba.Painel)); voltar.style.flexGrow = 1; voltar.style.marginRight = 8;
            var elenco = Botao("Elenco", () => Ir(Aba.Elenco)); elenco.style.flexGrow = 1; elenco.style.marginRight = 8;
            var assistir = Botao("ASSISTIR PARTIDA", Assistir, !c.Falido, Lima); assistir.style.width = 340; assistir.style.marginRight = 8; assistir.style.height = 54;
            var pular = Botao("Pular partida", PularPartida, !c.Falido); pular.style.flexGrow = 1; pular.style.marginRight = 8;
            var plano = Botao("Plano de jogo (em breve)", () => { }, false); plano.style.flexGrow = 1;
            barra.Add(voltar); barra.Add(elenco); barra.Add(assistir); barra.Add(pular); barra.Add(plano);
            raiz.Add(barra);
        }

        static VisualElement LadoPreMatch(string nome, int forca, Color cor, List<(string nome, Role funcao)> jogadores)
        {
            var l = Box(); l.style.width = 420; l.style.alignItems = Align.Center;
            l.Add(Brasao(nome, 132, cor));
            var f = Box(FlexDirection.Row, Lima, 0, 8); Pad(f, 12, 3); f.style.marginTop = -14; f.style.marginBottom = 14;
            f.Add(Txt("FORÇA " + forca, 14, Hex("12140a"), true));
            l.Add(f);
            var n = Txt(nome, 42, Texto, true); n.style.unityTextAlign = TextAnchor.MiddleCenter; n.style.marginBottom = 18;
            l.Add(n);
            foreach (var (jn, jf) in jogadores)
            {
                var r = Linha_(); r.style.height = 34; r.style.justifyContent = Justify.Center;
                var tag = Box(FlexDirection.Row, Painel2, 0, 6); Pad(tag, 7, 2); tag.style.width = 46; tag.style.justifyContent = Justify.Center; tag.style.marginRight = 12;
                tag.Add(Txt(Fmt.FuncaoCurta(jf), 11, Suave, true));
                r.Add(tag);
                var t = Txt(jn, 18, Texto); t.style.width = 150; r.Add(t);
                l.Add(r);
            }
            return l;
        }

        // ================================================================================================ elenco e mercado
        /// <summary>Tabela de jogadores: pessoa (avatar + nome), geral, atributos, salário, uma coluna extra e as ações.</summary>
        VisualElement Tabela(IEnumerable<Jogador> jogadores, string tituloExtra, Func<Jogador, VisualElement> extra, Func<Jogador, VisualElement> acoes, string tituloAcoes)
        {
            Cartao(null, out var corpo, 6);
            var cartao = corpo.parent;
            var h = LinhaTabela(true);
            h.Add(Celula(Txt("JOGADOR", 12, Suave, true), 250));
            h.Add(Celula(Txt("GERAL", 12, Suave, true), 70));
            foreach (var n in new[] { "MIRA", "REFLEXO", "SPRAY", "UTILITÁRIA", "SANGUE-FRIO" }) h.Add(Celula(Txt(n, 12, Suave, true), 104));
            h.Add(Celula(Txt("SALÁRIO", 12, Suave, true), 104));
            h.Add(Celula(Txt(tituloExtra, 12, Suave, true), 124));
            h.Add(Celula(Txt(tituloAcoes, 12, Suave, true), 0));
            corpo.Add(h);
            int linhas = 0;
            foreach (var j in jogadores)
            {
                linhas++;
                var r = LinhaTabela(); r.style.height = 62;
                var pessoa = Linha_();
                pessoa.Add(Avatar(j.nome, 40));
                var nomes = Box(); nomes.style.marginLeft = 12;
                nomes.Add(Txt(j.nome, 17, Texto, true));
                nomes.Add(Txt(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", 12, Suave));
                pessoa.Add(nomes);
                r.Add(Celula(pessoa, 250));
                r.Add(Celula(Txt(j.Overall.ToString(), 21, CorNota(j.Overall), true), 70));
                r.Add(Celula(Atributo(j.Mira), 104));
                r.Add(Celula(Atributo(j.Reflexo), 104));
                r.Add(Celula(Atributo(j.Spray), 104));
                r.Add(Celula(Atributo(j.Utilitaria), 104));
                r.Add(Celula(Atributo(j.SangueFrio), 104));
                r.Add(Celula(Txt(Fmt.Dinheiro(j.Salario), 15, Suave), 104));
                r.Add(Celula(extra(j), 124));
                r.Add(Celula(acoes(j), 0));
                r.RegisterCallback<PointerEnterEvent>(_ => r.style.backgroundColor = new Color(1, 1, 1, 0.03f));
                r.RegisterCallback<PointerLeaveEvent>(_ => r.style.backgroundColor = Transparente);
                corpo.Add(r);
            }
            if (linhas == 0) { var v = Paragrafo("Nenhum jogador nesse filtro.", 15); Pad(v, 18); corpo.Add(v); }
            return cartao;
        }

        void TelaElenco()
        {
            var todos = c.elenco.ToList();
            var chips = Chips(new[] { "Todos (" + todos.Count + ")", "Titulares (" + Math.Min(Provisorio.TITULARES, todos.Count) + ")", "Reservas (" + Math.Max(0, todos.Count - Provisorio.TITULARES) + ")" },
                filtroElenco, i => { filtroElenco = i; Mostrar(); });
            chips.style.marginBottom = 18;
            conteudo.Add(chips);

            IEnumerable<Jogador> lista = filtroElenco == 1 ? todos.Where(c.Titular) : filtroElenco == 2 ? todos.Where(j => !c.Titular(j)) : todos;
            conteudo.Add(Tabela(lista, "STATUS",
                j => { bool tit = c.Titular(j); return Selo(tit ? "TITULAR" : "RESERVA", tit ? Laranja : Suave); },
                j =>
                {
                    var a = Linha_();
                    var sobe = Botao("Subir", () => { c.Mover(j, -1); Mostrar(); }, c.elenco.IndexOf(j) > 0, null, 76); sobe.style.marginRight = 6;
                    var desce = Botao("Descer", () => { c.Mover(j, 1); Mostrar(); }, c.elenco.IndexOf(j) < c.elenco.Count - 1, null, 88); desce.style.marginRight = 10;
                    a.Add(sobe); a.Add(desce);
                    a.Add(Botao("Vender  +" + Fmt.Dinheiro(j.Venda), () => Resposta(c.Vender(j), j.nome + " foi vendido."), c.elenco.Count > Provisorio.TITULARES));
                    return a;
                }, "AÇÕES"));
            conteudo.Add(Paragrafo("Os atributos são a leitura do que a IA do bot usa na partida: mira, reflexo, spray, utilitária e sangue-frio.", 13, Apagado));
        }

        void TelaMercado()
        {
            var chips = Chips(FUNCOES_NOMES, filtroFuncao, i => { filtroFuncao = i; Mostrar(); });
            chips.style.marginBottom = 18;
            conteudo.Add(chips);

            bool cheio = c.elenco.Count >= Provisorio.ELENCO_MAX;
            var lista = c.mercado.Where(j => filtroFuncao == 0 || j.funcao == FUNCOES_ROLE[filtroFuncao]);
            conteudo.Add(Tabela(lista, "PREÇO",
                j => Txt(Fmt.Dinheiro(j.Valor), 15, c.dinheiro >= j.Valor ? Texto : Vermelho, true),
                j => Botao("Contratar", () => Resposta(c.Contratar(j), j.nome + " entrou no elenco (reserva)."), !cheio && c.dinheiro >= j.Valor, Lima),
                "CONTRATAÇÃO"));
            if (cheio) conteudo.Add(Paragrafo("Elenco cheio: venda alguém para contratar.", 14, Laranja));
        }

        // ================================================================================================ gaming house
        void TelaGamingHouse()
        {
            Cartao("Nível", out var corpo, 26);
            var cartao = corpo.parent;
            var niveis = Linha_(); niveis.style.marginBottom = 20;
            for (int n = 1; n <= Provisorio.NIVEL_GH_MAX; n++)
            {
                bool tem = n <= c.nivelGH;
                var q = Box(FlexDirection.Row, tem ? Lima : Painel2, 0, 14);
                q.style.width = 92; q.style.height = 68; q.style.marginRight = 12; q.style.alignItems = Align.Center; q.style.justifyContent = Justify.Center;
                q.Add(Txt(n.ToString(), 28, tem ? Hex("12140a") : Apagado, true));
                niveis.Add(q);
            }
            corpo.Add(niveis);
            if (c.nivelGH >= Provisorio.NIVEL_GH_MAX) corpo.Add(Txt("Nível máximo.", 18, Verde, true));
            else
            {
                var l = Linha_();
                var t = Txt("Subir para o nível " + (c.nivelGH + 1) + "  ·  " + Fmt.Dinheiro(c.CustoProximoNivelGH), 18, Texto, true); t.style.marginRight = 22; l.Add(t);
                l.Add(Botao("Subir de nível", () => Resposta(c.SubirGamingHouse(), "Gaming House no nível " + c.nivelGH + "."), c.dinheiro >= c.CustoProximoNivelGH, Lima));
                corpo.Add(l);
            }
            conteudo.Add(cartao);

            Cartao("O que ela libera", out var lib, 22);
            var libCartao = lib.parent;
            foreach (var (nome, nivel) in Provisorio.LIBERA_GH)
            {
                bool aberto = c.nivelGH >= nivel;
                var l = Linha_(); l.style.height = 44;
                var nm = Txt(nome, 18, aberto ? Texto : Suave, true); nm.style.width = 170; l.Add(nm);
                l.Add(aberto ? Selo("LIBERADO", Verde) : Selo("BLOQUEADO · nível " + nivel, Apagado));
                lib.Add(l);
            }
            lib.Add(Paragrafo("Treinos e scouts ainda não existem no protótipo: aqui só mostramos o bloqueio. Quantos níveis são, o que cada um libera e o custo ainda são perguntas abertas.", 13, Apagado));
            conteudo.Add(libCartao);
        }

        // ================================================================================================ finanças
        void TelaFinancas()
        {
            var linha = Box(FlexDirection.Row);
            linha.Add(Resumo("Saldo", Fmt.Dinheiro(c.dinheiro), c.dinheiro < 0 ? Vermelho : Texto, false));
            linha.Add(Resumo("Salários por partida", Fmt.Dinheiro(-c.SalariosPorPartida), Vermelho, false));
            linha.Add(Resumo("Prêmio (vitória / derrota)", Fmt.Dinheiro(Provisorio.PREMIO_VITORIA) + " / " + Fmt.Dinheiro(Provisorio.PREMIO_DERROTA), Verde, true));
            conteudo.Add(linha);

            Cartao("Extrato", out var ex);
            var cartao = ex.parent;
            if (c.extrato.Count == 0) ex.Add(Paragrafo("Sem movimentações ainda.", 15));
            foreach (var l in c.extrato.Take(12))
            {
                var r = Linha_(); r.style.height = 34;
                var rd = Txt("Partida " + l.numPartida, 14, Suave); rd.style.width = 120; r.Add(rd);
                var d = Txt(l.descricao, 15, Texto); d.style.flexGrow = 1; r.Add(d);
                r.Add(Txt((l.valor > 0 ? "+" : "") + Fmt.Dinheiro(l.valor), 15, l.valor >= 0 ? Verde : Vermelho, true));
                ex.Add(r);
            }
            conteudo.Add(cartao);
        }

        static VisualElement Resumo(string titulo, string valor, Color cor, bool ultimo)
        {
            var cartao = Cartao(titulo, out var corpo);
            cartao.style.flexGrow = 1; cartao.style.flexBasis = 0; if (!ultimo) cartao.style.marginRight = 14;
            corpo.Add(Txt(valor, 28, cor, true));
            return cartao;
        }
    }
}
