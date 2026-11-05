using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Plantas do Stylized Nature MegaKit (Quaternius, CC0; fonte em Assets/Game/Art/Natureza): textura com mipmap e alfa,
    /// e um prefab por modelo em Resources/Natureza com material próprio (URP Simple Lit, recorte da folha pela alfa, as duas
    /// faces). O jogo carrega pelo nome (CSM.View3D.Natureza3D). Visual3D.Setup chama Montar.
    /// </summary>
    public class NaturezaImport : AssetPostprocessor
    {
        public const string FONTE = "Assets/Game/Art/Natureza", DESTINO = "Assets/Game/Visual/Resources/Natureza";
        // material do modelo -> textura (nome do material no FBX = o do glTF do pack)
        static readonly Dictionary<string, string> TEX = new Dictionary<string, string>
        {
            ["Leaves_TwistedTree"] = "Leaves_TwistedTree_C", ["Leaves_NormalTree"] = "Leaves_NormalTree_C",
            ["Flowers"] = "Flowers", ["Leaves"] = "Leaves",
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(FONTE)) return;
            var ti = (TextureImporter)assetImporter;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(FONTE)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
        }

        public static void Montar()
        {
            if (!Directory.Exists(FONTE)) return;
            Directory.CreateDirectory(DESTINO);
            var sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            var mats = new Dictionary<string, Material>();
            Material Mat(string nome)
            {
                string tex = TEX.TryGetValue(nome, out var t) ? t : "Leaves";
                if (mats.TryGetValue(tex, out var m)) return m;
                string p = $"{DESTINO}/Mat_{tex}.mat";
                m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
                m.shader = sh;
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{FONTE}/{tex}.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0); m.doubleSidedGI = true;   // folha de um plano só: as duas faces
                m.SetFloat("_Smoothness", 0.05f); m.SetFloat("_SpecularHighlights", 0); m.DisableKeyword("_SPECULAR_COLOR");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(m);
                mats[tex] = m; return m;
            }
            foreach (var fbx in Directory.GetFiles(FONTE, "*.fbx"))
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.Replace('\\', '/'));
                if (src == null) continue;
                var inst = (GameObject)Object.Instantiate(src);
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    // o material importado do FBX tem o nome do material de origem: troca pelo nosso com a textura certa
                    var ms = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < ms.Length; i++) ms[i] = Mat(r.sharedMaterials[i] != null ? r.sharedMaterials[i].name : "Leaves");
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                string nome = Path.GetFileNameWithoutExtension(fbx);
                PrefabUtility.SaveAsPrefabAsset(inst, $"{DESTINO}/{nome}.prefab");
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
