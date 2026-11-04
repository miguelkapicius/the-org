using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Peças visuais da interface do manager (UI Toolkit montado em código, sem UXML/USS): cores, caixas, textos, botões.
    /// Só usa estilo inline e elementos básicos (VisualElement/Label), então não depende do tema do PanelSettings.
    /// Paleta: laranja = o nosso time (começa de TR no protótipo), azul = adversário, como na partida.
    /// </summary>
    public static class Ui
    {
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        public static readonly Color Fundo = Hex("0f1217"), Painel = Hex("171b22"), Painel2 = Hex("1f242d"), Linha = Hex("2a303b"),
            Texto = Hex("e6e9ef"), Suave = Hex("8b93a1"), Apagado = Hex("5b6270"),
            Laranja = Hex("f0aa46"), Azul = Hex("6eb4ff"), Verde = Hex("4cc38a"), Vermelho = Hex("e5534b");

        public static Color CorNota(int v) => v >= 80 ? Verde : v >= 60 ? Azul : v >= 40 ? Laranja : Vermelho;

        public static void Pad(VisualElement e, float v) => Pad(e, v, v);
        public static void Pad(VisualElement e, float h, float v)
        {
            e.style.paddingLeft = h; e.style.paddingRight = h; e.style.paddingTop = v; e.style.paddingBottom = v;
        }

        public static void Raio(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public static VisualElement Box(FlexDirection dir = FlexDirection.Column, Color? fundo = null, float pad = 0, float raio = 0)
        {
            var e = new VisualElement();
            e.style.flexDirection = dir;
            if (fundo.HasValue) e.style.backgroundColor = fundo.Value;
            if (pad > 0) Pad(e, pad);
            if (raio > 0) Raio(e, raio);
            return e;
        }

        public static VisualElement Linha_(FlexDirection dir = FlexDirection.Row)
        {
            var e = Box(dir); e.style.alignItems = Align.Center; return e;
        }

        public static Label Txt(string t, int tamanho = 16, Color? cor = null, bool negrito = false)
        {
            var l = new Label(t);
            l.style.fontSize = tamanho;
            l.style.color = cor ?? Texto;
            if (negrito) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Texto que quebra linha (parágrafos, avisos).</summary>
        public static Label Paragrafo(string t, int tamanho = 14, Color? cor = null)
        {
            var l = Txt(t, tamanho, cor ?? Suave); l.style.whiteSpace = WhiteSpace.Normal; return l;
        }

        /// <summary>Cartão com título: o bloco básico das telas.</summary>
        public static VisualElement Cartao(string titulo, out VisualElement corpo, float pad = 20)
        {
            var c = Box(FlexDirection.Column, Painel, pad, 10);
            c.style.marginBottom = 16;
            if (titulo != null)
            {
                var t = Txt(titulo.ToUpperInvariant(), 13, Suave, true); t.style.marginBottom = 12; c.Add(t);
            }
            corpo = Box(); c.Add(corpo);
            return c;
        }

        public static VisualElement Botao(string texto, Action acao, bool ativo = true, Color? cor = null, float largura = -1)
        {
            var b = Box(FlexDirection.Row, null, 0, 6);
            Color fundo = ativo ? (cor ?? Painel2) : Hex("14171c");
            b.style.backgroundColor = fundo;
            Pad(b, 16, 9);
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            if (largura > 0) b.style.width = largura;
            bool escuro = cor.HasValue && ativo;
            b.Add(Txt(texto, 15, !ativo ? Apagado : escuro ? Hex("1a1306") : Texto, true));
            if (ativo)
            {
                Color hover = Color.Lerp(fundo, Color.white, 0.14f);
                b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = hover);
                b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = fundo);
                b.RegisterCallback<ClickEvent>(_ => acao());
            }
            return b;
        }

        public static VisualElement Barra(int v, float largura = 52)
        {
            var f = Box(); f.style.width = largura; f.style.height = 6; f.style.backgroundColor = Linha; Raio(f, 3);
            var d = Box(); d.style.height = 6; d.style.width = Length.Percent(Mathf.Clamp(v, 0, 100)); d.style.backgroundColor = CorNota(v); Raio(d, 3);
            f.Add(d);
            f.pickingMode = PickingMode.Ignore;
            return f;
        }

        /// <summary>Número + barra de um atributo (0..100).</summary>
        public static VisualElement Atributo(int v)
        {
            var r = Linha_();
            var n = Txt(v.ToString(), 14, CorNota(v), true); n.style.width = 30;
            r.Add(n); r.Add(Barra(v));
            return r;
        }

        /// <summary>Uma célula de largura fixa (w > 0) ou que estica (w <= 0) numa linha de tabela.</summary>
        public static VisualElement Celula(VisualElement filho, float w)
        {
            var c = Linha_();
            if (w > 0) { c.style.width = w; c.style.flexShrink = 0; } else c.style.flexGrow = 1;
            c.Add(filho);
            return c;
        }

        public static VisualElement LinhaTabela(bool cabecalho = false)
        {
            var r = Linha_();
            Pad(r, 12, cabecalho ? 6 : 10);
            r.style.borderBottomWidth = 1; r.style.borderBottomColor = Linha;
            return r;
        }

        public static VisualElement Selo(string texto, Color cor)
        {
            var s = Box(FlexDirection.Row, new Color(cor.r, cor.g, cor.b, 0.16f), 0, 4);
            Pad(s, 8, 3);
            s.Add(Txt(texto, 12, cor, true));
            return s;
        }
    }
}
