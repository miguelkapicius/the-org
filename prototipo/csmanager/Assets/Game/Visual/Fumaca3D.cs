using System.Collections.Generic;
using UnityEngine;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Uma smoke da simulação desenhada por volume, no jeito da smoke do CS2 (FumacaVolume.shader + Fumaca.hlsl): as
    /// bolotas da SmokeVolume viram esferas macias numa fatia de uma textura 3D compartilhada (células de 4) e o shader lê
    /// ponto a ponto, com ruído de couve-flor na borda. Tiro que atravessa abre um túnel em cone (fino onde entra, largo
    /// onde sai) que abre na hora e fecha da borda pro meio — e quem olha de cima vê a fresta rala no caminho da bala; HE
    /// dentro abre uma bolha grande (rompe o topo) que volta a encher em ~3 s. Publicar() manda tudo pro shader uma vez
    /// por quadro.
    /// </summary>
    public class Fumaca3D
    {
        public const int MAX = 8, NX = 45, NY = 45, NZ = 18, FUROS = 24, HES = 4;
        // túnel de tiro: raio na entrada e quanto alarga por unidade percorrida dentro da smoke, até o teto; tempo de vida
        // = SmokeVolume.Shot da Sim (raio 2,5 na entrada, fechado em 1,5 s): o túnel na tela é o mesmo que deixa os bots
        // se verem (era 3,2 e 2,2 s: na tela ficava aberto depois que a Sim já tinha fechado)
        public const float FURO_R0 = 2.5f, FURO_ALARGA = 0.11f, FURO_RMAX = 8f, FURO_VIDA = 1.5f;
        // bolha da HE: raio cheio, quando começa a voltar e quando fechou
        public const float HE_R = 32f, HE_SEGURA = 1.0f, HE_VIDA = 3.2f;

        public readonly SmokeVolume s;
        readonly GameObject go; readonly Material mat; readonly int fatia;
        readonly List<(Vector3 a, Vector3 b, float r1, float t)> furos = new List<(Vector3, Vector3, float, float)>();
        readonly List<(Vector3 p, float t)> hes = new List<(Vector3, float)>();

        static Texture3D atlas, ruido;
        static byte[] dados;
        static readonly Fumaca3D[] slots = new Fumaca3D[MAX];
        static readonly Vector4[] vMin = new Vector4[MAX], vA = new Vector4[FUROS], vB = new Vector4[FUROS], vHe = new Vector4[HES];

        public Fumaca3D(SmokeVolume s, Transform pai, Shader sh)
        {
            this.s = s;
            Preparar();
            fatia = System.Array.IndexOf(slots, null);
            if (fatia < 0) fatia = 0;   // mais de 8 smokes ao mesmo tempo: a mais antiga perde a fatia
            if (slots[fatia] != null) slots[fatia].Soltar();
            slots[fatia] = this;
            mat = new Material(sh) { name = "smoke" }; mat.SetFloat("_Indice", fatia);
            // caixa da grade (a face da frente é onde o raio começa)
            go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "smoke";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(pai, false);
            var (mn, mx) = s.Bounds;
            var a = MalhaMapa.ParaUnity(mn.x, mn.y, mn.z); var b = MalhaMapa.ParaUnity(mx.x, mx.y, mx.z);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localScale = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = false;   // sombra: FumacaVolume ShadowCaster
            vMin[fatia] = new Vector4(mn.x, mn.y, mn.z, 1);
        }

        /// <summary>Atlas e ruído da smoke (chamado no começo da partida: a primeira smoke não trava o quadro montando).</summary>
        public static void Preparar()
        {
            if (atlas != null) return;
            dados = new byte[NX * NY * MAX * (NZ + 1)];
            atlas = new Texture3D(NX, NY, MAX * (NZ + 1), TextureFormat.R8, false)
            { name = "smoke atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            ruido = Ruido();
            Shader.SetGlobalTexture("_FumAtlas", atlas);
            Shader.SetGlobalTexture("_FumRuido", ruido);
            Shader.SetGlobalVector("_FumTam", new Vector4(NX * SmokeVolume.C, NY * SmokeVolume.C, NZ * SmokeVolume.C, 0));
            Shader.SetGlobalFloat("_FumEsc", MalhaMapa.ESC);
        }

        /// <summary>Ruído de couve-flor que repete (32³): 3 oitavas de ruído de valor com a grade dando a volta.</summary>
        static Texture3D Ruido()
        {
            const int N = 32;
            var rng = new System.Random(7);
            var px = new byte[N * N * N];
            float[][] grades = new float[3][]; int[] f = { 4, 8, 16 }; float[] peso = { 0.55f, 0.3f, 0.15f };
            for (int o = 0; o < 3; o++) { grades[o] = new float[f[o] * f[o] * f[o]]; for (int i = 0; i < grades[o].Length; i++) grades[o][i] = (float)rng.NextDouble(); }
            for (int z = 0; z < N; z++)
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float v = 0;
                        for (int o = 0; o < 3; o++)
                        {
                            int F = f[o]; float e = (float)F / N;
                            float gx = x * e, gy = y * e, gz = z * e;
                            int ix = (int)gx, iy = (int)gy, iz = (int)gz;
                            float ax = S(gx - ix), ay = S(gy - iy), az = S(gz - iz);
                            float G(int i, int j, int k) => grades[o][((k % F) * F + (j % F)) * F + (i % F)];
                            float c00 = Mathf.Lerp(G(ix, iy, iz), G(ix + 1, iy, iz), ax), c10 = Mathf.Lerp(G(ix, iy + 1, iz), G(ix + 1, iy + 1, iz), ax);
                            float c01 = Mathf.Lerp(G(ix, iy, iz + 1), G(ix + 1, iy, iz + 1), ax), c11 = Mathf.Lerp(G(ix, iy + 1, iz + 1), G(ix + 1, iy + 1, iz + 1), ax);
                            v += peso[o] * Mathf.Lerp(Mathf.Lerp(c00, c10, ay), Mathf.Lerp(c01, c11, ay), az);
                        }
                        px[(z * N + y) * N + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255), 0, 255);
                    }
            var t = new Texture3D(N, N, N, TextureFormat.R8, false) { name = "smoke ruído", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            t.SetPixelData(px, 0); t.Apply(false);
            return t;
            static float S(float u) => u * u * (3 - 2 * u);
        }

        /// <summary>Bala atravessando (coordenadas do jogo): túnel do ponto onde entra na smoke até onde sai.</summary>
        /// <returns>Onde a bala sai da smoke (Unity), pro tufo de fumaça arrastado; null se não cruzou fumaça de verdade.</returns>
        public Vector3? Furo(V3 a, V3 b, float t)
        {
            var d = b - a; float L = d.Len; if (L < 1e-3f) return null;
            float ent = -1, sai = -1;
            for (float u = 0; u <= L; u += 1.5f)
                if (s.Sample(a + d * (u / L), t) > 0.05f) { if (ent < 0) ent = u; sai = u; }
            if (ent < 0) return null;
            // além da borda desenhada (a nuvem é maior que a densidade 0,05 da Sim): o túnel abre a casca dos dois lados
            ent = Mathf.Max(0, ent - 7f); sai = Mathf.Min(L, sai + 7f);
            var pa = a + d * (ent / L); var pb = a + d * (sai / L);
            furos.Add((new Vector3(pa.x, pa.y, pa.z), new Vector3(pb.x, pb.y, pb.z), Mathf.Min(FURO_RMAX, FURO_R0 + FURO_ALARGA * (sai - ent)), t));
            var ps = a + d * (Mathf.Min(L, sai) / L);
            return MalhaMapa.ParaUnity(ps.x, ps.y, ps.z);
        }

        /// <summary>HE estourando perto (coordenadas do jogo): bolha que abre na hora e volta a encher.</summary>
        public void Explosao(V3 p, float t)
        {
            var (mn, mx) = s.Bounds;
            if (p.x < mn.x - HE_R || p.x > mx.x + HE_R || p.y < mn.y - HE_R || p.y > mx.y + HE_R) return;
            hes.Add((new Vector3(p.x, p.y, p.z + 6), t));
        }

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

        /// <summary>
        /// Bolotas da simulação (SmokeVolume.puffs: voam da lata, respiram, somem no fim) somadas como esferas macias na
        /// fatia do atlas: o contorno de couve-flor de antes, agora em volume. A força de cada bolota é a densidade da
        /// simulação no centro dela só enquanto cresce (nunca baixa): furo de bala e HE ficam pro shader, que abre do
        /// tamanho certo em vez de apagar a bolota inteira.
        /// </summary>
        public void Atualizar(float t, Camera _)
        {
            furos.RemoveAll(f => t - f.t > FURO_VIDA);
            hes.RemoveAll(h => t - h.t > HE_VIDA);
            int base0 = fatia * (NZ + 1) * NX * NY;
            System.Array.Clear(dados, base0, NZ * NX * NY);
            if (forca == null || forca.Length != s.puffs.Count) forca = new float[s.puffs.Count];
            var (mn, _) = s.Bounds;
            var centro = new Vector3(s.origin.x, s.origin.y, s.origin.z + 4);
            for (int k = 0; k < s.puffs.Count; k++)
            {
                var p = s.puffs[k];
                float r = s.PuffR(p, t); if (r <= 0.3f) continue;
                var c = s.PuffC(p, t);
                float d = s.Sample(c, t);
                float alfa = Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - 0.02f) / 0.14f));
                float voo = Mathf.Clamp01((s.age - p.t0 * 0.6f) / 0.7f);
                if (voo < 1) alfa = Mathf.Max(alfa, 0.85f * Mathf.Clamp01(r / p.r));
                forca[k] = Mathf.Max(forca[k], alfa);
                if (forca[k] < 0.02f) continue;
                var q = centro + (new Vector3(c.x, c.y, c.z) - centro) * ESCALA;
                float R = r * RAIO * (0.7f + 0.7f * Mathf.Repeat(p.ph * 0.37f, 1f));   // lóbulos grandes e pequenos
                Esfera(base0, q - new Vector3(mn.x, mn.y, mn.z), R, forca[k]);
            }
        }
        // a bolota da simulação vira esfera deste tamanho (raio que aparece ~0,78 disso): mesmo tamanho dos lóbulos de antes
        const float RAIO = 1.85f, ESCALA = 1.05f;
        float[] forca;

        /// <summary>Esfera macia (cheia até 55% do raio, some na borda) somada por máximo na fatia; p relativo ao canto.</summary>
        void Esfera(int base0, Vector3 p, float R, float f)
        {
            int C = SmokeVolume.C;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - R) / C)), x1 = Mathf.Min(NX - 1, Mathf.FloorToInt((p.x + R) / C));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((p.y - R) / C)), y1 = Mathf.Min(NY - 1, Mathf.FloorToInt((p.y + R) / C));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((p.z - R) / C)), z1 = Mathf.Min(NZ - 1, Mathf.FloorToInt((p.z + R) / C));
            float inv = 1f / R, R2 = R * R;
            for (int z = z0; z <= z1; z++)
            {
                float dz = (z + 0.5f) * C - p.z;
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f) * C - p.y, dyz = dy * dy + dz * dz;
                    if (dyz >= R2) continue;
                    int linha = base0 + (z * NY + y) * NX;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x + 0.5f) * C - p.x, d2 = dx * dx + dyz;
                        if (d2 >= R2) continue;
                        float v = f * Mathf.Clamp01((1 - Mathf.Sqrt(d2) * inv) / 0.45f);
                        byte b = (byte)(v * 255);
                        if (b > dados[linha + x]) dados[linha + x] = b;
                    }
                }
            }
        }

        /// <summary>Atlas, túneis e bolhas de todas as smokes pro shader (uma vez por quadro, depois de Atualizar).</summary>
        public static void Publicar(float t)
        {
            Preparar();
            int nf = 0, nh = 0;
            foreach (var f in slots)
            {
                if (f == null) continue;
                foreach (var (a, b, r1, t0) in f.furos)
                {
                    if (nf >= FUROS) break;
                    float idade = t - t0;
                    // abre na hora (~0,06 s), segura um pouco e fecha da borda pro meio
                    float k = Mathf.SmoothStep(0, 1, idade / 0.06f) * Mathf.Pow(1 - Mathf.SmoothStep(0, 1, (idade - 0.3f) / (FURO_VIDA - 0.3f)), 0.7f);
                    vA[nf] = new Vector4(a.x, a.y, a.z, FURO_R0 * k); vB[nf] = new Vector4(b.x, b.y, b.z, r1 * k); nf++;
                }
                foreach (var (p, t0) in f.hes)
                {
                    if (nh >= HES) break;
                    float idade = t - t0;
                    float k = Mathf.SmoothStep(0, 1, idade / 0.12f) * (1 - Mathf.SmoothStep(0, 1, (idade - HE_SEGURA) / (HE_VIDA - HE_SEGURA)));
                    vHe[nh++] = new Vector4(p.x, p.y, p.z, HE_R * k);
                }
            }
            for (int i = nf; i < FUROS; i++) vA[i] = vB[i] = Vector4.zero;
            for (int i = nh; i < HES; i++) vHe[i] = Vector4.zero;
            Shader.SetGlobalVectorArray("_FumMin", vMin);
            Shader.SetGlobalVectorArray("_FuroA", vA); Shader.SetGlobalVectorArray("_FuroB", vB);
            Shader.SetGlobalVectorArray("_HeP", vHe);
            Shader.SetGlobalFloat("_FumT", t);
            atlas.SetPixelData(dados, 0); atlas.Apply(false);
        }

        void Soltar()
        {
            if (slots[fatia] != this) return;
            slots[fatia] = null; vMin[fatia] = Vector4.zero;
            System.Array.Clear(dados, fatia * (NZ + 1) * NX * NY, NZ * NX * NY);
        }

        public void Apagar()
        {
            Soltar();
            if (Application.isPlaying) { Object.Destroy(go); Object.Destroy(mat); }
            else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }
        }
    }
}
