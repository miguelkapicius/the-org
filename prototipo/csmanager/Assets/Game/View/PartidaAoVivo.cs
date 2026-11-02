using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using CSM.Sim;

namespace CSM.View
{
    /// <summary>
    /// A partida rodando ao vivo no Play: a simulação anda em tempo real (ou acelerada) e cada quadro sai do mesmo desenho
    /// do vídeo (TelaJogo). A câmera segue a ação; clicando num jogador do painel ela segue ele e mostra o que ele pensa.
    /// Teclas (com o foco na janela Game): espaço pausa, 1-4 velocidade, N pula o round, P painel, Esc câmera automática.
    /// Lê do disco, relativo à raiz do projeto: Logs/sprites_jogo*, Assets/Game/Sim/rotas.bin e pesos.txt (roda no editor).
    /// </summary>
    public class PartidaAoVivo : MonoBehaviour
    {
        public int seed = 15;
        public float velocidade = 1;

        Sim.Partida pt; Match m; int rn; FrameRenderer fr;
        C3[] nat, buf; float[] prof, fum; Color32[] px; Texture2D tex;
        Dictionary<int, TelaJogo.Anim> est;
        V2 cam, foco; float focoAte = -9, relogio, fimResta, fimExtra;
        bool pausado, painel = true, carregando = true, acabou; int seguir = -1;
        // medição (mostrada no painel; -aovivoteste N grava e fecha o editor depois de N s)
        readonly Stopwatch rel = new Stopwatch(); float msQuadro, msSim, fps; float testeEm = -1, inicio; int quadros, rounds;
        GUIStyle sTxt, sCaixa, sBotao;

        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-aovivoteste") testeEm = float.Parse(a[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            yield return null; yield return null;   // deixa o "carregando" aparecer: montar o mapa leva uns segundos
            TelaJogo.recortar = true; Brain.Explicar = true;
            fr = new FrameRenderer(new StaticMap());
            nat = new C3[TelaJogo.NW * TelaJogo.NH]; prof = new float[nat.Length]; fum = new float[nat.Length];
            buf = new C3[TelaJogo.OW * TelaJogo.OH]; px = new Color32[buf.Length];
            tex = new Texture2D(TelaJogo.OW, TelaJogo.OH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            NovaPartida();
            carregando = false; inicio = Time.realtimeSinceStartup;
        }

        void NovaPartida() { pt = new Sim.Partida(seed, null, null); acabou = false; NovoRound(); }

        void NovoRound()
        {
            m = pt.Preparar(); rn = pt.r + 1; rounds++;
            est = new Dictionary<int, TelaJogo.Anim>();
            cam = foco = m.agents.Where(a => a.IsT).Aggregate(new V2(0, 0), (s, a) => s + a.pos) / 5;
            focoAte = -9; relogio = 0; fimResta = 3; fimExtra = 0;
        }

        void FechaRound()
        {
            m.t -= fimExtra; fimExtra = 0;
            pt.Fechar(m);
            if (pt.acabou) { acabou = true; pausado = true; } else NovoRound();
        }

        void PulaRound()
        {
            if (m == null || acabou) return;
            while (!m.over && m.t < 200) m.Step();
            FechaRound();
        }

        void Update()
        {
            if (carregando) return;
            float dt = MathF.Min(Time.unscaledDeltaTime, 0.1f);
            rel.Restart();
            if (!pausado && !acabou)
            {
                if (m.over || m.t > 200)
                {   // fim do round: 3 s ainda animando (quem morreu no último tiro termina de cair)
                    fimResta -= dt; m.t += dt; fimExtra += dt;
                    if (fimResta <= 0) FechaRound();
                }
                else
                {
                    relogio = MathF.Min(relogio + dt * velocidade, m.t + 0.5f);
                    while (m.t < relogio && !m.over && rel.ElapsedMilliseconds < 25) m.Step();
                }
            }
            float s = (float)rel.Elapsed.TotalMilliseconds; rel.Restart();
            var alvo = seguir >= 0 ? m.agents[seguir] : null;
            if (alvo != null) { foco = alvo.pos; cam = cam + (foco - cam) * (1 - MathF.Exp(-5f * dt)); }
            else TelaJogo.Mirar(m, ref cam, ref foco, ref focoAte, dt);
            TelaJogo.Quadro(m, fr, cam, est, nat, prof, fum, buf, rn, pt);
            Parallel.For(0, TelaJogo.OH, y =>
            {
                int OW = TelaJogo.OW, d = (TelaJogo.OH - 1 - y) * OW, o = y * OW;
                for (int x = 0; x < OW; x++) { C3 c = buf[o + x]; px[d + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255); }
            });
            tex.SetPixels32(px); tex.Apply(false);
            float q = (float)rel.Elapsed.TotalMilliseconds;
            msSim += (s - msSim) * 0.1f; msQuadro += (q - msQuadro) * 0.1f; fps += (1 / MathF.Max(0.001f, Time.unscaledDeltaTime) - fps) * 0.1f;
            quadros++;
            if (testeEm > 0 && Time.realtimeSinceStartup - inicio > testeEm) StartCoroutine(FimDoTeste());
        }

        IEnumerator FimDoTeste()
        {
            float dur = Time.realtimeSinceStartup - inicio; testeEm = -1;
            File.WriteAllBytes("Logs/aovivo_quadro.png", tex.EncodeToPNG());
            ScreenCapture.CaptureScreenshot("Logs/aovivo_tela.png");
            File.WriteAllText("Logs/aovivo.txt", $"{quadros} quadros em {dur:0.0} s = {quadros / dur:0.0} qps (últimos {fps:0.0})\nquadro {msQuadro:0.0} ms, simulação {msSim:0.0} ms\nround {rn}, t {m.t:0.0}, rounds vistos {rounds}\nsprites em cache {TelaJogo.cache.Count}, memória {GC.GetTotalMemory(false) / 1048576} MB\n");
            yield return new WaitForEndOfFrame(); yield return null; yield return null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        void OnDestroy() { if (tex != null) Destroy(tex); }

        void OnGUI()
        {
            if (sTxt == null)
            {
                sTxt = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
                sCaixa = new GUIStyle(GUI.skin.box); sBotao = new GUIStyle(GUI.skin.button) { fontSize = 12 };
                var fundo = new Texture2D(1, 1); fundo.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.06f, 0.72f)); fundo.Apply(); sCaixa.normal.background = fundo;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && !carregando) Tecla(e.keyCode);
            if (carregando) { GUI.Label(new Rect(20, 20, 400, 30), "Carregando o mapa e a partida..."); return; }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex, ScaleMode.ScaleToFit, false);
            float k = MathF.Max(1, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            float W = Screen.width / k, H = Screen.height / k;
            Controles(W, H);
            if (painel) Painel(W, H);
            if (acabou) GUI.Box(new Rect(W / 2 - 170, H / 2 - 18, 340, 36), $"Fim da partida: {pt.vencedor?.nome} venceu", sBotao);
        }

        void Tecla(KeyCode c)
        {
            if (c == KeyCode.Space) pausado = !pausado && !acabou;
            else if (c == KeyCode.Alpha1) velocidade = 1; else if (c == KeyCode.Alpha2) velocidade = 2;
            else if (c == KeyCode.Alpha3) velocidade = 4; else if (c == KeyCode.Alpha4) velocidade = 8;
            else if (c == KeyCode.N) PulaRound();
            else if (c == KeyCode.P) painel = !painel;
            else if (c == KeyCode.Escape) seguir = -1;
        }

        void Controles(float W, float H)
        {
            float x = 10, y = H - 32;
            bool B(string t, float w) { bool r = GUI.Button(new Rect(x, y, w, 24), t, sBotao); x += w + 4; return r; }
            if (B(pausado ? "Continuar" : "Pausar", 76)) pausado = !pausado && !acabou;
            foreach (float v in new[] { 1f, 2f, 4f, 8f }) if (B((velocidade == v ? "> " : "") + v + "x", 44)) velocidade = v;
            if (B("Próximo round", 104)) PulaRound();
            if (B("Nova partida", 96)) { seed++; pausado = false; NovaPartida(); }
            if (B(seguir >= 0 ? "Câmera automática" : "Câmera: automática", 128)) seguir = -1;
            if (B(painel ? "Esconder painel" : "Mostrar painel", 108)) painel = !painel;
            GUI.Label(new Rect(x + 4, y + 3, 170, 22), $"partida {seed} · {fps:0} qps", sTxt);
        }

        void Painel(float W, float H)
        {
            const float PW = 300; float x = W - PW - 8, y = 44;
            GUI.Box(new Rect(x, y, PW, 268), GUIContent.none, sCaixa);
            foreach (var a in m.agents)
            {
                var r = new Rect(x + 4, y + 4 + a.id * 26, PW - 8, 24);
                string cor = !a.alive ? "#777777" : a.IsT ? "#f0aa46" : "#6eb4ff";
                string t = a.alive ? $"<color={cor}><b>{a.name}</b></color> {a.hp} {a.gunId}{(a.hasBomb ? " <b>C4</b>" : "")}" : $"<color={cor}>{a.name} (morto)</color>";
                if (GUI.Button(r, seguir == a.id ? "> " + a.name : "", GUIStyle.none)) seguir = seguir == a.id ? -1 : a.id;
                GUI.Label(r, (seguir == a.id ? "> " : "") + t, sTxt);
            }
            float yy = y + 274;
            if (seguir >= 0)
            {
                var a = m.agents[seguir];
                GUI.Box(new Rect(x, yy, PW, 150), GUIContent.none, sCaixa);
                GUI.Label(new Rect(x + 6, yy + 3, PW - 12, 146), $"<b>{a.name}</b> — {a.brain.order.name}\n{a.brain.thought}\n<b>Por quê:</b> {a.brain.porqueAgora}\n<color=#aaaaaa>{a.brain.contextoAgora}</color>", sTxt);
                yy += 156;
            }
            var ult = m.log.Skip(Math.Max(0, m.log.Count - 8)).Concat(m.suspeitas.Skip(Math.Max(0, m.suspeitas.Count - 3)).Select(s => $"<color=#ff8866>SUSPEITO {s.t:0}s: {s.texto}</color>"));
            GUI.Box(new Rect(x, yy, PW, H - yy - 40), GUIContent.none, sCaixa);
            GUI.Label(new Rect(x + 6, yy + 3, PW - 12, H - yy - 46), string.Join("\n", ult), sTxt);
        }
    }
}
