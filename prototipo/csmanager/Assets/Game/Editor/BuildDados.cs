using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Depois de cada build: copia pra &lt;jogo&gt;_Data/StreamingAssets/dados os arquivos que o jogo lê por caminho relativo
    /// (bonecos 3D e dados dos bots), na mesma estrutura de pastas do projeto. CSM.View3D.DadosBuild aponta a pasta atual
    /// pra lá quando a build abre.
    /// </summary>
    public class BuildDados : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        // (pasta relativa ao projeto, padrões de arquivo)
        static readonly (string pasta, string[] padroes)[] DADOS =
        {
            ("Logs/bonecos3d", new[] { "boneco_*.json", "boneco_*.bin" }),
            ("Assets/Game/Sim", new[] { "*.bin", "*.txt" }),
        };

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup != BuildTargetGroup.Standalone) return;
            string exe = report.summary.outputPath;
            string destino = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + "_Data", "StreamingAssets", "dados");
            int n = 0; long bytes = 0;
            foreach (var (pasta, padroes) in DADOS)
            {
                if (!Directory.Exists(pasta)) { Debug.LogWarning("[BuildDados] faltou a pasta " + pasta); continue; }
                foreach (var arq in padroes.SelectMany(p => Directory.GetFiles(pasta, p)).Distinct())
                {
                    var alvo = Path.Combine(destino, pasta, Path.GetFileName(arq));
                    Directory.CreateDirectory(Path.GetDirectoryName(alvo));
                    File.Copy(arq, alvo, true); n++; bytes += new FileInfo(arq).Length;
                }
            }
            Debug.Log($"[BuildDados] {n} arquivos ({bytes / 1048576f:0} MB) copiados pra {destino}");
        }
    }
}
