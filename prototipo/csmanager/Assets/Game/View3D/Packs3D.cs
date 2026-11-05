using System.Collections.Generic;
using UnityEngine;

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
        enum Tipo { Barril, Sacos, Caixote }
        struct Item { public Tipo tipo; public float x, y, f, h; public int sem; }
        static readonly List<Item> itens = new List<Item>();
        static readonly string[] CAIXOTES = { "caixote_cebola", "caixote_batata", "caixote_tomate", "caixote_alface", "caixote_cenoura" };

        /// <summary>Os modelos estão no Resources? (sem eles o Adornos3D volta a desenhar os de código).</summary>
        public static bool Pronto => Resources.Load<GameObject>("Packs/barril") != null;

        public static void Limpar() => itens.Clear();
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
            foreach (var it in itens)
            {
                var rng = new System.Random(it.sem * 7 + 3);
                string nome = it.tipo == Tipo.Barril ? "barril" : it.tipo == Tipo.Sacos ? "sacos" : CAIXOTES[rng.Next(CAIXOTES.Length)];
                var (me, b) = Malha(nome); if (me == null) continue;
                var go = new GameObject(nome); go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = me;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Vector3 esc; float giro;
                if (it.tipo == Tipo.Barril)
                {   // altura da cobertura e o mesmo diâmetro do barril de código (6 do jogo)
                    float d = 6f * MalhaMapa.ESC;
                    esc = new Vector3(d / b.size.x, it.h * MalhaMapa.ESC / b.size.y, d / b.size.z); giro = (float)rng.NextDouble() * 360f;
                }
                else if (it.tipo == Tipo.Sacos)
                {   // pilha de sacos com ~10 do jogo de largura
                    float k = 10f * MalhaMapa.ESC / Mathf.Max(b.size.x, b.size.z); esc = Vector3.one * k; giro = (float)rng.NextDouble() * 360f;
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
                n++;
            }
            Debug.Log($"[Packs3D] {n} props de pack ({itens.Count} marcados)");
            return raiz;
        }
    }
}
