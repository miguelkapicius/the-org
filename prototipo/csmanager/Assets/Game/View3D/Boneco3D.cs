using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// Boneco 3D (CT/TR) exportado do Blender por Tools/sprites/exportar3d.py: uma peça por Transform, cada animação
    /// é a matriz de cada peça por quadro (30 qps) e o Boneco3D toca interpolando (posição/escala lineares, rotação
    /// esférica). Lê Logs/bonecos3d/boneco_&lt;time&gt;.json/.bin relativos ao projeto (como os sprites do 2D).
    /// Blender: X frente, Y esquerda, Z cima -> Unity: X direita, Y cima, Z frente.
    /// </summary>
    public class Boneco3D : MonoBehaviour
    {
        [Serializable] class PecaJ { public string nome; public int nv, v, n, c; public bool brilho; }
        [Serializable] class AnimJ { public string nome; public int n, q; public bool loop; }
        [Serializable] class Cabecalho { public string time; public int fps; public PecaJ[] pecas; public AnimJ[] anims; }

        public class Anim
        {
            public string nome; public int n; public bool loop;
            public Vector3[] pos, esc; public Quaternion[] rot; public bool[] vis;   // [quadro * peças + peça]
            public float Duracao(int fps) => Mathf.Max(1, loop ? n : n - 1) / (float)fps;
        }

        public class Modelo
        {
            public string time; public int fps; public string[] nomes; public Mesh[] malhas; public bool[] brilho;
            public Dictionary<string, Anim> anims = new Dictionary<string, Anim>();
        }

        public static string PASTA = "Logs/bonecos3d";
        /// <summary>Tamanho do boneco no mapa (o sprite do 2D usava 1,07: 1 unidade do Blender = 25 do jogo).</summary>
        public const float ESCALA = 1.07f;
        /// <summary>Camada dos bonecos (o renderer desenha a silhueta deles atrás de parede: Visual3D.Silhuetas).</summary>
        public const string CAMADA = "Bonecos";
        public static readonly Color COR_CT = new Color(0.43f, 0.71f, 1f, 0.9f), COR_TR = new Color(0.95f, 0.67f, 0.28f, 0.9f);
        static readonly Dictionary<string, Modelo> cache = new Dictionary<string, Modelo>();
        public static Material matCorpo, matBrilho;

        static Vector3 C(float x, float y, float z) => new Vector3(-y, z, x);

        /// <summary>Cor do Blender (linear, feita pro sprite de desenho escuro) -> tinta de brinquedo: mais clara e viva,
        /// com piso de claridade (preto vira grafite, senão o boneco some no chão claro de longe).</summary>
        static Color Brinquedo(Color lin)
        {
            Color.RGBToHSV(lin.gamma, out float h, out float s, out float v);
            v = Mathf.Clamp01(Mathf.Max(v * 1.2f, 0.2f)); s = Mathf.Clamp01(s * 1.15f);
            return Color.HSVToRGB(h, s, v).linear;
        }

        public static Modelo Carregar(string time)
        {
            if (cache.TryGetValue(time, out var m)) return m;
            var h = JsonUtility.FromJson<Cabecalho>(File.ReadAllText(Path.Combine(PASTA, $"boneco_{time}.json")));
            var raw = File.ReadAllBytes(Path.Combine(PASTA, $"boneco_{time}.bin"));
            var f = new float[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, f, 0, raw.Length);
            m = new Modelo { time = h.time, fps = h.fps };
            int P = h.pecas.Length;
            m.nomes = new string[P]; m.malhas = new Mesh[P]; m.brilho = new bool[P];
            for (int p = 0; p < P; p++)
            {
                var pj = h.pecas[p]; int nv = pj.nv;
                var V = new Vector3[nv]; var N = new Vector3[nv]; var Cs = new Color[nv]; var T = new int[nv];
                for (int i = 0; i < nv; i++)
                {
                    V[i] = C(f[pj.v + 3 * i], f[pj.v + 3 * i + 1], f[pj.v + 3 * i + 2]);
                    N[i] = C(f[pj.n + 3 * i], f[pj.n + 3 * i + 1], f[pj.n + 3 * i + 2]);
                    Cs[i] = Brinquedo(new Color(f[pj.c + 3 * i], f[pj.c + 3 * i + 1], f[pj.c + 3 * i + 2]));
                }
                // troca de mão (Blender destro -> Unity canhoto): inverte a ordem de cada triângulo
                for (int i = 0; i < nv; i += 3) { T[i] = i; T[i + 1] = i + 2; T[i + 2] = i + 1; }
                var me = new Mesh { name = pj.nome };
                if (nv > 65535) me.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                me.vertices = V; me.normals = N; me.colors = Cs; me.triangles = T;
                me.RecalculateBounds();
                m.nomes[p] = pj.nome; m.malhas[p] = me; m.brilho[p] = pj.brilho;
            }
            foreach (var aj in h.anims)
            {
                var a = new Anim { nome = aj.nome, n = aj.n, loop = aj.loop };
                a.pos = new Vector3[aj.n * P]; a.esc = new Vector3[aj.n * P]; a.rot = new Quaternion[aj.n * P]; a.vis = new bool[aj.n * P];
                for (int q = 0; q < aj.n; q++)
                    for (int p = 0; p < P; p++)
                    {
                        int o = aj.q + (q * P + p) * 12, k = q * P + p;
                        // linhas da 3x4 do Blender; M' = C M Cᵀ
                        float m00 = f[o], m01 = f[o + 1], m02 = f[o + 2], m03 = f[o + 3];
                        float m10 = f[o + 4], m11 = f[o + 5], m12 = f[o + 6], m13 = f[o + 7];
                        float m20 = f[o + 8], m21 = f[o + 9], m22 = f[o + 10], m23 = f[o + 11];
                        if (m00 == 0 && m11 == 0 && m22 == 0 && m01 == 0 && m10 == 0) { a.vis[k] = false; a.esc[k] = Vector3.zero; a.rot[k] = Quaternion.identity; continue; }
                        var M = new Matrix4x4();
                        // C = [[0,-1,0],[0,0,1],[1,0,0]]
                        M.m00 = m11; M.m01 = -m12; M.m02 = -m10;
                        M.m10 = -m21; M.m11 = m22; M.m12 = m20;
                        M.m20 = -m01; M.m21 = m02; M.m22 = m00;
                        M.m03 = -m13; M.m13 = m23; M.m23 = m03; M.m33 = 1;
                        a.vis[k] = true; a.pos[k] = M.GetColumn(3);
                        a.esc[k] = M.lossyScale; a.rot[k] = M.rotation;
                    }
                m.anims[a.nome] = a;
            }
            cache[time] = m;
            return m;
        }

        public static void GarantirMateriais()
        {
            if (matCorpo == null)
            {
                matCorpo = new Material(Shader.Find("CSM/Maquete")) { name = "Maquete boneco" };
                matCorpo.SetFloat("_Smoothness", 0.42f); matCorpo.SetFloat("_Variacao", 0.03f); matCorpo.SetFloat("_Borda", 0.35f);
            }
            if (matBrilho == null) matBrilho = new Material(Shader.Find("CSM/Brilho")) { name = "Brilho boneco" };
        }

        // ------------------------------------------------------------------ instância
        public Modelo modelo; Transform[] pecas; Renderer[] rends; bool[] perna;
        public string animAtual; public float tempo, uAtual;
        /// <summary>Giro das pernas em relação ao tronco (graus, + = horário visto de cima): as pernas correm pra onde o
        /// boneco anda e o tronco/arma ficam na mira (strafe, andar de costas).</summary>
        public float yawPernas;
        /// <summary>Esconde a arma da mão (morreu: a arma vira objeto solto caindo no chão).</summary>
        public bool semArma; HashSet<int> pecaArma;
        /// <summary>Camada das pernas: com uma ação de tronco (recarga, tiro, sacar, granada) andando, as pernas tocam
        /// o passo dessa animação (fase pernaU) e o resto do corpo a ação.</summary>
        public string pernaAnim; public float pernaU;
        /// <summary>Desce o corpo de cima (agachado atirando/recarregando: a ação é de pé, as pernas agachadas).</summary>
        public float troncoY;
        /// <summary>Mistura parcial no corpo de cima (meio cego: um pouco da pose de cego por cima do que estiver fazendo).</summary>
        public string misturaAnim; public float misturaU, misturaW;
        /// <summary>Branco do cego (0..1), por cima do piscar do golpe.</summary>
        public float brancoCego;
        float respT, respFase;   // fase sorteada no Criar (Random não pode no construtor do MonoBehaviour)
        // mistura entre animações: pose de onde saiu (congelada na troca) -> pose nova, em fadeDur segundos
        Vector3[] rPos, rEsc, dPos, dEsc; Quaternion[] rRot, dRot; bool[] rVis, dVis;
        float fadeT = 1, fadeDur = 0.15f; bool temPose;
        static readonly string[] PERNAS = { "coxa", "canela", "bota", "joelheira" };

        public static Boneco3D Criar(string time, Transform pai = null)
        {
            GarantirMateriais();
            var m = Carregar(time);
            var go = new GameObject("Boneco " + time);
            if (pai) go.transform.SetParent(pai, false);
            var b = go.AddComponent<Boneco3D>(); b.modelo = m;
            go.transform.localScale = Vector3.one * ESCALA;
            b.respFase = UnityEngine.Random.value * 6.28f;
            int P = m.malhas.Length;
            b.pecas = new Transform[P]; b.rends = new Renderer[P];
            int camada = LayerMask.NameToLayer(CAMADA);
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_CorTime", time == "ct" ? COR_CT : COR_TR);
            for (int p = 0; p < P; p++)
            {
                var pg = new GameObject(m.nomes[p]);
                if (camada >= 0 && !m.brilho[p]) pg.layer = camada;
                pg.transform.SetParent(go.transform, false);
                pg.AddComponent<MeshFilter>().sharedMesh = m.malhas[p];
                var r = pg.AddComponent<MeshRenderer>();
                r.sharedMaterial = m.brilho[p] ? matBrilho : matCorpo;
                r.shadowCastingMode = m.brilho[p] ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                if (!m.brilho[p]) r.SetPropertyBlock(mpb);
                b.pecas[p] = pg.transform; b.rends[p] = r;
            }
            b.perna = new bool[P];
            for (int p = 0; p < P; p++)
            {
                string n = m.nomes[p].Substring(2);   // tira o "ct"/"tr"
                foreach (var k in PERNAS) if (n.StartsWith(k)) b.perna[p] = true;
            }
            b.corSil = time == "ct" ? COR_CT : COR_TR;
            b.CriarAnel();
            b.rPos = new Vector3[P]; b.rEsc = new Vector3[P]; b.rRot = new Quaternion[P]; b.rVis = new bool[P];
            b.dPos = new Vector3[P]; b.dEsc = new Vector3[P]; b.dRot = new Quaternion[P]; b.dVis = new bool[P];
            return b;
        }

        public bool Tem(string anim) => modelo.anims.ContainsKey(anim);

        // ------------------------------------------------------------------ mira pra cima/baixo (aditiva)
        /// <summary>Inclinação da mira em graus (+ = cima, até ±25) com a arma da vez: soma a diferença entre a pose
        /// pitch_cima/baixo e a mira reta no corpo de cima, por cima de qualquer animação (andar, tiro, recarga).</summary>
        public float pitch; public string armaPitch;
        static readonly Dictionary<string, (Vector3[] t, Quaternion[] q)> deltas = new Dictionary<string, (Vector3[], Quaternion[])>();
        (Vector3[] t, Quaternion[] q) Delta(string arma, bool cima)
        {
            string k = modelo.time + (cima ? "+" : "-") + arma;
            if (deltas.TryGetValue(k, out var d)) return d;
            d = (null, null);
            if (modelo.anims.TryGetValue("mira_" + arma, out var b) && modelo.anims.TryGetValue((cima ? "pitch_cima_" : "pitch_baixo_") + arma, out var p))
            {
                int P = pecas.Length; d = (new Vector3[P], new Quaternion[P]);
                for (int i = 0; i < P; i++)
                {
                    if (!b.vis[i] || !p.vis[i]) { d.q[i] = Quaternion.identity; continue; }
                    var dq = p.rot[i] * Quaternion.Inverse(b.rot[i]);
                    d.q[i] = dq; d.t[i] = p.pos[i] - dq * b.pos[i];
                }
            }
            deltas[k] = d;
            return d;
        }

        // ------------------------------------------------------------------ anel do time, golpe e piscar
        MeshRenderer anel; static Mesh quad; static Material matAnel;
        float golpeT = 9, golpeK, flash; Color corSil; MaterialPropertyBlock mpbCorpo;

        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "disco" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 }; quad.RecalculateBounds();
            return quad;
        }

        void CriarAnel()
        {
            if (matAnel == null) matAnel = new Material(Shader.Find("CSM/Disco")) { name = "anel do time" };
            var g = new GameObject("anel"); g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0, 0.015f, 0); g.transform.localScale = Vector3.one * 0.62f;
            g.AddComponent<MeshFilter>().sharedMesh = Quad();
            anel = g.AddComponent<MeshRenderer>(); anel.sharedMaterial = matAnel; anel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mp = new MaterialPropertyBlock(); var c = modelo.time == "ct" ? COR_CT : COR_TR;
            mp.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.75f)); anel.SetPropertyBlock(mp);
        }

        /// <summary>Anel do time no chão: some na escada e no ar (ficaria flutuando).</summary>
        public bool noChao = true;

        /// <summary>Levou tiro: o tronco dá um tranco pra trás (força 0..1) e o corpo pisca branco.</summary>
        public void Golpe(float forca) { golpeT = 0; golpeK = Mathf.Clamp01(forca); }

        /// <summary>Atirou: o coice passa pro corpo (ombro empurrado pra trás, peito sobe um tiquinho, um tremor de lado).
        /// No automático os tiros somam até um teto (o corpo "segura" a rajada) e soltam rápido quando para.</summary>
        public void Disparo(float forca)
        {
            recuoK = Mathf.Min(1.4f, recuoK * Mathf.Exp(-recuoT / 0.09f) + forca);
            recuoT = 0; recuoLado = UnityEngine.Random.Range(-1f, 1f);
        }
        float recuoT = 9, recuoK, recuoLado;

        /// <summary>Peça (transform) cujo nome termina em <paramref name="sufixo"/> (ex.: "tronco").</summary>
        public Transform Peca(string sufixo)
        {
            for (int p = 0; p < pecas.Length; p++) if (modelo.nomes[p].EndsWith(sufixo)) return pecas[p];
            return null;
        }

        /// <summary>Posição no mundo da peça visível cujo nome termina em <paramref name="sufixo"/> (ex.: "fogo0").</summary>
        public bool PecaVisivel(string sufixo, out Vector3 mundo)
        {
            for (int p = 0; p < pecas.Length; p++)
                if (rends[p].enabled && modelo.nomes[p].EndsWith(sufixo)) { mundo = pecas[p].position; return true; }
            mundo = default; return false;
        }

        void AtualizarCorpo()
        {
            mpbCorpo ??= new MaterialPropertyBlock();
            mpbCorpo.SetColor("_CorTime", corSil); mpbCorpo.SetFloat("_Flash", flash);
            for (int p = 0; p < rends.Length; p++) if (!modelo.brilho[p]) rends[p].SetPropertyBlock(mpbCorpo);
        }

        bool silhueta = true;
        /// <summary>Liga/desliga a silhueta atrás de parede (morto não mostra).</summary>
        public void Silhueta(bool ligada)
        {
            if (anel != null) anel.enabled = ligada && noChao;
            if (ligada == silhueta) return;
            silhueta = ligada;
            corSil = modelo.time == "ct" ? COR_CT : COR_TR; if (!ligada) corSil.a = 0;
            AtualizarCorpo();
        }

        /// <summary>Quanto tempo leva a mistura ao ENTRAR numa animação (tiro entra seco, o resto assenta).</summary>
        public static float Fade(string de, string para)
        {
            if (de == null) return 0;
            if (para.StartsWith("tiro") || para.StartsWith("faca_m")) return 0.05f;
            if (de.StartsWith("tiro")) return 0.12f;
            if (para == "morrer") return 0.1f;
            if (para.StartsWith("no_ar") || para.StartsWith("faca_no_ar") || de.StartsWith("no_ar") || de.StartsWith("faca_no_ar")) return 0.1f;
            if (para.StartsWith("recarga") || para.StartsWith("sacar")) return 0.1f;
            if (para.StartsWith("agacha") || de.StartsWith("agacha")) return 0.22f;
            return 0.16f;
        }

        /// <summary>Amostra a animação no instante u (0..1) pra dPos/dRot/dEsc/dVis.</summary>
        void Amostrar(Anim a, float u)
        {
            int P = pecas.Length;
            float x = a.loop ? Mathf.Repeat(u, 1f) * a.n : Mathf.Clamp01(u) * (a.n - 1);
            int q0 = Mathf.Min((int)x, a.n - 1), q1 = a.loop ? (q0 + 1) % a.n : Mathf.Min(q0 + 1, a.n - 1);
            float w = x - q0;
            for (int p = 0; p < P; p++)
            {
                int k0 = q0 * P + p, k1 = q1 * P + p;
                bool v0 = a.vis[k0], v1 = a.vis[k1];
                dVis[p] = w < 0.5f ? v0 : v1;
                if (!dVis[p]) continue;
                if (v0 && v1)
                {
                    dPos[p] = Vector3.LerpUnclamped(a.pos[k0], a.pos[k1], w);
                    dRot[p] = Quaternion.SlerpUnclamped(a.rot[k0], a.rot[k1], w);
                    dEsc[p] = Vector3.LerpUnclamped(a.esc[k0], a.esc[k1], w);
                }
                else { int k = v0 ? k0 : k1; dPos[p] = a.pos[k]; dRot[p] = a.rot[k]; dEsc[p] = a.esc[k]; }
            }
        }

        /// <summary>Puxa o corpo de cima (não as pernas) pra outra animação com peso w.</summary>
        void Misturar(Anim a, float u, float w)
        {
            int P = pecas.Length;
            float x = a.loop ? Mathf.Repeat(u, 1f) * a.n : Mathf.Clamp01(u) * (a.n - 1);
            int q0 = Mathf.Min((int)x, a.n - 1);
            for (int p = 0; p < P; p++)
            {
                if (perna[p] || !dVis[p]) continue;
                int k = q0 * P + p; if (!a.vis[k]) continue;
                dPos[p] = Vector3.Lerp(dPos[p], a.pos[k], w); dRot[p] = Quaternion.Slerp(dRot[p], a.rot[k], w);
            }
        }

        /// <summary>Só as pernas de outra animação (camada de baixo) por cima do que Amostrar pôs.</summary>
        void AmostrarPernas(Anim a, float u)
        {
            int P = pecas.Length;
            float x = a.loop ? Mathf.Repeat(u, 1f) * a.n : Mathf.Clamp01(u) * (a.n - 1);
            int q0 = Mathf.Min((int)x, a.n - 1), q1 = a.loop ? (q0 + 1) % a.n : Mathf.Min(q0 + 1, a.n - 1);
            float w = x - q0;
            for (int p = 0; p < P; p++)
            {
                if (!perna[p]) continue;
                int k0 = q0 * P + p, k1 = q1 * P + p;
                if (!a.vis[k0] || !a.vis[k1]) continue;
                dVis[p] = true;
                dPos[p] = Vector3.LerpUnclamped(a.pos[k0], a.pos[k1], w);
                dRot[p] = Quaternion.SlerpUnclamped(a.rot[k0], a.rot[k1], w);
                dEsc[p] = Vector3.LerpUnclamped(a.esc[k0], a.esc[k1], w);
            }
        }

        /// <summary>Pose da animação no instante u (0..1 do ciclo/da ação), misturando com a anterior por dt segundos.
        /// dt = 0: sem mistura (pose seca, ex.: print).</summary>
        public void Pose(string anim, float u, float dt = 0)
        {
            if (!modelo.anims.TryGetValue(anim, out var a)) return;
            int P = pecas.Length;
            if (anim != animAtual && temPose && dt > 0)
            {
                // congela a pose mostrada agora como ponto de partida
                for (int p = 0; p < P; p++)
                {
                    var t = pecas[p]; rVis[p] = rends[p].enabled;
                    rPos[p] = t.localPosition; rRot[p] = t.localRotation; rEsc[p] = t.localScale;
                }
                fadeDur = Fade(animAtual, anim); fadeT = 0;
            }
            animAtual = anim; uAtual = u;
            fadeT += dt;
            Amostrar(a, u);
            if (pernaAnim != null && modelo.anims.TryGetValue(pernaAnim, out var ap)) AmostrarPernas(ap, pernaU);
            if (misturaAnim != null && misturaW > 0.01f && modelo.anims.TryGetValue(misturaAnim, out var am)) Misturar(am, misturaU, misturaW);
            // respirando parado (segurando ângulo não fica estátua): peito sobe e desce, ombro balança de leve
            respT += dt;
            bool parado = anim.StartsWith("mira") || anim.StartsWith("agachar") || anim == "awp_mirando" || anim == "faca_parado" || anim.StartsWith("lamentar");
            float resp = parado ? Mathf.Sin(respT * 2.2f + respFase) : 0, balanco = parado ? Mathf.Sin(respT * 0.9f + respFase * 1.7f) : 0;
            float wf = fadeDur <= 0 || dt <= 0 ? 1 : Mathf.Clamp01(fadeT / fadeDur);
            wf = wf * wf * (3 - 2 * wf);
            Quaternion giro = Quaternion.Euler(0, yawPernas, 0);
            golpeT += dt;
            float g = golpeT < 0.28f ? golpeK * Mathf.Sin(Mathf.PI * Mathf.Min(1, golpeT / 0.07f) * 0.5f) * Mathf.Pow(1 - golpeT / 0.28f, 2) : 0;
            Quaternion tranco = Quaternion.Euler(-16f * g, 0, 0); var pivo = new Vector3(0, 0.36f, 0);
            // coice no corpo: sobe rápido (15 ms) e volta em ~0,1 s; tronco gira pra trás no quadril, ombro recua
            recuoT += dt;
            float rc = recuoK * Mathf.Min(1, recuoT / 0.015f) * Mathf.Exp(-recuoT / 0.09f);
            Quaternion coice = Quaternion.Euler(-4.5f * rc, 2.2f * rc * recuoLado, 1.2f * rc * recuoLado);
            var pivoQ = new Vector3(0, 0.3f, 0); var recua = new Vector3(0, 0.004f, -0.014f) * rc;
            float fl = Mathf.Max(golpeT < 0.16f ? golpeK * (1 - golpeT / 0.16f) * 0.85f : 0, brancoCego);
            if (Mathf.Abs(fl - flash) > 0.02f || (fl == 0 && flash != 0)) { flash = fl; AtualizarCorpo(); }
            float wp = Mathf.Clamp01(Mathf.Abs(pitch) / 60f);   // pitch_cima/baixo exportadas a 60°
            (Vector3[] t, Quaternion[] q) dl = (null, null);
            if (wp > 0.01f && armaPitch != null) dl = Delta(armaPitch, pitch > 0);
            if (semArma && pecaArma == null) pecaArma = Armas3D.DeArma(modelo);
            for (int p = 0; p < P; p++)
            {
                bool vis = dVis[p] && !(semArma && pecaArma.Contains(p));
                rends[p].enabled = vis;
                if (!vis) continue;
                Vector3 pos = dPos[p], esc = dEsc[p]; Quaternion rot = dRot[p];
                if (dl.q != null && !perna[p])
                {
                    var dq = Quaternion.Slerp(Quaternion.identity, dl.q[p], wp);
                    pos = dq * pos + dl.t[p] * wp; rot = dq * rot;
                }
                if (perna[p] && yawPernas != 0) { pos = giro * pos; rot = giro * rot; }
                if (g > 0.001f && !perna[p]) { pos = pivo + tranco * (pos - pivo); rot = tranco * rot; }
                if (rc > 0.002f && !perna[p]) { pos = pivoQ + coice * (pos - pivoQ) + recua; rot = coice * rot; }
                if (!perna[p])
                {
                    if (troncoY != 0) pos.y += troncoY;
                    if (resp != 0 || balanco != 0)
                    {
                        var qr = Quaternion.Euler(0.7f * resp, 1.2f * balanco, 0);
                        pos = pivo + qr * (pos - pivo) + new Vector3(0, 0.004f * resp, 0); rot = qr * rot;
                    }
                }
                // a pose congelada já tem o giro das pernas de quando foi congelada
                if (wf < 1 && rVis[p]) { pos = Vector3.Lerp(rPos[p], pos, wf); rot = Quaternion.Slerp(rRot[p], rot, wf); esc = Vector3.Lerp(rEsc[p], esc, wf); }
                var t = pecas[p];
                t.localPosition = pos; t.localRotation = rot; t.localScale = esc;
            }
            temPose = true;
        }

        /// <summary>Toca pelo tempo em segundos (duração real exportada).</summary>
        public void Tocar(string anim, float segundos, float dt = 0)
        {
            if (!modelo.anims.TryGetValue(anim, out var a)) return;
            Pose(anim, segundos / a.Duracao(modelo.fps), dt);
        }

        public float Duracao(string anim) => modelo.anims.TryGetValue(anim, out var a) ? a.Duracao(modelo.fps) : 1;
    }
}
