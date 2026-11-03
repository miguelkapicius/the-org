using System.Collections.Generic;
using CSM.Map;
using static CSM.Map.MapData;

namespace CSM.Sim
{
    /// <summary>
    /// Lugares com nome que o IGL e os bots usam pra planejar (em quadradinhos da planta, convertidos pra unidades):
    /// posições de CT, pontos de reunião do TR, alvos de granada (lineups), spots de plant e pós-plant. Tudo em cima da
    /// navmesh (o caminho real é sempre calculado na hora).
    /// </summary>
    public static class Places
    {
        public static V2 Cell(float cx, float cy) => new V2((cx + 0.5f) * Q, (cy + 0.5f) * Q);

        public class Hold { public string name; public V2 pos; public V2[] look; public Hold(string n, V2 p, params V2[] l) { name = n; pos = p; look = l; } }

        // ---- entradas dos bombs (pra onde o CT olha e por onde o TR entra)
        public static readonly V2 A_Rampa = Cell(36, 12.5f), A_Sacada = Cell(30, 12), A_Conector = Cell(30, 9.5f), A_RuaCT = Cell(29, 2), A_Descida = Cell(30, 4.5f);
        public static readonly V2 B_Tapete = Cell(6, 16), B_Curta = Cell(9.5f, 12.5f), B_Viela = Cell(3.5f, 4.5f), B_Mercado = Cell(10, 4.5f);
        public static readonly V2 Meio_Sul = Cell(18.5f, 20), Meio_Norte = Cell(18.5f, 10), TopoEntrada = Cell(22, 23.5f), Janelao = Cell(18, 6.5f);

        // ---- bomb
        public static readonly V2 PlantA = Cell(33.5f, 7.5f), PlantB = Cell(6, 10);
        public static V2 Plant(char site) => site == 'A' ? PlantA : PlantB;

        // ---- CT: posições de segurar por setup
        public static readonly Hold CT_A_Plataforma = new Hold("plataforma", Cell(36, 2.6f), A_Rampa, A_Sacada);
        public static readonly Hold CT_A_Canto = new Hold("canto do A", Cell(32, 5.5f), A_Conector, A_Sacada, A_Rampa);
        public static readonly Hold CT_Janelao = new Hold("janelão", Cell(18.5f, 6.7f), Meio_Sul, TopoEntrada);
        public static readonly Hold CT_Praca = new Hold("praça", Cell(22, 7.5f), Meio_Norte, Cell(24, 12.5f));
        // ---- CT no meio: quem tem o meio escolhe onde lutar e corta a rotação do TR (o conector de um lado, o rato do outro)
        public static readonly Hold CT_MeioCaixa = new Hold("caixa do meio", Cell(20.2f, 8.9f), Meio_Sul, Cell(18.5f, 15));
        public static readonly Hold CT_Conector = new Hold("conector do CT", Cell(25, 12.5f), Cell(21.5f, 12.5f), Meio_Norte);
        public static readonly Hold CT_Rato = new Hold("caminho de rato", Cell(15.4f, 11), Cell(15.4f, 14.5f), Cell(12, 12.5f));
        public static readonly Hold CT_B_Banca = new Hold("banca", Cell(3, 7), B_Tapete, B_Curta);
        public static readonly Hold CT_B_Pilar = new Hold("pilar do B", Cell(7.5f, 9), B_Curta, B_Tapete);
        public static readonly Hold CT_B_Viela = new Hold("viela", Cell(6, 2), B_Viela, Cell(6, 8));
        public static readonly Hold CT_A_RuaCT = new Hold("rua do CT", Cell(27, 2), A_Descida, A_Conector);
        // âncora de ângulo perto (o rifle ganha de perto; a plataforma é linha longa, boa pra AWP do TR na rampa)
        public static readonly Hold CT_A_Caixa = new Hold("quina da rampa", Cell(38.3f, 10.8f), A_Rampa, A_Sacada);   // fora da linha da rampa: só aparece pra quem já saiu dela
        public static readonly Hold CT_B_Caixa = new Hold("canto do tapete", Cell(2.8f, 12.8f), B_Tapete, B_Curta);
        public static Hold[] AncoraA => new[] { CT_A_Plataforma, CT_A_Caixa };
        public static Hold[] AncoraB => new[] { CT_B_Banca, CT_B_Caixa };

        // ---- TR: posições de controle/espera
        public static readonly Hold T_TopoMeio = new Hold("topo do meio", Cell(22.8f, 26.5f), TopoEntrada, Cell(19, 24));
        public static readonly Hold T_MeioSul = new Hold("meio", Cell(16.2f, 20.5f), Cell(18.5f, 16), Cell(19.5f, 22));   // na boca da curta: toma o meio sem ficar parado na linha do janelão
        public static readonly Hold T_RampaBaixo = new Hold("rampa de baixo", Cell(32, 22), Cell(34, 17), A_Rampa);
        public static readonly Hold T_RampaDobra = new Hold("dobra da rampa", Cell(33, 17), A_Rampa, Cell(36, 13));
        public static readonly Hold T_PalacioSala = new Hold("sala do palácio", Cell(27, 16.5f), A_Sacada, Cell(28.5f, 13.5f));
        public static readonly Hold T_CasaraoDobra = new Hold("dobra do casarão", Cell(6, 19.5f), B_Tapete, Cell(4.5f, 17.5f));
        public static readonly Hold T_CurtaCima = new Hold("curta de cima", Cell(12.5f, 16.5f), B_Curta, Cell(12.5f, 13));
        public static readonly Hold T_Conector = new Hold("conector", Cell(23, 12.5f), A_Conector, Meio_Norte);
        public static readonly Hold T_RuaB = new Hold("rua do B", Cell(13.5f, 30.5f), Cell(10.5f, 30.5f), Cell(6, 26));

        // ---- CT: jogadas agressivas cedo (pega info/abate antes do TR chegar e volta pro setup)
        public static readonly Hold CT_Agr_Rampa = new Hold("peek agressivo na rampa", Cell(36, 12), Cell(33.5f, 16.5f), T_RampaBaixo.pos);
        public static readonly Hold CT_Agr_Meio = new Hold("agressão no meio", Cell(18.5f, 11), Meio_Sul, TopoEntrada);
        public static readonly Hold CT_Agr_Curta = new Hold("peek agressivo na curta", Cell(9.5f, 13), T_CurtaCima.pos, Cell(12.5f, 19));
        public static readonly Hold CT_Agr_Tapete = new Hold("agressão no tapete", Cell(6, 15), T_CasaraoDobra.pos, Cell(4.5f, 17.5f));

        // ---- pós-plant (TR protege o bomb de longe, olhando as entradas do CT)
        public static Hold[] PostPlant(char site) => site == 'A'
            ? new[] { new Hold("pós-plant rampa", Cell(36, 14), PlantA, A_Descida), new Hold("pós-plant sacada", Cell(30, 12), PlantA, A_RuaCT),
                      new Hold("pós-plant conector", Cell(27, 9.5f), PlantA, A_Descida), new Hold("pós-plant caixa A", Cell(37, 9), A_Descida, CT_A_Plataforma.pos) }
            : new[] { new Hold("pós-plant tapete", Cell(6, 16), PlantB, B_Viela), new Hold("pós-plant curta", Cell(11, 12.5f), PlantB, B_Mercado),
                      new Hold("pós-plant casarão", Cell(5, 18), PlantB, B_Viela), new Hold("pós-plant caixa B", Cell(3, 13), B_Viela, B_Mercado) };

        // ---- retomada do CT: onde juntar antes de entrar
        // entradas diferentes (rua do CT, plataforma, conector pela praça; viela, mercado): retomada entra por mais de um
        // lado ao mesmo tempo, não um bolo numa porta só
        public static V2[] RetakeStage(char site) => site == 'A' ? new[] { Cell(28, 2), Cell(36, 2.6f), Cell(22, 7.5f) } : new[] { Cell(6, 1.5f), Cell(11, 4) };

        // ---- granadas (de onde joga, onde cai): o TR joga do lado dele, o CT do lado dele
        public class Lineup { public string name, kind; public V2 from, to; public Lineup(string n, string k, V2 f, V2 t) { name = n; kind = k; from = f; to = t; } }
        public static readonly Lineup SmokeJanelao = new Lineup("smoke do janelão", "smoke", Cell(22.8f, 27.2f), Cell(18.75f, 14.5f));   // jogada de trás da quina (fora da mira do janelão)
        public static readonly Lineup SmokeRuaCT = new Lineup("smoke da descida do CT", "smoke", Cell(34, 17), Cell(30.5f, 4.25f));
        public static readonly Lineup SmokePlataforma = new Lineup("smoke da plataforma", "smoke", Cell(36, 15.5f), Cell(35.5f, 3.5f));
        public static readonly Lineup MolotovCantoA = new Lineup("molotov no canto do A", "molotov", Cell(36, 15), Cell(32.5f, 6));
        public static readonly Lineup FlashA = new Lineup("flash por cima do A", "flash", Cell(36, 14.5f), Cell(35, 8));
        public static readonly Lineup SmokeViela = new Lineup("smoke da viela", "smoke", Cell(5, 18.5f), Cell(4.25f, 4.5f));
        public static readonly Lineup SmokeMercado = new Lineup("smoke da descida do mercado", "smoke", Cell(12.5f, 15), Cell(8, 6.25f));
        public static readonly Lineup MolotovBanca = new Lineup("molotov na banca", "molotov", Cell(5, 18), Cell(3, 7));
        public static readonly Lineup FlashB = new Lineup("flash por cima do B", "flash", Cell(12.5f, 14.5f), Cell(7, 10));
        // CT
        public static readonly Lineup CT_MolotovRampa = new Lineup("molotov na rampa", "molotov", Cell(33, 6), Cell(35.5f, 14.5f));
        public static readonly Lineup CT_MolotovTapete = new Lineup("molotov no tapete", "molotov", Cell(5, 8), Cell(5.25f, 17));
        public static readonly Lineup CT_SmokeMeio = new Lineup("smoke no topo do meio", "smoke", Cell(21.5f, 7), Cell(18.5f, 19.25f));
        // ---- tomada de posição (TR): quem vai tomar o lugar sabe a granada que limpa o ângulo típico do CT dali
        public static readonly Lineup MolotovQuinaRampa = new Lineup("molotov na quina da rampa", "molotov", Cell(33, 17), Cell(37.3f, 10.8f));
        public static readonly Lineup FlashSacada = new Lineup("flash por cima da sacada", "flash", Cell(27, 16.5f), Cell(30, 11.75f));
        public static readonly Lineup MolotovCantoTapete = new Lineup("molotov no canto do tapete", "molotov", Cell(6, 19.5f), Cell(2.8f, 12.8f));
        public static readonly Lineup MolotovPilar = new Lineup("molotov no pilar do B", "molotov", Cell(12.5f, 16.5f), Cell(7, 8.75f));
        public static readonly Lineup FlashMeio = new Lineup("flash do meio", "flash", Cell(22, 25), Cell(19, 16));
        public static readonly Lineup FlashConector = new Lineup("flash do conector", "flash", Cell(23, 12.5f), Cell(25.5f, 10.5f));
        // ---- retomada (CT): smoke corta o reforço do TR, flash por cima do bomb na entrada
        public static readonly Lineup CT_SmokeRampaRetake = new Lineup("smoke da rampa (retake)", "smoke", Cell(30, 4), Cell(35.5f, 13));
        public static readonly Lineup CT_FlashARetake = new Lineup("flash por cima do A (retake)", "flash", Cell(28, 2), Cell(33.5f, 7.5f));
        public static readonly Lineup CT_SmokeTapeteRetake = new Lineup("smoke do tapete (retake)", "smoke", Cell(6, 1.5f), Cell(5.5f, 15.5f));
        public static readonly Lineup CT_FlashBRetake = new Lineup("flash por cima do B (retake)", "flash", Cell(11, 4), Cell(7, 10));
        public static Lineup[] RetakeSmokes(char s) => s == 'A' ? new[] { CT_SmokeRampaRetake } : new[] { CT_SmokeTapeteRetake };
        public static Lineup[] RetakeFlashes(char s) => s == 'A' ? new[] { CT_FlashARetake } : new[] { CT_FlashBRetake };
        /// <summary>Granadas de tomada por posição do TR (quem vai tomar aquele lugar).</summary>
        public static Lineup[] Tomada(Hold h) =>
            h == T_RampaDobra || h == T_RampaBaixo ? new[] { MolotovQuinaRampa } :
            h == T_PalacioSala ? new[] { FlashSacada } :
            h == T_CasaraoDobra ? new[] { MolotovCantoTapete } :
            h == T_CurtaCima ? new[] { MolotovPilar } :
            h == T_TopoMeio || h == T_MeioSul ? new[] { FlashMeio } :
            h == T_Conector ? new[] { FlashConector } : System.Array.Empty<Lineup>();

        // entradas de cada bomb onde o CT joga utilitária pra atrasar a execução (molotov ou smoke, o que tiver)
        public static readonly (string nome, V2 p)[] ENTRADAS_A = { ("rampa", Cell(35.5f, 14.5f)), ("sacada", Cell(30, 12.3f)) };
        public static readonly (string nome, V2 p)[] ENTRADAS_B = { ("tapete", Cell(5.25f, 17)), ("curta", Cell(10.5f, 12.8f)) };

        /// <summary>Pra que serve cada smoke: de onde o inimigo olha e os pontos que ela tem que esconder (validador).</summary>
        public static readonly Dictionary<Lineup, (V2 de, V2[] para)[]> CORTA = new Dictionary<Lineup, (V2, V2[])[]>
        {
            // o janelão é alto: a linha da AWP passa por cima do domo pra ver o fundo do meio — tem que cobrir até lá
            [SmokeJanelao] = new[] { (CT_Janelao.pos, new[] { Meio_Sul, T_MeioSul.pos, Cell(18.5f, 15), Cell(18.5f, 17), Cell(19.5f, 21.5f), Cell(18.5f, 23), Cell(20, 22.5f), TopoEntrada }), (CT_Praca.pos, new[] { Meio_Sul, Cell(18.5f, 15) }) },
            [SmokeRuaCT] = new[] { (CT_A_RuaCT.pos, new[] { PlantA, Cell(34, 10), A_Rampa }), (A_Descida, new[] { PlantA, Cell(34, 10) }) },
            [SmokePlataforma] = new[] { (CT_A_Plataforma.pos, new[] { A_Rampa, Cell(35.5f, 11), PlantA }) },
            [SmokeViela] = new[] { (CT_B_Viela.pos, new[] { PlantB, B_Tapete }), (B_Viela, new[] { PlantB, B_Tapete }) },
            // a descida do mercado não vê o bomb: a smoke fecha a saída (quem sai dela é que veria o plant)
            [SmokeMercado] = new[] { (Cell(7.5f, 6), new[] { PlantB, Cell(7, 10), B_Curta }) },
            [CT_SmokeRampaRetake] = new[] { (T_RampaDobra.pos, new[] { PlantA, Cell(34, 6) }), (Cell(36, 14), new[] { PlantA, Cell(34, 6) }) },
            [CT_SmokeTapeteRetake] = new[] { (T_CasaraoDobra.pos, new[] { PlantB, Cell(5, 10) }), (Cell(5, 18), new[] { PlantB, Cell(5, 10) }) },
            [CT_SmokeMeio] = new[] { (Cell(19, 24), new[] { Meio_Norte, Cell(18.5f, 15), CT_Janelao.pos }), (Meio_Sul, new[] { Meio_Norte, CT_Janelao.pos }), (T_MeioSul.pos, new[] { Meio_Norte, CT_Janelao.pos }) },
        };

        /// <summary>As lineups treinadas (todas as declaradas aqui): o jogador sabe o pixel, a granada cai no lugar.</summary>
        public static readonly HashSet<Lineup> TREINADAS = new HashSet<Lineup>(
            System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(typeof(Places).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                f => f.FieldType == typeof(Lineup)), f => (Lineup)f.GetValue(null)));

        /// <summary>Região do mapa onde está o ponto (pras calls do IGL).</summary>
        public static string Region(V2 p)
        {
            var a = AreaAt(p.x, p.y);
            if (a == null) return "?";
            return CALLOUT.TryGetValue(a, out var c) ? c : a;
        }

        /// <summary>
        /// Zona por FUNÇÃO, como nos dados pro de Mirage (tactics.json: A / Mid / B / Spawn): o que cada lugar da Vila
        /// faz no round — palácio/rampa/conector levam ao A, topo/meio/janelão/rato são o meio, curta/casarão/mercado/viela
        /// levam ao B. É a régua pra comparar nossos rounds com os pro e pra descrever estratégia por zona.
        /// </summary>
        public static string Zona(V2 p)
        {
            var a = AreaAt(p.x, p.y);
            return a != null && ZONA.TryGetValue(a, out var z) ? z : "Mid";
        }
        static readonly Dictionary<string, string> ZONA = new Dictionary<string, string>
        {
            ["TR"] = "Spawn", ["CT"] = "Spawn",
            ["A"] = "A", ["Plataforma"] = "A", ["Esc_plataforma"] = "A", ["Rua_CT"] = "A", ["Descida_CT"] = "A",
            ["Rampa_saida"] = "A", ["Rampa_baixo"] = "A", ["Rampa_dobra"] = "A", ["Rampa"] = "A",
            ["Hall_palacio"] = "A", ["Esc_palacio"] = "A", ["Palacio"] = "A", ["Palacio_sala"] = "A", ["Palacio_corredor"] = "A", ["Sacada_A"] = "A",
            ["Conector"] = "A", ["Conector_dobra"] = "A", ["Conector_fim"] = "A",
            ["Topo_meio"] = "Mid", ["Topo_entrada"] = "Mid", ["Meio"] = "Mid", ["Praca"] = "Mid", ["Esc_janelao"] = "Mid", ["Janelao"] = "Mid",
            ["Rato"] = "Mid", ["Esc_rato"] = "Mid", ["Rato_fundo"] = "Mid",
            ["Curta"] = "B", ["Curta_sobe"] = "B", ["Curta_fim"] = "B", ["B"] = "B", ["Banca"] = "B", ["Esc_banca"] = "B", ["Carroca"] = "B", ["Carroca_degrau"] = "B",
            ["Mercado"] = "B", ["Descida_mercado"] = "B", ["Passagem_viela"] = "B", ["Viela"] = "B", ["Esc_viela"] = "B",
            ["Rua_B"] = "B", ["Beco_casarao"] = "B", ["Esc_casarao"] = "B", ["Casarao"] = "B", ["Casarao_corredor"] = "B", ["Casarao_dobra"] = "B", ["Casarao_saida"] = "B", ["Tapete"] = "B",
        };

        /// <summary>Qual bomb fica mais perto (A/B) ou meio.</summary>
        public static char Side(V2 p)
        {
            float dA = (p - SITES["A"].Center).Len, dB = (p - SITES["B"].Center).Len;
            if (p.x > 23 * Q || dA < 230) return 'A';
            if (p.x < 13 * Q || dB < 230) return 'B';
            return 'M';
        }
    }
}
