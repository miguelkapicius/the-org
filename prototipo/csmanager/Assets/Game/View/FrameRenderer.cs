using System;
using System.Collections.Generic;
using CSM.Sim;
using static CSM.View.Iso;

namespace CSM.View
{
    /// <summary>O que o visual precisa saber de um jogador num quadro (sem lógica de jogo).</summary>
    public class ActorView
    {
        public int id; public bool ct; public string name;
        public V2 pos; public float z, aim, speed, crouch;
        public bool alive = true; public float deadFor = -1;   // segundos desde a morte (-1 = vivo)
        public bool hsDeath; public V2 killerPos;
        public string weapon = "rifle";        // ak, m4, m4s, awp, mp9, mac10, usp, glock (rifle/pistol = padrão do time)
        public string nade = "smoke";          // granada na mão no arremesso: smoke, flash, molotov, he
        public float recoil, white, throwT = -1, runPhase;
        public bool planting, defusing, blind;
        public bool scoped;                    // AWP na luneta (laser parado e comprido)
        public int target = -1;                // em duelo com (id) -> mostra a mira
    }

    public struct ShotView { public V2 from, to; public float fz, tz; public bool hit, hs, awp; }

    public class FeedLine { public string text; public bool ctKiller; public float age; }
    public class CallLine { public string text; public bool ct; }

    public class WorldView
    {
        public float t; public V2 cam;
        public List<ActorView> actors = new List<ActorView>();
        public List<ShotView> shots = new List<ShotView>();
        public List<FeedLine> feed = new List<FeedLine>();
        public List<CallLine> calls = new List<CallLine>();
        public string clock = "1:55"; public int scoreT, scoreCT; public string title;
        public bool plain;   // fundo liso (galeria de personagens), sem o mapa
        /// <summary>Pedaço de um quadro maior (vídeo com sprites): origem fixa na imagem do mapa, sem vinheta e sem HUD.</summary>
        public int? origemX, origemY; public bool bruto;
        public List<SmokeVolume> smokes = new List<SmokeVolume>();
        public List<FireArea> fires = new List<FireArea>();
    }

    /// <summary>
    /// Monta o quadro 256x192: recorte da imagem estática do mapa, anel do time e sombra no chão, personagens
    /// rasterizados com profundidade, contorno pixel art (fora/entre partes), fundo, vinheta quantizada, tiros e HUD.
    /// </summary>
    public class FrameRenderer
    {
        readonly StaticMap sm;
        readonly float[] T = new float[W * H];
        public readonly C3[] RGB = new C3[W * H];
        readonly short[] OBJ = new short[W * H], PART = new short[W * H];
        readonly bool[] FL = new bool[W * H];
        readonly byte[] SIL = new byte[W * H];   // silhueta do boneco atrás de casa: 1 = TR, 2 = CT
        public int ox, oy;
        static readonly C3 TCOL = new C3(240, 170, 70), CTCOL = new C3(110, 180, 255);

        public FrameRenderer(StaticMap sm) { this.sm = sm; }

        public static float Depth(V3 p) => p.Dot(DV) + LRAY;
        /// <summary>Profundidade por pixel do último quadro (menor = mais perto) e opacidade da smoke (vídeo com sprites).</summary>
        public float[] Prof => T;
        public float[] Fumaca => SA;
        (float x, float y) Pj(V3 p) { var (x, y) = Project(p); return (x - ox, y - oy); }

        public C3[] Render(WorldView w)
        {
            var (cx, cy) = Project(new V3(w.cam.x, w.cam.y, FT + 8));
            Array.Clear(SIL, 0, SIL.Length);
            ox = Math.Clamp(w.origemX ?? (int)MathF.Round(cx - W / 2f), 0, BW - W);
            oy = Math.Clamp(w.origemY ?? (int)MathF.Round(cy - H / 2f), 0, BH - H);
            for (int j = 0; j < H; j++)
            {
                int src = (oy + j) * BW + ox, dst = j * W;
                Array.Copy(sm.T, src, T, dst, W);
                Array.Copy(sm.RGB, src, RGB, dst, W);
                Array.Copy(sm.OBJ, src, OBJ, dst, W);
                Array.Copy(sm.OBJ, src, PART, dst, W);
                Array.Copy(sm.FLOOR, src, FL, dst, W);
            }
            plain = w.plain;
            if (plain)
                for (int i = 0; i < W * H; i++)
                {
                    int x = i % W, y = i / W;
                    T[i] = float.PositiveInfinity; OBJ[i] = 1; PART[i] = 1; FL[i] = true;
                    RGB[i] = ((x + ox) / 16 + (y + oy) / 16) % 2 == 0 ? new C3(206, 176, 128) : new C3(196, 166, 120);
                }
            // anel do time + sombra no chão (só no piso na altura do pé)
            foreach (var a in w.actors)
            {
                V3 foot = new V3(a.pos.x, a.pos.y, a.z);
                if (a.alive)
                {
                    Ring(foot, a.ct ? CTCOL : TCOL);
                    Shadow(foot, 7.5f, 5.5f, 0.7f);
                }
                else if (a.deadFor > 0.3f)
                    Shadow(Chibi.LocalToWorld(DeadTheta(a), new V3(-8, 0, 0), foot), 15, 9, 0.7f);
            }
            // personagens
            var muz = new Dictionary<int, V3>();
            foreach (var a in w.actors)
            {
                var team = a.ct ? TeamMats.E : TeamMats.TEAMS_T[a.id % 5];
                var pose = PoseFor(a);
                var (prims, gB) = Chibi.Build(pose, team);
                float tilt = 0, theta = a.aim;
                if (!a.alive)
                {
                    tilt = 1.42f * MathX.Smooth(a.deadFor / 0.45f);
                    theta = DeadTheta(a);
                    if (a.hsDeath && a.ct) prims.RemoveAll(p => p.part == 1 || p.part == 3);   // o capacete voa
                }
                V3 pos3 = new V3(a.pos.x, a.pos.y, a.z);
                muz[a.id] = Chibi.LocalToWorld(theta, gB, pos3);
                Merge(CharLayer.Render(prims, theta, pos3, tilt, -1, a.white, ox - 8, oy - 8, ox + W + 8, oy + H + 8), (short)(10 + a.id), (byte)(a.alive ? (a.ct ? 2 : 1) : 0));
                // capacete voando no HS
                if (!a.alive && a.hsDeath && a.deadFor < 1.6f)
                {
                    float k = a.deadFor * 20;
                    V2 dv = a.pos - a.killerPos; float dl = MathF.Max(dv.Len, 1e-6f); dv = dv / dl;
                    V3 v = new V3(dv.x * 1.1f, dv.y * 1.1f, 2.2f);
                    V3 hp = new V3(a.pos.x, a.pos.y, a.z + 20.3f) + v * k + new V3(0, 0, -0.16f * k * k);
                    hp.z = MathF.Max(hp.z, a.z + 3.5f);
                    Merge(CharLayer.Render(Chibi.HelmPrims(team), k * 0.75f, hp, MathF.Min(k * 0.5f, 2.6f), 0, 0, ox - 8, oy - 8, ox + W + 8, oy + H + 8), (short)(30 + a.id));
                }
            }
            Outline();
            FirePass(w);    // fogo antes: a fumaça cobre
            SmokePass(w);
            for (int i = 0; i < W * H; i++)
            {
                if (OBJ[i] == 0 && !outside[i]) RGB[i] = BGC;
                if (!w.bruto) RGB[i] = RGB[i] * VQ[i];
            }
            for (int i = 0; i < W * H; i++) if (SA[i] > 0.5f) SIL[i] = 0;   // dentro da smoke não aparece silhueta
            // silhueta de quem está atrás de casa: pontilhado na cor do time, borda cheia (sempre dá pra ver todo mundo)
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (SIL[i] == 0) continue;
                    C3 col = SIL[i] == 2 ? CTCOL : TCOL;
                    bool border = SIL[i - 1] == 0 || SIL[i + 1] == 0 || SIL[i - W] == 0 || SIL[i + W] == 0;
                    if (border) RGB[i] = col;
                    else if ((x + y) % 2 == 0) RGB[i] = C3.Lerp(RGB[i], col, 0.6f);
                }
            // tiros: clarão na boca, traçante, faíscas no acerto
            foreach (var s in w.shots)
            {
                var (mx, my) = Pj(new V3(s.from.x, s.from.y, s.fz));
                var (tx, ty) = Pj(new V3(s.to.x, s.to.y, s.tz));
                float ux = tx - mx, uy = ty - my;
                Flash(mx, my, ux, uy);
                if (s.awp) Line(mx, my, tx, ty, new C3(255, 255, 230));
                else Line(mx + ux * 0.2f, my + uy * 0.2f, mx + ux * 0.75f, my + uy * 0.75f, new C3(255, 244, 190));
                if (s.hit)
                    for (int k = 0; k < (s.hs ? 6 : 3); k++)
                    {
                        float aa = k * 1.05f + w.t * 20, r = 2 + k % 3;
                        Put((int)(tx + MathF.Cos(aa) * r), (int)(ty + MathF.Sin(aa) * r * 0.8f), s.hs ? new C3(255, 230, 140) : new C3(230, 120, 90));
                    }
            }
            // mira de quem está em duelo
            foreach (var a in w.actors)
            {
                if (!a.alive || a.target < 0) continue;
                var tg = w.actors.Find(q => q.id == a.target);
                if (tg == null) continue;
                float dist = V2.Dist(tg.pos, a.pos);
                var (x, y) = Pj(new V3(a.pos.x + MathF.Cos(a.aim) * dist, a.pos.y + MathF.Sin(a.aim) * dist, tg.z + 19));
                C3 col = a.ct ? CTCOL : TCOL;
                int ix = (int)MathF.Round(x), iy = (int)MathF.Round(y);
                for (int d = 2; d <= 4; d++) { Put(ix + d, iy, col); Put(ix - d, iy, col); Put(ix, iy + d, col); Put(ix, iy - d, col); }
            }
            foreach (var a in w.actors)
                if (a.alive && a.weapon == "awp" && muz.TryGetValue(a.id, out var m0)) AwpLaser(a, m0, w.t);
            foreach (var a in w.actors) if (a.alive && a.blind) BlindIcon(a, w.t);
            if (!w.bruto) Hud(w);
            return RGB;
        }

        static float DeadTheta(ActorView a)
        {
            // cai de lado na tela (caindo na vertical parecia em pé)
            V2 ax = new V2(1, -1) / MathF.Sqrt(2);
            float sg = V2.Ang(a.aim).Dot(ax) >= 0 ? 1 : -1;
            return MathF.Atan2(sg * ax.y, sg * ax.x);
        }

        Pose PoseFor(ActorView a)
        {
            var p = Chibi.Aim();
            string gun = a.weapon switch { "rifle" => a.ct ? "m4" : "ak", "pistol" => a.ct ? "usp" : "glock", _ => a.weapon };
            var wd = Weapons.Get(gun);
            void Hold(bool lowered)
            {
                var (gA, f) = Weapons.Mount(wd, lowered);
                var (rh, lh) = Weapons.Hands(wd, gA, f);
                p.gA = gA; p.gB = gA + f * wd.len; p.rh = rh; p.lh = lh;
            }
            p.gun = gun; p.nade = a.nade;
            if (wd != null) Hold(false);
            if (gun == "nade") { p.can = true; p.rh = new V3(4.0f, -2.8f, 9.8f); p.lh = new V3(3.4f, 1.6f, 10.2f); }   // granada sacada, na frente do peito
            if (!a.alive) return p;
            if (a.speed > 4)
            {
                if (a.speed > 35 && a.target < 0 && wd != null && wd.kind != Weapons.Kind.Pistol) Hold(true);
                p.run = a.runPhase;
            }
            if (a.crouch > 0) p.crouch = MathF.Min(a.crouch, 1) * 0.8f;
            if (a.planting || a.defusing)
            {
                p.crouch = 1; p.run = null;
                p.gA = Chibi.SLUNG.gA; p.gB = Chibi.SLUNG.gB;
                p.rh = Chibi.PLANT_H.rh + new V3(0, 0, 0.7f * MathF.Abs(MathF.Sin(a.runPhase * 3)));
                p.lh = Chibi.PLANT_H.lh + new V3(0, 0, 0.7f * MathF.Abs(MathF.Sin(a.runPhase * 3 + 1.4f)));
            }
            if (a.throwT >= 0) Chibi.ThrowPose(a.throwT * 20 * (16f / 18f), p);
            p.rec = a.recoil;
            return p;
        }

        // ------------------------------------------------------------- composição
        void Merge(CharLayer L, short objId, byte silTeam = 0)
        {
            if (L == null) return;
            for (int j = 0; j < L.h; j++)
            {
                int y = L.y0 + j - oy;
                if (y < 0 || y >= H) continue;
                for (int i = 0; i < L.w; i++)
                {
                    int x = L.x0 + i - ox;
                    if (x < 0 || x >= W) continue;
                    int k = j * L.w + i, d = y * W + x;
                    float t = L.t[k];
                    if (float.IsInfinity(t)) continue;
                    if (t >= T[d])
                    {
                        // atrás de casa (não de outro boneco): vira silhueta
                        if (silTeam > 0 && OBJ[d] < 10) SIL[d] = silTeam;
                        continue;
                    }
                    T[d] = t; RGB[d] = L.rgb[k]; OBJ[d] = objId; PART[d] = L.part[k]; SIL[d] = 0;
                }
            }
        }

        // ------------------------------------------------------------- smoke e molotov
        readonly float[] SA = new float[W * H];   // opacidade da smoke no pixel
        static readonly C3 SMOKE = new C3(216, 215, 208), SMOKE_EDGE = new C3(132, 130, 124);

        /// <summary>Smoke: desenha as bolotas da nuvem como esferas (profundidade por pixel contra o cenário e entre
        /// si), luz do sol em 3 tons, contorno cinza na borda da nuvem e entre bolotas em profundidades diferentes.
        /// Furo de tiro deixa o pixel transparente; no fim as bolotas somem pontilhadas.</summary>
        void SmokePass(WorldView w)
        {
            Array.Clear(SA, 0, SA.Length);
            if (w.smokes.Count == 0) return;
            var SZ = new float[W * H]; var tone = new float[W * H]; var sid = new int[W * H];
            for (int i = 0; i < SZ.Length; i++) SZ[i] = float.PositiveInfinity;
            int pid = 0;
            foreach (var sm in w.smokes)
                foreach (var pf in sm.puffs)
                {
                    pid++;
                    float r = sm.PuffR(pf, w.t);
                    if (r < 0.6f) continue;
                    float fade = r / pf.r;                              // some pontilhando no fim
                    V3 c = sm.PuffC(pf, w.t);
                    var (sx, sy) = Pj(c); float dc = Depth(c), rs = r * S;
                    for (int y = Math.Max(0, (int)(sy - rs - 1)); y < Math.Min(H, (int)(sy + rs + 2)); y++)
                        for (int x = Math.Max(0, (int)(sx - rs - 1)); x < Math.Min(W, (int)(sx + rs + 2)); x++)
                        {
                            float a = (x + 0.5f - sx) / S, b = -(y + 0.5f - sy) / S;
                            float q = a * a + b * b;
                            if (q > r * r) continue;
                            int idx = y * W + x;
                            if (sm.age > SmokeVolume.LIFE - SmokeVolume.FADE && fade < 0.999f && BAY4[y % 4, x % 4] + 0.5f > fade * 1.15f) continue;
                            float h = MathF.Sqrt(r * r - q), dep = dc - h;
                            if (dep >= T[idx] - 0.5f || dep >= SZ[idx]) continue;
                            V3 P = c + RV * a + UV * b - DV * h;
                            if (sm.InTunnel(P)) continue;
                            V3 n = (P - c) / r;
                            // volume: luz do sol na bolota + oclusão pela altura (a base do domo fica na sombra das de cima)
                            float ao = Math.Clamp((P.z - sm.origin.z) / 40f, 0, 1);
                            float l = n.Dot(SUN) * 0.75f + (ao - 0.45f) * 0.9f;
                            SZ[idx] = dep; sid[idx] = pid;
                            tone[idx] = l > 0.3f ? 1f : l > -0.12f ? 0.88f : l > -0.45f ? 0.77f : 0.68f;
                        }
                }
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int idx = j * W + i;
                    if (float.IsInfinity(SZ[idx])) continue;
                    RGB[idx] = SMOKE * tone[idx]; SA[idx] = 1; OBJ[idx] = 2;
                }
            for (int j = 1; j < H - 1; j++)
                for (int i = 1; i < W - 1; i++)
                {
                    int idx = j * W + i;
                    if (float.IsInfinity(SZ[idx])) continue;
                    bool rim = false, inner = false;
                    foreach (int d in new[] { 1, -1, W, -W })
                    {
                        int q = idx + d;
                        if (float.IsInfinity(SZ[q])) rim = true;
                        else if (sid[q] != sid[idx] && SZ[q] < SZ[idx] - 5f) inner = true;   // bolota da frente cobre essa
                    }
                    if (rim) RGB[idx] = SMOKE_EDGE;
                    else if (inner) RGB[idx] = RGB[idx] * 0.86f;   // linha suave entre bolotas (a da frente se destaca)
                }
        }

        static readonly C3 FIRE_RIM = new C3(130, 34, 14), FIRE_OUT = new C3(242, 88, 26), FIRE_MID = new C3(255, 156, 42), FIRE_IN = new C3(255, 226, 106);

        /// <summary>Molotov: mancha queimada no chão e chamas em gota (3 cores + borda escura) em células alternadas,
        /// tremendo, do fundo pra frente, com teste de profundidade contra o cenário.</summary>
        void FirePass(WorldView w)
        {
            foreach (var f in w.fires)
            {
                int vis = f.Visible; float str = f.Strength;
                if (vis == 0 || str <= 0) continue;
                // mancha queimada (escurece o piso, pontilhado nas bordas)
                foreach (var c in f.cells)
                {
                    if (c.order >= vis) continue;
                    V3 b = new V3(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z);
                    var (px, py) = Pj(b); float fd = Depth(b);
                    Ellipse(px, py, 5.5f, 3.8f, fd, (i, e, x, y) => { if (e < 0.85f || (x + y) % 2 == 0) RGB[i] = C3.Lerp(RGB[i], new C3(58, 40, 30), 0.5f * str); });
                }
                // chamas: uma a cada 3 células (grandes, se sobrepondo), mais altas no meio do fogo
                var list = new List<(float depth, V3 b, float h, float ph)>();
                float rmax = 1;
                foreach (var c in f.cells) rmax = MathF.Max(rmax, MathF.Sqrt(MathF.Pow(c.x * FireArea.C - f.origin.x, 2) + MathF.Pow(c.y * FireArea.C - f.origin.y, 2)));
                foreach (var c in f.cells)
                {
                    if (c.order >= vis || c.phase > 2.09f) continue;   // 1 em 3 células, sorteado (a fileira em diagonal denunciava a grade)
                    float grow = Math.Clamp((f.age - c.order / (float)f.cells.Count * FireArea.SPREAD) / 0.25f, 0, 1);
                    float dr = MathF.Sqrt(MathF.Pow(c.x * FireArea.C - f.origin.x, 2) + MathF.Pow(c.y * FireArea.C - f.origin.y, 2)) / rmax;
                    float h = (10f + 6f * (MathF.Sin(c.phase * 3.1f) * 0.5f + 0.5f)) * (1.15f - 0.45f * dr)
                              * (0.72f + 0.28f * MathF.Sin(w.t * 9f + c.phase)) * str * grow;
                    V3 b = new V3(c.x * FireArea.C + 2 + MathF.Sin(c.phase) * 3f, c.y * FireArea.C + 2 + MathF.Cos(c.phase * 1.7f) * 3f, c.z);   // chama solta (não em fileira)
                    list.Add((Depth(b), b, h, c.phase));
                }
                list.Sort((a, b) => b.depth.CompareTo(a.depth));
                foreach (var (fd, b, h, ph) in list)
                {
                    if (h < 1) continue;
                    var (px, py) = Pj(b);
                    float hp = h * S * MathF.Cos(EL), wp = 4.8f;
                    int y0 = (int)MathF.Round(py), y1 = (int)MathF.Round(py - hp);
                    for (int y = y0; y >= y1; y--)
                    {
                        if (y < 0 || y >= H) continue;
                        float fr = (y0 - y) / MathF.Max(hp, 1);
                        float half = wp * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(1, fr * 1.3f + 0.08f)), 0.8f) * (1 - fr * 0.45f);
                        float cx = px + MathF.Sin(fr * 3f + w.t * 8f + ph) * fr * 1.4f;
                        for (int x = (int)MathF.Floor(cx - half); x <= (int)MathF.Ceiling(cx + half); x++)
                        {
                            if (x < 0 || x >= W) continue;
                            int idx = y * W + x;
                            if (T[idx] < fd - 3f) continue;           // atrás de parede/boneco
                            float q = MathF.Abs(x + 0.5f - cx) / MathF.Max(half, 0.5f);
                            C3 col = q > 0.82f ? FIRE_RIM : (q < 0.42f && fr < 0.62f ? FIRE_IN : (q < 0.66f ? FIRE_MID : FIRE_OUT));
                            if (fr > 0.85f) col = q > 0.6f ? FIRE_RIM : FIRE_OUT;
                            RGB[idx] = col; OBJ[idx] = 3;
                        }
                    }
                }
            }
        }

        /// <summary>Laser da AWP saindo da boca do cano e parando na parede/caixa. Na luneta: reto, parado, comprido
        /// (até 420), vermelho forte com ponto na ponta. Sem luneta: balança (a mão não está firme), curto (160),
        /// tracejado e fraco.</summary>
        void AwpLaser(ActorView a, V3 muzzle, float t)
        {
            float ang = a.aim;
            if (!a.scoped) ang += 0.07f * MathF.Sin(t * 2.7f + a.id) + 0.035f * MathF.Sin(t * 6.1f + a.id * 2);
            float maxL = a.scoped ? 420f : 160f, z = muzzle.z;
            V2 d = V2.Ang(ang);
            float L = 0;
            for (; L < maxL; L += 2f)
            {
                int x = (int)(muzzle.x + d.x * L), y = (int)(muzzle.y + d.y * L);
                if (!Map.MapData.InMap(x, y) || Map.MapData.Top(x, y) > z) break;
            }
            var (x0, y0) = Pj(muzzle);
            var (x1, y1) = Pj(new V3(muzzle.x + d.x * L, muzzle.y + d.y * L, z));
            C3 strong = new C3(255, 46, 40), weak = new C3(230, 80, 70);
            int n = (int)MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                int px = (int)MathF.Round(x0 + (x1 - x0) * u), py = (int)MathF.Round(y0 + (y1 - y0) * u);
                if (px < 0 || py < 0 || px >= W || py >= H) continue;
                if (a.scoped) RGB[py * W + px] = strong;
                else if ((i / 4) % 2 == 0) RGB[py * W + px] = C3.Lerp(RGB[py * W + px], weak, 0.95f - u * 0.5f);   // tracejado que some na ponta
            }
            if (a.scoped && L < maxL)   // ponto onde bate
            {
                int px = (int)MathF.Round(x1), py = (int)MathF.Round(y1);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (Math.Abs(dx) + Math.Abs(dy) < 2) Put(px + dx, py + dy, new C3(255, 210, 200));
            }
        }

        /// <summary>Desfoque 3x3 no lugar (campo da smoke).</summary>
        static void Blur(float[] a)
        {
            var b = (float[])a.Clone();
            for (int j = 1; j < H - 1; j++)
                for (int i = 1; i < W - 1; i++)
                {
                    int k = j * W + i;
                    a[k] = (b[k] * 4 + b[k - 1] + b[k + 1] + b[k - W] + b[k + W]) / 8f;
                }
        }

        /// <summary>Ícone de cego (flash) em cima da cabeça: olho branco com pupila, riscado de vermelho, contorno
        /// escuro; balança de leve.</summary>
        void BlindIcon(ActorView a, float t)
        {
            var (hx, hy) = Pj(new V3(a.pos.x, a.pos.y, a.z + 34));
            int cx = (int)MathF.Round(hx), cy = (int)MathF.Round(hy + MathF.Sin(t * 6f) * 1.2f);
            var kind = new int[15, 11];   // 0 vazio, 1 branco, 2 pupila, 3 risco
            for (int y = -5; y <= 5; y++)
                for (int x = -7; x <= 7; x++)
                {
                    float lens = 3.2f * MathF.Sqrt(MathF.Max(0, 1 - (x / 6.6f) * (x / 6.6f)));
                    int v = 0;
                    if (MathF.Abs(y) <= lens) v = (x * x + y * y <= 4) ? 2 : 1;
                    if (MathF.Abs(x * 0.62f + y) <= 0.7f && Math.Abs(x) <= 6) v = 3;   // risco na diagonal
                    kind[x + 7, y + 5] = v;
                }
            C3 W1 = new C3(250, 250, 246), K = new C3(30, 26, 30), RED = new C3(226, 52, 40);
            for (int y = -5; y <= 5; y++)
                for (int x = -7; x <= 7; x++)
                {
                    int v = kind[x + 7, y + 5];
                    if (v == 0)
                    {   // contorno: vizinho cheio
                        bool edge = false;
                        for (int dy = -1; dy <= 1 && !edge; dy++)
                            for (int dx = -1; dx <= 1 && !edge; dx++)
                            {
                                int u = x + dx + 7, w_ = y + dy + 5;
                                if (u >= 0 && w_ >= 0 && u < 15 && w_ < 11 && kind[u, w_] != 0) edge = true;
                            }
                        if (edge) Put(cx + x, cy + y, K);
                        continue;
                    }
                    Put(cx + x, cy + y, v == 1 ? W1 : v == 2 ? K : RED);
                }
        }

        readonly bool[] outside = new bool[W * H];
        bool plain;

        void Outline()
        {
            var inner = new bool[W * H];
            Array.Clear(outside, 0, outside.Length);
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    foreach (int d in new[] { W, -W, 1, -1 })
                    {
                        int q = i + d;
                        bool near = T[q] < T[i] - 2.5f;
                        if (near && OBJ[q] != OBJ[i] && OBJ[q] > 0 && !(OBJ[q] == 1 && OBJ[i] > 0 && OBJ[i] < 10)) outside[i] = true;
                        if (OBJ[q] == OBJ[i] && OBJ[i] >= 10 && PART[q] != PART[i] && T[q] < T[i] - 0.9f) inner[i] = true;
                    }
                }
            for (int i = 0; i < W * H; i++)
            {
                if (outside[i]) RGB[i] = OUTC;
                else if (inner[i]) RGB[i] = RGB[i] * 0.55f;
            }
        }

        void Ring(V3 foot, C3 col)
        {
            var (fx, fy) = Pj(foot);
            float fd = Depth(foot);
            Ellipse(fx, fy, 9, 7, fd, (i, e, x, y) => { if (e < 1f && e > 0.62f) RGB[i] = RGB[i] * 0.45f + col * 0.55f; });
        }

        void Shadow(V3 foot, float rx, float ry, float k)
        {
            var (fx, fy) = Pj(foot);
            float fd = Depth(foot);
            Ellipse(fx, fy, rx, ry, fd, (i, e, x, y) =>
            {
                if (e < 0.55f || (e < 1f && (x + y) % 2 == 0)) RGB[i] = RGB[i] * k;
            });
        }

        void Ellipse(float fx, float fy, float rx, float ry, float fd, Action<int, float, int, int> f)
        {
            for (int y = Math.Max(0, (int)(fy - ry - 1)); y < Math.Min(H, (int)(fy + ry + 2)); y++)
                for (int x = Math.Max(0, (int)(fx - rx - 1)); x < Math.Min(W, (int)(fx + rx + 2)); x++)
                {
                    int i = y * W + x;
                    if (!FL[i] || (!plain && MathF.Abs(T[i] - fd) > 6f)) continue;
                    float ex = (x + 0.5f - fx) / rx, ey = (y + 0.5f - fy) / ry;
                    f(i, ex * ex + ey * ey, x + ox, y + oy);
                }
        }

        void Flash(float mx, float my, float fx, float fy)
        {
            float ln = MathF.Max(MathF.Sqrt(fx * fx + fy * fy), 1e-3f);
            float cx = mx + fx / ln * 2.5f, cy = my + fy / ln * 2.5f;
            Disk(cx, cy, 4, 3.5f, new C3(255, 150, 50)); Disk(cx, cy, 2.6f, 2.2f, new C3(255, 214, 90)); Disk(cx, cy, 1.4f, 1.2f, new C3(255, 248, 205));
            foreach (float ang in new[] { 0f, 90f, 45f, 135f })
            {
                float a = ang * MathF.PI / 180;
                Line(cx - MathF.Cos(a) * 6, cy - MathF.Sin(a) * 5, cx + MathF.Cos(a) * 6, cy + MathF.Sin(a) * 5, new C3(255, 190, 70));
            }
        }

        void Disk(float cx, float cy, float rx, float ry, C3 c)
        {
            for (int y = (int)(cy - ry); y <= (int)(cy + ry); y++)
                for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                {
                    float ex = (x + 0.5f - cx) / rx, ey = (y + 0.5f - cy) / ry;
                    if (ex * ex + ey * ey <= 1) Put(x, y, c);
                }
        }

        void Line(float x0, float y0, float x1, float y1, C3 c)
        {
            int n = (int)MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= n; i++) Put((int)MathF.Round(x0 + (x1 - x0) * i / n), (int)MathF.Round(y0 + (y1 - y0) * i / n), c);
        }

        void Rect(int x0, int y0, int x1, int y1, C3 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Put(x, y, c); }

        void Put(int x, int y, C3 c) { if (x >= 0 && y >= 0 && x < W && y < H) RGB[y * W + x] = c; }

        // ------------------------------------------------------------- HUD de transmissão
        void Hud(WorldView w)
        {
            string txt = $"TR {w.scoreT}   {w.clock}   {w.scoreCT} CT";
            int tw = PixelFont.Width(txt), x0 = W / 2 - tw / 2;
            Rect(x0 - 6, 1, x0 + tw + 6, 11, new C3(22, 16, 16));
            Rect(x0 - 6, 1, x0 + 12, 11, new C3(120, 70, 20));
            Rect(x0 + tw - 12, 1, x0 + tw + 6, 11, new C3(30, 70, 120));
            PixelFont.Draw(RGB, W, H, x0, 3, txt, new C3(250, 246, 236));
            // vivos (barrinha de vida)
            foreach (var a in w.actors)
            {
                int k = a.ct ? a.id - 5 : a.id;
                int xx = a.ct ? W - 4 - 5 * 7 + k * 7 : 4 + k * 7;
                if (a.alive)
                {
                    Rect(xx, 3, xx + 5, 13, new C3(40, 30, 30));
                    Rect(xx, 3, xx + 5, 13, a.ct ? CTCOL : TCOL);
                }
                else { Rect(xx, 3, xx + 5, 13, new C3(60, 50, 50)); Line(xx, 3, xx + 5, 13, new C3(150, 60, 50)); }
            }
            // kill feed
            int y = 16;
            foreach (var f in w.feed)
            {
                int fw = PixelFont.Width(f.text), xx = W - fw - 6;
                float pop = MathF.Max(0, 1 - f.age / 0.4f);
                Rect(xx - 3, y, W - 3, y + 10, new C3(30 + 90 * pop, 22, 22));
                Line(xx - 3, y, W - 3, y, f.ctKiller ? new C3(60, 120, 200) : new C3(200, 120, 40));
                PixelFont.Draw(RGB, W, H, xx, y + 2, f.text, new C3(255, 236, 210));
                y += 12;
            }
            // comunicação (calls)
            y = H - 14 - 11 * w.calls.Count;
            foreach (var c in w.calls)
            {
                int cw = PixelFont.Width(c.text);
                Rect(2, y, 8 + cw, y + 10, new C3(20, 16, 16));
                PixelFont.Draw(RGB, W, H, 5, y + 2, c.text, c.ct ? CTCOL : TCOL);
                y += 11;
            }
            if (!string.IsNullOrEmpty(w.title)) PixelFont.Draw(RGB, W, H, 4, H - 10, w.title, new C3(200, 190, 175));
        }
    }
}
