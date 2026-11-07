using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace CSM.View3D
{
    /// <summary>
    /// Peças de UI do HUD de maquete: painel arredondado (9-slice gerado na hora), círculo, texto TextMeshPro (SDF: nítido em
    /// qualquer resolução, sombra e contorno no material) com a Barlow Condensed. Tudo na camada UI (5), coordenadas em px
    /// da referência 1920x1080.
    /// </summary>
    public static class UiKit
    {
        public const int CAMADA_UI = 5;
        static Sprite redondo, circulo;
        static Font fonte, numero;
        static TMP_FontAsset sdfFonte, sdfNumero;
        static readonly System.Collections.Generic.Dictionary<(TMP_FontAsset, int), Material> estilos = new System.Collections.Generic.Dictionary<(TMP_FontAsset, int), Material>();
        // textura dos cantos em alta (raio 40 texels, com mipmap): o painel mostra raio 10 px via pixelsPerUnitMultiplier, então
        // a borda suave encolhe junto em vez de serrilhar (com 64 px e raio 10 a borda tinha 1 texel e quebrava ao reduzir)
        public const float RAIO_TEX = 40;

        // Barlow Condensed (OFL, Resources/Fontes): a fonte condensada das transmissões; sem ela, Bahnschrift do Windows
        public static Font Fonte => fonte != null ? fonte : fonte = Resources.Load<Font>("Fontes/BarlowCondensed-Bold") ?? Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI Semibold", "Arial" }, 32);
        public static Font Numero => numero != null ? numero : numero = Resources.Load<Font>("Fontes/BarlowCondensed-ExtraBold") ?? Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI Black", "Arial" }, 32);

        /// <summary>Fonte SDF gerada na hora a partir do TTF (atlas dinâmico: só os glifos usados entram).</summary>
        static TMP_FontAsset Sdf(Font f)
        {
            var a = TMP_FontAsset.CreateFontAsset(f, 72, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            a.name = f.name + " SDF";
            var sh = Shader.Find("TextMeshPro/Distance Field"); if (sh != null) a.material.shader = sh;
            // gera já os glifos de sempre: sem isso cada letra nova no meio da partida (nome no killfeed, call do IGL)
            // travava o quadro gerando o SDF (picos de até 50 ms no HUD)
            a.TryAddCharacters(GLIFOS, out _);
            return a;
        }
        const string GLIFOS = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                              "ÁÀÂÃÉÊÍÓÔÕÚÜÇáàâãéêíóôõúüç°ºª•—–…×";
        public static TMP_FontAsset FonteSdf => sdfFonte != null ? sdfFonte : sdfFonte = Sdf(Fonte);
        public static TMP_FontAsset NumeroSdf => sdfNumero != null ? sdfNumero : sdfNumero = Sdf(Numero);

        /// <summary>Material por estilo (0 liso, 1 sombra embaixo, 2 contorno escuro + sombra), compartilhado.</summary>
        static Material Estilo(TMP_FontAsset f, int tipo)
        {
            if (estilos.TryGetValue((f, tipo), out var m) && m != null) return m;
            m = new Material(f.material) { name = f.name + (tipo == 0 ? "" : tipo == 1 ? " sombra" : " contorno") };
            if (tipo >= 1)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, tipo == 2 ? 0.7f : 0.55f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
            }
            if (tipo == 2)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f); m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);   // contorno fino + letra engordada: o I de MIRA não some
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.05f, 0.08f, 0.9f));
            }
            ShaderUtilities.UpdateShaderRatios(m);
            estilos[(f, tipo)] = m;
            return m;
        }

        /// <summary>Contorno escuro em volta do texto (etiqueta de nome em cima do boneco: lê em cima de qualquer chão).</summary>
        public static void Contorno(TextMeshProUGUI t) => t.fontSharedMaterial = Estilo((TMP_FontAsset)t.font, 2);

        /// <summary>Raio do canto do painel em px da referência (a textura é uma só; escala pelo multiplicador).</summary>
        public static void Raio(Image im, float px) => im.pixelsPerUnitMultiplier = RAIO_TEX / Mathf.Max(0.5f, px);

        /// <summary>Retângulo de cantos redondos (raio 22 px na textura de 64) com borda suave.</summary>
        public static Sprite Redondo
        {
            get
            {
                if (redondo != null) return redondo;
                const int N = 128; const float R = RAIO_TEX;   // mostrado com raio 10 px (Raio): cantos menos redondos que pílula
                var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((R - d) / 2f + 0.5f) * 255));
                    }
                t.SetPixels32(px); t.Apply(true);
                redondo = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2), false);
                return redondo;
            }
        }

        public static Sprite Circulo
        {
            get
            {
                if (circulo != null) return circulo;
                const int N = 256;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - N / 2f) * (x + 0.5f - N / 2f) + (y + 0.5f - N / 2f) * (y + 0.5f - N / 2f));
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((N / 2f - 2 - d) / 2f + 0.5f) * 255));
                    }
                t.SetPixels32(px); t.Apply(true);
                circulo = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100);
                return circulo;
            }
        }

        public static RectTransform No(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, Vector2? pivo = null)
        {
            var g = new GameObject(nome, typeof(RectTransform)) { layer = CAMADA_UI };
            var r = (RectTransform)g.transform;
            r.SetParent(pai, false);
            r.anchorMin = r.anchorMax = ancora; r.pivot = pivo ?? ancora;
            r.anchoredPosition = pos; r.sizeDelta = tam;
            return r;
        }

        public static Image Painel(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, Color cor, Vector2? pivo = null, bool sombra = false)
        {
            var im = No(nome, pai, ancora, pos, tam, pivo).gameObject.AddComponent<Image>();
            im.sprite = Redondo; im.type = Image.Type.Sliced; im.color = cor; im.raycastTarget = false;
            Raio(im, Mathf.Min(HudTema.Raio, Mathf.Min(tam.x, tam.y) * 0.5f));   // 10 px no HUD clássico, 3 px no novo (decisão 0032)
            // sombra como efeito do próprio painel: some/anda junto com ele
            if (sombra && HudTema.Sombra) { var s = im.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.3f); s.effectDistance = new Vector2(0, -5); s.useGraphicAlpha = true; }
            return im;
        }

        public static Image Bola(string nome, Transform pai, Vector2 ancora, Vector2 pos, float d, Color cor)
        {
            var im = No(nome, pai, ancora, pos, new Vector2(d, d), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
            im.sprite = Circulo; im.color = cor; im.raycastTarget = false;
            return im;
        }

        /// <summary>Barra (preenche da esquerda ou da direita): devolve a imagem do preenchimento (fillAmount).</summary>
        public static Image Barra(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, Color fundo, Color cor, bool daDireita = false, Vector2? pivo = null)
        {
            var f = Painel(nome, pai, ancora, pos, tam, fundo, pivo);
            var b = No(nome + " cheio", f.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            var r = b.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            b.sprite = Texture2D.whiteTexture != null ? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f) : null;
            b.type = Image.Type.Filled; b.fillMethod = Image.FillMethod.Horizontal; b.fillOrigin = daDireita ? 1 : 0; b.color = cor; b.raycastTarget = false;
            return b;
        }

        public static TextMeshProUGUI Texto(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, string s, int tamanho, Color cor,
                                 TextAnchor alinha = TextAnchor.MiddleCenter, bool num = false, Vector2? pivo = null, bool sombra = true)
        {
            var t = No(nome, pai, ancora, pos, tam, pivo).gameObject.AddComponent<TextMeshProUGUI>();
            var f = num ? NumeroSdf : FonteSdf;
            t.font = f; t.fontSharedMaterial = Estilo(f, sombra ? 1 : 0);
            t.fontSize = tamanho; t.color = cor; t.alignment = Alinha(alinha); t.text = s;
            t.richText = true; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            t.fontStyle = FontStyles.Normal;   // o peso já vem do arquivo da fonte (Bold / ExtraBold)
            return t;
        }

        // meio na vertical = linha média das letras (maiúscula fica centrada de verdade, não pela altura do acento)
        static TextAlignmentOptions Alinha(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter => TextAlignmentOptions.Top, TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft, TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft, TextAnchor.LowerCenter => TextAlignmentOptions.Bottom, TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Midline,
        };

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // curvas de animação
        public static float SaiDeTras(float x) { x = Mathf.Clamp01(x); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2); }
        public static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        public static float SaiRapido(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }
    }
}
