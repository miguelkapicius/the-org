using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Props de pack CC0 (poly.pizza: barril e sacos do Quaternius, caixotes de legume da KayKit / Kay Lousberg) no lugar
    /// dos feitos por código (usuário: "nenhum prop vai ser feito por você"). Os GLB viraram FBX com a cor de cada face no
    /// vértice (Resources/Packs/pack_cor.py.txt). O Adornos3D marca onde vai cada um (mesmas posições e sementes das
    /// coberturas da Sim: a colisão não muda) e aqui eles entram como objeto.
    /// </summary>
    public static class Packs3D
    {
        enum Tipo { Barril, Sacos, Caixote, Bandeira, Banca, Vaso }
        struct Item { public Tipo tipo; public float x, y, f, h, lado; public int sem; public Vector2 n; }
        static readonly List<Item> itens = new List<Item>(), fachada = new List<Item>();
        static readonly string[] CAIXOTES = { "caixote_cebola", "caixote_batata", "caixote_tomate", "caixote_alface", "caixote_cenoura" };

        /// <summary>Os modelos estão no Resources? (sem eles o Adornos3D volta a desenhar os de código).</summary>
        public static bool Pronto => Resources.Load<GameObject>("Packs/barril") != null;

        public static void Limpar() => itens.Clear();
        /// <summary>Fachada3D: zera a lista dela (é montada antes do Adornos3D).</summary>
        public static void LimparFachada() => fachada.Clear();
        /// <summary>Bandeirola do Kenney pendurada na parede (no lugar do azulejo com lanterna): ponto na face da parede
        /// (jogo), normal pra rua (n.x, n.y do jogo) e altura do chão.</summary>
        public static void Bandeira(float x, float y, float f, Vector2 n, int sem) => fachada.Add(new Item { tipo = Tipo.Bandeira, x = x, y = y, f = f, n = n, sem = sem });
        /// <summary>Vaso com planta do Kenney Furniture Kit no pé da parede (no lugar do vaso de cilindro com a planta
        /// da Quaternius enfiada dentro: usuário "vasinho bugado"). alto = vaso alto (pottedPlant); senão um dos baixinhos
        /// (plantSmall1-3). h = altura (jogo).</summary>
        public static void Vaso(float x, float y, float f, int sem, bool alto, float h) => fachada.Add(new Item { tipo = Tipo.Vaso, x = x, y = y, f = f, sem = sem, lado = alto ? 1 : 0, h = h });
        static readonly string[] VASOS_BAIXOS = { "kenney_plantSmall1", "kenney_plantSmall2", "kenney_plantSmall3" };

        // ------------------------------------------------------------------ bancas do mercado (cobertura "stall")
        static IEnumerable<MapData.Cover> Bancas() { foreach (var c in MapData.covers) if (c.kind == "stall") yield return c; }
        static int BaseC(MapData.Cover c) => MapData.walk[c.r.x0, c.r.y0] ? MapData.floor[c.r.x0, c.r.y0] : MapData.GZ;
        /// <summary>Tira as voxels das bancas de bloco (mesa, mercadoria, toldo e pés: MapVoxels "stall").</summary>
        public static byte[] SemBancas(byte[] G)
        {
            if (!Pronto) return G;
            var g = (byte[])G.Clone(); int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ;
            foreach (var c in Bancas())
            {
                int f = BaseC(c);
                for (int x = c.r.x0; x < c.r.x1; x++) for (int y = c.r.y0; y < c.r.y1; y++)
                    for (int z = System.Math.Max(0, f); z < NZ && z <= f + 26; z++) { int i = (x * NY + y) * NZ + z; if (g[i] == 10 || g[i] == 11 || g[i] == 30 || g[i] == 17 || g[i] == 41 || g[i] == 5 || g[i] == 8) g[i] = 0; }
            }
            return g;
        }
        // ------------------------------------------------------------------ poço (cobertura "well")
        static IEnumerable<MapData.Cover> Pocos() { foreach (var c in MapData.covers) if (c.kind == "well") yield return c; }
        /// <summary>Tira as voxels do poço de bloco (aro de pedra 15 e fundo escuro 19 do MapVoxels "well").</summary>
        public static byte[] SemPocos(byte[] G)
        {
            if (Resources.Load<GameObject>("Packs/kenney_fountain-round") == null) return G;
            var g = (byte[])G.Clone(); int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ;
            foreach (var c in Pocos())
            {
                int f = BaseC(c); float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f;
                for (int x = (int)cx - 6; x <= (int)cx + 6; x++) for (int y = (int)cy - 6; y <= (int)cy + 6; y++)
                {
                    if (x < 0 || y < 0 || x >= MapData.NX || y >= NY) continue;
                    float dd = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)); if (dd > 3.6f) continue;
                    for (int z = System.Math.Max(0, f); z < NZ && z <= f + c.h + 1; z++) { int i = (x * NY + y) * NZ + z; if (g[i] == 15 || g[i] == 19) g[i] = 0; }
                }
            }
            return g;
        }
        public static void Barril(float x, float y, float f, float h) => itens.Add(new Item { tipo = Tipo.Barril, x = x, y = y, f = f, h = h });
        public static void Sacos(float x, float y, float f, int sem) => itens.Add(new Item { tipo = Tipo.Sacos, x = x, y = y, f = f, sem = sem });
        public static void Caixote(float x, float y, float f, int sem) => itens.Add(new Item { tipo = Tipo.Caixote, x = x, y = y, f = f, sem = sem });

        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            var raiz = new GameObject("Props de pack (Quaternius, KayKit)"); raiz.transform.SetParent(pai, false);
            mat = new Material(mat) { name = "Maquete props de pack" }; mat.SetFloat("_UsarTopo", 0); mat.SetFloat("_UsarVertice", 1);
            var malhas = new Dictionary<string, (Mesh me, Bounds b)>();
            (Mesh me, Bounds b) Malha(string nome)
            {
                if (malhas.TryGetValue(nome, out var r)) return r;
                var pf = Resources.Load<GameObject>("Packs/" + nome);
                if (pf == null) { Debug.LogWarning("[Packs3D] faltou Resources/Packs/" + nome); malhas[nome] = (null, default); return (null, default); }
                // uma malha só, já no espaço da raiz do prefab (o FBX traz o giro de eixo na raiz), cor de vértice no espaço
                // de cor do projeto
                var mfs = pf.GetComponentsInChildren<MeshFilter>(); var cis = new List<CombineInstance>();
                var w2l = pf.transform.worldToLocalMatrix;
                foreach (var mf in mfs) cis.Add(new CombineInstance { mesh = mf.sharedMesh, transform = pf.transform.localToWorldMatrix * (w2l * mf.transform.localToWorldMatrix) });
                var me = new Mesh { name = "pack " + nome, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                me.CombineMeshes(cis.ToArray(), true, true);
                var cs = me.colors;
                if (cs == null || cs.Length != me.vertexCount) { cs = new Color[me.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = Color.white; }
                if (linear) for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                me.colors = cs; me.RecalculateBounds();
                r = (me, me.bounds); malhas[nome] = r; return r;
            }
            int n = 0;
            // bancas: 2-3 do Kenney lado a lado no comprimento da cobertura (mesmo espaço: a colisão da Sim não muda)
            var todos = new List<Item>(itens); todos.AddRange(fachada);
            foreach (var c in Bancas())
            {
                bool emX = c.r.x1 - c.r.x0 >= c.r.y1 - c.r.y0; float comp = emX ? c.r.x1 - c.r.x0 : c.r.y1 - c.r.y0, larg = emX ? c.r.y1 - c.r.y0 : c.r.x1 - c.r.x0;
                int q = Mathf.Max(1, Mathf.RoundToInt(comp / Mathf.Max(8f, larg * 1.15f))); float passo = comp / q;
                for (int i = 0; i < q; i++)
                {
                    float px = emX ? c.r.x0 + passo * (i + 0.5f) : (c.r.x0 + c.r.x1) * 0.5f, py = emX ? (c.r.y0 + c.r.y1) * 0.5f : c.r.y0 + passo * (i + 0.5f);
                    todos.Add(new Item { tipo = Tipo.Banca, x = px, y = py, f = BaseC(c), lado = Mathf.Min(passo, larg * 1.3f), sem = c.r.x0 * 13 + i });
                }
            }
            foreach (var it in todos)
            {
                var rng = new System.Random(it.sem * 7 + 3);
                string nome = it.tipo == Tipo.Barril ? "barril" : it.tipo == Tipo.Sacos ? "sacos"
                    : it.tipo == Tipo.Bandeira ? (rng.Next(2) == 0 ? "kenney_banner-red" : "kenney_banner-green")
                    : it.tipo == Tipo.Banca ? (rng.Next(2) == 0 ? "kenney_stall-red" : "kenney_stall-green")
                    : it.tipo == Tipo.Vaso ? (it.lado > 0 ? "kenney_pottedPlant" : VASOS_BAIXOS[rng.Next(VASOS_BAIXOS.Length)]) : CAIXOTES[rng.Next(CAIXOTES.Length)];
                var (me, b) = Malha(nome); if (me == null) continue;
                var go = new GameObject(nome); go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = me;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Vector3 esc; float giro;
                if (it.tipo == Tipo.Barril)
                {   // um tico maior que a cobertura (usuário: "aumenta um pouco o tamanho dos barris"): 7,4 de diâmetro e 20% mais alto
                    float d = 7.4f * MalhaMapa.ESC;
                    esc = new Vector3(d / b.size.x, it.h * 1.2f * MalhaMapa.ESC / b.size.y, d / b.size.z); giro = (float)rng.NextDouble() * 360f;
                }
                else if (it.tipo == Tipo.Sacos)
                {   // pilha de sacos com ~10 do jogo de largura
                    float k = 10f * MalhaMapa.ESC / Mathf.Max(b.size.x, b.size.z); esc = Vector3.one * k; giro = (float)rng.NextDouble() * 360f;
                }
                else if (it.tipo == Tipo.Bandeira)
                {   // 16 de altura, lado fino (o pano) de frente pra rua: gira o eixo fino do modelo pra normal da parede
                    float k = 16f * MalhaMapa.ESC / b.size.y; esc = Vector3.one * k;
                    var nU = new Vector3(it.n.x, 0, -it.n.y);
                    giro = Quaternion.LookRotation(nU).eulerAngles.y + (b.size.x < b.size.z ? 90f : 0f);
                }
                else if (it.tipo == Tipo.Vaso)
                {   // altura pedida, proporção do kit, girado à toa (planta não tem frente)
                    float k = it.h * MalhaMapa.ESC / b.size.y; esc = Vector3.one * k; giro = (float)rng.NextDouble() * 360f;
                }
                else if (it.tipo == Tipo.Banca)
                {   // ocupa o lado da cobertura; o toldo sobe pela proporção do kit
                    float k = it.lado * MalhaMapa.ESC / Mathf.Max(b.size.x, b.size.z); esc = Vector3.one * k; giro = rng.Next(2) * 180f;
                }
                else
                {   // caixote de feira com ~7 do jogo de lado, alinhado à grade com um tico de torto
                    float k = 7f * MalhaMapa.ESC / Mathf.Max(b.size.x, b.size.z); esc = Vector3.one * k; giro = rng.Next(4) * 90f + ((float)rng.NextDouble() - 0.5f) * 16f;
                }
                go.transform.localScale = esc;
                go.transform.rotation = Quaternion.Euler(0, giro, 0);
                // pé no piso, centro no ponto (o conversor deixou o pé em y = 0 e o centro no meio)
                var c = new Vector3(b.center.x * esc.x, b.min.y * esc.y, b.center.z * esc.z);
                go.transform.position = MalhaMapa.ParaUnity(it.x, it.y, it.f) - go.transform.rotation * c;
                // bandeirola: o pé dela fica a 12 do chão, encostada na parede (meio pano pra fora)
                if (it.tipo == Tipo.Bandeira) go.transform.position += Vector3.up * 12f * MalhaMapa.ESC;
                n++;
            }
            // poço (cobertura "well"): o de voxel era um aro fino que o borrão picotava em 4 varetas cinza (usuário: "objeto
            // cinza meio quebrado" no bomb B; "pega um poço do kenney"). Montado com peças do Kenney Fantasy Town Kit: aro
            // de pedra com água (fountain-round, mais alto), dois postes (poles) e o telhadinho (roof-point) em telha
            foreach (var c in Pocos())
            {
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f, d = Mathf.Max(c.r.x1 - c.r.x0, c.r.y1 - c.r.y0);
                var pe = MalhaMapa.ParaUnity(cx, cy, BaseC(c)); float E = MalhaMapa.ESC;
                float giro = ((c.r.x0 * 7 + c.r.y0 * 13) % 4) * 90f;
                // tamanho alvo em unidades do jogo (largura, altura, fundo) no eixo do modelo; pé no ponto
                void Peca(string nome, Vector3 alvo, float subir, Color? pinta = null)
                {
                    var (me, b) = Malha(nome); if (me == null) return;
                    if (pinta is Color cor)
                    {   // repinta mantendo o sombreado do kit (o telhadinho verde vira telha da vila)
                        me = Object.Instantiate(me); var cs = me.colors; var cl = linear ? cor.linear : cor;
                        for (int i = 0; i < cs.Length; i++) { float l = 0.75f + 0.5f * Mathf.Clamp01((cs[i].r + cs[i].g + cs[i].b) / 3f / 0.5f - 0.5f); cs[i] = new Color(cl.r * l, cl.g * l, cl.b * l, 1); }
                        me.colors = cs;
                    }
                    var go = new GameObject("poço " + nome); go.transform.SetParent(raiz.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = me;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    var esc = new Vector3(alvo.x * E / Mathf.Max(1e-4f, b.size.x), alvo.y * E / Mathf.Max(1e-4f, b.size.y), alvo.z * E / Mathf.Max(1e-4f, b.size.z));
                    go.transform.localScale = esc; go.transform.rotation = Quaternion.Euler(0, giro, 0);
                    var cc = new Vector3(b.center.x * esc.x, b.min.y * esc.y, b.center.z * esc.z);
                    go.transform.position = pe + Vector3.up * subir * E - go.transform.rotation * cc;
                    n++;
                }
                // um tico maior (usuário: "aumenta um pouco o tamanho... do poço"): aro passa a borda da cobertura
                float aro = d * 1.15f, h = Mathf.Max(15f, c.h + 3f);
                Peca("kenney_fountain-round", new Vector3(aro, 5.5f, aro), 0);
                // postes: o kit traz os dois num modelo (finos num eixo, separados no outro); atravessam o aro
                var (mp, bp) = Malha("kenney_poles");
                if (mp != null)
                {
                    bool finoX = bp.size.x < bp.size.z;
                    Peca("kenney_poles", finoX ? new Vector3(0.9f, h + 1f, aro * 0.92f) : new Vector3(aro * 0.92f, h + 1f, 0.9f), 0);
                }
                // telhadinho do tamanho do aro e pontudo (com 1,2 do aro e baixo ele parecia guarda-sol)
                Peca("kenney_roof-point", new Vector3(aro * 1.02f, 4.6f, aro * 1.02f), h, new Color(0xE0 / 255f, 0x70 / 255f, 0x3F / 255f));
            }
            Debug.Log($"[Packs3D] {n} props de pack ({itens.Count} marcados)");
            return raiz;
        }
    }
}
