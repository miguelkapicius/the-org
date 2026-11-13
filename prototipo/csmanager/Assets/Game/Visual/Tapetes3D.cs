using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Tapetes marroquinos de verdade nos terraços da Vila (usuário: "os telhados podem ter realmente tapetes de verdade,
    /// tapetes marroquinos"): cada tapete é um objeto (malha própria, um quadradinho por ponto do desenho, com franja),
    /// estendido no meio do terraço ou pendurado na platibanda virado pra rua. Desenho e cores mudam de casa pra casa
    /// (losangos, faixas em zigue-zague, Beni Ourain creme com losango preto, medalhão). Ao lado de alguns, potes de barro
    /// do pack. Só visual: nada aqui entra na Sim. Usa as casas que o Pisos3D.Ksar separou.
    /// </summary>
    public static class Tapetes3D
    {
        static readonly int[][] PAL =
        {
            new[] { 0x5A1E1A, 0xB3322B, 0xF0E2C0, 0x2F4E8C },   // vermelho, creme e azul
            new[] { 0x2A2622, 0xE9DFC8, 0x2A2622, 0xB3322B },   // Beni Ourain: creme com preto
            new[] { 0x6A2A12, 0xD98A2B, 0xF0E2C0, 0x7A1F2A },   // açafrão
            new[] { 0x1F3A5F, 0x2F5E8E, 0xE8C36A, 0xB3322B },   // anil com ouro
            new[] { 0x5A1E3A, 0xA83A6A, 0xF0E2C0, 0xE0A030 },   // magenta
            new[] { 0x3A2A1E, 0x8A5A3A, 0xE9DFC8, 0xB3322B },   // lã crua marrom
        };

        static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1);

        /// <summary>Cor do ponto (u ao longo da largura, v ao longo do comprimento).</summary>
        static Color Ponto(int u, int v, int nu, int nv, int estilo, int[] p)
        {
            int bu = Mathf.Min(u, nu - 1 - u), bv = Mathf.Min(v, nv - 1 - v), b = Mathf.Min(bu, bv);
            if (bv < 2) return Hex((u / 2) % 2 == 0 ? p[0] : p[2]);          // barra listrada das pontas
            if (b == 0) return Hex(p[0]);
            if (b == 1) return Hex(p[2]);
            if (b == 2) return Hex(p[0]);
            switch (estilo)
            {
                case 0:
                {   // malha de losangos com miolo
                    int a = (u + v) % 8, c = ((u - v) % 8 + 8) % 8;
                    if (a == 0 || c == 0) return Hex(p[2]);
                    if (Mathf.Abs(a - 4) + Mathf.Abs(c - 4) <= 1) return Hex(p[3]);
                    return Hex(p[1]);
                }
                case 1:
                {   // faixas atravessadas com zigue-zague
                    int f = v / 5, w = v % 5, z = (f % 2 == 0) ? w : 4 - w;
                    if (w == 0) return Hex(p[0]);
                    if ((u + z) % 6 == 0) return Hex(p[2]);
                    return Hex(f % 3 == 0 ? p[3] : p[1]);
                }
                case 2:
                {   // Beni Ourain: campo liso com losangos grandes de linha fina
                    int a = (u + v) % 12, c = ((u - v) % 12 + 12) % 12;
                    return Hex(a == 0 || c == 0 ? p[0] : p[1]);
                }
                default:
                {   // medalhão no meio, cruzinhas no campo
                    float d = Mathf.Abs(u - (nu - 1) * 0.5f) * 1.5f + Mathf.Abs(v - (nv - 1) * 0.5f);
                    if (d < 11) { int r = (int)(d / 2.75f); return Hex(r == 0 ? p[3] : r == 1 ? p[2] : r == 2 ? p[0] : p[3]); }
                    if (u % 6 == 3 && v % 6 == 3) return Hex(p[2]);
                    if ((u % 6 == 3 && (v % 6 == 2 || v % 6 == 4)) || (v % 6 == 3 && (u % 6 == 2 || u % 6 == 4))) return Hex(p[3]);
                    return Hex(p[1]);
                }
            }
        }

        class Malha
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>(); public readonly List<int> T = new List<int>();
            readonly bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            /// <summary>quadradinho de canto o (Unity) e lados a, b; normal n.</summary>
            public void Q(Vector3 o, Vector3 a, Vector3 b, Vector3 n, Color c)
            {
                int k = V.Count; var lc = lin ? c.linear : c; lc.a = 1;
                V.Add(o); V.Add(o + a); V.Add(o + a + b); V.Add(o + b);
                for (int i = 0; i < 4; i++) { N.Add(n); C.Add(lc); }
                if (Vector3.Dot(Vector3.Cross(a, b), n) >= 0) { T.Add(k); T.Add(k + 1); T.Add(k + 2); T.Add(k); T.Add(k + 2); T.Add(k + 3); }
                else { T.Add(k); T.Add(k + 2); T.Add(k + 1); T.Add(k); T.Add(k + 3); T.Add(k + 2); }
            }
        }

        static Vector3 P(float x, float y, float z) => MalhaMapa.ParaUnity(x, y, z);
        static Vector3 D(float x, float y, float z) => new Vector3(x, z, -y) * MalhaMapa.ESC;

        /// <summary>Trecho de tapete: canto o (jogo), du = 1 ponto na largura, dv = 1 ponto no comprimento, normal n (jogo);
        /// pontos v0..v1 do desenho.</summary>
        static void Trecho(Malha m, Vector3 o, Vector3 du, Vector3 dv, Vector3 n, int nu, int nv, int v0, int v1, int estilo, int[] p)
        {
            var nU = new Vector3(n.x, n.z, -n.y);
            for (int v = v0; v < v1; v++)
                for (int u = 0; u < nu; u++)
                {
                    var q = o + du * u + dv * (v - v0);
                    m.Q(P(q.x, q.y, q.z), D(du.x, du.y, du.z), D(dv.x, dv.y, dv.z), nU, Ponto(u, v, nu, nv, estilo, p));
                }
        }

        /// <summary>Franja: fiapos creme de 1,6 saindo da ponta (um sim, um não).</summary>
        static void Franja(Malha m, Vector3 o, Vector3 du, Vector3 dv, Vector3 n, int nu)
        {
            var nU = new Vector3(n.x, n.z, -n.y); var cor = Hex(0xEADFC6);
            for (int u = 0; u < nu; u++)
            {
                if (u % 2 == 1) continue;
                var q = o + du * (u + 0.15f);
                m.Q(P(q.x, q.y, q.z), D(du.x, du.y, du.z) * 0.7f, D(dv.x, dv.y, dv.z) * 1.6f, nU, cor);
            }
        }

        static Vector3 Rm(float x, float y, float z) => new Vector3(x, z, y) * MalhaMapa.ESC;
        const float E = MalhaMapa.ESC;

        /// <summary>Pérgola de caniço: quatro paus, duas travessas e a esteira de varas por cima (faz sombra listrada).</summary>
        static void Pergola(Obra3D o, float x0, float y0, float w, float h, float z)
        {
            Color pau = Hex(0x7E5A3A), canico = Hex(0xCDAA6E); const float H = 13f;
            foreach (var (px, py) in new[] { (x0 + 0.6f, y0 + 0.6f), (x0 + w - 0.6f, y0 + 0.6f), (x0 + 0.6f, y0 + h - 0.6f), (x0 + w - 0.6f, y0 + h - 0.6f) })
                o.Caixa(P(px, py, z + H / 2), Rm(0.45f, 0.45f, H / 2), pau);
            o.Caixa(P(x0 + w / 2, y0 + 0.6f, z + H), Rm(w / 2 + 0.8f, 0.4f, 0.4f), pau); o.Caixa(P(x0 + w / 2, y0 + h - 0.6f, z + H), Rm(w / 2 + 0.8f, 0.4f, 0.4f), pau);
            int n = (int)(w / 1.5f);
            for (int i = 0; i < n; i++) o.Caixa(P(x0 + (i + 0.5f) * w / n, y0 + h / 2, z + H + 0.6f), Rm(0.42f, h / 2 + 0.9f, 0.18f), canico * (i % 2 == 0 ? 1f : 0.9f));
        }

        /// <summary>Roda de chá: mesinha baixa de latão com bule e pufes coloridos em volta.</summary>
        static void Cha(Obra3D o, float cx, float cy, float z, System.Random r)
        {
            o.Cilindro(P(cx, cy, z), D(0, 0, 1.6f), 1.2f * E, 1.2f * E, 10, Hex(0x6E4A2E));
            o.Cilindro(P(cx, cy, z + 1.6f), D(0, 0, 0.5f), 3.2f * E, 3.2f * E, 16, Hex(0xB8873A), Hex(0xD9A441));
            o.Cilindro(P(cx + 0.5f, cy - 0.3f, z + 2.1f), D(0, 0, 1.3f), 0.75f * E, 0.45f * E, 8, Hex(0xC9CCD0));
            int[] cores = { 0xB3322B, 0x2F5E8E, 0xD98A2B, 0xA83A6A, 0x3E8C6A };
            int n = 3 + r.Next(2); float a0 = (float)r.NextDouble() * 6.28f;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * 6.283f / n * 0.82f;
                o.Bola(P(cx + Mathf.Cos(a) * 5.6f, cy + Mathf.Sin(a) * 5.6f, z + 1.0f), Rm(2.5f, 2.5f, 1.4f), Hex(cores[r.Next(cores.Length)]), 10);
            }
        }

        /// <summary>Bandejas de palha com tempero secando ao sol: páprica, cúrcuma, hena, tâmara.</summary>
        static void Bandejas(Obra3D o, float x0, float y0, float w, float h, float z, System.Random r)
        {
            int[] po = { 0xB3322B, 0xE0A030, 0x7A8B3A, 0x6A3A1E, 0xD9662B };
            int q0 = r.Next(po.Length);
            for (int i = 0; i < 3; i++)
            {
                float px = x0 + (i + 0.5f) * w / 3, py = y0 + h / 2 + (i == 1 ? 0.8f : -0.4f);
                o.Cilindro(P(px, py, z), D(0, 0, 0.5f), 2.0f * E, 2.25f * E, 14, Hex(0xC9A66B), Hex(0xB89458));
                o.Bola(P(px, py, z + 0.45f), Rm(1.75f, 1.75f, 0.75f), Hex(po[(q0 + i) % po.Length]), 10);
            }
        }

        public static GameObject Montar(Material mat, Transform pai)
        {
            if (MapData.MAPA == "brasa" || Pisos3D.Casas.Count == 0) return null;
            int NX = MapData.NX, NY = MapData.NY;
            var id = Pisos3D.CasaDe; var alt = Pisos3D.Alt;
            // distância (em casas, 4 vizinhos) até sair da própria casa
            var dist = new int[NX, NY]; var fila = new Queue<(int, int)>();
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    dist[x, y] = id[x, y] < 0 ? 0 : 999;
                    if (id[x, y] < 0) continue;
                    bool beira = x == 0 || y == 0 || x == NX - 1 || y == NY - 1 || id[x - 1, y] != id[x, y] || id[x + 1, y] != id[x, y] || id[x, y - 1] != id[x, y] || id[x, y + 1] != id[x, y];
                    if (beira) { dist[x, y] = 1; fila.Enqueue((x, y)); }
                }
            while (fila.Count > 0)
            {
                var (x, y) = fila.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int u = x + dx, v = y + dy;
                    if (u < 0 || v < 0 || u >= NX || v >= NY || dist[u, v] <= dist[x, y] + 1) continue;
                    dist[u, v] = dist[x, y] + 1; fila.Enqueue((u, v));
                }
            }
            var tralhas = Adornos3D.Tralhas();
            bool Livre(int x0, int y0, int w, int h, int casa, int topo)
            {
                if (x0 < 1 || y0 < 1 || x0 + w >= NX - 1 || y0 + h >= NY - 1) return false;
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                        if (id[x, y] != casa || dist[x, y] < 5 || alt[x, y] != topo) return false;
                foreach (var t in tralhas)
                    if (t.cx > x0 - 6 && t.cx < x0 + w + 6 && t.cy > y0 - 6 && t.cy < y0 + h + 6) return false;
                return true;
            }
            var m = new Malha(); int nChao = 0, nPend = 0, nPote = 0, nExtra = 0;
            var ob = new Obra3D(); var usados = new List<RectInt>();
            bool Bate(int x0, int y0, int w, int h) { var q = new RectInt(x0, y0, w, h); foreach (var u in usados) if (u.Overlaps(q)) return true; return false; }
            var tam = new[] { (22, 36), (18, 30), (16, 26), (14, 22), (12, 18) };
            for (int k = 0; k < Pisos3D.Casas.Count; k++)
            {
                var c = Pisos3D.Casas[k];
                if (c.cel.Count < 300 || c.topo - MapData.GZ < 14) continue;
                uint hs = (uint)(c.cel[0].x * 73856093) ^ (uint)(c.cel[0].y * 19349663) ^ (uint)(c.cel.Count * 83492791); hs ^= hs >> 13; hs *= 0x5bd1e995; hs ^= hs >> 15;
                var r = new System.Random((int)(hs & 0x7fffffff));
                int estilo = new[] { 0, 1, 3, 0, 1, 3, 0, 2 }[r.Next(8)]; var p = PAL[new[] { 0, 2, 3, 4, 5 }[r.Next(5)]]; if (estilo == 2) p = PAL[1];
                // --- estendido no terraço (em ~6 de cada 10 casas grandes)
                if (r.NextDouble() < 0.46)
                {
                    bool posto = false;
                    for (int t = 0; t < tam.Length && !posto; t++)
                        for (int tent = 0; tent < 40 && !posto; tent++)
                        {
                            var (w, l) = tam[t]; bool deitado = r.Next(2) == 0; int sx = deitado ? l : w, sy = deitado ? w : l;
                            var (qx, qy) = c.cel[r.Next(c.cel.Count)];
                            int x0 = qx - sx / 2, y0 = qy - sy / 2;
                            if (!Livre(x0 - 1, y0 - 3, sx + 2, sy + 6, k, c.topo)) continue;
                            float z = c.topo + 0.32f;
                            if (deitado) { Trecho(m, new Vector3(x0, y0, z), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), w, l, 0, l, estilo, p); Franja(m, new Vector3(x0, y0, z), new Vector3(0, 1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), w); Franja(m, new Vector3(x0 + l, y0, z), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), w); }
                            else { Trecho(m, new Vector3(x0, y0, z), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), w, l, 0, l, estilo, p); Franja(m, new Vector3(x0, y0, z), new Vector3(1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), w); Franja(m, new Vector3(x0, y0 + l, z), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), w); }
                            posto = true; nChao++; usados.Add(new RectInt(x0 - 4, y0 - 4, sx + 8, sy + 8));
                            // potes de barro do pack num canto do tapete (em metade das casas)
                            if (Packs3D.TemModelos && r.NextDouble() < 0.5)
                            {
                                float px = x0 - 3.2f, py = y0 + (deitado ? w : l) * 0.25f;
                                Packs3D.Modelo(r.Next(2) == 0 ? "fp_Vase_2" : "fp_Pot_1", px, py, c.topo, 5.6f, (float)r.NextDouble() * 360f);
                                if (r.Next(2) == 0) Packs3D.Modelo("fp_Vase_4", px + 0.6f, py + 5.2f, c.topo, 4.2f, (float)r.NextDouble() * 360f);
                                nPote++;
                            }
                        }
                }
                // --- outras coisas de terraço (usuário: "além de tapete, podem ter outras coisas"): pérgola de caniço, roda de
                //     chá com pufes ou bandejas de tempero secando — uma por casa, em ~6 de cada 10, longe do tapete
                double se = r.NextDouble();
                if (se < 0.45)
                {
                    int tipo = se < 0.18 ? 0 : se < 0.33 ? 1 : 2;
                    int ew = tipo == 0 ? 15 : tipo == 1 ? 13 : 15, eh = tipo == 0 ? 12 : tipo == 1 ? 13 : 6;
                    for (int tent = 0; tent < 50; tent++)
                    {
                        var (qx, qy) = c.cel[r.Next(c.cel.Count)]; int x0 = qx - ew / 2, y0 = qy - eh / 2;
                        if (!Livre(x0 - 1, y0 - 1, ew + 2, eh + 2, k, c.topo) || Bate(x0, y0, ew, eh)) continue;
                        usados.Add(new RectInt(x0 - 2, y0 - 2, ew + 4, eh + 4));
                        if (tipo == 0) Pergola(ob, x0, y0, ew, eh, c.topo);
                        else if (tipo == 1) Cha(ob, x0 + ew / 2f, y0 + eh / 2f, c.topo, r);
                        else Bandejas(ob, x0, y0, ew, eh, c.topo, r);
                        nExtra++; break;
                    }
                }
                // --- pendurado na platibanda, virado pra rua (em ~1 de cada 3)
                if (r.NextDouble() < 0.3 && nPend < 9)
                {
                    const int W = 12, CAI = 20, DENTRO = 4; int ptopo = c.topo + Pisos3D.PLATIBANDA;
                    for (int tent = 0; tent < 60; tent++)
                    {
                        var (qx, qy) = c.cel[r.Next(c.cel.Count)];
                        if (dist[qx, qy] != 1) continue;
                        // pra que lado é fora, e se fora é rua mais baixa
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int ox = qx + dx * 3, oy = qy + dy * 3;
                            if (ox < 0 || oy < 0 || ox >= NX || oy >= NY || !MapData.walk[ox, oy] || MapData.floor[ox, oy] > c.topo - CAI - 2) continue;
                            bool ok = true;
                            for (int s = -1; s <= W && ok; s++)
                            {
                                int ex = qx + (dx == 0 ? s : 0), ey = qy + (dy == 0 ? s : 0);
                                ok = ex > 2 && ey > 2 && ex < NX - 3 && ey < NY - 3 && id[ex, ey] == k && alt[ex, ey] == ptopo && id[ex + dx, ey + dy] < 0 && MapData.walk[ex + dx * 2, ey + dy * 2];
                            }
                            if (!ok) continue;
                            // canto de fora da platibanda (0,45 pra fora da parede) e eixo ao longo da parede
                            var du = dx == 0 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0); var fora = new Vector3(dx, dy, 0);
                            var o = new Vector3(qx + (dx > 0 ? 1 : 0) + (dx == 0 ? 0 : 0), qy + (dy > 0 ? 1 : 0), ptopo + 0.35f);
                            if (dx != 0) o.x = qx + (dx > 0 ? 1 : 0); if (dy != 0) o.y = qy + (dy > 0 ? 1 : 0);
                            var oD = o - fora * DENTRO;                               // começa em cima da platibanda, pra dentro
                            Trecho(m, oD, du, fora * ((DENTRO + 0.45f) / DENTRO), new Vector3(0, 0, 1), W, DENTRO + CAI, 0, DENTRO, estilo, p);
                            var oF = o + fora * 0.45f;                                // desce pela parede
                            Trecho(m, oF, du, new Vector3(0, 0, -1), fora, W, DENTRO + CAI, DENTRO, DENTRO + CAI, estilo, p);
                            Franja(m, oF + new Vector3(0, 0, -CAI), du, new Vector3(0, 0, -1), fora, W);
                            nPend++; tent = 999; break;
                        }
                    }
                }
            }
            Debug.Log($"[Tapetes3D] tapetes: {nChao} estendidos, {nPend} pendurados; potes em {nPote} terraços; pérgola/chá/bandejas em {nExtra}");
            if (m.V.Count == 0) return null;
            var mt = new Material(mat) { name = "Maquete tapete" }; mt.SetFloat("_UsarTopo", 0); mt.SetFloat("_Smoothness", 0.08f); mt.SetFloat("_Variacao", 0.04f);
            if (ob.Vertices > 0) ob.Montar("Terraços (pérgola, chá, bandejas)", mt, pai);
            var me = new Mesh { name = "tapetes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            me.SetVertices(m.V); me.SetNormals(m.N); me.SetColors(m.C); me.SetTriangles(m.T, 0); me.RecalculateBounds();
            var go = new GameObject("Tapetes"); go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = me;
            var rr = go.AddComponent<MeshRenderer>(); rr.sharedMaterial = mt; go.isStatic = true;
            return go;
        }
    }
}
