using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Leitura geométrica do mapa pro gerador de táticas (PLANO_SIMULACAO.md, seções 4.1 e 5.1). Calculada UMA vez por mapa
    /// (o mapa é fixo no processo: MapData.MAPA) e guardada:
    ///  - as ENTRADAS de cada bomb (Places.PortasDoBomb) com o caminho de chegada do TR por cada uma (pontos, regiões, comprimento);
    ///  - o tempo correndo (MapCheck.RUN) de cada spawn até entradas, plant e pontos do meio, e a rotação do CT entre os bombs;
    ///  - as posições de defesa do CT (os Hold "CT_" do Places) e a LINHA de cada uma sobre cada entrada: vê a boca?, distância,
    ///    que trecho da chegada fica exposto a ela;
    ///  - por entrada, as linhas ordenadas por perigo: é daí que sai "que linha a smoke corta, que canto a molotov limpa".
    /// Só lê (NavGrid, MapData, Places). Na Brasa o que faltar sai vazio (nunca estoura).
    /// </summary>
    public static class AnaliseMapa
    {
        /// <summary>Passo das amostras do caminho no teste de visão, e quanto da chegada (antes da boca) conta na fração exposta.</summary>
        public const float PASSO = 20f, CHEGADA = 400f;
        const float FINO = 4f;   // passo fino: achar a boca e a sequência de regiões

        public class Defesa
        {
            public string nome, campo;   // nome do Hold ("plataforma") e do campo no Places ("CT_A_Plataforma")
            public Places.Hold hold;
            public V2 pos; public float z;
            public char site;            // 'A', 'B' ou 'M' (meio), pelo Places.Side
            public bool agressiva;       // CT_Agr_*: posição de agressão cedo, não de setup
            public bool ancora;          // está em AncoraA/AncoraB
            public string regiao;
        }

        public class Entrada
        {
            public char site;
            public string porta, nome;   // área interna (MapData) e o nome de rádio (callout)
            public V2 junta, boca, entra;   // ponto de juntar (~100 antes), a boca (primeiro ponto dentro do bomb) e um pouco pra dentro
            public bool ladoTR;          // o TR chega nela por fora do bomb sem cruzar a base CT (falso = porta do lado do CT)
            public bool porFora;         // o caminho guardado contorna o bomb (falso = só dá pra chegar nela passando por dentro dele)
            public float comprimento;    // do spawn TR até a boca, por esta entrada e por fora do bomb (custo do caminho, em unidades)
            public float tempoTR, tempoCT;   // s correndo até a boca (TR por esta entrada; CT pelo caminho mais curto)
            public List<V2> caminho = new List<V2>();       // chegada do TR, um ponto a cada PASSO, do spawn até `entra`
            public List<float> falta = new List<float>();   // pra cada ponto do caminho: quanto falta até a boca (negativo = já dentro)
            public string[] regioes = new string[0], zonas = new string[0];   // sequência de regiões (rádio) e de zonas (A/Mid/B/Spawn)
            public List<Linha> linhas = new List<Linha>();  // quem vê esta entrada, do mais perigoso pro menos
        }

        /// <summary>Linha de tiro de uma posição de defesa sobre uma entrada.</summary>
        public class Linha
        {
            public Defesa defesa; public Entrada entrada;
            public bool veBoca;          // vê a boca da entrada
            public float dist;           // da posição até a boca (reta)
            public float fracao;         // fração da CHEGADA (últimos 400 antes da boca, até entrar) exposta a ela
            public float expostoDe, expostoAte;   // o trecho exposto: começa a `expostoDe` da boca e vai até `expostoAte` (negativo = dentro do bomb)
            public float expostoTotal;   // unidades do caminho inteiro (desde o spawn) à vista dela
            public V2 alvo;              // onde a linha chega: a boca se vê; senão o ponto exposto mais perto da boca
            public V2 meio;              // ponto médio da linha (posição -> alvo): onde uma smoke corta
            public float perigo;         // fracao*60 + 25 se vê a boca + até 15 por estar perto (0 a 100)
        }

        class Dados
        {
            public V2 spawnTR, spawnCT; public bool ok; public string erro;
            public List<Defesa> defesas = new List<Defesa>();
            public Dictionary<char, List<Entrada>> entradas = new Dictionary<char, List<Entrada>> { ['A'] = new List<Entrada>(), ['B'] = new List<Entrada>() };
            public List<(string nome, V2 p)> meio = new List<(string, V2)>();
            public float rotAB = -1, rotBA = -1;
            public float[] campoTR, campoCT;   // custo do caminho do spawn até cada célula (Dijkstra próprio, exato)
            public Dictionary<char, (float[] dist, int[] pai)> fora = new Dictionary<char, (float[], int[])>();   // do spawn TR sem pisar no bomb
        }

        static Dados dados; static readonly object trava = new object();
        static Dados D { get { if (dados == null) lock (trava) { if (dados == null) dados = Calcula(); } return dados; } }

        // ---------------- consulta
        public static V2 SpawnTR => D.spawnTR;
        public static V2 SpawnCT => D.spawnCT;
        /// <summary>Entradas do bomb ('A' ou 'B'). Lista vazia se o mapa não tem o dado.</summary>
        public static IReadOnlyList<Entrada> Entradas(char site) => D.entradas.TryGetValue(site, out var l) ? l : new List<Entrada>();
        /// <summary>Entrada pelo nome da área ("Rampa") ou de rádio; null se não existe.</summary>
        public static Entrada EntradaDe(char site, string nome)
        {
            foreach (var e in Entradas(site)) if (e.porta == nome || e.nome == nome) return e;
            return null;
        }
        /// <summary>Todas as posições de defesa do CT (setup, meio e agressão).</summary>
        public static IReadOnlyList<Defesa> Defesas() => D.defesas;
        /// <summary>Posições de defesa de um lado ('A', 'B', 'M').</summary>
        public static List<Defesa> Defesas(char site) { var r = new List<Defesa>(); foreach (var d in D.defesas) if (d.site == site) r.Add(d); return r; }
        /// <summary>Quem vê esta entrada (a boca ou um trecho da chegada), do mais perigoso pro menos.</summary>
        public static IReadOnlyList<Linha> LinhasSobre(Entrada e) => e == null ? new List<Linha>() : e.linhas;
        /// <summary>O que uma posição de defesa enxerga: as linhas dela sobre as entradas (todas, dos dois bombs).</summary>
        public static List<Linha> LinhasDe(Defesa d)
        {
            var r = new List<Linha>();
            foreach (var kv in D.entradas) foreach (var e in kv.Value) foreach (var l in e.linhas) if (l.defesa == d) r.Add(l);
            return r;
        }
        /// <summary>Pontos principais do meio (nome, ponto).</summary>
        public static IReadOnlyList<(string nome, V2 p)> PontosDoMeio() => D.meio;
        /// <summary>Segundos correndo do spawn TR até o ponto (caminho mais curto). -1 = sem caminho.</summary>
        public static float TempoTR(V2 ponto) => NoCampo(D.campoTR, ponto);
        /// <summary>Segundos correndo do spawn CT até o ponto (caminho mais curto). -1 = sem caminho.</summary>
        public static float TempoCT(V2 ponto) => NoCampo(D.campoCT, ponto);
        static float NoCampo(float[] campo, V2 p)
        {
            if (campo == null) return -1;
            var (x, y) = NavGrid.NearestMovable(p); int i = NavGrid.IndexOf(x, y);
            return i < 0 || float.IsInfinity(campo[i]) ? -1 : campo[i] / MapCheck.RUN;
        }
        /// <summary>Rotação do CT entre os bombs (plant a plant), a pior das duas idas. -1 = sem caminho.</summary>
        public static float RotacaoCT() => Math.Max(D.rotAB, D.rotBA);
        /// <summary>Rotação do CT saindo de um bomb pro outro (`de` = 'A' dá A -> B).</summary>
        public static float RotacaoCT(char de) => de == 'A' ? D.rotAB : D.rotBA;
        /// <summary>Segundos correndo entre dois pontos quaisquer (NavGrid.PathLen: no meio de uma partida pode vir estimado). -1 = sem caminho.</summary>
        public static float Tempo(V2 de, V2 ate)
        {
            float l = NavGrid.PathLen(NavGrid.Snap(de), NavGrid.Snap(ate));
            return float.IsInfinity(l) || float.IsNaN(l) || l > 1e8f ? -1 : l / MapCheck.RUN;
        }
        /// <summary>Linha de visão de olho a olho entre dois pontos do chão (altura do piso de cada um + olho).</summary>
        public static bool Ve(V2 a, V2 b) => MapData.LosWalk(a, NavGrid.ZAt(a), b, NavGrid.ZAt(b), NavGrid.EYE);

        // ---------------- cálculo
        static V2 Media(V2[] ps) { V2 s = V2.Zero; foreach (var p in ps) s = s + p; return ps.Length > 0 ? s / ps.Length : s; }

        static Dados Calcula()
        {
            var d = new Dados();
            try
            {
                d.spawnTR = NavGrid.Snap(Media(MapData.SPAWN_T)); d.spawnCT = NavGrid.Snap(Media(MapData.SPAWN_CT));
                d.meio.Add(("meio sul", NavGrid.Snap(Places.Meio_Sul))); d.meio.Add(("meio norte", NavGrid.Snap(Places.Meio_Norte)));
                d.meio.Add(("entrada do topo", NavGrid.Snap(Places.TopoEntrada))); d.meio.Add(("janelão", NavGrid.Snap(Places.Janelao)));
                d.campoTR = CampoPorFora(d.spawnTR, null).dist; d.campoCT = CampoPorFora(d.spawnCT, null).dist;
                d.rotAB = NoCampo(CampoPorFora(NavGrid.Snap(Places.Plant('A')), null).dist, Places.Plant('B'));
                d.rotBA = NoCampo(CampoPorFora(NavGrid.Snap(Places.Plant('B')), null).dist, Places.Plant('A'));
                LeDefesas(d);
                foreach (char site in new[] { 'A', 'B' })
                {
                    try
                    {
                        d.fora[site] = CampoPorFora(d.spawnTR, site.ToString());
                        foreach (var (porta, junta, entra) in Places.PortasDoBomb(site))
                        {
                            var e = MontaEntrada(d, site, porta, junta, entra);
                            if (e != null) d.entradas[site].Add(e);
                        }
                    }
                    catch (Exception ex) { d.erro = (d.erro ?? "") + $"bomb {site}: {ex.Message}; "; }
                }
                foreach (var kv in d.entradas) foreach (var e in kv.Value) MontaLinhas(d, e);
                d.ok = true;
            }
            catch (Exception ex) { d.erro = (d.erro ?? "") + ex.Message; }
            return d;
        }

        /// <summary>Os Hold do CT declarados no Places (campos CT_*), com as âncoras marcadas.</summary>
        static void LeDefesas(Dados d)
        {
            var anc = new HashSet<Places.Hold>(); foreach (var h in Places.AncoraA) anc.Add(h); foreach (var h in Places.AncoraB) anc.Add(h);
            var visto = new HashSet<Places.Hold>();
            foreach (var f in typeof(Places).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(Places.Hold) || !f.Name.StartsWith("CT_", StringComparison.Ordinal)) continue;
                var h = (Places.Hold)f.GetValue(null); if (h == null || !visto.Add(h)) continue;
                d.defesas.Add(NovaDefesa(h, f.Name, anc.Contains(h)));
            }
            foreach (var h in anc) if (visto.Add(h)) d.defesas.Add(NovaDefesa(h, "âncora", true));   // âncora que não seja campo CT_ (hoje não há)
        }
        static Defesa NovaDefesa(Places.Hold h, string campo, bool ancora)
        {
            V2 p = NavGrid.Snap(h.pos);
            return new Defesa { nome = h.name, campo = campo, hold = h, pos = p, z = NavGrid.ZAt(p), site = Places.Side(p), agressiva = campo.StartsWith("CT_Agr_", StringComparison.Ordinal), ancora = ancora, regiao = Places.Region(p) };
        }

        /// <summary>Pontos de um caminho (a incluso), um a cada FINO.</summary>
        static List<V2> Fino(V2 a, V2 b)
        {
            var path = NavGrid.Path(a, b); if (path == null || path.Count == 0) return null;
            var pts = new List<V2> { a }; V2 ant = a;
            foreach (var w in path)
            {
                float dl = (w.p - ant).Len; int k = Math.Max(1, (int)(dl / FINO));
                for (int i = 1; i <= k; i++) pts.Add(ant + (w.p - ant) * (i / (float)k));
                ant = w.p;
            }
            return pts;
        }

        /// <summary>
        /// Dijkstra do spawn TR na malha do NavGrid (mesmas ligações e custos) SEM pisar na área do bomb: a chegada por uma
        /// entrada é o caminho até ela por fora. O menor caminho livre até a porta de trás corta por dentro do bomb, e aí a
        /// "chegada" por ela seria a de outra entrada. Com nomeSite null é o campo livre (tempos de spawn e rotação): exato e
        /// sem depender do orçamento/estimativa do PathLen.
        /// </summary>
        static (float[] dist, int[] pai) CampoPorFora(V2 de, string nomeSite)
        {
            int n = NavGrid.CellCount; var dist = new float[n]; var pai = new int[n]; var fechado = new bool[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.PositiveInfinity; pai[i] = -1;
                var (x, y) = NavGrid.CellAt(i); V2 c = NavGrid.Center(x, y);
                fechado[i] = NavGrid.soBoost[x, y] || (nomeSite != null && MapData.AreaAt(c.x, c.y) == nomeSite);
            }
            var (sx, sy) = NavGrid.NearestMovable(de); int s = NavGrid.IndexOf(sx, sy); if (s < 0) return (dist, pai);
            var h = new MinHeap(); dist[s] = 0; h.Push(s, 0);
            while (h.Count > 0)
            {
                var (u, k) = h.Pop(); if (k > dist[u]) continue;
                foreach (var (v, c, _) in NavGrid.Out(u))
                {
                    if (fechado[v]) continue;
                    float nd = k + c; if (nd >= dist[v]) continue;
                    dist[v] = nd; pai[v] = u; h.Push(v, nd);
                }
            }
            return (dist, pai);
        }
        /// <summary>Pontos (centros de célula) do spawn TR até p por fora do bomb; null se não chega.</summary>
        static List<V2> CaminhoPorFora(Dados d, char site, V2 p, out float custo)
        {
            custo = -1;
            if (!d.fora.TryGetValue(site, out var f)) return null;
            var (x, y) = NavGrid.NearestMovable(p); int i = NavGrid.IndexOf(x, y);
            if (i < 0 || float.IsInfinity(f.dist[i])) return null;
            custo = f.dist[i];
            var pts = new List<V2>();
            for (int u = i; u >= 0; u = f.pai[u]) { var (cx, cy) = NavGrid.CellAt(u); pts.Add(NavGrid.Center(cx, cy)); }
            pts.Reverse();
            return pts;
        }

        static Entrada MontaEntrada(Dados d, char site, string porta, V2 junta, V2 entra)
        {
            string nomeSite = site.ToString();
            var e = new Entrada { site = site, porta = porta, nome = MapData.CALLOUT.TryGetValue(porta, out var c) ? c : porta, junta = junta, entra = entra, boca = entra, ladoTR = true };
            // chegada do TR POR ESTA entrada: spawn -> ponto de juntar -> pra dentro (o caminho mais curto até o bomb pode ser por outra porta)
            var p1 = CaminhoPorFora(d, site, junta, out float l1); var p2 = Fino(junta, entra);
            e.porFora = p1 != null;
            if (p1 == null) { p1 = Fino(d.spawnTR, junta); l1 = NavGrid.PathLen(d.spawnTR, junta); e.ladoTR = false; }   // só por dentro do bomb
            if (p1 == null) p1 = new List<V2> { junta };
            if (p2 == null) p2 = new List<V2> { junta, entra };
            foreach (var p in p1) if (MapData.AreaAt(p.x, p.y) == "CT") { e.ladoTR = false; break; }   // contorna pela base CT: é porta do CT
            int iBoca = -1; for (int i = 0; i < p2.Count; i++) if (MapData.AreaAt(p2[i].x, p2[i].y) == nomeSite) { iBoca = i; break; }
            if (iBoca < 0) iBoca = p2.Count - 1;
            e.boca = NavGrid.Snap(p2[iBoca]);
            var todos = new List<V2>(p1); for (int i = 1; i < p2.Count; i++) todos.Add(p2[i]);
            int nBoca = p1.Count - 1 + iBoca;
            // distância andada até cada ponto e o que falta até a boca
            var s = new float[todos.Count]; for (int i = 1; i < todos.Count; i++) s[i] = s[i - 1] + (todos[i] - todos[i - 1]).Len;
            float sBoca = s[Math.Min(nBoca, todos.Count - 1)];
            float prox = -1e9f;
            for (int i = 0; i < todos.Count; i++)
            {
                bool ultimo = i == todos.Count - 1, naBoca = i == nBoca;
                if (!ultimo && !naBoca && s[i] - prox < PASSO) continue;
                prox = s[i]; e.caminho.Add(todos[i]); e.falta.Add(sBoca - s[i]);
            }
            // regiões e zonas por onde passa (sem repetir a vizinha)
            var regs = new List<string>(); var zs = new List<string>();
            foreach (var p in todos)
            {
                string a = MapData.AreaAt(p.x, p.y); if (a == null) continue;
                string r = Places.Region(p), z = Places.Zona(p);
                if (regs.Count == 0 || regs[regs.Count - 1] != r) regs.Add(r);
                if (zs.Count == 0 || zs[zs.Count - 1] != z) zs.Add(z);
            }
            e.regioes = regs.ToArray(); e.zonas = zs.ToArray();
            float l2 = NavGrid.PathLen(junta, e.boca);   // trecho curto (a junta fica ~100 antes)
            if (float.IsInfinity(l2) || float.IsNaN(l2)) { l2 = 0; for (int i = 1; i <= iBoca && i < p2.Count; i++) l2 += (p2[i] - p2[i - 1]).Len; }
            bool tem = l1 >= 0 && !float.IsInfinity(l1) && !float.IsInfinity(l2) && l1 < 1e8f && l2 < 1e8f;
            e.comprimento = tem ? l1 + l2 : -1; e.tempoTR = tem ? (l1 + l2) / MapCheck.RUN : -1;
            e.tempoCT = NoCampo(d.campoCT, e.boca);
            return e;
        }

        static void MontaLinhas(Dados d, Entrada e)
        {
            float zBoca = NavGrid.ZAt(e.boca);
            foreach (var df in d.defesas)
            {
                var l = new Linha { defesa = df, entrada = e, dist = (df.pos - e.boca).Len, expostoDe = float.NaN, expostoAte = float.NaN };
                l.veBoca = MapData.LosWalk(df.pos, df.z, e.boca, zBoca, NavGrid.EYE);
                int nJan = 0, nExp = 0; V2 perto = e.boca; float faltaPerto = float.MaxValue; bool algum = false;
                for (int i = 0; i < e.caminho.Count; i++)
                {
                    V2 p = e.caminho[i]; float f = e.falta[i];
                    bool jan = f <= CHEGADA; if (jan) nJan++;
                    if (!MapData.LosWalk(df.pos, df.z, p, NavGrid.ZAt(p), NavGrid.EYE)) continue;
                    l.expostoTotal += i > 0 ? (e.caminho[i] - e.caminho[i - 1]).Len : 0;
                    if (!jan) continue;
                    nExp++; algum = true;
                    if (float.IsNaN(l.expostoDe) || f > l.expostoDe) l.expostoDe = f;
                    if (float.IsNaN(l.expostoAte) || f < l.expostoAte) l.expostoAte = f;
                    if (Math.Abs(f) < faltaPerto) { faltaPerto = Math.Abs(f); perto = p; }
                }
                if (!l.veBoca && !algum) continue;   // não vê nada desta entrada: não é linha
                l.fracao = nJan > 0 ? nExp / (float)nJan : 0;
                if (float.IsNaN(l.expostoDe)) { l.expostoDe = 0; l.expostoAte = 0; }
                l.alvo = l.veBoca ? e.boca : perto;
                l.meio = (df.pos + l.alvo) * 0.5f;
                l.perigo = l.fracao * 60f + (l.veBoca ? 25f : 0f) + 15f * Math.Max(0f, 1f - l.dist / 1000f);
                e.linhas.Add(l);
            }
            e.linhas.Sort((a, b) => b.perigo.CompareTo(a.perigo));
        }

        // ---------------- texto
        static string N(float v, string f = "0.0") => v < 0 ? "sem caminho" : v.ToString(f, CultureInfo.InvariantCulture);
        static string P(V2 p) => "(" + p.x.ToString("0", CultureInfo.InvariantCulture) + ", " + p.y.ToString("0", CultureInfo.InvariantCulture) + ")";

        /// <summary>Tudo em texto legível (Headless -analiseMapa).</summary>
        public static string Texto()
        {
            var d = D; var sb = new StringBuilder();
            sb.AppendLine($"ANÁLISE DO MAPA: {MapData.MAPA}   (correndo a {N(MapCheck.RUN)} un/s; olho a {N(NavGrid.EYE, "0")}; amostra a cada {N(PASSO, "0")}; chegada = últimos {N(CHEGADA, "0")} antes da boca)");
            if (d.erro != null) sb.AppendLine("  AVISO: " + d.erro);
            sb.AppendLine($"spawn TR {P(d.spawnTR)} · spawn CT {P(d.spawnCT)}");
            sb.AppendLine();
            sb.AppendLine("TEMPOS (s correndo)                         TR      CT");
            void Tp(string nome, float tr, float ct) => sb.AppendLine("  " + nome.PadRight(38) + N(tr).PadLeft(6) + N(ct).PadLeft(8) + (tr >= 0 && ct >= 0 ? $"   (CT chega {N(Math.Abs(tr - ct))} s {(ct <= tr ? "antes" : "depois")})" : ""));
            foreach (char site in new[] { 'A', 'B' })
            {
                Tp($"plant {site}", TempoTR(Places.Plant(site)), TempoCT(Places.Plant(site)));
                foreach (var e in Entradas(site)) Tp($"bomb {site}, entrada {e.nome}" + (e.ladoTR ? "" : " (lado CT)"), e.tempoTR, e.tempoCT);
            }
            foreach (var (nome, p) in d.meio) Tp(nome, TempoTR(p), TempoCT(p));
            sb.AppendLine($"  rotação do CT: A -> B {N(d.rotAB)} s · B -> A {N(d.rotBA)} s");
            sb.AppendLine();
            foreach (char site in new[] { 'A', 'B' })
            {
                sb.AppendLine($"BOMB {site}: {Entradas(site).Count} entradas");
                foreach (var e in Entradas(site))
                {
                    sb.AppendLine($"  ENTRADA {e.nome} [{e.porta}]{(e.ladoTR ? "" : " — LADO CT" + (e.porFora ? " (por fora do bomb o TR só chega nela cruzando a base CT)" : " (o TR só chega nela passando por dentro do bomb)"))}: junta {P(e.junta)}, boca {P(e.boca)}, dentro {P(e.entra)}");
                    sb.AppendLine($"    chegada do TR{(e.porFora ? " por fora do bomb" : "")}: {N(e.comprimento, "0")} un, {N(e.tempoTR)} s (o mais curto até a boca, por onde for: {N(TempoTR(e.boca))} s) · zonas {string.Join(" > ", e.zonas)}");
                    sb.AppendLine($"    regiões: {string.Join(" > ", e.regioes)}");
                    if (e.linhas.Count == 0) sb.AppendLine("    !! nenhuma posição de defesa vê esta entrada");
                    foreach (var l in e.linhas)
                        sb.AppendLine($"    {l.perigo,5:0} {l.defesa.nome,-24} [{l.defesa.site}{(l.defesa.ancora ? " âncora" : "")}{(l.defesa.agressiva ? " agr" : "")}] {(l.veBoca ? "VÊ a boca" : "não vê a boca")}, dist {N(l.dist, "0")}, exposta {N(l.fracao * 100, "0")}% da chegada (de {N0(l.expostoDe)} a {N0(l.expostoAte)} da boca; {N(l.expostoTotal, "0")} un no caminho todo), meio da linha {P(l.meio)}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("POSIÇÕES DE DEFESA");
            foreach (var df in d.defesas)
            {
                var ls = LinhasDe(df); var partes = new List<string>();
                foreach (var l in ls) partes.Add($"{l.entrada.site}/{l.entrada.nome}{(l.veBoca ? "" : " (só a chegada)")}");
                sb.AppendLine($"  {df.nome,-24} [{df.site}{(df.ancora ? " âncora" : "")}{(df.agressiva ? " agr" : "")}] {P(df.pos)} z {N(df.z, "0")} em {df.regiao}: " + (partes.Count > 0 ? "vê " + string.Join(", ", partes) : "não vê nenhuma entrada"));
            }
            return sb.ToString();
        }
        static string N0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
    }
}
