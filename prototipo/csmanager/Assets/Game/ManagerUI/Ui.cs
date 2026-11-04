using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Peças visuais da interface do manager (UI Toolkit montado em código, sem UXML/USS): cores, caixas, textos, botões,
    /// avatares, abas. Só usa estilo inline e elementos básicos (VisualElement/Label), então não depende do tema do PanelSettings.
    /// Conceito: tema escuro, cartões bem arredondados com borda fina, barra lateral com contadores, abas em pílula e uma
    /// cor de destaque (lima) só para a ação principal. Laranja = nosso time, azul = adversário, como na partida.
    /// </summary>
    public static class Ui
    {
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        public static readonly Color Fundo = Hex("0a0a0c"), Janela = Hex("121214"), Lateral = Hex("161619"), Painel = Hex("1a1a1e"),
            Painel2 = Hex("27272d"), Linha = Hex("2b2b31"), Texto = Hex("f2f2f3"), Suave = Hex("8f8f99"), Apagado = Hex("5c5c66"),
            Lima = Hex("c6f432"), Laranja = Hex("f0aa46"), Azul = Hex("6eb4ff"), Verde = Hex("4cc38a"), Vermelho = Hex("e5534b"),
            Transparente = new Color(0, 0, 0, 0);

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

        public static void Borda(VisualElement e, Color c, float w = 1)
        {
            e.style.borderTopWidth = w; e.style.borderRightWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w;
            e.style.borderTopColor = c; e.style.borderRightColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c;
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

        /// <summary>Linha horizontal com os itens alinhados ao centro.</summary>
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

        /// <summary>Cartão com borda fina e cantos grandes; o título (opcional) fica em caixa alta, pequeno e apagado.</summary>
        public static VisualElement Cartao(string titulo, out VisualElement corpo, float pad = 22)
        {
            var c = Box(FlexDirection.Column, Painel, pad, 16);
            Borda(c, Linha);
            c.style.marginBottom = 18;
            if (titulo != null)
            {
                var t = Txt(titulo.ToUpperInvariant(), 12, Suave, true); t.style.marginBottom = 14; c.Add(t);
            }
            corpo = Box(); c.Add(corpo);
            return c;
        }

        public static VisualElement Botao(string texto, Action acao, bool ativo = true, Color? cor = null, float largura = -1)
        {
            var b = Box(FlexDirection.Row, null, 0, 10);
            Color fundo = ativo ? (cor ?? Painel2) : Hex("17171a");
            b.style.backgroundColor = fundo;
            Pad(b, 18, 11);
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            if (largura > 0) b.style.width = largura;
            bool escuro = cor.HasValue && ativo;   // botão colorido (ação principal) leva texto escuro
            b.Add(Txt(texto, 15, !ativo ? Apagado : escuro ? Hex("12140a") : Texto, true));
            if (ativo)
            {
                Color hover = Color.Lerp(fundo, Color.white, 0.14f);
                b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = hover);
                b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = fundo);
                b.RegisterCallback<ClickEvent>(_ => acao());
            }
            return b;
        }

        /// <summary>Abas em pílula (Todos | Titulares | Reservas): a escolhida fica preenchida.</summary>
        public static VisualElement Chips(string[] nomes, int atual, Action<int> escolher)
        {
            var linha = Linha_();
            for (int i = 0; i < nomes.Length; i++)
            {
                int k = i; bool sel = i == atual;
                var ch = Box(FlexDirection.Row, sel ? Painel2 : Transparente, 0, 10);
                Pad(ch, 16, 8); ch.style.marginRight = 8;
                Borda(ch, sel ? Painel2 : Linha);
                ch.Add(Txt(nomes[i], 14, sel ? Texto : Suave, sel));
                if (!sel) ch.RegisterCallback<ClickEvent>(_ => escolher(k));
                linha.Add(ch);
            }
            return linha;
        }

        /// <summary>Cartão de ação (como "Novo documento"): ícone, título, subtítulo; clicável.</summary>
        public static VisualElement Tile(string glifo, string titulo, string sub, Action acao, bool destaque = false, bool ultimo = false)
        {
            Color fundo = Painel;
            var t = Box(FlexDirection.Column, fundo, 18, 14);
            Borda(t, destaque ? Lima : Linha);
            t.style.flexGrow = 1; t.style.flexBasis = 0;
            if (!ultimo) t.style.marginRight = 14;
            var ic = Box(FlexDirection.Row, destaque ? Lima : Painel2, 0, 9);
            ic.style.width = 36; ic.style.height = 36; ic.style.alignItems = Align.Center; ic.style.justifyContent = Justify.Center; ic.style.marginBottom = 18;
            ic.Add(Txt(glifo, 16, destaque ? Hex("12140a") : Texto, true));
            t.Add(ic);
            t.Add(Txt(titulo, 17, Texto, true));
            var s = Txt(sub, 13, Suave); s.style.marginTop = 2; t.Add(s);
            Color hover = Color.Lerp(fundo, Color.white, 0.05f);
            t.RegisterCallback<PointerEnterEvent>(_ => t.style.backgroundColor = hover);
            t.RegisterCallback<PointerLeaveEvent>(_ => t.style.backgroundColor = fundo);
            t.RegisterCallback<ClickEvent>(_ => acao());
            return t;
        }

        /// <summary>Contador pequeno ao lado de um item do menu.</summary>
        public static VisualElement Contagem(string texto)
        {
            var c = Box(FlexDirection.Row, Painel2, 0, 7); Pad(c, 8, 2);
            c.Add(Txt(texto, 12, Suave, true));
            return c;
        }

        static readonly Color[] CORES_AVATAR = { Hex("e07a5f"), Hex("3d85c6"), Hex("81b29a"), Hex("9b72cf"), Hex("f2cc8f"), Hex("5fa8a3") };

        /// <summary>Avatar redondo com a inicial (ainda não há retrato de jogador); a cor vem do nome.</summary>
        public static VisualElement Avatar(string nome, float tam)
        {
            int h = 0; foreach (char ch in nome) h = h * 31 + ch;
            var a = Box(); a.style.width = tam; a.style.height = tam; a.style.flexShrink = 0; Raio(a, tam / 2);
            a.style.backgroundColor = CORES_AVATAR[(h & 0x7fffffff) % CORES_AVATAR.Length];
            a.style.alignItems = Align.Center; a.style.justifyContent = Justify.Center;
            a.Add(Txt(nome.Substring(0, 1), (int)(tam * 0.42f), Hex("14141a"), true));
            return a;
        }

        /// <summary>Brasão provisório do clube: quadrado arredondado com a inicial.</summary>
        public static VisualElement Brasao(string nome, float tam, Color cor)
        {
            var a = Box(); a.style.width = tam; a.style.height = tam; a.style.flexShrink = 0; Raio(a, tam * 0.28f);
            a.style.backgroundColor = cor; a.style.alignItems = Align.Center; a.style.justifyContent = Justify.Center;
            a.Add(Txt(nome.Substring(0, 1), (int)(tam * 0.5f), Hex("14141a"), true));
            return a;
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

        /// <summary>Atributo com nome (nos cartões dos jogadores): nome, barra que estica e número.</summary>
        public static VisualElement AtributoLinha(string nome, int v)
        {
            var r = Linha_(); r.style.height = 24;
            var n = Txt(nome, 12, Suave); n.style.width = 66; r.Add(n);
            var b = Barra(v, 0); b.style.flexGrow = 1; b.style.width = StyleKeyword.Auto; r.Add(b);
            var num = Txt(v.ToString(), 13, CorNota(v), true); num.style.width = 30; num.style.unityTextAlign = TextAnchor.MiddleRight; r.Add(num);
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
            Pad(r, 18, cabecalho ? 10 : 12);
            r.style.borderBottomWidth = 1; r.style.borderBottomColor = Linha;
            return r;
        }

        public static VisualElement Selo(string texto, Color cor)
        {
            var s = Box(FlexDirection.Row, new Color(cor.r, cor.g, cor.b, 0.16f), 0, 7);
            Pad(s, 9, 3);
            s.Add(Txt(texto, 12, cor, true));
            return s;
        }
    }
}
