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

        [MenuItem("CS Manager/Manager/Rodar o manager (carreira nova)")]
        public static void Rodar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ARQ) || !File.Exists(ARQ_PARTIDA)) Criar();
            CSM.Manager.Carreira.Atual = null;
            Salvamento.Apagar();   // carreira nova: descarta o salvamento anterior
            EditorSceneManager.OpenScene(ARQ);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("CS Manager/Manager/Continuar carreira salva")]
        public static void Continuar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ARQ) || !File.Exists(ARQ_PARTIDA)) Criar();
            CSM.Manager.Carreira.Atual = null;   // o manager carrega o salvamento (se não houver, começa uma carreira nova)
            EditorSceneManager.OpenScene(ARQ);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("CS Manager/Manager/Apagar carreira salva")]
        public static void ApagarSalvamento() { Salvamento.Apagar(); Debug.Log("Carreira salva apagada: " + Salvamento.Caminho); }

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

        /// <summary>Batch: roda o manager, salva um PNG de cada tela na pasta da variável CSM_CAPTURA e fecha o Editor
        /// (Unity -batchmode -executeMethod CSM.EditorTools.CenaManager.CapturarBatch). Serve para conferir o visual.</summary>
        public static void CapturarBatch()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CSM_CAPTURA"))) { Debug.LogError("Defina CSM_CAPTURA=<pasta>."); EditorApplication.Exit(1); return; }
            if (!File.Exists(ARQ) || !File.Exists(ARQ_PARTIDA)) Criar();
            CSM.Manager.Carreira.Atual = null;
            EditorSceneManager.OpenScene(ARQ);
            EditorApplication.isPlaying = true;
        }

        /// <summary>Batch: confere o ciclo salvar/carregar com o JsonUtility da Unity depois de algumas semanas de carreira; sai com 0 se
        /// tudo bate (as regras em si são testadas em ManagerTest, fora da Unity). Unity -batchmode -nographics -executeMethod
        /// CSM.EditorTools.CenaManager.TestarCarreiraBatch.</summary>
        public static void TestarCarreiraBatch()
        {
            int falhas = 0;
            void Ok(bool cond, string o) { if (!cond) { falhas++; Debug.LogError("FALHOU: " + o); } else Debug.Log("ok: " + o); }
            try
            {
                var c = CSM.Manager.Carreira.Nova(5);
                for (int i = 0; i < 30; i++)
                {
                    int g = 0; while (c.TemJogoAgora && g++ < 20) c.PularSerie();
                    c.AvancarSemana();
                }
                Ok(c.Semana > 25 && c.historico.Count > 0, "30 semanas de carreira (semana " + c.Semana + ", " + c.historico.Count + " mapas jogados)");
                CSM.ManagerUI.Salvamento.Salvar(c);
                var d = CSM.ManagerUI.Salvamento.Carregar();
                Ok(d != null, "o JsonUtility salva e carrega a carreira");
                if (d != null)
                {
                    Ok(d.dinheiro == c.dinheiro && d.Semana == c.Semana && d.nivelGH == c.nivelGH && d.clube == c.clube, "valores simples iguais");
                    Ok(d.elenco.Select(j => j.nome + j.Overall + j.potencial).SequenceEqual(c.elenco.Select(j => j.nome + j.Overall + j.potencial)), "elenco igual");
                    Ok(d.mundo.Ranking().Select(e => e.id).SequenceEqual(c.mundo.Ranking().Select(e => e.id)), "ranking dos 48 times igual");
                    Ok(d.historico.Count == c.historico.Count && d.extrato.Count == c.extrato.Count, "histórico e extrato do mesmo tamanho");
                    var pa = c.Proximo(); var pb = d.Proximo();
                    Ok(pa.camp?.id == pb.camp?.id && pa.semana == pb.semana, "o próximo compromisso é o mesmo");
                }
                CSM.ManagerUI.Salvamento.Apagar();
                Ok(CSM.ManagerUI.Salvamento.Carregar() == null, "sem arquivo, Carregar devolve null");
            }
            catch (System.Exception e) { falhas++; Debug.LogException(e); }
            Debug.Log(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " TESTE(S) FALHARAM");
            EditorApplication.Exit(falhas == 0 ? 0 : 1);
        }

        /// <summary>Batch: sintetiza os sons da interface (decisão 0029), confere que nenhum estoura, tem NaN, passa de 1 s ou estala no começo e no fim,
        /// e grava um .wav de cada em Logs/sons para ouvir. Unity -batchmode -nographics -executeMethod CSM.EditorTools.CenaManager.TestarSonsBatch.</summary>
        public static void TestarSonsBatch()
        {
            int falhas = 0; Directory.CreateDirectory("Logs/sons");
            foreach (CSM.ManagerUI.Som s in System.Enum.GetValues(typeof(CSM.ManagerUI.Som)))
            {
                var d = CSM.ManagerUI.SomSintese.Renderizar(s);
                float pico = 0f; bool nan = false; foreach (var x in d) { if (float.IsNaN(x) || float.IsInfinity(x)) nan = true; pico = Mathf.Max(pico, Mathf.Abs(x)); }
                float seg = d.Length / (float)CSM.ManagerUI.SomSintese.TAXA;
                bool ok = !nan && pico > 0.08f && pico < 0.99f && seg > 0.03f && seg < 1.8f && Mathf.Abs(d[0]) < 0.05f && Mathf.Abs(d[d.Length - 1]) < 0.01f;
                if (!ok) { falhas++; Debug.LogError("FALHOU: som " + s + " (pico " + pico.ToString("0.00") + ", " + seg.ToString("0.00") + " s, nan " + nan + ")"); }
                else Debug.Log("ok: som " + s + " (pico " + pico.ToString("0.00") + ", " + (seg * 1000f).ToString("0") + " ms)");
                CSM.ManagerUI.SomSintese.GravarWav("Logs/sons/" + s + ".wav", d);
            }
            var amb = CSM.ManagerUI.SomSintese.Ambiente(); float ap = 0f, passo = 0f; for (int i = 0; i < amb.Length; i++) { ap = Mathf.Max(ap, Mathf.Abs(amb[i])); if (i > 0 && i < 4000) passo += Mathf.Abs(amb[i] - amb[i - 1]) / 3999f; }
            float emenda = Mathf.Abs(amb[amb.Length - 1] - amb[0]);
            if (ap < 0.1f || ap > 0.99f || emenda > passo * 8f) { falhas++; Debug.LogError("FALHOU: ambiente (pico " + ap.ToString("0.00") + ", emenda " + emenda.ToString("0.0000") + " contra passo " + passo.ToString("0.0000") + ")"); }
            else Debug.Log("ok: ambiente (" + amb.Length / (float)CSM.ManagerUI.SomSintese.TAXA + " s, emenda do laço " + emenda.ToString("0.0000") + ")");
            CSM.ManagerUI.SomSintese.GravarWav("Logs/sons/Ambiente.wav", amb);
            Debug.Log(falhas == 0 ? "TODOS OS SONS PASSARAM" : falhas + " SOM(NS) FALHARAM");
            EditorApplication.Exit(falhas == 0 ? 0 : 1);
        }

        /// <summary>Batch: cria a cena e sai (Unity -batchmode -executeMethod CSM.EditorTools.CenaManager.CriarBatch).</summary>
        public static void CriarBatch()
        {
            try { Criar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
