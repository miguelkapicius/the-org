using UnityEditor;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>Ícones e radar do HUD (Resources/Icones): textura de interface nítida — sem compressão, com mipmap (o ícone
    /// de 128 px aparece com 20-30 px: sem mipmap a redução serrilhava a borda), sem repetir na borda.</summary>
    public class IconesImport : AssetPostprocessor
    {
        public override uint GetVersion() => 3;   // 3: mipmap nos ícones (com viés pra nitidez) (sobe o número e a Unity reimporta)

        /// <summary>Fontes do HUD (Resources/Fontes): o que a Barlow não tem (símbolos) cai nas fontes do Windows.</summary>
        void OnPreprocessAsset()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Fontes/") || !(assetImporter is TrueTypeFontImporter fi)) return;
            fi.includeFontData = true;
            fi.fontReferences = new Font[0];
            fi.fontNames = new[] { "Segoe UI Symbol", "Segoe UI", "Arial" };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Icones/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.mipMapBias = -0.5f; ti.alphaIsTransparency = true; ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Trilinear; ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
