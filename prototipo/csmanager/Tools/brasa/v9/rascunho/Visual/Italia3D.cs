using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Cara de vila italiana da Brasa (usuário: "uma vila italiana não parece uma vila de Marrocos"). Toma o lugar dos
    /// enfeites marroquinos do Fachada3D, só na Brasa:
    ///   reboco de cor por lote (ocre, salmão, creme, amarelo), janela em arco com persiana verde (aberta ou fechada),
    ///   porta de madeira com batente de pedra, grade de ferro rente à janela (sacada francesa: nada sai mais de ~3 do
    ///   muro, o navmesh não muda), trepadeira, varal com roupa atravessando rua estreita (acima da cabeça) e telhado de
    ///   duas águas com telha capa-canal nos lotes inteiros de prédio.
    /// Peças do Medieval Village MegaKit (Quaternius, CC0) em Resources/Italia, feitas pelo ItaliaImport. As malhas são
    /// juntadas por material (poucas draw calls). Só desenho: colisão, navegação e Sim não mudam.
    /// </summary>
    public static class Italia3D
    {
        /// <summary>Brasa montada e peças no Resources.</summary>
        public static bool Ativo => MapData.MAPA == "brasa" && Resources.Load<GameObject>("Italia/it_Window_Thin_Round1") != null;

        // ------------------------------------------------------------------ medidas
        /// <summary>Metro do pack em unidades do jogo (boneco = 25): 12,5 = maquete um pouco menor que o real, pra janela
        /// e porta caberem na fachada de um andar da Brasa (36 a 44 de altura).</summary>
        const float METRO = 12.5f;
        /// <summary>Giro do modelo do pack pra frente dele apontar pra rua (graus em volta do Y da Unity). Calibrar na captura.</summary>
        public static float GiroPack = 180f;
        /// <summary>Lote da planta (3 x 3 casas de 25): cada lote tem a sua cor de reboco e o seu telhado.</summary>
        const int LOTE = 75;
        /// <summary>Materiais do reboco por lote (Paleta3D.UsarBrasa pinta 52-54; 6 = o reboco de sempre).</summary>
        static readonly byte[] REBOCO = { 6, 52, 53, 54 };
        static readonly int[] PINTADOS = { 5, 8, 9, 11, 12, 13, 17, 18, 19, 30, 32, 41 };
        const bool TELHADO_DO_PACK = false;

        static uint Hash(int a, int b) => ((uint)(a * 73856093) ^ (uint)(b * 19349663)) * 2654435761u;
        /// <summary>Cor do reboco do lote (os mesmos lotes do telhado: cada casa da fileira tem a sua cor).</summary>
        public static byte RebocoDoLote(int x, int y) { var (a, b) = TelhadoItalia.Lote(x, y); return REBOCO[(Hash(a, b) >> 16) % REBOCO.Length]; }

        // ------------------------------------------------------------------ peças do pack, juntadas por material
        class Peca { public List<(Material mat, Mesh mesh)> partes = new List<(Material, Mesh)>(); public Bounds b; }
        static readonly Dictionary<string, Peca> pecas = new Dictionary<string, Peca>();
        static readonly Dictionary<Material, List<CombineInstance>> lote = new Dictionary<Material, List<CombineInstance>>();

        static Peca Carrega(string nome)
        {
            if (pecas.TryGetValue(nome, out var p)) return p;
            p = null;
            var pf = Resources.Load<GameObject>("Italia/" + nome);
            if (pf == null) Debug.LogWarning("[Italia3D] faltou Resources/Italia/" + nome);
            else
            {
                p = new Peca(); bool ok = false;
                var porMat = new Dictionary<Material, List<CombineInstance>>();
                var w2l = pf.transform.worldToLocalMatrix;
                foreach (var mr in pf.GetComponentsInChildren<MeshRenderer>())
                {
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var m = mf.sharedMesh; var tr = pf.transform.localToWorldMatrix * (w2l * mf.transform.localToWorldMatrix);
                    for (int i = 0; i < m.subMeshCount; i++)
                    {
                        var mat = mr.sharedMaterials[Mathf.Min(i, mr.sharedMaterials.Length - 1)];
                        if (!porMat.TryGetValue(mat, out var l)) porMat[mat] = l = new List<CombineInstance>();
                        l.Add(new CombineInstance { mesh = m, subMeshIndex = i, transform = tr });
                    }
                }
                foreach (var kv in porMat)
                {
                    var me = new Mesh { name = nome + " " + kv.Key.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    me.CombineMeshes(kv.Value.ToArray(), true, true); me.RecalculateBounds();
                    p.partes.Add((kv.Key, me));
                    if (!ok) { p.b = me.bounds; ok = true; } else p.b.Encapsulate(me.bounds);
                }
            }
            pecas[nome] = p; return p;
        }

        /// <summary>Põe a peça: pé em 'pe' (Unity), frente pra 'normal' (Unity, horizontal), escala em metros do pack.</summary>
        static void Poe(string nome, Vector3 pe, Vector3 normal, Vector3 escala, float giroExtra = 0)
        {
            var p = Carrega(nome); if (p == null) return;
            var q = Quaternion.LookRotation(new Vector3(normal.x, 0, normal.z).normalized, Vector3.up) * Quaternion.Euler(0, GiroPack + giroExtra, 0);
            var s = escala * (METRO * MalhaMapa.ESC);
            var mtx = Matrix4x4.TRS(pe, q, s);
            foreach (var (mat, mesh) in p.partes)
            {
                if (!lote.TryGetValue(mat, out var l)) lote[mat] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = mesh, transform = mtx });
            }
        }

        // ------------------------------------------------------------------ jogo -> Unity
        static Vector3 P(float x, float y, float z) => MalhaMapa.ParaUnity(x, y, z);
        static Vector3 N(float nx, float ny) => new Vector3(nx, 0, -ny);

        /// <summary>Cor do reboco por lote (antes do borrão): as paredes de prédio (material 6) de cada lote ganham a cor dele.</summary>
        public static void Reboco(byte[] G)
        {
            if (!Ativo) return;
            int NX = MapData.NX, NY = MapData.NY, NZ = MapVoxels.NZ;
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (MapData.walk[x, y]) continue;
                    byte m = RebocoDoLote(x, y); if (m == 6) continue;
                    for (int z = MapData.GZ; z < NZ; z++) { int i = (x * NY + y) * NZ + z; if (G[i] == 6) G[i] = m; }
                }
        }

        /// <summary>No lugar do Fachada3D.Montar na Brasa: limpa a pintura marroquina e monta as peças italianas.</summary>
        public static GameObject Montar(byte[] G, Material matObra, Transform pai)
        {
            lote.Clear();
            int NX = MapData.NX, NY = MapData.NY, NZ = MapVoxels.NZ;
            var walk = MapData.walk; var floor = MapData.floor;
            int Top(int x, int y) => MapVoxels.Topo(x, y);
            bool In(int x, int y) => x >= 0 && y >= 0 && x < NX && y < NY;
            var o = new Obra3D();
            int nJ = 0, nP = 0, nG = 0, nT = 0, nV = 0, nVar = 0;
            const int PASSO = 34;          // uma janela a cada ~2,7 m de fachada
            for (int x = 0; x < NX - 1; x++)
                for (int y = 0; y < NY - 1; y++)
                {
                    if (walk[x, y]) continue;
                    bool fx = walk[x + 1, y], fy = walk[x, y + 1];
                    if (!fx && !fy) continue;
                    int fz = fx ? floor[x + 1, y] : floor[x, y + 1];
                    int top = Top(x, y), H = top - fz;
                    // a pintura marroquina (toldo, tapete, vaso, viga) sai inteira: parede lisa da cor do lote
                    byte reb = RebocoDoLote(x, y);
                    for (int z = Mathf.Max(0, fz); z < top - 1 && z < NZ; z++)
                    {
                        int i = (x * NY + y) * NZ + z;
                        if (System.Array.IndexOf(PINTADOS, (int)G[i]) >= 0) G[i] = reb;
                    }
                    if (H < 30 || (fx && fy)) continue;
                    int s = fx ? y : x, k = s % PASSO, vao = s / PASSO;
                    if (k != PASSO / 2) continue;
                    // fachada reta 15 pra cada lado, mesmo piso e altura de sobra (nada em quina, ponta de muro ou parede curta)
                    bool reta = true;
                    for (int d = -15; d <= 15 && reta; d++)
                    {
                        int xx = fx ? x : x + d, yy = fx ? y + d : y, ux = fx ? xx + 1 : xx, uy = fx ? yy : yy + 1;
                        reta = In(xx, yy) && In(ux, uy) && !walk[xx, yy] && walk[ux, uy] && floor[ux, uy] == fz && Top(xx, yy) - fz >= 30;
                    }
                    if (!reta) continue;
                    // nada atrás de cobertura encostada (caixa, barril, carroça)
                    if (Tapado(x + (fx ? 1 : 0.5f), y + (fx ? 0.5f : 1), fx ? 1 : 0, fx ? 0 : 1)) continue;
                    float gx = x + (fx ? 1f : 0.5f), gy = y + (fx ? 0.5f : 1f);
                    var pe = P(gx, gy, fz); var nrm = N(fx ? 1 : 0, fx ? 0 : 1);
                    uint h = Hash(x * 3 + (fx ? 1 : 0), vao * 7 + y);
                    int tipo = (int)((h >> 8) % 10);
                    if (tipo <= 1 && H >= 34)
                    {
                        // porta de madeira em arco com batente de pedra (1 a cada ~5 vãos)
                        Poe("it_DoorFrame_Round_Brick", pe, nrm, Vector3.one);
                        Poe((h & 1) == 0 ? "it_Door_1_Round" : "it_Door_2_Round", pe, nrm, Vector3.one);
                        nP++;
                    }
                    else
                    {
                        // janela em arco; persiana aberta (70%) ou fechada; grade de ferro rente em 1 de cada 4
                        float baixa = H >= 40 ? 0 : -(40 - H) * 0.6f;   // fachada baixa: janela desce um pouco
                        var pj = pe + Vector3.up * (baixa * MalhaMapa.ESC);
                        bool larga = ((h >> 4) & 3) == 0;
                        Poe(larga ? "it_Window_Wide_Round1" : "it_Window_Thin_Round1", pj, nrm, Vector3.one);
                        bool fechada = ((h >> 12) % 10) < 3;
                        Poe("it_WindowShutters_" + (larga ? "Wide" : "Thin") + "_Round_" + (fechada ? "Closed" : "Open"), pj, nrm, Vector3.one);
                        nJ++;
                        if (((h >> 16) & 3) == 0 && !fechada)
                        {
                            // sacada francesa: grade baixa colada na janela (sai ~2 do muro)
                            Poe("it_Prop_MetalFence_Simple", pj + nrm * (2f * MalhaMapa.ESC) + Vector3.up * (13.6f * MalhaMapa.ESC), nrm, new Vector3(larga ? 1.1f : 0.7f, 0.36f, 1));
                            nG++;
                        }
                    }
                    // trepadeira subindo a parede ao lado de 1 em cada 6 vãos
                    if (((h >> 20) % 6) == 0)
                    {
                        string[] tr = { "it_Prop_Vine1", "it_Prop_Vine4", "it_Prop_Vine6" };
                        var lado = new Vector3(nrm.z, 0, -nrm.x) * (14f * MalhaMapa.ESC);
                        Poe(tr[(h >> 24) % 3], pe + lado + Vector3.up * (4f * MalhaMapa.ESC), nrm, Vector3.one * 0.8f);
                        nT++;
                    }
                    // varal atravessando rua estreita (outra fachada de frente a 40-110), acima da cabeça
                    if (((h >> 26) % 4) == 0)
                    {
                        int dx = fx ? 1 : 0, dy = fx ? 0 : 1, dist = 0;
                        for (int d = 1; d <= 110; d++)
                        {
                            int cx = x + dx * d, cy = y + dy * d;
                            if (!In(cx, cy)) break;
                            if (!walk[cx, cy]) { dist = d; break; }
                        }
                        int zc = fz + Mathf.Min(H, 38) - 4;
                        if (dist >= 40 && dist <= 110 && zc >= fz + 31)
                        {
                            int ox = x + dx * dist, oy = y + dy * dist;
                            if (Top(ox, oy) >= zc + 3)
                            {
                                Varal(o, gx, gy, gx + dx * (dist - 1), gy + dy * (dist - 1), zc, h);
                                nVar++;
                            }
                        }
                    }
                }
            // (telhado do pack por lote saiu: o telhado de duas águas agora é a própria altura do topo, TelhadoItalia)
            for (int lx = 0; TELHADO_DO_PACK && lx * LOTE < NX; lx++)
                for (int ly = 0; ly * LOTE < NY; ly++)
                {
                    int x0 = lx * LOTE, y0 = ly * LOTE, x1 = Mathf.Min(NX, x0 + LOTE), y1 = Mathf.Min(NY, y0 + LOTE);
                    int t0 = Top(x0, y0); bool cheio = x1 - x0 == LOTE && y1 - y0 == LOTE;
                    for (int x = x0; x < x1 && cheio; x += 3)
                        for (int y = y0; y < y1 && cheio; y += 3)
                            cheio = !walk[x, y] && Top(x, y) == t0;
                    if (!cheio || t0 <= MapData.GZ) continue;
                    uint h = Hash(lx, ly);
                    float giro = (h & 1) == 0 ? 0 : 90;
                    var c = P(x0 + LOTE / 2f, y0 + LOTE / 2f, t0);
                    // telha 6x6 do pack (8,2 m de lado, 5,7 m de altura) esticada pro lote e bem mais baixa
                    float lado = (LOTE + 4) / METRO / 8.2f;
                    Poe("it_Roof_RoundTiles_6x6", c + Vector3.up * (0.78f * 0.18f * METRO * MalhaMapa.ESC), Vector3.forward, new Vector3(lado, 0.18f, lado), giro);
                    nV++;
                }
            var raiz = o.Montar("Varais da Brasa", matObra, pai);
            int n = 0;
            foreach (var kv in lote)
            {
                // até ~1 milhão de vértices por malha (UInt32), separa se passar
                var me = new Mesh { name = "Brasa italiana " + kv.Key.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                me.CombineMeshes(kv.Value.ToArray(), true, true); me.RecalculateBounds();
                var go = new GameObject("Italia " + kv.Key.name); go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = me;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                n += kv.Value.Count;
            }
            lote.Clear();
            raiz.name = "Fachadas da Brasa (italiana)";
            Debug.Log($"[Italia3D] {nJ} janelas, {nP} portas, {nG} grades, {nT} trepadeiras, {nVar} varais, {nV} telhados ({n} peças)");
            return raiz;
        }

        static bool Tapado(float fx, float fy, int nx, int ny)
        {
            float x0 = Mathf.Min(fx, fx + nx * 12) - (ny != 0 ? 10 : 0.5f), x1 = Mathf.Max(fx, fx + nx * 12) + (ny != 0 ? 10 : 0.5f);
            float y0 = Mathf.Min(fy, fy + ny * 12) - (nx != 0 ? 10 : 0.5f), y1 = Mathf.Max(fy, fy + ny * 12) + (nx != 0 ? 10 : 0.5f);
            foreach (var c in MapData.covers)
                if (c.r.x0 < x1 && c.r.x1 > x0 && c.r.y0 < y1 && c.r.y1 > y0) return true;
            return false;
        }

        static Color Hex(int v) => new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        static readonly Color[] ROUPA = { Hex(0xF4F1E8), Hex(0xC9433A), Hex(0x3E6FA8), Hex(0xE8C45A), Hex(0xF0E2D0), Hex(0x6E9A5A) };

        /// <summary>Corda de uma fachada à outra e 3 a 5 peças de roupa penduradas (blocos finos).</summary>
        static void Varal(Obra3D o, float ax, float ay, float bx, float by, int z, uint h)
        {
            var a = P(ax, ay, z); var b = P(bx, by, z);
            var d = b - a; float L = d.magnitude; if (L < 0.5f) return;
            var t = d / L; var lado = Vector3.Cross(t, Vector3.up).normalized;
            o.Bloco((a + b) / 2, t * (L / 2), lado * 0.012f, Vector3.up * 0.012f, Hex(0xE8E2D4));
            int n = 3 + (int)(h % 3);
            for (int i = 0; i < n; i++)
            {
                float f = (i + 1f) / (n + 1f) + ((h >> (i * 3)) % 5 - 2) * 0.02f;
                float alt = (0.28f + ((h >> (i * 2 + 5)) % 3) * 0.06f), larg = 0.16f + ((h >> (i + 9)) % 3) * 0.04f;
                var c = a + d * f - Vector3.up * (alt / 2 + 0.01f);
                o.Bloco(c, t * larg, lado * 0.006f, Vector3.up * (alt / 2), ROUPA[(h >> (i * 4)) % (uint)ROUPA.Length]);
            }
        }
    }
}
