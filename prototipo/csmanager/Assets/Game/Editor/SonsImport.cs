using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>Importação dos sons autorais (Assets/Game/Audio/Resources/Sons): mono, Vorbis, descomprime ao carregar
    /// (tiro curto não pode ter atraso de decodificação), pré-carrega.</summary>
    public class SonsImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2;   // muda = reimporta os sons com a regra nova

        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Audio/Resources/Sons/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            bool longo = assetPath.Contains("/ambiente/") || assetPath.Contains("_loop");
            s.loadType = longo ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            // bip curto (C4, HUD) sem compressão: Vorbis em clipe de 30-110 ms é o 1º suspeito da C4 muda (2026-11-04)
            bool curto = assetPath.Contains("/c4/bip") || assetPath.Contains("/c4/final") || assetPath.Contains("/c4/desarme") || assetPath.Contains("/hud/") || assetPath.Contains("/ui/");
            s.compressionFormat = curto ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
            s.quality = 0.8f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
        }
    }
}
