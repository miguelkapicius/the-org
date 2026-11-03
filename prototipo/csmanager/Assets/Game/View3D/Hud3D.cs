using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CSM.Map;
using CSM.Sim;
using static CSM.View3D.UiKit;

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
        static readonly Color CT = new Color(0.36f, 0.62f, 0.98f), TR = new Color(0.96f, 0.62f, 0.22f);
        static readonly Color ESCURO = new Color(0.10f, 0.11f, 0.17f, 0.88f), CREME = new Color(0.99f, 0.95f, 0.86f), CINZA = new Color(0.55f, 0.55f, 0.6f);
        static readonly Color VERDE = new Color(0.55f, 0.88f, 0.45f), VERMELHO = new Color(0.95f, 0.30f, 0.26f);
        static Color Lado(bool t) => t ? TR : CT;
        static Color Escurece(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public readonly Camera cam; public readonly Canvas canvas; readonly RectTransform raiz;
        // tudo que é "jogo" (placar, cards, minimapa, killfeed, etiquetas) fica num grupo que some no replay
        RectTransform jogo; CanvasGroup jogoGrupo; readonly Camera principal;
        // etiqueta em cima de cada boneco: nome + vida
        class Etiqueta { public RectTransform r; public Text nome; public Image barra, fundo; public float alfa; }
        readonly Etiqueta[] etiquetas = new Etiqueta[10];
        float tempo;

        // placar
        Image clutchSelo; Text clutchTxt; float clutchDesde = -1;
        // histórico de rounds embaixo do placar (como nas transmissões: quem ganhou cada round e como)
        readonly Image[] histTile = new Image[30]; readonly Text[] histTxt = new Text[30];
        Image tileA, tileB, centro, barraC4; Text nomeA, nomeB, placarA, placarB, relogio, roundTxt; Image[] vivosA = new Image[5], vivosB = new Image[5];
        // killfeed
        class Linha { public RectTransform r; public Image fundo; public readonly List<Graphic> partes = new List<Graphic>(); public float nasceu; public float y; }
        readonly List<Linha> feed = new List<Linha>(); int feedVisto;
        // cards
        class Card
        {
            public RectTransform r; public Image fundo, hpBox, hpBarra, hpAtraso, borda; public Text hp, nome, arma, dinheiro, tags; public Image[] nades = new Image[4];
            public RawImage icone, iconeFora;   // arma NA MÃO (grande) e a que está guardada (pequena, apagada)
            public Image vidaFundo, vidaLag; public Image[] kills = new Image[5];
            public float hpMostra = 100, hpLag = 100, lagEspera, flash; public int ultHp = 100;
        }
        readonly Card[] cardsA = new Card[5], cardsB = new Card[5];
        // observado
        RectTransform obs; Text obsNome, obsHp, obsColete, obsArma, obsMun; Image obsFundo; float obsAlfa; RawImage obsIcone;
        /// <summary>Modelo de onde saem os ícones de arma (as armas são as mesmas nos dois times).</summary>
        public Boneco3D.Modelo modeloArmas;
        /// <summary>Onde fica a cabeça de cada boneco no mundo (pra etiqueta de nome/vida).</summary>
        public System.Func<Agent, Vector3> Cabeca;
        Texture2D Icone(string gunId) => modeloArmas == null ? null : Armas3D.Icone(modeloArmas, Armas3D.DoJogo(gunId));
        // banners
        RectTransform banner; Image bannerFundo; Text bannerTitulo, bannerSub; float bannerDesde = -1; string bannerChave;
        // minimapa
        RawImage mapaImg; RectTransform mapaR; readonly Image[] pontos = new Image[10]; readonly Image[] setas = new Image[10]; Image c4Ponto;
        readonly List<Image> manchas = new List<Image>();
        // economia (começo do round)
        class LinhaEco { public Text nome, dinheiro, tags; public RawImage icone; public Image[] nades = new Image[4]; }
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
            MontarPlacar(); MontarCards(); MontarObservado(); MontarMapa(minimapa); MontarEconomia();
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
                var tile = Painel(a ? "time A" : "time B", raiz, top, new Vector2(sx * 95, -22), new Vector2(270, 70), TR, new Vector2(a ? 1 : 0, 1), true);
                var nome = Texto("nome", tile.transform, new Vector2(a ? 0 : 1, 0.5f), new Vector2(sx * -18, 0), new Vector2(170, 40), "TIME", 24, CREME, a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, false, new Vector2(a ? 0 : 1, 0.5f));
                var pl = Texto("placar", tile.transform, new Vector2(a ? 1 : 0, 0.5f), new Vector2(sx * 16, 0), new Vector2(70, 60), "0", 50, CREME, TextAnchor.MiddleCenter, true, new Vector2(a ? 1 : 0, 0.5f));
                var vivos = a ? vivosA : vivosB;
                for (int i = 0; i < 5; i++)
                    vivos[i] = Bola("vivo", raiz, top, new Vector2(sx * (110 + i * 22), -104), 15, CREME);
                if (a) { tileA = tile; nomeA = nome; placarA = pl; } else { tileB = tile; nomeB = nome; placarB = pl; }
            }
            for (int i = 0; i < histTile.Length; i++)
            {
                histTile[i] = Painel("round " + (i + 1), raiz, top, Vector2.zero, new Vector2(19, 19), new Color(1, 1, 1, 0.15f), new Vector2(0.5f, 0.5f), true);
                histTxt[i] = Texto("como", histTile[i].transform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(19, 19), "", 12, CREME, TextAnchor.MiddleCenter, true, null, false);
                histTile[i].gameObject.SetActive(false);
            }
        }

        void MontarCards()
        {
            // card de transmissão (referência ESL/PGL): a VIDA preenche o fundo do card na cor do time (some da direita
            // pra esquerda), número da vida no quadrado, nome com as kills do round (bolinhas), dinheiro + colete/kit/C4 +
            // granadas embaixo, a arma NA MÃO grande do lado de dentro (pro meio da tela) e a guardada pequena e apagada
            foreach (bool a in new[] { true, false })
            {
                var cs = a ? cardsA : cardsB;
                var anc = new Vector2(a ? 0 : 1, 0);
                for (int i = 0; i < 5; i++)
                {
                    var c = new Card();
                    float sx = a ? 1 : -1;
                    c.borda = Painel("card borda", raiz, anc, new Vector2(sx * 22, 24 + (4 - i) * 66), new Vector2(404, 60), new Color(1, 1, 1, 0), anc);
                    c.fundo = Painel("card", raiz, anc, new Vector2(sx * 24, 26 + (4 - i) * 66), new Vector2(400, 56), ESCURO, anc, true);
                    c.r = c.fundo.rectTransform;
                    var t = c.r;
                    var la = new Vector2(a ? 0 : 1, 0.5f);
                    var lado = new Vector2(a ? 0 : 1, 0.5f);
                    // vida no fundo: atraso branco + cor do time, entre o quadrado e a ponta
                    // (só na faixa do nome, como na ESL: a de baixo fica escura pra ler dinheiro, granadas e arma)
                    c.vidaLag = Barra("vida atraso", t, lado, new Vector2(sx * 68, 11), new Vector2(326, 26), new Color(0, 0, 0, 0), new Color(1, 1, 1, 0.22f), !a, lado);
                    c.vidaFundo = No("vida", c.vidaLag.transform.parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                    var vr = c.vidaFundo.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
                    c.vidaFundo.sprite = c.vidaLag.sprite; c.vidaFundo.type = Image.Type.Filled; c.vidaFundo.fillMethod = Image.FillMethod.Horizontal;
                    c.vidaFundo.fillOrigin = a ? 0 : 1; c.vidaFundo.raycastTarget = false;
                    c.hpBox = Painel("hp", t, la, new Vector2(sx * 4, 0), new Vector2(60, 48), TR, la);
                    c.hp = Texto("hp txt", c.hpBox.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 48), "100", 28, CREME, TextAnchor.MiddleCenter, true);
                    c.nome = Texto("nome", t, new Vector2(a ? 0 : 1, 1), new Vector2(sx * 76, -4), new Vector2(200, 26), "Jogador", 20, CREME, a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, false, new Vector2(a ? 0 : 1, 1));
                    for (int k = 0; k < 5; k++) c.kills[k] = Bola("kill", t, new Vector2(a ? 0 : 1, 1), Vector2.zero, 9, CREME);
                    c.dinheiro = Texto("dinheiro", t, new Vector2(a ? 0 : 1, 0), new Vector2(sx * 76, 6), new Vector2(90, 22), "$0", 17, VERDE, a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true, new Vector2(a ? 0 : 1, 0), false);
                    c.tags = Texto("tags", t, new Vector2(a ? 0 : 1, 0), new Vector2(sx * 160, 6), new Vector2(70, 22), "", 15, CREME, a ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, false, new Vector2(a ? 0 : 1, 0), false);
                    for (int k = 0; k < 4; k++) c.nades[k] = Bola("nade", t, new Vector2(a ? 0 : 1, 0), new Vector2(sx * (236 + k * 15), 17), 11, CREME);
                    var dentro = new Vector2(a ? 1 : 0, 0.5f);
                    c.icone = No("arma na mão", t, dentro, new Vector2(sx * -12, 2), new Vector2(80, 26), dentro).gameObject.AddComponent<RawImage>();
                    c.icone.raycastTarget = false;
                    c.iconeFora = No("arma guardada", t, dentro, new Vector2(sx * -100, -12), new Vector2(40, 13), dentro).gameObject.AddComponent<RawImage>();
                    c.iconeFora.raycastTarget = false;
                    c.arma = Texto("arma", t, dentro, new Vector2(sx * -12, 0), new Vector2(150, 24), "", 17, new Color(0.9f, 0.88f, 0.84f), a ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, false, dentro, false);
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
            obsHp = Texto("hp", obs, new Vector2(0, 0), new Vector2(22, 10), new Vector2(140, 40), "", 34, CREME, TextAnchor.MiddleLeft, true, new Vector2(0, 0));
            obsColete = Texto("colete", obs, new Vector2(0, 0), new Vector2(150, 12), new Vector2(120, 34), "", 24, CINZA, TextAnchor.MiddleLeft, true, new Vector2(0, 0));
            obsArma = Texto("arma", obs, new Vector2(1, 1), new Vector2(-150, -12), new Vector2(200, 30), "", 18, CINZA, TextAnchor.MiddleRight, false, new Vector2(1, 1), false);
            obsIcone = No("ícone", obs, new Vector2(1, 1), new Vector2(-20, -6), new Vector2(120, 40), new Vector2(1, 1)).gameObject.AddComponent<RawImage>();
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
            float W = 340, H = W * MapData.NY / MapData.NX;
            var f = Painel("minimapa", raiz, new Vector2(0, 1), new Vector2(22, -22), new Vector2(W + 12, H + 12), ESCURO, new Vector2(0, 1), true);
            mapaImg = No("mapa", f.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H)).gameObject.AddComponent<RawImage>();
            mapaImg.texture = rt; mapaImg.raycastTarget = false; mapaImg.color = new Color(1, 1, 1, 0.95f);
            mapaR = mapaImg.rectTransform;
            for (int i = 0; i < 10; i++)
            {
                setas[i] = Painel("mira", mapaR, new Vector2(0, 0), Vector2.zero, new Vector2(4, 13), CREME, new Vector2(0.5f, 0));
                pontos[i] = Bola("jogador", mapaR, new Vector2(0, 0), Vector2.zero, 13, CREME);
            }
            c4Ponto = Painel("c4", mapaR, new Vector2(0, 0), Vector2.zero, new Vector2(12, 12), VERMELHO, new Vector2(0.5f, 0.5f));
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
                    for (int k = 0; k < 4; k++) l.nades[k] = Bola("nade", eco, new Vector2(0, 1), new Vector2(x0 + 330 + k * 14, y - 20), 10, CREME);
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
                e.nome = Texto("nome", e.r, new Vector2(0.5f, 1), new Vector2(0, 4), new Vector2(160, 22), "", 19, CREME, TextAnchor.MiddleCenter, false, new Vector2(0.5f, 1));
                var contorno = e.nome.gameObject.AddComponent<Outline>(); contorno.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.85f); contorno.effectDistance = new Vector2(1.5f, -1.5f);
                e.fundo = Painel("barra fundo", e.r, new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(58, 7), new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0));
                e.barra = No("barra", e.fundo.transform, new Vector2(0, 0.5f), new Vector2(1, 0), new Vector2(56, 5), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>();
                e.barra.sprite = Redondo; e.barra.type = Image.Type.Sliced; e.barra.raycastTarget = false;
                etiquetas[i] = e;
            }
        }

        /// <summary>Nome e vida em cima de cada boneco vivo (posição da cabeça no mundo -> âncora na tela).</summary>
        public void Etiquetas(Match m, System.Func<Agent, Vector3> cabeca, float dt)
        {
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
                if (mostra) { e.r.anchorMin = e.r.anchorMax = new Vector2(v.x, v.y); e.r.anchoredPosition = new Vector2(0, 6); }
                var cor = Lado(a.IsT);
                e.nome.text = a.name + (a.hasBomb ? " <color=#ff6655>C4</color>" : "");
                e.nome.color = new Color(cor.r * 0.55f + 0.45f, cor.g * 0.55f + 0.45f, cor.b * 0.55f + 0.45f, e.alfa);
                e.barra.rectTransform.sizeDelta = new Vector2(56 * Mathf.Clamp01(a.hp / 100f), 5);
                var cb = a.hp > 40 ? cor : VERMELHO; cb.a = e.alfa; e.barra.color = cb;
                e.fundo.color = new Color(0, 0, 0, 0.55f * e.alfa);
            }
        }

        void MontarReplay()
        {
            faixaCima = Painel("faixa", raiz, new Vector2(0.5f, 1), Vector2.zero, new Vector2(4000, 0), Color.black, new Vector2(0.5f, 1));
            faixaBaixo = Painel("faixa", raiz, new Vector2(0.5f, 0), Vector2.zero, new Vector2(4000, 0), Color.black, new Vector2(0.5f, 0));
            replaySelo = Painel("replay", raiz, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(170, 38), VERMELHO, new Vector2(0.5f, 0.5f), true);
            replayTxt = Texto("txt", replaySelo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 38), "● REPLAY", 22, Color.white, TextAnchor.MiddleCenter, true);
            legendaFundo = Painel("legenda fundo", raiz, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(600, 58), ESCURO, new Vector2(0.5f, 0.5f), true);
            replayLegenda = Texto("legenda", legendaFundo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 58), "", 30, CREME, TextAnchor.MiddleCenter);
            replaySelo.gameObject.SetActive(false); legendaFundo.gameObject.SetActive(false);
        }

        /// <summary>Liga o modo replay (faixas de cinema, selo piscando, legenda do lance).</summary>
        public void Replay(bool ligado, string legenda = "")
        {
            replayLigado = ligado;
            if (ligado) replayLegenda.text = legenda;
        }

        static int Preco(Agent a)
        {
            int v = WeaponStats.ALL.TryGetValue(a.gunId, out var st) ? st.price : 0;
            if (a.armor > 0) v += a.helmet ? 1000 : 650;
            if (!a.IsT && a.hasKit) v += 400;
            foreach (var n in a.nades) v += n == "flash" ? 200 : n == "molotov" ? 400 : n == "incendiaria" ? 500 : 300;
            return v;
        }

        void Economia(Sim.Partida pt, Match m, int rn, bool aT, Dictionary<string, Sim.Partida.Jogador> jog, float dt)
        {
            bool mostra = !m.over && m.t > 0.3f && m.t < 7.5f && !replayLigado;
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
                    if (ic != null) l.icone.rectTransform.sizeDelta = new Vector2(24f * ic.width / ic.height, 24);
                    // as linhas entram uma por uma
                    float k = Mathf.Clamp01((m.t - 0.3f - i * 0.08f - t * 0.04f) / 0.3f) * ecoK;
                    l.nome.color = new Color(CREME.r, CREME.g, CREME.b, k); l.icone.color = new Color(1, 1, 1, k);
                    l.tags.text = (a.armor > 0 ? (a.helmet ? "◈ " : "◇ ") : "") + (!a.IsT && a.hasKit ? "KIT" : "") + (a.hasBomb ? "<color=#ff6655>C4</color>" : "");
                    l.tags.color = new Color(1, 1, 1, k);
                    for (int q = 0; q < 4; q++) { bool tem = q < a.nades.Count; l.nades[q].gameObject.SetActive(tem); if (tem) { var c = CorNade(a.nades[q]); c.a = k; l.nades[q].color = c; } }
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
            tileA.color = Escurece(Lado(aT), 0.9f); tileB.color = Escurece(Lado(!aT), 0.9f);
            nomeA.text = pt.A.nome.ToUpper(); nomeB.text = pt.B.nome.ToUpper();
            // fim de round: o ponto já entra no placar (a Partida só soma ao fechar o round), com um pulo
            bool ganhouA = m.over && (m.winner == "TR") == aT, ganhouB = m.over && !ganhouA;
            placarA.text = (pt.A.placar + (ganhouA ? 1 : 0)).ToString(); placarB.text = (pt.B.placar + (ganhouB ? 1 : 0)).ToString();
            float pulo = m.over ? Mathf.Clamp01(1 - (tempo - bannerDesde) / 0.6f) : 0;
            placarA.rectTransform.localScale = Vector3.one * (1 + (ganhouA ? 0.35f * Mathf.Sin(pulo * Mathf.PI) : 0));
            placarB.rectTransform.localScale = Vector3.one * (1 + (ganhouB ? 0.35f * Mathf.Sin(pulo * Mathf.PI) : 0));
            roundTxt.text = $"ROUND {rn}";
            if (m.bombPlanted && !m.defused && !m.exploded)
            {
                float left = Mathf.Max(0, m.BombLeft);
                relogio.text = $"C4 {left:0}";
                float pulso = left < 10 ? 0.5f + 0.5f * Mathf.Sin(tempo * 14) : 0.5f + 0.5f * Mathf.Sin(tempo * 5);
                centro.color = Color.Lerp(new Color(0.45f, 0.08f, 0.08f, 0.92f), new Color(0.75f, 0.12f, 0.1f, 0.95f), pulso);
                barraC4.transform.parent.gameObject.SetActive(true); barraC4.fillAmount = left / Match.BOMB;
                relogio.rectTransform.localScale = Vector3.one * (1 + 0.06f * pulso);
            }
            else
            {
                float left = Mathf.Max(0, m.RoundLeft);
                relogio.text = $"{(int)left / 60}:{(int)left % 60:00}";
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
            clutchSelo.gameObject.SetActive(clutch);
            if (clutch)
            {
                clutchTxt.text = $"{va} v {vb}";
                clutchSelo.rectTransform.localScale = Vector3.one * SaiDeTras((tempo - clutchDesde) / 0.35f);
            }

            Feed(m, dt);
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

        void Feed(Match m, float dt)
        {
            if (m.feed.Count < feedVisto) feedVisto = 0;
            for (; feedVisto < m.feed.Count; feedVisto++)
            {
                var (t, s) = m.feed[feedVisto];
                var l = new Linha { nasceu = tempo };
                l.fundo = Painel("kill", jogo, new Vector2(1, 1), new Vector2(-22, -22), new Vector2(300, 40), ESCURO, new Vector2(1, 1));
                l.r = l.fundo.rectTransform;
                MontarLinha(m, l, s);
                feed.Insert(0, l);
            }
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var l = feed[i]; float idade = tempo - l.nasceu;
                if (idade > 7 || i > 5)
                {
                    if (Application.isPlaying) Object.Destroy(l.r.gameObject); else Object.DestroyImmediate(l.r.gameObject);
                    feed.RemoveAt(i); continue;
                }
                float alvoY = -22 - i * 46;
                l.y = idade < 0.01f ? alvoY : Mathf.Lerp(l.y, alvoY, 1 - Mathf.Exp(-14 * dt));
                float entra = SaiDeTras(idade / 0.35f), sai = Mathf.Clamp01((7 - idade) / 0.4f);
                l.r.anchoredPosition = new Vector2(-22 + (1 - entra) * 380, l.y);
                l.fundo.color = new Color(ESCURO.r, ESCURO.g, ESCURO.b, ESCURO.a * sai);
                foreach (var g in l.partes) { var cc = g.color; cc.a = sai * (g is RawImage ? 0.95f : 1); g.color = cc; }
                // morte nova: borda clara piscando um instante
                if (idade < 0.5f) l.fundo.color = Color.Lerp(new Color(1, 1, 1, 0.9f), l.fundo.color, idade / 0.5f);
            }
        }

        /// <summary>Linha do killfeed lado a lado: quem matou (cor do lado), silhueta da arma, ◎ no HS, vítima.</summary>
        void MontarLinha(Match m, Linha l, string s)
        {
            int a0 = s.IndexOf(" ["), a1 = s.IndexOf("] ");
            float x = 16;
            void Txt(string t, Color c)
            {
                var tx = Texto("txt", l.r, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(10, 40), t, 20, c, TextAnchor.MiddleLeft, false, new Vector2(0, 0.5f));
                float w = tx.preferredWidth; tx.rectTransform.sizeDelta = new Vector2(w, 40); x += w + 10; l.partes.Add(tx);
            }
            Color Cor(string nome) { var ag = m.agents.FirstOrDefault(q => q.name == nome); return ag == null ? CREME : Lado(ag.IsT); }
            if (a0 > 0 && a1 > a0)
            {
                string k = s.Substring(0, a0), arma = s.Substring(a0 + 2, a1 - a0 - 2).ToLower(), v = s.Substring(a1 + 2);
                bool hs = v.EndsWith(" (HS)"); if (hs) v = v.Substring(0, v.Length - 5);
                Txt(k, Cor(k));
                var ic = Icone(arma);
                if (ic != null)
                {
                    float w = 26f * ic.width / ic.height;
                    var ri = No("ícone", l.r, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(w, 26), new Vector2(0, 0.5f)).gameObject.AddComponent<RawImage>();
                    ri.texture = ic; ri.raycastTarget = false; l.partes.Add(ri); x += w + 8;
                }
                else Txt(NomeArma(arma), new Color(0.85f, 0.82f, 0.77f));
                if (hs) Txt("◎", Color.white);
                Txt(v, Cor(v));
            }
            else Txt(s, CREME);
            l.r.sizeDelta = new Vector2(x + 6, 40);
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
                return $"<color={Cor(k)}>{k}</color>   <color=#d8d2c4>{NomeArma(arma.ToLower())}</color>{(hs ? "  <color=#ffffff>◎</color>" : "")}   <color={Cor(v)}>{v}</color>";
            }
            int sp = s.IndexOf(' ');
            return sp > 0 ? $"<color={Cor(s.Substring(0, sp))}>{s.Substring(0, sp)}</color>{s.Substring(sp)}" : s;
        }

        public static string NomeArma(string g) => g switch
        {
            "ak" => "AK-47", "m4" => "M4A4", "m4s" => "M4A1-S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC-10",
            "usp" => "USP-S", "glock" => "Glock-18", "deagle" => "Desert Eagle", "faca" => "faca", _ => g.ToUpper()
        };

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
                    if (a.hp < c.ultHp) { c.flash = 1; c.lagEspera = 0.45f; }
                    c.ultHp = a.hp;
                    c.hpMostra = Mathf.Lerp(c.hpMostra, a.hp, 1 - Mathf.Exp(-18 * dt));
                    c.lagEspera -= dt;
                    if (c.lagEspera <= 0) c.hpLag = Mathf.MoveTowards(c.hpLag, c.hpMostra, 90 * dt);
                    if (c.hpLag < c.hpMostra) c.hpLag = c.hpMostra;
                    c.flash = Mathf.Max(0, c.flash - dt * 3);
                    var cor = Lado(t);
                    c.hp.text = a.alive ? a.hp.ToString() : "✕";
                    c.hpBox.color = a.alive ? Color.Lerp(Escurece(cor, 0.85f), Color.white, c.flash * 0.8f) : new Color(0.3f, 0.3f, 0.32f, 0.9f);
                    // vida enche o card (vermelho quando pouca)
                    var cv = a.hp > 40 ? cor : VERMELHO; cv.a = 0.42f;
                    c.vidaFundo.color = cv; c.vidaFundo.fillAmount = a.alive ? c.hpMostra / 100f : 0;
                    c.vidaLag.fillAmount = a.alive ? c.hpLag / 100f : 0;
                    c.nome.text = a.name;
                    c.nome.color = a.alive ? CREME : CINZA;
                    // kills do round: bolinhas depois do nome
                    float xn = 76 + c.nome.preferredWidth + 10;
                    for (int k = 0; k < 5; k++)
                    {
                        bool tem = k < a.kills;
                        c.kills[k].gameObject.SetActive(tem);
                        if (tem) { c.kills[k].rectTransform.anchoredPosition = new Vector2(sx * (xn + k * 12), -17); c.kills[k].color = a.alive ? CREME : CINZA; }
                    }
                    // a arma principal (a de mais valor que ele tem: rifle se tiver, senão a pistola), não o que está na mão
                    // agora (usuário: mesma regra pra todas as armas); faca/granada/C4 na mão não trocam o ícone
                    var ic = a.alive ? Icone(a.Primaria ?? a.gunId) : null;
                    string mao = a.alive ? NaMao(a) : null;   // só pra granada que está sendo jogada pulsar
                    c.icone.texture = ic; c.icone.gameObject.SetActive(ic != null);
                    if (ic != null) c.icone.rectTransform.sizeDelta = new Vector2(24f * ic.width / ic.height, 24);
                    c.icone.color = new Color(0.96f, 0.94f, 0.9f, 1f);
                    c.icone.uvRect = ladoA ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);   // o cano aponta pro meio da tela
                    c.iconeFora.gameObject.SetActive(false);
                    if (!a.alive) c.arma.text = $"<color=#9a9aa2>K/D</color>  {j.k + a.kills} / {j.d + a.deaths}";
                    else c.arma.text = ic != null ? "" : NomeArma(a.Primaria ?? a.gunId);
                    c.dinheiro.text = $"${j.dinheiro:N0}".Replace(",", ".");
                    c.dinheiro.color = a.alive ? VERDE : Escurece(VERDE, 0.6f);
                    string tags = "";
                    if (a.alive && a.armor > 0) tags += a.helmet ? "◈ " : "◇ ";
                    if (a.alive && a.hasBomb) tags += "<color=#ff6655>C4</color> ";
                    if (a.alive && !a.IsT && a.hasKit) tags += "KIT";
                    c.tags.text = tags;
                    for (int k = 0; k < 4; k++)
                    {
                        bool tem = a.alive && k < a.nades.Count;
                        c.nades[k].gameObject.SetActive(tem);
                        if (tem)
                        {
                            c.nades[k].color = CorNade(a.nades[k]);
                            // a que está sendo jogada pulsa
                            bool jogando = mao != null && mao.StartsWith("n:") && mao.Substring(2) == a.nades[k];
                            c.nades[k].rectTransform.localScale = Vector3.one * (jogando ? 1.25f + 0.2f * Mathf.Sin(tempo * 12) : 1);
                        }
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

        /// <summary>Faixa de rounds embaixo do placar: um quadradinho por round na cor do LADO que ganhou, com o jeito
        /// (✕ eliminação, ◎ bomba explodiu, ◈ desarmou, – tempo); vão no meio da partida (troca de lado).</summary>
        void Historico(Sim.Partida pt, Match m, int rn)
        {
            var h = pt.historico; int n = Mathf.Min(h.Count, histTile.Length);
            int ini = h.Count - n;
            float larg = 22, total = n * larg + (n > 12 ? 12 : 0);
            for (int i = 0; i < histTile.Length; i++)
            {
                bool tem = i < n; histTile[i].gameObject.SetActive(tem);
                if (!tem) continue;
                var info = h[ini + i];
                // lado que ganhou: o time A estava em ladoA (vencedor é o NOME do time)
                bool tr = info.timeAvenceu ? info.ladoA == "TR" : info.ladoA != "TR";
                float x = -total / 2 + i * larg + larg / 2 + (ini + i >= 12 ? 12 : 0);
                histTile[i].rectTransform.anchoredPosition = new Vector2(x, -136);
                histTile[i].color = Escurece(Lado(tr), 0.95f);
                string mo = (info.motivo ?? "").ToLower();
                histTxt[i].text = mo.Contains("explod") ? "◎" : mo.Contains("desarm") ? "◈" : mo.Contains("tempo") ? "–" : "✕";
                // o último round acabou de entrar: pulinho
                bool novo = i == n - 1 && m.t < 3;
                histTile[i].rectTransform.localScale = Vector3.one * (novo ? 1 + 0.3f * Mathf.Clamp01(1 - m.t / 1.5f) : 1);
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
            obsNome.text = $"<color={Hex(cor)}>{a.name}</color>";
            obsHp.text = $"✚ {a.hp}"; obsHp.color = a.hp > 40 ? CREME : VERMELHO;
            obsColete.text = a.armor > 0 ? $"{(a.helmet ? "◈" : "◇")} {a.armor}" : "";
            string mao = NaMao(a);
            obsArma.text = mao.StartsWith("n:") ? NomeNade(mao.Substring(2)) : mao == "c4" ? "C4" : mao == "kit" ? "kit de desarme" : NomeArma(mao);
            var oi = mao.StartsWith("n:") || mao == "c4" || mao == "kit" ? null : Icone(mao);
            obsIcone.texture = oi; obsIcone.gameObject.SetActive(oi != null);
            if (oi != null) obsIcone.rectTransform.sizeDelta = new Vector2(36f * oi.width / oi.height, 36);
            obsMun.text = a.faca || a.gun == null ? "" : a.gun.Reloading ? "<size=24>recarregando</size>" : $"{a.gun.inMag}<size=24> / {a.gun.mags * a.gun.s.mag}</size>";
        }

        void Banners(Sim.Partida pt, Match m, int rn, bool aT)
        {
            string chave = null, titulo = "", sub = ""; Color cor = ESCURO;
            if (m.over)
            {
                bool tr = m.winner == "TR";
                chave = "fim" + rn; titulo = tr ? "TR VENCEU" : "CT VENCEU"; cor = Escurece(Lado(tr), 0.8f);
                sub = Motivo(m.reason);
            }
            else if (m.t < 2.6f)
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
            float some = chave.StartsWith("ini") ? Mathf.Clamp01((2.6f - m.t) / 0.4f) : 1;
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
            for (int i = 0; i < 10; i++)
            {
                bool tem = i < m.agents.Count; pontos[i].gameObject.SetActive(tem); setas[i].gameObject.SetActive(tem);
                if (!tem) continue;
                var a = m.agents[i]; var p = NoMapa(a.pos);
                var cor = Lado(a.IsT);
                pontos[i].rectTransform.anchoredPosition = p;
                pontos[i].color = a.alive ? cor : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                float d = a.alive ? (seguir == a.id ? 17 + 2 * Mathf.Sin(tempo * 6) : 13) : 8;
                pontos[i].rectTransform.sizeDelta = new Vector2(d, d);
                setas[i].gameObject.SetActive(a.alive);
                setas[i].rectTransform.anchoredPosition = p;
                setas[i].rectTransform.localEulerAngles = new Vector3(0, 0, -a.aim * Mathf.Rad2Deg - 90);
                setas[i].color = new Color(cor.r, cor.g, cor.b, 0.9f);
            }
            bool c4 = m.bombPlanted || m.bombDropped;
            c4Ponto.gameObject.SetActive(c4);
            if (c4)
            {
                c4Ponto.rectTransform.anchoredPosition = NoMapa(m.bombPlanted ? m.bombPos : m.droppedBomb);
                c4Ponto.color = m.bombPlanted ? Color.Lerp(VERMELHO, Color.white, 0.5f + 0.5f * Mathf.Sin(tempo * 9)) : VERMELHO;
            }
            int n = 0;
            float k = mapaR.sizeDelta.x / MapData.NX;
            foreach (var s in m.smokes) Mancha(ref n, new V2(s.origin.x, s.origin.y), 34 * k * 2 * Mathf.Clamp01(s.age / 1.3f), new Color(0.92f, 0.92f, 0.92f, 0.7f));
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
