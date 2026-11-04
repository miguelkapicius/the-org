using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// UI Toolkit: a estrutura é montada aqui em código e o visual vem do USS (Resources/ManagerUI/Manager.uss), portado do
    /// mockup em prototipo/mockup-ui. As regras vêm de CSM.Manager.Carreira; aqui só desenho e clique. Todo número que a
    /// tela mostra (salário, preço, custo da Gaming House) é provisório. A cena é criada pelo menu CS Manager > Manager.
    /// </summary>
    public class GerenteUI : MonoBehaviour
    {
        public int seed = 7;
        public const string CENA_PARTIDA = "Partida3D";

        enum Aba { Painel, Elenco, Mercado, GamingHouse, Financas, PreMatch }
        Aba aba = Aba.Painel;
        Carreira c;
        VisualElement tela, janela, conteudo;
        string aviso; bool avisoErro;
        int filtroPainel = 1;   // 0 todos, 1 titulares, 2 reservas
        int filtroElenco;       // 0 todos, 1 titulares, 2 reservas
        int filtroFuncao;       // 0 todas, depois uma por função
        Jogador menuDe;         // jogador cujo menu "⋮" está aberto
        BonecosPreview preview;
        static readonly string[] FUNCOES_NOMES = { "Todas", "Entry", "Suporte", "Lurker", "AWPer", "IGL" };
        static readonly Role[] FUNCOES_ROLE = { Role.Entry, Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };   // [0] não é usado
        static readonly string[] FILTROS_ELENCO = { "Todos", "Titulares", "Reservas" };

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            if (Carreira.Atual == null) Carreira.Atual = Carreira.Nova(seed);
            c = Carreira.Atual;
            string pastaCaptura = Environment.GetEnvironmentVariable("CSM_CAPTURA");

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // 1600x900: a mesma referência do mockup; telas maiores escalam proporcionalmente, sem borrar o texto
            ps.referenceResolution = new Vector2Int(1600, 900);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            RenderTexture rtCaptura = null;
            if (!string.IsNullOrEmpty(pastaCaptura))
            {
                rtCaptura = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                ps.targetTexture = rtCaptura;
            }
#if UNITY_EDITOR
            // se o projeto tiver um tema de runtime, usa (o visual não depende dele: tudo está no Manager.uss)
            foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:ThemeStyleSheet"))
            {
                var t = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (t != null) { ps.themeStyleSheet = t; break; }
            }
#endif
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            yield return null;   // o UIDocument cria a raiz no quadro seguinte

            tela = E("tela-raiz");
            var estilo = Resources.Load<StyleSheet>("ManagerUI/Manager");
            if (estilo != null) tela.styleSheets.Add(estilo);
            else Debug.LogError("Falta Assets/Resources/ManagerUI/Manager.uss: a interface do manager fica sem estilo.");
            doc.rootVisualElement.Add(tela);
            // clicar fora do menu "⋮" fecha o menu
            tela.RegisterCallback<PointerDownEvent>(e =>
            {
                if (menuDe == null) return;
                for (var t = e.target as VisualElement; t != null; t = t.parent)
                    if (t.ClassListContains("dropdown") || t.ClassListContains("kebab")) return;
                menuDe = null; Mostrar();
            }, TrickleDown.TrickleDown);

            janela = E("janela");
            tela.Add(janela);
            Mostrar();

            if (rtCaptura != null) yield return Capturar(pastaCaptura, rtCaptura);
        }

        void OnDestroy() { if (preview != null) Destroy(preview.gameObject); }

        // ================================================================================================ estrutura
        void Mostrar()
        {
            janela.Clear();
            if (aba != Aba.PreMatch && preview != null) { Destroy(preview.gameObject); preview = null; }
            if (aba == Aba.PreMatch) { TelaPreMatch(); aviso = null; return; }

            janela.Add(MontarMenu());
            var principal = E("tela"); janela.Add(principal);

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

            var rolagem = new ScrollView(ScrollViewMode.Vertical); rolagem.AddToClassList("grow");
            rolagem.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            principal.Add(rolagem);
            conteudo = E("corpo"); rolagem.Add(conteudo);
            if (aviso != null) { conteudo.Add(Banner(aviso, avisoErro)); aviso = null; }
            switch (aba)
            {
                case Aba.Elenco: TelaElenco(); break;
                case Aba.Mercado: TelaMercado(); break;
                case Aba.GamingHouse: TelaGamingHouse(); break;
                case Aba.Financas: TelaFinancas(); break;
                default: TelaPainel(); break;
            }
        }

        static VisualElement Banner(string texto, bool erro)
        {
            var b = E("banner", erro ? "erro" : "ok");
            b.Add(L(texto));
            return b;
        }

        VisualElement MontarMenu()
        {
            var m = E("menu");

            var h = E("clube");
            h.Add(Brasao(c.clube, "laranja"));
            var nome = E("clube-nome");
            nome.Add(L(c.clube, "f18", "negrito"));
            nome.Add(L("Gaming House nv. " + c.nivelGH, "f13", "suave"));
            h.Add(nome);
            h.Add(new Icone("chev", 16));
            m.Add(h);

            var nav = E("menu-nav"); m.Add(nav);
            Item(nav, "Painel", "grid", Aba.Painel, null);
            Item(nav, "Elenco", "users", Aba.Elenco, c.elenco.Count.ToString());
            Item(nav, "Mercado", "swap", Aba.Mercado, c.mercado.Count.ToString());
            nav.Add(E("sep"));
            Item(nav, "Finanças", "dollar", Aba.Financas, null);
            Item(nav, "Gaming House", "house", Aba.GamingHouse, c.nivelGH.ToString());

            var prox = E("proxima");
            prox.Add(L("PRÓXIMA PARTIDA", "f12", "negrito", "apagado"));
            var vs = E("proxima-vs");
            var vsTxt = L("vs", "f16", "apagado"); vsTxt.style.marginLeft = 8; vsTxt.style.marginRight = 8;
            vs.Add(L(c.clube, "f18", "negrito")); vs.Add(vsTxt); vs.Add(L(c.proximo.nome, "f18", "negrito"));
            prox.Add(vs);
            prox.Add(Botao("Pré-partida", () => Ir(Aba.PreMatch), !c.Falido, true, "play", "cheio"));
            m.Add(prox);

            m.Add(L("Protótipo: salários, preços e prêmios são valores provisórios.", "f13", "apagado", "quebra", "rodape"));
            return m;
        }

        void Item(VisualElement menu, string texto, string icone, Aba a, string contagem)
        {
            var it = E("item"); if (aba == a) it.AddToClassList("ativo");
            it.Add(new Icone(icone, 22));
            it.Add(L(texto));
            if (contagem != null) { var ct = E("cont"); ct.Add(L(contagem)); it.Add(ct); }
            Clicavel(it, () => Ir(a));
            menu.Add(it);
        }

        VisualElement Cabecalho(string titulo, string sub)
        {
            var h = E("topo");
            var esq = E("titulo");
            esq.Add(L(titulo, "titulo-h"));
            esq.Add(L(sub, "f15", "suave"));
            h.Add(esq);

            var sal = E("mini-info");
            sal.Add(L("Salários por partida", "f13", "suave"));
            sal.Add(L(Fmt.Dinheiro(c.SalariosPorPartida), "f17", "negrito"));
            h.Add(sal);

            var u = E("usuario");
            u.Add(Brasao(c.clube, "laranja", true));
            var tx = E("usuario-txt");
            tx.Add(L(Fmt.Dinheiro(c.dinheiro), "f17", "negrito", c.dinheiro < 0 ? "vermelho" : "t"));
            tx.Add(L("saldo do clube", "f13", "suave"));
            u.Add(tx);
            h.Add(u);
            return h;
        }

        void Resposta(string erro, string ok)
        {
            if (erro != null) { aviso = erro; avisoErro = true; } else { aviso = ok; avisoErro = false; }
            menuDe = null;
            Mostrar();
        }

        void Ir(Aba a) { aba = a; menuDe = null; Mostrar(); }

        // ================================================================================================ painel
        void TelaPainel()
        {
            if (c.naoVisto != null)
            {
                var r = c.naoVisto; c.naoVisto = null;
                conteudo.Add(Banner((r.venceu ? "Vitória " : "Derrota ") + r.Placar + " contra " + r.adversario + (r.assistida ? "" : " (partida pulada)")
                    + "   ·   prêmio " + Fmt.Dinheiro(r.premio) + "   ·   salários " + Fmt.Dinheiro(-r.salarios), !r.venceu));
            }
            if (c.Falido)
            {
                var fc = E("cartao");
                fc.Add(L("Falência", "cartao-tit"));
                fc.Add(L("O dinheiro do clube acabou.", "f22", "negrito", "vermelho"));
                var p = L("O GDD define que o jogo acaba aqui. Ainda está em aberto se haverá aviso antes ou saída como empréstimo.", "f15", "suave", "quebra");
                p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.numPartida); c = Carreira.Atual; aba = Aba.Painel; Mostrar(); }, true, true));
                conteudo.Add(fc);
                return;
            }

            var atalhos = E("atalhos");
            atalhos.Add(Atalho("play", "Pré-partida", "Partida " + c.numPartida + " · contra " + c.proximo.nome, () => Ir(Aba.PreMatch), true));
            atalhos.Add(Atalho("skip", "Pular partida", "Só o resultado, por probabilidade", PularPartida));
            atalhos.Add(Atalho("swap", "Contratar", c.mercado.Count + " jogadores livres", () => Ir(Aba.Mercado)));
            atalhos.Add(Atalho("house", "Gaming House", c.nivelGH >= Provisorio.NIVEL_GH_MAX ? "Nível máximo" : "Subir por " + Fmt.Dinheiro(c.CustoProximoNivelGH), () => Ir(Aba.GamingHouse)));
            conteudo.Add(atalhos);

            conteudo.Add(L("Elenco", "h2"));
            conteudo.Add(Abas(FILTROS_ELENCO, filtroPainel, i => { filtroPainel = i; Mostrar(); }));
            var todos = c.elenco.ToList();
            Grade(filtroPainel == 1 ? todos.Where(c.Titular) : filtroPainel == 2 ? todos.Where(j => !c.Titular(j)) : todos);

            conteudo.Add(L("Últimas partidas", "h2"));
            if (c.historico.Count == 0) conteudo.Add(L("Nenhuma partida jogada ainda.", "f15", "suave"));
            foreach (var r in c.historico.Take(4))
            {
                var l = E("lista-l");
                var rod = L("Partida " + r.numPartida, "f15", "suave"); rod.style.width = 130; l.Add(rod);
                var selo = Selo(r.venceu ? "VITÓRIA" : "DERROTA", r.venceu ? "verde" : "vermelho"); selo.style.width = 96; l.Add(selo);
                var pl = L(r.Placar, "f18", "negrito"); pl.style.width = 100; pl.style.marginLeft = 18; l.Add(pl);
                l.Add(L("contra " + r.adversario + (r.assistida ? "" : " · pulada"), "f16", "suave"));
                conteudo.Add(l);
            }
        }

        static VisualElement Atalho(string icone, string rotulo, string sub, Action acao, bool destaque = false)
        {
            var a = E("atalho"); if (destaque) a.AddToClassList("destaque");
            a.Add(new Icone(icone, 24));
            var mais = new Icone("plus", 18); mais.AddToClassList("atalho-mais"); a.Add(mais);
            var tx = E();
            tx.Add(L(rotulo, "atalho-rot"));
            tx.Add(L(sub, "f14", "suave"));
            a.Add(tx);
            return Clicavel(a, acao);
        }

        /// <summary>Cartões de jogador em fileiras de 5 (a última fileira é completada com vazios para manter a largura).</summary>
        void Grade(IEnumerable<Jogador> jogadores)
        {
            var lista = jogadores.ToList();
            if (lista.Count == 0) { conteudo.Add(L("Nenhum jogador nesse filtro.", "f15", "suave")); return; }
            for (int i = 0; i < lista.Count; i += 5)
            {
                var fila = E("fila-cards");
                for (int k = 0; k < 5; k++) fila.Add(i + k < lista.Count ? CartaoJogador(lista[i + k]) : E("card-j", "vazio"));
                conteudo.Add(fila);
            }
        }

        VisualElement CartaoJogador(Jogador j)
        {
            var cj = E("card-j");
            var midia = E("midia");
            midia.Add(Avatar(j.nome, true));
            var ovr = E("ovr"); ovr.Add(L(j.Overall.ToString(), ClasseNota(j.Overall))); midia.Add(ovr);
            cj.Add(midia);
            var info = E("info");
            info.Add(L(j.nome, "info-nome"));
            info.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave", "info-meta"));
            info.Add(AtributoLinha("Mira", j.Mira));
            info.Add(AtributoLinha("Reflexo", j.Reflexo));
            info.Add(AtributoLinha("Spray", j.Spray));
            cj.Add(info);
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
        /// <summary>Tela cheia como a referência de pré-partida: os dois times no centro (brasão, força, lista), os bonecos 3D
        /// do jogo nas laterais e a barra de ações embaixo. O que o manager configura aqui ainda é pergunta aberta,
        /// por isso mapas e plano de jogo aparecem desligados.</summary>
        void TelaPreMatch()
        {
            var raiz = E("prematch");
            try { raiz.style.unityFontDefinition = FontDefinition.FromFont(Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Narrow", "Segoe UI" }, 40)); }
            catch (Exception) { /* sem a fonte condensada, fica a padrão */ }
            janela.Add(raiz);

            raiz.Add(new Quadriculado { name = "pixels" }.WithClass("pm-pixels"));

            // bonecos 3D reais (RenderTexture); sem eles a tela funciona do mesmo jeito
            if (preview != null) Destroy(preview.gameObject);
            preview = BonecosPreview.Criar();
            if (preview != null)
            {
                raiz.Add(BonecoImagem(preview.rtEsq, "esq"));
                raiz.Add(BonecoImagem(preview.rtDir, "dir"));
            }

            var rel = E("pm-relogio"); rel.Add(new Icone("clock", 22)); rel.Add(L("Partida " + c.numPartida)); raiz.Add(rel);
            raiz.Add(TagTime(c.clube, "VOCÊ", "esq"));
            raiz.Add(TagTime(c.proximo.nome, "ADVERSÁRIO", "dir"));

            var centro = E("pm-centro");
            int fa = c.Forca, fb = c.proximo.forca;
            centro.Add(LadoPreMatch(c.clube, fa, fa > fb ? "up" : fa < fb ? "down" : null, "laranja", c.Titulares.Select(j => (j.nome, j.funcao)).ToList()));
            centro.Add(E("pm-bola"));
            centro.Add(LadoPreMatch(c.proximo.nome, fb, fb > fa ? "up" : fb < fa ? "down" : null, "azul", c.proximo.jogadores));
            raiz.Add(centro);

            if (aviso != null) { var b = Banner(aviso, avisoErro); b.AddToClassList("pm-aviso"); raiz.Add(b); }

            var barra = E("pm-barra");
            var esq = E("seg");
            esq.Add(BotaoSeg("Voltar", () => Ir(Aba.Painel)));
            esq.Add(BotaoSeg("Elenco", () => Ir(Aba.Elenco)));
            barra.Add(esq);
            var comecar = E("comecar"); if (c.Falido) comecar.AddToClassList("desligado");
            comecar.Add(L("ASSISTIR PARTIDA"));
            var quad = E("quad"); quad.Add(new Icone("playf", 14)); comecar.Add(quad);
            if (!c.Falido) Clicavel(comecar, Assistir);
            barra.Add(comecar);
            var dir = E("seg");
            dir.Add(BotaoSeg("Pular partida", PularPartida, !c.Falido));
            dir.Add(BotaoSeg("Plano de jogo", null, false));
            barra.Add(dir);
            raiz.Add(barra);
        }

        static VisualElement BonecoImagem(RenderTexture rt, string lado)
        {
            var e = E("pm-boneco", lado);
            e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        static VisualElement TagTime(string nome, string sub, string lado)
        {
            var t = E("pm-tag", lado);
            var n = E("pm-tag-nome"); n.Add(L(nome.ToUpperInvariant())); t.Add(n);
            var s = E("pm-tag-sub"); s.Add(new Icone("house", 16)); s.Add(L(sub)); t.Add(s);
            return t;
        }

        static VisualElement BotaoSeg(string texto, Action acao, bool ativo = true)
        {
            var b = E("seg-b"); if (!ativo) b.AddToClassList("desligado");
            b.Add(L(texto.ToUpperInvariant()));
            if (ativo) Clicavel(b, acao);
            return b;
        }

        static VisualElement LadoPreMatch(string nome, int forca, string seta, string cor, List<(string nome, Role funcao)> jogadores)
        {
            var l = E("pm-time");
            var crest = E("crest", cor + "-bg"); crest.Add(L(nome.Substring(0, 1)));
            var f = E("forca"); f.Add(L(forca.ToString())); if (seta != null) f.Add(new Icone(seta, 16));
            crest.Add(f);
            l.Add(crest);
            l.Add(L(nome.ToUpperInvariant(), "pm-nome"));
            foreach (var (jn, jf) in jogadores)
            {
                var r = E("pm-j");
                var fn = E("pm-j-fn"); fn.Add(L(Fmt.FuncaoCurta(jf))); r.Add(fn);
                r.Add(L(jn.ToUpperInvariant()));
                l.Add(r);
            }
            return l;
        }

        // ================================================================================================ elenco e mercado
        /// <summary>Tabela de jogadores como a referência: sem cartão de fundo, só linhas finas. Colunas: pessoa, geral,
        /// atributos, salário, uma coluna extra e as ações (no fim da linha).</summary>
        VisualElement Tabela(IEnumerable<Jogador> jogadores, string tituloExtra, Func<Jogador, VisualElement> extra, Func<Jogador, VisualElement> acoes)
        {
            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")));
            h.Add(Celula("c-g", L("Geral")));
            foreach (var n in new[] { "Mira", "Reflexo", "Spray", "Util.", "Frio" }) h.Add(Celula("c-a", L(n)));
            h.Add(Celula("c-s", L("Salário")));
            h.Add(Celula("c-st", L(tituloExtra)));
            h.Add(Celula("c-k"));
            tab.Add(h);
            int linhas = 0;
            foreach (var j in jogadores)
            {
                linhas++;
                var r = E("tr");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(j.nome));
                var nomes = E("pessoa-txt");
                nomes.Add(L(j.nome, "f17", "negrito"));
                nomes.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave"));
                pessoa.Add(nomes);
                r.Add(Celula("c-j", pessoa));
                r.Add(Celula("c-g", L(j.Overall.ToString(), "geral", ClasseNota(j.Overall))));
                r.Add(CelulaAtributo(j.Mira)); r.Add(CelulaAtributo(j.Reflexo)); r.Add(CelulaAtributo(j.Spray));
                r.Add(CelulaAtributo(j.Utilitaria)); r.Add(CelulaAtributo(j.SangueFrio));
                r.Add(Celula("c-s", L(Fmt.Dinheiro(j.Salario), "f16")));
                r.Add(Celula("c-st", extra(j)));
                r.Add(Celula("c-k", acoes(j)));
                tab.Add(r);
            }
            if (linhas == 0) tab.Add(L("Nenhum jogador nesse filtro.", "f15", "suave"));
            return tab;
        }

        /// <summary>Botão "⋮" que abre o menu com as ações secundárias do jogador (como o "Copy link / Share" da referência).</summary>
        VisualElement MenuJogador(Jogador j)
        {
            bool aberto = menuDe == j;
            var k = E("kebab"); if (aberto) k.AddToClassList("on");
            k.Add(new Icone("kebab", 22));
            Clicavel(k, () => { menuDe = aberto ? null : j; Mostrar(); });
            return k;
        }

        /// <summary>O menu aberto é um elemento à parte, no fim da tabela (fica por cima de todas as linhas sem reordená-las).</summary>
        VisualElement Dropdown(Jogador j, int linha)
        {
            int i = c.elenco.IndexOf(j);
            var dd = E("dropdown");
            dd.style.top = 44 + linha * 68 + 4;   // altura do cabeçalho + linhas acima (ver .tr no USS)
            dd.Add(ItemMenu("up", "Subir na lista", i > 0, false, () => { menuDe = null; c.Mover(j, -1); Mostrar(); }));
            dd.Add(ItemMenu("down", "Descer na lista", i < c.elenco.Count - 1, false, () => { menuDe = null; c.Mover(j, 1); Mostrar(); }));
            dd.Add(ItemMenu("tag", "Vender por + " + Fmt.Dinheiro(j.Venda), c.elenco.Count > Provisorio.TITULARES, true, () => Resposta(c.Vender(j), j.nome + " foi vendido.")));
            return dd;
        }

        static VisualElement ItemMenu(string icone, string texto, bool ativo, bool perigo, Action acao)
        {
            var it = E("dd-item"); if (perigo) it.AddToClassList("perigo"); if (!ativo) it.AddToClassList("off");
            it.Add(new Icone(icone, 19)); it.Add(L(texto));
            if (ativo) Clicavel(it, acao);
            return it;
        }

        void TelaElenco()
        {
            var todos = c.elenco.ToList();
            conteudo.Add(Abas(FILTROS_ELENCO, filtroElenco, i => { filtroElenco = i; menuDe = null; Mostrar(); }));

            IEnumerable<Jogador> lista = filtroElenco == 1 ? todos.Where(c.Titular) : filtroElenco == 2 ? todos.Where(j => !c.Titular(j)) : todos;
            var linhasVisiveis = lista.ToList();
            var tabela = Tabela(linhasVisiveis, "Status",
                j => { bool tit = c.Titular(j); return Selo(tit ? "TITULAR" : "RESERVA", tit ? "laranja" : null); },
                MenuJogador);
            int idx = menuDe == null ? -1 : linhasVisiveis.IndexOf(menuDe);
            if (idx >= 0) tabela.Add(Dropdown(menuDe, idx));
            conteudo.Add(tabela);
            conteudo.Add(L("Os atributos são a leitura do que a IA do bot usa na partida. UTIL. = utilitária, FRIO = sangue-frio.", "legenda"));
        }

        void TelaMercado()
        {
            conteudo.Add(Abas(FUNCOES_NOMES, filtroFuncao, i => { filtroFuncao = i; Mostrar(); }));

            bool cheio = c.elenco.Count >= Provisorio.ELENCO_MAX;
            var lista = c.mercado.Where(j => filtroFuncao == 0 || j.funcao == FUNCOES_ROLE[filtroFuncao]);
            conteudo.Add(Tabela(lista, "Preço",
                j => L(Fmt.Dinheiro(j.Valor), "f16", "negrito", c.dinheiro >= j.Valor ? "t" : "vermelho"),
                j => Botao("Contratar", () => Resposta(c.Contratar(j), j.nome + " entrou no elenco (reserva)."), !cheio && c.dinheiro >= j.Valor, true)));
            if (cheio) conteudo.Add(L("Elenco cheio: venda alguém para contratar.", "f15", "laranja", "legenda"));
        }

        // ================================================================================================ gaming house
        void TelaGamingHouse()
        {
            var cartao = E("cartao");
            cartao.Add(L("NÍVEL", "cartao-tit"));
            var niveis = E("niveis");
            for (int n = 1; n <= Provisorio.NIVEL_GH_MAX; n++)
            {
                var q = E("nivel"); if (n <= c.nivelGH) q.AddToClassList("tem");
                q.Add(L(n.ToString()));
                niveis.Add(q);
            }
            cartao.Add(niveis);
            if (c.nivelGH >= Provisorio.NIVEL_GH_MAX) cartao.Add(L("Nível máximo.", "f18", "negrito", "verde"));
            else
            {
                var l = E("linha-h");
                var t = L("Subir para o nível " + (c.nivelGH + 1) + "  ·  " + Fmt.Dinheiro(c.CustoProximoNivelGH), "f18", "negrito"); t.style.marginRight = 22; l.Add(t);
                l.Add(Botao("Subir de nível", () => Resposta(c.SubirGamingHouse(), "Gaming House no nível " + c.nivelGH + "."), c.dinheiro >= c.CustoProximoNivelGH, true));
                cartao.Add(l);
            }
            conteudo.Add(cartao);

            var lib = E("cartao");
            lib.Add(L("O QUE ELA LIBERA", "cartao-tit"));
            foreach (var (nome, nivel) in Provisorio.LIBERA_GH)
            {
                bool aberto = c.nivelGH >= nivel;
                var l = E("lib");
                l.Add(L(nome, "lib-nome", aberto ? "t" : "suave"));
                l.Add(aberto ? Selo("LIBERADO", "verde") : Selo("BLOQUEADO · nível " + nivel));
                lib.Add(l);
            }
            var nota = L("Treinos e scouts ainda não existem no protótipo: aqui só mostramos o bloqueio. Quantos níveis são, o que cada um libera e o custo ainda são perguntas abertas.", "f14", "apagado", "quebra");
            nota.style.marginTop = 12; lib.Add(nota);
            conteudo.Add(lib);
        }

        // ================================================================================================ finanças
        void TelaFinancas()
        {
            var linha = E("resumos");
            linha.Add(Resumo("Saldo", Fmt.Dinheiro(c.dinheiro), c.dinheiro < 0 ? "vermelho" : "t"));
            linha.Add(Resumo("Salários por partida", Fmt.Dinheiro(-c.SalariosPorPartida), "vermelho"));
            linha.Add(Resumo("Prêmio (vitória / derrota)", Fmt.Dinheiro(Provisorio.PREMIO_VITORIA) + " / " + Fmt.Dinheiro(Provisorio.PREMIO_DERROTA), "verde"));
            conteudo.Add(linha);

            conteudo.Add(L("Extrato", "h2"));
            if (c.extrato.Count == 0) conteudo.Add(L("Sem movimentações ainda.", "f15", "suave"));
            foreach (var l in c.extrato.Take(12))
            {
                var r = E("lista-l");
                var rd = L("Partida " + l.numPartida, "f15", "suave"); rd.style.width = 130; r.Add(rd);
                var d = L(l.descricao, "f16", "grow"); r.Add(d);
                r.Add(L((l.valor > 0 ? "+" : "") + Fmt.Dinheiro(l.valor), "f16", "negrito", l.valor >= 0 ? "verde" : "vermelho"));
                conteudo.Add(r);
            }
        }

        static VisualElement Resumo(string titulo, string valor, string cor)
        {
            var r = E("resumo");
            r.Add(L(titulo, "f14", "suave"));
            r.Add(L(valor, "resumo-valor", cor));
            return r;
        }

        // ================================================================================================ captura (só para conferir o visual)
        /// <summary>Com a variável CSM_CAPTURA=&lt;pasta&gt;, salva um PNG de cada tela e fecha o Editor. Usado para conferir o visual
        /// sem abrir a janela (CS Manager > Manager > Capturar telas).</summary>
        IEnumerator Capturar(string pasta, RenderTexture rt)
        {
            Directory.CreateDirectory(pasta);
            var telas = new (string nome, Aba aba)[] { ("prematch", Aba.PreMatch), ("painel", Aba.Painel), ("elenco", Aba.Elenco), ("mercado", Aba.Mercado), ("financas", Aba.Financas), ("gaminghouse", Aba.GamingHouse) };
            foreach (var (nome, a) in telas)
            {
                aba = a;
                menuDe = a == Aba.Elenco && c.elenco.Count > 0 ? c.elenco[0] : null;
                Mostrar();
                for (int i = 0; i < 12; i++) yield return null;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                var antes = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(pasta, nome + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }

    static class ElementoExt
    {
        public static T WithClass<T>(this T e, string classe) where T : VisualElement { e.AddToClassList(classe); return e; }
    }
}
