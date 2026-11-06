using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CSM.Sim;

namespace CSM.EditorTools
{
    /// <summary>
    /// Prints do HUD de transmissão nos momentos que importam, numa execução só (a Sim não repete a partida entre execuções):
    /// compra no freeze, 1º e 3º abate (killfeed), plantando, C4 plantada e defuse. Saída em Logs/hud/*.png (1920x1080).
    /// Batch: -executeMethod CSM.EditorTools.HudCaptura.CapturarBatch [-hud_seed N] [-hud_max segundos] [-hud_seguir 1]
    /// Usa o Setup/Salvar do Visual3D (por reflexão, sem mexer no arquivo dele).
    /// </summary>
    public static class HudCaptura
    {
        static MethodInfo M(string n) => typeof(Visual3D).GetMethod(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        static float Arg(string nome, float padrao)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == nome && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            return padrao;
        }

        public static void CapturarBatch()
        {
            try { Capturar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("CS Manager/HUD/Capturar momentos")]
        public static void Capturar()
        {
            M("Setup")?.Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
            var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
            p.seed = (int)Arg("-hud_seed", 15);
            p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
            const string DIR = "Logs/hud"; Directory.CreateDirectory(DIR);
            var salvar = M("Salvar");
            void Foto(string nome)
            {
                salvar.Invoke(null, new object[] { p.Cenario.cam, $"{DIR}/{nome}.png", 1920, 1080, p.Hud });
                Debug.Log($"[HUD] foto {nome}: round t={p.Partida.t:0.0}");
            }
            // render de manutenção a cada meio segundo (simular milhares de passos sem desenhar derruba o d3d12)
            var rtMan = new RenderTexture(320, 180, 24);
            bool fFreeze = false, fKill1 = false, fKill3 = false, fPlantando = false, fPlantada = false, fDefuse = false;
            bool seguirMatador = Arg("-hud_seguir", 1) > 0;
            float limite = Arg("-hud_max", 900), t = 0, dt = 1 / 30f; int passo = 0;
            Match mAnt = null; int kills = 0; float killT = -9;
            while (t < limite && !(fFreeze && fKill1 && fKill3 && fPlantando && fPlantada && fDefuse))
            {
                p.Simular(dt); t += dt;
                if (++passo % 15 == 0) { p.Cenario.cam.targetTexture = rtMan; p.Cenario.cam.Render(); p.Cenario.cam.targetTexture = null; }
                var m = p.Partida;
                if (m != mAnt) { mAnt = m; kills = 0; killT = -9; p.seguir = -1; }
                if (!fFreeze && p.Hud.Freeze > 0 && p.Hud.Freeze < Match.FREEZE * 0.6f) { Foto("freeze_compras"); fFreeze = true; }
                if (m.abates.Count > kills)
                {
                    kills = m.abates.Count; killT = t;
                    var ab = m.abates[kills - 1];
                    if (seguirMatador && kills == 2 && ab.by != null && ab.by.alive) p.seguir = ab.by.id;   // o 3º abate mostra a borda do observado
                }
                if (killT > 0 && t - killT > 0.6f)
                {
                    if (!fKill1 && kills >= 1) { Foto("killfeed_1"); fKill1 = true; }
                    if (!fKill3 && kills >= 3) { Foto("killfeed_3"); fKill3 = true; }
                    killT = -9;
                }
                if (!fPlantando && !m.bombPlanted && m.agents.Any(a => a.alive && a.brain.action == "plantar" && a.brain.ActionEnd - m.t < Match.PLANT * 0.45f))
                { Foto("c4_plantando"); fPlantando = true; }
                if (!fPlantada && m.bombPlanted && !m.over && m.t - m.plantedAt > 4) { Foto("c4_plantada"); fPlantada = true; }
                if (!fDefuse && m.bombPlanted && m.defuserActive && !m.over && m.agents.Any(a => a.alive && a.brain.action == "desarmar" && a.brain.ActionEnd - m.t < Match.DefuseTime(a) * 0.6f))
                { Foto("c4_defuse"); fDefuse = true; }
            }
            rtMan.Release();
            Debug.Log($"[HUD] fim: {t:0} s simulados · freeze {fFreeze} kill1 {fKill1} kill3 {fKill3} plantando {fPlantando} plantada {fPlantada} defuse {fDefuse}");
        }

        /// <summary>
        /// Vídeo de um round inteiro como sai na transmissão (diretor automático + HUD + replay de melhores momentos e
        /// câmera lenta): do freeze até o round seguinte começar. Quadros em Logs/hud/video/q_NNNN.png; o mp4 sai pelo
        /// frames_to_mp4.py (Blender). Batch: -executeMethod CSM.EditorTools.HudCaptura.VideoBatch [-hud_seed 15]
        /// [-hud_round 2] [-hud_fps 30] [-hud_w 1280 -hud_h 720] [-hud_max 260]
        /// </summary>
        public static void VideoBatch()
        {
            try { Video(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Video()
        {
            M("Setup")?.Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
            var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
            p.seed = (int)Arg("-hud_seed", 15);
            p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
            p.PularAte((int)Arg("-hud_round", 2));
            const string DIR = "Logs/hud/video";
            if (Directory.Exists(DIR)) foreach (var f in Directory.GetFiles(DIR, "q_*.png")) File.Delete(f);
            Directory.CreateDirectory(DIR);
            int W = (int)Arg("-hud_w", 1280), H = (int)Arg("-hud_h", 720);
            float dt = 1f / Arg("-hud_fps", 30), max = Arg("-hud_max", 260), t = 0;
            int r0 = p.Round, n = 0, passo = 0; bool viuReplay = false;
            // -hud_sofim 1: grava só do fim do round (pós-round + replay); o resto simula sem salvar (cada quadro salvo
            // custa ~2 s em batch, o round inteiro levava horas). Render de manutenção pra não derrubar o d3d12.
            bool soFim = Arg("-hud_sofim", 0) > 0;
            var rtMan = new RenderTexture(320, 180, 24);
            while (p.Round == r0 && t < max)
            {
                p.Simular(dt, dt); t += dt;
                viuReplay |= p.EmReplay;
                if (soFim && !p.Partida.over && !p.EmReplay)
                {
                    if (++passo % 15 == 0) { p.Cenario.cam.targetTexture = rtMan; p.Cenario.cam.Render(); p.Cenario.cam.targetTexture = null; }
                    continue;
                }
                Quadro(p.Cenario.cam, p.Hud, $"{DIR}/q_{n++:0000}.png", W, H);
                if (n % 300 == 0) Debug.Log($"[HUD] vídeo: {n} quadros, round t={p.Partida.t:0.0}, replay {p.EmReplay}");
            }
            LiberarQuadro(); rtMan.Release();
            Debug.Log($"[HUD] vídeo pronto: {n} quadros ({t:0.0} s) do round {r0}, replay visto {viuReplay}");
        }

        /// <summary>
        /// Prints do menu inicial (vitrine rodando atrás), do placar no meio da partida (Tab) e do placar de fim de partida
        /// (com REINICIAR/MENU). Saída Logs/hud/menu.png, placar_tab.png, placar_fim.png.
        /// Batch: -executeMethod CSM.EditorTools.HudCaptura.MenuBatch [-hud_seed 15] [-hud_round 7]
        /// </summary>
        public static void MenuBatch()
        {
            try { Menu(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Menu()
        {
            M("Setup")?.Invoke(null, null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
            var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
            p.seed = (int)Arg("-hud_seed", 15);
            p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
            var menu = new CSM.View3D.Menu3D(p);   // fora do Play o jogo não abre o menu sozinho
            qExtra = new[] { menu.Tela };
            const string DIR = "Logs/hud"; Directory.CreateDirectory(DIR);
            const float dt = 1 / 30f;
            var rtMan = new RenderTexture(320, 180, 24); int manut = 0;
            void Andar(float seg, bool sim)
            {
                for (float t = 0; t < seg; t += dt)
                {
                    if (sim) p.Simular(dt, dt);
                    menu.Atualizar(dt);
                    if (++manut % 10 == 0) { p.Cenario.cam.targetTexture = rtMan; p.Cenario.cam.Render(); p.Cenario.cam.targetTexture = null; }
                }
            }
            // menu: a vitrine já no round andando (fim do freeze, bonecos saindo da base)
            Andar(9, true);
            Quadro(p.Cenario.cam, p.Hud, $"{DIR}/menu.png", 1920, 1080); Debug.Log("[HUD] foto menu");
            // placar no meio da partida (Tab)
            p.ComecarPartida(); p.PularAte((int)Arg("-hud_round", 7)); Andar(25, true);
            menu.forcarPlacar = true; Andar(0.5f, false);
            Quadro(p.Cenario.cam, p.Hud, $"{DIR}/placar_tab.png", 1920, 1080); Debug.Log("[HUD] foto placar tab");
            menu.forcarPlacar = false;
            // fim de partida
            p.PularAte(99); Andar(5, false);
            Quadro(p.Cenario.cam, p.Hud, $"{DIR}/placar_fim.png", 1920, 1080);
            Debug.Log($"[HUD] foto placar fim: {p.PartidaAtual.A.nome} {p.PartidaAtual.A.placar} x {p.PartidaAtual.B.placar} {p.PartidaAtual.B.nome}, acabou {p.Acabou}");
            LiberarQuadro(); rtMan.Release(); qExtra = null;
        }

        // quadro do vídeo: mesma composição do Visual3D.Salvar (cena com MSAA + HUD renderizado em fundo preto e branco pra
        // tirar a cobertura exata de cada pixel), mas com as texturas criadas UMA vez. O Salvar cria/destrói RenderTexture e
        // Texture2D a cada chamada e, em batch, o d3d12 só solta a memória quando o quadro do editor avança: num vídeo de
        // milhares de quadros a placa estourava (8007000e no quadro ~330).
        static RenderTexture qRt, qRh; static Texture2D qCena, qHud, qSai; static Color[] qPreto, qBranco, qPx;
        static Canvas[] qExtra;   // outros canvas por cima da HUD (menu/placar)
        static void Quadro(Camera c, CSM.View3D.Hud3D hud, string arq, int w, int h)
        {
            if (qRt == null)
            {
                qRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                qRh = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                qCena = new Texture2D(w, h, TextureFormat.RGBA32, false); qHud = new Texture2D(w, h, TextureFormat.RGBA32, false);
                qSai = new Texture2D(w, h, TextureFormat.RGB24, false);
                c.targetTexture = qRt; c.Render(); c.targetTexture = null;   // aquece sombra/SSAO
            }
            var old = RenderTexture.active;
            c.targetTexture = qRt; c.Render(); c.targetTexture = null;
            RenderTexture.active = qRt; qCena.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            qPx = qCena.GetPixels();
            hud.canvas.renderMode = RenderMode.ScreenSpaceCamera; hud.canvas.worldCamera = hud.cam;
            if (qExtra != null) foreach (var cv in qExtra) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = hud.cam; cv.planeDistance = 4; }
            var fundo0 = hud.cam.backgroundColor;
            Color[] Hud(Color fundo)
            {
                hud.cam.backgroundColor = fundo; hud.cam.targetTexture = qRh; Canvas.ForceUpdateCanvases(); hud.cam.Render(); hud.cam.targetTexture = null;
                RenderTexture.active = qRh; qHud.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                return qHud.GetPixels();
            }
            qPreto = Hud(Color.black); qBranco = Hud(Color.white);
            RenderTexture.active = old;
            hud.cam.backgroundColor = fundo0; hud.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (qExtra != null) foreach (var cv in qExtra) cv.renderMode = RenderMode.ScreenSpaceOverlay;
            for (int i = 0; i < qPx.Length; i++)
            {
                Color p = qPreto[i], q = qBranco[i];
                float cob = 1 - ((q.r - p.r) + (q.g - p.g) + (q.b - p.b)) / 3f;
                if (cob <= 0.001f) { qPx[i].a = 1; continue; }
                float pr = Mathf.GammaToLinearSpace(p.r), pg = Mathf.GammaToLinearSpace(p.g), pb = Mathf.GammaToLinearSpace(p.b);
                float fica = Mathf.Clamp01(((Mathf.GammaToLinearSpace(q.r) - pr) + (Mathf.GammaToLinearSpace(q.g) - pg) + (Mathf.GammaToLinearSpace(q.b) - pb)) / 3f);
                var cc = qPx[i];
                qPx[i] = new Color(Mathf.LinearToGammaSpace(pr + fica * Mathf.GammaToLinearSpace(cc.r)), Mathf.LinearToGammaSpace(pg + fica * Mathf.GammaToLinearSpace(cc.g)), Mathf.LinearToGammaSpace(pb + fica * Mathf.GammaToLinearSpace(cc.b)), 1);
            }
            qSai.SetPixels(qPx); qSai.Apply(false);
            File.WriteAllBytes(arq, qSai.EncodeToPNG());
        }
        static void LiberarQuadro()
        {
            if (qRt != null) { qRt.Release(); Object.DestroyImmediate(qRt); qRh.Release(); Object.DestroyImmediate(qRh); }
            if (qCena != null) { Object.DestroyImmediate(qCena); Object.DestroyImmediate(qHud); Object.DestroyImmediate(qSai); }
            qRt = qRh = null; qCena = qHud = qSai = null;
        }
    }
}
