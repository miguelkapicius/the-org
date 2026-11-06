using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Map;
using CSM.Sim;

namespace CSM.ManagerUI
{
    /// <summary>
    /// A planta do mapa com a rota de cada um dos 5 jogadores de um plano (decisão 0022): ponto na saída, linha pelo chão, bolinha onde
    /// para (ou X onde morreu). A planta é a mesma do radar da transmissão (gerada do MapData, 2 unidades por pixel), em escala 1:1.
    /// </summary>
    public class PlantaJogada : VisualElement
    {
        public static readonly Color[] CORES = { new Color(0.96f, 0.55f, 0.16f), new Color(0.43f, 0.71f, 1f), new Color(0.78f, 0.96f, 0.2f), new Color(1f, 0.36f, 0.63f), new Color(0.75f, 0.56f, 1f) };
        const int ESC = 2;   // unidades do mapa por pixel
        static Texture2D planta;

        readonly List<List<Vector2>> rotas = new List<List<Vector2>>();
        readonly List<bool> morreu = new List<bool>();

        public PlantaJogada(Estrategias.Plano p)
        {
            style.width = MapData.NX / ESC; style.height = (MapData.NY + ESC - 1) / ESC; style.flexShrink = 0;
            style.backgroundImage = Planta();
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 10;
            pickingMode = PickingMode.Ignore;
            for (int k = 0; k < 5; k++)
            {
                var r = Estrategias.Rota(p, k, out bool m);
                var l = new List<Vector2>();
                // cada jogador um pouco deslocado: quem faz o mesmo caminho não some debaixo do outro
                var desloc = new Vector2((k - 2) * 1.6f, (k - 2) * 1.6f);
                foreach (var v in r) l.Add(new Vector2(v.x / ESC, v.y / ESC) + desloc);
                rotas.Add(l); morreu.Add(m);
            }
            // letras dos bombs e das bases
            foreach (var kv in MapData.SITES) Marca(kv.Value.Center, kv.Key, new Color(0.95f, 0.76f, 0f));
            Marca(Estrategias.CentroDaArea("TR"), "TR", new Color(0.94f, 0.67f, 0.27f));
            Marca(Estrategias.CentroDaArea("CT"), "CT", new Color(0.43f, 0.71f, 1f));
            generateVisualContent += Desenhar;
        }

        void Marca(V2 c, string txt, Color cor)
        {
            var l = new Label(txt); l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute; l.style.left = c.x / ESC - 14; l.style.top = c.y / ESC - 11; l.style.width = 28; l.style.height = 22;
            l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.fontSize = 15; l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = cor; l.style.opacity = 0.75f;
            Add(l);
        }

        void Desenhar(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            for (int k = 0; k < rotas.Count; k++)
            {
                var r = rotas[k]; if (r.Count == 0) continue;
                var cor = CORES[k];
                if (r.Count > 1)
                {
                    p.strokeColor = cor; p.lineWidth = 3f;
                    p.BeginPath(); p.MoveTo(r[0]); for (int i = 1; i < r.Count; i++) p.LineTo(r[i]); p.Stroke();
                }
                p.fillColor = cor;
                p.BeginPath(); p.Arc(r[0], 3f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();   // saída
                var f = r[r.Count - 1];
                if (morreu[k])
                {   // morreu: X
                    p.lineWidth = 3f; p.BeginPath(); p.MoveTo(f + new Vector2(-6, -6)); p.LineTo(f + new Vector2(6, 6)); p.MoveTo(f + new Vector2(6, -6)); p.LineTo(f + new Vector2(-6, 6)); p.Stroke();
                }
                else { p.BeginPath(); p.Arc(f, 6.5f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
            }
        }

        /// <summary>A planta (as mesmas regras e cores do radar da transmissão). Feita uma vez.</summary>
        public static Texture2D Planta()
        {
            if (planta != null) return planta;
            int w = MapData.NX / ESC, h = (MapData.NY + ESC - 1) / ESC;
            bool Anda(int x, int y) => MapData.InMap(x, y) && (MapData.walk[x, y] || MapData.pisavel[x, y]);
            Color32 piso = new Color32(0x2B, 0x26, 0x22, 255), alto = new Color32(0x3D, 0x36, 0x31, 255), linha = new Color32(0xD9, 0xCC, 0xB8, 255), caixa = new Color32(0x8A, 0x80, 0x79, 255), nada = new Color32(0x16, 0x14, 0x12, 255);
            var px = new Color32[w * h];
            for (int iy = 0; iy < h; iy++)
                for (int ix = 0; ix < w; ix++)
                {
                    int x = ix * ESC + 1, y = MapData.NY - 1 - (iy * ESC + 1);   // linha 0 da textura = fundo do mapa (sul)
                    Color32 c = nada;
                    if (MapData.InMap(x, y))
                    {
                        if (MapData.coverH[x, y] > 0) c = caixa;
                        else if (Anda(x, y))
                        {
                            if (!Anda(x - 3, y) || !Anda(x + 3, y) || !Anda(x, y - 3) || !Anda(x, y + 3)) c = linha;
                            else c = MapData.floor[x, y] > MapData.GZ + 6 ? alto : piso;
                        }
                    }
                    px[iy * w + ix] = c;
                }
            planta = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "planta do playbook" };
            planta.SetPixels32(px); planta.Apply(false, true);
            return planta;
        }
    }
}
