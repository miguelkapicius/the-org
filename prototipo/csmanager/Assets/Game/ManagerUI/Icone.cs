using UnityEngine;
using UnityEngine.UIElements;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Ícone de traço fino desenhado em código (Painter2D), sem imagens: a UI Toolkit não carrega SVG sem pacote extra.
    /// Os desenhos são os do mockup (prototipo/mockup-ui), numa grade de 24x24, e usam a cor de texto do elemento
    /// (<c>color</c> no USS), então hover/ativo mudam o ícone junto com o texto.
    /// </summary>
    public class Icone : VisualElement
    {
        readonly string nome;

        public Icone(string nome, float tam = 22)
        {
            this.nome = nome;
            AddToClassList("ic");
            style.width = tam; style.height = tam;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Desenhar;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        void Desenhar(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            float s = Mathf.Min(contentRect.width, contentRect.height) / 24f;
            if (s <= 0) return;
            Color cor = resolvedStyle.color;
            Vector2 V(float x, float y) => new Vector2(x * s, y * s);
            void Linha(params float[] xy)
            {
                p.BeginPath(); p.MoveTo(V(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(V(xy[i], xy[i + 1]));
                p.Stroke();
            }
            void Circulo(float cx, float cy, float r, bool cheio = false)
            {
                p.BeginPath(); p.Arc(V(cx, cy), r * s, Angle.Degrees(0), Angle.Degrees(360));
                if (cheio) p.Fill(); else p.Stroke();
            }
            void Retang(float x, float y, float w, float h, float r)
            {
                p.BeginPath(); p.MoveTo(V(x + r, y));
                p.ArcTo(V(x + w, y), V(x + w, y + h), r * s); p.ArcTo(V(x + w, y + h), V(x, y + h), r * s);
                p.ArcTo(V(x, y + h), V(x, y), r * s); p.ArcTo(V(x, y), V(x + w, y), r * s);
                p.ClosePath(); p.Stroke();
            }

            p.lineWidth = 1.7f * s; p.strokeColor = cor; p.fillColor = cor;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;

            switch (nome)
            {
                case "grid":
                    Retang(3.5f, 3.5f, 7, 7, 1.8f); Retang(13.5f, 3.5f, 7, 7, 1.8f);
                    Retang(3.5f, 13.5f, 7, 7, 1.8f); Retang(13.5f, 13.5f, 7, 7, 1.8f); break;
                case "users":
                    Circulo(9, 8, 3.5f);
                    p.BeginPath(); p.MoveTo(V(2.5f, 20)); p.BezierCurveTo(V(2.5f, 16.4f), V(5.4f, 14), V(9, 14)); p.BezierCurveTo(V(12.6f, 14), V(15.5f, 16.4f), V(15.5f, 20)); p.Stroke();
                    Circulo(17.5f, 9, 2.5f);
                    p.BeginPath(); p.MoveTo(V(17, 14.2f)); p.BezierCurveTo(V(19.7f, 14.4f), V(21.5f, 16.3f), V(21.5f, 19)); p.Stroke(); break;
                case "swap":
                    Linha(4, 8, 19, 8); Linha(15.5f, 4.5f, 19, 8, 15.5f, 11.5f);
                    Linha(20, 16, 5, 16); Linha(8.5f, 12.5f, 5, 16, 8.5f, 19.5f); break;
                case "dollar":
                    Circulo(12, 12, 9);
                    p.BeginPath(); p.Arc(V(12, 10.1f), 2.1f * s, Angle.Degrees(330), Angle.Degrees(90), ArcDirection.CounterClockwise);
                    p.Arc(V(12, 14.3f), 2.1f * s, Angle.Degrees(270), Angle.Degrees(150), ArcDirection.Clockwise); p.Stroke();
                    Linha(12, 6, 12, 7.8f); Linha(12, 16.6f, 12, 18); break;
                case "house":
                    Retang(4.5f, 3.5f, 15, 17, 2);
                    Linha(9, 8, 11, 8); Linha(13, 8, 15, 8); Linha(9, 12, 11, 12); Linha(13, 12, 15, 12); Linha(10, 20.5f, 10, 16.5f, 14, 16.5f, 14, 20.5f); break;
                case "search": Circulo(11, 11, 6.5f); Linha(20, 20, 15.8f, 15.8f); break;
                case "plus": Linha(12, 5, 12, 19); Linha(5, 12, 19, 12); break;
                case "play": Linha(7.5f, 4.5f, 7.5f, 19.5f, 19.5f, 12, 7.5f, 4.5f); break;
                case "playf":
                    p.BeginPath(); p.MoveTo(V(8, 5)); p.LineTo(V(8, 19)); p.LineTo(V(19, 12)); p.ClosePath(); p.Fill(); break;
                case "skip": Linha(5, 5, 5, 19, 14.5f, 12, 5, 5); Linha(18, 5, 18, 19); break;
                case "up": Linha(12, 19, 12, 5); Linha(6, 11, 12, 5, 18, 11); break;
                case "down": Linha(12, 5, 12, 19); Linha(6, 13, 12, 19, 18, 13); break;
                case "tag":
                    Linha(3.5f, 12.5f, 3.5f, 4.5f, 4.5f, 3.5f, 12.5f, 3.5f, 20.5f, 11.5f, 11.5f, 20.5f, 3.5f, 12.5f); Circulo(8.5f, 8.5f, 1.2f); break;
                case "chev": Linha(8, 9.5f, 12, 5.5f, 16, 9.5f); Linha(8, 14.5f, 12, 18.5f, 16, 14.5f); break;
                case "clock": Circulo(12, 12, 8.5f); Linha(12, 7.5f, 12, 12, 15, 14); break;
                case "calendar":
                    Retang(3.5f, 5, 17, 15.5f, 2.2f); Linha(3.5f, 10, 20.5f, 10); Linha(8, 3, 8, 7); Linha(16, 3, 16, 7); break;
                case "bolt":
                    Linha(13, 2.5f, 5.5f, 13.5f, 11.5f, 13.5f, 10.5f, 21.5f, 18.5f, 10, 12.5f, 10, 13, 2.5f); break;
                case "eye":
                    p.BeginPath(); p.MoveTo(V(2, 12)); p.BezierCurveTo(V(5, 6), V(9, 5), V(12, 5)); p.BezierCurveTo(V(15, 5), V(19, 6), V(22, 12)); p.BezierCurveTo(V(19, 18), V(15, 19), V(12, 19)); p.BezierCurveTo(V(9, 19), V(5, 18), V(2, 12)); p.Stroke();
                    Circulo(12, 12, 3f); break;
                case "lock":
                    Retang(5, 11, 14, 9.5f, 2); p.BeginPath(); p.MoveTo(V(8, 11)); p.LineTo(V(8, 8)); p.Arc(V(12, 8), 4 * s, Angle.Degrees(180), Angle.Degrees(360), ArcDirection.Clockwise); p.LineTo(V(16, 11)); p.Stroke(); break;
                case "check": Linha(5, 12.5f, 10, 17.5f, 19, 7); break;
                case "minus": Linha(5, 12, 19, 12); break;
                case "trophy":
                    Linha(7, 4, 17, 4, 17, 10, 12, 14, 7, 10, 7, 4); Linha(12, 14, 12, 18); Linha(8, 20, 16, 20); Linha(7, 6, 4, 6, 4, 8.5f, 7, 10); Linha(17, 6, 20, 6, 20, 8.5f, 17, 10); break;
                case "engrenagem": Circulo(12, 12, 3.2f, false); Circulo(12, 12, 7f, false); for (int i = 0; i < 8; i++) { float an = i * Mathf.PI / 4f; Linha(12 + 7f * Mathf.Cos(an), 12 + 7f * Mathf.Sin(an), 12 + 9.8f * Mathf.Cos(an), 12 + 9.8f * Mathf.Sin(an)); } break;
                case "help": Circulo(12, 12, 9.5f, false); Linha(9.5f, 9.3f, 9.7f, 7.8f, 11, 6.9f, 12.4f, 6.9f, 14, 7.6f, 14.7f, 9, 14.3f, 10.6f, 12, 12.2f, 12, 13.6f); Circulo(12, 16.8f, 1.1f, true); break;
                case "som": Linha(4, 9.5f, 8, 9.5f, 12.5f, 5.5f, 12.5f, 18.5f, 8, 14.5f, 4, 14.5f, 4, 9.5f); Linha(15.5f, 9, 17, 12, 15.5f, 15); Linha(18, 6.5f, 20.5f, 12, 18, 17.5f); break;
                case "mudo": Linha(4, 9.5f, 8, 9.5f, 12.5f, 5.5f, 12.5f, 18.5f, 8, 14.5f, 4, 14.5f, 4, 9.5f); Linha(16, 9.5f, 21, 14.5f); Linha(21, 9.5f, 16, 14.5f); break;
                case "kebab": Circulo(12, 5.5f, 1.5f, true); Circulo(12, 12, 1.5f, true); Circulo(12, 18.5f, 1.5f, true); break;
            }
        }
    }
}
