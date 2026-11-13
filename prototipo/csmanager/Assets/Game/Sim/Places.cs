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
        // mapa 2 (Brasa): cada lugar com nome tem o equivalente pela FUNÇÃO (entrada principal do TR, ângulo fundo do CT...).
        // C(x, y da Vila, x, y da Brasa): a Vila continua com os mesmos números (bit a bit igual).
        static readonly bool BR = MAPA == "brasa";
        static V2 C(float vx, float vy, float bx, float by) => BR ? Cell(bx, by) : Cell(vx, vy);

        public class Hold { public string name; public V2 pos; public V2[] look; public Hold(string n, V2 p, params V2[] l) { name = n; pos = p; look = l; } }

        // ---- entradas dos bombs (pra onde o CT olha e por onde o TR entra)
        public static readonly V2 A_Rampa = C(36, 12.5f, 42.5f, 25.5f), A_Sacada = C(30, 12, 47.5f, 29.5f), A_Conector = C(30, 9.5f, 42, 18), A_RuaCT = C(29, 2, 51.5f, 9.5f), A_Descida = C(30, 4.5f, 40, 11);
        public static readonly V2 B_Tapete = C(6, 16, 18, 13), B_Curta = C(9.5f, 12.5f, 16, 9.5f), B_Viela = C(3.5f, 4.5f, 10, 5), B_Mercado = C(10, 4.5f, 16.5f, 6.5f);
        public static readonly V2 Meio_Sul = C(18.5f, 20, 25, 31), Meio_Norte = C(18.5f, 10, 37, 27), TopoEntrada = C(22, 23.5f, 24.5f, 37), Janelao = C(18, 6.5f, 37, 24);

        // ---- bomb
        public static readonly V2 PlantA = C(33.5f, 7.5f, 48.5f, 19), PlantB = C(6, 10, 10, 12);
        public static V2 Plant(char site) => site == 'A' ? PlantA : PlantB;
        /// <summary>Meio de cada bomb (campos fixos, leitura do IGL) e a saída do mercado/da pista pro B (entrada do CT).</summary>
        public static readonly V2 CentroA = C(34.5f, 8, 48.5f, 19), CentroB = C(6, 11, 11, 10.5f), B_MercadoSaida = C(9.5f, 5.5f, 15, 6.5f);

        // ---- CT: posições de segurar por setup
        public static readonly Hold CT_A_Plataforma = new Hold("plataforma", C(36, 2.6f, 51.5f, 13), A_Rampa, A_Sacada);
        public static readonly Hold CT_A_Canto = new Hold("canto do A", C(32, 5.5f, 44, 14), A_Conector, A_Sacada, A_Rampa);
        public static readonly Hold CT_Janelao = new Hold("janelão", C(18.5f, 6.7f, 37, 22), Meio_Sul, TopoEntrada);
        public static readonly Hold CT_Praca = new Hold("praça", C(22, 7.5f, 39.5f, 9), Meio_Norte, C(24, 12.5f, 36, 16));
        // ---- CT no meio: quem tem o meio escolhe onde lutar e corta a rotação do TR (o conector de um lado, o rato do outro)
        public static readonly Hold CT_MeioCaixa = new Hold("caixa do meio", C(20.2f, 8.9f, 36, 26), Meio_Sul, C(18.5f, 15, 30, 31));
        public static readonly Hold CT_Conector = new Hold("conector do CT", C(25, 12.5f, 41, 18), C(21.5f, 12.5f, 37, 18), Meio_Norte);
        public static readonly Hold CT_Rato = new Hold("caminho de rato", C(15.4f, 11, 26, 9), C(15.4f, 14.5f, 25, 12), C(12, 12.5f, 21, 6));
        public static readonly Hold CT_B_Banca = new Hold("banca", C(3, 7, 4.5f, 7), B_Tapete, B_Curta);
        public static readonly Hold CT_B_Pilar = new Hold("pilar do B", C(7.5f, 9, 12, 11), B_Curta, B_Tapete);
        public static readonly Hold CT_B_Viela = new Hold("viela", C(6, 2, 12, 4), B_Viela, C(6, 8, 10, 8));
        public static readonly Hold CT_A_RuaCT = new Hold("rua do CT", C(27, 2, 45, 6), A_Descida, A_Conector);
        // âncora de ângulo perto (o rifle ganha de perto; a plataforma é linha longa, boa pra AWP do TR na rampa)
        public static readonly Hold CT_A_Caixa = new Hold("quina da rampa", C(38.3f, 10.8f, 52.5f, 24), A_Rampa, A_Sacada);   // fora da linha da rampa: só aparece pra quem já saiu dela
        public static readonly Hold CT_B_Caixa = new Hold("canto do tapete", C(2.8f, 12.8f, 3, 11), B_Tapete, B_Curta);
        public static Hold[] AncoraA => new[] { CT_A_Plataforma, CT_A_Caixa };
        public static Hold[] AncoraB => new[] { CT_B_Banca, CT_B_Caixa };

        // ---- TR: posições de controle/espera
        public static readonly Hold T_TopoMeio = new Hold("topo do meio", C(22.8f, 26.5f, 24.5f, 40), TopoEntrada, C(19, 24, 25, 33));
        public static readonly Hold T_MeioSul = new Hold("meio", C(16.2f, 20.5f, 20, 33), C(18.5f, 16, 27, 31), C(19.5f, 22, 22, 36));   // na boca da curta: toma o meio sem ficar parado na linha do janelão
        public static readonly Hold T_RampaBaixo = new Hold("rampa de baixo", C(32, 22, 36.5f, 30), C(34, 17, 40.5f, 25.5f), A_Rampa);
        public static readonly Hold T_RampaDobra = new Hold("dobra da rampa", C(33, 17, 40.5f, 25.5f), A_Rampa, C(36, 13, 43, 25.5f));
        public static readonly Hold T_PalacioSala = new Hold("sala do palácio", C(27, 16.5f, 44.5f, 33), A_Sacada, C(28.5f, 13.5f, 47, 29));
        public static readonly Hold T_CasaraoDobra = new Hold("dobra do casarão", C(6, 19.5f, 14.5f, 24), B_Tapete, C(4.5f, 17.5f, 16, 18));
        public static readonly Hold T_CurtaCima = new Hold("curta de cima", C(12.5f, 16.5f, 18.5f, 16.5f), B_Curta, C(12.5f, 13, 18, 12));
        public static readonly Hold T_Conector = new Hold("conector", C(23, 12.5f, 35.5f, 17.5f), A_Conector, Meio_Norte);
        public static readonly Hold T_RuaB = new Hold("rua do B", C(13.5f, 30.5f, 11, 38), C(10.5f, 30.5f, 11, 32), C(6, 26, 10, 31));

        // ---- CT: jogadas agressivas cedo (pega info/abate antes do TR chegar e volta pro setup)
        public static readonly Hold CT_Agr_Rampa = new Hold("peek agressivo na rampa", C(36, 12, 40, 25), C(33.5f, 16.5f, 36.5f, 30), T_RampaBaixo.pos);
        public static readonly Hold CT_Agr_Meio = new Hold("agressão no meio", C(18.5f, 11, 33, 31), Meio_Sul, TopoEntrada);
        public static readonly Hold CT_Agr_Curta = new Hold("peek agressivo na curta", C(9.5f, 13, 17, 17), T_CurtaCima.pos, C(12.5f, 19, 15, 23));
        public static readonly Hold CT_Agr_Tapete = new Hold("agressão no tapete", C(6, 15, 15, 21), T_CasaraoDobra.pos, C(4.5f, 17.5f, 13, 28));

        // ---- pós-plant (TR protege o bomb de longe, olhando as entradas do CT)
        public static Hold[] PostPlant(char site) => site == 'A'
            ? new[] { new Hold("pós-plant rampa", C(36, 14, 43.5f, 27), PlantA, A_Descida), new Hold("pós-plant sacada", C(30, 12, 47.5f, 29.5f), PlantA, A_RuaCT),
                      new Hold("pós-plant conector", C(27, 9.5f, 41, 18.5f), PlantA, A_Descida), new Hold("pós-plant caixa A", C(37, 9, 52, 25), A_Descida, CT_A_Plataforma.pos) }
            : new[] { new Hold("pós-plant tapete", C(6, 16, 18, 12.5f), PlantB, B_Viela), new Hold("pós-plant curta", C(11, 12.5f, 17, 9.5f), PlantB, B_Mercado),
                      new Hold("pós-plant casarão", C(5, 18, 16.5f, 17), PlantB, B_Viela), new Hold("pós-plant caixa B", C(3, 13, 3, 11), B_Viela, B_Mercado) };

        // ---- retomada do CT: onde juntar antes de entrar
        // entradas diferentes (rua do CT, plataforma, conector pela praça; viela, mercado): retomada entra por mais de um
        // lado ao mesmo tempo, não um bolo numa porta só
        /* bx: PORTAS DO BOMB pelo mapa (vale pra qualquer mapa): de cada área, o caminho até o bomb entra por uma área vizinha —
           essas são as portas. O ponto de juntar de cada porta fica ~100 antes da entrada, no caminho. Portas por onde só 1
           caminho entra (canto/escada do próprio bomb) ficam de fora. Cacheado por bomb. */
        static readonly Dictionary<char, (string porta, V2 junta, V2 entra)[]> portas = new Dictionary<char, (string, V2, V2)[]>();
        public static (string porta, V2 junta, V2 entra)[] PortasDoBomb(char site)
        {
            lock (portas)
            {
                if (portas.TryGetValue(site, out var pr)) return pr;
                string nome = site.ToString();
                var rs = MapData.SITES[nome]; V2 alvo = NavGrid.Snap(new V2((rs.x0 + rs.x1) * 0.5f, (rs.y0 + rs.y1) * 0.5f));
                var achou = new Dictionary<string, (int n, V2 junta, V2 entra)>();
                foreach (var ar in MapData.areas)
                {
                    if (ar.name == nome) continue;
                    V2 o = NavGrid.Snap(new V2((ar.box.x0 + ar.box.x1) * 0.5f, (ar.box.y0 + ar.box.y1) * 0.5f));
                    if (MapData.AreaAt(o.x, o.y) == nome) continue;
                    var path = NavGrid.Path(o, alvo); if (path == null || path.Count == 0) continue;
                    var pts = new List<V2> { o }; V2 ant = o;
                    foreach (var w in path) { float dl = (w.p - ant).Len; int k = System.Math.Max(1, (int)(dl / 4)); for (int i = 1; i <= k; i++) pts.Add(ant + (w.p - ant) * (i / (float)k)); ant = w.p; }
                    int ent = -1; for (int i = 0; i < pts.Count; i++) if (MapData.AreaAt(pts[i].x, pts[i].y) == nome) { ent = i; break; }
                    if (ent <= 0) continue;
                    string porta = null; for (int i = ent - 1; i >= 0 && porta == null; i--) porta = MapData.AreaAt(pts[i].x, pts[i].y);
                    if (porta == null) continue;
                    float acc = 0; int j = ent - 1; while (j > 0 && acc < 100) { acc += (pts[j] - pts[j - 1]).Len; j--; }
                    V2 junta = NavGrid.Snap(pts[j]); V2 entra = NavGrid.Snap(pts[System.Math.Min(ent + 6, pts.Count - 1)]);   /* um pouco pra dentro do bomb, pela porta */
                    achou[porta] = achou.TryGetValue(porta, out var v) ? (v.n + 1, v.junta, v.entra) : (1, junta, entra);
                }
                pr = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(achou, kv => kv.Value.n >= 2), kv => (kv.Key, kv.Value.junta, kv.Value.entra)), x => x.Key, System.StringComparer.Ordinal));
                if (pr.Length == 0) { var rsg = RetakeStage(site); pr = new (string, V2, V2)[rsg.Length]; for (int i = 0; i < rsg.Length; i++) pr[i] = ("estágio " + i, rsg[i], rsg[i]); }
                portas[site] = pr; return pr;
            }
        }
        public static V2[] RetakeStage(char site) => site == 'A' ? new[] { C(28, 2, 45, 6), C(36, 2.6f, 40, 9), C(22, 7.5f, 39.5f, 19) } : new[] { C(6, 1.5f, 12, 4), C(11, 4, 18, 6.5f) };

        // ---- granadas (de onde joga, onde cai): o TR joga do lado dele, o CT do lado dele
        public class Lineup { public string name, kind; public V2 from, to; public Lineup(string n, string k, V2 f, V2 t) { name = n; kind = k; from = f; to = t; } }
        public static readonly Lineup SmokeJanelao = new Lineup("smoke do janelão", "smoke", C(22.8f, 27.2f, 25, 37), C(18.75f, 14.5f, 36, 24.5f));   // jogada de trás da quina (fora da mira do janelão)
        public static readonly Lineup SmokeRuaCT = new Lineup("smoke da descida do CT", "smoke", C(34, 17, 42.5f, 27), C(30.5f, 4.25f, 51.5f, 10.5f));   /* Brasa v7: alvo na plataforma nova da descida do CT */
        public static readonly Lineup SmokePlataforma = new Lineup("smoke da plataforma", "smoke", C(36, 15.5f, 44, 30), C(35.5f, 3.5f, 51, 14));
        public static readonly Lineup MolotovCantoA = new Lineup("molotov no canto do A", "molotov", C(36, 15, 42.5f, 26), C(32.5f, 6, 44, 14));
        public static readonly Lineup FlashA = new Lineup("flash por cima do A", "flash", C(36, 14.5f, 42.5f, 26), C(35, 8, 48, 18));
        public static readonly Lineup SmokeViela = new Lineup("smoke da viela", "smoke", C(5, 18.5f, 16.5f, 17), C(4.25f, 4.5f, 10, 6));
        public static readonly Lineup SmokeMercado = new Lineup("smoke da descida do mercado", "smoke", C(12.5f, 15, 18, 15), C(8, 6.25f, 15, 6.5f));
        public static readonly Lineup MolotovBanca = new Lineup("molotov na banca", "molotov", C(5, 18, 17, 13), C(3, 7, 4.5f, 7));
        public static readonly Lineup FlashB = new Lineup("flash por cima do B", "flash", C(12.5f, 14.5f, 18, 14), C(7, 10, 11, 10));
        // CT
        public static readonly Lineup CT_MolotovRampa = new Lineup("molotov na rampa", "molotov", C(33, 6, 46, 14), C(35.5f, 14.5f, 42, 25.5f));
        public static readonly Lineup CT_MolotovTapete = new Lineup("molotov no tapete", "molotov", C(5, 8, 12, 8), C(5.25f, 17, 18, 15));
        public static readonly Lineup CT_SmokeMeio = new Lineup("smoke no topo do meio", "smoke", C(21.5f, 7, 37, 20), C(18.5f, 19.25f, 28, 31));
        // ---- tomada de posição (TR): quem vai tomar o lugar sabe a granada que limpa o ângulo típico do CT dali
        public static readonly Lineup MolotovQuinaRampa = new Lineup("molotov na quina da rampa", "molotov", C(33, 17, 36.5f, 30), C(37.3f, 10.8f, 52.5f, 24));
        public static readonly Lineup FlashSacada = new Lineup("flash por cima da sacada", "flash", C(27, 16.5f, 44.5f, 34), C(30, 11.75f, 47.5f, 28));
        public static readonly Lineup MolotovCantoTapete = new Lineup("molotov no canto do tapete", "molotov", C(6, 19.5f, 14.5f, 24), C(2.8f, 12.8f, 3, 11));
        public static readonly Lineup MolotovPilar = new Lineup("molotov no pilar do B", "molotov", C(12.5f, 16.5f, 18.5f, 16.5f), C(7, 8.75f, 12, 11));
        public static readonly Lineup FlashMeio = new Lineup("flash do meio", "flash", C(22, 25, 24.5f, 37), C(19, 16, 30, 31));
        public static readonly Lineup FlashConector = new Lineup("flash do conector", "flash", C(23, 12.5f, 35.5f, 17.5f), C(25.5f, 10.5f, 41, 18));
        // ---- retomada (CT): smoke corta o reforço do TR, flash por cima do bomb na entrada
        public static readonly Lineup CT_SmokeRampaRetake = new Lineup("smoke da rampa (retake)", "smoke", C(30, 4, 45, 7), C(35.5f, 13, 42, 25));
        public static readonly Lineup CT_FlashARetake = new Lineup("flash por cima do A (retake)", "flash", C(28, 2, 45, 6), C(33.5f, 7.5f, 48.5f, 19));
        public static readonly Lineup CT_SmokeTapeteRetake = new Lineup("smoke do tapete (retake)", "smoke", C(6, 1.5f, 12, 4), C(5.5f, 15.5f, 17, 12));
        public static readonly Lineup CT_FlashBRetake = new Lineup("flash por cima do B (retake)", "flash", C(11, 4, 12, 4), C(7, 10, 10, 11));
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
        public static readonly (string nome, V2 p)[] ENTRADAS_A = { ("rampa", C(35.5f, 14.5f, 42.5f, 25.5f)), ("sacada", C(30, 12.3f, 47.5f, 29.5f)) };
        public static readonly (string nome, V2 p)[] ENTRADAS_B = { ("tapete", C(5.25f, 17, 18, 13)), ("curta", C(10.5f, 12.8f, 16, 9.5f)) };

        /// <summary>Pra que serve cada smoke: de onde o inimigo olha e os pontos que ela tem que esconder (validador).</summary>
        public static readonly Dictionary<Lineup, (V2 de, V2[] para)[]> CORTA = BR ? new Dictionary<Lineup, (V2, V2[])[]>() : new Dictionary<Lineup, (V2, V2[])[]>
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
        public static readonly HashSet<Lineup> TREINADAS = BR ? new HashSet<Lineup>() : new HashSet<Lineup>(   // Brasa: ninguém treinou lineup ainda (joga no olho)
            System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(typeof(Places).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                f => f.FieldType == typeof(Lineup)), f => (Lineup)f.GetValue(null)));

        /// <summary>
        /// bx: com o CLIP da granada (bxClipNadeLigado) as casas têm 2-3 andares pra granada, e 8 das 22 lineups treinadas
        /// (desenhadas com a casa de 1 andar) batiam no prédio. A lineup é dado do mapa: o ponto de onde se joga muda pro mais
        /// perto (que o caminho alcança a partir do antigo) de onde ela volta a cair. Uma vez por processo.
        /// </summary>
        static bool ajustouClip; static readonly object travaClip = new object();
        public static readonly List<string> AjusteClipLog = new List<string>();
        public static void AjustaClip()
        {
            if (ajustouClip || !Arremesso.ClipNade) return;
            lock (travaClip)
            {
                if (ajustouClip) return;
                foreach (var l in TREINADAS)
                {
                    float tol = l.kind == "flash" ? 20 : 12;
                    float e0 = Arremesso.Resolve(l.from, CSM.Map.NavGrid.ZAt(l.from), l.to, l.kind).erro;
                    if (e0 <= tol) continue;
                    V2? novo = null; float eN = e0;
                    for (float r = 12; r <= 240 && novo == null; r += 12)
                        for (int k = 0; k < 24 && novo == null; k++)
                        {
                            V2 q = CSM.Map.NavGrid.Snap(l.from + V2.Ang(k * 0.2618f) * r);
                            if (!CSM.Map.NavGrid.Movable(q) || (q - l.to).Len < 100 || CSM.Map.NavGrid.PathLenAte(l.from, q, 320) >= 320) continue;
                            float e = Arremesso.Resolve(q, CSM.Map.NavGrid.ZAt(q), l.to, l.kind).erro;
                            if (e <= tol) { novo = q; eN = e; }
                        }
                    AjusteClipLog.Add($"{l.name}: erro {e0:0} → " + (novo is V2 nq ? $"{eN:0} (ponto andou {(nq - l.from).Len:0})" : "sem ponto perto — fica no olho"));
                    if (novo is V2 n2) l.from = n2;
                }
                ajustouClip = true;
            }
        }

        // ---------------- banco de lineups aprendidas (repertório que cresce jogando)
        /// <summary>Lineups que alguém acertou no olho e guardou (lugar + mira). Cada jogador sabe as que ELE aprendeu.</summary>
        static readonly List<Lineup> bancoG = new List<Lineup>();
        static readonly HashSet<Lineup> bancoSetG = new HashSet<Lineup>();
        static readonly Dictionary<string, HashSet<Lineup>> sabeG = new Dictionary<string, HashSet<Lineup>>();
        // headless determinístico (NavGrid.PorRound): o banco era um só pra todos os rounds paralelos (pelo NOME do jogador),
        // então o que cada round sabia dependia de quais terminaram antes — no modo PorRound é da thread e zera a cada round
        [System.ThreadStatic] static List<Lineup> bancoT; [System.ThreadStatic] static HashSet<Lineup> bancoSetT; [System.ThreadStatic] static Dictionary<string, HashSet<Lineup>> sabeT;
        public static List<Lineup> BANCO => CSM.Map.NavGrid.PorRound ? (bancoT ??= new List<Lineup>()) : bancoG;
        static HashSet<Lineup> bancoSet => CSM.Map.NavGrid.PorRound ? (bancoSetT ??= new HashSet<Lineup>()) : bancoSetG;
        static Dictionary<string, HashSet<Lineup>> sabe => CSM.Map.NavGrid.PorRound ? (sabeT ??= new Dictionary<string, HashSet<Lineup>>()) : sabeG;
        public static void NovoRound() { if (!CSM.Map.NavGrid.PorRound) return; bancoT?.Clear(); bancoSetT?.Clear(); sabeT?.Clear(); }
        public static void ZeraDinamico() { lock (bancoG) { bancoG.Clear(); bancoSetG.Clear(); sabeG.Clear(); } bancoT?.Clear(); bancoSetT?.Clear(); sabeT?.Clear(); }
        public static bool Treinada(Lineup l) => TREINADAS.Contains(l) || bancoSet.Contains(l);
        public static bool DoBanco(Lineup l) => bancoSet.Contains(l);
        public static bool Aprendeu(string quem, Lineup l) => sabe.TryGetValue(quem, out var h) && h.Contains(l);
        /// <summary>Acertou no olho: vira lineup dele (se já existe uma igual no banco, ele passa a saber aquela).</summary>
        public static void Aprende(string quem, string kind, V2 de, V2 alvo)
        {
            lock (BANCO)
            {
                Lineup l = null;
                foreach (var x in BANCO) if (x.kind == kind && (x.from - de).Len < 12 && (x.to - alvo).Len < 16) { l = x; break; }
                if (l == null)
                {
                    if (BANCO.Count >= 600) return;
                    l = new Lineup($"{kind} aprendida em {Region(alvo)} (de {Region(de)})", kind, de, alvo);
                    BANCO.Add(l); bancoSet.Add(l);
                }
                if (!sabe.TryGetValue(quem, out var h)) sabe[quem] = h = new HashSet<Lineup>();
                h.Add(l);
            }
        }
        /// <summary>Uma lineup que ESSE jogador sabe pra cair perto do alvo, saindo de perto de mim.</summary>
        public static Lineup DoBanco(string quem, string kind, V2 alvo, V2 eu, float raioAlvo, float raioDe)
        {
            if (!sabe.TryGetValue(quem, out var h)) return null;
            Lineup best = null; float bd = float.MaxValue;
            lock (BANCO)
                foreach (var l in h)
                {
                    if (l.kind != kind || (l.to - alvo).Len > raioAlvo) continue;
                    float d = (l.from - eu).Len; if (d > raioDe) continue;
                    float sc = d + 2 * (l.to - alvo).Len; if (sc < bd) { bd = sc; best = l; }
                }
            return best;
        }

        // ---- português do rádio: o artigo certo antes do lugar ("na curta", "no bomb B", "nos fundos do janelão")
        static readonly HashSet<string> FEM = new HashSet<string> { "banca", "base", "carroça", "curta", "descida", "dobra", "entrada", "escada", "passagem", "plataforma", "praça", "rampa", "rua", "sacada", "saída", "sala", "subida", "viela" };
        static string Genero(string lugar)
        {
            string w = lugar.Split(' ')[0].ToLowerInvariant();
            return w == "fundos" ? "mp" : FEM.Contains(w) ? "f" : "m";
        }
        /// <summary>"na curta" / "no meio" / "nos fundos do janelão"; lugar desconhecido = "aqui".</summary>
        public static string Na(string lugar) => string.IsNullOrEmpty(lugar) || lugar == "?" ? "aqui" : Genero(lugar) switch { "f" => "na ", "mp" => "nos ", _ => "no " } + lugar;
        /// <summary>"pra curta" / "pro meio" / "pros fundos do janelão".</summary>
        public static string Pro(string lugar) => string.IsNullOrEmpty(lugar) || lugar == "?" ? "pra lá" : Genero(lugar) switch { "f" => "pra ", "mp" => "pros ", _ => "pro " } + lugar;
        /// <summary>"a curta" / "o meio" (objeto: "segura a curta").</summary>
        public static string ArtigoDe(string lugar) => string.IsNullOrEmpty(lugar) || lugar == "?" ? "isso" : Genero(lugar) switch { "f" => "a ", "mp" => "os ", _ => "o " } + lugar;

        /// <summary>
        /// Texto do rádio pra quem assiste: tira o número interno de crença ("devem ter 2.3", "(0.4)") e põe palavra, e
        /// corrige "em/pro + lugar" pro artigo certo. O log interno continua com os números (é ferramenta nossa).
        /// </summary>
        public static string Radio(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            static string Quanto(float n) => n >= 2.5f ? "vários" : n >= 1.5f ? "uns dois" : n >= 0.7f ? "um" : n >= 0.3f ? "talvez um" : "ninguém";
            static float F(string x) => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"devem ter (\d+[.,]\d)( CT)?", mm =>
            {
                float n = F(mm.Groups[1].Value.Replace(',', '.'));
                return n < 0.3f ? "não deve ter ninguém" : n < 0.7f ? "pode ter um" : n < 1.5f ? "deve ter um" : "devem ter " + Quanto(n);
            });
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\((\d+[.,]\d)\)", "");                                   // "vazio (0.4)"
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(\d+[.,]\d)\b", mm => Quanto(F(mm.Value.Replace(',', '.'))));   // "e 0.4 no B"
            s = System.Text.RegularExpressions.Regex.Replace(s, @"~(\d+)\s?s\b", "lá pelos $1 s");
            // nome interno do plano do banco ("TR full #252 (rush)") vira só o tipo ("rush"); "bomb" no sentido de C4 vira C4
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(TR|CT) \w+ #\d+ \(([^)]+)\)", "$2");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(TR|CT) \w+ #\d+", "o de sempre");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"plano: ([^—]+?) — bomb ([AB]), entrar lá pelos (\d+) s", "plano: $1 no $2, entra lá pelos $3 s");
            s = s.Replace("setup: setup", "setup padrão").Replace("plano: default", "plano: default").Replace("pega o bomb", "pega a C4").Replace("bomb caiu", "C4 caiu").Replace("bomb atrás", "C4 atrás");
            var lugares = new List<string>(CALLOUT.Values); lugares.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (var l in lugares)
            {
                if (s.IndexOf(l, System.StringComparison.Ordinal) < 0) continue;
                s = s.Replace("em " + l, Na(l)).Replace("pro " + l, Pro(l)).Replace("no " + l, Na(l));
            }
            return s;
        }

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
        public static string ZonaDaArea(string a) => a != null && (BR ? ZONA_BRASA : ZONA).TryGetValue(a, out var z) ? z : "Mid";   /* bx: medição de tática */
        public static string Zona(V2 p)
        {
            var a = AreaAt(p.x, p.y);
            return a != null && (BR ? ZONA_BRASA : ZONA).TryGetValue(a, out var z) ? z : "Mid";
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

        static readonly Dictionary<string, string> ZONA_BRASA = new Dictionary<string, string>
        {
            ["TR"] = "Spawn", ["CT"] = "Spawn",
            ["A"] = "A", ["Apê CT"] = "A", ["Biblioteca"] = "A", ["Caldeira"] = "A", ["Cemitério"] = "A", ["Curta"] = "A", ["Longo"] = "A",
            ["Ponte"] = "A", ["Poço"] = "A", ["Tapete"] = "A", ["Varanda"] = "A", ["Rua do tapete"] = "A",
            ["Alto do meio"] = "Mid", ["Arco"] = "Mid", ["Caverna"] = "Mid", ["Janela"] = "Mid", ["MF"] = "Mid", ["Meio"] = "Mid", ["Meio de baixo"] = "Mid",
            ["B"] = "B", ["Alto do banana"] = "B", ["Banana"] = "B", ["Caixão"] = "B", ["Construção"] = "B", ["Corredor do banana"] = "B",
            ["Pista"] = "B", ["Pé do banana"] = "B", ["Rampa"] = "B",
        };

        /// <summary>Qual bomb fica mais perto (A/B) ou meio.</summary>
        public static char Side(V2 p)
        {
            float dA = (p - SITES["A"].Center).Len, dB = (p - SITES["B"].Center).Len;
            if (BR) { if (dA < 230) return 'A'; if (dB < 230) return 'B'; string zb = Zona(p); return zb == "A" ? 'A' : zb == "B" ? 'B' : 'M'; }
            if (p.x > 23 * Q || dA < 230) return 'A';
            if (p.x < 13 * Q || dB < 230) return 'B';
            return 'M';
        }
    }
}
