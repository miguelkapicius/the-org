using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CSM.View;

namespace CSM.EditorTools
{
    /// <summary>
    /// A cena da partida ao vivo: câmera + PartidaAoVivo. Abrir o projeto já abre ela (é só apertar Play); o menu
    /// CS Manager recria a cena se precisar.
    /// </summary>
    [InitializeOnLoad]
    public static class Cena
    {
        public const string ARQ = "Assets/Scenes/Partida.unity";

        static Cena()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ARQ)) return;
                var s = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(s.path) && !s.isDirty) EditorSceneManager.OpenScene(ARQ);
            };
        }

        [MenuItem("CS Manager/Rodar partida ao vivo")]
        public static void Rodar()
        {
            if (!File.Exists(ARQ)) Criar();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ARQ);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("CS Manager/Recriar cena da partida")]
        public static void Criar()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var c = new GameObject("Main Camera").AddComponent<Camera>();
            c.tag = "MainCamera"; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.086f, 0.07f, 0.078f); c.cullingMask = 0;
            new GameObject("Partida ao vivo").AddComponent<PartidaAoVivo>();
            EditorSceneManager.SaveScene(s, ARQ);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ARQ, true) };
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch: cria a cena e sai.</summary>
        public static void CriarBatch() { Criar(); EditorApplication.Exit(0); }

        /// <summary>Teste com a janela do editor: entra no Play (a PartidaAoVivo grava a medição com -aovivoteste N e fecha).</summary>
        public static void Testar() { Criar(); EditorApplication.isPlaying = true; }
    }
}
