using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;

namespace CSM.View3D
{
    /// <summary>
    /// Chafariz da base TR (cobertura "fountain" do MapData): o de voxel (bacia de pedra + água + pilar) saía do borrão
    /// "sem resolução"; sai das voxels antes da malha e entra o fountain-round-detail do Kenney Fantasy Town Kit (CC0,
    /// Resources/Kenney), pintado com a Paleta3D (pedra 15, água 9) pela cor de vértice que o kenney_cor.py gravou
    /// (branco = pedra, azul = água). Mesma pegada no chão e altura do pilar que a cobertura da Sim (a colisão não muda).
    /// </summary>
    public static class Chafariz3D
    {
        const byte PEDRA = 15, AGUA = 9;

        static IEnumerable<MapData.Cover> Fontes()
        {
            foreach (var c in MapData.covers) if (c.kind == "fountain") yield return c;
        }

        static float Chao(MapData.Cover c)
        {
            // fora da bacia (no meio dela o pilar não é piso)
            float cy = (c.r.y0 + c.r.y1) / 2f;
            return NavGrid.ZAt(new V2(c.r.x0 - 3, cy));
        }

        /// <summary>Tira as voxels do chafariz (pedra e água dentro do círculo, do piso pra cima).</summary>
        public static byte[] SemChafariz(byte[] G)
        {
            var g = G; int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ; bool copiou = false;
            foreach (var c in Fontes())
            {
                if (!copiou) { g = (byte[])G.Clone(); copiou = true; }
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f, fr = (c.r.x1 - c.r.x0) / 2f + 0.6f;
                int f = Mathf.RoundToInt(Chao(c));
                for (int x = (int)(cx - fr) - 1; x <= (int)(cx + fr) + 1; x++)
                    for (int y = (int)(cy - fr) - 1; y <= (int)(cy + fr) + 1; y++)
                    {
                        if (x < 0 || y < 0 || x >= MapData.NX || y >= NY) continue;
                        float dd = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        if (dd > fr) continue;
                        for (int z = Mathf.Max(0, f); z < NZ && z < f + c.h + 2; z++)
                        {
                            int i = (x * NY + y) * NZ + z;
                            if (g[i] == PEDRA || g[i] == AGUA) g[i] = 0;
                        }
                    }
            }
            return g;
        }

        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            var pf = Resources.Load<GameObject>("Kenney/fountain-round-detail");
            if (pf == null) { Debug.LogWarning("[Chafariz3D] faltou Resources/Kenney/fountain-round-detail"); return null; }
            var raiz = new GameObject("Chafariz (Kenney)"); raiz.transform.SetParent(pai, false);
            // o material do mapa repinta o que olha pra cima com o mapa visto de cima (piso): a água e a borda viravam ladrilho
            mat = new Material(mat) { name = "Maquete chafariz" }; mat.SetFloat("_UsarTopo", 0); mat.SetFloat("_UsarVertice", 1);
            Color pedra = Paleta3D.Vertice(PEDRA, linear), agua = Paleta3D.Vertice(AGUA, linear);
            foreach (var c in Fontes())
            {
                var g = Object.Instantiate(pf, raiz.transform);
                // a raiz do FBX traz o giro de eixo (Blender Z pra cima -> Unity Y): não zerar
                g.transform.SetPositionAndRotation(Vector3.zero, pf.transform.localRotation);
                g.transform.localScale = pf.transform.localScale;
                // pinta: cópia da malha com a cor da paleta no lugar do branco/azul do conversor
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                {
                    var me = Object.Instantiate(mf.sharedMesh); var cs = me.colors;
                    if (cs == null || cs.Length != me.vertexCount) { cs = new Color[me.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = Color.white; }
                    for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].b > cs[i].r + 0.2f ? agua : pedra;
                    me.colors = cs; mf.sharedMesh = me;
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r != null) { r.sharedMaterials = new[] { mat }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                }
                // tamanho: o diâmetro da cobertura no chão
                var b = new Bounds(); bool ok = false;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
                if (!ok) continue;
                float diam = (c.r.x1 - c.r.x0) * MalhaMapa.ESC, k = diam / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                g.transform.localScale = pf.transform.localScale * k;
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f;
                var chao = MalhaMapa.ParaUnity(cx, cy, Chao(c));
                // o pivô do modelo pode não estar no pé nem no meio: alinha pela caixa
                var centro = new Vector3(b.center.x, b.min.y, b.center.z) * k;
                g.transform.position = chao - centro;
                Debug.Log($"[Chafariz3D] chafariz em ({cx:0},{cy:0}) diâmetro {diam:0.00} u, escala {k:0.000}");
            }
            return raiz;
        }
    }
}
