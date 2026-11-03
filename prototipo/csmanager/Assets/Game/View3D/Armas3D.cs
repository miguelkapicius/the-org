using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// As armas dos bonecos exportados como objeto solto: as peças de uma arma são as que aparecem na mira dela e somem
    /// na mira de alguma outra (corpo aparece em todas). Serve pra arma caindo no chão (física simples) e pros ícones do
    /// HUD (silhueta branca renderizada de lado).
    /// </summary>
    public static class Armas3D
    {
        public static readonly string[] ARMAS = { "AK47", "M4A4", "M4A1S", "AWP", "MP9", "MAC10", "USP", "Glock", "Deagle" };
        static readonly Dictionary<string, int[]> pecas = new Dictionary<string, int[]>();
        static readonly Dictionary<string, Texture2D> icones = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, HashSet<int>> deArma = new Dictionary<string, HashSet<int>>();

        public static string DoJogo(string gunId) => gunId switch
        {
            "ak" => "AK47", "m4" => "M4A4", "m4s" => "M4A1S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC10",
            "usp" => "USP", "glock" => "Glock", "deagle" => "Deagle", "faca" => "faca", _ => null
        };

        /// <summary>Índices das peças da arma (ou "faca") no modelo do time.</summary>
        public static int[] Pecas(Boneco3D.Modelo m, string arma)
        {
            string k = m.time + arma;
            if (pecas.TryGetValue(k, out var r)) return r;
            int P = m.malhas.Length;
            var lista = new List<int>();
            if (arma == "faca")
            {
                if (m.anims.TryGetValue("faca_parado", out var fp))
                    for (int p = 0; p < P; p++)
                        if (fp.vis[p] && ARMAS.All(w => !m.anims.TryGetValue("mira_" + w, out var a) || !a.vis[p])) lista.Add(p);
            }
            else if (m.anims.TryGetValue("mira_" + arma, out var mira))
                for (int p = 0; p < P; p++)
                    if (mira.vis[p] && ARMAS.Any(w => w != arma && m.anims.TryGetValue("mira_" + w, out var o) && !o.vis[p])) lista.Add(p);
            r = lista.ToArray(); pecas[k] = r;
            return r;
        }

        /// <summary>Toda peça que é de alguma arma (pra esconder a arma da mão quando ela vira objeto solto).</summary>
        public static HashSet<int> DeArma(Boneco3D.Modelo m)
        {
            if (deArma.TryGetValue(m.time, out var h)) return h;
            h = new HashSet<int>();
            foreach (var w in ARMAS) foreach (int p in Pecas(m, w)) h.Add(p);
            deArma[m.time] = h;
            return h;
        }

        /// <summary>Monta a arma como objeto: peças na pose da mira, centradas, cano pra +Z (frente do boneco).</summary>
        public static GameObject Objeto(Boneco3D.Modelo m, string arma, Material mat, int camada = 0)
        {
            var idx = Pecas(m, arma);
            var go = new GameObject("arma " + arma) { layer = camada };
            string anim = arma == "faca" ? "faca_parado" : "mira_" + arma;
            if (!m.anims.TryGetValue(anim, out var a) || idx.Length == 0) return go;
            int P = m.malhas.Length;
            var b = new Bounds(); bool tem = false;
            foreach (int p in idx)
            {
                var c = a.pos[p];
                if (!tem) { b = new Bounds(c, Vector3.zero); tem = true; } else b.Encapsulate(c);
            }
            foreach (int p in idx)
            {
                var g = new GameObject(m.nomes[p]) { layer = camada };
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = (a.pos[p] - b.center) * Boneco3D.ESCALA;
                g.transform.localRotation = a.rot[p]; g.transform.localScale = a.esc[p] * Boneco3D.ESCALA;
                g.AddComponent<MeshFilter>().sharedMesh = m.malhas[p];
                g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go;
        }

        /// <summary>Primária guardada nas costas (de pistola na mão): a arma na pose da animação costas_ARMA, já no
        /// referencial do tronco. Pra posar: posição = tronco + giroTronco * lp * ESCALA, giro = giroTronco * lr.</summary>
        public static GameObject NasCostas(Boneco3D.Modelo m, string arma, Material mat, int camada, out Vector3 lp, out Quaternion lr)
        {
            lp = Vector3.zero; lr = Quaternion.identity;
            var go = new GameObject("costas " + arma) { layer = camada };
            var idx = Pecas(m, arma); int tr = System.Array.FindIndex(m.nomes, n => n.EndsWith("tronco"));
            if (!m.anims.TryGetValue("costas_" + arma, out var a) || idx.Length == 0 || tr < 0) return go;
            var b = new Bounds(a.pos[idx[0]], Vector3.zero);
            foreach (int p in idx) b.Encapsulate(a.pos[p]);
            var rtInv = Quaternion.Inverse(a.rot[tr]);
            lp = rtInv * (b.center - a.pos[tr]); lr = rtInv;
            foreach (int p in idx)
            {
                if (!a.vis[p]) continue;
                var g = new GameObject(m.nomes[p]) { layer = camada };
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = (a.pos[p] - b.center) * Boneco3D.ESCALA;
                g.transform.localRotation = a.rot[p]; g.transform.localScale = a.esc[p] * Boneco3D.ESCALA;
                g.AddComponent<MeshFilter>().sharedMesh = m.malhas[p];
                g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go;
        }

        /// <summary>Objeto de mão de uma animação (granada no meio do arremesso, C4 no fim do plant): as peças visíveis
        /// naquele quadro que não são corpo (aparecem na mira) nem arma nem faca. Centrado, na pose daquele quadro.</summary>
        public static GameObject Prop(Boneco3D.Modelo m, string anim, float u, Material mat, string[] so = null)
        {
            var go = new GameObject("prop " + anim);
            if (!m.anims.TryGetValue(anim, out var a) || !m.anims.TryGetValue("mira_" + ARMAS[0], out var corpo)) return go;
            int P = m.malhas.Length, q = Mathf.Clamp(Mathf.RoundToInt(u * (a.n - 1)), 0, a.n - 1);
            var arma = DeArma(m); var faca = new HashSet<int>(Pecas(m, "faca"));
            var idx = new List<int>();
            for (int p = 0; p < P; p++)
                if (a.vis[q * P + p] && !corpo.vis[p] && !arma.Contains(p) && !faca.Contains(p) && !m.brilho[p]
                    && (so == null || System.Array.Exists(so, k => m.nomes[p].Contains(k)))) idx.Add(p);
            if (idx.Count == 0) return go;
            var b = new Bounds(a.pos[q * P + idx[0]], Vector3.zero);
            foreach (int p in idx) b.Encapsulate(a.pos[q * P + p]);
            foreach (int p in idx)
            {
                int k = q * P + p;
                var g = new GameObject(m.nomes[p]);
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = (a.pos[k] - b.center) * Boneco3D.ESCALA;
                g.transform.localRotation = a.rot[k]; g.transform.localScale = a.esc[k] * Boneco3D.ESCALA;
                g.AddComponent<MeshFilter>().sharedMesh = m.malhas[p];
                g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go;
        }

        /// <summary>Ícone da arma: silhueta branca de lado (cano pra direita), alfa pela claridade. Feito uma vez.</summary>
        public static Texture2D Icone(Boneco3D.Modelo m, string arma)
        {
            if (arma == null) return null;
            if (icones.TryGetValue(arma, out var t)) return t;
            const int W = 384, H = 128, CAM = 30;
            var mat = new Material(Shader.Find("CSM/Brilho")); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Intensidade", 1);
            var obj = Objeto(m, arma, mat, CAM);
            obj.transform.position = new Vector3(0, -500, 0);
            if (arma == "faca" && obj.transform.childCount > 0)
            {
                // a faca na mão fica inclinada: alinha pela lâmina (maior peça; +Z local = ponta, +Y = lombo)
                Transform lam = null; int mais = -1;
                foreach (Transform ch in obj.transform)
                {
                    int n = ch.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                    if (ch.name.Contains("lamina")) { lam = ch; break; }
                    if (n > mais) { mais = n; lam = ch; }
                }
                obj.transform.rotation = Quaternion.Inverse(lam.localRotation);
            }
            var bnd = new Bounds(obj.transform.position, Vector3.zero);
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) bnd.Encapsulate(r.bounds);
            var cg = new GameObject("câmera ícone"); var c = cg.AddComponent<Camera>();
            c.orthographic = true; c.cullingMask = 1 << CAM; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
            c.allowHDR = false; c.allowMSAA = false; c.aspect = W / (float)H;
            // de lado: câmera no +X olhando pra -X, assim o cano (+Z) aponta pra direita da imagem (pra vítima, no killfeed)
            c.transform.SetPositionAndRotation(bnd.center + Vector3.right * 10, Quaternion.LookRotation(Vector3.left, Vector3.up));
            c.orthographicSize = Mathf.Max(bnd.extents.y * 1.15f, bnd.extents.z * 1.15f / c.aspect, 0.02f);
            c.nearClipPlane = 0.1f; c.farClipPlane = 30;
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(c).renderPostProcessing = false;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            c.targetTexture = rt; c.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "ícone " + arma, wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = old; c.targetTexture = null;
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) { byte a = (byte)Mathf.Max(px[i].r, px[i].g, px[i].b); px[i] = new Color32(255, 255, 255, a); }
            t.SetPixels32(px); t.Apply();
            Apagar(cg); Apagar(obj); rt.Release(); Apagar(rt);
            icones[arma] = t;
            return t;
        }

        static void Apagar(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}
