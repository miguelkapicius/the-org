using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Em volta da vila (fora do mapa jogável, só cenário): cidade marroquina em maquete — chão de terra/areia ocre,
    /// casario de reboco (rosa de Marrakech, areia, creme) com mureta no telhado, janelinhas escuras, cúpulas e caixas
    /// d'água; palmeiras; um minarete quadrado com ameias; a muralha de taipa com torres; e dunas lá longe. Baixo perto
    /// da borda do mapa (não tapa o jogo na câmera isométrica) e mais alto quanto mais longe. Nada disso entra na
    /// simulação.
    /// </summary>
    public static class Arredores
    {
        static float H(float x, float y) => Mathf.PerlinNoise(x * 0.11f + 3.1f, y * 0.11f + 7.7f);
        static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);

        public static GameObject Montar(Material mat, Material matCor, Transform pai, int semente = 7)
        {
            var raiz = new GameObject("Arredores");
            raiz.transform.SetParent(pai, false);
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color L(Color c) => lin ? c.linear : c;
            float W = MapData.NX * MalhaMapa.ESC, D = MapData.NY * MalhaMapa.ESC;   // 40 x 35
            float M = 30f, piso = (MapData.GZ - 0.6f) * MalhaMapa.ESC;
            float Dist(float x, float z) { float dx = Mathf.Max(0, Mathf.Max(-x, x - W)), dz = Mathf.Max(0, Mathf.Max(-z - D, z)); return Mathf.Sqrt(dx * dx + dz * dz); }
            float Relevo(float x, float z) { float d = Dist(x, z); return piso + Mathf.SmoothStep(0, 1, (d - 14f) / 12f) * (H(x, z) * 2.4f); }   // dunas só longe

            // chão: terra batida ocre perto, areia mais clara e ondulada longe, trilhas mais escuras
            float x0 = -M, x1 = W + M, z0 = -D - M, z1 = M, step = 0.5f;
            int nx = Mathf.CeilToInt((x1 - x0) / step) + 1, nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
            var V = new Vector3[nx * nz]; var Cc = new Color[nx * nz]; var T = new List<int>();
            Color terra = Hex(0xD9B27C), areia = Hex(0xEBCB92), areia2 = Hex(0xE2BC80), trilha = Hex(0xC79A64);
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    float x = x0 + i * step, z = z0 + k * step;
                    V[i * nz + k] = new Vector3(x, Relevo(x, z), z);
                    float mancha = Mathf.PerlinNoise(x * 0.3f + 11, z * 0.3f + 5);
                    var c = Color.Lerp(terra, Color.Lerp(areia2, areia, mancha), Mathf.SmoothStep(4, 18, Dist(x, z)));
                    float tr = Mathf.Min(Mathf.Abs(x - W * 0.5f) + (z > 0 ? 0 : 99), Mathf.Abs(z + D * 0.5f) + (x > W ? 0 : 99));
                    tr = Mathf.Min(tr, Mathf.Abs(z + D * 0.5f) + (x < 0 ? 0 : 99));
                    c = Color.Lerp(c, trilha, 1 - Mathf.SmoothStep(0.6f, 1.0f, tr + Mathf.PerlinNoise(x, z) * 0.3f));
                    Cc[i * nz + k] = L(c);
                }
            for (int i = 0; i < nx - 1; i++)
                for (int k = 0; k < nz - 1; k++)
                {
                    int a = i * nz + k, b = (i + 1) * nz + k, c = (i + 1) * nz + k + 1, d = i * nz + k + 1;
                    T.Add(a); T.Add(d); T.Add(c); T.Add(a); T.Add(c); T.Add(b);
                }
            var me = new Mesh { name = "chão de fora", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            me.vertices = V; me.colors = Cc; me.SetTriangles(T, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var chao = new GameObject("Chão de fora"); chao.transform.SetParent(raiz.transform, false);
            chao.AddComponent<MeshFilter>().sharedMesh = me; chao.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var o = new Obra3D();
            var rng = new System.Random(semente);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // mesmos 8 tons de adobe das casas de dentro (usuário: "o cenário começa a ficar destoante do resto")
            var reboco = new Color[Paleta3D.NTONS]; for (int t = 0; t < reboco.Length; t++) { reboco[t] = Paleta3D.Srgb(Paleta3D.ADOBE + t); reboco[t].a = 1; }
            Color cal = Hex(0xEFE3CC);
            Color escuro = Hex(0x4A3428), creme = Hex(0xF4E4C4), telhaVerde = Hex(0x3E8C6A), madeira = Hex(0x8F5A32);

            // casario: quarteirões de casas coladas (grade irregular), fora do mapa e sem entrar na trilha
            var ocupado = new List<Rect>();
            bool Livre(Rect r) { foreach (var q in ocupado) if (q.Overlaps(r)) return false; return true; }
            for (int n = 0; n < 2600; n++)
            {
                float w = R(1.2f, 3.4f), d = R(1.2f, 3.4f);
                float x = R(x0 + 2, x1 - 2), z = R(z0 + 2, z1 - 2);
                var r = new Rect(x - w / 2, z - d / 2, w, d);
                float dist = Dist(x, z);
                if (dist < 1.2f || dist > 24f) continue;
                if (Mathf.Abs(x - W * 0.5f) < 1.4f && z > -1 || Mathf.Abs(z + D * 0.5f) < 1.4f && (x > W - 1 || x < 1)) continue;   // trilhas
                if (!Livre(new Rect(r.x - 0.08f, r.y - 0.08f, r.width + 0.16f, r.height + 0.16f))) continue;
                ocupado.Add(r);
                float y0 = Relevo(x, z) - 0.2f;
                // baixo perto do mapa (a câmera olha por cima dele) e subindo longe
                float hMax = Mathf.Lerp(0.5f, 3.6f, Mathf.SmoothStep(1.2f, 12f, dist));
                float h = R(0.55f, 1f) * hMax;
                // uma em cada oito é caiada (branca, lisa), pra quebrar o mar de terra; as outras são adobe com a textura do shader
                bool caiada = rng.NextDouble() < 0.12;
                var cor = (caiada ? cal : reboco[rng.Next(reboco.Length)]) * R(0.94f, 1.04f);
                var c = new Vector3(x, y0 + h / 2 + 0.1f, z);
                o.Alfa = caiada ? 1f : 0.30f;
                o.Caixa(c, new Vector3(w / 2, h / 2 + 0.1f, d / 2), cor);
                // mureta no telhado (parapeito) e telhado um tom mais claro
                float yt = y0 + h + 0.2f;
                o.Caixa(new Vector3(x, yt + 0.06f, z - d / 2 + 0.05f), new Vector3(w / 2, 0.08f, 0.05f), cor * 1.06f);
                o.Caixa(new Vector3(x, yt + 0.06f, z + d / 2 - 0.05f), new Vector3(w / 2, 0.08f, 0.05f), cor * 1.06f);
                o.Caixa(new Vector3(x - w / 2 + 0.05f, yt + 0.06f, z), new Vector3(0.05f, 0.08f, d / 2), cor * 1.06f);
                o.Caixa(new Vector3(x + w / 2 - 0.05f, yt + 0.06f, z), new Vector3(0.05f, 0.08f, d / 2), cor * 1.06f);
                // ameias em degrau nas quinas (como nas casas de dentro); as casas altas viram torre com a platibanda toda dentada
                if (!caiada && rng.NextDouble() < 0.7)
                    foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                    {
                        o.Caixa(new Vector3(x + sx * (w / 2 - 0.11f), yt + 0.2f, z + sz * (d / 2 - 0.11f)), new Vector3(0.11f, 0.14f, 0.11f), cor * 1.04f);
                        o.Caixa(new Vector3(x + sx * (w / 2 - 0.06f), yt + 0.4f, z + sz * (d / 2 - 0.06f)), new Vector3(0.06f, 0.07f, 0.06f), cor * 1.04f);
                    }
                if (!caiada && h > 2.2f && rng.NextDouble() < 0.6)
                {
                    int nmx = Mathf.Max(2, Mathf.FloorToInt(w / 0.42f)), nmz = Mathf.Max(2, Mathf.FloorToInt(d / 0.42f));
                    for (int j = 1; j < nmx; j += 2) foreach (int sd in new[] { -1, 1 }) o.Caixa(new Vector3(x - w / 2 + (j + 0.5f) * w / nmx, yt + 0.2f, z + sd * (d / 2 - 0.05f)), new Vector3(w / nmx * 0.5f, 0.07f, 0.05f), cor * 1.04f);
                    for (int j = 1; j < nmz; j += 2) foreach (int sd in new[] { -1, 1 }) o.Caixa(new Vector3(x + sd * (w / 2 - 0.05f), yt + 0.2f, z - d / 2 + (j + 0.5f) * d / nmz), new Vector3(0.05f, 0.07f, d / nmz * 0.5f), cor * 1.04f);
                }
                // terraço de terra batida (mesma classe do de dentro)
                if (!caiada) { o.Alfa = 0.15f; o.Caixa(new Vector3(x, yt + 0.004f, z), new Vector3(w / 2 - 0.1f, 0.004f, d / 2 - 0.1f), cor * 0.86f); o.Alfa = 0.30f; }
                // barrado no pé da casa (tom mais escuro do próprio reboco) e friso claro no alto, debaixo da mureta
                o.Caixa(new Vector3(x, y0 + 0.2f, z), new Vector3(w / 2 + 0.012f, 0.2f, d / 2 + 0.012f), cor * 0.84f);
                o.Caixa(new Vector3(x, yt - 0.04f, z), new Vector3(w / 2 + 0.012f, 0.03f, d / 2 + 0.012f), Color.Lerp(cor, creme, caiada ? 0.55f : 0.25f));
                o.Alfa = 1f;
                creme = caiada ? Hex(0xF4E4C4) : Color.Lerp(cor, Hex(0xF4E4C4), 0.4f);   // moldura de janela no tom da casa (a creme pura destoava do adobe)
                // janelas com moldura clara, peitoril e folha colorida (azul, verde ou madeira); porta com moldura no térreo
                Color[] folha = { Hex(0x2F6FB0), telhaVerde, madeira, escuro, escuro, escuro, madeira };
                int nj = Mathf.Max(1, Mathf.FloorToInt(w / 0.9f));
                for (int j = 0; j < nj; j++)
                    for (int andar = 0; andar < Mathf.Max(1, Mathf.FloorToInt(h / 0.9f)); andar++)
                    {
                        if (rng.NextDouble() < 0.35) continue;
                        float jx = x - w / 2 + (j + 0.5f) * w / nj, jy = y0 + 0.55f + andar * 0.9f;
                        if (jy + 0.25f > yt) continue;
                        var cj = folha[rng.Next(folha.Length)];
                        bool porta = andar == 0 && rng.NextDouble() < 0.3;
                        foreach (int sd in new[] { -1, 1 })
                        {
                            float zf = z + sd * (d / 2 + 0.01f);
                            if (porta)
                            {
                                o.Caixa(new Vector3(jx, y0 + 0.42f, zf), new Vector3(0.17f, 0.34f, 0.015f), creme);
                                o.Caixa(new Vector3(jx, y0 + 0.4f, zf + sd * 0.012f), new Vector3(0.13f, 0.3f, 0.015f), cj == escuro ? madeira : cj);
                            }
                            else
                            {
                                o.Caixa(new Vector3(jx, jy, zf), new Vector3(0.155f, 0.215f, 0.015f), creme);
                                o.Caixa(new Vector3(jx, jy, zf + sd * 0.012f), new Vector3(0.12f, 0.18f, 0.015f), cj);
                                o.Caixa(new Vector3(jx, jy - 0.21f, zf + sd * 0.02f), new Vector3(0.18f, 0.02f, 0.03f), creme);
                            }
                        }
                    }
                int nk = Mathf.Max(1, Mathf.FloorToInt(d / 0.9f));
                for (int j = 0; j < nk; j++)
                    for (int andar = 0; andar < Mathf.Max(1, Mathf.FloorToInt(h / 0.9f)); andar++)
                    {
                        if (rng.NextDouble() < 0.4) continue;
                        float jz = z - d / 2 + (j + 0.5f) * d / nk, jy = y0 + 0.55f + andar * 0.9f;
                        if (jy + 0.25f > yt) continue;
                        var cj = folha[rng.Next(folha.Length)];
                        foreach (int sd in new[] { -1, 1 })
                        {
                            float xf = x + sd * (w / 2 + 0.01f);
                            o.Caixa(new Vector3(xf, jy, jz), new Vector3(0.015f, 0.215f, 0.155f), creme);
                            o.Caixa(new Vector3(xf + sd * 0.012f, jy, jz), new Vector3(0.015f, 0.18f, 0.12f), cj);
                            o.Caixa(new Vector3(xf + sd * 0.02f, jy - 0.21f, jz), new Vector3(0.03f, 0.02f, 0.18f), creme);
                        }
                    }
                // coisas no telhado: caixa d'água, antena parabólica, cúpula, varal de tapetes
                double sorte = rng.NextDouble();
                if (sorte < 0.07) o.Cilindro(new Vector3(x + R(-w, w) * 0.25f, yt, z + R(-d, d) * 0.25f), Vector3.up * 0.32f, 0.17f, 0.17f, 10, Hex(0xBFB8AA), Hex(0xA9A294));
                else if (sorte < 0.3)
                {   // tapete estendido no terraço (a antena parabólica saiu: não combina com a vila de terra)
                    Color[] campo = { Hex(0xB3322B), Hex(0xD98A2B), Hex(0x2F5E8E), Hex(0xA83A6A), Hex(0xE9DFC8) };
                    var p = new Vector3(x + R(-w, w) * 0.12f, yt + 0.012f, z + R(-d, d) * 0.12f); bool emX = w > d;
                    float tw = Mathf.Min(Mathf.Min(w, d) * 0.2f, 0.34f), tl = Mathf.Min(Mathf.Max(w, d) * 0.24f, 0.55f); var cf = campo[rng.Next(campo.Length)];
                    o.Caixa(p, new Vector3(emX ? tl : tw, 0.006f, emX ? tw : tl), Hex(0x4A2A1E));
                    o.Caixa(p + Vector3.up * 0.006f, new Vector3((emX ? tl : tw) - 0.05f, 0.004f, (emX ? tw : tl) - 0.05f), cf);
                    o.Caixa(p + Vector3.up * 0.01f, new Vector3((emX ? tl : tw) * 0.35f, 0.003f, (emX ? tw : tl) * 0.35f), Hex(0xF0E2C0));
                }
                else if (sorte < 0.38 && dist > 5)
                {
                    float rr = Mathf.Min(w, d) * 0.32f;
                    o.Cilindro(new Vector3(x, yt, z), Vector3.up * 0.12f, rr, rr, 14, cor);
                    o.Bola(new Vector3(x, yt + 0.12f, z), new Vector3(rr, rr * 0.9f, rr), rng.NextDouble() < 0.5 ? telhaVerde : creme, 14);
                }
                else if (sorte < 0.45)
                {
                    var p = new Vector3(x, yt + 0.22f, z);
                    o.Caixa(p + Vector3.up * 0.02f, new Vector3(w * 0.35f, 0.01f, 0.01f), Hex(0x555555));
                    Color[] pano = { Hex(0xC9433A), Hex(0x2F7FBF), Hex(0xE8892E), Hex(0xF2E3C2) };
                    for (int j = 0; j < 3; j++) o.Caixa(p + new Vector3(-w * 0.25f + j * w * 0.25f, -0.1f, 0), new Vector3(0.08f, 0.11f, 0.01f), pano[rng.Next(pano.Length)]);
                }
            }

            // palmeiras (usuário: "coqueiros bem zoados"): tronco fino e curvo em anéis que afinam, com a base mais grossa;
            // copa em duas rodas de folhas compridas em arco (4 gomos cada, afinando e caindo), as de baixo mais caídas e
            // escuras; miolo e cacho de tâmaras
            void Palmeira(Vector3 b, float alt)
            {
                var tomba = new Vector3(R(-1f, 1f), 0, R(-1f, 1f)).normalized * R(0.05f, 0.22f); var p = b;
                int gomos = 12; Color casca = Hex(0x8E6B48);
                for (int g = 0; g < gomos; g++)
                {
                    float u = g / (float)gomos;
                    var e = (Vector3.up + tomba * (0.3f + 1.6f * u)).normalized * (alt / gomos);
                    float r0 = Mathf.Lerp(0.085f, 0.05f, u) + (g == 0 ? 0.03f : 0), r1 = Mathf.Lerp(0.085f, 0.05f, u + 1f / gomos);
                    o.Cilindro(p, e, r0, r1, 8, casca * (g % 2 == 0 ? 1f : 0.86f), null, false);
                    p += e;
                }
                o.Bola(p + Vector3.up * 0.02f, new Vector3(0.1f, 0.09f, 0.1f), Hex(0x6E5A32), 8);
                for (int roda = 0; roda < 2; roda++)
                {
                    int nf = roda == 0 ? 9 : 8; float comp = roda == 0 ? R(0.95f, 1.15f) : R(0.8f, 0.95f);
                    for (int f = 0; f < nf; f++)
                    {
                        float a = (f + roda * 0.5f) * Mathf.PI * 2 / nf + R(-0.15f, 0.15f);
                        var fora = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var lado = Vector3.Cross(fora, Vector3.up);
                        var cor = (roda == 0 ? Hex(0x58A53F) : Hex(0x3F8434)) * R(0.92f, 1.06f);
                        float sobe = roda == 0 ? 0.55f : 0.12f, cai = roda == 0 ? 0.55f : 0.95f;
                        var ant = p + Vector3.up * 0.04f;
                        for (int sg = 1; sg <= 4; sg++)
                        {
                            float t = sg / 4f;
                            var pt = p + Vector3.up * 0.04f + fora * (comp * t) + Vector3.up * (comp * (sobe * t - cai * t * t));
                            float larg = Mathf.Lerp(0.11f, 0.02f, t) * (sg == 1 ? 0.8f : 1f);
                            o.Bloco((ant + pt) / 2, (pt - ant) / 2, lado * larg, Vector3.Cross(lado, (pt - ant).normalized) * 0.012f, cor * (1f - 0.06f * sg));
                            ant = pt;
                        }
                    }
                }
                for (int c = 0; c < 3; c++) o.Bola(p + new Vector3(R(-0.09f, 0.09f), -0.07f, R(-0.09f, 0.09f)), Vector3.one * 0.045f, Hex(0xB9772E), 6);
            }
            for (int n = 0; n < 220; n++)
            {
                float x = R(x0 + 1, x1 - 1), z = R(z0 + 1, z1 - 1), dist = Dist(x, z);
                if (dist < 0.8f || dist > 26f) continue;
                var r = new Rect(x - 0.3f, z - 0.3f, 0.6f, 0.6f); if (!Livre(r)) continue;
                ocupado.Add(r);
                Palmeira(new Vector3(x, Relevo(x, z) - 0.05f, z), R(1.6f, 2.8f));
            }

            // minarete (torre quadrada com faixa de azulejo, ameias e lanterninha) num canto, de longe
            {
                float mx = W + 9f, mz = 8f, yb = Relevo(mx, mz) - 0.1f, lado = 1.5f, alt = 8.5f; var cor = Hex(0xD9956C);
                o.Caixa(new Vector3(mx, yb + alt / 2, mz), new Vector3(lado / 2, alt / 2, lado / 2), cor);
                o.Caixa(new Vector3(mx, yb + alt - 0.9f, mz), new Vector3(lado / 2 + 0.03f, 0.35f, lado / 2 + 0.03f), telhaVerde);
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        float a = i * Mathf.PI / 2; var f = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var t = Vector3.Cross(Vector3.up, f);
                        o.Caixa(new Vector3(mx, yb + alt + 0.12f, mz) + f * (lado / 2 - 0.08f) + t * ((j - 1) * 0.5f), new Vector3(0.12f, 0.14f, 0.12f), cor * 1.05f);
                        o.Caixa(new Vector3(mx, yb + alt * 0.55f + j * 1.1f, mz) + f * (lado / 2 + 0.01f), new Vector3(0.18f, 0.32f, 0.02f) , escuro);
                    }
                o.Caixa(new Vector3(mx, yb + alt + 0.6f, mz), new Vector3(0.38f, 0.6f, 0.38f), cor);
                o.Bola(new Vector3(mx, yb + alt + 1.25f, mz), new Vector3(0.3f, 0.3f, 0.3f), telhaVerde, 10);
                o.Cilindro(new Vector3(mx, yb + alt + 1.5f, mz), Vector3.up * 0.5f, 0.03f, 0.03f, 5, Hex(0xD9A441));
                for (int b = 0; b < 3; b++) o.Bola(new Vector3(mx, yb + alt + 1.62f + b * 0.14f, mz), Vector3.one * (0.08f - b * 0.015f), Hex(0xD9A441), 6);
            }

            // muralha de taipa com ameias e torres, contornando a cidade de longe
            {
                var cor = Hex(0xC97E58); float ex = 22f;
                Vector3[] cantos = { new Vector3(-ex, 0, ex), new Vector3(W + ex, 0, ex), new Vector3(W + ex, 0, -D - ex), new Vector3(-ex, 0, -D - ex) };
                for (int s = 0; s < 4; s++)
                {
                    var a = cantos[s]; var b = cantos[(s + 1) % 4]; var d = (b - a); float comp = d.magnitude; d /= comp;
                    var t = Vector3.Cross(Vector3.up, d);
                    for (float u = 0; u < comp; u += 0.5f)
                    {
                        var p = a + d * (u + 0.25f); float y = Relevo(p.x, p.z) - 0.2f;
                        o.Bloco(new Vector3(p.x, y + 0.9f, p.z), d * 0.26f, Vector3.up * 1.0f, t * 0.28f, cor * (0.95f + 0.05f * Mathf.Sin(u)));
                        if (((int)(u * 2)) % 2 == 0) o.Bloco(new Vector3(p.x, y + 2.05f, p.z), d * 0.13f, Vector3.up * 0.15f, t * 0.28f, cor * 1.03f);
                        if (((int)u) % 7 == 0 && u - Mathf.Floor(u) < 0.01f)
                        {
                            o.Caixa(new Vector3(p.x, y + 1.3f, p.z), new Vector3(0.7f, 1.3f, 0.7f), cor * 0.97f);
                            for (int q = 0; q < 4; q++) o.Caixa(new Vector3(p.x + (q % 2 == 0 ? -0.5f : 0.5f), y + 2.75f, p.z + (q < 2 ? -0.5f : 0.5f)), new Vector3(0.16f, 0.18f, 0.16f), cor * 1.04f);
                        }
                    }
                }
            }
            o.Montar("Cidade de fora", mat, raiz.transform);
            return raiz;
        }
    }
}
