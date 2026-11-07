using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CSM.Map;
using CSM.Sim;
using static CSM.View3D.UiKit;
using Text = TMPro.TextMeshProUGUI;   // texto SDF (nítido em qualquer resolução); a API usada aqui é a mesma

namespace CSM.View3D
{
    /// <summary>
    /// HUD de transmissão do visual de maquete (uGUI numa câmera própria, sem pós-processamento): placar com relógio,
    /// C4 e vivos; killfeed; cards dos 10 jogadores (vida com barra de atraso, colete, dinheiro, arma, granadas, C4/kit);
    /// jogador observado com munição; banner de início e de fim de round; minimapa com os bonecos, C4, smokes e fogo.
    /// Tudo animado por tempo (entra deslizando, pisca no dano, pulsa no C4). Só lê a simulação.
    /// </summary>
    public class Hud3D
    {
        // tema escuro aprovado no mockup (referência IEM/PGL): grafite quente, texto creme, cor do time viva na vida
        static Color Hx(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static readonly Color CT = Hx("#5B9BFF"), TR = Hx("#F5A524");
        static readonly Color ESCURO = new Color(31 / 255f, 27 / 255f, 24 / 255f, 0.92f), CREME = Hx("#FFF4E2"), CINZA = new Color(1f, 0.957f, 0.886f, 0.45f);
        static readonly Color VERDE = Hx("#8BE79C"), VERMELHO = Hx("#E5382E");
        static readonly Color VIDA_TR = Hx("#D9831A"), VIDA_CT = Hx("#2F6FD6"), VIDA_BAIXA = Hx("#D63A2F"), RASTRO = Hx("#FF8A7A");
        static readonly Color CAIXA_TR = Hx("#A85A00"), CAIXA_CT = Hx("#1E4FB0"), PLACAR_TR = Hx("#FFB43C"), PLACAR_CT = Hx("#6FA8FF");
        static readonly Color VAZIO = new Color(1f, 0.957f, 0.886f, 0.07f);
        static Color Lado(bool t) => t ? TR : CT;
        static Color Escurece(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public readonly Camera cam; public readonly Canvas canvas; readonly RectTransform raiz;
        // tudo que é "jogo" (placar, cards, minimapa, killfeed, etiquetas) fica num grupo que some no replay
        RectTransform jogo; CanvasGroup jogoGrupo; readonly Camera principal;
        // etiqueta em cima de cada boneco: nome + vida
        class Etiqueta
        {
            public RectTransform r; public Text nome, venda; public Image barra, fundo; public RawImage c4; public float alfa, larg, alt, y, sobe, vendaT; public Vector2 base_;
            // compras do freeze: pilha por jogador (a mais nova embaixo, as anteriores sobem uma linha; nenhuma tapa a outra)
            public readonly Text[] compra = new Text[COMPRA_MAX]; public readonly float[] compraT = new float[COMPRA_MAX];
            public readonly int[] compraSeq = new int[COMPRA_MAX];
        }
        const float VENDA_DUR = 1.6f, COMPRA_DUR = 1.5f, COMPRA_LINHA = 22;
        const int COMPRA_MAX = 5;
        /// <summary>Comprou no freeze (Match.compras): o item sobe da etiqueta do boneco, em fila — quem compra 3-4 coisas
        /// seguidas mostra todas empilhadas, a mais nova embaixo.</summary>
        public void Comprou(int i, string item)
        {
            if (i < 0 || i >= 10 || string.IsNullOrEmpty(item)) return;
            var e = etiquetas[i];
            int livre = -1; float maisVelha = -1;
            for (int k = 0; k < COMPRA_MAX; k++)
            {
                if (e.compraT[k] <= 0) { livre = k; break; }
                float idade = COMPRA_DUR - e.compraT[k];
                if (idade > maisVelha) { maisVelha = idade; livre = k; }   // cheia: reaproveita a mais velha
            }
            e.compraT[livre] = COMPRA_DUR;
            e.compraSeq[livre] = ++compraSeq;
            var t = e.compra[livre];
            t.text = NomeItem(item); t.color = CorItem(item);
            t.rectTransform.anchoredPosition = new Vector2(0, 30);
        }
        int compraSeq;
        static string NomeItem(string it) => it switch
        {
            "colete" => "COLETE", "capacete" => "CAPACETE", "kit" => "KIT", "smoke" => "SMOKE", "flash" => "FLASH",
            "molotov" => "MOLOTOV", "incendiaria" => "INCENDIÁRIA", "he" => "HE", _ => NomeArma(it).ToUpper()
        };
        static Color CorItem(string it) => it switch
        {
            "smoke" or "flash" or "molotov" or "incendiaria" or "he" => CorNade(it),
            "kit" => PLACAR_CT, _ => CREME
        };
        /// <summary>Tempo de compra que falta (PartidaAoVivo3D): o relógio mostra a contagem do freeze.</summary>
        public float Freeze;
        /// <summary>Segundos desde o começo do round no visual (o freeze vem antes do t = 0 da Sim).</summary>
        float Ti(Match m) => m.t + Match.FREEZE - Freeze;
        /// <summary>Vendeu no tempo de compra: "$ MP9" verde sobe da etiqueta do boneco e some.</summary>
        public void Vendeu(int i, string item)
        {
            if (i < 0 || i >= 10) return;
            var e = etiquetas[i]; e.vendaT = VENDA_DUR; e.venda.text = "$ " + NomeArma(item);
        }
        readonly Etiqueta[] etiquetas = new Etiqueta[10];
        float tempo;

        // placar
        Image clutchSelo; Text clutchTxt; float clutchDesde = -1;
        // histórico de rounds embaixo do placar (como nas transmissões: quem ganhou cada round e como)
        RectTransform histFundo; Image histPainel; CanvasGroup histGrupo; Text histA, histB; float histK;
        readonly Image[,] histCel = new Image[2, 24]; readonly RawImage[,] histIco = new RawImage[2, 24];
        Image tileA, tileB, faixaA, faixaB, centro, barraC4; Text nomeA, nomeB, placarA, placarB, relogio, roundTxt; Image[] vivosA = new Image[5], vivosB = new Image[5];
        // killfeed
        class Linha { public RectTransform r; public Image fundo, borda; public readonly List<Graphic> partes = new List<Graphic>(); public float nasceu, y, vida; public Color cor; public bool temBorda; }
        readonly List<Linha> feed = new List<Linha>();
        // cards
        class Card
        {
            public RectTransform r; public Image fundo, hpBox, hpBarra, hpAtraso, borda; public Text hp, nome, arma, dinheiro, tags; public RawImage[] nades = new RawImage[4]; public RawImage c4Ico, kitIco;
            public RawImage icone, iconeFora, capacete, caveira; public Text slot;
            public Image vidaFundo, vidaLag; public RawImage[] kills = new RawImage[5];
            public float hpMostra = 100, hpLag = 100, lagEspera, flash; public int ultHp = 100;
        }
        readonly Card[] cardsA = new Card[5], cardsB = new Card[5];
        // observado
        RectTransform obs; Text obsNome, obsHp, obsColete, obsArma, obsMun; Image obsFundo; float obsAlfa; RawImage obsIcone, obsIcVida, obsIcColete;
        /// <summary>Modelo de onde saem os ícones de arma (as armas são as mesmas nos dois times).</summary>
        public Boneco3D.Modelo modeloArmas;
        /// <summary>Onde fica a cabeça de cada boneco no mundo (pra etiqueta de nome/vida).</summary>
        public System.Func<Agent, Vector3> Cabeca;
        static readonly Dictionary<string, Texture2D> icones = new Dictionary<string, Texture2D>();
        /// <summary>Ícone do HUD em Resources/Icones (armas pelo nome do modelo; interface com "ui_").</summary>
        static Texture2D Tex(string nome)
        {
            if (nome == null) return null;
            if (!icones.TryGetValue(nome, out var t)) icones[nome] = t = Resources.Load<Texture2D>("Icones/" + nome);
            return t;
        }
        // armas que o HUD desenha com ícone próprio (Tools/icones); o Armas3D.DoJogo mapeia algumas pra arma parecida enquanto
        // o modelo 3D não existe, e aí o ícone sairia errado (Tec-9 com cara de Glock): sem ícone próprio, mostra o nome
        /// <summary>Altura do ícone da arma em relação à de um fuzil: todas na mesma régua (antes o ícone se encaixava numa
        /// caixa e a pistola saía com a altura cheia, maior que a AWP). Pistola menor, SMG um pouco menor.</summary>
        public static float FatorArma(string gunId) => gunId switch
        {
            "glock" or "usp" or "p250" or "fiveseven" or "tec9" or "deagle" => 0.74f,
            "mp9" or "mac10" => 0.88f,
            _ => 1f
        };
        /// <summary>Tamanho do ícone da arma: altura = alto x FatorArma; se passar da largura máxima, encolhe inteiro.</summary>
        static Vector2 TamArma(Texture2D t, string gunId, float alto, float largMax)
        {
            float h = alto * FatorArma(gunId), w = h * t.width / t.height;
            if (w > largMax) { h *= largMax / w; w = largMax; }
            return new Vector2(w, h);
        }
        static string IconeProprio(string gunId) => gunId switch
        {
            "tec9" => "Tec9", "fiveseven" => "FiveSeven", "p250" => "P250", "galil" => "Galil", "famas" => "Famas", _ => null
        };
        Texture2D Icone(string gunId)
        {
            if (gunId == null) return null;
            var proprio = IconeProprio(gunId);
            if (proprio != null) return Tex(proprio);
            return Tex(Armas3D.DoJogo(gunId)) ?? (modeloArmas == null ? null : Armas3D.Icone(modeloArmas, Armas3D.DoJogo(gunId)));
        }
        // banners
        RectTransform banner; Image bannerFundo; Text bannerTitulo, bannerSub; float bannerDesde = -1; string bannerChave;
        // minimapa
        // radar no padrão da transmissão IEM: planta gerada do MapData (piso escuro, contorno claro, 2º andar mais claro,
        // escada listrada, caixotes cinza), A/B em quadrado amarelo, spawns verdes; jogador = gota com o número do slot
        // (o mesmo do card) apontando pra mira, morto = X na cor do time, C4 = ícone (na gota de quem carrega, no chão
        // quando cai, piscando vermelho plantada); embaixo, onde está o observado
        RawImage mapaImg; RectTransform mapaR; bool radarPronto;
        class Marca { public Image bola, ponta, anel, x1, x2; public Text num; public RawImage c4; }
        readonly Marca[] marcas = new Marca[10];
        RawImage c4Chao; Text mapaLocal;
        readonly Dictionary<string, int> slotDe = new Dictionary<string, int>();
        readonly List<Image> manchas = new List<Image>();
        const float MAPA_W = 340, MARCA_D = 17;
        static readonly Color MAPA_PISO = Hx("#2B2622"), MAPA_ALTO = Hx("#3D3631"), MAPA_LINHA = Hx("#D9CCB8"), MAPA_CAIXA = Hx("#8A8079");
        static readonly Color MAPA_SITE = Hx("#F2C200"), MAPA_SPAWN = new Color(0.16f, 0.55f, 0.3f, 0.85f), MARCA_TXT = Hx("#1A1714");
        static readonly Color MARCA_T = Hx("#F2C76B"), MARCA_CT = Hx("#8EC0F5"), C4_MAPA = Hx("#FF4A3D");
        // economia (começo do round)
        class LinhaEco { public Text nome, dinheiro, tags; public RawImage icone; public RawImage[] nades = new RawImage[4]; }
        RectTransform eco; Image ecoFundo; Text[] ecoTime = new Text[2], ecoTipo = new Text[2], ecoValor = new Text[2];
        readonly LinhaEco[,] ecoLinhas = new LinhaEco[2, 5];
        // replay
        Image faixaCima, faixaBaixo, replaySelo, legendaFundo; Text replayTxt, replayLegenda; float replayK; bool replayLigado;
        Image corte;   // preto do corte de câmera
        float corteT = 1;

        public Hud3D(Transform pai, Camera principal, RenderTexture minimapa)
        {
            var cg = new GameObject("Câmera HUD") { layer = CAMADA_UI };
            cg.transform.SetParent(pai, false);
            cam = cg.AddComponent<Camera>();
            // só pro print (Visual3D.Salvar): no Play o canvas é overlay, desenhado direto na tela depois das câmeras
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << CAMADA_UI; cam.depth = principal.depth + 1; cam.enabled = false;
            cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 100; cam.allowHDR = false; cam.allowMSAA = false;
            var cd = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            cd.renderPostProcessing = false; cd.renderShadows = false; cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            principal.cullingMask &= ~(1 << CAMADA_UI);

            var go = new GameObject("HUD", typeof(RectTransform)) { layer = CAMADA_UI };
            go.transform.SetParent(pai, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = cam; canvas.planeDistance = 5;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1;
            raiz = (RectTransform)go.transform;

            this.principal = principal;
            MontarPlacar(); MontarC4(); MontarCards(); MontarObservado(); MontarMapa(minimapa); MontarEconomia();
            jogo = No("jogo", raiz, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            jogo.anchorMin = Vector2.zero; jogo.anchorMax = Vector2.one; jogo.offsetMin = jogo.offsetMax = Vector2.zero;
            jogoGrupo = jogo.gameObject.AddComponent<CanvasGroup>(); jogoGrupo.blocksRaycasts = false; jogoGrupo.interactable = false;
            var filhos = new List<Transform>(); foreach (Transform f in raiz) if (f != jogo) filhos.Add(f);
            foreach (var f in filhos) f.SetParent(jogo, true);
            MontarEtiquetas();
            MontarBanner(); MontarReplay();
            corte = Painel("corte", raiz, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000), new Color(0, 0, 0, 0));
        }

        // ------------------------------------------------------------------ montagem
        void MontarPlacar()
        {
            var top = new Vector2(0.5f, 1);
            centro = Painel("relógio", raiz, top, new Vector2(0, -18), new Vector2(170, 78), ESCURO, new Vector2(0.5f, 1), true);
            relogio = Texto("relógio txt", centro.transform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(170, 50), "1:55", 44, CREME, TextAnchor.MiddleCenter, true, new Vector2(0.5f, 1));
            roundTxt = Texto("round", centro.transform, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(170, 20), "ROUND 1", 15, CINZA, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 0), false);
            barraC4 = Barra("c4", centro.transform, new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(150, 7), new Color(0, 0, 0, 0.4f), VERMELHO, false, new Vector2(0.5f, 1));
            barraC4.transform.parent.gameObject.SetActive(false);
            clutchSelo = Painel("clutch", raiz, top, new Vector2(0, -164), new Vector2(110, 32), new Color(0.98f, 0.85f, 0.35f, 0.95f), new Vector2(0.5f, 0.5f), true);
            clutchTxt = Texto("clutch txt", clutchSelo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 32), "1 v 1", 22, new Color(0.15f, 0.12f, 0.1f), TextAnchor.MiddleCenter, true, null, false);
            clutchSelo.gameObject.SetActive(false);
            foreach (bool a in new[] { true, false })
            {
                float sx = a ? -1 : 1;
                // bloco escuro com a cor do time só na faixa de baixo e no número (antes o bloco todo laranja/azul com
                // texto branco: pouco contraste)
                var tile = Painel(a ? "time A" : "time B", raiz, top, new Vector2(sx * 95, -22), new Vector2(270, 70), ESCURO, new Vector2(a ? 1 : 0, 1), true);
                var faixa = No("faixa", tile.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(0, 6), new Vector2(0.5f, 0)).gameObject.AddComponent<Image>();
                faixa.rectTransform.anchorMin = new Vector2(0, 0); faixa.rectTransform.anchorMax = new Vector2(1, 0); faixa.rectTransform.sizeDelta = new Vector2(-16, 6);
                faixa.raycastTarget = false; if (a) faixaA = faixa; else faixaB = faixa;
                var nome = Texto("nome", tile.transform, new Vector2(a ? 0 : 1, 0.5f), new Vector2(sx * -18, 0), new Vector2(170, 40), "TIME", 24, CREME, a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, false, new Vector2(a ? 0 : 1, 0.5f));
                var pl = Texto("placar", tile.transform, new Vector2(a ? 1 : 0, 0.5f), new Vector2(sx * 16, 0), new Vector2(70, 60), "0", 50, CREME, TextAnchor.MiddleCenter, true, new Vector2(a ? 1 : 0, 0.5f));
                var vivos = a ? vivosA : vivosB;
                for (int i = 0; i < 5; i++)
                    vivos[i] = Bola("vivo", raiz, top, new Vector2(sx * (110 + i * 22), -115), 15, CREME);   // abaixo da barra da C4 (8 de alto desde 2026-11-05)
                if (a) { tileA = tile; nomeA = nome; placarA = pl; } else { tileB = tile; nomeB = nome; placarB = pl; }
            }
            // histórico (como nas transmissões): fileira de cima = time A, de baixo = time B; cada round vencido mostra
            // como (caveira, bomba, alicate, relógio) na cor do lado que venceu; divisória na troca de lado
            const float CEL = 22, PASSO = 24;
            float largH = 56 + 24 * PASSO + 14;
            histPainel = Painel("histórico", raiz, top, new Vector2(0, -116), new Vector2(largH, 2 * PASSO + 12), ESCURO, new Vector2(0.5f, 1), true);
            histFundo = histPainel.rectTransform; histGrupo = histFundo.gameObject.AddComponent<CanvasGroup>();
            histA = Texto("A", histFundo, new Vector2(0, 1), new Vector2(10, -6), new Vector2(44, CEL), "", 15, PLACAR_TR, TextAnchor.MiddleLeft, false, new Vector2(0, 1), false);
            histB = Texto("B", histFundo, new Vector2(0, 1), new Vector2(10, -6 - PASSO), new Vector2(44, CEL), "", 15, PLACAR_CT, TextAnchor.MiddleLeft, false, new Vector2(0, 1), false);
            Painel("meio", histFundo, new Vector2(0, 1), new Vector2(56 + 12 * PASSO + 4, -6), new Vector2(2, 2 * PASSO - 2), new Color(1, 0.957f, 0.886f, 0.25f), new Vector2(0, 1));
            for (int lin = 0; lin < 2; lin++)
                for (int i = 0; i < 24; i++)
                {
                    float x = 56 + i * PASSO + (i >= 12 ? 14 : 0);
                    histCel[lin, i] = Painel("round", histFundo, new Vector2(0, 1), new Vector2(x, -6 - lin * PASSO), new Vector2(CEL, CEL), VAZIO, new Vector2(0, 1));
                    histIco[lin, i] = No("ícone", histCel[lin, i].transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16)).gameObject.AddComponent<RawImage>();
                    histIco[lin, i].raycastTarget = false;
                }
            histFundo.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ C4: barra embaixo do placar (padrão IEM)
        // plantada: o relógio vira o ícone vermelho da C4 e uma barra vermelha fina corre embaixo do placar inteiro,
        // encolhendo pro meio nos 40 s (pisca nos últimos 10). Plantando: a mesma barra em âmbar cresce do meio em
        // Match.PLANT. Desarmando: barra azul fina logo embaixo cresce do meio (com kit 5 s, sem kit 10 s); vermelha se
        // não dá tempo.
        // 2026-11-05 (usuário: "as barrinhas quase não dão pra ver", "tem que aparecer quanto tempo ela tá plantada",
        // defuse "tem que ser mais óbvio"): barra grossa com trilho escuro mostrando o que já passou, os segundos da C4
        // no relógio do lado do ícone, e uma etiqueta embaixo com quem planta/desarma (ícone, nome, kit, segundos e a
        // barra de progresso na cor da ação).
        const float C4_LARG = 730, C4_Y = -96, C4_ALT = 8, ACAO_Y = -129, ACAO_L = 400, ACAO_A = 44;
        Image c4Barra, c4Def, c4Trilho, acao, acaoFaixa, acaoCheio; RawImage relogioC4, acaoIco; Text acaoTxt, acaoSeg; float c4Alfa, acaoAlfa;
        static readonly Color C4_VERMELHO = Hx("#E5382E"), C4_AMBAR = Hx("#F5A524"), C4_AZUL = Hx("#5B9BFF");
        void MontarC4()
        {
            var top = new Vector2(0.5f, 1);
            Image Fina(string n, float y, float h)
            {
                var im = No(n, raiz, top, new Vector2(0, y), new Vector2(C4_LARG, h), new Vector2(0.5f, 1)).gameObject.AddComponent<Image>();
                im.raycastTarget = false; im.gameObject.SetActive(false); return im;
            }
            c4Trilho = Fina("c4 trilho", C4_Y, C4_ALT); c4Trilho.color = new Color(0.1f, 0.03f, 0.03f, 0.8f);
            c4Barra = Fina("c4 barra", C4_Y, C4_ALT);
            c4Def = Fina("c4 defuse", C4_Y - C4_ALT, C4_ALT);
            relogioC4 = No("c4 relógio", centro.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(60, 40)).gameObject.AddComponent<RawImage>();
            relogioC4.raycastTarget = false; relogioC4.gameObject.SetActive(false);
            // etiqueta da ação (plantando/desarmando), logo abaixo das bolinhas de vivos
            acao = Painel("c4 ação", raiz, top, new Vector2(0, ACAO_Y), new Vector2(ACAO_L, ACAO_A), ESCURO, new Vector2(0.5f, 1), true);
            acaoFaixa = Painel("faixa", acao.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, ACAO_A), C4_AMBAR, new Vector2(0, 0.5f));
            acaoIco = No("ícone", acao.transform, new Vector2(0, 0.5f), new Vector2(22, 3), new Vector2(26, 26), new Vector2(0, 0.5f)).gameObject.AddComponent<RawImage>();
            acaoIco.raycastTarget = false;
            acaoTxt = Texto("txt", acao.transform, new Vector2(0, 0.5f), new Vector2(60, 3), new Vector2(ACAO_L - 140, ACAO_A), "", 22, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 0.5f), false);
            acaoSeg = Texto("seg", acao.transform, new Vector2(1, 0.5f), new Vector2(-14, 3), new Vector2(80, ACAO_A), "", 26, CREME, TextAnchor.MiddleRight, true, new Vector2(1, 0.5f), false);
            acaoCheio = Barra("progresso", acao.transform, new Vector2(0.5f, 0), new Vector2(4, 0), new Vector2(ACAO_L - 8, 6), new Color(1, 1, 1, 0.12f), C4_AMBAR, false, new Vector2(0.5f, 0));
            acao.gameObject.SetActive(false);
        }

        /// <summary>Atualiza a barra da C4; devolve se está na tela.</summary>
        bool C4Barra(Match m, float dt)
        {
            Agent plantando = null, desarmando = null;
            if (!m.over)
                foreach (var a in m.agents)
                {
                    if (!a.alive) continue;
                    if (a.IsT && !m.bombPlanted && a.brain.action == "plantar") plantando = a;
                    if (!a.IsT && m.bombPlanted && m.defuserActive && a.brain.action == "desarmar") desarmando = a;
                }
            bool plantada = m.bombPlanted && !m.defused && !m.exploded;
            bool mostra = plantando != null || plantada;
            c4Alfa = Mathf.MoveTowards(c4Alfa, mostra ? 1 : 0, dt * 6);
            relogioC4.gameObject.SetActive(plantada);
            float pAcao = 0, segAcao = 0;
            if (plantada)
            {
                float left = Mathf.Max(0, m.BombLeft);
                float pisca = left < 10 ? 0.5f + 0.5f * Mathf.Sin(tempo * 16) : 0;
                // ícone à esquerda e os segundos à direita no relógio (o número é escrito no Atualizar)
                PorIcone(relogioC4, "ui_bomba", 36); relogioC4.color = Color.Lerp(C4_VERMELHO, Color.white, pisca * 0.5f);
                relogioC4.rectTransform.anchoredPosition = new Vector2(-36, 6);
                c4Barra.color = Color.Lerp(C4_VERMELHO, Color.white, pisca * 0.45f);
                c4Barra.rectTransform.sizeDelta = new Vector2(C4_LARG * left / Match.BOMB, C4_ALT);
            }
            else if (plantando != null)
            {
                pAcao = Mathf.Clamp01(1 - (plantando.brain.ActionEnd - m.t) / Match.PLANT); segAcao = Mathf.Max(0, plantando.brain.ActionEnd - m.t);
                c4Barra.color = C4_AMBAR; c4Barra.rectTransform.sizeDelta = new Vector2(C4_LARG * pAcao, C4_ALT);
            }
            c4Barra.gameObject.SetActive(c4Alfa > 0.01f && mostra);
            c4Trilho.gameObject.SetActive(c4Alfa > 0.01f && mostra);
            c4Trilho.color = new Color(0.1f, 0.03f, 0.03f, 0.8f * c4Alfa);
            bool def = plantada && desarmando != null;
            // a barra azul embaixo da vermelha batia nas bolinhas de vivos; o progresso do defuse fica na etiqueta
            c4Def.gameObject.SetActive(false);
            if (def)
            {
                float dur = Match.DefuseTime(desarmando), fim = m.plantedAt + Match.BOMB + m.DefuseFolga;
                pAcao = Mathf.Clamp01(1 - (fim - m.t) / dur); segAcao = Mathf.Max(0, fim - m.t);
                c4Def.color = m.DefuseVaiDar ? C4_AZUL : new Color(1f, 0.55f, 0.5f);
                c4Def.rectTransform.sizeDelta = new Vector2(C4_LARG * pAcao, C4_ALT);
            }
            // etiqueta: quem está plantando/desarmando, com kit ou não, segundos que faltam pra ação e o progresso
            var quem = def ? desarmando : plantando;
            acaoAlfa = Mathf.MoveTowards(acaoAlfa, quem != null ? 1 : 0, dt * 8);
            acao.gameObject.SetActive(acaoAlfa > 0.01f);
            if (quem != null)
            {
                bool vai = !def || m.DefuseVaiDar;
                Color cor = def ? (vai ? C4_AZUL : new Color(1f, 0.45f, 0.4f)) : C4_AMBAR;
                acaoFaixa.color = cor; acaoCheio.color = cor; acaoCheio.fillAmount = pAcao;
                // ícone numa caixa de no máx. 34 de largo (o alicate é largo e tapava o texto); o texto anda junto
                // usuário: sem escrever "com kit" — o alicate aparece só se tem kit; sem kit, sem ícone
                bool ico = !def || desarmando.hasKit;
                acaoIco.gameObject.SetActive(ico);
                float wIco = 0;
                if (ico)
                {
                    wIco = PorIcone(acaoIco, def ? "ui_alicate" : "ui_bomba", 26);
                    if (wIco > 34) { acaoIco.rectTransform.sizeDelta = new Vector2(34, 26 * 34 / wIco); wIco = 34; }
                    acaoIco.color = def ? C4_AZUL : C4_AMBAR;
                }
                acaoTxt.rectTransform.anchoredPosition = new Vector2(ico ? 22 + wIco + 12 : 24, 3);
                string verbo = def ? "DESARMANDO" : "PLANTANDO";
                acaoTxt.text = $"<b><color={Hex(cor)}>{verbo}</color></b>  {quem.name.ToUpper()}";
                acaoSeg.text = def && !vai ? "<color=#FF7A70>NÃO DÁ</color>" : $"{segAcao:0.0}s";
                acaoSeg.fontSize = def && !vai ? 20 : 26;
                // entra "saindo de trás" do placar e pulsa de leve a faixa
                acao.rectTransform.localScale = new Vector3(1, Mathf.Lerp(0.6f, 1, acaoAlfa), 1);
                acaoFaixa.color = Color.Lerp(cor, Color.white, 0.25f + 0.25f * Mathf.Sin(tempo * 9));
            }
            float clutchY = acaoAlfa > 0.01f ? ACAO_Y - ACAO_A - 26 : -164;
            clutchSelo.rectTransform.anchoredPosition = new Vector2(0, clutchY);
            return mostra;
        }

        void MontarCards()
        {
            // card de transmissão (tema aprovado): a VIDA enche o card inteiro na cor do time (o que perdeu fica escuro,
            // com rastro vermelho-claro do dano), número da vida no quadrado do lado de fora, nome com os abates do round,
            // dinheiro + capacete + C4/KIT + granadas embaixo, a arma principal do lado de dentro (pro meio da tela)
            foreach (bool a in new[] { true, false })
            {
                var cs = a ? cardsA : cardsB;
                var anc = new Vector2(a ? 0 : 1, 0);
                for (int i = 0; i < 5; i++)
                {
                    var c = new Card();
                    float sx = a ? 1 : -1, y = 24 + (4 - i) * 62;
                    c.borda = Painel("card borda", raiz, anc, new Vector2(sx * 22, y - 2), new Vector2(444, 60), new Color(1, 1, 1, 0), anc);
                    c.fundo = Painel("card", raiz, anc, new Vector2(sx * 24, y), new Vector2(440, 56), ESCURO, anc, true);
                    c.r = c.fundo.rectTransform;
                    var t = c.r;
                    var lado = new Vector2(a ? 0 : 1, 0.5f);
                    c.vidaLag = Barra("vida", t, lado, new Vector2(sx * 56, 0), new Vector2(384, 56), new Color(0, 0, 0, 0), RASTRO, !a, lado);
                    c.vidaFundo = No("vida cheia", c.vidaLag.transform.parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                    var vr = c.vidaFundo.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
                    c.vidaFundo.sprite = c.vidaLag.sprite; c.vidaFundo.type = Image.Type.Filled; c.vidaFundo.fillMethod = Image.FillMethod.Horizontal;
                    c.vidaFundo.fillOrigin = a ? 0 : 1; c.vidaFundo.raycastTarget = false;
                    c.hpBox = Painel("hp", t, lado, Vector2.zero, new Vector2(58, 56), CAIXA_TR, lado);
                    c.hpBox.sprite = null; c.hpBox.type = Image.Type.Simple;   // quadrado (usuário: o canto redondo do número ficava esquisito)
                    Raio(c.fundo, 3); Raio(c.borda, 3);                         // card com canto quase reto, casando com o número
                    c.hp = Texto("hp txt", c.hpBox.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 56), "100", 30, Color.white, TextAnchor.MiddleCenter, true);
                    c.caveira = No("caveira", c.hpBox.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 27)).gameObject.AddComponent<RawImage>();
                    c.caveira.texture = Tex("ui_caveira"); c.caveira.color = CINZA; c.caveira.raycastTarget = false;
                    // número do slot (o mesmo da gota no radar, como na IEM), no canto de cima do quadrado da vida
                    var qs = new Vector2(a ? 0 : 1, 1);
                    c.slot = Texto("slot", c.hpBox.transform, qs, new Vector2(sx * 4, -1), new Vector2(14, 15), "", 13, CINZA, a ? TextAnchor.UpperLeft : TextAnchor.UpperRight, true, qs, false);
                    var al = a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                    c.nome = Texto("nome", t, new Vector2(a ? 0 : 1, 1), new Vector2(sx * 70, -3), new Vector2(230, 30), "Jogador", 25, CREME, al, false, new Vector2(a ? 0 : 1, 1));
                    // kills do round = caveirinha por abate (como o CS2: icons/ui/kill embaixo do avatar); antes eram bolinhas
                    for (int k = 0; k < 5; k++)
                    {
                        c.kills[k] = No("kill", t, new Vector2(a ? 0 : 1, 1), Vector2.zero, new Vector2(14, 15), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<RawImage>();
                        c.kills[k].texture = Tex("ui_caveira"); c.kills[k].raycastTarget = false;
                    }
                    var pe = new Vector2(a ? 0 : 1, 0);
                    c.dinheiro = Texto("dinheiro", t, pe, new Vector2(sx * 70, 5), new Vector2(90, 24), "$0", 19, VERDE, al, true, pe, false);
                    c.capacete = No("capacete", t, pe, Vector2.zero, new Vector2(14, 17), new Vector2(0.5f, 0)).gameObject.AddComponent<RawImage>();
                    c.capacete.texture = Tex("ui_capacete"); c.capacete.raycastTarget = false;
                    c.tags = Texto("tags", t, pe, Vector2.zero, new Vector2(70, 24), "", 16, CREME, al, false, pe, false);
                    for (int k = 0; k < 4; k++) c.nades[k] = IconeUi("nade", t, pe, new Vector2(0.5f, 0));
                    c.c4Ico = IconeUi("c4", t, pe, new Vector2(0.5f, 0)); c.kitIco = IconeUi("kit", t, pe, new Vector2(0.5f, 0));
                    var dentro = new Vector2(a ? 1 : 0, 0.5f);
                    c.icone = No("arma", t, dentro, new Vector2(sx * -14, 0), new Vector2(100, 30), dentro).gameObject.AddComponent<RawImage>();
                    c.icone.raycastTarget = false;
                    c.iconeFora = c.icone;
                    c.arma = Texto("arma", t, dentro, new Vector2(sx * -14, 0), new Vector2(150, 24), "", 18, CINZA, a ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, false, dentro, false);
                    c.hpAtraso = c.vidaLag; c.hpBarra = c.vidaFundo;
                    cs[i] = c;
                }
            }
        }

        void MontarObservado()
        {
            obsFundo = Painel("observado", raiz, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(560, 92), ESCURO, new Vector2(0.5f, 0), true);
            obs = obsFundo.rectTransform;
            obsNome = Texto("nome", obs, new Vector2(0, 1), new Vector2(22, -8), new Vector2(300, 34), "", 26, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
            obsIcVida = No("ic vida", obs, new Vector2(0, 0), new Vector2(22, 20), new Vector2(20, 20), new Vector2(0, 0)).gameObject.AddComponent<RawImage>();
            obsIcVida.texture = Tex("ui_vida"); obsIcVida.raycastTarget = false;
            obsHp = Texto("hp", obs, new Vector2(0, 0), new Vector2(48, 10), new Vector2(100, 40), "", 34, CREME, TextAnchor.MiddleLeft, true, new Vector2(0, 0));
            obsIcColete = No("ic colete", obs, new Vector2(0, 0), new Vector2(140, 19), new Vector2(18, 22), new Vector2(0, 0)).gameObject.AddComponent<RawImage>();
            obsIcColete.texture = Tex("ui_capacete"); obsIcColete.raycastTarget = false;
            obsColete = Texto("colete", obs, new Vector2(0, 0), new Vector2(164, 12), new Vector2(100, 34), "", 26, CREME, TextAnchor.MiddleLeft, true, new Vector2(0, 0));
            obsArma = Texto("arma", obs, new Vector2(1, 1), new Vector2(-22, -12), new Vector2(200, 30), "", 20, CINZA, TextAnchor.MiddleRight, false, new Vector2(1, 1), false);
            // ícone da arma na linha de cima (a do nome), pequeno: a munição grande fica embaixo sem nada por cima
            obsIcone = No("ícone", obs, new Vector2(1, 1), new Vector2(-22, -12), new Vector2(100, 28), new Vector2(1, 1)).gameObject.AddComponent<RawImage>();
            obsIcone.raycastTarget = false;
            obsMun = Texto("munição", obs, new Vector2(1, 0), new Vector2(-22, 8), new Vector2(240, 44), "", 38, CREME, TextAnchor.MiddleRight, true, new Vector2(1, 0));
        }

        void MontarBanner()
        {
            bannerFundo = Painel("banner", raiz, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(820, 150), ESCURO, new Vector2(0.5f, 0.5f), true);
            banner = bannerFundo.rectTransform;
            bannerTitulo = Texto("título", banner, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(800, 80), "", 62, CREME);
            bannerSub = Texto("sub", banner, new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(800, 34), "", 26, CREME);
            banner.gameObject.SetActive(false);
        }

        void MontarMapa(RenderTexture rt)
        {
            float W = MAPA_W, H = W * MapData.NY / MapData.NX;
            // fundo translúcido (a planta é que desenha o mapa; o que não é chão fica vazado, como no radar da IEM)
            var f = Painel("minimapa", raiz, new Vector2(0, 1), new Vector2(22, -22), new Vector2(W + 12, H + 12 + 28), new Color(ESCURO.r, ESCURO.g, ESCURO.b, 0.62f), new Vector2(0, 1), true);
            Raio(f, 3);
            mapaImg = No("mapa", f.transform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(W, H), new Vector2(0.5f, 1)).gameObject.AddComponent<RawImage>();
            // até a planta ficar pronta (MapData montado), o radar antigo do Visual3D.RadarBatch; sem ele, a câmera de cima
            var radar = Tex("radar");
            mapaImg.texture = radar != null ? (Texture)radar : rt; mapaImg.raycastTarget = false; mapaImg.color = Color.white;
            mapaR = mapaImg.rectTransform;
            mapaLocal = Texto("local", f.transform, new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(W, 26), "", 18, CREME, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 0), false);
            c4Chao = IconeUi("c4 chão", mapaR, Vector2.zero, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 10; i++)
            {
                var k = new Marca();
                k.x1 = Painel("morto", mapaR, Vector2.zero, Vector2.zero, new Vector2(13, 3.2f), CREME, new Vector2(0.5f, 0.5f));
                k.x2 = Painel("morto", mapaR, Vector2.zero, Vector2.zero, new Vector2(13, 3.2f), CREME, new Vector2(0.5f, 0.5f));
                k.x1.rectTransform.localEulerAngles = new Vector3(0, 0, 45); k.x2.rectTransform.localEulerAngles = new Vector3(0, 0, -45);
                k.anel = Bola("observado", mapaR, Vector2.zero, Vector2.zero, MARCA_D + 5, Color.white);
                k.ponta = Painel("mira", mapaR, Vector2.zero, Vector2.zero, new Vector2(10, 10), CREME, new Vector2(0.5f, 0.5f));
                k.ponta.sprite = null; k.ponta.type = Image.Type.Simple;
                k.bola = Bola("jogador", mapaR, Vector2.zero, Vector2.zero, MARCA_D, CREME);
                k.num = Texto("número", k.bola.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(MARCA_D, MARCA_D), "", 12, MARCA_TXT, TextAnchor.MiddleCenter, true, null, false);
                k.c4 = IconeUi("c4", mapaR, Vector2.zero, new Vector2(0.5f, 0.5f));
                marcas[i] = k;
            }
        }

        /// <summary>Planta do radar no padrão IEM, gerada do MapData (2 unidades por pixel) + A/B e spawns por cima.</summary>
        void MontarRadar()
        {
            if (MapData.SITES.Count == 0) return;            // mapa ainda não montado: tenta no próximo quadro
            radarPronto = true;
            const int S = 2;
            int w = MapData.NX / S, h = (MapData.NY + S - 1) / S;
            bool Anda(int x, int y) => MapData.InMap(x, y) && (MapData.walk[x, y] || MapData.pisavel[x, y]);
            Color32 piso = MAPA_PISO, alto = MAPA_ALTO, linha = MAPA_LINHA, caixa = MAPA_CAIXA, nada = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h]; int chao = 0;
            for (int iy = 0; iy < h; iy++)
                for (int ix = 0; ix < w; ix++)
                {
                    int x = ix * S + 1, y = MapData.NY - 1 - (iy * S + 1);   // linha 0 da textura = fundo do mapa (y grande)
                    Color32 c = nada;
                    if (MapData.InMap(x, y))
                    {
                        if (MapData.coverH[x, y] > 0) c = caixa;
                        else if (Anda(x, y))
                        {
                            chao++;
                            if (!Anda(x - 3, y) || !Anda(x + 3, y) || !Anda(x, y - 3) || !Anda(x, y + 3)) c = linha;
                            else if (MapData.stair[x, y]) c = ((x + y) / 8) % 2 == 0 ? alto : piso;      // escada/rampa listrada
                            else c = MapData.floor[x, y] > MapData.GZ + 6 ? alto : piso;
                        }
                    }
                    px[iy * w + ix] = c;
                }
            if (chao == 0) return;                           // sem chão: fica o radar antigo
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "radar IEM" };
            t.SetPixels32(px); t.Apply(false, true);
            mapaImg.texture = t;
            // spawns (verde) e bombsites (quadrado amarelo com a letra), atrás das marcas
            foreach (var sp in new[] { MapData.SPAWN_T, MapData.SPAWN_CT })
            {
                if (sp == null || sp.Length == 0) continue;
                float x0 = sp.Min(q => q.x) - 14, x1 = sp.Max(q => q.x) + 14, y0 = sp.Min(q => q.y) - 14, y1 = sp.Max(q => q.y) + 14;
                Vector2 a = NoMapa(new V2(x0, y1)), b = NoMapa(new V2(x1, y0));
                var r = Painel("spawn", mapaR, Vector2.zero, a, b - a, MAPA_SPAWN, Vector2.zero); Raio(r, 2);
                r.transform.SetSiblingIndex(0);
            }
            foreach (var kv in MapData.SITES)
            {
                var q = Painel("bombsite " + kv.Key, mapaR, Vector2.zero, NoMapa(kv.Value.Center), new Vector2(25, 25), MAPA_SITE, new Vector2(0.5f, 0.5f)); Raio(q, 2);
                Texto("letra", q.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(25, 25), kv.Key, 20, MARCA_TXT, TextAnchor.MiddleCenter, true, null, false);
                q.transform.SetSiblingIndex(manchas.Count);   // por cima das smokes/fogo (que ficam no começo), atrás das marcas
            }
        }

        void MontarEconomia()
        {
            ecoFundo = Painel("economia", raiz, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(980, 318), ESCURO, new Vector2(0.5f, 0), true);
            eco = ecoFundo.rectTransform;
            for (int t = 0; t < 2; t++)
            {
                float x0 = t == 0 ? 20 : 500;
                ecoTime[t] = Texto("time", eco, new Vector2(0, 1), new Vector2(x0, -14), new Vector2(300, 30), "", 24, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
                ecoTipo[t] = Texto("tipo", eco, new Vector2(0, 1), new Vector2(x0 + 300, -12), new Vector2(160, 30), "", 20, CREME, TextAnchor.MiddleRight, true, new Vector2(0, 1));
                ecoValor[t] = Texto("valor", eco, new Vector2(0, 1), new Vector2(x0, -44), new Vector2(460, 20), "", 15, CINZA, TextAnchor.MiddleLeft, false, new Vector2(0, 1), false);
                for (int i = 0; i < 5; i++)
                {
                    var l = new LinhaEco(); float y = -76 - i * 46;
                    Painel("linha", eco, new Vector2(0, 1), new Vector2(x0, y), new Vector2(460, 40), new Color(1, 1, 1, 0.05f), new Vector2(0, 1));
                    l.nome = Texto("nome", eco, new Vector2(0, 1), new Vector2(x0 + 12, y), new Vector2(140, 40), "", 18, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 1));
                    l.icone = No("ícone", eco, new Vector2(0, 1), new Vector2(x0 + 150, y - 8), new Vector2(72, 24), new Vector2(0, 1)).gameObject.AddComponent<RawImage>();
                    l.icone.raycastTarget = false;
                    l.tags = Texto("tags", eco, new Vector2(0, 1), new Vector2(x0 + 245, y), new Vector2(80, 40), "", 15, CREME, TextAnchor.MiddleLeft, false, new Vector2(0, 1), false);
                    for (int k = 0; k < 4; k++) { l.nades[k] = IconeUi("nade", eco, new Vector2(0, 1), new Vector2(0.5f, 0.5f)); l.nades[k].rectTransform.anchoredPosition = new Vector2(x0 + 336 + k * 16, y - 20); }
                    l.dinheiro = Texto("dinheiro", eco, new Vector2(0, 1), new Vector2(x0 + 448, y), new Vector2(90, 40), "", 17, VERDE, TextAnchor.MiddleRight, true, new Vector2(1, 1), false);
                    ecoLinhas[t, i] = l;
                }
            }
            eco.gameObject.SetActive(false);
        }

        void MontarEtiquetas()
        {
            for (int i = 0; i < 10; i++)
            {
                var e = new Etiqueta();
                e.r = No("etiqueta", jogo, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 34), new Vector2(0.5f, 0));
                e.r.SetSiblingIndex(0);   // atrás do resto do HUD: boneco passando atrás do card não põe o nome por cima dele
                e.nome = Texto("nome", e.r, new Vector2(0.5f, 1), new Vector2(0, 4), new Vector2(160, 24), "", 22, CREME, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 1));
                Contorno(e.nome);
                e.fundo = Painel("barra fundo", e.r, new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(60, 8), new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0));
                e.barra = No("barra", e.fundo.transform, new Vector2(0, 0.5f), new Vector2(1, 0), new Vector2(58, 6), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>();
                e.barra.sprite = Redondo; e.barra.type = Image.Type.Sliced; e.barra.raycastTarget = false; Raio(e.barra, 3);
                e.venda = Texto("venda", e.r, new Vector2(0.5f, 1), new Vector2(0, 30), new Vector2(200, 26), "", 22, VERDE, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 1));
                Contorno(e.venda); e.venda.gameObject.SetActive(false);
                // quem carrega a C4: ícone dela do lado do nome (antes o texto "C4")
                e.c4 = No("c4", e.r, new Vector2(0.5f, 1), Vector2.zero, new Vector2(18, 18), new Vector2(0, 1)).gameObject.AddComponent<RawImage>();
                e.c4.raycastTarget = false; e.c4.gameObject.SetActive(false);
                for (int k = 0; k < COMPRA_MAX; k++)
                {
                    e.compra[k] = Texto("compra", e.r, new Vector2(0.5f, 1), new Vector2(0, 30), new Vector2(200, 24), "", 19, CREME, TextAnchor.MiddleCenter, true, new Vector2(0.5f, 1));
                    Contorno(e.compra[k]); e.compra[k].gameObject.SetActive(false);
                }
                etiquetas[i] = e;
            }
        }

        /// <summary>Nome e vida em cima de cada boneco vivo (posição da cabeça no mundo -> âncora na tela). Etiquetas que se
        /// encostam na tela empilham: a do boneco mais embaixo (mais perto da câmera) fica no lugar e a de trás sobe.</summary>
        public void Etiquetas(Match m, System.Func<Agent, Vector3> cabeca, float dt)
        {
            var tela = ((RectTransform)canvas.transform).rect.size;
            int n = 0;
            for (int i = 0; i < 10; i++)
            {
                var e = etiquetas[i];
                var a = i < m.agents.Count ? m.agents[i] : null;
                bool mostra = a != null && a.alive;
                Vector3 v = mostra ? principal.WorldToViewportPoint(cabeca(a)) : Vector3.zero;
                mostra &= v.z > 0 && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
                e.alfa = Mathf.MoveTowards(e.alfa, mostra ? 1 : 0, dt * 4);
                e.r.gameObject.SetActive(e.alfa > 0.01f);
                if (a == null || e.alfa <= 0.01f) continue;
                if (mostra) e.r.anchorMin = e.r.anchorMax = new Vector2(v.x, v.y);
                var cor = Lado(a.IsT);
                e.nome.text = a.name;
                e.c4.gameObject.SetActive(a.hasBomb);
                float wc4 = 0;
                if (a.hasBomb)
                {
                    wc4 = PorIcone(e.c4, "ui_bomba", 19) + 5;
                    e.c4.rectTransform.anchoredPosition = new Vector2(e.nome.preferredWidth / 2 + 5 - wc4 / 2, 1);
                    e.nome.rectTransform.anchoredPosition = new Vector2(-wc4 / 2, 4);   // nome + ícone centrados juntos
                    e.c4.color = new Color(1f, 0.42f, 0.37f, e.alfa);
                }
                else e.nome.rectTransform.anchoredPosition = new Vector2(0, 4);
                e.nome.color = new Color(cor.r * 0.55f + 0.45f, cor.g * 0.55f + 0.45f, cor.b * 0.55f + 0.45f, e.alfa);
                e.barra.rectTransform.sizeDelta = new Vector2(58 * Mathf.Clamp01(a.hp / 100f), 6);
                var cb = a.hp > 40 ? cor : VERMELHO; cb.a = e.alfa; e.barra.color = cb;
                e.fundo.color = new Color(0, 0, 0, 0.55f * e.alfa);
                // venda: sobe uns 34 px desacelerando e apaga no último 0,5 s
                e.vendaT = Mathf.Max(0, e.vendaT - dt);
                e.venda.gameObject.SetActive(e.vendaT > 0);
                if (e.vendaT > 0)
                {
                    float k = 1 - e.vendaT / VENDA_DUR;
                    e.venda.rectTransform.anchoredPosition = new Vector2(0, 30 + 34 * (1 - (1 - k) * (1 - k)));
                    var cv = VERDE; cv.a = e.alfa * Mathf.Clamp01(e.vendaT / 0.5f) * Mathf.Clamp01(k / 0.08f); e.venda.color = cv;
                }
                // compras: linha pela ordem de chegada (a mais nova embaixo; as anteriores sobem uma linha, andando suave),
                // cada uma entra subindo 10 px e apaga no último 0,4 s
                int ativas = 0;
                for (int c = 0; c < COMPRA_MAX; c++)
                {
                    e.compraT[c] = Mathf.Max(0, e.compraT[c] - dt);
                    if (e.compraT[c] > 0) ativas++;
                    e.compra[c].gameObject.SetActive(e.compraT[c] > 0);
                }
                for (int c = 0; c < COMPRA_MAX; c++)
                {
                    if (e.compraT[c] <= 0) continue;
                    int maisNovas = 0;
                    for (int o = 0; o < COMPRA_MAX; o++) if (e.compraT[o] > 0 && e.compraSeq[o] > e.compraSeq[c]) maisNovas++;
                    float idade = COMPRA_DUR - e.compraT[c], ent = Mathf.Clamp01(idade / 0.18f);
                    var rt = e.compra[c].rectTransform;
                    float alvoY = 30 + maisNovas * COMPRA_LINHA + 10 * (1 - (1 - ent) * (1 - ent));
                    rt.anchoredPosition = new Vector2(0, Mathf.Lerp(rt.anchoredPosition.y, alvoY, dt > 0 ? 1 - Mathf.Exp(-dt * 16) : 1));
                    var cc = e.compra[c].color; cc.a = e.alfa * ent * Mathf.Clamp01(e.compraT[c] / 0.4f); e.compra[c].color = cc;
                }
                // venda sobe por cima da pilha de compras, se houver
                if (e.vendaT > 0 && ativas > 0) e.venda.rectTransform.anchoredPosition += new Vector2(0, ativas * COMPRA_LINHA);
                var anc = e.r.anchorMin;
                e.base_ = new Vector2(anc.x * tela.x, anc.y * tela.y); e.larg = Mathf.Max(64, e.nome.preferredWidth + 10 + wc4);
                // a pilha de compras/venda faz parte da etiqueta: a de trás sobe por cima dela, não por cima do nome
                e.alt = ALT_ETIQUETA + ativas * COMPRA_LINHA + (e.vendaT > 0 ? COMPRA_LINHA + 4 : 0);
                for (int c = 0; c < COMPRA_MAX; c++) if (e.compraT[c] > 0) e.larg = Mathf.Max(e.larg, e.compra[c].preferredWidth + 10);
                visiveis[n++] = e;
            }
            // empilha de baixo pra cima: cada etiqueta sobe até não encostar em nenhuma já posta
            for (int k = 1; k < n; k++)   // ordena pela altura na tela (no máximo 10: inserção)
                for (int j = k; j > 0 && visiveis[j].base_.y < visiveis[j - 1].base_.y; j--) (visiveis[j], visiveis[j - 1]) = (visiveis[j - 1], visiveis[j]);
            for (int k = 0; k < n; k++)
            {
                var e = visiveis[k]; float y = e.base_.y;
                for (int volta = 0; volta < n; volta++)
                {
                    bool mexeu = false;
                    for (int j = 0; j < k; j++)
                    {
                        var o = visiveis[j];
                        if (Mathf.Abs(o.base_.x - e.base_.x) < (o.larg + e.larg) * 0.5f && y < o.y + o.alt && y > o.y - e.alt)
                        { y = o.y + o.alt; mexeu = true; }
                    }
                    if (!mexeu) break;
                }
                e.y = y;
                // anda até o lugar novo em ~0,1 s (sem pular de um quadro pro outro quando dois se cruzam)
                e.sobe = dt > 0 ? Mathf.Lerp(e.sobe, y - e.base_.y, 1 - Mathf.Exp(-dt * 14)) : y - e.base_.y;
                e.r.anchoredPosition = new Vector2(0, 6 + e.sobe);
            }
        }
        const float ALT_ETIQUETA = 36;   // nome (24) + barra (8) + folga
        readonly Etiqueta[] visiveis = new Etiqueta[10];

        void MontarReplay()
        {
            faixaCima = Painel("faixa", raiz, new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 0), Color.black, new Vector2(0.5f, 1));
            faixaBaixo = Painel("faixa", raiz, new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 0), Color.black, new Vector2(0.5f, 0));
            replaySelo = Painel("replay", raiz, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(170, 38), VERMELHO, new Vector2(0.5f, 0.5f), true);
            replayTxt = Texto("txt", replaySelo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 38), "REPLAY", 22, Color.white, TextAnchor.MiddleCenter, true);
            legendaFundo = Painel("legenda fundo", raiz, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(600, 58), ESCURO, new Vector2(0.5f, 0.5f), true);
            replayLegenda = Texto("legenda", legendaFundo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 58), "", 30, CREME, TextAnchor.MiddleCenter);
            replaySelo.gameObject.SetActive(false); legendaFundo.gameObject.SetActive(false);
        }

        /// <summary>Liga o modo replay (faixas de cinema, selo piscando, legenda do lance).</summary>
        /// <summary>Faixas de cinema + selo (REPLAY / MELHORES MOMENTOS) + legenda do lance.</summary>
        public void Replay(bool ligado, string legenda = "", string selo = "REPLAY")
        {
            replayLigado = ligado;
            if (!ligado) return;
            replayLegenda.text = legenda;
            replayTxt.text = selo;
            float w = replayTxt.GetPreferredValues(selo).x + 40;
            replaySelo.rectTransform.sizeDelta = new Vector2(Mathf.Max(170, w), 38); replayTxt.rectTransform.sizeDelta = new Vector2(Mathf.Max(170, w), 38);
        }

        static int Preco(Agent a)
        {
            int v = WeaponStats.ALL.TryGetValue(a.gunId, out var st) ? st.price : 0;
            if (a.armor > 0) v += a.helmet ? 1000 : 650;
            if (!a.IsT && a.hasKit) v += 400;
            foreach (var n in a.nades) v += n == "flash" ? 200 : n == "molotov" ? 400 : n == "incendiaria" ? 500 : 300;
            return v;
        }

        /// <summary>Gravação de demonstração (-v3d_semeco): esconde o painel de economia do começo do round.</summary>
        public static bool SemEconomia;
        void Economia(Sim.Partida pt, Match m, int rn, bool aT, Dictionary<string, Sim.Partida.Jogador> jog, float dt)
        {
            bool mostra = !m.over && Ti(m) > 0.3f && Ti(m) < 7.5f && !replayLigado && !SemEconomia;
            float alvo = mostra ? 1 : 0;
            ecoK = Mathf.MoveTowards(ecoK, alvo, dt * (mostra ? 3.5f : 4f));
            eco.gameObject.SetActive(ecoK > 0.01f);
            if (ecoK <= 0.01f) return;
            eco.anchoredPosition = new Vector2(0, 150 - (1 - SaiDeTras(ecoK)) * 60);
            eco.localScale = Vector3.one * Mathf.Lerp(0.92f, 1, ecoK);
            ecoFundo.color = new Color(ESCURO.r, ESCURO.g, ESCURO.b, ESCURO.a * ecoK);
            for (int t = 0; t < 2; t++)
            {
                var time = t == 0 ? pt.A : pt.B; bool tr = t == 0 ? aT : !aT;
                int total = 0, n = 0;
                for (int i = 0; i < 5; i++)
                {
                    var j = time.js[i]; var a = m.agents.FirstOrDefault(x => x.name == j.nome); var l = ecoLinhas[t, i];
                    if (a == null) continue;
                    int v = Preco(a); total += v; n++;
                    l.nome.text = a.name;
                    var ic = Icone(a.Primaria ?? a.gunId); l.icone.texture = ic; l.icone.gameObject.SetActive(ic != null);
                    if (ic != null) l.icone.rectTransform.sizeDelta = TamArma(ic, a.Primaria ?? a.gunId, 24, 92);
                    // as linhas entram uma por uma
                    float k = Mathf.Clamp01((Ti(m) - 0.3f - i * 0.08f - t * 0.04f) / 0.3f) * ecoK;
                    l.nome.color = new Color(CREME.r, CREME.g, CREME.b, k); l.icone.color = new Color(1, 1, 1, k);
                    l.tags.text = (a.armor > 0 ? (a.helmet ? "COLETE+CAP " : "COLETE ") : "") + (!a.IsT && a.hasKit ? "<color=#9CC2FF>KIT</color> " : "") + (a.hasBomb ? "<color=#FF6A5E>C4</color>" : "");
                    l.tags.color = new Color(1, 1, 1, k);
                    for (int q = 0; q < 4; q++) { bool tem = q < a.nades.Count; l.nades[q].gameObject.SetActive(tem); if (tem) { PorIcone(l.nades[q], "nade_" + a.nades[q], 20); l.nades[q].color = new Color(1, 1, 1, k); } }
                    l.dinheiro.text = $"${j.dinheiro:N0}".Replace(",", "."); l.dinheiro.color = new Color(VERDE.r, VERDE.g, VERDE.b, k);
                }
                int media = n > 0 ? total / n : 0;
                string tipo = rn == 1 || rn == 13 ? "PISTOL" : media >= 3800 ? "FULL BUY" : media >= 1800 ? "FORCE" : "ECO";
                ecoTime[t].text = $"<color={Hex(Lado(tr))}>{time.nome.ToUpper()}</color>  <size=16>{(tr ? "TR" : "CT")}</size>";
                ecoTipo[t].text = tipo; ecoTipo[t].color = tipo == "ECO" ? CINZA : tipo == "FORCE" ? new Color(1f, 0.8f, 0.35f) : tipo == "PISTOL" ? CREME : VERDE;
                ecoValor[t].text = $"equipamento ${total:N0}".Replace(",", ".") + $"   ·   no banco ${time.js.Sum(j => j.dinheiro):N0}".Replace(",", ".");
            }
        }
        float ecoK;

        void ReplayHud(float dt)
        {
            replayK = Mathf.MoveTowards(replayK, replayLigado ? 1 : 0, dt * 3);
            float h = SaiRapido(replayK) * 92;
            faixaCima.rectTransform.sizeDelta = new Vector2(4000, h); faixaBaixo.rectTransform.sizeDelta = new Vector2(4000, h);
            faixaCima.gameObject.SetActive(h > 0.5f); faixaBaixo.gameObject.SetActive(h > 0.5f);
            replaySelo.gameObject.SetActive(replayLigado); legendaFundo.gameObject.SetActive(replayLigado);
            // no replay o HUD de jogo sai (a cena fica limpa entre as faixas) e volta junto com o round novo
            jogoGrupo.alpha = 1 - SaiRapido(replayK);
            if (replayLigado)
            {
                replaySelo.rectTransform.anchoredPosition = new Vector2(0, -92 - 34);
                replaySelo.color = Color.Lerp(VERMELHO, new Color(0.6f, 0.1f, 0.1f), 0.5f + 0.5f * Mathf.Sin(tempo * 5));
                replayLegenda.color = new Color(CREME.r, CREME.g, CREME.b, replayK);
                legendaFundo.rectTransform.sizeDelta = new Vector2(replayLegenda.preferredWidth + 60, 58);
                legendaFundo.rectTransform.anchoredPosition = new Vector2(0, 92 + 50 - (1 - SaiDeTras(replayK)) * 40);
                legendaFundo.color = new Color(ESCURO.r, ESCURO.g, ESCURO.b, ESCURO.a * replayK);
            }
        }

        Vector2 NoMapa(V2 p) => new Vector2(p.x / MapData.NX * mapaR.sizeDelta.x, (1 - p.y / MapData.NY) * mapaR.sizeDelta.y);

        // ------------------------------------------------------------------ por quadro
        /// <summary>Corte seco de câmera (o diretor trocou de lugar longe): pisca preto rápido.</summary>
        public void Corte() => corteT = 0;

        public void Atualizar(Sim.Partida pt, Match m, int rn, int seguir, float dt)
        {
            tempo += dt;
            bool aT = Sim.Partida.ATerrorista(pt.r);
            var jog = new Dictionary<string, Sim.Partida.Jogador>();
            foreach (var j in pt.A.js) jog[j.nome] = j;
            foreach (var j in pt.B.js) jog[j.nome] = j;

            // placar
            tileA.color = ESCURO; tileB.color = ESCURO; faixaA.color = Lado(aT); faixaB.color = Lado(!aT);
            placarA.color = aT ? PLACAR_TR : PLACAR_CT; placarB.color = !aT ? PLACAR_TR : PLACAR_CT;
            nomeA.text = pt.A.nome.ToUpper(); nomeB.text = pt.B.nome.ToUpper();
            // fim de round: o ponto já entra no placar (a Partida só soma ao fechar o round), com um pulo
            // o ponto do round que acabou ainda não entrou no placar só enquanto o round está aberto (pt.r = rounds fechados);
            // no último round o Partida já somou e fica parado com m.over: somar de novo dava 7×14 num 13×7
            bool aberto = pt.r < rn;
            bool ganhouA = m.over && aberto && (m.winner == "TR") == aT, ganhouB = m.over && aberto && !ganhouA;
            placarA.text = (pt.A.placar + (ganhouA ? 1 : 0)).ToString(); placarB.text = (pt.B.placar + (ganhouB ? 1 : 0)).ToString();
            float pulo = m.over ? Mathf.Clamp01(1 - (tempo - bannerDesde) / 0.6f) : 0;
            placarA.rectTransform.localScale = Vector3.one * (1 + (ganhouA ? 0.35f * Mathf.Sin(pulo * Mathf.PI) : 0));
            placarB.rectTransform.localScale = Vector3.one * (1 + (ganhouB ? 0.35f * Mathf.Sin(pulo * Mathf.PI) : 0));
            roundTxt.text = $"ROUND {rn}";
            if (m.bombPlanted && !m.defused && !m.exploded)
            {
                // plantada: o relógio vira o ícone da C4 piscando (como nas transmissões); o tempo fica na bolinha embaixo
                float left = Mathf.Max(0, m.BombLeft);
                // segundos da C4 do lado do ícone (usuário quer ver quanto falta); nos últimos 10 s com décimo
                relogio.text = left < 10 ? $"{left:0.0}" : $"{Mathf.CeilToInt(left)}";
                relogio.rectTransform.anchoredPosition = new Vector2(26, -6);
                float pulso = left < 10 ? 0.5f + 0.5f * Mathf.Sin(tempo * 14) : 0.5f + 0.5f * Mathf.Sin(tempo * 5);
                centro.color = Color.Lerp(new Color(0.45f, 0.08f, 0.08f, 0.92f), new Color(0.75f, 0.12f, 0.1f, 0.95f), pulso);
                barraC4.transform.parent.gameObject.SetActive(false);
                relogio.rectTransform.localScale = Vector3.one;
            }
            else if (Freeze > 0)
            {
                // tempo de compra: conta o freeze em verde (como o "buy time" do CS)
                relogio.text = $"0:{Mathf.CeilToInt(Freeze):00}";
                relogio.rectTransform.anchoredPosition = new Vector2(0, -6);
                centro.color = new Color(0.12f, 0.32f, 0.18f, 0.92f);
                barraC4.transform.parent.gameObject.SetActive(false);
                relogio.rectTransform.localScale = Vector3.one;
            }
            else
            {
                float left = Mathf.Max(0, m.RoundLeft);
                relogio.text = $"{(int)left / 60}:{(int)left % 60:00}";
                relogio.rectTransform.anchoredPosition = new Vector2(0, -6);
                centro.color = left < 10 && !m.over ? Color.Lerp(ESCURO, new Color(0.5f, 0.1f, 0.1f, 0.9f), 0.5f + 0.5f * Mathf.Sin(tempo * 8)) : ESCURO;
                barraC4.transform.parent.gameObject.SetActive(false);
                relogio.rectTransform.localScale = Vector3.one;
            }
            var vivosT = m.agents.Count(x => x.IsT && x.alive); var vivosCT = m.agents.Count(x => !x.IsT && x.alive);
            int va = aT ? vivosT : vivosCT, vb = aT ? vivosCT : vivosT;
            for (int i = 0; i < 5; i++)
            {
                vivosA[i].color = i < va ? Lado(aT) : new Color(1, 1, 1, 0.18f);
                vivosB[i].color = i < vb ? Lado(!aT) : new Color(1, 1, 1, 0.18f);
            }
            // clutch: um lado com 1 vivo contra 1+ — selo embaixo do relógio
            bool clutch = !m.over && va > 0 && vb > 0 && (va == 1 || vb == 1);
            if (clutch && clutchDesde < 0) clutchDesde = tempo;
            if (!clutch) clutchDesde = -1;
            C4Barra(m, dt);
            clutchSelo.gameObject.SetActive(clutch);
            if (clutch)
            {
                clutchTxt.text = $"{va} v {vb}";
                clutchSelo.rectTransform.localScale = Vector3.one * SaiDeTras((tempo - clutchDesde) / 0.35f);
            }

            Feed(m, seguir, dt);
            Historico(pt, m, rn);
            Cards(pt, m, aT, jog, seguir, dt);
            Observado(m, seguir, dt);
            if (Cabeca != null) Etiquetas(m, Cabeca, dt);
            Banners(pt, m, rn, aT);
            Economia(pt, m, rn, aT, jog, dt);
            ReplayHud(dt);
            Minimapa(m, seguir);
            corteT += dt;
            corte.color = new Color(0, 0, 0, Mathf.Clamp01(1 - corteT / 0.22f) * 0.9f);
        }

        // ------------------------------------------------------------------ killfeed (padrão CS2: huddeathnotice.xml/.css)
        // ordem da linha: [cego] MATADOR [+ (flash) ASSISTENTE] [no ar] ARMA [sem luneta][smoke][parede][HS] VÍTIMA;
        // a mais nova entra EMBAIXO; vive 5 s (x1,5 se envolve o jogador observado) e some em 1 s; o abate do observado
        // tem borda vermelha e fundo preto, a morte dele fundo vermelho. Tamanhos do CS2 (1080p) x1,3 pra transmissão.
        static readonly Color KF_T = Hx("#eabe54"), KF_CT = Hx("#6f9ce6"), KF_FUNDO = new Color(0.07f, 0.065f, 0.06f, 0.78f);
        static readonly Color KF_MATOU = new Color(0, 0, 0, 0.9f), KF_MORREU = Hx("#630606D2"), KF_BORDA = Hx("#E10000");
        const int KF_TXT = 20;
        const float KF_ALT = 38, KF_ICO = 27, KF_ICO_P = 23, KF_VIDA = 5, KF_SOME = 1, KF_PASSO = 4;
        const int KF_MAX = 6;
        Match feedMatch; int abVisto;
        void Feed(Match m, int seguir, float dt)
        {
            if (m != feedMatch || m.abates.Count < abVisto) { feedMatch = m; abVisto = 0; }
            for (; abVisto < m.abates.Count; abVisto++)
            {
                var ab = m.abates[abVisto];
                var l = new Linha { nasceu = tempo };
                l.borda = Painel("kill borda", jogo, new Vector2(1, 1), Vector2.zero, new Vector2(300, KF_ALT + 4), KF_BORDA, new Vector2(1, 1)); Raio(l.borda, 4);
                l.fundo = Painel("kill", jogo, new Vector2(1, 1), Vector2.zero, new Vector2(300, KF_ALT), KF_FUNDO, new Vector2(1, 1)); Raio(l.fundo, 3);
                l.r = l.fundo.rectTransform;
                bool matou = seguir >= 0 && ab.by != null && ab.by.id == seguir && ab.by != ab.v, morreu = seguir >= 0 && ab.v.id == seguir;
                l.cor = matou ? KF_MATOU : morreu ? KF_MORREU : KF_FUNDO; l.temBorda = matou;
                l.vida = KF_VIDA * (matou || morreu ? 1.5f : 1);
                MontarLinha(l, ab);
                feed.Add(l);
            }
            while (feed.Count > KF_MAX) { Apagar(feed[0]); feed.RemoveAt(0); }   // estourou: sai a mais velha
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var l = feed[i]; float idade = tempo - l.nasceu;
                if (idade > l.vida + KF_SOME) { Apagar(l); feed.RemoveAt(i); continue; }
                float alvoY = -22 - i * (KF_ALT + KF_PASSO);
                l.y = idade < 0.01f ? alvoY : Mathf.Lerp(l.y, alvoY, 1 - Mathf.Exp(-14 * dt));   // sobe 0,2 s quando sai uma de cima
                float entra = Mathf.Clamp01(idade / 0.18f), sai = Mathf.Clamp01((l.vida + KF_SOME - idade) / KF_SOME), a = entra * sai;
                l.r.anchoredPosition = new Vector2(-22 + (1 - entra) * 24, l.y);
                l.fundo.color = new Color(l.cor.r, l.cor.g, l.cor.b, l.cor.a * a);
                l.borda.gameObject.SetActive(l.temBorda);
                if (l.temBorda) { l.borda.rectTransform.anchoredPosition = l.r.anchoredPosition + new Vector2(2, 2); l.borda.rectTransform.sizeDelta = l.r.sizeDelta + new Vector2(4, 4); l.borda.color = new Color(KF_BORDA.r, KF_BORDA.g, KF_BORDA.b, a); }
                foreach (var g in l.partes) { var cc = g.color; cc.a = a; g.color = cc; }
            }
        }
        static void Apagar(Linha l)
        {
            foreach (var go in new[] { l.r.gameObject, l.borda.gameObject })
                if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        /// <summary>RawImage vazio pra um ícone de Resources/Icones (o tamanho vem do PorIcone, pela proporção da textura).</summary>
        RawImage IconeUi(string nome, Transform pai, Vector2 ancora, Vector2 pivo)
        {
            var ri = No(nome, pai, ancora, Vector2.zero, new Vector2(10, 10), pivo).gameObject.AddComponent<RawImage>();
            ri.raycastTarget = false; ri.gameObject.SetActive(false); return ri;
        }
        /// <summary>Põe a textura e acerta o tamanho pela altura (largura pela proporção). Devolve a largura.</summary>
        static float PorIcone(RawImage ri, string tex, float h)
        {
            var t = Tex(tex); ri.texture = t;
            float w = t == null ? h * 0.6f : h * t.width / t.height;
            ri.rectTransform.sizeDelta = new Vector2(w, h); return w;
        }

        /// <summary>Ícone do que matou (arma, granada, C4, faca) — null se não tem desenho.</summary>
        Texture2D IconeAbate(string arma) => arma switch
        {
            null => null,
            "he" or "flash" or "smoke" or "molotov" or "incendiaria" => Tex("nade_" + arma),
            "fogo" => Tex("nade_molotov"),
            "c4" or "bomb" or "explosão" => Tex("ui_bomba"),
            _ => Icone(arma)
        };

        void MontarLinha(Linha l, Match.Abate ab)
        {
            float x = 12;
            void Txt(string t, Color c)
            {
                var tx = Texto("txt", l.r, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(10, KF_ALT), t, KF_TXT, c, TextAnchor.MiddleLeft, true, new Vector2(0, 0.5f), false);
                float w = Mathf.Min(tx.preferredWidth, 210); tx.rectTransform.sizeDelta = new Vector2(w, KF_ALT); x += w + 6; l.partes.Add(tx);
            }
            void Ico(Texture2D t, float h, float dy = 0)
            {
                if (t == null) return;
                float w = h * t.width / t.height;
                var ri = No("ícone", l.r, new Vector2(0, 0.5f), new Vector2(x, dy), new Vector2(w, h), new Vector2(0, 0.5f)).gameObject.AddComponent<RawImage>();
                ri.texture = t; ri.color = Color.white; ri.raycastTarget = false; l.partes.Add(ri); x += w + 6;
            }
            Color Cor(Agent a) => a.IsT ? KF_T : KF_CT;
            bool temMatador = ab.by != null && !ab.suicidio;
            if (temMatador)
            {
                if (ab.cegoQuemMatou) Ico(Tex("kf_cego"), KF_ICO_P);
                Txt(ab.by.name, Cor(ab.by));
                if (ab.assist != null)
                {
                    Txt("+", new Color(1, 1, 1, 0.85f));
                    if (ab.flashAssist) Ico(Tex("nade_flash"), KF_ICO_P);
                    Txt(ab.assist.name, Cor(ab.assist));
                }
                if (ab.noAr) Ico(Tex("kf_noar"), KF_ICO_P, 4);
            }
            var arma = IconeAbate(ab.arma);
            if (arma != null) Ico(arma, KF_ICO * FatorArma(ab.arma));
            else if (!string.IsNullOrEmpty(ab.arma) && temMatador) Txt(NomeArma(ab.arma), new Color(0.85f, 0.82f, 0.77f));
            if (ab.semLuneta) Ico(Tex("kf_semluneta"), KF_ICO_P);
            if (ab.atravesSmoke) Ico(Tex("kf_smoke"), KF_ICO_P);
            if (ab.parede) Ico(Tex("kf_parede"), KF_ICO_P);
            if (ab.hs) Ico(Tex("ui_hs"), KF_ICO_P);
            if (!temMatador && arma == null) Ico(Tex("ui_caveira"), KF_ICO_P);   // suicídio / queda: caveira no lugar da arma
            Txt(ab.v.name, Cor(ab.v));
            l.r.sizeDelta = new Vector2(x + 6, KF_ALT);
        }

        /// <summary>"Fulano [AK] Ciclano (HS)" -> com cores do lado de cada um e a arma no meio.</summary>
        static string Colore(Match m, string s)
        {
            string Cor(string nome) { var a = m.agents.FirstOrDefault(x => x.name == nome); return a == null ? Hex(CREME) : Hex(Lado(a.IsT)); }
            int a0 = s.IndexOf(" ["), a1 = s.IndexOf("] ");
            if (a0 > 0 && a1 > a0)
            {
                string k = s.Substring(0, a0), arma = s.Substring(a0 + 2, a1 - a0 - 2), v = s.Substring(a1 + 2);
                bool hs = v.EndsWith(" (HS)"); if (hs) v = v.Substring(0, v.Length - 5);
                return $"<color={Cor(k)}>{k}</color>   <color=#d8d2c4>{NomeArma(arma.ToLower())}</color>{(hs ? "  <color=#ffffff>HS</color>" : "")}   <color={Cor(v)}>{v}</color>";
            }
            int sp = s.IndexOf(' ');
            return sp > 0 ? $"<color={Cor(s.Substring(0, sp))}>{s.Substring(0, sp)}</color>{s.Substring(sp)}" : s;
        }

        public static string NomeArma(string g) => CSM.Sim.WeaponStats.Nome(g);

        static Color CorNade(string n) => n switch
        {
            "smoke" => new Color(0.85f, 0.85f, 0.85f), "flash" => new Color(1f, 0.93f, 0.45f), "molotov" => new Color(1f, 0.5f, 0.2f),
            "incendiaria" => new Color(1f, 0.5f, 0.2f), "he" => new Color(0.5f, 0.8f, 0.4f), _ => CINZA
        };

        /// <summary>O que o jogador tem NA MÃO agora: granada que está jogando, C4 plantando, kit desarmando, faca ou a arma.</summary>
        static string NaMao(Agent a)
        {
            string ac = a.brain.action;
            if (ac == "granada") return "n:" + (a.brain.AcaoGranada ?? "smoke");
            if (ac == "plantar" || ac == "fake plant") return "c4";
            if (ac == "desarmar" || ac == "fake defuse") return "kit";
            return a.faca ? "faca" : a.gunId;
        }

        static string NomeNade(string n) => n switch { "smoke" => "SMOKE", "flash" => "FLASH", "he" => "HE", "incendiaria" => "MOLOTOV", "molotov" => "MOLOTOV", _ => n.ToUpper() };

        void Cards(Sim.Partida pt, Match m, bool aT, Dictionary<string, Sim.Partida.Jogador> jog, int seguir, float dt)
        {
            foreach (bool ladoA in new[] { true, false })
            {
                var time = ladoA ? pt.A : pt.B; bool t = ladoA ? aT : !aT;
                var cs = ladoA ? cardsA : cardsB;
                float sx = ladoA ? 1 : -1;
                for (int i = 0; i < 5; i++)
                {
                    var c = cs[i]; var j = time.js[i];
                    var a = m.agents.FirstOrDefault(x => x.name == j.nome);
                    if (a == null) { c.r.gameObject.SetActive(false); continue; }
                    c.r.gameObject.SetActive(true);
                    int slot = (ladoA ? 0 : 5) + i + 1; slotDe[a.name] = slot; c.slot.text = (slot % 10).ToString();
                    if (a.hp < c.ultHp) { c.flash = 1; c.lagEspera = 0.45f; }
                    c.ultHp = a.hp;
                    c.hpMostra = Mathf.Lerp(c.hpMostra, a.hp, 1 - Mathf.Exp(-18 * dt));
                    c.lagEspera -= dt;
                    if (c.lagEspera <= 0) c.hpLag = Mathf.MoveTowards(c.hpLag, c.hpMostra, 90 * dt);
                    if (c.hpLag < c.hpMostra) c.hpLag = c.hpMostra;
                    c.flash = Mathf.Max(0, c.flash - dt * 3);
                    var cor = Lado(t);
                    c.hp.text = a.alive ? a.hp.ToString() : ""; c.caveira.gameObject.SetActive(!a.alive);
                    c.hpBox.color = a.alive ? (t ? CAIXA_TR : CAIXA_CT) : new Color(1, 0.957f, 0.886f, 0.1f);
                    c.hp.color = a.alive ? Color.white : CINZA;
                    // vida enche o card na cor do time (vermelho quando pouca); levou tiro: pisca claro
                    var cv = a.hp > 30 ? (t ? VIDA_TR : VIDA_CT) : VIDA_BAIXA;
                    c.vidaFundo.color = Color.Lerp(cv, Color.white, c.flash * 0.55f); c.vidaFundo.fillAmount = a.alive ? c.hpMostra / 100f : 0;
                    c.vidaLag.fillAmount = a.alive ? c.hpLag / 100f : 0;
                    c.nome.text = a.name.ToUpper();
                    c.nome.color = a.alive ? CREME : CINZA;
                    // kills do round: caveirinhas depois do nome
                    float xn = 70 + c.nome.preferredWidth + 9;
                    for (int k = 0; k < 5; k++)
                    {
                        bool tem = k < a.kills;
                        c.kills[k].gameObject.SetActive(tem);
                        if (tem) { c.kills[k].rectTransform.anchoredPosition = new Vector2(sx * (xn + 4 + k * 17), -18); c.kills[k].color = a.alive ? CREME : CINZA; }
                    }
                    // a arma principal (a de mais valor que ele tem: rifle se tiver, senão a pistola), não o que está na mão
                    // agora (usuário: mesma regra pra todas as armas); faca/granada/C4 na mão não trocam o ícone
                    var ic = a.alive ? Icone(a.Primaria ?? a.gunId) : null;
                    string mao = a.alive ? NaMao(a) : null;   // só pra granada que está sendo jogada pulsar
                    c.icone.texture = ic; c.icone.gameObject.SetActive(ic != null);
                    if (ic != null) c.icone.rectTransform.sizeDelta = TamArma(ic, a.Primaria ?? a.gunId, 28, 124);
                    c.icone.color = CREME;
                    c.icone.uvRect = ladoA ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);   // o cano aponta pro meio da tela
                    if (!a.alive) c.arma.text = $"<color=#9a9aa2>K/D</color>  {j.k + a.kills} / {j.d + a.deaths}";
                    else c.arma.text = ic != null ? "" : NomeArma(a.Primaria ?? a.gunId);
                    c.dinheiro.text = $"${j.dinheiro:N0}".Replace(",", ".");
                    c.dinheiro.color = a.alive ? VERDE : Escurece(VERDE, 0.6f);
                    // linha de baixo: dinheiro, capacete, C4/KIT e granadas, um atrás do outro
                    float xl = 70 + c.dinheiro.preferredWidth + 10;
                    c.capacete.gameObject.SetActive(a.alive && a.armor > 0);
                    if (a.alive && a.armor > 0)
                    {   // colete + capacete = um ícone só (como no CS); só o colete = colete
                        float wc = PorIcone(c.capacete, a.helmet ? "ui_colete_capacete" : "ui_colete", 17);
                        c.capacete.rectTransform.anchoredPosition = new Vector2(sx * (xl + wc / 2), 7); c.capacete.color = CREME; xl += wc + 8;
                    }
                    // C4 e kit viram ícone (antes texto "C4"/"KIT" que encavalava nas granadas)
                    c.tags.text = "";
                    void Item(RawImage ri, bool tem, string tex, float h, Color cor)
                    {
                        ri.gameObject.SetActive(tem);
                        if (!tem) return;
                        float w = PorIcone(ri, tex, h); ri.color = cor;
                        ri.rectTransform.anchoredPosition = new Vector2(sx * (xl + w / 2), 7); xl += w + 7;
                    }
                    Item(c.c4Ico, a.alive && a.hasBomb, "ui_bomba", 16, new Color(1f, 0.42f, 0.37f));
                    Item(c.kitIco, a.alive && !a.IsT && a.hasKit, "ui_alicate", 12, new Color(0.61f, 0.76f, 1f));
                    for (int k = 0; k < 4; k++)
                    {
                        bool tem = a.alive && k < a.nades.Count;
                        // a que está sendo jogada pulsa
                        bool jogando = tem && mao != null && mao.StartsWith("n:") && mao.Substring(2) == a.nades[k];
                        Item(c.nades[k], tem, tem ? "nade_" + a.nades[k] : null, 19, CREME);
                        if (tem) c.nades[k].rectTransform.localScale = Vector3.one * (jogando ? 1.25f + 0.2f * Mathf.Sin(tempo * 12) : 1);
                    }
                    float alfa = a.alive ? 1 : 0.5f;
                    c.fundo.color = new Color(ESCURO.r, ESCURO.g, ESCURO.b, ESCURO.a * alfa);
                    // jogador seguido pela câmera: borda clara respirando
                    bool foco = seguir == a.id;
                    c.borda.color = foco ? new Color(1, 1, 1, 0.55f + 0.25f * Mathf.Sin(tempo * 4)) : new Color(1, 1, 1, 0);
                    // morreu agora: card dá um tranco pro lado
                    float morte = a.alive ? 0 : Mathf.Clamp01(1 - (m.t - a.diedAt) / 0.4f);
                    c.r.anchoredPosition = new Vector2(sx * (24 + 14 * Mathf.Sin(morte * Mathf.PI * 3) * morte), c.r.anchoredPosition.y);
                }
            }
        }

        /// <summary>Histórico de rounds (só no começo do round, como nas transmissões): fileira de cima = time A,
        /// de baixo = time B; o round vencido mostra como (caveira eliminação, bomba explodiu, alicate desarmou,
        /// relógio tempo) na cor do LADO que venceu.</summary>
        void Historico(Sim.Partida pt, Match m, int rn)
        {
            bool mostra = !m.over && Ti(m) < 9f && pt.historico.Count > 0 && !replayLigado;
            histK = Mathf.MoveTowards(histK, mostra ? 1 : 0, Time.unscaledDeltaTime * 4);
            histFundo.gameObject.SetActive(histK > 0.01f);
            if (histK <= 0.01f) return;
            histGrupo.alpha = histK;
            histFundo.anchoredPosition = new Vector2(0, -116 + (1 - SaiRapido(histK)) * 20);
            histA.text = pt.A.nome.ToUpper().Substring(0, Mathf.Min(3, pt.A.nome.Length));
            histB.text = pt.B.nome.ToUpper().Substring(0, Mathf.Min(3, pt.B.nome.Length));
            bool aT = Sim.Partida.ATerrorista(pt.r);
            histA.color = aT ? PLACAR_TR : PLACAR_CT; histB.color = aT ? PLACAR_CT : PLACAR_TR;
            var h = pt.historico; int ini = Mathf.Max(0, h.Count - 24);
            for (int i = 0; i < 24; i++)
                for (int lin = 0; lin < 2; lin++) { histCel[lin, i].color = VAZIO; histIco[lin, i].gameObject.SetActive(false); }
            for (int i = 0; i < h.Count - ini; i++)
            {
                var info = h[ini + i];
                int lin = info.timeAvenceu ? 0 : 1;
                bool tr = info.timeAvenceu ? info.ladoA == "TR" : info.ladoA != "TR";
                var cor = tr ? PLACAR_TR : PLACAR_CT;
                histCel[lin, i].color = new Color(cor.r, cor.g, cor.b, 0.2f);
                string mo = (info.motivo ?? "").ToLower();
                var ico = histIco[lin, i];
                string icn = mo.Contains("explod") ? "ui_bomba" : mo.Contains("desarm") ? "ui_alicate" : mo.Contains("tempo") ? "ui_relogio" : "ui_caveira";
                if (PorIcone(ico, icn, 16) > 18) PorIcone(ico, icn, 16 * 18 / ico.rectTransform.sizeDelta.x);   // cabe na célula
                ico.color = cor; ico.gameObject.SetActive(true);
            }
        }

        void Observado(Match m, int seguir, float dt)
        {
            var a = seguir >= 0 ? m.agents[seguir] : null;
            obsAlfa = Mathf.MoveTowards(obsAlfa, a != null && a.alive ? 1 : 0, dt * 5);
            obs.gameObject.SetActive(obsAlfa > 0.01f);
            if (a == null) return;
            obs.anchoredPosition = new Vector2(0, 24 - (1 - SaiRapido(obsAlfa)) * 120);
            var cor = Lado(a.IsT);
            obsNome.text = $"<color={Hex(a.IsT ? PLACAR_TR : PLACAR_CT)}>{a.name.ToUpper()}</color>";
            obsHp.text = a.hp.ToString(); obsHp.color = a.hp > 30 ? CREME : VERMELHO; obsIcVida.color = obsHp.color;
            obsColete.text = a.armor > 0 ? a.armor.ToString() : ""; obsIcColete.gameObject.SetActive(a.armor > 0);
            PorIcone(obsIcColete, a.helmet ? "ui_colete_capacete" : "ui_colete", 22); obsIcColete.color = CREME;
            // o número do colete anda junto com a largura do ícone (colete+capacete é mais largo e encostava no 100)
            obsColete.rectTransform.anchoredPosition = new Vector2(140 + obsIcColete.rectTransform.sizeDelta.x + 8, 12);
            string mao = NaMao(a);
            var oi = mao.StartsWith("n:") || mao == "c4" || mao == "kit" ? null : Icone(mao);
            // com ícone, só o ícone (o nome ficava embaixo dele); nome escrito só pra granada, C4 e kit
            obsArma.text = oi != null ? "" : mao.StartsWith("n:") ? NomeNade(mao.Substring(2)) : mao == "c4" ? "C4" : mao == "kit" ? "kit de desarme" : NomeArma(mao);
            obsIcone.texture = oi; obsIcone.gameObject.SetActive(oi != null);
            if (oi != null) obsIcone.rectTransform.sizeDelta = TamArma(oi, mao, 26, 118);
            obsIcone.color = CREME;
            obsMun.text = a.faca || a.gun == null ? "" : a.gun.Reloading ? "<size=24>recarregando</size>" : $"{a.gun.inMag}<size=24> / {a.gun.mags * a.gun.s.mag}</size>";
        }

        void Banners(Sim.Partida pt, Match m, int rn, bool aT)
        {
            string chave = null, titulo = "", sub = ""; Color cor = ESCURO;
            if (pt.acabou && pt.vencedor != null)
            {
                // fim da partida: quem ganhou e o placar final, na cor do lado em que o vencedor terminou
                bool trV = (pt.vencedor == pt.A) == aT;
                chave = "partida"; titulo = $"{pt.vencedor.nome} VENCEU"; cor = Escurece(Lado(trV), 0.8f);
                sub = $"{pt.A.nome} {pt.A.placar}  ×  {pt.B.placar} {pt.B.nome}";
            }
            else if (m.over)
            {
                bool tr = m.winner == "TR";
                chave = "fim" + rn; titulo = tr ? "TR VENCEU" : "CT VENCEU"; cor = Escurece(Lado(tr), 0.8f);
                sub = Motivo(m.reason);
            }
            else if (Ti(m) < 2.6f)
            {
                chave = "ini" + rn; titulo = $"ROUND {rn}";
                sub = $"{pt.A.nome} {pt.A.placar}  ×  {pt.B.placar} {pt.B.nome}";
                if (rn == 13) sub = "TROCA DE LADO · " + sub;
            }
            if (replayLigado) chave = null;
            if (chave != bannerChave) { bannerChave = chave; bannerDesde = tempo; }
            banner.gameObject.SetActive(chave != null);
            if (chave == null) return;
            float idade = tempo - bannerDesde;
            float entra = SaiDeTras(idade / 0.4f);
            float some = chave.StartsWith("ini") ? Mathf.Clamp01((2.6f - Ti(m)) / 0.4f) : 1;
            banner.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, entra);
            bannerFundo.color = new Color(cor.r, cor.g, cor.b, cor.a * Mathf.Clamp01(idade / 0.15f) * some);
            bannerTitulo.text = titulo; bannerSub.text = sub;
            bannerTitulo.color = new Color(CREME.r, CREME.g, CREME.b, Mathf.Clamp01(idade / 0.2f) * some);
            bannerSub.color = new Color(CREME.r, CREME.g, CREME.b, Mathf.Clamp01((idade - 0.15f) / 0.3f) * some);
        }

        static string Motivo(string r)
        {
            if (string.IsNullOrEmpty(r)) return "";
            string s = r.ToLower();
            if (s.Contains("explod")) return "a bomba explodiu";
            if (s.Contains("desarm") || s.Contains("defus")) return "bomba desarmada";
            if (s.Contains("tempo")) return "o tempo acabou";
            if (s.Contains("elimin") || s.Contains("morre") || s.Contains("mort")) return "time adversário eliminado";
            return r;
        }

        void Minimapa(Match m, int seguir)
        {
            if (!radarPronto) MontarRadar();
            Agent obs = null;
            for (int i = 0; i < 10; i++)
            {
                var mk = marcas[i];
                var a = i < m.agents.Count ? m.agents[i] : null;
                bool vivo = a != null && a.alive, morto = a != null && !a.alive, olhando = vivo && seguir == a.id;
                if (olhando) obs = a;
                mk.bola.gameObject.SetActive(vivo); mk.ponta.gameObject.SetActive(vivo); mk.anel.gameObject.SetActive(olhando);
                mk.x1.gameObject.SetActive(morto); mk.x2.gameObject.SetActive(morto); mk.c4.gameObject.SetActive(vivo && a.hasBomb);
                if (a == null) continue;
                var p = NoMapa(a.pos); var cor = a.IsT ? MARCA_T : MARCA_CT;
                if (morto)
                {
                    var cx = new Color(cor.r, cor.g, cor.b, 0.85f);
                    mk.x1.rectTransform.anchoredPosition = mk.x2.rectTransform.anchoredPosition = p; mk.x1.color = mk.x2.color = cx;
                    continue;
                }
                // gota: bola com o número + quadrado girado com a quina pra onde ele mira
                float d = MARCA_D * (olhando ? 1.18f : 1f);
                var dir = new Vector2(Mathf.Cos(a.aim), -Mathf.Sin(a.aim));
                mk.bola.rectTransform.anchoredPosition = p; mk.bola.rectTransform.sizeDelta = new Vector2(d, d); mk.bola.color = cor;
                mk.ponta.rectTransform.anchoredPosition = p + dir * d * 0.34f; mk.ponta.rectTransform.sizeDelta = new Vector2(d * 0.62f, d * 0.62f);
                mk.ponta.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 45); mk.ponta.color = cor;
                mk.anel.rectTransform.anchoredPosition = p; mk.anel.rectTransform.sizeDelta = new Vector2(d + 5, d + 5);
                mk.num.text = slotDe.TryGetValue(a.name, out var sl) ? (sl % 10).ToString() : "";
                mk.num.rectTransform.sizeDelta = new Vector2(d, d);
                if (a.hasBomb)
                {
                    PorIcone(mk.c4, "ui_bomba", 13);
                    mk.c4.rectTransform.anchoredPosition = p + new Vector2(d * 0.62f, d * 0.5f); mk.c4.color = C4_MAPA;
                }
            }
            // C4 fora de alguém: caída (vermelha) ou plantada (pisca; azul quando desarmada)
            bool c4 = m.bombPlanted || m.bombDropped;
            c4Chao.gameObject.SetActive(c4);
            if (c4)
            {
                PorIcone(c4Chao, "ui_bomba", m.bombPlanted ? 18 : 15);
                c4Chao.rectTransform.anchoredPosition = NoMapa(m.bombPlanted ? m.bombPos : m.droppedBomb);
                c4Chao.color = m.defused ? MARCA_CT : m.bombPlanted ? Color.Lerp(C4_MAPA, Color.white, 0.5f + 0.5f * Mathf.Sin(tempo * 9)) : C4_MAPA;
                c4Chao.transform.SetAsLastSibling();
            }
            // embaixo do radar: quem está sendo observado e onde
            mapaLocal.text = obs == null ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(obs.IsT ? MARCA_T : MARCA_CT)}>{obs.name.ToUpper()}</color>   {Places.Region(obs.pos).ToUpper()}";
            int n = 0;
            float k = mapaR.sizeDelta.x / MapData.NX;
            foreach (var s in m.smokes)
            {
                var cs = m.smokeDe.TryGetValue(s, out var tm) ? Color.Lerp(new Color(0.93f, 0.93f, 0.93f), tm == Team.T ? MARCA_T : MARCA_CT, 0.35f) : new Color(0.93f, 0.93f, 0.93f);
                cs.a = 0.62f;
                Mancha(ref n, new V2(s.origin.x, s.origin.y), 34 * k * 2 * Mathf.Clamp01(s.age / 1.3f), cs);
            }
            foreach (var f in m.fires) Mancha(ref n, new V2(f.origin.x, f.origin.y), 28 * k * 2, new Color(1f, 0.5f, 0.15f, 0.65f));
            for (int i = n; i < manchas.Count; i++) manchas[i].gameObject.SetActive(false);
        }

        void Mancha(ref int n, V2 p, float d, Color c)
        {
            if (n == manchas.Count) { var b = Bola("mancha", mapaR, Vector2.zero, Vector2.zero, 10, c); b.transform.SetSiblingIndex(0); manchas.Add(b); }
            var im = manchas[n++]; im.gameObject.SetActive(true);
            im.rectTransform.anchoredPosition = NoMapa(p); im.rectTransform.sizeDelta = new Vector2(d, d); im.color = c;
        }

        /// <summary>Visível no Play e no print (o print chama antes de renderizar).</summary>
        public void ForcarLayout() => Canvas.ForceUpdateCanvases();

        /// <summary>Retângulo do card do jogador (time A/B, posição 0..4) em pixels da tela (origem em cima, pro OnGUI).</summary>
        public Rect TelaDoCard(bool timeA, int i)
        {
            var r = (timeA ? cardsA : cardsB)[i].r;
            var c = new Vector3[4]; r.GetWorldCorners(c);
            var cm = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cm, c[0]), b = RectTransformUtility.WorldToScreenPoint(cm, c[2]);
            return new Rect(a.x, Screen.height - b.y, b.x - a.x, b.y - a.y);
        }

        public static RenderTexture Minimapa(Vector3 centro, float meiaAltura, float aspecto)
        {
            var rt = new RenderTexture(Mathf.RoundToInt(560 * aspecto), 560, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "minimapa" };
            var g = new GameObject("Câmera minimapa");
            var c = g.AddComponent<Camera>();
            c.orthographic = true; c.orthographicSize = meiaAltura; c.aspect = aspecto;
            c.transform.SetPositionAndRotation(centro + Vector3.up * 60, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            c.nearClipPlane = 1; c.farClipPlane = 120; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.25f, 0.45f, 0.3f);
            c.cullingMask = ~((1 << CAMADA_UI) | (1 << Mathf.Max(0, LayerMask.NameToLayer(Boneco3D.CAMADA))));
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(c).renderPostProcessing = false;
            c.targetTexture = rt; c.Render(); c.Render(); c.targetTexture = null;
            if (Application.isPlaying) Object.Destroy(g); else Object.DestroyImmediate(g);
            return rt;
        }
    }
}
