using System.Collections.Generic;
using UnityEngine;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Uma smoke da simulação em partículas (FumacaParticula.shader), no jeito da smoke do CS2: as bolotas da SmokeVolume
    /// (puffs) voam da lata até o lugar de cada uma na expansão, respiram e somem no fim; cada bolota vira 3 billboards
    /// sobrepostos (silhueta de couve-flor). O alfa de cada uma é a densidade da simulação ali (SmokeVolume.Sample): a
    /// bolha da HE apaga e volta a encher sozinha; os furos de bala (Furo, do desenho) abrem um túnel que fecha em ~2 s.
    /// Ordenadas de trás pra frente a cada quadro.
    /// </summary>
    public class Fumaca3D
    {
        public readonly SmokeVolume s;
        readonly GameObject go; readonly Mesh malha; readonly Material mat;
        readonly List<(Vector3 a, Vector3 b, float t)> furos = new List<(Vector3, Vector3, float)>();
        public const float FURO_R = 3.5f, FURO_VIDA = 1.0f, ESCALA = 1.05f;   // furo de bala = o do Sim (SmokeVolume.Shot: raio 3,5, ~1 s); usuário: abria demais
        struct Part { public Vector3 p, forma; public float tam, alfa, semente, dist, tom; }
        readonly List<Part> ps = new List<Part>();
        readonly List<Vector3> V = new List<Vector3>(); readonly List<Vector2> UV = new List<Vector2>(), UV2 = new List<Vector2>();
        readonly List<Color> C = new List<Color>(); readonly List<int> T = new List<int>(); readonly List<Vector3> N = new List<Vector3>();

        public Fumaca3D(SmokeVolume s, Transform pai, Mesh _, Shader sh)
        {
            this.s = s;
            mat = new Material(sh) { name = "smoke" };
            malha = new Mesh { name = "smoke" }; malha.MarkDynamic();
            go = new GameObject("smoke"); go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Bala atravessando (coordenadas do jogo): abre um túnel que fecha sozinho.</summary>
        public void Furo(V3 a, V3 b, float t) => furos.Add((new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), t));

        public bool Cruza(V3 a, V3 b)
        {
            var (mn, mx) = s.Bounds;
            for (int i = 0; i <= 24; i++)
            {
                var p = a + (b - a) * (i / 24f);
                if (p.x > mn.x && p.x < mx.x && p.y > mn.y && p.y < mx.y && p.z > mn.z && p.z < mx.z) return true;
            }
            return false;
        }

        float FatorFuro(Vector3 q, float t, float raioPart)
        {
            float k = 1;
            foreach (var (a, b, t0) in furos)
            {
                float idade = (t - t0) / FURO_VIDA;
                float r = FURO_R * Mathf.Sqrt(Mathf.Max(0, 1 - idade)) + raioPart * 0.12f;
                if (r < 0.5f) continue;
                var ab = b - a; float u = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                float d = (q - (a + ab * u)).magnitude;
                k *= Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - r * 0.6f) / (r * 0.5f)));
            }
            return k;
        }

        public void Atualizar(float t, Camera cam)
        {
            furos.RemoveAll(f => t - f.t > FURO_VIDA);
            ps.Clear();
            var camPos = cam != null ? cam.transform.position : Vector3.zero;
            int idx = 0;
            var centro = MalhaMapa.ParaUnity(s.origin.x, s.origin.y, s.origin.z + 4);
            foreach (var p in s.puffs)
            {
                idx++;
                float r = s.PuffR(p, t); if (r <= 0.3f) continue;
                var c = s.PuffC(p, t);
                // densidade da simulação no centro da bolota (crescimento, bolha da HE, desmanche) + furo de bala
                float d = s.Sample(c, t);
                // densa: qualquer densidade de verdade já é parede (usuário: "rala"); só a borda/desmanche afina
                float alfa = Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - 0.02f) / 0.14f));
                // na expansão a bolota ainda voando já aparece (a densidade da célula chega depois)
                float voo = Mathf.Clamp01((s.age - p.t0 * 0.6f) / 0.7f);
                if (voo < 1) alfa = Mathf.Max(alfa, 0.85f * Mathf.Clamp01(r / p.r));
                alfa *= FatorFuro(new Vector3(c.x, c.y, c.z), t, r);
                if (alfa < 0.02f) continue;
                var u = MalhaMapa.ParaUnity(c.x, c.y, c.z);
                u = centro + (u - centro) * ESCALA;   // um pouco maior que a área da simulação (pedido: +5%)
                // partícula grande e macia (se funde com as vizinhas: massa contínua, sem bola aparecendo)
                float var = 0.7f + 0.7f * Mathf.Repeat(p.ph * 0.37f, 1f);   // lóbulos grandes e pequenos na silhueta
                float tam = 3.5f * ESCALA * r * MalhaMapa.ESC * var;
                float tom = 0.96f + 0.04f * Mathf.Sin(p.ph * 2.3f);
                // normal da FORMA da smoke: do centro (um pouco abaixo, achata o domo) pra partícula — a luz é da massa inteira
                var forma = (u - centro); forma.y *= 1.6f; forma = forma.sqrMagnitude > 1e-6f ? forma.normalized : Vector3.up;
                ps.Add(new Part { p = u, tam = tam, alfa = alfa, semente = (idx * 0.618f) % 1, tom = tom, forma = forma });
                // enchimento: uma segunda bolota menor, deslocada pra fora e pra cima (fecha os buracos entre as vizinhas e
                // a silhueta fica mais cheia, de couve-flor)
                var sai = forma * (0.32f * r * MalhaMapa.ESC) + Vector3.up * (0.12f * r * MalhaMapa.ESC);
                ps.Add(new Part { p = u + sai, tam = tam * 0.72f, alfa = alfa, semente = (idx * 0.618f + 0.37f) % 1, tom = tom * 1.01f, forma = forma });
            }
            for (int i = 0; i < ps.Count; i++) { var q = ps[i]; q.dist = (q.p - camPos).sqrMagnitude; ps[i] = q; }
            ps.Sort((a, b) => b.dist.CompareTo(a.dist));   // de trás pra frente
            V.Clear(); UV.Clear(); UV2.Clear(); C.Clear(); T.Clear(); N.Clear();
            var dir = cam != null ? cam.transform.right : Vector3.right; var cima = cam != null ? cam.transform.up : Vector3.up;
            bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
            foreach (var q in ps)
            {
                int b0 = V.Count; float h = q.tam * 0.5f;
                V.Add(q.p + (-dir - cima) * h); V.Add(q.p + (-dir + cima) * h); V.Add(q.p + (dir + cima) * h); V.Add(q.p + (dir - cima) * h);
                UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(0, 1)); UV.Add(new Vector2(1, 1)); UV.Add(new Vector2(1, 0));
                var cor = new Color(q.tom, q.tom, q.tom); if (lin) cor = cor.linear; cor.a = q.alfa;
                for (int k = 0; k < 4; k++) { UV2.Add(new Vector2(q.semente, 0)); C.Add(cor); N.Add(q.forma); }
                T.Add(b0); T.Add(b0 + 1); T.Add(b0 + 2); T.Add(b0); T.Add(b0 + 2); T.Add(b0 + 3);
            }
            malha.Clear();
            malha.SetVertices(V); malha.SetNormals(N); malha.SetUVs(0, UV); malha.SetUVs(1, UV2); malha.SetColors(C); malha.SetTriangles(T, 0);
            malha.RecalculateBounds();
        }

        public void Apagar()
        {
            if (Application.isPlaying) { Object.Destroy(go); Object.Destroy(malha); Object.Destroy(mat); }
            else { Object.DestroyImmediate(go); Object.DestroyImmediate(malha); Object.DestroyImmediate(mat); }
        }
    }
}
