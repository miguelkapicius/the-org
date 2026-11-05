using System.IO;
using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Granadas do kit do EsportsPoc (modeladas do zero pelas medidas do CS2; fonte em Assets/Game/Art/Granadas): um prefab
    /// por modelo em Resources/Granadas, com os materiais de cor lisa do FBX. O jogo usa pra granada voando, caída no chão
    /// e a lata da smoke (PartidaAoVivo3D.Granada). Visual3D.Setup chama Montar.
    /// </summary>
    public class GranadasImport : AssetPostprocessor
    {
        public const string FONTE = "Assets/Game/Art/Granadas", DESTINO = "Assets/Game/Visual/Resources/Granadas";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(FONTE)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            // malha legível: o item no chão apoia pelo vértice mais baixo (PartidaAoVivo3D.Fundo) e o kit troca a cor do
            // atlas por cor de vértice linear em runtime
            mi.isReadable = true;
        }

        public static void Montar()
        {
            if (!Directory.Exists(FONTE)) return;
            Directory.CreateDirectory(DESTINO);
            foreach (var fbx in Directory.GetFiles(FONTE, "*.fbx"))
            {
                // os já importados antes do isReadable: reimporta uma vez
                if (AssetImporter.GetAtPath(fbx.Replace('\\', '/')) is ModelImporter mi0 && !mi0.isReadable) { mi0.isReadable = true; mi0.SaveAndReimport(); }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.Replace('\\', '/'));
                if (src == null) continue;
                var inst = (GameObject)Object.Instantiate(src);
                foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                PrefabUtility.SaveAsPrefabAsset(inst, $"{DESTINO}/{Path.GetFileNameWithoutExtension(fbx)}.prefab");
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
