using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Caixas de madeira do mapa como objeto de verdade (no borrão do mapa elas viravam "saco de areia"): os voxels das
    /// coberturas "crate"/"crate_top" saem da malha borrada e cada caixa vira um caixote de quina viva com as MESMAS
    /// medidas da colisão (mesma decomposição do MapVoxels: pilha alta = caixa grande + caixa menor em cima) — tábuas com
    /// fresta escura, moldura escura nas 12 arestas, travessa diagonal nas laterais e tampa de tábuas. Cor das madeiras
    /// da Paleta3D (10 clara, 11 escura) com variação por tábua e por caixa. Só desenho: navmesh e colisão não mudam.
    /// </summary>
    public static class Caixas3D
    {
        struct Caixa { public int x0, x1, y0, y1, z0, z1; }

        static List<Caixa> Lista()
        {
            var l = new List<Caixa>();
            foreach (var c in MapData.covers)
            {
                if (CSM.View.MapVoxels.Tipo(c.kind) != "crate" && c.kind != "crate_top") continue;
                if (Brasa3D.Cuida(c.kind)) continue;   // na Brasa a caixa é o caixote do Survival Kit (Brasa3D)
                int f = MapData.walk[c.r.x0, c.r.y0] ? MapData.floor[c.r.x0, c.r.y0] : MapData.GZ;
                int z1 = f + c.h;
                if (c.kind == "crate_top") l.Add(new Caixa { x0 = c.r.x0, x1 = c.r.x1, y0 = c.r.y0, y1 = c.r.y1, z0 = f + 16, z1 = z1 });
                else if (c.h > 18)
                {
                    l.Add(new Caixa { x0 = c.r.x0, x1 = c.r.x1, y0 = c.r.y0, y1 = c.r.y1, z0 = f, z1 = f + 16 });
                    int ix0 = c.r.x0 + 2, ix1 = c.r.x1 - 3, iy0 = c.r.y0 + 3, iy1 = c.r.y1 - 2;
                    if (ix1 - ix0 >= 4 && iy1 - iy0 >= 4) l.Add(new Caixa { x0 = ix0, x1 = ix1, y0 = iy0, y1 = iy1, z0 = f + 16, z1 = Mathf.Min(z1, f + 30) });
                }
                else l.Add(new Caixa { x0 = c.r.x0, x1 = c.r.x1, y0 = c.r.y0, y1 = c.r.y1, z0 = f, z1 = z1 });
            }
            return l;
        }

        /// <summary>Tira das voxels (cópia) a madeira das caixas, pra malha borrada não desenhar elas.</summary>
        public static byte[] SemCaixas(byte[] G)
        {
            var g = (byte[])G.Clone();
            int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ;
            foreach (var c in Lista())
                for (int x = c.x0; x < c.x1; x++)
                    for (int y = c.y0; y < c.y1; y++)
                        for (int z = c.z0; z < c.z1 && z < NZ; z++)
                        {
                            if (x < 0 || y < 0 || x >= MapData.NX || y >= NY || z < 0) continue;
                            int i = (x * NY + y) * NZ + z; if (g[i] == 10 || g[i] == 11) g[i] = 0;
                        }
            return g;
        }

        // ------------------------------------------------------------------ malha (coordenadas do jogo -> Unity)
        static readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
        static readonly List<Color> C = new List<Color>(); static readonly List<int> T = new List<int>();
        static Vector3 U(Vector3 g) => MalhaMapa.ParaUnity(g.x, g.y, g.z);
        static Vector3 Un(Vector3 n) => new Vector3(n.x, n.z, -n.y);

        /// <summary>Paralelepípedo com centro c e semi-eixos a, b, d (vetores do jogo), faces chapadas.</summary>
        static void Bloco(Vector3 c, Vector3 a, Vector3 b, Vector3 d, Color cor)
        {
            void Face(Vector3 n, Vector3 e1, Vector3 e2)
            {
                int k = V.Count; var o = c + n;
                V.Add(U(o - e1 - e2)); V.Add(U(o + e1 - e2)); V.Add(U(o + e1 + e2)); V.Add(U(o - e1 + e2));
                var nn = Un(n.normalized);
                for (int i = 0; i < 4; i++) { N.Add(nn); C.Add(cor); }
                // orientação: a face tem que olhar pra fora (troca de mão jogo -> Unity; igual ao Quad da MalhaMapa)
                var f = Vector3.Cross(V[k + 1] - V[k], V[k + 2] - V[k]);
                if (Vector3.Dot(f, nn) < 0) { T.Add(k); T.Add(k + 2); T.Add(k + 1); T.Add(k); T.Add(k + 3); T.Add(k + 2); }
                else { T.Add(k); T.Add(k + 1); T.Add(k + 2); T.Add(k); T.Add(k + 2); T.Add(k + 3); }
            }
            Face(a, b, d); Face(-a, b, d); Face(b, a, d); Face(-b, a, d); Face(d, a, b); Face(-d, a, b);
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);

        static void UmaCaixa(Caixa cx, System.Random rng, bool linear)
        {
            var clara = Paleta3D.Vertice(10, linear); var escura = Paleta3D.Vertice(11, linear);
            float tomCaixa = 0.9f + 0.2f * (float)rng.NextDouble();
            float x0 = cx.x0, x1 = cx.x1, y0 = cx.y0, y1 = cx.y1, z0 = cx.z0, z1 = cx.z1;
            var centro = new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
            float hx = (x1 - x0) / 2, hy = (y1 - y0) / 2, hz = (z1 - z0) / 2;
            const float MOLD = 1.5f, SAI = 0.35f, FRESTA = 0.28f;
            // miolo escuro (aparece nas frestas entre as tábuas)
            Bloco(centro, new Vector3(hx - 0.5f, 0, 0), new Vector3(0, hy - 0.5f, 0), new Vector3(0, 0, hz - 0.3f), Mul(escura, 0.55f * tomCaixa));
            // tábuas das 4 laterais (deitadas) e da tampa
            float Tom() => tomCaixa * (0.88f + 0.22f * (float)rng.NextDouble());
            foreach (var (eixo, meia, outro) in new[] { (Vector3.right, hx, hy), (Vector3.up, hy, hx) })
            {
                var lado = eixo == Vector3.right ? Vector3.up : Vector3.right;
                for (int s = -1; s <= 1; s += 2)
                {
                    int nT = Mathf.Max(2, Mathf.RoundToInt(hz * 2 / 4f));
                    float alt = (hz * 2 - 2 * MOLD) / nT;
                    for (int i = 0; i < nT; i++)
                    {
                        float zc = z0 + MOLD + alt * (i + 0.5f);
                        var c = new Vector3(centro.x, centro.y, zc) + eixo * s * (meia - 0.35f);
                        Bloco(c, eixo * 0.35f, lado * (outro - MOLD), new Vector3(0, 0, alt / 2 - FRESTA / 2), Mul(clara, Tom()));
                    }
                    // travessa diagonal (do canto de baixo ao de cima), um pouco pra fora
                    var p0 = new Vector3(centro.x, centro.y, z0 + MOLD) + eixo * s * (meia + SAI * 0.4f) - lado * (outro - MOLD);
                    var p1 = new Vector3(centro.x, centro.y, z1 - MOLD) + eixo * s * (meia + SAI * 0.4f) + lado * (outro - MOLD);
                    var dir = (p1 - p0); float L = dir.magnitude; dir /= L;
                    var larg = Vector3.Cross(eixo, dir).normalized;
                    Bloco((p0 + p1) / 2, dir * (L / 2), larg * (MOLD * 0.5f), eixo * 0.35f, Mul(escura, 0.95f * tomCaixa));
                }
            }
            {   // tampa: tábuas ao longo do lado maior
                bool aoLongoX = hx >= hy; var ax = aoLongoX ? Vector3.right : Vector3.up; var tr = aoLongoX ? Vector3.up : Vector3.right;
                float comp = aoLongoX ? hx : hy, larg = aoLongoX ? hy : hx;
                int nT = Mathf.Max(3, Mathf.RoundToInt(larg * 2 / 4.5f)); float w = (larg * 2 - 2 * MOLD) / nT;
                for (int i = 0; i < nT; i++)
                {
                    var c = new Vector3(centro.x, centro.y, z1 - 0.35f) + tr * (-larg + MOLD + w * (i + 0.5f));
                    Bloco(c, ax * (comp - MOLD), tr * (w / 2 - FRESTA / 2), new Vector3(0, 0, 0.35f), Mul(clara, Tom() * 1.04f));
                }
            }
            // moldura: 12 arestas escuras, saindo um pouco das faces
            float m = MOLD / 2 + SAI * 0.5f;
            foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            {
                Bloco(centro + new Vector3(sx * (hx - MOLD / 2 + SAI * 0.5f), sy * (hy - MOLD / 2 + SAI * 0.5f), 0), new Vector3(m, 0, 0), new Vector3(0, m, 0), new Vector3(0, 0, hz + 0.05f), Mul(escura, tomCaixa));
                Bloco(centro + new Vector3(0, sy * (hy - MOLD / 2 + SAI * 0.5f), sx * (hz - MOLD / 2 + 0.02f)), new Vector3(hx + SAI * 0.5f, 0, 0), new Vector3(0, m, 0), new Vector3(0, 0, m), Mul(escura, tomCaixa));
                Bloco(centro + new Vector3(sx * (hx - MOLD / 2 + SAI * 0.5f), 0, sy * (hz - MOLD / 2 + 0.02f)), new Vector3(m, 0, 0), new Vector3(0, hy + SAI * 0.5f, 0), new Vector3(0, 0, m), Mul(escura, tomCaixa));
            }
        }

        /// <summary>Monta as caixas (uma malha só) embaixo de pai.</summary>
        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            V.Clear(); N.Clear(); C.Clear(); T.Clear();
            var rng = new System.Random(77);
            foreach (var c in Lista()) UmaCaixa(c, rng, linear);
            var me = new Mesh { name = "caixas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            me.SetVertices(V); me.SetNormals(N); me.SetColors(C); me.SetTriangles(T, 0); me.RecalculateBounds();
            var go = new GameObject("Caixas de madeira"); go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = me;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }
    }
}
