using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CSM.EditorTools
{
    /// <summary>
    /// Abrir o projeto já abre a partida 3D (Assets/Scenes/Partida3D.unity: é só apertar Play). O visual 2D antigo saiu
    /// (a cena e a PartidaAoVivo estão em _lixo2d, fora de Assets).
    /// </summary>
    [InitializeOnLoad]
    public static class Cena
    {
        public const string ARQ = Visual3D.CENA3D;

        static Cena()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ARQ)) Visual3D.CriarCena3D();
                var s = EditorSceneManager.GetActiveScene();
                if (s.path != ARQ && !s.isDirty) EditorSceneManager.OpenScene(ARQ);
                if (EditorBuildSettings.scenes.Length != 1 || EditorBuildSettings.scenes[0].path != ARQ)
                    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ARQ, true) };
            };
        }

        [MenuItem("CS Manager/Rodar partida ao vivo")]
        public static void Rodar() => Visual3D.Rodar3D();
    }
}
