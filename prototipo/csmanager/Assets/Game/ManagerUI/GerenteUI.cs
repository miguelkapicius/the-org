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
    /// A interface do manager: onde o jogador cuida do clube entre as partidas (escalação, treino, scouts, mercado, finanças,
    /// Gaming House), acompanha o calendário de campeonatos, prepara cada série (veto de mapas e estratégia), escolhe assistir ou
    /// pular e vê como foi (pós-partida).
    ///
    /// UI Toolkit: a estrutura é montada aqui em código e o visual vem do USS (Resources/ManagerUI/Manager.uss), portado do mockup em
    /// prototipo/mockup-ui. As regras vêm de CSM.Manager.Carreira; aqui só desenho e clique. Todo número que a tela mostra
    /// (salário, preço, premiação, chance de vitória) é provisório. A cena é criada pelo menu CS Manager > Manager.
    /// A classe é dividida em arquivos por tela: GerenteUI.Calendario.cs, .Treino.cs, .Scouts.cs, .Serie.cs e .Financas.cs.
    /// </summary>
    public partial class GerenteUI : MonoBehaviour
    {
        public int seed = 7;
        public const string CENA_PARTIDA = "Partida3D";

        enum Aba { Painel, Calendario, Elenco, Treino, Mercado, Financas, GamingHouse, Scouts, Serie, PreMatch, PosPartida }
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
            ("Salário", "Pago a cada semana.", j => j.Salario),
        };

        static string NomeFormato(Formato f) => f switch
        {
            Formato.Liga => "Liga (pontos corridos)", Formato.GruposMataMata => "Grupos + mata-mata", Formato.GruposGslMataMata => "Grupos GSL + mata-mata",
            Formato.SuicoMataMata => "Suíço + mata-mata", _ => "Mata-mata",
        };

        static string Quando(int semana, int atual)
        {
            int k = semana - atual;
            return k <= 0 ? "esta semana" : k == 1 ? "na próxima semana" : "em " + k + " semanas";
        }

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
                case KeyCode.Alpha2: Ir(Aba.Calendario); break;
                case KeyCode.Alpha3: Ir(Aba.Elenco); break;
                case KeyCode.Alpha4: Ir(Aba.Treino); break;
                case KeyCode.Alpha5: Ir(Aba.Mercado); break;
                case KeyCode.Alpha6: Ir(Aba.Financas); break;
                case KeyCode.Alpha7: Ir(Aba.GamingHouse); break;
                case KeyCode.Alpha8: Ir(Aba.Scouts); break;
                case KeyCode.Escape:
                    if (aba == Aba.PreMatch) Ir(Aba.Serie);
                    else if (aba == Aba.Serie || aba == Aba.PosPartida) Ir(Aba.Painel);
                    else if (aba == Aba.Calendario && campAberto != null) { campAberto = null; Mostrar(); }
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
            t.schedule.Execute(() => { if (t.parent != null) t.RemoveFromHierarchy(); }).StartingIn(acaoRotulo != null ? 7000 : 5200);
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
            if (c.naoVisto != null && aba == Aba.Painel) aba = Aba.PosPartida;   // o resultado do mapa que acabou vem antes do painel
            mudou = aba != abaAnterior; abaAnterior = aba;
            janela.Clear(); previaTxt = null; previaCaixa = null; dicaTroca = null; rolagem = null;
            if (aba == Aba.PreMatch) { if (c.serie == null) { aba = Aba.Painel; abaAnterior = aba; } else { TelaPreMatch(); return; } }

            janela.Add(MontarMenu());
            var principal = E("tela"); janela.Add(principal);
            if (mudou) { principal.AddToClassList("entra"); principal.schedule.Execute(() => principal.AddToClassList("visivel")); }

            string titulo, sub;
            switch (aba)
            {
                case Aba.Calendario: titulo = "Calendário"; sub = campAberto != null ? campAberto.nome : "Temporada " + c.Temporada + " · campeonatos do circuito " + Mundo.NomeCircuito(c.CircuitoClube); break;
                case Aba.Elenco: titulo = "Elenco"; sub = "Escale os titulares, compare jogadores e veja o efeito de cada troca antes de fazê-la."; break;
                case Aba.Treino: titulo = "Treino"; sub = "Treinar evolui os jogadores e dá preparo para a partida, mas cansa. Ache o equilíbrio."; break;
                case Aba.Mercado: titulo = "Mercado"; sub = "Jogadores livres para contratar. A lista se renova toda semana."; break;
                case Aba.GamingHouse: titulo = "Gaming House"; sub = "Cada nível libera vantagens: treino, scouts, recuperação, patrocínio (decisão 0016)."; break;
                case Aba.Financas: titulo = "Finanças"; sub = "Salários e manutenção saem toda semana; prêmios e patrocínio entram."; break;
                case Aba.Scouts: titulo = "Scouts"; sub = "Descubra como o adversário joga e quem vale a pena contratar."; break;
                case Aba.Serie: titulo = "Preparar série"; sub = c.serie != null ? c.serie.camp.nome + " · " + Fmt.Rotulo(c.serie.camp.nome, c.serie.conf.rotulo) : ""; break;
                case Aba.PosPartida: { var r = resumoAberto ?? c.naoVisto; titulo = "Pós-partida"; sub = r != null ? r.campeonato + " · " + r.mapa + " · contra " + r.adversario : ""; break; }
                default: titulo = "Painel do clube"; sub = "Semana " + c.Semana + " · temporada " + c.Temporada + " · " + c.clube; break;
            }
            principal.Add(Cabecalho(titulo, sub));

            rolagem = new ScrollView(ScrollViewMode.Vertical); rolagem.AddToClassList("grow");
            rolagem.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            principal.Add(rolagem);
            conteudo = E("corpo"); rolagem.Add(conteudo);
            switch (aba)
            {
                case Aba.Calendario: TelaCalendario(); break;
                case Aba.Elenco: TelaElenco(); break;
                case Aba.Treino: TelaTreino(); break;
                case Aba.Mercado: TelaMercado(); break;
                case Aba.GamingHouse: TelaGamingHouse(); break;
                case Aba.Financas: TelaFinancas(); break;
                case Aba.Scouts: TelaScouts(); break;
                case Aba.Serie: TelaSerie(); break;
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
            nome.Add(L(Mundo.NomeCircuito(c.CircuitoClube) + " · ranking " + c.PosicaoRanking, "f13", "suave"));
            h.Add(nome);
            h.Add(new Icone("chev", 16));
            m.Add(h);

            var nav = E("menu-nav"); m.Add(nav);
            int nAvisos = c.Avisos().Count;
            Item(nav, "Painel", "grid", Aba.Painel, nAvisos > 0 ? nAvisos.ToString() : null);
            Item(nav, "Calendário", "calendar", Aba.Calendario, null);
            Item(nav, "Elenco", "users", Aba.Elenco, c.elenco.Count.ToString());
            Item(nav, "Treino", "bolt", Aba.Treino, null);
            Item(nav, "Mercado", "swap", Aba.Mercado, c.mercado.Count.ToString());
            nav.Add(E("sep"));
            Item(nav, "Finanças", "dollar", Aba.Financas, null);
            Item(nav, "Gaming House", "house", Aba.GamingHouse, c.nivelGH.ToString());
            Item(nav, "Scouts", "eye", Aba.Scouts, c.scouts.Count > 0 ? c.scouts.Count.ToString() : null);

            // o próximo passo do jogo: jogar a série marcada ou avançar o tempo
            var prox = E("proxima");
            var p = c.Proximo();
            if (c.TemJogoAgora)
            {
                prox.Add(L("SÉRIE ESTA SEMANA", "f12", "negrito", "apagado"));
                var vs = E("proxima-vs");
                var vsTxt = L("vs", "f16", "apagado"); vsTxt.style.marginLeft = 8; vsTxt.style.marginRight = 8;
                vs.Add(L(c.clube, "f18", "negrito")); vs.Add(vsTxt); vs.Add(L(p.adv.nome, "f18", "negrito"));
                prox.Add(vs);
                prox.Add(L(Fmt.Serie(p.conf.md) + " · chance de vitória " + Pct(c.ChanceVitoria()), "f14", "suave", "info-meta"));
                prox.Add(Botao("Preparar série", () => Ir(Aba.Serie), !c.Falido, true, "play", "cheio"));
            }
            else
            {
                prox.Add(L("PRÓXIMO COMPROMISSO", "f12", "negrito", "apagado"));
                var vs = E("proxima-vs"); vs.Add(L(p.camp != null ? p.camp.nome : "Sem campeonato à vista", "f16", "negrito", "quebra")); prox.Add(vs);
                prox.Add(L(p.camp != null ? "Semana " + p.semana + " (" + Quando(p.semana, c.Semana) + ")" : "Fim da temporada", "f14", "suave", "info-meta"));
                prox.Add(Botao("Avançar semana", AvancarSemanaUI, c.PodeAvancar, true, "skip", "cheio"));
            }
            m.Add(prox);

            // recomeçar a carreira pede confirmação (apaga o salvamento)
            var nova = E("aviso-btn"); nova.style.alignSelf = Align.FlexStart; nova.style.marginBottom = 10;
            nova.Add(L(confirmandoNova ? "Apagar e recomeçar?" : "Nova carreira", confirmandoNova ? "vermelho" : "suave"));
            Clicavel(nova, () =>
            {
                if (!confirmandoNova) { confirmandoNova = true; Mostrar(); return; }
                confirmandoNova = false; Carreira.Atual = Carreira.Nova(seed + c.Semana + c.Temporada * 53 + 101); c = Carreira.Atual;
                comparar.Clear(); selecionado = null; menuDe = null; resumoAberto = null; campAberto = null; Salvar();
                aba = Aba.Painel; Mostrar(); Toast("Carreira nova começada.");
            });
            m.Add(nova);
            m.Add(L("Salva sozinho. Teclas 1 a 8 trocam de tela. Valores provisórios (protótipo).", "f13", "apagado", "quebra", "rodape"));
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

            var sem = E("chip-semana"); sem.Add(new Icone("calendar", 18)); sem.Add(L("Semana " + c.Semana + " · T" + c.Temporada));
            ComDica(sem, "O tempo corre em semanas (52 por temporada). Temporada " + c.Temporada + ", semana " + c.Semana + ".");
            h.Add(sem);

            var u = E("usuario");
            u.Add(Brasao(c.clube, "laranja", true));
            var tx = E("usuario-txt");
            tx.Add(L(Fmt.Dinheiro(c.dinheiro), "f17", "negrito", c.dinheiro < 0 ? "vermelho" : "t"));
            tx.Add(L(Fmt.Dinheiro(c.DespesaFixaPorSemana) + " de despesa por semana", "f13", "suave"));
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

        void Ir(Aba a)
        {
            if (a != Aba.Calendario) campAberto = null;
            aba = a; menuDe = null; selecionado = null; colOrdem = -1; confirmandoNova = false;
            if (a != Aba.PosPartida) resumoAberto = null;
            Mostrar();
        }

        static string Pct(double p) => Math.Round(p * 100) + "%";

        /// <summary>Passa uma semana (treino, finanças, campeonatos do mundo) e mostra o que aconteceu.</summary>
        void AvancarSemanaUI()
        {
            var r = c.AvancarSemana();
            if (r == null) { Toast(c.Falido ? "O clube faliu." : "Há uma série marcada para esta semana: prepare, jogue ou pule antes de avançar.", true); return; }
            Salvar();
            Mostrar();
            string txt = r.virouTemporada ? "Nova temporada! " : "";
            txt += r.linhas.Count > 0 ? string.Join("  ·  ", r.linhas.Take(2)) : "Semana " + r.semana + " concluída.";
            Toast(txt, c.dinheiro < 0);
        }

        // ================================================================================================ painel
        void TelaPainel()
        {
            if (c.Falido)
            {
                var fc = E("cartao");
                fc.Add(L("Falência", "cartao-tit"));
                fc.Add(L("O saldo ficou negativo por " + Provisorio.SEMANAS_ATE_FALENCIA + " semanas e o clube faliu.", "f22", "negrito", "vermelho"));
                var p = L("É o fim desta carreira (decisão 0015). Você pode começar outra.", "f15", "suave", "quebra");
                p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.Semana + 7); c = Carreira.Atual; Salvar(); aba = Aba.Painel; Mostrar(); }, true, true));
                conteudo.Add(fc);
                return;
            }

            conteudo.Add(Hub());

            conteudo.Add(L("O que merece atenção", "h2", "topo-h2"));
            var avisos = c.Avisos();
            var caixa = E("avisos");
            if (avisos.Count == 0) { var ok = E("tudo-ok"); ok.Add(L("Tudo em ordem: nada pede a sua atenção agora.", "f16", "verde")); caixa.Add(ok); }
            bool primeiroAviso = true;
            foreach (var a in avisos.Take(4))
            {
                var l = E("aviso"); if (primeiroAviso) l.AddToClassList("primeiro"); primeiroAviso = false;
                l.Add(E("aviso-ponto", a.nivel == Aviso.Nivel.Critico ? "critico" : a.nivel == Aviso.Nivel.Atencao ? "atencao" : "neutro"));
                l.Add(L(a.texto, "aviso-txt"));
                var b = E("aviso-btn"); b.Add(L(a.acao));
                var destino = a.destino;
                Clicavel(b, () => Ir(DestinoAviso(destino)));
                l.Add(b);
                caixa.Add(l);
            }
            conteudo.Add(caixa);



            conteudo.Add(L("Notícias do clube", "h2"));
            var diario = E("diario");
            if (c.diario.Count == 0) diario.Add(L("Nada por enquanto.", "f15", "suave"));
            foreach (var d in c.diario.Take(5)) { var l = E("diario-linha"); l.Add(L(d, "suave")); diario.Add(l); }
            conteudo.Add(diario);

            conteudo.Add(L("Elenco", "h2"));
            conteudo.Add(Abas(FILTROS_ELENCO, filtroPainel, i => { filtroPainel = i; Mostrar(); }));
            var todos = c.elenco.ToList();
            Grade(filtroPainel == 1 ? todos.Where(c.Titular) : filtroPainel == 2 ? todos.Where(j => !c.Titular(j)) : todos);

            conteudo.Add(L("Últimas partidas", "h2"));
            if (c.historico.Count == 0) conteudo.Add(L("Nenhuma partida jogada ainda.", "f15", "suave"));
            foreach (var r in c.historico.Take(5))
            {
                var l = E("lista-l");
                var rod = L("Semana " + r.semana, "f15", "suave"); rod.style.width = 110; l.Add(rod);
                var selo = Selo(r.venceu ? "VITÓRIA" : "DERROTA", r.venceu ? "verde" : "vermelho"); selo.style.width = 96; l.Add(selo);
                var pl = L(r.Placar, "f18", "negrito"); pl.style.width = 100; pl.style.marginLeft = 18; l.Add(pl);
                var gr = L(r.mapa + " · contra " + r.adversario + (r.assistida ? "" : " · pulada") + (r.serieEncerrada ? " · série " + r.PlacarSerie : ""), "f16", "suave"); gr.style.flexGrow = 1; l.Add(gr);
                l.Add(L("Ver resumo", "f14", "suave"));
                var rr = r; Clicavel(l, () => { resumoAberto = rr; Ir(Aba.PosPartida); });
                conteudo.Add(l);
            }
        }


        // ------------------------------------------------------------------ hub do painel (cartões brancos, como nas referências do jogo de futebol)
        VisualElement Hub()
        {
            var hub = E("hub");
            var p = c.Proximo(); bool agora = c.TemJogoAgora;

            // 1) o compromisso: cartão lima com o botão da ação principal
            var cm = HubCartao(agora ? "SÉRIE CONTRA " + p.adv.nome.ToUpperInvariant() : p.camp != null ? p.camp.nome.ToUpperInvariant() : "FIM DA TEMPORADA",
                agora ? p.camp.nome + " · " + Fmt.Serie(p.conf.md) + " · chance de vitória " + Pct(c.ChanceVitoria()) : p.camp != null ? "Começa " + Quando(p.semana, c.Semana) + " (semana " + p.semana + ")" : "O ranking decide o circuito do ano que vem",
                null, null, true);
            var arte = cm.Q(null, "hub-arte");
            if (agora) { var par = E("linha-h"); par.Add(Brasao(c.clube, "laranja")); var b2 = Brasao(p.adv.nome, "azul"); b2.style.marginLeft = -12; par.Add(b2); foreach (var bb in par.Children()) { bb.style.width = 84; bb.style.height = 84; } arte.Add(par); }
            else arte.Add(new Icone("calendar", 96));
            var btn = E("hub-btn"); btn.Add(L(agora ? "PREPARAR SÉRIE" : "AVANÇAR SEMANA")); btn.Add(new Icone(agora ? "playf" : "skip", 18));
            if (agora ? !c.Falido : c.PodeAvancar) Clicavel(btn, agora ? (Action)(() => Ir(Aba.Serie)) : AvancarSemanaUI);
            cm.Q(null, "hub-corpo").Add(btn);
            hub.Add(cm);

            // 2) treino, calendário e mercado
            var tr = HubCartao("TREINO", c.elenco.Count(j => j.sessoes > 0) + " jogadores treinando: evolua o time sem estourar o cansaço", c.elenco.Count(j => j.sessoes > 0), () => Ir(Aba.Treino), false);
            ArteBoneco(tr.Q(null, "hub-arte")); hub.Add(tr);
            var ca = HubCartao("CALENDÁRIO", c.CampeonatosDoClube.Count(x => !x.terminado) + " campeonatos pela frente · ranking #" + c.PosicaoRanking, c.CampeonatosDoClube.Count(x => !x.terminado), () => Ir(Aba.Calendario), false);
            ca.Q(null, "hub-arte").Add(new Icone("trophy", 96)); hub.Add(ca);
            var me = HubCartao("MERCADO", "Contrate talentos: " + c.mercado.Count + " jogadores livres esta semana", c.mercado.Count, () => Ir(Aba.Mercado), false);
            me.Q(null, "hub-arte").Add(new Icone("swap", 96)); hub.Add(me);
            return hub;
        }

        VisualElement HubCartao(string titulo, string sub, int? selo, Action acao, bool lima)
        {
            var card = E("hub-card"); if (lima) card.AddToClassList("lima");
            card.Add(E("hub-arte"));
            if (selo.HasValue) { var bd = E("hub-selo"); bd.Add(L(selo.Value.ToString())); card.Add(bd); }
            var corpo = E("hub-corpo");
            corpo.Add(L(titulo, "hub-tit")); corpo.Add(L(sub, "hub-sub"));
            card.Add(corpo);
            if (acao != null) Clicavel(card, acao);
            return card;
        }

        /// <summary>O boneco 3D do jogo como ilustração do cartão (a mesma imagem dos retratos).</summary>
        static void ArteBoneco(VisualElement arte)
        {
            if (RetratoBoneco is RenderTexture rt) { arte.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt)); }
            else arte.Add(new Icone("bolt", 96));
        }

        Aba DestinoAviso(string d) => d switch
        {
            "elenco" => Aba.Elenco, "mercado" => Aba.Mercado, "gh" => Aba.GamingHouse, "treino" => Aba.Treino, "scouts" => Aba.Scouts,
            "serie" => Aba.Serie, "calendario" => Aba.Calendario, _ => Aba.Financas,
        };

        VisualElement CartaoCompromisso()
        {
            var p = c.Proximo(); bool agora = c.TemJogoAgora;
            var card = E("compromisso"); if (agora) card.AddToClassList("agora");
            var esq = E();
            if (agora)
            {
                esq.Add(L("SÉRIE MARCADA PARA ESTA SEMANA", "compromisso-tit"));
                esq.Add(L(c.clube + " contra " + p.adv.nome, "compromisso-nome"));
                esq.Add(L(p.camp.nome + " · " + Fmt.Rotulo(p.camp.nome, p.conf.rotulo) + " · " + Fmt.Serie(p.conf.md) + " · chance de vitória " + Pct(c.ChanceVitoria()), "compromisso-sub"));
            }
            else
            {
                esq.Add(L("PRÓXIMO COMPROMISSO", "compromisso-tit"));
                esq.Add(L(p.camp != null ? p.camp.nome : "Sem campeonato à vista", "compromisso-nome"));
                esq.Add(L(p.camp != null ? "Começa na semana " + p.semana + " (" + Quando(p.semana, c.Semana) + ") · " + NomeFormato(p.camp.formato) + " · enquanto isso, treine e cuide do clube" : "A temporada está acabando", "compromisso-sub"));
            }
            card.Add(esq);
            var ac = E("compromisso-acoes");
            if (agora)
            {
                ac.Add(Botao("Pular série", () => PularSerieUI(), !c.Falido));
                ac.Add(Botao("Preparar série", () => Ir(Aba.Serie), !c.Falido, true, "play"));
            }
            else ac.Add(Botao("Avançar semana", AvancarSemanaUI, c.PodeAvancar, true, "skip"));
            card.Add(ac);
            return card;
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

        /// <summary>Selo do traço de personalidade (decisão 0019), com a explicação ao passar o mouse. null = sem traço ou não visível.</summary>
        static VisualElement SeloTraco(Jogador j, bool visivel)
        {
            if (!visivel || j.traco == Traco.Nenhum) return null;
            var s = Selo(Tracos.Nome(j.traco).ToUpperInvariant(), null); ComDica(s, Tracos.Descricao(j.traco)); return s;
        }

        /// <summary>Nome do jogador com o selo do traço ao lado.</summary>
        static VisualElement NomeComTraco(Jogador j, string classe, bool veTraco)
        {
            var l = E(); l.style.flexDirection = FlexDirection.Row; l.style.alignItems = Align.Center;
            l.Add(classe == "f17" ? L(j.nome, classe, "negrito") : L(j.nome, classe));
            var s = SeloTraco(j, veTraco); if (s != null) { s.style.marginLeft = 8; l.Add(s); }
            return l;
        }

        /// <summary>"moral 65", em laranja abaixo de 45 e vermelho abaixo de 30, com a explicação.</summary>
        static VisualElement Moral(Jogador j, string antes)
        {
            int m = (int)j.moral;
            var l = L(antes + "moral " + m, "f14", m < Provisorio.MORAL_AVISO ? "vermelho" : m < 45 ? "laranja" : "suave");
            l.pickingMode = PickingMode.Position;   // o texto ignora o mouse por padrão: sem isso a dica não abre
            ComDica(l, "Moral (0 a 100): sobe com vitórias e jogando de titular, cai com derrotas e no banco. Muda a habilidade na partida de -4,5% (0) a +3% (100); neutra em 60.");
            return l;
        }

        /// <summary>"MVP 3× (1 de série)", com as últimas jogadas da partida dele ao passar o mouse (decisão 0020).</summary>
        static VisualElement Mvps(Jogador j, string antes)
        {
            var l = L(antes + "MVP " + j.mvps + "×" + (j.mvpsSerie > 0 ? " (" + j.mvpsSerie + " de série)" : ""), "f14", j.mvps > 0 ? "verde" : "suave");
            l.pickingMode = PickingMode.Position;
            ComDica(l, "Vezes que foi MVP de mapa" + (j.mvpsSerie > 0 ? " e de série" : "") + " pelo clube." +
                       (j.jogadas.Count > 0 ? "\n\nJogadas da partida:\n• " + string.Join("\n• ", j.jogadas) : "\n\nAinda sem jogada da partida (só conta a partida assistida)."));
            return l;
        }

        VisualElement CartaoJogador(Jogador j)
        {
            var cj = E("card-j");
            var midia = E("midia");
            midia.Add(Retrato(j.nome, "retrato-fill"));
            var ovr = E("ovr"); ovr.Add(L(j.Overall.ToString(), ClasseNota(j.Overall))); midia.Add(ovr);
            cj.Add(midia);
            var info = E("info");
            info.Add(NomeComTraco(j, "info-nome", true));
            info.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave", "info-meta"));
            info.Add(AtributoLinha("Moral", (int)j.moral));
            if (j.mvps > 0 || j.jogadas.Count > 0) info.Add(Mvps(j, ""));
            info.Add(AtributoLinha("Mira", j.Mira));
            info.Add(AtributoLinha("Reflexo", j.Reflexo));
            info.Add(AtributoLinha("Spray", j.Spray));
            var fad = E("attr"); fad.Add(L("Cansaço", "attr-nome")); var bc = BarraCansaco(j.cansaco); bc.style.flexGrow = 1; fad.Add(bc); fad.Add(L(((int)j.cansaco).ToString(), "attr-num")); info.Add(fad);
            cj.Add(info);
            return cj;
        }

        // ================================================================================================ pós-partida
        void TelaPosPartida()
        {
            var r = resumoAberto ?? c.naoVisto;
            if (r == null) { aba = Aba.Painel; Mostrar(); return; }

            var hero = E("hero", r.venceu ? "venceu" : "perdeu");
            var esq = E();
            esq.Add(L(r.venceu ? "VITÓRIA NO MAPA" : "DERROTA NO MAPA", "hero-res", r.venceu ? "verde" : "vermelho"));
            esq.Add(L(c.clube + " contra " + r.adversario, "f20", "negrito"));
            esq.Add(L(r.mapa + " · " + (r.assistida ? "assistida" : "pulada"), "f15", "suave"));
            hero.Add(esq);
            var meio = E("hero-meio"); meio.Add(L(r.meus + "  x  " + r.deles, "hero-placar")); hero.Add(meio);
            var dir = E(); dir.style.alignItems = Align.FlexEnd;
            dir.Add(L("Série " + Fmt.Serie(r.serieMd) + ": " + r.PlacarSerie, "f18", "negrito", r.serieEncerrada ? (r.venceuSerie ? "verde" : "vermelho") : "t"));
            if (r.serieEncerrada) dir.Add(L(r.venceuSerie ? "Você venceu a série" : "Você perdeu a série", "f15", r.venceuSerie ? "verde" : "vermelho"));
            else dir.Add(L("A série continua no próximo mapa", "f15", "suave"));
            dir.Add(L("Força " + r.forcaMinha + " contra " + r.forcaDeles, "f14", "suave"));
            hero.Add(dir);
            conteudo.Add(hero);

            if (r.mapasDaSerie.Count > 1)
            {
                var mp = E("ordem-mapas");
                foreach (var m in r.mapasDaSerie) mp.Add(Selo(m.mapa + " " + m.meus + " x " + m.deles, m.meus > m.deles ? "verde" : "vermelho"));
                conteudo.Add(mp);
            }

            conteudo.Add(L("Campeonato: " + r.campeonato + (string.IsNullOrEmpty(r.rotulo) ? "" : " · " + Fmt.Rotulo(r.campeonato, r.rotulo)), "f16", "suave"));
            if (r.serieEncerrada)
            {
                var fim = c.titulos.FirstOrDefault(t => t.nome == r.campeonato);
                if (fim != null) conteudo.Add(L("Campeonato encerrado: " + Fmt.Ordinal(fim.posicao) + " lugar, prêmio " + Fmt.Dinheiro(fim.premio) + ".", "f17", "negrito", "verde"));
            }

            // melhores momentos (decisão 0020)
            conteudo.Add(L("Jogada da partida", "h2"));
            if (r.jogada != null)
            {
                var jg = E("linha-h"); jg.style.alignItems = Align.Center;
                jg.Add(Selo(r.jogadaNossa ? "DO SEU TIME" : "DO ADVERSÁRIO", r.jogadaNossa ? "verde" : "vermelho"));
                var tj = L(r.jogada, "f18", "negrito"); tj.style.marginLeft = 12; tj.style.whiteSpace = WhiteSpace.Normal; tj.style.flexShrink = 1; jg.Add(tj);
                conteudo.Add(jg);
            }
            else conteudo.Add(L(r.assistida ? "Nenhum abate marcante nesta partida." : "O mapa foi pulado: só a partida assistida tem jogada da partida.", "f15", "suave"));
            if (r.mvpSerie != null) { var mvs = L("MVP da série: " + r.mvpSerie, "f17", "negrito", "verde"); mvs.style.marginTop = 10; conteudo.Add(mvs); }

            conteudo.Add(L("Desempenho dos jogadores", "h2"));
            if (r.estimado) conteudo.Add(L("Números estimados: o mapa foi pulado, então não houve simulação. Assista para ver o desempenho real.", "nota-estimada"));
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
                if (d == melhor) { var s = Selo("MVP", "verde"); s.style.marginLeft = 12; ComDica(s, "MVP do mapa: a melhor nota de desempenho (abates, mortes e dano por round)."); pessoa.Add(s); }
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
            bool eNovo = r == c.naoVisto;
            bool continua = eNovo && c.serie != null && !c.serie.Encerrada && !c.Falido;
            var cont = Botao(continua ? "Próximo mapa" : eNovo ? "Continuar" : "Voltar ao painel", Continuar, true, true, continua ? "play" : null);
            cont.style.marginRight = 12; botoes.Add(cont);
            if (continua) { var pularR = Botao("Pular o resto da série", () => { Continuar0(); PularSerieUI(); }); pularR.style.marginRight = 12; botoes.Add(pularR); }
            botoes.Add(Botao("Ver finanças", () => { Continuar0(); Ir(Aba.Financas); }));
            conteudo.Add(botoes);
        }

        void Continuar0() { if (resumoAberto == null || resumoAberto == c.naoVisto) c.naoVisto = null; resumoAberto = null; }

        void Continuar()
        {
            bool continua = (resumoAberto == null || resumoAberto == c.naoVisto) && c.serie != null && !c.serie.Encerrada && !c.Falido;
            Continuar0();
            if (continua) { Ir(Aba.PreMatch); return; }
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
            var linhasVisiveis = lista.ToList();
            var (tabela, linhas) = Tabela(linhasVisiveis, "Status", null,
                j => { bool tit = c.Titular(j); return Selo(tit ? "TITULAR" : "RESERVA", tit ? "laranja" : null); },
                MenuJogador, null, true);
            int idx = menuDe == null ? -1 : linhas.IndexOf(menuDe);
            if (idx >= 0) tabela.Add(Dropdown(menuDe, idx));
            conteudo.Add(tabela);
            conteudo.Add(L("Clique no nome de uma coluna para ordenar. UTIL. = utilitária, FRIO = sangue-frio, POT. = potencial (o teto de evolução). Os atributos são a leitura do que a IA do bot usa na partida.", "legenda"));
        }

        /// <summary>Os 5 titulares e as reservas como cartões: clicar em um jogador e depois em outro troca os dois de lugar, e o
        /// efeito na força e na chance de vitória aparece ANTES de clicar no segundo.</summary>
        VisualElement Escalacao()
        {
            var card = E("escala");

            var topo = E("escala-topo");
            topo.Add(L("Escalação titular", "f20", "negrito"));
            var res = E("escala-res");
            res.Add(L("Força " + c.Forca, "f17", "negrito"));
            var adv = c.Adversario;
            res.Add(L("Chance de vitória " + Pct(c.ChanceVitoria()) + (adv != null ? " contra " + adv.nome : ""), "f16", "suave"));
            int ent = (int)c.EntrosamentoTime;
            var le = L("Entrosamento " + ent, "f16", ent < 40 ? "laranja" : "suave");
            le.pickingMode = PickingMode.Position;
            ComDica(le, "Média do entrosamento dos titulares (0 a 100). Mais entrosado, mais os bots trocam o colega que morreu, e a força sobe até 2%. Sobe a cada semana jogando junto e com sessões coletivas; um contratado chega com pouco.");
            res.Add(le);
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

            int vagasReservas = c.ElencoMax - Provisorio.TITULARES;
            card.Add(L("Reservas (" + Math.Max(0, c.elenco.Count - Provisorio.TITULARES) + "/" + vagasReservas + ")", "f16", "negrito", "suave", "banco-tit"));
            var banco = E("fila-slots");
            var reservas = c.elenco.Skip(Provisorio.TITULARES).ToList();
            for (int k = 0; k < 5; k++)
            {
                if (k < reservas.Count) banco.Add(Slot(reservas[k]));
                else if (k < vagasReservas) { var v = E("slot", "vazio"); v.Add(L("Vaga livre", "f15", "apagado")); v.style.justifyContent = Justify.Center; v.style.minHeight = 150; banco.Add(v); }
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
            var st = SeloTraco(j, true); if (st != null) { st.style.alignSelf = Align.Center; st.style.marginTop = 2; s.Add(st); }
            s.Add(L(j.idade + " anos", "f14", "suave"));
            var mo = Moral(j, ""); mo.style.alignSelf = Align.Center; s.Add(mo);
            var cs = L("cansaço " + (int)j.cansaco, "f13", j.cansaco >= 60 ? "vermelho" : j.cansaco >= 35 ? "laranja" : "apagado"); cs.style.marginTop = 2; s.Add(cs);
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
            string Pot(Jogador j) => j.potencialRevelado || c.elenco.Contains(j) ? j.potencial.ToString() : "?";
            Linha("Função", 0, 0, false, Fmt.Funcao(a.funcao), Fmt.Funcao(b.funcao));
            Linha("Geral", a.Overall, b.Overall, false, a.Overall.ToString(), b.Overall.ToString());
            Linha("Mira", a.Mira, b.Mira, false, a.Mira.ToString(), b.Mira.ToString());
            Linha("Reflexo", a.Reflexo, b.Reflexo, false, a.Reflexo.ToString(), b.Reflexo.ToString());
            Linha("Spray", a.Spray, b.Spray, false, a.Spray.ToString(), b.Spray.ToString());
            Linha("Utilitária", a.Utilitaria, b.Utilitaria, false, a.Utilitaria.ToString(), b.Utilitaria.ToString());
            Linha("Sangue-frio", a.SangueFrio, b.SangueFrio, false, a.SangueFrio.ToString(), b.SangueFrio.ToString());
            Linha("Potencial", a.potencial, b.potencial, false, Pot(a), Pot(b));
            Linha("Salário (semana)", a.Salario, b.Salario, true, Fmt.Dinheiro(a.Salario), Fmt.Dinheiro(b.Salario));
            Linha("Idade", a.idade, b.idade, true, a.idade + " anos", b.idade + " anos");
            return card;
        }

        // ------------------------------------------------------------------ tabela
        /// <summary>Cabeçalho de coluna: clicar ordena (maior primeiro, depois menor primeiro, depois volta à ordem original).</summary>
        VisualElement CelulaCab(string classe, string rotulo, int col, string dica)
        {
            var cel = E("c", classe);
            if (col < 0) { var l = L(rotulo); cel.Add(l); if (dica != null) ComDica(l, dica); return cel; }
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

        /// <summary>Tabela de jogadores como a referência: sem cartão de fundo, só linhas finas. Devolve a tabela e a ordem final das linhas.
        /// <paramref name="forma"/>: mostra o cansaço (só para quem já está no elenco).</summary>
        (VisualElement, List<Jogador>) Tabela(IEnumerable<Jogador> jogadores, string tituloExtra, Func<Jogador, int> chaveExtra,
            Func<Jogador, VisualElement> extra, Func<Jogador, VisualElement> acoes, Action<Jogador> aoPassar, bool forma)
        {
            var linhas = jogadores.ToList();
            if (colOrdem >= 0)
            {
                Func<Jogador, int> chave = colOrdem < COLS.Length ? COLS[colOrdem].chave : colOrdem == COLS.Length + 1 ? (Func<Jogador, int>)(j => (int)j.cansaco) : chaveExtra;
                if (chave != null) linhas = (ordemAsc ? linhas.OrderBy(chave) : linhas.OrderByDescending(chave)).ToList();
            }

            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")));
            h.Add(CelulaCab("c-g", COLS[0].rot, 0, COLS[0].dica));
            for (int i = 1; i <= 5; i++) h.Add(CelulaCab("c-a", COLS[i].rot, i, COLS[i].dica));
            h.Add(CelulaCab("c-s", COLS[6].rot, 6, COLS[6].dica));
            if (forma) h.Add(CelulaCab("c-f", "Cansaço", COLS.Length + 1, "Desgaste por treino e partidas. Acima de 35 a habilidade na partida começa a cair."));
            h.Add(CelulaCab("c-p", "Pot.", -1, "Potencial: o teto de evolução do jogador. No mercado só aparece depois que um scout estuda o jogador."));
            h.Add(CelulaCab("c-st", tituloExtra, chaveExtra != null ? COLS.Length : -1, chaveExtra != null ? "Quanto custa contratar." : null));
            h.Add(Celula("c-k"));
            tab.Add(h);
            foreach (var j in linhas)
            {
                var r = E("tr");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(j.nome));
                var nomes = E("pessoa-txt");
                bool doElenco = c.elenco.Contains(j);
                nomes.Add(NomeComTraco(j, "f17", j.potencialRevelado || doElenco));
                var meta = E(); meta.style.flexDirection = FlexDirection.Row;
                meta.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave"));
                if (doElenco) meta.Add(Moral(j, " · "));
                if (doElenco && (j.mvps > 0 || j.jogadas.Count > 0)) meta.Add(Mvps(j, " · "));
                nomes.Add(meta);
                pessoa.Add(nomes);
                r.Add(Celula("c-j", pessoa));
                r.Add(Celula("c-g", L(j.Overall.ToString(), "geral", ClasseNota(j.Overall))));
                r.Add(CelulaAtributo(j.Mira)); r.Add(CelulaAtributo(j.Reflexo)); r.Add(CelulaAtributo(j.Spray));
                r.Add(CelulaAtributo(j.Utilitaria)); r.Add(CelulaAtributo(j.SangueFrio));
                r.Add(Celula("c-s", L(Fmt.Dinheiro(j.Salario), "f16")));
                if (forma)
                {
                    var cf = E("c", "c-f"); cf.Add(L(((int)j.cansaco).ToString(), "f16", "negrito", j.cansaco >= 60 ? "vermelho" : j.cansaco >= 35 ? "laranja" : "t")); cf.Add(BarraCansaco(j.cansaco)); r.Add(cf);
                }
                bool vePot = j.potencialRevelado || c.elenco.Contains(j);
                r.Add(Celula("c-p", L(vePot ? j.potencial.ToString() : "?", "f16", vePot ? (j.potencial >= 85 ? "verde" : "t") : "apagado")));
                r.Add(Celula("c-st", extra(j)));
                r.Add(Celula("c-k", acoes != null ? acoes(j) : null));
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
            bool temReserva = c.elenco.Count > Provisorio.TITULARES;
            if (tit) dd.Add(ItemMenu("down", "Tornar reserva", temReserva, false, () => { menuDe = null; c.MandarParaReserva(j); Salvar(); Mostrar(); Toast(j.nome + " agora é reserva. Força " + c.Forca + "."); }));
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

            bool cheio = c.elenco.Count >= c.ElencoMax;
            var lista = c.mercado.Where(j => filtroFuncao == 0 || j.funcao == FUNCOES_ROLE[filtroFuncao]);
            var (tabela, _) = Tabela(lista, "Preço", j => j.Preco,
                j => L(Fmt.Dinheiro(j.Preco), "f16", "negrito", c.dinheiro >= j.Preco ? "t" : "vermelho"),
                j =>
                {
                    var linha = E("linha-h");
                    var cmpBtn = E("btn", "btn-ic"); if (comparar.Contains(j)) cmpBtn.AddToClassList("on");
                    cmpBtn.Add(new Icone("swap", 18));
                    Clicavel(cmpBtn, () => AlternarComparar(j));
                    ComDica(cmpBtn, comparar.Contains(j) ? "Tirar da comparação" : "Comparar com outro jogador (do mercado ou do seu elenco)");
                    linha.Add(cmpBtn);
                    linha.Add(Botao("Contratar", () => Contratar(j), !cheio && c.dinheiro >= j.Preco, true));
                    return linha;
                }, AtualizarPrevia, false);
            conteudo.Add(tabela);
            if (cheio) conteudo.Add(L("Elenco cheio (" + c.ElencoMax + "): venda alguém ou suba a Gaming House para ter mais vagas.", "f15", "laranja", "legenda"));
            if (c.scouts.Count == 0) conteudo.Add(L("O potencial dos jogadores aparece como \"?\" até um scout estudá-los (Gaming House nível 3).", "legenda"));
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
                string t = j.nome + " (" + Fmt.Funcao(j.funcao) + ", geral " + j.Overall + (j.potencialRevelado ? ", potencial " + j.potencial : "") + ") custa " + Fmt.Dinheiro(j.Preco) + ". Saldo depois: " + Fmt.Dinheiro(c.dinheiro - j.Preco)
                    + "; salários por semana " + Fmt.Dinheiro(c.SalariosPorSemana) + " → " + Fmt.Dinheiro(c.SalariosPorSemana + j.Salario) + ".";
                if (sai != null)
                {
                    int f = c.ForcaCom(j, sai);
                    t += " Se ele for escalado no lugar de " + sai.nome + " (" + sai.Overall + "), a força vai de " + c.Forca + " para " + f + " e a chance de vitória de " + Pct(c.ChanceVitoria()) + " para " + Pct(c.ChanceVitoria(f)) + ".";
                    bom = true;
                }
                else t += " Não melhora a escalação: entraria só como reserva.";
                if (c.elenco.Count >= c.ElencoMax) t += " Elenco cheio: venda alguém antes.";
                else if (c.dinheiro < j.Preco) t += " Dinheiro insuficiente.";
                previaTxt.text = t;
            }
            previaCaixa?.EnableInClassList("bom", bom);
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
            // prepara um estado interessante: GH alta, scouts e treino em andamento, algumas semanas jogadas
            c.dinheiro = 1500000; for (int k = 0; k < 3; k++) c.SubirGamingHouse();
            c.DefinirScout(0, 2, c.Proximo().adv != null ? c.Proximo().adv.id : 5); c.DefinirScout(1, 1);
            foreach (var j in c.elenco) c.DefinirTreino(j, (int)Atrib.Mira + (j.nome.Length % 5), 2 + j.nome.Length % 3);
            c.DefinirColetivas(2); c.DefinirEstudoMapa(0);
            int guarda = 0;
            while (!c.TemJogoAgora && guarda++ < 40) c.AvancarSemana();
            c.PegarEmprestimo(80000);

            var passos = new List<(string nome, Action preparar, Action depois)>
            {
                ("painel", () => { aba = Aba.Painel; filtroPainel = 1; }, null),
                ("calendario", () => aba = Aba.Calendario, null),
                ("calendario-detalhe", () => { aba = Aba.Calendario; campAberto = c.CampeonatosDoClube.FirstOrDefault(x => x.iniciado) ?? c.CampeonatosDoClube.First(); }, null),
                ("elenco", () => { aba = Aba.Elenco; selecionado = c.elenco[0]; comparar.Clear(); comparar.Add(c.elenco[1]); comparar.Add(c.elenco[5]); }, () => AtualizarDicaTroca(c.elenco[5])),
                ("treino", () => { aba = Aba.Treino; selecionado = null; comparar.Clear(); }, null),
                ("mercado", () => { aba = Aba.Mercado; colOrdem = -1; menuDe = null; comparar.Clear(); }, () => AtualizarPrevia(c.mercado[1])),
                ("scouts", () => aba = Aba.Scouts, null),
                ("serie", () => { c.PrepararSerie(); aba = Aba.Serie; }, null),
                ("serie-veto-auto", () => { c.PrepararSerie(); c.VetoAutomatico(); aba = Aba.Serie; }, null),
                ("prematch", () => { c.PrepararSerie(); c.VetoAutomatico(); aba = Aba.PreMatch; }, null),
                ("financas", () => aba = Aba.Financas, null),
                ("gaminghouse", () => aba = Aba.GamingHouse, null),
                ("pospartida", () => { c.PularMapa(); aba = Aba.Painel; }, null),
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
