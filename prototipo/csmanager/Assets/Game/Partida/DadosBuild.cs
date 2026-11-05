using System.IO;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// Na build, os arquivos que o jogo lê por caminho relativo (bonecos em Logs/bonecos3d, dados dos bots em
    /// Assets/Game/Sim) ficam numa cópia dentro dela (StreamingAssets/dados, mesma estrutura de pastas: BuildDados copia
    /// depois da build). Antes de qualquer coisa carregar, a pasta atual vira essa cópia, então os mesmos caminhos
    /// funcionam de onde quer que o .exe seja aberto. No editor não faz nada.
    /// </summary>
    static class DadosBuild
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Pasta()
        {
            if (Application.isEditor) return;
            var d = Path.Combine(Application.streamingAssetsPath, "dados");
            if (Directory.Exists(d)) Directory.SetCurrentDirectory(d);
            else Debug.LogWarning("[DadosBuild] sem a pasta de dados da build: " + d);
        }
    }
}
