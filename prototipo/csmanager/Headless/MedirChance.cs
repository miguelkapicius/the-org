using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Manager;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Calibra a partida pulada (decisão 0008/0035) com a simulação: para cada diferença de força, roda n partidas inteiras
    /// (A com a força base + d/2, B com a base - d/2, atributos iguais ± 4 como no adversário do manager) e mede a chance de A
    /// vencer e a distribuição do placar. O resultado serve para ajustar <c>Mundo.Chance</c> (escala da curva) e a tabela de
    /// placares da partida pulada. Uso: Headless.dll -chance 60 -seed 700 [-base 65]. Relatório em Logs/bots/chance.txt.
    /// </summary>
    public static class MedirChance
    {
        public static readonly int[] DIFS = { 0, 3, 6, 10, 15, 20, 30 };

        public static int Rodar(int n, int seed0, int baseForca)
        {
            new Match(seed0); CSM.Map.Spots.All();   // aquece as tabelas estáticas antes das threads (como o MedirEstilos)
            var sb = new StringBuilder();
            sb.AppendLine($"Força base {baseForca}; A = base + d/2, B = base - d/2; {n} partidas por diferença (MR12, prorrogação incluída).");
            sb.AppendLine("d    | A vence | k=d/logit | placar do vencedor 13 x (0-3, 4-6, 7-9, 10-11, 12+ / prorrogação) | rounds ganhos pelo perdedor (média)");
            var ks = new List<double>();
            var linhas = new List<(int d, double p, double[] dist)>();
            foreach (int d in DIFS)
            {
                var trava = new object(); int vA = 0, ot = 0; long somaPerd = 0; var faixa = new int[5];
                Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                {
                    var pt = new Partida(seed0 + i * 7 + d);
                    var r = new Random(seed0 + i);
                    void Dar(Partida.Time t, float v100)
                    {
                        for (int v = 0; v < t.js.Length; v++)
                        {
                            float x = Math.Clamp(v100 + (float)(r.NextDouble() - 0.5) * 8f, 0f, 100f);
                            var jg = new Jogador(); for (int a = 0; a < 5; a++) jg.Definir(a, x);
                            t.js[v].skill = jg.skill;
                        }
                    }
                    Dar(pt.A, baseForca + d / 2f); Dar(pt.B, baseForca - d / 2f);
                    pt.Jogar(null);
                    int ganha = Math.Max(pt.A.placar, pt.B.placar), perde = Math.Min(pt.A.placar, pt.B.placar);
                    lock (trava)
                    {
                        if (pt.A.placar > pt.B.placar) vA++;
                        if (ganha + perde > 24) ot++;
                        somaPerd += perde;
                        int f = ganha + perde > 24 ? 4 : perde <= 3 ? 0 : perde <= 6 ? 1 : perde <= 9 ? 2 : 3;
                        faixa[f]++;
                    }
                });
                double p = (double)vA / n; double pc = Math.Clamp(p, 0.02, 0.98);
                double k = d == 0 ? double.NaN : d / Math.Log(pc / (1 - pc));
                if (!double.IsNaN(k) && p > 0.55 && p < 0.97) ks.Add(k);
                linhas.Add((d, p, faixa.Select(x => (double)x / n).ToArray()));
                sb.AppendLine($"{d,4} | {100 * p,6:0.0}% | {(double.IsNaN(k) ? "-" : k.ToString("0.0")),5} | " +
                    $"{100.0 * faixa[0] / n,3:0}% · {100.0 * faixa[1] / n,3:0}% · {100.0 * faixa[2] / n,3:0}% · {100.0 * faixa[3] / n,3:0}% · {100.0 * faixa[4] / n,3:0}% (12-12/prorrog.) | {(double)somaPerd / n:0.0}");
                Console.WriteLine(sb.ToString().Split('\n').Last(l => l.Length > 3).TrimEnd());
            }
            sb.AppendLine();
            sb.AppendLine(ks.Count > 0 ? $"Escala k medida (média das diferenças com chance entre 55 e 97 %): {ks.Average():0.0}. Hoje Mundo.Chance usa 6,0." : "Sem dados suficientes para k.");
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/chance.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
    }
}
