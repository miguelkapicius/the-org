using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Gravador de clipes de duelo (Headless -duelo N -clipes K): guarda o round passo a passo e, pra cada duelo escolhido
    /// (as CENAS do medidor + uma amostra dos duelos comuns), escreve o trecho visto de cima — posição, mira, tiros, vida,
    /// cobertura mais perto, fase e pensamento de cada um. Sai em dois formatos: uma página (Logs/duelos/&lt;saida&gt;.html, pra
    /// assistir) e um texto (Logs/duelos/&lt;saida&gt;.txt, pra ler como quem revisa demo). Número não mostra se a jogada foi
    /// burra; isto mostra. Só lê o estado (oráculo: é o juiz).
    /// </summary>
    sealed class Gravador
    {
        struct Q { public float x, y, aim, spd; public short hp; public byte st; public sbyte alvo; public string fase, pensa; }
        readonly Match m; readonly int seed;
        readonly List<float> ts = new List<float>(); readonly List<Q[]> qs = new List<Q[]>(); readonly List<UInt128> vis = new List<UInt128>();   // vis: bit a*10+b (a<b) = linha livre entre os dois, contando smoke
        readonly List<(float t, float x, float y, float r)> smokes = new List<(float, float, float, float)>();
        readonly float[] tiroAnt = new float[10];
        public readonly List<string> json = new List<string>(); public readonly List<string> texto = new List<string>();
        public readonly List<(string tag, int chave)> meta = new List<(string, int)>();
        public Gravador(Match m, int seed) { this.m = m; this.seed = seed; for (int i = 0; i < 10; i++) tiroAnt[i] = m.agents[i].lastShotT; }

        public void Quadro()
        {
            var q = new Q[10];
            for (int i = 0; i < 10; i++)
            {
                var a = m.agents[i]; var b = a.brain;
                q[i] = new Q
                {
                    x = a.pos.x, y = a.pos.y, aim = a.aim, spd = a.Speed, hp = (short)Math.Max(0, a.hp),
                    st = (byte)((a.alive ? 1 : 0) | (a.duck > 0.5f ? 2 : 0) | (a.lastShotT != tiroAnt[i] ? 4 : 0) | (a.Blind(m.t) ? 8 : 0) | (a.scoped ? 16 : 0) | (a.gun != null && a.gun.Reloading ? 32 : 0)),
                    alvo = (sbyte)(b.AlvoAgora != null ? b.AlvoAgora.id : -1), fase = b.fase, pensa = b.thought,
                };
                tiroAnt[i] = a.lastShotT;
            }
            UInt128 v = 0;
            for (int i = 0; i < 10; i++) for (int j = i + 1; j < 10; j++)
            {
                var A = m.agents[i]; var B = m.agents[j];
                if (A.team == B.team || !A.alive || !B.alive || (A.pos - B.pos).Len > 1000) continue;
                if (MapData.LosWalk(A.pos, A.z + A.Eye - 18f, B.pos, B.z + B.Eye - 18f, 18f) && !m.SmokeBlocks(A.pos, A.z + A.Eye, B.pos, B.z + B.Eye)) v |= (UInt128)1 << (i * 10 + j);
            }
            ts.Add(m.t); qs.Add(q); vis.Add(v);
            foreach (var s in m.smokes) { var (mn, mx) = s.Bounds; smokes.Add((m.t, s.origin.x, s.origin.y, MathF.Max(mx.x - mn.x, mx.y - mn.y) * 0.5f)); }
        }

        /// <summary>Distância (reta) até o ponto andável mais perto onde o outro não me vê; 999 = nada até 90.</summary>
        static float Cobertura(V2 p, V2 dele, float zDele)
        {
            float best = 999;
            foreach (float r in new[] { 10f, 20f, 35f, 55f, 90f })
            {
                if (r >= best) break;
                for (int k = 0; k < 16; k++)
                {
                    V2 q = p + V2.Ang(k * 0.3927f) * r;
                    if (!NavGrid.Movable(q) || !NavGrid.Direct(p, q)) continue;
                    if (!MapData.LosWalk(dele, zDele, q, NavGrid.ZAt(q), 18f)) { best = r; break; }
                }
            }
            return best;
        }

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string F2(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Js(string s) => s == null ? "\"\"" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";

        public void Clipe(int a, int b, float ini, float ult, string fim, string tag, float dist0)
        {
            float t0 = ini - 2.5f, t1 = ult + 1.5f;
            int i0 = ts.FindIndex(x => x >= t0); if (i0 < 0) i0 = 0;
            int i1 = ts.FindLastIndex(x => x <= t1); if (i1 <= i0) return;
            var A = m.agents[a]; var B = m.agents[b];
            // quem aparece: os dois e quem passa a menos de 600 de um deles no trecho
            var quem = new List<int> { a, b };
            for (int i = 0; i < 10; i++)
            {
                if (i == a || i == b) continue;
                for (int f = i0; f <= i1; f += 8)
                {
                    var q = qs[f]; if ((q[i].st & 1) == 0) continue;
                    float da = MathF.Sqrt((q[i].x - q[a].x) * (q[i].x - q[a].x) + (q[i].y - q[a].y) * (q[i].y - q[a].y)), db = MathF.Sqrt((q[i].x - q[b].x) * (q[i].x - q[b].x) + (q[i].y - q[b].y) * (q[i].y - q[b].y));
                    if (da < 600 || db < 600) { quem.Add(i); break; }
                }
            }
            // recorte do mapa
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int f = i0; f <= i1; f += 4) foreach (int i in new[] { a, b }) { var q = qs[f][i]; x0 = MathF.Min(x0, q.x); y0 = MathF.Min(y0, q.y); x1 = MathF.Max(x1, q.x); y1 = MathF.Max(y1, q.y); }
            const int CEL = 2; float mg = 140;
            int gx0 = Math.Max(0, (int)(x0 - mg)), gy0 = Math.Max(0, (int)(y0 - mg)), gx1 = Math.Min(MapData.NX - 1, (int)(x1 + mg)), gy1 = Math.Min(MapData.NY - 1, (int)(y1 + mg));
            var sb = new StringBuilder();
            sb.Append($"{{\"seed\":{seed},\"t0\":{F(ts[i0])},\"ini\":{F(ini)},\"ult\":{F(ult)},\"a\":{a},\"b\":{b},\"fim\":{Js(fim)},\"tag\":{Js(tag)},\"dist\":{F(dist0)},");
            sb.Append($"\"onde\":{Js(Places.Region(A.pos) + " x " + Places.Region(B.pos))},");
            sb.Append($"\"mapa\":{{\"x0\":{gx0},\"y0\":{gy0},\"cel\":{CEL},\"linhas\":[");
            for (int y = gy0; y <= gy1; y += CEL)
            {
                if (y > gy0) sb.Append(',');
                sb.Append('"');
                for (int x = gx0; x <= gx1; x += CEL) sb.Append(!MapData.walk[x, y] ? '2' : MapData.coverH[x, y] > 14 ? '3' : MapData.coverH[x, y] > 0 ? '1' : '0');   // 2 parede · 3 caixa alta · 1 cobertura baixa · 0 livre
                sb.Append('"');
            }
            sb.Append("]},\"jog\":[");
            sb.Append(string.Join(",", quem.Select(i => $"{{\"id\":{i},\"n\":{Js(m.agents[i].name)},\"tr\":{(m.agents[i].IsT ? 1 : 0)},\"arma\":{Js(m.agents[i].gunId)}}}")));
            sb.Append("],\"f\":[");
            var tx = new StringBuilder(); var txt = new StringBuilder();
            string[] faseAnt = new string[10], pensaAnt = new string[10];
            txt.AppendLine($"=== {tag} · semente {seed} · {F(ini)}–{F(ult)} s ({F(ult - ini)} s) · {A.name} ({(A.IsT ? "TR" : "CT")}, {A.gunId}) x {B.name} ({(B.IsT ? "TR" : "CT")}, {B.gunId}) · {Places.Region(A.pos)} x {Places.Region(B.pos)} · {F(dist0 * 2.9f / 40)} m · fim: {fim}");
            int[] tirosJan = new int[10]; float proxLinha = ts[i0]; bool prim = true;
            for (int f = i0; f <= i1; f++)
            {
                var q = qs[f]; float t = ts[f];
                foreach (int i in quem) if ((q[i].st & 4) != 0) tirosJan[i]++;
                foreach (int i in quem)
                {
                    if (q[i].fase != faseAnt[i] || q[i].pensa != pensaAnt[i])
                    { if (tx.Length > 0) tx.Append(','); tx.Append($"[{F2(t)},{i},{Js(q[i].fase)},{Js(q[i].pensa)}]"); faseAnt[i] = q[i].fase; pensaAnt[i] = q[i].pensa; }
                }
                bool vendo = (vis[f] & ((UInt128)1 << (Math.Min(a, b) * 10 + Math.Max(a, b)))) != 0;
                if (f % 2 == 0)
                {
                    if (!prim) sb.Append(','); prim = false;
                    sb.Append($"[{F2(t)},[");
                    bool p2 = true;
                    foreach (int i in quem)
                    {
                        float cob = -1;
                        if (i == a || i == b) { int o = i == a ? b : a; V2 po = new V2(q[o].x, q[o].y); cob = (q[i].st & 1) != 0 && (q[o].st & 1) != 0 ? Cobertura(new V2(q[i].x, q[i].y), po, NavGrid.ZAt(po)) : -1; }
                        if (!p2) sb.Append(','); p2 = false;
                        sb.Append($"[{F(q[i].x)},{F(q[i].y)},{F2(q[i].aim)},{q[i].hp},{q[i].st},{F(q[i].spd)},{q[i].alvo},{F(cob)}]");
                    }
                    sb.Append($"],{(vendo ? 1 : 0)},[");
                    sb.Append(string.Join(",", smokes.Where(s => s.t == t).Select(s => $"[{F(s.x)},{F(s.y)},{F(s.r)}]")));
                    sb.Append("]]");
                }
                if (t >= proxLinha)
                {   // texto: uma linha a cada 0,25 s por duelista
                    proxLinha += 0.25f;
                    foreach (int i in new[] { a, b })
                    {
                        int o = i == a ? b : a; var me = q[i]; var el = q[o];
                        if ((me.st & 1) == 0) { txt.AppendLine($"  {t,6:0.00} {m.agents[i].name,-8} MORTO"); continue; }
                        V2 pm = new V2(me.x, me.y), pe = new V2(el.x, el.y);
                        float cob = (el.st & 1) != 0 ? Cobertura(pm, pe, NavGrid.ZAt(pe)) : -1;
                        float erro = MathF.Abs(MathX.AngDiff((pe - pm).Atan2, me.aim)) * 180 / MathF.PI;
                        string mov = me.spd < 8 ? "parado " : me.spd < 40 ? "devagar" : "correndo";
                        txt.AppendLine($"  {t,6:0.00} {m.agents[i].name,-8} hp {me.hp,3} {mov}{((me.st & 2) != 0 ? " agach" : "      ")} {(vendo ? "LINHA" : "  -  ")} mira a {erro,3:0}° dele · cobertura a {(cob < 0 ? "  -" : cob > 900 ? ">90" : F(cob).PadLeft(3))} · tiros {tirosJan[i]} · alvo {(me.alvo < 0 ? "-" : m.agents[me.alvo].name)} · {me.fase} · {me.pensa}");
                        tirosJan[i] = 0;
                    }
                }
            }
            sb.Append("],\"txt\":[").Append(tx).Append("]}");
            json.Add(sb.ToString()); texto.Add(txt.ToString()); meta.Add((tag, seed * 100 + a * 10 + b));
        }
    }
}
