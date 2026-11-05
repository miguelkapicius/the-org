using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSM.Sim;
using CSM.Map;
using CSM.View;
using CSM.Audio;   // sons (Som3D, da sessão de som): chamados junto de cada efeito visual

namespace CSM.View3D
{
    // PartidaAoVivo3D, parte: granadas voando e no chão, C4 com LED, kit e granada na mão/no corpo.
    public partial class PartidaAoVivo3D
    {
        // granadas voando e no chão e o C4: os modelos de verdade (peças da animação de jogar/plantar)
        GameObject c4;
        GameObject c4Led; Light c4Luz; Material matLed; bool ledLogou;
        /// <summary>Segundos desde o último bip da C4 (mesma agenda do Som3D.C4: 1º bip 1 s depois de plantar, o intervalo
        /// encurta até 0,15 s); 0 no último segundo (aceso direto, como o chiado final).</summary>
        static float DesdeBip(float restante, float total)
        {
            if (restante <= Som3D.FINAL_C4) return 0;
            float r = total - 1f, ult = 9;
            for (int i = 0; i < 400 && r >= restante; i++) { ult = r - restante; r -= Mathf.Max(0.15f, 0.1f + 0.9f * r / total); }
            return ult;
        }
        /// <summary>LED da C4 (desdeBip &lt; 0 = apagado de vez): quadradinho vermelho no topo do pacote + luzinha vermelha.</summary>
        void LedC4(float desdeBip)
        {
            if (c4 == null) return;
            if (c4Led == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); var me = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh); DestroyImmediate(tmp);
                me.name = "LED C4";
                var cs = new Color[me.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = Color.white; me.colors = cs;
                c4Led = new GameObject("LED da C4"); c4Led.transform.SetParent(transform, false);
                c4Led.AddComponent<MeshFilter>().sharedMesh = me;
                matLed = new Material(Shader.Find("CSM/Brilho")) { name = "LED C4" }; matLed.SetColor("_BaseColor", new Color(1f, 0.06f, 0.04f));
                var mr = c4Led.AddComponent<MeshRenderer>(); mr.sharedMaterial = matLed; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var gl = new GameObject("luz"); gl.transform.SetParent(c4Led.transform, false);
                c4Luz = gl.AddComponent<Light>(); c4Luz.type = LightType.Point; c4Luz.color = new Color(1f, 0.12f, 0.08f); c4Luz.range = 0.9f; c4Luz.shadows = LightShadows.None;
            }
            bool on = c4.activeSelf && desdeBip >= 0;
            c4Led.SetActive(on); if (!on) return;
            // topo do pacote: canto da frente, um tico acima da tampa
            var bb = new Bounds(); bool ok = false;
            foreach (var r in c4.GetComponentsInChildren<Renderer>()) { if (!ok) { bb = r.bounds; ok = true; } else bb.Encapsulate(r.bounds); }
            if (!ok) { c4Led.SetActive(false); return; }
            float lado = Mathf.Clamp(Mathf.Min(bb.size.x, bb.size.z) * 0.1f, 0.006f, 0.02f);   // pontinho (0,03 parecia um botão)
            c4Led.transform.SetPositionAndRotation(new Vector3(bb.center.x + bb.extents.x * 0.45f, bb.max.y + lado * 0.3f, bb.center.z - bb.extents.z * 0.35f), c4.transform.rotation);
            c4Led.transform.localScale = new Vector3(lado, lado * 0.6f, lado);
            if (!ledLogou)
            {
                ledLogou = true; var lmr = c4Led.GetComponent<MeshRenderer>(); var lmf = c4Led.GetComponent<MeshFilter>();
                Debug.Log($"[C4] LED em {c4Led.transform.position} lado {lado:0.000}; C4 {bb.center} tamanho {bb.size}; camada {c4Led.layer}/{c4.layer}; ativo {c4Led.activeInHierarchy} rend {lmr.enabled} vert {lmf.sharedMesh?.vertexCount} limites {lmr.bounds} shader {matLed.shader.name} ok {matLed.shader.isSupported}");
            }
            // piscada curta como a do CS (40 ms cheia, apaga em ~60 ms); entre os bips fica um vermelho escuro (LED apagado
            // ainda tem cor). A luz é fraca e curta: forte, ela pintava a C4 inteira de vermelho
            float k = desdeBip < 0.035f ? 1f : Mathf.Exp(-(desdeBip - 0.035f) / 0.02f);
            matLed.SetFloat("_Intensidade", Mathf.Lerp(0.5f, 1.7f, k));   // (2,4 estourava o bloom e avermelhava a C4 toda)
            c4Luz.intensity = 0.35f * k; c4Luz.range = 0.3f; c4Luz.enabled = k > 0.15f;
        }
        readonly Dictionary<object, GameObject> props = new Dictionary<object, GameObject>();
        readonly HashSet<object> propsVistos = new HashSet<object>();
        // estado do desenho de cada granada voando: giro acumulado, quiques já vistos, raio (pra apoiar no chão)
        class Voo { public Quaternion rot = Quaternion.identity; public int quiques, paredes; public float raio; public Vector3 ultPos; public bool tem; }
        readonly Dictionary<object, Voo> voos = new Dictionary<object, Voo>();
        // lata da smoke caída no chão enquanto a smoke dura (no CS2 a granada fica lá soltando a fumaça)
        readonly List<(GameObject g, float ate)> latas = new List<(GameObject, float)>();
        float ultTProps = -1;
        // tamanho da granada na tela (usuário: no celular não dava pra ver voando nem no chão)
        const float ESC_GRANADA = 2.2f, ESC_GRANADA_CHAO = ESC_GRANADA;   // no chão do mesmo tamanho que voando
        /// <summary>Granada solta no mundo (voando, caída, lata): o modelo do kit feito pelas medidas do CS2
        /// (Resources/Granadas); sem ele, as peças da animação de jogar como antes. CT de molotov = incendiária.</summary>
        GameObject Granada(string tipo, bool tr)
        {
            string k = tipo == "incendiaria" ? "molotov" : tipo;
            string kit = k switch { "he" => "GRN_HE", "flash" => "GRN_Flashbang", "smoke" => "GRN_Smoke", "kit" => "DefuseKit", _ => tr ? "GRN_Molotov" : "GRN_Incendiary" };
            var pf = Resources.Load<GameObject>("Granadas/" + kit);
            if (pf != null)
            {
                // embrulho: o modelo vem em metros com o centro do corpo na origem; ESC_KIT dá o tamanho de antes na tela
                var g0 = new GameObject("granada " + kit); g0.transform.SetParent(transform, false);
                // a garrafa tem 35 cm: no tamanho real ficava do tamanho do boneco chibi
                var m0 = Instantiate(pf, g0.transform, false); m0.transform.localScale = pf.transform.localScale * (kit == "GRN_Molotov" ? ESC_KIT * 0.55f : ESC_KIT);
                if (kit == "DefuseKit")
                {
                    // o kit vem do atlas de armas do EsportsPoc convertido pra cor por vértice (atlas_cor.py): o mesmo
                    // material dos bonecos (sem ele saía branco)
                    Boneco3D.GarantirMateriais(); bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    // malha com esqueleto (vem do rig do kit de armas) não desenhava no chão: vira malha fixa na pose de agora
                    foreach (var sr in m0.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked = new Mesh { name = "kit fixo" }; sr.BakeMesh(baked, false);
                        var fixo = new GameObject("kit fixo"); fixo.transform.SetParent(sr.transform, false);
                        fixo.AddComponent<MeshFilter>().sharedMesh = baked; fixo.AddComponent<MeshRenderer>();
                        sr.enabled = false; DestroyImmediate(sr);
                    }
                    foreach (var mf in m0.GetComponentsInChildren<MeshFilter>())
                    {
                        if (lin && mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0)
                        {
                            var me = Instantiate(mf.sharedMesh); var cs = me.colors;
                            for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                            me.colors = cs; mf.sharedMesh = me;
                        }
                        var r = mf.GetComponent<MeshRenderer>(); if (r != null) r.sharedMaterial = Boneco3D.matCorpo;
                    }
                    // a escala do FBX convertido não é a das granadas: força a altura real (11 cm) na mesma régua (ESC_KIT)
                    float alt = 0; foreach (var rr in m0.GetComponentsInChildren<Renderer>()) alt = Mathf.Max(alt, rr.bounds.size.y, rr.bounds.size.x, rr.bounds.size.z);
                    if (alt > 1e-5f) m0.transform.localScale *= 0.11f * ESC_KIT * g0.transform.lossyScale.y / alt;
                    foreach (var sr in m0.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (lin && sr.sharedMesh != null && sr.sharedMesh.colors.Length > 0)
                        {
                            var me = Instantiate(sr.sharedMesh); var cs = me.colors;
                            for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                            me.colors = cs; sr.sharedMesh = me;
                        }
                        sr.sharedMaterial = Boneco3D.matCorpo;
                    }
                }
                return g0;
            }
            var g = Armas3D.Prop(Boneco3D.Carregar(tr ? "tr" : "ct"), "granada_" + k, 0.5f, Boneco3D.matCorpo, new[] { "sm_", "fl_", "mo_", "he_" });
            g.transform.SetParent(transform, false);
            return g;
        }
        const float ESC_KIT = 0.7f;   // unidades Unity por metro do modelo do kit (antes de ESC_GRANADA)

        // granada NA MÃO: o modelo do kit (o mesmo que voa e cai) no lugar da de elipsoides da animação — mesma posição,
        // em pé na direção do topo dela, um pouco maior que ela (o kit é mais fino e de longe sumia)
        static readonly string[] TIPO_MAO = { "smoke", "flash", "molotov", "he" };
        readonly Dictionary<int, (GameObject go, int tipo, bool tr, float alt)> naMao = new Dictionary<int, (GameObject, int, bool, float)>();
        /// <summary>Body block da Sim é de 8 (a escala do CS2), mas a cabeça chibi tem ~10 de largura: abaixo de 10 entre
        /// centros as cabeças se encostavam/entravam uma na outra. Só no DESENHO: cada boneco vivo é empurrado pra longe do
        /// outro, no máximo 1 unidade (a 8 de distância), gradual a partir de 10 (sem pulo). Na mesma altura só (|dz| &lt; 18).
        /// Usuário: "deixa 8 e arruma o visual".</summary>
        void AfastarCorpos()
        {
            const float LIVRE = 10f, MAX = 1f;
            var vivos = m.agents.Where(x => x.alive && bonecos.ContainsKey(x.id)).ToList();
            var desloc = new Dictionary<int, Vector3>();
            for (int i = 0; i < vivos.Count; i++)
                for (int j = i + 1; j < vivos.Count; j++)
                {
                    var a = vivos[i]; var c = vivos[j];
                    if (Mathf.Abs(a.z - c.z) >= 18) continue;
                    // pezinho (boost): a Sim sobrepõe os dois de propósito (quem sobe passa pelo ombro de quem está agachado)
                    if ((a.dandoPezinho || a.subindoPezinho) && (c.dandoPezinho || c.subindoPezinho)) continue;
                    Vector3 pa = bonecos[a.id].transform.position, pc = bonecos[c.id].transform.position;
                    var d = new Vector3(pa.x - pc.x, 0, pa.z - pc.z); float dist = d.magnitude / MalhaMapa.ESC;
                    if (dist >= LIVRE) continue;
                    var dir = dist > 0.01f ? d.normalized : Vector3.right;
                    float emp = Mathf.Min(MAX, (LIVRE - dist) / 2f) * MalhaMapa.ESC;
                    desloc[a.id] = (desloc.TryGetValue(a.id, out var da) ? da : Vector3.zero) + dir * emp;
                    desloc[c.id] = (desloc.TryGetValue(c.id, out var dc) ? dc : Vector3.zero) - dir * emp;
                }
            foreach (var kv in desloc)
                bonecos[kv.Key].transform.position += Vector3.ClampMagnitude(kv.Value, MAX * MalhaMapa.ESC);
        }

        // kit de desarme no corpo do CT que tem (Agent.hasKit): pendurado do lado de fora da coxa esquerda (o coldre é na
        // direita), seguindo a perna; morto não mostra (o kit cai no chão como item, ItemChao "kit")
        readonly Dictionary<int, GameObject> kitsCorpo = new Dictionary<int, GameObject>();
        void KitsNoCorpo()
        {
            foreach (var a in m.agents)
                if (bonecos.TryGetValue(a.id, out var b)) KitNoCorpo(a.id, b, a.alive && !a.IsT && a.hasKit);
        }
        void KitNoCorpo(int chave, Boneco3D b, bool tem)
        {
            kitsCorpo.TryGetValue(chave, out var g);
            Transform cL = tem ? b.Peca("coxaL") : null, cR = tem ? b.Peca("coxaR") : null;
            if (cL == null || cR == null) { if (g != null && g.activeSelf) g.SetActive(false); return; }
            if (g == null)
            {
                g = Granada("kit", false); g.name = "kit no corpo " + chave;
                g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); g.transform.localScale = Vector3.one;
                float alt = 0; foreach (var r in g.GetComponentsInChildren<Renderer>()) alt = Mathf.Max(alt, r.bounds.size.y);
                g.transform.localScale = Vector3.one * (0.085f / Mathf.Max(0.001f, alt));
                int camB = LayerMask.NameToLayer(Boneco3D.CAMADA);
                if (camB >= 0)
                {
                    var mp = new MaterialPropertyBlock(); mp.SetColor("_CorTime", Boneco3D.COR_CT);
                    foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camB;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mp);
                }
                kitsCorpo[chave] = g;
            }
            if (!g.activeSelf) g.SetActive(true);
            var lado = cL.position - cR.position; lado.y = 0;
            lado = lado.sqrMagnitude > 1e-8f ? lado.normalized : -b.transform.right;
            g.transform.SetPositionAndRotation(cL.position + lado * 0.045f + Vector3.up * 0.01f, Quaternion.LookRotation(lado, Vector3.up) * Quaternion.Euler(0, 90, 0));
        }

        void MaoGranadas()
        {
            // morto não segura nada: a granada ficava congelada onde a mão estava (a pose para e o ragdoll assume)
            foreach (var a in m.agents)
                if (bonecos.TryGetValue(a.id, out var b))
                {
                    if (a.alive || replay != null) NaMao(a.id, b, a.IsT);
                    else if (naMao.TryGetValue(a.id, out var nm) && nm.go != null && nm.go.activeSelf) nm.go.SetActive(false);
                }
        }
        void NaMao(int chave, Boneco3D b, bool tr)
        {
            naMao.TryGetValue(chave, out var nm);
            if (!b.GranadaNaMao(out var c, out var cima, out float alt, out int tipo))
            {
                if (nm.go != null && nm.go.activeSelf) nm.go.SetActive(false);
                return;
            }
            if (nm.go == null || nm.tipo != tipo || nm.tr != tr)
            {
                if (nm.go != null) Apagar(nm.go);
                var g = Granada(TIPO_MAO[tipo], tr);
                g.name = "granada na mão " + chave;
                // na camada dos bonecos (como a arma das costas): fora da profundidade do mapa, então a mão atrás dela não
                // vira silhueta "atrás da parede"; atrás de parede de verdade ela ganha a silhueta na cor do time
                int camB = LayerMask.NameToLayer(Boneco3D.CAMADA);
                if (camB >= 0)
                {
                    var mpS = new MaterialPropertyBlock(); mpS.SetColor("_CorTime", tr ? Boneco3D.COR_TR : Boneco3D.COR_CT);
                    foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camB;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpS);
                }
                g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); g.transform.localScale = Vector3.one;
                float alt0 = 0;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) alt0 = Mathf.Max(alt0, r.bounds.size.y);
                nm = (g, tipo, tr, Mathf.Max(0.001f, alt0)); naMao[chave] = nm;
            }
            if (!nm.go.activeSelf) nm.go.SetActive(true);
            var fr = Vector3.ProjectOnPlane(b.transform.forward, cima);
            var rot = fr.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fr, cima) : Quaternion.FromToRotation(Vector3.up, cima);
            nm.go.transform.SetPositionAndRotation(c, rot);
            nm.go.transform.localScale = Vector3.one * (alt * 1.45f / nm.alt);   // maior que a bolota: lê de longe
        }

        /// <summary>Laboratório do arremesso: CT (fila de cima) e TR, cada um parado numa fase (armado / soltando /
        /// continuação) de cada jeito (por cima, meia força, por baixo), com a granada do kit na mão.</summary>
        public void LabArremesso(V2 p, float giro, bool tr, float passo = 11)
        {
            string[] tipos = { "smoke", "he", "flash", "molotov" }; string[] jeitos = { "", "_meio", "_baixo" };
            int chave = 1000;
            // grade na tela: coluna = jeito (por cima, meia, por baixo), linha = fase (armado, soltando, continuação).
            // Na câmera iso, +x desce pra direita e +y desce pra esquerda: direita na tela = (+x, -y), baixo = (+x, +y)
            for (int j = 0; j < 3; j++)
                for (int f = 0; f < 3; f++)
                {
                    float cx = (j - 1) * passo, cy = (f - 1) * passo * 1.1f;
                    var q = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                    var b = Boneco3D.Criar(tr ? "tr" : "ct", transform); b.nadeKit = true;
                    b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                    string tp = tipos[(j + f) % 4];
                    if (f < 2) b.Pose("granada_" + tp + jeitos[j], f == 0 ? 0.72f : 0.97f, 0);
                    else b.Pose("granada_fim" + jeitos[j], 0.3f, 0);
                    NaMao(chave++, b, tr);
                }
        }

        /// <summary>Laboratório do body block: pares de bonecos a exatamente <paramref name="d"/> unidades (a distância mínima
        /// entre centros da Sim) — lado a lado, um atrás do outro e de frente — na linha de cima TR, na de baixo CT.</summary>
        public void LabEncosto(V2 p, float giro, float d)
        {
            float g = giro * Mathf.Deg2Rad;
            var frente = new V2(Mathf.Sin(g), -Mathf.Cos(g)); var lado = new V2(Mathf.Cos(g), Mathf.Sin(g));
            for (int lin = 0; lin < 2; lin++)
                for (int k = 0; k < 3; k++)
                {
                    float cx = (k - 1) * 30, cy = (lin - 0.5f) * 30;
                    var c = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                    var off = k == 0 ? lado : frente;
                    var a = new V2(c.x - off.x * d / 2, c.y - off.y * d / 2); var b2 = new V2(c.x + off.x * d / 2, c.y + off.y * d / 2);
                    for (int i = 0; i < 2; i++)
                    {
                        var q = i == 0 ? a : b2; bool tr = lin == 0;
                        var b = Boneco3D.Criar(tr ? "tr" : "ct", transform);
                        float rot = giro + (k == 2 && i == 1 ? 180 : 0);   // de frente: o segundo vira pro primeiro
                        b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, rot, 0));
                        b.Pose("andar_" + Transmissao.Padrao(!tr), 0.25f + 0.5f * i, 0);
                        if (!tr) KitNoCorpo(2000 + k * 2 + i, b, true);
                    }
                }
        }
        // cor do rastro de cada granada (lê de longe qual é qual)
        static Color CorRastro(string kind) => kind switch
        {
            "smoke" => new Color(0.86f, 0.88f, 0.9f), "flash" => new Color(1f, 0.97f, 0.8f), "he" => new Color(1f, 0.38f, 0.25f),
            _ => new Color(1f, 0.62f, 0.2f),
        };
        /// <summary>
        /// Granadas voando com a física da simulação (Match.Passo: quica na parede, quica e rola no chão, cai da borda):
        /// o modelo gira conforme a velocidade (rola quando está no chão) e fica apoiado pelo raio dele; rastro curto na
        /// cor da granada; cada quique solta poeira (chão) ou faísca (parede). Smoke que estourou deixa a lata no chão.
        /// </summary>
        void Props()
        {
            float dt = ultTProps < 0 ? 1 / 30f : Mathf.Clamp(m.t - ultTProps, 0, 0.1f); ultTProps = m.t;
            molotovsNoAr.Clear();
            propsVistos.Clear();
            foreach (var n in m.flying)
            {
                propsVistos.Add(n);
                if (!props.TryGetValue(n, out var g))
                {
                    g = Granada(n.l.kind, n.by != null && n.by.IsT); props[n] = g;
                    g.transform.localScale = Vector3.one * ESC_GRANADA;   // maior que a da mão: lê de longe na câmera de cima
                    var b = new Bounds(g.transform.position, Vector3.zero); bool achou = false;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds); }
                    voos[n] = new Voo { raio = achou ? Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z)) : 0.01f, rot = Quaternion.Euler(0, n.from.x * 7, 0), quiques = n.quiques, paredes = n.paredes };
                }
                var v = voos[n];
                var nv = NadeVisivel(n);
                var p = MalhaMapa.ParaUnity(nv.x, nv.y, nv.z) + Vector3.up * v.raio;
                // giro: rola em volta do eixo de lado ao movimento; no ar gira mais rápido (saiu da mão girando)
                var vel = new Vector3(n.vx, n.vz, -n.vy) * MalhaMapa.ESC; float sp = vel.magnitude;
                var vh = new Vector3(vel.x, 0, vel.z);
                if (sp > 1e-4f)
                {
                    var eixo = vh.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, vh.normalized) : Vector3.right;
                    float graus = sp * dt / Mathf.Max(v.raio, 0.005f) * Mathf.Rad2Deg * (n.noChao ? 1f : 0.35f);
                    v.rot = Quaternion.AngleAxis(graus, eixo) * v.rot;
                }
                g.transform.SetPositionAndRotation(p, v.rot);
                // quique: chão (poeira) ou parede (faísca)
                if (n.paredes > v.paredes) { fx.QuiqueParede(p, vh); Som3D.Quique(p, n.l.kind, true); }
                else if (n.quiques > v.quiques) { fx.QuiqueChao(p - Vector3.up * v.raio); Som3D.Quique(p, n.l.kind, false); }
                v.quiques = n.quiques; v.paredes = n.paredes;
                // rastro só enquanto voa rápido (parada no chão não risca)
                if (v.tem && sp > 0.15f) fx.RastroGranada(v.ultPos, p, CorRastro(n.l.kind));
                v.ultPos = p; v.tem = true;
                // só a molotov do TR voa acesa (pano pegando fogo); a incendiária do CT é granada de pino, como no CS2
                if ((n.l.kind == "incendiaria" || n.l.kind == "molotov") && n.by != null && n.by.IsT) { fx.Chama(g.transform.position, 1 / 30f, 1f); molotovsNoAr.Add(g.transform.position); }
            }
            foreach (var k in props.Keys.ToList())
                if (!propsVistos.Contains(k))
                {
                    var n = (Match.Nade)k;
                    // molotov/incendiária que estourou: o fogo novo herda de quem era (som da garrafa x da incendiária)
                    if ((n.l.kind == "molotov" || n.l.kind == "incendiaria") && props[k] != null)
                        molotovsSumiram.Add((props[k].transform.position, n.l.kind == "incendiaria" || (n.by != null && !n.by.IsT), m.t));
                    // smoke estourou: a lata fica deitada no chão enquanto a fumaça dura
                    if (n.l.kind == "smoke" && voos.TryGetValue(k, out var v))
                    {
                        var g = props[k];
                        g.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(n.x, n.y, n.z) + Vector3.up * v.raio, Quaternion.Euler(0, n.from.x * 7, 90) );
                        latas.Add((g, m.t + SmokeVolume.LIFE));
                    }
                    else Apagar(props[k]);
                    props.Remove(k); voos.Remove(k); nadeAntes.Remove((Match.Nade)k);
                }
            for (int i = latas.Count - 1; i >= 0; i--) if (m.t > latas[i].ate || m.t < latas[i].ate - SmokeVolume.LIFE - 1) { Apagar(latas[i].g); latas.RemoveAt(i); }
            if (c4 != null && !m.bombPlanted) c4.SetActive(false);
            Som3D.MolotovsVoando(molotovsNoAr);   // o pano pegando fogo voando (lista vazia para o som)
        }
    }
}
