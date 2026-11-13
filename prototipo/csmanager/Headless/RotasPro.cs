using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CSM.Map;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// "Onde o inimigo costuma estar" tirado dos PROS (Headless -rotasPro): o rotas.bin antigo foi aprendido de bot × bot e
    /// reforçava os vícios dos bots (a troca de tiro de longe na base CT todo round). Aqui cada pro do playbook, a cada 0,5 s,
    /// conta como presença espalhada pelas células da área equivalente da Vila (tradução pela função do lugar, a mesma do
    /// converter.py). Grava rotas_pro.bin no mesmo formato do Rotas (lido com -exp rotasProLigado).
    /// </summary>
    static class RotasPro
    {
        static readonly Dictionary<string, string> MIRAGE = new Dictionary<string, string>
        {
            ["TSpawn"] = "TR", ["CTSpawn"] = "CT", ["TRamp"] = "Rampa_baixo", ["PalaceAlley"] = "Hall_palacio", ["PalaceInterior"] = "Palacio_sala",
            ["Scaffolding"] = "Sacada_A", ["BombsiteA"] = "A", ["Stairs"] = "Esc_plataforma", ["Jungle"] = "Conector_fim", ["Connector"] = "Conector",
            ["Middle"] = "Meio", ["TopofMid"] = "Topo_meio", ["House"] = "Topo_entrada", ["Underpass"] = "Rato", ["Ladder"] = "Esc_rato",
            ["SnipersNest"] = "Janelao", ["Catwalk"] = "Curta_sobe", ["Shop"] = "Mercado", ["SideAlley"] = "Rua_B", ["BackAlley"] = "Casarao",
            ["Apartments"] = "Casarao_corredor", ["BombsiteB"] = "B", ["Truck"] = "Viela",
        };

        public static int Rodar()
        {
            if (MapData.MAPA != "vila") { Console.WriteLine("por enquanto só a Vila (Mirage); a Brasa usa o converter_brasa"); return 1; }
            var doc = JsonDocument.Parse(File.ReadAllText(@"D:\unity\demos_cs\cs-simulator\contract\artifacts\tactics_playbook.json"));
            var M = doc.RootElement.GetProperty("maps").GetProperty("de_mirage");
            var places = M.GetProperty("places").EnumerateArray().Select(x => x.GetString()).ToArray();
            // células (do grid do Intel) de cada área da Vila
            int n = Intel.GX * Intel.GY;
            var cel = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                V2 c = Intel.CenterOf(i);
                if (!NavGrid.Movable(c)) continue;
                string a = MapData.AreaAt(c.x, c.y); if (a == null) continue;
                if (!cel.TryGetValue(a, out var l)) cel[a] = l = new List<int>();
                l.Add(i);
            }
            var falta = MIRAGE.Values.Where(v => !cel.ContainsKey(v)).ToList();
            if (falta.Count > 0) Console.WriteLine("áreas sem célula: " + string.Join(", ", falta));
            var soma = new float[2][][]; for (int tm = 0; tm < 2; tm++) { soma[tm] = new float[Rotas.NF][]; for (int f = 0; f < Rotas.NF; f++) soma[tm][f] = new float[n]; }
            var amostras = new int[Rotas.NF];
            int rounds = 0;
            foreach (var R in M.GetProperty("rounds").EnumerateArray())
            {
                float dur = (float)R.GetProperty("duration_s").GetDouble(); rounds++;
                for (float t = 0; t < dur && t < Rotas.NF * Rotas.FAIXA; t += 0.5f)
                {
                    int f = (int)(t / Rotas.FAIXA); amostras[f]++;
                    foreach (var (lado, tm) in new[] { ("T", (int)Team.T), ("CT", (int)Team.CT) })
                        foreach (var j in R.GetProperty(lado).EnumerateArray())
                        {
                            if (j.TryGetProperty("died_at", out var da) && da.ValueKind == JsonValueKind.Number && da.GetDouble() <= t) continue;
                            string reg = null;
                            foreach (var st in j.GetProperty("steps").EnumerateArray()) { double a = st[1].GetDouble(), b = st[2].GetDouble(); if (a <= t && t < b) { reg = places[st[0].GetInt32()]; break; } }
                            if (reg == null || !MIRAGE.TryGetValue(reg, out var area) || !cel.TryGetValue(area, out var cs) || cs.Count == 0) continue;
                            float w = 1f / cs.Count; foreach (int i in cs) soma[tm][f][i] += w;
                        }
                }
            }
            var arq = Rotas.ArqPro;
            using (var bw = new BinaryWriter(File.Create(arq)))
            {
                bw.Write(n); bw.Write(Rotas.NF);
                for (int tm = 0; tm < 2; tm++) for (int f = 0; f < Rotas.NF; f++) for (int i = 0; i < n; i++) bw.Write(amostras[f] > 0 ? soma[tm][f][i] / amostras[f] : 0f);
            }
            Console.WriteLine($"{rounds} rounds pro de Mirage → {arq} ({cel.Count} áreas da Vila, {n} células)");
            return 0;
        }
    }
}
