using UnityEngine;
using UnityEngine.UI;

namespace CSM.View3D
{
    /// <summary>
    /// Peças de UI do HUD de maquete: painel arredondado (9-slice gerado na hora), círculo, texto com fonte do sistema
    /// (Segoe UI Black pra nome/rótulo, Bahnschrift pra número) e sombra. Tudo na camada UI (5), coordenadas em px da
    /// referência 1920x1080.
    /// </summary>
    public static class UiKit
    {
        public const int CAMADA_UI = 5;
        static Sprite redondo, circulo;
        static Font fonte, numero;

        public static Font Fonte => fonte != null ? fonte : fonte = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Black", "Segoe UI", "Segoe UI Symbol", "Arial" }, 32);
        public static Font Numero => numero != null ? numero : numero = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI Black", "Arial" }, 32);

        /// <summary>Retângulo de cantos redondos (raio 22 px na textura de 64) com borda suave.</summary>
        public static Sprite Redondo
        {
            get
            {
                if (redondo != null) return redondo;
                const int N = 64; const float R = 22;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(R - d + 0.5f) * 255));
                    }
                t.SetPixels32(px); t.Apply();
                redondo = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
                return redondo;
            }
        }

        public static Sprite Circulo
        {
            get
            {
                if (circulo != null) return circulo;
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - N / 2f) * (x + 0.5f - N / 2f) + (y + 0.5f - N / 2f) * (y + 0.5f - N / 2f));
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(N / 2f - 1 - d + 0.5f) * 255));
                    }
                t.SetPixels32(px); t.Apply();
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
            // sombra como efeito do próprio painel: some/anda junto com ele
            if (sombra) { var s = im.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.3f); s.effectDistance = new Vector2(0, -5); s.useGraphicAlpha = true; }
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

        public static Text Texto(string nome, Transform pai, Vector2 ancora, Vector2 pos, Vector2 tam, string s, int tamanho, Color cor,
                                 TextAnchor alinha = TextAnchor.MiddleCenter, bool num = false, Vector2? pivo = null, bool sombra = true)
        {
            var t = No(nome, pai, ancora, pos, tam, pivo).gameObject.AddComponent<Text>();
            t.font = num ? Numero : Fonte; t.fontSize = tamanho; t.color = cor; t.alignment = alinha; t.text = s;
            t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (num) t.fontStyle = FontStyle.Bold;
            if (sombra) { var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.45f); sh.effectDistance = new Vector2(0, -2); }
            return t;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // curvas de animação
        public static float SaiDeTras(float x) { x = Mathf.Clamp01(x); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2); }
        public static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        public static float SaiRapido(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }
    }
}
