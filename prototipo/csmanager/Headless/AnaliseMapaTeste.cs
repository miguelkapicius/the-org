using System;
using System.IO;
using System.Text;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Headless -analiseMapa: imprime a leitura geométrica do mapa (AnaliseMapa.Texto: entradas, tempos, posições de defesa e
    /// a linha de cada uma sobre cada entrada) e grava em Logs/bots/analise_mapa.txt. Só lê.
    /// </summary>
    static class AnaliseMapaTeste
    {
        public static int Rodar()
        {
            string txt = AnaliseMapa.Texto();
            Console.WriteLine(txt);
            string pasta = Path.Combine("Logs", "bots");
            Directory.CreateDirectory(pasta);
            string arq = Path.Combine(pasta, CSM.Map.MapData.MAPA == "vila" ? "analise_mapa.txt" : "analise_mapa_" + CSM.Map.MapData.MAPA + ".txt");   // a Vila no nome de sempre
            File.WriteAllText(arq, txt, new UTF8Encoding(false));
            Console.WriteLine("gravado em " + arq);
            return 0;
        }
    }
}
