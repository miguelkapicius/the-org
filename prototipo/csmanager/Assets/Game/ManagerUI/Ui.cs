using System;
using System.Collections.Generic;
using System.Linq;
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

        static Texture2D veu;
        /// <summary>Véu escuro de baixo para cima (transparente no alto, quase opaco embaixo): põe o texto legível sobre as artes.</summary>
        public static Texture2D VeuInferior()
        {
            if (veu != null) return veu;
            const int h = 128;
            veu = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VeuInferior", hideFlags = HideFlags.DontUnloadUnusedAsset };
            var cor = new Color(0.043f, 0.051f, 0.063f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);                       // 0 = embaixo
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.78f - v) / 0.78f)) * 0.96f;
                veu.SetPixel(0, y, new Color(cor.r, cor.g, cor.b, a));
            }
            veu.Apply(false, false);
            return veu;
        }

        static Texture2D veuLat;
        /// <summary>Véu horizontal: escuro à esquerda e transparente à direita, para o texto ficar legível sobre a foto do jogador.</summary>
        public static Texture2D VeuLateral()
        {
            if (veuLat != null) return veuLat;
            const int w = 128;
            veuLat = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VeuLateral", hideFlags = HideFlags.DontUnloadUnusedAsset };
            var cor = new Color(0.043f, 0.051f, 0.063f);
            for (int x = 0; x < w; x++)
            {
                float v = x / (float)(w - 1);                       // 0 = esquerda
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.8f - v) / 0.66f));   // opaco na borda esquerda: sem emenda com o painel
                veuLat.SetPixel(x, 0, new Color(cor.r, cor.g, cor.b, a));
            }
            veuLat.Apply(false, false);
            return veuLat;
        }

        static Texture2D fotoTR, fotoCT;
        /// <summary>Foto do jogador no lado TR ou CT (Resources/ManagerUI/Retratos); null se o arquivo não existir.</summary>
        public static Texture2D FotoLado(bool tr)
        {
            if (tr) return fotoTR != null ? fotoTR : (fotoTR = Resources.Load<Texture2D>("ManagerUI/Retratos/tr"));
            return fotoCT != null ? fotoCT : (fotoCT = Resources.Load<Texture2D>("ManagerUI/Retratos/ct"));
        }

        /// <summary>Foto que cobre a caixa e fica alinhada ao topo (o rosto nunca é cortado, mesmo em caixa larga).</summary>
        public static void AlinharAoTopo(VisualElement e)
        {
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
        }

        /// <summary>O lado da foto: o lado em que o jogador rende mais; se está equilibrado, um dos dois pelo nome (sempre o mesmo).</summary>
        public static bool FotoTR(CSM.Manager.Jogador j) => j.LadoForte ?? ((IndiceCor(j.nome) & 1) == 0);

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

        // ------------------------------------------------------------------ fotos dos jogadores (decisão 0027)
        // Resources/ManagerUI/Jogadores/p01..pNN: retratos de estúdio, por faixa de idade (1-12 até 22 anos, 13-24 de 23 a 27, 25 em diante com 28+).
        const string PASTA_FOTOS = "ManagerUI/Jogadores/p";
        static readonly Dictionary<int, Texture2D> fotosCache = new Dictionary<int, Texture2D>();
        static int fotosTotal = -1;

        /// <summary>Quantos retratos existem na pasta (conta até o primeiro que falta).</summary>
        public static int FotosTotal()
        {
            if (fotosTotal >= 0) return fotosTotal;
            int n = 0; while (CarregaFoto(n + 1) != null) n++;
            return fotosTotal = n;
        }

        static Texture2D CarregaFoto(int id)
        {
            if (fotosCache.TryGetValue(id, out var t)) return t;
            return fotosCache[id] = Resources.Load<Texture2D>(PASTA_FOTOS + id.ToString("00"));
        }

        /// <summary>O retrato do jogador (null se ainda não tem ou se a pasta não existe).</summary>
        public static Texture2D FotoJogador(CSM.Manager.Jogador j) => j.fotoId > 0 ? CarregaFoto(j.fotoId) : null;

        /// <summary>Dá um retrato fixo a quem ainda não tem: da faixa de idade dele e, sempre que der, sem repetir um rosto que já está
        /// em uso entre os <paramref name="visiveis"/> (elenco e mercado). A escolha é pelo nome, então é sempre a mesma.</summary>
        public static void AtribuirFotos(IEnumerable<CSM.Manager.Jogador> visiveis)
        {
            int total = FotosTotal(); if (total == 0) return;
            var todos = visiveis.ToList();
            var usados = new HashSet<int>(todos.Where(x => x.fotoId > 0).Select(x => x.fotoId));
            foreach (var j in todos.Where(x => x.fotoId <= 0))
            {
                int lo = j.idade <= 22 ? 1 : j.idade <= 27 ? 13 : 25, hi = j.idade <= 22 ? 12 : j.idade <= 27 ? 24 : 32;
                lo = Mathf.Min(lo, total); hi = Mathf.Min(hi, total);
                var candidatos = Enumerable.Range(lo, hi - lo + 1).Where(i => !usados.Contains(i)).ToList();
                if (candidatos.Count == 0) candidatos = Enumerable.Range(1, total).Where(i => !usados.Contains(i)).ToList();
                if (candidatos.Count == 0) candidatos = Enumerable.Range(lo, hi - lo + 1).ToList();
                int h = 7; foreach (char ch in j.nome) h = h * 31 + ch;
                j.fotoId = candidatos[(h & 0x7fffffff) % candidatos.Count];
                usados.Add(j.fotoId);
            }
        }

        static Texture2D rimTR, rimCT;
        /// <summary>Luz de contorno do lado: laranja vinda da esquerda (TR) ou azul vinda da direita (CT), que esmaece para dentro da foto.</summary>
        static Texture2D RimLado(bool tr)
        {
            if (tr ? rimTR != null : rimCT != null) return tr ? rimTR : rimCT;
            const int w = 96;
            var t = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = tr ? "RimTR" : "RimCT", hideFlags = HideFlags.DontUnloadUnusedAsset };
            var cor = tr ? new Color(0.94f, 0.65f, 0.25f) : new Color(0.38f, 0.66f, 0.93f);
            for (int x = 0; x < w; x++)
            {
                float d = tr ? x / (float)(w - 1) : 1f - x / (float)(w - 1);   // 0 = na borda da luz
                t.SetPixel(x, 0, new Color(cor.r, cor.g, cor.b, Mathf.Pow(1f - d, 3.4f) * 0.16f));
            }
            t.Apply(false, false);
            if (tr) rimTR = t; else rimCT = t;
            return t;
        }

        /// <summary>Acrescenta a luz de contorno do lado forte do jogador (nada se ele rende igual nos dois lados).</summary>
        public static void AddRim(VisualElement foto, CSM.Manager.Jogador j)
        {
            var lado = j.LadoForte; if (lado == null) return;
            var rim = E("retrato-rim"); rim.pickingMode = PickingMode.Ignore;
            rim.style.backgroundImage = new StyleBackground(RimLado(lado.Value)); rim.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            foto.Add(rim);
        }

        /// <summary>Retrato do jogador: o rosto dele (foto de estúdio) com a luz do lado forte. Sem foto, cai para o operador do lado e, por fim, a inicial.
        /// <paramref name="tamanho"/>: "retrato-p" (caixa pequena), "retrato-fill" (preenche o pai) ou uma classe de tamanho própria.</summary>
        public static VisualElement Retrato(CSM.Manager.Jogador j, string tamanho)
        {
            var e = E("retrato", tamanho, "rt" + IndiceCor(j.nome));
            var foto = FotoJogador(j) ?? FotoLado(FotoTR(j));
            if (foto != null)
            {
                e.style.backgroundImage = new StyleBackground(foto); e.AddToClassList("foto"); AlinharAoTopo(e); AddRim(e, j);
                // nas fotos redondas pequenas o rosto precisa ocupar a caixa: aproxima e mantém o topo
                if (tamanho.StartsWith("neg-foto") || tamanho.StartsWith("vit-top-foto")) e.style.backgroundSize = new BackgroundSize(Length.Percent(190f), Length.Percent(190f));
            }
            else if (RetratoBoneco is RenderTexture rt) e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else e.Add(L(j.nome.Substring(0, 1)));
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
