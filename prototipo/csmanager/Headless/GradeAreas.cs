using System;
using System.IO;
using System.Text;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Headless -gradeAreas: grava Tools/pro/areas_&lt;mapa&gt;.tsv com a área e a zona (A/Mid/B/Spawn) de cada célula de 5
    /// unidades do mapa. Serve pros scripts em Python lerem a caixa-preta por região (Tools/pro/macro_ct.py) sem copiar o mapa.
    /// </summary>
    static class GradeAreas
    {
        public static int Rodar()
        {
            const int P = 5; int nx = MapData.NX / P, ny = MapData.NY / P;
            var sb = new StringBuilder(); sb.Append(P).Append('\t').Append(nx).Append('\t').Append(ny).Append('\n');
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    string a = MapData.AreaAt(x * P + 2, y * P + 2); if (a == null) continue;
                    sb.Append(x).Append('\t').Append(y).Append('\t').Append(a).Append('\t').Append(Places.ZonaDaArea(a)).Append('\t').Append(Places.Region(new V2(x * P + 2, y * P + 2))).Append('\n');
                }
            string arq = Path.Combine("Tools", "pro", "areas_" + MapData.MAPA + ".tsv");
            File.WriteAllText(arq, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("gravado em " + arq);
            return 0;
        }
    }
}
