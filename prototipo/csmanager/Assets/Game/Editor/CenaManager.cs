using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CSM.ManagerUI;

namespace CSM.EditorTools
{
    /// <summary>
    /// A cena da interface do manager (Assets/Scenes/Manager.unity): uma câmera e o GerenteUI. Fica em primeiro no
    /// Build Settings, com a Partida3D em segundo (é pra ela que o botão "Assistir partida" leva).
    /// </summary>
    public static class CenaManager
    {
        public const string ARQ = "Assets/Scenes/Manager.unity";
        const string ARQ_PARTIDA = "Assets/Scenes/Partida3D.unity";

        [MenuItem("CS Manager/Manager/Rodar o manager")]
        public static void Rodar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ARQ) || !File.Exists(ARQ_PARTIDA)) Criar();
            CSM.Manager.Carreira.Atual = null;   // carreira nova a cada Play pelo menu
            EditorSceneManager.OpenScene(ARQ);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("CS Manager/Manager/Recriar cena do manager")]
        public static void Criar()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ARQ_PARTIDA)) Visual3D.CriarCena3D();   // o botão Assistir precisa dela
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Câmera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.059f, 0.071f, 0.09f); cam.cullingMask = 0;
            new GameObject("Manager UI").AddComponent<GerenteUI>();
            EditorSceneManager.SaveScene(s, ARQ);

            var cenas = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ARQ, true), new EditorBuildSettingsScene(ARQ_PARTIDA, true) };
            cenas.AddRange(EditorBuildSettings.scenes.Where(c => c.path != ARQ && c.path != ARQ_PARTIDA));
            EditorBuildSettings.scenes = cenas.ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch: cria a cena e sai (Unity -batchmode -executeMethod CSM.EditorTools.CenaManager.CriarBatch).</summary>
        public static void CriarBatch()
        {
            try { Criar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
