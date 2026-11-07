using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Peças da interface do manager. O visual inteiro mora em Assets/Resources/ManagerUI/Manager.uss (classes de USS,
    /// as mesmas do mockup em prototipo/mockup-ui); aqui só se monta a árvore de elementos e se põem as classes.
    /// Regra: cor, tamanho de fonte, borda e espaçamento ficam no USS; o código só escolhe a classe.
    /// </summary>
    public static class Ui
    {
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        /// <summary>
        /// Fundo da interface: céu noturno em degradê (azul-noite no alto, brilho índigo e uma faixa quente no horizonte) com
        /// poucas estrelas. É o que aparece "atrás do vidro" dos painéis translúcidos. Gerado em código (sem imagem no repositório);
        /// pequeno e com filtro bilinear, a Unity estica para a tela.
        /// </summary>
        static Texture2D veu;
        /// <summary>Véu escuro de baixo para cima (transparente no alto, quase opaco embaixo): põe o texto legível sobre as artes.</summary>
        public static Texture2D VeuInferior()
        {
            if (veu != null) return veu;
            const int h = 128;
            veu = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VeuInferior", hideFlags = HideFlags.DontUnloadUnusedAsset };
            var cor = new Color(0.027f, 0.043f, 0.102f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);                       // 0 = embaixo
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.62f - v) / 0.62f)) * 0.94f;
                veu.SetPixel(0, y, new Color(cor.r, cor.g, cor.b, a));
            }
            veu.Apply(false, false);
            return veu;
        }

        public static Texture2D FundoNoite()
        {
            const int w = 480, h = 270;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "FundoNoite" };
            var topo = new Color(0.030f, 0.045f, 0.110f); var meio = new Color(0.045f, 0.060f, 0.150f); var base_ = new Color(0.085f, 0.070f, 0.140f);
            var px = new Color[w * h];
            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);                       // 0 = embaixo, 1 = no alto
                Color c = v > 0.45f ? Color.Lerp(meio, topo, (v - 0.45f) / 0.55f) : Color.Lerp(base_, meio, v / 0.45f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float dx = (u - 0.28f) * 1.6f, dy = v - 0.62f;
                    float brilho = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.55f);   // mancha índigo atrás do menu
                    float dx2 = (u - 0.85f) * 1.6f, dy2 = v - 0.25f;
                    float brilho2 = Mathf.Clamp01(1f - Mathf.Sqrt(dx2 * dx2 + dy2 * dy2) / 0.45f);
                    Color cc = c + new Color(0.03f, 0.06f, 0.20f) * (brilho * brilho) + new Color(0.07f, 0.04f, 0.16f) * (brilho2 * brilho2);
                    cc.a = 1f; px[y * w + x] = cc;
                }
            }
            for (int i = 0; i < 28; i++)                            // estrelas (mais no alto)
            {
                int x = rng.Next(w), y = (int)(h * (1f - (float)Math.Pow(rng.NextDouble(), 1.8) * 0.8f)) - 1;
                if (y < 0 || y >= h) continue;
                float b = 0.15f + (float)rng.NextDouble() * 0.30f;
                px[y * w + x] = Color.Lerp(px[y * w + x], Color.white, b);
            }
            tex.SetPixels(px); tex.Apply(false, false); tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }

        /// <summary>Classe de cor da nota (0..100): verde, azul, laranja, vermelho. Serve para texto (.verde) e barra (.fill.verde).</summary>
        public static string ClasseNota(int v) => v >= 80 ? "verde" : v >= 60 ? "azul" : v >= 40 ? "laranja" : "vermelho";

        public static VisualElement E(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label L(string texto, params string[] classes)
        {
            var l = new Label(texto); l.selection.isSelectable = false;
            l.AddToClassList("t");
            foreach (var c in classes) l.AddToClassList(c);
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Elemento clicável (o USS cuida do :hover).</summary>
        public static VisualElement Clicavel(VisualElement e, Action acao)
        {
            if (acao != null) e.RegisterCallback<ClickEvent>(_ => acao());
            return e;
        }

        // ------------------------------------------------------------------ botões e abas
        public static VisualElement Botao(string texto, Action acao, bool ativo = true, bool destaque = false, string icone = null, params string[] extra)
        {
            var b = E("btn");
            if (destaque && ativo) b.AddToClassList("destaque");
            if (!ativo) b.AddToClassList("desligado");
            foreach (var c in extra) b.AddToClassList(c);
            if (icone != null) b.Add(new Icone(icone, 18));
            b.Add(L(texto));
            if (ativo) Clicavel(b, acao);
            return b;
        }

        /// <summary>Abas segmentadas (caixa com borda; a escolhida fica preenchida).</summary>
        public static VisualElement Abas(string[] nomes, int atual, Action<int> escolher)
        {
            var a = E("abas");
            for (int i = 0; i < nomes.Length; i++)
            {
                int k = i;
                var ab = E("aba"); if (i == atual) ab.AddToClassList("ativo");
                ab.Add(L(nomes[i]));
                if (i != atual) Clicavel(ab, () => escolher(k));
                a.Add(ab);
            }
            return a;
        }

        public static VisualElement Selo(string texto, string cor = null)
        {
            var s = E("selo"); if (cor != null) s.AddToClassList(cor + "-bg");
            s.Add(L(texto)); return s;
        }

        // ------------------------------------------------------------------ identidade
        static int IndiceCor(string nome) { int h = 0; foreach (char ch in nome) h = h * 31 + ch; return (h & 0x7fffffff) % 6; }

        /// <summary>Avatar redondo com a inicial; a cor vem do nome.</summary>
        public static VisualElement Avatar(string nome, bool grande = false)
        {
            var a = E("av", "av" + IndiceCor(nome)); if (grande) a.AddToClassList("grande");
            a.Add(L(nome.Substring(0, 1)));
            return a;
        }

        /// <summary>Gancho para as dicas ao passar o mouse; a tela (GerenteUI) liga isto ao elemento de dica dela.</summary>
        public static Action<VisualElement, string> Dica;
        public static void ComDica(VisualElement e, string texto) => Dica?.Invoke(e, texto);

        /// <summary>Render 3D do boneco (um só, por enquanto: todos os jogadores usam o mesmo modelo); null se não houver.</summary>
        public static Texture RetratoBoneco;

        /// <summary>Retrato do jogador: o boneco 3D sobre um fundo da cor dele (sem o render, cai para a inicial).
        /// <paramref name="tamanho"/>: "retrato-p" (caixa pequena) ou "retrato-fill" (preenche o pai).</summary>
        public static VisualElement Retrato(string nome, string tamanho)
        {
            var e = E("retrato", tamanho, "rt" + IndiceCor(nome));
            if (RetratoBoneco is RenderTexture rt) e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else e.Add(L(nome.Substring(0, 1)));
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        /// <summary>Brasão provisório do clube: quadrado arredondado com a inicial. cor = "laranja" ou "azul".</summary>
        public static VisualElement Brasao(string nome, string cor, bool redondo = false)
        {
            var b = E("brasao", cor + "-bg"); if (redondo) b.AddToClassList("mini-b");
            b.Add(L(nome.Substring(0, 1)));
            return b;
        }

        // ------------------------------------------------------------------ atributos
        public static VisualElement Barra(int v, bool fina = false)
        {
            var f = E("barra"); if (fina) f.AddToClassList("fina");
            var d = E("fill", ClasseNota(v)); d.style.width = Length.Percent(Mathf.Clamp(v, 0, 100));
            f.Add(d); f.pickingMode = PickingMode.Ignore;
            return f;
        }

        /// <summary>Linha de atributo dos cartões: nome, barra que estica e número.</summary>
        public static VisualElement AtributoLinha(string nome, int v)
        {
            var r = E("attr");
            r.Add(L(nome, "attr-nome"));
            r.Add(Barra(v));
            r.Add(L(v.ToString(), "attr-num", ClasseNota(v)));
            return r;
        }

        // ------------------------------------------------------------------ tabela
        /// <summary>Célula de largura fixa da tabela (classe c-j, c-g, c-a...); o filho entra dentro.</summary>
        public static VisualElement Celula(string classe, VisualElement filho = null)
        {
            var c = E("c", classe);
            if (filho != null) c.Add(filho);
            return c;
        }

        /// <summary>Textura de quadrados do topo da pré-partida (some para baixo); a UI Toolkit não tem gradiente em USS.</summary>
        public class Quadriculado : VisualElement
        {
            public Quadriculado()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += mgc =>
                {
                    var p = mgc.painter2D; float h = contentRect.height, w = contentRect.width; const float T = 56;
                    for (int iy = 0; iy * T < h; iy++)
                        for (int ix = 0; ix * T < w; ix++)
                        {
                            float a = Mathf.Clamp01(1f - (iy * T + T / 2) / h) * 0.8f;
                            p.fillColor = (ix + iy) % 2 == 0 ? new Color(0.078f, 0.078f, 0.086f, a) : new Color(0.047f, 0.047f, 0.055f, a);
                            p.BeginPath(); p.MoveTo(new Vector2(ix * T, iy * T)); p.LineTo(new Vector2((ix + 1) * T, iy * T));
                            p.LineTo(new Vector2((ix + 1) * T, (iy + 1) * T)); p.LineTo(new Vector2(ix * T, (iy + 1) * T)); p.ClosePath(); p.Fill();
                        }
                };
                RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            }
        }

        /// <summary>Barra de cansaço (0..100): ao contrário das notas, mais alto é pior (verde, laranja, vermelho).</summary>
        public static VisualElement BarraCansaco(float v)
        {
            var f = E("barra", "fina");
            var d = E("fill", v < 35 ? "verde-c" : v < 60 ? "laranja-c" : "vermelho-c"); d.style.width = Length.Percent(Mathf.Clamp(v, 0, 100));
            f.Add(d); f.pickingMode = PickingMode.Ignore;
            return f;
        }

        /// <summary>Botão pequeno (opções, passos, filtros). <paramref name="on"/> = a opção escolhida.</summary>
        public static VisualElement BotaoMini(string texto, Action acao, bool on = false, bool ativo = true, string icone = null)
        {
            var b = E("btn-mini"); if (on) b.AddToClassList("on"); if (!ativo) b.AddToClassList("desligado");
            if (icone != null) { var ic = new Icone(icone, 16); ic.style.marginRight = 6; b.Add(ic); }
            b.Add(L(texto));
            if (ativo) Clicavel(b, acao);
            return b;
        }

        /// <summary>Botão só com ícone (passo de contador).</summary>
        public static VisualElement BotaoIcone(string icone, Action acao, bool ativo = true)
        {
            var b = E("btn", "btn-ic"); if (!ativo) b.AddToClassList("desligado");
            b.Add(new Icone(icone, 16));
            if (ativo) Clicavel(b, acao);
            return b;
        }

        /// <summary>Célula de atributo: número e barrinha fina embaixo.</summary>
        public static VisualElement CelulaAtributo(int v)
        {
            var c = E("c", "c-a", "c-a-col");
            c.Add(L(v.ToString(), "f17", "negrito", ClasseNota(v)));   // sem barra: na tabela, a cor já diz se é bom (decisão 0023)
            return c;
        }
    }
}
