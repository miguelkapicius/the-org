using System;

namespace CSM.Map
{
    /// <summary>
    /// Qual mapa o MapData monta (escolhido ANTES de qualquer acesso ao MapData: o construtor estático monta uma vez só).
    /// Ordem: Nome (o Unity escolhe aqui antes de tocar no mapa) → "-mapa X" na linha de comando → "vila" (padrão).
    /// Mapas: "vila", "brasa".
    /// </summary>
    public static class MapaEscolhido
    {
        public static string Nome;
        public static string Qual()
        {
            if (!string.IsNullOrEmpty(Nome)) return Nome.Trim().ToLowerInvariant();
            try { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-mapa"); if (i >= 0 && i + 1 < a.Length) return a[i + 1].Trim().ToLowerInvariant(); }
            catch { }
            return "vila";
        }
        /// <summary>Arquivo de dados do mapa montado: a Vila fica no caminho de sempre; os outros em &lt;pasta&gt;/&lt;mapa&gt;/&lt;arquivo&gt;.</summary>
        public static string Arq(string vila) => MapData.MAPA == "vila" ? vila : vila.Substring(0, vila.LastIndexOf('/') + 1) + MapData.MAPA + vila.Substring(vila.LastIndexOf('/'));
    }
}
