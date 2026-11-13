using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Peças da vila italiana da Brasa: Medieval Village MegaKit (Quaternius, CC0; fonte em Assets/Game/Art/Italia, só as
    /// peças usadas) + cipreste e oliveira do Stylized Nature MegaKit (Assets/Game/Art/Natureza). Um prefab por peça em
    /// Resources/Italia, com material próprio já na cor da Brasa (URP Simple Lit, textura do pack multiplicada pela cor):
    /// persiana verde, moldura de pedra clara, telha capa-canal, ferro escuro, trepadeira e folha de cipreste/oliveira.
    /// O jogo carrega pelo nome (CSM.View3D.Italia3D). Visual3D.Setup chama Montar.
    /// </summary>
    public class ItaliaImport : AssetPostprocessor
    {
        public const string FONTE = "Assets/Game/Art/Italia", NATUREZA = "Assets/Game/Art/Natureza", DESTINO = "Assets/Game/Visual/Resources/Italia";

        /// <summary>Material do pack -> (textura, cor que multiplica, recorte pela alfa).</summary>
        static readonly Dictionary<string, (string tex, Color cor, bool recorte)> MAT = new Dictionary<string, (string, Color, bool)>
        {
            ["MI_WoodTrim"] = ("T_WoodTrim_BaseColor", new Color(0.82f, 0.62f, 0.45f), false),          // porta e batente: madeira
            ["MI_WoodTrim_Wear"] = ("T_WoodTrim_BaseColor", new Color(0.36f, 0.62f, 0.46f), false),     // persiana: verde
            ["MI_RockTrim"] = ("T_RockTrim_BaseColor", new Color(0.98f, 0.95f, 0.88f), false),          // moldura de pedra clara
            ["MI_Brick"] = ("T_Brick_BaseColor", new Color(0.95f, 0.90f, 0.82f), false),
            ["MI_Plaster"] = ("T_Plaster_BaseColor", new Color(1.00f, 0.86f, 0.62f), false),           // chaminé de reboco
            ["MI_RoundTiles"] = ("T_RoundTiles_BaseColor", new Color(1.00f, 0.92f, 0.86f), false),      // telha capa-canal
            ["MI_MetalOrnaments"] = ("T_MetalOrnaments_BaseColor", new Color(0.75f, 0.75f, 0.78f), false),
            ["MI_VineLeaf"] = ("T_VineLeaf", Color.white, true),
            ["MI_Window"] = ("T_WindowGradient", Color.white, false),
            // natureza (cipreste = Pine afinado; oliveira = TwistedTree com folha prateada)
            ["Leaves_NormalTree"] = ("Leaves_NormalTree_C", new Color(0.55f, 0.72f, 0.55f), true),
            ["Leaves_TwistedTree"] = ("Leaves_TwistedTree_C", new Color(0.78f, 0.86f, 0.72f), true),
            ["Leaves"] = ("Leaves", new Color(0.60f, 0.75f, 0.58f), true),
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
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.isReadable = true;   // o Italia3D junta as malhas por material (CombineMeshes no exe precisa disso)
        }

        static Texture2D Tex(string nome)
        {
            foreach (var pasta in new[] { FONTE, NATUREZA })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pasta}/{nome}.png");
                if (t != null) return t;
            }
            return null;
        }

        public static void Montar()
        {
            if (!Directory.Exists(FONTE)) return;
            Directory.CreateDirectory(DESTINO);
            var sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            var mats = new Dictionary<string, Material>();
            Material Mat(string origem)
            {
                string chave = origem.Split(' ')[0].Split('.')[0];
                if (!MAT.ContainsKey(chave)) chave = chave.StartsWith("Leaves") ? "Leaves" : "MI_WoodTrim";
                if (mats.TryGetValue(chave, out var m)) return m;
                var (tex, cor, recorte) = MAT[chave];
                string p = $"{DESTINO}/Mat_{chave}.mat";
                m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
                m.shader = sh;
                m.SetTexture("_BaseMap", Tex(tex)); m.SetColor("_BaseColor", cor);
                m.SetFloat("_Smoothness", 0.08f); m.SetFloat("_SpecularHighlights", 0); m.DisableKeyword("_SPECULAR_COLOR");
                if (recorte)
                {
                    m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0); m.doubleSidedGI = true;
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(m);
                mats[chave] = m; return m;
            }
            void Prefab(string fbx, string nome)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.Replace('\\', '/'));
                if (src == null) return;
                var inst = (GameObject)Object.Instantiate(src);
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var ms = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < ms.Length; i++) ms[i] = Mat(r.sharedMaterials[i] != null ? r.sharedMaterials[i].name : "MI_WoodTrim");
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, $"{DESTINO}/{nome}.prefab");
                Object.DestroyImmediate(inst);
            }
            foreach (var fbx in Directory.GetFiles(FONTE, "*.fbx"))
                Prefab(fbx, "it_" + Path.GetFileNameWithoutExtension(fbx));
            // cipreste e oliveira (do pack de natureza que já está no projeto)
            foreach (var n in new[] { "Pine_1", "Pine_2", "Pine_3", "TwistedTree_1", "TwistedTree_3" })
                if (File.Exists($"{NATUREZA}/{n}.fbx")) Prefab($"{NATUREZA}/{n}.fbx", "it_" + n);
            AssetDatabase.SaveAssets();
        }
    }
}
