using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CSM.Sim;
using CSM.Map;

namespace CSM.View
{
    /// <summary>
    /// O quadro do jogo com os personagens em sprite (Blender, Logs/sprites_jogo*): o mapa vem do FrameRenderer (voxel,
    /// smoke, fogo, tiros) em 2x2 pedaços de 256x192, ampliado 3x; cada bot vira o sprite da animação do que ele está
    /// fazendo, virado pra onde mira, com profundidade (atrás de parede vira silhueta, dentro da smoke some). Usado pelo
    /// vídeo (Editor/VideoJogo) e pela partida ao vivo no Play (PartidaAoVivo).
    /// </summary>
    public static class TelaJogo
    {
        public const int OW = 1280, OH = 720, U = 3;            // saída e ampliação do quadro do jogo (6 px por unidade)
        public const int NW = 512, NH = 384;                     // quadro nativo montado com 2x2 pedaços
        public const float UNID_POR_BU = 25f, PX_POR_BU = 140f;  // boneco do Blender: 1 unidade Blender = 25 do jogo
        public static readonly float K = Iso.S * U / (PX_POR_BU / UNID_POR_BU);   // escala do sprite no vídeo (~1,07)
        public static readonly string SPR = File.Exists("Logs/sprites_jogo32/tr_mira_AK47_31_00.png") ? "Logs/sprites_jogo32"
                                   : Directory.Exists("Logs/sprites_jogo16") ? "Logs/sprites_jogo16" : "Logs/sprites_jogo";   // 32 direções (11,25°): olha 360
        public static int NDIR = -1;
        /// <summary>Quantas direções a biblioteca tem (32, 16 ou 8).</summary>
        public static int Direcoes()
        {
            if (NDIR < 0) NDIR = File.Exists(Path.Combine(SPR, "tr_mira_AK47_31_00.png")) ? 32 : File.Exists(Path.Combine(SPR, "tr_mira_AK47_15_00.png")) ? 16 : 8;
            return NDIR;
        }
        public static readonly C3 TC = new C3(240, 170, 70), CTC = new C3(110, 180, 255);

        public class Spr { public int w, h; public Color32[] px; public float ax, ay; }
        public static readonly Dictionary<string, Spr> cache = new Dictionary<string, Spr>();
        public static readonly Dictionary<string, int> nQuadros = new Dictionary<string, int>();

        /// <summary>Ao vivo: guarda só o retângulo do boneco (o quadro de 288 é quase todo transparente; o vídeo valida a borda, aqui não).</summary>
        public static bool recortar;
        static void Recorta(Spr s)
        {
            int x0 = s.w, y0 = s.h, x1 = -1, y1 = -1;
            for (int y = 0; y < s.h; y++) for (int x = 0; x < s.w; x++) if (s.px[y * s.w + x].a >= 100) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            if (x1 < 0) return;
            int w = x1 - x0 + 1, h = y1 - y0 + 1; var px = new Color32[w * h];
            for (int y = 0; y < h; y++) Array.Copy(s.px, (y + y0) * s.w + x0, px, y * w, w);
            s.px = px; s.w = w; s.h = h; s.ax -= x0; s.ay -= y0;
        }

        public static Spr Load(string nome)
        {
            if (cache.TryGetValue(nome, out var s)) return s;
            string f = Path.Combine(SPR, nome + ".png");
            if (!File.Exists(f)) { cache[nome] = null; return null; }
            var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(f));
            var raw = tex.GetPixels32(); int w = tex.width, h = tex.height;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) Array.Copy(raw, (h - 1 - y) * w, px, y * w, w);   // de baixo pra cima → de cima pra baixo
            UnityEngine.Object.DestroyImmediate(tex);
            // pé do boneco: a raiz (0,0,0) projetada — FOCO (0,08; 0; 0,5) fica no centro do quadro
            s = new Spr { w = w, h = h, px = px, ax = w / 2f - 7.92f, ay = h / 2f + 38.9f };
            if (recortar) Recorta(s);
            cache[nome] = s; return s;
        }

        /// <summary>Quantos quadros a animação tem (procura até faltar).</summary>
        public static int Quadros(string team, string anim, string arma)
        {
            string k = $"{team}_{anim}_{arma}";
            if (nQuadros.TryGetValue(k, out int n)) return n;
            n = 0; while (File.Exists(Path.Combine(SPR, $"{k}_0_{n:00}.png"))) n++;
            nQuadros[k] = n; return n;
        }

        public static string Padrao(bool ct) => ct ? "M4A4" : "AK47";

        /// <summary>Quanto o corpo anda num ciclo de passo (Blender: pé plantado = 4 x passada), por animação: o ciclo
        /// toca na velocidade do boneco e o pé não patina.</summary>
        public static Dictionary<string, float> passo;
        public static float Ciclo(string anim)
        {
            if (passo == null)
            {
                passo = new Dictionary<string, float> { ["andar"] = 13, ["agachado"] = 8, ["faca_andar"] = 15 };
                string f = Path.Combine(SPR, "passo.txt");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f)) { var p = l.Split(' '); if (p.Length == 2) passo[p[0]] = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture); }
            }
            return passo.TryGetValue(anim, out float c) ? c : 13;
        }
        public static string ArmaSprite(Agent a, out bool faltou)
        {
            faltou = false;
            string w = a.gunId switch { "ak" => "AK47", "m4" => "M4A4", "m4s" => "M4A1S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC10", "usp" => "USP", "glock" => "Glock", "deagle" => "Deagle", _ => null };
            string team = a.IsT ? "tr" : "ct";
            if (w == null || Quadros(team, "mira", w) == 0)
            {   // arma sem sprite nesse time (pegou do chão, Deagle...): a mais parecida que existe
                faltou = true;
                w = WeaponStats.Pistola(a.gunId) ? (a.IsT ? "Glock" : "USP") : Padrao(!a.IsT);
            }
            return w;
        }

        /// <summary>Estado da animação de cada bot (fase do passo, quando começou a ação, a recarga, o tiro).</summary>
        public class Anim { public float passo; public string acao; public float acaoIni, acaoDur; public V2 ultPos; public bool tem; }

        public static readonly Dictionary<string, int> usoAnim = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> faltas = new Dictionary<string, int>();
        public static void Conta(Dictionary<string, int> d, string k) => d[k] = d.TryGetValue(k, out int v) ? v + 1 : 1;
        public static double ciclosAndando, velAndando; public static int quadrosAndando;

        public static void Quadro(Match m, FrameRenderer fr, V2 cam, Dictionary<int, Anim> est, C3[] nat, float[] prof, float[] fum, C3[] buf, int rn, Sim.Partida pt)
        {
            // origem do quadro nativo grande (2x2 pedaços), centrado na câmera
            var (cx, cy) = Iso.Project(new V3(cam.x, cam.y, Iso.FT + 8));
            int OX = Math.Clamp((int)cx - NW / 2, 0, Iso.BW - NW), OY = Math.Clamp((int)cy - NH / 2, 0, Iso.BH - NH);
            var shots = m.shots.Where(s => m.t - s.t < 0.07f).Select(s => new ShotView { from = s.a, to = s.b, fz = s.za, tz = s.zb, hit = s.hit, hs = s.hs }).ToList();
            for (int ty = 0; ty < 2; ty++)
                for (int tx = 0; tx < 2; tx++)
                {
                    var w = new WorldView { t = m.t, cam = cam, origemX = OX + tx * Iso.W, origemY = OY + ty * Iso.H, bruto = true };
                    w.smokes.AddRange(m.smokes); w.fires.AddRange(m.fires); w.shots.AddRange(shots);
                    var img = fr.Render(w); var T = fr.Prof; var S = fr.Fumaca;
                    for (int y = 0; y < Iso.H; y++)
                        for (int x = 0; x < Iso.W; x++)
                        {
                            int d = (ty * Iso.H + y) * NW + tx * Iso.W + x, s = y * Iso.W + x;
                            nat[d] = img[s]; prof[d] = T[s]; fum[d] = S[s];
                        }
                }
            // janela de 427x240 nativos no centro do quadro grande → ampliada 3x
            int vx = (NW - OW / U) / 2, vy = (NH - OH / U) / 2;
            for (int y = 0; y < OH; y++) for (int x = 0; x < OW; x++) buf[y * OW + x] = nat[(vy + y / U) * NW + vx + x / U];
            float ORX = OX + vx, ORY = OY + vy;   // canto da janela na imagem do mapa (px nativos)
            (float x, float y) Tela(V3 p) { var (px, py) = Iso.Project(p); return ((px - ORX) * U, (py - ORY) * U); }
            float cosEl = MathF.Cos(Iso.EL), sinEl = MathF.Sin(Iso.EL);

            // granadas voando (bolinha) — o sprite de arremesso mostra a mão
            foreach (var n in m.flying)
            {
                var (gx, gy) = Tela(new V3(n.x, n.y, n.z));   // posição da física (quica, rola)
                Disco(buf, gx, gy, 4, n.l.kind == "smoke" ? new C3(230, 230, 230) : n.l.kind == "molotov" ? new C3(255, 120, 30) : n.l.kind == "he" ? new C3(90, 140, 60) : new C3(255, 255, 160));
            }
            // o que está no chão: armas de quem morreu/trocou e granadas dropadas
            foreach (var d in m.drops)
            {
                // a arma de quem acabou de morrer já aparece caindo no sprite de morrer: não desenha outra por cima
                if (m.agents.Any(a => !a.alive && (a.pos - d.pos).Len < 20)) continue;
                var (dx, dy) = Tela(new V3(d.pos.x, d.pos.y, d.z));
                int len = d.gun == "awp" ? 26 : WeaponStats.Pistola(d.gun) ? 10 : 20;
                for (int i = -len / 2; i <= len / 2; i++) for (int j = -2; j <= 2; j++) Pix(buf, (int)dx + i, (int)dy + j - i / 4, j == -2 || j == 2 ? new C3(15, 14, 14) : new C3(55, 55, 58));
            }
            foreach (var it in m.itensChao)
            {
                var (ix, iy) = Tela(new V3(it.pos.x, it.pos.y, it.z));
                Disco(buf, ix, iy, 5, it.nade == "smoke" ? new C3(200, 200, 200) : it.nade == "molotov" ? new C3(230, 110, 40) : it.nade == "he" ? new C3(90, 140, 60) : new C3(240, 240, 170));
            }
            // C4 no chão (plantado ou caído)
            if (m.bombPlanted || m.bombDropped)
            {
                V2 bp = m.bombPlanted ? m.bombPos : m.droppedBomb;
                var (bx, by) = Tela(new V3(bp.x, bp.y, m.bombPlanted ? m.bombZ : NavGrid.ZAt(bp)));
                C4(buf, bx, by, m.bombPlanted && !m.defused && !m.exploded, m.t, m.BombLeft);
            }
            // bots do fundo pra frente
            var ordem = m.agents.OrderByDescending(a => FrameRenderer.Depth(new V3(a.pos.x, a.pos.y, a.z))).ToList();
            foreach (var a in ordem)
            {
                if (!est.TryGetValue(a.id, out var e)) { e = new Anim { ultPos = a.pos }; est[a.id] = e; }
                string team = a.IsT ? "tr" : "ct";
                string arma = ArmaSprite(a, out bool faltou);
                if (faltou && a.alive) Conta(faltas, $"{team.ToUpper()} com {a.gunId} → {arma}");
                // ação começando (plantar, desarmar, granada): guarda quando e quanto dura
                if (a.brain.action != e.acao) { e.acao = a.brain.action; e.acaoIni = m.t; e.acaoDur = MathF.Max(0.2f, a.brain.ActionEnd - m.t); }
                float dist = (a.pos - e.ultPos).Len; e.ultPos = a.pos;
                string anim; int quadro; bool loop = false; float u = 0;
                if (!a.alive) { anim = "morrer"; arma = Padrao(!a.IsT); u = (m.t - a.diedAt) / 0.8f; }
                else if (a.brain.action == "plantar") { anim = "plantar"; arma = Padrao(false); u = (m.t - e.acaoIni) / e.acaoDur; }
                else if (a.brain.action == "desarmar") { anim = "desarmar"; arma = Padrao(true); u = (m.t - e.acaoIni) * 10 / 8f; loop = true; }
                else if (a.brain.action == "granada") { anim = "granada"; arma = Padrao(!a.IsT); u = (m.t - e.acaoIni) / e.acaoDur; }
                else if (a.Blind(m.t)) { anim = "cego"; arma = Padrao(!a.IsT); u = m.t * 10 / 8f; loop = true; }
                else if (!a.faca && a.gun.Reloading) { anim = "recarga"; u = 1 - a.gun.reloadLeft / a.gun.s.Reload; }
                else if (!a.faca && m.t - a.lastShotT < (arma == "AWP" ? 0.5f : 0.25f)) { anim = "tiro"; u = (m.t - a.lastShotT) / (arma == "AWP" ? 0.5f : 0.25f); }
                else if (a.noAr) { anim = (a.faca ? "faca_" : "") + (a.vz > 0 ? "no_ar_sobe" : "no_ar_cai"); if (a.faca) arma = Padrao(!a.IsT); Conta(usoAnim, a.vz > 0 ? "PULO" : "QUEDA"); }
                else if (a.faca) { anim = a.Speed > 6 ? "faca_andar" : "faca_parado"; arma = Padrao(!a.IsT); e.passo += dist / Ciclo("faca_andar"); u = e.passo; loop = true; }
                else if (a.Speed > 6 && a.crouch) { anim = "agachado"; arma = Padrao(!a.IsT); e.passo += dist / Ciclo("agachado"); u = e.passo; loop = true; }
                else if (a.Speed > 6) { anim = "andar"; e.passo += dist / Ciclo("andar"); u = e.passo; loop = true; ciclosAndando += dist / Ciclo("andar"); quadrosAndando++; velAndando += a.Speed; }
                else anim = "mira";
                int n = Quadros(team, anim, arma);
                if (n == 0) { Conta(faltas, $"{team}_{anim}_{arma} (não existe)"); anim = "mira"; arma = Padrao(!a.IsT); n = Quadros(team, anim, arma); if (n == 0) continue; }
                quadro = loop ? ((int)(u * n) % n + n) % n : Math.Clamp((int)(u * n), 0, n - 1);
                int nd = Direcoes();
                int dirI = (((int)MathF.Round(a.aim / (2 * MathF.PI / nd))) % nd + nd) % nd;
                Conta(usoAnim, $"{team}_{anim}_{arma}");
                var spr = Load($"{team}_{anim}_{arma}_{dirI}_{quadro:00}");
                if (spr == null) { Conta(faltas, $"{team}_{anim}_{arma}_{dirI}_{quadro:00}.png (arquivo)"); continue; }
                V3 pe = new V3(a.pos.x, a.pos.y, a.z);
                var (fx, fy) = Tela(pe);
                float dPe = FrameRenderer.Depth(pe) - 2f;
                // sombra no chão (se o pé não está atrás de nada)
                if (a.alive) Sombra(buf, prof, fx, fy, dPe + 2, vx, vy);
                // morto (da metade da queda em diante) está deitado: o corpo é chão, não um boneco em pé
                bool deitado = !a.alive && quadro >= n / 2;
                Desenha(buf, prof, fum, spr, fx, fy, dPe, cosEl, sinEl, vx, vy, a.alive ? (a.IsT ? TC : CTC) : (C3?)null, deitado);
                // nome e vida em cima da cabeça
                if (a.alive)
                {
                    int nx = (int)fx - 30, ny = (int)(fy - 112 * K);
                    Texto(buf, nx, ny - 12, a.name + (a.hasBomb ? " C4" : ""), a.IsT ? TC : CTC, 1);
                    for (int i = 0; i < 60; i++) for (int j = 0; j < 4; j++) Pix(buf, nx + i, ny + j, i < a.hp * 0.6f ? (a.hp > 40 ? new C3(110, 220, 110) : new C3(230, 90, 70)) : new C3(40, 30, 30));
                }
            }
            Hud(m, buf, rn, pt);
        }

        /// <summary>Cola o sprite com profundidade: cada linha acima do pé é um ponto mais alto do corpo (plano em pé
        /// voltado pra câmera). Atrás de parede → silhueta pontilhada na cor do time; dentro da smoke → some.</summary>
        public static byte[] mask = new byte[0];
        public static void Desenha(C3[] buf, float[] prof, float[] fum, Spr s, float fx, float fy, float dPe, float cosEl, float sinEl, int vx, int vy, C3? time, bool deitado = false)
        {
            // em pé: cada linha acima do pé é um ponto mais alto (plano vertical). Deitado: cada linha é um ponto do CHÃO
            // mais longe/perto da câmera (subir na tela = mais fundo), senão pedaço do corpo some atrás de parede à toa
            float porLinha = deitado ? -cosEl / (sinEl * Iso.S * U) : -sinEl / (cosEl * Iso.S * U);
            int w = (int)(s.w * K), h = (int)(s.h * K);
            int x0 = (int)MathF.Round(fx - s.ax * K), y0 = (int)MathF.Round(fy - s.ay * K);
            if (mask.Length < (w + 2) * (h + 2)) mask = new byte[(w + 2) * (h + 2)];
            Array.Clear(mask, 0, (w + 2) * (h + 2));
            int MW = w + 2;
            // 1ª passada: o que aparece (1) e o que está escondido atrás de parede ou dentro da smoke (2)
            for (int j = 0; j < h; j++)
            {
                int Y = y0 + j; if (Y < 0 || Y >= OH) continue;
                int sy = Math.Min(s.h - 1, (int)(j / K));
                float d = deitado ? dPe - 2 + (fy - Y) * -porLinha : dPe + MathF.Max(0, fy - Y) * porLinha;   // deitado: um pouco acima do piso
                for (int i = 0; i < w; i++)
                {
                    int X = x0 + i; if (X < 0 || X >= OW) continue;
                    var c = s.px[sy * s.w + Math.Min(s.w - 1, (int)(i / K))];
                    if (c.a < 100) continue;
                    int ni = (vy + Y / U) * NW + vx + X / U;
                    bool escondido = fum[ni] > 0.5f || d >= prof[ni];
                    mask[(j + 1) * MW + i + 1] = (byte)(escondido ? 2 : 1);
                    if (escondido) continue;
                    int o = Y * OW + X;
                    var col = new C3(c.r, c.g, c.b);
                    buf[o] = c.a >= 250 ? col : C3.Lerp(buf[o], col, c.a / 255f);
                }
            }
            if (time == null) return;
            // 2ª passada: silhueta na cor do time (borda cheia, miolo pontilhado) — dá pra ver todo mundo sempre
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int k = (j + 1) * MW + i + 1; if (mask[k] != 2) continue;
                    int X = x0 + i, Y = y0 + j, o = Y * OW + X;
                    bool borda = mask[k - 1] == 0 || mask[k + 1] == 0 || mask[k - MW] == 0 || mask[k + MW] == 0;
                    if (borda) buf[o] = time.Value;
                    else if ((X + Y) % 2 == 0) buf[o] = C3.Lerp(buf[o], time.Value, 0.55f);
                }
        }

        /// <summary>C4 no chão: caixinha escura com a luz vermelha piscando (plantado pisca mais rápido no fim).</summary>
        public static void C4(C3[] buf, float x, float y, bool plantado, float t, float resta)
        {
            for (int j = -7; j <= 3; j++) for (int i = -12; i <= 12; i++) Pix(buf, (int)x + i, (int)y + j, j == -7 || j == 3 || i == -12 || i == 12 ? new C3(20, 20, 18) : new C3(70, 72, 60));
            for (int i = -9; i <= 9; i += 3) Pix(buf, (int)x + i, (int)y - 2, new C3(40, 40, 36));
            float hz = plantado ? (resta < 10 ? 4 : 1.2f) : 0;
            bool aceso = plantado && (t * hz) % 1 < 0.35f;
            Disco(buf, x + 7, y - 3, 3, aceso ? new C3(255, 40, 30) : new C3(110, 20, 20));
        }

        public static void Sombra(C3[] buf, float[] prof, float fx, float fy, float dPe, int vx, int vy)
        {
            for (int y = -10; y <= 10; y++)
                for (int x = -26; x <= 26; x++)
                {
                    if (x * x / 676f + y * y / 100f > 1) continue;
                    int X = (int)fx + x, Y = (int)fy + y; if (X < 0 || Y < 0 || X >= OW || Y >= OH) continue;
                    int ni = (vy + Y / U) * NW + vx + X / U;
                    if (prof[ni] < dPe - 6) continue;
                    buf[Y * OW + X] = buf[Y * OW + X] * 0.62f;
                }
        }

        public static void Hud(Match m, C3[] buf, int rn, Sim.Partida pt)
        {
            for (int y = 0; y < 34; y++) for (int x = 0; x < OW; x++) buf[y * OW + x] = buf[y * OW + x] * 0.35f;
            float rest = m.bombPlanted ? m.BombLeft : m.RoundLeft;
            string relogio = $"{(int)rest / 60}:{(int)rest % 60:00}";
            var tTime = m.agents.First(a => a.IsT);
            Texto(buf, 20, 8, $"ROUND {rn}", new C3(240, 240, 240), 2);
            Texto(buf, OW / 2 - 150, 8, $"TR {m.Alive(Team.T)} VIVOS", TC, 2);
            Texto(buf, OW / 2 - 10, 8, relogio, m.bombPlanted ? new C3(255, 80, 60) : new C3(255, 255, 255), 2);
            Texto(buf, OW / 2 + 70, 8, $"CT {m.Alive(Team.CT)} VIVOS", CTC, 2);
            if (m.bombPlanted) Texto(buf, OW - 200, 8, $"BOMB NO {m.planSite}", new C3(255, 80, 60), 2);
            int fyy = 44;
            foreach (var (t, s) in m.feed.Skip(Math.Max(0, m.feed.Count - 5)))
            {
                if (m.t - t > 6) continue;
                int wtx = s.Length * 12 + 10;
                for (int y = fyy - 4; y < fyy + 16; y++) for (int x = OW - wtx - 14; x < OW - 10; x++) buf[y * OW + x] = buf[y * OW + x] * 0.4f;
                Texto(buf, OW - wtx - 8, fyy, s, new C3(240, 240, 240), 2); fyy += 22;
            }
            if (m.over) Texto(buf, OW / 2 - 220, OH / 2 - 160, $"{m.winner} GANHOU: {m.reason}".ToUpper(), new C3(255, 230, 80), 3);
        }

        // ---------------------------------------------------------------- texto e desenho
        public static readonly C3 NADA = new C3(-1, -1, -1);
        public static void Texto(C3[] buf, int x, int y, string s, C3 c, int esc)
        {
            s = s.ToUpper();
            int tw = PixelFont.Width(s) + 2, th = PixelFont.GH + 2;
            var tmp = new C3[tw * th]; for (int i = 0; i < tmp.Length; i++) tmp[i] = NADA;
            PixelFont.Draw(tmp, tw, th, 0, 0, s, c, new C3(10, 8, 8));
            for (int j = 0; j < th * esc; j++) for (int i = 0; i < tw * esc; i++)
            {
                var q = tmp[(j / esc) * tw + i / esc]; if (q.r < 0) continue;
                Pix(buf, x + i, y + j, q);
            }
        }
        public static void Pix(C3[] b, int x, int y, C3 c) { if (x < 0 || y < 0 || x >= OW || y >= OH) return; b[y * OW + x] = c; }
        public static void Disco(C3[] b, float cx, float cy, float r, C3 c) { for (int y = (int)(cy - r); y <= (int)(cy + r); y++) for (int x = (int)(cx - r); x <= (int)(cx + r); x++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Pix(b, x, y, c); }

        /// <summary>Câmera automática: onde está a ação (duelo) > bomb plantado > quem leva o bomb > meio dos TR; nunca o vazio.</summary>
        public static void Mirar(Match m, ref V2 cam, ref V2 foco, ref float focoAte, float dt)
        {
            var duelo = m.agents.Where(a => a.alive && a.brain.target != null).OrderBy(a => a.id).FirstOrDefault();
            if (duelo != null) { foco = (duelo.pos + duelo.brain.target.pos) / 2; focoAte = m.t + 1.5f; }
            else if (m.t > focoAte)
            {
                var vivosT = m.agents.Where(a => a.alive && a.IsT).ToList();
                var carrier = vivosT.FirstOrDefault(a => a.hasBomb);
                foco = m.bombPlanted ? m.bombPos : carrier != null ? carrier.pos : vivosT.Count > 0 ? vivosT.Aggregate(new V2(0, 0), (s, a) => s + a.pos) / vivosT.Count : cam;
            }
            V2 f = foco;
            var vivos = m.agents.Where(a => a.alive).ToList();
            if (vivos.Count > 0 && !vivos.Any(a => (a.pos - f).Len < 150))
            {
                var perto = vivos.OrderBy(a => (a.pos - f).Len).First();
                foco = m.bombPlanted && (perto.pos - f).Len < 400 ? (perto.pos + f) / 2 : perto.pos;
            }
            cam = cam + (foco - cam) * (1 - MathF.Exp(-2.2f * dt));
        }
    }
}
