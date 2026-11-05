using System.Collections.Generic;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// Montador de malha de maquete com cor por vértice (pro material CSM/Maquete): bloco (paralelepípedo com faces
    /// chapadas), cilindro/cone e bola, tudo em coordenadas Unity, numa malha só (milhares de peças viram 1 objeto).
    /// Usado pelos enfeites de fachada e pelo cenário de fora.
    /// </summary>
    public class Obra3D
    {
        readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
        readonly List<Color> C = new List<Color>(); readonly List<int> T = new List<int>();
        readonly bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
        public int Vertices => V.Count;

        Color L(Color c) => lin ? c.linear : c;

        void Tri(int a, int b, int c, Vector3 fora)
        {
            var f = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(f, fora) >= 0) { T.Add(a); T.Add(b); T.Add(c); } else { T.Add(a); T.Add(c); T.Add(b); }
        }

        /// <summary>Paralelepípedo: centro c e semi-eixos a, b, d (podem ser inclinados).</summary>
        public void Bloco(Vector3 c, Vector3 a, Vector3 b, Vector3 d, Color cor)
        {
            var lc = L(cor);
            void Face(Vector3 n, Vector3 e1, Vector3 e2)
            {
                int k = V.Count; var o = c + n;
                V.Add(o - e1 - e2); V.Add(o + e1 - e2); V.Add(o + e1 + e2); V.Add(o - e1 + e2);
                var nn = n.normalized;
                // um pouco mais escuro embaixo (oclusão de desenho)
                var cc = nn.y < -0.5f ? lc * 0.8f : lc; cc.a = 1;
                for (int i = 0; i < 4; i++) { N.Add(nn); C.Add(cc); }
                Tri(k, k + 1, k + 2, nn); Tri(k, k + 2, k + 3, nn);
            }
            Face(a, b, d); Face(-a, b, d); Face(b, a, d); Face(-b, a, d); Face(d, a, b); Face(-d, a, b);
        }

        public void Caixa(Vector3 c, Vector3 meia, Color cor) => Bloco(c, new Vector3(meia.x, 0, 0), new Vector3(0, meia.y, 0), new Vector3(0, 0, meia.z), cor);

        /// <summary>Caixa girada (q) com meia-medida.</summary>
        public void Caixa(Vector3 c, Vector3 meia, Quaternion q, Color cor) => Bloco(c, q * new Vector3(meia.x, 0, 0), q * new Vector3(0, meia.y, 0), q * new Vector3(0, 0, meia.z), cor);

        /// <summary>Cilindro/cone de b0 até b0 + eixo, raio r0 embaixo e r1 em cima; tampas opcionais.</summary>
        public void Cilindro(Vector3 b0, Vector3 eixo, float r0, float r1, int seg, Color cor, Color? tampa = null, bool tampas = true)
        {
            var lc = L(cor); var lt = L(tampa ?? cor);
            var up = eixo.normalized; var e1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var e2 = Vector3.Cross(up, e1);
            int k0 = V.Count;
            float incl = (r0 - r1) / Mathf.Max(eixo.magnitude, 1e-4f);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg; var r = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                var n = (r + up * incl).normalized;
                V.Add(b0 + r * r0); N.Add(n); C.Add(lc * 0.86f);
                V.Add(b0 + eixo + r * r1); N.Add(n); C.Add(lc);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = k0 + i * 2;
                var fora = (V[a] + V[a + 3]) * 0.5f - (b0 + eixo * 0.5f);
                Tri(a, a + 2, a + 3, fora); Tri(a, a + 3, a + 1, fora);
            }
            if (!tampas) return;
            foreach (var (o, rr, n) in new[] { (b0, r0, -up), (b0 + eixo, r1, up) })
            {
                if (rr < 1e-4f) continue;
                int c0 = V.Count; V.Add(o); N.Add(n); C.Add(n == up ? lt : lc * 0.8f);
                for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2 / seg; V.Add(o + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * rr); N.Add(n); C.Add(n == up ? lt : lc * 0.8f); }
                for (int i = 0; i < seg; i++) Tri(c0, c0 + 1 + i, c0 + 2 + i, n);
            }
        }

        /// <summary>Bola (elipsoide) de raios r em c; de baixo mais escura.</summary>
        public void Bola(Vector3 c, Vector3 r, Color cor, int seg = 10)
        {
            var lc = L(cor); int an = seg, rn = seg / 2 + 1; int k0 = V.Count;
            for (int j = 0; j <= rn; j++)
            {
                float th = Mathf.PI * j / rn;
                for (int i = 0; i <= an; i++)
                {
                    float ph = 2 * Mathf.PI * i / an;
                    var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    V.Add(c + Vector3.Scale(u, r)); N.Add(new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized);
                    var cc = lc * Mathf.Lerp(0.74f, 1.05f, u.y * 0.5f + 0.5f); cc.a = 1; C.Add(cc);
                }
            }
            for (int j = 0; j < rn; j++)
                for (int i = 0; i < an; i++)
                {
                    int a = k0 + j * (an + 1) + i, b = a + an + 1;
                    var fora = V[a] - c;
                    Tri(a, b, a + 1, fora); Tri(a + 1, b, b + 1, fora);
                }
        }

        public GameObject Montar(string nome, Material mat, Transform pai, bool sombra = true)
        {
            var me = new Mesh { name = nome, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            me.SetVertices(V); me.SetNormals(N); me.SetColors(C); me.SetTriangles(T, 0); me.RecalculateBounds();
            var go = new GameObject(nome); go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = me;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }
    }
}
