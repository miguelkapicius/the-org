using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;

namespace CSM.View3D
{
    /// <summary>
    /// Pilares do mapa (cobertura "pillar" do MapData, ex.: o do bomb B): o de voxel era um cilindro liso de bloco
    /// (usuário: "muito esquisito"); sai das voxels antes da malha e entra o pillar-stone do Kenney Fantasy Town Kit (CC0,
    /// Resources/Kenney), com cor por vértice = brilho do atlas (Art/Kenney/kenney_pedra.py.txt) vezes a pedra da Paleta3D.
    /// Mesma altura da cobertura e pegada no chão (a colisão e o navmesh da Sim não mudam).
    /// </summary>
    public static class Pilar3D
    {
        const byte PEDRA = 15;
        const float RAIO_VOXEL = 3.4f;   // o MapVoxels pinta até 3,2 do centro (base e topo)

        static IEnumerable<MapData.Cover> Pilares()
        {
            foreach (var c in MapData.covers) if (c.kind == "pillar") yield return c;
        }

        static int Base(MapData.Cover c) => MapData.walk[c.r.x0, c.r.y0] ? MapData.floor[c.r.x0, c.r.y0] : MapData.GZ;

        /// <summary>Tira as voxels dos pilares (pedra dentro do círculo, do piso até o topo).</summary>
        public static byte[] SemPilares(byte[] G)
        {
            var g = G; int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ; bool copiou = false;
            foreach (var c in Pilares())
            {
                if (!copiou) { g = (byte[])G.Clone(); copiou = true; }
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f; int f = Base(c);
                for (int x = (int)(cx - RAIO_VOXEL) - 1; x <= (int)(cx + RAIO_VOXEL) + 1; x++)
                    for (int y = (int)(cy - RAIO_VOXEL) - 1; y <= (int)(cy + RAIO_VOXEL) + 1; y++)
                    {
                        if (x < 0 || y < 0 || x >= MapData.NX || y >= NY) continue;
                        float dd = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        if (dd > RAIO_VOXEL) continue;
                        for (int z = Mathf.Max(0, f); z < NZ && z < f + c.h + 1; z++)
                        {
                            int i = (x * NY + y) * NZ + z;
                            if (g[i] == PEDRA) g[i] = 0;
                        }
                    }
            }
            return g;
        }

        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            var pf = Resources.Load<GameObject>("Kenney/pillar-stone");
            if (pf == null) { Debug.LogWarning("[Pilar3D] faltou Resources/Kenney/pillar-stone"); return null; }
            var raiz = new GameObject("Pilares (Kenney)"); raiz.transform.SetParent(pai, false);
            mat = new Material(mat) { name = "Maquete pilar" }; mat.SetFloat("_UsarTopo", 0); mat.SetFloat("_UsarVertice", 1);
            Color pedra = Paleta3D.Vertice(PEDRA, linear);
            foreach (var c in Pilares())
            {
                var g = Object.Instantiate(pf, raiz.transform);
                g.transform.SetPositionAndRotation(Vector3.zero, pf.transform.localRotation);   // a raiz traz o giro de eixo
                g.transform.localScale = pf.transform.localScale;
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                {
                    var me = Object.Instantiate(mf.sharedMesh); var cs = me.colors;
                    if (cs == null || cs.Length != me.vertexCount) { cs = new Color[me.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = Color.white; }
                    // brilho do atlas (0,5..1) vira sombreado da pedra da paleta: frisos e base um pouco mais escuros
                    for (int i = 0; i < cs.Length; i++) { float l = Mathf.Clamp01((cs[i].r - 0.5f) / 0.5f); cs[i] = Color.Lerp(pedra * 0.72f, pedra, l); cs[i].a = 1; }
                    me.colors = cs; mf.sharedMesh = me;
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r != null) { r.sharedMaterials = new[] { mat }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                }
                var b = new Bounds(); bool ok = false;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
                if (!ok) continue;
                // altura da cobertura da Sim e largura do pilar de voxel (6,8 do jogo): na proporção do kit ele saía um
                // palito (usuário: "era pra ser um pilar kkk"). Escala por eixo do mundo num pai (a raiz do FBX é girada)
                float alt = c.h * MalhaMapa.ESC, larg = 6.8f * MalhaMapa.ESC;
                var pai2 = new GameObject("pilar").transform; pai2.SetParent(raiz.transform, false);
                g.transform.SetParent(pai2, true);
                g.transform.position = -new Vector3(b.center.x, b.min.y, b.center.z);   // pé no meio, na origem do pai
                pai2.localScale = new Vector3(larg / Mathf.Max(0.001f, b.size.x), alt / Mathf.Max(0.001f, b.size.y), larg / Mathf.Max(0.001f, b.size.z));
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f;
                pai2.position = MalhaMapa.ParaUnity(cx, cy, Base(c));
                Debug.Log($"[Pilar3D] pilar em ({cx:0},{cy:0}) altura {alt:0.00} u, largura 6,8 do jogo (kit {b.size.x / b.size.y:0.00} de largura por altura)");
            }
            return raiz;
        }
    }
}
