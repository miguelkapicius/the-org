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

        /// <summary>Batch: confere as regras novas da carreira e o ciclo salvar/carregar; sai com 0 se tudo bate, 1 se não
        /// (Unity -batchmode -nographics -executeMethod CSM.EditorTools.CenaManager.TestarCarreiraBatch).</summary>
        public static void TestarCarreiraBatch()
        {
            int falhas = 0;
            void Ok(bool cond, string o) { if (!cond) { falhas++; Debug.LogError("FALHOU: " + o); } else Debug.Log("ok: " + o); }
            try
            {
                var c = CSM.Manager.Carreira.Nova(5);
                Ok(c.elenco.Count == 7 && c.Forca > 0, "carreira nova com 7 jogadores");

                // troca de lugar e desfazer
                var a = c.elenco[0]; var b = c.elenco[5];
                c.Trocar(a, b);
                Ok(c.elenco[0] == b && c.elenco[5] == a, "Trocar inverte os lugares");

                // vender e desfazer: dinheiro, posição e extrato voltam ao que eram
                int din = c.dinheiro, ext = c.extrato.Count, pos = c.elenco.IndexOf(a);
                Ok(c.Vender(a) == null && c.elenco.Count == 6 && c.dinheiro == din + a.Venda, "Vender paga o valor de venda");
                Ok(c.DesfazerVenda() == null && c.dinheiro == din && c.extrato.Count == ext && c.elenco.IndexOf(a) == pos, "DesfazerVenda restaura dinheiro, extrato e posição");
                Ok(c.DesfazerVenda() != null, "DesfazerVenda sem venda recente devolve erro");

                // prévia de força e chance
                var cand = c.mercado.OrderByDescending(j => j.Overall).First();
                var sai = c.QuemSaiPara(cand);
                if (sai != null) Ok(c.ForcaCom(cand, sai) > c.Forca, "ForcaCom: entrar no lugar do mais fraco aumenta a força");
                Ok(c.ChanceVitoria(c.Forca + 10) > c.ChanceVitoria(c.Forca), "mais força = mais chance de vitória");

                // avisos
                c.dinheiro = 1000;
                Ok(c.Avisos().Any(x => x.nivel == CSM.Manager.Aviso.Nivel.Critico), "saldo que não paga salários gera aviso crítico");

                // desempenho estimado ao pular + salvar/carregar
                c.dinheiro = 400000;
                var r = c.Pular();
                Ok(r.estimado && r.desempenho.Count == 5 && r.Melhor != null, "partida pulada gera desempenho estimado de 5 jogadores");
                var salvo = CSM.ManagerUI.Salvamento.Caminho;
                CSM.ManagerUI.Salvamento.Salvar(c);
                var d = CSM.ManagerUI.Salvamento.Carregar();
                Ok(d != null, "Carregar devolve a carreira salva");
                if (d != null)
                {
                    Ok(d.dinheiro == c.dinheiro && d.numPartida == c.numPartida && d.nivelGH == c.nivelGH && d.clube == c.clube, "valores simples iguais");
                    Ok(d.elenco.Select(j => j.nome + j.funcao + j.Overall).SequenceEqual(c.elenco.Select(j => j.nome + j.funcao + j.Overall)), "elenco igual (nome, função, geral)");
                    Ok(d.mercado.Count == c.mercado.Count && d.extrato.Count == c.extrato.Count && d.historico.Count == c.historico.Count, "mercado, extrato e histórico com o mesmo tamanho");
                    Ok(d.proximo.nome == c.proximo.nome && d.proximo.jogadores.Count == c.proximo.jogadores.Count, "adversário igual");
                    Ok(d.historico[0].desempenho.Count == 5 && d.historico[0].estimado, "desempenho do histórico preservado");
                }
                CSM.ManagerUI.Salvamento.Apagar();
                Ok(CSM.ManagerUI.Salvamento.Carregar() == null, "sem arquivo, Carregar devolve null");
            }
            catch (System.Exception e) { falhas++; Debug.LogException(e); }
            Debug.Log(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " TESTE(S) FALHARAM");
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
