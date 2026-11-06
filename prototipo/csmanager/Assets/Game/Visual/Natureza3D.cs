using System.Collections.Generic;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// Plantas de verdade (Stylized Nature MegaKit da Quaternius, CC0; prefabs em Resources/Natureza feitos pelo
    /// NaturezaImport) nos lugares que a fachada marcou: cada ponto escolhe um dos modelos pela semente, gira à toa e
    /// escala pra largura pedida (unidades do jogo). Substitui as bolotas verdes geradas antes.
    /// </summary>
    public static class Natureza3D
    {
        static readonly Dictionary<string, (GameObject prefab, float largura)> cache = new Dictionary<string, (GameObject, float)>();

        static (GameObject prefab, float largura) Modelo(string nome)
        {
            if (cache.TryGetValue(nome, out var c)) return c;
            var p = Resources.Load<GameObject>("Natureza/" + nome);
            float w = 1;
            if (p != null)
            {
                // largura do modelo em escala 1 (no chão, maior lado)
                var b = new Bounds(); bool ok = false;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds; var s = mf.transform.lossyScale;
                    var e = new Vector3(mb.size.x * s.x, mb.size.y * s.y, mb.size.z * s.z);
                    var bb = new Bounds(mf.transform.position + mb.center, e);
                    if (!ok) { b = bb; ok = true; } else b.Encapsulate(bb);
                }
                if (ok) w = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            }
            cache[nome] = (p, w);
            return cache[nome];
        }

        public static GameObject Plantar(List<(Vector3 pos, float largura, string[] opcoes, int sem)> pontos, Transform pai)
        {
            var raiz = new GameObject("Plantas (Quaternius)"); raiz.transform.SetParent(pai, false); int n = 0;
            foreach (var (pos, largura, opcoes, sem) in pontos)
            {
                var rng = new System.Random(sem);
                var (prefab, w) = Modelo(opcoes[rng.Next(opcoes.Length)]);
                if (prefab == null) continue;
                var g = Object.Instantiate(prefab, raiz.transform);
                g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
                // por cima da escala do próprio prefab (o FBX vem com 100 na raiz, da conversão de unidade)
                g.transform.localScale = prefab.transform.localScale * (largura * MalhaMapa.ESC / w) * (0.85f + 0.3f * (float)rng.NextDouble());
                n++;
            }
            Debug.Log($"[Natureza3D] {n} de {pontos.Count} plantas; modelos: " + string.Join(", ", System.Linq.Enumerable.Select(cache, kv => $"{kv.Key}={(kv.Value.prefab != null ? kv.Value.largura.ToString("0.00") : "FALTOU")}")));
            return raiz;
        }
    }
}
