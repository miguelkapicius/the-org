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

        enum Aba { Painel, Calendario, Elenco, Treino, Mercado, Financas, GamingHouse, Scouts, Playbook, Serie, PreMatch, PosPartida, Opcoes }
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
        readonly List<Jogador> comparar = new List<Jogador>();
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

            var ps = ScriptableObject.CreateInstance<PanelSettings>(); painel = ps;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // 1600x900: a mesma referência do mockup; telas maiores escalam proporcionalmente, sem borrar o texto
            ps.referenceResolution = captura ? new Vector2Int(1600, 900) : Opcoes.Referencia();   // o jogador escolhe o tamanho da interface nas opções
            Opcoes.AplicarTela();
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            RenderTexture rtCaptura = null;
            if (captura)
            {
                // CSM_CAPTURA_TAM=1024x768 testa outras proporções de janela (o padrão é 1600x900)
                int cw = 1600, ch = 900;
                var tam = Environment.GetEnvironmentVariable("CSM_CAPTURA_TAM");
                if (!string.IsNullOrEmpty(tam)) { var pp = tam.Split('x'); if (pp.Length == 2) { int.TryParse(pp[0], out cw); int.TryParse(pp[1], out ch); } }
                rtCaptura = new RenderTexture(Mathf.Max(320, cw), Mathf.Max(240, ch), 24, RenderTextureFormat.ARGB32);
                ps.targetTexture = rtCaptura;
            }
            // O tema de runtime vem de Resources (e não do AssetDatabase, que só existe no Editor): sem ele o PanelSettings
            // criado em código nasce sem tema no build ("No Theme Style Sheet set to PanelSettings") e a interface colapsa.
            var tema = Resources.Load<ThemeStyleSheet>("ManagerUI/TemaRuntime");
            if (tema != null) ps.themeStyleSheet = tema;
            else Debug.LogError("Falta Assets/Resources/ManagerUI/TemaRuntime.tss: sem tema a interface do manager não renderiza direito no build.");
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
            janela = E("janela");
            tela.Add(janela);
            LigarDica();

            retratoRT = BonecosPreview.GerarRetrato();
            RetratoBoneco = retratoRT;

            Mostrar();
            tela.Focus();
            SomManager.AmbienteLigar();   // o zumbido da sala de controle (decisão 0029); some quando a partida começa

            if (rtCaptura != null) yield return Capturar(pastaCaptura, rtCaptura);
        }

        void OnDestroy()
        {
            if (preview != null) Destroy(preview.gameObject);
            if (retratoRT != null) { retratoRT.Release(); Destroy(retratoRT); }
            RetratoBoneco = null; Ui.Dica = null;
            SomManager.AmbienteDesligar();
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
                case KeyCode.Alpha9: Ir(Aba.Playbook); break;
                case KeyCode.Escape:
                    if (aba == Aba.PreMatch) Ir(Aba.Serie);
                    else if (aba == Aba.Serie || aba == Aba.PosPartida) Ir(Aba.Painel);
                    else if (aba == Aba.Calendario && campAberto != null) { campAberto = null; Mostrar(); }
                    else { Mostrar(); }
                    break;
                case KeyCode.Return: case KeyCode.KeypadEnter:
                    if (aba == Aba.PreMatch && !c.Falido) Assistir();
                    else if (aba == Aba.PosPartida) Continuar();
                    break;
            }
        }

        // ================================================================================================ avisos temporários e dicas
        /// <summary>Aviso que some sozinho (no canto), com um botão opcional (ex.: Desfazer). Não é refeito quando a tela é redesenhada.</summary>
        Som? proximoSomToast;   // um som específico para o próximo aviso (ex.: contrato assinado)

        void Toast(string texto, bool erro = false, string acaoRotulo = null, Action acao = null)
        {
            toastEl?.RemoveFromHierarchy();
            var t = E("toast", erro ? "erro" : "ok");
            SomManager.Tocar(proximoSomToast ?? (erro ? Som.Erro : Som.Aviso), 0.8f); proximoSomToast = null;
            if (TutorialAtivo) t.style.bottom = 300;   // sobe para não cair em cima do cartão dos primeiros passos
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
            AtribuirFotos(c.elenco.Concat(c.mercado));   // cada jogador tem o seu rosto (decisão 0027)
            janela.Clear(); previaTxt = null; previaCaixa = null; dicaTroca = null; rolagem = null;
            if (aba == Aba.PreMatch) { if (c.serie == null) { aba = Aba.Painel; abaAnterior = aba; } else { TelaPreMatch(); return; } }

            janela.Add(MontarMenu());
            var principal = E("tela"); janela.Add(principal);
            if (mudou) { principal.AddToClassList("entra"); principal.schedule.Execute(() => principal.AddToClassList("visivel")); }

            string titulo, sub;
            switch (aba)
            {
                case Aba.Calendario: titulo = "Calendário"; sub = campAberto != null ? campAberto.nome : "Temporada " + c.Temporada + " · campeonatos do circuito " + Mundo.NomeCircuito(c.CircuitoClube); break;
                case Aba.Elenco: titulo = "Elenco"; sub = "Escale os 5 titulares. Passe o mouse para ver o efeito antes de trocar."; break;
                case Aba.Treino: titulo = "Treino"; sub = treinoJogador != null && c.elenco.Contains(treinoJogador) ? "Plano da semana de " + treinoJogador.nome + "." : "Treino evolui, mas cansa. Ache o equilíbrio."; break;
                case Aba.Mercado: titulo = "Mercado"; sub = "Jogadores livres. A lista muda toda semana."; break;
                case Aba.GamingHouse: titulo = "Gaming House"; sub = "Cada nível libera vantagens para o time."; break;
                case Aba.Financas: titulo = "Finanças"; sub = "O que entra e o que sai toda semana."; break;
                case Aba.Scouts: titulo = "Scouts"; sub = "Descubra como o adversário joga e quem vale contratar."; break;
                case Aba.Opcoes: titulo = "Opções"; sub = "Som, tamanho da interface e janela. Vale na hora e fica guardado."; break;
                case Aba.Playbook: titulo = "Playbook"; sub = "As jogadas que o time prefere e as que nunca faz."; break;
                case Aba.Serie: titulo = "Preparar série"; sub = c.serie != null ? c.serie.camp.nome + " · " + Fmt.Rotulo(c.serie.camp.nome, c.serie.conf.rotulo) : ""; break;
                case Aba.PosPartida: { var r = resumoAberto ?? c.naoVisto; titulo = "Pós-partida"; sub = r != null ? r.campeonato + " · " + r.mapa + " · contra " + r.adversario : ""; break; }
                default: titulo = "Painel do clube"; sub = "O que fazer agora e como o time está."; break;
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
                case Aba.Playbook: TelaPlaybook(); break;
                case Aba.Opcoes: TelaOpcoes(); break;
                case Aba.Serie: TelaSerie(); break;
                case Aba.PosPartida: TelaPosPartida(); break;
                default: TelaPainel(); break;
            }
            var tut = CartaoTutorial(); if (tut != null) janela.Add(tut);
            if (rolagemGuardada != Vector2.zero) { var alvo = rolagemGuardada; rolagem.schedule.Execute(() => rolagem.scrollOffset = alvo).StartingIn(1); }
        }

        /// <summary>Menu lateral só de ícones (referência: barra estreita com o logo no alto, ícones em quadrinhos e o ativo destacado).
        /// O nome de cada tela vai na dica; o ponto laranja no Painel avisa que há o que resolver.</summary>
        VisualElement MontarMenu()
        {
            var m = E("menu");

            var h = E("menu-logo");
            h.Add(Brasao(c.clube, "laranja"));
            ComDica(h, c.clube + " · " + Mundo.NomeCircuito(c.CircuitoClube) + " · ranking " + c.PosicaoRanking);
            m.Add(h);

            var nav = E("menu-nav"); m.Add(nav);
            int nAvisos = c.Avisos().Count(a => a.destino != "serie");   // a série da semana já tem o cartão do hub e o botão do menu
            Item(nav, "Painel", "grid", Aba.Painel, nAvisos > 0 ? nAvisos.ToString() : null);
            Item(nav, "Calendário", "calendar", Aba.Calendario, null);
            Item(nav, "Elenco", "users", Aba.Elenco, c.elenco.Count.ToString());
            Item(nav, "Treino", "bolt", Aba.Treino, null);
            Item(nav, "Mercado", "swap", Aba.Mercado, c.mercado.Count.ToString());
            nav.Add(E("sep"));
            Item(nav, "Finanças", "dollar", Aba.Financas, null);
            Item(nav, "Gaming House", "house", Aba.GamingHouse, c.nivelGH.ToString());
            Item(nav, "Scouts", "eye", Aba.Scouts, c.scouts.Count > 0 ? c.scouts.Count.ToString() : null);
            int nPlay = c.favoritasTR.Count + c.favoritasCT.Count + c.proibidasTR.Count + c.proibidasCT.Count;
            Item(nav, "Playbook", "tag", Aba.Playbook, nPlay > 0 ? nPlay.ToString() : null);

            var rod = E("menu-rodape"); m.Add(rod);

            // o próximo passo do jogo: jogar a série marcada ou avançar o tempo (no Painel isso já é o cartão do hub)
            if (aba != Aba.Painel)
            {
                var p = c.Proximo();
                bool serie = c.TemJogoAgora;
                bool ativa = serie ? !c.Falido : c.PodeAvancar;
                var ab = E("menu-acao"); if (!ativa) ab.AddToClassList("desligado");
                ab.Add(new Icone(serie ? "play" : "skip", 22));
                if (ativa) Clicavel(ab, serie ? (Action)(() => Ir(Aba.Serie)) : AvancarSemanaUI);
                ComDica(ab, serie ? "Preparar série contra " + p.adv.nome + " (" + Fmt.Serie(p.conf.md) + ", chance de vitória " + Pct(c.ChanceVitoria()) + ")"
                                  : "Avançar semana" + (p.camp != null ? ". Próximo: " + p.camp.nome + " (" + Quando(p.semana, c.Semana) + ")" : ""));
                rod.Add(ab);
            }

            // opções: som, tamanho da interface e janela (decisão 0030); o liga e desliga do som fica lá dentro
            Item(rod, "Opções", "engrenagem", Aba.Opcoes, null);

            // ajuda: reinicia o tour dos primeiros passos (decisão 0028)
            var ajuda = E("item", "item-ajuda"); ajuda.Add(new Icone("help", 22));
            ComDica(ajuda, TutorialAtivo ? "Tutorial em andamento" : "Rever o tutorial dos primeiros passos");
            Clicavel(ajuda, () => { c.tutorial = 0; Salvar(); Ir(AlvoTutorial(0)); });
            rod.Add(ajuda);

            // recomeçar a carreira pede confirmação (apaga o salvamento)
            var nova = E("item", "item-nova"); if (confirmandoNova) nova.AddToClassList("perigo");
            nova.Add(new Icone("kebab", 22));
            ComDica(nova, confirmandoNova ? "Clique de novo para apagar e recomeçar a carreira" : "Nova carreira");
            Clicavel(nova, () =>
            {
                if (!confirmandoNova) { confirmandoNova = true; Mostrar(); return; }
                confirmandoNova = false; Carreira.Atual = Carreira.Nova(seed + c.Semana + c.Temporada * 53 + 101); c = Carreira.Atual;
                comparar.Clear(); resumoAberto = null; campAberto = null; Salvar();
                aba = Aba.Painel; Mostrar(); Toast("Carreira nova começada.");
            });
            rod.Add(nova);
            return m;
        }

        void Item(VisualElement menu, string texto, string icone, Aba a, string contagem)
        {
            var it = E("item"); if (aba == a) it.AddToClassList("ativo");
            if (TutorialAtivo && AlvoTutorial(c.tutorial) == a)   // o tour aponta a tela do passo com um brilho que pulsa
            {
                it.AddToClassList("pulso");
                it.schedule.Execute(() => it.ToggleInClassList("pulso-alto")).Every(800);
            }
            it.Add(new Icone(icone, 22));
            if (a == Aba.Painel && contagem != null) it.Add(E("item-ponto"));
            ComDica(it, a == Aba.Painel ? (contagem != null ? "Painel · " + contagem + (contagem == "1" ? " aviso" : " avisos") : "Painel")
                                         : texto + (contagem != null ? " · " + contagem : ""));
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

            // dois dados à direita, sem caixa: rótulo pequeno em cima, número embaixo
            var sem = E("dado-topo");
            sem.Add(L("SEMANA", "dado-rot")); sem.Add(L(c.Semana + " · T" + c.Temporada, "dado-val"));
            ComDica(sem, "O tempo corre em semanas (52 por temporada). Temporada " + c.Temporada + ", semana " + c.Semana + ".");
            h.Add(sem);

            var u = E("carteira");
            var ic = E("carteira-ic"); ic.Add(new Icone("dollar", 18)); u.Add(ic);
            var tx = E("carteira-txt");
            tx.Add(L("SALDO", "dado-rot"));
            tx.Add(L(Fmt.Dinheiro(c.dinheiro), "dado-val", c.dinheiro < 0 ? "vermelho" : "t"));
            u.Add(tx);
            ComDica(u, "Saldo do clube. Despesa fixa: " + Fmt.Dinheiro(c.DespesaFixaPorSemana) + " por semana (salários, manutenção e parcelas).");
            h.Add(u);
            return h;
        }

        /// <summary>Mostra o resultado de uma ação: erro vira aviso vermelho, sucesso vira aviso verde (que some sozinho).</summary>
        void Resposta(string erro, string ok, string acaoRotulo = null, Action acao = null)
        {
            if (erro == null) Salvar();
            Mostrar();
            if (erro != null) Toast(erro, true); else Toast(ok, false, acaoRotulo, acao);
        }

        void Ir(Aba a)
        {
            if (a != Aba.Calendario) campAberto = null;
            if (a == Aba.Calendario && aba != Aba.Calendario) { mesCal = -1; calAba = 0; }   // abre sempre no mês atual
            aba = a; confirmandoNova = false;
            if (a == Aba.Treino) treinoJogador = null;   // o menu volta para a lista do time
            if (a != Aba.PosPartida) resumoAberto = null;
            Mostrar();
        }

        static string Pct(double p) => Math.Round(p * 100) + "%";

        /// <summary>Passa uma semana (treino, finanças, campeonatos do mundo) e mostra o que aconteceu.</summary>
        void AvancarSemanaUI()
        {
            SomManager.Tocar(Som.Semana);
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
                var p = L("É o fim desta carreira. Você pode começar outra.", "f15", "suave", "quebra");
                p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.Semana + 7); c = Carreira.Atual; Salvar(); aba = Aba.Painel; Mostrar(); }, true, true));
                conteudo.Add(fc);
                return;
            }

            conteudo.Add(Hub());

            // painel limpo: hub, o que pede atenção (só se houver), notícias e partidas lado a lado, rivais. O elenco tem tela própria.
            var avisos = c.Avisos().Where(a => a.destino != "serie").ToList();   // a série da semana já está no hub
            if (avisos.Count > 0) conteudo.Add(L("O que merece atenção", "h2", "topo-h2"));
            var caixa = E("avisos");
            if (avisos.Count == 0) caixa.style.display = DisplayStyle.None;
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

            var colunas = E("duas-colunas"); conteudo.Add(colunas);
            var colN = E("coluna"); colunas.Add(colN);
            colN.Add(L("Notícias", "h2", avisos.Count == 0 ? "topo-h2" : "t"));
            var diario = E("diario");
            if (c.diario.Count == 0) diario.Add(L("Nada por enquanto.", "f15", "suave"));
            foreach (var d in c.diario.Take(4)) { var l = E("diario-linha"); if (diario.childCount == 0) l.AddToClassList("primeiro"); var tx = L(d, "suave"); tx.style.overflow = Overflow.Hidden; tx.style.textOverflow = TextOverflow.Ellipsis; tx.style.flexShrink = 1; ComDica(tx, d); l.Add(tx); diario.Add(l); }
            colN.Add(diario);

            var colP = E("coluna", "ultima"); colunas.Add(colP);
            colP.Add(L("Últimas partidas", "h2", avisos.Count == 0 ? "topo-h2" : "t"));
            var partidas = E("diario");
            if (c.historico.Count == 0) partidas.Add(L("Nenhuma partida jogada ainda.", "f15", "suave"));
            foreach (var r in c.historico.Take(4))
            {
                var l = E("diario-linha", "clicavel-l"); if (partidas.childCount == 0) l.AddToClassList("primeiro");
                var selo = Selo(r.venceu ? "V" : "D", r.venceu ? "verde" : "vermelho"); selo.style.marginRight = 12; selo.style.alignSelf = Align.Center; l.Add(selo);
                var pl = L(r.Placar, "f16", "negrito"); pl.style.width = 70; l.Add(pl);
                var gr = L(r.adversario + " · " + r.mapa, "suave"); gr.style.flexGrow = 1; l.Add(gr);
                var rr = r; Clicavel(l, () => { resumoAberto = rr; Ir(Aba.PosPartida); });
                ComDica(l, "Semana " + r.semana + (r.assistida ? "" : " · pulada") + (r.serieEncerrada ? " · série " + r.PlacarSerie : "") + ". Clique para ver o resumo.");
                partidas.Add(l);
            }
            colP.Add(partidas);

            // rivalidades (decisão 0021)
            var rivais = c.Rivais;
            if (rivais.Count > 0)
            {
                conteudo.Add(L("Rivalidades", "h2"));
                var caixaR = E("diario");
                for (int i = 0; i < Math.Min(3, rivais.Count); i++)
                {
                    var rv = rivais[i];
                    var l = E("diario-linha"); l.style.flexDirection = FlexDirection.Row; l.style.alignItems = Align.Center;
                    if (i == 0) { var sa = Selo("ARQUIRRIVAL", "laranja"); sa.style.marginRight = 8; l.Add(sa); }
                    l.Add(L(rv.adversario, "f16", "negrito"));
                    var t = L(" · " + rv.Texto + (rv.sequencia >= 2 ? " · " + rv.sequencia + " vitórias seguidas" : rv.sequencia <= -2 ? " · " + (-rv.sequencia) + " derrotas seguidas" : ""), "suave");
                    l.Add(t); caixaR.Add(l);
                }
                conteudo.Add(caixaR);
            }
        }


        // ------------------------------------------------------------------ hub do painel: 4 cartões com arte em tela cheia, uma ação cada
        VisualElement Hub()
        {
            var hub = E("hub");
            var p = c.Proximo(); bool agora = c.TemJogoAgora;

            // 1) o compromisso: o único cartão com botão (a ação principal da semana)
            var cm = HubCartao(agora ? "SÉRIE" : p.camp != null ? p.camp.nome.ToUpperInvariant() : "FIM DA TEMPORADA",
                agora ? "Contra " + p.adv.nome + " · " + Fmt.Serie(p.conf.md) + " · " + Pct(c.ChanceVitoria()) + " de chance" : p.camp != null ? "Começa " + Quando(p.semana, c.Semana) : "O ranking decide o circuito do ano que vem",
                null, null, true, agora ? "serie" : "calendario");
            var btn = E("hub-btn"); btn.Add(L(agora ? "PREPARAR SÉRIE" : "AVANÇAR SEMANA")); btn.Add(new Icone(agora ? "playf" : "skip", 18));
            if (agora ? !c.Falido : c.PodeAvancar) Clicavel(btn, agora ? (Action)(() => Ir(Aba.Serie)) : AvancarSemanaUI);
            cm.Q(null, "hub-corpo").Add(btn);
            hub.Add(cm);

            // 2) treino, calendário e mercado: o cartão inteiro é o botão; o texto diz só o essencial
            int treinando = c.elenco.Count(j => j.sessoes > 0), camps = c.CampeonatosDoClube.Count(x => !x.terminado);
            hub.Add(HubCartao("TREINO", treinando + " de " + c.elenco.Count + " treinando", null, () => Ir(Aba.Treino), false, "treino"));
            hub.Add(HubCartao("CALENDÁRIO", camps + " campeonatos · ranking #" + c.PosicaoRanking, null, () => Ir(Aba.Calendario), false, "calendario"));
            hub.Add(HubCartao("MERCADO", c.mercado.Count + " jogadores livres", null, () => Ir(Aba.Mercado), false, "mercado"));
            return hub;
        }

        VisualElement HubCartao(string titulo, string sub, int? selo, Action acao, bool destaque, string arte)
        {
            var card = E("hub-card"); if (destaque) card.AddToClassList("destaque");
            var img = Resources.Load<Texture2D>("ManagerUI/Hub/" + arte);   // ilustrações em Assets/Resources/ManagerUI/Hub (geradas no Figma)
            if (img != null) { card.style.backgroundImage = new StyleBackground(img); AlinharAoTopo(card); }   // cobre o cartão sem esticar, rostos no alto
            var veu = E("hub-veu"); veu.style.backgroundImage = new StyleBackground(Ui.VeuInferior()); veu.pickingMode = PickingMode.Ignore; card.Add(veu);
            if (selo.HasValue) { var bd = E("hub-selo"); bd.Add(L(selo.Value.ToString())); card.Add(bd); }
            var corpo = E("hub-corpo");
            corpo.Add(L(titulo, "hub-tit")); corpo.Add(L(sub, "hub-sub"));
            card.Add(corpo);
            if (acao != null) Clicavel(card, acao);
            return card;
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
            midia.Add(Retrato(j, "retrato-fill"));
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

            if (r.serieEncerrada)
            {
                var fim = c.titulos.FirstOrDefault(t => t.nome == r.campeonato);
                if (fim != null) conteudo.Add(L("Campeonato encerrado: " + Fmt.Ordinal(fim.posicao) + " lugar, prêmio " + Fmt.Dinheiro(fim.premio) + ".", "f17", "negrito", "verde"));
            }

            // melhores momentos (decisão 0020)
            if (r.assistida || r.jogada != null) conteudo.Add(L("Jogada da partida", "h2"));   // mapa pulado não tem jogada: a seção some
            if (r.jogada != null)
            {
                var jg = E("linha-h"); jg.style.alignItems = Align.Center;
                jg.Add(Selo(r.jogadaNossa ? "DO SEU TIME" : "DO ADVERSÁRIO", r.jogadaNossa ? "verde" : "vermelho"));
                var tj = L(r.jogada, "f18", "negrito"); tj.style.marginLeft = 12; tj.style.whiteSpace = WhiteSpace.Normal; tj.style.flexShrink = 1; jg.Add(tj);
                conteudo.Add(jg);
            }
            else if (r.assistida) conteudo.Add(L("Nenhum abate marcante nesta partida.", "f15", "suave"));
            if (r.mvpSerie != null) { var mvs = L("MVP da série: " + r.mvpSerie, "f17", "negrito", "verde"); mvs.style.marginTop = 10; conteudo.Add(mvs); }

            conteudo.Add(L("Desempenho dos jogadores", "h2"));
            if (r.estimado) conteudo.Add(L("Números estimados: o mapa foi pulado.", "nota-estimada"));
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
        /// <summary>O elenco só em cartões: titulares e reservas, cada cartão com os botões à vista (escalar ou tornar reserva, comparar, vender).</summary>
        void TelaElenco()
        {
            conteudo.Add(ResumoEscalacao());
            var cmp = Comparacao(); if (cmp != null) conteudo.Add(cmp);

            conteudo.Add(L("Titulares", "h2", "topo-h2"));
            CartoesElenco(c.Titulares.ToList());

            int vagas = c.ElencoMax - Provisorio.TITULARES;
            var reservas = c.elenco.Skip(Provisorio.TITULARES).ToList();
            conteudo.Add(L("Reservas (" + reservas.Count + "/" + vagas + ")", "h2"));
            if (reservas.Count == 0) conteudo.Add(L("Sem reservas. Contrate no mercado: se alguém sair, o time fica incompleto.", "f15", "suave"));
            else CartoesElenco(reservas);
        }

        /// <summary>Força, chance de vitória e cobertura de funções do time titular.</summary>
        VisualElement ResumoEscalacao()
        {
            // a conclusão em uma linha: força e chance; o resto só aparece se pedir atenção (decisão 0023)
            var card = E("escala");
            var topo = E("escala-topo");
            var res = E("escala-res");
            var forca = L("Força " + c.Forca, "escala-forca"); forca.pickingMode = PickingMode.Position;
            ComDica(forca, "Força do time titular: média do geral dos 5, com cansaço, moral e entrosamento.");
            res.Add(forca);
            var adv = c.Adversario;
            res.Add(L(Pct(c.ChanceVitoria()) + " de chance" + (adv != null ? " contra " + adv.nome : ""), "f17", "suave"));
            topo.Add(res);

            var alertas = E("chips-func");
            var faltando = c.FuncoesFaltando();
            int ent = (int)c.EntrosamentoTime;
            const string DICA_ENT = "Entrosamento dos titulares (0 a 100). Mais entrosado, mais os bots trocam o colega que morreu, e a força sobe até 2%. Sobe a cada semana jogando junto e com sessões coletivas; um contratado chega com pouco.";
            foreach (var f in faltando) { var chip = E("chip-f", "falta"); chip.Add(L("FALTA " + Fmt.FuncaoCurta(f))); ComDica(chip, "Nenhum titular na função " + Fmt.Funcao(f) + ". Escale alguém dessa função."); alertas.Add(chip); }
            if (ent < 40) { var ce = E("chip-f", "falta"); ce.Add(L("POUCO ENTROSADO")); ComDica(ce, DICA_ENT); alertas.Add(ce); }
            if (alertas.childCount == 0)
            {
                var ok = E("chip-f", "ok"); ok.Add(L("FUNÇÕES COMPLETAS · ENTROSAMENTO " + ent));
                ComDica(ok, "Há um titular em cada função (" + string.Join(", ", FUNCOES_TIME.Select(Fmt.Funcao)) + ").\n\n" + DICA_ENT);
                alertas.Add(ok);
            }
            topo.Add(alertas);
            dicaTroca = L("", "dica-troca");   // a prévia da troca ocupa a linha dos alertas durante o hover (a tela não pula)
            topo.Add(dicaTroca);
            card.Add(topo);
            PreverTroca(null);
            return card;
        }

        /// <summary>Cartões do elenco em fileiras de 5 (a última fileira é completada com vazios para manter a largura).</summary>
        void CartoesElenco(List<Jogador> lista)
        {
            for (int i = 0; i < lista.Count; i += 5)
            {
                var fila = E("fila-cards");
                for (int k = 0; k < 5; k++) fila.Add(i + k < lista.Count ? CartaoElenco(lista[i + k]) : E("card-j", "vazio"));
                conteudo.Add(fila);
            }
        }

        /// <summary>
        /// Cartão do elenco, enxuto (decisão 0023): retrato com função e geral; nome; o ESTADO em palavras (em forma, cansado,
        /// desmotivado...) em vez de barras de cansaço e moral; os 5 atributos que a IA usa; uma ação principal e duas de ícone.
        /// Os números finos (cansaço, moral, potencial, salário) ficam na dica.
        /// </summary>
        VisualElement CartaoElenco(Jogador j)
        {
            bool tit = c.Titular(j);
            var cj = E("card-j"); if (comparar.Contains(j)) cj.AddToClassList("comp");
            var midia = MidiaJogador(j);
            cj.Add(midia);

            var info = E("info");
            info.Add(NomeComTraco(j, "info-nome", true));
            var meta = L(j.idade + " anos · " + Fmt.Dinheiro(j.Salario) + "/sem", "f13", "suave", "info-meta");
            meta.pickingMode = PickingMode.Position;
            ComDica(meta, "Potencial " + j.potencial + " (o teto de evolução). Salário pago toda semana. Vende por " + Fmt.Dinheiro(j.Venda) + ".");
            info.Add(meta);

            // estado: a conclusão primeiro; o número na dica
            var estado = E("estado");
            int cs = (int)j.cansaco, mo = (int)j.moral;
            var sc = Selo(cs >= 60 ? "EXAUSTO" : cs >= 35 ? "CANSADO" : "EM FORMA", cs >= 60 ? "vermelho" : cs >= 35 ? "laranja" : "verde");
            ComDica(sc, "Cansaço " + cs + " de 100. Acima de 35 a habilidade na partida cai; descansa sem treino.");
            estado.Add(sc);
            if (mo < 45 || mo >= 75)
            {
                var sm = Selo(mo < Provisorio.MORAL_AVISO ? "DESMOTIVADO" : mo < 45 ? "MORAL BAIXA" : "MOTIVADO", mo < Provisorio.MORAL_AVISO ? "vermelho" : mo < 45 ? "laranja" : "verde");
                ComDica(sm, "Moral " + mo + " de 100: sobe com vitórias e jogando de titular, cai com derrotas e no banco. Muda a habilidade de -4,5% a +3%.");
                estado.Add(sm);
            }
            if (j.mvps > 0) { var sv = Selo("MVP " + j.mvps + "×", null); ComDica(sv, "Vezes que foi MVP de mapa pelo clube" + (j.jogadas.Count > 0 ? ".\n\nJogadas da partida:\n• " + string.Join("\n• ", j.jogadas) : ".")); estado.Add(sv); }
            info.Add(estado);

            for (int k = 1; k <= 5; k++)
            {
                var col = COLS[k];
                var linha = AtributoLinha(k == 4 ? "Utilitária" : col.rot, col.chave(j));
                linha.pickingMode = PickingMode.Position; ComDica(linha, col.dica);
                info.Add(linha);
            }
            cj.Add(info);

            // uma ação principal (largura toda) e duas de ícone, com a explicação na dica
            var ac = E("card-acoes");
            var jj = j;
            var principal = BotaoMini(tit ? "Para a reserva" : "Escalar", () =>
            {
                if (jj == null) return;
                if (tit) c.MandarParaReserva(jj); else c.TornarTitular(jj);
                Salvar(); Mostrar(); Toast(jj.nome + (tit ? " agora é reserva." : " agora é titular.") + " Força " + c.Forca + ".");
            }, !tit, tit ? c.elenco.Count > Provisorio.TITULARES : true);
            principal.RegisterCallback<PointerEnterEvent>(_ => PreverTroca(jj));
            principal.RegisterCallback<PointerLeaveEvent>(_ => PreverTroca(null));
            principal.AddToClassList("grow"); if (!tit) principal.AddToClassList("destaque");
            ac.Add(principal);
            var cmpBtn = BotaoMini("", () => AlternarComparar(jj), comparar.Contains(j), true, "swap"); cmpBtn.AddToClassList("so-ic"); ComDica(cmpBtn, comparar.Contains(j) ? "Tirar da comparação" : "Comparar com outro jogador"); ac.Add(cmpBtn);
            var vender = BotaoMini("", () => Vender(jj), false, c.elenco.Count > Provisorio.TITULARES, "tag"); vender.AddToClassList("perigo"); vender.AddToClassList("so-ic");
            ComDica(vender, c.elenco.Count > Provisorio.TITULARES ? "Vender por " + Fmt.Dinheiro(j.Venda) + " (dá para desfazer logo depois)." : "É preciso manter pelo menos " + Provisorio.TITULARES + " jogadores.");
            ac.Add(vender);
            cj.Add(ac);
            return cj;
        }

        /// <summary>Mostra, antes de clicar, o que muda na força e na chance de vitória se o jogador for escalado ou for para a reserva.</summary>
        void PreverTroca(Jogador j)
        {
            if (dicaTroca == null) return;
            dicaTroca.style.display = j == null ? DisplayStyle.None : DisplayStyle.Flex;   // a prévia só aparece quando o mouse está no botão
            var alertas = dicaTroca.parent?.Q(null, "chips-func"); if (alertas != null) alertas.style.display = j == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (j == null) { dicaTroca.text = ""; return; }
            Jogador sai, entra;
            if (c.Titular(j)) { sai = j; entra = c.elenco.Skip(Provisorio.TITULARES).OrderByDescending(r => r.Overall).FirstOrDefault(); }
            else { entra = j; sai = c.Titulares.OrderBy(t => t.Overall).First(); }
            if (entra == null) { dicaTroca.text = "Não há reserva para entrar no lugar de " + j.nome + "."; return; }
            int depois = c.ForcaCom(entra, sai), dif = depois - c.Forca;
            dicaTroca.text = entra.nome + " no lugar de " + sai.nome + ":  força " + c.Forca + " → " + depois + "  ·  chance " + Pct(c.ChanceVitoria()) + " → " + Pct(c.ChanceVitoria(depois));
            dicaTroca.EnableInClassList("verde", dif > 0); dicaTroca.EnableInClassList("vermelho", dif < 0);
        }

        // ------------------------------------------------------------------ comparação
        void AlternarComparar(Jogador j)
        {
            if (comparar.Contains(j)) comparar.Remove(j);
            else { if (comparar.Count >= 2) comparar.RemoveAt(0); comparar.Add(j); }
            Mostrar();
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

        void Vender(Jogador j)
        {
            var erro = c.Vender(j);
            comparar.Remove(j); 
            Resposta(erro, j.nome + " foi vendido por + " + Fmt.Dinheiro(j.Venda) + ".", "Desfazer", () =>
            {
                var e2 = c.DesfazerVenda();
                Salvar(); Mostrar();
                Toast(e2 ?? j.nome + " voltou para o elenco.", e2 != null);
            });
        }

        // ================================================================================================ mercado (vitrine em GerenteUI.Mercado.cs)
        void AtualizarPrevia(Jogador j)
        {
            if (previaTxt == null) return;
            bool bom = false;
            // a conclusão em uma linha: melhora o time? quanto custa? (decisão 0023)
            if (j == null)
                previaTxt.text = "Passe o mouse num jogador para ver o que muda no seu time.";
            else
            {
                var sai = c.QuemSaiPara(j);
                string t;
                if (c.elenco.Count >= c.ElencoMax) t = "Elenco cheio: venda alguém antes de contratar.";
                else if (c.dinheiro < Negociador.Pedido(c, j).luvas) t = "Falta dinheiro: " + j.nome + " pede " + Fmt.Dinheiro(Negociador.Pedido(c, j).luvas) + " de luvas.";
                else if (sai != null)
                {
                    int f = c.ForcaCom(j, sai);
                    t = j.nome + " no lugar de " + sai.nome + ":  força " + c.Forca + " → " + f + "  ·  chance " + Pct(c.ChanceVitoria()) + " → " + Pct(c.ChanceVitoria(f));
                    bom = true;
                }
                else t = j.nome + " não melhora os titulares: entraria como reserva.";
                var ped = Negociador.Pedido(c, j);
                t += "   |   se aceitar o pedido: saldo depois " + Fmt.Dinheiro(c.dinheiro - ped.luvas) + " · salário +" + Fmt.Dinheiro(ped.salario) + "/sem";
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
            c.dinheiro = 1500000; c.tutorial = Carreira.TUTORIAL_PASSOS; for (int k = 0; k < 3; k++) c.SubirGamingHouse();
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
                ("calendario-detalhe", () => { aba = Aba.Calendario; campAberto = c.CampeonatosDoClube.FirstOrDefault(x => x.iniciado) ?? c.CampeonatosDoClube.First(); campAba = 0; }, null),
                ("camp-premios", () => { campAba = 1; }, null),
                ("camp-participantes", () => { campAba = 2; }, null),
                ("camp-partidas", () => { campAba = 3; }, null),
                ("camp-chaves", () => { var kk = c.mundo.campeonatos.FirstOrDefault(x => x.formato != Formato.Liga && !x.iniciado && x.times.Count >= 8); if (kk != null) { kk.iniciado = true; kk.Iniciar(-1); campAberto = kk; campAbaDe = kk; } campAba = 4; }, () => { }),
                ("elenco", () => { aba = Aba.Elenco; comparar.Clear(); }, () => PreverTroca(c.elenco[5])),
                ("elenco-comparar", () => { aba = Aba.Elenco; comparar.Clear(); comparar.Add(c.elenco[1]); comparar.Add(c.elenco[5]); }, null),
                ("treino", () => { aba = Aba.Treino; treinoJogador = null; comparar.Clear(); }, null),
                ("treino-jogador", () => { aba = Aba.Treino; treinoJogador = c.elenco[1]; c.DefinirLadoTreino(treinoJogador, 0); }, null),
                ("treino-jogador-ct", () => { aba = Aba.Treino; treinoJogador = c.elenco.FirstOrDefault(x => x.LadoForte == false) ?? c.elenco[2]; }, () => treinoJogador = null),
                ("mercado", () => { aba = Aba.Mercado; mercadoChat = false; comparar.Clear(); }, () => AtualizarPrevia(c.mercado[1])),
                ("mercado-negociacao", () => { mercadoChat = true; var jj = c.mercado.OrderByDescending(x => x.Overall).Skip(2).First(); chatJogador = jj; var n = Negociador.Abrir(c, jj); Negociador.Propor(c, n, (int)(n.luvasPedida * 0.6), (int)(n.salarioPedido * 0.7), false); Negociador.Propor(c, n, (int)(n.luvasPedida * 0.95), (int)(n.salarioPedido * 0.95), true); aba = Aba.Mercado; }, null),
                ("scouts", () => aba = Aba.Scouts, null),
                ("opcoes", () => aba = Aba.Opcoes, null),
                ("playbook", () => { PlantaJogada.InicioDaCaptura = 0.62f; ladoPlaybookTR = true; var js = Estrategias.Jogadas(true); c.MarcarJogada(js[0].nome, true, Carreira.MarcaJogada.Favorita); c.MarcarJogada(js[2].nome, true, Carreira.MarcaJogada.Proibida); jogadaAberta = js[1].nome; exemploAberto = 0; aba = Aba.Playbook; }, null),
                ("playbook-troca", () => { PlantaJogada.InicioDaCaptura = 0.5f; ladoPlaybookTR = true; var js = Estrategias.Jogadas(true); jogadaAberta = js[1].nome; exemploAberto = 0; aba = Aba.Playbook; }, () => { var js = Estrategias.Jogadas(true); exemploAberto++; AtualizarPainelPlaybook(false); jogadaAberta = js[3].nome; exemploAberto = 0; PreencherListaPlaybook(); AtualizarPainelPlaybook(true); Marcar(js[3].nome, true, Carreira.MarcaJogada.Favorita); }),
                ("playbook-ct", () => { ladoPlaybookTR = false; jogadaAberta = Estrategias.Jogadas(false)[0].nome; exemploAberto = 0; aba = Aba.Playbook; }, null),
                ("serie", () => { c.PrepararSerie(); aba = Aba.Serie; }, null),
                ("serie-veto-auto", () => { c.PrepararSerie(); c.VetoAutomatico(); aba = Aba.Serie; }, null),
                ("prematch", () => { c.PrepararSerie(); c.VetoAutomatico(); aba = Aba.PreMatch; }, null),
                ("financas", () => aba = Aba.Financas, null),
                ("gaminghouse", () => aba = Aba.GamingHouse, null),
                ("pospartida", () => { c.PularMapa(); aba = Aba.Painel; }, null),
                ("calendario-depois", () => { c.naoVisto = null; int g2 = 0; while (g2++ < 12) { if (c.TemJogoAgora) c.PularSerie(); c.AvancarSemana(); } c.naoVisto = null; campAberto = null; mesCal = -1; calAba = 0; aba = Aba.Calendario; }, null),
                ("calendario-campeonatos", () => { campAberto = null; calAba = 1; aba = Aba.Calendario; }, null),
                ("calendario-ranking", () => { campAberto = null; calAba = 2; aba = Aba.Calendario; }, null),
                ("painel-depois", () => { c.naoVisto = null; aba = Aba.Painel; }, () => Toast(c.elenco[5].nome + " foi vendido por + " + Fmt.Dinheiro(c.elenco[5].Venda) + ".", false, "Desfazer", () => { })),
                ("tutorial-painel", () => { c.tutorial = 0; aba = Aba.Painel; }, null),
                ("tutorial-treino", () => { c.tutorial = 2; aba = Aba.Treino; treinoJogador = null; }, null),
                ("tutorial-mercado", () => { c.tutorial = 3; mercadoChat = false; aba = Aba.Mercado; }, null),
            };
            foreach (var (nome, preparar, depois) in passos)
            {
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
