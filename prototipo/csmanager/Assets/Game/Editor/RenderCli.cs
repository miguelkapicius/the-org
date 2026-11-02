using System;
using System.Diagnostics;
using System.IO;
using CSM.View;
using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Render em batch (sem abrir a janela): visão geral do mapa e quadros do demo.
    ///   Unity -batchmode -projectPath D:\unity\CSManager -executeMethod CSM.EditorTools.RenderCli.Overview
    /// Saída em Logs/render/.
    /// </summary>
    public static class RenderCli
    {
        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        public static void SavePng(C3[] rgb, int w, int h, string path, int scale = 1)
        {
            var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGB24, false);
            var px = new Color32[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++)
                for (int x = 0; x < w * scale; x++)
                {
                    C3 c = rgb[(y / scale) * w + x / scale];
                    px[(h * scale - 1 - y) * w * scale + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255);
                }
            tex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>Ferramenta de desenho: casas automáticas que cortam linhas de visão longas. Grava as chamadas
        /// House(...) em Logs/render/casas.txt pra fixar no MapData.</summary>
        public static void Chicanes()
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                var code = Map.MapCheck.AutoChicanes(int.Parse(Arg("-iter", "60")), sb);
                sb.AppendLine("// ---- código pro MapData");
                foreach (var c in code) sb.AppendLine("            " + c);
            }
            catch (Exception e) { sb.AppendLine("ERRO " + e); }
            Directory.CreateDirectory("Logs/render");
            File.WriteAllText("Logs/render/casas.txt", sb.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Quadros de teste de proporção: boneco na frente de uma porta (perto da base TR) e boneco atrás de casa na
        /// rua do meio (silhueta). Saída Logs/render/teste_*.png (256x192 ampliado x4).
        /// </summary>
        public static void TestFrame()
        {
            string log = "";
            try
            {
                var sm = new StaticMap();
                var fr = new FrameRenderer(sm);
                // acha uma porta: prédio com rua em +x/+y, altura >= 34, faixa da porta (mesma regra da fachada)
                CSM.Sim.V2 door = default; bool found = false; float best = float.MaxValue; int doorZ = Map.MapData.GZ;
                var alvo = new CSM.Sim.V2(500, 700);
                CSM.Sim.V2 ao = default, fora = default;   // direção ao longo da parede / pra fora dela
                for (int x = 1; x < Map.MapData.NX - 1; x++)
                    for (int y = 1; y < Map.MapData.NY - 1; y++)
                    {
                        if (Map.MapData.walk[x, y]) continue;
                        int k = (x + y) % 60, bay = (x + y) / 60;
                        if (k != 15 || bay % 5 != 2) continue;
                        foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                        {
                            if (!Map.MapData.walk[x + dx, y + dy]) continue;
                            int fz = Map.MapData.floor[x + dx, y + dy];
                            if (Map.MapData.Top(x, y) - fz < 34 || fz != Map.MapData.GZ) continue;
                            var p = new CSM.Sim.V2(x + dx * 8, y + dy * 8);
                            float d = (p - alvo).Len;
                            if (d < best) { best = d; door = p; doorZ = fz; found = true; ao = new CSM.Sim.V2(dy, dx); fora = new CSM.Sim.V2(dx, dy); }
                        }
                    }
                log += found ? $"porta em {door}\n" : "porta não achada\n";
                // caminho de rato: beco -> fundos do janelão pela escada (reto ~95) e curta -> janelão
                log += $"rato beco->fundos: {Map.NavGrid.PathLen(new CSM.Sim.V2(387, 268), new CSM.Sim.V2(387, 170)):F0}\n";
                log += $"rato curta->janelão: {Map.NavGrid.PathLen(new CSM.Sim.V2(340, 325), new CSM.Sim.V2(450, 165)):F0}\n";
                log += $"praça->janelão (escada de mão): {Map.NavGrid.PathLen(new CSM.Sim.V2(570, 172), new CSM.Sim.V2(480, 172)):F0}\n";
                log += $"chão do B->casarão: {Map.NavGrid.PathLen(new CSM.Sim.V2(120, 330), new CSM.Sim.V2(175, 650)):F0}\n";
                log += $"carroça chão->tapete:{Map.NavGrid.PathLen(new CSM.Sim.V2(120, 330), new CSM.Sim.V2(160, 412)):F0}\n";
                var w = new WorldView { t = 1, cam = door, title = "TESTE: BONECO X PORTA" };
                w.actors.Add(new ActorView { id = 0, pos = door, z = doorZ, aim = MathF.PI * 0.75f, weapon = "rifle" });
                w.actors.Add(new ActorView { id = 1, pos = door + ao * 30, z = doorZ, aim = MathF.PI * 0.25f, weapon = "rifle" });
                w.actors.Add(new ActorView { id = 6, ct = true, pos = door + fora * 30 - ao * 25, z = doorZ, aim = MathF.PI, weapon = "rifle" });
                SavePng(fr.Render(w), Iso.W, Iso.H, "Logs/render/teste_porta.png", 4);
                // rua do meio: um no meio, outro colado na casa do lado da câmera (leste) -> silhueta
                var w2 = new WorldView { t = 1, cam = new CSM.Sim.V2(470, 380), title = "TESTE: SILHUETA ATRÁS DA CASA" };
                w2.actors.Add(new ActorView { id = 2, pos = new CSM.Sim.V2(460, 360), z = Map.MapData.GZ, aim = MathF.PI * 0.5f, weapon = "rifle" });
                w2.actors.Add(new ActorView { id = 7, ct = true, pos = new CSM.Sim.V2(519, 420), z = Map.MapData.GZ, aim = -MathF.PI * 0.5f, weapon = "awp" });
                w2.actors.Add(new ActorView { id = 8, ct = true, pos = new CSM.Sim.V2(450, 170), z = Map.MapData.UP, aim = MathF.PI * 0.5f, weapon = "awp" });
                SavePng(fr.Render(w2), Iso.W, Iso.H, "Logs/render/teste_silhueta.png", 4);
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            Directory.CreateDirectory("Logs/render");
            File.WriteAllText("Logs/render/teste.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Galeria dos agentes em fundo liso: CT e TR nas 8 direções, as 8 armas na mão de cada time, granadas e
        /// posturas. Saída Logs/render/galeria_*.png (256x192 ampliado x4).
        /// </summary>
        public static void Galeria()
        {
            string log = "";
            try
            {
                var sm = new StaticMap();
                var fr = new FrameRenderer(sm);
                var cam = new CSM.Sim.V2(500, 437);
                CSM.Sim.V2 R = new CSM.Sim.V2(1, -1) / MathF.Sqrt(2), D = new CSM.Sim.V2(1, 1) / MathF.Sqrt(2);
                CSM.Sim.V2 Slot(int col, int row) => cam + R * ((col - 1.5f) * 24f) + D * ((row - 0.5f) * 40f - 4f);
                float face = 0.15f;   // 3/4 de frente pra câmera, arma apontando pra baixo-direita
                void Save(WorldView w, string name, string[] labels)
                {
                    var rgb = fr.Render(w);
                    for (int i = 0; i < w.actors.Count && labels != null; i++)
                    {
                        var a = w.actors[i];
                        var (px, py) = Iso.Project(new V3(a.pos.x, a.pos.y, a.z));
                        string s = labels[i];
                        PixelFont.Draw(rgb, Iso.W, Iso.H, (int)(px - fr.ox) - PixelFont.Width(s) / 2, (int)(py - fr.oy) + 7, s, new C3(255, 255, 255), new C3(30, 20, 16));
                    }
                    SavePng(rgb, Iso.W, Iso.H, $"Logs/render/galeria_{name}.png", 4);
                }
                string[] dirs = { "L", "SE", "S", "SO", "O", "NO", "N", "NE" };
                foreach (bool ct in new[] { true, false })
                {
                    // 8 direções com o fuzil do time
                    var w = new WorldView { t = 1, cam = cam, plain = true, title = ct ? "CT - 8 DIREÇÕES" : "TR - 8 DIREÇÕES" };
                    var lab = new string[8];
                    for (int k = 0; k < 8; k++)
                    {
                        w.actors.Add(new ActorView { id = k, ct = ct, pos = Slot(k % 4, k / 4), z = Map.MapData.GZ, aim = k * MathF.PI / 4, weapon = ct ? "m4" : "ak" });
                        lab[k] = dirs[k];
                    }
                    Save(w, ct ? "ct_direcoes" : "tr_direcoes", lab);
                    // as 8 armas
                    string[] guns = { "ak", "m4", "m4s", "awp", "mp9", "mac10", "usp", "glock" };
                    w = new WorldView { t = 1, cam = cam, plain = true, title = ct ? "CT - ARMAS" : "TR - ARMAS" };
                    for (int k = 0; k < 8; k++)
                    {
                        w.actors.Add(new ActorView { id = k, ct = ct, pos = Slot(k % 4, k / 4), z = Map.MapData.GZ, aim = face, weapon = guns[k] });
                        lab[k] = Weapons.ALL[guns[k]].name;
                    }
                    Save(w, ct ? "ct_armas" : "tr_armas", lab);
                }
                // granadas na mão (meio do arremesso) e posturas: correndo, agachado, plantando
                var w3 = new WorldView { t = 1, cam = cam, plain = true, title = "GRANADAS E POSTURAS" };
                string[] nades = { "smoke", "flash", "molotov", "he" };
                var lab3 = new string[8];
                for (int k = 0; k < 4; k++)
                {
                    w3.actors.Add(new ActorView { id = k, ct = k % 2 == 0, pos = Slot(k, 0), z = Map.MapData.GZ, aim = face, weapon = "nade", nade = nades[k] });
                    lab3[k] = nades[k].ToUpper();
                }
                w3.actors.Add(new ActorView { id = 4, ct = true, pos = Slot(0, 1), z = Map.MapData.GZ, aim = face, weapon = "m4", speed = 60, runPhase = 1.2f }); lab3[4] = "CORRE";
                w3.actors.Add(new ActorView { id = 5, ct = false, pos = Slot(1, 1), z = Map.MapData.GZ, aim = face, weapon = "ak", speed = 60, runPhase = 2.6f }); lab3[5] = "CORRE";
                w3.actors.Add(new ActorView { id = 6, ct = true, pos = Slot(2, 1), z = Map.MapData.GZ, aim = face, weapon = "awp", crouch = 1 }); lab3[6] = "AGACHA";
                w3.actors.Add(new ActorView { id = 7, ct = false, pos = Slot(3, 1), z = Map.MapData.GZ, aim = face, weapon = "ak", planting = true, runPhase = 0.5f }); lab3[7] = "PLANTA";
                Save(w3, "granadas", lab3);
                log += "ok\n";
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            Directory.CreateDirectory("Logs/render");
            File.WriteAllText("Logs/render/galeria.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Teste dos efeitos no jogo: smoke no meio (cresce, enche a rua e escorre pro conector), tiro abrindo furo,
        /// fim desfazendo; molotov na curta (espalha e queima). Folha Logs/render/efeitos_jogo.png (quadros x3).
        /// </summary>
        public static void Efeitos()
        {
            string log = "";
            try
            {
                var sm = new StaticMap();
                var fr = new FrameRenderer(sm);
                int GZ = Map.MapData.GZ;
                var frames = new System.Collections.Generic.List<(string tit, C3[] img)>();
                var smk = new SmokeVolume(new V3(462, 372, GZ));
                var cam = new CSM.Sim.V2(470, 372);
                float age = 0;
                void Pass(float to) { while (age < to - 1e-4f) { float dt = MathF.Min(0.05f, to - age); smk.Update(dt); age += dt; } }
                var who = new ActorView { id = 6, ct = true, pos = new CSM.Sim.V2(470, 392), z = GZ, aim = MathF.PI * 0.75f, weapon = "rifle" };
                var tr = new ActorView { id = 1, pos = new CSM.Sim.V2(440, 470), z = GZ, aim = -MathF.PI * 0.4f, weapon = "rifle" };
                foreach (float a in new[] { 0.25f, 0.6f, 1.3f, 4f })
                {
                    Pass(a);
                    var w = new WorldView { t = a, cam = cam, title = $"SMOKE {a:0.0}s" };
                    w.smokes.Add(smk); w.actors.Add(who); w.actors.Add(tr);
                    frames.Add(($"smoke {a:0.0}s", (C3[])fr.Render(w).Clone()));
                }
                // tiro atravessando a nuvem (rajada do TR): abre o túnel, que fecha sozinho
                for (int k = 0; k < 4; k++)
                    smk.Shot(new V3(440, 470, GZ + 18), new V3(440 + 60, 470 - 150 + k * 6, GZ + 18 + k));
                foreach (float a in new[] { 4.05f, 4.6f, 5.4f })
                {
                    Pass(a);
                    var w = new WorldView { t = a, cam = cam, title = $"TIROS {a:0.0}s" };
                    w.smokes.Add(smk); w.actors.Add(who); w.actors.Add(tr);
                    if (a < 4.1f) w.shots.Add(new ShotView { from = new CSM.Sim.V2(440, 470), to = new CSM.Sim.V2(500, 320), fz = GZ + 18, tz = GZ + 18 });
                    frames.Add(($"furo {a:0.0}s", (C3[])fr.Render(w).Clone()));
                }
                Pass(14.2f);
                {
                    var w = new WorldView { t = 14.2f, cam = cam, title = "SMOKE ACABANDO" };
                    w.smokes.Add(smk); w.actors.Add(who); w.actors.Add(tr);
                    frames.Add(("acabando 14.2s", (C3[])fr.Render(w).Clone()));
                }
                var fire = new FireArea(new V3(352, 519, GZ));
                float fa = 0;
                foreach (float a in new[] { 0.15f, 0.4f, 2.0f, 2.1f, 6.6f })
                {
                    while (fa < a - 1e-4f) { float dt = MathF.Min(0.05f, a - fa); fire.Update(dt); fa += dt; }
                    var w = new WorldView { t = a, cam = new CSM.Sim.V2(360, 500), title = $"MOLOTOV {a:0.0}s" };
                    w.fires.Add(fire);
                    w.actors.Add(new ActorView { id = 2, pos = new CSM.Sim.V2(395, 560), z = GZ, aim = MathF.PI * 1.2f, weapon = "rifle", blind = true });
                    frames.Add(($"molotov {a:0.0}s", (C3[])fr.Render(w).Clone()));
                }
                // folha 4 colunas x3
                int sc = 3, cols = 4, rows = (frames.Count + cols - 1) / cols, FW = Iso.W * sc, FH = Iso.H * sc;
                var tex = new Texture2D(cols * FW, rows * FH, TextureFormat.RGB24, false);
                var px = new Color32[cols * FW * rows * FH];
                for (int n = 0; n < frames.Count; n++)
                {
                    int c0 = (n % cols) * FW, r0 = (n / cols) * FH;
                    var img = frames[n].img;
                    for (int y = 0; y < FH; y++)
                        for (int x = 0; x < FW; x++)
                        {
                            C3 c = img[(y / sc) * Iso.W + x / sc];
                            px[(rows * FH - 1 - (r0 + y)) * cols * FW + c0 + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255);
                        }
                }
                tex.SetPixels32(px); Directory.CreateDirectory("Logs/render");
                File.WriteAllBytes("Logs/render/efeitos_jogo.png", tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                log += $"ok: {frames.Count} quadros\n";
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            Directory.CreateDirectory("Logs/render");
            File.WriteAllText("Logs/render/efeitos.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Vídeo da smoke: à esquerda no aberto (meio), à direita num corredor estreito (saída do conector) — mostra a
        /// expansão saindo da lata, o volume ocupando o espaço, uma rajada abrindo túnel (2,5 s) e o fim (13-15 s).
        /// Quadros em Logs/render/smoke_quadros (x2), depois o mp4 é montado no Blender.
        /// </summary>
        public static void SmokeVideo()
        {
            string log = "";
            try
            {
                var sm = new StaticMap(); var fr = new FrameRenderer(sm);
                int GZ = Map.MapData.GZ;
                var cenas = new[] { (new V3(462, 372, GZ), new CSM.Sim.V2(470, 372), "ABERTO (MEIO)"), (new V3(700, 250, GZ), new CSM.Sim.V2(700, 255), "CORREDOR") };
                var smokes = new SmokeVolume[2];
                for (int c = 0; c < 2; c++) smokes[c] = new SmokeVolume(cenas[c].Item1);
                string dir = "Logs/render/smoke_quadros"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
                var tempos = new System.Collections.Generic.List<float>();
                for (float t = 0; t < 4.01f; t += 0.0833f) tempos.Add(t);
                for (float t = 12.8f; t < 15.01f; t += 0.0833f) tempos.Add(t);
                float age = 0; int n = 0; bool shot = false;
                const int sc = 2; int FW = Iso.W * sc, FH = Iso.H * sc;
                var tex = new Texture2D(FW * 2, FH, TextureFormat.RGB24, false); var px = new Color32[FW * 2 * FH];
                foreach (float t in tempos)
                {
                    while (age < t - 1e-4f) { float dt = MathF.Min(0.05f, t - age); foreach (var s in smokes) s.Update(dt); age += dt; }
                    if (!shot && t >= 2.5f)
                    {
                        shot = true;
                        smokes[0].Shot(new V3(420, 460, GZ + 18), new V3(500, 290, GZ + 18));
                        smokes[1].Shot(new V3(640, 250, GZ + 18), new V3(770, 250, GZ + 18));
                    }
                    for (int c = 0; c < 2; c++)
                    {
                        var w = new WorldView { t = t, cam = cenas[c].Item2, title = $"{cenas[c].Item3}  {t:0.0}s" };
                        w.smokes.Add(smokes[c]);
                        if (t >= 2.5f && t < 2.6f)
                            w.shots.Add(c == 0 ? new ShotView { from = new CSM.Sim.V2(420, 460), to = new CSM.Sim.V2(500, 290), fz = GZ + 18, tz = GZ + 18 }
                                               : new ShotView { from = new CSM.Sim.V2(640, 250), to = new CSM.Sim.V2(770, 250), fz = GZ + 18, tz = GZ + 18 });
                        var img = fr.Render(w);
                        for (int y = 0; y < FH; y++)
                            for (int x = 0; x < FW; x++)
                            {
                                C3 cc = img[(y / sc) * Iso.W + x / sc];
                                px[(FH - 1 - y) * FW * 2 + c * FW + x] = new Color32((byte)Mathf.Clamp(cc.r, 0, 255), (byte)Mathf.Clamp(cc.g, 0, 255), (byte)Mathf.Clamp(cc.b, 0, 255), 255);
                            }
                    }
                    tex.SetPixels32(px); File.WriteAllBytes($"{dir}/q_{n++:D4}.png", tex.EncodeToPNG());
                }
                UnityEngine.Object.DestroyImmediate(tex);
                log += $"ok {n} quadros; bolotas aberto {smokes[0].puffs.Count}, corredor {smokes[1].puffs.Count}\n";
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            File.WriteAllText("Logs/render/smoke_video.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>Laser da AWP: CT no meio olhando a rua, 3 quadros sem luneta (balança) e 3 na luneta (parado).
        /// Logs/render/awp_laser.png.</summary>
        public static void AwpLaser()
        {
            string log = "";
            try
            {
                var fr = new FrameRenderer(new StaticMap());
                int GZ = Map.MapData.GZ; const int sc = 3; int FW = Iso.W * sc, FH = Iso.H * sc;
                var tex = new Texture2D(FW * 3, FH * 2, TextureFormat.RGB24, false); var px = new Color32[FW * 3 * FH * 2];
                for (int n = 0; n < 6; n++)
                {
                    bool scoped = n >= 3; float t = (n % 3) * 0.45f;
                    var w = new WorldView { t = t, cam = new CSM.Sim.V2(470, 300), title = scoped ? "AWP NA LUNETA" : "AWP SEM LUNETA" };
                    w.actors.Add(new ActorView { id = 7, ct = true, pos = new CSM.Sim.V2(455, 250), z = GZ, aim = MathF.PI * 0.5f, weapon = "awp", scoped = scoped });
                    var img = fr.Render(w);
                    int c0 = (n % 3) * FW, r0 = (n / 3) * FH;
                    for (int y = 0; y < FH; y++)
                        for (int x = 0; x < FW; x++)
                        {
                            C3 c = img[(y / sc) * Iso.W + x / sc];
                            px[(FH * 2 - 1 - (r0 + y)) * FW * 3 + c0 + x] = new Color32((byte)Mathf.Clamp(c.r, 0, 255), (byte)Mathf.Clamp(c.g, 0, 255), (byte)Mathf.Clamp(c.b, 0, 255), 255);
                        }
                }
                tex.SetPixels32(px); File.WriteAllBytes("Logs/render/awp_laser.png", tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                log = "ok\n";
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            File.WriteAllText("Logs/render/awp_laser.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Navmesh desenhada no mapa inteiro: célula alcançável (verde), inacessível (vermelho), escada de mão (roxo),
        /// pulos (amarelo) e quedas (laranja), e os caminhos das bases pros bombs. Logs/render/navmesh.png + nav.txt.
        /// </summary>
        public static void NavReport()
        {
            string log = "";
            try
            {
                var sm = new StaticMap();
                var rgb = new C3[Iso.BW * Iso.BH];
                for (int i = 0; i < rgb.Length; i++) rgb[i] = sm.OBJ[i] == 0 ? Iso.BGC : sm.RGB[i] * 0.85f;
                log += Map.NavCheck.Report();
                void Blend(int x, int y, C3 c, float a) { if (x >= 0 && y >= 0 && x < Iso.BW && y < Iso.BH) rgb[y * Iso.BW + x] = C3.Lerp(rgb[y * Iso.BW + x], c, a); }
                (float, float) P(float x, float y, float z) => Iso.Project(new V3(x, y, z));
                C3 GREEN = new C3(60, 220, 90), RED = new C3(255, 40, 40), PURP = new C3(190, 90, 255), YEL = new C3(255, 230, 40), ORG = new C3(255, 130, 20);
                int n = Map.NavGrid.CellCount;
                for (int i = 0; i < n; i++)
                {
                    var (cx, cy) = Map.NavGrid.CellAt(i);
                    var c = Map.NavGrid.Center(cx, cy);
                    var (px, py) = P(c.x, c.y, Map.NavGrid.z[cx, cy]);
                    bool ok = Map.NavCheck.reachT[i] && Map.NavCheck.reachCT[i];
                    C3 col = Map.NavGrid.ladder[cx, cy] ? PURP : ok ? GREEN : RED;
                    for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) Blend((int)px + a, (int)py + b, col, ok ? 0.28f : 0.8f);
                    foreach (var (to, _, link) in Map.NavGrid.Out(i))
                    {
                        if (link != Map.NavGrid.Link.Jump && link != Map.NavGrid.Link.Drop) continue;
                        var (tx, ty) = Map.NavGrid.CellAt(to); var tc = Map.NavGrid.Center(tx, ty);
                        var (qx, qy) = P(tc.x, tc.y, Map.NavGrid.z[tx, ty]);
                        C3 lc = link == Map.NavGrid.Link.Jump ? YEL : ORG;
                        for (int s = 0; s <= 6; s++) Blend((int)(px + (qx - px) * s / 6f), (int)(py + (qy - py) * s / 6f), lc, 0.9f);
                    }
                }
                // caminhos das bases pros bombs (linha grossa) — azul CT, laranja TR
                void DrawPath(System.Collections.Generic.List<Map.NavGrid.Waypoint> p, C3 col)
                {
                    for (int pass = 0; pass < 2; pass++)   // contorno escuro primeiro, cor por cima
                        for (int k = 1; k < p.Count; k++)
                        {
                            var (ax, ay) = P(p[k - 1].p.x, p[k - 1].p.y, p[k - 1].z + 2); var (bx, by) = P(p[k].p.x, p[k].p.y, p[k].z + 2);
                            int m = (int)MathF.Max(MathF.Abs(bx - ax), MathF.Abs(by - ay)) + 1, R = pass == 0 ? 4 : 2;
                            for (int s = 0; s <= m; s++)
                            {
                                int x = (int)(ax + (bx - ax) * s / m), y = (int)(ay + (by - ay) * s / m);
                                for (int a = -R; a <= R; a++) for (int b = -R; b <= R; b++) Blend(x + a, y + b, pass == 0 ? new C3(20, 16, 16) : col, 1f);
                            }
                        }
                    foreach (var w in p)
                    {
                        var (x, y) = P(w.p.x, w.p.y, w.z + 2);
                        C3 dc = w.link == Map.NavGrid.Link.Jump ? YEL : w.link == Map.NavGrid.Link.Drop ? ORG : w.link == Map.NavGrid.Link.Ladder ? PURP : new C3(255, 255, 255);
                        for (int a = -4; a <= 4; a++) for (int b = -4; b <= 4; b++) if (a * a + b * b <= 16) Blend((int)x + a, (int)y + b, a * a + b * b > 9 ? new C3(20, 16, 16) : dc, 1f);
                    }
                }
                C3 TC = new C3(255, 150, 40), CTC = new C3(70, 150, 255);
                // muitas rotas: das duas bases até posições espalhadas pelo mapa inteiro (linha fina)
                var spots = Map.Spots.All(i => Map.NavCheck.reachT[i] && Map.NavCheck.reachCT[i]);
                void Thin(System.Collections.Generic.List<Map.NavGrid.Waypoint> p, C3 col)
                {
                    for (int k = 1; k < p.Count; k++)
                    {
                        var (ax, ay) = P(p[k - 1].p.x, p[k - 1].p.y, p[k - 1].z + 2); var (bx, by) = P(p[k].p.x, p[k].p.y, p[k].z + 2);
                        int m = (int)MathF.Max(MathF.Abs(bx - ax), MathF.Abs(by - ay)) + 1;
                        for (int s = 0; s <= m; s++) { int x = (int)(ax + (bx - ax) * s / m), y = (int)(ay + (by - ay) * s / m); Blend(x, y, col, 0.75f); Blend(x + 1, y, col, 0.75f); }
                    }
                }
                for (int k = 0; k < spots.Count; k += Math.Max(1, spots.Count / 70))
                {
                    Thin(Map.NavGrid.Path(Map.MapData.SPAWN_T[0], spots[k].p), TC);
                    Thin(Map.NavGrid.Path(Map.MapData.SPAWN_CT[0], spots[k].p), CTC);
                }
                // posições: cantinho branco, atrás de caixa ciano, em cima de caixa amarelo
                foreach (var s in spots)
                {
                    var (x, y) = P(s.p.x, s.p.y, s.z + 1);
                    C3 sc_ = s.kind == Map.Spots.Kind.Canto ? new C3(255, 255, 255) : s.kind == Map.Spots.Kind.AtrasCaixa ? new C3(60, 230, 255) : new C3(255, 230, 40);
                    for (int a = -4; a <= 4; a++) for (int b = -4; b <= 4; b++) { int d = a * a + b * b; if (d <= 16) Blend((int)x + a, (int)y + b, d > 8 ? new C3(20, 16, 16) : sc_, 1f); }
                }
                SavePng(rgb, Iso.BW, Iso.BH, "Logs/render/navmesh.png");
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            Directory.CreateDirectory("Logs/render");
            File.WriteAllText("Logs/render/nav.txt", log);
            EditorApplication.Exit(0);
        }

        /// <summary>Mapa inteiro estático (BW x BH) com fundo, pra conferir o traçado.</summary>
        public static void Overview()
        {
            string log = "";
            try
            {
                var sw = Stopwatch.StartNew();
                var sm = new StaticMap();
                log += $"mapa estático em {sw.Elapsed.TotalSeconds:F1} s\n";
                var rgb = new C3[Iso.BW * Iso.BH];
                for (int i = 0; i < rgb.Length; i++) rgb[i] = sm.OBJ[i] == 0 ? Iso.BGC : sm.RGB[i];
                SavePng(rgb, Iso.BW, Iso.BH, "Logs/render/mapa.png", int.Parse(Arg("-escala", "1")));
                log += Map.MapCheck.Report();
            }
            catch (Exception e) { log += "ERRO " + e + "\n"; }
            File.WriteAllText("Logs/render/log.txt", log);
            EditorApplication.Exit(0);
        }
    }
}
