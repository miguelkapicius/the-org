using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Manager;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Mede o efeito da estratégia do manager (decisão 0014) na partida: para cada estilo, o time A joga n partidas com ele
    /// contra um B Equilibrado, primeiro só com os pesos do preset (antes, -exp semEstiloBanco) e depois preferindo os planos
    /// do banco daquele estilo. Mostra quantos rounds usaram um plano do estilo, a hora do plant, as mortes antes dos 25 s e
    /// as vitórias por lado. Uso: dotnet Headless/bin/Release/net9.0/Headless.dll -estilos 18 -seed 500
    /// Relatório em Logs/bots/estilos.txt.
    /// </summary>
    public static class MedirEstilos
    {
        public static int Rodar(int n, int seed0)
        {
            var sb = new StringBuilder();
            foreach (bool tr in new[] { true, false })
                foreach (var g in Estrategias.Todos.Where(p => p.tr == tr).GroupBy(p => p.categoria).OrderBy(g => g.Key))
                    sb.AppendLine($"{(tr ? "TR" : "CT")} {g.Key,-17} {g.Count(),4} planos  " + string.Join(" ", g.GroupBy(p => p.compra).Select(x => x.Key + "=" + x.Count())));
            new Match(seed0); CSM.Map.Spots.All();
            sb.AppendLine();
            sb.AppendLine("Time A com o estilo, B Equilibrada. Por lado: % dos rounds com plano do banco daquele estilo · hora média do plant (% de rounds com plant) · mortes do lado antes dos 25 s · vitórias de A");
            foreach (string exp in new[] { "semEstiloBanco", "" })
            {
                TeamBrain.EXP = exp;
                sb.AppendLine(exp == "" ? "\nDEPOIS (prefere os planos do estilo):" : "\nANTES (só os pesos do preset):");
                foreach (Estilo e in Enum.GetValues(typeof(Estilo)))
                {
                    var cat = Estilos.Categoria(e); var trava = new object();
                    int rT = 0, bancoT = 0, doEstT = 0, vT = 0, plants = 0, rC = 0, bancoC = 0, doEstC = 0, vC = 0; double somaPlant = 0, mortesT = 0, mortesC = 0;
                    Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                    {
                        var pt = new Partida(seed0 + i, Estilos.Pesos(e), Estilos.Pesos(Estilo.Equilibrada));
                        int r = 0;
                        pt.Jogar(m =>
                        {
                            bool aT = Partida.ATerrorista(r++);
                            var est = m.igl[aT ? 0 : 1].Estrategia; bool venceuA = (m.winner == "TR") == aT;
                            lock (trava)
                            {
                                if (aT)
                                {
                                    rT++; if (est != null) { bancoT++; if (est.categoria == cat) doEstT++; } if (venceuA) vT++;
                                    if (m.bombPlanted) { plants++; somaPlant += m.plantedAt; }
                                    mortesT += m.agents.Count(x => x.IsT && x.diedAt >= 0 && x.diedAt < 25);
                                }
                                else
                                {
                                    rC++; if (est != null) { bancoC++; if (est.categoria == cat) doEstC++; } if (venceuA) vC++;
                                    mortesC += m.agents.Count(x => !x.IsT && x.diedAt >= 0 && x.diedAt < 25);
                                }
                            }
                        });
                    });
                    sb.AppendLine($"{e,-17} | TR {100.0 * doEstT / Math.Max(1, bancoT),3:0}% · plant {somaPlant / Math.Max(1, plants),3:0} s ({100.0 * plants / Math.Max(1, rT):0}%) · mortes {mortesT / Math.Max(1, rT):0.00} · vence {100.0 * vT / Math.Max(1, rT):0}%" +
                                  $" | CT {100.0 * doEstC / Math.Max(1, bancoC),3:0}% · mortes {mortesC / Math.Max(1, rC):0.00} · vence {100.0 * vC / Math.Max(1, rC):0}%  ({rT + rC} rounds)");
                }
            }
            TeamBrain.EXP = "";
            Directory.CreateDirectory("Logs/bots");
            File.WriteAllText("Logs/bots/estilos.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
    }
}
