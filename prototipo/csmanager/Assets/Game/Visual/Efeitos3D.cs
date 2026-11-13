using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Partículas e luzes do visual de maquete (só desenho; a simulação não sabe delas): clarão com luz no cano,
    /// cápsula que quica no chão, poeira/faísca no impacto e respingo no acerto, explosão da HE (bola de fogo, fumaça,
    /// anel de poeira), estouro da flash, fogo do molotov em línguas que sobem, fumacinha do fogo e poeira dos passos.
    /// Física simples: gravidade, arrasto e quique no piso do MapData.
    /// </summary>
    public class Efeitos3D
    {
        enum Forma { Bola, Caixa }
        struct P
        {
            public Vector3 p, v, escala0, escala1; public Quaternion r; public Vector3 giro;
            public float vida, idade, grav, quica, arrasto, brilho; public Color c0, c1; public Forma forma; public bool solido, parado;
        }

        readonly List<P> ps = new List<P>();
        readonly Transform raiz;
        readonly Mesh bola, caixa;
        readonly Material matNuvem, matSolido;
        readonly List<(Transform t, MeshRenderer r, MeshFilter f)> pool = new List<(Transform, MeshRenderer, MeshFilter)>();
        readonly List<(Light l, float ate, float dur, float i0)> luzes = new List<(Light, float, float, float)>();
        readonly Light[] luzFogo = new Light[8];
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly System.Random rng = new System.Random(5);
        readonly bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        float tempo;

        public Efeitos3D(Transform pai, Mesh esfera, Mesh cubo)
        {
            raiz = new GameObject("Efeitos 3D").transform; raiz.SetParent(pai, false);
            bola = esfera; caixa = cubo;
            matNuvem = new Material(Shader.Find("CSM/Nuvem")) { name = "nuvem" };
            matSolido = new Material(Shader.Find("CSM/Maquete")) { name = "fx sólido" };
            matSolido.SetFloat("_UsarVertice", 0); matSolido.SetFloat("_Smoothness", 0.7f); matSolido.SetFloat("_Variacao", 0);
            for (int i = 0; i < luzFogo.Length; i++)
            {
                var g = new GameObject("luz do fogo"); g.transform.SetParent(raiz, false);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 3.5f; l.enabled = false;
                l.shadows = LightShadows.None; luzFogo[i] = l;
            }
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Vector3 Esfera(float r) { var v = new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)); return v.normalized * r * Mathf.Sqrt(R(0, 1)); }
        Color L(Color c) => linear ? c.linear : c;

        /// <summary>Piso (altura Unity) embaixo de um ponto Unity: o mesmo topo que a granada usa (MapData.Top: piso +
        /// cobertura onde anda, altura do bloco onde não anda, fora do mapa é parede). Antes a parede "automática" (solid 0)
        /// e o caixote contavam como chão, e ragdoll/arma passavam por eles.</summary>
        public static float Piso(Vector3 u) => MapData.Top(Mathf.FloorToInt(u.x / MalhaMapa.ESC), Mathf.FloorToInt(-u.z / MalhaMapa.ESC)) * MalhaMapa.ESC;

        /// <summary>Caminho horizontal de "de" até "ate" na altura "alto" (Unity): até onde dá pra ir sem entrar em parede
        /// (piso acima de "alto"), de meio em meio voxel — parede fina não é atravessada num passo grande.</summary>
        public static Vector3 Varre(Vector3 de, Vector3 ate, float alto)
        {
            float d = new Vector2(ate.x - de.x, ate.z - de.z).magnitude;
            int n = Mathf.CeilToInt(d / (MalhaMapa.ESC * 0.5f));
            for (int s = 1; s <= n; s++)
            {
                var p = Vector3.Lerp(de, ate, s / (float)n);
                if (Piso(p) > alto) { var q = Vector3.Lerp(de, ate, (s - 1) / (float)n); q.y = ate.y; return q; }
            }
            return ate;
        }

        void Add(Vector3 p, Vector3 v, float vida, float s0, float s1, Color c0, Color c1, float grav = 0, float brilho = 0,
                 float arrasto = 1.5f, Forma forma = Forma.Bola, Vector3? esc = null, bool solido = false, float quica = 0)
        {
            var e = esc ?? Vector3.one;
            ps.Add(new P
            {
                p = p, v = v, vida = vida, escala0 = e * s0, escala1 = e * s1, c0 = L(c0), c1 = L(c1), grav = grav, brilho = brilho,
                arrasto = arrasto, forma = forma, solido = solido, quica = quica,
                r = Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)), giro = new Vector3(R(-720, 720), R(-720, 720), R(-720, 720))
            });
        }

        void Luz(Vector3 p, Color c, float intensidade, float alcance, float dur)
        {
            var g = new GameObject("luz"); g.transform.SetParent(raiz, false); g.transform.position = p;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensidade; l.range = alcance; l.shadows = LightShadows.None;
            luzes.Add((l, tempo + dur, dur, intensidade));
        }

        // ------------------------------------------------------------------ eventos
        static readonly Color AREIA = new Color(0.93f, 0.83f, 0.64f), FUMO = new Color(0.55f, 0.52f, 0.5f), LATAO = new Color(0.93f, 0.72f, 0.3f);

        /// <summary>Tiro: clarão com luz no cano, fumacinha e cápsula ejetada pra direita.</summary>
        public void Tiro(Vector3 boca, Vector3 frente, Vector3 direita, bool pistola, bool awp)
        {
            // luz do cano um pouco à frente da boca e fraca: com 6-9 colada no corpo o atirador estourava de branco a cada tiro
            Luz(boca + frente * 0.12f, new Color(1f, 0.78f, 0.45f), awp ? 2.2f : 1.4f, awp ? 1.6f : 1.1f, 0.05f);
            for (int i = 0; i < 3; i++)
                Add(boca + frente * 0.05f, frente * R(0.3f, 0.8f) + Esfera(0.25f) + Vector3.up * 0.2f, R(0.35f, 0.6f), 0.05f, 0.16f, new Color(0.9f, 0.88f, 0.85f, 0.55f), new Color(0.8f, 0.8f, 0.8f, 0f), -0.3f);
            var ej = boca - frente * (pistola ? 0.05f : 0.22f);
            Add(ej, direita * R(1.2f, 1.9f) + Vector3.up * R(1.4f, 2.2f) - frente * R(0, 0.4f), 2.6f, 1, 1, LATAO, LATAO, 9.8f, 0, 0.2f,
                Forma.Caixa, new Vector3(0.022f, 0.022f, pistola ? 0.032f : 0.05f), true, 0.35f);
        }

        static readonly Color SANGUE = new Color(0.78f, 0.07f, 0.08f), SANGUE_ESC = new Color(0.45f, 0.03f, 0.04f);
        // gota no ar com um pouco de brilho próprio: só com a luz da cena ela lia marrom (cara de terra) vista de cima
        const float BRILHO_GOTA = 0.7f;

        /// <summary>Rastro da bala (tracer do CS): risco fino e quente que corre da boca até onde ela chega.</summary>
        public void Rastro(Vector3 boca, Vector3 ate)
        {
            var d = ate - boca; float dist = d.magnitude; if (dist < 0.6f) return;
            const float VEL = 75f;
            Add(boca + d / dist * 0.3f, d / dist * VEL, Mathf.Max(0.05f, (dist - 0.3f) / VEL), 1, 1, new Color(1f, 0.9f, 0.6f, 0.75f), new Color(1f, 0.8f, 0.45f, 0.5f), 0, 3, 0,
                Forma.Caixa, new Vector3(0.012f, 0.012f, 0.7f));
        }

        /// <summary>Bala chegando: poeira + faísca na parede/chão. No corpo (pegada do CS): HS sem capacete = estouro de
        /// sangue; HS com capacete = faísca no metal e pouco sangue; tronco sem colete = respingo; com colete = abafado
        /// (pouco sangue e um tiquinho de pó do tecido).</summary>
        public void Impacto(Vector3 p, Vector3 dir, bool acerto, bool cabeca, bool protegido = false)
        {
            var volta = -dir.normalized; var vai = dir.normalized;
            if (acerto)
            {
                // (usuário: sangue "muito imperceptível" -> mais gotas, maiores, vivendo mais)
                int gotas = cabeca ? (protegido ? 8 : 36) : (protegido ? 4 : 16);   // HS sem capacete: bem mais sangue (usuário)
                float forca = protegido ? 0.6f : 1f;
                // gotas: metade volta pro atirador, metade atravessa (saída), caem com gravidade
                for (int i = 0; i < gotas; i++)
                    // HS: o sangue sobe e abre pros lados (a câmera é de cima: na linha do tiro a própria cabeça escondia)
                    Add(p, (i % 2 == 0 ? volta : vai) * R(0.5f, 1.5f) * forca + Esfera(cabeca && !protegido ? 1.1f : 0.8f) + Vector3.up * (cabeca && !protegido ? R(0.7f, 1.6f) : R(0.3f, 0.9f)), R(0.4f, 0.7f), R(0.055f, 0.1f), 0.03f,
                        SANGUE, new Color(SANGUE_ESC.r, SANGUE_ESC.g, SANGUE_ESC.b, 0.75f), 6f, BRILHO_GOTA, 1.2f);
                // névoa vermelha (no capacete/colete bem menor)
                int nevoa = protegido ? 2 : cabeca ? 10 : 5;
                for (int i = 0; i < nevoa; i++)
                    // borrifo curto e translúcido (grande e opaco virava uma "bola vermelha" em volta da cabeça)
                    Add(p + Esfera(0.04f), (vai * 0.7f + Esfera(0.5f) + Vector3.up * (cabeca ? 0.5f : 0f)) * forca, R(0.18f, 0.3f), 0.05f, protegido ? 0.12f : cabeca ? 0.3f : 0.18f,
                        new Color(SANGUE.r, SANGUE.g, SANGUE.b, cabeca ? 0.55f : 0.6f), new Color(SANGUE_ESC.r, SANGUE_ESC.g, SANGUE_ESC.b, 0f), -0.1f, 0, 3f);
                if (cabeca && !protegido)
                {   // HS sem capacete: jato saindo pelo outro lado da cabeça (lê de longe que foi na cabeça)
                    for (int i = 0; i < 10; i++)
                        Add(p + vai * 0.04f, (vai + Esfera(0.45f) + Vector3.up * 0.6f).normalized * R(1.6f, 2.8f), R(0.25f, 0.45f), R(0.035f, 0.06f), 0.015f,
                            SANGUE, new Color(SANGUE_ESC.r, SANGUE_ESC.g, SANGUE_ESC.b, 0.7f), 7f, BRILHO_GOTA, 1.2f);
                }
                if (cabeca && protegido)
                {   // capacete (CS2): faísca BRANCA no metal e um clarão rápido (usuário: "faísquinha branca")
                    Add(p, Vector3.zero, 0.06f, 0.07f, 0.24f, new Color(1, 1, 1, 1), new Color(1, 0.95f, 0.85f, 0), 0, 7);
                    for (int i = 0; i < 18; i++)
                        Add(p, (volta * 0.6f + Esfera(1f) + Vector3.up * 0.3f).normalized * R(2.2f, 4.2f), R(0.14f, 0.3f), 1, 0.5f, new Color(1f, 1f, 0.97f, 1), new Color(1f, 0.85f, 0.6f, 1), 8f, 5, 0.5f,
                            Forma.Caixa, new Vector3(0.014f, 0.014f, 0.08f));
                }
                else if (protegido)   // colete: pó do tecido
                    for (int i = 0; i < 3; i++)
                        Add(p, volta * R(0.2f, 0.5f) + Esfera(0.2f), R(0.3f, 0.5f), 0.04f, 0.12f, new Color(0.7f, 0.68f, 0.62f, 0.7f), new Color(0.7f, 0.68f, 0.62f, 0f), -0.1f);
                // (o clarão branco da HS sem capacete saiu: usuário quer só sangue)
                Respingo(p, vai, protegido, cabeca);
                return;
            }
            for (int i = 0; i < 4; i++)
                Add(p, volta * R(0.2f, 0.6f) + Esfera(0.25f) + Vector3.up * 0.15f, R(0.5f, 0.9f), 0.05f, R(0.14f, 0.22f), new Color(AREIA.r, AREIA.g, AREIA.b, 0.85f), new Color(AREIA.r, AREIA.g, AREIA.b, 0f), 0.15f);
            // faísca (usuário: "muito imperceptível"): mais riscos, mais grossos e compridos, vivendo mais, e um pontinho
            // de clarão no impacto
            Add(p + volta * 0.02f, Vector3.zero, 0.05f, 0.05f, 0.11f, new Color(1f, 0.95f, 0.75f, 1), new Color(1f, 0.7f, 0.35f, 0), 0, 6);
            for (int i = 0; i < 8; i++)
                Add(p, (volta + Esfera(0.9f)).normalized * R(1.8f, 3.6f), R(0.18f, 0.35f), 1, 0.55f, new Color(1f, 0.9f, 0.5f, 1), new Color(1f, 0.5f, 0.2f, 1), 8f, 5, 0.5f,
                    Forma.Caixa, new Vector3(0.017f, 0.017f, 0.075f));
        }

        /// <summary>Abate: além do respingo do tiro, um espirro a mais no sentido da bala (gotas grandes + manchas no chão em
        /// leque e na parede de trás, se tiver até ~1,6), que ficam até o round acabar. Sem poça.</summary>
        public void SangueMorte(Vector3 p, Vector3 dir, bool cabeca)
        {
            var vai = dir; vai.y = 0; vai = vai.sqrMagnitude > 1e-6f ? vai.normalized : Vector3.forward;
            for (int i = 0; i < (cabeca ? 22 : 14); i++)
                Add(p, (vai * R(0.8f, 2.0f) + Esfera(0.7f) + Vector3.up * R(0.4f, 1.2f)), R(0.45f, 0.75f), R(0.06f, 0.11f), 0.03f,
                    SANGUE, new Color(SANGUE_ESC.r, SANGUE_ESC.g, SANGUE_ESC.b, 0.75f), 6f, BRILHO_GOTA, 1.2f);
            Respingo(p, vai, false, cabeca, 1.6f, 1.25f);
        }

        // manchas de sangue no chão e na parede (Mancha.shader): ficam até o round acabar (usuário: "espirra mais sangue e
        // fica na parede, no ambiente que o cara morreu, até finalizar um round"); NovoRound chama LimparManchas. Pool fixo:
        // cheio, a mais velha é reaproveitada
        readonly List<(MeshRenderer r, float nasceu)> manchas = new List<(MeshRenderer, float)>();
        Material matMancha; Mesh quadChao; int proxMancha;
        const int MANCHAS = 160;

        /// <summary>Levou tiro: manchas no chão atrás dele, no sentido da bala (HS 4-5, corpo 2-3, colete/capacete 1) e,
        /// se tem parede logo atrás, uma mancha nela na altura do tiro (HS quase sempre). Perceptível, sem exagero.</summary>
        void Respingo(Vector3 p, Vector3 vai, bool protegido, bool cabeca, float alcance = 1f, float mais = 1f)
        {
            if (matMancha == null)
            {
                matMancha = new Material(Shader.Find("CSM/Mancha")) { name = "mancha de sangue" };
                quadChao = new Mesh { name = "quad do chão" };
                quadChao.SetVertices(new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
                quadChao.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                quadChao.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); quadChao.RecalculateBounds();
            }
            vai.y = 0; vai = vai.sqrMagnitude > 1e-6f ? vai.normalized : Vector3.forward;
            var lado = new Vector3(vai.z, 0, -vai.x);
            float chao = Piso(p);
            // (usuário: mais perceptível e ficando até o fim do round) mais manchas, maiores, em leque um pouco mais longe
            int n = Mathf.RoundToInt((protegido ? 2 : cabeca ? 6 + (R(0, 1) < 0.5f ? 1 : 0) : 3 + (R(0, 1) < 0.5f ? 1 : 0)) * mais);
            float longe = (cabeca && !protegido ? 0.9f : 0.6f) * alcance;
            for (int i = 0; i < n; i++)
            {
                var q = p + vai * R(0.06f, longe) + lado * R(-0.18f, 0.18f) * alcance;
                float f = Piso(q);
                if (Mathf.Abs(f - chao) > 0.04f) continue;
                PorMancha(new Vector3(q.x, f + 0.004f, q.z), Quaternion.Euler(0, R(0, 360), 0),
                          (i == 0 ? R(0.1f, 0.15f) : R(0.05f, 0.09f)) * (protegido ? 0.7f : cabeca ? 1.25f : 1f));
            }
            // parede atrás (no sentido da bala, até ~1,2 x alcance): mancha grande em pé na altura do tiro + gotinhas em volta
            if (R(0, 1) < (protegido ? 0.35f : cabeca ? 1f : 0.75f) * Mathf.Min(1.3f, mais))
                for (float d = 0.05f; d < 1.2f * alcance; d += 0.02f)
                {
                    var q = p + vai * d;
                    if (Piso(q) <= p.y - 0.03f) continue;
                    var na = p + vai * (d - 0.015f);
                    var rot = Quaternion.FromToRotation(Vector3.up, -vai) * Quaternion.Euler(0, R(0, 360), 0);
                    float alt = Mathf.Max(chao + 0.05f, p.y + R(-0.12f, 0.04f));
                    PorMancha(new Vector3(na.x, alt, na.z), rot, R(0.17f, 0.26f) * (cabeca ? 1.25f : 1f) * Mathf.Min(1.2f, mais));
                    for (int k = 0; k < 3; k++)
                    {
                        var o = lado * R(-0.16f, 0.16f) + Vector3.up * R(-0.1f, 0.12f);
                        var pk = na + o; pk.y = Mathf.Max(chao + 0.03f, alt + o.y);
                        PorMancha(new Vector3(pk.x, pk.y, pk.z) - vai * 0.002f * (k + 1), rot, R(0.045f, 0.09f));
                    }
                    break;
                }
        }

        void PorMancha(Vector3 pos, Quaternion rot, float s)
        {
            {
                MeshRenderer r;
                if (manchas.Count < MANCHAS)
                {
                    var g = new GameObject("mancha"); g.transform.SetParent(raiz, false);
                    g.AddComponent<MeshFilter>().sharedMesh = quadChao;
                    r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = matMancha;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                    manchas.Add((r, tempo));
                }
                else { r = manchas[proxMancha].r; manchas[proxMancha] = (r, tempo); proxMancha = (proxMancha + 1) % MANCHAS; }
                r.gameObject.SetActive(true);
                r.transform.SetPositionAndRotation(pos, rot);
                r.transform.localScale = new Vector3(s, 1, s * R(0.8f, 1.3f));
                mpb.Clear(); mpb.SetColor("_BaseColor", new Color(0.42f, 0.03f, 0.04f, 0.85f)); mpb.SetFloat("_Semente", R(0, 100)); r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Round novo: chão limpo.</summary>
        public void LimparManchas() { foreach (var (r, _) in manchas) if (r) r.gameObject.SetActive(false); }

        /// <summary>HE: bola de fogo, nuvem de fumaça subindo, anel de poeira no chão, faíscas e luz forte.</summary>
        public void HE(Vector3 p)
        {
            Luz(p + Vector3.up * 0.3f, new Color(1f, 0.65f, 0.3f), 14, 7, 0.35f);
            for (int i = 0; i < 7; i++)
                Add(p + Vector3.up * 0.2f + Esfera(0.2f), Esfera(1.2f) + Vector3.up * 0.6f, R(0.25f, 0.4f), 0.2f, R(0.7f, 1.0f), new Color(1f, 0.92f, 0.55f, 1), new Color(1f, 0.35f, 0.1f, 0f), -0.5f, 3, 4);
            for (int i = 0; i < 12; i++)
                Add(p + Vector3.up * 0.25f + Esfera(0.3f), Esfera(0.7f) + Vector3.up * R(0.4f, 1.0f), R(1.4f, 2.4f), 0.25f, R(0.55f, 0.85f), new Color(0.38f, 0.35f, 0.33f, 0.85f), new Color(0.6f, 0.58f, 0.55f, 0f), -0.25f, 0, 1.2f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Add(p + d * 0.2f + Vector3.up * 0.05f, d * R(2.0f, 3.0f), R(0.6f, 0.9f), 0.12f, 0.35f, new Color(AREIA.r, AREIA.g, AREIA.b, 0.8f), new Color(AREIA.r, AREIA.g, AREIA.b, 0), 0, 0, 3.5f);
            }
            for (int i = 0; i < 16; i++)
                Add(p + Vector3.up * 0.2f, (Esfera(1) + Vector3.up * 0.6f).normalized * R(3f, 6f), R(0.3f, 0.6f), 1, 0.5f, new Color(1f, 0.9f, 0.5f, 1), new Color(1f, 0.45f, 0.15f, 1), 9f, 5, 0.8f,
                    Forma.Caixa, new Vector3(0.015f, 0.015f, 0.07f));
        }

        /// <summary>C4: explosão grande — bola de fogo alta, coluna de fumaça escura, anel de poeira largo, chuva de
        /// faíscas e destroços, luz enorme.</summary>
        public void ExplosaoC4(Vector3 p)
        {
            Luz(p + Vector3.up * 0.8f, new Color(1f, 0.62f, 0.28f), 40, 22, 0.9f);
            for (int i = 0; i < 26; i++)
                Add(p + Vector3.up * 0.3f + Esfera(0.6f), Esfera(3.2f) + Vector3.up * R(1.5f, 3.5f), R(0.5f, 0.9f), 0.5f, R(1.8f, 2.8f), new Color(1f, 0.92f, 0.55f, 1), new Color(1f, 0.3f, 0.08f, 0f), -1f, 3.5f, 2.5f);
            for (int i = 0; i < 40; i++)
                Add(p + Vector3.up * R(0.5f, 2.5f) + Esfera(1.2f), Esfera(1.0f) + Vector3.up * R(1.0f, 2.6f), R(2.5f, 4.5f), 0.6f, R(1.6f, 2.6f), new Color(0.3f, 0.27f, 0.25f, 0.85f), new Color(0.55f, 0.52f, 0.5f, 0f), -0.25f, 0, 0.8f);
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Add(p + d * 0.4f + Vector3.up * 0.1f, d * R(5f, 8f), R(0.9f, 1.4f), 0.3f, 0.9f, new Color(0.93f, 0.83f, 0.64f, 0.85f), new Color(0.93f, 0.83f, 0.64f, 0), 0, 0, 2.2f);
            }
            for (int i = 0; i < 40; i++)
                Add(p + Vector3.up * 0.4f, (Esfera(1) + Vector3.up * 0.8f).normalized * R(5f, 11f), R(0.6f, 1.2f), 1, 0.4f, new Color(1f, 0.9f, 0.5f, 1), new Color(1f, 0.45f, 0.15f, 1), 9f, 6, 0.6f,
                    Forma.Caixa, new Vector3(0.02f, 0.02f, 0.1f));
            for (int i = 0; i < 18; i++)   // destroços (pedaços de reboco/madeira) caindo e quicando
                Add(p + Vector3.up * 0.3f, (Esfera(1) + Vector3.up).normalized * R(3f, 7f), R(2.5f, 3.5f), 1, 1, new Color(0.75f, 0.62f, 0.45f), new Color(0.75f, 0.62f, 0.45f), 12f, 0, 0.3f,
                    Forma.Caixa, new Vector3(R(0.04f, 0.09f), R(0.03f, 0.06f), R(0.04f, 0.09f)), true, 0.35f);
        }

        /// <summary>Flash: estouro branco rápido com luz muito forte.</summary>
        public void Flash(Vector3 p)
        {
            Luz(p, new Color(1f, 0.98f, 0.9f), 30, 9, 0.28f);
            Add(p, Vector3.zero, 0.22f, 0.15f, 1.1f, new Color(1, 1, 1, 1), new Color(1, 1, 0.9f, 0), 0, 8, 0);
            for (int i = 0; i < 10; i++)
                Add(p, Esfera(1).normalized * R(2.5f, 4.5f), R(0.15f, 0.3f), 1, 0.4f, new Color(1, 1, 0.95f, 1), new Color(1, 1, 0.8f, 1), 2f, 6, 1.5f,
                    Forma.Caixa, new Vector3(0.012f, 0.012f, 0.06f));
            for (int i = 0; i < 4; i++)
                Add(p + Esfera(0.1f), Esfera(0.3f), R(0.6f, 1.0f), 0.08f, 0.25f, new Color(0.85f, 0.85f, 0.85f, 0.6f), new Color(0.9f, 0.9f, 0.9f, 0), -0.2f);
        }

        /// <summary>Garrafa quebrou: estilhaço de vidro e labareda inicial.</summary>
        public void Molotov(Vector3 p)
        {
            Luz(p + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.2f), 10, 5, 0.4f);
            for (int i = 0; i < 10; i++)
                Add(p + Vector3.up * 0.05f, (Esfera(1) + Vector3.up * 0.8f).normalized * R(1.2f, 2.4f), R(0.5f, 0.9f), 1, 1, new Color(0.55f, 0.85f, 0.55f, 1), new Color(0.55f, 0.85f, 0.55f, 1), 9.8f, 0, 0.3f,
                    Forma.Caixa, new Vector3(0.03f, 0.008f, 0.03f), true, 0.2f);
            for (int i = 0; i < 6; i++)
                Add(p + Esfera(0.15f), Vector3.up * R(0.8f, 1.5f) + Esfera(0.4f), R(0.3f, 0.5f), 0.15f, 0.45f, new Color(1f, 0.9f, 0.45f, 1), new Color(1f, 0.3f, 0.1f, 0), -0.5f, 3, 2);
        }

        /// <summary>Fogo apagado pela smoke: nuvem de vapor branco subindo rápido em volta.</summary>
        public void Vapor(Vector3 p, float raio)
        {
            for (int i = 0; i < 22; i++)
            {
                var o = Esfera(raio); o.y = Mathf.Abs(o.y) * 0.2f;
                Add(p + o, Vector3.up * R(0.8f, 1.6f) + Esfera(0.3f), R(0.7f, 1.3f), 0.08f, R(0.3f, 0.5f), new Color(0.95f, 0.95f, 0.97f, 0.7f), new Color(1, 1, 1, 0), -0.4f, 0, 1.6f);
            }
        }

        /// <summary>Smoke apagou a molotov: em cada chama que estava acesa sai uma fumacinha branca que chia (primeiro um
        /// tufo baixo e rápido, depois sobe devagar e some), como a chama apagada do CS2.</summary>
        public void ApagouFogo(FireArea f)
        {
            int n = f.Visible; if (n == 0) return;
            int passo = Mathf.Max(1, n / 60);
            // a fumaça escura que o fogo já tinha soltado vira vapor branco (senão fica uma nuvem suja onde ele estava)
            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = f.cells[i]; var q = MalhaMapa.ParaUnity(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z);
                mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q);
            }
            mn -= new Vector3(0.4f, 0.2f, 0.4f); mx += new Vector3(0.4f, 3f, 0.4f);
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p.solido || p.brilho > 0 || p.c0.r > 0.25f || p.p.x < mn.x || p.p.x > mx.x || p.p.y < mn.y || p.p.y > mx.y || p.p.z < mn.z || p.p.z > mx.z) continue;
                p.c0 = L(new Color(0.95f, 0.95f, 0.96f, p.c0.a)); p.c1 = L(new Color(0.9f, 0.9f, 0.92f, 0)); p.brilho = 1;
                ps[i] = p;
            }
            for (int i = 0; i < n; i += passo)
            {
                var c = f.cells[i];
                var p = MalhaMapa.ParaUnity(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z) + new Vector3(R(-0.08f, 0.08f), 0.04f, R(-0.08f, 0.08f));
                // tufo: sai de lado rente ao chão
                var lado = Esfera(1); lado.y = 0;
                Add(p, lado * R(0.5f, 1.1f) + Vector3.up * R(0.2f, 0.5f), R(0.4f, 0.7f), 0.14f, R(0.4f, 0.55f), new Color(0.98f, 0.98f, 0.99f, 0.9f), new Color(0.95f, 0.95f, 0.96f, 0), -0.2f, 1f, 3f);
                // fumacinha branca subindo e abrindo
                Add(p + Vector3.up * 0.05f, Vector3.up * R(0.6f, 1.2f) + Esfera(0.2f), R(1.4f, 2.4f), 0.12f, R(0.5f, 0.8f), new Color(0.95f, 0.95f, 0.96f, 0.75f), new Color(0.88f, 0.88f, 0.9f, 0), -0.15f, 1f, 1.3f);
            }
        }

        /// <summary>Rastro da granada voando: pontinhos na cor dela que somem rápido, um a cada ~1,5 cm do caminho.</summary>
        public void RastroGranada(Vector3 de, Vector3 ate, Color cor)
        {
            float d = (ate - de).magnitude; int n = Mathf.Clamp(Mathf.CeilToInt(d / 0.015f), 1, 8);
            for (int i = 0; i < n; i++)
                Add(Vector3.Lerp(de, ate, (i + 1f) / n), Vector3.zero, 0.6f, 0.036f, 0.006f, new Color(cor.r, cor.g, cor.b, 0.85f), new Color(cor.r, cor.g, cor.b, 0), 0, 1f, 0);
        }

        /// <summary>Granada bateu no chão: poeirinha baixa em volta.</summary>
        public void QuiqueChao(Vector3 p)
        {
            for (int i = 0; i < 6; i++)
            {
                var d = Esfera(1); d.y = 0;
                Add(p + d * 0.02f, d.normalized * R(0.25f, 0.55f) + Vector3.up * R(0.05f, 0.2f), R(0.35f, 0.6f), 0.03f, R(0.07f, 0.11f), new Color(AREIA.r, AREIA.g, AREIA.b, 0.75f), new Color(AREIA.r, AREIA.g, AREIA.b, 0), 0, 0, 3.5f);
            }
        }

        /// <summary>Granada bateu na parede: faisquinha e um pouco de reboco.</summary>
        public void QuiqueParede(Vector3 p, Vector3 vel)
        {
            var volta = vel.sqrMagnitude > 1e-8f ? -vel.normalized : Vector3.up;
            for (int i = 0; i < 4; i++)
                Add(p, (volta + Esfera(0.8f)) * R(0.6f, 1.2f), R(0.1f, 0.18f), 0.012f, 0.004f, new Color(1f, 0.9f, 0.55f, 1), new Color(1f, 0.6f, 0.2f, 0), 2f, 3f, 1f);
            for (int i = 0; i < 4; i++)
                Add(p, (volta + Esfera(0.6f)) * R(0.2f, 0.5f), R(0.4f, 0.7f), 0.025f, R(0.06f, 0.09f), new Color(AREIA.r, AREIA.g, AREIA.b, 0.7f), new Color(AREIA.r, AREIA.g, AREIA.b, 0), 0, 0, 3f);
        }

        /// <summary>Bala saindo da smoke: um tufinho de fumaça arrastado na direção do tiro (a fumaça cede no furo).</summary>
        public void TufoSmoke(Vector3 p, Vector3 dir)
        {
            var d = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
            for (int i = 0; i < 4; i++)
                Add(p + Esfera(0.05f), d * R(0.5f, 1.1f) + Esfera(0.15f), R(0.5f, 0.9f), 0.06f, R(0.18f, 0.28f), new Color(0.93f, 0.93f, 0.92f, 0.65f), new Color(0.9f, 0.9f, 0.9f, 0), 0, 1f, 2.5f);
        }

        /// <summary>Pegando fogo (usuário: "tem que ter um feedback visual que o cara tá pegando fogo"; a chaminha de antes,
        /// 1 a cada ~3 quadros só no pé, não se via): línguas de fogo subindo pelo corpo até a cintura/peito, mais densas
        /// embaixo, brasinhas soltas subindo, um fiapo de fumaça e luz laranja tremendo nele.</summary>
        public void Queimando(Vector3 pe, float dt)
        {
            // (no 1º teste as línguas amarelo-claras sumiam no chão creme: agora laranja/vermelho saturado, maiores e
            // durando mais, com miolo amarelo só nas de baixo)
            int n = Mathf.Max(1, Mathf.RoundToInt(dt * 60));
            for (int i = 0; i < n; i++)
            {
                // (a câmera é de cima: chama no meio do corpo fica escondida por ele) nascem na beira do corpo, em volta,
                // e as de cima passam da cabeça
                float h = R(0, 1); h = h * 0.7f + 0.02f;
                float ang = R(0, Mathf.PI * 2), raio = R(0.12f, 0.2f) - h * 0.06f;
                bool miolo = h < 0.15f && R(0, 1) < 0.5f;
                Add(pe + new Vector3(Mathf.Cos(ang) * raio, h, Mathf.Sin(ang) * raio), Vector3.up * R(0.6f, 1.0f) + Esfera(0.08f), R(0.3f, 0.55f), R(0.12f, 0.18f) * (1.15f - h * 0.8f), 0.03f,
                    // (vermelho no fim lia como gota de sangue: termina laranja e some)
                    miolo ? new Color(1f, 0.85f, 0.35f, 1) : new Color(1f, 0.62f, 0.14f, 1), new Color(1f, 0.32f, 0.06f, 0f), -0.6f, 3.2f, 1.4f, Forma.Bola, new Vector3(1, 1.8f, 1));
            }
            if (R(0, 1) < dt * 14)
                Add(pe + new Vector3(R(-0.1f, 0.1f), R(0.1f, 0.45f), R(-0.1f, 0.1f)), Vector3.up * R(0.8f, 1.5f) + Esfera(0.35f), R(0.5f, 0.9f), 0.018f, 0.006f,
                    new Color(1f, 0.9f, 0.5f, 1), new Color(1f, 0.35f, 0.15f, 1), -0.3f, 6f, 0.8f);
            if (R(0, 1) < dt * 3)
                Add(pe + Vector3.up * R(0.4f, 0.6f), Vector3.up * R(0.3f, 0.5f) + Esfera(0.05f), R(0.7f, 1.0f), 0.05f, 0.16f,
                    new Color(0.25f, 0.22f, 0.2f, 0.35f), new Color(0.4f, 0.38f, 0.36f, 0), -0.1f);
            LuzDeChama(pe + Vector3.up * 0.3f, 0.75f);
        }

        /// <summary>Molotov acesa (no pano da garrafa na mão ou voando): as MESMAS línguas de fogo do chão, pequenas, que
        /// ficam pra trás quando a garrafa anda, e uma luz laranja tremendo que ilumina quem segura e o chão em volta.
        /// As luzes são do quadro: FimChamas() apaga as que não foram usadas.</summary>
        public void Chama(Vector3 p, float dt, float forca)
        {
            if (forca <= 0.01f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(dt * 60 * forca));
            for (int i = 0; i < n; i++)
                if (R(0, 1) < 0.8f)
                    Add(p + Esfera(0.02f), Vector3.up * R(0.35f, 0.6f) + Esfera(0.06f), R(0.18f, 0.3f), R(0.05f, 0.075f) * forca, 0.008f,
                        new Color(1f, 0.85f, 0.35f, 1), new Color(0.95f, 0.25f, 0.08f, 0.6f), -0.6f, 3.2f, 1.2f, Forma.Bola, new Vector3(1, 1.6f, 1));
            if (R(0, 1) < dt * 3 * forca)
                Add(p + Vector3.up * 0.1f, Vector3.up * R(0.3f, 0.5f) + Esfera(0.05f), R(0.6f, 0.9f), 0.04f, 0.12f, new Color(0.25f, 0.22f, 0.2f, 0.4f), new Color(0.4f, 0.38f, 0.36f, 0), -0.1f);
            LuzDeChama(p + Vector3.up * 0.06f, forca);
        }

        /// <summary>Luz laranja tremendo neste quadro (pool das chamas: FimChamas apaga as que sobraram).</summary>
        void LuzDeChama(Vector3 p, float forca)
        {
            if (chamasQuadro >= luzChama.Count)
            {
                var g = new GameObject("luz da chama"); g.transform.SetParent(raiz, false);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.shadows = LightShadows.None;
                luzChama.Add(l);
            }
            var lc = luzChama[chamasQuadro++]; lc.enabled = true;
            lc.transform.position = p;
            lc.intensity = (1.6f + 0.5f * Mathf.Sin(tempo * 23 + chamasQuadro) + 0.3f * Mathf.Sin(tempo * 37)) * forca;
            lc.range = 1.6f + 0.6f * forca;
        }
        readonly List<Light> luzChama = new List<Light>(); int chamasQuadro;
        /// <summary>Fim do quadro das chamas: apaga as luzes de chama que ninguém usou neste quadro.</summary>
        public void FimChamas()
        {
            for (int i = chamasQuadro; i < luzChama.Count; i++) luzChama[i].enabled = false;
            chamasQuadro = 0;
        }

        // ------------------------------------------------------------------ laser da AWP na luneta
        // como nas transmissões: risco vermelho fino da arma até a parede/smoke onde o AWPer está travado
        readonly Dictionary<int, (Transform t, MeshRenderer r)> lasers = new Dictionary<int, (Transform, MeshRenderer)>();
        readonly HashSet<int> lasersQuadro = new HashSet<int>();
        readonly MaterialPropertyBlock mpbLaser = new MaterialPropertyBlock();

        /// <summary>Laser do AWPer <paramref name="id"/> neste quadro (forca 0..1: parado travado = 1, andando de luneta = fraco).</summary>
        public void Laser(int id, Vector3 de, Vector3 ate, float forca)
        {
            if (!lasers.TryGetValue(id, out var l))
            {
                var g = new GameObject("laser AWP"); g.transform.SetParent(raiz, false);
                g.AddComponent<MeshFilter>().sharedMesh = caixa;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = matNuvem; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                l = (g.transform, r); lasers[id] = l;
            }
            var d = ate - de; float comp = d.magnitude; if (comp < 0.05f) { l.r.enabled = false; return; }
            l.r.enabled = true; lasersQuadro.Add(id);
            l.t.SetPositionAndRotation(de + d * 0.5f, Quaternion.LookRotation(d));
            l.t.localScale = new Vector3(0.012f, 0.012f, comp);
            float pulso = 0.85f + 0.15f * Mathf.Sin(tempo * 9 + id);
            mpbLaser.Clear(); mpbLaser.SetColor("_BaseColor", L(new Color(1f, 0.16f, 0.12f, Mathf.Lerp(0.25f, 0.8f, forca) * pulso)));
            mpbLaser.SetFloat("_Brilho", Mathf.Lerp(1.5f, 4f, forca)); l.r.SetPropertyBlock(mpbLaser);
        }

        /// <summary>Fim do quadro dos lasers: some o de quem saiu da luneta.</summary>
        public void FimLasers()
        {
            foreach (var kv in lasers) if (!lasersQuadro.Contains(kv.Key)) kv.Value.r.enabled = false;
            lasersQuadro.Clear();
        }

        /// <summary>Passo correndo: poeirinha no pé.</summary>
        public void Passo(Vector3 pe, Vector3 vel)
        {
            for (int i = 0; i < 2; i++)
                Add(pe + Vector3.up * 0.02f + Esfera(0.04f), -vel * 0.12f + Vector3.up * R(0.1f, 0.25f) + Esfera(0.1f), R(0.35f, 0.55f), 0.04f, R(0.1f, 0.15f), new Color(AREIA.r, AREIA.g, AREIA.b, 0.6f), new Color(AREIA.r, AREIA.g, AREIA.b, 0), 0, 0, 2.5f);
        }

        /// <summary>Fogo do molotov por quadro: línguas que nascem nas células acesas e sobem encolhendo, fumacinha escura em
        /// cima e uma luz tremendo por área.</summary>
        public void Fogos(List<FireArea> fogos, float dt)
        {
            for (int i = 0; i < luzFogo.Length; i++) luzFogo[i].enabled = false;
            for (int k = 0; k < fogos.Count; k++)
            {
                var f = fogos[k]; int n = f.Visible; float forca = f.Strength;
                if (n == 0) continue;
                Vector3 soma = Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    var c = f.cells[i];
                    var p = MalhaMapa.ParaUnity(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z);
                    soma += p;
                    if (R(0, 1) < dt * 7 * forca)
                        Add(p + new Vector3(R(-0.07f, 0.07f), 0.03f, R(-0.07f, 0.07f)), Vector3.up * R(0.5f, 0.9f) + Esfera(0.1f), R(0.35f, 0.6f), R(0.12f, 0.18f) * forca, 0.02f,
                            new Color(1f, 0.85f, 0.35f, 1), new Color(0.95f, 0.25f, 0.08f, 0.6f), -0.6f, 3.2f, 1.2f, Forma.Bola, new Vector3(1, 1.6f, 1));
                    if (R(0, 1) < dt * 0.7f * forca)
                        Add(p + Vector3.up * 0.3f, Vector3.up * R(0.5f, 0.8f) + Esfera(0.1f), R(1.2f, 1.8f), 0.1f, 0.35f, new Color(0.25f, 0.22f, 0.2f, 0.5f), new Color(0.4f, 0.38f, 0.36f, 0), -0.1f);
                }
                if (k < luzFogo.Length)
                {
                    var l = luzFogo[k]; l.enabled = true;
                    l.transform.position = soma / n + Vector3.up * 0.35f;
                    l.intensity = (2.2f + 0.8f * Mathf.Sin(tempo * 17 + k) + 0.5f * Mathf.Sin(tempo * 29)) * forca;
                    l.range = 2.5f + Mathf.Sqrt(n) * 0.15f;
                }
            }
        }

        // ------------------------------------------------------------------ passo de física e desenho
        public void Atualizar(float dt)
        {
            tempo += dt;
            for (int i = ps.Count - 1; i >= 0; i--)
            {
                var p = ps[i];
                p.idade += dt;
                if (p.idade >= p.vida) { ps.RemoveAt(i); continue; }
                if (!p.parado)
                {
                    p.v.y -= p.grav * dt;
                    p.v *= Mathf.Exp(-p.arrasto * dt);
                    p.p += p.v * dt;
                    if (p.quica > 0)
                    {
                        float chao = Piso(p.p) + p.escala0.y * 0.5f;
                        if (p.p.y < chao)
                        {
                            p.p.y = chao;
                            if (Mathf.Abs(p.v.y) < 0.4f) { p.parado = true; p.r = Quaternion.Euler(0, p.r.eulerAngles.y, 90); }
                            p.v = new Vector3(p.v.x * 0.5f, -p.v.y * p.quica, p.v.z * 0.5f);
                            p.giro *= 0.5f;
                        }
                    }
                    p.r = Quaternion.Euler(p.giro * dt) * p.r;
                }
                ps[i] = p;
            }
            for (int i = luzes.Count - 1; i >= 0; i--)
            {
                var (l, ate, dur, i0) = luzes[i];
                float resta = ate - tempo;
                if (resta <= 0) { Destroy(l.gameObject); luzes.RemoveAt(i); continue; }
                l.intensity = i0 * Mathf.Clamp01(resta / dur);
            }
            // desenho
            while (pool.Count < ps.Count)
            {
                var g = new GameObject("fx"); g.transform.SetParent(raiz, false);
                var f = g.AddComponent<MeshFilter>(); var r = g.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pool.Add((g.transform, r, f));
            }
            for (int i = 0; i < pool.Count; i++)
            {
                var (t, r, f) = pool[i];
                if (i >= ps.Count) { r.enabled = false; continue; }
                var p = ps[i]; float u = p.idade / p.vida;
                r.enabled = true;
                f.sharedMesh = p.forma == Forma.Bola ? bola : caixa;
                r.sharedMaterial = p.solido ? matSolido : matNuvem;
                r.shadowCastingMode = p.solido ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                // esticado na direção do voo (faísca/estilhaço)
                Quaternion q = p.r;
                if (p.forma == Forma.Caixa && !p.solido && p.v.sqrMagnitude > 1e-4f) q = Quaternion.LookRotation(p.v);
                t.SetPositionAndRotation(p.p, q);
                float e = p.solido ? 1 : Mathf.SmoothStep(0, 1, Mathf.Min(1, u * 4)) * 0.3f + 0.7f;
                t.localScale = Vector3.Lerp(p.escala0, p.escala1, Mathf.Sqrt(u)) * e;
                var c = Color.Lerp(p.c0, p.c1, u);
                if (p.solido && u > 0.8f) t.localScale *= Mathf.Clamp01((1 - u) / 0.2f);
                mpb.Clear(); mpb.SetColor("_BaseColor", c);
                if (!p.solido) mpb.SetFloat("_Brilho", p.brilho);
                r.SetPropertyBlock(mpb);
            }
        }

        static void Destroy(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}
