using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CSM.Sim;
using CSM.View;
using CSM.Map;
using static CSM.View.TelaJogo;

namespace CSM.EditorTools
{
    /// <summary>
    /// Vídeo de uma partida com os personagens em sprite (Blender, Logs/sprites_jogo, 8 direções): o mapa vem do
    /// FrameRenderer (voxel, smoke, fogo, tiros) em 2x2 pedaços de 256x192, ampliado 3x; cada bot vira o sprite da
    /// animação do que ele está fazendo (parado, andando, agachado, tiro, recarga, granada, plantar, desarmar, cego,
    /// faca, morrendo), virado pra onde mira, com profundidade (atrás de parede vira silhueta, dentro da smoke some).
    /// Valida a biblioteca de sprites (quadro vazio, cortado na borda, pé fora do lugar) e o uso no vídeo.
    ///   -seed S -rounds N -fps F → Logs/video_jogo/q_*.png + Logs/video_jogo/validacao.txt (o mp4 sai no Blender)
    /// </summary>
    public static class VideoJogo
    {
        public static void Gravar()
        {
            var log = new StringBuilder();
            try
            {
                int seed = int.Parse(Arg("-seed", "3")), rounds = int.Parse(Arg("-rounds", "3"));
                float fps = float.Parse(Arg("-fps", "30"), System.Globalization.CultureInfo.InvariantCulture);
                string dir = "Logs/video_jogo"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
                log.AppendLine(ValidaBiblioteca());
                var sm = new StaticMap(); var fr = new FrameRenderer(sm);
                var nat = new C3[NW * NH]; var prof = new float[NW * NH]; var fum = new float[NW * NH];
                var buf = new C3[OW * OH];
                var tex = new Texture2D(OW, OH, TextureFormat.RGB24, false); var px = new Color32[OW * OH];
                var pt = new Sim.Partida(seed, null, null);
                int f = 0;
                void Salva()
                {
                    for (int y = 0; y < OH; y++) for (int x = 0; x < OW; x++) { C3 c = buf[y * OW + x]; px[(OH - 1 - y) * OW + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255); }
                    tex.SetPixels32(px); File.WriteAllBytes($"{dir}/q_{f++:D5}.png", tex.EncodeToPNG());
                }
                int desde = int.Parse(Arg("-desde", "1"));
                while (pt.r + 1 < desde && !pt.acabou) { var mm = pt.Preparar(); while (!mm.over && mm.t < 200) mm.Step(); pt.Fechar(mm); }   // rounds antes: sem filmar
                for (int r = 0; r < rounds && !pt.acabou; r++)
                {
                    var m = pt.Preparar(); int rn = pt.r + 1;
                    // cartela do round
                    Array.Clear(buf, 0, buf.Length);
                    for (int i = 0; i < buf.Length; i++) buf[i] = new C3(22, 18, 20);
                    Texto(buf, OW / 2 - 200, OH / 2 - 40, $"ROUND {rn}", new C3(255, 255, 255), 6);
                    Texto(buf, OW / 2 - 200, OH / 2 + 20, $"TR {m.compra[0].ToUpper()}  X  CT {m.compra[1].ToUpper()}", new C3(220, 210, 190), 3);
                    for (int k = 0; k < (int)(fps * 1.5f); k++) Salva();
                    var est = new Dictionary<int, Anim>();
                    V2 cam = m.agents.Where(a => a.IsT).Aggregate(new V2(0, 0), (s, a) => s + a.pos) / 5; float next = 0, focoAte = -9; V2 foco = cam;
                    while (true)
                    {
                        if (m.t >= next || m.over)
                        {
                            next += 1f / fps;
                            TelaJogo.Mirar(m, ref cam, ref foco, ref focoAte, 1f / fps);
                            Quadro(m, fr, cam, est, nat, prof, fum, buf, rn, pt);
                            Salva();
                            if (m.over)
                            {   // fim do round: 2 s ainda animando (quem morreu no último tiro termina de cair e fica deitado)
                                float tFim = m.t;
                                for (int k = 0; k < (int)(fps * 2); k++) { m.t += 1f / fps; Quadro(m, fr, cam, est, nat, prof, fum, buf, rn, pt); Salva(); }
                                m.t = tFim; break;
                            }
                        }
                        m.Step();
                        if (m.t > 200) break;
                    }
                    log.AppendLine($"round {rn}: {m.winner} ({m.reason}) em {m.t:0}s, TR {m.compra[0]} x CT {m.compra[1]}");
                    pt.Fechar(m);
                }
                UnityEngine.Object.DestroyImmediate(tex);
                log.AppendLine($"{f} quadros a {fps} qps ({f / fps / 60f:0.0} min)");
                if (quadrosAndando > 0)
                {
                    double s = quadrosAndando / fps;   // bot-segundos andando
                    log.AppendLine($"PASSO: andando a {velAndando / quadrosAndando:0} u/s em média, {2 * ciclosAndando / s:0.0} passos/s (ciclo de {Ciclo("andar"):0.0} u, pé plantado: não patina); " +
                                   $"{ciclosAndando / s * 12 / fps:0.00} quadros da animação por quadro de vídeo");
                }
                log.AppendLine("USO DAS ANIMAÇÕES NO VÍDEO (quadros x bots):");
                foreach (var kv in usoAnim.OrderByDescending(x => x.Value)) log.AppendLine($"  {kv.Value,7}  {kv.Key}");
                log.AppendLine("FALTOU SPRITE (usou outro no lugar):");
                if (faltas.Count == 0) log.AppendLine("  nada");
                foreach (var kv in faltas.OrderByDescending(x => x.Value)) log.AppendLine($"  {kv.Value,7}  {kv.Key}");
            }
            catch (Exception e) { log.AppendLine("ERRO " + e); }
            File.WriteAllText("Logs/video_jogo/validacao.txt", log.ToString());
            EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- validação da biblioteca
        /// <summary>
        /// Cada animação x arma x direção: quadros que faltam, quadro vazio, boneco cortado na borda (o quadro de 192 não
        /// coube — arma comprida), e o pé fora do lugar (a âncora do jogo é a raiz projetada; o pé desenhado tem que
        /// estar perto dela, senão o boneco "flutua" ou "afunda" no mapa).
        /// </summary>
        static string ValidaBiblioteca()
        {
            var sb = new StringBuilder($"VALIDAÇÃO DOS SPRITES ({SPR}, {Direcoes()} direções)\n");
            var files = Directory.Exists(SPR) ? Directory.GetFiles(SPR, "*.png") : new string[0];
            sb.AppendLine($"  {files.Length} quadros");
            var grupos = files.Select(Path.GetFileNameWithoutExtension).GroupBy(n => n.Substring(0, n.LastIndexOf('_', n.LastIndexOf('_') - 1)));
            int ruins = 0;
            foreach (var g in grupos.OrderBy(x => x.Key))
            {
                var porDir = g.GroupBy(n => n.Split('_')[n.Split('_').Length - 2]).ToDictionary(x => x.Key, x => x.Count());
                var problemas = new List<string>();
                if (porDir.Count != Direcoes()) problemas.Add($"só {porDir.Count} de {Direcoes()} direções");
                if (porDir.Values.Distinct().Count() > 1) problemas.Add("número de quadros diferente entre direções");
                int vazios = 0, cortados = 0; float peMax = 0; var cortDir = new HashSet<string>();
                foreach (var nome in g)
                {
                    var s = Load(nome); if (s == null) continue;
                    int minY = s.h, maxY = -1, opacos = 0; bool borda = false;
                    for (int y = 0; y < s.h; y++) for (int x = 0; x < s.w; x++)
                    {
                        if (s.px[y * s.w + x].a < 100) continue;
                        opacos++; minY = Math.Min(minY, y);
                        if (MathF.Abs(x - s.ax) < 22) maxY = Math.Max(maxY, y);   // pé: só perto da âncora (a arma pode passar abaixo)
                        if (x == 0 || y == 0 || x == s.w - 1 || y == s.h - 1) borda = true;
                    }
                    if (opacos == 0) { vazios++; continue; }
                    if (borda) { cortados++; cortDir.Add(nome.Split('_')[nome.Split('_').Length - 2]); }
                    if (!g.Key.Contains("morrer") && !g.Key.Contains("plantar") && !g.Key.Contains("desarmar")) peMax = MathF.Max(peMax, MathF.Abs(maxY - s.ay));
                }
                if (vazios > 0) problemas.Add($"{vazios} quadros vazios");
                if (cortados > 0) problemas.Add($"{cortados} quadros cortados na borda (direções {string.Join(",", cortDir.OrderBy(x => x))})");
                // passo pra frente (na direção da câmera) desce o pé ~20 px; mais que isso é a arma/braço perto do pé — só
                // passa a ser problema se nem os quadros parados (mira) acertam
                if (peMax > 30 && g.Key.Contains("_mira_") && !g.Key.Contains("AWP") && !g.Key.Contains("M4A1S")) problemas.Add($"pé até {peMax:0} px longe da âncora");
                if (problemas.Count > 0) ruins++;
                sb.AppendLine($"  {(problemas.Count == 0 ? "ok  " : "RUIM")} {g.Key,-22} {g.Count(),4} quadros{(problemas.Count > 0 ? " — " + string.Join("; ", problemas) : "")}");
            }
            sb.Insert(0, $"animações com problema: {ruins}\n");
            return sb.ToString();
        }

        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }
    }
}
