using System;
using System.Collections.Generic;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// O que um time sabe dos inimigos.
    ///  • Um rastro por inimigo (onde ELE pode estar): grade de 12 unidades; começa no spawn dele e "escorre" pela
    ///    navmesh na velocidade de quem anda. Tudo que algum aliado está vendo zera — é o "limpar pixel". Visto ou
    ///    ouvido, o rastro dele colapsa naquele ponto. O calor junta os rastros pesando pela certeza de cada um (área
    ///    pequena = sei onde está), então 5 vistos entrando no A esfriam o resto do mapa; Esperados() conta quantos
    ///    devem estar num lugar.
    ///  • Contatos: último lugar onde cada inimigo foi visto, e por quem; passos e tiros ouvidos.
    ///  • Quantos inimigos vivos (a gente sabe pelo placar).
    /// </summary>
    public class Intel
    {
        public const int C = 12;
        public static readonly int GX = MapData.NX / C + 1, GY = MapData.NY / C + 1;
        static bool[] pass; static float[] gz; static int[][] nbr; static int[] passIdx;
        public readonly float[] heat = new float[GX * GY];
        public readonly float[] seenAt = new float[GX * GY];
        public readonly Team team;
        public class Contact { public int id; public V2 pos; public float z, t; public bool seen; public string by; public int hp = 100; public float hpT = -9; }
        public readonly Dictionary<int, Contact> contacts = new Dictionary<int, Contact>();
        /// <summary>Onde e quando o portador do bomb foi visto por último.</summary>
        public V2 bombaPos; public float bombaT = -99;
        public readonly List<(float t, V2 p, string kind)> heard = new List<(float, V2, string)>();

        public static void Build()
        {
            if (pass != null) return;
            pass = new bool[GX * GY]; gz = new float[GX * GY]; nbr = new int[GX * GY][];
            for (int x = 0; x < GX; x++)
                for (int y = 0; y < GY; y++)
                {
                    var c = Center(x, y);
                    var (nx, ny) = NavGrid.CellOf(c);
                    // passável se alguma célula da navmesh perto do centro anda
                    bool ok = false; float zz = 0;
                    for (int dx = -2; dx <= 2 && !ok; dx++)
                        for (int dy = -2; dy <= 2 && !ok; dy++)
                        {
                            int a = nx + dx, b = ny + dy;
                            if (a >= 0 && b >= 0 && a < NavGrid.NGX && b < NavGrid.NGY && NavGrid.move[a, b]) { ok = true; zz = NavGrid.z[a, b]; }
                        }
                    pass[x * GY + y] = ok; gz[x * GY + y] = zz;
                }
            for (int x = 0; x < GX; x++)
                for (int y = 0; y < GY; y++)
                {
                    int i = x * GY + y; if (!pass[i]) { nbr[i] = Array.Empty<int>(); continue; }
                    var l = new List<int>();
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int a = x + dx, b = y + dy; if (a < 0 || b < 0 || a >= GX || b >= GY) continue;
                            int j = a * GY + b;
                            if (pass[j] && MathF.Abs(gz[j] - gz[i]) <= 34) l.Add(j);
                        }
                    nbr[i] = l.ToArray();
                }
            var pl = new List<int>(); for (int i = 0; i < pass.Length; i++) if (pass[i]) pl.Add(i);
            passIdx = pl.ToArray();
            Tops();
        }

        public static V2 Center(int x, int y) => new V2((x + 0.5f) * C, (y + 0.5f) * C);
        public static int Idx(V2 p) => Math.Clamp((int)(p.x / C), 0, GX - 1) * GY + Math.Clamp((int)(p.y / C), 0, GY - 1);
        public static bool Pass(int i) => pass[i];
        public static float Z(int i) => gz[i];
        public static V2 CenterOf(int i) => Center(i / GY, i % GY);

        /// <summary>Onde um inimigo pode estar: 0..1 por célula (1 = cabe com a última info; cai devagar com a distância).</summary>
        public class Rastro { public int id; public float[] poss = new float[GX * GY]; public float infoT = -1000, area, conf; public bool vivo = true; }
        public readonly List<Rastro> inimigos = new List<Rastro>();
        /// <summary>Área possível (em células ponderadas) do tamanho de um bomb: com isso ele "está ali" (confiança 1).</summary>
        public const float A0 = 70;

        public Intel(Team team, IEnumerable<Agent> enemies)
        {
            Build(); this.team = team;
            for (int i = 0; i < seenAt.Length; i++) seenAt[i] = -999;   // nada visto ainda
            foreach (var e in enemies) { var r = new Rastro { id = e.id }; MarkR(r, e.pos, 2, 1f); inimigos.Add(r); }
            Recalc();
        }

        public float HeatAt(V2 p) => heat[Idx(p)];

        /// <summary>
        /// O que um jogador enxerga agora, em leque: raios a cada ~1,2° até o alcance; cada raio guarda o "horizonte"
        /// (a maior inclinação de obstáculo até ali) e um ponto é visível se a linha olho→olho passa por cima de tudo
        /// antes dele — o mesmo teste do LosWalk, mas lendo cada pixel do mapa uma vez só por raio (antes eram ~1.400
        /// linhas de visão por jogador 4x por segundo, o que deixava o round em ~4 s). Smoke densa corta o raio.
        /// </summary>
        public static void Viewshed(V2 pos, float eyeZ, float aim, float halfFov, float range, Func<V2, float, bool> smoke, Action<int> visible)
        {
            var tops = Tops(); const int NY = MapData.NY;
            int rays = (int)(2 * halfFov / (1.6f * MathF.PI / 180)) + 2;   // ~1 célula de 12 entre raios a 450 de distância
            for (int r = 0; r < rays; r++)
            {
                float ang = aim - halfFov + 2 * halfFov * r / (rays - 1);
                float dx = MathF.Cos(ang), dy = MathF.Sin(ang);
                float horizon = float.NegativeInfinity; int last = -1;
                for (int s = 2; s < range; s += 2)   // de 2 em 2, conferindo parede no pixel do meio (parede fina não vaza)
                {
                    int xm = (int)(pos.x + dx * (s - 1)), ym = (int)(pos.y + dy * (s - 1));
                    if ((uint)xm >= MapData.NX || (uint)ym >= NY || tops[xm * NY + ym] > 9000) break;
                    float px = pos.x + dx * s, py = pos.y + dy * s;
                    int x = (int)px, y = (int)py;
                    if ((uint)x >= MapData.NX || (uint)y >= NY) break;
                    float top = tops[x * NY + y]; if (top > 9000) break;
                    int i = Math.Min(x / C, GX - 1) * GY + Math.Min(y / C, GY - 1);
                    if (smoke != null && s % 6 == 0 && smoke(new V2(px, py), (eyeZ + gz[i] + 18) * 0.5f)) break;   // s par: a cada 6
                    if (i != last && pass[i] && (gz[i] + 18 - eyeZ) / s >= horizon) { visible(i); last = i; }
                    float sl = (top - eyeZ) / s; if (sl > horizon) horizon = sl;
                }
            }
        }

        static float[] tops;
        /// <summary>Altura que tapa a visão em cada pixel (o mesmo critério do LosWalk numa tabela só; parede = 9999).</summary>
        static float[] Tops()
        {
            if (tops != null) return tops;
            var t = new float[MapData.NX * MapData.NY];
            for (int x = 0; x < MapData.NX; x++)
                for (int y = 0; y < MapData.NY; y++)
                {
                    if (!MapData.walk[x, y]) { t[x * MapData.NY + y] = 9999; continue; }
                    int f = MapData.floor[x, y]; float top = f + MapData.coverH[x, y];
                    if (f > MapData.GZ + 1 && !MapData.stair[x, y] && MapData.fmat[x, y] != MapData.Floor.Cart) top = MathF.Max(top, f + 8);
                    t[x * MapData.NY + y] = top;
                }
            return tops = t;
        }

        static void MarkR(Rastro rr, V2 p, int r, float v)
        {
            int cx = Math.Clamp((int)(p.x / C), 0, GX - 1), cy = Math.Clamp((int)(p.y / C), 0, GY - 1);
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= GX || y >= GY) continue;
                    int i = x * GY + y;
                    float hv = v * (1 - 0.15f * MathF.Max(Math.Abs(x - cx), Math.Abs(y - cy)));
                    if (pass[i] && hv > rr.poss[i]) rr.poss[i] = hv;
                }
        }

        /// <summary>Info nova de um inimigo: tudo que se achava dele some, ele está ali (raio = incerteza).</summary>
        void Colapsa(Rastro r, V2 p, int raio, float v, float t)
        {
            Array.Clear(r.poss, 0, r.poss.Length); MarkR(r, p, raio, v); r.infoT = t;
        }

        /// <summary>
        /// Cada rastro escorre uma célula por passo (~0,25 s): o inimigo pode ter andado até ali; perde um pouco a cada
        /// célula (longe da última info = menos provável). Área que alguém do time limpou depois da última info desse
        /// inimigo não volta cheia na hora: o teto cresce com o tempo desde que foi vista (memória, em
        /// <paramref name="memoria"/> s volta a ser suspeita de vez). Rota toda vigiada = o rastro não passa.
        /// </summary>
        public void Spread(float t, float memoria)
        {
            foreach (var r in inimigos)
            {
                if (!r.vivo) continue;
                var h = r.poss; var nh = (float[])h.Clone();
                foreach (int i in passIdx)
                {
                    if (h[i] <= 0.02f) continue;
                    float v = h[i] * 0.985f;
                    foreach (int j in nbr[i])
                    {
                        if (nh[j] >= v) continue;
                        float teto = r.infoT > seenAt[j] ? 1 : (t - seenAt[j]) / memoria;
                        if (teto > nh[j]) nh[j] = MathF.Min(v, teto);
                    }
                }
                Array.Copy(nh, h, h.Length);
            }
        }

        /// <summary>Aliado vê a célula agora: ninguém está lá (todos os rastros zeram ali). Recalc depois.</summary>
        public void Clear(int i, float t) { seenAt[i] = t; heat[i] = 0; foreach (var r in inimigos) r.poss[i] = 0; }

        /// <summary>Quem está sendo visto agora não some do mapa só porque a célula dele foi "limpa" pela mesma visão.</summary>
        public void ReacendeVistos(float t)
        {
            foreach (var r in inimigos)
                if (r.vivo && contacts.TryGetValue(r.id, out var c) && t - c.t < 0.3f) MarkR(r, c.pos, 1, 1f);
        }

        /// <summary>
        /// Junta os rastros: confiança de cada inimigo = quão pequena é a área onde ele pode estar (A0/área); o calor
        /// de uma célula é a chance de ter pelo menos um ali. Se os 5 foram vistos entrando no A, o resto do mapa esfria.
        /// </summary>
        public void Recalc()
        {
            foreach (var r in inimigos)
            {
                if (!r.vivo) continue;
                float s = 0; var ps = r.poss; foreach (int i in passIdx) s += ps[i];
                r.area = s; r.conf = MathX.Clamp(A0 / MathF.Max(1, s), 0.03f, 1f);
            }
            Array.Clear(heat, 0, heat.Length);
            foreach (var r in inimigos)
            {
                if (!r.vivo) continue;
                var ps = r.poss; float cf = r.conf;
                foreach (int i in passIdx) { float v = ps[i]; if (v > 0) heat[i] = 1 - (1 - heat[i]) * (1 - v * cf); }   // chance de pelo menos um
            }
        }

        public void Seen(Agent enemy, float t, string by) => Seen(enemy.id, enemy.pos, enemy.z, enemy.hasBomb, t, by);

        /// <summary>Call do time: o inimigo estava em <paramref name="pos"/> no instante <paramref name="t"/> (pode ser de um pouco atrás).</summary>
        public void Seen(int id, V2 pos, float z, bool bomba, float t, string by)
        {
            contacts.TryGetValue(id, out var old);
            if (old != null && old.t > t) return;   // já tem info mais nova dele
            contacts[id] = new Contact { id = id, pos = pos, z = z, t = t, seen = true, by = by, hp = old?.hp ?? 100, hpT = old?.hpT ?? -9 };
            if (bomba) { bombaPos = pos; bombaT = t; }   // dá pra ver o C4 nas costas
            var r = inimigos.Find(x => x.id == id);
            if (r != null && r.vivo) Colapsa(r, pos, 1, 1f, t);
        }

        /// <summary>
        /// Som (passo, tiro, plant): de quem é? Do inimigo que mais plausivelmente estava ali (o rastro dele cobre o
        /// lugar; empate = quem está há mais tempo sem info). Esse inimigo passa a estar ali.
        /// </summary>
        public void Heard(V2 p, float t, string kind, float uncertainty)
        {
            heard.Add((t, p, kind));
            int i = Idx(p); Rastro best = null; float bs = -1;
            foreach (var r in inimigos)
            {
                if (!r.vivo) continue;
                float s = r.poss[i] + 0.001f * (t - r.infoT);
                if (s > bs) { bs = s; best = r; }
            }
            if (best != null) Colapsa(best, p, Math.Max(1, (int)(uncertainty / C)), kind == "tiro" ? 0.95f : 0.85f, t);
        }

        public void Morreu(int id) { var r = inimigos.Find(x => x.id == id); if (r != null) { r.vivo = false; Array.Clear(r.poss, 0, r.poss.Length); } }

        /// <summary>Quantos inimigos devem estar num raio (soma das probabilidades de cada um estar ali).</summary>
        public float Esperados(V2 p, float radius)
        {
            float tot = 0; int rr = (int)(radius / C) + 1, cx = (int)(p.x / C), cy = (int)(p.y / C);
            foreach (var r in inimigos)
            {
                if (!r.vivo || r.area <= 0) continue;
                float s = 0;
                for (int x = cx - rr; x <= cx + rr; x++)
                    for (int y = cy - rr; y <= cy + rr; y++)
                    {
                        if (x < 0 || y < 0 || x >= GX || y >= GY) continue;
                        int i = x * GY + y; if (r.poss[i] > 0 && (Center(x, y) - p).Len <= radius) s += r.poss[i];
                    }
                tot += s / r.area;
            }
            return tot;
        }

        /// <summary>Quantos inimigos o time acha que estão perto de um lugar (contatos recentes).</summary>
        public int RecentNear(V2 p, float radius, float now, float within)
        {
            int n = 0;
            foreach (var c in contacts.Values) if (now - c.t < within && (c.pos - p).Len < radius) n++;
            return n;
        }

        /// <summary>Calor médio numa região (0..1: chance de ter gente ali).</summary>
        public float HeatAvg(V2 p, float radius)
        {
            float s = 0; int n = 0, r = (int)(radius / C) + 1;
            int cx = (int)(p.x / C), cy = (int)(p.y / C);
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= GX || y >= GY) continue;
                    int i = x * GY + y; if (pass[i] && (Center(x, y) - p).Len <= radius) { s += heat[i]; n++; }
                }
            return n > 0 ? s / n : 0;
        }

        /// <summary>Soma do calor numa região (quanto "provavelmente tem gente" ali).</summary>
        public float HeatIn(V2 p, float radius)
        {
            float s = 0; int r = (int)(radius / C) + 1;
            int cx = (int)(p.x / C), cy = (int)(p.y / C);
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= GX || y >= GY) continue;
                    int i = x * GY + y; if (pass[i] && (Center(x, y) - p).Len <= radius) s += heat[i];
                }
            return s;
        }
    }
}
