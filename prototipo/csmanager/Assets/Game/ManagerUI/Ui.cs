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
            var l = new Label(texto);
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
        public static VisualElement Botao(string texto, Action acao, bool ativo = true, bool lima = false, string icone = null, params string[] extra)
        {
            var b = E("btn");
            if (lima && ativo) b.AddToClassList("lima");
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
        /// <summary>Avatar redondo com a inicial (ainda não há retrato de jogador); a cor vem do nome.</summary>
        public static VisualElement Avatar(string nome, bool grande = false)
        {
            int h = 0; foreach (char ch in nome) h = h * 31 + ch;
            var a = E("av", "av" + ((h & 0x7fffffff) % 6)); if (grande) a.AddToClassList("grande");
            a.Add(L(nome.Substring(0, 1)));
            return a;
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

        /// <summary>Célula de atributo: número e barrinha fina embaixo.</summary>
        public static VisualElement CelulaAtributo(int v)
        {
            var c = E("c", "c-a", "c-a-col");
            c.Add(L(v.ToString(), "f16", "negrito", ClasseNota(v)));
            c.Add(Barra(v, true));
            return c;
        }
    }
}
