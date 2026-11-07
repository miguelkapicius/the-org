using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Map;
using CSM.Sim;

namespace CSM.ManagerUI
{
    /// <summary>
    /// O quadro tático do playbook (decisão 0022): a planta do mapa com a rota de cada um dos 5 jogadores de um plano. As rotas são
    /// ANIMADAS: cada jogador sai da base e percorre o caminho com um rastro luminoso até onde para (ou morre, com um X). A velocidade
    /// é ilustrativa (constante); o fim de cada rota vem do plano. A planta é a do radar da transmissão (2 unidades por pixel), ampliada
    /// pelo zoom e recolorida em grafite para combinar com a interface.
    /// </summary>
    public class PlantaJogada : VisualElement
    {
        public static readonly Color[] CORES = { new Color(0.96f, 0.55f, 0.16f), new Color(0.43f, 0.71f, 1f), new Color(0.78f, 0.96f, 0.2f), new Color(1f, 0.36f, 0.63f), new Color(0.75f, 0.56f, 1f) };
        const int ESC = 2;                  // unidades do mapa por pixel
        const float SEGUNDOS_ANIM = 8f;     // quanto dura a animação inteira (a rota real tem entre 30 e 90 s)
        static Texture2D planta;

        /// <summary>Só para a captura de tela: a animação começa neste ponto (0..1) em vez do começo.</summary>
        public static float InicioDaCaptura;

        readonly float z;
        readonly List<List<Vector2>> rotas = new List<List<Vector2>>();
        readonly List<float[]> acum = new List<float[]>();   // comprimento acumulado de cada rota, ponto a ponto
        readonly List<float> total = new List<float>(), dur = new List<float>();
        readonly List<bool> morreu = new List<bool>();
        readonly List<Vector2> sites = new List<Vector2>();

        public float Duracao { get; private set; }   // segundos reais do plano (para o relógio)
        public float Tempo { get; private set; }
        public bool Tocando { get; private set; } = true;
        public Action<float> AoTempo;
        float espera, ultimo;

        public PlantaJogada(Estrategias.Plano p, float zoom = 1f)
        {
            z = zoom;
            style.width = MapData.NX / ESC * z; style.height = (MapData.NY + ESC - 1) / ESC * z; style.flexShrink = 0;
            style.backgroundImage = Planta();
            AddToClassList("pb-planta");
            Duracao = Mathf.Max(10f, Estrategias.RotaAte(p));
            for (int k = 0; k < 5; k++)
            {
                var r = Estrategias.Rota(p, k, out bool m);
                var l = new List<Vector2>();
                // cada jogador um pouco deslocado: quem faz o mesmo caminho não some debaixo do outro
                var desloc = new Vector2((k - 2) * 1.6f, (k - 2) * 1.6f);
                foreach (var v in r) l.Add((new Vector2(v.x / ESC, v.y / ESC) + desloc) * z);
                var a = new float[l.Count]; for (int i = 1; i < l.Count; i++) a[i] = a[i - 1] + Vector2.Distance(l[i - 1], l[i]);
                rotas.Add(l); acum.Add(a); total.Add(l.Count > 0 ? a[l.Count - 1] : 0f); morreu.Add(m);
                float fim = m && p.js[k].morreu > 0 ? p.js[k].morreu : Duracao;   // quem morre para de andar quando morre
                dur.Add(Mathf.Clamp(fim, 4f, Duracao));
            }
            // letras dos bombs e das bases
            foreach (var kv in MapData.SITES) { Marca(kv.Value.Center, kv.Key, new Color(0.95f, 0.76f, 0f)); sites.Add(new Vector2(kv.Value.Center.x / ESC, kv.Value.Center.y / ESC) * z); }
            Marca(Estrategias.CentroDaArea("TR"), "TR", new Color(0.94f, 0.67f, 0.27f));
            Marca(Estrategias.CentroDaArea("CT"), "CT", new Color(0.43f, 0.71f, 1f));
            generateVisualContent += Desenhar;
            Tempo = Mathf.Clamp01(InicioDaCaptura) * Duracao;
            ultimo = Time.realtimeSinceStartup;
            var tarefa = schedule.Execute(Passo).Every(16);
            RegisterCallback<DetachFromPanelEvent>(_ => tarefa.Pause());
        }

        void Marca(V2 c, string txt, Color cor)
        {
            var l = new Label(txt); l.pickingMode = PickingMode.Ignore; l.AddToClassList("pb-site");
            l.style.position = Position.Absolute; l.style.left = c.x / ESC * z - 16; l.style.top = c.y / ESC * z - 13; l.style.width = 32; l.style.height = 26;
            l.style.color = cor; l.style.opacity = 0.9f;
            Add(l);
        }

        // ------------------------------------------------------------------------------------------ animação
        void Passo()
        {
            float agora = Time.realtimeSinceStartup, dt = Mathf.Min(0.1f, agora - ultimo); ultimo = agora;
            if (!Tocando) return;
            if (espera > 0f) { espera -= dt; if (espera <= 0f) Tempo = 0f; MarkDirtyRepaint(); AoTempo?.Invoke(Tempo); return; }
            Tempo += dt * (Duracao / SEGUNDOS_ANIM);
            if (Tempo >= Duracao) { Tempo = Duracao; espera = 1.8f; }   // para um instante no fim e recomeça
            AoTempo?.Invoke(Tempo); MarkDirtyRepaint();
        }

        public void Alternar() { Tocando = !Tocando; }
        public void Reiniciar() { Tempo = 0f; espera = 0f; Tocando = true; MarkDirtyRepaint(); AoTempo?.Invoke(Tempo); }
        public void Ir(float fracao) { Tempo = Mathf.Clamp01(fracao) * Duracao; espera = 0f; MarkDirtyRepaint(); AoTempo?.Invoke(Tempo); }

        /// <summary>O pedaço da rota k já percorrido até a distância <paramref name="alvo"/> e a posição da cabeça.</summary>
        List<Vector2> Parcial(int k, float alvo, out Vector2 cabeca)
        {
            var r = rotas[k]; var a = acum[k]; var lista = new List<Vector2> { r[0] }; cabeca = r[0];
            for (int i = 1; i < r.Count; i++)
            {
                if (a[i] <= alvo) { lista.Add(r[i]); cabeca = r[i]; continue; }
                float seg = a[i] - a[i - 1]; float t = seg > 0.0001f ? (alvo - a[i - 1]) / seg : 0f;
                cabeca = Vector2.Lerp(r[i - 1], r[i], Mathf.Clamp01(t)); lista.Add(cabeca); break;
            }
            return lista;
        }

        static void Linha(Painter2D p, List<Vector2> pts)
        {
            if (pts.Count < 2) return;
            p.BeginPath(); p.MoveTo(pts[0]); for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]); p.Stroke();
        }

        static void Disco(Painter2D p, Vector2 c, float raio, Color cor) { p.fillColor = cor; p.BeginPath(); p.Arc(c, raio, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }

        void Desenhar(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D; float w = contentRect.width, h = contentRect.height;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;

            // grade tática bem fraca, a cada 50 unidades de planta
            p.strokeColor = new Color(1f, 1f, 1f, 0.012f); p.lineWidth = 1f;
            for (float x = 50f * z; x < w; x += 50f * z) { p.BeginPath(); p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, h)); p.Stroke(); }
            for (float y = 50f * z; y < h; y += 50f * z) { p.BeginPath(); p.MoveTo(new Vector2(0, y)); p.LineTo(new Vector2(w, y)); p.Stroke(); }

            // anéis nos bombs
            foreach (var s in sites) { p.strokeColor = new Color(0.95f, 0.76f, 0f, 0.35f); p.lineWidth = 1.5f; p.BeginPath(); p.Arc(s, 20f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke(); }

            for (int k = 0; k < rotas.Count; k++)
            {
                var r = rotas[k]; if (r.Count < 2) continue;
                var cor = CORES[k];
                float frac = Mathf.Clamp01(Tempo / dur[k]);
                var parte = Parcial(k, frac * total[k], out Vector2 cabeca);

                p.strokeColor = new Color(cor.r, cor.g, cor.b, 0.2f); p.lineWidth = 2f; Linha(p, r);                // a rota inteira, apagada
                p.strokeColor = new Color(cor.r, cor.g, cor.b, 0.16f); p.lineWidth = 10f; Linha(p, parte);          // brilho do rastro
                p.strokeColor = new Color(cor.r, cor.g, cor.b, 0.35f); p.lineWidth = 5.5f; Linha(p, parte);
                p.strokeColor = cor; p.lineWidth = 2.6f; Linha(p, parte);                                           // o rastro

                Disco(p, r[0], 3.5f, cor);                                                                            // saída
                bool chegou = frac >= 1f;
                if (chegou && morreu[k])
                {   // morreu: X
                    p.strokeColor = cor; p.lineWidth = 3.2f;
                    p.BeginPath(); p.MoveTo(cabeca + new Vector2(-6, -6)); p.LineTo(cabeca + new Vector2(6, 6)); p.MoveTo(cabeca + new Vector2(6, -6)); p.LineTo(cabeca + new Vector2(-6, 6)); p.Stroke();
                }
                else
                {
                    Disco(p, cabeca, 13f, new Color(cor.r, cor.g, cor.b, 0.18f));   // halo
                    Disco(p, cabeca, 6.5f, cor);
                    Disco(p, cabeca, 2.4f, new Color(1f, 1f, 1f, 0.9f));
                }
            }

            // cantos do quadro (como a mira de um mapa tático)
            p.strokeColor = new Color(1f, 1f, 1f, 0.35f); p.lineWidth = 2f; const float c = 18f, m = 8f;
            void Canto(float x, float y, float sx, float sy) { p.BeginPath(); p.MoveTo(new Vector2(x + sx * c, y)); p.LineTo(new Vector2(x, y)); p.LineTo(new Vector2(x, y + sy * c)); p.Stroke(); }
            Canto(m, m, 1, 1); Canto(w - m, m, -1, 1); Canto(m, h - m, 1, -1); Canto(w - m, h - m, -1, -1);
        }

        /// <summary>A planta (as mesmas regras do radar da transmissão, recolorida em grafite). Feita uma vez.</summary>
        public static Texture2D Planta()
        {
            if (planta != null) return planta;
            int w = MapData.NX / ESC, h = (MapData.NY + ESC - 1) / ESC;
            bool Anda(int x, int y) => MapData.InMap(x, y) && (MapData.walk[x, y] || MapData.pisavel[x, y]);
            Color32 piso = new Color32(0x1C, 0x20, 0x26, 255), alto = new Color32(0x27, 0x2C, 0x34, 255), linha = new Color32(0xB9, 0xC2, 0xCF, 255), caixa = new Color32(0x5E, 0x66, 0x72, 255), nada = new Color32(0x0C, 0x0E, 0x11, 255);
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

    /// <summary>Indicador em anel (0 a 1) com o valor no centro; o anel cresce até o valor ao aparecer.</summary>
    public class Anel : VisualElement
    {
        readonly float alvo; readonly Color cor; float atual;

        public Anel(float valor01, Color cor, string centro)
        {
            alvo = Mathf.Clamp01(valor01); this.cor = cor;
            style.width = 92; style.height = 92; style.alignItems = Align.Center; style.justifyContent = Justify.Center; style.flexShrink = 0;
            var v = new Label(centro); v.AddToClassList("pb-anel-v"); v.pickingMode = PickingMode.Ignore; Add(v);
            generateVisualContent += Desenhar;
            float t0 = Time.realtimeSinceStartup;
            var tarefa = schedule.Execute(() =>
            {
                if (PlantaJogada.InicioDaCaptura > 0f) { atual = alvo; return; }   // na captura de tela o anel já vem cheio
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / 0.7f);
                atual = alvo * (1f - Mathf.Pow(1f - t, 3f)); MarkDirtyRepaint();
            }).Every(16).Until(() => Time.realtimeSinceStartup - t0 > 0.8f);
            RegisterCallback<DetachFromPanelEvent>(_ => tarefa.Pause());
            if (PlantaJogada.InicioDaCaptura > 0f) atual = alvo;
        }

        void Desenhar(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D; var c = new Vector2(contentRect.width / 2f, contentRect.height / 2f); const float raio = 38f;
            p.lineCap = LineCap.Round;
            p.strokeColor = new Color(1f, 1f, 1f, 0.05f); p.lineWidth = 7f;
            p.BeginPath(); p.Arc(c, raio, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (atual <= 0.001f) return;
            p.strokeColor = new Color(cor.r, cor.g, cor.b, 0.22f); p.lineWidth = 12f;
            p.BeginPath(); p.Arc(c, raio, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * atual)); p.Stroke();
            p.strokeColor = cor; p.lineWidth = 7f;
            p.BeginPath(); p.Arc(c, raio, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * atual)); p.Stroke();
        }
    }
}
