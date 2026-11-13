using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Em volta da Brasa (fora do mapa jogável, só cenário): vila italiana de morro em maquete, no lugar da cidade
    /// marroquina do Arredores. Chão de capim verde-oliva com manchas de trigo e terra, morros subindo longe, fileiras de
    /// vinha, casario de reboco (ocre, salmão, creme, amarelo) com telhado de duas águas de telha capa-canal e persianas
    /// verdes, ciprestes e oliveiras do pack (Pine afinado, TwistedTree), e um campanário de tijolo de referência.
    /// Baixo perto da borda do mapa (não tapa o jogo na câmera) e mais alto quanto mais longe. Nada entra na Sim.
    /// </summary>
    public static class ArredoresItalia
    {
        static float H(float x, float y) => Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.07f + 7.7f);
        static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);

        public static GameObject Montar(Material mat, Material matCor, Transform pai, int semente = 11)
        {
            var raiz = new GameObject("Arredores (Itália)");
            raiz.transform.SetParent(pai, false);
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color L(Color c) => lin ? c.linear : c;
            float W = MapData.NX * MalhaMapa.ESC, D = MapData.NY * MalhaMapa.ESC;
            float M = 34f, piso = (MapData.GZ - 0.6f) * MalhaMapa.ESC;
            float Dist(float x, float z) { float dx = Mathf.Max(0, Mathf.Max(-x, x - W)), dz = Mathf.Max(0, Mathf.Max(-z - D, z)); return Mathf.Sqrt(dx * dx + dz * dz); }
            // morros: sobe devagar a partir de 10 da borda (a vila está no alto do morro, o campo desce e sobe de novo longe)
            float Relevo(float x, float z) { float d = Dist(x, z); return piso + Mathf.SmoothStep(0, 1, (d - 10f) / 16f) * (H(x, z) * 5.5f + 0.6f); }

            float x0 = -M, x1 = W + M, z0 = -D - M, z1 = M, step = 0.5f;
            int nx = Mathf.CeilToInt((x1 - x0) / step) + 1, nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
            var V = new Vector3[nx * nz]; var Cc = new Color[nx * nz]; var T = new List<int>();
            Color calcada = Hex(0xB8B2A6), capim = Hex(0x93A45A), capim2 = Hex(0x7E9450), trigo = Hex(0xD8BC6A), terra = Hex(0xB98E5E), vinha = Hex(0x5E7E3E);
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    float x = x0 + i * step, z = z0 + k * step, d = Dist(x, z);
                    V[i * nz + k] = new Vector3(x, Relevo(x, z), z);
                    float m = Mathf.PerlinNoise(x * 0.18f + 11, z * 0.18f + 5), campo = Mathf.PerlinNoise(x * 0.05f + 2, z * 0.05f + 9);
                    var c = Color.Lerp(capim2, capim, m);
                    if (campo > 0.62f) c = Color.Lerp(c, trigo, Mathf.SmoothStep(0.62f, 0.7f, campo));            // talhão de trigo
                    else if (campo < 0.3f) c = Color.Lerp(c, terra, Mathf.SmoothStep(0.3f, 0.22f, campo));        // terra arada
                    if (campo > 0.4f && campo < 0.55f && d > 12f && Mathf.Repeat(x * 1.6f + z * 0.4f, 1f) < 0.35f) c = vinha;   // fileira de vinha
                    c = Color.Lerp(calcada, c, Mathf.SmoothStep(0.8f, 3.5f, d));                                    // calçada de pedra colada no mapa
                    Cc[i * nz + k] = L(c * (0.94f + 0.06f * m));
                }
            for (int i = 0; i < nx - 1; i++)
                for (int k = 0; k < nz - 1; k++)
                {
                    int a = i * nz + k, b = (i + 1) * nz + k, c = (i + 1) * nz + k + 1, d = i * nz + k + 1;
                    T.Add(a); T.Add(d); T.Add(c); T.Add(a); T.Add(c); T.Add(b);
                }
            var me = new Mesh { name = "chão de fora (Itália)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            me.vertices = V; me.colors = Cc; me.SetTriangles(T, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var chao = new GameObject("Chão de fora"); chao.transform.SetParent(raiz.transform, false);
            chao.AddComponent<MeshFilter>().sharedMesh = me; chao.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var o = new Obra3D();
            var rng = new System.Random(semente);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Color[] reboco = { Hex(0xE9B067), Hex(0xE79A7E), Hex(0xF1DDB4), Hex(0xF2C66E), Hex(0xDFA57C), Hex(0xEBD0A0) };
            Color telha = Hex(0xC4562E), telha2 = Hex(0xB04A28), persiana = Hex(0x4C7A58), escuro = Hex(0x3A2E26), pedra = Hex(0xE7DCC6);

            // casario: casas coladas perto (borgo) e espalhadas longe (casa de campo), sempre com telhado de duas águas
            var ocupado = new List<Rect>();
            bool Livre(Rect r) { foreach (var q in ocupado) if (q.Overlaps(r)) return false; return true; }
            void Telhado(Vector3 c, float w, float d, float alt, bool emX, Color cor)
            {
                // duas águas: duas placas inclinadas se encontrando na cumeeira (eixo comprido) + cumeeira
                float meio = (emX ? d : w) / 2 + 0.12f, comp = (emX ? w : d) / 2 + 0.12f;
                float ang = Mathf.Atan2(alt, meio), lado = Mathf.Sqrt(meio * meio + alt * alt) / 2;
                var eixo = emX ? Vector3.right : Vector3.forward; var tr = emX ? Vector3.forward : Vector3.right;
                foreach (int s in new[] { -1, 1 })
                {
                    var desce = (tr * s * Mathf.Cos(ang) - Vector3.up * Mathf.Sin(ang)).normalized;
                    var centro = c + Vector3.up * alt + desce * lado;
                    var norm = Vector3.Cross(eixo, desce).normalized; if (norm.y < 0) norm = -norm;
                    o.Bloco(centro, eixo * comp, desce * lado, norm * 0.04f, s < 0 ? cor : cor * 0.9f);
                }
                o.Bloco(c + Vector3.up * (alt + 0.03f), eixo * comp, tr * 0.06f, Vector3.up * 0.05f, telha2);
            }
            for (int n = 0; n < 2400; n++)
            {
                float x = R(x0 + 2, x1 - 2), z = R(z0 + 2, z1 - 2), dist = Dist(x, z);
                if (dist < 1.0f || dist > 28f) continue;
                bool borgo = dist < 9f;
                if (!borgo && rng.NextDouble() < 0.86) continue;           // longe: casa de campo esparsa
                float w = R(1.3f, 3.2f), d = R(1.3f, 2.6f);
                var r = new Rect(x - w / 2, z - d / 2, w, d);
                if (!Livre(new Rect(r.x - (borgo ? 0.06f : 1.2f), r.y - (borgo ? 0.06f : 1.2f), r.width + (borgo ? 0.12f : 2.4f), r.height + (borgo ? 0.12f : 2.4f)))) continue;
                ocupado.Add(r);
                float y0 = Relevo(x, z) - 0.2f;
                float hMax = borgo ? Mathf.Lerp(0.55f, 2.6f, Mathf.SmoothStep(1f, 9f, dist)) : R(1.1f, 1.8f);
                float h = R(0.6f, 1f) * hMax;
                var cor = reboco[rng.Next(reboco.Length)] * R(0.95f, 1.04f);
                o.Caixa(new Vector3(x, y0 + h / 2 + 0.1f, z), new Vector3(w / 2, h / 2 + 0.1f, d / 2), cor);
                o.Caixa(new Vector3(x, y0 + 0.16f, z), new Vector3(w / 2 + 0.02f, 0.06f, d / 2 + 0.02f), pedra);   // rodapé de pedra
                bool emX = w >= d;
                Telhado(new Vector3(x, y0 + h + 0.2f, z), w, d, Mathf.Min(w, d) * 0.22f, emX, telha * R(0.92f, 1.05f));
                // janelas com persiana verde (quase todas) nas duas faces compridas
                int nj = Mathf.Max(1, Mathf.FloorToInt((emX ? w : d) / 0.8f)), andares = Mathf.Max(1, Mathf.FloorToInt(h / 0.85f));
                for (int j = 0; j < nj; j++)
                    for (int a = 0; a < andares; a++)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        float u = -(emX ? w : d) / 2 + (j + 0.5f) * (emX ? w : d) / nj, jy = y0 + 0.5f + a * 0.85f;
                        if (jy + 0.25f > y0 + h + 0.1f) continue;
                        foreach (int sd in new[] { -1, 1 })
                        {
                            var p = emX ? new Vector3(x + u, jy, z + sd * (d / 2 + 0.01f)) : new Vector3(x + sd * (w / 2 + 0.01f), jy, z + u);
                            var meia = emX ? new Vector3(0.11f, 0.17f, 0.02f) : new Vector3(0.02f, 0.17f, 0.11f);
                            o.Caixa(p, meia, rng.NextDouble() < 0.7 ? persiana : escuro);
                        }
                    }
            }

            // campanário de tijolo com janela do sino em arco e telhado em pirâmide (referência de longe)
            {
                float mx = W + 7f, mz = -D * 0.3f, yb = Relevo(mx, mz) - 0.1f, lado = 1.3f, alt = 7.5f;
                var tij = Hex(0xC9774E);
                o.Caixa(new Vector3(mx, yb + alt / 2, mz), new Vector3(lado / 2, alt / 2, lado / 2), tij);
                for (int f = 0; f < 4; f++)
                {
                    float a = f * Mathf.PI / 2; var nrm = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    o.Caixa(new Vector3(mx, yb + alt - 1.0f, mz) + nrm * (lado / 2 + 0.01f), new Vector3(0.24f, 0.45f, 0.24f) - new Vector3(Mathf.Abs(nrm.x), 0, Mathf.Abs(nrm.z)) * 0.22f, escuro);
                    o.Caixa(new Vector3(mx, yb + alt * 0.45f, mz) + nrm * (lado / 2 + 0.01f), new Vector3(0.1f, 0.22f, 0.1f) - new Vector3(Mathf.Abs(nrm.x), 0, Mathf.Abs(nrm.z)) * 0.09f, escuro);
                }
                o.Caixa(new Vector3(mx, yb + alt + 0.06f, mz), new Vector3(lado / 2 + 0.08f, 0.06f, lado / 2 + 0.08f), pedra);
                for (int f = 0; f < 4; f++)   // pirâmide: 4 placas inclinadas
                {
                    float a = f * Mathf.PI / 2; var nrm = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var t = Vector3.Cross(Vector3.up, nrm);
                    var sobe = (-nrm * (lado / 2) + Vector3.up * 1.4f); var c = new Vector3(mx, yb + alt + 0.12f, mz) + nrm * (lado / 2) + sobe / 2;
                    o.Bloco(c, t * (lado / 2) * 0.55f, sobe / 2, Vector3.Cross(t, sobe).normalized * 0.04f, telha);
                }
            }
            o.Montar("Borgo de fora", mat, raiz.transform);

            // ciprestes (Pine afinado e esticado) em fileira na beira dos caminhos e soltos; oliveiras (TwistedTree) no campo
            var cip = new[] { "it_Pine_1", "it_Pine_2", "it_Pine_3" }; var oli = new[] { "it_TwistedTree_1", "it_TwistedTree_3" };
            var arv = new GameObject("Ciprestes e oliveiras"); arv.transform.SetParent(raiz.transform, false);
            int na = 0;
            void Arvore(string nome, Vector3 p, Vector3 esc, float giro)
            {
                var pf = Resources.Load<GameObject>("Italia/" + nome); if (pf == null) return;
                var g = Object.Instantiate(pf, arv.transform);
                g.transform.SetPositionAndRotation(p, Quaternion.Euler(0, giro, 0));
                g.transform.localScale = Vector3.Scale(pf.transform.localScale, esc);
                na++;
            }
            for (int n = 0; n < 900; n++)
            {
                float x = R(x0 + 1, x1 - 1), z = R(z0 + 1, z1 - 1), dist = Dist(x, z);
                if (dist < 2.5f || dist > 30f) continue;
                var r = new Rect(x - 0.25f, z - 0.25f, 0.5f, 0.5f); if (!Livre(r)) continue;
                bool cipreste = rng.NextDouble() < 0.55;
                ocupado.Add(r);
                float s = R(0.8f, 1.15f) * (dist < 6 ? 0.8f : 1f);
                if (cipreste) Arvore(cip[rng.Next(cip.Length)], new Vector3(x, Relevo(x, z) - 0.05f, z), new Vector3(0.11f, 0.3f, 0.11f) * s, R(0, 360));
                else Arvore(oli[rng.Next(oli.Length)], new Vector3(x, Relevo(x, z) - 0.05f, z), Vector3.one * 0.13f * s, R(0, 360));
            }
            Debug.Log($"[ArredoresItalia] {na} árvores");
            return raiz;
        }
    }
}
