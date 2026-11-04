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
    /// A interface do manager: onde o jogador cuida do clube entre as partidas (escalação, mercado, Gaming House, finanças),
    /// escolhe assistir ou pular a próxima partida (pré-partida) e vê como foi (pós-partida).
    ///
    /// UI Toolkit: a estrutura é montada aqui e o visual vem do USS (Resources/ManagerUI/Manager.uss), portado do mockup em
    /// prototipo/mockup-ui. As regras vêm de CSM.Manager.Carreira; aqui só desenho e clique. Todo número que a tela mostra
    /// (salário, preço, custo da Gaming House, chance de vitória) é provisório. A cena é criada pelo menu CS Manager > Manager.
    /// </summary>
    public class GerenteUI : MonoBehaviour
    {
        public int seed = 7;
        public const string CENA_PARTIDA = "Partida3D";

        enum Aba { Painel, Elenco, Mercado, GamingHouse, Financas, PreMatch, PosPartida }
        Aba aba = Aba.Painel, abaAnterior = Aba.Painel;
        Carreira c;
        VisualElement tela, janela, conteudo, toastEl, dicaEl;
        Label dicaTxt, dicaTroca, previaTxt;
        VisualElement previaCaixa;
        ScrollView rolagem;
        Vector2 rolagemGuardada;
        bool captura;
        RenderTexture retratoRT;

        int filtroPainel = 1;   // 0 todos, 1 titulares, 2 reservas
        int filtroElenco;       // 0 todos, 1 titulares, 2 reservas
        int filtroFuncao;       // 0 todas, depois uma por função
        Jogador menuDe;         // jogador cujo menu "⋮" está aberto
        Jogador selecionado;    // jogador escolhido na escalação (o próximo clique troca de lugar)
        readonly List<Jogador> comparar = new List<Jogador>();
        int colOrdem = -1; bool ordemAsc;   // ordenação das tabelas (-1 = ordem do elenco / do mercado)
        Resultado resumoAberto;             // resumo de partida antiga aberto pelo histórico
        bool confirmandoNova;
        BonecosPreview preview;

        static readonly string[] FUNCOES_NOMES = { "Todas", "Entry", "Suporte", "Lurker", "AWPer", "IGL" };
        static readonly Role[] FUNCOES_ROLE = { Role.Entry, Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };   // [0] não é usado
        static readonly string[] FILTROS_ELENCO = { "Todos", "Titulares", "Reservas" };
        static readonly Role[] FUNCOES_TIME = { Role.Igl, Role.Entry, Role.Awper, Role.Lurker, Role.Suporte };

        /// <summary>Colunas das tabelas: rótulo, explicação (dica) e a chave de ordenação.</summary>
        static readonly (string rot, string dica, Func<Jogador, int> chave)[] COLS =
        {
            ("Geral", "Média dos atributos; mira e reflexo valem o dobro.", j => j.Overall),
            ("Mira", "Precisão do tiro: quanto maior, menos o bot erra a mira.", j => j.Mira),
            ("Reflexo", "Rapidez para reagir quando vê um inimigo.", j => j.Reflexo),
            ("Spray", "Controle do recuo em rajadas.", j => j.Spray),
            ("Util.", "Utilitária: o quanto o bot acerta o pixel das granadas.", j => j.Utilitaria),
            ("Frio", "Sangue-frio: erra menos em clutch e sob pressão.", j => j.SangueFrio),
            ("Salário", "Pago a cada partida da carreira.", j => j.Salario),
        };

        // ================================================================================================ início
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            string pastaCaptura = Environment.GetEnvironmentVariable("CSM_CAPTURA");
            captura = !string.IsNullOrEmpty(pastaCaptura);
            if (Carreira.Atual == null) Carreira.Atual = (captura ? null : Salvamento.Carregar()) ?? Carreira.Nova(seed);
            c = Carreira.Atual;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // 1600x900: a mesma referência do mockup; telas maiores escalam proporcionalmente, sem borrar o texto
            ps.referenceResolution = new Vector2Int(1600, 900);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            RenderTexture rtCaptura = null;
            if (captura)
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
            tela.focusable = true;
            tela.RegisterCallback<KeyDownEvent>(AoTeclar);
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
            LigarDica();

            retratoRT = BonecosPreview.GerarRetrato();
            RetratoBoneco = retratoRT;

            Mostrar();
            tela.Focus();

            if (rtCaptura != null) yield return Capturar(pastaCaptura, rtCaptura);
        }

        void OnDestroy()
        {
            if (preview != null) Destroy(preview.gameObject);
            if (retratoRT != null) { retratoRT.Release(); Destroy(retratoRT); }
            RetratoBoneco = null; Ui.Dica = null;
        }

        void Salvar() { if (!captura && c != null) Salvamento.Salvar(c); }

        // ================================================================================================ teclado
        void AoTeclar(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Alpha1: Ir(Aba.Painel); break;
                case KeyCode.Alpha2: Ir(Aba.Elenco); break;
                case KeyCode.Alpha3: Ir(Aba.Mercado); break;
                case KeyCode.Alpha4: Ir(Aba.Financas); break;
                case KeyCode.Alpha5: Ir(Aba.GamingHouse); break;
                case KeyCode.Escape:
                    if (aba == Aba.PreMatch || aba == Aba.PosPartida) Ir(Aba.Painel);
                    else { menuDe = null; selecionado = null; Mostrar(); }
                    break;
                case KeyCode.Return: case KeyCode.KeypadEnter:
                    if (aba == Aba.PreMatch && !c.Falido) Assistir();
                    else if (aba == Aba.PosPartida) Continuar();
                    break;
            }
        }

        // ================================================================================================ avisos temporários e dicas
        /// <summary>Aviso que some sozinho (no canto), com um botão opcional (ex.: Desfazer). Não é refeito quando a tela é redesenhada.</summary>
        void Toast(string texto, bool erro = false, string acaoRotulo = null, Action acao = null)
        {
            toastEl?.RemoveFromHierarchy();
            var t = E("toast", erro ? "erro" : "ok");
            t.Add(L(texto));
            if (acaoRotulo != null)
            {
                var b = E("toast-acao"); b.Add(L(acaoRotulo));
                Clicavel(b, () => { t.RemoveFromHierarchy(); acao?.Invoke(); });
                t.Add(b);
            }
            tela.Add(t); toastEl = t;
            t.schedule.Execute(() => { if (t.parent != null) t.RemoveFromHierarchy(); }).StartingIn(acaoRotulo != null ? 7000 : 4200);
        }

        void LigarDica()
        {
            dicaEl = E("dica"); dicaEl.pickingMode = PickingMode.Ignore; dicaEl.style.display = DisplayStyle.None;
            dicaTxt = L("", "quebra"); dicaEl.Add(dicaTxt);
            tela.Add(dicaEl);
            void Posicionar(Vector2 p)
            {
                float larg = tela.resolvedStyle.width, x = p.x + 16;
                if (x + 340 > larg) x = p.x - 340 - 8;
                dicaEl.style.left = Mathf.Max(8, x); dicaEl.style.top = p.y + 22;
            }
            Ui.Dica = (el, txt) =>
            {
                el.RegisterCallback<PointerEnterEvent>(ev => { dicaTxt.text = txt; dicaEl.style.display = DisplayStyle.Flex; Posicionar(ev.position); dicaEl.BringToFront(); });
                el.RegisterCallback<PointerMoveEvent>(ev => Posicionar(ev.position));
                el.RegisterCallback<PointerLeaveEvent>(_ => dicaEl.style.display = DisplayStyle.None);
            };
        }

        // ================================================================================================ estrutura
        void Mostrar()
        {
            if (dicaEl != null) dicaEl.style.display = DisplayStyle.None;   // o elemento sob o mouse pode ter sumido sem avisar
            // volta para a posição de rolagem em que estava (só zera ao trocar de tela)
            bool mudou = aba != abaAnterior;
            rolagemGuardada = !mudou && rolagem != null ? rolagem.scrollOffset : Vector2.zero;
            if (c.naoVisto != null && aba == Aba.Painel) aba = Aba.PosPartida;   // o resultado da partida que acabou vem antes do painel
            mudou = aba != abaAnterior; abaAnterior = aba;
            janela.Clear(); previaTxt = null; previaCaixa = null; dicaTroca = null; rolagem = null;
            if (aba == Aba.PreMatch) { TelaPreMatch(); return; }

            janela.Add(MontarMenu());
            var principal = E("tela"); janela.Add(principal);
            if (mudou) { principal.AddToClassList("entra"); principal.schedule.Execute(() => principal.AddToClassList("visivel")); }

            string titulo, sub;
            switch (aba)
            {
                case Aba.Elenco: titulo = "Elenco"; sub = "Escale os titulares, compare jogadores e veja o efeito de cada troca antes de fazê-la."; break;
                case Aba.Mercado: titulo = "Mercado"; sub = "Jogadores livres para contratar. A lista se renova a cada partida."; break;
                case Aba.GamingHouse: titulo = "Gaming House"; sub = "Subir o nível libera outras melhorias (decisão 0007)."; break;
                case Aba.Financas: titulo = "Finanças"; sub = "Prêmios e vendas entram; salários, contratações e melhorias saem."; break;
                case Aba.PosPartida: { var r = resumoAberto ?? c.naoVisto; titulo = "Pós-partida"; sub = r != null ? "Partida " + r.numPartida + " da carreira · contra " + r.adversario : ""; break; }
                default: titulo = "Painel do clube"; sub = "Partida " + c.numPartida + " da carreira"; break;
            }
            principal.Add(Cabecalho(titulo, sub));

            rolagem = new ScrollView(ScrollViewMode.Vertical); rolagem.AddToClassList("grow");
            rolagem.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            principal.Add(rolagem);
            conteudo = E("corpo"); rolagem.Add(conteudo);
            switch (aba)
            {
                case Aba.Elenco: TelaElenco(); break;
                case Aba.Mercado: TelaMercado(); break;
                case Aba.GamingHouse: TelaGamingHouse(); break;
                case Aba.Financas: TelaFinancas(); break;
                case Aba.PosPartida: TelaPosPartida(); break;
                default: TelaPainel(); break;
            }
            if (rolagemGuardada != Vector2.zero) { var alvo = rolagemGuardada; rolagem.schedule.Execute(() => rolagem.scrollOffset = alvo).StartingIn(1); }
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
            Item(nav, "Painel", "grid", Aba.Painel, c.Avisos().Count > 0 ? c.Avisos().Count.ToString() : null, "Painel (1)");
            Item(nav, "Elenco", "users", Aba.Elenco, c.elenco.Count.ToString(), "Elenco (2)");
            Item(nav, "Mercado", "swap", Aba.Mercado, c.mercado.Count.ToString(), "Mercado (3)");
            nav.Add(E("sep"));
            Item(nav, "Finanças", "dollar", Aba.Financas, null, "Finanças (4)");
            Item(nav, "Gaming House", "house", Aba.GamingHouse, c.nivelGH.ToString(), "Gaming House (5)");

            var prox = E("proxima");
            prox.Add(L("PRÓXIMA PARTIDA", "f12", "negrito", "apagado"));
            var vs = E("proxima-vs");
            var vsTxt = L("vs", "f16", "apagado"); vsTxt.style.marginLeft = 8; vsTxt.style.marginRight = 8;
            vs.Add(L(c.clube, "f18", "negrito")); vs.Add(vsTxt); vs.Add(L(c.proximo.nome, "f18", "negrito"));
            prox.Add(vs);
            prox.Add(L("Chance de vitória " + Pct(c.ChanceVitoria()), "f14", "suave", "info-meta"));
            prox.Add(Botao("Pré-partida", () => Ir(Aba.PreMatch), !c.Falido, true, "play", "cheio"));
            m.Add(prox);

            // recomeçar a carreira pede confirmação (apaga o salvamento)
            var nova = E("aviso-btn"); nova.style.alignSelf = Align.FlexStart; nova.style.marginBottom = 10;
            nova.Add(L(confirmandoNova ? "Apagar e recomeçar?" : "Nova carreira", confirmandoNova ? "vermelho" : "suave"));
            Clicavel(nova, () =>
            {
                if (!confirmandoNova) { confirmandoNova = true; Mostrar(); return; }
                confirmandoNova = false; Carreira.Atual = Carreira.Nova(seed + c.numPartida + 101); c = Carreira.Atual;
                comparar.Clear(); selecionado = null; menuDe = null; resumoAberto = null; Salvar();
                aba = Aba.Painel; Mostrar(); Toast("Carreira nova começada.");
            });
            m.Add(nova);
            m.Add(L("Carreira salva automaticamente. Teclas 1 a 5 trocam de tela. Valores provisórios (protótipo).", "f13", "apagado", "quebra", "rodape"));
            return m;
        }

        void Item(VisualElement menu, string texto, string icone, Aba a, string contagem, string dica)
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

        /// <summary>Mostra o resultado de uma ação: erro vira aviso vermelho, sucesso vira aviso verde (que some sozinho).</summary>
        void Resposta(string erro, string ok, string acaoRotulo = null, Action acao = null)
        {
            menuDe = null;
            if (erro == null) Salvar();
            Mostrar();
            if (erro != null) Toast(erro, true); else Toast(ok, false, acaoRotulo, acao);
        }

        void Ir(Aba a) { aba = a; menuDe = null; selecionado = null; colOrdem = -1; confirmandoNova = false; if (a != Aba.PosPartida) resumoAberto = null; Mostrar(); }

        static string Pct(double p) => Math.Round(p * 100) + "%";

        // ================================================================================================ painel
        void TelaPainel()
        {
            if (c.Falido)
            {
                var fc = E("cartao");
                fc.Add(L("Falência", "cartao-tit"));
                fc.Add(L("O dinheiro do clube acabou.", "f22", "negrito", "vermelho"));
                var p = L("O GDD define que o jogo acaba aqui. Ainda está em aberto se haverá aviso antes ou saída como empréstimo.", "f15", "suave", "quebra");
                p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.numPartida); c = Carreira.Atual; Salvar(); aba = Aba.Painel; Mostrar(); }, true, true));
                conteudo.Add(fc);
                return;
            }

            // o que merece atenção agora (mais grave primeiro)
            conteudo.Add(L("O que merece atenção", "h2", "topo-h2"));
            var avisos = c.Avisos();
            var caixa = E("avisos");
            if (avisos.Count == 0) { var ok = E("tudo-ok"); ok.Add(L("Tudo em ordem: nada pede a sua atenção agora.", "f16", "verde")); caixa.Add(ok); }
            bool primeiroAviso = true;
            foreach (var a in avisos.Take(3))
            {
                var l = E("aviso"); if (primeiroAviso) l.AddToClassList("primeiro"); primeiroAviso = false;
                l.Add(E("aviso-ponto", a.nivel == Aviso.Nivel.Critico ? "critico" : a.nivel == Aviso.Nivel.Atencao ? "atencao" : "neutro"));
                l.Add(L(a.texto, "aviso-txt"));
                var b = E("aviso-btn"); b.Add(L(a.acao));
                var destino = a.destino;
                Clicavel(b, () => Ir(destino == "elenco" ? Aba.Elenco : destino == "mercado" ? Aba.Mercado : destino == "gh" ? Aba.GamingHouse : Aba.Financas));
                l.Add(b);
                caixa.Add(l);
            }
            conteudo.Add(caixa);

            var atalhos = E("atalhos");
            atalhos.Add(Atalho("play", "Pré-partida", "Partida " + c.numPartida + " · contra " + c.proximo.nome, () => Ir(Aba.PreMatch), true));
            atalhos.Add(Atalho("skip", "Pular partida", "Só o resultado · chance de vitória " + Pct(c.ChanceVitoria()), PularPartida));
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
                var gr = L("contra " + r.adversario + (r.assistida ? "" : " · pulada"), "f16", "suave"); gr.style.flexGrow = 1; l.Add(gr);
                l.Add(L("Ver resumo", "f14", "suave"));
                var rr = r; Clicavel(l, () => { resumoAberto = rr; Ir(Aba.PosPartida); });
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
            midia.Add(Retrato(j.nome, "retrato-fill"));
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
            Salvar();
            aba = Aba.Painel;
            Mostrar();   // o resultado aparece na pós-partida
        }

        void Assistir()
        {
            if (!Application.CanStreamedLevelBeLoaded(CENA_PARTIDA))
            {
                Toast("A cena " + CENA_PARTIDA + " não está no Build Settings. Rode o menu CS Manager > Manager > Recriar cena do manager.", true);
                return;
            }
            Salvar();
            c.PrepararAssistir(1000 + c.numPartida * 17);
            SceneManager.LoadScene(CENA_PARTIDA);
        }

        // ================================================================================================ pré-partida
        /// <summary>Tela cheia como a referência de pré-partida: os dois times no centro (brasão, força, lista), os bonecos 3D
        /// do jogo nas laterais, a chance de vitória e a barra de ações embaixo. O que o manager configura aqui ainda é pergunta
        /// aberta, por isso mapas e plano de jogo aparecem desligados.</summary>
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

            // chance de vitória: é a mesma que a partida pulada usa (decisão 0008), então dá para decidir entre assistir e pular
            double chance = c.ChanceVitoria(); int pct = (int)Math.Round(chance * 100);
            var ch = E("pm-chance");
            ch.Add(L("CHANCE DE VITÓRIA", "pm-chance-tit"));
            ch.Add(L(pct + "%", "pm-chance-num"));
            var barra0 = E("pm-chance-barra");
            var ba = E("pm-chance-a"); ba.style.flexGrow = Mathf.Max(1, pct); barra0.Add(ba);
            var bb = E("pm-chance-b"); bb.style.flexGrow = Mathf.Max(1, 100 - pct); barra0.Add(bb);
            ch.Add(barra0);
            ch.Add(L("Sua força " + fa + " contra " + fb + ". Se você pular a partida, o resultado sai por essa chance.", "pm-chance-sub"));
            raiz.Add(ch);

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

            var dicas = E("pm-dicas");
            foreach (var (tecla, txt) in new[] { ("Enter", "ASSISTIR"), ("Esc", "VOLTAR") })
            {
                var it = E("pm-dicas-item"); var tk = E("tecla"); tk.Add(L(tecla)); it.Add(tk); it.Add(L(txt)); dicas.Add(it);
            }
            raiz.Add(dicas);
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

        // ================================================================================================ pós-partida
        void TelaPosPartida()
        {
            var r = resumoAberto ?? c.naoVisto;
            if (r == null) { aba = Aba.Painel; Mostrar(); return; }

            var hero = E("hero", r.venceu ? "venceu" : "perdeu");
            var esq = E();
            esq.Add(L(r.venceu ? "VITÓRIA" : "DERROTA", "hero-res", r.venceu ? "verde" : "vermelho"));
            esq.Add(L(c.clube + " contra " + r.adversario, "f20", "negrito"));
            hero.Add(esq);
            var meio = E("hero-meio"); meio.Add(L(r.meus + "  x  " + r.deles, "hero-placar")); hero.Add(meio);
            var dir = E(); dir.style.alignItems = Align.FlexEnd;
            dir.Add(L(r.assistida ? "Partida assistida" : "Partida pulada", "f16", "suave"));
            dir.Add(L("Força " + r.forcaMinha + " contra " + r.forcaDeles, "f16", "suave"));
            hero.Add(dir);
            conteudo.Add(hero);

            int saldoDepois = r.saldoAntes + r.premio - r.salarios;
            var resumos = E("resumos");
            resumos.Add(Resumo("Prêmio", "+" + Fmt.Dinheiro(r.premio), "verde"));
            resumos.Add(Resumo("Salários pagos", Fmt.Dinheiro(-r.salarios), "vermelho"));
            resumos.Add(Resumo("Saldo do clube", Fmt.Dinheiro(r.saldoAntes) + "  →  " + Fmt.Dinheiro(saldoDepois), saldoDepois < 0 ? "vermelho" : "t"));
            conteudo.Add(resumos);

            conteudo.Add(L("Desempenho dos jogadores", "h2"));
            if (r.estimado) conteudo.Add(L("Números estimados: a partida foi pulada, então não houve simulação. Assista para ver o desempenho real.", "nota-estimada"));
            var melhor = r.Melhor;
            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")).WithClass("largo"));
            foreach (var n in new[] { "Abates", "Mortes", "K/D", "HS %", "Dano/round", "1º abate" }) { var cel = Celula("c-n", L(n)); cel.AddToClassList("cab"); h.Add(cel); }
            tab.Add(h);
            foreach (var d in r.desempenho)
            {
                var linha = E("tr");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(d.nome));
                var nomes = E("pessoa-txt");
                nomes.Add(L(d.nome, "f17", "negrito"));
                nomes.Add(L(Fmt.Funcao(d.funcao), "f14", "suave"));
                pessoa.Add(nomes);
                if (d == melhor) { var s = Selo("MELHOR", "verde"); s.style.marginLeft = 12; pessoa.Add(s); }
                linha.Add(Celula("c-j", pessoa).WithClass("largo"));
                linha.Add(Celula("c-n", L(d.k.ToString(), "negrito")));
                linha.Add(Celula("c-n", L(d.d.ToString())));
                linha.Add(Celula("c-n", L(d.KD.ToString("0.00"), "negrito", d.KD >= 1.2f ? "verde" : d.KD >= 0.8f ? "t" : "vermelho")));
                linha.Add(Celula("c-n", L(d.HsPct + "%")));
                linha.Add(Celula("c-n", L(d.DanoPorRound.ToString())));
                linha.Add(Celula("c-n", L(d.fk.ToString())));
                tab.Add(linha);
            }
            conteudo.Add(tab);

            var botoes = E("linha-h"); botoes.style.marginTop = 24;
            var cont = Botao(r == c.naoVisto ? "Continuar" : "Voltar ao painel", Continuar, true, true);
            cont.style.marginRight = 12; botoes.Add(cont);
            botoes.Add(Botao("Ver finanças", () => { Continuar(); Ir(Aba.Financas); }));
            conteudo.Add(botoes);
        }

        void Continuar()
        {
            if (resumoAberto == null || resumoAberto == c.naoVisto) c.naoVisto = null;
            resumoAberto = null;
            Ir(Aba.Painel);
        }

        // ================================================================================================ elenco
        void TelaElenco()
        {
            conteudo.Add(Escalacao());
            var cmp = Comparacao(); if (cmp != null) conteudo.Add(cmp);

            conteudo.Add(L("Todos os jogadores", "h2", "topo-h2"));
            conteudo.Add(Abas(FILTROS_ELENCO, filtroElenco, i => { filtroElenco = i; menuDe = null; Mostrar(); }));
            var todos = c.elenco.ToList();
            IEnumerable<Jogador> lista = filtroElenco == 1 ? todos.Where(c.Titular) : filtroElenco == 2 ? todos.Where(j => !c.Titular(j)) : todos;
            var (tabela, linhas) = Tabela(lista, "Status", null,
                j => { bool tit = c.Titular(j); return Selo(tit ? "TITULAR" : "RESERVA", tit ? "laranja" : null); },
                MenuJogador, null);
            int idx = menuDe == null ? -1 : linhas.IndexOf(menuDe);
            if (idx >= 0) tabela.Add(Dropdown(menuDe, idx));
            conteudo.Add(tabela);
            conteudo.Add(L("Clique no nome de uma coluna para ordenar. UTIL. = utilitária, FRIO = sangue-frio. Os atributos são a leitura do que a IA do bot usa na partida.", "legenda"));
        }

        /// <summary>Os 5 titulares e o banco como cartões: clicar em um jogador e depois em outro troca os dois de lugar, e o
        /// efeito na força e na chance de vitória aparece ANTES de clicar no segundo.</summary>
        VisualElement Escalacao()
        {
            var card = E("escala");

            var topo = E("escala-topo");
            topo.Add(L("Escalação titular", "f20", "negrito"));
            var res = E("escala-res");
            res.Add(L("Força " + c.Forca, "f17", "negrito"));
            res.Add(L("Chance de vitória " + Pct(c.ChanceVitoria()) + " contra " + c.proximo.nome, "f16", "suave"));
            topo.Add(res);
            var chips = E("chips-func");
            var faltando = c.FuncoesFaltando();
            foreach (var f in FUNCOES_TIME)
            {
                var chip = E("chip-f", faltando.Contains(f) ? "falta" : "ok");
                chip.Add(L(Fmt.FuncaoCurta(f)));
                ComDica(chip, faltando.Contains(f) ? "Nenhum titular na função " + Fmt.Funcao(f) + "." : "Há um titular na função " + Fmt.Funcao(f) + ".");
                chips.Add(chip);
            }
            topo.Add(chips);
            card.Add(topo);

            var fila = E("fila-slots");
            foreach (var j in c.Titulares) fila.Add(Slot(j));
            card.Add(fila);

            int vagasBanco = Provisorio.ELENCO_MAX - Provisorio.TITULARES;
            card.Add(L("Reservas (" + Math.Max(0, c.elenco.Count - Provisorio.TITULARES) + "/" + vagasBanco + ")", "f16", "negrito", "suave", "banco-tit"));
            var banco = E("fila-slots");
            var reservas = c.elenco.Skip(Provisorio.TITULARES).ToList();
            for (int k = 0; k < 5; k++)
            {
                if (k < reservas.Count) banco.Add(Slot(reservas[k]));
                else if (k < vagasBanco) { var v = E("slot", "vazio"); v.Add(L("Vaga livre", "f15", "apagado")); v.style.justifyContent = Justify.Center; v.style.minHeight = 150; banco.Add(v); }
                else banco.Add(E("slot", "vazio", "oculto"));
            }
            card.Add(banco);

            dicaTroca = L("", "dica-troca");
            card.Add(dicaTroca);
            AtualizarDicaTroca(null);
            return card;
        }

        VisualElement Slot(Jogador j)
        {
            var s = E("slot"); if (selecionado == j) s.AddToClassList("sel");
            var topo = E("slot-topo");
            var fn = E("slot-fn"); fn.Add(L(Fmt.FuncaoCurta(j.funcao))); topo.Add(fn);
            topo.Add(L(j.Overall.ToString(), "slot-ovr", ClasseNota(j.Overall)));
            s.Add(topo);
            s.Add(Retrato(j.nome, "retrato-p"));
            s.Add(L(j.nome, "slot-nome"));
            s.Add(L(j.idade + " anos", "f14", "suave"));
            Clicavel(s, () => EscolherParaTroca(j));
            s.RegisterCallback<PointerEnterEvent>(_ => AtualizarDicaTroca(j));
            s.RegisterCallback<PointerLeaveEvent>(_ => AtualizarDicaTroca(null));
            return s;
        }

        void EscolherParaTroca(Jogador j)
        {
            if (selecionado == null || selecionado == j) { selecionado = selecionado == j ? null : j; Mostrar(); return; }
            var a = selecionado; selecionado = null;
            c.Trocar(a, j);
            Salvar(); Mostrar();
            Toast(a.nome + " e " + j.nome + " trocaram de lugar. Força " + c.Forca + ".");
        }

        void AtualizarDicaTroca(Jogador sobre)
        {
            if (dicaTroca == null) return;
            if (selecionado == null) { dicaTroca.text = "Clique em um jogador e depois em outro para trocá-los de lugar; o efeito na força aparece antes de confirmar."; return; }
            if (sobre == null || sobre == selecionado) { dicaTroca.text = selecionado.nome + " escolhido. Clique em outro jogador para trocar de lugar (ou no mesmo para cancelar)."; return; }
            var lista = c.elenco.ToList();
            int i = lista.IndexOf(selecionado), k = lista.IndexOf(sobre);
            lista[i] = sobre; lista[k] = selecionado;
            int depois = Carreira.ForcaDe(lista.Take(Provisorio.TITULARES));
            int dif = depois - c.Forca;
            dicaTroca.text = "Trocar " + selecionado.nome + " e " + sobre.nome + ": força " + c.Forca + " → " + depois + " (" + (dif > 0 ? "+" : "") + dif
                + "), chance de vitória " + Pct(c.ChanceVitoria()) + " → " + Pct(c.ChanceVitoria(depois)) + ".";
        }

        // ------------------------------------------------------------------ comparação
        void AlternarComparar(Jogador j)
        {
            if (comparar.Contains(j)) comparar.Remove(j);
            else { if (comparar.Count >= 2) comparar.RemoveAt(0); comparar.Add(j); }
            menuDe = null; Mostrar();
        }

        VisualElement Comparacao()
        {
            if (comparar.Count == 0) return null;
            var card = E("compara");
            var topo = E("compara-topo");
            topo.Add(L(comparar.Count == 2 ? "Comparação" : "Comparando " + comparar[0].nome + ": escolha outro jogador no menu ⋮ (elenco) ou no botão de comparar (mercado).", "f16", "negrito"));
            topo.Add(Botao("Limpar", () => { comparar.Clear(); Mostrar(); }));
            card.Add(topo);
            if (comparar.Count < 2) return card;

            var a = comparar[0]; var b = comparar[1];
            // ordem visual: nome A | avatar A | (meio) | avatar B | nome B
            var cab = E("cmp-cab");
            var la = E("cmp-lado", "dir"); la.Add(L(a.nome, "f20", "negrito")); la.Add(Avatar(a.nome));
            var lb = E("cmp-lado"); lb.Add(Avatar(b.nome)); lb.Add(L(b.nome, "f20", "negrito"));
            cab.Add(la); cab.Add(E("cmp-meio")); cab.Add(lb);
            card.Add(cab);

            void Linha(string rot, int va, int vb, bool menorMelhor, string ta, string tb)
            {
                var l = E("cmp-linha");
                bool aGanha = menorMelhor ? va < vb : va > vb, bGanha = menorMelhor ? vb < va : vb > va;
                l.Add(L(ta, "cmp-v", "a", aGanha ? "verde" : "t"));
                l.Add(L(rot, "cmp-rot"));
                l.Add(L(tb, "cmp-v", "b", bGanha ? "verde" : "t"));
                card.Add(l);
            }
            Linha("Função", 0, 0, false, Fmt.Funcao(a.funcao), Fmt.Funcao(b.funcao));
            Linha("Geral", a.Overall, b.Overall, false, a.Overall.ToString(), b.Overall.ToString());
            Linha("Mira", a.Mira, b.Mira, false, a.Mira.ToString(), b.Mira.ToString());
            Linha("Reflexo", a.Reflexo, b.Reflexo, false, a.Reflexo.ToString(), b.Reflexo.ToString());
            Linha("Spray", a.Spray, b.Spray, false, a.Spray.ToString(), b.Spray.ToString());
            Linha("Utilitária", a.Utilitaria, b.Utilitaria, false, a.Utilitaria.ToString(), b.Utilitaria.ToString());
            Linha("Sangue-frio", a.SangueFrio, b.SangueFrio, false, a.SangueFrio.ToString(), b.SangueFrio.ToString());
            Linha("Salário", a.Salario, b.Salario, true, Fmt.Dinheiro(a.Salario), Fmt.Dinheiro(b.Salario));
            Linha("Idade", a.idade, b.idade, true, a.idade + " anos", b.idade + " anos");
            return card;
        }

        // ------------------------------------------------------------------ tabela
        /// <summary>Cabeçalho de coluna: clicar ordena (maior primeiro, depois menor primeiro, depois volta à ordem original).</summary>
        VisualElement CelulaCab(string classe, string rotulo, int col, string dica)
        {
            var cel = E("c", classe);
            if (col < 0) { cel.Add(L(rotulo)); return cel; }
            var cab = E("cab-ordem"); bool ativa = colOrdem == col; if (ativa) cab.AddToClassList("ativo");
            cab.Add(L(rotulo));
            if (ativa) cab.Add(new Icone(ordemAsc ? "up" : "down", 14));
            Clicavel(cab, () =>
            {
                if (colOrdem == col) { if (!ordemAsc) ordemAsc = true; else colOrdem = -1; } else { colOrdem = col; ordemAsc = false; }
                menuDe = null; Mostrar();
            });
            if (dica != null) ComDica(cab, dica);
            cel.Add(cab);
            return cel;
        }

        /// <summary>Tabela de jogadores como a referência: sem cartão de fundo, só linhas finas. Devolve a tabela e a ordem final das linhas.</summary>
        (VisualElement, List<Jogador>) Tabela(IEnumerable<Jogador> jogadores, string tituloExtra, Func<Jogador, int> chaveExtra,
            Func<Jogador, VisualElement> extra, Func<Jogador, VisualElement> acoes, Action<Jogador> aoPassar)
        {
            var linhas = jogadores.ToList();
            if (colOrdem >= 0)
            {
                Func<Jogador, int> chave = colOrdem < COLS.Length ? COLS[colOrdem].chave : chaveExtra;
                if (chave != null) linhas = (ordemAsc ? linhas.OrderBy(chave) : linhas.OrderByDescending(chave)).ToList();
            }

            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")));
            h.Add(CelulaCab("c-g", COLS[0].rot, 0, COLS[0].dica));
            for (int i = 1; i <= 5; i++) h.Add(CelulaCab("c-a", COLS[i].rot, i, COLS[i].dica));
            h.Add(CelulaCab("c-s", COLS[6].rot, 6, COLS[6].dica));
            h.Add(CelulaCab("c-st", tituloExtra, chaveExtra != null ? COLS.Length : -1, chaveExtra != null ? "Quanto custa contratar." : null));
            h.Add(Celula("c-k"));
            tab.Add(h);
            foreach (var j in linhas)
            {
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
                if (aoPassar != null)
                {
                    r.RegisterCallback<PointerEnterEvent>(_ => aoPassar(j));
                    r.RegisterCallback<PointerLeaveEvent>(_ => aoPassar(null));
                }
                tab.Add(r);
            }
            if (linhas.Count == 0) tab.Add(L("Nenhum jogador nesse filtro.", "f15", "suave"));
            return (tab, linhas);
        }

        /// <summary>Botão "⋮" que abre o menu com as ações do jogador (como o "Copy link / Share" da referência).</summary>
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
            var dd = E("dropdown");
            dd.style.top = 44 + linha * 68 + 4;   // altura do cabeçalho + linhas acima (ver .tr no USS)
            bool tit = c.Titular(j);
            bool temBanco = c.elenco.Count > Provisorio.TITULARES;
            if (tit) dd.Add(ItemMenu("down", "Tornar reserva", temBanco, false, () => { menuDe = null; c.MandarParaReserva(j); Salvar(); Mostrar(); Toast(j.nome + " agora é reserva. Força " + c.Forca + "."); }));
            else dd.Add(ItemMenu("up", "Escalar como titular", true, false, () => { menuDe = null; c.TornarTitular(j); Salvar(); Mostrar(); Toast(j.nome + " agora é titular. Força " + c.Forca + "."); }));
            dd.Add(ItemMenu("swap", comparar.Contains(j) ? "Tirar da comparação" : "Comparar com outro jogador", true, false, () => AlternarComparar(j)));
            dd.Add(ItemMenu("tag", "Vender por + " + Fmt.Dinheiro(j.Venda), c.elenco.Count > Provisorio.TITULARES, true, () => Vender(j)));
            return dd;
        }

        void Vender(Jogador j)
        {
            var erro = c.Vender(j);
            comparar.Remove(j); if (selecionado == j) selecionado = null;
            Resposta(erro, j.nome + " foi vendido por + " + Fmt.Dinheiro(j.Venda) + ".", "Desfazer", () =>
            {
                var e2 = c.DesfazerVenda();
                Salvar(); Mostrar();
                Toast(e2 ?? j.nome + " voltou para o elenco.", e2 != null);
            });
        }

        static VisualElement ItemMenu(string icone, string texto, bool ativo, bool perigo, Action acao)
        {
            var it = E("dd-item"); if (perigo) it.AddToClassList("perigo"); if (!ativo) it.AddToClassList("off");
            it.Add(new Icone(icone, 19)); it.Add(L(texto));
            if (ativo) Clicavel(it, acao);
            return it;
        }

        // ================================================================================================ mercado
        void TelaMercado()
        {
            // prévia: ao passar o mouse num jogador, mostra o que muda no time se ele for contratado
            previaCaixa = E("previa"); previaTxt = L("", "quebra"); previaCaixa.Add(previaTxt);
            conteudo.Add(previaCaixa);
            AtualizarPrevia(null);

            var cmp = Comparacao(); if (cmp != null) conteudo.Add(cmp);

            conteudo.Add(Abas(FUNCOES_NOMES, filtroFuncao, i => { filtroFuncao = i; Mostrar(); }));

            bool cheio = c.elenco.Count >= Provisorio.ELENCO_MAX;
            var lista = c.mercado.Where(j => filtroFuncao == 0 || j.funcao == FUNCOES_ROLE[filtroFuncao]);
            var (tabela, _) = Tabela(lista, "Preço", j => j.Valor,
                j => L(Fmt.Dinheiro(j.Valor), "f16", "negrito", c.dinheiro >= j.Valor ? "t" : "vermelho"),
                j =>
                {
                    var linha = E("linha-h");
                    var cmpBtn = E("btn", "btn-ic"); if (comparar.Contains(j)) cmpBtn.AddToClassList("on");
                    cmpBtn.Add(new Icone("swap", 18));
                    Clicavel(cmpBtn, () => AlternarComparar(j));
                    ComDica(cmpBtn, comparar.Contains(j) ? "Tirar da comparação" : "Comparar com outro jogador (do mercado ou do seu elenco)");
                    linha.Add(cmpBtn);
                    linha.Add(Botao("Contratar", () => Contratar(j), !cheio && c.dinheiro >= j.Valor, true));
                    return linha;
                }, AtualizarPrevia);
            conteudo.Add(tabela);
            if (cheio) conteudo.Add(L("Elenco cheio: venda alguém para contratar.", "f15", "laranja", "legenda"));
        }

        void Contratar(Jogador j)
        {
            var erro = c.Contratar(j);
            comparar.Remove(j);
            var sai = erro == null ? c.QuemSaiPara(j) : null;
            Resposta(erro, j.nome + " entrou no elenco (reserva).", sai != null ? "Escalar agora" : null, () =>
            {
                c.TornarTitular(j); Salvar(); Mostrar();
                Toast(j.nome + " agora é titular. Força " + c.Forca + ", chance de vitória " + Pct(c.ChanceVitoria()) + ".");
            });
        }

        void AtualizarPrevia(Jogador j)
        {
            if (previaTxt == null) return;
            bool bom = false;
            if (j == null)
                previaTxt.text = "Passe o mouse sobre um jogador para ver o que muda no seu time se você contratá-lo (força, chance de vitória, salários e saldo).";
            else
            {
                var sai = c.QuemSaiPara(j);
                string t = j.nome + " (" + Fmt.Funcao(j.funcao) + ", geral " + j.Overall + ") custa " + Fmt.Dinheiro(j.Valor) + ". Saldo depois: " + Fmt.Dinheiro(c.dinheiro - j.Valor)
                    + "; salários por partida " + Fmt.Dinheiro(c.SalariosPorPartida) + " → " + Fmt.Dinheiro(c.SalariosPorPartida + j.Salario) + ".";
                if (sai != null)
                {
                    int f = c.ForcaCom(j, sai);
                    t += " Se ele for escalado no lugar de " + sai.nome + " (" + sai.Overall + "), a força vai de " + c.Forca + " para " + f + " e a chance de vitória de " + Pct(c.ChanceVitoria()) + " para " + Pct(c.ChanceVitoria(f)) + ".";
                    bom = true;
                }
                else t += " Não melhora a escalação: entraria só como reserva.";
                if (c.elenco.Count >= Provisorio.ELENCO_MAX) t += " Elenco cheio: venda alguém antes.";
                else if (c.dinheiro < j.Valor) t += " Dinheiro insuficiente.";
                previaTxt.text = t;
            }
            previaCaixa?.EnableInClassList("bom", bom);
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
                var depois = L("Saldo depois: " + Fmt.Dinheiro(c.dinheiro - c.CustoProximoNivelGH) + " (" + (int)Math.Max(0, (c.dinheiro - c.CustoProximoNivelGH) / (double)Math.Max(1, c.SalariosPorPartida)) + " partidas de salários).", "f15", "suave");
                depois.style.marginTop = 12; cartao.Add(depois);
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
            var folego = L("O saldo paga cerca de " + (int)c.PartidasDeFolego + " partida(s) de salários, sem contar prêmios.", "f15", c.PartidasDeFolego < 3 ? "laranja" : "suave");
            folego.style.marginTop = 6; conteudo.Add(folego);

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
        /// sem abrir a janela (ver CenaManager.CapturarBatch). Não salva a carreira em disco.</summary>
        IEnumerator Capturar(string pasta, RenderTexture rt)
        {
            Directory.CreateDirectory(pasta);
            var passos = new List<(string nome, Action preparar, Action depois)>
            {
                ("prematch", () => aba = Aba.PreMatch, null),
                ("painel", () => { aba = Aba.Painel; filtroPainel = 1; }, null),
                ("elenco", () => { aba = Aba.Elenco; selecionado = c.elenco[0]; comparar.Clear(); comparar.Add(c.elenco[1]); comparar.Add(c.elenco[5]); }, () => AtualizarDicaTroca(c.elenco[5])),
                ("elenco-menu", () => { aba = Aba.Elenco; selecionado = null; comparar.Clear(); menuDe = c.elenco[5]; colOrdem = 0; ordemAsc = false; }, null),
                ("mercado", () => { aba = Aba.Mercado; colOrdem = -1; menuDe = null; comparar.Clear(); }, () => AtualizarPrevia(c.mercado[1])),
                ("financas", () => aba = Aba.Financas, null),
                ("gaminghouse", () => aba = Aba.GamingHouse, null),
                ("pospartida", () => { c.Pular(); aba = Aba.Painel; }, null),
                ("painel-depois", () => { c.naoVisto = null; aba = Aba.Painel; }, () => Toast(c.elenco[5].nome + " foi vendido por + " + Fmt.Dinheiro(c.elenco[5].Venda) + ".", false, "Desfazer", () => { })),
            };
            foreach (var (nome, preparar, depois) in passos)
            {
                menuDe = null; selecionado = null;
                preparar();
                Mostrar();
                depois?.Invoke();
                for (int i = 0; i < 14; i++) yield return null;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                var antes = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(pasta, nome + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            if (retratoRT != null)   // a própria imagem do retrato, para conferir o render 3D
            {
                var t2 = new Texture2D(retratoRT.width, retratoRT.height, TextureFormat.RGBA32, false);
                var ant = RenderTexture.active; RenderTexture.active = retratoRT;
                t2.ReadPixels(new Rect(0, 0, retratoRT.width, retratoRT.height), 0, 0); t2.Apply();
                RenderTexture.active = ant;
                File.WriteAllBytes(Path.Combine(pasta, "retrato.png"), t2.EncodeToPNG());
                Destroy(t2);
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
