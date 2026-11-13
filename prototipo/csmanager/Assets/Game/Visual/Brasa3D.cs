using System.Collections.Generic;
using UnityEngine;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Props da Brasa (mapa 2, clima da Inferno) com cara própria (usuário: não repetir os da Vila): caixas, toras,
    /// carroças, fonte do B e sacos de cimento viram modelos do Survival Kit / Graveyard Kit / Fantasy Town (Kenney,
    /// Resources/Packs). Cada modelo ocupa exatamente o retângulo e a altura da cobertura da Sim (o que se vê é o que
    /// tapa). Os barris continuam no Adornos3D/Packs3D (ksv_barrel). Só desenho: colisão e navmesh não mudam.
    /// Na Vila não faz nada.
    /// </summary>
    public static class Brasa3D
    {
        /// <summary>Brasa montada e kit no Resources (sem o kit, cada um desenha o de sempre).</summary>
        public static bool Ativo => MapData.MAPA == "brasa" && Resources.Load<GameObject>("Packs/ksv_box-large") != null;

        /// <summary>Tipos de cobertura que viram modelo aqui (o MapVoxels não desenha, o Caixas3D/Chafariz3D/Adornos3D pulam).</summary>
        public static bool Cuida(string kind)
        {
            kind = CSM.View.MapVoxels.Tipo(kind);   // crate_pilha = crate (só a navegação muda)
            return Ativo && (kind == "crate" || kind == "logs" || kind == "cart" || kind == "fountain" || kind == "sacks");
        }

        static int Base(MapData.Cover c) => MapData.walk[c.r.x0, c.r.y0] ? MapData.floor[c.r.x0, c.r.y0] : MapData.GZ;

        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            if (!Ativo) return null;
            var raiz = new GameObject("Props da Brasa (Kenney Survival/Graveyard)"); raiz.transform.SetParent(pai, false);
            mat = new Material(mat) { name = "Maquete props da Brasa" }; mat.SetFloat("_UsarTopo", 0); mat.SetFloat("_UsarVertice", 1);
            var malhas = new Dictionary<string, (Mesh me, Bounds b)>();
            (Mesh me, Bounds b) Malha(string nome)
            {
                if (malhas.TryGetValue(nome, out var r)) return r;
                var pf = Resources.Load<GameObject>("Packs/" + nome);
                if (pf == null) { Debug.LogWarning("[Brasa3D] faltou Resources/Packs/" + nome); malhas[nome] = (null, default); return (null, default); }
                var mfs = pf.GetComponentsInChildren<MeshFilter>(); var cis = new List<CombineInstance>();
                var w2l = pf.transform.worldToLocalMatrix;
                foreach (var mf in mfs) cis.Add(new CombineInstance { mesh = mf.sharedMesh, transform = pf.transform.localToWorldMatrix * (w2l * mf.transform.localToWorldMatrix) });
                var me = new Mesh { name = "brasa " + nome, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                me.CombineMeshes(cis.ToArray(), true, true);
                var cs = me.colors;
                if (cs == null || cs.Length != me.vertexCount) { cs = new Color[me.vertexCount]; for (int i = 0; i < cs.Length; i++) cs[i] = Color.white; }
                if (linear) for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                me.colors = cs; me.RecalculateBounds();
                r = (me, me.bounds); malhas[nome] = r; return r;
            }
            int n = 0;
            // modelo esticado pra caber em (largura x, altura, fundo y) do jogo, centro (cx, cy), pé em f; giro em graus
            // (90/270 troca os eixos: o comprido do modelo vai pro comprido do retângulo)
            void Peca(string nome, float cx, float cy, float f, float lx, float h, float ly, float giro)
            {
                var (me, b) = Malha(nome); if (me == null) return;
                var go = new GameObject(nome); go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = me;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                bool troca = Mathf.Abs(Mathf.DeltaAngle(giro, 90)) < 1 || Mathf.Abs(Mathf.DeltaAngle(giro, 270)) < 1;
                float E = MalhaMapa.ESC, ax = troca ? ly : lx, az = troca ? lx : ly;
                var esc = new Vector3(ax * E / Mathf.Max(1e-4f, b.size.x), h * E / Mathf.Max(1e-4f, b.size.y), az * E / Mathf.Max(1e-4f, b.size.z));
                go.transform.localScale = esc; go.transform.rotation = Quaternion.Euler(0, giro, 0);
                var c = new Vector3(b.center.x * esc.x, b.min.y * esc.y, b.center.z * esc.z);
                go.transform.position = MalhaMapa.ParaUnity(cx, cy, f) - go.transform.rotation * c;
                n++;
            }
            // giro que põe o lado comprido do modelo no lado comprido do retângulo
            float Alinha(string nome, float lx, float ly)
            {
                var (me, b) = Malha(nome); if (me == null) return 0;
                bool modeloEmX = b.size.x >= b.size.z, retEmX = lx >= ly;
                return modeloEmX == retEmX ? 0 : 90;
            }
            foreach (var c in MapData.covers)
            {
                if (!Cuida(c.kind)) continue;
                float f = Base(c), w = c.r.x1 - c.r.x0, d = c.r.y1 - c.r.y0, cx = (c.r.x0 + c.r.x1) * 0.5f, cy = (c.r.y0 + c.r.y1) * 0.5f;
                var rng = new System.Random(c.r.x0 * 131 + c.r.y0 * 7);
                switch (CSM.View.MapVoxels.Tipo(c.kind))
                {
                    case "crate":
                    {
                        // caixote do Survival Kit, um por casa da planta (25 x 25); alto (20+) = pilha de dois, o de cima menor
                        int nx = Mathf.Max(1, Mathf.RoundToInt(w / 25f)), ny = Mathf.Max(1, Mathf.RoundToInt(d / 25f));
                        float sx = w / nx, sy = d / ny;
                        for (int i = 0; i < nx; i++)
                            for (int j = 0; j < ny; j++)
                            {
                                float px = c.r.x0 + sx * (i + 0.5f), py = c.r.y0 + sy * (j + 0.5f), giro = rng.Next(2) * 180f;
                                if (c.h >= 20)
                                {
                                    Peca("ksv_box-large", px, py, f, sx, 14f, sy, giro);
                                    Peca("ksv_box", px + (float)(rng.NextDouble() - 0.5) * 3f, py + (float)(rng.NextDouble() - 0.5) * 3f, f + 14f, sx * 0.62f, c.h - 14f, sy * 0.62f, rng.Next(4) * 90f + 12f);
                                }
                                else Peca("ksv_box-large", px, py, f, sx, c.h, sy, giro);
                            }
                        break;
                    }
                    case "logs":
                    {
                        // pilha de toras: 3 embaixo, 2 em cima, deitadas no comprido
                        bool emX = w >= d; float comp = emX ? w : d, larg = emX ? d : w, r = Mathf.Min(larg / 6f, c.h / 3.7f) * 1.0f;
                        float giro = Alinha("ksv_tree-log", emX ? comp : r * 2, emX ? r * 2 : comp);
                        for (int k = 0; k < 2; k++)
                        {
                            int q = 3 - k;
                            for (int j = 0; j < q; j++)
                            {
                                float off = (j - (q - 1) * 0.5f) * r * 2f;
                                float px = emX ? cx + (float)(rng.NextDouble() - 0.5) * 2f : cx + off, py = emX ? cy + off : cy + (float)(rng.NextDouble() - 0.5) * 2f;
                                Peca("ksv_tree-log", px, py, f + k * r * 1.7f, emX ? comp * 0.95f : r * 2f, r * 2f, emX ? r * 2f : comp * 0.95f, giro);
                            }
                        }
                        break;
                    }
                    case "cart":
                        Peca("kenney_cart-high", cx, cy, f, w, c.h, d, Alinha("kenney_cart-high", w, d) + rng.Next(2) * 180f);
                        break;
                    case "fountain":
                        Peca("kenney_fountain-square-detail", cx, cy, f, w, c.h, d, 0);
                        break;
                    case "sacks":
                    {
                        // sacos de cimento: fardos do Graveyard Kit lado a lado (um por meia casa)
                        int nx = Mathf.Max(1, Mathf.RoundToInt(w / 12.5f)), ny = Mathf.Max(1, Mathf.RoundToInt(d / 12.5f));
                        float sx = w / nx, sy = d / ny;
                        for (int i = 0; i < nx; i++)
                            for (int j = 0; j < ny; j++)
                                Peca("kgy_hay-bale", c.r.x0 + sx * (i + 0.5f), c.r.y0 + sy * (j + 0.5f), f, sx * 0.94f, c.h * ((i + j) % 2 == 0 ? 1f : 0.85f), sy * 0.94f, rng.Next(2) * 180f);
                        break;
                    }
                }
            }
            Debug.Log($"[Brasa3D] {n} peças do kit da Brasa");
            return raiz;
        }
    }
}
