using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CSM.Sim;
using CSM.Audio;
using static CSM.View3D.UiKit;

namespace CSM.View3D
{
    /// <summary>
    /// Menu inicial e placar (sessão HUDs/interface). Menu: a vila de fundo com um round de verdade rodando atrás
    /// (PartidaAoVivo3D.ComecarVitrine, outra semente, sem HUD), só o som do vento, botões INICIAR e SAIR. Placar no
    /// jeito do TAB do CS2 (referência: placar do CS2): placar de cada time num círculo na cor do lado, linhas listradas
    /// com K / A / D / +/− / ADR / HS% / MVP, e no meio o histórico dos rounds (ícone de como acabou, na linha de quem
    /// ganhou). Segurar Tab na partida mostra; no fim da partida aparece sozinho com REINICIAR e MENU.
    /// </summary>
    public class Menu3D
    {
        /// <summary>Menu só no Play de verdade: batch, print do editor e medição (-csm_*) vão direto pra partida.</summary>
        public static bool Ligado => Application.isPlaying && !Application.isBatchMode
            && !System.Environment.GetCommandLineArgs().Any(s => s.StartsWith("-csm_") || s == "-sem_menu");

        // placar e botões do fim leem o tema do HUD (decisão 0032); o menu da vitrine do protótipo continua como era
        static Color ESCURO => new Color(HudTema.Escuro.r, HudTema.Escuro.g, HudTema.Escuro.b, 0.97f); static Color CREME => HudTema.Creme; static Color CINZA => new Color(HudTema.Cinza.r, HudTema.Cinza.g, HudTema.Cinza.b, 0.6f);
        static readonly Color LARANJA = Hx("#F5A524"); static Color TR => HudTema.PlacarTR; static Color CT => HudTema.PlacarCT; static Color TINTA => HudTema.Tinta;
        static Color Hx(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color Alfa(Color c, float a) => new Color(c.r, c.g, c.b, a);

        readonly PartidaAoVivo3D p;
        readonly RectTransform raiz;
        public readonly Canvas Tela;
        /// <summary>Mostra o placar como se o Tab estivesse segurado (print/teste).</summary>
        public bool forcarPlacar;
        readonly CanvasGroup menuG, placarG, botoesG;
        readonly Image preto;
        readonly AudioSource vento;
        bool vitrineAntes = true;
        float fimT, pretoAlvo;

        /// <summary>Placar ou menu na tela: o jogo esconde os controles dele.</summary>
        public bool Cobrindo => placarG.alpha > 0.05f || menuG.alpha > 0.05f;

        public Menu3D(PartidaAoVivo3D partida)
        {
            p = partida;
            var go = new GameObject("Menu e placar", typeof(RectTransform)) { layer = CAMADA_UI };
            var cv = Tela = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 50;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1;
            go.AddComponent<GraphicRaycaster>();
            raiz = (RectTransform)go.transform;
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            placarG = MontarPlacar();
            menuG = MontarMenu();
            botoesG = MontarBotoesFim();
            preto = Cheio("preto", raiz, Color.black); preto.color = new Color(0, 0, 0, 1); pretoAlvo = 0;

            var clip = Resources.Load<AudioClip>("Sons/ambiente/vento_0");
            vento = new GameObject("Vento do menu").AddComponent<AudioSource>();
            vento.clip = clip; vento.loop = true; vento.spatialBlend = 0; vento.volume = 0; if (clip != null) vento.Play();
            if (PartidaAoVivo3D.DoManager) { vitrineAntes = false; Iniciar(); }   // vindo do manager: direto na partida, sem o menu do protótipo
            else EntrarMenu();
        }

        // ------------------------------------------------------------------ peças
        static Image Cheio(string nome, Transform pai, Color cor)
        {
            var im = No(nome, pai, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            var r = im.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            im.color = cor; im.raycastTarget = false; return im;
        }

        static Button Botao(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, string txt, bool principal, System.Action acao, Vector2? pivo = null)
        {
            var im = Painel(nome, pai, ancora, pos, tam, Color.white, pivo);
            im.raycastTarget = true;
            var b = im.gameObject.AddComponent<Button>();
            var c = b.colors;
            Color baseC = principal ? HudTema.Acao : HudTema.Novo ? new Color(0.09f, 0.1f, 0.12f, 0.88f) : new Color(0.12f, 0.105f, 0.095f, 0.85f);
            c.normalColor = baseC; c.highlightedColor = principal ? HudTema.AcaoRealce : HudTema.Novo ? new Color(0.18f, 0.2f, 0.23f, 0.95f) : new Color(0.24f, 0.21f, 0.19f, 0.95f);
            c.pressedColor = principal ? HudTema.AcaoApertada : HudTema.Novo ? new Color(0.06f, 0.07f, 0.08f, 0.95f) : new Color(0.08f, 0.07f, 0.065f, 0.95f); c.selectedColor = c.normalColor; c.fadeDuration = 0.08f;
            b.colors = c; b.targetGraphic = im;
            b.onClick.AddListener(() => acao());
            var t = Texto("txt", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, tam, txt, 34, principal ? TINTA : CREME, TextAnchor.MiddleCenter, false, null, false);
            t.characterSpacing = 4;
            return b;
        }

        // ------------------------------------------------------------------ menu inicial
        CanvasGroup MontarMenu()
        {
            var r = No("menu", raiz, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var g = r.gameObject.AddComponent<CanvasGroup>();
            // escurece a esquerda (onde fica o texto), a vila aparece limpa à direita
            // (faixas sólidas: imagem lisa com cor, o mesmo caminho dos painéis)
            const int N = 28; const float LARG = 1150;
            for (int i = 0; i < N; i++)
            {
                float u = (i + 0.5f) / N, a = 0.86f * (1 - u * u * (3 - 2 * u));
                var f = No("sombra " + i, r, new Vector2(0, 0.5f), new Vector2(i * LARG / N, 0), new Vector2(LARG / N + 1, 1080), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>();
                f.rectTransform.anchorMin = new Vector2(0, 0); f.rectTransform.anchorMax = new Vector2(0, 1); f.rectTransform.sizeDelta = new Vector2(LARG / N + 1, 0);
                f.color = new Color(0.05f, 0.04f, 0.035f, a); f.raycastTarget = false;
            }
            // título
            Painel("traço", r, new Vector2(0, 0.5f), new Vector2(120, 196), new Vector2(10, 128), LARANJA, new Vector2(0, 0.5f));
            var tit = Texto("título", r, new Vector2(0, 0.5f), new Vector2(150, 214), new Vector2(900, 140), "CS MANAGER", 132, CREME, TextAnchor.MiddleLeft, true, new Vector2(0, 0.5f));
            tit.characterSpacing = 2;
            Texto("subtítulo", r, new Vector2(0, 0.5f), new Vector2(154, 128), new Vector2(900, 40), "TÉCNICO DE CS  ·  MAPA VILA", 30, CINZA, TextAnchor.MiddleLeft, false, new Vector2(0, 0.5f));
            // botões
            var ini = Botao("iniciar", r, new Vector2(0, 0.5f), new Vector2(120, -10), new Vector2(400, 76), "INICIAR", true, Iniciar, new Vector2(0, 0.5f));
            Botao("sair", r, new Vector2(0, 0.5f), new Vector2(120, -104), new Vector2(400, 76), "SAIR", false, Sair, new Vector2(0, 0.5f));
            Texto("rodapé", r, new Vector2(0, 0), new Vector2(124, 56), new Vector2(900, 30), "protótipo  ·  as opções chegam com a interface de verdade", 22, CINZA, TextAnchor.MiddleLeft, false, new Vector2(0, 0));
            EventSystem.current?.SetSelectedGameObject(null);
            return g;
        }

        void EntrarMenu()
        {
            Som3D.mudo = true; Som3D.Ambiente(0);   // na vitrine só o vento (os tiros do round de fundo ficam mudos)
            if (!p.Vitrine) p.ComecarVitrine();
            vitrineAntes = true;
        }

        void Iniciar()
        {
            preto.color = Color.black; pretoAlvo = 0;   // corte pro preto e abre na partida
            Som3D.mudo = false; Som3D.Ambiente(1);
            p.ComecarPartida();
            fimT = 0;
        }

        void Sair()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ placar (TAB do CS2)
        const float PW = 1260, PH = 780, LINHA = 44, NOME_X = 200;
        static readonly (string tit, float x)[] COLS = { ("K", 760), ("A", 832), ("D", 904), ("+/−", 984), ("ADR", 1068), ("HS%", 1148), ("MVP", 1216) };
        class Bloco
        {
            public Image circulo; public TextMeshProUGUI num, time, lado;
            public Image[] fundo = new Image[5]; public TextMeshProUGUI[] nome = new TextMeshProUGUI[5]; public TextMeshProUGUI[,] col = new TextMeshProUGUI[5, 7];
        }
        readonly Bloco[] blocos = new Bloco[2];
        TextMeshProUGUI pTitulo, pSub, hRotulo1, hRotulo2;
        RectTransform hist;
        readonly List<RawImage> histIcones = new List<RawImage>();
        Image histDivisa;

        CanvasGroup MontarPlacar()
        {
            var pan = Painel("placar", raiz, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(PW, PH), ESCURO, new Vector2(0.5f, 0.5f), true);
            var r = pan.rectTransform; var g = pan.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0; g.blocksRaycasts = false;
            pTitulo = Texto("título", r, new Vector2(0, 1), new Vector2(32, -20), new Vector2(700, 46), "", 38, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
            pSub = Texto("sub", r, new Vector2(1, 1), new Vector2(-32, -24), new Vector2(600, 40), "", 24, CINZA, TextAnchor.MiddleRight, false, new Vector2(1, 1));
            foreach (var (tit, x) in COLS)
                Texto("col " + tit, r, new Vector2(0, 1), new Vector2(x, -86), new Vector2(80, 26), tit, 20, CINZA, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 0.5f));
            blocos[0] = MontarBloco(r, -106);
            // histórico dos rounds entre os dois times
            hist = No("histórico", r, new Vector2(0, 1), new Vector2(NOME_X - 6, -360), new Vector2(PW - NOME_X - 24, 76), new Vector2(0, 1));
            hRotulo1 = Texto("1º", hist, new Vector2(0, 1), Vector2.zero, new Vector2(200, 20), "1º TEMPO", 17, CINZA, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
            hRotulo2 = Texto("2º", hist, new Vector2(0, 1), Vector2.zero, new Vector2(200, 20), "2º TEMPO", 17, CINZA, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
            histDivisa = Painel("divisa", hist, new Vector2(0, 1), Vector2.zero, new Vector2(2, 76), new Color(1, 1, 1, 0.18f), new Vector2(0.5f, 1));
            Painel("meio", hist, new Vector2(0, 1), new Vector2(0, -48), new Vector2(PW - NOME_X - 24, 2), new Color(1, 1, 1, 0.08f), new Vector2(0, 0.5f));
            blocos[1] = MontarBloco(r, -454);
            return g;
        }

        Bloco MontarBloco(RectTransform r, float y)
        {
            var b = new Bloco();
            float alt = 5 * LINHA + 4 * 4;
            b.circulo = Bola("círculo", r, new Vector2(0, 1), new Vector2(100, y - alt / 2 + 18), 116, CT);
            b.num = Texto("placar", b.circulo.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(116, 90), "0", 70, TINTA, TextAnchor.MiddleCenter, true, null, false);
            b.time = Texto("time", r, new Vector2(0, 1), new Vector2(100, y - alt / 2 - 62), new Vector2(180, 30), "", 26, CREME, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 0.5f));
            b.lado = Texto("lado", r, new Vector2(0, 1), new Vector2(100, y - alt / 2 - 90), new Vector2(180, 24), "", 18, CINZA, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 5; i++)
            {
                float yi = y - i * (LINHA + 4);
                b.fundo[i] = Painel("linha " + i, r, new Vector2(0, 1), new Vector2(NOME_X - 12, yi), new Vector2(PW - NOME_X - 18, LINHA), Color.clear, new Vector2(0, 1));
                b.nome[i] = Texto("nome", r, new Vector2(0, 1), new Vector2(NOME_X + 6, yi - LINHA / 2), new Vector2(480, LINHA), "", 28, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 0.5f));
                for (int c = 0; c < COLS.Length; c++)
                    b.col[i, c] = Texto(COLS[c].tit, r, new Vector2(0, 1), new Vector2(COLS[c].x, yi - LINHA / 2), new Vector2(80, LINHA), "", 28, CREME, TextAnchor.MiddleCenter, true, new Vector2(0.5f, 0.5f));
            }
            return b;
        }

        CanvasGroup MontarBotoesFim()
        {
            var r = No("botões do fim", raiz, new Vector2(0.5f, 0.5f), new Vector2(0, -PH / 2 - 40), new Vector2(PW, 80), new Vector2(0.5f, 1));
            var g = r.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0; g.blocksRaycasts = false; g.interactable = false;
            if (PartidaAoVivo3D.DoManager)
                Botao("voltar", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 70), "VOLTAR AO MANAGER", true, () => p.VoltarAoManager());   // a série da carreira fecha aqui: prêmio, salários e a pós-partida
            else
            {
                Botao("reiniciar", r, new Vector2(0.5f, 0.5f), new Vector2(-170, 0), new Vector2(320, 70), "REINICIAR", true, () => { preto.color = Color.black; pretoAlvo = 0; p.Reiniciar(); fimT = 0; });
                Botao("menu", r, new Vector2(0.5f, 0.5f), new Vector2(170, 0), new Vector2(320, 70), "MENU", false, () => { preto.color = Color.black; pretoAlvo = 0; p.ComecarVitrine(); EntrarMenu(); fimT = 0; });
            }
            return g;
        }

        // estatística de um jogador na partida (rounds fechados + o round em andamento)
        struct Linha { public string nome; public int k, a, d, mvp; public float adr, hs; }

        List<Linha> Linhas(Sim.Partida pt, Sim.Partida.Time tm, bool roundAberto)
        {
            var m = p.RoundAtual;
            var lst = new List<Linha>();
            foreach (var j in tm.js)
            {
                var l = new Linha { nome = j.nome, k = j.k, d = j.d };
                int hs = j.hs, dano = j.dano, rounds = j.rounds;
                if (roundAberto && m != null)
                {
                    var ag = m.agents.Find(x => x.name == j.nome);
                    if (ag != null) { l.k += ag.kills; l.d += ag.deaths; hs += ag.hs; dano += ag.damage; rounds++; }
                }
                foreach (var info in pt.historico) l.a += Assist(info.m, j.nome);
                if (roundAberto && m != null) l.a += Assist(m, j.nome);
                l.adr = rounds > 0 ? dano / (float)rounds : 0;
                l.hs = l.k > 0 ? 100f * hs / l.k : 0;
                foreach (var info in pt.historico) if (Mvp(info.m) == j.nome) l.mvp++;
                lst.Add(l);
            }
            return lst.OrderByDescending(x => x.k).ThenBy(x => x.d).ThenByDescending(x => x.adr).ToList();
        }

        static int Assist(Match m, string nome)
        {
            if (m == null) return 0;
            int n = 0;
            foreach (var ab in m.abates) if (ab.assist != null && ab.assist.name == nome && ab.by != null && ab.by != ab.v && !ab.aliado) n++;
            return n;
        }

        /// <summary>MVP do round: do time que ganhou, quem mais matou (desempate pelo dano); sem abate no time, ninguém.</summary>
        static string Mvp(Match m)
        {
            if (m == null || m.winner == null) return null;
            bool tr = m.winner == "TR";
            var best = m.agents.Where(a => a.IsT == tr).OrderByDescending(a => a.kills).ThenByDescending(a => a.damage).FirstOrDefault();
            return best != null && best.kills > 0 ? best.name : null;
        }

        void PreencherPlacar(bool fim)
        {
            var pt = p.PartidaAtual; if (pt == null) return;
            bool roundAberto = !fim;
            // lado de cada time agora (o CS2 põe o CT em cima)
            int rAgora = fim ? Mathf.Max(0, pt.r - 1) : pt.r;
            bool aT = Sim.Partida.ATerrorista(rAgora);
            var cima = aT ? pt.B : pt.A; var baixo = aT ? pt.A : pt.B;
            int rounds = pt.historico.Count;
            pTitulo.text = fim ? $"FIM DE PARTIDA  <color={Hex(LARANJA)}>{pt.vencedor?.nome} VENCEU</color>" : $"PLACAR  <color={Hex(CINZA)}>·  ROUND {rounds + 1}</color>";
            pSub.text = $"Vila  ·  {pt.A.nome} {pt.A.placar} x {pt.B.placar} {pt.B.nome}";
            PreencherBloco(blocos[0], pt, cima, false, roundAberto);
            PreencherBloco(blocos[1], pt, baixo, true, roundAberto);
            Historico(pt, cima);
        }

        void PreencherBloco(Bloco b, Sim.Partida pt, Sim.Partida.Time tm, bool trLado, bool roundAberto)
        {
            var cor = trLado ? TR : CT;
            b.circulo.color = cor; b.num.text = tm.placar.ToString();
            b.time.text = tm.nome; b.lado.text = trLado ? "TERRORISTAS" : "CONTRA-TERRORISTAS"; b.lado.color = Alfa(cor, 0.85f);
            var ls = Linhas(pt, tm, roundAberto);
            for (int i = 0; i < 5; i++)
            {
                var l = ls[i];
                b.fundo[i].color = Alfa(cor, i % 2 == 0 ? 0.16f : 0.09f);
                b.nome[i].text = l.nome;
                int saldo = l.k - l.d;
                string[] v = { l.k.ToString(), l.a.ToString(), l.d.ToString(),
                               saldo > 0 ? $"<color=#8BE79C>+{saldo}</color>" : saldo < 0 ? $"<color=#FF7A6E>{saldo}</color>" : "0",
                               l.adr.ToString("0"), l.hs.ToString("0") + "%", l.mvp > 0 ? $"<color=#FFC14D>{l.mvp}</color>" : "" };
                for (int c = 0; c < v.Length; c++) b.col[i, c].text = v[c];
            }
        }

        void Historico(Sim.Partida pt, Sim.Partida.Time cima)
        {
            var h = pt.historico;
            float larg = hist.sizeDelta.x;
            int n = Mathf.Max(24, h.Count);
            float passo = Mathf.Min(28, (larg - 20) / n);
            while (histIcones.Count < h.Count)
            {
                var ri = No("round", hist, new Vector2(0, 1), Vector2.zero, new Vector2(20, 20), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<RawImage>();
                ri.raycastTarget = false; histIcones.Add(ri);
            }
            for (int i = 0; i < histIcones.Count; i++) histIcones[i].gameObject.SetActive(i < h.Count);
            float X(int i) => passo * (i + 0.5f) + (i >= 12 ? 12 : 0) + (i >= 24 ? 12 * (1 + (i - 24) / 6) : 0);
            for (int i = 0; i < h.Count; i++)
            {
                var info = h[i];
                bool ganhouCima = info.vencedor == cima.nome;
                bool ladoTR = info.timeAvenceu ? info.ladoA == "TR" : info.ladoA != "TR";
                string ic = info.motivo == "bomb explodiu" ? "ui_bomba" : info.motivo == "bomb desarmado" ? "ui_alicate" : info.motivo == "tempo acabou" ? "ui_relogio" : "ui_caveira";
                var ri = histIcones[i];
                ri.texture = Resources.Load<Texture2D>("Icones/" + ic);
                float alto = 20, w = ri.texture != null ? alto * ri.texture.width / ri.texture.height : alto;
                ri.rectTransform.sizeDelta = new Vector2(Mathf.Min(w, passo - 2), alto);
                ri.color = ladoTR ? TR : CT;
                ri.rectTransform.anchoredPosition = new Vector2(X(i), ganhouCima ? -33 : -64);
            }
            hRotulo1.rectTransform.anchoredPosition = new Vector2(6, -2);
            hRotulo2.rectTransform.anchoredPosition = new Vector2(X(12) - passo * 0.5f + 6, -2);
            hRotulo2.gameObject.SetActive(h.Count > 12 || n > 12);
            histDivisa.rectTransform.anchoredPosition = new Vector2(X(12) - passo * 0.5f - 6, 0);
        }

        // ------------------------------------------------------------------ quadro
        public void Atualizar(float dt)
        {
            bool vit = p.Vitrine;
            if (vit && p.Acabou) p.ComecarVitrine();   // a partida de fundo acabou: outra
            if (vit != vitrineAntes) { if (vit) EntrarMenu(); vitrineAntes = vit; }
            p.Hud.canvas.enabled = !vit && fimT <= 3.5f;   // (no placar do fim a HUD sai)

            // menu
            menuG.alpha = Mathf.MoveTowards(menuG.alpha, vit ? 1 : 0, dt * 4);
            menuG.blocksRaycasts = menuG.interactable = vit;
            if (vit && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Iniciar();

            // placar: Tab segurado na partida; no fim, sozinho depois do banner de quem venceu
            bool fim = !vit && p.Acabou;
            fimT = fim ? fimT + dt : 0;
            bool fimPlacar = fimT > 3.5f;
            bool mostra = !vit && (forcarPlacar || Input.GetKey(KeyCode.Tab) || fimPlacar);
            if (mostra) PreencherPlacar(fim);
            placarG.alpha = Mathf.MoveTowards(placarG.alpha, mostra ? 1 : 0, dt * (mostra ? 8 : 6));
            botoesG.alpha = Mathf.MoveTowards(botoesG.alpha, fimPlacar ? 1 : 0, dt * 4);
            botoesG.blocksRaycasts = botoesG.interactable = fimPlacar;

            preto.color = new Color(0, 0, 0, Mathf.MoveTowards(preto.color.a, pretoAlvo, dt * 2.2f));
            vento.volume = Mathf.MoveTowards(vento.volume, vit ? 0.55f : 0, dt * (vit ? 0.5f : 1.5f));
        }
    }
}
