using System;
using System.IO;
using UnityEngine;
using CSM.Manager;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Salva e carrega a carreira em disco (JSON em Application.persistentDataPath). O GDD ainda não definiu o salvamento
    /// (gdd/09-tecnico.md): isto é o mínimo para o protótipo não perder a carreira ao fechar. A conversão carreira ⇄ dados está em
    /// CSM.Manager.SalvamentoDados (sem Unity, testada em ManagerTest); aqui só o arquivo e o JsonUtility.
    /// </summary>
    public static class Salvamento
    {
        public static string Caminho => Path.Combine(Application.persistentDataPath, "carreira.json");
        public static bool Existe => File.Exists(Caminho);

        public static void Salvar(Carreira c)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string tmp = Caminho + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(SalvamentoDados.Para(c)));
                if (File.Exists(Caminho)) File.Delete(Caminho);
                File.Move(tmp, Caminho);   // escreve num arquivo temporário e troca: uma queda no meio não corrompe o salvamento
            }
            catch (Exception e) { Debug.LogWarning("Não foi possível salvar a carreira: " + e.Message); }
        }

        /// <summary>Carrega a carreira salva; null se não houver, se for de outra versão ou se o arquivo estiver ilegível.</summary>
        public static Carreira Carregar()
        {
            try
            {
                if (!Existe) return null;
                var c = SalvamentoDados.De(JsonUtility.FromJson<CarreiraD>(File.ReadAllText(Caminho)));
                if (c == null) Debug.LogWarning("O salvamento é de outra versão do jogo (ou está incompleto): começando uma carreira nova.");
                return c;
            }
            catch (Exception e) { Debug.LogWarning("Salvamento ilegível, começando uma carreira nova: " + e.Message); return null; }
        }

        public static void Apagar() { try { if (Existe) File.Delete(Caminho); } catch (Exception) { } }
    }
}
