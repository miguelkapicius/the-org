using UnityEditor;

namespace CSM.EditorTools
{
    /// <summary>
    /// Modelos do Kenney em Visual/Resources/Kenney (chafariz, pilar): malha legível. O jogo lê a cor por vértice que o
    /// conversor gravou (Chafariz3D/Pilar3D pintam com a Paleta3D); sem isso o .exe não lê a malha (no editor lia).
    /// </summary>
    public class KenneyImport : AssetPostprocessor
    {
        const string PASTA = "Assets/Game/Visual/Resources/Kenney", PACKS = "Assets/Game/Visual/Resources/Packs";   // Packs: props CC0 (Packs3D)
        const uint VERSAO = 2;   // 2: Packs entrou depois (os .meta ficaram isReadable 0 e o .exe não lia a malha: props sumiam)

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
