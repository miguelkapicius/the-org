using UnityEditor;

namespace CSM.EditorTools
{
    /// <summary>
    /// Modelos do Kenney em View3D/Resources/Kenney (chafariz, pilar): malha legível. O jogo lê a cor por vértice que o
    /// conversor gravou (Chafariz3D/Pilar3D pintam com a Paleta3D); sem isso o .exe não lê a malha (no editor lia).
    /// </summary>
    public class KenneyImport : AssetPostprocessor
    {
        const string PASTA = "Assets/Game/View3D/Resources/Kenney", PACKS = "Assets/Game/View3D/Resources/Packs";   // Packs: props CC0 (Packs3D)
        const uint VERSAO = 1;

        public override uint GetVersion() => VERSAO;

        void OnPreprocessModel()
        {
            var ap = assetPath.Replace('\\', '/');
            if (!ap.StartsWith(PASTA) && !ap.StartsWith(PACKS)) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
        }
    }
}
